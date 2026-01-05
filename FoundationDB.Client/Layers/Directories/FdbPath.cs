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

	/// <summary>Represents a path in a Directory Layer</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	[PublicAPI]
	public readonly struct FdbPath : IReadOnlyList<FdbPathSegment>, IEquatable<FdbPath>, IComparable<FdbPath>, ISpanFormattable, IJsonDeserializable<FdbPath>, IJsonSerializable, IJsonPackable
	{
		/// <summary>The "empty" path</summary>
		/// <remarks>This path is relative</remarks>
		public static readonly FdbPath Empty;

		/// <summary>The "root" path ("/").</summary>
		/// <remarks>This path is absolute</remarks>
		public static readonly FdbPath Root = new(default, absolute: true);

		/// <summary>Segments of this path</summary>
		public readonly ReadOnlyMemory<FdbPathSegment> Segments;

		/// <summary>If <see langword="true"/>, this is an absolute path (ex: "/Foo/Bar"); otherwise, this a relative path ("Foo/Bar")</summary>
		public readonly bool IsAbsolute;

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbPath(ReadOnlyMemory<FdbPathSegment> path, bool absolute)
		{
			this.Segments = path;
			this.IsAbsolute = absolute;
		}

		/// <summary>Test if this is the empty directory</summary>
		/// <remarks>The <see cref="Root"/> path ("/") is NOT considered empty!</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !this.IsAbsolute && this.Segments.Length == 0;
		}

		/// <summary>Test if this is the root directory ("/")</summary>
		/// <remarks>The <see cref="Empty"/> path is NOT considered to be the root!</remarks>
		public bool IsRoot
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.IsAbsolute && this.Segments.Length == 0;
		}

		/// <summary>Number of segments in the path</summary>
		public int Count => this.Segments.Length;

		/// <summary>Returns the segment at the specified index (0-based)</summary>
		public FdbPathSegment this[int index] => this.Segments.Span[index];

		/// <summary>Returns the segment at the specified index</summary>
		public FdbPathSegment this[Index index] => this.Segments.Span[index];

		/// <summary>Returns a subsection of the current path</summary>
		public FdbPath this[Range range] => new(this.Segments[range], this.IsAbsolute && range.GetOffsetAndLength(this.Count).Offset == 0);

		/// <summary>Return the name of the last segment of this path</summary>
		/// <example><see cref="Relative(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
		/// <remarks>The name of the <see cref="Empty"/> or <see cref="Root"/> paths is, by convention, the empty string.</remarks>
		public string Name
		{
			[Pure]
			get
			{
				var path = this.Segments.Span;
				return path.Length != 0 ? path[^1].Name : string.Empty;
			}
		}

		/// <summary>Returns the LayerId of the last segment of this path</summary>
		/// <example><see cref="Relative(string[])"/>Combine("Foo", "Bar", "Baz").Name => "Baz"</example>
		/// <remarks>Be convention, the LayerId of the <see cref="Empty"/> path is the <c>empty</c> string, and the LayerId of the <see cref="Root"/> path is <c>"partition"</c></remarks>
		public string LayerId
		{
			[Pure]
			get
			{
				var path = this.Segments.Span;
				return path.Length != 0 ? path[^1].LayerId
				       : this.IsAbsolute ? FdbDirectoryPartition.LayerId
				       : string.Empty;
			}
		}

		/// <summary>Returns the same path, but with the specified LayerId</summary>
		/// <param name="layerId">LayerId value for this path</param>
		/// <returns>New path that points to the same location, but with the attached LayerId metadata</returns>
		/// <exception cref="InvalidOperationException">If the path already has an explicit LayerId</exception>
		public FdbPath WithLayer(string? layerId)
		{
			layerId ??= string.Empty;

			var path = this.Segments.Span;
			if (path.Length == 0) throw new InvalidOperationException("Cannot change the layer of the empty path.");
			var tail = path[^1];
			if (tail.LayerId == layerId) return this;

			var tmp = this.Segments.ToArray();
			tmp[^1] = new(tail.Name, layerId);
			return new(tmp, this.IsAbsolute);
		}

		/// <summary>Returns the equivalent relative path, using the same path segments.</summary>
		/// <returns>The relative version of this path</returns>
		/// <example>"/Foo/Bar".AsRelative() == "Foo/Bar"; "Foo/Bar".AsRelative() == "Foo/Bar"</example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbPath AsRelative() => new(this.Segments, absolute: false);

		/// <summary>Returns the equivalent absolute path, using the same path segments.</summary>
		/// <returns>The absolute version of this path</returns>
		/// <example>"Foo/Bar".AsAbsolute() == "/Foo/Bar"; "/Foo/Bar".AsAbsolute() == "/Foo/Bar"</example>
		/// <remarks>This is the equivalent of adding this path to the <see cref="Root"/> path</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbPath AsAbsolute() => new(this.Segments, absolute: true);

		/// <summary>Returns the relative path that, if added to <paramref name="parent"/>, would be equal to the current path.</summary>
		/// <param name="parent">Parent path. Must be of the same type (absolute/relative) as the current path.</param>
		/// <param name="relative">If the method returns <see langword="true"/>, receives the relative path from <paramref name="parent"/> to the current path.</param>
		/// <returns>Returns <see langword="true"/> if <paramref name="parent"/> is an ancestor or equal to the current path; otherwise, returns <see langword="false"/>.</returns>
		public bool TryGetRelative(FdbPath parent, out FdbPath relative)
		{
			if (this.IsAbsolute)
			{
				if (!parent.IsAbsolute)
				{ // We cannot compare an absolute path with a relative parent path
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new ArgumentException("Parent path cannot be relative.", nameof(parent));
				}
			}
			else
			{
				if (parent.IsAbsolute)
				{ // We cannot compare a relative path with an absolute parent path
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new ArgumentException("Parent path cannot be absolute.", nameof(parent));
				}
			}

			if (!StartsWith(parent))
			{
				relative = default;
				return false;
			}

			if (parent.Count == this.Count)
			{ // both paths are the same!
				relative = FdbPath.Empty;
				return true;
			}

			relative = new(this.Segments[parent.Count..], absolute: false);
			return true;
		}

		/// <summary>Gets the parent path of the current path, if it is not empty.</summary>
		/// <param name="parent">Receive the path of the parent, if there is one.</param>
		/// <returns>If <see langword="true"/>, <paramref name="parent"/> contains the parent path. If <see langword="false"/>, the current path was <see cref="Empty"/> or the <see cref="Root"/>, and does not have a parent.</returns>>
		/// <example>"/Foo/Bar/Baz".TryGetParent() => (true, "/Foo/Bar")</example>
		public bool TryGetParent(out FdbPath parent)
		{
			var path = this.Segments;
			if (path.Length == 0)
			{
				parent = default;
				return false;
			}
			parent = new(path[..^1], this.IsAbsolute);
			return true;
		}

		/// <summary>Returns the parent path of the current path</summary>
		/// <example>"/Foo/Bar/Baz".GetParent() => "/Foo/Bar"</example>
		/// <exception cref="InvalidOperationException">If this path is empty.</exception>
		[Pure]
		public FdbPath GetParent()
			=> TryGetParent(out var parent)
				? parent
				: throw new InvalidOperationException("The root path does not have a parent path.");

		/// <summary>Appends a new segment to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <remarks>The new segment will not have a LayerId.</remarks>
		public FdbPath this[string name] => Add(FdbPathSegment.Create(name));

		/// <summary>Appends a new segment - composed of a name and LayerId - to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">LayerId of the segment</param>
		public FdbPath this[string name, string layerId] => Add(FdbPathSegment.Create(name, layerId));

		/// <summary>Appends a new segment to the current path</summary>
		public FdbPath this[FdbPathSegment segment] => Add(segment);

		/// <summary>Appends a relative path to the current path</summary>
		public FdbPath this[FdbPath path] => Add(path);

		private static ReadOnlyMemory<FdbPathSegment> AppendSegment(ReadOnlySpan<FdbPathSegment> head, FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment));
			int n = head.Length;
			var tmp = new FdbPathSegment[n + 1];
			head.CopyTo(tmp);
			tmp[n] = segment;
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, FdbPathSegment segment1, FdbPathSegment segment2)
		{
			if (segment1.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment1));
			if (segment2.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment2));
			int n = head.Length;
			var tmp = new FdbPathSegment[n + 2];
			head.CopyTo(tmp);
			tmp[n] = segment1;
			tmp[n + 1] = segment2;
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, ReadOnlySpan<FdbPathSegment> tail)
		{
			var tmp = new FdbPathSegment[head.Length + tail.Length];
			head.CopyTo(tmp);
			tail.CopyTo(tmp.AsSpan(head.Length));
			return tmp;
		}

		private static ReadOnlyMemory<FdbPathSegment> AppendSegments(ReadOnlySpan<FdbPathSegment> head, IEnumerable<FdbPathSegment> suffix)
		{
			var list = new List<FdbPathSegment>(head.Length + ((suffix as ICollection<FdbPathSegment>)?.Count ?? 4));
			foreach (var segment in head) list.Add(segment);
			foreach (var segment in suffix) list.Add(segment);
			return list.ToArray();
		}

		/// <summary>Appends a new segment to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <remarks>This segment will not have any LayerId defined.</remarks>
		[Pure]
		public FdbPath Add(string name)
			=> new(AppendSegment(this.Segments.Span, FdbPathSegment.Create(name)), this.IsAbsolute);

		/// <summary>Appends a new segment to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">LayerId of the segment</param>
		[Pure]
		public FdbPath Add(string name, string layerId)
			=> new(AppendSegment(this.Segments.Span, FdbPathSegment.Create(name, layerId)), this.IsAbsolute);

		/// <summary>Appends a new segment to the current path</summary>
		[Pure]
		public FdbPath Add(FdbPathSegment segment)
			=> new(AppendSegment(this.Segments.Span, segment), this.IsAbsolute);

		/// <summary>Adds new segments to the current path</summary>
		[Pure]
		public FdbPath Add(params ReadOnlySpan<FdbPathSegment> segments)
			=> segments.Length != 0 ? new(AppendSegments(this.Segments.Span, segments), this.IsAbsolute) : this;

		/// <summary>Adds new segments to the current path</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbPath Add(params FdbPathSegment[] segments)
			=> Add(segments.AsSpan());

		/// <summary>Adds new segments to the current path</summary>
		[Pure]
		public FdbPath Add(IEnumerable<FdbPathSegment> segments)
			=> segments is FdbPathSegment[] arr ? Add(arr.AsSpan()) : new(AppendSegments(this.Segments.Span, segments), this.IsAbsolute);

		/// <summary>Adds a path to the current path</summary>
		[Pure]
		public FdbPath Add(FdbPath path)
		{
			if (this.IsAbsolute)
			{
				// Can only add relative to an absolute
				if (path.IsAbsolute)
				{
					// we only allow Fdb.Root[absolutePath] as a convenience
					if (this.IsRoot) return path;
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException("Cannot add two absolute directory path together.");
				}
				if (path.IsEmpty) return this;
			}
			else
			{
				// we are still Empty + "/Foo/Bar"
				if (!this.IsEmpty && path.IsAbsolute)
				{
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException("Cannot add an absolute directory path to a relative path.");
				}
				if (this.IsEmpty) return path;
			}
			return new(AppendSegments(this.Segments.Span, path.Segments.Span), this.IsAbsolute);
		}

		/// <summary>Returns a suffix of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		[Pure]
		public FdbPath Substring(int offset)
			=> new(this.Segments[offset..], absolute: this.IsAbsolute && offset == 0);

		/// <summary>Returns a subsection of the current path</summary>
		/// <param name="offset">Number of segments to skip</param>
		/// <param name="count">Number of segments to keep</param>
		[Pure]
		public FdbPath Substring(int offset, int count)
			=> new(this.Segments.Slice(offset, count), absolute: this.IsAbsolute && offset == 0);

		/// <summary>Tests if the current path is a child of another path</summary>
		/// <remarks>This differs from <see cref="StartsWith"/> in that a path is not a child of itself</remarks>
		[Pure]
		public bool IsChildOf(FdbPath prefix)
		{
			// we allow Absolute.EndsWith(Relative), but not Relative.EndsWith(Absolute)
			if (this.IsAbsolute != prefix.IsAbsolute) return false;

			var thisSpan = this.Segments.Span;
			var otherSpan = prefix.Segments.Span;
			if (thisSpan.Length <= otherSpan.Length) return false;

			//note: we have to compare only the names, ignoring the partitions
			for (int i = 0; i < otherSpan.Length; i++)
			{
				if (thisSpan[i].Name != otherSpan[i].Name)
				{
					return false;
				}
				// if both have a LayerId, it must be identical
				if (thisSpan[i].LayerId is not "" && otherSpan[i].LayerId is not "" && thisSpan[i].LayerId != otherSpan[i].LayerId)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Tests if the current path is the same, or a child of another path</summary>
		/// <remarks>This differs from <see cref="IsChildOf"/> in that a path always starts with itself</remarks>
		[Pure]
		public bool StartsWith(FdbPath prefix)
		{
			// we allow Absolute.StartsWith(Absolute) and Relative.StartsWith(Relative), but not a mix of Absolute/Relative
			if (this.IsAbsolute != prefix.IsAbsolute) return false;

			var thisSpan = this.Segments.Span;
			var otherSpan = prefix.Segments.Span;
			if (thisSpan.Length < otherSpan.Length) return false;

			for (int i = 0; i < otherSpan.Length; i++)
			{
				if (thisSpan[i].Name != otherSpan[i].Name)
				{
					return false;
				}

				// if both have a LayerId, it must be identical
				if (thisSpan[i].LayerId is not "" && otherSpan[i].LayerId is not "" && thisSpan[i].LayerId != otherSpan[i].LayerId)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Tests if the current path is a parent of another path</summary>
		/// <remarks>This differs from <see cref="StartsWith"/> in that a path is not a parent of itself</remarks>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public bool IsParentOf(FdbPath suffix) => suffix.IsChildOf(this);

		/// <summary>Tests if the current path is the same or a parent of another path</summary>
		/// <remarks>This differs from <see cref="IsParentOf"/> in that a path always ends with itself</remarks>
		[Pure]
		public bool EndsWith(FdbPath suffix)
		{
			// we allow Absolute.EndsWith(Relative), but not Relative.EndsWith(Absolute)
			if (!this.IsAbsolute && suffix.IsAbsolute) return false;

			var thisSpan = this.Segments.Span;
			var otherSpan = suffix.Segments.Span;

			int thisOffset = thisSpan.Length - otherSpan.Length;
			if (thisOffset < 0) return false;

			for (int i = otherSpan.Length - 1; i >= 0; i--)
			{
				if (thisSpan[thisOffset + i].Name != otherSpan[i].Name)
				{
					return false;
				}

				// if both have a LayerId, it must be identical
				if (thisSpan[thisOffset + i].LayerId is not "" && otherSpan[i].LayerId is not "" && thisSpan[thisOffset + i].LayerId != otherSpan[i].LayerId)
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>Returns the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <param name="relativePath">If this path is equal to, or a child or <paramref name="parent"/>, receives the relative path; otherwise, <see cref="Empty"/>.</param>
		/// <returns>Returns <see langword="true"/> if path is equal to, or a child of <paramref name="parent"/>; otherwise, false.</returns>
		/// <remarks>If this path is equal to <paramref name="parent"/>, still returns true but <paramref name="relativePath"/> will be empty.</remarks>
		/// <example>"/Foo/Bar/Baz".TryGetRelativePath("/Foo") => (true, "Bar/Baz")</example>
		public bool TryGetRelativePath(FdbPath parent, out FdbPath relativePath)
		{
			if (!StartsWith(parent))
			{
				relativePath = default;
				return false;
			}
			relativePath = new(this.Segments[parent.Segments.Length..], absolute: false);
			Contract.Debug.Ensures(!relativePath.IsAbsolute);
			return true;
		}

		/// <summary>Returns the relative part of this path inside its <paramref name="parent"/></summary>
		/// <param name="parent">Parent of this path</param>
		/// <returns>Returns the relative portion of the current path under its <paramref name="parent"/>. It this path is equal to <paramref name="parent"/>, returns <see cref="Empty"/>.</returns>
		/// <exception cref="ArgumentException">If this path is not equal to, or a child of <paramref name="parent"/>.</exception>
		/// <example>"/Foo/Bar/Baz".GetRelativePath("/Foo") => "Bar/Baz"</example>
		public FdbPath GetRelativePath(FdbPath parent)
			=> TryGetRelativePath(parent, out var relative)
				? relative
				: throw new ArgumentException("The current path is not equal to, or a child of, the specified parent path.", nameof(parent));

		/// <summary>Returns a serialized string that represents this path.</summary>
		/// <returns>All the path segments joined with a '/'. If the path is absolute, starts with a leading '/'</returns>
		/// <remarks>Any string produced by this method can be passed back to <see cref="Parse(string)"/> to get back the original path.</remarks>
		public override string ToString() => ToString(null);

		///  <summary>Returns a serialized string that represents this path.</summary>
		///  <param name="format">Supported formats are <see langword="null"/> or <c>"D"</c> to include layer ids, and <c>"N"</c> for names only</param>
		///  <param name="provider">The value is ignored</param>
		///  <returns>All the path segments joined with a '/'. If the path is absolute, starts with a leading '/'</returns>
		///  <remarks>
		///  <para>Supported formats:
		///  <list type="table">
		/// 	<listheader><term>Format</term><description>Result</description></listheader>
		/// 	<item><term><c>D</c></term><description><c>"/Tenants/ACME[partition]/Documents/Users"</c></description></item>
		/// 	<item><term><c>N</c></term><description><c>"/Tenants/ACME/Documents/Users"</c></description></item>
		///  </list></para>
		///  <para>Any string produced by this method can be passed back to <see cref="Parse(string)"/> to get back the original path.</para>
		///  </remarks>
		[Pure]
		public string ToString(string? format, IFormatProvider? provider = null) =>
			format switch
			{
				null or "D" or "d" => FormatPath(this.Segments.Span, this.IsAbsolute, namesOnly: false),
				"N" or "n" => FormatPath(this.Segments.Span, this.IsAbsolute, namesOnly: true),
				_ => throw new ArgumentException("Unsupported format", nameof(format))
			};


		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => TryFormatPath(destination, out charsWritten, this.Segments.Span, this.IsAbsolute, namesOnly: false),
			"N" or "n" => TryFormatPath(destination, out charsWritten, this.Segments.Span, this.IsAbsolute, namesOnly: true),
			_ => throw new ArgumentException("Unsupported format", nameof(format))
		};

		/// <summary>Encode a path into a string representation</summary>
		/// <param name="path">Path to encode</param>
		/// <param name="namesOnly">If <see langword="true"/>, omit the LayerIds in the resulting string.</param>
		/// <returns>String representation of the path (with or without the LayerId)</returns>
		/// <remarks>If <paramref name="namesOnly"/> is <see langword="true"/>, the result will not <see cref="Parse(string)">round-trip</see> into the original path (LayerIds will be lost).</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string Encode(FdbPath path, bool namesOnly = false)
			=> FormatPath(path.Segments.Span, path.IsAbsolute, namesOnly);

		[MustUseReturnValue]
		internal static string FormatPath(ReadOnlySpan<FdbPathSegment> paths, bool absolute, bool namesOnly)
		{
			if (paths.Length == 0) return absolute ? "/" : string.Empty;

			var sb = new FastStringBuilder(stackalloc char[64]);
			if (namesOnly)
			{
				foreach (var seg in paths)
				{
					if (absolute || sb.Length != 0) sb.Append('/');
					FdbPathSegment.AppendTo(ref sb, seg.Name);
				}
			}
			else
			{
				foreach (var seg in paths)
				{
					if (absolute || sb.Length != 0) sb.Append('/');
					FdbPathSegment.AppendTo(ref sb, seg.Name, seg.LayerId);
				}
			}

			return sb.ToString();
		}

		[MustUseReturnValue]
		internal static bool TryFormatPath(Span<char> destination, out int charsWritten, ReadOnlySpan<FdbPathSegment> paths, bool absolute, bool namesOnly)
		{
			if (paths.Length == 0)
			{
				if (!absolute)
				{
					charsWritten = 0;
					return true;
				}

				if (destination.Length < 1) goto too_small;
				destination[0] = '/';
				charsWritten = 1;
				return true;
			}

			var buffer = destination;

			int len;
			if (namesOnly)
			{
				for(int i = 0; i < paths.Length; i++)
				{
					if (absolute || i != 0)
					{
						if (!buffer.TryAppendAndAdvance('/'))
						{
							goto too_small;
						}
					}

					if (!FdbPathSegment.TryFormatTo(buffer, out len, paths[i].Name))
					{
						goto too_small;
					}
					buffer = buffer[len..];
				}
			}
			else
			{
				for(int i = 0; i < paths.Length; i++)
				{
					if (absolute || i != 0)
					{
						if (!buffer.TryAppendAndAdvance('/'))
						{
							goto too_small;
						}
					}

					if (!FdbPathSegment.TryFormatTo(buffer, out len, paths[i].Name, paths[i].LayerId))
					{
						goto too_small;
					}
					buffer = buffer[len..];
				}
			}

			charsWritten = destination.Length - buffer.Length;
			Contract.Debug.Ensures(charsWritten > 0);
			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<FdbPathSegment> GetEnumerator()
		{
			return MemoryMarshal.ToEnumerable(this.Segments).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#region Factory Methods...

		/// <summary>Adds a segment to a parent path</summary>
		/// <param name="path">Parent path</param>
		/// <param name="segment">Encoded path segment, composed of just a name (<c>"Foo"</c>), with an optional LayerId (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Combine(FdbPath path, FdbPathSegment segment)
		{
			return path.Add(segment);
		}

		/// <summary>Adds a pair of segments to a root path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, FdbPathSegment segment1, FdbPathSegment segment2)
		{
			return path.Add(segment1, segment2);
		}

		/// <summary>Adds one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments);
			if (segments.Length == 0) return path;

			return path.Add(segments);
		}

		/// <summary>Adds one or more segments to a parent path</summary>
		[Pure]
		public static FdbPath Combine(FdbPath path, ReadOnlySpan<FdbPathSegment> segments)
		{
			if (segments.Length == 0) return path;

			return path.Add(segments);
		}

		/// <summary>Returns a relative path composed of a single segment</summary>
		/// <param name="segment">Encoded path segment, composed of just a name (<c>"Foo"</c>), with an optional LayerId (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(string segment)
		{
			Contract.NotNull(segment);
			return new(new [] { FdbPathSegment.Parse(segment) }, absolute: false);
		}

		/// <summary>Returns a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Relative(FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty", nameof(segment));
			return new(new [] { segment }, absolute: false);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		/// <param name="segments">Array of encoded path segments, composed of just a name (<c>"Foo"</c>), with an optional LayerId (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(params string[] segments)
		{
			Contract.NotNull(segments);
			if (segments.Length == 0) return FdbPath.Empty;
			return new(FdbPathSegment.Parse(segments.AsSpan()), absolute: false);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments);
			if (segments.Length == 0) return FdbPath.Empty;
			return new(segments, absolute: false);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(ReadOnlySpan<FdbPathSegment> segments)
		{
			// we have to copy the buffer!
			return new(segments.ToArray(), absolute: false);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Relative(ReadOnlyMemory<FdbPathSegment> segments)
		{
			return new(segments, absolute: false);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		/// <param name="segments">Sequence of path segment, composed of just a name (<c>"Foo"</c>), with an optional LayerId (<c>"Foo[SomeLayer]"</c>)</param>
		[Pure]
		public static FdbPath Relative(IEnumerable<FdbPathSegment> segments)
		{
			Contract.NotNull(segments);
			return new(segments.ToArray(), absolute: false);
		}

		/// <summary>Returns a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Absolute(string segment)
		{
			Contract.NotNull(segment);
			return new(new [] { FdbPathSegment.Parse(segment) }, absolute: true);
		}

		/// <summary>Returns a relative path composed of a single segment</summary>
		[Pure]
		public static FdbPath Absolute(FdbPathSegment segment)
		{
			if (segment.IsEmpty) throw new ArgumentException("Segment cannot be empty.", nameof(segment));
			return new(new [] { segment }, absolute: true);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(params string[] segments)
		{
			Contract.NotNull(segments);
			if (segments.Length == 0) return FdbPath.Root;
			return new(FdbPathSegment.Parse(segments.AsSpan()), absolute: true);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(params FdbPathSegment[] segments)
		{
			Contract.NotNull(segments);
			if (segments.Length == 0) return FdbPath.Root;
			return new(segments, absolute: true);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(ReadOnlySpan<FdbPathSegment> segments)
		{
			// we have to copy the buffer!
			return new(segments.ToArray(), absolute: true);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(ReadOnlyMemory<FdbPathSegment> segments)
		{
			return new(segments, absolute: true);
		}

		/// <summary>Returns a relative path composed of the specified segments</summary>
		[Pure]
		public static FdbPath Absolute(IEnumerable<FdbPathSegment> segments)
		{
			Contract.NotNull(segments);
			return new(segments.ToArray(), absolute: true);
		}

		/// <summary>Parses a string representing a path into the corresponding <see cref="FdbPath"/> instance.</summary>
		/// <param name="path">Path (either absolute or relative).</param>
		/// <returns>Corresponding path.</returns>
		/// <remarks>This method can take the out of <see cref="ToString()"/> and return the original path.</remarks>
		[Pure]
		public static FdbPath Parse(string? path)
			=> !string.IsNullOrWhiteSpace(path) ? Parse(path.AsSpan()) : Empty;

		/// <summary>Parses a string representing a path into the corresponding <see cref="FdbPath"/> instance.</summary>
		/// <param name="path">Path (either absolute or relative).</param>
		/// <returns>Corresponding path.</returns>
		/// <remarks>This method can take the out of <see cref="ToString()"/> and return the original path.</remarks>
		[Pure]
		public static FdbPath Parse(ReadOnlySpan<char> path)
		{
			return path.Length switch
			{
				0 => Empty,
				1 when path[0] == '/' => Root,
				_ => ParseInternal(path)
			};
		}

		private static FdbPath ParseInternal(ReadOnlySpan<char> path)
		{
			return path.Length switch
			{
				0 => Empty,
				1 when path[0] == '/' => Root,
				_ => TryParseInternal(null, path, withException: true, out var res, out var error) ? res : throw error!
			};
		}

		/// <summary>Parses a string representing a path into the corresponding <see cref="FdbPath"/> instance, if it is valid.</summary>
		public static bool TryParse(ReadOnlySpan<char> path, out FdbPath result)
		{
			if (path.Length == 0)
			{
				result = Empty;
				return true;
			}

			if (path.Length == 1 && path[0] == '/')
			{
				result = Root;
				return true;
			}

			return TryParseInternal(null, path, withException: false, out result, out _);
		}

		internal static bool TryParseInternal(StringBuilder? sb, ReadOnlySpan<char> path, bool withException, out FdbPath result, out Exception? error)
		{
			//TODO: version that takes a Span<char> instead of StringBuilder !
			sb ??= new(path.Length);

			var segments = new List<FdbPathSegment>();

			// the main loop only attempts to split the segments by finding valid '/' separators (note that '/' can be escaped (via '\/') and is NOT a segment separator)
			// => when we find a segment, we slice it and then use FdbPathSegment.Parse(...) to actual unescape the name (and optional LayerId)

			bool absolute = false;
			bool escaped = false;
			int start = 0;
			int pos = 0;

			foreach (var c in path)
			{
				switch (c)
				{
					case '\\':
					{
						escaped = !escaped;
						++pos;
						break;
					}
					case '/':
					{
						if (escaped)
						{
							escaped = false;
							++pos;
							break;
						}

						if (pos == 0)
						{
							if (segments.Count == 0)
							{ // first '/' means that it is an absolute path
								absolute = true;
								pos = 1;
								start = 1;
								continue;
							}

							result = default;
							error = new FormatException("Invalid directory path: segment cannot be empty.");
							return false;
						}

						sb.Clear();
						if (!FdbPathSegment.TryParse(sb, path.Slice(start, pos - start), withException, out var segment, out error))
						{
							result = default;
							return false;
						}

						segments.Add(segment);
						++pos;
						start = pos;
						break;
					}
					default:
					{
						if (escaped)
						{
							escaped = false;
						}
						++pos;
						break;
					}
				}
			}

			if (pos != start)
			{ // add last segment
				segments.Add(FdbPathSegment.Parse(path.Slice(start, pos - start)));
			}
			else if (segments.Count > 0 && path[^1] == '/')
			{ // extra '/' at the end !
				result = default;
				error = withException ? new FormatException("Invalid directory path: last segment cannot be empty.") : null;
				return false;
			}

			result = new(segments.ToArray(), absolute);
			error = null;
			return true;
		}

		#endregion

		#region Equality...

		/// <inheritdoc />
		public override int GetHashCode()
		{
			//this must be fast because we will use paths as keys in directories a lot (cache context)

			//note: we cannot use this.Segments.GetHashCode() because it will vary depending on the underlying backing store!
			// => we will use the head, middle point and tail of the segment to compute the hash

			var segments = this.Segments.Span;
			return segments.Length switch
			{
				0 => this.IsAbsolute ? -1 : 0,
				1 => HashCode.Combine(this.IsAbsolute, segments[0].GetHashCode()),
				2 => HashCode.Combine(this.IsAbsolute, segments[0].GetHashCode(), segments[1].GetHashCode()),
				_ => HashCode.Combine(this.IsAbsolute, segments[0].GetHashCode(), segments[segments.Length >> 1].GetHashCode(), segments[^1].GetHashCode())
			};
		}

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj)
		{
			return obj is FdbPath other && Equals(other);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbPath other)
		{
			return this.IsAbsolute == other.IsAbsolute && this.Segments.Span.SequenceEqual(other.Segments.Span);
		}

		public int CompareTo(FdbPath other)
		{
			// make sure that both are of the same kind
			if (this.IsAbsolute ^ other.IsAbsolute)
			{
				throw new InvalidOperationException("Cannot compare absolute with relative paths");
			}

			var left = this.Segments.Span;
			var right = other.Segments.Span;
			int len = Math.Min(left.Length, right.Length);

			for (int i = 0; i < len; i++)
			{
				int cmp = left[i].CompareTo(right[i]);
				if (cmp != 0) return cmp;
			}

			return left.Length.CompareTo(right.Length);
		}

		/// <summary>Tests if two paths are equal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(FdbPath left, FdbPath right)
		{
			return left.IsAbsolute == right.IsAbsolute && left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		/// <summary>Tests if two paths are not equal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(FdbPath left, FdbPath right)
		{
			return left.IsAbsolute != right.IsAbsolute || !left.Segments.Span.SequenceEqual(right.Segments.Span);
		}

		/// <summary>Combine two paths</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPath operator +(FdbPath head, FdbPath tail)
			=> head.Add(tail);

		/// <summary>Appends a path segment to a path</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbPath operator +(FdbPath path, FdbPathSegment segment)
			=> path.Add(segment);

		#endregion

		#region JSON Serialization...

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(this.ToString());
		}

		/// <inheritdoc />
		static FdbPath IJsonDeserializable<FdbPath>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value is JsonNull) return FdbPath.Empty;
			if (value is not JsonString str) throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(FdbPath));

			return FdbPath.Parse(str.Value);
		}

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			return JsonString.Return(this.ToString());
		}

		#endregion

	}

}
