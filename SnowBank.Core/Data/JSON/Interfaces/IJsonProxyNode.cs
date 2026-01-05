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

namespace SnowBank.Data.Json
{
	using SnowBank.Buffers;

	/// <summary>Node in the hierarchy of a document proxy</summary>
	[PublicAPI]
	public interface IJsonProxyNode
	{

		/// <summary>Parent of this node</summary>
		/// <remarks>If <c>null</c>, this node if the root of the wrapped document</remarks>
		IJsonProxyNode? Parent { get; }

		/// <summary>Segment of path from the parent to this node</summary>
		JsonPathSegment Segment { get; }

		/// <summary>Type of JSON value wrapped by this node</summary>
		JsonType Type { get; }

		/// <summary>Number of nodes between the root and this node</summary>
		int Depth { get; }

		/// <summary>Returns the path to this node, from the root</summary>
		JsonPath GetPath();

		/// <summary>Returns the path to a child of this value, from the root</summary>
		JsonPath GetPath(JsonPathSegment child);

		/// <summary>Write the path from the root to this node</summary>
		/// <param name="builder">Builder for the path</param>
		void WritePath(ref JsonPathBuilder builder);

	}

	/// <summary>Extension methods for working with <see cref="IJsonProxyNode"/></summary>
	public static class JsonProxyNodeExtensions
	{

		/// <summary>Returns the path to the given node, from the root</summary>
		public static JsonPath ComputePath(IJsonProxyNode? parent, in JsonPathSegment segment)
		{
			// in most cases, we don't have any parent
			if (parent == null)
			{
				return JsonPath.Create(segment);
			}

			return GetPathMultiple(parent, segment);

			static JsonPath GetPathMultiple(IJsonProxyNode? parent, JsonPathSegment segment)
			{
				using var buf = new ValueBuffer<JsonPathSegment>(8);
				while (parent != null)
				{
					buf.Add(segment);
					segment = parent.Segment;
					parent = parent.Parent;
				}

				return JsonPath.FromSegments(buf.Span, reversed: true);
			}
		}

		/// <summary>Returns the path to a child of this node, from the root</summary>
		public static JsonPath ComputePath(IJsonProxyNode? parent, in JsonPathSegment segment, JsonPathSegment child)
		{
			return child.TryGetName(out var name) ? ComputePath(parent, in segment, name)
				: child.TryGetIndex(out var index) ? ComputePath(parent, in segment, index)
				: ComputePath(parent, in segment);
		}

		/// <summary>Returns the path to a field of this object, from the root</summary>
		/// <param name="parent">Parent of this node (or <c>null</c> if this is the top level)</param>
		/// <param name="segment">Name or index of this node in its parent</param>
		/// <param name="name">Name of a field in this object</param>
		public static JsonPath ComputePath(IJsonProxyNode? parent, in JsonPathSegment segment, ReadOnlyMemory<char> name)
		{
			Contract.Debug.Requires(name.Length > 0);

			if (segment.IsEmpty())
			{
				return JsonPath.Create(new JsonPathSegment(name));
			}

			Span<char> scratch = stackalloc char[32];
			var writer = new JsonPathBuilder(scratch);
			try
			{
				parent?.WritePath(ref writer);
				writer.Append(in segment);
				writer.Append(name);
				return writer.ToPath();
			}
			finally
			{
				writer.Dispose();
			}
		}

		/// <summary>Returns the path to an item of this array, from the root</summary>
		/// <param name="parent">Parent of this node (or <c>null</c> if this is the top level)</param>
		/// <param name="segment">Name or index of this node in its parent</param>
		/// <param name="index">Index of the item in this array</param>
		public static JsonPath ComputePath(IJsonProxyNode? parent, in JsonPathSegment segment, Index index)
		{
			if (segment.IsEmpty())
			{
				return JsonPath.Create(index);
			}

			Span<char> scratch = stackalloc char[32];
			// ReSharper disable once NotDisposedResource
			var writer = new JsonPathBuilder(scratch);
			try
			{
				parent?.WritePath(ref writer);
				writer.Append(in segment);
				writer.Append(index);
				return writer.ToPath();
			}
			finally
			{
				writer.Dispose();
			}
		}

		/// <summary>Computes the path segments to a node, including an optional child.</summary>
		public static JsonPathSegment[] GetPathSegments(this IJsonProxyNode node, JsonPathSegment child = default)
		{
			var hasChild = !child.IsEmpty();
			var depth = node.Depth;

			if (depth == 0)
			{
				return hasChild ? [ child ] : [ ];
			}

			var buffer = new JsonPathSegment[checked(depth + (hasChild ? 1 : 0))];
			if (hasChild)
			{
				buffer[depth] = child;
			}

			var current = node;
			int p = depth - 1;
			while (current.Parent != null)
			{
				Contract.Debug.Assert(p >= 0);
				buffer[p--] = current.Segment;
				current = current.Parent;
			}
			Contract.Debug.Assert(p == -1);
			return buffer;
		}

		/// <summary>Computes the path segments to a child of a node.</summary>
		public static bool TryGetPathSegments(this IJsonProxyNode node, JsonPathSegment child, Span<JsonPathSegment> buffer, out int written)
		{
			bool hasChild = !child.IsEmpty();
			int depth = node.Depth;
			int capacity = checked(depth + (hasChild ? 1 : 0));

			if (buffer.Length < capacity)
			{
				written = 0;
				return false;
			}

			if (hasChild)
			{
				buffer[depth] = child;
			}

			var current = node;
			int p = depth - 1;
			while(current.Parent != null)
			{
				Contract.Debug.Assert(p >= 0);
				buffer[p--] = current.Segment;
				current = current.Parent;
			}
			Contract.Debug.Assert(p == -1);
			written = capacity;
			return true;
		}

	}

}
