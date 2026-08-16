using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Scriban;
using Scriban.Runtime;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SnowBank.DocGen
{
	// Spike: render FoundationDB/Documentation/Tuples.md to a static, reflow-free HTML page with the
	// existing SnowBank CSS, the custom fdb-* blocks rendered at build time, and the nav baked in.
	//   dotnet run                -> render to Sandbox/DocGenSpike/out/
	//   dotnet run -- --serve 8080 -> render, then serve out/ over http (the preview pane blocks file://)
	public static class Program
	{
		public static int Main(string[] args)
		{
			string? rootArg = null;
			int? servePort = null;
			for (int i = 0; i < args.Length; i++)
			{
				if (args[i] == "--root" && i + 1 < args.Length) rootArg = args[++i];
				else if (args[i] == "--serve") servePort = i + 1 < args.Length && int.TryParse(args[i + 1], out var p) ? p : 8080;
			}
			var root = rootArg != null ? Path.GetFullPath(rootArg) : FindRoot();
			var docs = Path.Combine(root, "Documentation");
			var outDir = Path.Combine(root, "artifacts", "_site");
			var config = DocGenConfig.Load(docs);

			Render(root, docs, outDir, config);

			if (servePort is int port) Serve(outDir, port);
			return 0;
		}

		private static void Render(string root, string docs, string outDir, DocGenConfig config)
		{
			if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true); // fresh output, no orphan pages
			Directory.CreateDirectory(outDir);

			var toolDir = ToolDir(); // the tool's own dir, so its assets/scripts resolve over any repo, not just its own
			var nodes = ParseToc(File.ReadAllText(Path.Combine(docs, "toc.yml")));

			// shared assets at the site root (pages link them root-absolute, so they resolve at any depth)
			File.Copy(Path.Combine(toolDir, "assets", "main.css"), Path.Combine(outDir, "main.css"), overwrite: true);
			var logo = Path.Combine(docs, "images", "logo.png");
			if (File.Exists(logo)) File.Copy(logo, Path.Combine(outDir, "logo.png"), overwrite: true);
			var d3 = Path.Combine(toolDir, "assets", "d3.min.js");
			if (File.Exists(d3)) File.Copy(d3, Path.Combine(outDir, "d3.min.js"), overwrite: true);

			var template = Template.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "templates", "layout.sbn-html")));

			var pages = new List<(string Href, string Name)>();
			Flatten(nodes, pages);
			// the diataxis toc lists some pages twice (a section landing that repeats as its first child,
			// and the Reference section aliasing folder pages), so render / index each once
			pages = pages.GroupBy(p => p.Href).Select(g => g.First()).ToList();
			var landingHtml = pages.Count > 0 ? HrefToHtml(pages[0].Href) : "index.html"; // first toc page = the site landing

			// Phase 1: read + transform every page, then parse it to collect the blocks that need a
			// build-time pass: mermaid diagrams (one headless-Chrome batch) and code fences (one Shiki
			// batch). Parsing here (not a regex) picks up fences nested in lists too.
			var collect = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
			var mdByHref = new Dictionary<string, string>();
			var frMdByHref = new Dictionary<string, string>(); // English href -> French content from the X.fr.md twin
			var mermaidCodes = new List<string>();
			var codeFences = new List<(string Lang, string Code)>();
			void Collect(string text)
			{
				foreach (var fcb in Markdig.Markdown.Parse(text, collect).Descendants<FencedCodeBlock>())
				{
					var lang = FenceLang(fcb);
					var code = FenceText(fcb);
					if (lang.Equals("mermaid", StringComparison.OrdinalIgnoreCase))
					{
						if (code.Length > 0 && !mermaidCodes.Contains(code)) mermaidCodes.Add(code);
					}
					else if (ShikiLangs.Contains(lang)) codeFences.Add((lang, code));
				}
			}
			int missing = 0;
			foreach (var (href, _) in pages)
			{
				var mdPath = Path.Combine(docs, href.Replace('/', Path.DirectorySeparatorChar));
				if (!File.Exists(mdPath)) { missing++; Console.Error.WriteLine($"  [skip] {href} (no file)"); continue; }
				var text = ReflowCodeComments(InjectPackageVersions(File.ReadAllText(mdPath), config.Version));
				mdByHref[href] = text;
				Collect(text);
				// French twin (X.md -> X.fr.md), collected so its fences are highlighted in the same batches
				var frPath = Path.ChangeExtension(mdPath, null) + ".fr.md";
				if (File.Exists(frPath))
				{
					var frText = ReflowCodeComments(InjectPackageVersions(File.ReadAllText(frPath), config.Version));
					frMdByHref[href] = frText;
					Collect(frText);
				}
			}
			var mermaidSvg = RenderMermaid(toolDir, mermaidCodes);
			var shiki = Highlight(toolDir, codeFences);

			// API reference: reflect each configured assembly + its XML docs into one markdown page per public
			// type, then feed those pages through the same nav / search / render path as the guides. Done
			// after Phase 1 so the collect pass does not re-scan them (API pages carry no fenced blocks).
			// The symbol map drives auto-linking of inline-code type names in the guides.
			var apiByName = new Dictionary<string, string>(StringComparer.Ordinal);   // "TypeName" -> /api/slug.html
			var apiByMember = new Dictionary<string, string>(StringComparer.Ordinal); // "Type.Member" -> /api/slug.html#anchor
			var apiHrefs = new HashSet<string>();
			var apiAssemblies = config.Assemblies
				.Select(n => ResolveAssembly(root, config, n))
				.Where(p => p.Dll != null)
				.Select(p => (p.Dll!, p.Xml!))
				.ToList();
			if (apiAssemblies.Count > 0)
			{
				var types = ApiDocs.Extract(apiAssemblies);
				mdByHref["api/index.md"] = ApiDocs.RenderIndex(types);
				pages.Add(("api/index.md", "API Reference"));
				apiHrefs.Add("api/index.md");
				foreach (var t in types)
				{
					var href = "api/" + t.Slug + ".md";
					mdByHref[href] = ApiDocs.RenderPage(t);
					pages.Add((href, t.Display));
					apiHrefs.Add(href);
					var typeName = t.Display.Split('<')[0];             // FdbKey, or Fdb.Bulk for a nested type
					if (!apiByName.ContainsKey(typeName)) apiByName[typeName] = "/api/" + t.Slug + ".html";
					int dot = typeName.LastIndexOf('.');
					var lastSeg = dot >= 0 ? typeName[(dot + 1)..] : typeName;
					foreach (var m in t.Members)
					{
						var mhref = "/api/" + t.Slug + ".html#" + m.Anchor;
						if (!apiByMember.ContainsKey(typeName + "." + m.Name)) apiByMember[typeName + "." + m.Name] = mhref;
						if (!apiByMember.ContainsKey(lastSeg + "." + m.Name)) apiByMember[lastSeg + "." + m.Name] = mhref;
					}
				}
				// One collapsed "API Reference" entry (folded unless the current page is an API page), so the
				// namespaces do not clutter the menu as a block of uppercase labels. Namespace labels drop the
				// shared "FoundationDB." prefix to stay short.
				var apiSection = new TocNode { Name = "API Reference", NameFr = "Référence API", Collapsed = true, Items = new List<TocNode>() };
				apiSection.Items.Add(new TocNode { Name = "Overview", NameFr = "Vue d'ensemble", Href = "api/index.md" });
				apiSection.Items.AddRange(types.GroupBy(t => t.Namespace).OrderBy(g => g.Key, StringComparer.Ordinal)
					.Select(g => new TocNode
					{
						Name = g.Key.StartsWith("FoundationDB.", StringComparison.Ordinal) ? g.Key["FoundationDB.".Length..] : g.Key,
						Collapsed = true, // namespace groups are large; keep them folded unless they hold the current page
						Items = g.Select(t => new TocNode { Name = t.Display, Href = "api/" + t.Slug + ".md" }).ToList(),
					}));
				nodes.Add(apiSection);
				Console.WriteLine($"  api: {types.Count} types across {types.Select(t => t.Namespace).Distinct().Count()} namespace(s)");
			}
			else Console.Error.WriteLine($"  [api] no API assemblies resolved from docgen.json ({config.Assemblies.Count} configured; build them Release {config.ApiTfm})");

			// Phase 2: the fenced-block set for this build: FoundationDB's fdb-* blocks, CloudLayer's
			// island(s), and mermaid from the pre-rendered map. Regular code fences resolve against the
			// Shiki map, passed to the extension as the fallback for languaged fences.
			var renderers = new Dictionary<string, FenceRenderer>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in FdbBlocks.Renderers) renderers[kv.Key] = kv.Value;
			foreach (var kv in IslandBlocks.Renderers) renderers[kv.Key] = kv.Value;
			renderers["mermaid"] = code => mermaidSvg.TryGetValue(code, out var svg)
				? $"<div class=\"mermaid\">{svg}</div>"
				: $"<pre>{EscHtml(code)}</pre>";
			var pipeline = BuildPipeline(renderers, (lang, code) => shiki.TryGetValue(ShikiKey(lang, code), out var html) ? html : null);

			var index = new List<SearchDoc>();
			int rendered = 0;
			foreach (var (href, name) in pages)
			{
				if (!mdByHref.TryGetValue(href, out var mdText)) continue;
				try {

				// Parse once so the "In this article" ids match the auto-identifier ids on the rendered
				// headings; rewrite inter-page .md links to .html before rendering the body.
				var doc = Markdig.Markdown.Parse(mdText, pipeline);
				RewriteMdLinks(doc);
				AlignNumericColumns(doc);
				if (!apiHrefs.Contains(href)) AutoLinkSymbols(doc, apiByName, apiByMember); // link inline-code symbols in guides

				index.Add(new SearchDoc("/" + HrefToHtml(href), name, PageHeadings(doc), PlainText(doc)));

				var body = RenderBody(doc, pipeline);
				var model = new ScriptObject
				{
					["title"] = name,
					["nav_html"] = BuildNav(nodes, href, false),
					["body_html"] = body,
					["on_this_page"] = BuildOnThisPage(doc),
					["has_island"] = body.Contains("class=\"island "), // load d3 + the island script only where used
					["lang"] = "en",
					["search_index"] = "/search-index.json",
					["en_url"] = "/" + HrefToHtml(href),
					["fr_url"] = "/fr/" + HrefToHtml(href),
					["has_fr"] = !apiHrefs.Contains(href), // API reference is English-only
				};
				var ctx = new TemplateContext();
				ctx.PushGlobal(model);
				var page = template.Render(ctx);

				var outPath = Path.Combine(outDir, HrefToHtml(href).Replace('/', Path.DirectorySeparatorChar));
				Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
				File.WriteAllText(outPath, page, new UTF8Encoding(false));
				rendered++;

				} catch (Exception ex) { Console.Error.WriteLine($"  [FAIL] {href}: {ex.GetType().Name} {ex.Message}"); }
			}

			// build-time search index: the client search box fetches this once, on first use, and ranks
			// client-side (small corpus). On-demand, so it adds no load-time work and no reflow.
			var indexJson = JsonSerializer.Serialize(index, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			File.WriteAllText(Path.Combine(outDir, "search-index.json"), indexJson, new UTF8Encoding(false));

			// French tree under /fr/ (doc pages only; the API reference stays English). An untranslated page
			// falls back to the English body with a short note, so the /fr/ tree has no holes.
			var indexFr = new List<SearchDoc>();
			var frNames = new Dictionary<string, string>();
			CollectFrNames(nodes, frNames);
			int renderedFr = 0;
			foreach (var (href, name) in pages)
			{
				if (apiHrefs.Contains(href) || !mdByHref.TryGetValue(href, out var enText)) continue;
				try
				{
					var frName = frNames.TryGetValue(href, out var fn) ? fn : name;
					var src = frMdByHref.TryGetValue(href, out var frText)
						? frText
						: "> Cette page n'est pas encore traduite. La version anglaise est affichée ci-dessous.\n\n" + enText;
					var doc = Markdig.Markdown.Parse(src, pipeline);
					AlignFrenchAnchors(enText, src, doc, pipeline); // keep heading ids/anchors on the English slugs so links survive translation
					RewriteMdLinks(doc);
					AlignNumericColumns(doc);
					AutoLinkSymbols(doc, apiByName, apiByMember);
					var htmlHref = HrefToHtml(href);
					indexFr.Add(new SearchDoc("/fr/" + htmlHref, frName, PageHeadings(doc), PlainText(doc)));
					var body = RenderBody(doc, pipeline);
					var model = new ScriptObject
					{
						["title"] = frName,
						["nav_html"] = BuildNav(nodes, href, true),
						["body_html"] = body,
						["on_this_page"] = BuildOnThisPage(doc),
						["has_island"] = body.Contains("class=\"island "),
						["lang"] = "fr",
						["search_index"] = "/search-index.fr.json",
						["en_url"] = "/" + htmlHref,
						["fr_url"] = "/fr/" + htmlHref,
						["has_fr"] = true,
					};
					var ctx = new TemplateContext();
					ctx.PushGlobal(model);
					var outPath = Path.Combine(outDir, ("fr/" + htmlHref).Replace('/', Path.DirectorySeparatorChar));
					Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
					File.WriteAllText(outPath, template.Render(ctx), new UTF8Encoding(false));
					renderedFr++;
				}
				catch (Exception ex) { Console.Error.WriteLine($"  [FAIL] fr/{href}: {ex.GetType().Name} {ex.Message}"); }
			}
			var indexFrJson = JsonSerializer.Serialize(indexFr, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
			File.WriteAllText(Path.Combine(outDir, "search-index.fr.json"), indexFrJson, new UTF8Encoding(false));
			Console.WriteLine($"  french: {renderedFr} pages under /fr/ ({frMdByHref.Count} translated, {renderedFr - frMdByHref.Count} english fallback)");

			Console.WriteLine($"rendered {rendered} page(s) to {outDir}{(missing > 0 ? $" ({missing} missing)" : "")}");
			Console.WriteLine($"  search index: {index.Count} pages, {indexJson.Length:N0} bytes; mermaid: {mermaidSvg.Count}/{mermaidCodes.Count} diagrams");

			// a root index.html so "/" and the logo's href="/" resolve on a static host (Pages has no
			// directory-default): copy the first toc page as the landing, for the English and French trees
			CopyAsIndex(outDir, landingHtml);
			CopyAsIndex(Path.Combine(outDir, "fr"), landingHtml);

			// generic self-checks: no repo-specific page names, so the tool validates any doc set
			Check(rendered >= 1, $"rendered at least one page ({rendered})");
			Check(File.Exists(Path.Combine(outDir, "search-index.json")), "build-time search index written");
			Check(File.Exists(Path.Combine(outDir, "main.css")), "stylesheet copied to the site root");
			Check(File.Exists(Path.Combine(outDir, "index.html")), "root index.html emitted (site landing)");
			Check(mermaidCodes.Count == 0 || mermaidSvg.Count > 0, "mermaid diagrams rendered to static SVG at build time");
			// a page with no island must ship no external script: the reflow-free invariant (d3 loads only on island pages)
			var noIsland = Directory.EnumerateFiles(outDir, "*.html", SearchOption.AllDirectories)
				.Select(File.ReadAllText).FirstOrDefault(h => !h.Contains("class=\"island "));
			if (noIsland != null)
			{
				Check(!noIsland.Contains("<script src"), "a non-island page ships no external script (reflow-free)");
				Check(noIsland.Contains("class=\"toc-section\""), "nav sidebar baked into static HTML");
				Check(noIsland.Contains("id=\"docsearch\""), "search box present in header");
			}
			if (apiHrefs.Count > 0)
				Check(File.Exists(Path.Combine(outDir, "api", "index.html")), "API reference index generated");
		}

		private sealed record MermaidResult(string? Id, string? Svg, string? Error);

		// Render every mermaid diagram to a static SVG in one headless-Chrome pass (tools/mermaid.mjs).
		// Returns a map from the trimmed diagram source to its SVG. Empty results leave the block to fall
		// back to raw source, so a missing Node/Chrome degrades instead of failing the build.
		private static Dictionary<string, string> RenderMermaid(string toolDir, List<string> codes)
		{
			var map = new Dictionary<string, string>();
			if (codes.Count == 0) return map;

			var items = codes.Select((c, i) => new { id = i.ToString(), code = c });
			var script = Path.Combine(toolDir, "tools", "mermaid.mjs");
			try
			{
				var psi = new ProcessStartInfo("node")
				{
					ArgumentList = { script },
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					StandardOutputEncoding = new UTF8Encoding(false), // node emits UTF-8; decode it as such so accented French survives
					StandardErrorEncoding = new UTF8Encoding(false),
					StandardInputEncoding = new UTF8Encoding(false),
				};
				using var p = Process.Start(psi)!;
				var outTask = p.StandardOutput.ReadToEndAsync();
				var errTask = p.StandardError.ReadToEndAsync();
				p.StandardInput.Write(JsonSerializer.Serialize(items));
				p.StandardInput.Close();
				if (!p.WaitForExit(120_000)) { p.Kill(true); throw new TimeoutException("mermaid render timed out"); }
				var results = JsonSerializer.Deserialize<List<MermaidResult>>(outTask.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
				foreach (var r in results)
					if (r.Id != null && r.Svg != null && int.TryParse(r.Id, out var idx) && idx < codes.Count)
						map[codes[idx]] = r.Svg;
				if (map.Count < codes.Count) Console.Error.WriteLine($"  [mermaid] {map.Count}/{codes.Count} rendered. {errTask.Result}");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"  [mermaid] render step failed ({ex.Message}); blocks fall back to raw source");
			}
			return map;
		}

		// ---- build-time syntax highlighting (Shiki) ----
		private static readonly HashSet<string> ShikiLangs = new(StringComparer.OrdinalIgnoreCase)
		{
			"csharp", "cs", "c#", "json", "xml", "console", "bash", "sh", "shell",
		};

		private static string FenceLang(FencedCodeBlock f) => (f.Info ?? "").Trim().Split(' ')[0];

		private static string FenceText(LeafBlock block)
		{
			var sb = new StringBuilder();
			var lines = block.Lines.Lines;
			int count = block.Lines.Count;
			for (int i = 0; i < count; i++) sb.Append(lines[i].Slice.ToString()).Append('\n');
			return sb.ToString().TrimStart('\n').TrimEnd();
		}

		private static string ShikiKey(string lang, string code) => lang.ToLowerInvariant() + " " + code;

		private sealed record HlResult(string? Id, string? Html, string? Error);

		// Highlight every code fence to static HTML in one Shiki pass (tools/highlight.mjs, VS Code's
		// dark-plus theme). Returns a map keyed by (lang, code). A missing Node leaves code to fall back
		// to a plain <pre>, so the build still succeeds.
		private static Dictionary<string, string> Highlight(string toolDir, List<(string Lang, string Code)> fences)
		{
			var map = new Dictionary<string, string>();
			if (fences.Count == 0) return map;

			var distinct = new List<(string Lang, string Code)>();
			var seen = new HashSet<string>();
			foreach (var f in fences) if (seen.Add(ShikiKey(f.Lang, f.Code))) distinct.Add(f);

			var items = distinct.Select((f, i) => new { id = i.ToString(), lang = f.Lang, code = f.Code });
			var script = Path.Combine(toolDir, "tools", "highlight.mjs");
			try
			{
				var psi = new ProcessStartInfo("node")
				{
					ArgumentList = { script },
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					StandardOutputEncoding = new UTF8Encoding(false), // node emits UTF-8; decode it as such so accented French survives
					StandardErrorEncoding = new UTF8Encoding(false),
					StandardInputEncoding = new UTF8Encoding(false),
				};
				using var p = Process.Start(psi)!;
				var outTask = p.StandardOutput.ReadToEndAsync();
				var errTask = p.StandardError.ReadToEndAsync();
				p.StandardInput.Write(JsonSerializer.Serialize(items));
				p.StandardInput.Close();
				if (!p.WaitForExit(120_000)) { p.Kill(true); throw new TimeoutException("shiki timed out"); }
				var results = JsonSerializer.Deserialize<List<HlResult>>(outTask.Result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
				foreach (var r in results)
					if (r.Id != null && r.Html != null && int.TryParse(r.Id, out var idx) && idx < distinct.Count)
						map[ShikiKey(distinct[idx].Lang, distinct[idx].Code)] = r.Html;
				if (map.Count < distinct.Count) Console.Error.WriteLine($"  [shiki] {map.Count}/{distinct.Count} highlighted. {errTask.Result}");
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"  [shiki] step failed ({ex.Message}); code falls back to plain <pre>");
			}
			return map;
		}

		// Right-align a table column when every body cell in it starts with a digit (a numeric column),
		// unless the author already set an alignment. Numbers are easier to compare right-aligned.
		private static void AlignNumericColumns(MarkdownObject root)
		{
			foreach (var table in root.Descendants<Table>())
			{
				var body = table.OfType<TableRow>().Where(r => !r.IsHeader).ToList();
				if (body.Count == 0) continue;
				for (int c = 0; c < table.ColumnDefinitions.Count; c++)
				{
					if (table.ColumnDefinitions[c].Alignment != null) continue; // respect author alignment
					bool allNumeric = true;
					int seen = 0;
					foreach (var r in body)
					{
						var cells = r.OfType<TableCell>().ToList();
						if (c >= cells.Count) continue;
						var text = CellText(cells[c]).TrimStart();
						if (text.Length == 0) continue;
						seen++;
						if (!char.IsDigit(text[0])) { allNumeric = false; break; }
					}
					if (allNumeric && seen > 0) table.ColumnDefinitions[c].Alignment = TableColumnAlign.Right;
				}
			}
		}

		private static string CellText(TableCell cell)
		{
			var sb = new StringBuilder();
			foreach (var n in cell.Descendants())
			{
				if (n is LiteralInline lit) sb.Append(lit.Content.ToString());
				else if (n is CodeInline code) sb.Append(code.Content);
			}
			return sb.ToString();
		}

		private static List<TocNode> ParseToc(string yaml)
		{
			var deser = new DeserializerBuilder()
				.WithNamingConvention(CamelCaseNamingConvention.Instance)
				.IgnoreUnmatchedProperties()
				.Build();
			return deser.Deserialize<List<TocNode>>(yaml) ?? new();
		}

		private static void Flatten(List<TocNode> nodes, List<(string, string)> acc)
		{
			foreach (var n in nodes)
			{
				if (n.Href != null) acc.Add((n.Href, n.Name ?? n.Href));
				if (n.Items is { Count: > 0 }) Flatten(n.Items, acc);
			}
		}

		// href -> French page title (from the toc nameFr), for the /fr/ page <title> and search entry
		private static void CollectFrNames(List<TocNode> nodes, Dictionary<string, string> acc)
		{
			foreach (var n in nodes)
			{
				if (n.Href != null && n.NameFr != null) acc[n.Href] = n.NameFr;
				if (n.Items is { Count: > 0 }) CollectFrNames(n.Items, acc);
			}
		}

		private static string HrefToHtml(string href) => Regex.Replace(href, @"\.md$", ".html");

		// Emit dir/index.html as a copy of the landing page, so a static host serves "/" (and the logo's
		// href="/") without a directory listing or 404. No-op if the landing was not rendered in this dir.
		private static void CopyAsIndex(string dir, string landingHtml)
		{
			var src = Path.Combine(dir, landingHtml.Replace('/', Path.DirectorySeparatorChar));
			if (File.Exists(src)) File.Copy(src, Path.Combine(dir, "index.html"), overwrite: true);
		}

		// The docs write <PackageReference Include="..." /> with no Version (the repo builds under central
		// package management), so a standalone sample a reader copies needs one. Inject the configured
		// release version into OUR packages only (injecting it into a third-party reference would be wrong).
		private static readonly Regex OurPackageRef = new(
			@"(<PackageReference\s+Include=""(?:SnowBank|FoundationDB)[^""]*"")\s*/>",
			RegexOptions.Compiled);

		private static string InjectPackageVersions(string markdown, string version)
			=> OurPackageRef.Replace(markdown, $"$1 Version=\"{version}\" />");

		// A long code line with a trailing // comment forces a horizontal scrollbar. Move the comment to
		// its own line above the code when the combined line is too wide. Scoped to code fences whose
		// language uses // line comments (never the fdb-* DSL blocks), and string-aware so a // inside a
		// "http://..." literal is not mistaken for a comment.
		private const int MaxCodeLineLength = 88;
		private static readonly HashSet<string> SlashCommentLangs = new(StringComparer.OrdinalIgnoreCase)
		{
			"csharp", "cs", "c#", "c", "cpp", "c++", "js", "javascript", "ts", "typescript", "java", "go", "rust",
		};

		private static string ReflowCodeComments(string markdown)
		{
			var lines = markdown.Replace("\r\n", "\n").Split('\n');
			var outp = new List<string>(lines.Length);
			bool inFence = false, reflow = false;
			string fence = "";
			foreach (var line in lines)
			{
				var t = line.TrimStart();
				if (!inFence && (t.StartsWith("```") || t.StartsWith("~~~")))
				{
					inFence = true;
					fence = t.Substring(0, 3);
					var lang = t.Substring(3).Trim();
					int sp = lang.IndexOfAny(new[] { ' ', '\t' });
					if (sp >= 0) lang = lang.Substring(0, sp);
					reflow = SlashCommentLangs.Contains(lang);
					outp.Add(line);
				}
				else if (inFence && t.StartsWith(fence))
				{
					inFence = false; reflow = false;
					outp.Add(line);
				}
				else if (inFence && reflow && line.Length > MaxCodeLineLength)
				{
					int c = TrailingCommentPos(line);
					var codePart = c > 0 ? line.Substring(0, c).TrimEnd() : "";
					if (c > 0 && codePart.TrimStart().Length > 0)
					{
						var indent = line.Substring(0, line.Length - line.TrimStart().Length);
						outp.Add(indent + line.Substring(c).TrimEnd()); // comment on its own line, at code indent
						outp.Add(codePart);
					}
					else outp.Add(line);
				}
				else outp.Add(line);
			}
			return string.Join("\n", outp);
		}

		// Index of the first // that starts a line comment (outside string/char literals), or -1.
		private static int TrailingCommentPos(string line)
		{
			bool inStr = false;
			char q = '\0';
			for (int i = 0; i < line.Length - 1; i++)
			{
				char ch = line[i];
				if (inStr)
				{
					if (ch == '\\') { i++; continue; }
					if (ch == q) inStr = false;
				}
				else if (ch == '"' || ch == '\'') { inStr = true; q = ch; }
				else if (ch == '/' && line[i + 1] == '/') return i;
			}
			return -1;
		}

		// Turn an inline-code span into a link to the generated API reference: an exact `Type.Member`
		// (e.g. `FdbKey.FromBytes`, with or without a trailing argument list) links to the member anchor,
		// a bare `TypeName` (`FdbWatch`, `IFdbDatabase`) links to the type page. Spans already inside a
		// link are left alone.
		private static void AutoLinkSymbols(MarkdownObject root, IReadOnlyDictionary<string, string> byName, IReadOnlyDictionary<string, string> byMember)
		{
			foreach (var code in root.Descendants<CodeInline>().ToList())
			{
				if (code.Parent == null || AncestorIsLink(code) || InHeading(code)) continue;
				var text = code.Content;
				var bare = text; // drop a trailing "(...)" so `Type.Method(args)` still matches
				int paren = bare.IndexOf('(');
				if (paren >= 0) bare = bare[..paren].Trim();
				var href = Lookup(byMember, text, bare) ?? Lookup(byName, text, bare);
				if (href == null) continue;
				var link = new LinkInline(href, "");
				code.ReplaceBy(link);
				link.AppendChild(code);
			}
		}

		private static string? Lookup(IReadOnlyDictionary<string, string> map, string text, string bare)
			=> map.TryGetValue(text, out var a) ? a : map.TryGetValue(bare, out var b) ? b : null;

		private static bool AncestorIsLink(Inline inline)
		{
			for (var p = inline.Parent; p != null; p = p.Parent)
				if (p is LinkInline) return true;
			return false;
		}

		// True when the inline sits inside a heading (so we do not turn heading text into a link).
		private static bool InHeading(Inline inline)
		{
			var c = inline.Parent;
			if (c == null) return false;
			while (c.Parent != null) c = c.Parent; // climb to the root ContainerInline
			return c.ParentBlock is HeadingBlock;
		}

		// A translated heading gets a different auto id than its English source, which breaks every #anchor
		// link (whether the translator kept the English slug or localized it). Re-pin each French heading's
		// id to the English page's slug (paired by position), and rewrite any same-page anchor the translator
		// localized back to the English slug. Cross-page anchors keep the English slug, which now matches the
		// target French page's re-pinned heading id.
		private static void AlignFrenchAnchors(string enText, string frText, MarkdownDocument frDoc, MarkdownPipeline pipeline)
		{
			var enIds = Markdig.Markdown.Parse(enText, pipeline).Descendants<HeadingBlock>()
				.Select(h => h.GetAttributes().Id).ToList();
			var frHeads = frDoc.Descendants<HeadingBlock>().ToList();
			var frToEn = new Dictionary<string, string>(StringComparer.Ordinal);
			for (int i = 0; i < frHeads.Count && i < enIds.Count; i++)
			{
				var enId = enIds[i];
				if (enId == null) continue;
				var frId = frHeads[i].GetAttributes().Id;
				if (frId != null && frId != enId) frToEn[frId] = enId;
				frHeads[i].GetAttributes().Id = enId; // heading now carries the English slug
			}
			if (frToEn.Count == 0) return;
			foreach (var link in frDoc.Descendants<LinkInline>())
			{
				var url = link.Url;
				if (url != null && url.Length > 1 && url[0] == '#' && frToEn.TryGetValue(url.Substring(1), out var en))
					link.Url = "#" + en;
			}
		}

		// Rewrite inter-page markdown links (foo.md, ../bar.md#anchor) to .html, leaving the path relative
		// to the page (correct when served) and skipping absolute/external links.
		private static void RewriteMdLinks(MarkdownObject root)
		{
			foreach (var link in root.Descendants<LinkInline>())
			{
				var url = link.Url;
				if (string.IsNullOrEmpty(url)) continue;
				if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("mailto:") || url.StartsWith("//")) continue;
				link.Url = Regex.Replace(url, @"\.md(#|$)", ".html$1");
			}
		}

		// ---- build-time search index ----
		private sealed record SearchHeading(string T, string A);
		private sealed record SearchDoc(string Url, string Title, List<SearchHeading> Headings, string Text);

		private static List<SearchHeading> PageHeadings(MarkdownDocument doc)
		{
			var list = new List<SearchHeading>();
			foreach (var h in doc.Descendants<HeadingBlock>())
			{
				if (h.Level != 2 && h.Level != 3) continue;
				var id = h.GetAttributes().Id;
				if (string.IsNullOrEmpty(id)) continue;
				list.Add(new SearchHeading(InlineText(h.Inline), id));
			}
			return list;
		}

		private static string PlainText(MarkdownDocument doc)
		{
			var sb = new StringBuilder();
			foreach (var node in doc.Descendants())
			{
				switch (node)
				{
					case LiteralInline lit: sb.Append(lit.Content.ToString()).Append(' '); break;
					case CodeInline code: sb.Append(code.Content).Append(' '); break;
					case HtmlBlock hb when hb.Lines.Count > 0: // API member tables are raw HTML; index their text
						for (int i = 0; i < hb.Lines.Count; i++) sb.Append(Regex.Replace(hb.Lines.Lines[i].Slice.ToString(), "<[^>]+>", " ")).Append(' ');
						break;
					case CodeBlock cb when cb.Lines.Count > 0:
						for (int i = 0; i < cb.Lines.Count; i++) sb.Append(cb.Lines.Lines[i].Slice.ToString()).Append(' ');
						break;
				}
			}
			var text = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
			return text.Length > 12000 ? text.Substring(0, 12000) : text; // bound the per-page index size
		}

		// The right-hand "In this article" panel: the page's h2/h3 headings as static anchor links.
		// Static by design, no scroll-spy script, so the reflow-free guarantee holds.
		private static string BuildOnThisPage(MarkdownDocument doc)
		{
			var items = new StringBuilder();
			int n = 0;
			foreach (var h in doc.Descendants<HeadingBlock>())
			{
				if (h.Level != 2 && h.Level != 3) continue;
				var id = h.GetAttributes().Id;
				if (string.IsNullOrEmpty(id)) continue;
				items.Append($"<li class=\"level{h.Level}\"><a href=\"#{id}\">{EscHtml(InlineText(h.Inline))}</a></li>");
				n++;
			}
			return n == 0 ? "" : $"<h5>In this article</h5><ul>{items}</ul>";
		}

		private static string InlineText(ContainerInline? inline)
		{
			if (inline == null) return "";
			var sb = new StringBuilder();
			foreach (var node in inline.Descendants())
			{
				if (node is LiteralInline lit) sb.Append(lit.Content.ToString());
				else if (node is CodeInline code) sb.Append(code.Content);
			}
			return sb.ToString();
		}

		private static string RenderBody(MarkdownDocument doc, MarkdownPipeline pipeline)
		{
			var sw = new StringWriter();
			var renderer = new HtmlRenderer(sw);
			pipeline.Setup(renderer);
			renderer.Render(doc);
			sw.Flush();
			return sw.ToString();
		}

		private static void Check(bool ok, string what)
			=> Console.WriteLine($"  [{(ok ? "ok" : "FAIL")}] {what}");

		private static MarkdownPipeline BuildPipeline(IReadOnlyDictionary<string, FenceRenderer> renderers, Func<string, string, string?> highlight)
		{
			var builder = new MarkdownPipelineBuilder().UseAdvancedExtensions();
			// wraps the default code-block renderer: custom fences emit bespoke HTML, languaged fences get
			// the Shiki-highlighted HTML, anything else falls back to a plain <pre>
			builder.Extensions.Add(new FencedBlockExtension(renderers, highlight));
			// upgrades raw <pre> ASCII box art to Unicode box-drawing characters at build time
			builder.Extensions.Add(new BoxArtExtension());
			return builder.Build();
		}

		// ---- nav from toc.yml ----
		private sealed class TocNode
		{
			public string? Name { get; set; }
			public string? NameFr { get; set; } // French nav label (yaml key "nameFr"); falls back to Name
			public string? Href { get; set; }
			public List<TocNode>? Items { get; set; }
			public bool Collapsed { get; set; } // render <details> without "open" unless it holds the current page
		}

		private static bool SubtreeHasHref(TocNode n, string cur)
		{
			if (n.Href == cur) return true;
			if (n.Items != null) foreach (var c in n.Items) if (SubtreeHasHref(c, cur)) return true;
			return false;
		}

		private static string BuildNav(List<TocNode> nodes, string currentHref, bool french)
		{
			var sb = new StringBuilder();
			RenderNav(nodes, currentHref, sb, 0, french);
			return sb.ToString();
		}

		// Left padding steps in with real nesting depth, so a link and a section header at the same level
		// line up and a deeper level always sits further in (the diataxis tree is 3 levels in places).
		private static string Indent(int depth)
			=> (0.7 + depth * 1.0).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "rem";

		private static void RenderNav(List<TocNode> nodes, string cur, StringBuilder sb, int depth, bool french)
		{
			sb.Append("<ul>");
			foreach (var n in nodes)
			{
				bool hasChildren = n.Items is { Count: > 0 };
				string name = EscHtml((french ? (n.NameFr ?? n.Name) : n.Name) ?? "");
				var pad = $" style=\"padding-left:{Indent(depth)}\"";
				if (hasChildren)
				{
					// Every folder is a collapsible chevron section, whether or not it also has a landing
					// page. The landing is reached via the folder's "Overview" child, so the summary only
					// toggles and never carries the active highlight, which is why the overview page was
					// lighting up both the folder and its Overview entry. open unless marked collapsed and
					// the current page is not inside it.
					bool open = !n.Collapsed || SubtreeHasHref(n, cur);
					sb.Append($"<li class=\"toc-section\"><details{(open ? " open" : "")}><summary{pad}>{name}</summary>");
					RenderNav(n.Items!, cur, sb, depth + 1, french);
					sb.Append("</details></li>");
				}
				else
				{
					string active = n.Href == cur ? " class=\"active\"" : "";
					sb.Append($"<li{active}><a{pad} href=\"{Href(n.Href, french)}\" title=\"{name}\">{name}</a></li>");
				}
			}
			sb.Append("</ul>");
		}

		// Nav links are root-absolute so the one shared sidebar resolves from any page depth.
		private static string Href(string? md, bool french)
			{
				if (md == null) return "#";
				var html = "/" + Regex.Replace(md, @"\.md$", ".html");
				return french && !md.StartsWith("api/", StringComparison.Ordinal) ? "/fr" + html : html;
			}

		private static string EscHtml(string s)
			=> s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

		private static string FindRoot()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Documentation", "docgen.json")))
				dir = dir.Parent;
			return dir?.FullName ?? throw new InvalidOperationException("could not find a repo root with Documentation/docgen.json; pass --root <dir>");
		}

		// The tool's own project dir (SnowBank.DocGen/), so its bundled assets and node scripts resolve
		// whether the tool documents its own repo or a parent repo pointed at by --root.
		private static string ToolDir()
		{
			var dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SnowBank.DocGen.csproj")))
				dir = dir.Parent;
			return dir?.FullName ?? AppContext.BaseDirectory;
		}

		// Per-repo doc config (Documentation/docgen.json): the release version stamped into sample
		// PackageReferences, the TFM and bin roots to find built assemblies under, and the assembly names
		// whose public API becomes reference pages. The names live in each repo, not in the tool source.
		private sealed class DocGenConfig
		{
			public string Version { get; set; } = "0.0.0";
			public string ApiTfm { get; set; } = "net10.0";
			public List<string> BinRoots { get; set; } = new() { "artifacts/bin" };
			public List<string> Assemblies { get; set; } = new();

			public static DocGenConfig Load(string docsDir)
			{
				var path = Path.Combine(docsDir, "docgen.json");
				if (!File.Exists(path))
				{
					Console.Error.WriteLine($"  [config] no docgen.json in {docsDir}; API reference disabled, version 0.0.0");
					return new DocGenConfig();
				}
				var cfg = JsonSerializer.Deserialize<DocGenConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new DocGenConfig();
				if (cfg.BinRoots.Count == 0) cfg.BinRoots.Add("artifacts/bin");
				return cfg;
			}
		}

		// Find a configured assembly's built dll (first matching bin root) with its sibling xml docs.
		private static (string? Dll, string? Xml) ResolveAssembly(string root, DocGenConfig config, string name)
		{
			foreach (var br in config.BinRoots)
			{
				var dll = Path.Combine(root, br.Replace('/', Path.DirectorySeparatorChar), name, "release_" + config.ApiTfm, name + ".dll");
				if (File.Exists(dll)) return (dll, Path.ChangeExtension(dll, ".xml"));
			}
			Console.Error.WriteLine($"  [api] '{name}' not found under {string.Join(", ", config.BinRoots)} (release_{config.ApiTfm})");
			return (null, null);
		}

		// ---- tiny static file server (the preview pane blocks file://) ----
		private static void Serve(string dir, int port)
		{
			var listener = new HttpListener();
			listener.Prefixes.Add($"http://localhost:{port}/");
			listener.Start();
			Console.WriteLine($"serving {dir} on http://localhost:{port}/  (Ctrl+C to stop)");
			while (true)
			{
				var ctx = listener.GetContext();
				var rel = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath).TrimStart('/');
				if (rel.Length == 0) rel = "introduction.html";
				else if (rel is "fr" or "fr/") rel = "fr/introduction.html"; // French home
				var file = Path.GetFullPath(Path.Combine(dir, rel.Replace('/', Path.DirectorySeparatorChar)));
				if (file.StartsWith(Path.GetFullPath(dir), StringComparison.Ordinal) && File.Exists(file))
				{
					var bytes = File.ReadAllBytes(file);
					ctx.Response.ContentType = ContentType(file);
					ctx.Response.ContentLength64 = bytes.Length;
					ctx.Response.OutputStream.Write(bytes);
				}
				else
				{
					ctx.Response.StatusCode = 404;
				}
				ctx.Response.Close();
			}
		}

		private static string ContentType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
		{
			".html" => "text/html; charset=utf-8",
			".css" => "text/css; charset=utf-8",
			".js" => "text/javascript; charset=utf-8",
			".json" => "application/json; charset=utf-8",
			".svg" => "image/svg+xml",
			".png" => "image/png",
			_ => "application/octet-stream",
		};
	}
}
