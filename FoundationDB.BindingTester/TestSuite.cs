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

namespace FoundationDB.Client.Testing
{
	using System.IO;

	public sealed record TestSuite
	{
		public required string Name { get; init; }

		public required Slice Prefix { get; init; }

		public required TestInstruction[] Instructions { get; init; }

	}

	public sealed record TestSuiteBuilder
	{

		public TestSuiteBuilder(string name = "test", Slice prefix = default)
		{
			this.Name = name;
			this.Prefix = prefix;
		}

		public string Name { get; set; }

		public Slice Prefix { get; set; }

		public List<TestInstruction> Instructions { get; } = new();

		public TestSuiteBuilder WithName(string name)
		{
			this.Name = name;
			return this;
		}

		public TestSuiteBuilder WithPrefix(Slice prefix)
		{
			this.Prefix = prefix;
			return this;
		}

		private static char ParseEscapedUnicodeCharacter(ReadOnlySpan<char> quad)
		{
			int x = 0;
			for (int i = 0; i < quad.Length; i++)
			{
				char c = quad[i];
				x <<= 4;
				x |= c switch
				{
					>= '0' and <= '9' => (c - 48),
					>= 'A' and <= 'F' => (c - 55),
					>= 'a' and <= 'f' => (c - 87),
					_ => throw new FormatException("Invalid Unicode character escaping")
				};
			}
			return (char) x;
		}

		private static Slice ParseBinaryLiteral(ReadOnlySpan<char> input)
		{
			//TODO: BUGBUG:
			char quote = input[0];
			if (input[^1] != quote)
			{
				throw new FormatException($"Invalid binary literal: {input.ToString()}");
			}

			var literal = input[1..^1];
			var sw = new SliceWriter(literal.Length);
			for (int i = 0; i < literal.Length; i++)
			{
				char c = literal[i];
				if (c != '\\')
				{
					sw.WriteByte((byte) c);
					continue;
				}

				++i;
				c = literal[i];

				switch (c)
				{
					case '0':
					{ // NUL
						sw.WriteByte(0);
						break;
					}
					case 'x':
					{ // ASCII
						var x = literal.Slice(i + 1, 2);
						var xx = ParseEscapedUnicodeCharacter(x);
						sw.WriteByte((byte) xx);
						i += 2;
						break;
					}
					case 'u':
					{ // UNICODE
						var x = literal.Slice(i + 1, 4);
						var xx = ParseEscapedUnicodeCharacter(x);
						if (xx > 255) throw new FormatException("Unexpected unicode character in escaped binary literal");
						sw.WriteByte((byte) xx);
						i += 4;
						break;
					}
					case 't':
					{ // TAB
						sw.WriteByte('\t');
						break;
					}
					case 'r':
					{ // TAB
						sw.WriteByte('\r');
						break;
					}
					case 'n':
					{ // TAB
						sw.WriteByte('\n');
						break;
					}
					case 'b':
					{ // BELL
						sw.WriteByte('\b');
						break;
					}
					case 'f':
					{ // FORM FEED
						sw.WriteByte('\f');
						break;
					}
					case '\\' or '\'' or '"':
					{ // TAB
						sw.WriteByte(c);
						break;
					}
					default:
					{
						throw new FormatException("Invalid binary token");
					}
				}

			}
			return sw.ToSlice();
		}

		private static ReadOnlySpan<char> SubstringBefore(ReadOnlySpan<char> literal, char c, out int p)
		{
			p = literal.IndexOf(c);
			return p < 0 ? literal : literal[..p];
		}

		private static ReadOnlySpan<char> SubstringAfter(ReadOnlySpan<char> literal, char c, out int p)
		{
			p = literal.IndexOf(c);
			return p < 0 ? literal : literal[(p + 1)..];
		}

		public static TestSuiteBuilder ParseTestDump(string path)
		{
			var name = "unknown";
			var prefix = TuPack.EncodeKey("binding_tester");
			TestSuiteBuilder? builder = null;
			foreach(var l in File.ReadAllLines(path))
			{
				if (l.Length == 0) continue;
				var line = l.AsSpan();

				if (!line.StartsWith("  "))
				{
					if (line[0] == 'G' && line.StartsWith("Generating "))
					{ // "Generating ### test at seed ### with ### op(s) and ### concurrent tester(s)..."
						var x = line["Generating ".Length..];
						name = SubstringBefore(x, ' ', out _).ToString();
					}
					else if (line[0] == 'T' && line.StartsWith("Thread at prefix b"))
					{ // "Thread at prefix b'###':"
						var tok = SubstringBefore(line["Thread at prefix b".Length..], ':', out _);
						prefix = ParseBinaryLiteral(tok);
					}
					continue;
				}

				builder ??= new(name, prefix);

				// ex: "  38. 'PUSH' b'P\x1e\x1c\xc7\xbav\x9f\x0ck-j\xf2\xf49\x0f\x07s\xad\xc7\x84\xc0a'"
				var s = line.Trim();
				var numLit = SubstringBefore(s, '.', out int p);
				var num = int.Parse(numLit.Trim());
				s = s[(p + 3)..]; // skip ". '"
				var cmd = SubstringBefore(s, '\'', out p).ToString();
				s = s[(p + 1)..].Trim();

				IVarTuple val;
				if (s.Length > 0)
				{
					char c = s[0];
					if (c == 'b')
					{
						Slice lit;
						if (s[1] is '\'' or '"')
						{
							lit = ParseBinaryLiteral(s[1..]);
						}
						else
						{
							throw new FormatException("Unsupported binary literal: " + s.ToString());
						}
						val = STuple.Create(cmd, lit);
					}
					else if (char.IsDigit(c) || c == '-')
					{
						int x = int.Parse(s);
						val = STuple.Create(cmd, x);
					}
					else
					{
						throw new FormatException("Unsupported argument: " + s.ToString());
					}
				}
				else
				{
					val = STuple.Create(cmd);
				}

				builder.Instructions.Add(TestInstruction.Parse(val));
			}

			if (builder == null) throw new InvalidOperationException("Failed to load test suite");

			return builder;
		}

		public TestSuite Build()
		{
			if (string.IsNullOrWhiteSpace(this.Name)) throw new InvalidOperationException("You must specify a test name");
			if (this.Prefix.IsNullOrEmpty) throw new InvalidOperationException("You must specify a test prefix");
			return new() { Name = this.Name, Prefix = this.Prefix.Copy(), Instructions = this.Instructions.ToArray() };
		}

		public TestSuiteBuilder Push<T>(T item)
		{
			this.Instructions.Add(TestInstruction.Push(item));
			return this;
		}

		public TestSuiteBuilder Pop()
		{
			this.Instructions.Add(TestInstruction.Pop());
			return this;
		}

		public TestSuiteBuilder Dup()
		{
			this.Instructions.Add(TestInstruction.Dup());
			return this;
		}

		public TestSuiteBuilder Swap(int depth)
		{
			this.Instructions.Add(TestInstruction.Push(depth));
			this.Instructions.Add(TestInstruction.Swap());
			return this;
		}

		public TestSuiteBuilder Sub()
		{
			this.Instructions.Add(TestInstruction.Sub());
			return this;
		}

		public TestSuiteBuilder Concat()
		{
			this.Instructions.Add(TestInstruction.Concat());
			return this;
		}

		public TestSuiteBuilder LogStack(Slice prefix)
		{
			this.Instructions.Add(TestInstruction.Push(prefix));
			this.Instructions.Add(TestInstruction.LogStack());
			return this;
		}

		public TestSuiteBuilder UseTransaction(string name)
		{
			this.Instructions.Add(TestInstruction.Push(name));
			this.Instructions.Add(TestInstruction.UseTransaction());
			return this;
		}

		public TestSuiteBuilder NewTransaction()
		{
			this.Instructions.Add(TestInstruction.NewTransaction());
			return this;
		}

		public TestSuiteBuilder Get(Slice key)
		{
			this.Instructions.Add(TestInstruction.Push(key));
			this.Instructions.Add(TestInstruction.Get());
			return this;
		}

		public TestSuiteBuilder Set(Slice key, Slice value)
		{
			this.Instructions.Add(TestInstruction.Push(value));
			this.Instructions.Add(TestInstruction.Push(key));
			this.Instructions.Add(TestInstruction.Set());
			return this;
		}

		public TestSuiteBuilder Clear(Slice key)
		{
			this.Instructions.Add(TestInstruction.Push(key));
			this.Instructions.Add(TestInstruction.Clear());
			return this;
		}

	}

}
