#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 	* Redistributions of source code must retain the above copyright
// 	  notice, this list of conditions and the following disclaimer.
// 	* Redistributions in binary form must reproduce the above copyright
// 	  notice, this list of conditions and the following disclaimer in the
// 	  documentation and/or other materials provided with the distribution.
// 	* Neither the name of SnowBank nor the
// 	  names of its contributors may be used to endorse or promote products
// 	  derived from this software without specific prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable UnusedMember.Global

namespace SnowBank.Serialization.Json.CodeGen
{
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Text;
	using Microsoft.CodeAnalysis.CSharp;

	/// <summary>Basic C# code source builder</summary>
	[DebuggerDisplay("Length={Output.Length}")]
	public sealed class CSharpCodeBuilder
	{

		public readonly StringBuilder Output = new();

		public readonly Stack<string?> Structure = [ ];

		/// <summary>Current indentation depth</summary>
		public int Depth { get; private set; }

		/// <summary>Clear the buffer, so that it can be reused</summary>
		public void Clear()
		{
			this.Depth = 0;
			this.Output.Clear();
		}

		/// <summary>Returns the source code written so far, and clear the builder for reuse</summary>
		public string ToStringAndClear()
		{
			if (this.Depth != 0) throw new InvalidOperationException("Code seems to be truncated");
			var s = this.Output.ToString();
			Clear();
			return s;
		}

		/// <inheritdoc />
		public override string ToString() => this.Output.ToString();

		/// <summary>Writes a line of code</summary>
		public void AppendLine(string text)
		{
			this.Output.Append('\t', this.Depth).AppendLine(text);
		}

		/// <summary>Writes an empty line</summary>
		public void NewLine()
		{
			this.Output.AppendLine();
		}

		/// <summary>Writes multiple lines of code</summary>
		/// <remarks>This method will automatically add the correct indentation for all the lines</remarks>
		public void WriteLines(string text)
		{
			// we need to parse the text and insert the proper indentation
			//TODO: optimized version later (the roslyn implementation uses readonly spans to split each line
			
			if (text.Contains("\n"))
			{
				foreach (var line in text.Split(CSharpCodeBuilder.LineBreakChars, StringSplitOptions.RemoveEmptyEntries))
				{
					this.Output.Append('\t', this.Depth).AppendLine(line.TrimEnd());
				}
			}
			else
			{
				this.Output.Append('\t', this.Depth).AppendLine(text);
			}
		}

		/// <summary>Encodes a value into the corresponding C# string literal, with quotes and proper escaping</summary>
		public static string Constant(string literal) => SymbolDisplay.FormatLiteral(literal, quote: true);

		/// <summary>Encodes a value into the corresponding C# boolean literal</summary>
		public static string Constant(bool literal) => literal ? "true" : "false";

		/// <summary>Encodes a value into the corresponding C# char literal, with quotes and proper escaping</summary>
		public static string Constant(char c) => SymbolDisplay.FormatLiteral(c, quote: true);

		/// <summary>Encodes a value into the corresponding C# number literal</summary>
		public static string Constant(int literal) => SymbolDisplay.FormatPrimitive(literal, false, false)!;

		/// <summary>Encodes a value into the corresponding C# number literal</summary>
		public static string Constant(long literal) => SymbolDisplay.FormatPrimitive(literal, false, false)!;

		/// <summary>Encodes a value into the corresponding C# number literal</summary>
		public static string Constant(float literal) => float.IsNaN(literal) ? "float.NaN" : SymbolDisplay.FormatPrimitive(literal, false, false)!;

		/// <summary>Encodes a value into the corresponding C# number literal</summary>
		public static string Constant(double literal) => double.IsNaN(literal) ? "double.NaN" : SymbolDisplay.FormatPrimitive(literal, false, false)!;

		/// <summary>Encodes a value into the corresponding C# Guid construction</summary>
		public static string Constant(Guid literal)
		{
			if (literal == Guid.Empty) return nameof(Guid) + "." + nameof(Guid.Empty);

			//note: we are targeting .NET 8+ where Guid as a ctor that takes a ReadOnlySpan<byte>
			var bytes = literal.ToByteArray();
			return $"new Guid([ {string.Join(", ", bytes)} ])";
		}

		/// <summary>Encodes a value into the corresponding C# DateTime construction</summary>
		public static string Constant(DateTime literal)
		{
			if (literal == DateTime.MinValue) return "DateTime.MinValue";
			if (literal == DateTime.MaxValue) return "DateTime.MaxValue";

			switch (literal.Kind)
			{
				case DateTimeKind.Utc: return $"new DateTime({literal.Ticks}L, DateTimeKind.Utc)";
				case DateTimeKind.Local: return $"new DateTime({literal.Ticks}L, DateTimeKind.Local)";
				default: return $"new DateTime({literal.Ticks}L)";
			}
		}

		/// <summary>Encodes a value into the corresponding C# DateTimeOffset literal</summary>
		public static string Constant(DateTimeOffset literal)
		{
			if (literal == DateTimeOffset.MinValue) return "DateTimeOffset.MinValue";
			if (literal == DateTimeOffset.MaxValue) return "DateTimeOffset.MaxValue";

			if (literal.Offset == TimeSpan.Zero)
			{
				return $"new DateTimeOffset({literal.Ticks}L, TimeSpan.Zero)";
			}
			return $"new DateTimeOffset({literal.Ticks}L, new TimeSpan({literal.Offset.Ticks}L))";
		}

		/// <summary>Encodes a value into the corresponding C# enum literal</summary>
		public static string Constant<TEnum>(TEnum literal) where TEnum : Enum
		{
			return TypeName<TEnum>() + "." + literal.ToString("G");
		}

		/// <summary>Encodes a value into the corresponding C# representation</summary>
		public static string Constant(Type type, object? boxed)
		{
			return boxed switch
			{
				null => type.IsValueType ? "default" : "null",
				string s => Constant(s),
				bool b => Constant(b),
				int i => Constant(i),
				long l => Constant(l),
				float f => Constant(f),
				double d => Constant(d),
				char c => Constant(c),
				Guid g => Constant(g),
				DateTime dt => Constant(dt),
				DateTimeOffset dto => Constant(dto),
				_ => throw new NotSupportedException($"Does not know how to transform value '{boxed}' of type {type.Name} into a C# constant")
			};
		}

		/// <summary>Returns the most appropriate c# literal for the default value of this type (<c>null</c>, <c>0</c>, <c>false</c>, <c>default</c>, <c>Guid.Empty</c>, <c>TimeSpan.Zero</c>, ...)</summary>
		public static string DefaultOf(Type type)
		{
			if (type == typeof(string)) return "null";
			if (type == typeof(bool)) return "false";
			if (type == typeof(int)) return "0";
			if (type == typeof(long)) return "0L";
			if (type == typeof(uint)) return "0U";
			if (type == typeof(ulong)) return "0UL";
			if (type == typeof(float)) return "0f";
			if (type == typeof(double)) return "0d";
			if (type == typeof(decimal)) return "0m";
			if (type == typeof(char)) return "'\0'";
			if (type == typeof(Guid)) return "Guid.Empty";
			if (type == typeof(DateTime)) return "DateTime.MinValue";
			if (type == typeof(DateTimeOffset)) return "DateTimeOffset.MinValue";
			if (type == typeof(TimeSpan)) return "TimeSpan.Zero";
			if (type == typeof(byte)) return "(byte) 0"; // not sure if there is more direct way?
			if (type == typeof(IntPtr)) return "IntPtr.Zero";
			if (type == typeof(UIntPtr)) return "UIntPtr.Zero";
			if (!type.IsValueType || type.IsNullableType())
			{
				return "null";
			}
			return "default";
		}

		public static string DefaultOf<T>() => DefaultOf(typeof(T));

		/// <summary>Tests if this type can be written without the namespace</summary>
		public static bool CanUseShortName(Type t)
		{
			if (t.Namespace == KnownTypeSymbols.CrystalJsonNamespace) return true;
			return false;
		}

		/// <summary>Returns the canonical name of this type, which can be reference in the generated code without ambiguity</summary>
		public static string TypeName(Type t) => CanUseShortName(t)
			? TypeHelper.GetCompilableTypeName(t, omitNamespace: true, global: false)
			: TypeHelper.GetCompilableTypeName(t, omitNamespace: false, global: true);

		/// <summary>Returns the canonical name of this type, which can be reference in the generated code without ambiguity</summary>
		public static string TypeName<T>() => TypeName(typeof(T));

		public static string TypeNameGeneric(Type genericType, params string[] arguments)
		{
			var name = CanUseShortName(genericType) ? genericType.Name : ("global::" + genericType.FullName!);
			var suffix = "`" + arguments.Length;
			if (!name.EndsWith(suffix)) throw new InvalidOleVariantTypeException("genericType type argument count mismatch");
			name = name.Substring(0, name.Length - suffix.Length);
			return $"{name}<{string.Join(", ", arguments)}>";
		}

		public static string Parameter(string type, string name, bool nullable = false) => !nullable ? $"{type} {name}" : $"{type}? {name} = default";

		public static string Parameter<T>(string name, bool nullable = false) => Parameter(TypeName<T>(), name, nullable);

		public static string MethodName(Type parent, string name) => TypeName(parent) + "." + name;

		public static string MethodName<T>(string name) => TypeName<T>() + "." + name;

		public static string Singleton(Type type, string name) => TypeName(type) + "." + name;

		public static string Singleton<T>(string name) => TypeName<T>() + "." + name;

		public static string EscapeCref(string typeName) => typeName.Replace("global::", "T:").Replace('<', '{').Replace('>', '}');

		public static string EscapeCref(string typeName, string memberName) => typeName.Replace("<global::", "{T:").Replace("global::", "").Replace('<', '{').Replace('>', '}') + "." + memberName;

		public void EnterBlock(string? type = null, string? comment = null)
		{
			this.Output.Append('\t', this.Depth).AppendLine(comment == null ? "{" : "{ // " + comment);
			this.Structure.Push(type);
			++this.Depth;
		}

		public void LeaveBlock(string? type = null, char suffix = '\0')
		{
			var expected = this.Structure.Count > 0 ? this.Structure.Peek() : null;
			if (expected != null && expected != type)
			{
				throw new InvalidOperationException($"Code structure mismatch: cannot leave '{type}' while inside a '{expected}'");
			}

			--this.Depth;
			this.Structure.Pop();
			this.Output.Append('\t', this.Depth).AppendLine(suffix != '\0' ? ("}" + suffix) : "}");
		}

		public void Block(Action statement)
		{
			this.EnterBlock("block");
			statement();
			this.LeaveBlock("block");
		}

		public void EnterCollection(string? type = null, string? comment = null)
		{
			this.Output.Append('\t', this.Depth).AppendLine(comment == null ? "[" : "[ // " + comment);
			this.Structure.Push(type);
			++this.Depth;
		}

		public void LeaveCollection(string? type = null, char suffix = '\0')
		{
			var expected = this.Structure.Count > 0 ? this.Structure.Peek() : null;
			if (expected != null && expected != type)
			{
				throw new InvalidOperationException($"Code structure mismatch: cannot leave '{type}' while inside a '{expected}'");
			}

			--this.Depth;
			this.Structure.Pop();
			this.Output.Append('\t', this.Depth).AppendLine(suffix != '\0' ? ("]" + suffix) : "]");
		}

		public void Call(string method, params string[] args)
		{
			this.AppendLine($"{method}({string.Join(", ", args)});");
		}

		public void Return()
		{
			this.AppendLine("return;");
		}

		public void Return(string expr)
		{
			this.AppendLine($"return {expr};");
		}

		public void WriteNamespace(string name)
		{
			this.AppendLine($"namespace {name}");
		}

		public void Namespace(string name, Action block)
		{
			this.WriteNamespace(name);
			this.EnterBlock("namespace:" + name);
			block();
			this.LeaveBlock("namespace:" + name);
		}

		public void WriteUsing(string name, string? alias = null)
		{
			if (alias == null)
			{
				this.AppendLine($"using {name};");
			}
			else
			{
				this.AppendLine($"using {alias} = {name};");
			}
		}

		public void Class(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} class {name} : {string.Join(", ", implements)}");
			}
			else
			{
				this.AppendLine($"{modifiers} class {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("class:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("class:" + name);
			this.NewLine();
		}

		public void Record(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} record {name} : {string.Join(", ", implements)}");
			}
			else
			{
				this.AppendLine($"{modifiers} record {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("record:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("record:" + name);
			this.NewLine();
		}

		public void Struct(string modifiers, string name, string[] implements, string[] where, Action block)
		{
			if (implements.Length > 0)
			{
				this.AppendLine($"{modifiers} struct {name} : {string.Join(", ", implements)}");
			}
			else
			{
				this.AppendLine($"{modifiers} struct {name}");
			}

			foreach (var w in where)
			{
				this.AppendLine($"\twhere {w}");
			}

			this.EnterBlock("struct:" + name);
			this.NewLine();

			block();

			this.LeaveBlock("struct:" + name);
			this.NewLine();
		}

		public void Method(string modifiers, string returnType, string name, string[] parameters, Action block)
		{
			this.AppendLine($"{modifiers} {returnType} {name}({string.Join(", ", parameters)})");
			this.EnterBlock("method:" + name);
			block();
			this.LeaveBlock("method:" + name);
			this.NewLine();
		}

		private static readonly char[] LineBreakChars = "\r\n".ToCharArray();

		public void BeginRegion(string name)
		{
			this.AppendLine($"#region {name}");
		}

		public void EndRegion(string? name = null)
		{
			if (name == null)
			{
				this.AppendLine("#endregion");
			}
			else
			{
				this.AppendLine($"#endregion {name}");
			}
		}

		public void Comment(string comment)
		{
			if (comment.Contains("\n"))
			{
				foreach (var line in comment.Split(CSharpCodeBuilder.LineBreakChars, StringSplitOptions.RemoveEmptyEntries))
				{
					this.Output.Append('\t', this.Depth).Append("// ").AppendLine(line.TrimEnd());
				}
			}
			else
			{
				this.Output.Append('\t', this.Depth).Append("// ").AppendLine(comment.TrimEnd());
			}
		}

		public void XmlComment(string comment)
		{
			if (comment.Contains("\n"))
			{
				foreach (var line in comment.Split(CSharpCodeBuilder.LineBreakChars, StringSplitOptions.RemoveEmptyEntries))
				{
					this.Output.Append('\t', this.Depth).Append("/// ").AppendLine(line.TrimEnd());
				}
			}
			else
			{
				this.Output.Append('\t', this.Depth).Append("/// ").AppendLine(comment.TrimEnd());
			}
		}

		public void InheritDoc(string? cref = null)
		{
			if (cref == null)
			{
				AppendLine("/// <inheritdoc />");
			}
			else
			{
				AppendLine($"/// <inheritdoc cref=\"{cref}\" />");
			}
		}

		public void If(string conditionText, Action thenBlock, Action? elseBock = null)
		{
			this.AppendLine($"if ({conditionText})");

			this.EnterBlock("then:" + conditionText);
			thenBlock();
			this.LeaveBlock("then:" + conditionText);

			if (elseBock != null)
			{
				this.EnterBlock("else:" + conditionText);
				elseBock();
				this.LeaveBlock("else:" + conditionText);
			}
		}

		public void Ternary(string conditionText, string ifTrue, string ifFalse)
		{
			this.Output.Append($"{conditionText} ? {ifTrue} : {ifFalse}");
		}

		public void Attribute(string name, string[]? args = null, (string Name, string Value)[]? extras = null)
		{
			args ??= [ ];
			extras ??= [ ];

			if (extras.Length == 0)
			{
				this.AppendLine($"[{name}({string.Join(", ", args)})]");
			}
			else
			{
				var sb = new StringBuilder();
				sb.Append('[').Append(name).Append('(');
				for (int i = 0; i < args.Length; i++)
				{
					if (i != 0) sb.Append(", ");
					sb.Append(args[i]);
				}
				foreach (var kv in extras)
				{
					sb.Append($", {kv.Name} = {kv.Value}");
				}
				sb.Append(")]");
				this.AppendLine(sb.ToString());
			}
		}

		public void Attribute<TAttribute>(string[]? args = null, (string Name, string Value)[]? extras = null)
			where TAttribute : Attribute
		{
			var name = TypeName<TAttribute>();
			if (name.EndsWith("Attribute")) name = name.Substring(0, name.Length -"Attribute".Length);
			this.Attribute(name, args, extras);
		}

	}

}
