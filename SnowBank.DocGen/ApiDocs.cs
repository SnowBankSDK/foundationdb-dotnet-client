using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SnowBank.DocGen
{
	// Reads a built assembly + its compiler XML doc file and produces one markdown page per public
	// type (top-level and nested). Uses MetadataLoadContext (reflection without executing the assembly).
	// This is the lean "reflection + XML" path; a production SnowBank.DocGen would use Roslyn for the
	// last mile (reference-nullable annotations, cross-assembly <inheritdoc>).
	public sealed record ApiParam(string Name, string Type, string Doc);
	public sealed record ApiMember(string Group, string Name, string Anchor, List<ApiOverload> Overloads);
	public sealed record ApiOverload(string Signature, string Summary, List<ApiParam> Params, string Returns, string Remarks, string Example);
	public sealed record ApiType(string Assembly, string Namespace, string Display, string Slug, string Kind, string Summary, string Remarks, string Example, List<string> Implements, List<ApiMember> Members);

	public static class ApiDocs
	{
		public static List<ApiType> Extract(IReadOnlyList<(string Dll, string Xml)> assemblies) => new Builder(assemblies).Run();

		// Markdig lower-cases a heading's text for its auto id and drops non-alphanumerics; member names
		// here are alphanumeric, so the same rule gives a link target that matches the rendered heading.
		public static string Anchor(string memberName) => Regex.Replace(memberName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

		public static string Slug(Type t) => (t.FullName ?? t.Name).Replace('+', '.').Replace("`", "-");

		// ---------- extraction ----------
		private sealed class Builder
		{
			private Dictionary<string, XElement> Xml { get; } = new();  // docId -> <member>
			private Dictionary<string, string> Href { get; } = new();   // docId -> /api/slug.html[#anchor]
			private MetadataLoadContext Mlc { get; }
			private List<Assembly> Asms { get; } = new();

			public Builder(IReadOnlyList<(string Dll, string Xml)> assemblies)
			{

				var paths = new List<string>();
				// each configured assembly's own dll first, so its canonical copy wins over any copy that
				// CopyLocalLockFileAssemblies dropped next to a peer that depends on it
				foreach (var (dll, _) in assemblies) paths.Add(dll);
				foreach (var (dll, _) in assemblies) paths.AddRange(Directory.GetFiles(Path.GetDirectoryName(dll)!, "*.dll"));
				var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
				paths.AddRange(Directory.GetFiles(runtimeDir, "*.dll"));
				// FoundationDB.Client references Microsoft.Extensions.* (DI), which ship in the AspNetCore
				// shared framework, not the base runtime. Add the same-major AspNetCore framework.
				var sharedRoot = Directory.GetParent(runtimeDir)?.Parent?.FullName; // runtimeDir -> .../shared/Microsoft.NETCore.App/<ver> ; up two -> .../shared
				var aspNet = sharedRoot == null ? null : Path.Combine(sharedRoot, "Microsoft.AspNetCore.App");
				if (aspNet != null && Directory.Exists(aspNet))
				{
					var dir = Directory.GetDirectories(aspNet)
						.Select(d => (d, ok: Version.TryParse(Path.GetFileName(d), out var v), v))
						.Where(x => x.ok && x.v!.Major == Environment.Version.Major)
						.OrderBy(x => x.v).Select(x => x.d).LastOrDefault();
					if (dir != null) paths.AddRange(Directory.GetFiles(dir, "*.dll"));
				}
				// exactly one path per assembly file name: MetadataLoadContext throws "already loaded" when the
				// same dependency sits in several dirs (a copy next to each assembly that pulled it in)
				var byFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
				foreach (var p in paths) { var n = Path.GetFileName(p); if (!byFile.ContainsKey(n)) byFile[n] = p; }
				this.Mlc = new MetadataLoadContext(new PathAssemblyResolver(byFile.Values));
				foreach (var (dll, xml) in assemblies) { if (File.Exists(xml)) LoadXml(xml); this.Asms.Add(this.Mlc.LoadFromAssemblyPath(dll)); }
			}

			public List<ApiType> Run()
			{
				var types = new List<(string Asm, Type T)>();
				foreach (var a in this.Asms)
				{
					var asm = a.GetName().Name ?? "";
					foreach (var t in a.GetExportedTypes()
						.Where(t => t.Namespace is { } ns && !ns.StartsWith("JetBrains", StringComparison.Ordinal) && ns != "System.Runtime.CompilerServices")
						.Where(t => !t.Name.Contains('<') && !t.Name.Contains('$'))) // drop compiler-generated display classes
						types.Add((asm, t));
				}

				// Pass A: register the href of every type and member, so <see cref> in any summary can
				// resolve to a link, even a forward reference to a type rendered later.
				foreach (var (_, t) in types)
				{
					this.Href["T:" + DocType(t)] = "/api/" + Slug(t) + ".html";
					foreach (var mi in Members(t))
						try { this.Href[DocId(mi)] = "/api/" + Slug(t) + ".html#" + Anchor(MemberName(mi, t)); }
						catch { /* signature references an unresolved type; member is skipped everywhere */ }
				}

				// Pass B: render.
				var result = new List<ApiType>();
				foreach (var (asm, t) in types)
				{
					var doc = Effective("T:" + DocType(t), t);
					result.Add(new ApiType(
						asm, t.Namespace ?? "", FullDisplay(t), Slug(t), Kind(t),
						Summary(doc), Block(doc?.Element("remarks")), Block(doc?.Element("example")),
						Implements(t), BuildMembers(t)));
				}
				return result
					.OrderBy(x => x.Assembly, StringComparer.Ordinal)
					.ThenBy(x => x.Namespace, StringComparer.Ordinal)
					.ThenBy(x => x.Display, StringComparer.Ordinal)
					.ToList();
			}

			private static readonly string[] GroupOrder = { "Constructors", "Properties", "Methods", "Fields", "Events" };

			private List<ApiMember> BuildMembers(Type t)
			{
				var flat = new List<(string Group, string Name, MemberInfo Mi)>();
				foreach (var mi in Members(t)) flat.Add((GroupOf(mi), MemberName(mi, t), mi));

				var members = new List<ApiMember>();
				foreach (var g in flat.GroupBy(x => x.Group).OrderBy(g => Array.IndexOf(GroupOrder, g.Key)))
				{
					foreach (var byName in g.GroupBy(x => x.Name).OrderBy(x => x.Key, StringComparer.Ordinal))
					{
						var overloads = new List<ApiOverload>();
						foreach (var m in byName.OrderBy(x => ParamCountSafe(x.Mi)))
						{
							try { overloads.Add(BuildOverload(m.Mi, t)); }
							catch (Exception ex) { Console.Error.WriteLine($"  [api] dropped {t.FullName}.{m.Name}: {ex.GetType().Name} {ex.Message}"); }
						}
						if (overloads.Count > 0) members.Add(new ApiMember(g.Key, byName.Key, Anchor(byName.Key), overloads));
					}
				}
				return members;
			}

			// GetParameters() decodes the method signature and can throw when it references a type in an
			// assembly the resolver cannot find; treat that as zero for ordering (the overload is dropped later).
			private static int ParamCountSafe(MemberInfo mi)
			{
				try { return (mi as MethodBase)?.GetParameters().Length ?? 0; }
				catch { return 0; }
			}

			private ApiOverload BuildOverload(MemberInfo mi, Type owner)
			{
				var doc = Effective(DocId(mi), mi);
				var ps = new List<ApiParam>();
				if (mi is MethodBase mb)
				{
					var pdoc = ParamDocs(doc);
					foreach (var p in mb.GetParameters())
						ps.Add(new ApiParam(p.Name ?? "", ParamType(p), pdoc.TryGetValue(p.Name ?? "", out var d) ? d : ""));
				}
				return new ApiOverload(Signature(mi, owner), Summary(doc), ps, Inline(doc?.Element("returns")), Block(doc?.Element("remarks")), Block(doc?.Element("example")));
			}

			// ---------- member set ----------
			private const BindingFlags F = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

			private static IEnumerable<MemberInfo> Members(Type t)
			{
				foreach (var c in t.GetConstructors(F)) yield return c;
				foreach (var p in t.GetProperties(F)) yield return p;
				foreach (var m in t.GetMethods(F)) if (!m.IsSpecialName) yield return m;
				foreach (var f in t.GetFields(F)) if (!f.IsSpecialName) yield return f;
				foreach (var e in t.GetEvents(F)) yield return e;
			}

			private static string GroupOf(MemberInfo mi) => mi switch
			{
				ConstructorInfo => "Constructors",
				PropertyInfo => "Properties",
				MethodInfo => "Methods",
				FieldInfo => "Fields",
				EventInfo => "Events",
				_ => "Members",
			};

			private static string MemberName(MemberInfo mi, Type owner) => mi is ConstructorInfo ? SimpleName(owner) : mi.Name;

			// ---------- <inheritdoc> resolution ----------
			// Return the doc element to render for a member, following <inheritdoc> to its cref target or,
			// when there is no cref, to the same member on a base type or implemented interface.
			private XElement? Effective(string docId, MemberInfo? mi) => Effective(docId, mi, new HashSet<string>());

			private XElement? Effective(string docId, MemberInfo? mi, HashSet<string> seen)
			{
				if (!seen.Add(docId)) return null;
				this.Xml.TryGetValue(docId, out var elem);
				var inherit = elem?.Element("inheritdoc");
				if (elem != null && inherit == null) return elem; // has its own doc

				if (inherit != null)
				{
					var cref = inherit.Attribute("cref")?.Value;
					if (cref != null) return Effective(cref, null, seen) ?? elem;
				}
				// no cref (or no xml at all): inherit from base type / interface member
				if (mi != null)
					foreach (var baseMi in BaseMembers(mi))
					{
						var r = Effective(DocId(baseMi), baseMi, seen);
						if (r != null && r.Element("summary") != null) return r;
					}
				return elem;
			}

			// The same member as it appears on base classes and implemented interfaces (name + parameter
			// types match). MetadataLoadContext supports these reflection queries.
			private static IEnumerable<MemberInfo> BaseMembers(MemberInfo mi)
			{
				var declaring = mi.DeclaringType;
				if (declaring == null) yield break;
				var sources = new List<Type>();
				for (var b = declaring.BaseType; b != null; b = b.BaseType) sources.Add(b);
				sources.AddRange(declaring.GetInterfaces());

				foreach (var s in sources)
				{
					MemberInfo? found = null;
					try
					{
						switch (mi)
						{
							case MethodInfo m:
								found = s.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
									.FirstOrDefault(x => x.Name == m.Name && SameParams(x, m));
								break;
							case PropertyInfo p:
								found = s.GetProperty(p.Name);
								break;
							case EventInfo e:
								found = s.GetEvent(e.Name);
								break;
						}
					}
					catch { found = null; }
					if (found != null) yield return found;
				}
			}

			private static bool SameParams(MethodInfo a, MethodInfo b)
			{
				var pa = a.GetParameters();
				var pb = b.GetParameters();
				if (pa.Length != pb.Length) return false;
				for (int i = 0; i < pa.Length; i++)
					if (pa[i].ParameterType.FullName != pb[i].ParameterType.FullName) return false;
				return true;
			}

			// ---------- signatures (source-accurate) ----------
			private string Signature(MemberInfo mi, Type owner)
			{
				var st = IsStatic(mi) ? "static " : "";
				switch (mi)
				{
					case ConstructorInfo c: return $"{SimpleName(owner)}({Params(c)})";
					case MethodInfo m: return $"{st}{Display(m.ReturnType)} {m.Name}{MethodGenerics(m)}({Params(m)})";
					case PropertyInfo p: return $"{st}{Display(p.PropertyType)} {p.Name} {Accessors(p)}";
					case FieldInfo f when owner.IsEnum: return $"{f.Name} = {f.GetRawConstantValue()}";
					case FieldInfo f: return $"{FieldMods(f)}{Display(f.FieldType)} {f.Name}";
					case EventInfo e: return $"{st}event {Display(e.EventHandlerType!)} {e.Name}";
					default: return mi.Name;
				}
			}

			private static bool IsStatic(MemberInfo mi) => mi switch
			{
				MethodBase mb => mb.IsStatic,
				PropertyInfo p => (p.GetMethod ?? p.SetMethod)?.IsStatic == true,
				FieldInfo f => f.IsStatic,
				EventInfo e => (e.AddMethod ?? e.RemoveMethod)?.IsStatic == true,
				_ => false,
			};

			private static string FieldMods(FieldInfo f)
				=> f.IsLiteral ? "const " : f.IsStatic ? (f.IsInitOnly ? "static readonly " : "static ") : f.IsInitOnly ? "readonly " : "";

			private static string Accessors(PropertyInfo p)
			{
				var get = p.GetMethod is { IsPublic: true } ? "get; " : "";
				var set = p.SetMethod is { IsPublic: true } ? "set; " : "";
				return "{ " + get + set + "}";
			}

			private static string MethodGenerics(MethodInfo m) => m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "";

			private string Params(MethodBase m) => string.Join(", ", m.GetParameters().Select(ParamDecl));

			private string ParamDecl(ParameterInfo p)
			{
				var mods = "";
				// MetadataLoadContext only supports GetCustomAttributesData(); IsDefined/GetCustomAttributes throw.
				if (p.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute")) mods = "params ";
				else if (p.ParameterType.IsByRef) mods = p.IsOut ? "out " : p.IsIn ? "in " : "ref ";
				var def = "";
				if (p.HasDefaultValue) def = " = " + Literal(p.RawDefaultValue);
				return $"{mods}{ParamType(p)} {p.Name}{def}";
			}

			private string ParamType(ParameterInfo p) => Display(p.ParameterType.IsByRef ? p.ParameterType.GetElementType()! : p.ParameterType);

			private static string Literal(object? v) => v switch
			{
				null => "null",
				string s => "\"" + s + "\"",
				bool b => b ? "true" : "false",
				char c => "'" + c + "'",
				_ => v.ToString() ?? "",
			};

			// C# keyword names, Nullable<T> -> T?, arrays, generics as Name<...>
			private static readonly Dictionary<string, string> Keywords = new()
			{
				["System.Boolean"] = "bool", ["System.Byte"] = "byte", ["System.SByte"] = "sbyte",
				["System.Int16"] = "short", ["System.UInt16"] = "ushort", ["System.Int32"] = "int",
				["System.UInt32"] = "uint", ["System.Int64"] = "long", ["System.UInt64"] = "ulong",
				["System.Single"] = "float", ["System.Double"] = "double", ["System.Decimal"] = "decimal",
				["System.Char"] = "char", ["System.String"] = "string", ["System.Object"] = "object",
				["System.Void"] = "void",
			};

			private string Display(Type t)
			{
				if (t.IsByRef) return Display(t.GetElementType()!);
				if (t.IsArray) return Display(t.GetElementType()!) + "[]";
				if (t.IsGenericParameter) return t.Name;
				if (t.IsGenericType)
				{
					var def = t.GetGenericTypeDefinition();
					if (def.FullName == "System.Nullable`1") return Display(t.GetGenericArguments()[0]) + "?";
					if (def.FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true)
						return "(" + string.Join(", ", t.GetGenericArguments().Select(Display)) + ")";
				}
				var full = t.FullName != null ? t.FullName : (t.Namespace + "." + t.Name);
				if (Keywords.TryGetValue(full, out var kw)) return kw;
				var name = t.Name;
				int tick = name.IndexOf('`');
				if (tick < 0) return name;
				name = name[..tick];
				return $"{name}<{string.Join(", ", t.GetGenericArguments().Select(Display))}>";
			}

			private List<string> Implements(Type t)
			{
				var list = new List<string>();
				if (t.BaseType is { } b && b.FullName != "System.Object" && b.FullName != "System.ValueType" && b.FullName != "System.Enum")
					list.Add(TypeLink(b));
				foreach (var i in t.GetInterfaces()) list.Add(TypeLink(i));
				return list;
			}

			// a type reference as a markdown link when it is one of our pages, else as escaped code text
			private string TypeLink(Type t)
			{
				var id = "T:" + DocType(t.IsGenericType ? t.GetGenericTypeDefinition() : t);
				var text = Display(t);
				return this.Href.TryGetValue(id, out var href) ? $"[{MdText(text)}]({href})" : Code(text);
			}

			// ---------- XML rendering ----------
			private void LoadXml(string xmlPath)
			{
				foreach (var m in XDocument.Load(xmlPath).Descendants("member"))
				{
					var name = m.Attribute("name")?.Value;
					if (name != null) this.Xml[name] = m;
				}
			}

			private string Summary(XElement? doc) => Inline(doc?.Element("summary"));

			private Dictionary<string, string> ParamDocs(XElement? doc)
			{
				var map = new Dictionary<string, string>();
				if (doc == null) return map;
				foreach (var p in doc.Elements("param"))
				{
					var n = p.Attribute("name")?.Value;
					if (n != null) map[n] = InlineNodes(p.Nodes());
				}
				return map;
			}

			// inline content of an element (summary, returns, param) to markdown, one line
			private string Inline(XElement? el) => el == null ? "" : Regex.Replace(InlineNodes(el.Nodes()), @"\s+", " ").Trim();

			private string InlineNodes(IEnumerable<XNode> nodes)
			{
				var sb = new StringBuilder();
				foreach (var node in nodes)
				{
					switch (node)
					{
						case XText txt: sb.Append(txt.Value); break;
						case XElement e:
							switch (e.Name.LocalName)
							{
								case "see": case "seealso": sb.Append(SeeLink(e)); break;
								case "paramref": case "typeparamref": sb.Append(Code(e.Attribute("name")?.Value ?? e.Value)); break;
								case "c": sb.Append(Code(e.Value)); break;
								case "b": case "strong": sb.Append("**").Append(InlineNodes(e.Nodes())).Append("**"); break;
								case "i": case "em": sb.Append('*').Append(InlineNodes(e.Nodes())).Append('*'); break;
								default: sb.Append(InlineNodes(e.Nodes())); break;
							}
							break;
					}
				}
				return sb.ToString();
			}

			// block content (remarks, example): paragraphs, code blocks, and lists to markdown
			private string Block(XElement? el)
			{
				if (el == null) return "";
				var sb = new StringBuilder();
				var run = new StringBuilder();
				void FlushRun()
				{
					var s = Regex.Replace(run.ToString(), @"\s+", " ").Trim();
					if (s.Length > 0) sb.Append(s).Append("\n\n");
					run.Clear();
				}
				foreach (var node in el.Nodes())
				{
					if (node is XElement e && e.Name.LocalName == "code")
					{
						FlushRun();
						sb.Append("```csharp\n").Append(Dedent(e.Value)).Append("\n```\n\n");
					}
					else if (node is XElement p && p.Name.LocalName == "para")
					{
						FlushRun();
						run.Append(InlineNodes(p.Nodes()));
						FlushRun();
					}
					else if (node is XElement l && l.Name.LocalName == "list")
					{
						FlushRun();
						foreach (var item in l.Elements("item"))
						{
							var term = item.Element("term");
							var desc = item.Element("description");
							var text = desc != null ? InlineNodes(desc.Nodes()) : InlineNodes(item.Nodes());
							if (term != null) text = "**" + InlineNodes(term.Nodes()).Trim() + "** " + text;
							sb.Append("- ").Append(Regex.Replace(text, @"\s+", " ").Trim()).Append('\n');
						}
						sb.Append('\n');
					}
					else if (node is XText t) run.Append(t.Value);
					else if (node is XElement other) run.Append(InlineNodes(other.Nodes()));
				}
				FlushRun();
				return sb.ToString().Trim();
			}

			private static string Dedent(string code)
			{
				var lines = code.Replace("\r\n", "\n").Trim('\n').Split('\n');
				int min = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
				return string.Join("\n", lines.Select(l => l.Length >= min ? l[min..] : l)).TrimEnd();
			}

			// <see cref="X"> / <see langword="y"> to a link (when X is one of our pages) or code text
			private string SeeLink(XElement e)
			{
				var lang = e.Attribute("langword")?.Value;
				if (lang != null) return Code(lang);
				var href = e.Attribute("href")?.Value;
				if (href != null) return $"[{(e.Value.Length > 0 ? e.Value : href)}]({href})";
				var cref = e.Attribute("cref")?.Value ?? "";
				var text = e.Value.Length > 0 ? e.Value : ShortName(cref);
				return this.Href.TryGetValue(cref, out var link) ? $"[{MdText(text)}]({link})" : Code(text);
			}

			private static string ShortName(string cref)
			{
				var s = cref.Contains(':') ? cref[(cref.IndexOf(':') + 1)..] : cref;
				int paren = s.IndexOf('(');
				if (paren >= 0) s = s[..paren];
				int dot = s.LastIndexOf('.');
				var name = dot >= 0 ? s[(dot + 1)..] : s;
				int tick = name.IndexOf('`'); // drop the CLR arity marker: FdbTupleKey`2 / Memoize``1
				return tick >= 0 ? name[..tick] : name;
			}

			// A CommonMark inline-code span for arbitrary content: fence longer than any backtick run
			// inside, with a pad space when the content itself starts or ends with a backtick.
			private static string Code(string content)
			{
				int max = 0, cur = 0;
				foreach (var ch in content) { if (ch == '`') { if (++cur > max) max = cur; } else cur = 0; }
				var fence = new string('`', max + 1);
				var pad = content.Length > 0 && (content[0] == '`' || content[^1] == '`') ? " " : "";
				return fence + pad + content + pad + fence;
			}
		}

		// ---------- names / kinds / doc ids (shared) ----------
		private static string SimpleName(Type t)
		{
			var name = t.Name;
			int tick = name.IndexOf('`');
			if (tick < 0) return name;
			return name[..tick] + "<" + string.Join(", ", t.GetGenericArguments().Select(a => a.Name)) + ">";
		}

		private static string FullDisplay(Type t)
			=> t.IsNested && t.DeclaringType != null ? FullDisplay(t.DeclaringType) + "." + SimpleName(t) : SimpleName(t);

		private static string Kind(Type t) =>
			t.IsEnum ? "enum" :
			t.IsInterface ? "interface" :
			IsDelegate(t) ? "delegate" :
			t.IsValueType ? "struct" : "class";

		private static bool IsDelegate(Type t) { for (var b = t.BaseType; b != null; b = b.BaseType) if (b.FullName == "System.MulticastDelegate") return true; return false; }

		private static string DocType(Type t) => (t.FullName ?? (t.Namespace + "." + t.Name)).Replace('+', '.');

		private static string DocId(MemberInfo m)
		{
			var decl = DocType(m.DeclaringType!);
			return m switch
			{
				FieldInfo f => $"F:{decl}.{f.Name}",
				PropertyInfo p => $"P:{decl}.{p.Name}",
				EventInfo e => $"E:{decl}.{e.Name}",
				ConstructorInfo c => $"M:{decl}.#ctor{DocParams(c)}",
				MethodInfo mi => $"M:{decl}.{mi.Name}{(mi.IsGenericMethodDefinition ? "``" + mi.GetGenericArguments().Length : "")}{DocParams(mi)}",
				_ => "",
			};
		}

		private static string DocParams(MethodBase m)
		{
			var ps = m.GetParameters();
			return ps.Length == 0 ? "" : "(" + string.Join(",", ps.Select(p => DocParam(p.ParameterType))) + ")";
		}

		private static string DocParam(Type t)
		{
			if (t.IsByRef) return DocParam(t.GetElementType()!) + "@";
			if (t.IsPointer) return DocParam(t.GetElementType()!) + "*";
			if (t.IsArray)
			{
				var el = DocParam(t.GetElementType()!);
				int rank = t.GetArrayRank();
				return rank == 1 ? el + "[]" : el + "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]";
			}
			if (t.IsGenericParameter) return (t.DeclaringMethod != null ? "``" : "`") + t.GenericParameterPosition;
			if (t.IsGenericType)
			{
				var full = (t.FullName ?? (t.Namespace + "." + t.Name)).Replace('+', '.');
				int tick = full.IndexOf('`');
				var name = tick < 0 ? full : full[..tick];
				return $"{name}{{{string.Join(",", t.GetGenericArguments().Select(DocParam))}}}";
			}
			return DocType(t);
		}

		// ---------- page rendering ----------
		public static string RenderPage(ApiType t)
		{
			var sb = new StringBuilder();
			sb.Append("# ").Append(MdText(t.Display)).Append("\n\n");
			sb.Append("Namespace: `").Append(t.Namespace).Append("` · ").Append(t.Kind).Append("\n\n");
			if (t.Implements.Count > 0) sb.Append("Implements: ").Append(string.Join(", ", t.Implements)).Append("\n\n");
			if (t.Summary.Length > 0) sb.Append(t.Summary).Append("\n\n");
			if (t.Remarks.Length > 0) sb.Append("## Remarks\n\n").Append(t.Remarks).Append("\n\n");
			if (t.Example.Length > 0) sb.Append("## Example\n\n").Append(t.Example).Append("\n\n");

			foreach (var group in t.Members.GroupBy(m => m.Group))
			{
				sb.Append("## ").Append(group.Key).Append("\n\n");
				foreach (var m in group)
				{
					sb.Append("#### ").Append(MdText(m.Name)).Append("\n\n");
					foreach (var o in m.Overloads)
					{
						sb.Append("`").Append(o.Signature).Append("`\n\n");
						if (o.Summary.Length > 0) sb.Append(o.Summary).Append("\n\n");
						var documented = o.Params.Where(p => p.Doc.Length > 0).ToList();
						if (documented.Count > 0)
						{
							foreach (var p in documented)
								sb.Append("- `").Append(p.Name).Append("` — ").Append(p.Doc).Append('\n');
							sb.Append('\n');
						}
						if (o.Returns.Length > 0) sb.Append("Returns: ").Append(o.Returns).Append("\n\n");
						if (o.Remarks.Length > 0) sb.Append(o.Remarks).Append("\n\n");
						if (o.Example.Length > 0) sb.Append(o.Example).Append("\n\n");
					}
				}
			}
			return sb.ToString();
		}

		public static string RenderIndex(List<ApiType> types)
		{
			var sb = new StringBuilder("# API Reference\n\nPublic types, one page each, grouped by assembly.\n\n");
			foreach (var asm in types.GroupBy(x => x.Assembly).OrderBy(g => g.Key, StringComparer.Ordinal))
			{
				sb.Append("## ").Append(asm.Key).Append(" {#").Append(Anchor(asm.Key)).Append("}\n\n"); // explicit id matches the nav anchor
				foreach (var ns in asm.GroupBy(x => x.Namespace).OrderBy(g => g.Key, StringComparer.Ordinal))
				{
					sb.Append("### ").Append(ns.Key).Append("\n\n");
					foreach (var t in ns) sb.Append("- [").Append(MdText(t.Display)).Append("](/api/").Append(t.Slug).Append(".html) — ").Append(Cell(FirstSentence(t.Summary))).Append('\n');
					sb.Append('\n');
				}
			}
			return sb.ToString();
		}

		private static string Cell(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();
		private static string FirstSentence(string s) { var i = s.IndexOf(". ", StringComparison.Ordinal); return i > 0 ? s[..(i + 1)] : s; }
		// escape angle brackets for markdown heading / link text so FdbTupleKey<T1> is not eaten as a tag
		private static string MdText(string s) => s.Replace("<", "&lt;").Replace(">", "&gt;");
	}
}
