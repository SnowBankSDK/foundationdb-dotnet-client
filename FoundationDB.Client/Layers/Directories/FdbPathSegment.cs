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

namespace FoundationDB.Client
{
	using SnowBank.Buffers.Text;

	/// <summary>Represent a segment in a <see cref="FdbPath">path</see> to a <see cref="IFdbDirectory">Directory</see>.</summary>
	/// <remark>A path segment is composed of a <see cref="Name"/> and optional <see cref="LayerId"/> field.</remark>
	public readonly struct FdbPathSegment : IEquatable<FdbPathSegment>, IComparable<FdbPathSegment>, ISpanFormattable
	{

		// Rules for encoding a segment into a string: '/', '\', '[' and ']' are escaped by prefixing them by another '\'
		// - (Name: "Hello", LayerId: "") => "Hello"
		// - (Name: "Hello", LayerId: "Foo") => "Hello[Foo]"
		// - (Name: "Hello/World", "") => "Hello\/World"
		// - (Name: "Hello[World]", "") => "Hello\[World\]"
		// - (Name: "Hello[World]", "Foo") => "Hello\[World\][Foo]"
		// - (Name: "Hello, "Foo/Bar") => "Hello[Foo\/Bar]"
		// - (Name: "Hello, "Foo[Bar]") => "Hello[Foo\[Bar\]]"

		/// <summary>Name of the segment</summary>
		/// <remarks>This is the equivalent of the name of the "folder"</remarks>
		public readonly string Name;

		/// <summary>Id of the layer used by the corresponding directory; or <c>null</c> if it is not specified</summary>
		/// <remarks>If present: when opening a directory, its LayerId will be compared to this value; when creating a directory, this value will be used as its LayerId.</remarks>
		public readonly string LayerId;

		/// <summary>Empty segment</summary>
		public static readonly FdbPathSegment Empty = default;

		public FdbPathSegment(string name, string? layerId = null)
		{
			this.Name = name;
			this.LayerId = layerId ?? string.Empty;
		}

		public bool IsEmpty => string.IsNullOrEmpty(this.Name);

#if NET8_0_OR_GREATER
		private static readonly System.Buffers.SearchValues<char> EscapedLiterals = System.Buffers.SearchValues.Create("\\/[]");
#else
		private static readonly char[] EscapedLiterals = "\\/[]".ToCharArray();
#endif

		private static string Escape(string value)
		{
			if (value.IndexOfAny(EscapedLiterals) >= 0)
			{
				return EscapeSlow(value);
			}
			return value;

			[MethodImpl(MethodImplOptions.NoInlining)]
			static string EscapeSlow(string value)
			{
				Span<char> tmp = value.Length <= 128 ? stackalloc char[value.Length * 2] : new char[value.Length * 2];
				TryEscapeToSlow(tmp, out int len, value);
				return tmp[..len].ToString();
			}
		}

		private static void EscapeTo(ref FastStringBuilder sb, ReadOnlySpan<char> value)
		{
			if (value.IndexOfAny(EscapedLiterals) >= 0)
			{
				EscapeToSlow(ref sb, value);
			}
			else
			{
				sb.Append(value);
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			static void EscapeToSlow(ref FastStringBuilder sb, ReadOnlySpan<char> value)
			{
				var span = sb.GetSpan(value.Length * 2);
				TryEscapeTo(span, out int len, value);
				sb.Advance(len);
			}
		}

		private static bool TryEscapeTo(Span<char> destination, out int charsWritten, ReadOnlySpan<char> value)
		{
			return value.IndexOfAny(EscapedLiterals) >= 0
				? TryEscapeToSlow(destination, out charsWritten, value)
				: value.TryCopyTo(destination, out charsWritten);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static bool TryEscapeToSlow(Span<char> destination, out int charsWritten, ReadOnlySpan<char> value)
		{
			// we know there is at least one escaped char
			if (value.Length + 1 > destination.Length) goto too_small;

			int p = 0;
			foreach (var c in value)
			{
				if (c is '\\' or '/' or '[' or ']')
				{
					if (p + 2 > destination.Length) goto too_small;
					destination[p] = '\\';
					destination[p + 1] = c;
					p += 2;
				}
				else
				{
					if (p + 1 > destination.Length) goto too_small;
					destination[p++] = c;
				}
			}

			charsWritten = p;
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		/// <summary>Return a path segment composed of only a name, but without any LayerId specified</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPathSegment Create(string name)
			=> new(name, string.Empty);

		/// <summary>Return a path segment composed both a name, and a LayerId</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPathSegment Create(string name, string layerId)
			=> new(name, layerId);

		/// <summary>Return a path segment composed a name, and the "partition" LayerId</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPathSegment Partition(string name)
			=> new(name, FdbDirectoryPartition.LayerId);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Encode(string name)
			=> Escape(name);

		public static string Encode(string name, string? layerId)
			=> string.IsNullOrEmpty(layerId)
				? Escape(name)
				: $"{Escape(name)}[{Escape(layerId)}]";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void AppendTo(ref FastStringBuilder sb, string name)
			=> EscapeTo(ref sb, name);

		internal static void AppendTo(ref FastStringBuilder sb, string name, string? layerId)
		{
			EscapeTo(ref sb, name);
			if (!string.IsNullOrEmpty(layerId))
			{
				sb.Append('[');
				EscapeTo(ref sb, layerId);
				sb.Append(']');
			}
		}

		[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TryFormatTo(Span<char> destination, out int charsWritten, string name)
			=> TryEscapeTo(destination, out charsWritten, name);

		[MustUseReturnValue]
		internal static bool TryFormatTo(Span<char> destination, out int charsWritten, ReadOnlySpan<char> name, ReadOnlySpan<char> layerId)
		{
			if (!TryEscapeTo(destination, out int len, name))
			{
				goto too_small;
			}

			if (layerId.Length == 0)
			{
				charsWritten = len;
				return true;
			}

			var buffer = destination[len..];

			if (!buffer.TryAppendAndAdvance('['))
			{
				goto too_small;
			}

			if (!TryEscapeTo(buffer, out len, layerId))
			{
				goto too_small;
			}
			buffer = buffer[len..];

			if (!buffer.TryAppendAndAdvance(']'))
			{
				goto too_small;
			}

			charsWritten = destination.Length - buffer.Length;
			Contract.Debug.Ensures(charsWritten > 3);
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		public static FdbPathSegment[] Parse(ReadOnlySpan<string> segments)
		{
			var tmp = new FdbPathSegment[segments.Length];
			for (int i = 0; i < segments.Length; i++)
			{
				tmp[i] = Parse(segments[i]);
			}
			return tmp;
		}

		/// <summary>Parse a string representation of a path segment (name with optional LayerId)</summary>
		/// <param name="value">Encoded path segment</param>
		/// <returns>Decoded path segment (may include an optional LayerId)</returns>
		/// <example>Parse("Foo") == FdbPathSegment.Create("Foo"); Parse("Foo[SomeLayer]") == FdbPathSegment.Create("Foo", "SomeLayer")</example>
		public static FdbPathSegment Parse(string value)
		{
			return Parse(value.AsSpan());
		}

		/// <summary>Parse a string representation of a path segment (name with optional LayerId)</summary>
		/// <param name="value">Encoded path segment</param>
		/// <returns>Decoded path segment (may include an optional LayerId)</returns>
		/// <example>Parse("Foo") == FdbPathSegment.Create("Foo"); Parse("Foo[SomeLayer]") == FdbPathSegment.Create("Foo", "SomeLayer")</example>
		public static FdbPathSegment Parse(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				return Empty;
			}

			if (!TryParse(null, value, withException: true, out var segment, out var error))
			{
				throw error ?? new ArgumentException("Invalid path segment", nameof(value));
			}
			return segment;
		}

		public static bool TryParse(ReadOnlySpan<char> value, out FdbPathSegment segment)
		{
			if (value.Length == 0)
			{
				segment = Empty;
				return true;
			}

			return TryParse(null, value, withException: false, out segment, out _);
		}

		internal static bool TryParse(StringBuilder? sb, ReadOnlySpan<char> value, bool withException, out FdbPathSegment segment, out Exception? error)
		{
			//REVIEW: use a Span<char> instead of StringBuilder !
			sb ??= new(value.Length);

			bool escaped = false;
			bool inLayer = false;

			string? name = null;
			string? layerId = null;

			foreach (var c in value)
			{
				switch (c)
				{
					case '\\':
					{
						if (escaped)
						{
							sb.Append('\\');
							escaped = false;
						}
						else
						{
							escaped = true;
						}
						break;
					}
					case '[':
					{
						if (escaped)
						{
							sb.Append('[');
							escaped = false;
							break;
						}
						if (inLayer)
						{
							segment = default;
							error = withException ? new FormatException("Invalid path segment: unescaped '[' inside layer keyword") : null;
							return false;
						}
						name = sb.ToString();
						sb.Clear();
						inLayer = true;
						break;
					}
					case ']':
					{
						if (escaped || !inLayer)
						{
							sb.Append(']');
							escaped = false;
							break;
						}
						//note: the layer string can be empty '[]'
						layerId = sb.ToString();
						sb.Clear();
						inLayer = false;
						break;
					}
					default:
					{
						sb.Append(c);
						escaped = false;
						break;
					}
				}
			}

			if (name == null && sb.Length != 0)
			{
				name = sb.ToString();
			}

			if (string.IsNullOrEmpty(name))
			{
				segment = default;
				error = withException ? new FormatException("Invalid path segment: name cannot be empty") : null;
				return false;
			}

			segment = new(name, layerId);
			error = null;
			return true;
		}

		/// <summary>Returns an encoded string representation of this path segment</summary>
		/// <returns>Encoded string, with optional LayerId</returns>
		/// <example><code>
		/// FdbPathSegment.Create("Foo").ToString()              // => "Foo";
		/// FdbPathSegment.Create("Foo", "SomeLayer").ToString() // => "Foo[SomeLayer]"
		/// </code></example>
		/// <remarks>
		/// <para>The string returned can be parsed back into the original segment via <see cref="Parse(string)"/>.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Returns an encoded string representation of this path segment</summary>
		///  <param name="format">Supported formats are <see langword="null"/> or <c>"D"</c> to include layer ids, and <c>"N"</c> for names only</param>
		///  <param name="provider">The value is ignored</param>
		/// <returns>Encoded string</returns>
		/// <example><code>
		/// FdbPathSegment.Create("Hello").ToString("D")               // => "Hello";
		/// FdbPathSegment.Create("Hello", "WorldLayer").ToString("D") // => "Hello[WorldLayer]"
		/// FdbPathSegment.Create("Hello", "WorldLayer").ToString("N") // => "Hello"
		/// </code></example>
		///  <remarks>
		///  <para>Supported formats:
		///  <list type="table">
		///		<listheader><term>Format</term><description>Result</description></listheader>
		///		<item><term><c>D</c></term><description><c>"ACME[partition]"</c></description></item>
		///		<item><term><c>N</c></term><description><c>"ACME"</c></description></item>
		///  </list></para>
		///  <para>Any string produced by this method can be passed back to <see cref="Parse(string)"/> to get back the original path.</para>
		///  </remarks>
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null) => format switch
		{
			null or "D" or "d" => Encode(this.Name, this.LayerId),
			"N" or "n" => Encode(this.Name),
			_ => throw new ArgumentException("Unsupported format", nameof(format))
		};

		/// <inheritdoc />
		[MustUseReturnValue]
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => TryFormatTo(destination, out charsWritten, this.Name, this.LayerId),
			"N" or "n" => TryFormatTo(destination, out charsWritten, this.Name),
			_ => throw new ArgumentException("Unsupported format", nameof(format))
		};

		/// <summary>Extract the pair of name and LayerId from this segment</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Deconstruct(out string name, out string? layerId)
		{
			name = this.Name;
			layerId = this.LayerId ?? string.Empty;
		}

		#region Equality...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj)
			=> obj is FdbPathSegment other && Equals(other);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
			=> HashCode.Combine(this.Name, this.LayerId);

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbPathSegment other)
			=> string.Equals(this.Name, other.Name) && string.Equals(this.LayerId, other.LayerId);

		/// <inheritdoc />
		public int CompareTo(FdbPathSegment other)
		{
			int cmp = string.CompareOrdinal(this.Name, other.Name);
			if (cmp == 0)
			{
				cmp = string.CompareOrdinal(this.LayerId, other.LayerId);
			}
			return cmp;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbPathSegment left, FdbPathSegment right)
			=> left.Equals(right);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbPathSegment left, FdbPathSegment right)
			=> !left.Equals(right);

		#endregion

	}
}
