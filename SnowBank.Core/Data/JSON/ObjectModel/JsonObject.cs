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

#if DEBUG
#define CHECK_INVARIANTS
#endif

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.Collections.Frozen;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.ComponentModel;
	using System.Dynamic;
	using System.Runtime.InteropServices;
#if NETSTANDARD2_0
	using CollectionsMarshal = SnowBank.Compat.CollectionsMarshalCompat; // shim: CollectionsMarshal does not exist on netstandard2.0
#endif
	using System.Text;
	using SnowBank.Runtime.Converters;
	using SnowBank.Buffers;
	using SnowBank.Text;

	/// <summary>JSON Object with fields</summary>
	[Serializable]
	[DebuggerDisplay("JSON Object[{Count}]{GetMutabilityDebugLiteral(),nq} {GetCompactRepresentation(0),nq}")]
	[DebuggerTypeProxy(typeof(DebugView))]
	[PublicAPI]
#if !NETSTANDARD2_0
	// System.Text.Json interop is disabled on the netstandard2.0 build
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
#endif
	[DebuggerNonUserCode]
#if NET9_0_OR_GREATER
	[CollectionBuilder(typeof(JsonObject), nameof(Create))]
#endif
	public sealed partial class JsonObject : JsonValue, IDictionary<string, JsonValue>, IReadOnlyDictionary<string, JsonValue>, IEquatable<JsonObject>
	{
		// A JSON object can be writable (mutable), or read-only (immutable)
		// - Writable means that items can be added or removed from the "top-level" of this object.
		// - Read-only means that no items can be added or removed, BUT it does not mean that any children is itself readonly!
		// A JSON object can track the mutability of its children, and will maintain a flag it there was at least one mutable children at some point.
		// - We could track and update this state in real-time, but we are mostly interested in keeping track of readonly objects that were immutable from the moment of creation.

		/// <summary>Map of the properties of this object</summary>
		/// <remarks>If <see cref="m_readOnly"/> is not <see langword="0"/>, then any attempt to modify this dictionary should throw an exception</remarks>
		private readonly Dictionary<string, JsonValue> m_items;

		/// <summary>Defines the mutability of this object.</summary>
		/// <remarks>Mutability can change from Immutable to any of the ReadOnlyXYZ variants, but not the over way around!</remarks>
		private bool m_readOnly;

		/// <summary>Returns an empty, read-only, <see cref="JsonObject">JSON Object</see> singleton</summary>
		/// <remarks>This instance cannot be modified, and should be used to reduce memory allocations when working with read-only JSON</remarks>
		[Obsolete("Use JsonObject.ReadOnly.Empty instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject EmptyReadOnly => JsonObject.ReadOnly.Empty;

		#region Debug View...

		[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
		internal sealed class DebugView
		{
			private readonly JsonObject m_obj;

			public DebugView(JsonObject obj)
			{
				m_obj = obj;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public DebugViewItem[] Items
			{
				get
				{
					var tmp = m_obj.ToArray();
					var items = new DebugViewItem[tmp.Length];
					for (int i = 0; i < items.Length; ++i)
					{
						items[i] = new(tmp[i].Key, tmp[i].Value);
					}
					return items;
				}
			}

		}

		[DebuggerDisplay("{Value.GetCompactRepresentation(0),nq}", Name = "[{Key}]")]
		internal readonly struct DebugViewItem
		{
			public DebugViewItem(string key, JsonValue value)
			{
				this.Key = key;
				this.Value = value;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.Never)]
			public string Key { [UsedImplicitly] get; }

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public JsonValue Value { [UsedImplicitly] get; }

		}

		#endregion

		#region Constructors...

		/// <summary>Creates a new JSON object that is empty</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject()
			: this(0, StringComparer.Ordinal)
		{ }

		/// <summary>Creates a new JSON object that is empty and has the specified capacity</summary>
		/// <param name="capacity">The initial number of elements that the <see cref="JsonObject" /> can contain.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(int capacity)
			: this(capacity, StringComparer.Ordinal)
		{ }

		/// <summary>Creates a new JSON object that is empty, and uses the specified <see cref="T:System.Collections.Generic.IEqualityComparer`1" />.</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="StringComparer.Ordinal">ordinal string comparer</see>.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public JsonObject(IEqualityComparer<string>? comparer) //REVIEW: remove this method and force to use ctor(capacity, comparer) instead?
		{
			m_items = new(0, comparer ?? StringComparer.Ordinal);
		}

		public JsonObject(int capacity, IEqualityComparer<string>? comparer)
		{
			Contract.Positive(capacity);
			m_items = new(capacity, comparer ?? StringComparer.Ordinal);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use obj.Copy() to clone an object")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public JsonObject(JsonObject copy)
		{
			Contract.NotNull(copy);

			// we have to create a defensive copy
			m_items = new Dictionary<string, JsonValue>(copy.m_items, copy.Comparer);
			CheckInvariants();
		}

		/// <summary>Creates a new <see cref="JsonObject"/> that will wrap the specified items</summary>
		/// <param name="items">Pre-computed map of elements</param>
		/// <param name="readOnly">If the object is marked as read-only</param>
		/// <remarks>If <paramref name="readOnly"/> is <see langword="true"/> then all elements in <paramref name="items"/> MUST also be read-only!</remarks>
		internal JsonObject(Dictionary<string, JsonValue> items, bool readOnly)
		{
			Contract.Debug.Requires(items is not null);
			m_items = items;
			m_readOnly = readOnly;
			CheckInvariants();
		}

		[Conditional("CHECK_INVARIANTS")]
		private void CheckInvariants()
		{
#if CHECK_INVARIANTS
			if (m_readOnly)
			{
				foreach (var kv in m_items)
				{
					if (!kv.Value.IsReadOnly) Contract.Fail($"Immutable JSON object cannot contain mutable children '{kv.Key}' of type {kv.Value.Type}");
				}
			}
#endif
		}

		/// <summary>Exposes the internal items dictionary used by this object</summary>
		/// <remarks>Should only be used by custom serialization or formatting methods!</remarks>
		internal Dictionary<string, JsonValue> GetItems() => m_items;

		internal static JsonObject CreateEmptyWithComparer(IEqualityComparer<string>? comparer) => new(new(comparer ?? StringComparer.Ordinal), readOnly: false);

		internal static JsonObject CreateEmptyWithComparer<TValue>(IEqualityComparer<string>? comparer, [NoEnumeration] IEnumerable<KeyValuePair<string, TValue>>? items) => new(new(comparer ?? items switch
		{
			null => StringComparer.Ordinal,
			JsonObject obj => obj.m_items.Comparer,
			Dictionary<string, JsonValue> dic => dic.Comparer,
			FrozenDictionary<string, JsonValue> frz => frz.Comparer,
			ImmutableDictionary<string, JsonValue> imm => imm.KeyComparer,
			_ => StringComparer.Ordinal
		}), readOnly: false);

		/// <summary>Capture the KeyComparer used by an existing dictionary</summary>
		internal static IEqualityComparer<string>? ExtractKeyComparer<TValue>([NoEnumeration] IEnumerable<KeyValuePair<string, TValue>> items)
		{
			// if T == JsonObject or T == Dictionary<K,V>, we need to use the same key comparer as the original
			// ReSharper disable once SuspiciousTypeConversion.Global
			// ReSharper disable once ConstantNullCoalescingCondition
			return (items as JsonObject)?.Comparer ?? (items as Dictionary<string, TValue>)?.Comparer;
		}

		/// <summary>Freezes this object, once it has been initialized, by switching it to read-only mode.</summary>
		/// <remarks>Once "frozen", the operation cannot be reverted, and if additional mutation is required, a new copy of the object must be used.</remarks>
#if NET5_0_OR_GREATER
		public override JsonObject Freeze()
#else
		public new JsonObject Freeze()
#endif
		{
			if (!m_readOnly)
			{ // at least one mutable children must be frozen as well!
				foreach (var value in m_items.Values)
				{
					value.Freeze();
				}
				m_readOnly = true;
			}

			CheckInvariants();
			return this;
		}

#if !NET5_0_OR_GREATER
		// forward the JsonValue virtual cores to the strongly typed shadows above (covariant-return emulation)
		protected override JsonValue FreezeCore() => Freeze();
		protected override JsonValue ToReadOnlyCore() => ToReadOnly();
		protected override JsonValue ToMutableCore() => ToMutable();
		protected override JsonValue CopyCore() => Copy();
#endif

		internal JsonObject FreezeUnsafe()
		{
			if (m_items.Count == 0) return JsonObject.ReadOnly.Empty;

			m_readOnly = true;
			CheckInvariants();
			return this;
		}

		/// <summary>Returns a new immutable read-only version of this JSON object (and all of its children)</summary>
		/// <returns>The same object, if it is already immutable; otherwise, a deep copy marked as read-only.</returns>
		/// <remarks>A JSON object that is immutable is truly safe against any modification, including any of its direct or indirect children.</remarks>
#if NET5_0_OR_GREATER
		public override JsonObject ToReadOnly()
#else
		public new JsonObject ToReadOnly()
#endif
		{
			if (m_readOnly)
			{
				CheckInvariants();
				return this;
			}

			var items = m_items;
			var map = new Dictionary<string, JsonValue>(items.Count, items.Comparer);
			foreach (var item in items)
			{
				var child = item.Value.ToReadOnly();
#if DEBUG
				Contract.Debug.Assert(child.IsReadOnly);
#endif
				map[item.Key] = child;
			}
			return new(map, readOnly: true);
		}

		/// <summary>Converts this JSON Object so that it, or any of its children that were previously read-only, can be mutated.</summary>
		/// <returns>The same instance if it is already fully mutable, OR a copy where any read-only Object or Array has been converted to allow mutations.</returns>
		/// <remarks>
		/// <para>Will return the same instance if it is already mutable, or a new deep copy with all children marked as mutable.</para>
		/// <para>This attempts to only copy what is necessary, and will not copy objects or arrays that are already mutable, or all other "value types" (string, boolean, number, ...) that are always immutable.</para>
		/// </remarks>
#if NET5_0_OR_GREATER
		public override JsonObject ToMutable()
#else
		public new JsonObject ToMutable()
#endif
		{
			if (m_readOnly)
			{ // create a mutable copy
				return Copy();
			}

			// the top-level is mutable, but maybe it has read-only children?
			Dictionary<string, JsonValue>? copy = null;
			foreach (var kv in m_items)
			{
				if (kv.Value is (JsonObject or JsonArray) && kv.Value.IsReadOnly)
				{
					copy ??= new(m_items.Count, m_items.Comparer);
					copy[kv.Key] = kv.Value.Copy();
				}
			}

			if (copy is null)
			{ // already mutable
				return this;
			}

			return new(copy, readOnly: false);
		}

		/// <summary>Returns a new mutable copy of this JSON array (and all of its children)</summary>
		/// <returns>A deep copy of this array and its children.</returns>
		/// <remarks>
		/// <para>This will recursively copy all JSON objects or arrays present in the array, even if they are already mutable.</para>
		/// <para>The new instance can be freely modified without any effect on its parent. Likewise, if the parent is modified, it will not have any effect on the copy.</para>
		/// </remarks>
#if NET5_0_OR_GREATER
		public override JsonObject Copy()
#else
		public new JsonObject Copy()
#endif
		{
			var items = m_items;
			if (items.Count == 0) return new JsonObject();

			var map = new Dictionary<string, JsonValue>(items.Count, items.Comparer);
			// we want to make sure that any mutable children is copied as well
			foreach (var kvp in items)
			{
				map[kvp.Key] = kvp.Value.Copy();
			}

			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Creates a copy of this object</summary>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
#if NET5_0_OR_GREATER
		protected internal override JsonObject Copy(bool deep, bool readOnly) => Copy(this, deep, readOnly);
#else
		protected internal override JsonValue Copy(bool deep, bool readOnly) => Copy(this, deep, readOnly); // covariant return types need runtime support that netstandard2.0/netfx lacks; the derived type is returned as the base type there
#endif

		/// <summary>Creates a copy of a JSON object</summary>
		/// <param name="obj">Object to copy</param>
		/// <param name="deep">If <see langword="true" />, recursively copy the children as well. If <see langword="false" />, perform a shallow copy that reuse the same children.</param>
		/// <param name="readOnly">If <see langword="true" />, the copy will become read-only. If <see langword="false" />, the copy will be writable.</param>
		/// <returns>Copy of the object, and optionally of its children (if <paramref name="deep"/> is <see langword="true" /></returns>
		/// <remarks>Performing a deep copy will protect against any change, but will induce a lot of memory allocations. For example, any child array will be cloned even if they will not be modified later on.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject Copy(JsonObject obj, bool deep = false, bool readOnly = false)
		{
			Contract.NotNull(obj);

			if (readOnly)
			{
				return obj.ToReadOnly();
			}

			if (deep)
			{
				return obj.Copy();
			}

			// simply create a shallow copy of the top-level
			var items = obj.m_items;
			return new JsonObject(new Dictionary<string, JsonValue>(items, items.Comparer), readOnly: false);
		}

		#region Create...

		#region Mutable...

		/// <summary>Creates a new empty JSON object</summary>
		/// <returns>JSON object of size 0, that can be modified.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject Create() => new(new Dictionary<string, JsonValue>(0, StringComparer.Ordinal), readOnly: false);

		/// <summary>Creates a new empty JSON object</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>JSON object of size 0, that can be modified.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject Create(IEqualityComparer<string> comparer) => new(new Dictionary<string, JsonValue>(0, comparer), readOnly: false);

		/// <summary>Creates a new JSON object with a single field</summary>
		/// <param name="key0">Name of the field</param>
		/// <param name="value0">Value of the field</param>
		/// <returns>JSON object of size 1, that can be modified.</returns>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: consider removing these when dictionary collection expressions are generally available?
		public static JsonObject Create(string key0, JsonValue? value0) => new(new Dictionary<string, JsonValue>(1, StringComparer.Ordinal)
		{
			[key0] = value0 ?? JsonNull.Null
		}, readOnly: false);

		/// <summary>Creates a new JSON object with a single field</summary>
		/// <param name="item">Name and value of the field</param>
		/// <returns>JSON object of size 1, that can be modified.</returns>
		[Pure]
		public static JsonObject Create((string Key, JsonValue? Value) item) => new(new Dictionary<string, JsonValue>(1, StringComparer.Ordinal)
		{
			[item.Key] = item.Value ?? JsonNull.Null
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 2 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <returns>JSON object of size 2, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: consider removing these when dictionary collection expressions are generally available?
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1) => new(new Dictionary<string, JsonValue>(2, StringComparer.Ordinal)
		{
			[key0] = value0 ?? JsonNull.Null,
			[key1] = value1 ?? JsonNull.Null,
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 2 fields</summary>
		/// <param name="item1">Name and value of the first field</param>
		/// <param name="item2">Name and value of the second field</param>
		/// <returns>JSON object of size 2, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject Create(
			(string Key, JsonValue? Value) item1,
			(string Key, JsonValue? Value) item2
		) => new(new Dictionary<string, JsonValue>(2, StringComparer.Ordinal)
		{
			[item1.Key] = item1.Value ?? JsonNull.Null,
			[item2.Key] = item2.Value ?? JsonNull.Null,
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 3 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <returns>JSON object of size 3, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: consider removing these when dictionary collection expressions are generally available?
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2) => new(new Dictionary<string, JsonValue>(3, StringComparer.Ordinal)
		{
			{ key0, value0 ?? JsonNull.Null },
			{ key1, value1 ?? JsonNull.Null },
			{ key2, value2 ?? JsonNull.Null },
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 3 fields</summary>
		/// <param name="item1">Name and value of the first field</param>
		/// <param name="item2">Name and value of the second field</param>
		/// <param name="item3">Name and value of the third field</param>
		/// <returns>JSON object of size 3, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject Create(
			(string Key, JsonValue? Value) item1,
			(string Key, JsonValue? Value) item2,
			(string Key, JsonValue? Value) item3
		) => new(new Dictionary<string, JsonValue>(3, StringComparer.Ordinal)
		{
			[item1.Key] = item1.Value ?? JsonNull.Null,
			[item2.Key] = item2.Value ?? JsonNull.Null,
			[item3.Key] = item3.Value ?? JsonNull.Null,
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 4 fields</summary>
		/// <param name="key0">Name of the first field</param>
		/// <param name="value0">Value of the first field</param>
		/// <param name="key1">Name of the second field</param>
		/// <param name="value1">Value of the second field</param>
		/// <param name="key2">Name of the third field</param>
		/// <param name="value2">Value of the third field</param>
		/// <param name="key3">Name of the fourth field</param>
		/// <param name="value3">Value of the fourth field</param>
		/// <returns>JSON object of size 4, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)] //TODO: consider removing these when dictionary collection expressions are generally available?
		public static JsonObject Create(string key0, JsonValue? value0, string key1, JsonValue? value1, string key2, JsonValue? value2, string key3, JsonValue? value3) => new(new Dictionary<string, JsonValue>(4, StringComparer.Ordinal)
		{
			{ key0, value0 ?? JsonNull.Null },
			{ key1, value1 ?? JsonNull.Null },
			{ key2, value2 ?? JsonNull.Null },
			{ key3, value3 ?? JsonNull.Null },
		}, readOnly: false);

		/// <summary>Creates a new JSON object with 4 fields</summary>
		/// <param name="item1">Name and value of the first field</param>
		/// <param name="item2">Name and value of the second field</param>
		/// <param name="item3">Name and value of the third field</param>
		/// <param name="item4">Name and value of the fourth field</param>
		/// <returns>JSON object of size 4, that can be modified.</returns>
		[Pure]
		[Obsolete("Please use the JsonObject.Create([ (k, v), ... ]) instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static JsonObject Create(
			(string Key, JsonValue? Value) item1,
			(string Key, JsonValue? Value) item2,
			(string Key, JsonValue? Value) item3,
			(string Key, JsonValue? Value) item4
		) => new(new Dictionary<string, JsonValue>(4, StringComparer.Ordinal)
		{
			[item1.Key] = item1.Value ?? JsonNull.Null,
			[item2.Key] = item2.Value ?? JsonNull.Null,
			[item3.Key] = item3.Value ?? JsonNull.Null,
			[item4.Key] = item4.Value ?? JsonNull.Null,
		}, readOnly: false);

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IDictionary<string, JsonValue> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		[OverloadResolutionPriority(1)]
		public static JsonObject Create(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			return items.Length == 0 ? new() : CreateEmptyWithComparer(null).SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>
		/// <para>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</para>
		/// <para>This overload is intended for creating a <see cref="JsonObject"/> with case-insensitive keys via collection expressions using the following syntax:
		/// <code>
		/// var obj = (JsonObject) [ with(StringComparer.OrdinalIgnoreCase), "hElLo": 123, "WorLd": 456, /*...*/ ];
		/// Console.WriteLine(obj["hello"]); // => 123
		/// Console.WriteLine(obj["WORLD"]); // => 456
		/// </code>
		/// </para>
		/// </remarks>
		public static JsonObject Create(IEqualityComparer<string> comparer, ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			return CreateEmptyWithComparer(comparer).SetRange(items);
		}

		/// <summary>Creates a new JSON object from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonObject"/> that cannot be modified.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// <para>This overload is intended for creating a read-only <see cref="JsonObject"/> using collection expressions via the following syntax:
		/// <code>
		/// // create a new read-only object
		/// JsonObject immutable = [ with(readOnly: true), "hello": 123, "world": 456, /*...*/ ];
		/// // object cannot be modified
		/// immutable["hello"] = "there"; // => throws InvalidOperationException
		/// </code>
		/// </para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.ReadOnlySpan{System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}})"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject Create(bool readOnly, ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			return readOnly ? JsonObject.ReadOnly.Create(items) : Create(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			//note: this overload without optional IEqualityComparer is required to resolve an overload ambiguity with the Create(ReadOnlySpan<KeyValuePair<string, JsonValue>>) variant when calling JsonObject.Create([])
			// => it seems that if one of the two has an optional argument, it will have a lower priority.

			return Create().SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEqualityComparer<string> comparer, ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			return CreateEmptyWithComparer(comparer).SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject FromValues<TValue>(ReadOnlySpan<(string Key, TValue Value)> items)
		{
			return Create().AddValues(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, ReadOnlySpan<(string Key, TValue Value)> items)
		{
			return Create(comparer).AddValues(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null).SetRange(items.AsSpan());
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEqualityComparer<string> comparer, KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return Create(comparer, items.AsSpan());
		}

		/// <summary>Creates a new JSON object from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonObject"/> that cannot be modified.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}[])"/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonObject Create(bool readOnly, KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return Create(readOnly, items.AsSpan());
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		[Pure]
		public static JsonObject Create((string Key, JsonValue? Value)[] items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null).SetRange(items.AsSpan());
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		[Pure]
		public static JsonObject Create(IEqualityComparer<string> comparer, (string Key, JsonValue? Value)[] items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer).SetRange(items.AsSpan());
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		[Pure]
		[OverloadResolutionPriority(-1)]
		public static JsonObject Create(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).SetRange(items);
		}

		/// <summary>Creates a new JSON object from the specified items, that will be either read-only or mutable.</summary>
		/// <param name="readOnly">If <c>true</c>, creates a read-only <see cref="JsonObject"/> that cannot be modified.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/>.</returns>
		/// <remarks>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, any <see cref="JsonValue"/> in <paramref name="items"/> will replaced by a read-only equivalent, if they are mutable.</para>
		/// </remarks>
		/// <seealso cref="JsonObject.ReadOnly.Create(System.Collections.Generic.IEnumerable{System.Collections.Generic.KeyValuePair{string,SnowBank.Data.Json.JsonValue}})"/>
		[OverloadResolutionPriority(-1)]
		public static JsonObject Create(bool readOnly, IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			return readOnly ? JsonObject.ReadOnly.Create(items) : Create(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEnumerable<(string Key, JsonValue Value)> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null).SetRange(items);
		}

		/// <summary>Creates a new JSON object with the specified items</summary>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Map of key/values to copy</param>
		/// <returns>New JSON object with the same elements in <paramref name="items"/></returns>
		/// <remarks>Adding or removing items in this new object will not modify <paramref name="items"/> (and vice versa), but any change to a mutable children will be reflected in both.</remarks>
		public static JsonObject Create(IEqualityComparer<string> comparer, IEnumerable<(string Key, JsonValue Value)> items)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer).SetRange(items);
		}

		#endregion

		#endregion

		#region FromValues...

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IDictionary<string, TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddValues(items, settings, resolver);
		}

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IDictionary<string, TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValues(items, settings, resolver);
		}

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(null, items).AddValues(items, settings, resolver);
		}

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValues(items, settings, resolver);
		}

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="packer">Helper that can pack instances of <typeparamref name="TValue"> into JSON values</typeparamref></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, TValue>> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValues(items, packer, settings, resolver);
		}

		/// <summary>Creates a JSON Object from a sequence of key/value pairs.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="items">Sequence of key/value pairs that will become the fields of the new JSON Object. There must not be any duplicate key, or an exception will be thrown.</param>
		/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IEnumerable<KeyValuePair<string, TValue>> items, [InstantHandle] Func<TValue, JsonValue?> valueSelector)
		{
			Contract.NotNull(items);
			return CreateEmptyWithComparer(comparer, items).AddValues(items, valueSelector);
		}

		/// <summary>Creates a JSON Object from an existing dictionary, using a custom JSON converter.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="members">Dictionary that must be converted.</param>
		/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector)
		{
			Contract.NotNull(members);
			Contract.NotNull(valueSelector);

			var comparer = ExtractKeyComparer(members) ?? StringComparer.Ordinal;

			var items = new Dictionary<string, JsonValue>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Missing);
			}
			return new JsonObject(items, readOnly: false);
		}

		/// <summary>Creates a JSON Object from an existing dictionary, using a custom JSON converter.</summary>
		/// <typeparam name="TValue">Type of the values, that must support conversion to JSON values</typeparam>
		/// <param name="members">Dictionary that must be converted.</param>
		/// <param name="valueSelector">Handler that is called for each value of the dictionary, and must return the converted JSON value.</param>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <returns>Corresponding JSON object, that can be modified.</returns>
		public static JsonObject FromValues<TValue>(IEqualityComparer<string> comparer, IDictionary<string, TValue> members, Func<TValue, JsonValue?> valueSelector)
		{
			Contract.NotNull(comparer);
			Contract.NotNull(members);
			Contract.NotNull(valueSelector);

			var items = new Dictionary<string, JsonValue>(members.Count, comparer);
			foreach (var kvp in members)
			{
				items.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Missing);
			}
			return new JsonObject(items, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding JSON value.</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, StringComparer.Ordinal);
			foreach (var item in source)
			{
				map.Add(keySelector(item), valueSelector(item) ?? JsonNull.Missing);
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding JSON value.</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement>(IEqualityComparer<string> comparer, IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, JsonValue?> valueSelector)
		{
			Contract.NotNull(comparer);
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			foreach (var item in source)
			{
				map.Add(keySelector(item), valueSelector(item) ?? JsonNull.Missing);
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, StringComparer.Ordinal);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var writer = CrystalJsonDomWriter.Create(settings, resolver);
			foreach (var item in source)
			{
				var child = FromValue(writer, ref context, valueSelector(item));
				map.Add(keySelector(item), child);
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
		/// <param name="packer">Helper that can pack instances of <typeparamref name="TValue"> into JSON values</typeparamref></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement, TValue>(IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);
			Contract.NotNull(packer);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, StringComparer.Ordinal);
			foreach (var item in source)
			{
				map.Add(keySelector(item), packer.Pack(valueSelector(item), settings, resolver) ?? JsonNull.Null);
			}
			return new(map, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement, TValue>(IEqualityComparer<string>? comparer, IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(comparer);
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			var context = new CrystalJsonDomWriter.VisitingContext();
			var writer = CrystalJsonDomWriter.Create(settings, resolver);
			foreach (var item in source)
			{
				var child = FromValue(writer, ref context, valueSelector(item));
				map.Add(keySelector(item), child);
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Creates a JSON Object from a sequence of elements, using a custom key and value selector.</summary>
		/// <typeparam name="TElement">Types of elements to be converted into key/value pairs.</typeparam>
		/// <typeparam name="TValue">Type of the extracted values, that must support conversion to JSON values</typeparam>
		/// <param name="comparer">The <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> implementation to use when comparing keys, or <see langword="null" /> to use the default <see cref="T:System.Collections.Generic.EqualityComparer`1" /> for the type of the key.</param>
		/// <param name="source">Sequence of elements to convert</param>
		/// <param name="keySelector">Handler that is called for each element of the sequence, and should return the corresponding unique key.</param>
		/// <param name="valueSelector">Handler that is called for each element of the sequence, and should return the corresponding value, that will in turn be converted into JSON.</param>
		/// <param name="packer">Helper that can pack instances of <typeparamref name="TValue"> into JSON values</typeparamref></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON object, that can be modified</returns>
		public static JsonObject FromValues<TElement, TValue>(IEqualityComparer<string>? comparer, IEnumerable<TElement> source, Func<TElement, string> keySelector, Func<TElement, TValue> valueSelector, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(comparer);
			Contract.NotNull(source);
			Contract.NotNull(keySelector);
			Contract.NotNull(valueSelector);
			Contract.NotNull(packer);

			var map = new Dictionary<string, JsonValue>(source.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			foreach (var item in source)
			{
				map.Add(keySelector(item), packer.Pack(valueSelector(item), settings, resolver) ?? JsonNull.Null);
			}
			return new JsonObject(map, readOnly: false);
		}

		#endregion

		#region FromObject...

		/// <summary>Converts an instance of type <typeparamref name="TValue"/> into the equivalent JSON Object.</summary>
		/// <typeparam name="TValue">Publicly known type of the instance.</typeparam>
		/// <param name="value">Instance to convert.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Corresponding JSON Object, or <see langword="null"/> if <paramref name="value"/> is null</returns>
		/// <remarks>The JSON Object that is returned is mutable, and cannot safely be cached or shared. If you need an immutable instance, consider calling <see cref="JsonObject.ReadOnly.FromObject{TValue}"/> instead.</remarks>
		[return: NotNullIfNotNull(nameof(value))]
		public static JsonObject? FromObject<TValue>(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return CrystalJsonDomWriter.Create(settings, resolver).ParseObject(value, typeof(TValue)).AsObjectOrDefault();
		}

		#endregion

		/// <summary>Converts an untyped dictionary into a JSON Object</summary>
		/// <returns>Corresponding mutable JSON Object</returns>
		/// <remarks>This should only be used to interface with legacy APIs that generate a <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, object></see>.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject CreateBoxed(IDictionary<string, object> members)
		{
			Contract.NotNull(members);

			var map = new Dictionary<string, JsonValue>(members.Count, ExtractKeyComparer(members) ?? StringComparer.Ordinal);
			foreach (var kvp in members)
			{
				map.Add(kvp.Key, FromValue(kvp.Value));
			}
			return new JsonObject(map, readOnly: false);
		}

		/// <summary>Converts an untyped dictionary into a JSON Object</summary>
		/// <returns>Corresponding mutable JSON Object</returns>
		/// <remarks>This should only be used to interface with legacy APIs that generate a <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, object></see>.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject CreateBoxed(IEqualityComparer<string> comparer, IDictionary<string, object> members)
		{
			Contract.NotNull(comparer);
			Contract.NotNull(members);

			var map = new Dictionary<string, JsonValue>(members.Count, comparer);
			foreach (var kvp in members)
			{
				map.Add(kvp.Key, FromValue(kvp.Value));
			}
			return new JsonObject(map, readOnly: false);
		}

		#endregion

		/// <summary>Gets the number of elements in the <see cref="JsonObject"/></summary>
		public int Count => m_items.Count;

		ICollection<string> IDictionary<string, JsonValue>.Keys => m_items.Keys;

		IEnumerable<string> IReadOnlyDictionary<string, JsonValue>.Keys => m_items.Keys;

		/// <summary>Gets an enumerable collection that contains the keys in the <see cref="JsonObject"/>.</summary>
		public Dictionary<string, JsonValue>.KeyCollection Keys => m_items.Keys;

		ICollection<JsonValue> IDictionary<string, JsonValue>.Values => m_items.Values;

		IEnumerable<JsonValue> IReadOnlyDictionary<string, JsonValue>.Values => m_items.Values;

		/// <summary>Gets an enumerable collection that contains the values in the <see cref="JsonObject"/>.</summary>
		public Dictionary<string, JsonValue>.ValueCollection Values => m_items.Values;

		/// <summary>Returns an enumerator that iterates through the key/value pairs in the <see cref="JsonObject"/>.</summary>
		public Dictionary<string, JsonValue>.Enumerator GetEnumerator() => m_items.GetEnumerator();

		IEnumerator<KeyValuePair<string, JsonValue>> IEnumerable<KeyValuePair<string, JsonValue>>.GetEnumerator() => m_items.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => m_items.GetEnumerator();

		/// <inheritdoc cref="JsonValue.IsReadOnly" />
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public override bool IsReadOnly => m_readOnly;

		/// <summary>Gets the <see cref="IEqualityComparer{T}"/> that is used to determine the equality of keys for the object</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public IEqualityComparer<string> Comparer => m_items.Comparer;

		/// <inheritdoc cref="JsonValue.this[string]" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[AllowNull]
		public override JsonValue this[string key]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_items.TryGetValue(key, out var value) ? value : JsonNull.Missing;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(key, value);
		}

		/// <inheritdoc cref="JsonValue.this[ReadOnlySpan{char}]" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[AllowNull]
		public override JsonValue this[ReadOnlySpan<char> key]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TryGetValue(key, out var value) ? value : JsonNull.Missing;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(key, value);
		}


		/// <inheritdoc cref="JsonValue.this[ReadOnlyMemory{char}]" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[AllowNull]
		public override JsonValue this[ReadOnlyMemory<char> key]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => TryGetValue(key, out var value) ? value : JsonNull.Missing;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set => Set(key, value);
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
				if (segment.TryGetName(out var name))
				{
					Set(name, value);
				}
				else
				{
					throw (m_readOnly ? FailCannotMutateReadOnlyValue(this) : ThrowHelper.InvalidOperationException($"Cannot set value at an index on a JSON {this.Type}"));
				}
			}
		}

		/// <inheritdoc cref="JsonValue.TryGetPathValue(string,out JsonValue)" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public override bool TryGetValue(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			return m_items.TryGetValue(key, out value);
		}

		/// <inheritdoc cref="JsonValue.TryGetValue(ReadOnlySpan{char},out JsonValue)" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public override bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out value);
#else
			// We cannot use span lookups so we may need to allocate the string in order to find it :(
			// - if the object is _small_ (1 or 2 keys?) AND uses the ordinal comparer, then simply enumerating the key/value pairs and calling SequenceEqual would be quicker (in theoriy no allocation)
			// - for larger objects we eat the cost and allocate, hoping to be able to optimize this in the feature with span lookups

			var items = m_items;
			switch (items.Count)
			{
				case 0:
				{
					value = null;
					return false;
				}
				case <= 3 when ReferenceEquals(items.Comparer, StringComparer.Ordinal):
				{
					foreach (var kv in items)
					{
						if (key.SequenceEqual(kv.Key.AsSpan()))
						{
							value = kv.Value;
							return true;
						}
					}
					value = null;
					return false;
				}
				default:
				{
					//PERF: we unfortunately need to allocate the string :(
					return items.TryGetValue(key.ToString(), out value);
				}
			}
#endif
		}

		/// <inheritdoc cref="JsonValue.TryGetValue(ReadOnlyMemory{char},out JsonValue)" />
		[EditorBrowsable(EditorBrowsableState.Always)]
		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public override bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key.Span, out value);
#else
			if (key.TryGetString(out var k))
			{ // we have the whole string, we can do the standard lookup
				return m_items.TryGetValue(k, out value);
			}
			return TryGetValue(key.Span, out value);
#endif
		}

#if NET9_0_OR_GREATER

		/// <inheritdoc/>
		[EditorBrowsable(EditorBrowsableState.Always)]
		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public override bool TryGetValue(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out actualKey, out value);
		}

		/// <inheritdoc/>
		[EditorBrowsable(EditorBrowsableState.Always)]
		[ContractAnnotation("halt<=key:null; =>true,value:notnull; =>false,value:null")]
		public override bool TryGetValue(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key.Span, out actualKey, out value);
		}

#endif

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public void Add(string key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null && !ReferenceEquals(this, value));
			m_items.Add(key, value ?? JsonNull.Null);
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public void Add(ReadOnlySpan<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			//note: there is no "Add" on AlternateLookup<...> (only "TryAdd") so there is no real point in optimizing the allocation here (if we assume that the failure case is very rare/a bug)
			m_items.Add(key.ToString(), value ?? JsonNull.Null);
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public void Add(ReadOnlyMemory<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			//note: there is no "Add" on AlternateLookup<...> (only "TryAdd") so there is no real point in optimizing the allocation here (if we assume that the failure case is very rare/a bug)
			m_items.Add(key.ToString(), value ?? JsonNull.Null);
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if it was not present before</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool TryAdd(string key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			return m_items.TryAdd(key, value ?? JsonNull.Null);
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if it was not present before</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryAdd(ReadOnlySpan<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(key, value ?? JsonNull.Null);
#else
			return m_items.TryAdd(key.ToString(), value ?? JsonNull.Null);
#endif
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if it was not present before</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool TryAdd(ReadOnlyMemory<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().TryAdd(key.Span, value ?? JsonNull.Null);
#else
			return m_items.TryAdd(key.GetStringOrCopy(), value ?? JsonNull.Null);
#endif
		}

		//REVIEW: should all these AddIfXYZ() be moved to extension methods??
		
		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is not null or missing</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or an instance of <see cref="JsonNull"/></remarks>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public void AddIfNotNull(string key, JsonValue? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null && !ReferenceEquals(this, value));
			if (value is not (null or JsonNull))
			{
				m_items.Add(key, value);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is not null</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/></remarks>
		public void AddIfNotNull(string key, string? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null)
			{
				m_items.Add(key, JsonString.Return(value));
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is not null or empty</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or the empty string</remarks>
		public void AddIfNotNullOrEmpty(string key, string? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (!string.IsNullOrEmpty(value))
			{
				m_items.Add(key, JsonString.Return(value));
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is <c>true</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="false"/></remarks>
		public void AddIfTrue(string key, bool condition, JsonValue? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (condition)
			{
				m_items.Add(key, value ?? JsonNull.Null);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is <c>true</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="false"/></remarks>
		public void AddIfTrue(string key, bool value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value)
			{
				m_items.Add(key, JsonBoolean.True);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is <c>true</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or <see langword="false"/></remarks>
		public void AddIfTrue(string key, bool? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value ?? false)
			{
				m_items.Add(key, JsonBoolean.True);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/>, only if its value is not null or missing</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/></remarks>
		public void AddIfNotNull(string key, bool? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null)
			{
				m_items.Add(key, JsonBoolean.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="0"/></remarks>
		public void AddIfNonZero(string key, int value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c> or <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or <see langword="0"/></remarks>
		public void AddIfNonZero(string key, int? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null && value.Value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/></remarks>
		public void AddIfNotNull(string key, int? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="0"/></remarks>
		public void AddIfNonZero(string key, long value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c> or <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or <see langword="0"/></remarks>
		public void AddIfNonZero(string key, long? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null && value.Value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/></remarks>
		public void AddIfNotNull(string key, long? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="0"/></remarks>
		public void AddIfNonZero(string key, double value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c> or <c>0</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/> or <see langword="0"/></remarks>
		public void AddIfNonZero(string key, double? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null && value.Value != 0)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds a number to this <see cref="JsonObject"/>, only if its value is not <c>null</c></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>The method is a no-op if <paramref name="value"/> is <see langword="null"/></remarks>
		public void AddIfNotNull(string key, double? value)
		{
			//note: this method is mostly used by generated source code
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			if (value is not null)
			{
				m_items.Add(key, JsonNumber.Create(value.Value));
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public void AddIfNotNull(ReadOnlySpan<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			if (value is not (null or JsonNull))
			{
				//note: there is no "Add" on AlternateLookup<...> (only "TryAdd") so there is no real point in optimizing the allocation here (if we assume that the failure case is very rare/a bug)
				m_items.Add(key.ToString(), value);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public void AddIfNotNull(ReadOnlyMemory<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(!ReferenceEquals(this, value));
			if (value is not (null or JsonNull))
			{
				//note: there is no "Add" on AlternateLookup<...> (only "TryAdd") so there is no real point in optimizing the allocation here (if we assume that the failure case is very rare/a bug)
				m_items.Add(key.GetStringOrCopy(), value);
			}
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void Add(KeyValuePair<string, JsonValue> item)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
			m_items.Add(item.Key, item.Value ?? JsonNull.Null);
		}

		/// <summary>Adds an element to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void Add((string Key, JsonValue? Value) item)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
			m_items.Add(item.Key, item.Value ?? JsonNull.Null);
		}

		#region SetRange/AddRange...

		#region Mutable...

		/// <summary>Sets several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>If there are duplicate keys in <paramref name="items"/>, the last one wins.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRange(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self[item.Key] = item.Value ?? JsonNull.Null;
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <remarks>If there are duplicate keys in <paramref name="items"/>, the last one wins.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRange(ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				self[item.Key] = item.Value ?? JsonNull.Null;
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		[OverloadResolutionPriority(-1)]
		public JsonObject SetRange(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			return items switch
			{
				IDictionary<string, JsonValue> dict        => SetRange(dict),
				KeyValuePair<string, JsonValue>[] arr      => SetRange(arr.AsSpan()),
				List<KeyValuePair<string, JsonValue>> list => SetRange(CollectionsMarshal.AsSpan(list)),
				_                                          => SetRangeSlow(this, items)
			};

			[MethodImpl(MethodImplOptions.NoInlining)]
			static JsonObject SetRangeSlow(JsonObject obj, IEnumerable<KeyValuePair<string, JsonValue>> items)
			{
				var self = obj.m_items;
				if (items.TryGetNonEnumeratedCount(out var count))
				{
					if (count == 0) return obj;
					self.EnsureCapacity(unchecked(self.Count + count));
				}

				foreach (var item in items)
				{
					Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(obj, item.Value));
					// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
					self[item.Key] = item.Value ?? JsonNull.Null;
				}

				return obj;
			}
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRange(IEnumerable<(string Key, JsonValue Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			return items switch
			{
				(string, JsonValue?)[] arr      => SetRange(arr.AsSpan()),
				List<(string, JsonValue?)> list => SetRange(CollectionsMarshal.AsSpan(list)),
				_                               => SetRangeSlow(this, items)
			};

			[MethodImpl(MethodImplOptions.NoInlining)]
			static JsonObject SetRangeSlow(JsonObject obj, IEnumerable<(string Key, JsonValue Value)> items)
			{
				var self = obj.m_items;
				if (items.TryGetNonEnumeratedCount(out var count))
				{
					if (count == 0) return obj;
					self.EnsureCapacity(unchecked(self.Count + count));
				}

				foreach (var item in items)
				{
					Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(obj, item.Value));
					// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
					self[item.Key] = item.Value ?? JsonNull.Null;
				}

				return obj;
			}
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <exception cref="T:System.ArgumentException">if there are duplicate keys in <paramref name="items"/>, or if one the keys already exists in this instance.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		/// <exception cref="T:System.ArgumentException">if there are duplicate keys in <paramref name="items"/>, or if one the keys already exists in this instance.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				self.Add(item.Key, item.Value ?? JsonNull.Null);
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(ReadOnlySpan<(string Key, TValue Value)> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null);
				self.Add(item.Key, FromValue<TValue>(item.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return AddRange(items.AsSpan());
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange((string, JsonValue?)[] items)
		{
			Contract.NotNull(items);
			return AddRange(items.AsSpan());
		}

		/// <summary>Adds the content of an existing dictionary to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(IDictionary<string, JsonValue> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items.Count == 0)
			{
				return this;
			}

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Count));

			switch (items)
			{
				case JsonObject obj:
				{
					foreach (var item in obj.m_items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						self.Add(item.Key, item.Value);
					}

					break;
				}
				case Dictionary<string, JsonValue> dictionary:
				{
					foreach (var item in dictionary)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, item.Value ?? JsonNull.Null);
					}

					break;
				}
				case FrozenDictionary<string, JsonValue> frozenDictionary:
				{
					foreach (var item in frozenDictionary)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, item.Value ?? JsonNull.Null);
					}

					break;
				}
				case ImmutableDictionary<string, JsonValue> immutableDictionary:
				{
					foreach (var item in immutableDictionary)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, item.Value ?? JsonNull.Null);
					}

					break;
				}
				default:
				{
					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, item.Value ?? JsonNull.Null);
					}

					break;
				}
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		[OverloadResolutionPriority(-1)]
		public JsonObject AddRange(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			return items switch
			{
				IDictionary<string, JsonValue> dict        => AddRange(dict),
				KeyValuePair<string, JsonValue>[] arr      => AddRange(arr.AsSpan()),
				List<KeyValuePair<string, JsonValue>> list => AddRange(CollectionsMarshal.AsSpan(list)),
				_                                          => AddRangeSlow(this, items)
			};

			[MethodImpl(MethodImplOptions.NoInlining)]
			static JsonObject AddRangeSlow(JsonObject obj, IEnumerable<KeyValuePair<string, JsonValue>> items)
			{
				var self = obj.m_items;
				if (items.TryGetNonEnumeratedCount(out var count))
				{
					if (count == 0) return obj;
					self.EnsureCapacity(unchecked(self.Count + count));
				}

				foreach (var item in items)
				{
					Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(obj, item.Value));
					// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
					self.Add(item.Key, item.Value ?? JsonNull.Null);
				}

				return obj;
			}

		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRange(IEnumerable<(string Key, JsonValue Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			return items switch
			{
				(string, JsonValue?)[] arr      => AddRange(arr.AsSpan()),
				List<(string, JsonValue?)> list => AddRange(CollectionsMarshal.AsSpan(list)),
				_                               => AddRangeSlow(this, items)
			};

			[MethodImpl(MethodImplOptions.NoInlining)]
			static JsonObject AddRangeSlow(JsonObject obj, IEnumerable<(string Key, JsonValue Value)> items)
			{
				var self = obj.m_items;
				if (items.TryGetNonEnumeratedCount(out var count))
				{
					if (count == 0) return obj;
					self.EnsureCapacity(unchecked(self.Count + count));
				}

				foreach (var item in items)
				{
					Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(obj, item.Value));
					// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
					self.Add(item.Key, item.Value ?? JsonNull.Null);
				}

				return obj;
			}
		}

		#endregion

		#region Immutable...

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRangeReadOnly(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly();
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRangeReadOnly(ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly();
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRangeReadOnly(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			switch (items)
			{
				case IDictionary<string, JsonValue> dict:
				{
					return SetRangeReadOnly(dict);
				}
				case KeyValuePair<string, JsonValue>[] arr:
				{
					return SetRangeReadOnly(arr.AsSpan());
				}
				case List<KeyValuePair<string, JsonValue>> list:
				{
					return SetRangeReadOnly(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly();
					}

					return this;
				}
			}
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject SetRangeReadOnly(IEnumerable<(string Key, JsonValue Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			switch (items)
			{
				case (string, JsonValue?)[] arr:
				{
					return SetRangeReadOnly(arr.AsSpan());
				}
				case List<(string, JsonValue?)> list:
				{
					return SetRangeReadOnly(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self[item.Key] = (item.Value ?? JsonNull.Null).ToReadOnly();
					}

					return this;
				}
			}
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(ReadOnlySpan<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(ReadOnlySpan<(string Key, JsonValue? Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(unchecked(self.Count + items.Length));

			foreach (var item in items)
			{
				Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
				// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
				self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(KeyValuePair<string, JsonValue>[] items)
		{
			Contract.NotNull(items);
			return AddRangeReadOnly(items.AsSpan());
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly((string Key, JsonValue? Value)[] items)
		{
			Contract.NotNull(items);
			return AddRangeReadOnly(items.AsSpan());
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <param name="items">Set of elements to add. If some values are mutable, a read-only copy will be added instead.</param>
		/// <returns>Same instance (for chaining)</returns>
		/// <remarks>
		/// <para>Fields that already exist will be overwritten.</para>
		/// <para>For performance reasons, added JSON Objects or Arrays should already be read-only, otherwise a deep-copy will be performed.</para></remarks>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(IDictionary<string, JsonValue> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (items.Count == 0) return this;

			var self = m_items;

			self.EnsureCapacity(unchecked(self.Count + items.Count));

			switch (items)
			{
				case JsonObject obj:
				{
					if (obj.IsReadOnly)
					{
						// we assume that the values are already guaranteed to be read-only, so we can skip the ToReadOnly() call!
						foreach (var item in obj.m_items)
						{
							Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value) && item.Value.IsReadOnly);
							self.Add(item.Key, item.Value);
						}
					}
					else
					{
						foreach (var item in obj.m_items)
						{
							Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
							self.Add(item.Key, item.Value.ToReadOnly());
						}
					}
					break;
				}
				case Dictionary<string, JsonValue> dict:
				{
					foreach (var item in dict)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}
					break;
				}
				case FrozenDictionary<string, JsonValue> dict:
				{
					foreach (var item in dict)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}
					break;
				}
				case ImmutableDictionary<string, JsonValue> dict:
				{
					foreach (var item in dict)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}
					break;
				}
				default:
				{
					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}
					break;
				}
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(IEnumerable<KeyValuePair<string, JsonValue>> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			switch (items)
			{
				case IDictionary<string, JsonValue> dict:
				{
					return AddRangeReadOnly(dict);
				}
				case KeyValuePair<string, JsonValue>[] arr:
				{
					return AddRangeReadOnly(arr.AsSpan());
				}
				case List<KeyValuePair<string, JsonValue>> list:
				{
					return AddRangeReadOnly(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}

					return this;
				}
			}
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddRangeReadOnly(IEnumerable<(string Key, JsonValue Value)> items)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			switch (items)
			{
				case (string, JsonValue?)[] arr:
				{
					return AddRangeReadOnly(arr.AsSpan());
				}
				case List<(string, JsonValue?)> list:
				{
					return AddRangeReadOnly(CollectionsMarshal.AsSpan(list));
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(unchecked(self.Count + count));
					}

					foreach (var item in items)
					{
						Contract.Debug.Requires(item.Key is not null && !ReferenceEquals(this, item.Value));
						// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
						self.Add(item.Key, (item.Value ?? JsonNull.Null).ToReadOnly());
					}

					return this;
				}
			}
		}

		#endregion

		#endregion

		#region AddValues [of T] ...

		#region Mutable...

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items.Length == 0) return this;

			var self = m_items;
			int capacity = checked(this.Count + items.Length);
			self.EnsureCapacity(capacity);

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValue(kvp.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(packer);
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items.Length == 0) return this;

			var self = m_items;
			int capacity = checked(this.Count + items.Length);
			self.EnsureCapacity(capacity);

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, packer.Pack(kvp.Value, settings, resolver) ?? JsonNull.Null);
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items, Func<TValue, JsonValue?> valueSelector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items.Length == 0) return this;

			var self = m_items;
			int capacity = checked(this.Count + items.Length);
			self.EnsureCapacity(capacity);

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Null);
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(KeyValuePair<string, TValue>[]? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Length == 0) return this;

			return AddValues<TValue>(items.AsSpan(), settings, resolver);
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(KeyValuePair<string, TValue>[]? items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Length == 0) return this;

			return AddValues<TValue>(items.AsSpan(), packer, settings, resolver);
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(KeyValuePair<string, TValue>[]? items, Func<TValue, JsonValue?> valueSelector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Length == 0) return this;

			return AddValues<TValue>(items.AsSpan(), valueSelector);
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IDictionary<string, TValue>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			if (items is Dictionary<string, TValue> dict)
			{
				foreach (var kvp in dict)
				{
					self.Add(kvp.Key, FromValue(kvp.Value, settings, resolver));
				}
			}
			else
			{
				foreach (var kvp in items)
				{
					self.Add(kvp.Key, FromValue(kvp.Value, settings, resolver));
				}
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IDictionary<string, TValue>? items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(packer);
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			if (items is Dictionary<string, TValue> dict)
			{
				foreach (var kvp in dict)
				{
					self.Add(kvp.Key, packer.Pack(kvp.Value, settings, resolver) ?? JsonNull.Null);
				}
			}
			else
			{
				foreach (var kvp in items)
				{
					self.Add(kvp.Key, packer.Pack(kvp.Value, settings, resolver) ?? JsonNull.Null);
				}
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IDictionary<string, TValue>? items, Func<TValue, JsonValue?> valueSelector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null || items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			if (items is Dictionary<string, TValue> dict)
			{
				foreach (var kvp in dict)
				{
					self.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Null);
				}
			}
			else
			{
				foreach (var kvp in items)
				{
					self.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Null);
				}
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null) return this;

			if (items is IDictionary<string, TValue> dict)
			{
				return AddValues<TValue>(dict, settings, resolver);
			}

			if (items.TryGetSpan(out var span))
			{
				return AddValues<TValue>(span, settings, resolver);
			}

			var self = m_items;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0) return this;
				self.EnsureCapacity(checked(this.Count + count));
			}

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, FromValue(kvp.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(packer);
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null)
			{
				return this;
			}

			if (items is IDictionary<string, TValue> dict)
			{
				return AddValues<TValue>(dict, packer, settings, resolver);
			}

			if (items.TryGetSpan(out var span))
			{
				return AddValues<TValue>(span, packer, settings, resolver);
			}

			var self = m_items;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0) return this;
				self.EnsureCapacity(checked(this.Count + count));
			}

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, packer.Pack(kvp.Value, settings, resolver) ?? JsonNull.Null);
			}

			return this;
		}

		/// <summary>Adds several elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValues<TValue>(IEnumerable<KeyValuePair<string, TValue>>? items, Func<TValue, JsonValue?> valueSelector)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items is null) return this;

			if (items is IDictionary<string, TValue> dict)
			{
				return AddValues<TValue>(dict, valueSelector);
			}

			if (items.TryGetSpan(out var span))
			{
				return AddValues<TValue>(span, valueSelector);
			}

			var self = m_items;
			if (items.TryGetNonEnumeratedCount(out var count))
			{
				if (count == 0) return this;
				self.EnsureCapacity(checked(this.Count + count));
			}

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, valueSelector(kvp.Value) ?? JsonNull.Null);
			}

			return this;
		}

		#endregion

		#region Immutable...

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (items.Length == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Length));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(ReadOnlySpan<KeyValuePair<string, TValue>> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return AddValues<TValue>(items, packer, settings.AsReadOnly(), resolver);
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(KeyValuePair<string, TValue>[] items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValuesReadOnly<TValue>(items.AsSpan(), settings, resolver);
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(KeyValuePair<string, TValue>[] items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(items);
			return AddValues<TValue>(items.AsSpan(), packer, settings.AsReadOnly(), resolver);
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(Dictionary<string, TValue> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(Dictionary<string, TValue> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return AddValues<TValue>(items, packer, settings.AsReadOnly(), resolver);
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(List<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			if (items.Count == 0) return this;

			var self = m_items;
			self.EnsureCapacity(checked(this.Count + items.Count));

			foreach (var kvp in items)
			{
				self.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value, settings, resolver));
			}

			return this;
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(List<KeyValuePair<string, TValue>> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return AddValues<TValue>(items, packer, settings.AsReadOnly(), resolver);
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.NotNull(items);

			switch (items)
			{
				case Dictionary<string, TValue> dict:
				{
					return AddValuesReadOnly(dict, settings, resolver);
				}
				case List<KeyValuePair<string, TValue>> list:
				{
					return AddValuesReadOnly<TValue>(CollectionsMarshal.AsSpan(list), settings, resolver);
				}
				case KeyValuePair<string, TValue>[] arr:
				{
					return AddValuesReadOnly<TValue>(arr.AsSpan(), settings, resolver);
				}
				default:
				{
					var self = m_items;
					if (items.TryGetNonEnumeratedCount(out var count))
					{
						if (count == 0) return this;
						self.EnsureCapacity(checked(this.Count + count));
					}

					foreach (var kvp in items)
					{
						self.Add(kvp.Key, JsonValue.ReadOnly.FromValue(kvp.Value, settings, resolver));
					}

					return this;
				}
			}
		}

		/// <summary>Adds several read-only elements to this <see cref="JsonObject"/></summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public JsonObject AddValuesReadOnly<TValue>(IEnumerable<KeyValuePair<string, TValue>> items, IJsonPacker<TValue> packer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return AddValues<TValue>(items, packer, settings.AsReadOnly(), resolver);
		}

		#endregion

		#endregion

		/// <summary>Removes the value with the specified key from this object.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool Remove(string key)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(key is not null);
			return m_items.Remove(key);
		}

		/// <summary>Removes the value with the specified key from this object.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool Remove(ReadOnlySpan<char> key)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key);
#else
			// we have to allocate the string here :(
			return m_items.Remove(key.ToString());
#endif
		}

		/// <summary>Removes the value with the specified key from this object.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool Remove(ReadOnlyMemory<char> key)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key.Span);
#else
			// we may have to allocate the string here :(
			return m_items.Remove(key.GetStringOrCopy());
#endif
		}

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="value">The removed element.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(string key, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
#if NET5_0_OR_GREATER
			return m_items.Remove(key, out value);
#else
			// Dictionary<K,V>.Remove(key, out value) is not available on netstandard2.0
			if (!m_items.TryGetValue(key, out value))
			{
				return false;
			}
			return m_items.Remove(key);
#endif
		}

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="value">The removed element.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool Remove(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key, out _, out value);
#elif NET5_0_OR_GREATER
			// we have to allocate the string here :(
			return m_items.Remove(key.ToString(), out value);
#else
			// Dictionary<K,V>.Remove(key, out value) is not available on netstandard2.0
			// we have to allocate the string here :(
			var str = key.ToString();
			if (!m_items.TryGetValue(str, out value))
			{
				return false;
			}
			return m_items.Remove(str);
#endif
		}

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="value">The removed element.</param>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)]
#endif
		public bool Remove(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key.Span, out _, out value);
#elif NET5_0_OR_GREATER
			// we may have to allocate the string here :(
			return m_items.Remove(key.GetStringOrCopy(), out value);
#else
			// Dictionary<K,V>.Remove(key, out value) is not available on netstandard2.0
			// we may have to allocate the string here :(
			var str = key.GetStringOrCopy();
			if (!m_items.TryGetValue(str, out value))
			{
				return false;
			}
			return m_items.Remove(str);
#endif
		}

#if NET9_0_OR_GREATER

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="actualKey">The removed key.</param>
		/// <param name="value">The removed element.</param>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(ReadOnlySpan<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key, out actualKey, out value);
		}

		/// <summary>Removes the value with the specified key from this object, and copies the element to the <paramref name="value" /> parameter.</summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="actualKey">The removed key.</param>
		/// <param name="value">The removed element.</param>
		/// <returns><see langword="true" /> if the element is successfully found and removed; otherwise, <see langword="false" />.</returns>
		/// <exception cref="T:System.ArgumentNullException"><paramref name="key" /> is <see langword="null" />.</exception>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(ReadOnlyMemory<char> key, [MaybeNullWhen(false)] out string actualKey, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().Remove(key.Span, out actualKey, out value);
		}

#endif

		/// <summary>Removes the value with the specified key from this object, only if its value has not changed.</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public bool Remove(KeyValuePair<string, JsonValue> keyValuePair)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			Contract.Debug.Requires(keyValuePair.Key is not null);
			if (!m_items.TryGetValue(keyValuePair.Key, out var prev) || !prev.Equals(keyValuePair.Value))
			{
				return false;
			}
			return m_items.Remove(keyValuePair.Key);
		}

		/// <summary>Removes all the element of this object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public void Clear()
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			m_items.Clear();
		}

		/// <summary>Ensures that the dictionary can hold up to a specified number of entries without any further expansion of its backing storage.</summary>
		/// <param name="capacity">The number of entries.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="capacity" /> is less than 0.</exception>
		/// <returns>The current capacity of the <see cref="T:System.Collections.Generic.Dictionary`2" />.</returns>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public int EnsureCapacity(int capacity) => m_items.EnsureCapacity(capacity);

		/// <summary>Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries.</summary>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public void TrimExcess() => this.TrimExcess(this.Count);

		/// <summary>Sets the capacity of this dictionary to hold up a specified number of entries without any further expansion of its backing storage.</summary>
		/// <param name="capacity">The new capacity.</param>
		/// <exception cref="T:System.ArgumentOutOfRangeException">
		/// <paramref name="capacity" /> is less than <see cref="Count" />.</exception>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
#if NET5_0_OR_GREATER
		public void TrimExcess(int capacity) => m_items.TrimExcess(capacity);
#else
		// Dictionary<K,V>.TrimExcess is not available on netstandard2.0: trimming is a best-effort no-op there
		public void TrimExcess(int capacity) { }
#endif

		#region Public Properties...

		/// <summary>Type of JSON value (Object, Array, String, ...)</summary>
		public override JsonType Type => JsonType.Object;

		/// <summary>Returns <see langword="true"/> if the object is empty.</summary>
		public override bool IsDefault => this.Count == 0;

		/// <summary>Returns <see langword="true"/> if the object is not empty.</summary>
		public bool HasValues => this.Count > 0;

		/// <summary>Returns the "__class" attribute, if present</summary>
		public string? CustomClassName => Get<string?>(JsonTokens.CustomClassAttribute, null);

		#endregion

		#region Getters...

		/// <summary>Determines whether this <see cref="JsonObject"/> contains an element with the given <paramref name="key"/>.</summary>
		/// <param name="key">Name of the key</param>
		/// <returns>Returns <see langword="true" /> if the entry is present; otherwise, <see langword="false" /></returns>
		/// <remarks>Please note that this will return <see langword="true" /> even if the property value is <see langword="null"/>. To treat <see langword="null"/> the same as missing, please use <see cref="Has(string)"/> instead.</remarks>
		/// <example><code>
		/// // Cases that return 'true':
		/// new JsonObject { ["Foo"] = "hello" }.Has("Foo") => true  // 'Foo' exists
		/// new JsonObject { ["Foo"] = ""      }.Has("Foo") => true  // 'Foo' is the empty string, which is not 'null'
		/// new JsonObject { ["Foo"] = false   }.Has("Foo") => true  // 'Foo' is false, which is not 'null'
		/// new JsonObject { ["Foo"] = null    }.Has("Foo") => true  // 'Foo' is an explicit 'null', ***: behavior different from Has(key)
		/// // Cases that return 'false':
		/// new JsonObject { ["Bar"] = "world" }.Has("Foo") => false // 'Foo' not found
		/// </code></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool ContainsKey(string key) => m_items.ContainsKey(key);

		/// <summary>Determines whether this <see cref="JsonObject"/> contains an element with the given <paramref name="key"/>.</summary>
		/// <param name="key">Name of the key</param>
		/// <returns>Returns <see langword="true" /> if the entry is present; otherwise, <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
#if !NET9_0_OR_GREATER
		[EditorBrowsable(EditorBrowsableState.Never)] // allocates memory in .NET 8 or lower!
#endif
		public bool ContainsKey(ReadOnlySpan<char> key)
		{
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().ContainsKey(key);
#else
			return m_items.ContainsKey(key.ToString());
#endif
		}

		/// <summary>Determines whether this <see cref="JsonObject"/> contains an element with the given <paramref name="key"/>.</summary>
		/// <param name="key">Name of the key</param>
		/// <returns>Returns <see langword="true" /> if the entry is present; otherwise, <see langword="false" /></returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(ReadOnlyMemory<char> key)
		{
#if NET9_0_OR_GREATER
			return m_items.GetAlternateLookup<ReadOnlySpan<char>>().ContainsKey(key.Span);
#else
			return m_items.ContainsKey(key.GetStringOrCopy());
#endif
		}

		bool ICollection<KeyValuePair<string, JsonValue>>.Contains(KeyValuePair<string, JsonValue> keyValuePair) => ((ICollection<KeyValuePair<string, JsonValue>>) m_items).Contains(keyValuePair);

		/// <summary>Determines whether this <see cref="JsonObject"/> contains an element with the given <paramref name="key"/> and with a non-null value</summary>
		/// <param name="key">Name of the key</param>
		/// <returns>Returns <see langword="true" /> if the entry is present and not <see cref="JsonNull.Null"/> or <see cref="JsonNull.Missing"/>.</returns>
		/// <seealso cref="ContainsKey(string)"/>
		/// <remarks>
		/// Result of <c>obj.Has("Foo")</c>:
		/// <list type="table">
		///   <listheader><term>JSON</term><description>Result</description></listheader>
		///   <item><term><c>{ "Foo" = "hello" }</c></term><description><c>true</c>, 'Foo' exists</description></item>
		///   <item><term><c>{ "Foo" = ""      }</c></term><description><c>true</c>, 'Foo' is the empty string, which is not 'null'</description></item>
		///   <item><term><c>{ "Foo" = false   }</c></term><description><c>true</c>, 'Foo' is false, which is not 'null'</description></item>
		///   <item><term><c>{ "Foo" = null    }</c></term><description><c>false</c>, 'Foo' is an explicit 'null', ***: behavior different from ContainsKey(key)</description></item>
		///   <item><term><c>{ "Bar" = "world" }</c></term><description><c>false</c>, 'Foo' not found</description></item>
		/// </list>
		/// </remarks>
		[EditorBrowsable(EditorBrowsableState.Always)]
		public bool Has(string key) => m_items.TryGetValue(key, out var value) && !value.IsNullOrMissing();

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValue(string key) => m_items.TryGetValue(key, out var value) && value is not (null or JsonNull) ? value : JsonValueExtensions.FailFieldIsNullOrMissing(value, key);

		/// <inheritdoc/>
		[Pure]
		public override JsonValue GetValue(ReadOnlySpan<char> key) => TryGetValue(key, out var value) ? value : JsonValueExtensions.FailFieldIsNullOrMissing(value, key);

		/// <inheritdoc/>
		[Pure]
		public override JsonValue GetValue(ReadOnlyMemory<char> key) => TryGetValue(key, out var value) ? value : JsonValueExtensions.FailFieldIsNullOrMissing(value, key);

#if NET9_0_OR_GREATER

		/// <inheritdoc/>
		[Pure]
		public override JsonValue GetValue(ReadOnlySpan<char> key, out string actualKey) => TryGetValue(key, out actualKey!, out var value) ? value : JsonValueExtensions.FailFieldIsNullOrMissing(value, key);

		/// <inheritdoc/>
		[Pure]
		public override JsonValue GetValue(ReadOnlyMemory<char> key, out string actualKey) => TryGetValue(key.Span, out actualKey!, out var value) ? value : JsonValueExtensions.FailFieldIsNullOrMissing(value, key);

#endif

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(string key, JsonValue? missingValue = null)
			=> m_items.TryGetValue(key, out var value) ? value : (missingValue ?? JsonNull.Missing);

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? missingValue = null)
			=> TryGetValue(key, out var value) ? value : (missingValue ?? JsonNull.Missing);

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? missingValue = null)
			=> TryGetValue(key, out var value) ? value : (missingValue ?? JsonNull.Missing);

#if NET9_0_OR_GREATER

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(ReadOnlySpan<char> key, JsonValue? missingValue, out string? actualKey)
			=> TryGetValue(key, out actualKey, out var value) ? value : (missingValue ?? JsonNull.Missing);

		/// <inheritdoc/>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Always)]
		public override JsonValue GetValueOrDefault(ReadOnlyMemory<char> key, JsonValue? missingValue, out string? actualKey)
			=> TryGetValue(key.Span, out actualKey, out var value) ? value : (missingValue ?? JsonNull.Missing);

#endif

		/// <summary>Returns a JSON Object at the given path, or create a new empty object if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the object</param>
		/// <returns>Existing object, or a new empty object.</returns>
		/// <example><code>
		/// { }.GetOrCreateObject("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreateObject("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonObject GetOrCreateObject(string path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonObject) SetPathInternal(JsonPath.Create(path), null, JsonType.Object);
		}

		/// <summary>Returns a JSON Object at the given path, or create a new empty object if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the object</param>
		/// <returns>Existing object, or a new empty object.</returns>
		/// <example><code>
		/// { }.GetOrCreateObject("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreateObject("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonObject GetOrCreateObject(ReadOnlyMemory<char> path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonObject) SetPathInternal(JsonPath.Create(path), null, JsonType.Object);
		}

		/// <summary>Returns a JSON Object at the given path, or create a new empty object if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the object</param>
		/// <returns>Existing object, or a new empty object.</returns>
		/// <example><code>
		/// { }.GetOrCreateObject("foo").Set("bar", 123) => { "foo": { "bar": 123 } }
		/// { }.GetOrCreateObject("foo.bar").Set("baz", 123) => { "foo": { "bar": { "baz": 123 } } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonObject GetOrCreateObject(JsonPath path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonObject) SetPathInternal(path, null, JsonType.Object);
		}

		/// <summary>Returns a JSON Array at the given path, or create a new empty array if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the array</param>
		/// <returns>Existing array, or a new empty array.</returns>
		/// <example><code>
		/// { }.GetOrCreateArray("foo").Set(0, "bar") => { "foo": [ "bar" ] }
		/// { }.GetOrCreateArray("foo.bar").Set(0, "baz") => { "foo": { "bar": [ "baz" ] } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonArray GetOrCreateArray(string path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonArray) SetPathInternal(JsonPath.Create(path), null, JsonType.Array);
		}

		/// <summary>Returns a JSON Array at the given path, or create a new empty array if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the array</param>
		/// <returns>Existing array, or a new empty array.</returns>
		/// <example><code>
		/// { }.GetOrCreateArray("foo").Set(0, "bar") => { "foo": [ "bar" ] }
		/// { }.GetOrCreateArray("foo.bar").Set(0, "baz") => { "foo": { "bar": [ "baz" ] } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonArray GetOrCreateArray(ReadOnlyMemory<char> path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonArray) SetPathInternal(JsonPath.Create(path), null, JsonType.Array);
		}

		/// <summary>Returns a JSON Array at the given path, or create a new empty array if missing</summary>
		/// <param name="path"><see cref="JsonPath">path</see> to the array</param>
		/// <returns>Existing array, or a new empty array.</returns>
		/// <example><code>
		/// { }.GetOrCreateArray("foo").Set(0, "bar") => { "foo": [ "bar" ] }
		/// { }.GetOrCreateArray("foo.bar").Set(0, "baz") => { "foo": { "bar": [ "baz" ] } }
		/// </code></example>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		/// <exception cref="System.ArgumentNullException">If <paramref name="path"/> is <see langword="null"/> or empty</exception>
		/// <exception cref="System.ArgumentException">If any traversed node in the path is of an incompatible path. For example with <c>"foo[1].bar.baz"</c> if either <c>foo</c> is not an array, or <c>bar</c> is not an object</exception>
		public JsonArray GetOrCreateArray(JsonPath path)
		{
			JsonPath.ThrowIfEmpty(path);
			return (JsonArray) SetPathInternal(path, null, JsonType.Array);
		}

		/// <summary>Sets the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to set.</param>
		/// <param name="value">New value</param>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		public void SetPath(string path, JsonValue? value)
		{
			JsonPath.ThrowIfEmpty(path);
			SetPathInternal(JsonPath.Create(path), value ?? JsonNull.Null);
		}

		/// <summary>Sets the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to set.</param>
		/// <param name="value">New value</param>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		public void SetPath(ReadOnlyMemory<char> path, JsonValue? value)
		{
			JsonPath.ThrowIfEmpty(path);
			SetPathInternal(JsonPath.Create(path), value ?? JsonNull.Null);
		}

		/// <summary>Sets the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to set.</param>
		/// <param name="value">New value</param>
		/// <remarks>If any intermediate element in the traversed path is missing, it will be created as required (either as an object or an array)</remarks>
		public void SetPath(JsonPath path, JsonValue? value)
		{
			JsonPath.ThrowIfEmpty(path);
			SetPathInternal(path, value ?? JsonNull.Null);
		}

		private JsonValue SetPathInternal(JsonPath path, JsonValue? valueToSet, JsonType? expected = null)
		{
			JsonValue current = this;
			JsonValue? prevNode = null;
			ReadOnlyMemory<char> prevKey = default;
			Index prevIndex = default;
			foreach (var (parent, segment, last) in path.Tokenize())
			{
				if (segment.TryGetName(out var key))
				{ // field access

					// we have foo.bar, but foo was missing, so current = missing, key = "bar"
					// => we have to Set("foo", {}) on the parent... which itself must be an object

					if (current is not JsonObject obj)
					{
						if (!current.IsNullOrMissing())
						{ // incompatible type!
							throw ThrowHelper.InvalidOperationException($"Cannot set key '{key.ToString()}' because parent '{parent}' is not an object");
						}

						if (prevNode is null)
						{
							throw ThrowHelper.InvalidOperationException("Cannot update a null root object");
						}

						// we need to create the parent object first
						obj = JsonObject.Create();
						// and assign it to it's parent
						if (prevKey.Length > 0)
						{
							if (prevNode is not JsonObject prevObj)
							{
								throw ThrowHelper.InvalidOperationException($"Cannot set key '{key.ToString()}' because parent of '{parent}' is not an object");
							}
							prevObj.Set(prevKey, obj);
						}
						else
						{
							if (prevNode is not JsonArray prevArray)
							{
								throw ThrowHelper.InvalidOperationException($"Cannot set key '{key.ToString()}' because parent of '{parent}' is not an array");
							}
							prevArray.Set(prevIndex, obj);
						}
					}

					if (last)
					{ // the last token is a field access
						if (valueToSet is null)
						{ // we need to return the value or create it if required

							var actual = obj.GetValueOrDefault(key);

							if (expected == JsonType.Null)
							{ // means "delete"
								obj.Remove(key);
								return actual;
							}
							if (actual.IsNullOrMissing())
							{
								actual = expected == JsonType.Object ? JsonObject.Create() : expected == JsonType.Array ? JsonArray.Create() : throw new ArgumentException("Unsupported JSON type", nameof(expected));
								obj.Set(key, actual);
							}
							else if (actual.Type != expected)
							{
								throw new InvalidOperationException($"The specified key '{key}' exists, but is of type {actual.Type} instead of expected {expected}");
							}
							return actual;
						}
						else
						{ // we need to set the value
							obj.Set(key, valueToSet);
							return valueToSet;
						}
					}

					// we need to continue
					prevNode = obj;
					current = obj.GetValueOrDefault(key);
					prevKey = key;
					prevIndex = default;
				}
				else if (segment.TryGetIndex(out var idx))
				{ // array index

					if (current is not JsonArray arr)
					{
						if (!current.IsNullOrMissing())
						{ // incompatible type!
							throw ThrowHelper.InvalidOperationException($"Cannot set index {idx} because parent '{parent}' is not an array");
						}
						if (prevNode is null)
						{
							throw ThrowHelper.InvalidOperationException("Cannot update a null root array");
						}

						// we need to create the parent array first
						arr = JsonArray.Create();
						// and assign it to it's parent
						if (prevKey.Length > 0)
						{
							if (prevNode is not JsonObject prevObj)
							{
								throw ThrowHelper.InvalidOperationException($"Cannot set index {idx} because parent of '{parent}' is not an object");
							}
							prevObj.Set(prevKey, arr);
						}
						else
						{
							if (prevNode is not JsonArray prevArray)
							{
								throw ThrowHelper.InvalidOperationException($"Cannot set index {idx} because parent of '{parent}' is not an array");
							}
							prevArray.Set(prevIndex, arr);
						}
					}

					if (last)
					{ // the last token is an array index
						if (valueToSet is null)
						{ // we need to return the value or create it if required

							var actual = arr[idx];

							if (expected == JsonType.Null)
							{ // means "delete"
								arr.RemoveAt(idx);
								return actual;
							}

							if (actual.IsNullOrMissing())
							{
								actual = expected == JsonType.Object ? JsonObject.Create() : expected == JsonType.Array ? JsonArray.Create() : throw new ArgumentException("Unsupported JSON type", nameof(expected));
								arr[idx] = actual;
							}
							else if (actual.Type != expected)
							{
								throw new InvalidOperationException($"The specified index '{idx}' exists, but is of type {actual.Type} instead of expected {expected}");
							}
							return actual;
						}
						else
						{ // we need to set the value
							arr.Set(idx, valueToSet);
							return valueToSet;
						}
					}

					prevNode = arr;
					current = arr.GetValueOrDefault(idx);
					prevKey = default;
					prevIndex = idx;
				}
			}

			// we should not end up here!
			throw new InvalidOperationException();
		}

		/// <summary>Removes the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to remove.</param>
		/// <returns><see langword="true"/> if the value was found and was removed, or <see langword="false"/> if it was no present.</returns>
		/// <example>
		/// <c>{ "foo": { "bar": 123, "baz": 456 } }.RemovePath("foo.bar") => { "foo": { "baz": 456 } }</c>
		/// </example>
		public bool RemovePath(string path)
		{
			JsonPath.ThrowIfEmpty(path);
			return !SetPathInternal(JsonPath.Create(path), null, JsonType.Null).IsNullOrMissing();
		}

		/// <summary>Removes the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to remove.</param>
		/// <returns><see langword="true"/> if the value was found and was removed, or <see langword="false"/> if it was no present.</returns>
		/// <example>
		/// <c>{ "foo": { "bar": 123, "baz": 456 } }.RemovePath("foo.bar") => { "foo": { "baz": 456 } }</c>
		/// </example>
		public bool RemovePath(ReadOnlyMemory<char> path)
		{
			JsonPath.ThrowIfEmpty(path);
			return !SetPathInternal(JsonPath.Create(path), null, JsonType.Null).IsNullOrMissing();
		}

		/// <summary>Removes the value at the given path</summary>
		/// <param name="path"><see cref="JsonPath">path</see> of the value to remove.</param>
		/// <returns><see langword="true"/> if the value was found and was removed, or <see langword="false"/> if it was no present.</returns>
		/// <example>
		/// <c>{ "foo": { "bar": 123, "baz": 456 } }.RemovePath("foo.bar") => { "foo": { "baz": 456 } }</c>
		/// </example>
		public bool RemovePath(JsonPath path)
		{
			JsonPath.ThrowIfEmpty(path);
			return !SetPathInternal(path, null, JsonType.Null).IsNullOrMissing();
		}

		#endregion

		#region Setters...

		/// <summary>Sets the value of the field in this object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set<TValue>(string key, TValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			m_items[key] = FromValue(value);
			return this;
		}

		/// <summary>Sets the value of the field in this object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set<TValue>(ReadOnlySpan<char> key, TValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

#if NET9_0_OR_GREATER
			var items = m_items.GetAlternateLookup<ReadOnlySpan<char>>();
			items[key] = FromValue(value);
#else
			m_items[key.ToString()] = FromValue(value);
#endif
			return this;
		}

		/// <summary>Sets the value of the field in this object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set<TValue>(ReadOnlyMemory<char> key, TValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			// if we can get the original string, we won't need to allocate
			if (key.TryGetString(out var k))
			{
				m_items[k] = FromValue(value);
				return this;
			}

#if NET9_0_OR_GREATER
			// won't allocate if the key already exists
			var items = m_items.GetAlternateLookup<ReadOnlySpan<char>>();
			items[key.Span] = FromValue(value);
#else
			// we need to allocate in all cases, even if the key already exists
			m_items[key.ToString()] = FromValue(value);
#endif
			return this;
		}

		/// <summary>Sets the value of a field in the object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set(string key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			m_items[key] = value ?? JsonNull.Null;
			return this;
		}

		/// <summary>Sets the value of a field in the object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set(ReadOnlySpan<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

#if NET9_0_OR_GREATER
			var items = m_items.GetAlternateLookup<ReadOnlySpan<char>>();
			//note: this will not allocate if the key already exists
			items[key] = value ?? JsonNull.Null;
#else
			m_items[key.ToString()] = value ?? JsonNull.Null;
#endif
			return this;
		}

		/// <summary>Sets the value of a field in the object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set(ReadOnlyMemory<char> key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			if (key.TryGetString(out var k))
			{
				m_items[k] = value ?? JsonNull.Null;
				return this;
			}
#if NET9_0_OR_GREATER
			var items = m_items.GetAlternateLookup<ReadOnlySpan<char>>();
			//note: this will not allocate if the key already exists
			items[key.Span] = value ?? JsonNull.Null;
#else
			m_items[key.Span.ToString()] = value ?? JsonNull.Null;
#endif
			return this;
		}

		/// <summary>Sets the value of a field in the object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set(int key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			m_items[StringConverters.ToString(key)] = value ?? JsonNull.Null;
			return this;
		}

		/// <summary>Sets the value of a field in the object</summary>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		[CollectionAccess(CollectionAccessType.UpdatedContent)]
		public JsonObject Set(long key, JsonValue? value)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);

			m_items[StringConverters.ToString(key)] = value ?? JsonNull.Null;
			return this;
		}

		#endregion

		#region Merging...

		/// <summary>Copy the fields of an object onto the current object</summary>
		/// <param name="other">Object that will be merged with the current instance.</param>
		/// <param name="deepCopy">If <see langword="false"/> (default), copy the content of <paramref name="other"/> as-is; otherwise, clone all the elements before merging them.</param>
		/// <param name="keepNull">If <see langword="false"/> (default), fields set to null in <paramref name="other"/> will be removed; otherwise, they will be kept as null entries in the merged result.</param>
		public void MergeWith(JsonObject other, bool deepCopy = false, bool keepNull = false)
		{
			Merge(this, other, deepCopy, keepNull);
		}

		/// <summary>Copy the fields of an object onto the another object</summary>
		/// <param name="parent">Object that will be modified</param>
		/// <param name="other">Object that will be copied to the parent.</param>
		/// <param name="deepCopy">If <see langword="false"/> (default), copy the content of <paramref name="other"/> as-is; otherwise, clone all the elements before merging them.</param>
		/// <param name="keepNull">If <see langword="false"/> (default), fields set to null in <paramref name="other"/> will be removed; otherwise, they will be kept as null entries in the merged result.</param>
		public static JsonObject Merge(JsonObject parent, JsonObject? other, bool deepCopy = false, bool keepNull = false)
		{
			Contract.NotNull(parent);

			// cannot mutate a read-only object!
			if (parent.IsReadOnly) throw FailCannotMutateReadOnlyValue(parent);

			if (other is not null && other.Count > 0)
			{
				// recursively merge all properties:
				// - Copy the items from 'other', optionally merging them if they already exist in 'parent'
				// - If the new value is null or missing:
				//   - it will be set to null iif keepNull is true and the new value is an explicit null
				//   - otherwise, it will be removed (keepNull is false, or the new value is JsonNull.Missing)
				// - Merging is only supported between two objects or two arrays
				// - In all other cases, the value in 'other' will overwrite the previous value

				foreach (var kv in other)
				{
					if (!parent.TryGetValue(kv.Key, out var mine))
					{
						mine = JsonNull.Missing;
					}

					switch ((mine, kv.Value))
					{
						case (JsonObject a, JsonObject b):
						{ // merge two objects together
							Merge(a, b, deepCopy, keepNull);
							break;
						}
						case (JsonArray a, JsonArray b):
						{ // merge two arrays together
							JsonArray.Merge(a, b, deepCopy, keepNull);
							break;
						}
						case (_, JsonNull n):
						{ // remove value (or set to null)
							if (!keepNull || !ReferenceEquals(n, JsonNull.Null))
							{
								parent.Remove(kv.Key);
							}
							else
							{
								parent[kv.Key] = JsonNull.Null;
							}
							break;
						}
						default:
						{ // overwrite previous value
							parent[kv.Key] = deepCopy ? kv.Value.Copy() : kv.Value;
							break;
						}
					}
				}
			}
			return parent;
		}

		public JsonObject ComputePatch(JsonObject after, bool deepCopy = false, bool readOnly = false)
		{
			//note: we already know that there is a difference
			var patch = new JsonObject();

			var items = m_items;

			// mark for deletion any keys that are missing from 'after'
			foreach (var k in items.Keys)
			{
				if (!after.ContainsKey(k))
				{ // use explicit null to trigger a deletion when the patch is applied later
					patch[k] = JsonNull.Null;
				}
			}

			// add/update any new keys
			foreach (var kv in after)
			{
				if (!items.TryGetValue(kv.Key, out var p))
				{ // add new key
					if (!kv.Value.IsNullOrMissing())
					{
						var value = deepCopy ? kv.Value.Copy() : kv.Value;
						if (readOnly)
						{
							value = value.ToReadOnly();
						}
						patch[kv.Key] = value;
					}
				}
				else if (!p.Equals(kv.Value))
				{ // update/patch
					switch (p, kv.Value)
					{
						case (JsonObject a, JsonObject b):
						{
							patch[kv.Key] = a.ComputePatch(b, deepCopy, readOnly);
							break;
						}
						case (JsonArray a, JsonArray b):
						{
							patch[kv.Key] = a.ComputePatch(b, deepCopy, readOnly);
							break;
						}
						case (_, JsonNull):
						{ // use explicit null to trigger a deletion when the patch is applied later
							patch[kv.Key] = JsonNull.Null;
							break;
						}
						default:
						{
							var value = deepCopy ? kv.Value.Copy() : kv.Value;
							if (readOnly)
							{
								value = value.ToReadOnly();
							}
							patch[kv.Key] = value;
							break;
						}
					}
				}
			}

			if (readOnly)
			{
				patch.FreezeUnsafe();
			}

			return patch;
		}

		/// <summary>Apply a patch to the object (in place)</summary>
		/// <param name="patch">Object that will be copied to the parent.</param>
		/// <param name="deepCopy"></param>
		/// <returns>The same object instance</returns>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		public JsonObject ApplyPatch(JsonObject patch, bool deepCopy = false)
		{
			if (m_readOnly) throw FailCannotMutateImmutableValue(this);

			// recursively merge all properties:
			// - Copy the items from 'other', optionally merging them if they already exist in 'parent'
			// - If the new value is null or missing:
			//   - it will be set to null iif keepNull is true and the new value is an explicit null
			//   - otherwise, it will be removed (keepNull is false, or the new value is JsonNull.Missing)
			// - Merging is only supported between two objects or two arrays
			// - In all other cases, the value in 'other' will overwrite the previous value

			var items = m_items;

			foreach (var kv in patch)
			{
				if (!items.TryGetValue(kv.Key, out var mine))
				{
					mine = JsonNull.Missing;
				}

				switch ((mine, kv.Value))
				{
					case (JsonObject a, JsonObject b):
					{ // merge two objects together
						if (a.IsReadOnly)
						{
							a = a.ToMutable();
							items[kv.Key] = a;
						}
						a.ApplyPatch(b, deepCopy);
						break;
					}
					case (JsonArray a, JsonObject b) when (b.ContainsKey("__patch")):
					{ // merge two arrays (using the patch-object "form")
						if (a.IsReadOnly)
						{
							a = a.ToMutable();
							items[kv.Key] = a;
						}
						a.ApplyPatch(b, deepCopy);
						break;
					}
					case (_, JsonNull):
					{ // remove value (or set to null)
						items.Remove(kv.Key);
						break;
					}
					default:
					{ // overwrite previous value
						items[kv.Key] = deepCopy ? kv.Value.Copy() : kv.Value;
						break;
					}
				}
			}

			return this;
		}

		#endregion

		#region Projection...

		/// <summary>Generates a cacheable Picker that can extract a list of fields from a JSON Object</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(ReadOnlySpan<string> fields, bool removeFromSource = false)
		{
			var projections = CheckProjectionFields(fields, keepMissing: false);
			return (obj) => Project(obj, projections, removeFromSource);
		}

		/// <summary>Generates a cacheable Picker that can extract a list of fields from a JSON Object</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(IEnumerable<string> fields, bool keepMissing, bool removeFromSource = false)
		{
			var projections = CheckProjectionFields(fields as string[] ?? fields.ToArray(), keepMissing);
			return (obj) => Project(obj, projections, removeFromSource);
		}

		/// <summary>Generates a cacheable Picker that can extract a list of fields from a JSON Object</summary>
		public static Func<JsonObject, JsonObject> CreatePicker(IDictionary<string, JsonValue?> defaults, bool removeFromSource = false, bool keepMutable = false)
		{
			var projections = CheckProjectionDefaults(defaults);
			return (obj) => Project(obj, projections, removeFromSource, keepMutable);
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="false"/>, any field missing from the object will be omitted in the result. If <see langword="true"/>, they will be present but with a <see cref="JsonNull.Missing"/> value</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that only contains the values of the fields specified in <paramref name="fields"/></returns>
		public JsonObject Pick(ReadOnlySpan<string> fields, bool keepMissing = false, bool keepMutable = false)
		{
			return Project(this, CheckProjectionFields(fields, keepMissing), keepMutable: keepMutable);
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="false"/>, any field missing from the object will be omitted in the result. If <see langword="true"/>, they will be present but with a <see cref="JsonNull.Missing"/> value</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that only contains the values of the fields specified in <paramref name="fields"/></returns>
		public JsonObject Pick(string[] fields, bool keepMissing = false, bool keepMutable = false)
		{
			return Project(this, CheckProjectionFields(fields, keepMissing), keepMutable: keepMutable);
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="false"/>, any field missing from the object will be omitted in the result. If <see langword="true"/>, they will be present but with a <see cref="JsonNull.Missing"/> value</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that only contains the values of the fields specified in <paramref name="fields"/></returns>
		public JsonObject Pick(IEnumerable<string> fields, bool keepMissing = false, bool keepMutable = false)
		{
			return Project(this, CheckProjectionFields(fields as string[] ?? fields.ToArray(), keepMissing), keepMutable: keepMutable);
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that only contains the values of the fields specified in <paramref name="fields"/></returns>
		public JsonObject Project(ReadOnlySpan<(string Name, JsonPath Path, JsonValue? Fallback)> fields, bool keepMutable = false)
		{
			return Project(this, fields, keepMutable);
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="false"/>, any field missing from the object will be omitted in the result. If <see langword="true"/>, they will be present but with a <see cref="JsonNull.Missing"/> value</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that only contains the values of the fields specified in <paramref name="fields"/></returns>
		public JsonObject Project(ReadOnlySpan<(string Name, JsonPath Path)> fields, bool keepMissing = false, bool keepMutable = false)
		{
			return Project(this, CheckProjectionFields(fields, keepMissing), keepMutable);
		}

		/// <summary>Returns a new object containing only specific fields of this object</summary>
		/// <param name="defaults">List of fields to keep, with an optional default value</param>
		/// <returns>New object that contains only the fields specified in <paramref name="defaults"/></returns>
		public JsonObject PickFrom(IDictionary<string, JsonValue?> defaults)
		{
			return Project(this, CheckProjectionDefaults(defaults));
		}

		/// <summary>Returns a new object containing only specific fields of this object</summary>
		/// <param name="defaults">List of fields to keep, with an optional default value</param>
		/// <returns>New object that contains only the fields specified in <paramref name="defaults"/></returns>
		public JsonObject PickFrom(object defaults)
		{
			return Project(this, CheckProjectionDefaults(defaults));
		}

		/// <summary>Checks that the list of projection fields does not contain null, empty, or duplicates</summary>
		/// <param name="keys">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="true"/>, any field missing from the object will be present with value <see cref="JsonNull.Missing"/>; otherwise, they will be omitted.</param>
		[ContractAnnotation("keys:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionFields(ReadOnlySpan<string> keys, bool keepMissing)
		{
			var res = new KeyValuePair<string, JsonValue?>[keys.Length];
			var set = new HashSet<string>();
			int p = 0;

			foreach (var key in keys)
			{
				if (string.IsNullOrEmpty(key))
				{
					throw ThrowHelper.InvalidOperationException($"Cannot project empty or null field name: [{string.Join(", ", keys.ToArray())}]");
				}
				set.Add(key);
				res[p++] = new(key, keepMissing ? JsonNull.Missing : null);
			}
			if (set.Count != keys.Length)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot project duplicate field name: [{string.Join(", ", keys.ToArray())}]");
			}

			return res;
		}

		/// <summary>Checks that the list of projection fields does not contain null, empty, or duplicates</summary>
		/// <param name="fields">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMissing">If <see langword="true"/>, any field missing from the object will be present with value <see cref="JsonNull.Missing"/>; otherwise, they will be omitted.</param>
		internal static (string Name, JsonPath Path, JsonValue? Fallback)[] CheckProjectionFields(ReadOnlySpan<(string Name, JsonPath Path)> fields, bool keepMissing)
		{
			var res = new (string, JsonPath, JsonValue?)[fields.Length];
			var set = new HashSet<string>(StringComparer.Ordinal);
			int p = 0;

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field.Name))
				{
					throw ThrowHelper.InvalidOperationException($"Cannot project empty or null field name: [{string.Join(", ", fields.ToArray())}]");
				}
				if (field.Path.IsEmpty())
				{
					throw ThrowHelper.InvalidOperationException($"Cannot project empty field path: [{string.Join(", ", fields.ToArray())}]");
				}
				set.Add(field.Name);
				res[p++] = (field.Name, field.Path, keepMissing ? JsonNull.Missing : null);
			}
			if (set.Count != fields.Length)
			{
				throw ThrowHelper.InvalidOperationException($"Cannot project duplicate field name: [{string.Join(", ", fields.ToArray())}]");
			}

			return res;
		}

		/// <summary>Checks that the list of projection fields does not contain null, empty, or duplicates</summary>
		/// <param name="defaults">List of keys to project, with their default value</param>
		/// <remarks>If a field is missing in the source object, the default value is used, unless it is null.</remarks>
		[ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(IDictionary<string, JsonValue?> defaults)
		{
			Contract.NotNull(defaults);

			var res = new KeyValuePair<string, JsonValue?>[defaults.Count];
			var set = new HashSet<string>();
			int p = 0;

			foreach (var kvp in defaults)
			{
				if (string.IsNullOrEmpty(kvp.Key))
				{
					ThrowHelper.ThrowInvalidOperationException($"Cannot project empty or null field name: [{string.Join(", ", defaults.Select(x => x.Key))}]");
				}

				set.Add(kvp.Key);
				res[p++] = kvp;
			}

			if (set.Count != defaults.Count)
			{
				ThrowHelper.ThrowInvalidOperationException($"Cannot project duplicate field name: [{string.Join(", ", defaults.Select(x => x.Key))}]");
			}

			return res;
		}

		[ContractAnnotation("defaults:null => halt")]
		internal static KeyValuePair<string, JsonValue?>[] CheckProjectionDefaults(object defaults)
		{
			Contract.NotNull(defaults);

			var obj = JsonObject.ReadOnly.FromObject(defaults);
			Contract.Debug.Assert(obj is not null);
			return obj.ToArray()!;
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="item">Source JSON object</param>
		/// <param name="defaults">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="removeFromSource">If <see langword="true"/>, any projected field will be removed from the source. If <see langword="false"/>, they will be copied into the resulting object</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that contains the selected fields from the source, or their default values.</returns>
		/// <remarks><code>
		/// { "A": 1, "C": false }.Project({ "A": 0, "B": 42, "C": true}) => { "A": 1, "B": 42, "C": false }
		/// </code></remarks>
		internal static JsonObject Project(JsonObject item, ReadOnlySpan<KeyValuePair<string, JsonValue?>> defaults, bool removeFromSource = false, bool keepMutable = false)
		{
			Contract.Debug.Requires(item is not null);

			if (removeFromSource && item.IsReadOnly)
			{
				throw new InvalidOperationException("Cannot remove picked fields from a read-only source");
			}

			var obj = new JsonObject(defaults.Length, item.Comparer);
			foreach (var prop in defaults)
			{
				if (item.TryGetValue(prop.Key, out var value))
				{
					obj[prop.Key] = value;
					if (removeFromSource)
					{
						item.Remove(prop.Key);
					}
				}
				else if (prop.Value is not null)
				{
					obj[prop.Key] = prop.Value;
				}
			}

			// keep the "readonly-ness" of the original, unless specified otherwise
			if (item.IsReadOnly && !keepMutable)
			{
				obj = obj.FreezeUnsafe();
			}

			return obj;
		}

		/// <summary>Returns a new object that only contains the specified fields of this instance</summary>
		/// <param name="item">Source JSON object</param>
		/// <param name="defaults">List of the names of the fields to keep, each with a default value if they are missing from the source</param>
		/// <param name="keepMutable">If <see langword="false"/>, the created object will be marked as read-only if the source is already read-only; otherwise, it will be mutable.</param>
		/// <returns>New object that contains the selected fields from the source, or their default values.</returns>
		internal static JsonObject Project(JsonObject item, ReadOnlySpan<(string Name, JsonPath Path, JsonValue? Fallback)> defaults, bool keepMutable = false)
		{
			Contract.Debug.Requires(item is not null);

			var obj = new JsonObject(defaults.Length, item.Comparer);
			foreach (var prop in defaults)
			{
				if (item.TryGetPathValue(prop.Path, out var value))
				{
					obj[prop.Name] = value;
				}
				else if (prop.Fallback is not null)
				{
					obj[prop.Name] = prop.Fallback;
				}
			}

			// keep the "readonly-ness" of the original, unless specified otherwise
			if (item.IsReadOnly && !keepMutable)
			{
				obj = obj.FreezeUnsafe();
			}

			return obj;
		}

		#endregion

		#region Filtering...

		/// <summary>Returns a new object that only includes the fields of the original that passed a given filter</summary>
		/// <param name="value">Original object</param>
		/// <param name="filter">Filter that is called for each field of the original. If the filter returns <see langword="false"/>, for a field, it will not be copied into the result.</param>
		/// <param name="deepCopy">If <see langword="true"/>, performs a deep copy of the fields that pass the filter. If <see langword="false"/>, copy them by reference. Has no effect for fields that are already read-only.</param>
		/// <returns>New object with only the fields that passed the filter.</returns>
		/// <remarks>If all fields are discarded, the returned object will be empty.</remarks>
		internal static JsonObject Without(JsonObject value, Func<string, bool> filter, bool deepCopy)
		{
			Contract.Debug.Requires(value is not null && filter is not null);

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach (var item in value)
			{
				if (!filter(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy() : item.Value;
				}
			}
			return obj;
		}

		/// <summary>Returns a new object with only the fields of the original whose names are present in a given list</summary>
		/// <param name="value">Original object</param>
		/// <param name="filtered">List of names of fields that must be copied. Any field not present in this set will be discarded.</param>
		/// <param name="deepCopy">If <see langword="true"/>, performs a deep copy of the fields that pass the filter. If <see langword="false"/>, copy them by reference. Has no effect for fields that are already read-only.</param>
		/// <returns>New object with only the fields that passed the filter.</returns>
		/// <remarks>If all fields are discarded, the returned object will be empty.</remarks>
		internal static JsonObject Without(JsonObject value, HashSet<string> filtered, bool deepCopy)
		{
			Contract.Debug.Requires(value is not null && filtered is not null);

			var obj = new JsonObject(value.Count, value.Comparer);
			foreach (var item in value)
			{
				if (!filtered.Contains(item.Key))
				{
					obj[item.Key] = deepCopy ? item.Value.Copy() : item.Value;
				}
			}
			return obj;
		}

		/// <summary>Returns a new object without the specified field from the original</summary>
		/// <param name="value">Original object</param>
		/// <param name="field">Name of the field that must be omitted.</param>
		/// <param name="deepCopy">If <see langword="true"/>, performs a deep copy of the fields that pass the filter. If <see langword="false"/>, copy them by reference. Has no effect for fields that are already read-only.</param>
		/// <returns>New object with all the fields of the original, except the one with the same name as <paramref name="field"/>.</returns>
		/// <remarks>If all fields are discarded, the returned object will be empty.</remarks>
		internal static JsonObject Without(JsonObject value, string field, bool deepCopy)
		{
			Contract.Debug.Requires(value is not null && field is not null);

			var obj = Copy(value, deepCopy, readOnly: false);
			obj.Remove(field);
			return obj;
		}

		/// <summary>Returns a new object that only includes the fields of the original that passed a given filter</summary>
		/// <param name="filter">Filter that is called for each field of the object. If the filter returns <see langword="false"/>, for a field, it will not be copied into the result.</param>
		/// <param name="deepCopy">If <see langword="true"/>, performs a deep copy of the fields that pass the filter. If <see langword="false"/>, copy them by reference. Has no effect for fields that are already read-only.</param>
		/// <returns>New object with only the fields that passed the filter.</returns>
		public JsonObject Without(Func<string, bool> filter, bool deepCopy = false)
		{
			Contract.NotNull(filter);
			return Without(this, filter, deepCopy);
		}

		/// <summary>Returns a new object, without the specified field</summary>
		/// <param name="fieldToRemove">Name of the field that should be omitted, if present.</param>
		/// <param name="deepCopy">If <see langword="true"/>, performs a deep copy of the fields that pass the filter. If <see langword="false"/>, copy them by reference. Has no effect for fields that are already read-only.</param>
		/// <returns>New object that does not exclude the specified field.</returns>
		public JsonObject Without(string fieldToRemove, bool deepCopy = false)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			return Without(this, fieldToRemove, deepCopy);
		}

		/// <summary>Removes a field from this object</summary>
		/// <param name="fieldToRemove">Name of the field to remove</param>
		/// <returns>The same object, but with the field removed (if it was present)</returns>
		/// <remarks>This method is identical to <see cref="Remove(string)"/>, be can be chained with another call</remarks>
		public JsonObject Erase(string fieldToRemove)
		{
			Contract.NotNullOrEmpty(fieldToRemove);
			this.Remove(fieldToRemove);
			return this;
		}

		#endregion

		#region Sorting...

		private static bool TrySortValue(JsonValue item, IComparer<string> comparer, [MaybeNullWhen(false)] out JsonValue result)
		{
			result = null!;

			switch (item)
			{
				case JsonObject obj:
				{
					if (TrySortByKeys(obj.m_items, comparer, out var subItems))
					{
						result = new JsonObject(subItems, obj.m_readOnly);
						return true;
					}

					return false;
				}
				case JsonArray arr:
				{
					// only allocate the buffer if at least one child has changed
					JsonValue[]? items = null;
					for (int i = 0; i < arr.Count; i++)
					{
						if (TrySortValue(arr[i], comparer, out var val))
						{
							(items ??= arr.ToArray())[i] = val;
						}
					}

					if (items is not null)
					{ // at least one change
						result = new JsonArray(items, items.Length, arr.IsReadOnly);
						return true;
					}

					return false;
				}
				default:
				{
					return false;
				}
			}
		}

		/// <summary>Order the keys of a dictionary, using the specified comparer</summary>
		/// <returns><see langword="false"/> if the object was already ordered, or <see langword="true"/> if the object has been changed to re-order the keys.</returns>
		/// <remarks>This is used to guarantee that serializing a JSON object produces the same text or bytes, preserving equality and checksum.</remarks>
		private static bool TrySortByKeys(Dictionary<string, JsonValue> items, IComparer<string> comparer, [MaybeNullWhen(false)] out Dictionary<string, JsonValue> result)
		{
			//ATTENTION: this assumes that currently (as of .NET 8) a Dictionary<TKey,TValue> preserves the insertion order of keys, as long as there are no deletions, meaning that enumerating the Dictionary will yield the same order.
			// => it is unlikely that this will ever change, because it would break a lot of code. But if this happens, we will need to find a different solution!

			Contract.Debug.Requires(items is not null && comparer is not null);
			result = null!;

			int count = items.Count;
			if (count == 0)
			{ // nothing to do
				return false;
			}

			bool changed = false;

			// each value needs to be sorted recursively
			var valuesArray = ArrayPool<JsonValue>.Shared.Rent(count);
			items.Values.CopyTo(valuesArray, 0);
			var values = valuesArray.AsSpan(0, count);

			for (int i = 0; i < values.Length; i++)
			{
				if (TrySortValue(values[i], comparer, out var val))
				{
					values[i] = val;
					changed = true;
				}
			}

			// order by the keys
			var keysArray = ArrayPool<string>.Shared.Rent(count);
			items.Keys.CopyTo(keysArray, 0);
			var keys = keysArray.AsSpan(0, count);

			var indexesArray = ArrayPool<int>.Shared.Rent(count);
			var indexes = indexesArray.AsSpan(0, count);
			for (int i = 0; i < indexes.Length; i++)
			{
				indexes[i] = i;
			}

#if NET5_0_OR_GREATER
			keys.Sort(indexes, comparer);
#else
			// the span-based key/value Sort is not available on netstandard2.0: sort the backing arrays directly
			Array.Sort(keysArray, indexesArray, 0, count, comparer);
#endif

			if (!changed)
			{
				// If all keys where already ordered, the array of indexes will stay ordered as well [0, 1, 2, ..., N-1].
				// => in this case, we don't have to create a copy, and simply need to return the same instance
				for (int i = 0; i < indexes.Length; i++)
				{
					if (indexes[i] != i)
					{ // there was at least one swap!
						changed = true;
						break;
					}
				}
			}

			if (changed)
			{ // order was changed, need to create a new object
				result = new Dictionary<string, JsonValue>(keys.Length, items.Comparer);
				for (int i = 0; i < keys.Length; i++)
				{
					result[keys[i]] = values[indexes[i]];
				}
			}

			// return the buffers to the pool
			indexes.Clear();
			ArrayPool<int>.Shared.Return(indexesArray);
			keys.Clear();
			ArrayPool<string>.Shared.Return(keysArray);
			values.Clear();
			ArrayPool<JsonValue>.Shared.Return(valuesArray);

			return changed;
		}

		/// <summary>Order the keys of this object</summary>
		/// <param name="comparer">Optional key comparer, or <see cref="StringComparer.Ordinal"/> if omitted.</param>
		/// <exception cref="T:System.InvalidOperationException">The object is read-only.</exception>
		public void SortKeys(IComparer<string>? comparer = null)
		{
			if (m_readOnly) throw FailCannotMutateReadOnlyValue(this);
			if (TrySortByKeys(m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				m_items.Clear();
				foreach (var kvp in items)
				{
					m_items[kvp.Key] = kvp.Value;
				}
			}
		}

		/// <summary>Returns a new JSON Object, with the same content as the original, but with the keys sorted</summary>
		/// <param name="map">Original JSON Object. This object will not be modified.</param>
		/// <param name="comparer">Optional key comparer, or <see cref="StringComparer.Ordinal"/> if omitted.</param>
		/// <returns>Shallow copy of the object, with the keys sorted using the specified key comparer</returns>
		public static JsonObject OrderedByKeys(JsonObject map, IComparer<string>? comparer = null)
		{
			Contract.NotNull(map);

			if (TrySortByKeys(map.m_items, comparer ?? StringComparer.Ordinal, out var items))
			{
				return new JsonObject(items, map.m_readOnly);
			}

			//TODO: to copy or not to copy?
			return map;
		}

		/// <summary>Returns a copy of this JSON Object, with the same content as the original, but with the keys sorted</summary>
		public JsonObject OrderedByKeys(IComparer<string>? comparer = null)
		{
			return OrderedByKeys(this, comparer);
		}

		#endregion

		#region Conversion...

		internal override bool IsSmallValue()
		{
			const int LARGE_OBJECT = 5;
			if (m_items.Count >= LARGE_OBJECT)
			{
				return false;
			}

			foreach (var v in m_items.Values)
			{
				if (v.IsSmallValue())
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
			const int MAX_ITEMS = 4;

			if (m_items.Count == 0) return "{ }"; // empty

			// We will output up to 4 fields.
			// If the value of a field is "small" it is written entirely. If not, it will be replaced with '...'

			var sb = new StringBuilder("{ ");
			int i = 0;
			foreach (var kv in m_items)
			{
				if (i >= MAX_ITEMS) { sb.AppendInvariant($", /* \u2026 {(m_items.Count - MAX_ITEMS):N0} more */"); break; }
				if (i > 0) sb.Append(", ");

				sb.Append(kv.Key).Append(": ");
				if (depth == 0 || kv.Value.IsSmallValue())
				{
					sb.Append(kv.Value.GetCompactRepresentation(depth + 1));
				}
				else
				{
					switch (kv.Value.Type)
					{
						case JsonType.Object: sb.Append("{\u2026}"); break;
						case JsonType.Array: sb.Append("[\u2026]"); break;
						case JsonType.String: sb.Append("\"\u2026\""); break;
						default: sb.Append('\u2026'); break;
					}
				}
				i++;
			}
			sb.Append(" }");
			return sb.ToString();
		}

		/// <summary>Converts this JSON Object into a <see cref="Dictionary{TKey,TValue}">Dictionary&lt;string, object?&gt;</see>.</summary>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override object? ToObject()
		{
#pragma warning disable IL2026
			return CrystalJsonParser.DeserializeCustomClassOrStruct(this, typeof(object), CrystalJson.DefaultResolver);
#pragma warning restore IL2026
		}

		/// <inheritdoc />
		public override TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
			where TValue : default
		{
			TValue? res;
			if (resolver is not null && !ReferenceEquals(resolver, CrystalJson.DefaultResolver))
			{
				if (!resolver.TryGetConverterFor<TValue>(out var converter))
				{
					throw new NotSupportedException(); //TODO: error message!
				}
				res = converter.Unpack(this, resolver);
			}
			else
			{
				res = (TValue?) CrystalJson.DefaultResolver.BindJsonObject(typeof(TValue), this);
			}

			if (res is null)
			{
				return default(TValue) is null && (typeof(TValue) == typeof(JsonValue) || typeof(TValue) == typeof(JsonNull))
					? (defaultValue ?? (TValue?) (object?) JsonNull.Null)
					: defaultValue;
			}
			return (TValue?) res;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
		{
			if (resolver is not null && !ReferenceEquals(resolver, CrystalJson.DefaultResolver))
			{
				if (!resolver.TryGetConverterFor(type ?? typeof(object), out var converter))
				{
					throw new NotSupportedException(); //TODO: error message!
				}

				return converter.BindJsonValue(this, resolver);
			}
			return CrystalJson.DefaultResolver.BindJsonObject(type, this);
		}

		#endregion

		#region IJsonSerializable

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (m_items.Count == 0)
			{
				return settings.IsCompactLayout() ? "{}" : "{ }";
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
			var items = m_items;
			if (items.Count == 0)
			{
				writer.WriteEmptyObject();
				return;
			}

			var state = writer.BeginObject();
			foreach (var item in items)
			{
				// first check if the value is not a discarded null or default
				if (!writer.WillBeDiscarded(item.Value))
				{
					//note: the key may require escaping!
					writer.WriteNameEscaped(item.Key);
					item.Value.JsonSerialize(writer);
				}
			}
			writer.EndObject(state);
		}

		/// <inheritdoc />
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			string? literal;

			switch (format)
			{
				case "" or "D" or "d" or "N" or "n":
				{
					return TryFormatNormal(m_items, destination, out charsWritten);
				}
				case "C" or "c":
				{
					return TryFormatCompact(m_items, destination, out charsWritten);
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
		public override bool Equals(JsonValue? other) => other is JsonObject obj && Equals(obj);

		/// <inheritdoc />
		public bool Equals(JsonObject? other)
		{
			if (other is null || other.Count != this.Count)
			{
				return false;
			}

			var otherItems = other.m_items;
			foreach (var kvp in this)
			{
				if (!otherItems.TryGetValue(kvp.Key, out var o) || !o.Equals(kvp.Value))
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Tests if two objects are considered equal, using a custom equality comparer</summary>
		public bool Equals(JsonObject? other, IEqualityComparer<JsonValue>? comparer)
		{
			if (other is null || other.Count != this.Count)
			{
				return false;
			}
			comparer ??= JsonValueComparer.Default;
			var otherItems = other.m_items;
			foreach (var kvp in this)
			{
				if (!otherItems.TryGetValue(kvp.Key, out var o) || !comparer.Equals(o, kvp.Value))
				{
					return false;
				}
			}
			return true;
		}

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonObject obj && StrictEquals(obj);

		private static int CountNonNullOrMissingValues(Dictionary<string, JsonValue> items)
		{
			int n = 0;
			foreach (var v in items.Values)
			{
				if (!v.IsNullOrMissing()) { ++n; }
			}
			return n;
		}

		static bool StrictEqualsEnumerable(Dictionary<string, JsonValue> items, Dictionary<string, JsonValue> other)
		{
			Contract.Debug.Requires(items != null && other != null);

			// We have to take special care of null/missing entries, because an explicit null field on one side is equivalent to the field missing on the other side:
			// - if 'other' contains an item with a value that is null/missing, then 'this' should either not have the key, or the key should also be null/missing
			// - if 'this' contains an item that is not in 'other', then its value should be null/missing
			// ex: { "foo": 123, "bar": null } and { "foo": 123, "baz": null } are strictly equal, because they both have foo==123, and all the other elements are null/missing

			// first, check if all items in 'other' are also in 'this' (except null fields that should not have a value in 'this')
			// we also maintain a count of 'non-null' values in 'other', to speed up the second check
			int otherCount = 0;
			foreach (var (k, v) in other)
			{
				//TODO: handle case where v == JsonNull.Missing ?
				if (v.IsNullOrMissing())
				{
					if (items.TryGetValue(k, out var o) && !o.IsNullOrMissing())
					{ // we have a value for this, even though we want it to be null or missing
						return false;
					}
				}
				else
				{
					if (!items.TryGetValue(k, out var o) || !o.StrictEquals(v))
					{ // we don't have a value, or it is not equal to what we are expecting
						return false;
					}
					++otherCount;
				}
			}

			// if there was N non-null items in 'other', and we did not find any difference, then 'this' should also have N non-null items
			// (technically it cannot have less than N, otherwise we would have already returned false above)
			return otherCount == CountNonNullOrMissingValues(items);
		}

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		[Pure]
		public bool StrictEquals(JsonObject? other)
		{
			return other is not null && StrictEqualsEnumerable(m_items, other.m_items);
		}

		/// <inheritdoc cref="StrictEquals(JsonValue?)" />
		[Pure]
		public bool StrictEquals(Dictionary<string, JsonValue>? other)
		{
			return other is not null && StrictEqualsEnumerable(m_items, other);
		}

		/// <inheritdoc />
		public override int GetHashCode()
		{
			if (!this.IsReadOnly)
			{
				// the hash code must NEVER change, even if the object is mutated!
				return RuntimeHelpers.GetHashCode(this);
			}
			else
			{
				return ComputeReadOnlyHashCode(m_items);
			}

			static int ComputeReadOnlyHashCode(Dictionary<string, JsonValue> items)
			{
				//note: the order of the keys is not significant, but it would be too costly to order them
				// => we take the middle road of XORing all the hashcodes for the keys together (same for the values), since XOR is commutative
				// => this means the hashcode will be of a lesser quality!

				int keyHash = 0;
				int valueHash = 0;
				foreach (var kv in items)
				{
					keyHash ^= kv.Key.GetHashCode();
					valueHash ^= kv.Value.GetHashCode();
				}

				return HashCode.Combine(items.Count, keyHash, valueHash);
			}
		}

		/// <inheritdoc />
		public override int CompareTo(JsonValue? other)
		{
			throw new NotSupportedException("JSON Object cannot be compared with other elements");
		}

		#endregion

		public ExpandoObject ToExpando()
		{
			var expando = new ExpandoObject();
			var map = (IDictionary<string, object?>) expando;
			foreach (var kvp in m_items)
			{
				map.Add(kvp.Key, kvp.Value.ToObject());
			}
			return expando;
		}

		public KeyValuePair<string, JsonValue>[] ToArray()
		{
			var res = new KeyValuePair<string, JsonValue>[m_items.Count];
			CopyTo(res, 0);
			return res;
		}

		public Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(ICrystalJsonTypeResolver? resolver = null)
			where TKey : notnull
		{
			return Bind<Dictionary<TKey, TValue>>(null, resolver)!;
		}

		public void CopyTo(KeyValuePair<string, JsonValue>[] array)
		{
			((ICollection<KeyValuePair<string, JsonValue>>) m_items).CopyTo(array, 0);
		}

		/// <inheritdoc />
		public void CopyTo(KeyValuePair<string, JsonValue>[] array, int arrayIndex)
		{
			((ICollection<KeyValuePair<string, JsonValue>>) m_items).CopyTo(array, arrayIndex);
		}

		public void CopyTo(Span<KeyValuePair<string, JsonValue>> array)
		{
			if (!TryCopyTo(array))
			{
				throw new ArgumentException("Destination is too small");
			}
		}

		public bool TryCopyTo(Span<KeyValuePair<string, JsonValue>> array)
		{
			if (m_items.Count > array.Length)
			{
				return false;
			}
			int p = 0;
			foreach (var kv in m_items)
			{
				array[p++] = kv;
			}
			return true;
		}

		public void CopyTo(Span<(string Key, JsonValue Value)> array)
		{
			if (!TryCopyTo(array))
			{
				throw new ArgumentException("Destination is too small");
			}
		}

		public bool TryCopyTo(Span<(string Key, JsonValue Value)> array)
		{
			if (m_items.Count > array.Length)
			{
				return false;
			}
			int p = 0;
			foreach (var kv in m_items)
			{
				array[p++] = (kv.Key, kv.Value);
			}
			return true;
		}

		public override void WriteTo(ref SliceWriter writer)
		{
			writer.WriteByte('{');
			bool first = true;
			foreach (var kv in this)
			{
				// by default, we don't serialize "Missing" values
				if (kv.Value.IsMissing()) break;

				if (first)
				{
					first = false;
				}
				else
				{
					writer.WriteByte(',');
				}

				if (JsonEncoding.NeedsEscaping(kv.Key))
				{
					writer.WriteStringUtf8(JsonEncoding.EncodeSlow(kv.Key));
				}
				else
				{
					writer.WriteByte('"');
					writer.WriteStringUtf8(kv.Key);
					writer.WriteByte('"');
				}
				writer.WriteByte(':');
				kv.Value.WriteTo(ref writer);
			}
			writer.WriteByte('}');
		}

	}

}
