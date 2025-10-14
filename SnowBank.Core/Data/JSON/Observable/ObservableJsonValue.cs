#region Copyright (c) 2023-2025 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

#pragma warning disable IL2091

namespace SnowBank.Data.Json
{
	using System.ComponentModel;

	/// <summary>Observable JSON Object that will capture all reads</summary>
	[DebuggerDisplay("{ToStringUntracked(),nq}")]
	[PublicAPI]
	public sealed class ObservableJsonValue : IJsonProxyNode, IJsonSerializable, IJsonPackable
		, IEnumerable<ObservableJsonValue>
		, IEquatable<ObservableJsonValue>, IComparable<ObservableJsonValue>
		, IEquatable<JsonValue>, IComparable<JsonValue>
		, IFormattable
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, IUtf8SpanFormattable
#endif
	{

		/// <summary>Constructs a <see cref="ObservableJsonValue"/></summary>
		/// <param name="ctx">Tracking context (optional)</param>
		/// <param name="parent">Parent of this value (or <c>null</c> if root)</param>
		/// <param name="segment">Path from the parent to this node (or <see cref="JsonPathSegment.Empty"/> if root)</param>
		/// <param name="json">Value of this node</param>
		public ObservableJsonValue(IObservableJsonContext? ctx, IJsonProxyNode? parent, JsonPathSegment segment, JsonValue json)
		{
			Contract.Debug.Requires(json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Segment = segment;
			this.Json = json;
			this.Depth = (parent?.Depth ?? -1) + 1;
		}

		/// <summary>Untracked singleton for an explicit <c>null</c> value</summary>
		public static readonly ObservableJsonValue Null = new(null, null, default, JsonNull.Null);

		/// <summary>Untracked singleton for "missing" values</summary>
		/// <remarks>This can be used as the root of an observable document that does not exist yet, or has been deleted.</remarks>
		public static readonly ObservableJsonValue Missing = new(null, null, default, JsonNull.Missing);

		/// <summary>Untracked singleton for <c>false</c></summary>
		public static readonly ObservableJsonValue False = new(null, null, default, JsonBoolean.True);

		/// <summary>Untracked singleton for <c>true</c></summary>
		public static readonly ObservableJsonValue True = new(null, null, default, JsonBoolean.True);

		/// <summary>Untracked singleton for <c>0</c></summary>
		public static readonly ObservableJsonValue Zero = new(null, null, default, JsonNumber.Zero);

		/// <summary>Untracked singleton for <c>[ ]</c></summary>
		public static readonly ObservableJsonValue EmptyArray = new(null, null, default, JsonArray.ReadOnly.Empty);

		/// <summary>Untracked singleton for <c>{ }</c></summary>
		public static readonly ObservableJsonValue EmptyObject = new(null, null, default, JsonObject.ReadOnly.Empty);

		/// <summary>Returns an untracked JSON value</summary>
		/// <remarks>This may return singleton instances for well-known values (null, true/false, 0, empty array of object, ...)</remarks>
		[Pure, MustUseReturnValue]
		public static ObservableJsonValue Untracked(JsonValue value)
		{
			// return singletons for well known values
			switch (value)
			{
				case JsonNull jNull:
				{
					if (ReferenceEquals(jNull, JsonNull.Missing)) return Missing;
					if (ReferenceEquals(jNull, JsonNull.Null)) return Null;
					break;
				}
				case JsonNumber jNum:
				{
					if (ReferenceEquals(jNum, JsonNumber.Zero)) return Zero;
					break;
				}
				case JsonBoolean jBool:
				{
					return jBool.Value ? True : False;
				}
				case JsonObject jObj:
				{
					if (ReferenceEquals(jObj, JsonObject.ReadOnly.Empty)) return EmptyObject;
					break;
				}
				case JsonArray jArr:
				{
					if (ReferenceEquals(jArr, JsonArray.ReadOnly.Empty)) return EmptyArray;
					break;
				}
			}

			// allocate a new wrapper
			return new(null, null, default, value);
		}

		/// <summary>Returns an JSON value tracked by a context</summary>
		[Pure, MustUseReturnValue]
		public static ObservableJsonValue Tracked(IObservableJsonContext ctx, JsonValue value) => new(ctx, null, default, value);

		/// <summary>Contains the read-only version of this JSON object</summary>
		private JsonValue Json { get; }

		/// <summary>Expose the underlying <see cref="JsonValue"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		[Obsolete("Use ToJsonValue() instead")] //TODO: => remove this after a while, once we are sure that the code-gen has been upgraded
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonValue ToJson()
		{
			// since we don't know what the caller will do, we have to assume that any content change would trigger a recompute
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json;
		}

		/// <summary>Expose the underlying <see cref="JsonValue"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		public JsonValue ToJsonValue()
		{
			// since we don't know what the caller will do, we have to assume that any content change would trigger a recompute
			// note: this method is used by JSON code-gen
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json;
		}

		/// <summary>Expose the underlying <see cref="JsonValue"/> of this node, without recording it as a read access</summary>
		/// <remarks>This should only be used by infrastructure that need to inspect the content of the node, outside the "view" of the context</remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonValue GetJsonUnsafe()
		{
			return this.Json;
		}

		internal IObservableJsonContext? Context { get; }

		/// <summary>Returns the tracking context that is attached to this node</summary>
		public IObservableJsonContext? GetContext() => this.Context;

		#region IJsonProxyNode...

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => this.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => this.Segment;

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => this.Json.Type;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => this.Depth;

		#endregion

		#region Path...

		internal IJsonProxyNode? Parent { get; }

		/// <summary>Returns the parent of this node</summary>
		/// <returns>Parent node, or <c>null</c> if root</returns>
		public IJsonProxyNode? GetParent() => this.Parent;

		/// <summary>Segment of path to this node from its parent</summary>
		private readonly JsonPathSegment Segment;

		/// <summary>Depth from the root to this node</summary>
		/// <remarks>Required to pre-compute the size of path segment arrays</remarks>
		internal int Depth { get; }

		/// <summary>Tests if this is the top-level node of the document</summary>
		public bool IsRoot() => this.Depth == 0;

		/// <summary>Returns the depth from the root to this value</summary>
		/// <returns>Number of parents of this value, or 0 if this is the top-level value</returns>
		public int GetDepth() => this.Depth;

		/// <inheritdoc />
		public JsonPath GetPath() => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment);

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment, child);

		/// <summary>Writes the path from the root to this node into the output buffer</summary>
		public void WritePath(ref JsonPathBuilder sb)
		{
			this.Parent?.WritePath(ref sb);
			sb.Append(this.Segment);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordSelfAccess(ObservableJsonAccess access, JsonValue? value = null)
		{
			this.Context?.RecordRead(this, default, value ?? this.Json, access);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(ReadOnlyMemory<char> name, JsonValue value, ObservableJsonAccess access) => this.Context?.RecordRead(this, new(name), value, access);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void RecordChildAccess(Index index, JsonValue value, ObservableJsonAccess access) => this.Context?.RecordRead(this, new(index), value, access);

		#endregion

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of items in this array</summary>
		public int Count
		{
			get
			{
				// inspecting the value using the debugger may unintentionally trigger a "false read" !
#if DEBUG
				Debugger.NotifyOfCrossThreadDependency();
#endif
				return TryGetCount(out var count) ? count : 0;
				//REVIEW: should we throw if this is not an array or null?
			}
		}

		/// <summary>Try getting the size of this array</summary>
		/// <param name="count">Receives the length of the array, or 0 if this is not an array</param>
		/// <returns><c>true</c> if this is an array; otherwise, <c>false</c></returns>
		public bool TryGetCount(out int count)
		{
			if (this.Json is JsonArray arr)
			{
				//note: 'Length' already implies a check on the type
				RecordSelfAccess(ObservableJsonAccess.Length, this.Json);
				count = arr.Count;
				return true;
			}
			else
			{
				RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
				count = 0;
				return false;
			}
		}

		/// <summary>Tests if the wrapped JSON value is of the given type</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfType(JsonType type)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			return this.Json.Type == type;
		}

		/// <summary>Tests if the wrapped JSON value is of the given type, or is null-or-missing</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfTypeOrNull(JsonType type)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			return this.Json.Type == type || this.Json.Type == JsonType.Null;
		}

		/// <summary>Tests if the wrapped JSON is either an empty <see cref="JsonArray"/> (<c>[ ]</c>), an empty <see cref="JsonObject"/> (<c>{ }</c>), or an empty <see cref="JsonString"/> (<c>" "</c>).</summary>
		public bool IsEmpty()
		{
			switch (this.Json)
			{
				case JsonObject obj:
				{
					RecordSelfAccess(ObservableJsonAccess.Length, obj);
					return obj.Count == 0;
				}
				case JsonArray arr:
				{
					RecordSelfAccess(ObservableJsonAccess.Length, arr);
					return arr.Count == 0;
				}
				case JsonString str:
				{
					RecordSelfAccess(ObservableJsonAccess.Length, str);
					return str.Value.Length == 0;
				}
				default:
				{
					RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
					return false;
				}
			}
		}

		/// <summary>Tests if the wrapped JSON value is a non-null array.</summary>
		/// <returns><c>true</c> if the value is an array; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsArray() => IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is either an array, or null-or-missing.</summary>
		/// <returns><c>true</c> if the value is an array, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsArrayOrMissing() => IsOfTypeOrNull(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is a non-null array.</summary>
		/// <param name="value">Receives the underlying JSON array</param>
		/// <returns><c>true</c> if the value is an array; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This method will record a <see cref="ObservableJsonAccess.Type"/> access, but any reads performed on <paramref name="value"/> will not be tracked by the attached context!</para>
		/// <para>It is intended to be used by infrastructure code that will manually record any access to the value.</para>
		/// </remarks>
		public bool IsArrayUnsafe([MaybeNullWhen(false)] out JsonArray value)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			value = this.Json as JsonArray;
			return value is not null;
		}

		/// <summary>Tests if the wrapped JSON value is a non-null object.</summary>
		/// <returns><c>true</c> if the value is an object; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsObject() => IsOfType(JsonType.Array);

		/// <summary>Tests if the wrapped JSON value is either an object, or null-or-missing.</summary>
		/// <returns><c>true</c> if the value is an object, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsObjectOrMissing() => IsOfTypeOrNull(JsonType.Object);

		/// <summary>Tests if the wrapped JSON value is a non-null object.</summary>
		/// <param name="value">Receives the underlying JSON object</param>
		/// <returns><c>true</c> if the value is an object; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>This method will record a <see cref="ObservableJsonAccess.Type"/> access, but any reads performed on <paramref name="value"/> will not be tracked by the attached context!</para>
		/// <para>It is intended to be used by infrastructure code that will manually record any access to the value.</para>
		/// </remarks>
		public bool IsObjectUnsafe([MaybeNullWhen(false)] out JsonObject value)
		{
			RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
			value = this.Json as JsonObject;
			return value is not null;
		}

		/// <summary>Tests if the wrapped JSON value is null or missing.</summary>
		/// <returns><c>true</c> if the value is <see cref="JsonNull"/>; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Exists"/> access.</remarks>
		[Pure]
		public bool IsNullOrMissing()
		{
			RecordSelfAccess(ObservableJsonAccess.Exists);
			return this.Json is JsonNull;
		}

		/// <summary>Tests if the wrapped JSON value is not null or missing.</summary>
		/// <returns><c>false</c> if the value is <see cref="JsonNull"/>; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Exists"/> access.</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool Exists() => !this.IsNullOrMissing();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(ReadOnlyMemory<char> name, JsonValue value)
		{
			return this.Context?.FromJson(this, new(name), value) ?? new(null, this, new(name), value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private ObservableJsonValue ReturnChild(Index index, JsonValue value)
		{
			return this.Context?.FromJson(this, new(index), value) ?? new(null, this, new(index), value);
		}

		/// <summary>Converts the wrapped JSON value into the specified CLR type, with a fallback value if it is null or missing.</summary>
		/// <typeparam name="TValue">Target CLR type</typeparam>
		/// <param name="defaultValue">Value returned if the wrapped JSON value is null-or-missing</param>
		/// <exception cref="JsonBindingException">If the wrapped JSON value cannot be bound to the specified type.</exception>
		/// <returns>Converted value</returns>
		/// <remarks>
		/// <para>Examples:<code>
		/// ({ "hello": "world" })["hello"].As&lt;string>() => "world"
		/// ({ "hello": null })["hello"].As&lt;string>() => null
		/// ({ "hello": null })["hello"].As&lt;string>("there") => "there"
		/// ({ /* ... */ })["hello"].As&lt;string>() => null
		/// ({ /* ... */ })["hello"].As&lt;string>("there") => "there"
		/// </code></para>
		/// </remarks>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<TValue>(TValue? defaultValue = default)
		{
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json.As<TValue>(defaultValue);
		}

		/// <summary>Converts the wrapped JSON value into the specified CLR type.</summary>
		/// <typeparam name="TValue">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the wrapped JSON value is null-or-missing, or if it cannot be bound to the specified type.</exception>
		/// <returns>Converted value</returns>
		/// <remarks>
		/// <para>Examples:<code>
		/// ({ "hello": "world" })["hello"].Required&lt;string>() => "world"
		/// ({ "hello": null })["hello"].Required&lt;string>() => throws
		/// ({ /* ... */ })["hello"].Required&lt;string>() => throws
		/// </code></para>
		/// </remarks>
		public TValue Required<TValue>() where TValue : notnull
		{
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json.Required<TValue>();
		}

		/// <summary>Tests if the wrapped JSON value is an object that contains a field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns><c>true</c> if the current value is an object, and if the corresponding field has a non-null value; otherwise, <c>false</c></returns>
		/// <remarks>
		/// <para>Examples:<code>
		/// ({ "hello": "world" }).ContainsKey("hello") => true
		/// ({ "hello": "world" }).ContainsKey("other") => false
		/// ([ "one", "two", "three" ]).ContainsKey("two") => false
		/// </code></para>
		/// </remarks>
		public bool ContainsKey(string name)
		{
			if (!this.Json.TryGetPathValue(name, out var child))
			{
				RecordChildAccess(name.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Exists);
				return false;
			}
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Exists);
			return true;
		}

		/// <summary>Tests if the wrapped JSON value is either an array or an object that contains the specified value</summary>
		/// <param name="value">Value that is being searched.</param>
		/// <returns><c>true</c> if the current value is an array that contains <paramref name="value"/>, or an object with a field whose value is equal to <paramref name="value"/>; otherwise, <c>false</c>;</returns>
		/// <remarks>
		/// <para>Examples:<code>
		/// ({ "hello": "world" }).ContainsValue("world") => true
		/// ({ "hello": "world" }).ContainsValue("there") => false
		/// ([ "one", "two", "three" ]).ContainsValue("two") => true
		/// ([ "one", "two", "three" ]).ContainsValue("four") => false
		/// </code></para>
		/// </remarks>
		public bool ContainsValue(JsonValue value)
		{
			RecordSelfAccess(ObservableJsonAccess.Value);
			return this.Json switch
			{
				JsonObject obj => obj.Contains(value),
				JsonArray arr => arr.Contains(value),
				_ => false
			};
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		public ObservableJsonValue this[string name]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(name);
		}

		/// <summary>Returns a wrapper for the node at the specified path</summary>
		/// <param name="path">Path of the node to return</param>
		/// <returns>Corresponding node, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		public ObservableJsonValue this[JsonPath path]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(path);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		public ObservableJsonValue this[ReadOnlyMemory<char> name]
		{
			[MustUseReturnValue]
			get => Get(name);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		public ObservableJsonValue this[int index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		public ObservableJsonValue this[Index index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		#region TryGetValue...

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(string name, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(name.AsMemory(), JsonNull.Missing);
				return false;
			}

			value = ReturnChild(name.AsMemory(), child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(ReadOnlyMemory<char> name, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(name, JsonNull.Missing);
				return false;
			}

			value = ReturnChild(name, child);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(string name, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(name.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> name, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(name, JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(name, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Receives the unpacked value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(string name, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(name.AsMemory(), JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value of the given field, if it is not null or missing</summary>
		/// <param name="name">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Receives the unpacked value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> name, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(name, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(name, JsonNull.Missing, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(name, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(int index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(index, JsonNull.Error);
				return false;
			}

			value = ReturnChild(index, child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(int index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		[Pure]
		public bool TryGetValue(Index index, out ObservableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = ReturnChild(index, JsonNull.Error);
				return false;
			}

			value = ReturnChild(index, child);
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(Index index, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <typeparam name="TValue">Type of the returned value</typeparam>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Object that can deserialize instances of <typeparamref name="TValue"/> from JSON values</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(Index index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				RecordChildAccess(index, JsonNull.Error, ObservableJsonAccess.Value);
				value = default;
				return false;
			}

			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value of the given child, if it is not null or missing</summary>
		/// <param name="segment">Name of the field in this object, or location in this array.</param>
		/// <param name="value">Value that represents this child.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(JsonPathSegment segment, out ObservableJsonValue value)
		{
			if (segment.TryGetName(out var name))
			{
				return TryGetValue(name, out value);
			}
			if (segment.TryGetIndex(out var index))
			{
				return TryGetValue(index, out value);
			}

			value = this;
			return Exists();
		}

		/// <summary>Returns the value of the given child, if it is not null or missing</summary>
		/// <param name="segment">Name of the field in this object, or location in this array.</param>
		/// <param name="value">Value that represents this child.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(JsonPathSegment segment, [MaybeNullWhen(false)] out TValue value)
		{
			if (segment.TryGetName(out var name))
			{
				return TryGetValue<TValue>(name, out value);
			}
			if (segment.TryGetIndex(out var index))
			{
				return TryGetValue<TValue>(index, out value);
			}

			var json = ToJsonValue();
			if (json.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = json.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the given child, if it is not null or missing</summary>
		/// <param name="segment">Name of the field in this object, or location in this array.</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this child.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(JsonPathSegment segment, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			if (segment.TryGetName(out var name))
			{
				return TryGetValue<TValue>(name, converter, out value);
			}
			if (segment.TryGetIndex(out var index))
			{
				return TryGetValue<TValue>(index, converter, out value);
			}

			var json = ToJsonValue();
			if (json.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(json, null);
			return true;
		}

		/// <summary>Returns the value of the element at the given path, if it is not null or missing</summary>
		/// <param name="path">Path to the element.</param>
		/// <param name="value">Value that represents this element.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue(JsonPath path, out ObservableJsonValue value)
		{
			value = Get(path);
			return value.Exists();
		}

		/// <summary>Returns the value of the element at the given path, if it is not null or missing</summary>
		/// <param name="path">Path to the element.</param>
		/// <param name="value">Value that represents this element, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(JsonPath path, [MaybeNullWhen(false)] out TValue value)
		{
			var child = GetValue(path);
			
			if (child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the element at the given path, if it is not null or missing</summary>
		/// <param name="path">Path to the element.</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this element, converted into <typeparamref name="TValue"/> using <paramref name="converter"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		[Pure]
		public bool TryGetValue<TValue>(JsonPath path, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var child = GetValue(path);
			
			if (child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child, null);
			return true;
		}

		#endregion

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(string name) => ReturnChild(name.AsMemory(), this.Json.GetValueOrDefault(name));

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(string name)
		{
			var value = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Value);
			return value;
		}

		/// <summary>Returns the underlying <see cref="JsonArray"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding array</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is either null-or-missing, or not an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonArray GetArray(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonArray array)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
			}
			RecordChildAccess(name.AsMemory(), array, ObservableJsonAccess.Value);
			return array;
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is neither null-or-missing, nor an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonArray? GetArrayOrDefault(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonArray array)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				return value is JsonNull ? null : throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
			}
			RecordChildAccess(name.AsMemory(), array, ObservableJsonAccess.Value);
			return array;
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is neither null-or-missing, nor an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonArray GetArrayOrEmpty(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonArray array)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				return value is JsonNull ? JsonArray.ReadOnly.Empty : throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
			}
			RecordChildAccess(name.AsMemory(), array, ObservableJsonAccess.Value);
			return array;
		}

		/// <summary>Returns the underlying <see cref="JsonArray"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding array</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is either null-or-missing, or not an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonObject GetObject(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonObject obj)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value);
			}
			RecordChildAccess(name.AsMemory(), obj, ObservableJsonAccess.Value);
			return obj;
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is neither null-or-missing, nor an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonObject? GetObjectOrDefault(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonObject obj)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				return value is JsonNull ? null : throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value);
			}
			RecordChildAccess(name.AsMemory(), obj, ObservableJsonAccess.Value);
			return obj;
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		/// <exception cref="JsonBindingException">If the field is neither null-or-missing, nor an array.</exception>
		[Pure, MustUseReturnValue]
		public JsonObject GetObjectOrEmpty(string name)
		{
			var value = this.Json.GetValue(name);
			if (value is not JsonObject obj)
			{
				RecordChildAccess(name.AsMemory(), value, ObservableJsonAccess.Type);
				return value is JsonNull ? JsonObject.ReadOnly.Empty : throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value);
			}
			RecordChildAccess(name.AsMemory(), obj, ObservableJsonAccess.Value);
			return obj;
		}

		/// <summary>Reads the value of the <b>required</b> field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Corresponding value.</returns>
		/// <exception cref="JsonBindingException"> if the field is null or missing, or if its value could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(string name) where TValue : notnull
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			if (child.IsNullOrMissing())
			{
				var path = this.GetPath(name);
				throw new JsonBindingException($"Required JSON field '{path}' was null or missing", path, child, typeof(TValue));
			}
			return child.As<TValue>()!;
		}

		/// <summary>Reads the value of the optional field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="defaultValue">Value returned if the field is null or missing</param>
		/// <returns>Corresponding value</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string name, TValue defaultValue)
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualName);
			name = actualName?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			return ReturnChild(name, value);
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the field with the specified name</summary>
		/// <param name="name">Name of the field to return</param>
		/// <returns>Corresponding field</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualName);
			name = actualName?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			RecordChildAccess(name, value, ObservableJsonAccess.Value);
			return value;
		}

		/// <summary>Reads the value of the <b>required</b> field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Corresponding value.</returns>
		/// <exception cref="JsonBindingException"> if the field is null or missing, or if its value could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(ReadOnlyMemory<char> name) where TValue : notnull
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValue(name, out var actualName);
			name = actualName?.AsMemory() ?? name;
#else
			var value = this.Json.GetValue(name);
#endif
			RecordChildAccess(name, value, ObservableJsonAccess.Value);
			return value.As<TValue>()!;
		}

		/// <summary>Reads the value of the optional field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="defaultValue">Value returned if the field is null or missing</param>
		/// <returns>Corresponding value</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> name, TValue? defaultValue)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualName);
			name = actualName?.AsMemory() ?? name;
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
#endif
			RecordChildAccess(name, value, ObservableJsonAccess.Value);
			return value.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(int index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the item at the specified location</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or <see cref="JsonNull.Error"/> if the index is out of bounds, of this is not an array.</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(int index)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child;
		}

		/// <summary>Reads the value of the <b>required</b> item at the specified index</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Corresponding value</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(int index) where TValue : notnull
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValue(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As<TValue>()!;
		}

		/// <summary>Reads the value of the optional item at the specified index</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="defaultValue">Value returned if the item is null or missing</param>
		/// <returns>Corresponding value</returns>
		/// <exception cref="JsonBindingException"> if the value of this item could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue? defaultValue)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the field with the specified name</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or an error placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the array</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(Index index)
		{
			//note: this fails is not JsonArray or JsonObject!
			return ReturnChild(index, this.Json.GetValueOrDefault(index));
		}
		
		/// <summary>Returns the underlying <see cref="JsonValue"/> of the item at the specified location</summary>
		/// <param name="index">Index of the item to return</param>
		/// <returns>Corresponding item, or <see cref="JsonNull.Error"/> if the index is out of bounds, of this is not an array.</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(Index index)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child;
		}

		/// <summary>Reads the value of the <b>required</b> item at the specified index</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Corresponding value</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue? Get<TValue>(Index index) where TValue : notnull
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As<TValue>();
		}

		/// <summary>Reads the value of the optional item at the specified index</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="defaultValue">Value returned if the item is null or missing</param>
		/// <returns>Corresponding value</returns>
		/// <exception cref="JsonBindingException"> if the value of this item could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue)
		{
			//note: this fails is not JsonArray or JsonObject!
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.As(defaultValue);
		}

		/// <summary>Returns a wrapper for the descendant of this node at the specified location</summary>
		/// <param name="path">Path of the node to return</param>
		/// <returns>Corresponding node, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(JsonPath path)
		{
			var current = this;
			foreach (var segment in path)
			{
				current = current.Get(segment);
			}
			return current;
		}

		/// <summary>Returns the underlying <see cref="JsonValue"/> of the node at the specified location</summary>
		/// <param name="path">Path to the node to read</param>
		/// <returns>Corresponding value</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(JsonPath path) => Get(path).ToJsonValue();

		/// <summary>Reads the value of the <b>required</b> node at the specified location</summary>
		/// <param name="path">Path to the node to read</param>
		/// <returns>Corresponding value.</returns>
		/// <exception cref="JsonBindingException"> if the node is null or missing, or if its value could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(JsonPath path) where TValue : notnull
		{
			var value = this.Json.GetPathValueOrDefault(path);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_DescendantIsNullOrMissing(this, path, null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Reads the value of the optional node at the specified location</summary>
		/// <param name="path">Path to the node to read</param>
		/// <param name="defaultValue">Value returned if the node is null or missing</param>
		/// <returns>Corresponding value</returns>
		/// <exception cref="JsonBindingException"> if the value of this node could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(JsonPath path, TValue defaultValue) => this.Json.GetPathValueOrDefault(path).As<TValue>(defaultValue);

		/// <summary>Returns a wrapper for the child of this node at the specified location</summary>
		/// <param name="segment">Path of the node to return</param>
		/// <returns>Corresponding node, or a null-or-missing placeholder.</returns>
		/// <remarks>This operation in itself will not be recorded as a use of the object</remarks>
		[Pure, MustUseReturnValue]
		public ObservableJsonValue Get(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Get(name)
			 : segment.TryGetIndex(out var index) ? Get(index)
			 : this;

		/// <summary>Reads the value of the <b>required</b> node at the specified location</summary>
		/// <param name="segment">Path to the node to read</param>
		/// <returns>Corresponding value.</returns>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue GetValue(JsonPathSegment segment) => Get(segment).ToJsonValue();

		/// <summary>Reads the value of the <b>required</b> node at the specified location</summary>
		/// <param name="segment">Path to the node to read</param>
		/// <returns>Corresponding value.</returns>
		/// <exception cref="JsonBindingException"> if the node is null or missing, or if its value could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(JsonPathSegment segment) where TValue : notnull
		{
			var value = GetValue(segment);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, segment, null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Reads the value of the optional node at the specified location</summary>
		/// <param name="segment">Path to the node to read</param>
		/// <param name="defaultValue">Value returned if the node is null or missing</param>
		/// <returns>Corresponding value.</returns>
		/// <exception cref="JsonBindingException"> if the value of this node could not be bound to type <typeparamref name="TValue"/>.</exception>
		/// <remarks>This operation will be record as a <see cref="ObservableJsonAccess.Value"/> access.</remarks>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(JsonPathSegment segment, TValue defaultValue) => GetValue(segment).As<TValue>(defaultValue);

		/// <summary>Returns a new instance with the same path, but with the different value </summary>
		/// <param name="newValue">New value of this instance</param>
		/// <returns>Equivalent instance, that could replace the current one in its parent</returns>
		public ObservableJsonValue Visit(JsonValue? newValue)
		{
			newValue ??= JsonNull.Null;
			if (ReferenceEquals(this.Json, newValue))
			{
				return this;
			}
			return this.Context?.FromJson(this.Parent, this.Segment, newValue) ?? new(null, this.Parent, this.Segment, newValue);
		}

		#endregion

		#region ValueEquals...

		/// <summary>Tests if the value of this node is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(TValue value, IEqualityComparer<TValue>? comparer = null) => this.ToJsonValue().ValueEquals(value, comparer);

		/// <summary>Tests if the value of a field of this document is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(string name, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name.AsMemory(), child, ObservableJsonAccess.Value);
			return child.ValueEquals(value, comparer);
		}

		/// <summary>Tests if the value of a field of this document is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(ReadOnlyMemory<char> name, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			var child = this.Json.GetValueOrDefault(name);
			RecordChildAccess(name, child, ObservableJsonAccess.Value);
			return child.ValueEquals(value, comparer);
		}

		/// <summary>Tests if the value of an item of this array is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="index">Index of this item</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(int index, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.ValueEquals(value, comparer);
		}

		/// <summary>Tests if the value of an item of this array is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="index">Index of this item</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(Index index, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			var child = this.Json.GetValueOrDefault(index);
			RecordChildAccess(index, child, ObservableJsonAccess.Value);
			return child.ValueEquals(value, comparer);
		}

		/// <summary>Tests if the value of a child of this node is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="segment">Path of this child</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(JsonPathSegment segment, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			JsonValue item;
			if (segment.TryGetName(out var name))
			{
				item = this.Json.GetValueOrDefault(name);
				RecordChildAccess(name, item, ObservableJsonAccess.Value);
			}
			else if (segment.TryGetIndex(out var index))
			{
				item = this.Json.GetValueOrDefault(index);
				RecordChildAccess(index, item, ObservableJsonAccess.Value);
			}
			else
			{
				item = this.Json;
				RecordSelfAccess(ObservableJsonAccess.Value, item);
			}
			return item.ValueEquals(value, comparer);
		}

		/// <summary>Tests if the value of a descendant of this node is equal to a given value</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Equality comparer to use (optional)</param>
		public bool ValueEquals<TValue>(JsonPath path, TValue value, IEqualityComparer<TValue>? comparer = null)
		{
			//TODO: REVIEW: how can we optimize this?
			return Get(path).ValueEquals(value, comparer);
		}

		#endregion

		/// <inheritdoc />
		public override string ToString()
		{
			// note: there is an issue with the debugger calling ToString() when inspecting variables on the stack,
			// which will could automatically track the read, even though the calling code never intended for this.
			// There seems to be no 100% reliable way to prevent the debugger from invoking ToString().
			// We already have added [DebuggerDisplay(...)] which should remove most of the unwanted calls.
			// One solution would be to inspect the callstack to look for signs of the .NET debugger, but this seems too costly

			RecordSelfAccess(ObservableJsonAccess.Value, this.Json);
			return this.Json.ToString();
		}

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null)
		{
			RecordSelfAccess(ObservableJsonAccess.Value, this.Json);
			return this.Json.ToString(format, provider);
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			RecordSelfAccess(ObservableJsonAccess.Value, this.Json);
			return this.Json.TryFormat(destination, out charsWritten, format, provider);
		}

		/// <inheritdoc />
		public bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			RecordSelfAccess(ObservableJsonAccess.Value, this.Json);
			return this.Json.TryFormat(destination, out bytesWritten, format, provider);
		}

#endif

		/// <summary>Generate a string representation of this object for debugging purpose</summary>
		private string ToStringUntracked() => this.Segment.IsEmpty() ? this.Json.ToString("Q") : $"{this.GetPath()}: {this.Json.ToString("Q")}";

		/// <inheritdoc />
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc />
		public IEnumerator<ObservableJsonValue> GetEnumerator()
		{
			// we cannot know how the result will be used, so mark this a full "Value" read.

			if (this.Json is not JsonArray array)
			{
				if (this.Json is JsonNull)
				{
					RecordSelfAccess(ObservableJsonAccess.Exists, this.Json);
					yield break;
				}

				RecordSelfAccess(ObservableJsonAccess.Type, this.Json);
				throw new InvalidOperationException("Cannot iterate a non-array");
			}

			// we depend on the size of the array!
			RecordSelfAccess(ObservableJsonAccess.Length, this.Json);

			// but we don't know how the items will be consumed
			for(int i = 0; i < array.Count; i++)
			{
				yield return ReturnChild(i, array[i]);
			}
		}

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			ObservableJsonValue value => Equals(value),
			JsonValue value => Equals(value),
			null => IsNullOrMissing(),
			_ => false,
		};

		/// <inheritdoc />
		public override int GetHashCode() => this.Json.GetHashCode();
		//REVIEW: should we register this as a read? this could be bad for perf if used as a key in a dictionary, and we expect Equals(...) to be called right after??

		/// <inheritdoc />
		public bool Equals(ObservableJsonValue? other) => other == null ? IsNullOrMissing() : this.ToJsonValue().StrictEquals(other.ToJsonValue());

		/// <inheritdoc />
		public bool Equals(JsonValue? other) => other == null ? IsNullOrMissing() : this.ToJsonValue().StrictEquals(other);

		/// <inheritdoc />
		public int CompareTo(ObservableJsonValue? other) => other != null ? this.ToJsonValue().CompareTo(other.ToJsonValue()) : Exists() ? +1 : 0;

		/// <inheritdoc />
		public int CompareTo(JsonValue? other) => other != null ? this.ToJsonValue().CompareTo(other) : Exists() ? +1 : 0;

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.ToJsonValue().JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.ToJsonValue();

		/// <summary>Expose the underlying <see cref="JsonArray"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is null, missing, or not a JSON Array.</exception>
		public JsonArray AsArray() => this.ToJsonValue().AsArray();

		/// <summary>Expose the underlying <see cref="JsonArray"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is not a JSON Array.</exception>
		public JsonArray? AsArrayOrDefault() => this.ToJsonValue().AsArrayOrDefault();

		/// <summary>Expose the underlying <see cref="JsonObject"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is null, missing, or not a JSON Object.</exception>
		public JsonObject AsObject() => this.ToJsonValue().AsObject();

		/// <summary>Expose the underlying <see cref="JsonObject"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is not a JSON Object.</exception>
		public JsonObject? AsObjectOrDefault() => this.ToJsonValue().AsObjectOrDefault();

		/// <summary>Expose the underlying <see cref="JsonArray"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is null, missing, or not a JSON Array.</exception>
		public JsonArray<TValue> AsArrayOf<TValue>() where TValue: notnull
			=> AsArray().AsArrayOf<TValue>();

		/// <summary>Expose the underlying <see cref="JsonArray"/> of this node</summary>
		/// <remarks>This will we recorded as a full use of the value</remarks>
		/// <exception cref="JsonBindingException">If this node is null, missing, or not a JSON Array.</exception>
		public JsonArrayOrDefault<TValue> AsArrayOrDefaultOf<TValue>(TValue? missingValue = default)
			=> AsArray().AsArrayOrDefaultOf<TValue>(missingValue);

	}

}
