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

// ReSharper disable RedundantTypeArgumentsOfMethod

#pragma warning disable IL2087 // Target parameter argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The generic parameter of the source method or type does not have matching annotations.

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Collections;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Diagnostics.CodeAnalysis;
	using System.Reflection;
	using System.Runtime.InteropServices;
	using System.Text;
	using NodaTime;
	using SnowBank.Buffers;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	/// <summary>Array of JSON values</summary>
	[Serializable]
	[DebuggerDisplay("JSON Array[{m_size}]{GetMutabilityDebugLiteral(),nq} {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(DebugView))]
	[PublicAPI]
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
	[DebuggerNonUserCode]
#if NET9_0_OR_GREATER
	[CollectionBuilder(typeof(JsonArray), nameof(JsonArray.Create))]
#endif
	public sealed partial class JsonArray : JsonValue, IList<JsonValue>, IReadOnlyList<JsonValue>, IEquatable<JsonArray>, IComparable<JsonArray>
	{
		/// <summary>Initial resize capacity for an empty array</summary>
		internal const int DEFAULT_CAPACITY = 4;

		/// <summary>Maximum size for a buffer to be kept after a clear</summary>
		internal const int MAX_KEEP_CAPACITY = 1024;

		/// <summary>Maximum auto-growth capacity</summary>
		internal const int MAX_GROWTH_CAPACITY = 0X7FEFFFFF;

		private JsonValue[] m_items;
		private int m_size;
		private bool m_readOnly;

		#region Debug View...

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		internal sealed class DebugView
		{
			private readonly JsonArray m_array;

			public DebugView(JsonArray array) => m_array = array;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public DebugViewItem[] Items
			{
				get
				{
					var tmp = m_array.ToArray();
					var items = new DebugViewItem[tmp.Length];
					for (int i = 0; i < items.Length; ++i)
					{
						items[i] = new(i, tmp[i]);
					}
					return items;
				}
			}

		}

		[DebuggerDisplay("{Value.GetCompactRepresentation(0),nq}", Name = "[{Index}]")]
		internal readonly struct DebugViewItem
		{
			public DebugViewItem(int index, JsonValue value)
			{
				this.Index = index;
				this.Value = value;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public readonly int Index;

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public readonly JsonValue Value;

		}

		#endregion

		#region Constructors...

		/// <summary>Returns an empty, read-only, <see cref="JsonArray">JSON Array</see> singleton</summary>
		/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
		[Obsolete("Use JsonArray.ReadOnly.Empty instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonArray EmptyReadOnly => JsonArray.ReadOnly.Empty;

		/// <summary>Creates a new empty JSON Array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonArray()
		{
			m_items = [ ];
		}

		/// <summary>Creates a new empty JSON Array</summary>
		/// <param name="capacity">Initial capacity</param>
		public JsonArray([Positive] int capacity)
		{
			Contract.Positive(capacity);

			m_items = capacity == 0 ? [ ] : new JsonValue[capacity];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonArray(JsonValue[] items, int count, bool readOnly)
		{
			Contract.Debug.Requires(items is not null && count <= items.Length);
			m_items = items;
			m_size = count;
			m_readOnly = readOnly;
#if FULL_DEBUG
			for (int i = 0; i < count; i++) Contract.Debug.Requires(items[i] is not null, "Array cannot contain nulls");
#endif
		}

		/// <summary>Fills all the occurrences of <see langword="null"/> with <see cref="JsonNull.Null"/> in the specified array</summary>
		private static void FillNullValues(Span<JsonValue?> items)
		{
			for (int i = 0; i < items.Length; i++)
			{
				items[i] ??= JsonNull.Null;
			}
		}

		/// <summary>Freezes this object and all its children, and mark it as read-only.</summary>
		/// <remarks>
		/// <para>This is similar to the <c>{ get; init; }</c> pattern for CLR records, and allows initializing a JSON object and then marking it as read-only once it is ready to be returned and/or shared, without performing extra memory allocations.</para>
		/// <para>Please note that, once "frozen", the operation cannot be reverted, and if additional mutations are required, a new copy of the object must be created.</para>
		/// </remarks>
		public override JsonArray Freeze()
		{
			if (!m_readOnly)
			{
				// we need to ensure that all children are also frozen!
				foreach(var item in AsSpan())
				{
					item.Freeze();
				}
				m_readOnly = true;
			}

			return this;
		}

		/// <summary>[DANGEROUS] Marks this array as read-only, without performing any extra checks!</summary>
		internal JsonArray FreezeUnsafe()
		{
			if (m_readOnly && m_size == 0)
			{ // ensure that we always return the empty singleton !
				return JsonArray.ReadOnly.Empty;
			}
			m_readOnly = true;
			return this;
		}

		/// <summary>Returns a new immutable read-only version of this <see cref="JsonArray">JSON Array</see> (and all of its children)</summary>
		/// <returns>The same object, if it is already read-only; otherwise, a deep copy marked as read-only.</returns>
		/// <remarks>A JSON object that is immutable is truly safe against any modification, including any of its direct or indirect children.</remarks>
		public override JsonArray ToReadOnly()
		{
			if (m_readOnly)
			{ // already immutable
				return this;
			}

			var items = AsSpan();
			var res = new JsonValue[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = items[i].ToReadOnly();
			}
			return new(res, items.Length, readOnly: true);
		}

		/// <summary>Returns a new mutable copy of this <see cref="JsonArray">JSON Array</see> (and all of its children)</summary>
		/// <returns>A deep copy of this array and its children.</returns>
		/// <remarks>
		/// <para>This will recursively copy all JSON objects or arrays present in the array, even if they are already mutable.</para>
		/// </remarks>
		public override JsonArray Copy()
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];

			var buf = new JsonValue[items.Length];
			// copy all children
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].Copy();
			}
			return new(buf, items.Length, readOnly: false);
		}

		/// <summary>Creates a copy of this array</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the array, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		[Pure]
		protected internal override JsonArray Copy(bool deep, bool readOnly) => Copy(this, deep, readOnly);

		/// <summary>Creates a copy of a <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="array"><see cref="JsonArray">JSON Array</see> to clone</param>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of <paramref name="array"/>, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public static JsonArray Copy(JsonArray array, bool deep, bool readOnly = false)
		{
			Contract.NotNull(array);

			if (readOnly)
			{
				return array.ToReadOnly();
			}

			if (array.Count == 0)
			{ // empty mutable singleton
				return [ ];
			}

			var items = array.AsSpan();
			if (!deep)
			{
				return new(items.ToArray(), items.Length, readOnly);
			}

			// copy all children
			var buf = new JsonValue[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].Copy();
			}
			return new(buf, items.Length, readOnly: false);
		}

		#region Create [JsonValue] ...

		// these methods take the items as JsonValue, and create a new mutable array that wraps them

		/// <summary>Creates a new <b>mutable</b> empty <see cref="JsonArray">JSON Array</see></summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create()"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static JsonArray Create() => new();

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> with a single element</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(JsonValue?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static JsonArray Create(JsonValue? value) => new([
			value ?? JsonNull.Null
		], 1, readOnly: false);

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> with 2 elements</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(JsonValue?,JsonValue?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null
		], 2, readOnly: false);

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> with 3 elements</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(JsonValue?,JsonValue?,JsonValue?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null,
			value3 ?? JsonNull.Null
		], 3, readOnly: false);

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> with 4 elements</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(JsonValue?,JsonValue?,JsonValue?,JsonValue?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3, JsonValue? value4) => new([
			value1 ?? JsonNull.Null,
			value2 ?? JsonNull.Null,
			value3 ?? JsonNull.Null,
			value4 ?? JsonNull.Null
		], 4, readOnly: false);

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> from an array of elements</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(JsonValue?[])"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static JsonArray Create(JsonValue?[] values)
#else
		public static JsonArray Create(params JsonValue?[] values)
#endif
		{
			return Create(new ReadOnlySpan<JsonValue?>(Contract.ValueNotNull(values)));
		}

		/// <summary>Creates a new <see cref="JsonArray"/> from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonArray"/> that cannot be modified.</param>
		/// <param name="items">Items to copy</param>
		/// <returns>New <see cref="JsonArray"/> with the same elements as in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}})"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray Create(bool readOnly, JsonValue?[] items)
		{
			return readOnly ? JsonArray.ReadOnly.Create(items) : Create(items);
		}

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> from a span of elements</summary>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(ReadOnlySpan{JsonValue?})"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET9_0_OR_GREATER
		public static JsonArray Create(params ReadOnlySpan<JsonValue?> items)
#else
		public static JsonArray Create(ReadOnlySpan<JsonValue?> items)
#endif
		{
			// <JIT_HACK>
			// In the case where the call-site is constructing the span using a collection express: var xs = JsonArray.Create([ "hello", "world" ])
			// the JIT can optimize the method, since it knows that values.Length == 2, and can safely remove the test and the rest of the method, as well as inline the ctor.
			// => using JIT disassembly, we can see that the whole JSON Array construction will be completely inlined in the caller's method body

			// note: currently (.NET 9 preview), the JIT will not elide the null-check on the values, even if they are known to be not-null.

			switch (items.Length)
			{
				case 0:
				{
					return new();
				}
				case 1:
				{
					return new([
						items[0] ?? JsonNull.Null
					], 1, readOnly: false);
				}
				case 2:
				{
					return new([
						items[0] ?? JsonNull.Null,
						items[1] ?? JsonNull.Null
					], 2, readOnly: false);
				}
				case 3:
				{
					return new([
						items[0] ?? JsonNull.Null,
						items[1] ?? JsonNull.Null,
						items[2] ?? JsonNull.Null
					], 3, readOnly: false);
				}
			}

			// </JIT_HACK>

			var buf = items.ToArray();
			for (int i = 0; i < buf.Length; i++)
			{
				buf[i] ??= JsonNull.Null;
			}
			return new JsonArray(buf!, buf.Length, readOnly: false);
		}

		
		/// <summary>Creates a new <see cref="JsonArray"/> from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonArray"/> that cannot be modified.</param>
		/// <param name="items">Items to copy</param>
		/// <returns>New <see cref="JsonArray"/> with the same elements as in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// <para>This overload is intended for creating a read-only <see cref="JsonArray"/> using collection expressions via the following syntax:
		/// <code>
		/// // create a new read-only array
		/// JsonArray immutable = [ with(readOnly: true), "hello", "world", 123, /*...*/ ];
		/// // array cannot be modified
		/// immutable.Add("there"); // => throws InvalidOperationException
		/// </code>
		/// </para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.ReadOnlySpan{System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}})"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray Create(bool readOnly, ReadOnlySpan<JsonValue?> items)
		{
			return readOnly ? JsonArray.ReadOnly.Create(items) : Create(items);
		}

		/// <summary>Creates a new <b>mutable</b> <see cref="JsonArray">JSON Array</see> from a sequence of elements</summary>
		/// <param name="items">Elements of the new array</param>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.Create(IEnumerable{JsonValue?})"/></remarks>
		[Pure, OverloadResolutionPriority(-1)]
		public static JsonArray Create(IEnumerable<JsonValue?> items)
		{
			Contract.NotNull(items);
			return new JsonArray().AddRange(items);
		}

		/// <summary>Creates a new <see cref="JsonArray"/> from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonArray"/> that cannot be modified.</param>
		/// <param name="items">Items to copy</param>
		/// <returns>New <see cref="JsonArray"/> with the same elements as in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}})"/>
		[Pure, OverloadResolutionPriority(-1)]
		public static JsonArray Create(bool readOnly, IEnumerable<JsonValue?> items)
		{
			return readOnly ? JsonArray.ReadOnly.Create(items) : Create(items);
		}

		#endregion

		#region FromValues [of T] ...

		/// <summary>Creates a new mutable JSON Array from a span of raw values.</summary>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues<TValue>(ReadOnlySpan<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues<TValue>(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from an array of raw values.</summary>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="values">Array of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(TValue[],CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull(nameof(values))]
		public static JsonArray? FromValues<TValue>(TValue[]? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (values is null) return null;

			var arr = new JsonArray();
			arr.AddValues<TValue>(new ReadOnlySpan<TValue>(values), settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a sequence of raw values.</summary>
		/// <typeparam name="TValue">Type of the values</typeparam>
		/// <param name="values">Sequence of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(IEnumerable{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull(nameof(values))]
		public static JsonArray? FromValues<TValue>(IEnumerable<TValue>? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (values is null) return null;

			var arr = new JsonArray();
			arr.AddValues<TValue>(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array with values extracted from a span of source items.</summary>
		/// <typeparam name="TItem">Type of the source items</typeparam>
		/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
		/// <param name="values">Span of items to convert</param>
		/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all the values extracted from the source, and converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TItem,TValue}(ReadOnlySpan{TItem},Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure]
		public static JsonArray FromValues<TItem, TValue>(ReadOnlySpan<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(selector);

			if (values.Length == 0) return settings.IsReadOnly() ? JsonArray.ReadOnly.Empty : [ ];

			var arr = new JsonArray().AddValues(values, selector, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array with values extracted from an array of source items.</summary>
		/// <typeparam name="TItem">Type of the source items</typeparam>
		/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
		/// <param name="values">Array of items to convert</param>
		/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all the values extracted from the source, and converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TItem,TValue}(TItem[],Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull(nameof(values))]
		public static JsonArray? FromValues<TItem, TValue>(TItem[]? values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return values is null ? null : FromValues(new ReadOnlySpan<TItem>(values), selector, settings, resolver);
		}

		/// <summary>Creates a new mutable JSON Array with values extracted from a sequence of source items.</summary>
		/// <typeparam name="TItem">Type of the source items</typeparam>
		/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
		/// <param name="values">Sequence of items to convert</param>
		/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all the values extracted from the source, and converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TItem,TValue}(IEnumerable{TItem},Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure]
		[return: NotNullIfNotNull(nameof(values))]
		public static JsonArray? FromValues<TItem, TValue>(IEnumerable<TItem>? values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (values is null) return null;
			if (values.TryGetSpan(out var span))
			{
				return FromValues(span, selector, settings, resolver);
			}

			return FromValuesEnumerable(values, selector, settings, resolver);

			static JsonArray FromValuesEnumerable(IEnumerable<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
			{
				if (values.TryGetNonEnumeratedCount(out int count) && count == 0)
				{
					return settings.IsReadOnly() ? JsonArray.ReadOnly.Empty : [ ];
				}

				var arr = new JsonArray().AddValues(values, selector, settings, resolver);
				if (settings?.ReadOnly ?? false)
				{
					arr = arr.FreezeUnsafe();
				}
				return arr;
			}

		}

		/// <summary>Creates a new mutable JSON Array from a span of string literals.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<string?> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of numbers.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<int> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of numbers.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<long> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of numbers.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<float> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of numbers.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<double> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of <see cref="Guid"/>.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<Guid> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}

		/// <summary>Creates a new mutable JSON Array from a span of <see cref="Uuid128"/>.</summary>
		/// <param name="values">Span of values that must be converted</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Mutable array with all values converted into <see cref="JsonValue"/> instances</returns>
		/// <remarks>For a <b>read-only</b> array, see <see cref="JsonArray.ReadOnly.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray FromValues(ReadOnlySpan<Uuid128> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var arr = new JsonArray();
			arr.AddValues(values, settings, resolver);
			if (settings?.ReadOnly ?? false)
			{
				arr = arr.FreezeUnsafe();
			}
			return arr;
		}


		#endregion

		#region Tuples...

		/// <summary>Converts a <see cref="ValueTuple{T1}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1>(ValueTuple<T1> tuple) => new([ FromValue<T1>(tuple.Item1) ], 1, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2>(in ValueTuple<T1, T2> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2) ], 2, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3>(in ValueTuple<T1, T2, T3> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3) ], 3, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4>(in ValueTuple<T1, T2, T3, T4> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4) ], 4, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5>(in ValueTuple<T1, T2, T3, T4, T5> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5) ], 5, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6>(in ValueTuple<T1, T2, T3, T4, T5, T6> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6) ], 6, readOnly: false);

		/// <summary>Converts a <see cref="ValueTuple{T1,T2,T3,T4,T5,T6,T7}"/> into the equivalent <see cref="JsonArray"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray Return<T1, T2, T3, T4, T5, T6, T7>(in ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple) => new([ FromValue<T1>(tuple.Item1), FromValue<T2>(tuple.Item2), FromValue<T3>(tuple.Item3), FromValue<T4>(tuple.Item4), FromValue<T5>(tuple.Item5), FromValue<T6>(tuple.Item6), FromValue<T7>(tuple.Item7) ], 7, readOnly: false);

		#endregion

		/// <summary>Combines two JsonArrays into a single new array</summary>
		/// <remarks>The new array contains a copy of the items of the two input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2)
		{
			var items1 = arr1.AsSpan();
			var items2 = arr2.AsSpan();

			var newSize = checked(items1.Length + items2.Length);

			var tmp = new JsonValue[newSize];
			items1.CopyTo(tmp);
			items2.CopyTo(tmp.AsSpan(items1.Length));

			return new(tmp, newSize, readOnly: false);
		}

		/// <summary>Combines three JsonArrays into a single new array</summary>
		/// <remarks>The new array contains a copy of the items of the three input arrays</remarks>
		public static JsonArray Combine(JsonArray arr1, JsonArray arr2, JsonArray arr3)
		{
			var items1 = arr1.AsSpan();
			var items2 = arr2.AsSpan();
			var items3 = arr3.AsSpan();

			var newSize = checked(items1.Length + items2.Length + items3.Length);

			var tmp = new JsonValue[newSize];
			items1.CopyTo(tmp);
			items2.CopyTo(tmp.AsSpan(items1.Length));
			items3.CopyTo(tmp.AsSpan(items1.Length + items2.Length));

			return new(tmp, newSize, readOnly: false);
		}

		#endregion

		/// <inheritdoc />
		public override JsonType Type => JsonType.Array;

		/// <inheritdoc />
		public override bool IsNull => false;

		/// <inheritdoc />
		public override bool IsDefault => false;

		/// <inheritdoc cref="JsonValue.IsReadOnly"/>
		public override bool IsReadOnly => m_readOnly;

		#region List<T>...

		/// <summary>Returns the number of items in the array.</summary>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public int Count
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_size;
		}

		/// <summary>Gets or sets the internal capacity of the array</summary>
		/// <remarks>
		/// <para>Should be used before inserting a large number of items, if the final length is known in advance.</para>
		/// </remarks>
		/// <exception cref="ArgumentOutOfRangeException">If the capacity is less than the current <see cref="Count"/></exception>
		public int Capacity
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items.Length;
			set
			{
				if (value < m_size) throw ThrowHelper.ArgumentOutOfRangeException(nameof(value), value, "capacity was less than the current size.");
				if (value != m_items.Length)
				{
					ResizeBuffer(value);
				}
			}
		}

		/// <summary>Ensures that the buffer is large enough, and resize it if that is not the case</summary>
		/// <param name="requiredCapacity">Minimum capacity requested</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private JsonValue[] EnsureCapacity(int requiredCapacity)
		{
			var items = m_items;
			return items.Length < requiredCapacity ? GrowBuffer(requiredCapacity) : items;
		}

		/// <summary>Resizes the buffer so that it can hold at least the specified number of items</summary>
		/// <param name="requiredCapacity">Minimum capacity requested</param>
		/// <remarks>Doubles the size of the buffer, until it can fit the requested number of items</remarks>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private JsonValue[] GrowBuffer(int requiredCapacity)
		{
			// Double the array size until it fits the expected capacity
			long newCapacity = Math.Max(m_items.Length, DEFAULT_CAPACITY);
			while (newCapacity < requiredCapacity)
			{
				newCapacity <<= 1;
			}

			// After a certain size, we cannot double anymore!
			if (newCapacity > MAX_GROWTH_CAPACITY)
			{
				newCapacity = MAX_GROWTH_CAPACITY;
				if (newCapacity < requiredCapacity)
				{
					throw ThrowHelper.InvalidOperationException("Cannot resize JSON array because it would exceed the maximum allowed size");
				}
			}

			return ResizeBuffer((int) newCapacity);
		}

		/// <summary>Replace the internal buffer with a larger buffer</summary>
		/// <param name="size">New buffer size</param>
		private JsonValue[] ResizeBuffer(int size)
		{
			JsonValue[] tmp;
			if (size > 0)
			{
				tmp = new JsonValue[size];
				int currentSize = m_size;
				if (currentSize > 0)
				{ // copy existing items
					m_items.AsSpan(0, currentSize).CopyTo(tmp);
				}
			}
			else
			{
				tmp = [ ];
			}
			m_items = tmp;
			return tmp;
		}

		/// <summary>Returns a span of all items in this array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Span<JsonValue> AsSpan() => m_items.AsSpan(0, m_size);

		/// <summary>Returns a memory of all items in this array</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Memory<JsonValue> AsMemory() => m_items.AsMemory(0, m_size);

		/// <summary>Sets the count of the array and returns a span on all the items</summary>
		/// <param name="count">Number of items that will be written to this array</param>
		/// <param name="skipInit">If <see langword="false"/> (default), pre-fill the span with <see cref="JsonNull.Null"/>, if <see langword="true"/> the caller <b>MUST</b> fill the span with non-null entries or risk breaking the array!</param>
		/// <returns>Span that can be used to fill this array.</returns>
		internal Span<JsonValue> GetSpanAndSetCount([Positive] int count, bool skipInit = false)
		{
			Contract.Positive(count);

			int size = m_size;
			if (count < size)
			{
				throw new InvalidOperationException("Cannot reduce the size of an array");
			}

			if (m_items.Length < count)
			{ // set the buffer size to the exact amount
				ResizeBuffer(count);
			}

			if (!skipInit && count > size)
			{ // fill the items 
				m_items.AsSpan(size, count - size).Fill(JsonNull.Null);
			}

			m_size = count;
			return m_items.AsSpan(0, count);
		}

		/// <summary>Trim the size of the internal buffer to reduce memory consumption.</summary>
		/// <remarks>
		/// <para>Should only be used to reduce the wasted internal space if the JSON array is expected to be kept alive for a long duration</para>
		/// </remarks>
		public void TrimExcess()
		{
			// Only trim if we are wasting more than 10% of the internal buffer space
			int threshold = (int) (m_items.Length * 0.9);
			if (m_size < threshold)
			{
				this.Capacity = m_size;
			}
		}

		/// <inheritdoc cref="JsonValue.this[int]"/>
		[AllowNull] // only for the setter
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TryGetValue(index, out var value) ? value : JsonNull.Error;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(index, value);
		}

		/// <inheritdoc cref="JsonValue.this[Index]"/>
		[AllowNull] // only for the setter
		public override JsonValue this[Index index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TryGetValue(index, out var value) ? value : JsonNull.Error;
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(index, value);
		}

		/// <inheritdoc cref="JsonValue.this[JsonPathSegment]"/>
		[AllowNull]
		public override JsonValue this[JsonPathSegment segment]
		{
			[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => segment.TryGetName(out var name) ? GetValueOrDefault(name) : segment.TryGetIndex(out var index) ? GetValueOrDefault(index) : this;
			[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
			set
			{
				if (segment.TryGetIndex(out var index))
				{
					Set(index, value);
				}
				else
				{
					throw (m_readOnly ? FailCannotMutateReadOnlyValue(this) : ThrowHelper.InvalidOperationException($"Cannot set value of a field on a JSON {this.Type}"));
				}
			}
		}

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Add(JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			// invariant: adding 'null' will add JsonNull.Null
			int size = m_size;
			var items = m_items;
			if ((uint) size >= (uint) items.Length)
			{
				items = GrowBuffer(size + 1);
			}
			items[size] = value ?? JsonNull.Null;
			m_size = size + 1;
		}

		/// <summary>Add a <see cref="JsonNull.Null"/> item to the array</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddNull()
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Add(JsonNull.Null);
		}

		/// <summary>Adds a value of type <typeparamref name="TValue"/> item to the array</summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void AddValue<TValue>(TValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Add(FromValue<TValue>(value));
		}

		#region AddRange...

		#region AddRange [JsonValue] ...

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(ReadOnlySpan<JsonValue?> values)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (values.Length > 0)
			{
				int size = m_size;
				int newSize = checked(size + values.Length);
				var tail = EnsureCapacity(newSize).AsSpan(size, values.Length);

				values.CopyTo(tail!);
				FillNullValues(tail!);
				m_size = newSize;
			}
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TSource>(ReadOnlySpan<TSource> values, Func<TSource, JsonValue?> selector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (values.Length > 0)
			{
				int size = m_size;
				int newSize = checked(size + values.Length);
				var tail = EnsureCapacity(newSize).AsSpan(size, values.Length);

				for (int i = 0; i < values.Length; i++)
				{
					tail[i] = selector(values[i]) ?? JsonNull.Null;
				}
				m_size = newSize;
			}
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly(ReadOnlySpan<JsonValue?> values)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (values.Length > 0)
			{
				// resize if needed
				int size = m_size;
				int newSize = checked(size + values.Length);
				var tail = EnsureCapacity(newSize).AsSpan(size, values.Length);

				// append
				for (int i = 0; i < tail.Length; i++)
				{
					tail[i] = (values[i] ?? JsonNull.Null).ToReadOnly();
				}
				m_size = newSize;
			}
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly<TSource>(ReadOnlySpan<TSource> values, Func<TSource, JsonValue?> selector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (values.Length > 0)
			{
				// resize if needed
				int size = m_size;
				int newSize = checked(size + values.Length);
				var tail = EnsureCapacity(newSize).AsSpan(size, values.Length);

				// append
				for (int i = 0; i < tail.Length; i++)
				{
					tail[i] = (selector(values[i]) ?? JsonNull.Null).ToReadOnly();
				}
				m_size = newSize;
			}
			return this;
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange(JsonValue[] values)
		{
			Contract.NotNull(values);
			return AddRange(new ReadOnlySpan<JsonValue?>(values));
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TSource>(TSource[] values, Func<TSource, JsonValue?> selector)
		{
			Contract.NotNull(values);
			return AddRange(new ReadOnlySpan<TSource>(values), selector);
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly(JsonValue[] values)
		{
			Contract.NotNull(values);
			return AddRangeReadOnly(new ReadOnlySpan<JsonValue?>(values));
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="items"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRangeReadOnly<TSource>(TSource[] items, Func<TSource, JsonValue?> selector)
		{
			Contract.NotNull(items);
			return AddRangeReadOnly(new ReadOnlySpan<TSource>(items), selector);
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		[OverloadResolutionPriority(-1)]
		public JsonArray AddRange(IEnumerable<JsonValue?> values)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(values);

			// JsonArray
			if (values is JsonArray jArr)
			{ // optimized
				return AddRange(jArr.GetSpan());
			}

			// Regular Array
			if (values.TryGetSpan(out var span))
			{ // optimized
				return AddRange(span);
			}

			if (values.TryGetNonEnumeratedCount(out var count))
			{ // we can pre-allocate to the new size and copy into tail of the buffer

				int newSize = checked(m_size + count);
				var tail = EnsureCapacity(newSize).AsSpan(m_size, count);

				// append to the tail
				int i = 0;
				foreach (var item in values)
				{
					tail[i++] = item ?? JsonNull.Null;
				}
				Contract.Debug.Assert(i == count);
				m_size = newSize;
			}
			else
			{ // we don't know the size in advance, we may need to resize multiple times
				foreach (var item in values)
				{
					Add(item ?? JsonNull.Null);
				}
			}

			return this;
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TSource>(IEnumerable<TSource> values, Func<TSource, JsonValue?> selector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(values);

			if (values.TryGetSpan(out var span))
			{ // optimized
				return AddRange(span, selector);
			}

			if (values.TryGetNonEnumeratedCount(out var count))
			{ // we can pre-allocate to the new size and copy into tail of the buffer

				int newSize = checked(m_size + count);
				var tail = EnsureCapacity(newSize).AsSpan(m_size, count);

				// append to the tail
				int i = 0;
				foreach (var item in values)
				{
					tail[i++] = selector(item) ?? JsonNull.Null;
				}
				Contract.Debug.Assert(i == count);
				m_size = newSize;
			}
			else
			{ // we don't know the size in advance, we may need to resize multiple times
				foreach (var item in values)
				{
					Add(selector(item) ?? JsonNull.Null);
				}
			}

			return this;
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddRange<TKey, TValue>(IDictionary<TKey, TValue> values, Func<TKey, TValue, JsonValue?> selector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(values);

			int count = values.Count;
			int newSize = checked(m_size + count);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, count);

			// append to the tail
			int i = 0;
			foreach (var item in values)
			{
				tail[i++] = selector(item.Key, item.Value) ?? JsonNull.Null;
			}
			Contract.Debug.Assert(i == count);
			m_size = newSize;

			return this;
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		[OverloadResolutionPriority(-1)]
		public JsonArray AddRangeReadOnly(IEnumerable<JsonValue?> values)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(values);

			// JsonArray
			if (values is JsonArray jArr)
			{ // optimized
				return AddRangeReadOnly(jArr.GetSpan());
			}

			if (values.TryGetSpan(out var span))
			{ // optimized
				return AddRangeReadOnly(span);
			}

			if (values.TryGetNonEnumeratedCount(out var count))
			{
				// pre-resize to the new capacity
				int newSize = checked(m_size + count);
				var tail = EnsureCapacity(newSize).AsSpan(m_size, count);

				// append to the tail
				int i = 0;
				foreach (var item in values)
				{
					tail[i++] = (item ?? JsonNull.Null).ToReadOnly();
				}
				Contract.Debug.Assert(i == count);
				m_size = newSize;
			}
			else
			{
				// may trigger multiple resizes!
				foreach (var item in values)
				{
					Add((item ?? JsonNull.Null).ToReadOnly());
				}
			}

			return this;
		}

		#endregion

		#region AddValues [of T] ...

		#region Mutable...

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(this.Count + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < items.Length; i++)
				{
					tail[i] = FromValue<TValue>(items[i]); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(TValue).IsAssignableTo(typeof(JsonValue)))
			{
				var json = MemoryMarshal.CreateReadOnlySpan<JsonValue?>(ref Unsafe.As<TValue, JsonValue?>(ref MemoryMarshal.GetReference(items)), items.Length);
				return AddRange(json);
			}

			if (typeof(TValue).IsAssignableTo(typeof(IJsonPackable)))
			{
				settings ??= CrystalJsonSettings.Json;
				resolver ??= CrystalJson.DefaultResolver;
				if (default(TValue) is null)
				{
					for (int i = 0; i < items.Length; i++)
					{
						tail[i] = ((IJsonPackable?) items[i])?.JsonPack(settings, resolver) ?? JsonNull.Null;
					}
				}
				else
				{
					for (int i = 0; i < items.Length; i++)
					{
						tail[i] = ((IJsonPackable) items[i]!).JsonPack(settings, resolver);
					}
				}
			}
			else
			{
				var dom = CrystalJsonDomWriter.Create(settings, resolver);
				var context = new CrystalJsonDomWriter.VisitingContext();
				var type = typeof(TValue);
				for(int i = 0; i < items.Length; i++)
				{
					tail[i] = dom.ParseObjectInternal(ref context, items[i], type, null);
				}
			}

			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(TValue[] items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValues<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TValue>(IEnumerable<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			if (items.TryGetSpan(out var span))
			{ // fast path for arrays
				return AddValues<TValue>(span, settings, resolver);
			}

			if (items is IEnumerable<JsonValue?> json)
			{ // fast path if already JSON values
				return AddRange(json);
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{ // pre-allocated to the new size 
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version of As<T> for these as well!
				foreach (var item in items)
				{
					Add(FromValue<TValue>(item));
				}
				return this;
			}

#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var value in items)
			{
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Transforms the elements of a read-only span into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input read-only span of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(ReadOnlySpan<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(transform);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < tail.Length; i++)
				{
					tail[i] = FromValue(transform(items[i])); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for(int i = 0; i < tail.Length; i++)
			{
				tail[i] = dom.ParseObjectInternal(ref context, transform(items[i]), type, null);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Transforms the elements of an array into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input array of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(TSource[] items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValues(new ReadOnlySpan<TSource>(items), transform, settings, resolver);
		}

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input sequence of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues<TSource, TValue>(IEnumerable<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);
			Contract.NotNull(transform);

			if (items.TryGetSpan(out var span))
			{
				return AddValues(span, transform, settings, resolver);
			}

			// pre-allocate if we know the final size
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			   )
			{
				// we shoud have a JIT optimized version of FromValue<T> for these as well
				foreach (var item in items)
				{
					Add(FromValue(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var item in items)
			{
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<string?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonString.Return(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<int> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonNumber.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<long> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonNumber.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<float> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonNumber.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<double> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonNumber.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<Guid> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonString.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValues(ReadOnlySpan<Uuid128> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(m_size + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			for(int i = 0; i < items.Length; i++)
			{
				tail[i] = JsonString.Create(items[i]);
			}
			m_size = newSize;
			return this;
		}

		#endregion

		#region Immutable...

		/// <summary>Appends all the elements of a read-only span to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(ReadOnlySpan<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			// pre-allocate the backing array
			int newSize = checked(this.Count + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T> for these as well
				for(int i = 0; i < items.Length; i++)
				{
					tail[i] = FromValue<TValue>(items[i]); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(TValue) == typeof(JsonValue))
			{
				// force cast to a ReadOnlySpan<JsonValue>
				var json = MemoryMarshal.CreateReadOnlySpan<JsonValue?>(ref Unsafe.As<TValue, JsonValue?>(ref MemoryMarshal.GetReference(items)), items.Length);
				// then add these values directly
				return AddRangeReadOnly(json);
			}

			var dom = CrystalJsonDomWriter.Create(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for (int i = 0; i < items.Length; i++)
			{
				tail[i] = dom.ParseObjectInternal(ref context, items[i], type, null);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(TValue[] items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly<TValue>(new ReadOnlySpan<TValue>(items), settings, resolver);
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TValue>(IEnumerable<TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			if (items.TryGetSpan(out var span))
			{ // fast path for arrays, lists, ...
				return AddValuesReadOnly<TValue>(span, settings, resolver);
			}

			if (items is IEnumerable<JsonValue?> json)
			{ // fast path if already JSON values
				return AddRangeReadOnly(json);
			}

			if (items.TryGetNonEnumeratedCount(out var count))
			{ // pre-allocated to the new size 
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version of As<T> for these as well!
				foreach (var item in items)
				{
					Add(FromValue<TValue>(item));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var value in items)
			{
				Add(dom.ParseObjectInternal(ref context, value, type, null));
			}
			return this;
		}

		/// <summary>Transforms the elements of a read-only span into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input read-only span of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(ReadOnlySpan<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(transform);
			if (items.Length == 0) return this;

			int newSize = checked(this.Count + items.Length);
			var tail = EnsureCapacity(newSize).AsSpan(m_size, items.Length);

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// use the JIT optimized version of FromValue<T>
				for(int i = 0; i < tail.Length; i++)
				{
					tail[i] = FromValue(transform(items[i])); // this should be inlined
				}
				m_size = newSize;
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings ?? CrystalJsonSettings.JsonReadOnly, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			for(int i = 0; i < tail.Length; i++)
			{
				tail[i] = dom.ParseObjectInternal(ref context, transform(items[i]), type, null);
			}
			m_size = newSize;
			return this;
		}

		/// <summary>Transforms the elements of an array into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input array of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(TSource[] items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly(new ReadOnlySpan<TSource>(items), transform, settings, resolver);
		}

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <typeparam name="TSource">Type of the elements of <paramref name="items"/></typeparam>
		/// <typeparam name="TValue">Type of the transformed elements</typeparam>
		/// <param name="items">Input sequence of elements to transform</param>
		/// <param name="transform">Transformation applied to each input element</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonArray AddValuesReadOnly<TSource, TValue>(IEnumerable<TSource> items, Func<TSource, TValue> transform, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);
			Contract.NotNull(transform);

			if (items.TryGetSpan(out var span))
			{
				return AddValuesReadOnly(span, transform, settings, resolver);
			}

			// there is a higher chance that items are a list/array/collection (unless there is a Where(..) before)
			// so it's worth trying to pre-allocate the array
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we shoud have a JIT optimized version of FromValue<T> for these as well
				foreach (var item in items)
				{
					Add(FromValue<TValue>(transform(item)));
				}
				return this;
			}
#endif
			#endregion </JIT_HACK>

			var dom = CrystalJsonDomWriter.CreateReadOnly(settings, resolver);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var type = typeof(TValue);
			foreach (var item in items)
			{
				Add(dom.ParseObjectInternal(ref context, transform(item), type, null));
			}
			return this;
		}

		#endregion

		#endregion

		#region AddRange [object] (boxed) ...

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		internal JsonArray AddRangeBoxed(IEnumerable<object?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			// If this is already a collection of JsonValue, there is no need to convert
			if (items is IEnumerable<JsonValue?> values)
			{
				AddRange(values);
				return this;
			}

			settings ??= CrystalJsonSettings.Json;
			resolver ??= CrystalJson.DefaultResolver;

			// Pre-allocate if we know the size in advance
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			foreach (var value in items)
			{
				Add(FromValue(value, settings, resolver));
			}

			return this;
		}

		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		internal JsonArray AddRangeBoxedReadOnly(IEnumerable<object?> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			// If this is already a collection of JsonValue, there is no need to convert
			if (items is IEnumerable<JsonValue?> values)
			{
				AddRangeReadOnly(values);
				return this;
			}

			settings = (settings ?? CrystalJsonSettings.Json).AsReadOnly();
			resolver ??= CrystalJson.DefaultResolver;

			// Pre-allocate if we know the size in advance
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				EnsureCapacity(checked(this.Count + count));
			}

			foreach (var value in items)
			{
				Add(FromValue(value, settings, resolver));
			}

			return this;
		}

		#endregion

		#endregion

		/// <summary>Clears the content of the array</summary>
		/// <remarks>
		/// <para>Keeps the internal buffer, unless its capacity is greater than 1024 items</para>
		/// <para>To always release the buffer, you should call <see cref="TrimExcess()"/> after <see cref="Clear()"/></para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">If the array is read-only</exception>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void Clear()
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			int size = m_size;
			if (size is > 0 and <= JsonArray.MAX_KEEP_CAPACITY)
			{ // reuse the buffer
				m_items.AsSpan(0, size).Clear();
			}
			else
			{ // drop the buffer
				m_items = [ ];
			}
			m_size = 0;
		}

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValue(int index) => m_items.AsSpan(0, m_size)[index].RequiredIndex(index);

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValue(Index index) => m_items.AsSpan(0, m_size)[index].RequiredIndex(index);

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public override bool TryGetValue(int index, [MaybeNullWhen(false)] out JsonValue value)
		{
			if ((uint) index < m_size)
			{
				value = m_items[index];
				return true;
			}
			value = null;
			return false;
		}

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public override bool TryGetValue(Index index, [MaybeNullWhen(false)] out JsonValue value)
		{
			var offset = index.GetOffset(m_size);
			if ((uint) offset < m_size)
			{
				value = m_items[offset];
				return true;
			}
			value = null;
			return false;
		}

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(int index, JsonValue? defaultValue = null)
		{
			if ((uint) index >= m_size)
			{
				return defaultValue ?? JsonNull.Error;
			}
			var child = m_items[index];
			return child is not (null or JsonNull) ? child : defaultValue ?? JsonNull.Null;
		}

		/// <inheritdoc />
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(Index index, JsonValue? defaultValue = null)
		{
			var offset = index.GetOffset(m_size);
			if ((uint) offset >= m_size)
			{
				return defaultValue ?? JsonNull.Error;
			}
			var child = m_items[offset];
			return child is not (null or JsonNull) ? child : defaultValue ?? JsonNull.Null;
		}

		/// <summary>Sets the value of the item at the specified index</summary>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Set(int index, JsonValue? item)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (index < 0) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			if (index < m_size)
			{
				m_items[index] = item ?? JsonNull.Null;
			}
			else
			{
				SetAfterResize(index, item);
			}
		}

		/// <summary>Sets the value of the item at the specified index</summary>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Set(Index index, JsonValue? item)
		{
			Set(index.GetOffset(m_size), item);
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void SetAfterResize(int index, JsonValue? item)
		{
			var size = m_size;
			Contract.Debug.Requires(index >= size);
			var items = EnsureCapacity(checked(index + 1));
			int gap = index - size;
			if (gap > 0) m_items.AsSpan(size, gap).Fill(JsonNull.Null);
			items[index] = item ?? JsonNull.Null;
			m_size = index + 1;
		}

		/// <summary>Inserts an item to the <see cref="JsonArray">JSON Array</see> at the specified index.</summary>
		/// <param name="index">The zero-based index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The object to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Insert(int index, JsonValue? item)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			var size = m_size;
			if ((uint) index > size) ThrowHelper.ThrowArgumentOutOfRangeException(nameof(index));

			var items = m_items;
			if (m_size >= items.Length)
			{
				items = GrowBuffer(checked(size + 1));
			}

			if (index < size)
			{
				items.AsSpan(index, size - index).CopyTo(items.AsSpan(index + 1));
			}

			items[index] = item ?? JsonNull.Null;
			m_size = size + 1;
		}

		/// <summary>Inserts an item to the <see cref="JsonArray">JSON Array</see> at the specified index.</summary>
		/// <param name="index">The index at which <paramref name="item" /> should be inserted.</param>
		/// <param name="item">The object to insert into the array.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public void Insert(Index index, JsonValue? item)
		{
			Insert(index.GetOffset(m_size), item);
		}

		/// <summary>Removes the first occurrence of a specific value from the <see cref="JsonArray">JSON Array</see>.</summary>
		/// <param name="item">The value to remove from the array.</param>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		/// <returns><see langword="true" /> if <paramref name="item" /> was successfully removed from the array; otherwise, <see langword="false" />. This method also returns <see langword="false" /> if <paramref name="item" /> is not found in the original array.</returns>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public bool Remove(JsonValue item)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			int index = IndexOf(item);
			if (index >= 0)
			{
				RemoveAt(index);
				return true;
			}
			return false;
		}

		/// <summary>Set the new size of the array, adding or removing elements if necessary</summary>
		/// <param name="size">New size of the array</param>
		/// <param name="padding">Value used to fill the array, if it needs to be enlarged</param>
		/// <returns>The same instance</returns>
		/// <remarks>
		/// <para>If the array was already the correct size, there is no changes to the array.</para>
		/// <para>If the array was smaller, new padding elements are added until the length is equal to <paramref name="size"/></para>
		/// <para>If the array was larger, all the extra elements are removed (<paramref name="padding"/> is ignored in this case)</para>
		/// </remarks>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		/// <exception cref="ArgumentOutOfRangeException"> <paramref name="size"/> is negative</exception>
		public JsonArray Truncate(int size, JsonValue? padding = null)
		{
			Contract.Positive(size);
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (size == 0)
			{ // clear the buffer
				Clear();
			}
			else if (size < m_size)
			{ // remove elements from the end
				m_items.AsSpan(size).Clear();
				m_size = size;
			}
			else if (size > m_size)
			{ // enlarge the array and fill with the padding values
				if (size > m_items.Length)
				{ // current buffer is too small
					ResizeBuffer(size);
				}
				// fill the tail with the padding value
				m_items.AsSpan(m_size, size - m_size).Fill(padding ?? JsonNull.Null);
				m_size = size;
			}
			return this;
		}

		/// <summary>Removes the item at the specified index.</summary>
		/// <param name="index">The zero-based index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveAt(int index)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			var size = m_size;
			if ((uint) index >= size) ThrowHelper.ThrowArgumentOutOfRangeException();

			--size;
			var items = m_items;
			if (index < size)
			{
				items.AsSpan(index + 1, size - index).CopyTo(items.AsSpan(index));
			}
		
			items[size] = null!; // clear the reference to prevent any GC leak!
			m_size = size;
		}

		/// <summary>Removes the item at the specified index.</summary>
		/// <param name="index">The index of the item to remove.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is not a valid index in the array.</exception>
		/// <exception cref="T:System.InvalidOperationException">The array is read-only.</exception>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveAt(Index index)
		{
			RemoveAt(index.GetOffset(m_size));
		}

		/// <summary>Swap two items in this <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="first">Index of the first item</param>
		/// <param name="second">Index of the second item</param>
		/// <exception cref="ArgumentOutOfRangeException">If any of <paramref name="first"/> or <paramref name="second"/> is outside the bounds of the array</exception>
		/// <example><c>[ 1, 2, 3, 4 ].Swap(1, 2) === [ 1, 3, 2, 4 ]</c></example>
		public void Swap(int first, int second)
		{
			if ((uint) first >= m_size) throw new ArgumentOutOfRangeException(nameof(first), first, "The first index is outside the bounds of the array");
			if ((uint) second >= m_size) throw new ArgumentOutOfRangeException(nameof(second), second, "The second index is outside the bounds of the array");

			if (first != second)
			{ // swap both items
				(m_items[second], m_items[first]) = (m_items[first], m_items[second]);
			}
		}

		/// <summary>Swap two items in this <see cref="JsonArray">JSON Array</see></summary>
		/// <param name="first">Index of the first item</param>
		/// <param name="second">Index of the second item</param>
		/// <exception cref="ArgumentOutOfRangeException">If any of <paramref name="first"/> or <paramref name="second"/> is outside the bounds of the array</exception>
		/// <example><c>[ 1, 2, 3, 4 ].Swap(^2, ^1) === [ 1, 2, 4, 3 ]</c></example>
		public void Swap(Index first, Index second) => Swap(first.GetOffset(m_size), second.GetOffset(m_size));

		/// <summary>Copies the contents of this <see cref="JsonArray">JSON Array</see> into a destination <see cref="T:System.Span`1" />.</summary>
		/// <param name="destination">The destination <see cref="T:System.Span`1" /> object.</param>
		/// <exception cref="T:System.ArgumentException"><paramref name="destination" /> is shorter than the source <see cref="T:System.Span`1" />.</exception>
		[CollectionAccess(CollectionAccessType.Read)]
		public void CopyTo(Span<JsonValue> destination) => this.AsSpan().CopyTo(destination);

		/// <summary>Attempts to copy the contents of this <see cref="JsonArray">JSON Array</see> to a destination <see cref="T:System.Span`1" /> and returns a value that indicates whether the copy operation succeeded.</summary>
		/// <param name="destination">The target of the copy operation.</param>
		/// <returns> <see langword="true" /> if the copy operation succeeded; otherwise, <see langword="false" />.</returns>
		[CollectionAccess(CollectionAccessType.Read)]
		public bool TryCopyTo(Span<JsonValue> destination) => this.AsSpan().TryCopyTo(destination);

		/// <inheritdoc />
		[CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void CopyTo(JsonValue[] array, int arrayIndex) => this.AsSpan().CopyTo(array.AsSpan(arrayIndex));

		#endregion

		#region Operators...

		/// <summary>Determines the index of a specific value in the <see cref="JsonArray">JSON Array</see>.</summary>
		/// <param name="item">The value to locate in the array.</param>
		/// <returns>The index of <paramref name="item" /> if found in the array; otherwise, <see langword="-1"/>.</returns>
		/// <remarks>If <paramref name="item"/> is <see langword="null"/>, it will match any null or missing entries. If it is any of <see cref="JsonNull.Null"/>, <see cref="JsonNull.Missing"/> or <see cref="JsonNull.Error"/>, it will only match the same singletons.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public int IndexOf(JsonValue? item)
		{
			return item is not null
				?  this.AsSpan().IndexOf(item)
				: IndexOfNullLike(this.AsSpan());

			static int IndexOfNullLike(ReadOnlySpan<JsonValue> items)
			{
				int i = 0;
				foreach (var item in items)
				{
					if (item.IsNullOrMissing())
					{
						return i;
					}
					++i;
				}

				return -1;
			}
		}

		/// <summary>Determines whether the <see cref="JsonArray">JSON Array</see> contains a specific JSON value.</summary>
		/// <param name="item">The value to locate in the array.</param>
		/// <returns> <see langword="true" /> if <paramref name="item" /> is found in the array; otherwise, <see langword="false" />.</returns>
		/// <remarks>If <paramref name="item"/> is <see langword="null"/>, it will match any null or missing entries. If it is any of <see cref="JsonNull.Null"/>, <see cref="JsonNull.Missing"/> or <see cref="JsonNull.Error"/>, it will only match the same singletons.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override bool Contains(JsonValue? item)
		{
			// if item is null, we want _any_ types of null (Null, Missing, Error)
			return item is not null
				? this.AsSpan().Contains(item)
				: ContainsNullLike(this.AsSpan());

			static bool ContainsNullLike(ReadOnlySpan<JsonValue> items)
			{
				foreach(var x in items)
				{
					if (x.IsNullOrMissing())
					{
						return true;
					}
				}
				return false;
			}
		}

		/// <summary>Finds and returns the first element in the array that matches the given predicate</summary>
		/// <param name="predicate">Predicate that will evaluate all the elements in the array, in order, until it returns either <see langword="true"/>, or there are no more elements</param>
		/// <returns>First element where <paramref name="predicate"/> returned <see langword="true"/>, or <see langword="null"/> if the array is empty, or no element was matched.</returns>
		public JsonValue? Find(Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			foreach (var item in AsSpan())
			{
				if (predicate(item))
				{
					return item;
				}
			}

			return null;
		}

		/// <summary>Finds and returns the first element in the array that matches the given predicate</summary>
		/// <param name="predicate">Predicate that will evaluate all the elements in the array, in order, until it returns either <see langword="true"/>, or there are no more elements</param>
		/// <param name="value">Receives the first element that matched</param>
		/// <returns> <see langword="true"/> if <paramref name="predicate"/> matched an element, or <see langword="false"/> if the array is empty, or no element was matched.</returns>
		public bool TryFind(Func<JsonValue, bool> predicate, [MaybeNullWhen(false)] out JsonValue value)
		{
			Contract.NotNull(predicate);

			foreach (var item in AsSpan())
			{
				if (predicate(item))
				{
					value = item;
					return true;
				}
			}

			value = null;
			return false;
		}

		/// <summary>Determines whether the elements of specified array appears at the start of the current array.</summary>
		/// <param name="prefix">An array of elements to search for at the start of the current array</param>
		/// <returns><see langword="true"/> all the elements of <paramref name="prefix"/> match the first elements of the current array; otherwise, <see langword="false"/></returns>
		public bool StartsWith(JsonArray? prefix)
		{
			return prefix == null || StartsWith(prefix.AsSpan());
		}

		/// <summary>Determines whether the specified elements appear at the start of the current array.</summary>
		/// <param name="prefix">A span of elements to search for at the start of the current array</param>
		/// <returns><see langword="true"/> all the elements of <paramref name="prefix"/> match the first elements of the current array; otherwise, <see langword="false"/></returns>
		public bool StartsWith(ReadOnlySpan<JsonValue> prefix)
		{
			// null or the empty array are always a prefix of any array
			if (prefix.Length == 0) return true;

			var selfItems = AsSpan();
			if (prefix.Length > selfItems.Length) return false;
			for (int i = 0; i < prefix.Length; i++)
			{
				if (!prefix[i].Equals(selfItems[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Determines whether the elements of specified array appears at the end of the current array.</summary>
		/// <param name="suffix">An array of elements to search for at the end of the current array</param>
		/// <returns><see langword="true"/> all the elements of <paramref name="suffix"/> match the last elements of the current array; otherwise, <see langword="false"/></returns>
		public bool EndsWith(JsonArray? suffix)
		{
			return suffix == null || EndsWith(suffix.AsSpan());
		}

		/// <summary>Determines whether the specified elements appear at the end of the current array.</summary>
		/// <param name="suffix">A span to search for at the end of the current array</param>
		/// <returns><see langword="true"/> all the elements of <paramref name="suffix"/> match the last elements of the current array; otherwise, <see langword="false"/></returns>
		public bool EndsWith(ReadOnlySpan<JsonValue> suffix)
		{
			// null or the empty array are always a prefix of any array
			if (suffix.Length == 0) return true;

			var selfItems = AsSpan();
			if (suffix.Length > selfItems.Length) return false;
			selfItems = selfItems[^suffix.Length..];
			for (int i = 0; i < suffix.Length; i++)
			{
				if (!suffix[i].Equals(selfItems[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Forms a new array out of the given array, beginning at 'start'.</summary>
		/// <param name="start">The zero-based index at which to begin this slice.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="start"/> index is not in range (&lt;0 or &gt;Length).</exception>
		/// <remarks>
		/// <para>The returned array keeps the "readonly-ness" of the original</para>
		/// </remarks>
		public JsonArray Slice(int start)
		{
			// let the runtime perform the bound-checking
			var items = AsSpan()[start..].ToArray();
			return new(items, items.Length, m_readOnly);
		}

		/// <summary>Forms a new array out of the given array, beginning at 'start', of given length</summary>
		/// <param name="start">The zero-based index at which to begin this slice.</param>
		/// <param name="length">The desired length for the slice (exclusive).</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="start"/> or end index is not in range (&lt;0 or &gt;Length).</exception>
		/// <remarks>
		/// <para>The returned array keeps the "readonly-ness" of the original</para>
		/// </remarks>
		public JsonArray Slice(int start, int length)
		{
			// let the runtime perform the bound-checking
			var items = AsSpan().Slice(start, length).ToArray();
			return new(items, items.Length, m_readOnly);
		}

		/// <summary>Forms a new array out of the given array, using a given range</summary>
		/// <param name="range">The range the specifies the elements to copy.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the specified <paramref name="range"/> exceeds the bounds of the array.</exception>
		/// <remarks>
		/// <para>The returned array keeps the "readonly-ness" of the original</para>
		/// </remarks>
		public JsonArray this[Range range]
		{
			get
			{
				// let the runtime perform the bound-checking
				var items = AsSpan()[range].ToArray();
				return new(items, items.Length, m_readOnly);
			}
		}

		/// <summary>Keep only the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that should return <see langword="true"/> for elements to keep, and <see langword="false"/> for elements to discard</param>
		/// <returns>Number of elements that where kept</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int KeepOnly([InstantHandle] Func<JsonValue, bool> predicate)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (predicate(items[i]))
				{
					items[p++] = i;
				}
			}
			Contract.Debug.Ensures(p <= m_size);
			if (p < m_size)
			{
				items.AsSpan(p, m_size - p).Clear();
			}
			m_size = p;
			return p;
		}

		/// <summary>Remove all the elements that match a predicate</summary>
		/// <param name="predicate">Predicate that returns <see langword="true"/> for elements to remove, and <see langword="false"/> for elements to keep</param>
		/// <returns>Number of elements that where removed</returns>
		/// <remarks>The original array is modified</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public int RemoveAll([InstantHandle] Func<JsonValue, bool> predicate)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(predicate);

			// if already empty, nothing much to do
			if (m_size == 0) return 0;

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (!predicate(items[i]))
				{
					items[p++] = i;
				}
			}
			int r = m_size - p;
			Contract.Debug.Assert(r >= 0);
			if (r > 0)
			{
				items.AsSpan(p, r).Clear();
			}
			m_size = p;
			return r;
		}

		/// <summary>Remove duplicate elements from this array</summary>
		/// <remarks>Similar to Distinct(), except that it modifies the original array.</remarks>
		[CollectionAccess(CollectionAccessType.ModifyExistingContent)]
		public void RemoveDuplicates()
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (m_size <= 1)
			{ // no duplicates possible
				return;
			}

			// Two pass algorithm:
			// 1) copy all the elements in a set
			//    - if count == m_size, no duplicates => EXIT
			// 2) for each element:
			//    - if in set, keep it, remove from set
			//    - if not int set, remove it

			var set = new HashSet<JsonValue>(this, EqualityComparer<JsonValue>.Default); //TODO: JsonValueComparer!
			if (set.Count == m_size)
			{ // no duplicates found
				return;
			}

			int p = 0;
			var items = m_items;
			for (int i = 0; i < m_size; i++)
			{
				if (set.Remove(items[i]))
				{ // it was in the set, keep it
					items[p++] = items[i];
				}
				//else: it has already been copied
			}
			Contract.Debug.Assert(p == set.Count);
			if (p < m_size)
			{
				items.AsSpan(p, m_size - p).Clear();
			}
			m_size = p;
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(int index)
		{
			if (index == 0 && m_readOnly)
			{ // return the whole immutable array
				return this;
			}

			// get the corresponding slice
			var tmp = this.AsSpan()[index..];
			if (tmp.Length == 0)
			{ // empty
				return m_readOnly ? JsonArray.ReadOnly.Empty : new();
			}

			// return a new array wrapping these items
			return new(tmp.ToArray(), tmp.Length, m_readOnly);
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(Index index)
		{
			return GetRange(index.GetOffset(m_size));
		}

		/// <summary>Returns a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items in the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(int index, int count)
		{
			if (index == 0 && count == m_size && m_readOnly)
			{ // return the whole immutable array
				return this;
			}

			// get the corresponding slice
			var tmp = this.AsSpan().Slice(index, count);
			if (tmp.Length == 0)
			{ // empty
				return m_readOnly ? JsonArray.ReadOnly.Empty : new JsonArray();
			}

			// return a new array wrapping these items
			return new JsonArray(tmp.ToArray(), tmp.Length, m_readOnly);
		}

		/// <summary>Return a new <see cref="JsonArray">JSON Array</see> with a shallow copy of all the items in the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonArray GetRange(Range range)
		{
			var (index, count) = range.GetOffsetAndLength(m_size);
			return GetRange(index, count);
		}

		/// <summary>Returns a read-only span of all items in this array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan() => this.AsSpan();

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(int start) => this.AsSpan()[start..];

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index for a specified length</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(int start, int length) => this.AsSpan().Slice(start, length);

		/// <summary>Returns a read-only span of the items in this array, for the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlySpan<JsonValue> GetSpan(Range range) => this.AsSpan()[range];

		/// <summary>Returns a read-only span of all items in this array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlyMemory<JsonValue> GetMemory() => this.AsMemory();

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlyMemory<JsonValue> GetMemory(int start) => this.AsMemory()[start..];

		/// <summary>Returns a read-only span of the items in this array, starting from the specified index for a specified length</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlyMemory<JsonValue> GetMemory(int start, int length) => this.AsMemory().Slice(start, length);

		/// <summary>Returns a read-only span of the items in this array, for the specified range</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public ReadOnlyMemory<JsonValue> GetMemory(Range range) => this.AsMemory()[range];

		#endregion

		#region Enumerator...

		/// <summary>Returns an enumerator that iterates through the array.</summary>
		public Enumerator GetEnumerator() => new(m_items, m_size);

		IEnumerator<JsonValue> IEnumerable<JsonValue>.GetEnumerator() => new Enumerator(m_items, m_size);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>Enumerates through the elements of a <see cref="JsonArray"/></summary>
		public struct Enumerator : IEnumerator<JsonValue>
		{
			private readonly JsonValue[] m_items;
			private readonly int m_size;
			private int m_index;
			private JsonValue? m_current;

			internal Enumerator(JsonValue[] items, int size)
			{
				m_items = items;
				m_size = size;
				m_index = 0;
				m_current = null;
			}

			/// <inheritdoc />
			public readonly void Dispose()
			{ }

			/// <inheritdoc />
			public bool MoveNext()
			{
				if ((uint) m_index < m_size)
				{
					m_current = m_items[m_index];
					m_index++;
					return true;
				}
				return MoveNextRare();
			}

			private bool MoveNextRare()
			{
				m_index = m_size + 1;
				m_current = null;
				return false;
			}

			readonly object IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index == m_size + 1)
					{
						ThrowHelper.ThrowInvalidOperationException("Operation cannot happen.");
					}
					return this.Current;
				}
			}

			void IEnumerator.Reset()
			{
				m_index = 0;
				m_current = null;
			}

			/// <inheritdoc />
			public readonly JsonValue Current => m_current!;

		}

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON objects</see></summary>
		/// <remarks>
		/// <para>If the array contains any item that is either null or not an object, and exception will be thrown when iterating!</para>
		/// <para>If null entries are allowed, use <see cref="AsObjectsOrDefault"/> instead</para>
		/// </remarks>
		public JsonArray<JsonObject> AsObjects() => new(this);

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON objects</see> or null entries</summary>
		/// <remarks>
		/// <para>If the array contains any item that is not null and not an object, and exception will be thrown when iterating!</para>
		/// <para>If null entries are not allowed, use <see cref="AsObjects"/> instead</para>
		/// </remarks>
		public JsonArrayOrDefault<JsonObject> AsObjectsOrDefault() => new(this, null);

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON objects</see> or null entries</summary>
		/// <remarks>
		/// <para>If the array contains any item that is not null and not an object, and exception will be thrown when iterating!</para>
		/// <para>If null entries are not allowed, use <see cref="AsObjects"/> instead</para>
		/// </remarks>
		public JsonArrayOrDefault<JsonObject> AsObjectsOrEmpty() => new(this, JsonObject.ReadOnly.Empty);

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON arrays</see></summary>
		/// <remarks>
		/// <para>If the array contains any item that is either null or not an array, and exception will be thrown when iterating!</para>
		/// <para>If null entries are allowed, use <see cref="AsArraysOrDefault"/> instead</para>
		/// </remarks>
		[Pure]
		public JsonArray<JsonArray> AsArrays() => new(this);

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON arrays</see></summary>
		/// <remarks>
		/// <para>If the array contains any item that is either null or not an array, and exception will be thrown when iterating!</para>
		/// <para>If null entries are allowed, use <see cref="AsArraysOrDefault"/> instead</para>
		/// </remarks>
		[Pure]
		public JsonArrayOrDefault<JsonArray> AsArraysOrDefault() => new(this, null);

		/// <summary>Returns a typed view of this <see cref="JsonArray">array</see> that is expected to only contain <see cref="JsonObject">JSON arrays</see></summary>
		/// <remarks>
		/// <para>If the array contains any item that is either null or not an array, and exception will be thrown when iterating!</para>
		/// <para>If null entries are allowed, use <see cref="AsArraysOrDefault"/> instead</para>
		/// </remarks>
		[Pure]
		public JsonArrayOrDefault<JsonArray> AsArraysOrEmpty() => new(this, JsonArray.ReadOnly.Empty);

		/// <summary>Returns a wrapper that will convert all the elements of this <see cref="JsonArray"/> as values of type <typeparamref name="TValue"/> when enumerated.</summary>
		/// <remarks><para>This method can be used to remove the need of allocating a temporary array or list of items that would only be called inside a <see langword="foreach"/> loop, or used with LINQ.</para></remarks>
		public JsonArray<TValue> AsArrayOf<TValue>() where TValue : notnull
		{
			return new(this);
		}

		/// <summary>Returns a wrapper that will convert all the elements of this <see cref="JsonArray"/> as values of type <typeparamref name="TValue"/> when enumerated.</summary>
		/// <remarks><para>This method can be used to remove the need of allocating a temporary array or list of items that would only be called inside a <see langword="foreach"/> loop, or used with LINQ.</para></remarks>
		public JsonArrayOrDefault<TValue> AsArrayOrDefaultOf<TValue>(TValue? missingValue = default)
		{
			return new(this, missingValue);
		}

		/// <summary>Returns a wrapper that will convert all the elements of this <see cref="JsonArray"/> as values of type <typeparamref name="TValue"/> when enumerated.</summary>
		/// <remarks><para>This method can be used to remove the need of allocating a temporary array or list of items that would only be called inside a <see langword="foreach"/> loop, or used with LINQ.</para></remarks>
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use AsArrayOf<TValue> instead.")]
		public JsonArray<TValue> Cast<TValue>() where TValue : notnull
		{
			return new(this);
		}

		/// <summary>Returns a wrapper that will convert all the elements of this <see cref="JsonArray"/> as values of type <typeparamref name="TValue"/> when enumerated.</summary>
		/// <remarks><para>This method can be used to remove the need of allocating a temporary array or list of items that would only be called inside a <see langword="foreach"/> loop, or used with LINQ.</para></remarks>
		public JsonArrayOrDefault<TValue> Cast<TValue>(TValue defaultValue)
		{
			return new(this, defaultValue);
		}

		#endregion

		internal override bool IsSmallValue()
		{
			const int LARGE_ARRAY = 5;

			var items = this.AsSpan();
			if (items.Length >= LARGE_ARRAY) return false;

			foreach(var item in items)
			{
				if (!item.IsSmallValue())
				{
					return false;
				}
			}

			return true;
		}

		internal override bool IsInlinable() => false;

		private string GetMutabilityDebugLiteral() => m_readOnly ? " ReadOnly" : "";

		internal override string GetCompactRepresentation(int depth)
		{
			int size = m_size;
			if (size == 0) return "[ ]"; // empty

			var items = m_items;

			if (depth >= 3 || (depth == 2 && !IsSmallValue()))
			{
				if (size == 1)
				{
					return "[ " + items[0].GetCompactRepresentation(depth + 1) + " ]";
				}

				return GetElementsTypeOrDefault() switch
				{
					JsonType.Number  => size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Numbers */ ]",
					JsonType.Array   => $"[ /* {size:N0} Arrays */ ]",
					JsonType.Object  => $"[ /* {size:N0} Objects */ ]",
					JsonType.Boolean => size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Booleans */ ]",
					JsonType.String  => size == 2 ? "[ " + items[0] + ", " + items[1] + " ]" : $"[ /* {size:N0} Strings */ ]",
					_                => $"[ /* {size:N0} x \u2026 */ ]"
				};
			}

			var sb = new StringBuilder(128);
			// If the size of the array is higher than 5, dump the first 4 items, followed by ", ... X more".
			// Note: If size is 5 , we will dump the fifth item, in order to not end up with ", ... 1 more" that would not be helpful and will frequently be longer than dumping the item anyway...
			++depth;
			sb.Append("[ ").Append(items[0].GetCompactRepresentation(depth));
			if (size >= 2) sb.Append(", ").Append(items[1].GetCompactRepresentation(depth));
			if (size >= 3) sb.Append(", ").Append(items[2].GetCompactRepresentation(depth));
			if (depth == 1)
			{ // we allow up to 4 items
				if (size >= 4) sb.Append(", ").Append(items[3].GetCompactRepresentation(depth));
				if (size == 5) sb.Append(", ").Append(items[4].GetCompactRepresentation(depth));
				else if (size > 5) sb.AppendInvariant($", /* … {size - 4:N0} more */");
			}
			else
			{ // we allow up to 3 items
				if (size == 4) sb.Append(", ").Append(items[3].GetCompactRepresentation(depth));
				else if (size > 4) sb.AppendInvariant($", /* … {size - 3:N0} more */");
			}
			sb.Append(" ]");
			return sb.ToString();
		}

		/// <summary>Converts this <see cref="JsonArray">JSON Array</see> with a <see cref="List{T}">List&lt;object?></see>.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override object ToObject()
		{
			//TODO: detect when all items have the same type T,
			// in which case we could create a List<T> instead of List<object> !

			var list = new List<object?>(this.Count);
			foreach (var value in this.AsSpan())
			{
				list.Add(value.ToObject());
			}
			return list;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			//note: we cannot use JIT optimization here, because the type will usually be an array or list of value types, which itself is not a value type.
			if (resolver is not null && !ReferenceEquals(resolver, CrystalJson.DefaultResolver))
			{
				if (!resolver.TryGetConverterFor(type ?? typeof(object), out var converter))
				{
					throw new NotSupportedException(); //TODO: error message!
				}
				return converter.BindJsonValue(this, resolver);
			}
			return CrystalJson.DefaultResolver.BindJsonArray(type, this);
		}

		/// <summary>Returns an array of <see cref="JsonValue"/> with the same items as this <see cref="JsonArray"/></summary>
		/// <remarks>Return a shallow copy of the array.</remarks>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public JsonValue[] ToArray() => this.AsSpan().ToArray();

		/// <summary>Deserializes this array into an array of <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the deserialized items</typeparam>
		/// <param name="defaultValue">Default value for items that are null or missing</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>Array of deserialized items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public TValue?[] ToArray<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)) return Unsafe.As<TValue?[]>(ToBoolArray((bool) (object) defaultValue!));
			if (typeof(TValue) == typeof(int)) return Unsafe.As<TValue?[]>(ToInt32Array((int) (object) defaultValue!));
			if (typeof(TValue) == typeof(long)) return Unsafe.As<TValue?[]>(ToInt64Array((long) (object) defaultValue!));
			if (typeof(TValue) == typeof(float)) return Unsafe.As<TValue?[]>(ToSingleArray((float) (object) defaultValue!));
			if (typeof(TValue) == typeof(double)) return Unsafe.As<TValue?[]>(ToDoubleArray((double) (object) defaultValue!));
			if (typeof(TValue) == typeof(Guid)) return Unsafe.As<TValue?[]>(ToGuidArray((Guid) (object) defaultValue!));
			if (typeof(TValue) == typeof(Uuid128)) return Unsafe.As<TValue?[]>(ToUuid128Array((Uuid128) (object) defaultValue!));
			if (typeof(TValue) == typeof(DateTime)) return Unsafe.As<TValue?[]>(ToDateTimeArray((DateTime) (object) defaultValue!));
			if (typeof(TValue) == typeof(DateTimeOffset)) return Unsafe.As<TValue?[]>(ToDateTimeOffsetArray((DateTimeOffset) (object) defaultValue!));
			if (typeof(TValue) == typeof(NodaTime.Instant)) return Unsafe.As<TValue?[]>(ToInstantArray((NodaTime.Instant) (object) defaultValue!));

			//TODO: convert more!

			if (typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				// we should have a JIT optimized version for these as well
				return ToPrimitiveArray<TValue>();
			}
#endif
			#endregion </JIT_HACK>

			if (typeof(TValue) == typeof(string))
			{
				return (TValue[]) (object) ToStringArray();
			}

			var items = this.AsSpan();
			if (items.Length == 0)
			{
				return [];
			}

			var result = new TValue?[items.Length];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = items[i].As(defaultValue, resolver);
			}
			return result;
		}

		/// <summary>Deserializes this array into an array of <typeparamref name="TValue"/>, using a custom decoder</summary>
		/// <typeparam name="TValue">Type of the deserialized items</typeparam>
		/// <param name="decoder">Func that is called do decode each element of this array</param>
		/// <returns>Array of deserialized items</returns>
		public TValue[] ToArray<TValue>(Func<JsonValue, TValue> decoder)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];
			var result = new TValue[items.Length];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = decoder(items[i]);
			}
			return result;
		}

		/// <summary>Deserializes this array into an array of <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the deserialized items, which implements <see cref="IJsonDeserializable{TSelf}"/></typeparam>
		/// <returns>Array of deserialized items</returns>
		public TValue[] ToArrayDeserializable<TValue>(ICrystalJsonTypeResolver? resolver = null)
			where TValue : IJsonDeserializable<TValue>
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];

			resolver ??= CrystalJson.DefaultResolver;
			var result = new TValue[items.Length];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = TValue.JsonDeserialize(items[i], resolver);
			}
			return result;
		}

#if !DEBUG // <JIT_HACK>

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private T?[] ToPrimitiveArray<T>(T? defaultValue = default)
		{
			//IMPORTANT! typeof(T) doit être un type primitif reconnu par As<T> via compile time scanning!!!

			var items = this.AsSpan();
			if (items.Length == 0) return [];
			var buf = new T?[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].As(defaultValue);
			}
			return buf;
		}

#endif

		/// <summary>Deserializes a <see cref="JsonArray">JSON Array</see> into an array of <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Types of the items in the deserialized array</typeparam>
		/// <param name="value">JSON value known to be a JSON Array</param>
		/// <param name="defaultValue">Default value if the value is null or missing</param>
		/// <param name="resolver">Optional type resolver</param>
		/// <param name="required">If <see langword="true"/>, all items must be non-null</param>
		/// <returns>Array of <typeparamref name="TValue"/></returns>
		[Pure, ContractAnnotation("required:true => notnull")]
		public static TValue?[]? BindArray<TValue>(JsonValue? value, TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null, bool required = false)
		{
			if (value is not JsonArray array)
			{
				return value is null || value.IsNull
					? (required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<TValue[]>() : null)
					: throw JsonBindingException.CannotBindJsonValueToArrayOfThisType(value, typeof(TValue));
			}

			return array.ToArray<TValue>(defaultValue, resolver);
		}

		/// <summary>Returns the equivalent <see cref="bool"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool[] ToBoolArray(bool defaultValue = false)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new bool[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToBoolean(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="int"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public int[] ToInt32Array(int defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new int[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToInt32(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="uint"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public uint[] ToUInt32Array(uint defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new uint[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToUInt32(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="long"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public long[] ToInt64Array(long defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new long[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToInt64(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="ulong"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ulong[] ToUInt64Array(ulong defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new ulong[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToUInt64(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="float"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public float[] ToSingleArray(float defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new float[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToSingle(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="double"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public double[] ToDoubleArray(double defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new double[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToDouble(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="Half"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Half[] ToHalfArray(Half defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new Half[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToHalf(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="decimal"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public decimal[] ToDecimalArray(decimal defaultValue = 0)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new decimal[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToDecimal(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="Guid"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Guid[] ToGuidArray(Guid defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new Guid[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToGuid(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="Uuid128"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public Uuid128[] ToUuid128Array(Uuid128 defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var buf = new Uuid128[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = items[i].ToUuid128(defaultValue);
			}
			return buf;
		}

		/// <summary>Returns the equivalent <see cref="DateTime"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public DateTime[] ToDateTimeArray(DateTime defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new DateTime[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToDateTime(defaultValue);
			}
			return result;
		}

		/// <summary>Returns the equivalent <see cref="DateTimeOffset"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public DateTimeOffset[] ToDateTimeOffsetArray(DateTimeOffset defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new DateTimeOffset[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToDateTimeOffset(defaultValue);
			}
			return result;
		}

		/// <summary>Returns the equivalent <see cref="Instant"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public NodaTime.Instant[] ToInstantArray(NodaTime.Instant defaultValue = default)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new Instant[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToInstant(defaultValue);
			}
			return result;
		}

		/// <summary>Returns the equivalent <see cref="string"/> array</summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public string?[] ToStringArray(string? defaultValue = null)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [];

			var result = new string?[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				result[i] = items[i].ToStringOrDefault(defaultValue);
			}
			return result;
		}

		/// <summary>Returns a <see cref="List{JsonValue}">List&lt;JsonValue&gt;</see> with the same elements as this array</summary>
		/// <returns>A shallow copy of the original items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<JsonValue> ToList()
		{
			var items = this.AsSpan();
			var res = new List<JsonValue>(items.Length);
#if NET8_0_OR_GREATER
			res.AddRange(items);
#else
			foreach(var item in items)
			{
				res.Add(item);
			}
#endif
			return res;
		}

		/// <summary>Returns a <see cref="List{JsonValue}">List&lt;JsonValue&gt;</see> with the same elements as this array</summary>
		/// <returns>A shallow copy of the original items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<TValue?> ToList<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			#region <JIT_HACK>

			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)
			 || typeof(TValue) == typeof(char)
			 || typeof(TValue) == typeof(byte)
			 || typeof(TValue) == typeof(sbyte)
			 || typeof(TValue) == typeof(short)
			 || typeof(TValue) == typeof(ushort)
			 || typeof(TValue) == typeof(int)
			 || typeof(TValue) == typeof(uint)
			 || typeof(TValue) == typeof(long)
			 || typeof(TValue) == typeof(ulong)
			 || typeof(TValue) == typeof(float)
			 || typeof(TValue) == typeof(double)
			 || typeof(TValue) == typeof(decimal)
			 || typeof(TValue) == typeof(Guid)
			 || typeof(TValue) == typeof(DateTime)
			 || typeof(TValue) == typeof(DateTimeOffset)
			 || typeof(TValue) == typeof(TimeSpan)
			 || typeof(TValue) == typeof(Instant)
			 || typeof(TValue) == typeof(Duration)
			 // nullables!
			 || typeof(TValue) == typeof(bool?)
			 || typeof(TValue) == typeof(char?)
			 || typeof(TValue) == typeof(byte?)
			 || typeof(TValue) == typeof(sbyte?)
			 || typeof(TValue) == typeof(short?)
			 || typeof(TValue) == typeof(ushort?)
			 || typeof(TValue) == typeof(int?)
			 || typeof(TValue) == typeof(uint?)
			 || typeof(TValue) == typeof(long?)
			 || typeof(TValue) == typeof(ulong?)
			 || typeof(TValue) == typeof(float?)
			 || typeof(TValue) == typeof(double?)
			 || typeof(TValue) == typeof(decimal?)
			 || typeof(TValue) == typeof(Guid?)
			 || typeof(TValue) == typeof(DateTime?)
			 || typeof(TValue) == typeof(DateTimeOffset?)
			 || typeof(TValue) == typeof(TimeSpan?)
			 || typeof(TValue) == typeof(Instant?)
			 || typeof(TValue) == typeof(Duration?)
			)
			{
				return ToPrimitiveList<TValue>();
			}
#endif
			#endregion </JIT_HACK>

			return ToListSlow(this, defaultValue, resolver);

			static List<TValue?> ToListSlow(JsonArray self, TValue? defaultValue, ICrystalJsonTypeResolver? resolver)
			{
				var items = self.AsSpan();
#if NET8_0_OR_GREATER
				var list = new List<TValue?>();
				CollectionsMarshal.SetCount(list, items.Length);
				var span = CollectionsMarshal.AsSpan(list);
				for(int i = 0; i < items.Length; i++)
				{
					span[i] = items[i].As(defaultValue, resolver);
				}
				return list;
#else
				var list = new List<TValue?>(items.Length);
				foreach(var item in items)
				{
					list.Add(item.As(defaultValue, resolver));
				}
				return list;
#endif
			}
		}

		/// <summary>Returns a <see cref="List{T}"/> with the transformed elements of this array</summary>
		/// <param name="decoder">Func that is called do decode each element of this array</param>
		/// <returns>A list of all elements that have been converted <paramref name="decoder"/></returns>
		[Pure]
		public List<TValue> ToList<TValue>([InstantHandle] Func<JsonValue, TValue> decoder)
		{
			Contract.NotNull(decoder);
			var items = this.AsSpan();
			var result = new List<TValue>(items.Length);
#if NET8_0_OR_GREATER
			// update the list in-place
			CollectionsMarshal.SetCount(result, m_size);
			var tmp = CollectionsMarshal.AsSpan(result);
			for (int i = 0; i < items.Length; i++)
			{
				tmp[i] = decoder(items[i]);
			}
#else
			foreach(var item in items)
			{
				result.Add(decoder(item));
			}
#endif
			return result;
		}

		/// <summary>Deserializes this array into an array of <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the deserialized items, which implements <see cref="IJsonDeserializable{TSelf}"/></typeparam>
		/// <returns>Array of deserialized items</returns>
		public List<TValue> ToListDeserializable<TValue>(ICrystalJsonTypeResolver? resolver = null)
			where TValue : IJsonDeserializable<TValue>
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];

			var result = new List<TValue>(items.Length);
#if NET8_0_OR_GREATER
			// update the list in-place
			CollectionsMarshal.SetCount(result, m_size);
			var tmp = CollectionsMarshal.AsSpan(result);
			for (int i = 0; i < items.Length; i++)
			{
				tmp[i] = TValue.JsonDeserialize(items[i], resolver);
			}
#else
			foreach(var item in items)
			{
				result.Add(TValue.JsonDeserialize(items[i], resolver));
			}
#endif
			return result;
		}

		/// <summary>Returns a <see cref="HashSet{JsonValue}"/> with the same elements as this array</summary>
		/// <param name="comparer">Comparer used by the set. If <c>null</c>, uses the <see cref="JsonValueComparer.Strict"/> value comparer.</param>
		/// <returns>A shallow copy of the original items</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public HashSet<JsonValue> ToHashSet(IEqualityComparer<JsonValue>? comparer = null)
		{
			var items = this.AsSpan();
			var res = new HashSet<JsonValue>(items.Length, comparer ?? JsonValueComparer.Strict);
			foreach(var item in items)
			{
				res.Add(item);
			}
			return res;
		}

		/// <summary>Returns a <see cref="HashSet{TValue}"/> with the same elements as this array</summary>
		/// <param name="defaultValue">Default value of <c>null</c> items</param>
		/// <param name="comparer">Comparer used by the set</param>
		/// <param name="resolver">Optional type resolver</param>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public HashSet<TValue?> ToHashSet<TValue>(TValue? defaultValue = default, IEqualityComparer<TValue?>? comparer = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var items = this.AsSpan();
			var res = new HashSet<TValue?>(items.Length, comparer);
			foreach(var item in items)
			{
				res.Add(item.As(defaultValue, resolver));
			}
			return res;
		}

		/// <summary>Returns a <see cref="HashSet{TValue}"/> with the transformed elements of this array</summary>
		/// <param name="decoder">Func that is called do decode each element of this array</param>
		/// <param name="comparer">Comparer used by the set</param>
		/// <returns>A list of all elements that have been converted <paramref name="decoder"/></returns>
		[Pure]
		public HashSet<TValue> ToHashSet<TValue>([InstantHandle] Func<JsonValue, TValue> decoder, IEqualityComparer<TValue>? comparer = null)
		{
			Contract.NotNull(decoder);
			var items = this.AsSpan();
			var result = new HashSet<TValue>(items.Length, comparer);
			foreach(var item in items)
			{
				result.Add(decoder(item));
			}
			return result;
		}

		/// <summary>Converts this <see cref="JsonArray">JSON Array</see> so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (string, boolean, number, ...) that are always immutable.</para>
		/// </remarks>
		public override JsonArray ToMutable()
		{
			if (m_readOnly)
			{ // create a mutable copy
				return Copy();
			}

			// the top-level is mutable, but maybe it has read-only children?
			var items = this.GetSpan();
			JsonValue[]? copy = null;
			for(int i = 0; i < items.Length; i++)
			{
				var child = items[i];
				if (child is (JsonObject or JsonArray) && child.IsReadOnly)
				{
					copy ??= items.ToArray();
					copy[i] = child.Copy();
				}
			}

			if (copy is null)
			{ // already mutable
				return this;
			}

			return new(copy, items.Length, readOnly: false);
		}

		/// <summary>Deserializes a <see cref="JsonArray">JSON Array</see> into a list of objects of the specified type</summary>
		/// <typeparam name="TValue">Type of the elements in the list</typeparam>
		/// <param name="value"><see cref="JsonArray">JSON Array</see> that contains the elements to bind</param>
		/// <param name="resolver">Optional type resolver</param>
		/// <param name="required">If <see langword="true"/> the array cannot be null</param>
		/// <returns>A list of all elements that have been deserialized into instance of type <typeparamref name="TValue"/></returns>
		[Pure, ContractAnnotation("value:null => null")]
		public static List<TValue?>? BindList<TValue>(JsonValue? value, ICrystalJsonTypeResolver? resolver = null, bool required = false)
		{
			if (value is null || value.IsNull) return required ? JsonValueExtensions.FailRequiredValueIsNullOrMissing<List<TValue?>>() : null;
			if (value is not JsonArray array) throw JsonBindingException.CannotBindJsonValueToArrayOfThisType(value, typeof(TValue));
			return array.ToList<TValue?>(default, resolver);
		}

#if NET8_0_OR_GREATER

		/// <summary>Returns the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		public TNumber Sum<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>
		{
			var total = TNumber.Zero;
			foreach (var item in this.AsSpan())
			{
				total += item.As<TNumber>(TNumber.Zero);
			}
			return total;
		}

		/// <summary>Returns the average of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		public TNumber Average<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>
		{
			return Sum<TNumber>() / TNumber.CreateChecked(this.Count);
		}

		/// <summary>Returns the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		public TNumber Min<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>, System.Numerics.IComparisonOperators<TNumber, TNumber, bool>
		{
			var span = this.AsSpan();
			if (span.Length <= 0)
			{
				throw ThrowHelper.InvalidOperationNoElements();
			}

			var min = span[0].As<TNumber>(TNumber.Zero);
			for (int i = 1; i < span.Length; i++)
			{
				var x = span[i].As<TNumber>(TNumber.Zero);
				if (x < min)
				{
					min = x;
				}
			}
			return min;
		}

		/// <summary>Returns the sum of all the items in <see cref="JsonArray">JSON Array</see> interpreted as the corresponding <typeparamref name="TNumber"/></summary>
		public TNumber Max<TNumber>() where TNumber : System.Numerics.INumberBase<TNumber>, System.Numerics.IComparisonOperators<TNumber, TNumber, bool>
		{
			var span = this.AsSpan();
			if (span.Length <= 0)
			{
				throw ThrowHelper.InvalidOperationNoElements();
			}

			var max = span[0].As<TNumber>(TNumber.Zero);
			for (int i = 1; i < span.Length; i++)
			{
				var x = span[i].As<TNumber>(TNumber.Zero);
				if (x > max)
				{
					max = x;
				}
			}
			return max;
		}

#endif

#if !DEBUG // <JIT_HACK>

		[Pure, CollectionAccess(CollectionAccessType.Read), UsedImplicitly]
		private List<T?> ToPrimitiveList<T>()
		{
			//IMPORTANT! T must a primitive type that is recognized by As<T> and inline by the JIT!!!
			var items = this.AsSpan();
			var result = new List<T?>(items.Length);
#if NET8_0_OR_GREATER
			if (items.Length > 0)
			{
				// update the list in-place
				CollectionsMarshal.SetCount(result, m_size);
				var tmp = CollectionsMarshal.AsSpan(result);
				Contract.Debug.Assert(tmp.Length == m_size);
				ref var ptr = ref tmp[0];
				foreach (var item in items)
				{
					ptr = item.As<T>();
					ptr = ref Unsafe.Add(ref ptr, 1);
				}
			}
#else
			foreach(var item in items)
			{
				result.Add(item.As<T>());
			}
#endif

			return result;
		}

#endif

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="bool"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<bool> ToBoolList(bool defaultValue = false)
		{
			var result = new List<bool>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToBoolean(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="int"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<int> ToInt32List(int defaultValue = 0)
		{
			var result = new List<int>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInt32(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="uint"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<uint> ToUInt32List(uint defaultValue = 0)
		{
			var result = new List<uint>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToUInt32(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="long"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<long> ToInt64List(long defaultValue = 0)
		{
			var result = new List<long>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInt64(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="ulong"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<ulong> ToUInt64List(ulong defaultValue = 0)
		{
			var result = new List<ulong>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToUInt64(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="float"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<float> ToSingleList(float defaultValue = 0f)
		{
			var result = new List<float>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToSingle(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="double"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<double> ToDoubleList(double defaultValue = 0d)
		{
			var result = new List<double>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDouble(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="double"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Half> ToHalfList(Half defaultValue = default)
		{
			var result = new List<Half>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToHalf(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="decimal"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<decimal> ToDecimalList(decimal defaultValue = 0m)
		{
			var result = new List<decimal>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDecimal(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="Guid"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Guid> ToGuidList(Guid defaultValue = default)
		{
			var result = new List<Guid>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToGuid(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="ulong"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Uuid128> ToUuid128List(Uuid128 defaultValue = default)
		{
			var result = new List<Uuid128>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToUuid128(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="ulong"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<DateTime> ToDateTimeList(DateTime defaultValue = default)
		{
			var result = new List<DateTime>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDateTime(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="ulong"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<DateTimeOffset> ToDateTimeOffsetList(DateTimeOffset defaultValue = default)
		{
			var result = new List<DateTimeOffset>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToDateTimeOffset(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="Instant"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<Instant> ToInstantList(NodaTime.Instant defaultValue = default)
		{
			var result = new List<Instant>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToInstant(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into a list of <see cref="string"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public List<string?> ToStringList(string? defaultValue = null)
		{
			var result = new List<string?>(this.Count);
			foreach (var item in this.AsSpan())
			{
				result.Add(item.ToStringOrDefault(defaultValue));
			}
			return result;
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into an <see cref="ImmutableList{TValue}"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableList<TValue?> ToImmutableList<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];

			resolver ??= CrystalJson.DefaultResolver;
			var result = ImmutableList.CreateBuilder<TValue?>();
			foreach (var item in items)
			{
				result.Add(item.As<TValue?>(default, resolver));
			}
			return result.ToImmutable();
		}

		/// <summary>Deserializes this array into an immutable array of <typeparamref name="TValue"/>, using a custom decoder</summary>
		/// <typeparam name="TValue">Type of the deserialized items</typeparam>
		/// <param name="decoder">Func that is called do decode each element of this array</param>
		/// <returns>Immutable array of deserialized items</returns>
		public ImmutableList<TValue> ToImmutableList<TValue>(Func<JsonValue, TValue> decoder)
		{
			var items = this.AsSpan();
			if (items.Length == 0) return [ ];

			var result = ImmutableList.CreateBuilder<TValue>();
			foreach (var item in items)
			{
				result.Add(decoder(item));
			}
			return result.ToImmutableList();
		}

		/// <summary>Deserializes this <see cref="JsonArray">JSON Array</see> into an <see cref="ImmutableList{TValue}"/></summary>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public ImmutableArray<TValue?> ToImmutableArray<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			resolver ??= CrystalJson.DefaultResolver;
			var items = AsSpan();
			switch (items.Length)
			{
				case 0: return [ ];
				case 1: return [ items[0].As<TValue?>(defaultValue, resolver) ];
				case 2: return [ items[0].As<TValue?>(defaultValue, resolver), items[1].As<TValue?>(defaultValue, resolver) ];
				case 3: return [ items[0].As<TValue?>(defaultValue, resolver), items[1].As<TValue?>(defaultValue, resolver), items[2].As<TValue?>(defaultValue, resolver) ];
				case 4: return [ items[0].As<TValue?>(defaultValue, resolver), items[1].As<TValue?>(defaultValue, resolver), items[2].As<TValue?>(defaultValue, resolver), items[3].As<TValue?>(defaultValue, resolver) ];
				default:
				{
					var list = ImmutableArray.CreateBuilder<TValue?>(items.Length);
					foreach (var item in items)
					{
						list.Add(item.As<TValue?>(defaultValue, resolver));
					}
					return list.ToImmutable();
				}
			}

		}

		/// <summary>Deserializes this array into an immutable array of <typeparamref name="TValue"/>, using a custom decoder</summary>
		/// <typeparam name="TValue">Type of the deserialized items</typeparam>
		/// <param name="decoder">Func that is called do decode each element of this array</param>
		/// <returns>Immutable array of deserialized items</returns>
		public ImmutableArray<TValue> ToImmutableArray<TValue>(Func<JsonValue, TValue> decoder)
		{
			var items = this.AsSpan();
			switch (items.Length)
			{
				case 0: return [ ];
				case 1: return ImmutableArray.Create<TValue>(decoder(items[0]));
				case 2: return ImmutableArray.Create<TValue>(decoder(items[0]), decoder(items[1]));
				case 3: return ImmutableArray.Create<TValue>(decoder(items[0]), decoder(items[1]), decoder(items[2]));
				case 4: return ImmutableArray.Create<TValue>(decoder(items[0]), decoder(items[1]), decoder(items[2]), decoder(items[3]));
				default:
				{
					var result = ImmutableArray.CreateBuilder<TValue>(items.Length);
					foreach (var item in items)
					{
						result.Add(decoder(item));
					}
					return result.ToImmutableArray();
				}
			}
		}

		/// <summary>Tests if there is at least one element in the array</summary>
		/// <returns><see langword="false"/> if the array is empty; otherwise, <see langword="true"/>.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any() => m_size > 0;

		/// <summary>Tests if there is at least one element in the array with the specified type</summary>
		/// <param name="type">Type of element (<see cref="JsonType.Object"/>, <see cref="JsonType.String"/>, <see cref="JsonType.Number"/>, ...)</param>
		/// <returns><see langword="true"/> if there is at least one element of this type in the array, or <see langword="false"/> if the array is empty or contains only elements that are of a different type.</returns>
		public bool Any(JsonType type)
		{
			foreach (var item in this.AsSpan())
			{
				if (item.Type != type)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Tests if there is at least one element in the array that matches the specified <paramref name="predicate"/></summary>
		/// <param name="predicate">Callback that should return <see langword="true"/> for matching elements.</param>
		/// <returns><see langword="true"/> if there is at least one element that matches, or <see langword="true"/> if the array is empty or without any matching element.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool Any([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			foreach (var item in this.AsSpan())
			{
				if (predicate(item))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Tests if all the elements in the array have the specified type</summary>
		/// <param name="type">Type of element (<see cref="JsonType.Object"/>, <see cref="JsonType.String"/>, <see cref="JsonType.Number"/>, ...)</param>
		/// <remarks><see langword="false"/> if at least one element is of a different type, or <see langword="true"/> if the array is empty or with only elements of this type.</remarks>
		/// <remarks>Always return <see langword="true"/> for empty arrays.</remarks>
		public bool All(JsonType type)
		{
			foreach (var item in this.AsSpan())
			{
				if (item.Type != type)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Tests if all elements in the array match the specified <paramref name="predicate"/>.</summary>
		/// <param name="predicate">Callback that should return <see langword="true"/> for matching elements.</param>
		/// <returns><see langword="false"/> if there is at least one element that does not match, or <see langword="true"/> if the array is empty or without only matching elements.</returns>
		[Pure, CollectionAccess(CollectionAccessType.Read)]
		public bool All([InstantHandle] Func<JsonValue, bool> predicate)
		{
			Contract.NotNull(predicate);

			foreach (var item in this.AsSpan())
			{
				if (!predicate(item))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Tests if all elements in the array have a similar type, or if they are dissimilar</summary>
		/// <returns>The <see cref="JsonType"/> that have elements all have in common, or <see langword="null"/> if there is at least two incompatible types present.</returns>
		/// <remarks>
		/// <para>Ignore any null or missing elements in the array, so for example <c>[ 123, null, 789 ]</c> will return <see cref="JsonType.Number"/>.</para>
		/// <para>Will return <see langword="null"/> if the array is only filled with null or missing values, instead of <see cref="JsonType.Null"/>.</para>
		/// </remarks>
		public JsonType? GetElementsTypeOrDefault()
		{
			JsonType? type = null;
			foreach (var item in this.AsSpan())
			{
				var t = item.Type;
				if (t == JsonType.Null) continue;

				if (type is null)
				{ // first non-null
					type = t;
				}
				else if (t != type)
				{ // different type
					return null;
				}
			}
			return type;
		}

		/// <summary>Merges the content of an array into the current array</summary>
		/// <param name="other">Source array that should be copied into the current array.</param>
		/// <param name="deepCopy">If <see langword="false"/> (default), copy the content of <paramref name="other"/> as-is; otherwise, clone all the elements before merging them.</param>
		/// <param name="keepNull">If <see langword="false"/> (default), fields set to null in <paramref name="other"/> will be removed; otherwise, they will be kept as null entries in the merged result.</param>
		public void MergeWith(JsonArray other, bool deepCopy = false, bool keepNull = false)
		{
			Merge(this, other, deepCopy, keepNull);
		}

		/// <summary>Merges the content of an array into the current array</summary>
		/// <param name="parent">Parent array that will be modified</param>
		/// <param name="other">Source array that should be copied into the <paramref name="parent"/>.</param>
		/// <param name="deepCopy">If <see langword="false"/> (default), copy the content of <paramref name="other"/> as-is; otherwise, clone all the elements before merging them.</param>
		/// <param name="keepNull">If <see langword="false"/> (default), fields set to null in <paramref name="other"/> will be removed; otherwise, they will be kept as null entries in the merged result.</param>
		public static JsonArray Merge(JsonArray parent, JsonArray other, bool deepCopy = false, bool keepNull = false)
		{
			if (parent.IsReadOnly) throw FailCannotMutateReadOnlyValue(parent);

			if (parent.Count == 0) return JsonArray.Copy(other, deepCopy, readOnly: false);
			if (other.Count == 0) return JsonArray.Copy(parent, deepCopy, readOnly: false);

			// only the elements that are shared between both arrays are merged
			// if the new array is smaller, the original is not resized
			// if the new array is larger, any extra element is copied as-is

			// merge all elements in common
			int n = Math.Min(parent.Count, other.Count);
			int lastNonNull = -1;
			for (int i = 0; i < n; i++)
			{
				var left = parent[i];
				var right = other[i];
				switch(left, right)
				{
					case (JsonObject a, JsonObject b):
					{ // merge two objects
						parent[i] = JsonObject.Merge(a, b, deepCopy, keepNull);
						lastNonNull = i;
						break;
					}
					case (JsonArray a, JsonArray b):
					{ // merge two arrays
						parent[i] = Merge(a, b, deepCopy, keepNull);
						lastNonNull = i;
						break;
					}
					case (_, JsonNull):
					{ // set to null
						parent[i] = JsonNull.Null;
						if (keepNull && ReferenceEquals(right, JsonNull.Null))
						{
							lastNonNull = i;
						}
						break;
					}
					default:
					{ // overwrite
						parent[i] = deepCopy ? right.Copy() : right;
						lastNonNull = i;
						break;
					}
				}
			}

			//PERF: if the tail is only nulls, we may add them, just to remove them again in the next step!
			// => maybe pre-scan the tail to look for the last non-null value?

			// copy over any extra elements
			for (int i = n; i < other.Count; i++)
			{
				var right = other[i];
				if (right is JsonNull)
				{
					parent[i] = JsonNull.Null;
					if (keepNull && ReferenceEquals(right, JsonNull.Null))
					{
						lastNonNull = i;
					}
				}
				else
				{
					parent[i] = deepCopy ? other[i].Copy() : other[i];
					lastNonNull = i;
				}
			}

			if (!keepNull)
			{ // test if we need to truncate the original to remove the trailing nulls?

				if (lastNonNull == -1)
				{ // all items were removed!
					return new JsonArray();
				}

				if (lastNonNull < parent.Count - 1)
				{ // truncate extra nulls that must be removed!
					parent.Truncate(lastNonNull + 1);
				}
			}

			return parent;
		}

		/// <summary>Compute the delta between this array and a different version, in order to produce a patch that contains the instruction to go from this instance to the new version</summary>
		/// <param name="after">New version of the array</param>
		/// <param name="deepCopy">If <see langword="true"/>, create a copy of all mutable elements before adding them to the resulting patch.</param>
		/// <param name="readOnly">If <see langword="true"/>, the resulting patch will be read-only</param>
		/// <returns>Value that can be passed to <see cref="ApplyPatch"/> in order to transform this array into <paramref name="after"/>.</returns>
		/// <remarks>The patch produced is not marked as immutable. The caller should call <see cref="Freeze"/> on the result if immutability is required.</remarks>
		public JsonValue ComputePatch(JsonArray after, bool deepCopy = false, bool readOnly = false)
		{
			if (this.Count == 0)
			{ // all items added
				var value = deepCopy ? after.Copy() : after;
				return readOnly ? value.ToReadOnly() : value;
			}
			if (after.Count == 0)
			{ // all items removed
				return JsonArray.ReadOnly.Empty;
			}

			var patch = new JsonObject()
			{
				["__patch"] = after.Count, // new size of the array
			};

			var size = m_size;
			var items = m_items;
			for (int i = 0; i < after.Count; i++)
			{
				var left = i < size ? items[i] : JsonNull.Missing;
				var right = after[i];

				// skip items that are identical
				if (left.Equals(right)) continue;

				// changed value
				switch (left, right)
				{
					case (JsonObject a, JsonObject b):
					{ // compute object patch
						var diff = a.ComputePatch(b, deepCopy, readOnly);
						patch[StringConverters.ToString(i)] = diff;
						break;
					}
					case (JsonArray a, JsonArray b):
					{ // compute array patch
						var diff = a.ComputePatch(b, deepCopy, readOnly);
						patch[StringConverters.ToString(i)] = diff;
						break;
					}
					case (JsonNull, JsonNull):
					{ // ignore
						break;
					}
					case (_, JsonNull):
					{ // mark for deletion
						patch[StringConverters.ToString(i)] = JsonNull.Null;
						break;
					}
					default:
					{ // overwrite with new value
						var value = deepCopy ? right.Copy() : right;
						if (readOnly)
						{
							value = value.ToReadOnly();
						}
						patch[StringConverters.ToString(i)] = value;
						break;
					}
				}
			}

			if (readOnly)
			{
				patch.FreezeUnsafe();
			}

			return patch;
		}

		/// <summary>Applies a patch to this array, by modifying it in-place</summary>
		/// <param name="patch">Patch containing the changes to apply to this instance (previously computed by a call to <see cref="ComputePatch"/>)</param>
		/// <param name="deepCopy">If <see langword="false"/> (default), copy the content of <paramref name="patch"/> as-is; otherwise, clone all the elements before merging them.</param>
		/// <remarks>If this array was equal to 'before', and <paramref name="patch"/> is the result of calling <see cref="ComputePatch">ComputePatch(before, after)</see>, then the array will now be equal to 'after'</remarks>
		public void ApplyPatch(JsonObject patch, bool deepCopy = false)
		{
			Contract.NotNull(patch);
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			int newSize = patch.Get<int?>("__patch", null) ?? throw new ArgumentException("Object is not a valid patch for an array: required '__patch' field is missing");
			if (newSize == 0)
			{ // clear all items
				Clear();
				return;
			}

			var size = m_size;
			if (newSize > size)
			{
				ResizeBuffer(newSize);
			}
			var items = m_items;

			// patch all changed items
			foreach (var kv in patch)
			{
				if (kv.Key == "__patch") continue;
				if (!int.TryParse(kv.Key, out var idx)) throw new ArgumentException($"Object is not a valid patch for an array: unexpected '{kv.Key}' field");
				if (idx >= newSize) throw new ArgumentException($"Object is not a valid patch for an array: key '{kv.Key}' is greater than the final size");

				var prev = idx < size ? items[idx] : JsonNull.Missing;
				switch (prev, kv.Value)
				{
					case (JsonObject a, JsonObject b):
					{
						if (a.IsReadOnly)
						{
							a = a.ToMutable();
							items[idx] = a;
						}
						a.ApplyPatch(b, deepCopy);
						break;
					}
					case (JsonArray a, JsonObject b) when (b.ContainsKey("__patch")):
					{
						if (a.IsReadOnly)
						{
							a = a.ToMutable();
							items[idx] = a;
						}
						a.ApplyPatch(b, deepCopy);
						break;
					}
					case (_, JsonNull):
					{
						items[idx] = JsonNull.Null;
						break;
					}
					default:
					{
						items[idx] = deepCopy ? kv.Value.Copy() : kv.Value;
						break;
					}
				}
			}

			// clear the tail
			items.AsSpan(newSize).Clear();
			m_size = newSize;
		}

		[CollectionAccess(CollectionAccessType.Read)]
		internal static JsonArray Project(IEnumerable<JsonValue?> source, KeyValuePair<string, JsonValue?>[] defaults)
		{
			Contract.Debug.Requires(source is not null && defaults is not null);

			var array = source is ICollection<JsonValue> coll ? new JsonArray(coll.Count) : new JsonArray();

			foreach (var item in source)
			{
				if (item is null)
				{
					array.Add(JsonNull.Null);
					continue;
				}

				switch (item.Type)
				{
					case JsonType.Object:
					{
						array.Add(JsonObject.Project((JsonObject) item, defaults));
						break;
					}
					case JsonType.Null:
					{ // null/JsonNull are not modified
						array.Add(JsonNull.Null);
						break;
					}
					default:
					{
						throw ThrowHelper.InvalidOperationException($"Cannot project element of type {item.Type} in JSON array");
					}
				}
			}
			return array;
		}

		#region Flatten...

		/// <summary>Flattens a list of nested lists into a single list</summary>
		/// <param name="deep">If <see langword="false"/>, only flattens the first level. If <see langword="true"/>, recursively flattens all nested lists</param>
		/// <returns>Flattened list</returns>
		/// <example>
		/// <c>Flatten([ [1,2], 3, [4, [5,6]] ], deep: false) => [ 1, 2, 3, 4, [5, 6] ]</c>
		/// <c>Flatten([ [1,2], 3, [4, [5,6]] ], deep: true) => [ 1, 2, 3, 4, 5, 6 ]</c>
		/// </example>
		public JsonArray Flatten(bool deep = false)
		{
			var array = new JsonArray();
			FlattenRecursive(this, array, deep ? int.MaxValue : 1);
			return array;
		}

		private static void FlattenRecursive(JsonArray items, JsonArray output, int limit)
		{
			Contract.Debug.Requires(items is not null && output is not null);
			foreach(var item in items.AsSpan())
			{
				if (limit > 0 && item is JsonArray arr)
				{
					Contract.Debug.Requires(!ReferenceEquals(arr, items));
					FlattenRecursive(arr, output, limit - 1);
				}
				else
				{
					output.Add(item);
				}
			}
		}

		#endregion

		#region Changes...

		/// <summary>Compare this array with a previous version, and return the list of added, removed and unchanged elements</summary>
		/// <param name="previous">Previous version of this array</param>
		/// <remarks>The change detection is "by value" semantics, but will be faster if both the current and previous arrays are "immutable" and where unchanged elements are copied by reference</remarks>
		public (JsonArray Added, JsonArray Removed, JsonArray Unchanged) ComputeChanges(JsonArray previous)
		{
			//TODO: find a better algorithm!

			var added = new JsonArray();
			var removed = new JsonArray();
			var unchanged = new JsonArray();

			var items = new HashSet<JsonValue>(previous, JsonValueComparer.Default);

			//HACKHACK: first naive implementation, just to get things working!
			foreach (var item in this)
			{
				if (items.Remove(item))
				{
					unchanged.Add(item);
				}
				else
				{
					added.Add(item);
				}
			}

			foreach (var item in items)
			{
				removed.Add(item);
			}

			return (added, removed, unchanged);
		}

		#endregion

		#region Errors...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception Error_CannotMapNullValuesToDictionary(int index)
		{
			return new InvalidOperationException($"Cannot map a JSON Array containing null value (at offset {index}) into a Dictionary");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception Error_CannotMapValueTypeToDictionary(JsonValue item, int index)
		{
			return new InvalidOperationException($"Cannot map a JSON Array containing a value of type {item.Type} (at offset {index}) into a Dictionary");
		}

		#endregion

		#region IJsonSerializable

		private static bool ShouldInlineArray(ReadOnlySpan<JsonValue> items) => items.Length switch
		{
			0 => true,
			1 => items[0].IsInlinable(),
			2 => items[0].IsInlinable() && items[1].IsInlinable(),
			_ => false
		};

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_size == 0)
			{
				return settings.IsCompactLayout() ? "[]" : "[ ]";
			}

			var writer = CrystalJson.WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				this.JsonSerialize(writer);

				return writer.GetString();
			}
			finally
			{
				writer.Dispose();
				CrystalJson.WriterPool.Free(writer);
			}
		}

		/// <inheritdoc />
		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			var items = GetSpan();
			if (items.Length == 0)
			{
				writer.WriteEmptyArray();
				return;
			}

			writer.MarkVisited(this);
			bool inline = writer.Indented && ShouldInlineArray(items);
			if (inline)
			{
				var state = writer.BeginInlineArray();
				writer.WriteInlineHeadSeparator();
				items[0].JsonSerialize(writer);
				for (int i = 1; i < items.Length; i++)
				{
					writer.WriteInlineTailSeparator();
					items[i].JsonSerialize(writer);
				}
				writer.EndInlineArray(state);
			}
			else
			{
				var state = writer.BeginArray();
				writer.WriteHeadSeparator();
				items[0].JsonSerialize(writer);
				for (int i = 1; i < items.Length; i++)
				{
					writer.WriteTailSeparator();
					items[i].JsonSerialize(writer);
				}
				writer.EndArray(state);
			}
			writer.Leave(this);
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			string? literal;

			switch (format)
			{
				case "" or "D" or "d" or "N" or "n":
				{
					return TryFormatNormal(GetSpan(), destination, out charsWritten);
				}
				case "C" or "c":
				{
					return TryFormatCompact(GetSpan(), destination, out charsWritten);
				}
				case "P" or "p":
				{
					//TODO: maybe attempt to do it without allocating?
					// => for the moment, we will serialize the object into memory, and copy the result
					literal = ToJsonText(CrystalJsonSettings.JsonIndented);
					break;
				}
				case "Q" or "q":
				{
					literal = ToString("Q", provider);
					break;
				}
				case "J" or "j":
				{
					//TODO: maybe attempt to do it without allocating?
					// => for the moment, we will serialize the object into memory, and copy the result
					literal = ToJsonText(CrystalJsonSettings.JavaScript);
					break;
				}
				case "B" or "b":
				{
					literal = ToString("B", provider);
					break;
				}
				default:
				{
					// we MUST throw here; otherwise, the caller will infinitely call use back with a larger and larger buffer!
					throw new NotSupportedException($"Invalid JSON format '{format}' specification");
				}
			}

			return literal.TryCopyTo(destination, out charsWritten);
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			var settings = format switch
			{
				"" or "D" or "d" => CrystalJsonSettings.Json,
				"C" or "c" => CrystalJsonSettings.JsonCompact,
				"P" or "p" => CrystalJsonSettings.JsonIndented,
				"J" or "j" => CrystalJsonSettings.JavaScript,
				_ => null
			};

			if (settings is null)
			{
				if (format is "Q" or "q")
				{
					//PERF: is there a better way?
					return Encoding.UTF8.TryGetBytes(GetCompactRepresentation(0), destination, out bytesWritten);
				}
				if (format is "B" or "b")
				{
					return JsonEncoding.TryEncodeTo(this.ToString("C"), destination, out bytesWritten);
				}

				// we MUST throw here; otherwise, the caller will infinitely call use back with a larger and larger buffer!
				throw new NotSupportedException($"Invalid JSON format '{format}' specification");
			}

			// PERF: TODO: maybe attempt to do it without allocating?
			// => for the moment, we will serialize the object into memory, and copy the result
			using var data = CrystalJson.ToSlice(this, null, ArrayPool<byte>.Shared, settings);
			return data.TryCopyTo(destination, out bytesWritten);
		}

#endif

		#endregion

		#region IEquatable<...>

		/// <inheritdoc />
		public override bool Equals(JsonValue? other) => other is JsonArray arr && Equals(arr);

		/// <summary>Tests if two arrays are considered equal</summary>
		public bool Equals(JsonArray? other)
		{
			int n = m_size;
			if (other is null || other.Count != n)
			{
				return false;
			}
			var l = m_items;
			var r = other.m_items;
			for (int i = 0; i < n; i++)
			{
				if (!l[i].Equals(r[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Tests if two arrays are considered equal, using a custom equality comparer</summary>
		public bool Equals(JsonArray? other, IEqualityComparer<JsonValue>? comparer)
		{
			int n = m_size;
			if (other is null || other.Count != n)
			{
				return false;
			}

			comparer ??= JsonValueComparer.Default;
			var l = m_items;
			var r = other.m_items;
			for (int i = 0; i < n; i++)
			{
				if (!comparer.Equals(l[i], r[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonArray arr && StrictEquals(arr);

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		public bool StrictEquals(ReadOnlySpan<JsonValue> other)
		{
			var items = AsSpan();
			if (other.Length != items.Length)
			{
				return false;
			}
			for (int i = 0; i < items.Length; i++)
			{
				if (!items[i].StrictEquals(other[i]))
				{
					return false;
				}
			}
			return true;
		}

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		public bool StrictEquals(JsonValue[]? other)
		{
			if (other is null) return false;
			return StrictEquals(new ReadOnlySpan<JsonValue>(other));
		}

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		public bool StrictEquals(JsonArray? other)
		{
			if (other is null) return false;
			return StrictEquals(other.AsSpan());
		}

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		public bool StrictEquals(IEnumerable<JsonValue>? other)
		{
			if (other is null) return false;
			if (other is JsonArray arr) return StrictEquals(arr.AsSpan());
			if (other.TryGetSpan(out var span)) return StrictEquals(span);

			span = AsSpan();
			int p = 0;
			foreach (var item in other)
			{
				if (p >= span.Length || !span[p++].Equals(item))
				{
					return false;
				}
			}
			return p == span.Length;
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool ValueEquals<TCollection>(TCollection? value, IEqualityComparer<TCollection>? comparer = null)
			where TCollection : default
		{
			// we will attempt to optimize for some of the most common array and list types,
			// in order to reduce memory allocations, otherwise we have to "materialize" the array into the corresponding type,
			// which could be _very_ costly, especially if this is an embedded array inside a larger object in diff/delta operations

			if (default(TCollection) is null)
			{
				if (value is null) return false;

				return value switch
				{
					JsonValue j => StrictEquals(j),

					// common array types
					string[] xs => ValuesEqual(xs),
					int[] xs => ValuesEqual(xs),
					bool[] xs => ValuesEqual(xs),
					long[] xs => ValuesEqual(xs),
					double[] xs => ValuesEqual(xs),
					float[] xs => ValuesEqual(xs),
					Guid[] xs => ValuesEqual(xs),
					Uuid128[] xs => ValuesEqual(xs),
					decimal[] xs => ValuesEqual(xs),
#if NET8_0_OR_GREATER
					Half[] xs => ValuesEqual(xs),
					Int128[] xs => ValuesEqual(xs),
#endif
					// common list types
					List<string> xs => ValuesEqual(xs),
					List<int> xs => ValuesEqual(xs),
					List<bool> xs => ValuesEqual(xs),
					List<long> xs => ValuesEqual(xs),
					List<double> xs => ValuesEqual(xs),
					List<float> xs => ValuesEqual(xs),
					List<Guid> xs => ValuesEqual(xs),
					List<Uuid128> xs => ValuesEqual(xs),
					List<decimal> xs => ValuesEqual(xs),
#if NET8_0_OR_GREATER
					List<Half> xs => ValuesEqual(xs),
					List<Int128> xs => ValuesEqual(xs),
#endif
					_ => ValueEqualsSlow(this, value, comparer),
				};
			}
			else
			{
				return ValueEqualsSlow(this, value!, comparer);
			}

			static bool ValueEqualsSlow(JsonArray self, TCollection value, IEqualityComparer<TCollection>? comparer)
			{
				var type = typeof(TCollection);
				if (type.IsEnumerableType(out var itemType))
				{
#pragma warning disable IL2060, IL3050
					if (type.IsArray)
					{ // optimized for arrays
						return (bool) (s_helperArray ??= GetArrayComparisonHelper()).MakeGenericMethod(itemType).Invoke(self, [ value ])!;
					}
					else
					{
						// TODO: call the IEnumerable<T> variant
						return (bool) (s_helperEnumerable ??= GetEnumerableComparisonHelper()).MakeGenericMethod(itemType).Invoke(self, [ value ])!;
					}
#pragma warning restore IL2060, IL3050
				}

				var x = self.Bind<TCollection>();
				return (comparer ?? EqualityComparer<TCollection>.Default).Equals(x, value);
			}
		}

		private static MethodInfo? s_helperArray;
		private static MethodInfo GetArrayComparisonHelper() => typeof(JsonArray).GetMethod(nameof(ValuesEqualArrayHelper), BindingFlags.Instance | BindingFlags.NonPublic)!;

		private static MethodInfo? s_helperEnumerable;
		private static MethodInfo GetEnumerableComparisonHelper() => typeof(JsonArray).GetMethod(nameof(ValuesEqualEnumerableHelper), BindingFlags.Instance | BindingFlags.NonPublic)!;

		private bool ValuesEqualArrayHelper<TValue>(TValue[] values) => ValuesEqual(values);

		private bool ValuesEqualEnumerableHelper<TValue>(IEnumerable<TValue> values) => ValuesEqual(values);

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(string?[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<string?>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<string?> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				var item = items[i];
				if (item == null)
				{
					if (span[i] is not JsonNull) return false;
				}
				else
				{
					if (span[i] is not JsonString str || !str.Equals(item)) return false;
				}
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(int[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<int>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<int> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i] is not JsonNumber num || !num.Equals(items[i])) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(long[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<long>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<long> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i] is not JsonNumber num || !num.Equals(items[i])) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(float[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<float>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<float> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i] is not JsonNumber num || !num.Equals(items[i])) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(double[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<double>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<double> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i] is not JsonNumber num || !num.Equals(items[i])) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(bool[]? items) => items is not null && ValuesEqual(new ReadOnlySpan<bool>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[OverloadResolutionPriority(2)]
		public bool ValuesEqual(ReadOnlySpan<bool> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (span[i] is not JsonBoolean b || b.Value != items[i]) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[Pure]
		[OverloadResolutionPriority(1)]
		public bool ValuesEqual<TValue>(ReadOnlySpan<TValue> items)
		{
			var span = AsSpan();
			if (span.Length != items.Length) return false;
			for (int i = 0; i < span.Length; i++)
			{
				if (!span[i].ValueEquals(items[i])) return false;
			}
			return true;
		}

		/// <summary>Tests if the elements of this array are equal to the elements of the specified span, using the strict JSON comparison semantics</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ValuesEqual<TValue>(ReadOnlyMemory<TValue> items) => ValuesEqual<TValue>(items.Span);

		/// <summary>Tests if the elements of this array are equal to the elements of the specified array, using the strict JSON comparison semantics</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ValuesEqual<TValue>(TValue[]? items) => items is not null && ValuesEqual<TValue>(new ReadOnlySpan<TValue>(items));

		/// <summary>Tests if the elements of this array are equal to the elements of the specified sequence, using the strict JSON comparison semantics</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ValuesEqual<TValue>(IEnumerable<TValue>? items)
		{
			if (items is null) return false;
			if (items is JsonArray arr) return StrictEquals(arr);
			if (items.TryGetSpan(out var xs)) return ValuesEqual<TValue>(xs);
			return ValueEqualsEnumerable(AsSpan(), items);

			static bool ValueEqualsEnumerable(ReadOnlySpan<JsonValue> values, IEnumerable<TValue> items)
			{
				int p = 0;
				foreach (var item in items)
				{
					if (p >= values.Length || !values[p++].ValueEquals(item))
					{
						return false;
					}
				}
				return p == values.Length;
			}
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			if (!this.IsReadOnly)
			{
				// the hash code of the array should never change, even if the _content_ of the array can change
				return RuntimeHelpers.GetHashCode(this);
			}
			else
			{
				return ComputeReadOnlyHashCode(GetSpan());
			}

			static int ComputeReadOnlyHashCode(ReadOnlySpan<JsonValue> items)
			{
				HashCode h = new();
				h.Add(items.Length);
				foreach (var item in items)
				{
					h.Add(item.GetHashCode());
				}
				return h.ToHashCode();
			}
		}

		/// <inheritdoc />
		public override int CompareTo(JsonValue? other)
		{
			if (other is JsonArray array) return CompareTo(array);
			return base.CompareTo(other);
		}

		/// <inheritdoc />
		public int CompareTo(JsonArray? other)
		{
			if (other is null) return +1;

			var selfItems = AsSpan();
			var otherItems = other.AsSpan();

			int n = Math.Min(selfItems.Length, otherItems.Length);
			for (int i = 0; i < n;i++)
			{
				int cmp = selfItems[i].CompareTo(otherItems[i]);
				if (cmp != 0) return cmp;
			}
			return selfItems.Length - otherItems.Length;
		}

		#endregion

		#region ISliceSerializable

		/// <inheritdoc />
		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('[');
			bool first = true;
			foreach (var item in this.AsSpan())
			{
				if (first)
				{
					first = false;
				}
				else
				{
					writer.WriteByte(',');
				}

				item.WriteTo(ref writer);
			}
			writer.WriteByte(']');
		}

		#endregion

	}

	/// <summary>Extension methods for <see cref="JsonArray"/></summary>
	[PublicAPI]
	public static class JsonArrayExtensions
	{

		/// <summary>Returns an array of <typeparamref name="TValue"/> containing the transformed elements of a <see cref="JsonArray"/></summary>
		/// <remarks>This is the logical equivalent of <c>array.Select(transform).ToArray()</c></remarks>
		[Pure]
		public static TValue[] ToArray<TValue>(this JsonArray self, [InstantHandle] Func<JsonValue, TValue> transform)
		{
			Contract.NotNull(self);
			Contract.NotNull(transform);

			var items = self.AsSpan();
			var res = new TValue[items.Length];

			for (int i = 0; i < items.Length; i++)
			{
				res[i] = transform(items[i]);
			}

			return res;
		}

		/// <summary>Returns a <see cref="List{TValue}"/> containing the transformed elements of a <see cref="JsonArray"/></summary>
		/// <remarks>This is the logical equivalent of <c>array.Select(transform).ToList()</c></remarks>
		[Pure]
		public static List<TValue> ToList<TValue>(this JsonArray self, [InstantHandle] Func<JsonValue, TValue> transform)
		{
			Contract.NotNull(self);
			Contract.NotNull(transform);

			var items = self.AsSpan();
			
#if NET8_0_OR_GREATER
			var res = new List<TValue>(items.Length);
			CollectionsMarshal.SetCount(res, items.Length);
			var buf = CollectionsMarshal.AsSpan(res);
			for (int i = 0; i < items.Length; i++)
			{
				buf[i] = transform(items[i]);
			}
			return res;
#else
			var res = new List<TValue>(items.Length);
			for (int i = 0; i < items.Length; i++)
			{
				res[i] = transform(items[i]);
			}
			return res;
#endif
		}

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public static JsonArray AddRange(this JsonArray self, IEnumerable<JsonObject?> values)
			=> self.AddRange(values);

		/// <summary>Appends all the elements of an <see cref="IEnumerable{T}"/> to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public static JsonArray AddRangeReadOnly(this JsonArray self, IEnumerable<JsonObject?> values)
			=> self.AddRangeReadOnly(values);

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public static JsonArray AddRange(this JsonArray self, JsonObject[] values) =>
			// ReSharper disable once CoVariantArrayConversion
			self.AddRange(values);

		/// <summary>Appends all the elements of an array to the end of this <see cref="JsonArray"/></summary>
		/// <remarks>Any mutable element in <paramref name="values"/> will be converted to read-only before being added. Elements that were already read-only will be added be reference.</remarks>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public static JsonArray AddRangeReadOnly(this JsonArray self, JsonObject[] values) =>
			// ReSharper disable once CoVariantArrayConversion
			self.AddRangeReadOnly(values);

		#region ToJsonArray...

		#region Mutable...

		/// <summary>Creates a <see cref="JsonArray"/> from a read-only span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this ReadOnlySpan<JsonValue> source)
			=> new JsonArray().AddRange(source);

		/// <summary>Creates a <see cref="JsonArray"/> from a span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this Span<JsonValue> source)
			=> new JsonArray().AddRange(source);

		/// <summary>Creates a <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this JsonValue[] source)
			=> new JsonArray().AddRange(source);

		/// <summary>Creates a <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this JsonObject[] source)
		// ReSharper disable once CoVariantArrayConversion
			=> new JsonArray().AddRange(source);

		/// <summary>Creates a <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonValue}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonValue?> source)
			=> new JsonArray().AddRange(source);

		/// <summary>Creates a <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonObject}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray([InstantHandle] this IEnumerable<JsonObject?> source)
			=> new JsonArray().AddRange(source);

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <returns>A <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArray<TSource>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, JsonValue?> selector)
			=> new JsonArray().AddRange(source, selector);

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <returns>A <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArray<TSource>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, JsonObject?> selector)
			=> new JsonArray().AddRange(source, selector);

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <returns>A <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArray<TTKey, TValue>(this IDictionary<TTKey, TValue> source, [InstantHandle] Func<TTKey, TValue, JsonValue?> selector)
			=> new JsonArray().AddRange(source, selector);

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <returns>A <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArray<TTKey, TValue>(this IDictionary<TTKey, TValue> source, [InstantHandle] Func<TTKey, TValue, JsonObject?> selector)
			=> new JsonArray().AddRange(source, selector);

		/// <summary>Creates a <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonValue}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>([InstantHandle] this IEnumerable<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues<TElement>(source, settings, resolver);

		/// <summary>Creates a <see cref="JsonArray"/> from a read-only span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this ReadOnlySpan<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues<TElement>(source, settings, resolver);

		/// <summary>Creates a <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArray<TElement>(this TElement[] source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues(source, settings, resolver);

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new <see cref="JsonArray"/></summary>
		/// <returns>A <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArray<TSource, TValue>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValues(source, selector, settings, resolver);

		#endregion

		#region Immutable...

		/// <summary>Creates a read-only <see cref="JsonArray"/> from a read-only span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this ReadOnlySpan<JsonValue> source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from a span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this Span<JsonValue> source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this JsonValue[] source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this JsonObject[] source)
			// ReSharper disable once CoVariantArrayConversion
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonValue}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this IEnumerable<JsonValue?> source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonValue}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly(this IEnumerable<JsonObject?> source)
			=> new JsonArray().AddRangeReadOnly(source).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an <see cref="IEnumerable{JsonValue}"/>.</summary>
		/// <param name="source">The <see cref="T:System.Collections.Generic.IEnumerable`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this IEnumerable<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from a read-only span.</summary>
		/// <param name="source">The <see cref="T:System.ReadOnlySpan`1" /> to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input span.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this ReadOnlySpan<TElement> source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Creates a read-only <see cref="JsonArray"/> from an array.</summary>
		/// <param name="source">The array to create a <see cref="JsonArray" /> from.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
		/// <exception cref="T:System.ArgumentNullException"> <paramref name="source" /> is <see langword="null" />.</exception>
		/// <returns>A read-only <see cref="JsonArray" /> that contains elements from the input sequence.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonArray ToJsonArrayReadOnly<TElement>(this TElement[] source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly<TElement>(source, settings, resolver).FreezeUnsafe();

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new read-only <see cref="JsonArray"/></summary>
		/// <returns>A read-only <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArrayReadOnly<TSource>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, JsonValue?> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly(source, selector, settings, resolver).FreezeUnsafe();

		/// <summary>Transforms the elements of an <see cref="T:System.Collections.Generic.IEnumerable`1"/> into a new read-only <see cref="JsonArray"/></summary>
		/// <returns>A read-only <see cref="JsonArray" /> that contains values transformed from the elements of the input sequence.</returns>
		public static JsonArray ToJsonArrayReadOnly<TSource, TValue>(this IEnumerable<TSource> source, [InstantHandle] Func<TSource, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> new JsonArray().AddValuesReadOnly(source, selector, settings, resolver).FreezeUnsafe();

		#endregion

		#endregion

		#region Pick...

		/// <summary>Returns a list of objects with only the fields listed in <paramref name="keys"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, ReadOnlySpan<string> keys, bool keepMissing = false)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys, keepMissing));
		}

		/// <summary>Returns a list of objects with only the fields listed in <paramref name="keys"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, IEnumerable<string> keys, bool keepMissing = false)
		{
			Contract.NotNull(source);
			Contract.NotNull(keys);

			return JsonArray.Project(source, JsonObject.CheckProjectionFields(keys as string[] ?? keys.ToArray(), keepMissing));
		}

		/// <summary>Returns a list of objects with only the fields listed in <paramref name="defaults"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, JsonObject defaults)
		{
			Contract.NotNull(source);
			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults!));
		}

		/// <summary>Returns a list of objects with only the fields listed in <paramref name="defaults"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, IDictionary<string, JsonValue?> defaults)
		{
			Contract.NotNull(source);
			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults));
		}

		/// <summary>Returns a list of objects with only the fields listed in <paramref name="defaults"/>.</summary>
		public static JsonArray Pick([InstantHandle] this IEnumerable<JsonValue?> source, object defaults)
		{
			Contract.NotNull(source);

			return JsonArray.Project(source, JsonObject.CheckProjectionDefaults(defaults));
		}

		#endregion

		/// <summary>Maps the elements of a list into a Dictionary</summary>
		/// <typeparam name="TKey">Type of the keys of the Dictionary</typeparam>
		/// <typeparam name="TValue">Type of the values of Dictionary</typeparam>
		/// <param name="source">Sequence of non-null elements to be mapped.</param>
		/// <param name="target">Target dictionary that will receive the mapped key/value pairs</param>
		/// <param name="expectedType">Expected type for the objects in the sequence (or null if unknown)</param>
		/// <param name="keySelector">Lambda used to extract a key for each element in the sequence</param>
		/// <param name="valueSelector">Lambda used to extract a value for each element in the sequence</param>
		/// <param name="resolver">Custom type resolver</param>
		/// <param name="overwrite">If <see langword="true"/>, overwrite existing keys in <paramref name="target"/>. If <see langword="false"/>, throws an exception in case of duplicate keys</param>
		/// <returns>The value of <paramref name="target"/></returns>
		/// <exception cref="System.ArgumentNullException">If a required parameter is null</exception>
		/// <exception cref="System.InvalidOperationException">If an element of the sequence is null, or not of the expected type</exception>
		public static IDictionary<TKey, TValue> MapTo<TKey, TValue>(
			[InstantHandle] this IEnumerable<JsonValue?> source,
			IDictionary<TKey, TValue> target,
			JsonType? expectedType,
			[InstantHandle] Func<JsonValue, JsonValue> keySelector,
			[InstantHandle] Func<JsonValue, JsonValue> valueSelector,
			ICrystalJsonTypeResolver? resolver = null,
			bool overwrite = false
		) where TKey : notnull
		{
			Contract.NotNull(source);
			Contract.NotNull(target);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			resolver ??= CrystalJson.DefaultResolver;

			int index = 0;
			foreach (var item in source)
			{
				if (item is null || item.IsNull)
				{
					throw JsonArray.Error_CannotMapNullValuesToDictionary(index);
				}
				if (expectedType.HasValue && item.Type != expectedType.GetValueOrDefault())
				{
					throw JsonArray.Error_CannotMapValueTypeToDictionary(item, index);
				}

				var key = keySelector(item).Required<TKey>(resolver);
				var value = valueSelector(item).As<TValue>(default!, resolver);
				if (overwrite)
				{
					target[key] = value;
				}
				else
				{
					target.Add(key, value);
				}
				++index;
			}

			return target;
		}

	}

	/// <summary>Wrapper for a <see cref="JsonArray"/> that casts each element into a required <typeparamref name="TValue"/>.</summary>
	[PublicAPI]
	public readonly struct JsonArray<TValue> : IReadOnlyList<TValue>
		where TValue : notnull
	{

		private readonly JsonArray m_array;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonArray(JsonArray array)
		{
			m_array = array;
		}

		/// <summary>Returns an enumerator that iterates through the array.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new(m_array.AsMemory());

		IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => new Enumerator(m_array.AsMemory());

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(m_array.AsMemory());

		/// <summary>Enumerator that casts each element of a <see cref="JsonArray"/> into a specified JSON type.</summary>
		public struct Enumerator : IEnumerator<TValue>
		{
			private readonly ReadOnlyMemory<JsonValue> m_items;
			private int m_index;
			private TValue? m_current;

			internal Enumerator(ReadOnlyMemory<JsonValue> items)
			{
				m_items = items;
				m_index = 0;
				m_current = default;
			}

			/// <inheritdoc />
			public readonly void Dispose()
			{ }

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				var items = m_items.Span;
				int index = m_index;
				if ((uint) index >= (uint) items.Length)
				{
					return MoveNextRare();
				}

				m_current = items[index].RequiredIndex(index).Required<TValue>();
				m_index++;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				m_index = m_items.Length + 1;
				m_current = default;
				return false;
			}

			readonly object IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index > m_items.Length)
					{
						throw ThrowHelper.InvalidOperationException("Operation cannot happen.");
					}
					return this.Current;
				}
			}

			void IEnumerator.Reset()
			{
				m_index = 0;
				m_current = default;
			}

			/// <inheritdoc />
			public readonly TValue Current => m_current!;
		}

		/// <inheritdoc />
		public int Count => m_array.Count;

		/// <summary>Returns the element at the specified index, converted into type <typeparamref name="TValue"/></summary>
		/// <remarks>Throw an exception if the index is out of bounds, or if the value is null or missing</remarks>
		public TValue this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_array.Get<TValue>(index);
		}

		/// <summary>Returns the element at the specified index, converted into type <typeparamref name="TValue"/></summary>
		/// <remarks>Throw an exception if the index is out of bounds, or if the value is null or missing</remarks>
		public TValue this[Index index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_array.Get<TValue>(index);
		}

		/// <summary>Returns an array with all the elements, converted into type <typeparamref name="TValue"/>.</summary>
		/// <remarks>If any element is null or missing, an exception will be thrown.</remarks>
		[Pure]
		public TValue[] ToArray()
		{
			var items = m_array.AsSpan();
			if (items.Length == 0) return [];

			var res = new TValue[items.Length];
			for(int i = 0; i < items.Length; i++)
			{
				res[i] = items[i].RequiredIndex(i).Required<TValue>();
			}
			return res;
		}

		/// <summary>Returns a <see cref="List{TValue}"/> with all the elements, converted into type <typeparamref name="TValue"/>.</summary>
		/// <remarks>If any element is null or missing, an exception will be thrown.</remarks>
		[Pure]
		public List<TValue> ToList()
		{
			var items = m_array.AsSpan();
			if (items.Length == 0) return [];

			var res = new List<TValue>(items.Length);
			for(int i = 0; i < items.Length; i++)
			{
				res.Add(items[i].RequiredIndex(i).Required<TValue>());
			}
			return res;
		}

	}

	/// <summary>Wrapper for a <see cref="JsonArray"/> that casts each element into an optional <typeparamref name="TValue"/>.</summary>
	[PublicAPI]
	public readonly struct JsonArrayOrDefault<TValue> : IReadOnlyList<TValue?>
	{
		//note: this is to convert JsonValue into JsonArray, JsonObject, JsonType, ...

		private readonly JsonArray m_array;

		private readonly TValue? m_missing;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonArrayOrDefault(JsonArray array, TValue? missing)
		{
			m_array = array;
			m_missing = missing;
		}

		/// <summary>Returns an enumerator that iterates through the array.</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Enumerator GetEnumerator() => new(m_array.AsMemory(), m_missing);

		IEnumerator<TValue> IEnumerable<TValue?>.GetEnumerator() => new Enumerator(m_array.AsMemory(), m_missing);

		IEnumerator IEnumerable.GetEnumerator() => new Enumerator(m_array.AsMemory(), m_missing);

		/// <summary>Enumerator that casts each element of a <see cref="JsonArray"/> into a specified type.</summary>
		public struct Enumerator : IEnumerator<TValue?>
		{
			private readonly ReadOnlyMemory<JsonValue> m_items;
			private int m_index;
			private TValue? m_current;
			private readonly TValue? m_missing;

			internal Enumerator(ReadOnlyMemory<JsonValue> items, TValue? missing)
			{
				m_items = items;
				m_index = 0;
				m_current = default;
				m_missing = missing;
			}

			/// <inheritdoc />
			public readonly void Dispose()
			{ }

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool MoveNext()
			{
				var items = m_items.Span;
				int index = m_index;
				if ((uint) index >= (uint) items.Length)
				{
					return MoveNextRare();
				}

				m_current = items[index].As<TValue?>(defaultValue: m_missing);
				m_index++;
				return true;
			}

			[MethodImpl(MethodImplOptions.NoInlining)]
			private bool MoveNextRare()
			{
				m_index = m_items.Length + 1;
				m_current = default;
				return false;
			}

			readonly object? IEnumerator.Current
			{
				get
				{
					if (m_index == 0 || m_index > m_items.Length)
					{
						throw ThrowHelper.InvalidOperationException("Operation cannot happen.");
					}
					return this.Current;
				}
			}

			void IEnumerator.Reset()
			{
				m_index = 0;
				m_current = default;
			}

			/// <inheritdoc />
			public readonly TValue Current => m_current!;
			
		}

		/// <inheritdoc />
		public int Count => m_array.Count;

		/// <summary>Returns the element at the specified index, converted into type <typeparamref name="TValue"/></summary>
		/// <remarks>Returns <see langword="null"/> if the index is out of bounds, or if the value is null or missing</remarks>
		public TValue? this[int index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_array.Get<TValue?>(index, m_missing);
		}

		/// <summary>Returns the element at the specified index, converted into type <typeparamref name="TValue"/></summary>
		/// <remarks>Returns <see langword="null"/> if the index is out of bounds, or if the value is null or missing</remarks>
		public TValue? this[Index index]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_array.Get<TValue?>(index, m_missing);
		}

		/// <summary>Returns an array with all the elements, converted into type <typeparamref name="TValue"/>.</summary>
		/// <remarks>Any element that is null or missing will be <see langword="null"/> in the returned array.</remarks>
		[Pure]
		public TValue?[] ToArray()
		{
			if (m_array.Count == 0)
			{
				return [ ];
			}

			if (m_missing is null || default(TValue) is not null && EqualityComparer<TValue>.Default.Equals(m_missing, default))
			{ // use the optimized variant
				return m_array.ToArray<TValue?>();
			}

			var items = m_array.AsSpan();
			var res = new TValue?[items.Length];

			for (int i = 0; i < items.Length; i++)
			{
				res[i] = items[i].As<TValue>(m_missing);
			}

			return res;
		}

		/// <summary>Returns a <see cref="List{TValue}"/> with all the elements, converted into type <typeparamref name="TValue"/>.</summary>
		/// <remarks>Any element that is null or missing will be <see langword="null"/> in the returned array.</remarks>
		[Pure]
		public List<TValue?> ToList()
		{
			if (m_array.Count == 0)
			{
				return [];
			}

			if (m_missing is null || default(TValue) is not null && EqualityComparer<TValue>.Default.Equals(m_missing, default))
			{ // use the optimized variant
				return m_array.ToList<TValue?>();
			}

			var items = m_array.AsSpan();
			var res = new List<TValue?>(items.Length);

			foreach (var item in items)
			{
				res.Add(item.As<TValue>(m_missing));
			}

			return res;
		}

	}

}
