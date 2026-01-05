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
	using System.ComponentModel;
	using System.Globalization;
	using SnowBank.Buffers;
	using SnowBank.Text;

	/// <summary>Represents a value in a JSON Document</summary>
	/// <remarks>The value can be null or missing (<see cref="JsonNull"/>), a literal (<see cref="JsonString"/>, <see cref="JsonBoolean"/>, <see cref="JsonNumber"/>), an object (<see cref="JsonObject"/>) or an array (<see cref="JsonArray"/>)</remarks>
	[Serializable]
	[PublicAPI]
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
	[JetBrains.Annotations.CannotApplyEqualityOperator]
	[DebuggerNonUserCode]
	public abstract partial class JsonValue : IEquatable<JsonValue>, IComparable<JsonValue>, IJsonSerializable, IFormattable, ISliceSerializable, IConvertible
#if NET8_0_OR_GREATER
		, IParsable<JsonValue>
		, ISpanParsable<JsonValue>
		, ISpanFormattable
		, IUtf8SpanFormattable
#endif
	{
		/// <summary>Type of JSON value (<see cref="JsonType.Boolean">Boolean</see>, <see cref="JsonType.String">String</see>, <see cref="JsonType.Number">Number</see>, <see cref="JsonType.Object">Object</see>, <see cref="JsonType.Array">Array</see>, ...)</summary>
		public abstract JsonType Type { [Pure] get; }

		/// <summary>Converts this into a CLR object (with a type that matches the value)</summary>
		/// <remarks>Prefer casting to a specific type, using <see cref="JsonValueExtensions.As{TValue}">As&lt;TValue&gt;()</see> or any equivalent method.</remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public abstract object? ToObject();

		/// <summary>Bind this value into an instance of the specified <paramref name="type"/></summary>
		/// <param name="type">Target managed type</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>An instance of the target type that is equivalent to the original JSON value, if there exists a valid conversion path or convention. Otherwise, an exception will be thrown.</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound into an instance of the target <paramref name="type"/>.</exception>
		/// <example><c>JsonNumber.Return(123).Bind(typeof(long))</c> will return a boxed Int64 with value <c>123</c>.</example>
		/// <remarks>If the target type is a Value Type, the instance will be boxed, which may cause extra memory allocations. Consider calling <see cref="Bind{TValue}"/> instance, or use any of the convenience methods like <see cref="JsonValueExtensions.Required{TValue}"/>, <see cref="JsonValueExtensions.As{TValue}"/>, ...</remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public abstract object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Bind this value into an instance of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Target managed type</typeparam>
		/// <param name="defaultValue">Default value to return if the current instance is null or missing</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>An instance of the type <typeparamref name="TValue"/> that is equivalent to the original JSON value, if there exists a valid conversion path. Otherwise, an exception will be thrown.</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound into an instance of the target type <typeparamref name="TValue"/>.</exception>
		/// <example><c>JsonNumber.Return(123).Bind&lt;long>()</c> will return the value <c>123</c>.</example>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			var res = Bind(typeof(TValue), resolver);
			return res is not null ? (TValue?) res : defaultValue;
		}

		/// <summary>Tests if this value is null or missing</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual bool IsNull { [Pure] get => false; }

		/// <summary>Tests if this value corresponds to the logical default for this type (0, false, null or missing, ...)</summary>
		/// <returns><see langword="true"/> for values like 0, false, or null; or <see langword="false"/> for non-zero integers, true, strings, arrays and objects</returns>
		/// <remarks>The empty string, array and object are NOT considered to be the default value of their type!</remarks>
		/// <example>
		/// <c>JsonNumber.Zero.IsDefault == true</c>,
		/// <c>JsonNumber.Return(123).IsDefault == false</c>,
		/// <c>JsonString.Return("").IsDefault == false</c>,
		/// <c>new JsonArray().IsDefault == false</c>,
		/// <c>new JsonObject().IsDefault == false</c>
		/// </example>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsDefault { [Pure] get; }

		/// <summary>Returns <see langword="true"/> if this value is read-only, and cannot be modified, or <see langword="false"/> if it allows mutations.</summary>
		/// <remarks>
		/// <para>Only JSON Objects and Arrays can return <see langword="false"/>. All other "value types" (string, boolean, numbers, ...) are always immutable, and will always be read-only.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy()"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract bool IsReadOnly { [Pure] get; }

		/// <summary>Prevents any future mutations of this JSON value (of type Object or Array), by recursively freezing it and all of its children.</summary>
		/// <returns>The same instance, which is now converted to a read-only instance.</returns>
		/// <remarks>
		/// <para>Any future attempt to modify this object, or any of its children that was previously mutable, will fail.</para>
		/// <para>If this instance is already read-only, this will be a no-op.</para>
		/// <para>This should only be used with care, and only on objects that are entirely owned by the called, or that have not been published yet. Freezing a shared mutable object may cause issues for other threads that still were expecting a mutable instance.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy()"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual JsonValue Freeze() => this;

		/// <summary>Returns a read-only copy of this value, unless it is already read-only.</summary>
		/// <returns>The same instance if it is already read-only, or a new read-only deep copy, if it was mutable.</returns>
		/// <remarks>
		/// <para>The value returned is guaranteed to be immutable, and is safe to cache, share, or use as a singleton.</para>
		/// <para>Only JSON Objects and Arrays are impacted. All other "value types" (string, boolean, number, ...) are always immutable, and will not be copied.</para>
		/// <para>If you need to modify a JSON Object or Array that is read-only, you should first create a copy, by calling either <see cref="Copy()"/> or <see cref="ToMutable"/>, perform any changes required, and then either <see cref="Freeze"/> the copy, or call <see cref="ToReadOnly"/> again.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToReadOnly() => this;

		/// <summary>Convert this JSON value so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (string, boolean, number, ...) that are always immutable.</para>
		/// </remarks>
		[Pure]
		public virtual JsonValue ToMutable() => this;

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" and can be safely written to a debug log (without flooding).</summary>
		/// <remarks>
		/// <para>For example, any JSON Object or Array must have 5 children or fewer, all small values, to be considered "small". Likewise, a JSON String literal must have a length or 36 characters or fewer, to be considered "small".</para>
		/// <para>When generating a compact representation of a JSON value, for troubleshooting purpose, an Object or Array that is not "small", may be written as <c>[ 1, 2, 3, 4, 5, ...]</c> with the rest of the value omitted.</para>
		/// </remarks>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsSmallValue();

		/// <summary>Returns <see langword="true"/> if this value is considered as "small" enough to be inlined with its parent when generating compact or one-line JSON, like a small array or JSON object.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		internal abstract bool IsInlinable();

		/// <summary>Serializes this JSON value into a JSON string literal</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use ToJsonText() instead!")] //TODO: phase out this overload ASAP!
		public string ToJson(CrystalJsonSettings? settings = null) => ToJsonText(settings);

		/// <summary>Serializes this JSON value into a JSON string literal</summary>
		/// <param name="settings">Settings used to change the serialized JSON output (optional)</param>
		/// <param name="resolver">Optional custom resolver.</param>
		/// <returns>JSON text literal that can be written to disk, returned as the body of an HTTP request, or </returns>
		/// <example><c>var jsonText = new JsonObject() { ["hello"] = "world" }.ToJsonText(); // == "{ \"hello\": \"world\" }"</c></example>
		[Pure]
		public abstract string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Returns a "compact" string representation of this value, that can fit into a troubleshooting log.</summary>
		/// <remarks>
		/// <para>If the value is too large, parts of it will be shortened by adding "<c>, ...</c>" tokens, so that it is still readable by a human.</para>
		/// <para>Note: the string that is return is not valid JSON and may not be parseable! This is only intended for quick introspection of a JSON document, by a human, using a log file or console output.</para>
		/// </remarks>
		[Pure]
		internal virtual string GetCompactRepresentation(int depth) => ToJsonText();

		/// <summary>Converts this JSON value into a printable string</summary>
		/// <remarks>
		/// <para>Please note that, due to convention in .NET, this will return the empty string for null values, since <see cref="object.ToString"/> must not return a null reference! Please call <see cref="ToStringOrDefault"/> if you need null references for null or missing JSON values.</para>
		/// <para>See <see cref="ToString(string,IFormatProvider)"/> if you need to specify a different format than the default</para></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		/// <summary>Converts this JSON value into a printable string, using the specified format and provider</summary>
		/// <param name="format">Desired format, or "D" (default) if omitted</param>
		/// <param name="provider">This parameter is ignored. JSON values are always formatted using <see cref="CultureInfo.InvariantCulture"/>.</param>
		/// <remarks>Supported values for <paramref name="format"/> are:
		/// <list type="table">
		///   <listheader><term>format</term><description>foo</description></listheader>
		///   <item><term>D</term><description>Default, returns the value in the default format (no quotes for strings, ...)</description></item>
		///   <item><term>N</term><description>Normal, equivalent to calling <see cref="ToJsonText"/> with <see cref="CrystalJsonSettings.Json"/></description></item>
		///   <item><term>C</term><description>Compact, equivalent to calling <see cref="ToJsonText"/> with <see cref="CrystalJsonSettings.JsonCompact"/></description></item>
		///   <item><term>P</term><description>Pretty, equivalent to calling <see cref="ToJsonText"/> with <see cref="CrystalJsonSettings.JsonIndented"/></description></item>
		///   <item><term>Q</term><description>Quick, equivalent to calling <see cref="GetCompactRepresentation"/>, that will return a simplified/partial version, suitable for logs/traces.</description></item>
		///   <item><term>J</term><description>JavaScript, equivalent to calling <see cref="ToJsonText"/> with <see cref="CrystalJsonSettings.JavaScript"/>.</description></item>
		///   <item><term>B</term><description>returns a doubly-encoded JSON version of this value, that can be passed through inside another JSON payload, or copy-pasted as a valid C# string literal.</description></item>
		/// </list>
		/// </remarks>
		[Pure]
		public virtual string ToString(string? format, IFormatProvider? provider = null)
		{
			return (format ?? "") switch
			{
				"" or "D" or "d" // "D" is for Default!
					// this is overriden for strings, numbers, boolean
					=> this.ToJsonText(),
				"C" or "c" // "C" is for Compact!
					=> this.ToJsonText(CrystalJsonSettings.JsonCompact),
				"P" or "p" // "P" is for Pretty!
					=> this.ToJsonText(CrystalJsonSettings.JsonIndented),
				"N" or "n" // "N" is for Normal!
					=> this.ToJsonText(CrystalJsonSettings.Json),
				"Q" or "q" // "Q" is for Quick!
					=> this.GetCompactRepresentation(0),
				"J" or "j" // "J" is for Javascript!
					=> this.ToJsonText(CrystalJsonSettings.JavaScript),
				"B" or "b" // "B" is for Build!
					=> JsonEncoding.Encode(this.ToJsonText(CrystalJsonSettings.JsonCompact)),
				_ => throw ErrorNotSupportedFormatLiteral(format)
			};
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static NotSupportedException ErrorNotSupportedFormatLiteral(ReadOnlySpan<char> format)
		{
#if DEBUG
			if (format!.EndsWith("}}", StringComparison.Ordinal))
			{
				// ATTENTION! you have a typo in a string interpolation format!
				// If you have written something like "... {{{obj:P}}}", it will parse "P}}" as the string format, which is incorrect.
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
			}
#endif
			throw new NotSupportedException($"Invalid JSON format '{format}' specification");
		}

		/// <summary>Creates a deep copy of this value (and all of its children)</summary>
		/// <returns>A new instance, isolated from the original.</returns>
		/// <remarks>
		/// <para>Any changes to this copy will not have any effect of the original, and vice versa</para>
		/// <para>If the value is an immutable type (like <see cref="JsonString"/>, <see cref="JsonNumber"/>, <see cref="JsonBoolean"/>, ...) then the same instance will be returned.</para>
		/// <para>If the value is an array then a new array containing a </para>
		/// </remarks>
		public virtual JsonValue Copy() => Copy(deep: true, readOnly: false);

		/// <summary>Creates a copy of this object</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		/// <remarks>Immutable JSON values (like strings, numbers, ...) will return themselves without any change.</remarks>
		[Pure]
		protected internal virtual JsonValue Copy(bool deep, bool readOnly) => this;

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is JsonValue value && Equals(value);

		/// <summary>Tests if these two JSON values are considered equal, using the relaxed JSON comparison semantics</summary>
		/// <remarks>
		/// <para>Two values are considered "equal" if they would produce the same result when serialized using a canonical representation.</para>
		/// <para>Numbers are equal if they represent the "same" number (ie: <c>1</c> and <c>1.0</c> are equal)</para>
		/// <para>Numbers and strings can be equal, if the later can be parsed into a number equal to the former (ie: <c>123</c> and <c>"123"</c> are equal)</para>
		/// <para>Arrays are equal if they have the same length and all their elements are equals pairwise</para>
		/// <para>Objects are equal if they have the same key/value pairs, irrespective of their order.</para>
		/// <para>A <see cref="JsonNull.Missing"/> field is not considered equal to an explicit <see cref="JsonNull.Null"/> field, and vice versa</para>
		/// <para>If you require strict equality (ie: <c>"123"</c> and <c>123</c> are different), consider calling <see cref="StrictEquals"/> instead.</para>
		/// </remarks>
		public abstract bool Equals(JsonValue? other);

		/// <summary>Tests if the current instance is equal to the specified value, using the strict JSON comparison semantics</summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="value">Value to test with the current instance</param>
		/// <param name="comparer">Custom equality comparer if specified; otherwise, uses the default comparer for this type</param>
		/// <returns><see langword="true"/> if both arguments are considered equal; otherwise, <see langword="false"/></returns>
		/// <remarks>This method tries to perform an optimized comparison, and should perform less memory allocations than calling </remarks>
		public virtual bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null)
			=> (comparer ?? EqualityComparer<TValue>.Default).Equals(Bind<TValue>(), value);

		/// <summary>Tests if these two JSON values are considered equal, using the strict JSON comparison semantics</summary>
		/// <remarks>
		/// <para>For two values to be considered "equal", they must be of the same type (string, number, array, ...) AND have the same result when serialized using a canonical representation.</para>
		/// <para>Arrays are equals if they have the same length and all their elements are also strictly equal.</para>
		/// <para>Objects are equal if they have the same keys, irrespective of their order, and each value is strictly equal.</para>
		/// <para>A <see cref="JsonNull.Missing"/> field is not considered equal to an explicit <see cref="JsonNull.Null"/> field, and vice versa</para>
		/// </remarks>
		public abstract bool StrictEquals(JsonValue? other);

		/// <summary>Tests if two JSON values are equivalent</summary>
		public static bool Equals(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).Equals(right ?? JsonNull.Missing);

		/// <summary>Compares two JSON values, and returns an integer that indicates whether the first value precedes, follows, or occurs in the same position in the sort order as the second value.</summary>
		public static int Compare(JsonValue? left, JsonValue? right) => (left ?? JsonNull.Missing).CompareTo(right ?? JsonNull.Missing); 

		/// <summary>Returns a hash code that can be used to quickly identify a JSON value.</summary>
		/// <remarks>
		/// <para>The hash code is guaranteed to remain unchanged during the lifetime of the object.</para>
		/// <para>
		/// <b>Caution:</b> there is *NO* guarantee that two equivalent Objects or Arrays will have the same hash code! 
		/// This means that it is *NOT* safe to use a JSON object or array has the key of a Dictionary or other collection that uses hash codes to quickly compare two instances.
		/// </para>
		/// </remarks>
		public abstract override int GetHashCode();

		/// <summary>Compares two JSON values, and returns an integer that indicates whether the first value precedes, follows, or occurs in the same position in the sort order as the second value.</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public virtual int CompareTo(JsonValue? other)
		{
			if (other is null) return this.IsNull ? 0 : +1;
			if (ReferenceEquals(this, other)) return 0;

			// cannot compare a value type directly with an object or array
			if (other is JsonObject or JsonArray)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot compare a JSON value with another value of type {other.Type}");
			}

			// no real silver bullet here, we will simply compare the hash codes (which _MUST_ be constant, so at least the order will be deterministic)
			int c = ((int) this.Type).CompareTo((int) other.Type);
			if (c == 0)
			{
				c = this.GetHashCode().CompareTo(other.GetHashCode());
			}

			return c;
		}

		/// <summary>Tests if the current instance is an array that contains the specified value</summary>
		/// <exception cref="InvalidOperationException"> when <paramref name="value"/> is not a <see cref="JsonArray"/></exception>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool Contains(JsonValue? value) => throw FailDoesNotSupportContains(this);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportContains(JsonValue value) => new($"Cannot index into a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, string key) => new($"Cannot read property '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, int index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingRead(JsonValue value, Index index) => new($"Cannot read value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, string key) => new($"Cannot set property '{key}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, ReadOnlySpan<char> key) => new($"Cannot set property '{key.ToString()}' on a JSON {value.Type}, because it is not a JSON Object");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, int index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static InvalidOperationException FailDoesNotSupportIndexingWrite(JsonValue value, Index index) => new($"Cannot set value at index '{index}' on a JSON {value.Type}, because it is not a JSON Array");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateReadOnlyValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is marked as read-only.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		protected static InvalidOperationException FailCannotMutateImmutableValue(JsonValue value) => new($"Cannot mutate JSON {value.Type} because it is immutable.");

		/// <summary>Returns the value of field with the specified name, if the current is an object and the field was found.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="value">Value of the field, if it was found</param>
		/// <returns><see langword="true"/> if the field was found, or <see langword="false"/> if the field was not found, or the current value is not an object</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value of field with the specified name, if the current is an object and the field was found.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="value">Value of the field, if it was found</param>
		/// <returns><see langword="true"/> if the field was found, or <see langword="false"/> if the field was not found, or the current value is not an object</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value of field with the specified name, if the current is an object and the field was found.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="value">Value of the field, if it was found</param>
		/// <returns><see langword="true"/> if the field was found, or <see langword="false"/> if the field was not found, or the current value is not an object</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

#if NET9_0_OR_GREATER

		/// <summary>Returns the value of field with the specified name, if the current is an object and the field was found.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <param name="value">Value of the field, if it was found</param>
		/// <returns><see langword="true"/> if the field was found, or <see langword="false"/> if the field was not found, or the current value is not an object</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			actualKey = null;
			return false;
		}

		/// <summary>Returns the value of field with the specified name, if the current is an object and the field was found.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <param name="value">Value of the field, if it was found</param>
		/// <returns><see langword="true"/> if the field was found, or <see langword="false"/> if the field was not found, or the current value is not an object</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			actualKey = null;
			return false;
		}

#endif

		/// <summary>Returns the value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="value">When this method returns, the value located at the specified index, if the index is inside the bounds of the array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if <paramref name="index"/> is inside the bounds of the array; otherwise, <see langword="false"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		/// <summary>Returns the value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="value">When this method returns, the value located at the specified index, if the index is inside the bounds of the array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if <paramref name="index"/> is inside the bounds of the array; otherwise, <see langword="false"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual bool TryGetValue(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			//TODO: REVIEW: should we return false, or fail if not supported? (note: this[xxx] throws on values that do not support indexing)
			value = null;
			return false;
		}

		#region GetValue...

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(string key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key);

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue GetValue(ReadOnlySpan<char> key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key);

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue GetValue(ReadOnlyMemory<char> key) => GetValueOrDefault(key, JsonNull.Missing).RequiredField(key);

#if NET9_0_OR_GREATER

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValue(ReadOnlySpan<char> key, out string? actualKey) => GetValueOrDefault(key, JsonNull.Missing, out actualKey).RequiredField(key);

		/// <summary>Returns the value of the <b>required</b> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <returns>The value of the specified field, or an exception if it is null or missing.</returns>
		/// <exception cref="InvalidOperationException">If the field is null or missing</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValue(ReadOnlyMemory<char> key, out string? actualKey) => GetValueOrDefault(key, JsonNull.Missing, out actualKey).RequiredField(key);

#endif

		/// <summary>Returns the value at the <b>required</b> item at the specified index.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <returns>The value located at the specified index, or an exception if the index is outside the bounds of the array, or if the item is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(int index) => GetValueOrDefault(index, JsonNull.Missing).RequiredIndex(index);

		/// <summary>Returns the value at the <b>required</b> item at the specified index.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <returns>The value located at the specified index, or an exception if the index is outside the bounds of the array, or if the item is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public virtual JsonValue GetValue(Index index) => GetValueOrDefault(index, JsonNull.Missing).RequiredIndex(index);

		#endregion

		#region GetValueOrDefault...

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValueOrDefault(string key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key);

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key.ToString());

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, key.GetStringOrCopy());

#if NET9_0_OR_GREATER

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? defaultValue, out string? actualKey) => throw FailDoesNotSupportIndexingRead(this, key.ToString());

		/// <summary>Returns the value of the <i>optional</i> field with the specified name.</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <param name="defaultValue">The value that is returned if field was null or missing.</param>
		/// <param name="actualKey">If the field is present, receives the previously allocated key; otherwise, <c>null</c>.</param>
		/// <returns>The value of the specified field, or <paramref name="defaultValue"/> if it is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonObject"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? defaultValue, out string? actualKey) => throw FailDoesNotSupportIndexingRead(this, key.GetStringOrCopy());

#endif

		/// <summary>Returns the value at the <i>optional</i> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array, or if the item at this location is null or missing.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, of the item is null or missing.</returns>
		/// <remarks>If the index is outside the bounds, and <paramref name="defaultValue"/> is not specified, then <see cref="JsonNull.Error"/> is returned.</remarks>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonArray"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		/// <summary>Returns the value at the <i>optional</i> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the item to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array, or if the item at this location is null or missing.</param>
		/// <returns>The value located at the specified index, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, of the item is null or missing.</returns>
		/// <remarks>
		/// <para>If the value is not a <see cref="JsonArray"/> or <see cref="JsonNull">null or missing</see>, an exception will be thrown.</para>
		/// </remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public virtual JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null) => throw FailDoesNotSupportIndexingRead(this, index);

		#endregion

		/// <summary>Returns the value of the <i>optional</i> field with the specified name, if the current value is a JSON Object</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or <see cref="JsonNull.Missing"/> if it was not found.</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is <see langword="null"/>.</exception>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing, or the object is read-only</exception>
		/// <remarks>If the current value is null or missing, returns <see cref="JsonNull.Missing"/> in order to allow for null-propagation.</remarks>
		public virtual JsonValue this[string key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(key);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, key));
		}

		/// <summary>Returns the value of the <i>optional</i> field with the specified name, if the current value is a JSON Object</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or <see cref="JsonNull.Missing"/> if it was not found.</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is <see langword="null"/>.</exception>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing, or the object is read-only</exception>
		/// <remarks>If the current value is null or missing, returns <see cref="JsonNull.Missing"/> in order to allow for null-propagation.</remarks>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue this[ReadOnlySpan<char> key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(key);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, key));
		}

		/// <summary>Returns the value of the <i>optional</i> field with the specified name, if the current value is a JSON Object</summary>
		/// <param name="key">Name of the field to retrieve</param>
		/// <returns>The value of the specified field, or <see cref="JsonNull.Missing"/> if it was not found.</returns>
		/// <exception cref="System.ArgumentNullException">If <paramref name="key"/> is <see langword="null"/>.</exception>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing, or the object is read-only</exception>
		/// <remarks>If the current value is null or missing, returns <see cref="JsonNull.Missing"/> in order to allow for null-propagation.</remarks>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public virtual JsonValue this[ReadOnlyMemory<char> key]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(key);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, key.Span));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/> or <see cref="JsonNull.Error"/> if the index is outside the bounds of the array. An exception is thrown if the current value is not a JSON Array.</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing, or the array is read-only</exception>
		/// <remarks>If the current value is null or missing, returns <see cref="JsonNull.Missing"/> in order to allow for null-propagation.</remarks>
		[AllowNull]
		public virtual JsonValue this[int index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : FailDoesNotSupportIndexingWrite(this, index));
		}

		/// <summary>Returns the element at the specified index, if the current value is a JSON Array</summary>
		/// <param name="index">Index of the element to return</param>
		/// <returns>Value of the element at the specified <paramref name="index"/> or <see cref="JsonNull.Error"/> if the index is outside the bounds of the array. An exception is thrown if the current value is not a JSON Array.</returns>
		/// <exception cref="System.InvalidOperationException">The current JSON value does not support indexing, or the array is read-only</exception>
		/// <remarks>If the current value is null or missing, returns <see cref="JsonNull.Missing"/> in order to allow for null-propagation.</remarks>
		[AllowNull]
		public virtual JsonValue this[Index index]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetValueOrDefault(index);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : ThrowHelper.InvalidOperationException($"Cannot set value at index {index} on a JSON {this.Type}"));
		}

		[AllowNull]
		public virtual JsonValue this[JsonPath path]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => GetPathValueOrDefault(path);
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : ThrowHelper.InvalidOperationException($"Cannot set value of a child on a JSON {this.Type}"));
		}

		[AllowNull]
		public virtual JsonValue this[JsonPathSegment segment]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => segment.TryGetName(out var name) ? GetValueOrDefault(name) : segment.TryGetIndex(out var index) ? GetValueOrDefault(index) : this;
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set => throw (this.IsReadOnly ? FailCannotMutateReadOnlyValue(this) : ThrowHelper.InvalidOperationException($"Cannot set value of a child on a JSON {this.Type}"));
		}

		#region NEW API

		#region Object...

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(string key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#else
		[EditorBrowsable(EditorBrowsableState.Always)]
#endif
		public bool TryGetObject(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="obj">When this method returns, contains the value of the field if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#else
		[EditorBrowsable(EditorBrowsableState.Always)]
#endif
		public bool TryGetObject(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(key, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the item at the specified location, if it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="obj">When this method returns, contains the value at this location if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(int index, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(index, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the JSON Object that corresponds to the item at the specified location, if it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="obj">When this method returns, contains the value at this location if it exists, and is a valid JSON Object; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an object; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetObject(Index index, [MaybeNullWhen(false)] out JsonObject obj)
		{
			if (TryGetValue(index, out var child) && child is JsonObject j)
			{
				obj = j;
				return true;
			}
			obj = null;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(string key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryGet<TValue>(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryGet<TValue>(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(string key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryGet<TValue>(ReadOnlySpan<char> key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <paramref name="key"/> property of this object, if it exists.</summary>
		/// <param name="key">Name of the property</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="value">If the property exists and is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ({ "Hello": "World"}).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ({ "Hello": "123"}).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="true" /> and value will be equal to <c>123"</c>
		/// ({ }).TryGet&lt;string&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ }).TryGet&lt;int&gt;("Hello", out var value) // returns <see langword="false" />, and value will be <c>0</c>
		/// ({ "Hello": null }).TryGet&lt;string&gt;("Hello") // returns <see langword="false" />, and value will be <see langword="null"/>
		/// ({ "Hello": null }).TryGet&lt;int&gt;("Hello") // returns <see langword="false" />, and value will be <c>0</c>
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryGet<TValue>(ReadOnlyMemory<char> key, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(key, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello") // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => Exception
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TValue Get<TValue>(string key) where TValue : notnull => GetValue(key).Required<TValue>();

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello") // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => Exception
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public TValue Get<TValue>(ReadOnlySpan<char> key) where TValue : notnull => GetValue(key).Required<TValue>();

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello") // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => Exception
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public TValue Get<TValue>(ReadOnlyMemory<char> key) where TValue : notnull => GetValue(key).Required<TValue>();

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the field is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(string key, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(key).RequiredField(key, message).Required<TValue>(resolver);

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the field is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(ReadOnlySpan<char> key, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(key).RequiredField(key, message).Required<TValue>(resolver);

		/// <summary>Gets the value of the <b>required</b> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the field is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or an exception if the value is null or missing</returns>
		/// <remarks>
		/// <para>This method can never return <see langword="null"/>, which means that there is no point in using a <see cref="Nullable{T}">nullable value type</see> for <typeparamref name="TValue"/>.</para>
		/// </remarks>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(ReadOnlyMemory<char> key, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(key).RequiredField(key, message).Required<TValue>(resolver);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello", "not_found") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello", -1) // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello", null) // => <see langword="null"/>
		/// ({ }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ }).Get&lt;int&gt;("Hello", -1) // => <c>-1</c>
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => <see langword="null"/>
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string key, TValue defaultValue) => GetValueOrDefault(key).As(defaultValue);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello", "not_found") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello", -1) // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello", null) // => <see langword="null"/>
		/// ({ }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ }).Get&lt;int&gt;("Hello", -1) // => <c>-1</c>
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => <see langword="null"/>
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlySpan<char> key, TValue defaultValue) => GetValueOrDefault(key).As(defaultValue);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <example>
		/// ({ "Hello": "World"}).Get&lt;string&gt;("Hello", "not_found") // => <c>"World"</c>
		/// ({ "Hello": "123"}).Get&lt;int&gt;("Hello", -1) // => <c>123</c>
		/// ({ }).Get&lt;string&gt;("Hello", null) // => <see langword="null"/>
		/// ({ }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ }).Get&lt;int&gt;("Hello", -1) // => <c>-1</c>
		/// ({ }).Get&lt;int?&gt;("Hello", null) // => <see langword="null"/>
		/// ({ "Hello": null }).Get&lt;string&gt;("Hello", "not_found") // => <c>"not_found"</c>
		/// ({ "Hello": null }).Get&lt;int&gt;("Hello") // => Exception
		/// ({ "Hello": null }).Get&lt;int?&gt;("Hello") // => Exception
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> key, TValue defaultValue) => GetValueOrDefault(key).As(defaultValue);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(string key, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(key).As(defaultValue, resolver);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlySpan<char> key, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(key).As(defaultValue, resolver);

		/// <summary>Gets the value of the <i>optional</i> field with the specified name, converted into type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the value</typeparam>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Value returned if the value is null or missing</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Value converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the value is null or missing</returns>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(ReadOnlyMemory<char> key, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(key).As(defaultValue, resolver);

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(string key) => GetValue(key).AsObject();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject GetObject(ReadOnlySpan<char> key) => GetValue(key).AsObject();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject GetObject(ReadOnlyMemory<char> key) => GetValue(key).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(string key, JsonObject? defaultValue = null) => GetValueOrDefault(key).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject? GetObjectOrDefault(ReadOnlySpan<char> key, JsonObject? defaultValue = null) => GetValueOrDefault(key).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject? GetObjectOrDefault(ReadOnlyMemory<char> key, JsonObject? defaultValue = null) => GetValueOrDefault(key).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(string key) => GetValueOrDefault(key).AsObjectOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject GetObjectOrEmpty(ReadOnlySpan<char> key) => GetValueOrDefault(key).AsObjectOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="key"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject GetObjectOrEmpty(ReadOnlyMemory<char> key) => GetValueOrDefault(key).AsObjectOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(int index) => GetValueOrDefault(index).RequiredIndex(index).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(int index, JsonObject? defaultValue) => GetValueOrDefault(index).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(int index) => GetValueOrDefault(index).AsObjectOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="InvalidOperationException">If the value is null or missing.</exception>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObject(Index index) => GetValueOrDefault(index).RequiredIndex(index).AsObject();

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <param name="defaultValue">Value that is returned if the value is null or missing</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, <paramref name="defaultValue"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetObjectOrDefault(Index index, JsonObject? defaultValue = null) => GetValueOrDefault(index).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Gets the <i>optional</i> JSON Object that corresponds to the field with the specified name, or an empty (read-only) object if it was null or missing.</summary>
		/// <param name="index">Name of the field that is expected to be an object.</param>
		/// <returns>Value of the field <paramref name="index"/> as a <see cref="JsonObject"/>, the <see cref="JsonObject.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Object.</returns>
		/// <exception cref="ArgumentException">If the value is not a JSON Object.</exception>
		[Pure]
		public JsonObject GetObjectOrEmpty(Index index) => GetValueOrDefault(index).AsObjectOrEmpty();

		#endregion

		#region Array

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(string key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#else
		[EditorBrowsable(EditorBrowsableState.Always)]
#endif
		public bool TryGetArray(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the field with the specified name, if it exists</summary>
		/// <param name="key">Name of the field</param>
		/// <param name="array">When this method returns, contains the value of the field if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#else
		[EditorBrowsable(EditorBrowsableState.Always)]
#endif
		public bool TryGetArray(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(key, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the item at the specified location, if it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="array">When this method returns, contains the value at this location if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(int index, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(index, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the JSON Array that corresponds to the item at the specified location, if it exists</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="array">When this method returns, contains the value at this location if it exists, and is a valid JSON Array; otherwise, <see langword="null"/>. This parameter is passed uninitialized.</param>
		/// <returns><see langword="true"/> if the field exists and contains an array; otherwise, <see langword="false"/>.</returns>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryGetArray(Index index, [MaybeNullWhen(false)] out JsonArray array)
		{
			if (TryGetValue(index, out var child) && child is JsonArray j)
			{
				array = j;
				return true;
			}
			array = null;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(0, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(2, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(3, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(3, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(0, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(0, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(int index, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(0, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(2, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(3, resolver, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(3, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(0, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(0, resolver, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(int index, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^3, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^1, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^4, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^4, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(^1, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(^1, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(Index index, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>();
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the item at the specified location, if it exists.</summary>
		/// <param name="index">Index of the item</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="value">When this method returns, if the location is within the bounds of the array, and the value is not equal to <see langword="null"/>, will receive its value converted into type <typeparamref name="TValue"/>.</param>
		/// <returns><see langword="true" /> if the value was found, and has been converted; otherwise, <see langword="false" />.</returns>
		/// <example>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^3, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>"World"</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^1, resolver, out var value) // returns <see langword="true" /> and value will be equal to <c>123</c>
		/// ([ "Hello", "World", 123 ]).TryGet&lt;string&gt;(^4, resolver, out var value) // returns <see langword="false" />
		/// ([ "Hello", "World", 123 ]).TryGet&lt;int&gt;(^4, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;string&gt;(^1, resolver, out var value) // returns <see langword="false" />
		/// ({ null }).TryGet&lt;int&gt;(^1, resolver, out var value) // returns <see langword="false" />
		/// </example>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public bool TryGet<TValue>(Index index, ICrystalJsonTypeResolver? resolver, [MaybeNullWhen(false)] out TValue value) where TValue : notnull
		{
			if (TryGetValue(index, out var child) && child is not (null or JsonNull))
			{
				value = child.Required<TValue>(resolver);
				return true;
			}
			value = default;
			return false;
		}

		/// <summary>Gets the converted value of the <b>required</b> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TValue Get<TValue>(int index) where TValue : notnull => GetValue(index).Required<TValue>();

		/// <summary>Gets the converted value of the <b>required</b> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="message"></param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(int index, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValueOrDefault(index).RequiredIndex(index, message).Required<TValue>(resolver);

		/// <summary>Gets the converted value of the <b>required</b> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public TValue Get<TValue>(Index index) where TValue : notnull => GetValue(index).Required<TValue>();

		/// <summary>Gets the converted value of the <b>required</b> item at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <param name="message"></param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or an exception if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		// ReSharper disable once MethodOverloadWithOptionalParameter
		public TValue Get<TValue>(Index index, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TValue : notnull => GetValue(index).Required<TValue>(resolver);

		/// <summary>Gets the converted value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>The value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue defaultValue) => GetValueOrDefault(index).As(defaultValue);

		/// <summary>Gets the converted value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(int index, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(index).As(defaultValue, resolver);

		/// <summary>Gets the converted value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue) => GetValueOrDefault(index).As(defaultValue);

		/// <summary>Gets the converted value at the specified index, if it is contained inside the array's bound.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <param name="defaultValue">The value that is returned if the index is outside the bounds of the array.</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>the value located at the specified index converted into type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the index is outside the bounds of the array, OR the value is null or missing.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? Get<TValue>(Index index, TValue defaultValue, ICrystalJsonTypeResolver? resolver) => GetValueOrDefault(index).As(defaultValue, resolver);

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(string key) => GetValue(key).AsArray();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray GetArray(ReadOnlySpan<char> key) => GetValue(key).AsArray();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray GetArray(ReadOnlyMemory<char> key) => GetValue(key).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(string key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray? GetArrayOrDefault(ReadOnlySpan<char> key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray? GetArrayOrDefault(ReadOnlyMemory<char> key) => GetValueOrDefault(key).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(string key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray GetArrayOrEmpty(ReadOnlySpan<char> key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="key">Name of the field that is expected to be an array.</param>
		/// <returns>the value of the field <paramref name="key"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonArray GetArrayOrEmpty(ReadOnlyMemory<char> key) => GetValueOrDefault(key).AsArrayOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(int index) => GetValue(index).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(int index) => GetValueOrDefault(index).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(int index) => GetValueOrDefault(index).AsArrayOrEmpty();

		/// <summary>Gets the <b>required</b> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, or an exception if it is null, missing, or not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is null, missing, or not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArray(Index index) => GetValue(index).AsArray();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, <see langword="null"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray? GetArrayOrDefault(Index index) => GetValueOrDefault(index).AsArrayOrDefault();

		/// <summary>Gets the <i>optional</i> JSON Array that corresponds to the field with the specified name, or and empty (read-only) array if it is null or missing.</summary>
		/// <param name="index">Index of the value to retrieve</param>
		/// <returns>the value of the field <paramref name="index"/> as a <see cref="JsonArray"/>, the <see cref="JsonArray.ReadOnly.Empty"/> if it is null or missing, or an exception if it is not a JSON Array.</returns>
		/// <exception cref="JsonBindingException">If the value is not a JSON Array.</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray GetArrayOrEmpty(Index index) => GetValueOrDefault(index).AsArrayOrEmpty();

		#endregion

		#endregion

		/// <summary>Gets the value at the specified path, if it exists</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="value">Receives the value found at this location, if it exists</param>
		/// <returns><see langword="true"/> if the value was found and was not null or missing; otherwise, <see langword="false"/></returns>
		/// <remarks>This method will return <see langword="false"/> if a field is present but is explicitly set to <see langword="null"/>.</remarks>
		public bool TryGetPathValue(string path, out JsonValue value) => TryGetPathValue(JsonPath.Create(path), out value);

		/// <summary>Gets the value at the specified path, if it exists</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="value">Receives the value found at this location, if it exists</param>
		/// <returns><see langword="true"/> if the value was found and was not null or missing; otherwise, <see langword="false"/></returns>
		/// <remarks>This method will return <see langword="false"/> if a field is present but is explicitly set to <see langword="null"/>.</remarks>
		public bool TryGetPathValue(ReadOnlyMemory<char> path, out JsonValue value) => TryGetPathValue(JsonPath.Create(path), out value);

		/// <summary>Gets the value at the specified path, if it exists</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="value">Receives the value found at this location, if it exists</param>
		/// <returns><see langword="true"/> if the value was found and was not null or missing; otherwise, <see langword="false"/></returns>
		/// <remarks>This method will return <see langword="false"/> if a field is present but is explicitly set to <see langword="null"/>.</remarks>
		public bool TryGetPathValue(JsonPath path, out JsonValue value)
		{
			value = GetPathCore(path, null, required: false);
			return value is not (null or JsonNull);
		}

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValue(string path) => GetPathCore(JsonPath.Create(path), null, required: true);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValue(ReadOnlyMemory<char> path) => GetPathCore(JsonPath.Create(path), null, required: true);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, or <see cref="JsonNull.Missing"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValue(JsonPath path) => GetPathCore(path, null, required: true);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">Value that is returned if the path was not found, or the value is null or missing.</param>
		/// <returns>the value found at this location, or <paramref name="defaultValue"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValueOrDefault(string path, JsonValue? defaultValue = null) => GetPathCore(JsonPath.Create(path), defaultValue, required: false);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">Value that is returned if the path was not found, or the value is null or missing.</param>
		/// <returns>the value found at this location, or <paramref name="defaultValue"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValueOrDefault(ReadOnlyMemory<char> path, JsonValue? defaultValue = null) => GetPathCore(JsonPath.Create(path), defaultValue, required: false);

		/// <summary>Gets the value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">Value that is returned if the path was not found, or the value is null or missing.</param>
		/// <returns>the value found at this location, or <paramref name="defaultValue"/> if no match was found</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public JsonValue GetPathValueOrDefault(JsonPath path, JsonValue? defaultValue = null) => GetPathCore(path, defaultValue, required: false);

		private JsonValue GetPathCore(JsonPath path, JsonValue? defaultValue, bool required)
		{
			var current = this;
			foreach (var segment in path)
			{
				if (segment.TryGetName(out var key))
				{ // field access
					if (current.IsNullOrMissing())
					{
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Missing;
					}
					if (current is not JsonObject obj)
					{ // equivalent to null, but to notify that we tried to index into a value that is not an object
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Error;
					}
					current = obj.GetValueOrDefault(key);
				}
				else if (segment.TryGetIndex(out var idx))
				{ // array index
					if (current.IsNullOrMissing())
					{
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Missing;
					}
					if (current is not JsonArray arr)
					{ // equivalent to null, but to notify that we tried to index into a value that is not an object
						return required ? JsonValueExtensions.FailPathIsNullOrMissing(this, path) : defaultValue ?? JsonNull.Error;
					}
					current = arr.GetValueOrDefault(idx);
				}
			}

			if (current is JsonNull)
			{
				if (required) throw JsonValueExtensions.ErrorPathIsNullOrMissing(this, path);
				// must keep the "type" of null so that we can distinguish between an explicit null, or a missing field in the path
				return defaultValue ?? current;
			}

			return current;
		}

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Object.</exception>
		[Pure]
		public JsonObject GetPathObject(string path) => GetPathCore(JsonPath.Create(path), null, required: true).AsObject();

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Object.</exception>
		[Pure]
		public JsonObject GetPathObject(ReadOnlyMemory<char> path) => GetPathCore(JsonPath.Create(path), null, required: true).AsObject();

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Object.</exception>
		[Pure]
		public JsonObject GetPathObject(JsonPath path) => GetPathCore(path, null, required: true).AsObject();

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetPathObjectOrDefault(string path, JsonObject? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: false).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetPathObjectOrDefault(ReadOnlyMemory<char> path, JsonObject? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: false).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Returns the JSON Object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonObject? GetPathObjectOrDefault(JsonPath path, JsonObject? defaultValue = null) => GetPathCore(path, null, required: false).AsObjectOrDefault() ?? defaultValue;

		/// <summary>Returns the object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonObject.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure]
		public JsonObject GetPathObjectOrEmpty(string path) => GetPathCore(JsonPath.Create(path), null, required: false).AsObjectOrEmpty();

		/// <summary>Returns the object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonObject.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure]
		public JsonObject GetPathObjectOrEmpty(ReadOnlyMemory<char> path) => GetPathCore(JsonPath.Create(path), null, required: false).AsObjectOrEmpty();

		/// <summary>Returns the object at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonObject.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON object.</exception>
		[Pure]
		public JsonObject GetPathObjectOrEmpty(JsonPath path) => GetPathCore(path, null, required: false).AsObjectOrEmpty();

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArray(string path) => GetPathCore(JsonPath.Create(path), null, required: true).AsArray();

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArray(ReadOnlyMemory<char> path) => GetPathCore(JsonPath.Create(path), null, required: true).AsArray();

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <exception cref="JsonBindingException">If there is no matching value, it is null, missing, or not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArray(JsonPath path) => GetPathCore(path, null, required: true).AsArray();

		/// <summary>Returns the JSON AArray at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching array, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonArray? GetPathArrayOrDefault(string path, JsonArray? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrDefault() ?? defaultValue;

		/// <summary>Returns the JSON AArray at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching array, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonArray? GetPathArrayOrDefault(ReadOnlyMemory<char> path, JsonArray? defaultValue = null) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrDefault() ?? defaultValue;

		/// <summary>Returns the JSON AArray at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching array, or null if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public JsonArray? GetPathArrayOrDefault(JsonPath path, JsonArray? defaultValue = null) => GetPathCore(path, null, required: false).AsArrayOrDefault() ?? defaultValue;

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonArray.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArrayOrEmpty(string path) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrEmpty();

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonArray.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArrayOrEmpty(ReadOnlyMemory<char> path) => GetPathCore(JsonPath.Create(path), null, required: false).AsArrayOrEmpty();

		/// <summary>Returns the JSON Array at the specified <see cref="JsonPath">path</see> within this instance</summary>
		/// <returns>The matching object, or <see cref="JsonArray.ReadOnly.Empty"/> if it was not found</returns>
		/// <exception cref="JsonBindingException">If there is a matching value that is not null or missing, and is not a JSON Array.</exception>
		[Pure]
		public JsonArray GetPathArrayOrEmpty(JsonPath path) => GetPathCore(path, null, required: false).AsArrayOrEmpty();

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or and exception if there was no match, or the matched value is null.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue GetPath<TValue>(string path) where TValue : notnull
		{
			return GetPathCore(JsonPath.Create(path), null, required: true).Required<TValue>();
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or and exception if there was no match, or the matched value is null.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue GetPath<TValue>(ReadOnlyMemory<char> path) where TValue : notnull
		{
			return GetPathCore(JsonPath.Create(path), null, required: true).Required<TValue>();
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? GetPath<TValue>(string path, TValue defaultValue)
		{
			return GetPathCore(JsonPath.Create(path), JsonNull.Missing, required: false).As(defaultValue);
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? GetPath<TValue>(ReadOnlyMemory<char> path, TValue defaultValue)
		{
			return GetPathCore(JsonPath.Create(path), JsonNull.Missing, required: false).As(defaultValue);
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or and exception if there was no match, or the matched value is null.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public TValue GetPath<TValue>(JsonPath path) where TValue : notnull
		{
			return GetPathCore(path, null, required: true).Required<TValue>();
		}

		/// <summary>Gets the converted value at the specified path</summary>
		/// <param name="path">Path to the value. ex: <c>"foo"</c>, <c>"foo.bar"</c> or <c>"foo[2].baz"</c></param>
		/// <param name="defaultValue">The default value to return when the no match is found for the specified <paramref name="path" />, or it is null or missing.</param>
		/// <returns>the value found at this location, converted into an instance of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if no match was found or the value is null or missing.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Always)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public TValue? GetPath<TValue>(JsonPath path, TValue defaultValue)
		{
			return GetPathCore(path, JsonNull.Missing, required: false).As(defaultValue);
		}

		#region IJsonSerializable

		/// <inheritdoc />
		public abstract void JsonSerialize(CrystalJsonWriter writer);

		#endregion

		#region IJsonConvertible...

		/// <summary>Returns the equivalent <see cref="System.String"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual string? ToStringOrDefault(string? defaultValue = null) => ToString();

		/// <summary>Returns the equivalent <see cref="System.Boolean"/>, if there exists a valid conversion</summary>
		public virtual bool ToBoolean(bool defaultValue = false) => throw Errors.JsonConversionNotSupported(this, typeof(bool));

		/// <summary>Returns the equivalent <see cref="System.Boolean"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual bool? ToBooleanOrDefault(bool? defaultValue = null) => ToBoolean();

		/// <summary>Returns the equivalent <see cref="System.Byte"/>, if there exists a valid conversion</summary>
		public virtual byte ToByte(byte defaultValue = 0) => throw Errors.JsonConversionNotSupported(this, typeof(byte));

		/// <summary>Returns the equivalent <see cref="System.Byte"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual byte? ToByteOrDefault(byte? defaultValue = null) => ToByte();

		/// <summary>Returns the equivalent <see cref="System.SByte"/>, if there exists a valid conversion</summary>
		public virtual sbyte ToSByte(sbyte defaultValue = 0) => throw Errors.JsonConversionNotSupported(this, typeof(sbyte));

		/// <summary>Returns the equivalent <see cref="System.SByte"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual sbyte? ToSByteOrDefault(sbyte? defaultValue = null) => ToSByte();

		/// <summary>Returns the equivalent <see cref="System.Char"/>, if there exists a valid conversion</summary>
		public virtual char ToChar(char defaultValue = '\0') => throw Errors.JsonConversionNotSupported(this, typeof(char));

		/// <summary>Returns the equivalent <see cref="System.Char"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual char? ToCharOrDefault(char? defaultValue = null) => ToChar();

		/// <summary>Returns the equivalent <see cref="System.Int16"/>, if there exists a valid conversion</summary>
		public virtual short ToInt16(short defaultValue = 0) => throw Errors.JsonConversionNotSupported(this, typeof(short));

		/// <summary>Returns the equivalent <see cref="System.Int16"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual short? ToInt16OrDefault(short? defaultValue = null) => ToInt16();

		/// <summary>Returns the equivalent <see cref="System.UInt16"/>, if there exists a valid conversion</summary>
		public virtual ushort ToUInt16(ushort defaultValue = 0) => throw Errors.JsonConversionNotSupported(this, typeof(ushort));

		/// <summary>Returns the equivalent <see cref="System.UInt16"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual ushort? ToUInt16OrDefault(ushort? defaultValue = null) => ToUInt16();

		/// <summary>Returns the equivalent <see cref="System.Int32"/>, if there exists a valid conversion</summary>
		public virtual int ToInt32(int defaultValue = 0) => throw Errors.JsonConversionNotSupported(this, typeof(int));

		/// <summary>Returns the equivalent <see cref="System.Int32"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual int? ToInt32OrDefault(int? defaultValue = null) => ToInt32();

		/// <summary>Returns the equivalent <see cref="System.UInt32"/>, if there exists a valid conversion</summary>
		public virtual uint ToUInt32(uint defaultValue = 0U) => throw Errors.JsonConversionNotSupported(this, typeof(uint));

		/// <summary>Returns the equivalent <see cref="System.UInt32"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual uint? ToUInt32OrDefault(uint? defaultValue = null) => ToUInt32();

		/// <summary>Returns the equivalent <see cref="System.Int64"/>, if there exists a valid conversion</summary>
		public virtual long ToInt64(long defaultValue = 0L) => throw Errors.JsonConversionNotSupported(this, typeof(long));

		/// <summary>Returns the equivalent <see cref="System.Int64"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual long? ToInt64OrDefault(long? defaultValue = null) => ToInt64();

		/// <summary>Returns the equivalent <see cref="System.UInt64"/>, if there exists a valid conversion</summary>
		public virtual ulong ToUInt64(ulong defaultValue = 0UL) => throw Errors.JsonConversionNotSupported(this, typeof(ulong));

		/// <summary>Returns the equivalent <see cref="System.UInt64"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual ulong? ToUInt64OrDefault(ulong? defaultValue = null) => ToUInt64();

		/// <summary>Returns the equivalent <see cref="System.Single"/>, if there exists a valid conversion</summary>
		public virtual float ToSingle(float defaultValue = 0f) => throw Errors.JsonConversionNotSupported(this, typeof(float));

		/// <summary>Returns the equivalent <see cref="System.Single"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual float? ToSingleOrDefault(float? defaultValue = null) => ToSingle();

		/// <summary>Returns the equivalent <see cref="System.Double"/>, if there exists a valid conversion</summary>
		public virtual double ToDouble(double defaultValue = 0d) => throw Errors.JsonConversionNotSupported(this, typeof(double));

		/// <summary>Returns the equivalent <see cref="System.Double"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual double? ToDoubleOrDefault(double? defaultValue = null) => ToDouble();

		/// <summary>Returns the equivalent <see cref="System.Half"/>, if there exists a valid conversion</summary>
		public virtual Half ToHalf(Half defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Half));

		/// <summary>Returns the equivalent <see cref="System.Half"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Half? ToHalfOrDefault(Half? defaultValue = null) => ToHalf();

		/// <summary>Returns the equivalent <see cref="Decimal"/>, if there exists a valid conversion</summary>
		public virtual decimal ToDecimal(decimal defaultValue = 0m) => throw Errors.JsonConversionNotSupported(this, typeof(decimal));

		/// <summary>Returns the equivalent <see cref="Decimal"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual decimal? ToDecimalOrDefault(decimal? defaultValue = null) => ToDecimal();

#if NET8_0_OR_GREATER

		/// <summary>Returns the equivalent <see cref="System.Int128"/>, if there exists a valid conversion</summary>
		public virtual Int128 ToInt128(Int128 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(double));

		/// <summary>Returns the equivalent <see cref="System.Int128"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Int128? ToInt128OrDefault(Int128? defaultValue = null) => ToInt128();

		/// <summary>Returns the equivalent <see cref="System.UInt128"/>, if there exists a valid conversion</summary>
		public virtual UInt128 ToUInt128(UInt128 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(double));

		/// <summary>Returns the equivalent <see cref="System.UInt128"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual UInt128? ToUInt128OrDefault(UInt128? defaultValue = null) => ToUInt128();

#endif

		/// <summary>Returns the equivalent <see cref="Guid"/>, if there exists a valid conversion</summary>
		public virtual Guid ToGuid(Guid defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Guid));

		/// <summary>Returns the equivalent <see cref="Guid"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Guid? ToGuidOrDefault(Guid? defaultValue = null) => ToGuid();

		/// <summary>Returns the equivalent <see cref="Uuid128"/>, if there exists a valid conversion</summary>
		public virtual Uuid128 ToUuid128(Uuid128 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Uuid128));

		/// <summary>Returns the equivalent <see cref="Uuid128"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid128? ToUuid128OrDefault(Uuid128? defaultValue = null) => ToUuid128();

		/// <summary>Returns the equivalent <see cref="Uuid96"/>, if there exists a valid conversion</summary>
		public virtual Uuid96 ToUuid96(Uuid96 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Uuid96));

		/// <summary>Returns the equivalent <see cref="Uuid96"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid96? ToUuid96OrDefault(Uuid96? defaultValue = null) => ToUuid96();

		/// <summary>Returns the equivalent <see cref="Uuid80"/>, if there exists a valid conversion</summary>
		public virtual Uuid80 ToUuid80(Uuid80 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Uuid80));

		/// <summary>Returns the equivalent <see cref="Uuid80"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid80? ToUuid80OrDefault(Uuid80? defaultValue = null) => ToUuid80();

		/// <summary>Returns the equivalent <see cref="Uuid64"/>, if there exists a valid conversion</summary>
		public virtual Uuid64 ToUuid64(Uuid64 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Uuid64));

		/// <summary>Returns the equivalent <see cref="Uuid64"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid64? ToUuid64OrDefault(Uuid64? defaultValue = null) => ToUuid64();

		/// <summary>Returns the equivalent <see cref="Uuid48"/>, if there exists a valid conversion</summary>
		public virtual Uuid48 ToUuid48(Uuid48 defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(Uuid48));

		/// <summary>Returns the equivalent <see cref="Uuid48"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual Uuid48? ToUuid48OrDefault(Uuid48? defaultValue = null) => ToUuid48();

		/// <summary>Returns the equivalent <see cref="DateTime"/>, if there exists a valid conversion</summary>
		public virtual DateTime ToDateTime(DateTime defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(DateTime));

		/// <summary>Returns the equivalent <see cref="DateTime"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual DateTime? ToDateTimeOrDefault(DateTime? defaultValue = null) => ToDateTime();

		/// <summary>Returns the equivalent <see cref="DateTimeOffset"/>, if there exists a valid conversion</summary>
		public virtual DateTimeOffset ToDateTimeOffset(DateTimeOffset defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(DateTimeOffset));

		/// <summary>Returns the equivalent <see cref="DateTimeOffset"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual DateTimeOffset? ToDateTimeOffsetOrDefault(DateTimeOffset? defaultValue = null) => ToDateTimeOffset();

		/// <summary>Returns the equivalent <see cref="DateOnly"/>, if there exists a valid conversion</summary>
		public virtual DateOnly ToDateOnly(DateOnly defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(DateOnly));

		/// <summary>Returns the equivalent <see cref="DateOnly"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual DateOnly? ToDateOnlyOrDefault(DateOnly? defaultValue = null) => ToDateOnly();

		/// <summary>Returns the equivalent <see cref="TimeOnly"/>, if there exists a valid conversion</summary>
		public virtual TimeOnly ToTimeOnly(TimeOnly defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(TimeOnly));

		/// <summary>Returns the equivalent <see cref="TimeOnly"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TimeOnly? ToTimeOnlyOrDefault(TimeOnly? defaultValue = null) => ToTimeOnly();

		/// <summary>Returns the equivalent <see cref="TimeSpan"/>, if there exists a valid conversion</summary>
		public virtual TimeSpan ToTimeSpan(TimeSpan defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		/// <summary>Returns the equivalent <see cref="TimeSpan"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TimeSpan? ToTimeSpanOrDefault(TimeSpan? defaultValue = null) => ToTimeSpan();

		/// <summary>Returns the equivalent <see cref="Enum"/>, if there exists a valid conversion</summary>
		public virtual TEnum ToEnum<TEnum>(TEnum defaultValue = default)
			where TEnum : struct, Enum
			=> throw Errors.JsonConversionNotSupported(this, typeof(TimeSpan));

		/// <summary>Returns the equivalent <see cref="Enum"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual TEnum? ToEnumOrDefault<TEnum>(TEnum? defaultValue = null)
			where TEnum : struct, Enum
			=> ToEnum<TEnum>();

		/// <summary>Returns the equivalent <see cref="NodaTime.Instant"/>, if there exists a valid conversion</summary>
		public virtual NodaTime.Instant ToInstant(NodaTime.Instant defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Instant));

		/// <summary>Returns the equivalent <see cref="NodaTime.Instant"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual NodaTime.Instant? ToInstantOrDefault(NodaTime.Instant? defaultValue = null) => ToInstant();

		/// <summary>Returns the equivalent <see cref="NodaTime.Duration"/>, if there exists a valid conversion</summary>
		public virtual NodaTime.Duration ToDuration(NodaTime.Duration defaultValue = default) => throw Errors.JsonConversionNotSupported(this, typeof(NodaTime.Duration));

		/// <summary>Returns the equivalent <see cref="NodaTime.Duration"/>, if there exists a valid conversion</summary>
		[Pure][return: NotNullIfNotNull(nameof(defaultValue))]
		public virtual NodaTime.Duration? ToDurationOrDefault(NodaTime.Duration? defaultValue = null) => ToDuration();

		//TODO: ToZonedDateTime, ToLocalDateTime ?

		#endregion

		#region IConvertible...

		/// <inheritdoc />
		TypeCode IConvertible.GetTypeCode() => this.Type switch
		{
			JsonType.String => TypeCode.String,
			JsonType.Boolean => TypeCode.Boolean,
			_ => TypeCode.Object,
		};

		/// <inheritdoc />
		bool IConvertible.ToBoolean(IFormatProvider? provider) => ToBoolean();

		/// <inheritdoc />
		byte IConvertible.ToByte(IFormatProvider? provider) => ToByte();

		/// <inheritdoc />
		char IConvertible.ToChar(IFormatProvider? provider) => ToChar();

		/// <inheritdoc />
		DateTime IConvertible.ToDateTime(IFormatProvider? provider) => ToDateTime();

		/// <inheritdoc />
		decimal IConvertible.ToDecimal(IFormatProvider? provider) => ToDecimal();

		/// <inheritdoc />
		double IConvertible.ToDouble(IFormatProvider? provider) => ToDouble();

		/// <inheritdoc />
		short IConvertible.ToInt16(IFormatProvider? provider) => ToInt16();

		/// <inheritdoc />
		int IConvertible.ToInt32(IFormatProvider? provider) => ToInt32();

		/// <inheritdoc />
		long IConvertible.ToInt64(IFormatProvider? provider) => ToInt64();

		/// <inheritdoc />
		sbyte IConvertible.ToSByte(IFormatProvider? provider) => ToSByte();

		/// <inheritdoc />
		float IConvertible.ToSingle(IFormatProvider? provider) => ToSingle();

		/// <inheritdoc />
		string IConvertible.ToString(IFormatProvider? provider) => ToStringOrDefault()!;

		/// <inheritdoc />
		ushort IConvertible.ToUInt16(IFormatProvider? provider) => ToUInt16();

		/// <inheritdoc />
		uint IConvertible.ToUInt32(IFormatProvider? provider) => ToUInt32();

		/// <inheritdoc />
		ulong IConvertible.ToUInt64(IFormatProvider? provider) => ToUInt64();

		/// <inheritdoc />
#pragma warning disable IL2026
#pragma warning disable IL2067
		object IConvertible.ToType(Type conversionType, IFormatProvider? provider) => Bind(conversionType)!;
#pragma warning restore IL2067
#pragma warning restore IL2026

		#endregion

		#region ISliceSerializable...

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public abstract void WriteTo(ref SliceWriter writer);

		#endregion

		#region IFormattable, IParsable<>, ...

		/// <inheritdoc />
		[Pure]
		public static JsonValue Parse(string jsonText, IFormatProvider? provider)
			=> Parse(jsonText, CrystalJsonSettings.Json);

		/// <inheritdoc />
		[Pure]
		public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonValue result)
			=> TryParse(s, CrystalJsonSettings.Json, out result);

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>It is better to call overloads of this method that accept a <see cref="CrystalJsonSettings"/> argument.</remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<char> jsonText, IFormatProvider? provider)
			=> Parse(jsonText, CrystalJsonSettings.Json);

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value if it is valid.</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></param>
		/// <returns><c>true</c> if <paramref name="jsonText"/> contained valid JSON; otherwise, <c>false</c></returns>
		/// <remarks>It is better to call overloads of this method that accept a <see cref="CrystalJsonSettings"/> argument.</remarks>
		[Pure]
		public static bool TryParse(ReadOnlySpan<char> jsonText, IFormatProvider? provider, [MaybeNullWhen(false)] out JsonValue result)
			=> TryParse(jsonText, CrystalJsonSettings.Json, out result);

		/// <inheritdoc />
		public abstract bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null);

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public abstract bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null);

#endif

		#endregion

	}

	#region System.Text.Json Adapter...

	/// <summary>Converter that adds support for <see cref="JsonValue"/> (and derived types) to <c>System.Text.Json</c>'s <see cref="System.Text.Json.Serialization.JsonConverter"/></summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public sealed class CrystalJsonCustomJsonConverter: System.Text.Json.Serialization.JsonConverter<JsonValue>
	{

		/// <summary>Singleton that can be added to <see cref="System.Text.Json.JsonSerializerOptions"/> if support for <see cref="JsonValue"/> is required</summary>
		public static readonly CrystalJsonCustomJsonConverter Instance = new();

		/// <inheritdoc />
		public override bool CanConvert(Type typeToConvert) => typeof(JsonValue).IsAssignableFrom(typeToConvert);

		/// <inheritdoc />
		public override JsonValue Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
		{
			// skip comments
			while(reader.TokenType is System.Text.Json.JsonTokenType.Comment && reader.Read())
			{ }

			switch (reader.TokenType)
			{
				case System.Text.Json.JsonTokenType.Null:
				{
					reader.Read();
					return JsonNull.Null;
				}
				case System.Text.Json.JsonTokenType.False:
				{
					reader.Read();
					return JsonBoolean.False;
				}
				case System.Text.Json.JsonTokenType.True:
				{
					reader.Read();
					return JsonBoolean.True;
				}
				case System.Text.Json.JsonTokenType.String:
				{
					return JsonString.Return(reader.GetString());
				}
				case System.Text.Json.JsonTokenType.Number:
				{
					// is it an int? a decimal?
					if (reader.TryGetInt64(out var l))
					{
						return JsonNumber.Return(l);
					}
					if (reader.TryGetUInt64(out var ul))
					{
						return JsonNumber.Return(ul);
					}
					if (reader.TryGetDouble(out var d))
					{
						return JsonNumber.Return(d);
					}
					if (reader.TryGetDecimal(out var m))
					{
						return JsonNumber.Return(m);
					}
					throw new System.Text.Json.JsonException();
				}
				case System.Text.Json.JsonTokenType.StartObject:
				{
					var obj = new JsonObject();
					while (reader.Read())
					{
						if (reader.TokenType == System.Text.Json.JsonTokenType.EndObject)
						{
							break;
						}

						if (reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
						{
							var name = reader.GetString()!;
							reader.Read();
							var value = Read(ref reader, typeof(object), options);
							obj.Add(name, value);
						}
					}
					reader.Read();
					return obj;
				}
				case System.Text.Json.JsonTokenType.StartArray:
				{
					var arr = new JsonArray();
					reader.Read();
					while (true)
					{
						if (reader.TokenType == System.Text.Json.JsonTokenType.EndArray)
						{
							break;
						}
						if (reader.TokenType is System.Text.Json.JsonTokenType.Comment)
						{
							continue;
						}
						arr.Add(Read(ref reader, typeof(object), options));
					}
					reader.Read();
					return arr;
				}
				default:
				{
					throw new System.Text.Json.JsonException();
				}
			}
		}

		/// <inheritdoc />
		public override void Write(System.Text.Json.Utf8JsonWriter writer, JsonValue value, System.Text.Json.JsonSerializerOptions options)
		{
			switch (value)
			{
				case JsonNull:
				{
					writer.WriteNullValue();
					break;
				}
				case JsonBoolean b:
				{
					writer.WriteBooleanValue(b.Value);
					break;
				}
				case JsonNumber n:
				{
					if (n.IsDecimal)
					{
						//TODO: handle "decimal" ?
						writer.WriteNumberValue(n.ToDouble());
					}
					else if (n.IsUnsigned)
					{
						writer.WriteNumberValue(n.ToUInt64());
					}
					else
					{
						writer.WriteNumberValue(n.ToInt64());
					}
					break;
				}
				case JsonString str:
				{
					writer.WriteStringValue(str.Value);
					break;
				}
				case JsonDateTime dt:
				{
					if (dt.HasOffset)
					{
						writer.WriteStringValue(dt.DateWithOffset);
					}
					else
					{
						writer.WriteStringValue(dt.Date);
					}
					break;
				}
				case JsonObject obj:
				{
					writer.WriteStartObject();
					foreach (var kv in obj)
					{
						writer.WritePropertyName(kv.Key);
						Write(writer, kv.Value, options);
					}
					writer.WriteEndObject();
					break;
				}
				case JsonArray arr:
				{
					writer.WriteStartArray();
					foreach (var v in arr)
					{
						Write(writer, v, options);
					}
					writer.WriteEndArray();
					break;
				}
				default:
				{
					throw new NotSupportedException();
				}
			}
		}
	}

	#endregion

}
