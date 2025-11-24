#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Runtime.InteropServices;

	/// <summary>Represents the type of read access to a node in an observable JSON document</summary>
	[PublicAPI]
	public enum ObservableJsonAccess
	{
		/// <summary>The node was not accessed, or it was only traversed to reach any of its children</summary>
		/// <remarks>
		/// <para>If the node was traversed to reach a child, it will be recorded by another entry.</para>
		/// <para>The associated argument should be <c>null</c></para>
		/// <para>For example, in <c>{ point: [ 1, 0, 1 ] } => { point: [ 1, 1, 1 ] }</c> the node 'point' was only traversed, and any change in the outcome would be caused by a child, `point[1]` in this case.</para>
		/// </remarks>
		None = 0,

		/// <summary>The content of the node was used.</summary>
		/// <remarks>
		/// <para>The outcome could change if the value of the node changes from any value to any other value</para>
		/// <para>The associated argument will be the value of this field in the original document</para>
		/// <para>Example: <c>{ x: 123 } => { x: 456 }</c> will change the outcome, but <c>{ x: 123 } => { x: 123, y: 456 }</c> will not.</para>
		/// </remarks>
		Value,

		/// <summary>Only the length of the array, number of field in the object, or length of the string was used.</summary>
		/// <remarks>
		/// <para>The outcome would only change if the length of the array would change (added/removed).</para>
		/// <para>The associated argument must be the length of array in the original document, or <see cref="JsonNull.Missing"/> if the node was missing, or not an array.</para>
		/// <para>Example: <c>{ xs: [ 1, 2, 3 ] } => { xs: [ 4, 5, 6 ] }</c> will not change the outcome, but <c>{ xs: [ 1, 2, 3 ] } => { xs: [ 1, 2, 3, 4 ] }</c> or { xs: [ 1, 2, 3 ] } => { xs: null } will.</para>
		/// <para>Example: <c>{ xs: { "hello": "world" } } => { xs: { "hello": "there" } }</c> will not change the outcome, but <c>{ xs: { "hello": "world" } } => { xs: { "hello": "world", "foo": "bar" } }</c> or <c>{ xs: { "hello": "world" } } => { xs: null } will.</c></para>
		/// <para>Example: <c>{ xs: { "hello": "world" } } => { xs: { "hello": "there" } }</c> will not change the outcome, but <c>{ xs: { "hello": "world" } } => { xs: { "hello": "world!" } }</c> or <c>{ xs: { "hello": "world" } } => { xs: { "hello": null } } will.</c></para>
		/// </remarks>
		Length,

		/// <summary>Only the presence of the node was used.</summary>
		/// <remarks>
		/// <para>The outcome would only change if the node comes from null/missing to non-null, or vice versa.</para>
		/// <para>The associated argument will be <c>true</c> if the node exists, or <c>false</c> if the node does not exist (null or missing) in the original document.</para>
		/// <para>Example: <c>{ x: 123 } => { x: null }</c> or { } => { x: 123 } will change the outcome, but <c>{ x: 123 } => { x: 456 }</c> will not.</para>
		/// </remarks>
		Exists,

		/// <summary>Only the type of the node was used.</summary>
		/// <remarks>
		/// <para>The outcome would only change if the type of node would change (including to and from null)</para>
		/// <para>The associated argument will be the integer value of the corresponding <see cref="JsonType"/> enum</para>
		/// <para>This is more precise than <see cref="Exists"/> which only differentiate between <see cref="JsonType.Null"/> or any other type.</para>
		/// <para>Example: <c>{ x: "hello" } => { x: "world" }</c> will not change the outcome, but <c>{ x: "hello" } => { x: [ "hello", "there"] }</c> or { x: "hello" } => { x: null } will.</para>
		/// </remarks>
		Type,

	}

	/// <summary>Context that will record all the reads performed on a <see cref="ObservableJsonValue"/></summary>
	[PublicAPI]
	public interface IObservableJsonContext
	{

		/// <summary>Reset the context to its initial state, reverting any previous mutations.</summary>
		/// <remarks>This method can be used to reuse the current context for a different session</remarks>
		void Reset();

		/// <summary>Wraps a top-level JSON into an observable document</summary>
		ObservableJsonValue FromJson(JsonValue value);

		/// <summary>Wraps a children JSON value into an observable document node</summary>
		/// <param name="parent">Parent node</param>
		/// <param name="path">Path from the parent node to the child node</param>
		/// <param name="value">Value of the child node</param>
		/// <returns>Observable child node</returns>
		ObservableJsonValue FromJson(IJsonProxyNode? parent, JsonPathSegment path, JsonValue value);

		/// <summary>Records the access to a node, or one of its children</summary>
		/// <param name="node">Node that was accessed.</param>
		/// <param name="child">Path to the accessed child, or <see cref="JsonPathSegment.Empty"/> if <paramref name="node"/> itself was accessed</param>
		/// <param name="argument">Current value of the accessed part, or <see cref="JsonNull.Missing"/> if the part does not exist</param>
		/// <param name="access">Specifies how the value was used by the reader</param>
		/// <remarks>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Null"/>, it means the accessed part is present in <paramref name="node"/>, but with a <c>null</c> value.</para>
		/// <para>If <paramref name="argument"/> is <see cref="JsonNull.Missing"/>, it means the accessed part is not present in <paramref name="node"/>.</para>
		/// </remarks>
		/// <example><code>
		/// ctx.RecordRead(obj, new("hello"), obj["hello"], ObservableJsonAccess.Value); // read the value of the 'hello' field of obj
		/// ctx.RecordRead(arr, new(2), obj[2], ObservableJsonAccess.Value); // read the value of the first item in arr
		/// ctx.RecordRead(obj, default, obj, ObservableJsonAccess.Value); // read the value of the object itself (ex: conversion to a CLR object, serialization to JSON text, ...)
		/// ctx.RecordRead(arr, default, arr, ObservableJsonAccess.Length); // used the length of the array, but not its content (ex: read the Count property, or accessed an item with [^1], ...)
		/// ctx.RecordRead(value, default, value, ObservableJsonAccess.Exists); // tested if the value is null or missing
		/// ctx.RecordRead(obj, new("hello"), obj["hello"], ObservableJsonAccess.Exists); // tested for the present of the 'hello' field of obj
		/// </code></example>
		void RecordRead(IJsonProxyNode node, JsonPathSegment child, JsonValue argument, ObservableJsonAccess access);

	}

	/// <summary>Context that will capture all read access to an <see cref="ObservableJsonValue"/> into a <see cref="ObservableJsonTrace"/></summary>
	public sealed class ObservableJsonTraceCapturingContext : IObservableJsonContext
	{

		/// <summary>Trace that recorded all reads to the observed document</summary>
		public ObservableJsonTrace Trace { get; } = new();

		/// <summary>Wraps a top-level <see cref="JsonValue"/> into a <see cref="ObservableJsonValue"/> that will be monitored by this context</summary>
		/// <param name="value">JSON value that represents the top-most object or array of the document</param>
		/// <remarks>Mutable value that will notify this context whenever it (or any of its descendant) is accessed</remarks>
		public ObservableJsonValue FromJson(JsonValue value) => new(this, null, JsonPathSegment.Empty, value);

		/// <summary>Wraps a <see cref="JsonValue"/> into a <see cref="ObservableJsonValue"/> that will be monitored by this context</summary>
		/// <param name="parent">Parent that contains this value</param>
		/// <param name="path">Name or index of this value in its parent</param>
		/// <param name="value">JSON value that must be wrapped</param>
		/// <remarks>Mutable value that will notify this context whenever it (or any of its descendant) is accessed</remarks>
		public ObservableJsonValue FromJson(IJsonProxyNode? parent, JsonPathSegment path, JsonValue value) => new(this, parent, path, value);

		/// <inheritdoc />
		public void RecordRead(IJsonProxyNode node, JsonPathSegment child, JsonValue argument, ObservableJsonAccess access)
		{
			Contract.Debug.Requires(node != null);

			// we assume that most accesses are child of the top-level node, were we can skip the allocation for the segments array
			if (node.Depth == 0)
			{
				this.Trace.Add(child, access, argument);
			}
			else
			{
				var pool = ArrayPool<JsonPathSegment>.Shared;
				var segments = pool.Rent(node.Depth);
				if (!node.TryGetPathSegments(child, segments, out var len))
				{ // bug in the depth computation?
#if DEBUG
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
					throw new InvalidOperationException(); 
				}
				this.Trace.Add(segments.AsSpan(0, len), access, argument);
				segments.AsSpan(0, len).Clear();
				pool.Return(segments, clearArray: true);
			}
		}

		/// <inheritdoc />
		public void Reset()
		{
			this.Trace.Clear();
		}

	}

	/// <summary>Recording of all the nodes of a JSON document that were accessed by a previous computation.</summary>
	/// <remarks>This holds the values of all the fields and items that were read, which can then be compared against a new revision of the document, in order to determine if the same computation would now produce a different result.</remarks>
	public sealed class ObservableJsonTrace
	{

		/// <summary>Represents a node in the subtree of a monitored JSON document</summary>
		[DebuggerDisplay("Access={Access}, Value={Value}, Children={Children != null ? Children.Count : 0}")]
		public struct Node
		{
			/// <summary>Access type of this node</summary>
			/// <remarks>If <see cref="ObservableJsonAccess.None"/>, the node was only traversed to reach one or more of its children</remarks>
			public ObservableJsonAccess Access;
			/// <summary>Captured value of this node in the original document (or <c>null</c> if the node was only traversed)</summary>
			public JsonValue? Value;
			/// <summary>Optional list of accessed children</summary>
			public Dictionary<JsonPathSegment, Node>? Children;
		}

		/// <summary>Root node of the document</summary>
		public Node Root;
		// important: must be a field!

		/// <summary>Update the trace to record access to a leaf</summary>
		/// <remarks>Will automatically handle merging any previous access to the same leaf (or any of its children)</remarks>
		private static void UpdateLeafAccess(ref Node leaf, ObservableJsonAccess access, JsonValue? value)
		{
			switch (access)
			{
				case ObservableJsonAccess.Value:
				{
					// Value is stronger than all the other types
					leaf.Access = ObservableJsonAccess.Value;
					leaf.Value = value;
					// prune any recorded children
					leaf.Children = null;
					break;
				}
				case ObservableJsonAccess.Exists:
				{
					// Exists has less priority than any of the other types
					if (leaf.Access is ObservableJsonAccess.None)
					{
						leaf.Access = access;
						leaf.Value = JsonBoolean.Return(!value.IsNullOrMissing());
					}
					break;
				}
				case ObservableJsonAccess.Type:
				{
					// Type only has priority over Exists
					if (leaf.Access is ObservableJsonAccess.None or ObservableJsonAccess.Exists)
					{
						leaf.Access = access;
						leaf.Value = JsonNumber.Return((int) (value?.Type ?? JsonType.Null));
					}
					break;
				}
				case ObservableJsonAccess.Length:
				{
					// Length is a superset of Exists, is less than Value
					if (leaf.Access is ObservableJsonAccess.None or ObservableJsonAccess.Exists or ObservableJsonAccess.Type)
					{
						leaf.Access = ObservableJsonAccess.Length;
						leaf.Value = value switch
						{
							JsonArray arr  => JsonNumber.Return(arr.Count), // number of items in the array
							JsonObject obj => JsonNumber.Return(obj.Count), // number of fields in the object
							JsonString str => JsonNumber.Return(str.Value.Length), // length of the string
							_              => JsonNull.Missing,
						};
					}
					break;
				}
				default:
				{
					throw new NotSupportedException("Unsupported access mode");
				}
			}
		}

		/// <summary>Records the access to a direct child of top-level document</summary>
		/// <param name="segment">Name or index of the child that was accessed</param>
		/// <param name="access">Type of access</param>
		/// <param name="value">Captured value of the child</param>
		public void Add(JsonPathSegment segment, ObservableJsonAccess access, JsonValue? value)
		{
			ref Node current = ref this.Root;

			if (current.Access == ObservableJsonAccess.Value)
			{ // we are already observing the whole object!
				return;
			}

			if (!segment.IsEmpty())
			{
				var children = current.Children;
				if (children == null)
				{
					current.Children = children = new(JsonPathSegment.Comparer.Default);
				}
				current = ref CollectionsMarshal.GetValueRefOrAddDefault(children, segment, out _);
			}

			UpdateLeafAccess(ref current, access, value);
		}

		/// <summary>Records the access to a node of the document</summary>
		/// <param name="path">Path to the node that was accessed</param>
		/// <param name="access">Type of access</param>
		/// <param name="value">Captured value of the node</param>
		public void Add(ReadOnlySpan<JsonPathSegment> path, ObservableJsonAccess access, JsonValue? value)
		{
			ref Node current = ref this.Root;

			foreach (var segment in path)
			{
				if (current.Access == ObservableJsonAccess.Value)
				{ // we are already observing the whole object!
					return;
				}

				var children = current.Children;
				if (children == null)
				{
					current.Children = children = new(JsonPathSegment.Comparer.Default);
				}

				current = ref CollectionsMarshal.GetValueRefOrAddDefault(children, segment, out _);
			}

			UpdateLeafAccess(ref current, access, value);
		}

		/// <summary>Clears all records from this trace</summary>
		/// <remarks>After this call, this instance can be reused for a new capture session</remarks>
		public void Clear()
		{
			this.Root = default;
		}

		/// <summary>Tests if all nodes captured by this trace are still equal in a new version of the JSON document</summary>
		/// <param name="value">New version of the document</param>
		/// <returns><c>true</c> if <paramref name="value"/> would produce the exact same trace than the previous version, or <c>false</c> if there is at least one difference.</returns>
		public bool IsMatch(JsonValue value)
		{
			return VisitNode(in this.Root, value);

			static bool VisitNode(in Node current, JsonValue value)
			{
				switch (current.Access)
				{
					case ObservableJsonAccess.Value:
					{
						// since we check the entire value, there is no point in also checking any children
						return current.Value?.StrictEquals(value) ?? false;
					}
					case ObservableJsonAccess.Exists:
					{
						if (current.Value is JsonBoolean b)
						{ // verify the existence
							if (value.IsNullOrMissing())
							{ // no value
								if (b.Value)
								{ // the value disappeared
									return false;
								}
							}
							else
							{ // exists
								if (!b.Value)
								{ // the value appeared
									return false;
								}
							}
						}
						break;
					}
					case ObservableJsonAccess.Type:
					{
						if (current.Value is JsonNumber num)
						{ // verify the type
							if ((JsonType) num.ToInt32() != value.Type)
							{ // the type changed!
								return false;
							}
						}
						break;
					}
					case ObservableJsonAccess.Length:
					{
						if (current.Value is JsonNumber len)
						{ // verify the array length
							if (value is JsonArray arr)
							{ // still an array
								if (arr.Count != len.ToInt32())
								{ // but the length changed!
									return false;
								}
							}
							else
							{ // this is not an array anymore!
								return false;
							}
						}
						break;
					}
				}

				if (current.Children != null)
				{ // we need to check all the children
					foreach (var (segment, node) in current.Children)
					{
						var child = value[segment];
						if (!VisitNode(in node, child))
						{
							return false;
						}
					}
				}

				return true;
			}
		}

		/// <summary>Returns all the access records captured by this trace</summary>
		public List<(JsonPath Path, ObservableJsonAccess Access, JsonValue? Value)> GetRecords()
		{
			var res = new List<(JsonPath Path, ObservableJsonAccess Access, JsonValue? Value)>();

			Span<char> scratch = stackalloc char[64];
			var builder = new JsonPathBuilder(scratch);
			try
			{
				VisitNode(res, in this.Root, ref builder);
			}
			finally
			{
				builder.Dispose();
			}
			return res;

			static void VisitNode(List<(JsonPath Path, ObservableJsonAccess Access, JsonValue? Value)> res, in Node current, ref JsonPathBuilder path)
			{
				if (current.Access is not ObservableJsonAccess.None)
				{
					res.Add((path.ToPath(), current.Access, current.Value));
				}

				if (current.Children != null)
				{ // we need to check all the children
					foreach (var (segment, node) in current.Children)
					{
						int pos = path.Length;
						path.Append(segment);
						VisitNode(res, in node, ref path);
						path.Length = pos;
					}
				}
			}
		}

	}

}
