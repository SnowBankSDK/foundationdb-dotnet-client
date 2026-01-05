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

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{
	using System.IO;

	public enum FqlPathSegmentType
	{
		Invalid = 0,
		Root, // only valid as the single segment of "/"
		Parent, // ".."
		Literal,
		Any,
	}

	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct FqlPathSegment : IEquatable<FqlPathSegment>, IFqlExpression, IFormattable
	{

		public readonly FqlPathSegmentType Type;

		public readonly FdbPathSegment Value;

		public FqlPathSegment(FqlPathSegmentType type, FdbPathSegment? value)
		{
			this.Type = type;
			this.Value = value ?? FdbPathSegment.Empty;
		}

		/// <inheritdoc />
		public bool IsPattern => this.Type is FqlPathSegmentType.Any;

		public bool IsRoot => this.Type is FqlPathSegmentType.Root;

		public bool IsParent => this.Type is FqlPathSegmentType.Parent;

		public bool IsAny => this.Type is FqlPathSegmentType.Any;

		public static FqlPathSegment Literal(FdbPathSegment segment) => new(FqlPathSegmentType.Literal, segment);

		public static FqlPathSegment Literal(string value, string? layerId) => new(FqlPathSegmentType.Literal, layerId != null ? FdbPathSegment.Create(value, layerId) : FdbPathSegment.Create(value));

		public static FqlPathSegment Any() => new(FqlPathSegmentType.Any, null);

		public static FqlPathSegment Root() => new(FqlPathSegmentType.Root, null);

		public static FqlPathSegment Parent() => new(FqlPathSegmentType.Parent, null);

		public bool Matches(FdbPathSegment segment)
		{
			switch (this.Type)
			{
				case FqlPathSegmentType.Root:
				{
					return segment.IsEmpty;
				}
				case FqlPathSegmentType.Literal:
				{
					return string.IsNullOrEmpty(this.Value.LayerId)
						? this.Value.Name == segment.Name
						: this.Value.Name == segment.Name && this.Value.LayerId == segment.LayerId;
				}
				case FqlPathSegmentType.Parent:
				{
					// parent paths should be normalized before being evaluated!
					return false;
				}
				case FqlPathSegmentType.Any:
				{
					return true;
				}
				default:
				{
					return false;
				}
			}

		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlPathSegment seg && Equals(seg);

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Type, this.Value);

		/// <inheritdoc />
		public bool Equals(FqlPathSegment other) => other.Type == this.Type && this.Value.Equals(other.Value);

		/// <inheritdoc />
		public override string ToString() => this.Type switch
		{
			FqlPathSegmentType.Root => "/",
			FqlPathSegmentType.Parent => "..",
			FqlPathSegmentType.Literal => (this.Value.Name.Contains('"') ? $"\"{this.Value.Name.Replace("\"", "\\\"")}\"" : this.Value.Name) + (!string.IsNullOrEmpty(this.Value.LayerId) ? $"[{this.Value.LayerId}]" : ""),
			FqlPathSegmentType.Any => "<>",
			_ => "<invalid>"
		};

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

		/// <inheritdoc />
		public void Explain(ExplanationBuilder builder)
		{
			builder.WriteLine(ToString());
		}

	}

	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public sealed class FqlDirectoryExpression : IEquatable<FqlDirectoryExpression>, IFqlExpression, IFormattable
	{

		public List<FqlPathSegment> Segments { get; } = [ ];

		/// <inheritdoc />
		public bool IsPattern => this.Segments.Any(x => x.IsPattern);

		public static FqlDirectoryExpression Create() => new();

		public FqlDirectoryExpression Name(FqlPathSegment segment)
		{
			this.Segments.Add(segment);
			return this;
		}

		public FqlDirectoryExpression Parent()
		{
			this.Segments.Add(FqlPathSegment.Parent());
			return this;
		}

		public FqlDirectoryExpression Name(string name)
		{
			this.Segments.Add(FqlPathSegment.Literal(FdbPathSegment.Create(name)));
			return this;
		}

		public FqlDirectoryExpression Name(string name, string layerId)
		{
			this.Segments.Add(FqlPathSegment.Literal(FdbPathSegment.Create(name, layerId)));
			return this;
		}

		public FqlDirectoryExpression Root()
		{
			if (this.Segments.Count != 0) throw new InvalidOleVariantTypeException("Root must be the first item");
			this.Segments.Add(FqlPathSegment.Root());
			return this;
		}

		public FqlDirectoryExpression Any()
		{
			this.Segments.Add(FqlPathSegment.Any());
			return this;
		}

		public bool Match(FdbPath path)
		{
			var segments = CollectionsMarshal.AsSpan(this.Segments);

			if (segments.Length == 0)
			{
				return path.IsEmpty;
			}

			if (segments[0].IsRoot)
			{ // check if the path is aboslute
				if (!path.IsAbsolute) return false;
				segments = segments[1..];
			}
			
			if (path.Count != segments.Length)
			{ // must have the same number of segments
				return false;
			}

			for (int i = 0; i < segments.Length; i++)
			{
				if (!segments[i].Matches(path[i])) return false;
			}

			return true;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			var sb = new StringBuilder();

			var segments = CollectionsMarshal.AsSpan(this.Segments);

			if (segments.Length == 0) return "";
			if (segments.Length == 1 && segments[0].IsRoot) return "/";

			bool needsSlash = false;
			for(int i = 0; i < segments.Length; i++)
			{
				if (needsSlash) sb.Append('/');
				switch (segments[i].Type)
				{
					case FqlPathSegmentType.Root: sb.Append('/'); break;
					case FqlPathSegmentType.Literal: sb.Append(segments[i].ToString()); needsSlash = true; break;
					case FqlPathSegmentType.Parent: sb.AppendLine(".."); break;
					case FqlPathSegmentType.Any: sb.Append("<>"); needsSlash = true; break;
					default: sb.Append("<???>"); needsSlash = true; break;
				}
			}

			return sb.ToString();
		}

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

		/// <inheritdoc />
		public void Explain(ExplanationBuilder builder)
		{
			if (!builder.Recursive)
			{
				builder.WriteLine($"Directory: [{this.Segments.Count}] {ToString()}");
				return;
			}

			builder.WriteLine($"Directory: [{this.Segments.Count}]");
			builder.ExplainChildren(this.Segments);
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj is FqlDirectoryExpression dir && Equals(dir);

		/// <inheritdoc />
		public override int GetHashCode()
		{
			var segments = this.Segments;
			var h = new HashCode();
			foreach (var seg in segments)
			{
				h.Add(seg.GetHashCode());
			}
			return h.ToHashCode();
		}

		/// <inheritdoc />
		public bool Equals(FqlDirectoryExpression? other)
		{
			if (other is null) return false;
			if (ReferenceEquals(this, other)) return true;
			var segments = this.Segments;
			var otherSegments = other.Segments;
			if (segments.Count != otherSegments.Count) return false;
			for (int i = 0; i < segments.Count; i++)
			{
				if (!segments[i].Equals(otherSegments[i])) return false;
			}
			return true;
		}

		public (FdbPath Path, int Next) GetFixedPrefix(int from)
		{
			var items = CollectionsMarshal.AsSpan(this.Segments);

			int index = from;
			items = items[from..];

			FdbPath p = FdbPath.Empty;
			if (from == 0 && items[0].IsRoot)
			{
				p = FdbPath.Root;
				items = items[1..];
				++index;
			}

			foreach (var item in items)
			{
				if (item.Type != FqlPathSegmentType.Literal) break;
				p = p[item.Value];
				++index;
			}

			return (p, index);
		}

		public bool TryGetPath(out FdbPath path)
		{
			var items = CollectionsMarshal.AsSpan(this.Segments);

			if (items.Length == 0)
			{
				path = FdbPath.Empty;
				return true;
			}

			FdbPath p = FdbPath.Empty;
			if (items[0].IsRoot)
			{
				p = FdbPath.Root;
				items = items[1..];
			}

			foreach (var item in items)
			{
				if (item.Type != FqlPathSegmentType.Literal) goto invalid;
				p = p[item.Value];
			}

			path = p;
			return true;

		invalid:
			path = default;
			return false;
		}

		public FqlPathSegment this[int index] => this.Segments[index];

	}

}

#endif
