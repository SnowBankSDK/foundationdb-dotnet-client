#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Data.Json
{
	using System.ComponentModel;
	using System.Diagnostics;
	using System.Diagnostics.CodeAnalysis;
	using System.Runtime.CompilerServices;

	/// <summary>Mutable JSON Object</summary>
	[DebuggerDisplay("Count={Count}, Path={ToString(),nq}")]
	[PublicAPI]
	public sealed class MutableJsonValue : IJsonProxyNode, IJsonSerializable, IJsonPackable
		, IEnumerable<MutableJsonValue>
		, IEquatable<JsonValue>, IEquatable<MutableJsonValue>, IEquatable<ObservableJsonValue>
		, IFormattable
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, IUtf8SpanFormattable
#endif
	{

		/// <summary>Constructs a <see cref="MutableJsonValue"/></summary>
		/// <param name="ctx">Tracking context (optional)</param>
		/// <param name="parent">Parent of this value (or <c>null</c> if root)</param>
		/// <param name="segment">Path from the parent to this node (or <see cref="JsonPathSegment.Empty"/> if root)</param>
		/// <param name="json">Value of this node</param>
		public MutableJsonValue(IMutableJsonContext? ctx, MutableJsonValue? parent, JsonPathSegment segment, JsonValue json)
		{
			Contract.Debug.Requires(json != null);
			this.Context = ctx;
			this.Parent = parent;
			this.Segment = segment;
			this.Json = json;
			this.Depth = (parent?.Depth ?? -1) + 1;
		}

		/// <summary>Returns an untracked mutable JSON value</summary>
		[Pure, MustUseReturnValue]
		public static MutableJsonValue Untracked(JsonValue value) => new(null, null, default, value);

		/// <summary>Returns a mutable JSON value tracked by a transaction</summary>
		public static MutableJsonValue Tracked(IMutableJsonContext tr, JsonValue value) => new(tr, null, default, value);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			MutableJsonValue value    => ReferenceEquals(obj, this) || this.Json.StrictEquals(value.Json),
			JsonValue value           => this.Json.StrictEquals(value),
			ObservableJsonValue value => this.Json.StrictEquals(value.ToJsonValue()),
			null                      => this.IsNullOrMissing(),
			_                         => false,
		};

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int GetHashCode() => throw new NotSupportedException("This instance can change value, and MUST NOT be used as a key in a dictionary!");

		/// <inheritdoc />
		public bool Equals(MutableJsonValue? other) => ReferenceEquals(this, other) || this.Json.StrictEquals(other?.Json);

		/// <inheritdoc />
		public bool Equals(ObservableJsonValue? other) => this.Json.StrictEquals(other?.ToJsonValue());

		/// <inheritdoc />
		public bool Equals(JsonValue? other) => this.Json.StrictEquals(other);

		/// <inheritdoc />
		public override string ToString() => this.Json.ToString();

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? provider = null) => this.Json.ToString(format, provider);

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return this.Json.TryFormat(destination, out charsWritten, format, provider);
		}

		/// <inheritdoc />
		public bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
		{
			return this.Json.TryFormat(destination, out bytesWritten, format, provider);
		}

#endif

		/// <summary>Contains the read-only version of this JSON object</summary>
		internal JsonValue Json { get; private set; }

		/// <summary>Returns the <see cref="JsonValue"/> tracked by this node</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use ToJsonValue() instead!")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonValue ToJson() => this.Json;
		//TODO: => remove this after a while, once we are sure that the code-gen has been upgraded

		/// <summary>Returns the <see cref="JsonValue"/> tracked by this node</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonValue ToJsonValue()
		{
			// note: this method is used by JSON code-gen
			return this.Json;
		}

		internal IMutableJsonContext? Context { get; }

		/// <summary>Returns the tracking context that is attached to this node</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IMutableJsonContext? GetContext() => this.Context;

		#region Path...

		/// <summary>Parent of this value, or <see langword="null"/> if this is the root of the document</summary>
		internal readonly MutableJsonValue? Parent;

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => this.Parent;

		/// <summary>Returns the parent of this node</summary>
		/// <returns>Parent node, or <c>null</c> if root</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableJsonValue? GetParent() => Parent;

		/// <summary>Number of nodes between the root and this node</summary>
		internal int Depth { get; }

		int IJsonProxyNode.Depth => this.Depth;

		/// <summary>Returns the number of nodes between the root and this node</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetDepth() => this.Depth;

		/// <inheritdoc />
		public JsonType Type => this.Json.Type;

		/// <summary>Name of the field that contains this value in its parent object, or <see langword="null"/> if it was not part of an object</summary>
		internal readonly JsonPathSegment Segment;

		JsonPathSegment IJsonProxyNode.Segment => this.Segment;

		/// <summary>Tests if this is the top-level node of the document</summary>
		public bool IsRoot() => this.Segment.IsEmpty();

		/// <inheritdoc />
		public JsonPath GetPath() => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment);

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => JsonProxyNodeExtensions.ComputePath(this.Parent, in this.Segment, child);

		/// <inheritdoc />
		public void WritePath(ref JsonPathBuilder sb)
		{
			this.Parent?.WritePath(ref sb);
			sb.Append(this.Segment);
		}

		#endregion

		#region IDictionary<TKey, TValue>...

		/// <summary>Number of fields in this object</summary>
		public int Count => this.Json switch
		{
			JsonObject obj => obj.Count,
			JsonArray arr  => arr.Count,
			_              => 0 // what should we do here?
		};

		/// <summary>Tests if the wrapped JSON value is of the given type</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfType(JsonType type)
		{
			return this.Json.Type == type;
		}

		/// <summary>Tests if the wrapped JSON value is of the given type, or is null-or-missing</summary>
		/// <param name="type">Expected type of the value</param>
		/// <returns><c>true</c> if the value is of this type, null or missing; otherwise, <c>false</c></returns>
		/// <remarks>This method will record a <see cref="ObservableJsonAccess.Type"/> access.</remarks>
		public bool IsOfTypeOrNull(JsonType type)
		{
			return this.Json.Type == type || this.Json.Type == JsonType.Null;
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
			value = this.Json as JsonObject;
			return value is not null;
		}

		/// <summary>Tests if the node has no value (null or missing)</summary>
		/// <returns></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)] 
		public bool IsNullOrMissing() => this.Json is JsonNull;

		/// <summary>Tests if the value of this node is not null or missing</summary>
		public bool Exists() => this.Json is not JsonNull;

		/// <summary>Converts the optional value of this node into an instance of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Target conversion type</typeparam>
		/// <param name="defaultValue">Value returned if this node is either null or missing</param>
		/// <returns>Converted value, or <paramref name="defaultValue"/> if the node is null or missing</returns>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? As<TValue>(TValue? defaultValue = default) => this.Json.As<TValue>(defaultValue);

		/// <summary>Converts the <b>required</b> value of this node into an instance of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Target conversion type</typeparam>
		/// <returns>Converter value</returns>
		/// <exception cref="JsonBindingException">if the node is null or missing, or could not be bound to type <typeparamref name="TValue"/></exception>
		public TValue Required<TValue>() where TValue : notnull
		{
			if (this.Json is null or JsonNull)
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, default, null);
			}
			return this.Json.As<TValue>()!;
		}

		/// <summary>Tests if this document contains a field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns><c>true</c> if the field is present; otherwise, <c>false</c></returns>
		public bool ContainsKey(string name) => this.Json is JsonObject obj && obj.ContainsKey(name);

		/// <summary>Tests if this document contains a field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns><c>true</c> if the field is present; otherwise, <c>false</c></returns>
		public bool ContainsKey(ReadOnlyMemory<char> name) => this.Json is JsonObject obj && obj.ContainsKey(name);

		/// <summary>Tests if this document contains a field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns><c>true</c> if the field is present; otherwise, <c>false</c></returns>
		public bool ContainsKey(ReadOnlySpan<char> name) => this.Json is JsonObject obj && obj.ContainsKey(name);

		/// <summary>Tests if this document contains the given value</summary>
		/// <param name="value">Value to search</param>
		/// <returns><c>true</c> if the document is an object with a field that has this value, or an array with an item equal to this value; otherwise, <c>false</c>.</returns>
		public bool ContainsValue(JsonValue value) => this.Json switch
		{
			JsonObject obj => obj.Contains(value),
			JsonArray arr  => arr.Contains(value),
			_              => false
		};

		/// <summary>Returns the value of the field with the specified name</summary>
		public MutableJsonValue this[string name]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(name);
		}

		/// <summary>Returns the value of the field with the specified name</summary>
		public MutableJsonValue this[ReadOnlyMemory<char> name]
		{
			[MustUseReturnValue]
			get => Get(name);
		}

		/// <summary>Returns the value of the element at the specified location</summary>
		public MutableJsonValue this[int index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		/// <summary>Returns the value of the element at the specified location</summary>
		public MutableJsonValue this[Index index]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(index);
		}

		/// <summary>Returns the value of the child at the specified path</summary>
		public MutableJsonValue this[JsonPath path]
		{
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Get(path);
		}

		#region TryGetValue...

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root.TryGetValue("Error", out var error))
		/// { // this is the first error, will automatically create a new 'Error' object
		///		error["Attempts"] = 1;
		///		error["FirstAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": 1, "FirstAttempt": "..." } }
		/// }
		/// else
		/// { // there was already an 'Error' object, record the new attempt
		///		error["Attempts"].Increment();
		///		error["LastAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": (+1), "FirstAttempt": "...", "LastAttempt": "...", ... } }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(string key, out MutableJsonValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child))
			{
				value = new(this.Context, this, new(key), JsonNull.Missing);
				return false;
			}

			value = new(this.Context, this, new(key), child);
			return true;
		}

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar", out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(string key, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar", GeneratedConverters.Bar.Default, out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar, GeneratedConverters.Bar.Default);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(string key, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root.TryGetValue("Error", out var error))
		/// { // this is the first error, will automatically create a new 'Error' object
		///		error["Attempts"] = 1;
		///		error["FirstAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": 1, "FirstAttempt": "...", ... } }
		/// }
		/// else
		/// { // there was already an 'Error' object, record the new attempt
		///		error["Attempts"].Increment();
		///		error["LastAttempt"] = DateTimeOffset.UtcNow;
		///		// root will now have { ..., "Error": { "Attempts": (+1), "FirstAttempt": "...", "LastAttempt": "...", ... } }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(ReadOnlyMemory<char> key, out MutableJsonValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child))
			{
				value = new(this.Context, this, new(key), JsonNull.Missing);
				return false;
			}

			value = new(this.Context, this, new(key), child);
			return true;
		}

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar".AsMemory(), out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the field with the given name, if it is not null or missing</summary>
		/// <param name="key">Name of the field in this object</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this field in the current object.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!foo.TryGetValue&lt;Bar>("bar".AsMemory(), GeneratedConverters.Bar.Default, out var bar))
		/// { // first time
		///		bar = new Bar { /* ... */ }
		///     foo.Set("bar", bar, GeneratedConverters.Bar.Default);
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(ReadOnlyMemory<char> key, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(key, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value of the item at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root["Foos"].TryGetValue(2, out var item))
		/// { // either Foos[] has size less than 3, or Foos[2] is null.
		///		item["Version"] = 0;
		///		item["Created"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to: { "Version": 0, "Created": "..." }
		/// }
		/// else
		/// { // Foos[2] already had a non-null value
		///		item["Version"].Increment();
		///		item["LastModified"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to: { "Version": (+ 1), "Created": "...", "LastModified": "...", ... }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(int index, out MutableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = new(this.Context, this, new(index), JsonNull.Error);
				return false;
			}

			value = new(this.Context, this, new(index), child);
			return true;
		}

		/// <summary>Returns the value of the item at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(int index, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, GeneratedConverters.Foo.Default, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(int index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child, null);
			return true;
		}

		/// <summary>Returns the value of the item at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (!root["Foos"].TryGetValue(2, out var item))
		/// { // either Foos[] has size less than 3, or Foos[2] is null.
		///		item["Version"] = 0;
		///		item["Created"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to: { "Version": 0, "Created": "..." }
		/// }
		/// else
		/// { // Foos[2] already had a non-null value
		///		item["Version"].Increment();
		///		item["LastModified"] = DateTimeOffset.UtcNow;
		///		// Foos[2] will now be equal to: { "Version": (+ 1), "Created": "...", "LastModified": "...", ... }
		/// }
		/// </code></example>
		[Pure]
		public bool TryGetValue(Index index, out MutableJsonValue value)
		{
			if (!this.Json.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = new(this.Context, this, new(index), JsonNull.Error);
				return false;
			}

			value = new(this.Context, this, new(index), child);
			return true;
		}

		/// <summary>Returns the value of the item at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(Index index, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = child.As<TValue>()!;
			return true;
		}

		/// <summary>Returns the value of the item at the given location, if it was non-null and inside the bounds of the array</summary>
		/// <param name="index">Index of the element in this array</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		/// <param name="value">Value that represents this index in the current array, converted into <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true"/> if the element exists and has a non-null value; otherwise, <see langword="false"/>.</returns>
		/// <remarks><para>This can be used to perform a different operation if the value exists or not (initialize a counter or increment its value, throw a specialized exception, ....)</para></remarks>
		/// <example><code>
		/// if (root["Foos"].TryGetValue(1, GeneratedConverters.Foo.Default, out var foo))
		/// {
		///     ProcessFoo(foo);
		/// }
		/// // ...
		/// static void ProcessFoo(Foo instance) { /* ... */ }
		/// </code></example>
		[Pure]
		public bool TryGetValue<TValue>(Index index, IJsonDeserializer<TValue> converter, [MaybeNullWhen(false)] out TValue value)
		{
			var items = this.Json;
			if (!items.TryGetValue(index, out var child) || child.IsNullOrMissing())
			{
				value = default;
				return false;
			}

			value = converter.Unpack(child, null);
			return true;
		}

		#endregion

		#region Get...

		#region Get(string)...

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(string name) => new(this.Context, this, new(name), this.Json.GetValueOrDefault(name));

		/// <summary>Returns the underlying JSON value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(string name) => this.Json.GetValueOrDefault(name);

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(string name) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(name);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(name), null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string name, TValue defaultValue) => this.Json.GetValueOrDefault(name).As<TValue>(defaultValue);

		#endregion

		#region Get(ReadOnlyMemory<char>)...

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(ReadOnlyMemory<char> name)
		{
#if NET9_0_OR_GREATER
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing, out var actualKey);
			return new(this.Context, this, new(actualKey?.AsMemory() ?? name), value);
#else
			var value = this.Json.GetValueOrDefault(name, JsonNull.Missing);
			return new(this.Context, this, new(name), value);
#endif
		}

		/// <summary>Returns the underlying JSON value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(ReadOnlyMemory<char> name) => this.Json.GetValueOrDefault(name);

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(ReadOnlyMemory<char> name) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(name);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(name), null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Returns the value of the field with the specified name</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> name, TValue defaultValue) => this.Json.GetValueOrDefault(name).As<TValue>(defaultValue);

		#endregion

		#region Get(int)...

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(int index) => new(this.Context, this, new(index), this.Json.GetValueOrDefault(index));

		/// <summary>Returns the underlying JSON value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(int index) => this.Json.GetValueOrDefault(index);

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(int index) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(index);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(index), null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue defaultValue) => this.Json.GetValueOrDefault(index).As<TValue>(defaultValue);

		#endregion

		#region Get(Index)...

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(Index index) => new(this.Context, this, new(index), this.Json.GetValueOrDefault(index));

		/// <summary>Returns the underlying JSON value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(Index index) => this.Json.GetValueOrDefault(index);

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		public TValue Get<TValue>(Index index) where TValue : notnull
		{
			var value = this.Json.GetValueOrDefault(index);
			if (value.IsNullOrMissing())
			{
				throw CrystalJson.Errors.Parsing_ChildIsNullOrMissing(this, new(index), null);
			}
			return value.As<TValue>()!;
		}

		/// <summary>Returns the value of the element at the specified location</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue) => this.Json.Get<TValue>(index, defaultValue);

		#endregion

		#region Get(JsonPath)...

		/// <summary>Returns the value of the child at the specified path</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(JsonPath path)
		{
			var current = this;
			foreach (var segment in path)
			{
				current = current.Get(segment);
			}
			return current;
		}

		/// <summary>Returns the underlying JSON value of the child at the specified path</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(JsonPath path) => this.Json.GetPathValueOrDefault(path);

		/// <summary>Returns the value of the child at the specified path</summary>
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

		/// <summary>Returns the value of the child at the specified path</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(JsonPath path, TValue defaultValue) => this.Json.GetPathValueOrDefault(path).As<TValue>(defaultValue);

		#endregion

		#region Get(JsonPathSegment)...

		/// <summary>Returns the value of the child with the given name or index</summary>
		[Pure, MustUseReturnValue]
		public MutableJsonValue Get(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Get(name)
			 : segment.TryGetIndex(out var idx) ? Get(idx)
			 : this;

		/// <summary>Returns the value of the child with the given name or index</summary>
		[Pure, MustUseReturnValue]
		public JsonValue GetValue(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? GetValue(name)
			 : segment.TryGetIndex(out var idx) ? GetValue(idx)
			 : this.Json;

		/// <summary>Returns the value of the child with the given name or index</summary>
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

		/// <summary>Returns the value of the child with the given name or index</summary>
		[Pure, MustUseReturnValue]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(JsonPathSegment segment, TValue defaultValue) => GetValue(segment).As<TValue>(defaultValue);

		#endregion

		#region GetOrCreateObject...

		private static InvalidOperationException ErrorFieldDoesNotMatchExpectedType(JsonPath path, JsonType expectedType, JsonType actualType) => new($"Field '{path}' is of type {actualType} instead of {expectedType} as expected.");

		/// <summary>Returns the object in the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Existing object, or a newly created object if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the field exists, but its value is not an object.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateObject(string name)
		{
			var child = this.Get(name);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonObject.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Object)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(name), JsonType.Object, child.Type);
			}
			return child;
		}

		/// <summary>Returns the object in the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Existing object, or a newly created object if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the field exists, but its value is not an object.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateObject(ReadOnlyMemory<char> name)
		{
			var child = this.Get(name);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonObject.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Object)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(name), JsonType.Object, child.Type);
			}
			return child;
		}

		/// <summary>Returns the object in the item at the given location</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Existing object, or a newly created object if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the item exists, but its value is not an object.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateObject(int index)
		{
			var child = this.Get(index);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonObject.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Object)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(index), JsonType.Object, child.Type);
			}
			return child;
		}

		/// <summary>Returns the object in the item at the given location</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Existing object, or a newly created object if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the item exists, but its value is not an object.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateObject(Index index)
		{
			var child = this.Get(index);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Object)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(index), JsonType.Object, child.Type);
			}
			return child;
		}

		#endregion

		#region GetOrCreateArray...

		/// <summary>Returns the array in the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Existing array, or a newly created array if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the field exists, but its value is not an array.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateArray(string name)
		{
			var child = this.Get(name);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Array)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(name), JsonType.Array, child.Type);
			}
			return child;
		}

		/// <summary>Returns the array in the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <returns>Existing array, or a newly created array if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the field exists, but its value is not an array.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateArray(ReadOnlyMemory<char> name)
		{
			var child = this.Get(name);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Array)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(name), JsonType.Array, child.Type);
			}
			return child;
		}

		/// <summary>Returns the array in the item at the given location</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Existing array, or a newly created array if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the item exists, but its value is not an array.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateArray(int index)
		{
			var child = this.Get(index);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Array)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(index), JsonType.Array, child.Type);
			}
			return child;
		}

		/// <summary>Returns the array in the item at the given location</summary>
		/// <param name="index">Index of the item</param>
		/// <returns>Existing array, or a newly created array if the field was null or missing</returns>
		/// <exception cref="InvalidOperationException">If the item exists, but its value is not an array.</exception>
		[MustUseReturnValue]
		public MutableJsonValue GetOrCreateArray(Index index)
		{
			var child = this.Get(index);
			if (child.IsNullOrMissing())
			{
				child.Set(JsonArray.ReadOnly.Empty);
			}
			else if (child.Type != JsonType.Array)
			{
				throw ErrorFieldDoesNotMatchExpectedType(GetPath(index), JsonType.Array, child.Type);
			}
			return child;
		}

		#endregion

		#endregion

		#region ValueEquals...

		/// <summary>Tests if the current node is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(TValue value, IEqualityComparer<TValue>? comparer = null) => this.Json.ValueEquals(value, comparer);

		/// <summary>Tests if the field with the specified name is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(string name, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(name).ValueEquals(value, comparer);

		/// <summary>Tests if the field with the specified name is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(ReadOnlyMemory<char> name, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(name).ValueEquals(value, comparer);

		/// <summary>Tests if the item at the specified location is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="index">Index of the item</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(int index, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(index).ValueEquals(value, comparer);

		/// <summary>Tests if the item at the specified location is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="index">Index of the item</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(Index index, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(index).ValueEquals(value, comparer);

		/// <summary>Tests if the child with the specified name or index is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="segment">Name or index of the child</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(JsonPathSegment segment, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(segment).ValueEquals(value, comparer);

		/// <summary>Tests if the child at the specified path is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="path">Path to the  child</param>
		/// <param name="value">Expected value</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public bool ValueEquals<TValue>(JsonPath path, TValue value, IEqualityComparer<TValue>? comparer = null) => this.GetValue(path).ValueEquals(value, comparer);

		#endregion

		/// <summary>Applies a patch to the current instance</summary>
		/// <param name="patch">Patch that describes how this node should be modified.</param>
		/// <param name="deepCopy">If <c>false</c> (default) the content of <paramref name="patch"/> are added by reference. If <c>true</c>, a copy is made before being added to the current node.</param>
		public void ApplyPatch(JsonObject patch, bool deepCopy = false)
		{
			Contract.NotNull(patch);

			if (patch.ContainsKey("__patch"))
			{ // we are patching an array
				ApplyPatchToArray(patch, deepCopy);
			}
			else
			{
				ApplyPatchToObject(patch, deepCopy);
			}
		}

		/// <summary>Applies a patch to the current node which is known to be an Object</summary>
		private void ApplyPatchToObject(JsonObject patch, bool deepCopy)
		{
			RealizeObjectIfRequired();

			foreach (var kv in patch)
			{
				switch (kv.Value)
				{
					case JsonObject b:
					{ // merge two objects together
						this[kv.Key].ApplyPatch(b, deepCopy);
						break;
					}
					case JsonNull:
					{ // remove value (or set to null)
						Remove(kv.Key);
						break;
					}
					default:
					{ // overwrite previous value
						Set(kv.Key, deepCopy ? kv.Value.Copy() : kv.Value);
						break;
					}
				}
			}
		}

		/// <summary>Applies a patch to the current node which is known to be an Array</summary>
		private void ApplyPatchToArray(JsonObject patch, bool deepCopy)
		{
			int newSize = patch.Get<int?>("__patch", null) ?? throw new ArgumentException("Object is not a valid patch for an array: required '__patch' field is missing");
			if (newSize == 0)
			{ // clear all items
				Clear();
				return;
			}

			RealizeArrayIfRequired();

			// patch all changed items
			foreach (var kv in patch)
			{
				if (kv.Key == "__patch") continue;
				if (!int.TryParse(kv.Key, out var idx)) throw new ArgumentException($"Object is not a valid patch for an array: unexpected '{kv.Key}' field");
				if (idx >= newSize) throw new ArgumentException($"Object is not a valid patch for an array: key '{kv.Key}' is greater than the final size");

				switch (kv.Value)
				{
					case JsonObject subPatch:
					{
						this[idx].ApplyPatch(subPatch, deepCopy);
						break;
					}
					case (JsonNull):
					{
						RemoveAt(idx);
						break;
					}
					default:
					{
						Set(idx, deepCopy ? kv.Value.Copy() : kv.Value);
						break;
					}
				}
			}
		}

		/// <summary>Returns the list of keys on this object</summary>
		/// <exception cref="InvalidOperationException">If the wrapped JSON value is neither null nor an object</exception>
		public ICollection<string> Keys
		{
			get
			{
				if (this.Json is not JsonObject obj)
				{
					if (this.Json is JsonNull)
					{
						return Array.Empty<string>();
					}
					throw new InvalidOperationException("Cannot iterate keys on a non-array");
				}
				return obj.Keys;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void NotifyParent(MutableJsonValue child)
		{
			this.Parent?.NotifyChildChanged(child, this.Segment);
		}

		private static void InsertInParent(MutableJsonValue parent, in JsonPathSegment segment, MutableJsonValue value)
		{
			Contract.Debug.Assert(parent != null);
			if (segment.TryGetName(out var name))
			{
				parent.Set(name, value.Json);
			}
			else if (segment.TryGetIndex(out var index))
			{
				parent.Set(index, value.Json);
			}
			else
			{
				throw new InvalidOperationException();
			}
		}

		private JsonObject RealizeObjectIfRequired()
		{
			var json = this.Json;
			if (json.IsNullOrMissing())
			{ // the parent should have been an object

				json = this.Context?.NewObject() ?? new JsonObject();
				this.Json = json;

				var res = new MutableJsonValue(this.Context, this.Parent, this.Segment, json);
				if (this.Parent != null)
				{
					InsertInParent(this.Parent, in this.Segment, res);
				}
			}
			if (json is not JsonObject obj)
			{
				throw new InvalidOperationException($"Cannot add a field to a JSON {json.Type}");
			}
			return obj;
		}

		private JsonArray RealizeArrayIfRequired()
		{
			var json = this.Json;
			if (json.IsNullOrMissing())
			{ // the parent should have been an object

				json = this.Context?.NewArray() ?? new JsonArray();
				this.Json = json;

				var res = new MutableJsonValue(this.Context, this.Parent, this.Segment, json);
				if (this.Parent != null)
				{
					InsertInParent(this.Parent, in this.Segment, res);
				}
			}
			if (json is not JsonArray arr)
			{
				throw new InvalidOperationException($"Cannot add a field to a JSON {json.Type}");
			}
			return arr;
		}

		internal void NotifyChildChanged(MutableJsonValue child, JsonPathSegment segment)
		{
			JsonValue newJson;
			if (segment.TryGetName(out var name))
			{
				var prevObj = RealizeObjectIfRequired();
				if (prevObj.IsReadOnly)
				{ // first mutation
					prevObj = JsonObject.Copy(prevObj, deep: false, readOnly: false);
					prevObj[name] = child.Json;
					newJson = prevObj;
				}
				else
				{ // additional mutation
					prevObj[name] = child.Json;
					return;
				}
			}
			else if (segment.TryGetIndex(out var index))
			{
				var prevArr = RealizeArrayIfRequired();
				if (prevArr.IsReadOnly)
				{
					prevArr = JsonArray.Copy(prevArr, deep: false, readOnly: false);
					prevArr[index] = child.Json;
					newJson = prevArr;
				}
				else
				{ // additional mutation
					prevArr[index] = child.Json;
					return;
				}
			}
			else
			{
				throw new NotSupportedException();
			}

			this.Json = newJson;
			this.NotifyParent(this);
		}

		#region InsertOrUpdate...

		internal enum InsertionBehavior : byte
		{
			None,
			OverwriteExisting,
			ThrowOnExisting,
		}

		/// <summary>Changes the value of the current instance in its parent</summary>
		internal bool InsertOrUpdate(JsonValue? value, InsertionBehavior behavior = InsertionBehavior.OverwriteExisting)
		{
			value ??= JsonNull.Missing;

			if (this.Parent == null) throw new InvalidOperationException("Cannot replace the root value");

			var prevJson = this.Json;
			if (!prevJson.IsNullOrMissing())
			{
				switch (behavior)
				{
					case InsertionBehavior.None:
					{
						return false;
					}
					case InsertionBehavior.ThrowOnExisting:
					{
						if (!this.Segment.IsIndex())
						{
							throw new ArgumentException("A field with this same name already exists in this object");
						}
						break;
					}
					default:
					{
						if (prevJson.StrictEquals(value))
						{ // idempotent
							return true;
						}
						break;
					}
				}
			}

			//TODO: if the new value is a JsonObject that is somewhat complex,
			// it could better to compare it to the previous, and only update the actual things that changed ?
			// for ex, sometimes only one (or two) subtrees are changed
			// => HOW DO WE CHEAPLY COMPUTE THE COST OF THE CHECK VS THE REWARDS?

			if (this.Context == null)
			{
				if (prevJson is JsonNull && value is JsonNull)
				{
					return true;
				}
				this.Json = value;
				this.NotifyParent(this);
				return true;
			}

			JsonValue? patch;
			switch (prevJson, value)
			{
				case (JsonObject before, JsonObject after):
				{ // Object patch
					patch = before.ComputePatch(after, readOnly: true);
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = before.ComputePatch(after, readOnly: true);
					break;
				}
				case (JsonNull, JsonNull):
				{
					return true;
				}
				default:
				{ // overwrite
					patch = null;
					break;
				}
			}

			this.Json = value;
			this.NotifyParent(this);

			if (this.Segment.TryGetName(out var name))
			{
				if (patch == null)
				{
					this.Context.RecordUpdate(this.Parent, name, value);
				}
				else
				{
					this.Context.RecordPatch(this.Parent, name, patch);
				}
			}
			else if (this.Segment.TryGetIndex(out var idx))
			{
				// the pattern "[^0]" is used to append a new item to an array
				// -> since the parent now contains the appended item, its Count is already incremented, so we have to replace it with "^1" instead!
				if (idx.Equals(^0))
				{
					idx = ^1;
				}

				var index = idx.GetOffset(this.Parent.Count);
				if (patch == null)
				{
					this.Context.RecordUpdate(this.Parent, index, value);
				}
				else
				{
					this.Context.RecordPatch(this.Parent, index, patch);
				}
			}

			return true;
		}

		internal bool InsertOrUpdate(ReadOnlyMemory<char> name, JsonValue? value, InsertionBehavior behavior = InsertionBehavior.OverwriteExisting)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = RealizeObjectIfRequired();

			var prevJson = selfJson.GetValueOrDefault(name);
			if (!prevJson.IsNullOrMissing())
			{
				switch (behavior)
				{
					case InsertionBehavior.None:
					{
						return false;
					}
					case InsertionBehavior.ThrowOnExisting:
					{
						throw new ArgumentException("A field with the same name already exists in the object.");
					}
					default:
					{
						if (prevJson.StrictEquals(value))
						{ // idempotent
							return true;
						}
						break;
					}
				}
			}
			else
			{
				if (value is JsonNull)
				{ // idempotent
					return true;
				}
			}

			JsonValue? patch;
			switch (prevJson, value)
			{
				case (JsonObject before, JsonObject after):
				{ // Object patch
					patch = this.Context != null ? before.ComputePatch(after, readOnly: true) : null;
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = this.Context != null ? before.ComputePatch(after, readOnly: true) : null;
					break;
				}
				case (_, JsonNull):
				{ // delete
					this.Context?.RecordDelete(this, name);
					return true;
				}
				default:
				{ // overwrite
					patch = null;
					break;
				}
			}

			JsonObject newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the object with a mutable version
				newJson = JsonObject.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can update in-place
				newJson = selfJson;
			}
			newJson[name] = value;

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			if (this.Context != null)
			{
				if (patch == null)
				{
					this.Context.RecordUpdate(this, name, value);
				}
				else
				{
					this.Context.RecordPatch(this, name, patch);
				}
			}
			return true;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JsonValue Convert<T>(T? value) => JsonValue.ReadOnly.FromValue(value);

		[Pure]
		private static JsonValue Convert<T>(T? value, IJsonPacker<T> converter) => value is not null ? converter.Pack(value, CrystalJsonSettings.JsonReadOnly) : JsonNull.Null;

		#endregion

		#region Set(value)

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <remarks>
		/// <para>If the current element is a field of an object, the field will either be added or updated in the parent object.</para>
		/// <para>If the current element is an index in an array, the entry will be set to the new value. If the array was smaller, it will automatically be expanded, and any gaps will be filled with null values.</para>
		/// </remarks>
		public void Set(JsonValue? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonObject? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set(JsonArray? value) => InsertOrUpdate(value);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set(MutableJsonValue? value) => InsertOrUpdate(value?.Json);

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <remarks>This will we recorded as a full use of <paramref name="value"/></remarks>
		public void Set(ObservableJsonValue? value) => InsertOrUpdate(value?.ToJsonValue());

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		public void Set<TValue>(TValue? value) => InsertOrUpdate(Convert<TValue>(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		/// <param name="value">New value for this element</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		public void Set<TValue>(TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(Convert<TValue>(value, converter));

		#region Primitive Types...

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(bool value) => InsertOrUpdate(value ? JsonBoolean.True : JsonBoolean.False);

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(int value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(uint value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(long value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(ulong value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(float value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(double value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(decimal value) => InsertOrUpdate(JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(Half value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(Int128 value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(UInt128 value) => InsertOrUpdate(JsonNumber.Return(value));

#endif

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(string? value) => InsertOrUpdate(JsonString.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateTime value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateTimeOffset value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(NodaTime.Instant value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(DateOnly value) => InsertOrUpdate(JsonDateTime.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(TimeSpan value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(NodaTime.Duration value) => InsertOrUpdate(JsonNumber.Return(value));

		/// <summary>Changes the value of the current instance in its parent</summary>
		public void Set(TimeOnly value) => InsertOrUpdate(JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(string, ...)

		/// <summary>Sets or changes the value of the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">New value for this field</param>
		/// <remarks>
		/// <para>If the current element is null or missing in its parent, it will automatically be created as a new object.</para>
		/// </remarks>
		public void Set(string name, JsonValue? value) => InsertOrUpdate(name.AsMemory(), value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, JsonObject? value) => InsertOrUpdate(name.AsMemory(), value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, JsonArray? value) => InsertOrUpdate(name.AsMemory(), value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, MutableJsonValue? value) => InsertOrUpdate(name.AsMemory(), value?.Json);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, ObservableJsonValue? value) => InsertOrUpdate(name.AsMemory(), value?.ToJsonValue());

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set<TValue>(string name, TValue? value) => InsertOrUpdate(name.AsMemory(), Convert<TValue>(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set<TValue>(string name, TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(name.AsMemory(), Convert<TValue>(value, converter));

		#region Primitive Types...

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, bool value) => InsertOrUpdate(name.AsMemory(), value ? JsonBoolean.True : JsonBoolean.False);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, int value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, uint value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, long value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, ulong value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, float value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, double value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, decimal value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, Half value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, Int128 value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, UInt128 value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

#endif

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, string? value) => InsertOrUpdate(name.AsMemory(), JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, Guid value) => InsertOrUpdate(name.AsMemory(), JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, Uuid128 value) => InsertOrUpdate(name.AsMemory(), JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, DateTime value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, DateTimeOffset value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, NodaTime.Instant value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, DateOnly value) => InsertOrUpdate(name.AsMemory(), JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, TimeSpan value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, NodaTime.Duration value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(string name, TimeOnly value) => InsertOrUpdate(name.AsMemory(), JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(ReadOnlyMemory<char>, ...)

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, JsonValue? value) => InsertOrUpdate(name, value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, JsonObject? value) => InsertOrUpdate(name, value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, JsonArray? value) => InsertOrUpdate(name, value);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, MutableJsonValue? value) => InsertOrUpdate(name, value?.Json);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, ObservableJsonValue? value) => InsertOrUpdate(name, value?.ToJsonValue());

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set<TValue>(ReadOnlyMemory<char> name, TValue? value) => InsertOrUpdate(name, Convert<TValue>(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set<TValue>(ReadOnlyMemory<char> name, TValue? value, IJsonPacker<TValue> converter) => InsertOrUpdate(name, Convert<TValue>(value, converter));

		#region Primitive Types...

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, bool value) => InsertOrUpdate(name, value ? JsonBoolean.True : JsonBoolean.False);

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, int value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, uint value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, long value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, ulong value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, float value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, double value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, decimal value) => InsertOrUpdate(name, JsonNumber.Return(value));

#if NET8_0_OR_GREATER

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, Half value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, Int128 value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, UInt128 value) => InsertOrUpdate(name, JsonNumber.Return(value));

#endif

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, string? value) => InsertOrUpdate(name, JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, Guid value) => InsertOrUpdate(name, JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, Uuid128 value) => InsertOrUpdate(name, JsonString.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, DateTime value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, DateTimeOffset value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, NodaTime.Instant value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, DateOnly value) => InsertOrUpdate(name, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, TimeSpan value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, NodaTime.Duration value) => InsertOrUpdate(name, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(string,JsonValue?)"/>
		public void Set(ReadOnlyMemory<char> name, TimeOnly value) => InsertOrUpdate(name, JsonNumber.Return(value));

		#endregion

		#endregion

		#region Set(path, value)

		/// <summary>Sets or changes the value of the child at the given path</summary>
		/// <param name="path">Path to the child</param>
		/// <param name="value">New value for this child</param>
		/// <remarks>
		/// <para>If any descendant between the current node and the child is either null or missing, it will automatically be created as either a new object or array, as determined by the path.</para>
		/// </remarks>
		public void Set(JsonPath path, JsonValue? value) => Get(path).Set(value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonNull? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonBoolean? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonNumber? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonString? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonDateTime? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonObject? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, JsonArray? value) => Set(path, (JsonValue?) value);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, MutableJsonValue? value) => Set(path, value?.Json);

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(JsonPath path, ObservableJsonValue? value) => Set(path, value?.ToJsonValue());

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(JsonPath path, TValue? value) => Set(path, Convert<TValue>(value));

		/// <inheritdoc cref="Set(JsonPath,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(JsonPath path, TValue? value, IJsonPacker<TValue> converter) => Set(path, Convert<TValue>(value, converter));

		#endregion

		#region Set(index, value)

		internal bool SetOrAdd(Index index, JsonValue? value)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = this.RealizeArrayIfRequired();

			var prevJson = selfJson.GetValueOrDefault(index);
			if (!prevJson.IsNullOrMissing())
			{
				if (prevJson.StrictEquals(value))
				{ // idempotent
					return true;
				}
			}

			JsonValue? patch;
			switch (prevJson, value)
			{
				case (JsonObject before, JsonObject after):
				{ // Object patch
					patch = this.Context != null ? before.ComputePatch(after, readOnly: true) : null;
					break;
				}
				case (JsonArray before, JsonArray after):
				{ // Array patch
					patch = this.Context != null ? before.ComputePatch(after, readOnly: true) : null;
					break;
				}
				default:
				{ // overwrite
					patch = null;
					break;
				}
			}

			var offset = index.GetOffset(selfJson.Count);
			JsonArray newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the array with a mutable version
				newJson = JsonArray.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can modify the array in-place
				newJson = selfJson;
			}
			selfJson[offset] = value;

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			if (this.Context != null)
			{
				if (patch == null)
				{
					this.Context.RecordUpdate(this, offset, value);
				}
				else
				{
					this.Context.RecordPatch(this, offset, patch);
				}
			}

			return true;
		}

		/// <summary>Sets or changes the value of the item at the given location</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="value">New value for this item</param>
		/// <remarks>
		/// <para>If the current element is null or missing in its parent, it will automatically be created as a new array.</para>
		/// </remarks>
		public void Set(int index, JsonValue? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonNull? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, bool value) => SetOrAdd(index, value ? JsonBoolean.True : JsonBoolean.False);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, int value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, long value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, float value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, double value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, string? value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, Guid value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, Uuid128 value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, DateTime value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, DateTimeOffset value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, NodaTime.Instant value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, DateOnly value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, TimeSpan value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, NodaTime.Duration value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, TimeOnly value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonObject? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, JsonArray? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, MutableJsonValue? value) => SetOrAdd(index, value?.Json);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(int index, ObservableJsonValue? value) => SetOrAdd(index, value?.ToJsonValue());

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(int index, TValue? value) => SetOrAdd(index, Convert<TValue>(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(int index, TValue? value, IJsonPacker<TValue> converter) => SetOrAdd(index, Convert<TValue>(value, converter));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonValue? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonNull? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, bool value) => SetOrAdd(index, value ? JsonBoolean.True : JsonBoolean.False);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, int value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, long value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, float value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, double value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, string? value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, Guid value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, Uuid128 value) => SetOrAdd(index, JsonString.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, DateTime value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, DateTimeOffset value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, NodaTime.Instant value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, DateOnly value) => SetOrAdd(index, JsonDateTime.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, TimeSpan value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, NodaTime.Duration value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, TimeOnly value) => SetOrAdd(index, JsonNumber.Return(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonObject? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, JsonArray? value) => SetOrAdd(index, value);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, MutableJsonValue? value) => SetOrAdd(index, value?.Json);

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set(Index index, ObservableJsonValue? value) => SetOrAdd(index, value?.ToJsonValue());

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(Index index, TValue? value) => SetOrAdd(index, Convert<TValue>(value));

		/// <inheritdoc cref="Set(int,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Set<TValue>(Index index, TValue? value, IJsonPacker<TValue> converter) => SetOrAdd(index, Convert<TValue>(value, converter));

		#endregion

		#region Add(...)

		/// <summary>Appends a new value at the end of this array</summary>
		/// <param name="value">Value of the new item</param>
		/// <remarks>
		/// <para>If the current element is null or missing, it will automatically be promoted to an array with <paramref name="value"/> as its single element.</para>
		/// </remarks>
		/// <exception cref="ArgumentException">If the current element has a value that is not an array.</exception>
		public void Add(JsonValue? value) => InsertOrAdd(^0, value);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonNull? value) => Add((JsonValue?) value);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(bool value) => InsertOrAdd(^0, value ? JsonBoolean.True : JsonBoolean.False);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(int value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(long value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(float value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(double value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string? value) => InsertOrAdd(^0, JsonString.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(Guid value) => InsertOrAdd(^0, JsonString.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(Uuid128 value) => InsertOrAdd(^0, JsonString.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(DateTime value) => InsertOrAdd(^0, JsonDateTime.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(DateTimeOffset value) => InsertOrAdd(^0, JsonDateTime.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(NodaTime.Instant value) => InsertOrAdd(^0, JsonDateTime.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(DateOnly value) => InsertOrAdd(^0, JsonDateTime.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TimeSpan value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(NodaTime.Duration value) => InsertOrAdd(^0, JsonNumber.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(TimeOnly value) => InsertOrAdd(^0, JsonString.Return(value));

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonObject? value) => Add((JsonValue?) value);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(JsonArray? value) => Add((JsonValue?) value);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(MutableJsonValue? value) => Add(value?.Json);

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ObservableJsonValue? value) => Add(value?.ToJsonValue());

		/// <inheritdoc cref="Add(JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add<TValue>(TValue value) => Add(Convert<TValue>(value));

		#endregion

		#region Add(name, ...)

		/// <summary>Adds a new field to the object</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value of the field</param>
		/// <remarks>If the current value is null or missing, it will automatically be promoted to an empty object.</remarks>
		/// <exception cref="NotSupportedException">If the current value is not an Object.</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, JsonValue? value) => InsertOrUpdate(name.AsMemory(), value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, JsonNull? value) => InsertOrUpdate(name.AsMemory(), value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, JsonObject? value) => InsertOrUpdate(name.AsMemory(), value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, JsonArray? value) => InsertOrUpdate(name.AsMemory(), value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, MutableJsonValue? value) => InsertOrUpdate(name.AsMemory(), value?.Json, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(string name, ObservableJsonValue? value) => InsertOrUpdate(name.AsMemory(), value?.ToJsonValue(), InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add<TValue>(string name, TValue value) => Add(name, Convert(value));

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, JsonValue? value) => InsertOrUpdate(name, value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, JsonNull? value) => InsertOrUpdate(name, value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, JsonObject? value) => InsertOrUpdate(name, value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, JsonArray? value) => InsertOrUpdate(name, value, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, MutableJsonValue? value) => InsertOrUpdate(name, value?.Json, InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(ReadOnlyMemory<char> name, ObservableJsonValue? value) => InsertOrUpdate(name, value?.ToJsonValue(), InsertionBehavior.ThrowOnExisting);

		/// <inheritdoc cref="Add(string,JsonValue?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add<TValue>(ReadOnlyMemory<char> name, TValue value) => Add(name, Convert(value));

		#endregion

		#region TryAdd...

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string name, JsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name.AsMemory(), value);
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string name, MutableJsonValue? value)
		{
			//REVIEW: should we _copy_ the wrapped value? what if the original is mutated after ?
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name.AsMemory(), value?.Json);
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(string name, ObservableJsonValue? value)
		{
			//REVIEW: should we _copy_ the wrapped value? what if the original is mutated after ?
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name.AsMemory(), value?.ToJsonValue());
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		public bool TryAdd<TValue>(string name, TValue? value)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name.AsMemory(), Convert<TValue>(value));
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <param name="converter">Custom JSON converter</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		public bool TryAdd<TValue>(string name, TValue? value, IJsonPacker<TValue> converter)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name.AsMemory(), Convert<TValue>(value, converter));
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(ReadOnlyMemory<char> name, JsonValue? value)
		{
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name, value);
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(ReadOnlyMemory<char> name, MutableJsonValue? value)
		{
			//REVIEW: should we _copy_ the wrapped value? what if the original is mutated after ?
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name, value?.Json);
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(ReadOnlyMemory<char> name, ObservableJsonValue? value)
		{
			//REVIEW: should we _copy_ the wrapped value? what if the original is mutated after ?
			if (ContainsKey(name))
			{
				return false;
			}
			InsertOrUpdate(name, value?.ToJsonValue());
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		public bool TryAdd<TValue>(ReadOnlyMemory<char> name, TValue? value)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name, Convert<TValue>(value));
			return true;
		}

		/// <summary>Adds a new field to this object, if it does not already exist</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">Value to add.</param>
		/// <param name="converter">Custom JSON converter</param>
		/// <returns><c>true</c> if the field was added, or <c>false</c> if there was already a field with this name (the object will not be modified)</returns>
		public bool TryAdd<TValue>(ReadOnlyMemory<char> name, TValue? value, IJsonPacker<TValue> converter)
		{
			// check before packing
			if (ContainsKey(name))
			{
				return false;
			}

			InsertOrUpdate(name, Convert<TValue>(value, converter));
			return true;
		}

		/// <summary>Adds a new node to this document, if it does not already exist</summary>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the node was added, or <c>false</c> if there was already a node at this location (the document will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, JsonValue? value) => Get(path).InsertOrUpdate(value, InsertionBehavior.None);

		/// <summary>Adds a new node to this document, if it does not already exist</summary>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the node was added, or <c>false</c> if there was already a node at this location (the document will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, MutableJsonValue? value) => Get(path).InsertOrUpdate(value?.Json, InsertionBehavior.None);

		/// <summary>Adds a new node to this document, if it does not already exist</summary>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the node was added, or <c>false</c> if there was already a node at this location (the document will not be modified)</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryAdd(JsonPath path, ObservableJsonValue? value) => Get(path).InsertOrUpdate(value?.ToJsonValue(), InsertionBehavior.None); //BUGBUG: TODO: should not consume the value if the field already exist!

		/// <summary>Adds a new node to this document, if it does not already exist</summary>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Value to add.</param>
		/// <returns><c>true</c> if the node was added, or <c>false</c> if there was already a node at this location (the document will not be modified)</returns>
		public bool TryAdd<TValue>(JsonPath path, TValue? value)
		{
			var child = Get(path);
			// check before packing
			if (child.Exists())
			{
				return false;
			}

			child.Set(value);
			return true;
		}

		/// <summary>Adds a new node to this document, if it does not already exist</summary>
		/// <param name="path">Path to the node</param>
		/// <param name="value">Value to add.</param>
		/// <param name="converter">Custom JSON packer</param>
		/// <returns><c>true</c> if the node was added, or <c>false</c> if there was already a node at this location (the document will not be modified)</returns>
		public bool TryAdd<TValue>(JsonPath path, TValue? value, IJsonPacker<TValue> converter)
		{
			var child = Get(path);
			// check before packing
			if (child.Exists())
			{
				return false;
			}

			child.Set(value, converter);
			return true;
		}

		#endregion

		#region Insert...

		internal bool InsertOrAdd(Index index, JsonValue? value)
		{
			// validate the value
			value ??= JsonNull.Missing;

			var selfJson = this.RealizeArrayIfRequired();

			var offset = index.GetOffset(selfJson.Count);
			JsonArray newJson;
			if (selfJson.IsReadOnly)
			{ // first mutation, replace the array with a mutable version
				newJson = JsonArray.Copy(selfJson, deep: false, readOnly: false);
			}
			else
			{ // additional mutation, we can modify the array in-place
				newJson = selfJson;
			}

			newJson.Insert(offset, value);

			// notify the parent chain if we changed the current json instance
			if (!ReferenceEquals(this.Json, newJson))
			{
				this.Json = newJson;
				this.NotifyParent(this);
			}

			this.Context?.RecordInsertAt(this, offset, value);
			return true;
		}

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonValue value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonNull? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, bool value) => InsertOrAdd(index, value ? JsonBoolean.True : JsonBoolean.False);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, int value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, long value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, float value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, double value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, string? value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, Guid value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, Uuid128 value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, DateTime value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, DateTimeOffset value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, NodaTime.Instant value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, DateOnly value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, TimeSpan value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, NodaTime.Duration value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, TimeOnly value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonObject? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, JsonArray? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, MutableJsonValue? value) => InsertOrAdd(index, value?.Json);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(int index, ObservableJsonValue? value) => InsertOrAdd(index, value?.ToJsonValue());

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(int index, TValue value) => InsertOrAdd(index, Convert<TValue>(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(int index, TValue value, IJsonPacker<TValue> converter) => InsertOrAdd(index, Convert<TValue>(value, converter));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonValue value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonNull? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, bool value) => InsertOrAdd(index, value ? JsonBoolean.True : JsonBoolean.False);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, int value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, long value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, float value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, double value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, string? value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, Guid value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, Uuid128 value) => InsertOrAdd(index, JsonString.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, DateTime value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, DateTimeOffset value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, NodaTime.Instant value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, DateOnly value) => InsertOrAdd(index, JsonDateTime.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, TimeSpan value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, NodaTime.Duration value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, TimeOnly value) => InsertOrAdd(index, JsonNumber.Return(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonObject? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, JsonArray? value) => InsertOrAdd(index, value);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, MutableJsonValue? value) => InsertOrAdd(index, value?.Json);

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert(Index index, ObservableJsonValue? value) => InsertOrAdd(index, value?.ToJsonValue());

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(Index index, TValue value) => InsertOrAdd(index, Convert<TValue>(value));

		/// <summary>Inserts a new value at the specified index of this array</summary>
		/// <param name="index">Index where the value should be inserted</param>
		/// <param name="value">Value to insert</param>
		/// <param name="converter">Converter used to unpack the JSON value into a <typeparamref name="TValue"/> instance</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Insert<TValue>(Index index, TValue value, IJsonPacker<TValue> converter) => InsertOrAdd(index, Convert<TValue>(value, converter));

		#endregion

		#region Remove...

		/// <summary>Removes this node from its parent</summary>
		public bool Remove()
		{
			// simulate the value to be Missing
			this.Json = JsonNull.Missing;
			if (this.Segment.TryGetName(out var name))
			{
				return this.Parent?.Remove(name) ?? false;
			}
			if (this.Segment.TryGetIndex(out var index))
			{
				return this.Parent?.RemoveAt(index) ?? false;
			}

			throw new NotSupportedException();
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(string name)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.ContainsKey(name))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name.AsMemory());
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(ReadOnlyMemory<char> name)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.ContainsKey(name))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name);
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(string name, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.TryGetValue(name, out value))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name.AsMemory());
			return true;
		}

		/// <summary>Removes a field from this node</summary>
		public bool Remove(ReadOnlyMemory<char> name, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonObject prevJson) throw new NotSupportedException();
			if (!prevJson.TryGetValue(name, out value))
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(name);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, name);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(int index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, index);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			if ((uint) index >= prevJson.Count)
			{ // not found
				value = null;
				return false;
			}

			value = prevJson[index];
			var newJson = prevJson.CopyAndRemove(index);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, index);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(Index index)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				return false;
			}

			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, offset);
			return true;
		}

		/// <summary>Removes the element at the specified index from this node</summary>
		public bool RemoveAt(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (this.Json is not JsonArray prevJson) throw new NotSupportedException();
			var offset = index.GetOffset(prevJson.Count);
			if ((uint) offset >= prevJson.Count)
			{ // not found
				value = null;
				return false;
			}

			value = prevJson[offset];
			var newJson = prevJson.CopyAndRemove(offset);
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordDelete(this, offset);
			return true;
		}

		/// <summary>Removes the specified child from this node</summary>
		public bool Remove(JsonPathSegment segment)
			=> segment.TryGetName(out var name) ? Remove(name)
			 : segment.TryGetIndex(out var index) ? RemoveAt(index)
			 : Remove();

		/// <summary>Removes the child at the specified path</summary>
		public bool Remove(JsonPath path)
		{
			var current = this;
			foreach (var token in path.Tokenize())
			{
				if (!current.Exists()) return false;
				if (token.Last)
				{
					return current.Remove(token.Segment);
				}
				current = current.Get(token.Segment);
			}
			return false;
		}

		#endregion

		#region Increment...

		private static MutableJsonValue Increment(MutableJsonValue value)
		{
			switch (value.Json)
			{
				case JsonNumber num:
				{
					value.Set((JsonValue) (num + 1));
					return value;
				}
				case JsonNull:
				{
					value.Set((JsonValue) JsonNumber.One);
					return value;
				}
				default:
				{
					throw new InvalidOperationException($"Cannot increment '{value.GetPath()}' because it is a {value.Json.Type}");
				}
			}
		}

		/// <summary>Increments the value of this node</summary>
		/// <remarks>If the node is null or missing, it will be set to <c>1</c></remarks>
		/// <exception cref="InvalidOperationException">if this node is not a number</exception>
		public void Increment() => Increment(this);

		/// <summary>Increments the value of a field of this object</summary>
		/// <remarks>If the field is null or missing, it will be set to <c>1</c></remarks>
		/// <exception cref="InvalidOperationException">if this field is not a number</exception>
		public MutableJsonValue Increment(string name)
		{
			return Increment(Get(name));
		}

		/// <summary>Increments the value of an item of this object</summary>
		/// <remarks>If the item is null or missing, it will be set to <c>1</c></remarks>
		/// <exception cref="InvalidOperationException">if this item is not a number</exception>
		public MutableJsonValue Increment(int index)
		{
			return Increment(Get(index));
		}

		/// <summary>Increments the value of an item of this object</summary>
		/// <remarks>If the item is null or missing, it will be set to <c>1</c></remarks>
		/// <exception cref="InvalidOperationException">if this item is not a number</exception>
		public MutableJsonValue Increment(Index index)
		{
			return Increment(Get(index));
		}

		#endregion

		#region Exchange...

		/// <summary>Replaces the value of this node, it is equal to an expected value</summary>
		/// <param name="value">New value for this node if the comparison succeeds</param>
		/// <param name="comparand">Expected current value of this node</param>
		/// <returns>Previous value of this node</returns>
		/// <remarks>
		/// <para>If the previous value of this node is equal to <paramref name="comparand"/>, then it will be replaced with <paramref name="value"/>; otherwise, it will not be modified.</para>
		/// <para>To test if the object was changed, test if the returned value is equal to <paramref name="comparand"/> (no change) or not (changed)</para>
		/// </remarks>
		public bool CompareExchange<TValue>(TValue? value, TValue? comparand)
		{
			if (!this.Json.ValueEquals(comparand))
			{
				return false;
			}
			Set(Convert<TValue>(value));
			return true;
		}

		/// <summary>Replaces the value of this node</summary>
		/// <param name="value">New value for this node</param>
		/// <returns>Previous value of this node</returns>
		/// <remarks>If the object is already equal to <paramref name="value"/>, it will not be changed.</remarks>
		public JsonValue Exchange(JsonValue? value)
		{
			var prev = this.Json;
			Set(value);
			return prev;
		}

		/// <summary>Replaces the value of this node</summary>
		/// <param name="value">New value for this node</param>
		/// <returns>Previous value of this node</returns>
		/// <remarks>If the object is already equal to <paramref name="value"/>, it will not be changed.</remarks>
		public TValue? Exchange<TValue>(TValue? value)
		{
			var prev = this.As<TValue>();
			Set(Convert<TValue>(value));
			return prev;
		}

		/// <summary>Replaces the value of field of this object</summary>
		/// <param name="name">Name of the field to update</param>
		/// <param name="value">New value for this node</param>
		/// <returns>Previous value of this field</returns>
		/// <remarks>If the field is already equal to <paramref name="value"/>, the object will not be changed.</remarks>
		public JsonValue Exchange(string name, JsonValue? value)
		{
			var prev = this.Json[name];
			Set(name, value);
			return prev;
		}

		/// <summary>Replaces the value of an item of this array</summary>
		/// <param name="index">Index of the item to update</param>
		/// <param name="value">New value for this item</param>
		/// <returns>Previous value of this item</returns>
		/// <remarks>If the item is already equal to <paramref name="value"/>, the array will not be changed.</remarks>
		public JsonValue Exchange(int index, JsonValue? value)
		{
			var prev = this.Json[index];
			Set(index, value);
			return prev;
		}

		/// <summary>Replaces the value of an item of this array</summary>
		/// <param name="index">Index of the item to update</param>
		/// <param name="value">New value for this item</param>
		/// <returns>Previous value of this item</returns>
		/// <remarks>If the item is already equal to <paramref name="value"/>, the array will not be changed.</remarks>
		public JsonValue Exchange(Index index, JsonValue? value)
		{
			var prev = this.Json[index];
			Set(index, value);
			return prev;
		}

		/// <summary>Replaces the value of field of this object</summary>
		/// <param name="name">Name of the field to update</param>
		/// <param name="value">New value for this node</param>
		/// <returns>Previous value of this field</returns>
		/// <remarks>If the field is already equal to <paramref name="value"/>, the object will not be changed.</remarks>
		public TValue? Exchange<TValue>(string name, TValue? value)
		{
			var prev = Get<TValue?>(name, default);
			Set(name, value);
			return prev;
		}

		/// <summary>Replaces the value of an item of this array</summary>
		/// <param name="index">Index of the item to update</param>
		/// <param name="value">New value for this item</param>
		/// <returns>Previous value of this item</returns>
		/// <remarks>If the item is already equal to <paramref name="value"/>, the array will not be changed.</remarks>
		public TValue? Exchange<TValue>(int index, TValue? value)
		{
			var prev = Get<TValue?>(index, default);
			Set(index, value);
			return prev;
		}

		/// <summary>Replaces the value of an item of this array</summary>
		/// <param name="index">Index of the item to update</param>
		/// <param name="value">New value for this item</param>
		/// <returns>Previous value of this item</returns>
		/// <remarks>If the item is already equal to <paramref name="value"/>, the array will not be changed.</remarks>
		public TValue? Exchange<TValue>(Index index, TValue? value)
		{
			var prev = Get<TValue?>(index, default);
			Set(index, value);
			return prev;
		}

		#endregion

		#region Clear...

		/// <summary>Clears the contents of this object or array</summary>
		/// <exception cref="NotSupportedException">If the current node is not an object or an array or null</exception>
		public void Clear()
		{
			JsonValue newJson;
			switch (this.Json)
			{
				case JsonObject obj:
				{
					if (obj.Count == 0) return; // already empty!
					newJson = JsonObject.ReadOnly.Create(obj.Comparer);
					break;
				}
				case JsonArray arr:
				{
					if (arr.Count == 0) return; // already empty!
					newJson = JsonArray.ReadOnly.Empty;
					break;
				}
				case JsonNull:
				{
					// already "empty"
					return;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
			this.Json = newJson;
			this.NotifyParent(this);
			this.Context?.RecordUpdate(this, JsonPathSegment.Empty, newJson);
		}

		#endregion

		#endregion

		#region IEnumerable<...>

		/// <inheritdoc />
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

		/// <inheritdoc />
		public IEnumerator<MutableJsonValue> GetEnumerator()
		{
			// we cannot know how the result will be used, so mark this a full "Value" read.

			if (this.Json is not JsonArray array)
			{
				if (this.Json is JsonNull)
				{
					yield break;
				}

				throw new InvalidOperationException("Cannot iterate a non-array");
			}

			// but we don't know how the items will be consumed
			for(int i = 0; i < array.Count; i++)
			{
				yield return new(this.Context, this, new(i), array[i]);
			}
		}

		#endregion

		#region JSON Serialization...

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => this.Json.JsonSerialize(writer);

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => this.Json;

		#endregion

	}

}
