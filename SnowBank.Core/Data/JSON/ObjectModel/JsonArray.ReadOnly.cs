#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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

namespace SnowBank.Data.Json
{
	using System.Collections.Generic;

	public sealed partial class JsonArray
	{

		/// <summary>Operations for <b>read-only</b> JSON arrays</summary>
		[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
		[PublicAPI]
		public new static class ReadOnly
		{

			/// <summary>Returns an empty, read-only, <see cref="JsonArray">JSON Array</see> singleton</summary>
			/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
			public static readonly JsonArray Empty = new([], 0, readOnly: true);

			#region Create...

			/// <summary>Returns a <b>read-only</b> empty array, that cannot be modified</summary>
			/// <remarks>
			/// <para>This method will always return <see cref="JsonArray.ReadOnly.Empty"/> singleton.</para>
			/// <para>For a mutable array, see <see cref="JsonArray.Create()"/></para>
			/// </remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[OverloadResolutionPriority(1)]
			public static JsonArray Create() => JsonArray.ReadOnly.Empty;

			/// <summary>Creates a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> with a single element</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(JsonValue?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[OverloadResolutionPriority(1)]
			public static JsonArray Create(JsonValue? value) => new([
				(value ?? JsonNull.Null).ToReadOnly()
			], 1, readOnly: true);

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> with 2 elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(JsonValue?,JsonValue?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[OverloadResolutionPriority(1)]
			public static JsonArray Create(JsonValue? value1, JsonValue? value2) => new([
				(value1 ?? JsonNull.Null).ToReadOnly(),
				(value2 ?? JsonNull.Null).ToReadOnly()
			], 2, readOnly: true);

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> with 3 elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(JsonValue?,JsonValue?,JsonValue?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[OverloadResolutionPriority(1)]
			public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3) => new([
				(value1 ?? JsonNull.Null).ToReadOnly(),
				(value2 ?? JsonNull.Null).ToReadOnly(),
				(value3 ?? JsonNull.Null).ToReadOnly()
			], 3, readOnly: true);

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> with 4 elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(JsonValue?,JsonValue?,JsonValue?,JsonValue?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[OverloadResolutionPriority(1)]
			public static JsonArray Create(JsonValue? value1, JsonValue? value2, JsonValue? value3, JsonValue? value4) => new([
				(value1 ?? JsonNull.Null).ToReadOnly(),
				(value2 ?? JsonNull.Null).ToReadOnly(),
				(value3 ?? JsonNull.Null).ToReadOnly(),
				(value4 ?? JsonNull.Null).ToReadOnly()
			], 4, readOnly: true);

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> from an array of elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(JsonValue?[])"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray Create(params JsonValue?[] values)
			{
				return Create(new ReadOnlySpan<JsonValue?>(Contract.ValueNotNull(values)));
			}

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> from a span of elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(ReadOnlySpan{JsonValue?})"/></remarks>
			[Pure]
#if NET9_0_OR_GREATER
			public static JsonArray Create(params ReadOnlySpan<JsonValue?> values)
#else
			public static JsonArray Create(ReadOnlySpan<JsonValue?> values)
#endif
			{
				// <JIT_HACK>
				// In the case where the call-site is constructing the span using a collection express: var xs = JsonArray.ReadOnly.Create([ "hello", "world" ])
				// the JIT can optimize the method, since it knows that values.Length == 2, and can safely remove the test and the rest of the method, as well as inline the ctor.
				// => using JIT disassembly, we can see that the whole JSON Array construction will be completely inlined in the caller's method body

				// note: currently (.NET 9 preview), the JIT will not elide the null-check on the values, even if they are known to be not-null.

				switch (values.Length)
				{
					case 0:
					{
						return JsonArray.ReadOnly.Empty;
					}
					case 1:
					{
						return new([
							(values[0] ?? JsonNull.Null).ToReadOnly()
						], 1, readOnly: true);
					}
					case 2:
					{
						return new([
							(values[0] ?? JsonNull.Null).ToReadOnly(),
							(values[1] ?? JsonNull.Null).ToReadOnly()
						], 2, readOnly: true);
					}
					case 3:
					{
						return new([
							(values[0] ?? JsonNull.Null).ToReadOnly(),
							(values[1] ?? JsonNull.Null).ToReadOnly(),
							(values[2] ?? JsonNull.Null).ToReadOnly()
						], 3, readOnly: true);
					}
				}

				// </JIT_HACK>

				var buf = new JsonValue[values.Length];
				for (int i = 0; i < buf.Length; i++)
				{
					buf[i] = (values[i] ?? JsonNull.Null).ToReadOnly();
				}
				return new JsonArray(buf, buf.Length, readOnly: true);
			}

			//note: we only add this for .NET9+ because we require overload resolution priority to be able to fix ambiguous calls between IEnumerable<> en ReadOnlySpan<>

			/// <summary>Create a new <b>read-only</b> <see cref="JsonArray">JSON Array</see> from a sequence of elements</summary>
			/// <remarks>For a mutable array, see <see cref="JsonArray.Create(IEnumerable{JsonValue?})"/></remarks>
			[Pure, OverloadResolutionPriority(-1)]
			public static JsonArray Create(IEnumerable<JsonValue?> values)
			{
				Contract.NotNull(values);

				return values.TryGetNonEnumeratedCount(out var count) && count == 0
					? JsonArray.ReadOnly.Empty
					: new JsonArray().AddRangeReadOnly(values).FreezeUnsafe();
			}

			#endregion

			#region Parse...

			// these are just alias to JsonValue.ReadOnly.ParseArray(...)

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseArray(string?,CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray Parse(
#if NET8_0_OR_GREATER
				[StringSyntax(StringSyntaxAttribute.Json)]
#endif
				string? jsonText,
				CrystalJsonSettings? settings = null
			) => JsonValue.ReadOnly.ParseArray(jsonText, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseArray(ReadOnlySpan{char},CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray Parse(
#if NET8_0_OR_GREATER
				[StringSyntax(StringSyntaxAttribute.Json)]
#endif
				ReadOnlySpan<char> jsonText,
				CrystalJsonSettings? settings = null
			) => JsonValue.ReadOnly.ParseArray(jsonText, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseArray(ReadOnlySpan{byte},CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
				=> JsonValue.ReadOnly.ParseArray(jsonBytes, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseArray(Slice,CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
				=> JsonValue.ReadOnly.ParseArray(jsonBytes, settings);

			#endregion

			#region FromValues...

			/// <summary>Creates a new <b>read-only</b> JSON Array from a span of raw values.</summary>
			/// <typeparam name="TValue">Type of the values</typeparam>
			/// <param name="values">Span of values that must be converted</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TValue}(ReadOnlySpan{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray FromValues<TValue>(ReadOnlySpan<TValue> values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return values.Length == 0 ? JsonArray.ReadOnly.Empty : new JsonArray().AddValuesReadOnly<TValue>(values, settings.AsReadOnly(), resolver).FreezeUnsafe();
			}

			/// <summary>Creates a new <b>read-only</b> JSON Array from an array of raw values.</summary>
			/// <typeparam name="TValue">Type of the values</typeparam>
			/// <param name="values">Array of values that must be converted</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TValue}(TValue[],CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: NotNullIfNotNull(nameof(values))]
			public static JsonArray? FromValues<TValue>(TValue[]? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (values is null) return null;
				return values.Length == 0 ? JsonArray.ReadOnly.Empty : new JsonArray().AddValuesReadOnly<TValue>(new ReadOnlySpan<TValue>(values), settings.AsReadOnly(), resolver).FreezeUnsafe();
			}

			/// <summary>Creates a new <b>read-only</b> JSON Array from a sequence of raw values.</summary>
			/// <typeparam name="TValue">Type of the values</typeparam>
			/// <param name="values">Sequence of values that must be converted</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TValue}(IEnumerable{TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: NotNullIfNotNull(nameof(values))]
			public static JsonArray? FromValues<TValue>(IEnumerable<TValue>? values, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				if (values is null) return null;
				return values.TryGetNonEnumeratedCount(out var count) && count == 0
					? JsonArray.ReadOnly.Empty
					: new JsonArray().AddValuesReadOnly<TValue>(values, settings.AsReadOnly(), resolver).FreezeUnsafe();
			}

			/// <summary>Creates a new <b>read-only</b> JSON Array with values extracted from a span of source items.</summary>
			/// <typeparam name="TItem">Type of the source items</typeparam>
			/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
			/// <param name="values">Span of items to convert</param>
			/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TItem,TValue}(ReadOnlySpan{TItem},Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray FromValues<TItem, TValue>(ReadOnlySpan<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(selector);
				return values.Length == 0 ? JsonArray.ReadOnly.Empty : new JsonArray().AddValuesReadOnly(values, selector, settings.AsReadOnly(), resolver).FreezeUnsafe();
			}

			/// <summary>Creates a new <b>read-only</b> JSON Array with values extracted from an array of source items.</summary>
			/// <typeparam name="TItem">Type of the source items</typeparam>
			/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
			/// <param name="values">Array of items to convert</param>
			/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TItem,TValue}(TItem[],Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonArray FromValues<TItem, TValue>(TItem[] values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);
				return values.Length == 0 ? JsonArray.ReadOnly.Empty : new JsonArray().AddValuesReadOnly(new ReadOnlySpan<TItem>(values), selector, settings.AsReadOnly(), resolver).FreezeUnsafe();
			}

			/// <summary>Creates a new <b>read-only</b> JSON Array with values extracted from a sequence of source items.</summary>
			/// <typeparam name="TItem">Type of the source items</typeparam>
			/// <typeparam name="TValue">Type of the values extracted from each item</typeparam>
			/// <param name="values">Sequence of items to convert</param>
			/// <param name="selector">Lambda that will extract a value from each item in the source.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Optional custom resolver used to bind the value into a managed type.</param>
			/// <returns>Immutable JSON Array with all values converted into <b>read-only</b> <see cref="JsonValue"/> instances.</returns>
			/// <remarks>For a mutable array, see <see cref="JsonArray.FromValues{TItem,TValue}(IEnumerable{TItem},Func{TItem,TValue},CrystalJsonSettings?,ICrystalJsonTypeResolver?)"/></remarks>
			[Pure]
			public static JsonArray FromValues<TItem, TValue>(IEnumerable<TItem> values, Func<TItem, TValue> selector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(values);
				return values.TryGetNonEnumeratedCount(out var count) && count == 0
					? JsonArray.ReadOnly.Empty
					: new JsonArray().AddValuesReadOnly(values, selector, settings.AsReadOnly(), resolver).FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
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
				return arr.FreezeUnsafe();
			}

			#endregion

		}

		#region CopyAndXYZ...

		private static void MakeReadOnly(Span<JsonValue> items)
		{
			for (int i = 0; i < items.Length; i++)
			{
				if (!items[i].IsReadOnly)
				{
					items[i] = items[i].ToReadOnly();
				}
			}
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array concatenated with another array</summary>
		/// <param name="tail">Array that will be appended to the end of the new copy</param>
		/// <returns>A new instance with the same content of the original array, plus the content of the tail</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// <para>>To add the array <i>itself</i> as a new item, please use <see cref="CopyAndAdd(JsonValue?)"/> instead.</para>
		/// </remarks>
		/// <seealso cref="CopyAndAdd(JsonValue?)"/>
		[Pure]
		public JsonArray CopyAndConcat(JsonArray tail)
		{
			if (tail.Count == 0)
			{ // nothing to append, but we may not to return a copy !
				return ToReadOnly();
			}

			if (m_size == 0)
			{ // return the tail as readonly
				return tail.ToReadOnly();
			}

			// copy and append the tail
			int newSize = checked(m_size + tail.Count);
			var items = new JsonValue[newSize];

			// copy the original array
			var chunk = items.AsSpan(..m_size);
			this.AsSpan().CopyTo(chunk);
			if (!m_readOnly)
			{
				MakeReadOnly(chunk);
			}

			// copy the tail
			chunk = items.AsSpan(m_size..);
			tail.AsSpan().CopyTo(chunk);
			if (!tail.m_readOnly)
			{
				MakeReadOnly(chunk);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array concatenated with another array</summary>
		/// <param name="tail">Span of items that will be appended to the end of the new copy</param>
		/// <returns>A new instance with the same content of the original array, plus the content of the tail</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		[Pure, OverloadResolutionPriority(1)]
		public JsonArray CopyAndConcat(ReadOnlySpan<JsonValue> tail)
		{
			if (tail.Length == 0)
			{ // nothing to append, but we may not to return a copy !
				return ToReadOnly();
			}

			if (m_size == 0)
			{ // return the tail as readonly
				return JsonArray.ReadOnly.Create(tail);
			}

			// copy and append the tail
			int newSize = checked(m_size + tail.Length);
			var items = new JsonValue[newSize];

			// copy the original array
			var chunk = items.AsSpan(..m_size);
			this.AsSpan().CopyTo(chunk);
			if (!m_readOnly)
			{
				MakeReadOnly(chunk);
			}

			// copy the tail
			chunk = items.AsSpan(m_size..);
			tail.CopyTo(chunk);
			MakeReadOnly(chunk);

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with extra items appended to the end, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="tail">Array that will be appended to the end of the new copy</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new items.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// <para>>To add the array <i>itself</i> as a new item, please use <see cref="CopyAndAdd(ref JsonArray,JsonValue?)"/> instead.</para>
		/// </remarks>
		/// <seealso cref="CopyAndAdd(ref JsonArray,JsonValue?)"/>
		[Pure]
		public static JsonArray CopyAndConcat(ref JsonArray original, JsonArray tail)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndConcat(tail);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndConcatSpin(ref original, tail);

			static JsonArray CopyAndConcatSpin(ref JsonArray original, JsonArray tail)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndConcat(tail);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with extra items appended to the end, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="tail">Array of items that will be appended to the end of the new copy</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new items.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		[Pure]
		public static JsonArray CopyAndConcat(ref JsonArray original, ReadOnlySpan<JsonValue> tail)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndConcat(tail);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndConcatSpin(ref original, tail);

			static JsonArray CopyAndConcatSpin(ref JsonArray original, ReadOnlySpan<JsonValue> tail)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndConcat(tail);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array with an additional item</summary>
		/// <param name="value">Item that will be appended to the end of the new copy</param>
		/// <returns>A new instance with the same content of the original array, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// <para>Please note that is <paramref name="value"/> is a JSON Array, it will be added as a child element. To append the <i>items</i> of an array, please use <see cref="CopyAndConcat(SnowBank.Data.Json.JsonArray)"/> instead</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndAdd(JsonValue? value)
		{
			value = value?.ToReadOnly() ?? JsonNull.Null;

			if (m_size == 0)
			{
				return new JsonArray([value], 1, readOnly: true);
			}

			// copy and add the new value
			int newSize = checked(m_size + 1);
			var items = new JsonValue[newSize];
			this.AsSpan().CopyTo(items);
			items[m_size] = value;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an added item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="value">Value of the field to append</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// <para>Please note that is <paramref name="value"/> is a JSON Array, it will be added as a child element. To append the <i>items</i> of an array, please use <see cref="CopyAndConcat(SnowBank.Data.Json.JsonArray)"/> instead</para>
		/// </remarks>
		public static JsonArray CopyAndAdd(ref JsonArray original, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndAdd(value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndAddSpin(ref original, value);

			static JsonArray CopyAndAddSpin(ref JsonArray original, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndAdd(value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, except the additional item at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(int index, JsonValue? value) => CopyAndSet(index, value, out _);

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, except the additional item at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		public JsonArray CopyAndSet(Index index, JsonValue? value) => CopyAndSet(index.GetOffset(m_size), value, out _);

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with a new item at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndSet(ref JsonArray original, int index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(index, value, out _);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, index, value);

			static JsonArray CopyAndSetSpin(ref JsonArray original, int index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(index, value, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with a new item at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index of the item to modify. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndSet(ref JsonArray original, Index index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(index, value, out _);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, index, value);

			static JsonArray CopyAndSetSpin(ref JsonArray original, Index index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(index, value, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index where to write the new item</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="previous">Receives the previous value at this location, or <see langword="null"/> if the index is outside the bounds of the array.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(int index, JsonValue? value, out JsonValue? previous)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			// copy and set the new value
			JsonValue[] items;
			int newSize;
			if (index < m_size)
			{ // update in place
				items = this.AsSpan().ToArray();
				newSize = m_size;
				previous = items[index];
			}
			else
			{ // update outside the array, must resize
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				var prev = this.AsSpan();
				prev.CopyTo(items);
				if (index > prev.Length)
				{ // fill the gap!
					items.AsSpan(prev.Length, index - prev.Length).Fill(JsonNull.Null);
				}
				previous = null;
			}
			items[index] = value?.ToReadOnly() ?? JsonNull.Null;

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item at the specified location</summary>
		/// <param name="index">Index where to write the new item</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="previous">Receives the previous value at this location, or <see langword="null"/> if the index is outside the bounds of the array.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndSet(Index index, JsonValue? value, out JsonValue? previous) => CopyAndSet(index.GetOffset(m_size), value, out previous);

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item inserted at the specified location</summary>
		/// <param name="index">Index where to insert, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>A new instance with the same content of the original object, with the additional item inserted at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonArray CopyAndInsert(int index, JsonValue? value)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			value = value?.ToReadOnly() ?? JsonNull.Null;

			// copy and set the new value
			JsonValue[] items;
			int newSize;
			if (m_size == 0)
			{ // add to empty array
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				// fill with nulls
				if (index > 0)
				{
					items.AsSpan(0, index).Fill(JsonNull.Null);
				}
				// insert item
				items[index] = value;
			}
			else if (index < m_size)
			{ // insert inside the array, must shift the tail
				newSize = checked(m_size + 1);
				var prev = this.AsSpan();
				items = new JsonValue[newSize];
				// copy head
				prev[..index].CopyTo(items);
				// insert item
				items[index] = value;
				// copy tail
				prev[index..].CopyTo(items.AsSpan(index + 1));
			}
			else
			{ // insert outside the array, must fill gaps with nulls
				newSize = checked(index + 1);
				items = new JsonValue[newSize];
				var prev = this.AsSpan();
				prev.CopyTo(items);
				if (index > prev.Length)
				{ // please, mind the gap!
					items.AsSpan(prev.Length, index - prev.Length).Fill(JsonNull.Null);
				}
				items[index] = value;
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, newSize, readOnly: true);
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array, with a new item inserted at the specified location</summary>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>A new instance with the same content of the original object, with the additional item inserted at the specified location.</returns>
		/// <remarks>
		/// <para>If the array was not-readonly, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays, and with read-only values.</para>
		/// </remarks>
		public JsonArray CopyAndInsert(Index index, JsonValue? value) => CopyAndInsert(index.GetOffset(m_size), value);

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an item inserted at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndInsert(ref JsonArray original, int index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndInsert(index, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndInsertSpin(ref original, index, value);

			static JsonArray CopyAndInsertSpin(ref JsonArray original, int index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndInsert(index, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published <see cref="JsonArray">JSON Array</see> with a new version with an item inserted at the specified location, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published <see cref="JsonArray">JSON Array</see></param>
		/// <param name="index">Index where to insert the item, with all following items shifted to the right. If the array is too small, any gaps will be filled with nulls, and <paramref name="value"/> will be inserted last.</param>
		/// <param name="value">Value to write at this location</param>
		/// <returns>New published <see cref="JsonArray">JSON Array</see>, that includes the new item.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original <see cref="JsonArray">JSON Array</see> with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndInsert(ref JsonArray original, Index index, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndInsert(index, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndInsertSpin(ref original, index, value);

			static JsonArray CopyAndInsertSpin(ref JsonArray original, Index index, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndInsert(index, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array without the specified item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(int index) => CopyAndRemove(index, out _);

		/// <summary>Returns a new <b>read-only</b> copy of this array without the specified item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(Index index) => CopyAndRemove(index.GetOffset(m_size), out _);

		/// <summary>Returns a new <b>read-only</b> copy of this array without the specified item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <param name="previous">Receives the value that was removed, or <see langword="null"/> if the index was outside the bounds of the array</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(int index, out JsonValue? previous)
		{
			if (index < 0) throw new IndexOutOfRangeException("Index of outside the bounds of the JSON Array");

			var prev = this.AsSpan();
			if (index >= prev.Length)
			{ // the index is outside the bounds, no changes
				previous = null;
				return m_readOnly ? this : ToReadOnly();
			}

			if (prev.Length == 1)
			{ // removing the last item
				Contract.Debug.Assert(index == 0);
				previous = prev[0];
				return JsonArray.ReadOnly.Empty;
			}

			// copy and remove
			var items = new JsonValue[prev.Length - 1];
			if (index > 0)
			{ // copy head
				prev[..index].CopyTo(items);
			}
			previous = prev[index];
			if (index < prev.Length - 1)
			{ // copy tail
				prev[(index + 1)..].CopyTo(items.AsSpan(index));
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, items.Length, readOnly: true);
		}

		/// <summary>Returns a new <b>read-only</b> copy of this array without the specified item</summary>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <param name="previous">Receives the value that was removed, or <see langword="null"/> if the index was outside the bounds of the array</param>
		/// <returns>A new instance with the same content of the original array, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the array was not read-only, existing non-readonly items will also be converted to read-only.</para>
		/// <para>For best performance, this should only be used on already read-only arrays.</para>
		/// </remarks>
		public JsonArray CopyAndRemove(Index index, out JsonValue? previous) => CopyAndRemove(index.GetOffset(m_size), out previous);

		/// <summary>Replaces a published JSON Array with a new version without the specified item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Array</param>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>New published JSON Array without the field, or the original array if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Array with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndRemove(ref JsonArray original, int index)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(index, out _);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, index);

			static JsonArray CopyAndRemoveSpin(ref JsonArray original, int index)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(index, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Replaces a published JSON Array with a new version without the specified item, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Array</param>
		/// <param name="index">Index of the location to remove, with all following items shifted to the left.</param>
		/// <returns>New published JSON Array without the field, or the original array if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Array with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonArray CopyAndRemove(ref JsonArray original, Index index)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(index, out _);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, index);

			static JsonArray CopyAndRemoveSpin(ref JsonArray original, Index index)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(index, out _);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		#endregion

	}

}
