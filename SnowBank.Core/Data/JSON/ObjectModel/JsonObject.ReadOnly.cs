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

#define CHECK_INVARIANTS

namespace SnowBank.Data.Json
{
	using System.Collections.Generic;
	using System.ComponentModel;

	public partial class JsonObject
	{

		/// <summary>Operations for <b>read-only</b> JSON objects</summary>
		[SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
		[PublicAPI]
		public new static class ReadOnly
		{

			/// <summary>Returns an empty, read-only, <see cref="JsonObject">JSON Object</see> singleton</summary>
			/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
			public static readonly JsonObject Empty = new(new(0, StringComparer.Ordinal), readOnly: true);

			#region Create...

			/// <summary>Returns a <b>read-only</b> empty object, that cannot be modified</summary>
			/// <remarks>
			/// <para>This method will always return the same <see cref="JsonObject.ReadOnly.Empty"/> singleton.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create()"/></para>
			/// </remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Create() => JsonObject.ReadOnly.Empty;

			/// <summary>Creates a new empty read-only JSON object</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <remarks>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string}?)"/></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Create(IEqualityComparer<string>? comparer)
			{
				comparer ??= StringComparer.Ordinal;
				return ReferenceEquals(comparer, StringComparer.Ordinal) ? JsonObject.ReadOnly.Empty : new(new(0, comparer), readOnly: true);
			}

			/// <summary>Creates a new immutable JSON object with a single field</summary>
			/// <param name="key0">Name of the field</param>
			/// <param name="value0">Value of the field</param>
			/// <returns>JSON object of size 1, that cannot be modified.</returns>
			/// <remarks>For a mutable object, see <see cref="JsonObject.Create(string,JsonValue?)"/></remarks>
			[Pure]
			public static JsonObject Create(string key0, JsonValue? value0) => new(new(1, StringComparer.Ordinal)
			{
				[key0] = (value0 ?? JsonNull.Null).ToReadOnly()
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with a single field</summary>
			/// <param name="item">Name and value of the field</param>
			/// <returns>JSON object of size 1, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			public static JsonObject Create((string Key, JsonValue? Value) item) => new(new(1, StringComparer.Ordinal)
			{
				[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly()
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 2 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public static JsonObject Create((string Key, JsonValue? Value) item1, (string Key, JsonValue? Value) item2) => new(new(2, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 3 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <param name="item3">Name and value of the third field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.ReadOnly.Create([ (k, v), ... ]) instead.")]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public static JsonObject Create(
				(string Key, JsonValue? Value) item1,
				(string Key, JsonValue? Value) item2,
				(string Key, JsonValue? Value) item3
			) => new(new(3, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
				[item3.Key] = (item3.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new immutable JSON object with 4 fields</summary>
			/// <param name="item1">Name and value of the first field</param>
			/// <param name="item2">Name and value of the second field</param>
			/// <param name="item3">Name and value of the third field</param>
			/// <param name="item4">Name and value of the fourth field</param>
			/// <returns>JSON object of size 2, that cannot be modified.</returns>
			/// <remarks>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?},ValueTuple{string,JsonValue?})"/></para>
			/// </remarks>
			[Pure]
			[Obsolete("Please use the JsonObject.ReadOnly.Create([ (k, v), ... ]) instead.")]
			[EditorBrowsable(EditorBrowsableState.Never)]
			public static JsonObject Create(
				(string Key, JsonValue? Value) item1,
				(string Key, JsonValue? Value) item2,
				(string Key, JsonValue? Value) item3,
				(string Key, JsonValue? Value) item4
			) => new(new(4, StringComparer.Ordinal)
			{
				[item1.Key] = (item1.Value ?? JsonNull.Null).ToReadOnly(),
				[item2.Key] = (item2.Value ?? JsonNull.Null).ToReadOnly(),
				[item3.Key] = (item3.Value ?? JsonNull.Null).ToReadOnly(),
				[item4.Key] = (item4.Value ?? JsonNull.Null).ToReadOnly(),
			}, readOnly: true);

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IDictionary{string,JsonValue})"/></para>
			/// </remarks>
			public static JsonObject Create(IDictionary<string, JsonValue> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null, items).AddRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ReadOnlySpan{KeyValuePair{string,JsonValue}})"/></para>
			/// </remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Create(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
			{
				return items.Length == 0 ? Empty : CreateEmptyWithComparer(null).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},ReadOnlySpan{KeyValuePair{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
			{
				return CreateEmptyWithComparer(comparer).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ReadOnlySpan{ValueTuple{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(ReadOnlySpan<(string Key, JsonValue? Value)> items)
			{
				return items.Length == 0 ? Empty : CreateEmptyWithComparer(null).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},ReadOnlySpan{ValueTuple{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, ReadOnlySpan<(string Key, JsonValue? Value)> items)
			{
				return CreateEmptyWithComparer(comparer).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(KeyValuePair{string,JsonValue}[])"/></para>
			/// </remarks>
			public static JsonObject Create(KeyValuePair<string, JsonValue>[] items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null).SetRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},KeyValuePair{string,JsonValue}[])"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, KeyValuePair<string, JsonValue>[] items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).SetRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(ValueTuple{string,JsonValue}[])"/></para>
			/// </remarks>
			public static JsonObject Create((string Key, JsonValue?)[] items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null).SetRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},ValueTuple{string,JsonValue}[])"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, (string Key, JsonValue?)[] items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).SetRangeReadOnly(items.AsSpan()).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEnumerable{KeyValuePair{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEnumerable<KeyValuePair<string, JsonValue>> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null, items).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},IEnumerable{KeyValuePair{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, JsonValue>> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer, items).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEnumerable{ValueTuple{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEnumerable<(string Key, JsonValue Value)> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null).SetRangeReadOnly(items).FreezeUnsafe();
			}

			/// <summary>Creates a new JSON object with the specified items</summary>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Map of key/values to copy</param>
			/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
			/// <remarks>
			/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
			/// <para>For a mutable object, see <see cref="JsonObject.Create(IEqualityComparer{string},IEnumerable{ValueTuple{string,JsonValue}})"/></para>
			/// </remarks>
			public static JsonObject Create(IEqualityComparer<string> comparer, IEnumerable<(string Key, JsonValue Value)> items)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer).SetRangeReadOnly(items).FreezeUnsafe();
			}

			#endregion

			#region Parse...

			// these are just alias to JsonValue.ReadOnly.ParseObject(...)

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseObject(string?,CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Parse(
#if NET8_0_OR_GREATER
				[StringSyntax(StringSyntaxAttribute.Json)]
#endif
				string? jsonText,
				CrystalJsonSettings? settings = null
			) => JsonValue.ReadOnly.ParseObject(jsonText, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseObject(ReadOnlySpan{char},CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Parse(
#if NET8_0_OR_GREATER
				[StringSyntax(StringSyntaxAttribute.Json)]
#endif
				ReadOnlySpan<char> jsonText,
				CrystalJsonSettings? settings = null
			) => JsonValue.ReadOnly.ParseObject(jsonText, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseObject(ReadOnlySpan{byte},CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
				=> JsonValue.ReadOnly.ParseObject(jsonBytes, settings);

			/// <inheritdoc cref="JsonValue.ReadOnly.ParseObject(Slice,CrystalJsonSettings?)"/>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static JsonObject Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
				=> JsonValue.ReadOnly.ParseObject(jsonBytes, settings);

			#endregion

			#region FromValues...

			/// <summary>Creates a read-only JSON Object from a list of key/value pairs.</summary>
			/// <typeparam name="TValue"></typeparam>
			/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(null, items).AddValuesReadOnly(items, settings, resolver).FreezeUnsafe();
			}

			/// <summary>Creates a read-only JSON Object from a list of key/value pairs.</summary>
			/// <typeparam name="TValue"></typeparam>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(items);
				return CreateEmptyWithComparer(comparer, items).AddValuesReadOnly(items, settings, resolver).FreezeUnsafe();
			}

			/// <summary>Creates a read-only JSON Object from an existing dictionary, using a custom JSON converter.</summary>
			/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
			/// <param name="members">Dictionary that must be converted.</param>
			/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector)
			{
				Contract.NotNull(members);
				Contract.NotNull(valueSelector);

				var comparer = ExtractKeyComparer(members) ?? StringComparer.Ordinal;

				var items = new Dictionary<string, JsonValue>(members.Count, comparer);
				foreach (var kvp in members)
				{
					items.Add(kvp.Key, (valueSelector(kvp.Value) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(items, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from an existing dictionary, using a custom JSON converter.</summary>
			/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="members">Dictionary that must be converted.</param>
			/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
			/// <returns>Corresponding JSON object, that cannot be modified.</returns>
			public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector)
			{
				Contract.NotNull(comparer);
				Contract.NotNull(members);
				Contract.NotNull(valueSelector);

				var items = new Dictionary<string, JsonValue>(members.Count, comparer);
				foreach (var kvp in members)
				{
					items.Add(kvp.Key, (valueSelector(kvp.Value) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(items, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding JSON value.</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector)
			{
				Contract.NotNull(source);
				Contract.NotNull(keySelector);
				Contract.NotNull(valueSelector);

				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, StringComparer.Ordinal);
				foreach (var item in source)
				{
					map.Add(keySelector(item), (valueSelector(item) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(map, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding JSON value.</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement>(IEqualityComparer<string> comparer, IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector)
			{
				Contract.NotNull(comparer);
				Contract.NotNull(source);
				Contract.NotNull(keySelector);
				Contract.NotNull(valueSelector);
				
				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
				foreach (var item in source)
				{
					map.Add(keySelector(item), (valueSelector(item) ?? JsonNull.Missing).ToReadOnly());
				}
				return new(map, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector)
			{
				Contract.NotNull(source);
				Contract.NotNull(keySelector);
				Contract.NotNull(valueSelector);

				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, StringComparer.Ordinal);
				var context = new CrystalJsonDomWriter.VisitingContext();
				foreach (var item in source)
				{
					var child = FromValue(CrystalJsonDomWriter.DefaultReadOnly, ref context, valueSelector(item));
					Contract.Debug.Assert(child.IsReadOnly);
					map.Add(keySelector(item), child);
				}
				return new(map, readOnly: true);
			}

			/// <summary>Creates a read-only JSON Object from a sequence of elements, using a custom key and value selector.</summary>
			/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
			/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
			/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
			/// <param name="source">Sequence of elements to convert</param>
			/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
			/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns>Corresponding JSON object, that cannot be modified</returns>
			public static JsonObject FromValues<TElement, TValue>(IEqualityComparer<string> comparer, IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				Contract.NotNull(comparer);
				Contract.NotNull(source);
				Contract.NotNull(keySelector);
				Contract.NotNull(valueSelector);

				var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
				var context = new CrystalJsonDomWriter.VisitingContext();
				var writer = CrystalJsonDomWriter.CreateReadOnly(settings, resolver);
				foreach (var item in source)
				{
					var child = FromValue(writer, ref context, valueSelector(item));
					Contract.Debug.Assert(child.IsReadOnly);
					map.Add(keySelector(item), child);
				}
				return new(map, readOnly: true);
			}

			#endregion

			/// <summary>Converts an instance of type <typeparamref name="TValue"/> into the equivalent read-only JSON Object.</summary>
			/// <typeparam name="TValue">Publicly known type of the instance.</typeparam>
			/// <param name="value">Instance to convert.</param>
			/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
			/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
			/// <returns>Corresponding immutable JSON Object, or <see langword="null"/> if <paramref name="value"/> is null</returns>
			/// <remarks>The JSON Object that is returned is read-only, and can safely be cached or shared. If you need a mutable instance, consider calling <see cref="JsonObject.FromObject{TValue}"/> instead.</remarks>
			[return: NotNullIfNotNull(nameof(value))]
			public static JsonObject? FromObject<TValue>(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			{
				return CrystalJsonDomWriter.CreateReadOnly(settings, resolver).ParseObject(value, typeof(TValue)).AsObjectOrDefault();
			}

			/// <summary>Converts an untyped dictionary into a JSON Object</summary>
			/// <returns>Corresponding immutable JSON Object</returns>
			/// <remarks>This should only be used to interface with legacy APIs that generate a <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, object></see>.</remarks>
			[EditorBrowsable(EditorBrowsableState.Advanced)]
			public static JsonObject CreateBoxed(IDictionary<string, object> members)
			{
				Contract.NotNull(members);

				var map = new Dictionary<string, JsonValue>(members.Count, ExtractKeyComparer(members) ?? StringComparer.Ordinal);
				foreach (var kvp in members)
				{
					map.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value));
				}
				return new JsonObject(map, readOnly: true);
			}
		
			/// <summary>Converts an untyped dictionary into a JSON Object</summary>
			/// <returns>Corresponding immutable JSON Object</returns>
			/// <remarks>This should only be used to interface with legacy APIs that generate a <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, object></see>.</remarks>
			[EditorBrowsable(EditorBrowsableState.Advanced)]
			public static JsonObject CreateBoxed(IEqualityComparer<string> comparer, IDictionary<string, object> members)
			{
				Contract.NotNull(comparer);
				Contract.NotNull(members);

				var map = new Dictionary<string, JsonValue>(members.Count, comparer);
				foreach (var kvp in members)
				{
					map.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value));
				}
				return new JsonObject(map, readOnly: true);
			}

		}

		#region CopyAndXYZ()...

		private static void MakeReadOnly(Dictionary<string, JsonValue> items)
		{
#if NET5_0_OR_GREATER
			foreach (var kv in items)
			{
				if (!kv.Value.IsReadOnly)
				{
					items[kv.Key] = kv.Value.ToReadOnly();
				}
			}
#else
			// overwriting an existing key while enumerating is only tolerated by the modern Dictionary
			// (the netfx version-checks ANY write): collect the keys to convert first (extra allocation)
			List<string>? toConvert = null;
			foreach (var kv in items)
			{
				if (!kv.Value.IsReadOnly)
				{
					(toConvert ??= [ ]).Add(kv.Key);
				}
			}
			if (toConvert != null)
			{
				foreach (var key in toConvert)
				{
					items[key] = items[key].ToReadOnly();
				}
			}
#endif
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, an exception will be thrown.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndAdd(string key, JsonValue? value)
		{
			// copy and add the new value
			var items = new Dictionary<string, JsonValue>(m_items);
#if !NET5_0_OR_GREATER
			// the netfx duplicate-key exception does not include the key name in its message: mirror the modern BCL message
			if (items.ContainsKey(key)) throw new ArgumentException($"An item with the same key has already been added. Key: {key}", nameof(key));
#endif
			items.Add(key, value?.ToReadOnly() ?? JsonNull.Null);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, an exception will be thrown.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject CopyAndAdd(ReadOnlySpan<char> key, JsonValue? value)
		{
			// copy and add the new value
			var items = new Dictionary<string, JsonValue>(m_items);
#if NET9_0_OR_GREATER
			// note: there is no .Add() in alternate lookups as of .NET 10 :(
			if (!items.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(key, value?.ToReadOnly() ?? JsonNull.Null))
			{
				throw new ArgumentException($"An item with the same key has already been added. Key: {key.ToString()}", nameof(key));
			}
#else
			items.Add(key.ToString(), value?.ToReadOnly() ?? JsonNull.Null);
#endif

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the new item</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, an exception will be thrown.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject CopyAndAdd(ReadOnlyMemory<char> key, JsonValue? value)
		{
#if NET9_0_OR_GREATER
			return key.TryGetString(out var str)
				? CopyAndAdd(str, value)
				: CopyAndAdd(key.Span, value);
#else
			return CopyAndAdd(key.GetStringOrCopy(), value);
#endif
		}

		/// <summary>Replaces a published JSON Object with a new version with an added field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, an exception will be thrown.</param>
		/// <param name="value">Value of the field to add</param>
		/// <returns>New published JSON Object, that includes the new field.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndAdd(ref JsonObject original, string key, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndAdd(key, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndAddSpin(ref original, key, value);

			static JsonObject CopyAndAddSpin(ref JsonObject original, string key, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndAdd(key, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this object with an additional item</summary>
		/// <param name="key">Name of the field to add. If a field with the same name already exists, the method will return <see langword="false"/>.</param>
		/// <param name="value">Value of the new item</param>
		/// <param name="copy">Receives a new instance with the same content of the original object, plus the additional item</param>
		/// <returns><see langword="true"/> if the field was added, or <see langword="false"/> if there was already a field with the same name.</returns>
		/// <remarks>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public bool TryCopyAndAdd(string key, JsonValue? value, [MaybeNullWhen(false)] out JsonObject copy)
		{
			if (m_items.ContainsKey(key))
			{
				copy = null;
				return false;
			}

			// copy and add the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			items.Add(key, value?.ToReadOnly() ?? JsonNull.Null);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			copy = new(items, readOnly: true);
			return true;
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the new field</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten.</para>
		/// <para>If <paramref name="value"/> is <see cref="JsonNull.Missing"/>, the field will be <i>removed</i>.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndSet(string key, JsonValue? value)
		{
			// copy and set the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			if (ReferenceEquals(value, JsonNull.Missing))
			{
				items.Remove(key);
			}
			else
			{
				items[key] = value?.ToReadOnly() ?? JsonNull.Null;
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the new field</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten.</para>
		/// <para>If <paramref name="value"/> is <see cref="JsonNull.Missing"/>, the field will be <i>removed</i>.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject CopyAndSet(ReadOnlySpan<char> key, JsonValue? value)
		{
#if !NET9_0_OR_GREATER
			return CopyAndSet(key.ToString(), value);
#else
			// copy and set the new value
			var items = new Dictionary<string, JsonValue>(m_items);
			var alternate = items.GetAlternateLookup<ReadOnlySpan<char>>();
			if (ReferenceEquals(value, JsonNull.Missing))
			{
				alternate.Remove(key);
			}
			else
			{
				alternate[key] = value?.ToReadOnly() ?? JsonNull.Null;
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
#endif
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the new field</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public JsonObject CopyAndSet(ReadOnlyMemory<char> key, JsonValue? value)
		{
#if NET9_0_OR_GREATER
			return key.TryGetString(out var str)
				? CopyAndSet(str, value)
				: CopyAndSet(key.Span, value);
#else
			return CopyAndSet(key.GetStringOrCopy(), value);
#endif
		}

		/// <summary>Replaces a published JSON Object with a new version with an added field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to set. If a field with the same name already exists, its previous value will be overwritten.</param>
		/// <param name="value">Value of the field.</param>
		/// <returns>New published JSON Object, that includes the new field.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndSet(ref JsonObject original, string key, JsonValue? value)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndSet(key, value);

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndSetSpin(ref original, key, value);

			static JsonObject CopyAndSetSpin(ref JsonObject original, string key, JsonValue? value)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndSet(key, value);
					if (ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot)))
					{
						return copy;
					}
				}
			}
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="key">Name of the new field</param>
		/// <param name="value">Value of the new field</param>
		/// <param name="previous">If the field was already present, receives its previous value. If not, receives <see langword="null"/>.</param>
		/// <returns>A new instance with the same content of the original object, plus the additional item</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten and the previous value will be stored in <paramref name="previous"/>.</para>
		/// <para>If <paramref name="value"/> is <see cref="JsonNull.Missing"/>, the field will be <i>removed</i>.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		[Pure, MustUseReturnValue]
		public JsonObject CopyAndSet(string key, JsonValue? value, out JsonValue? previous)
		{
			var items = new Dictionary<string, JsonValue>(m_items);

			// get the previous value if it exists
			items.TryGetValue(key, out previous);
			// set the new value
			if (ReferenceEquals(value, JsonNull.Missing))
			{
				items.Remove(key);
			}
			else
			{
				items[key] = value?.ToReadOnly() ?? JsonNull.Null;
			}

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object, with an additional field</summary>
		/// <param name="path">Path to the field</param>
		/// <param name="value">Value of the new field</param>
		/// <returns>A new instance with the same content of the original object, plus the additional field</returns>
		/// <remarks>
		/// <para>If a field with the same name already exists, its value will be overwritten.</para>
		/// <para>If <paramref name="value"/> is <see cref="JsonNull.Missing"/>, the field will be <i>removed</i>.</para>
		/// <para>If the object was not-readonly, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already-readonly objects, and with read-only values.</para>
		/// </remarks>
		public JsonObject CopyAndSet(JsonPath path, JsonValue? value) => CopyAndPatch(path, value ?? JsonNull.Null, remove: false);

		internal JsonObject CopyAndPatch(JsonPath path, JsonValue value, bool remove)
		{
			JsonObject copy = this.Copy();
			JsonValue current = copy;
			var prevSegment = JsonPathSegment.Empty;
			var prevNode = current;
			foreach (var (_, segment, last) in path.Tokenize())
			{
				if (current.IsNullOrMissing())
				{ // materialize the missing parent
					// we can use the fact that segment is a field or index as a hint on whether the parent should be an object or an array
					if (segment.IsName())
					{
						current = new JsonObject();
					}
					else
					{
						current = new JsonArray();
					}

					if (prevSegment.IsEmpty())
					{
						copy = (JsonObject) current;
					}
					else
					{
						prevNode[prevSegment] = current;
					}
				}

				if (last)
				{
					if (segment.TryGetName(out var name))
					{
						if (remove)
						{
							((JsonObject) current).Remove(name);
						}
						else
						{
							current[name] = value;
						}
					}
					else if (segment.TryGetIndex(out var index))
					{
						if (remove)
						{
							((JsonArray) current).RemoveAt(index);
						}
						else
						{
							current[index] = value;
						}
					}
				}
				else
				{
					prevNode = current;
					prevSegment = segment;
					var next = current[segment];
					current = next;
				}
			}

			return copy.Freeze();
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(string key)
		{
			Contract.NotNull(key);

			var items = m_items;
			if (!items.ContainsKey(key))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return JsonObject.ReadOnly.Empty;
			}

			// copy and remove
			items = new(items);
			items.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(ReadOnlySpan<char> key)
		{
#if !NET9_0_OR_GREATER
			// we will have to allocate in all cases, so we should probably call the string overload
			return CopyAndRemove(key.ToString());
#else
			var items = m_items;
			var alternate = items.GetAlternateLookup<ReadOnlySpan<char>>();
			if (!alternate.ContainsKey(key))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return JsonObject.ReadOnly.Empty;
			}

			// copy and remove
			items = new(items);
			alternate.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
#endif
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(ReadOnlyMemory<char> key)
		{
#if NET9_0_OR_GREATER
			return key.TryGetString(out var str)
				? CopyAndRemove(str)
				: CopyAndRemove(key.Span);
#else
			// we will have to allocate in all cases, so we should probably call the string overload
			return CopyAndRemove(key.ToString());
#endif
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="path">Path of the field to remove from the copy</param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(JsonPath path) => CopyAndPatch(path, JsonNull.Missing, remove: true);

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <param name="previous"></param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(string key, out JsonValue? previous)
		{
			Contract.NotNull(key);

			var items = m_items;
			if (!items.TryGetValue(key, out previous))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return JsonObject.ReadOnly.Empty;
			}

			// copy and remove
			items = new(items);
			items.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <param name="previous"></param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(ReadOnlySpan<char> key, out JsonValue? previous)
		{
#if !NET9_0_OR_GREATER
			// we will have to allocate in all cases, so we should probably call the string overload
			return CopyAndRemove(key.ToString(), out previous);
#else
			var items = m_items;
			var alternate = items.GetAlternateLookup<ReadOnlySpan<char>>();
			if (!alternate.TryGetValue(key, out previous))
			{ // the key does not exist so there will be no changes
				return m_readOnly ? this : ToReadOnly();
			}

			if (items.Count == 1)
			{ // we already now key is contained in the object, so if it's the only one, the object will become empty.
				return JsonObject.ReadOnly.Empty;
			}

			// copy and remove
			items = new(items);
			alternate.Remove(key);

			if (!m_readOnly)
			{ // some existing items may not be readonly, we may have to convert them as well
				MakeReadOnly(items);
			}

			return new(items, readOnly: true);
#endif
		}

		/// <summary>Returns a new read-only copy of this object without the specified item</summary>
		/// <param name="key">Name of the field to remove from the copy</param>
		/// <param name="previous"></param>
		/// <returns>A new instance with the same content of the original object, but with the specified item removed.</returns>
		/// <remarks>
		/// <para>If the object was not read-only, existing non-readonly fields will also be converted to read-only.</para>
		/// <para>For best performances, this should only be used on already read-only objects.</para>
		/// </remarks>
		public JsonObject CopyAndRemove(ReadOnlyMemory<char> key, out JsonValue? previous)
		{
#if NET9_0_OR_GREATER
			return key.TryGetString(out var str)
				? CopyAndRemove(str, out previous)
				: CopyAndRemove(key.Span, out previous);
#else
			// we will have to allocate in all cases, so we should probably call the string overload
			return CopyAndRemove(key.ToString(), out previous);
#endif
		}

		/// <summary>Replaces a published JSON Object with a new version without the specified field, in a thread-safe manner, using a <see cref="SpinWait"/> if necessary.</summary>
		/// <param name="original">Reference to the currently published JSON Object</param>
		/// <param name="key">Name of the field to remove. If the field was not present, the object will not be changed.</param>
		/// <returns>New published JSON Object without the field, or the original object if the was not present.</returns>
		/// <remarks>
		/// <para>This method will attempt to atomically replace the original JSON Object with a new version, unless another thread was able to update it faster, in which case it will simply retry with the newest version, until it is able to successfully update the reference.</para>
		/// <para>Caution: the order of operation between threads is not guaranteed, and this method _may_ loop infinitely if it is perpetually blocked by another, faster, thread !</para>
		/// </remarks>
		public static JsonObject CopyAndRemove(ref JsonObject original, string key)
		{
			var snapshot = Volatile.Read(ref original);
			var copy = snapshot.CopyAndRemove(key);
			if (ReferenceEquals(copy, snapshot))
			{ // the field did not exist
				return snapshot;
			}

			return ReferenceEquals(snapshot, Interlocked.CompareExchange(ref original, copy, snapshot))
				? copy
				: CopyAndRemoveSpin(ref original, key);

			static JsonObject CopyAndRemoveSpin(ref JsonObject original, string key)
			{
				var spinner = new SpinWait();
				while (true)
				{
					spinner.SpinOnce();
					var snapshot = Volatile.Read(ref original);
					var copy = snapshot.CopyAndRemove(key);
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
