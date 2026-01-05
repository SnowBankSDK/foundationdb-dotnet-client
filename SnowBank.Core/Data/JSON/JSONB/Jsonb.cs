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

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable IdentifierTypo

namespace SnowBank.Data.Json.Binary
{
	using System.Buffers;
	using System.Buffers.Binary;
	using System.Buffers.Text;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using System.Text;
	using System.Text.Unicode;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;
	using SnowBank.Runtime.Converters;
	using SnowBank.Text;

	// Adapted from: http://git.postgresql.org/gitweb/?p=postgresql.git;a=blob_plain;f=src/include/utils/jsonb.h;hb=def4c28cf9147472ba4cfc5b68a098d7a29fb0fb
	// This is NOT binary compatible with the original spec!

	/// <summary>Compact binary JSON encoder that supports partial decoding of documents</summary>
	/// <remarks>
	/// <para>Generates a binary payload intended for cases where only part of large documents would be decoded (for indexing, filtering, ...)</para>
	/// <para>It is optimized for "random traversal" from the root to specific fields, without having to decode the entire document.</para>
	/// <para>All items have a size prefix as well as an optional offset table that quickly allows jumping to the desired location.</para>
	/// <para>For cases where the document is small, or will always need to be decoded completely, please consider using <see cref="JsonPack"/> instead.</para>
	/// </remarks>
	[PublicAPI]
	public static class Jsonb
	{
		#region Format Specifications...

		// This is an adaptation of the jsonb (aka hstore2) from Postgres 9.4+, that has been changed according to our needs.
		// The basic principle is the same (structured json, designed so that it is very easy to extract specific values without having to parse everything).

		// All header fields and containers are always aligned to 32 bits. Meaning that the total size of a jsonb document is always divisible by 4 bytes. Padding is used to fill the gaps.
		// Unless specified otherwise, all 32 bits fields are stored in little-endian, so 0x12345678 will be stored as the four bytes { 0x78, 0x56, 0x34, 0x12 }.

		#region Header...

		// The header is a 32 bits prefix with the following format:
		// - bit 31      : EXTRA      : [TBD!] If set, means that the header is immediately followed by an extra 32 bits of flags.
		// - bit 30      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bit 29      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bit 28      : RESERVED   : Reserved for future use. Must be 0 in current version
		// - bits 0..27  : TOTAL_SIZE : Total size (including this header, but excluding the padding) of the document. If the value is not a multiple of 4, there should be from 1 to 3 additional padding bytes (0) at the end of the document.

		// Since the total size must fit in 28 bits, it means that the maximum allowed data size is 268 435 455 (1 GB minus 1 byte). Chunking must be used to store more data.
		// If multiple jsonb documents are sent via a stream, it is possible to know the size of the block to read by rounding up TOTAL_SIZE to the next multiple of 4, and reading that number of bytes from the stream.

		/// <summary>Flag indicating the presence of an extra 32-bits field</summary>
		private const uint HEADER_FLAGS_EXTRA = 0x80_00_00_00U;
		/// <summary>Flags reserved for future use (should be set to 0)</summary>
		private const uint HEADER_FLAGS_RESERVED = 0x70_00_00_00U;

		private const uint HEADER_SIZE_MASK = 0x0F_FF_FF_FFU; // 28 bits

		private const uint JCONTAINER_COUNT_MASK	= 0x0FFFFFFF; /* mask for size */
		private const uint JCONTAINER_FLAG_SCALAR	= 0x10000000; /* flag bits */
		private const uint JCONTAINER_FLAG_OBJECT	= 0x20000000;
		private const uint JCONTAINER_FLAG_ARRAY	= 0x40000000;
		private const uint JCONTAINER_FLAG_HASHED	= 0x80000000;
		private const int JCONTAINER_CHILDREN_OFFSET = 4; // offset (in the container) to the start of the JEntries
		private const int JCONTAINER_OFFSET_STRIDE = 32; // number of entries between two jump offsets

		private const int JENTRY_SIZEOF = 4;
		private const int JENTRY_OFFLENBITS = 28;
		private const uint JENTRY_OFFLENMASK = (1 << JENTRY_OFFLENBITS) - 1;
		private const uint JENTRY_TYPEMASK = 0x70000000;
		private const uint JENTRY_HAS_OFF = 0x80000000;

		private const uint JENTRY_TYPE_STRING		= 0x00000000;
		private const uint JENTRY_TYPE_INTEGER		= 0x10000000; // [CHANGED] (changed to 64-bit varint)
		private const uint JENTRY_TYPE_FALSE		= 0x20000000;
		private const uint JENTRY_TYPE_TRUE			= 0x30000000;
		private const uint JENTRY_TYPE_NULL			= 0x40000000;
		private const uint JENTRY_TYPE_CONTAINER	= 0x50000000;
		private const uint JENTRY_TYPE_NUMERIC		= 0x60000000; // [ADDITION] (text literal, unaligned)
		private const uint JENTRY_TYPE_RESERVED		= 0x70000000;

		#endregion

		#endregion

		#region Nested Structs...

		private enum JType
		{
			//note: these values must be identical to the JENTRY_TYPE_xxx constants
			String = 0,		// 000
			Numeric = 6,	// 001
			False = 2,		// 010
			True = 3,		// 011
			Null = 4,		// 100
			Container = 5,	// 101
			Integer = 1,	// 110 [ADDITION]
			Reserved = 7,	// 111
		}

		private readonly ref struct JContainer
		{

			public JContainer(ReadOnlySpan<byte> data)
			{
				uint header = BinaryPrimitives.ReadUInt32LittleEndian(data);

				this.Data = data;
				this.Header = header;
				this.BaseAddress = GetBaseAddress(header);
			}

			private readonly ReadOnlySpan<byte> Data;

			private readonly uint Header; // 4 flags + 28 bit count

			/// <summary>Offset (relative to the container) where the data starts (items, or key+values)</summary>
			/// <remarks>If this is a Hashed Object, points directly to after the hashmap/idxmap</remarks>
			private readonly int BaseAddress;

			public int Count
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (int) (this.Header & JCONTAINER_COUNT_MASK);
			}

			public bool IsScalar
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_SCALAR) != 0;
			}

			public bool IsObject
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_OBJECT) != 0;
			}

			public bool IsArray
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_ARRAY) != 0;
			}

			public bool IsHashed
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (this.Header & JCONTAINER_FLAG_HASHED) != 0;
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			internal static int GetIndexMapStride(int count)
			{
				if (count < (1 << 8)) return 1;
				if (count < (1 << 16)) return 2;
				Contract.Debug.Assert(count < (1 << 24));
				return 3;
			}

			[Pure]
			private static int GetBaseAddress(uint header)
			{
				int count = (int) (header & JCONTAINER_COUNT_MASK);
				int numEntries = count;
				if ((header & JCONTAINER_FLAG_OBJECT) != 0) numEntries <<= 1;

				int baseAddress = JCONTAINER_CHILDREN_OFFSET + numEntries * JENTRY_SIZEOF;
				if ((header & JCONTAINER_FLAG_HASHED) != 0)
				{ // skip the hashmap + idxmap
					baseAddress += Aligned(count * (4 + GetIndexMapStride(count)));
				}
				return baseAddress;
			}

			/// <summary>Decodes the metadata about an item on an array</summary>
			/// <param name="index">Index of the item in the array (0 ... N-1)</param>
			/// <param name="numEntries">Size of the array</param>
			/// <param name="result">Decoded value</param>
			public void GetContainerEntry(int index, int numEntries, out JValue result)
			{
				// les données démarrent juste après les children
				if ((uint) index >= numEntries) throw ThrowHelper.ArgumentOutOfRangeIndex(index);

				int offset = GetJsonbOffset(index);
				int len = GetJsonbLength(index, offset);

				int childOffset = checked(JCONTAINER_CHILDREN_OFFSET + index * JENTRY_SIZEOF);
				uint entry = BinaryPrimitives.ReadUInt32LittleEndian(this.Data.Slice(childOffset));

				GetEntryAt(index, entry, offset, len, out result);
			}

			/// <summary>Returns a JValue corresponding to the item at the specified index</summary>
			/// <param name="index">Index of the entry (in the jentries table)</param>
			/// <param name="entry">Value of the entry (in the jentries table)</param>
			/// <param name="dataOffset">Offset (relative) to the start of the data (or 0 if no data)</param>
			/// <param name="dataLen">Size (in bytes) of the data (or 0 if no data)</param>
			/// <param name="result">Decoded value</param>
			private void GetEntryAt(int index, uint entry, int dataOffset, int dataLen, out JValue result)
			{
				int baseAddress = this.BaseAddress;
				if (!Fits(this.Data, baseAddress + dataOffset, dataLen))
				{
					throw ThrowHelper.FormatException($"Malformed jsonb entry: data for entry {index} would be outside of the bounds of the container");
				}

				JType type = GetEntryType(entry);
				switch (type)
				{
					case JType.String: // strings are encoded in UTF-8 (what else?)
					case JType.Integer:	// binary integers are unaligned
					case JType.Numeric:	// floating point numbers are encoded as a base 10 literal
					{
						result = new JValue(type, this.Data.Slice(baseAddress + dataOffset, dataLen));
						break;
					}
					case JType.True:
					case JType.False:
					case JType.Null:
					{
						result = new JValue(type, default);
						break;
					}
					case JType.Container:
					{
						// containers are aligned to 32-bits boundaries
						int padLen = ComputePadding(baseAddress + dataOffset);
						if (padLen > dataLen) throw ThrowHelper.FormatException($"Invalid jsonb entry: padding is larger than reported value size at array index {index}.");
						result = new JValue(JType.Container, this.Data.Slice(baseAddress + dataOffset + padLen, dataLen - padLen));
						break;
					}
					default:
					{
						throw ThrowHelper.FormatException($"Invalid jsonb entry: invalid entry type at array index {index}.");
					}
				}
			}

			/// <summary>
			/// Gets the offset of the variable-length portion of a Jsonb node within the variable-length-data part of its container.
			/// The node is identified by index within the container's JEntry array.
			/// </summary>
			/// <param name="index">Index of the entry</param>
			/// <returns>Offset of the entry (in bytes)</returns>
			[Pure]
			private int GetJsonbOffset(int index)
			{
				int offset = 0;
				if (index > 0)
				{
					ref byte ptr = ref Unsafe.AsRef(in this.Data[checked(JCONTAINER_CHILDREN_OFFSET + (index - 1) * JENTRY_SIZEOF)]);
					for (int i = index - 1; i >= 0; i--)
					{
						uint value = UnsafeHelpers.ReadUInt32LE(in ptr);
						int len = GetEntryOffsetOrLength(value);
						offset = checked(offset + len);
						if (GetEntryHasOffset(value))
						{
							break;
						}

						ptr = ref Unsafe.Add(ref ptr, -JENTRY_SIZEOF);
					}
				}
				return offset;
			}

			/// <summary>
			/// Gets the length of the variable-length portion of a Jsonb node.
			/// The node is identified by index within the container's JEntry array.
			/// </summary>
			/// <param name="index">Index of the entry</param>
			/// <param name="offset">Offset of the entry (computed via <see cref="GetJsonbOffset"/>)</param>
			/// <returns>Size of the entry (in bytes)</returns>
			[Pure]
			private int GetJsonbLength(int index, int offset)
			{
				/*
				 * If the length is stored directly in the JEntry, just return it.
				 * Otherwise, get the start offset of the entry, and subtract that from
				 * the stored end+1 offset.
				 */

				uint value = BinaryPrimitives.ReadUInt32LittleEndian(this.Data.Slice(checked(JCONTAINER_CHILDREN_OFFSET + index * JENTRY_SIZEOF)));
				int len = GetEntryOffsetOrLength(value);
				if (GetEntryHasOffset(value)) len = checked(len - offset);

				return len;
			}

			private JsonArray DecodeArray(StringTable? table)
			{
				if (!this.IsArray) throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an array.");

				var data = this.Data;
				int numElems = this.Count;
				if (numElems == 0) return JsonArray.ReadOnly.Empty;
				if (data.Length < checked(4 + numElems * 4)) throw ThrowHelper.FormatException($"Json container is too small for an array of size {numElems}.");

				// decodes the items one by one
				var arr = new JsonValue[numElems];
				int childOffset = JCONTAINER_CHILDREN_OFFSET;
				int dataOffset = 0;
				for (int i = 0; i < numElems; i++)
				{
					uint entry = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(childOffset));

					int dataLen = (int) (entry & JENTRY_OFFLENMASK);
					if ((entry & JENTRY_HAS_OFF) != 0)
					{
						dataLen -= dataOffset;
					}

					GetEntryAt(i, entry, dataOffset, dataLen, out JValue item);
					arr[i] = item.ToJsonValue(table);
					dataOffset += dataLen;
					childOffset += JENTRY_SIZEOF;
				}
				return new JsonArray(arr, numElems, readOnly: true);
			}

			private JsonObject DecodeObject(StringTable? table)
			{
				if (!this.IsObject) throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an object.");

				int numPairs = this.Count;
				if (numPairs == 0)
				{
					return JsonObject.ReadOnly.Empty;
				}
				if (this.Data.Length  < checked(4 + numPairs * 2 * 4)) throw ThrowHelper.FormatException($"Json container is too small for an object of size {numPairs}.");

				//TODO: scan the entries, instead of calling GetArrayEntry(..) for each of them

				// decode the keys and values in a single pass
				var map = new JsonObject(numPairs);
				for (int i = 0; i < numPairs; i++)
				{
					GetContainerEntry(i, numPairs * 2, out var keyItem);
					GetContainerEntry(i + numPairs, numPairs * 2, out var valueItem);
					map[keyItem.ToKey(table)] = valueItem.ToJsonValue(table);
				}
				//note: by default we will create immutable objects!
				//REVIEW: if we need to make this configurable, we would need to add a way to pass settings to this method!
				return map.FreezeUnsafe();
			}

			public JsonValue ToJsonValue(StringTable? table = null)
			{
				return this.IsArray ? DecodeArray(table)
					: this.IsObject ? DecodeObject(table)
					: ThrowInvalidJsonbContainer();
			}

			[ContractAnnotation("=> halt")]
			private static JsonValue ThrowInvalidJsonbContainer()
			{
				throw new InvalidOperationException("Invalid jsonb container");
			}

			private static int ReadIndexFromTable(int p, ReadOnlySpan<byte> table, int hashIdxLen)
			{
				switch (hashIdxLen)
				{
					case 1:
					{
						return table[p];
					}
					case 2:
					{
						return BinaryPrimitives.ReadUInt16LittleEndian(table.Slice(p * 2));
					}
					default:
					{
						var s = table.Slice(p * 3);
						return s[0] | (s[1] << 8) | (s[2] << 16);
					}
				}
			}

			public bool GetEntryByName(scoped JLookupKey key, out JValue value)
			{
				if (!this.IsObject)
				{
					throw ThrowHelper.InvalidOperationException("Specified jsonb container is not an object.");
				}

				var data = this.Data;
				int numPairs = this.Count;
				if (data.Length < 4 + numPairs * 2 * 4)
				{
					throw ThrowHelper.FormatException($"Json container is too small for an object of size {numPairs}.");
				}

				if (this.IsHashed)
				{
					return GetEntryByNameHashed(key, out value);
				}
				else
				{
					return GetEntryByNameNonHashed(key, out value);
				}
			}

			private bool GetEntryByNameHashed(scoped JLookupKey key, out JValue value)
			{
				var data = this.Data;
				int numPairs = this.Count;

				// the keys range from 0 to numPairs - 1

				int childrenOffset = JCONTAINER_CHILDREN_OFFSET;

				int entriesSize = checked(numPairs * 2 * JENTRY_SIZEOF);

				// the size of the index depends on the number of items
				int hashIdxLen = GetIndexMapStride(numPairs);
				int hashMapSize = checked(numPairs * (4 + hashIdxLen)); // 32 bit hashcode + index per entry

				int hashesOffset = childrenOffset + entriesSize;

				var hashTable = data.Slice(hashesOffset, hashMapSize);
				var hashes = MemoryMarshal.Cast<byte, int>(hashTable.Slice(0, numPairs * 4));
				var indexes = hashTable.Slice(numPairs * 4);

				// look for the index in the table!

				var hash = ComputeKeyHash(key.Bytes);

				int p = hashes.BinarySearch(hash);
				if (p < 0 || p >= hashes.Length)
				{ // no key found with the hash
					value = default;
					return false;
				}

				// if there are hash collisions, the binary search may not land on the very first, so we may need to move back a little bit
				// ex: 1 2 3 4 5 5 5 5 5 6 7 8 9
				//                 ^------------ binary search could land here when serach for '5'
				//             ^---------------- we need to move to the left until we reach here!
				while (p > 0 && hashes[p - 1] == hash)
				{
					--p;
				}

				while (p < hashes.Length)
				{
					int index = ReadIndexFromTable(p, indexes, hashIdxLen);

					int offset = GetJsonbOffset(index);
					int len = GetJsonbLength(index, offset);

					int childOffset = checked(JCONTAINER_CHILDREN_OFFSET + index * JENTRY_SIZEOF);
					uint entry = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(childOffset));

					GetEntryAt(index, entry, offset, len, out var result);

					if (result.Equals(key))
					{
						// match
						GetContainerEntry(index + numPairs, numPairs * 2, out value);
						return true;
					}

					// we may need to scan the next hash if it is the same
					if (p + 1 >= numPairs || hashes[p + 1] != hash)
					{ // reached the end, or a differnt hash
						break;
					}

					++p;
				}
				// no match
				value = default;
				return false;
			}

			private bool GetEntryByNameNonHashed(scoped JLookupKey key, out JValue value)
			{
				var data = this.Data;
				int numPairs = this.Count;

				// the keys range from 0 to numPairs - 1

				int childrenOffset = JCONTAINER_CHILDREN_OFFSET;
				int dataOffset = 0;

				//HACKHACK: TODO: binarysearch? hashmap?
				for (int i = 0; i < numPairs; i++)
				{
					uint entry = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(childrenOffset));
					int dataLen = (int) (entry & JENTRY_OFFLENMASK);
					if ((entry & JENTRY_HAS_OFF) != 0)
					{
						dataLen -= dataOffset;
					}

					GetEntryAt(i, entry, dataOffset, dataLen, out JValue item);

					if (item.Equals(key))
					{ // match
						GetContainerEntry(i + numPairs, numPairs * 2, out value);
						return true;
					}

					dataOffset += dataLen;
					childrenOffset += JENTRY_SIZEOF;
				}

				value = default;
				return false;
			}

			/// <summary>Align an offset to the next 32-bit boundary</summary>
			private static int Aligned(int offset)
			{
				// 4 => 4
				// 5 => 8
				// 6 => 8
				// 7 => 8
				// 8 => 8
				return (offset + 3) & ~3;
			}

			/// <summary>Computes the number of padding bytes required to align with the next 32-bit boundary</summary>
			private static int ComputePadding(int offset)
			{
				// 4 => 0
				// 5 => 3
				// 6 => 2
				// 7 => 1
				// 8 => 0
				return (~offset + 1) & 3;
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private readonly ref struct JValue
		{
			/// <summary>Type (bool, string, ...)</summary>
			public readonly JType Type;

			/// <summary>Linked data (properly aligned), or <see langword="default"/> if there is no data</summary>
			public readonly ReadOnlySpan<byte> Value;

			public JValue(JType type, ReadOnlySpan<byte> value)
			{
				this.Type = type;
				this.Value = value;
			}

			public JsonValue ToJsonValue(StringTable? table)
			{
				switch (this.Type)
				{
					case JType.String:
					{ // String are encoded in UTF-8
						return JsonString.Return(DecodeUtf8String(this.Value, table));
					}
					case JType.Numeric:
					{ // Integers are encoded as an ASCII string literal (in base 10)

						var value = this.Value;
						int len = this.Value.Length;
						switch (len)
						{
							case 0:
							{
								return JsonNumber.Zero;
							}
							case 1:
							{
								int b = value[0];
								if (IsDigit(b)) return JsonNumber.Return(b - 48);
								break;
							}
							case 2:
							{
								int b1 = value[0];
								int b2 = value[1];
								if (b1 == '-' && IsDigit(b2)) return JsonNumber.Return(-(b2 - 48));
								if (IsDigit(b1) && IsDigit(b2)) return JsonNumber.Return((b1 - 48) * 10 + (b2 - 48));
								break;
							}
						}
						//TODO: use string table
						return JsonNumber.Parse(value);
					}
					case JType.True:
					{
						return JsonBoolean.True;
					}
					case JType.False:
					{
						return JsonBoolean.False;
					}
					case JType.Null:
					{
						return JsonNull.Null;
					}
					case JType.Container:
					{
						var container = new JContainer(this.Value);
						return container.ToJsonValue(table);
					}
					case JType.Integer:
					{ // signed integer, up to 64 bits
						long x = DecodeVarInt64(this.Value);
						return JsonNumber.Return(x);
					}
					default:
					{
						throw new InvalidOperationException("Invalid jsonb value type");
					}
				}
			}

			public bool TryReadAsBoolean(out bool value)
			{
				switch (this.Type)
				{
					case JType.True:
					{
						value = true;
						return true;
					}
					case JType.False:
					{
						value = false;
						return true;
					}
				}

				value = default;
				return false;
			}

			public bool TryReadAsString(StringTable? table, out string? result)
			{
				switch (this.Type)
				{
					case JType.String:
					{ // String are encoded in UTF-8
						result = DecodeUtf8String(this.Value, table);
						return true;
					}
					case JType.Null:
					{
						result = null;
						return true;
					}
				}

				result = null;
				return false;
			}

			public bool TryReadAsUuid128(out Uuid128 result)
			{
				switch (this.Type)
				{
					case JType.String:
					{ // Uuids are encoded as a string
						if (Uuid128.TryParse(this.Value, out result))
						{
							return true;
						}
						break;
					}
				}

				result = default;
				return false;
			}

			public bool TryReadAsInteger(out long result)
			{
				switch (this.Type)
				{
					case JType.Numeric:
					{ // Integers are encoded as an ASCII string literal (in base 10)

						var value = this.Value;
						int len = this.Value.Length;
						if (len == 0)
						{
							result = 0;
							return true;
						}
						return Utf8Parser.TryParse(value, out result, out int n) && n == value.Length;
					}
					case JType.Integer:
					{ // signed integer, up to 64 bits
						result = DecodeVarInt64(this.Value);
						return true;
					}
				}

				result = 0;
				return false;
			}

			public bool TryReadAsDecimal(out double result)
			{
				switch (this.Type)
				{
					case JType.Numeric:
					{ // Integers are encoded as an ASCII string literal (in base 10)

						var value = this.Value;
						int len = this.Value.Length;
						if (len == 0)
						{
							result = 0;
							return true;
						}
						return Utf8Parser.TryParse(value, out result, out int n) && n == value.Length;
					}
					case JType.Integer:
					{ // signed integer, up to 64 bits
						result = DecodeVarInt64(this.Value);
						return true;
					}
				}

				result = 0;
				return false;
			}

			public string ToKey(StringTable? table)
			{
				switch (this.Type)
				{
					case JType.String:
					{
						return DecodeUtf8String(this.Value, table);
					}
					case JType.Numeric:
					{
						//TODO: cache for small integers?
						return DecodeAsciiString(this.Value, table);
					}
					case JType.Integer:
					{ // signed integer up to 64 bits
						long x = DecodeVarInt64(this.Value);
						return StringConverters.ToString(x);
					}
					default:
					{
						throw ThrowHelper.FormatException($"Cannot convert jsonb value of type {this.Type} into a key");
					}
				}
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(JLookupKey key)
			{
				// for now, we only support string comparison
				if (this.Type != JType.String)
				{
					throw ThrowHelper.InvalidOperationException($"Cannot compare objet key with entry of type {this.Type}");
				}

				return key.Bytes.SequenceEqual(this.Value);
			}

			[Pure]
			public JContainer ToContainer()
			{
				if (this.Type != JType.Container)
				{
					throw ThrowHelper.InvalidOperationException("Value is not a container");
				}
				return new JContainer(this.Value);
			}

			public bool TextEquals(string? text)
			{
				return text is not null ? TextEquals(text.AsSpan()) : this.Type == JType.Null;
			}

			public bool TextEquals(ReadOnlySpan<char> text)
			{
				if (this.Type != JType.String) return false;
				if (text.Length == 0) return this.Value.Length == 0;

				// we have to encode into a tmp buffer, and then compare the bytes
				// => still faster than allocating a string!

				return TextEqualsHelper(Value, text);
			}

			[Pure]
			[return: NotNullIfNotNull(nameof(missing))]
			public TValue? As<TValue>(TValue? missing = default)
			{
				if (default(TValue) is null)
				{ // ref type or Nullable<T> !

					if (typeof(TValue) == typeof(bool?)) return TryReadAsBoolean(out var x) ? (TValue?) (object?) x : missing;
					if (typeof(TValue) == typeof(int?)) return TryReadAsInteger(out var x) ? (TValue?) (object?) checked((int) x) : missing;
					if (typeof(TValue) == typeof(long?)) return TryReadAsInteger(out var x) ? (TValue?) (object?) x : missing;
					if (typeof(TValue) == typeof(float?)) return TryReadAsDecimal(out var x) ? (TValue?) (object?) (float) x : missing;
					if (typeof(TValue) == typeof(double?)) return TryReadAsDecimal(out var x) ? (TValue?) (object?) x : missing;
					if (typeof(TValue) == typeof(Guid?)) return TryReadAsUuid128(out var x) ? (TValue?) (object?) x.ToGuid() : missing;
					if (typeof(TValue) == typeof(Uuid128?)) return TryReadAsUuid128(out var x) ? (TValue?) (object?) x : missing;

					if (typeof(TValue) == typeof(string))
					{
						return TryReadAsString(null, out var x) ? (TValue?) (object?) x : missing;
					}

					if (typeof(TValue) == typeof(JsonValue))
					{
						var json = ToJsonValue(null);
						return !json.IsNullOrMissing() ? (TValue) (object) json : missing is null ? (TValue) (object) JsonNull.Missing : missing;
					}
				}
				else
				{ // value type!

					if (typeof(TValue) == typeof(bool)) return TryReadAsBoolean(out var x) ? (TValue) (object) x : missing;
					if (typeof(TValue) == typeof(int)) return TryReadAsInteger(out var x) ? (TValue) (object) checked((int) x) : missing;
					if (typeof(TValue) == typeof(long)) return TryReadAsInteger(out var x) ? (TValue) (object) x : missing;
					if (typeof(TValue) == typeof(float)) return TryReadAsDecimal(out var x) ? (TValue) (object) (float) x : missing;
					if (typeof(TValue) == typeof(double)) return TryReadAsDecimal(out var x) ? (TValue) (object) x : missing;
					if (typeof(TValue) == typeof(Guid)) return TryReadAsUuid128(out var x) ? (TValue) (object) x.ToGuid() : missing;
					if (typeof(TValue) == typeof(Uuid128)) return TryReadAsUuid128(out var x) ? (TValue) (object) x : missing;
				}

				if (this.Type == JType.Null) return missing;

				//OH NOES!
				return ToJsonValue(null).As(missing);
			}

			public bool ValueEquals<TValue>(TValue? value)
			{
				if (default(TValue) is null)
				{ // ref type or Nullable<T> !

					if (value is null) return this.Type == JType.Null;

					if (typeof(TValue) == typeof(bool?)) return TryReadAsBoolean(out var b) && b == (bool) (object) value;
					if (typeof(TValue) == typeof(int?)) return TryReadAsInteger(out var x) && x == (int) (object) value;
					if (typeof(TValue) == typeof(long?)) return TryReadAsInteger(out var x) && x == (long) (object) value;
					if (typeof(TValue) == typeof(float?)) return TryReadAsDecimal(out var x) && x == (float) (object) value;
					if (typeof(TValue) == typeof(double?)) return TryReadAsDecimal(out var x) && x == (double) (object) value;
					if (typeof(TValue) == typeof(Guid?)) return TryReadAsUuid128(out var x) && x == (Guid) (object) value;
					if (typeof(TValue) == typeof(Uuid128?)) return TryReadAsUuid128(out var x) && x == (Uuid128) (object) value;

					if (typeof(TValue) == typeof(string))
					{
						return TextEquals(Unsafe.As<string>(value));
					}

					if (value is JsonValue j)
					{
						return j switch
						{
							JsonNull => this.Type == JType.Null,
							JsonString str => TextEquals(Unsafe.As<string>(str.Value)),
							JsonBoolean b => TryReadAsBoolean(out var x) && x == b.Value,
							JsonNumber num => num.IsDecimal ? TryReadAsDecimal(out var d) && num.Equals(d) : TryReadAsInteger(out var i) && num.Equals(i),
							_ => ToJsonValue(null).Equals(j)
						};
					}

				}
				else
				{ // value type!

					if (typeof(TValue) == typeof(bool)) return TryReadAsBoolean(out var b) && b == (bool) (object) value!;
					if (typeof(TValue) == typeof(int)) return TryReadAsInteger(out var x) && x == (int) (object) value!;
					if (typeof(TValue) == typeof(long)) return TryReadAsInteger(out var x) && x == (long) (object) value!;
					if (typeof(TValue) == typeof(float)) return TryReadAsDecimal(out var x) && x == (float) (object) value!;
					if (typeof(TValue) == typeof(double)) return TryReadAsDecimal(out var x) && x == (double) (object) value!;
					if (typeof(TValue) == typeof(Guid)) return TryReadAsUuid128(out var x) && x == (Guid) (object) value!;
					if (typeof(TValue) == typeof(Uuid128)) return TryReadAsUuid128(out var x) && x == (Uuid128) (object) value!;

					if (this.Type == JType.Null) return false;
				}

				//OH NOES!
				return ToJsonValue(null).Equals(JsonValue.FromValue(value));
			}

		}

		/// <summary>Creates a selector for the value at the specified path</summary>
		public static JLookupSelector CreateSelector(string path)
			=> CreateSelector(JsonPath.Create(path));

		/// <summary>Creates a selector for the value at the specified path</summary>
		public static JLookupSelector CreateSelector(ReadOnlyMemory<char> path)
			=> CreateSelector(JsonPath.Create(path));

		/// <summary>Creates a selector for the value at the specified path</summary>
		public static JLookupSelector CreateSelector(JsonPath path)
		{
			// we assume that the sum of encoded path segments will never be more than the encoded complete path,
			// we may allocate a little bit more than necessary, but it should only be one or two bytes per segments

			var encoding = Encoding.UTF8;

			var capacity = Encoding.UTF8.GetByteCount(path.Value.Span);
			var tmp = new byte[capacity];
			encoding.GetBytes(path.Value.Span, tmp);

			int count = path.GetSegmentCount();
			var xs = new (int KeyStart, int KeyLength, Index Index)[count];
			int i = 0, p = 0;
			var current = path.Value.Span;
			while(current.Length > 0)
			{
				int consumed = JsonPath.ParseNext(current, out var keyLength, out var index);
				Contract.Debug.Assert(consumed > 0 && consumed >= keyLength);

				xs[i++] = (p, keyLength, index);
				if (keyLength > 0)
				{
					p += encoding.GetBytes(
						JsonPath.DecodeKeyName(current[..keyLength]),
						tmp.AsSpan(p)
					);
				}
				current = current[consumed..];
			}

			return new JLookupSelector(path, tmp, xs);
		}

		/// <summary>Selector that will always return the value of the element at the given path of a Jsonb document</summary>
		public readonly struct JLookupSelector
		{

			/// <summary>Path corresponding to this selector</summary>
			public readonly JsonPath Path;

			/// <summary>Internal buffer that contains the UTF-8 encoded field names (if there are any)</summary>
			internal readonly byte[] Encoded;

			/// <summary>List of the segments in this selector</summary>
			internal readonly (int KeyStart, int KeyLength, Index Index)[] Segments;

			/// <summary>Constructs a selector</summary>
			public JLookupSelector(JsonPath path, byte[] encoded, (int KeyStart, int KeyLength, Index Index)[] segments)
			{
				this.Path = path;
				this.Encoded = encoded;
				this.Segments = segments;
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private readonly ref struct JLookupKey
		{
			/// <summary>Key encoded as bytes (UTF-8)</summary>
			public readonly ReadOnlySpan<byte> Bytes;

			public JLookupKey(ReadOnlySpan<byte> bytes)
			{
				this.Bytes = bytes;
			}

		}

		[StructLayout(LayoutKind.Sequential)]
		private ref struct JDocument
		{

			private readonly ReadOnlySpan<byte> Data;

			private JDocument(ReadOnlySpan<byte> data, uint header, uint extraFlags, int offset)
			{
				this.Data = data;
				this.Header = header;
				this.ExtraFlags = extraFlags;
				this.Offset = offset;
			}

			public static JDocument Parse(ReadOnlySpan<byte> data)
			{
				// ROOT_ENTRY + ROOT_HEADER = (empty array or object)
				if (data.Length < 8) throw ThrowHelper.FormatException("Buffer is too small to be a valid jsonb document.");

				int pos = 0;

				uint header = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos));
				pos += 4;

				int size = (int) (header & HEADER_SIZE_MASK);
				// check that the buffer is large enough to hold the root container
				if ((uint) size > data.Length) throw ThrowHelper.FormatException($"The reported size of the jsonb container ({size}) is larger than the size of the buffer in memory ({data.Length})");

				if ((header & HEADER_FLAGS_RESERVED) != 0)
				{ // extra flags must be all 0 for now
					throw ThrowHelper.FormatException("Unsupported extra flags in header. Either this is not a jsonb document, or it is a newer format version.");
				}

				uint extraFlags = 0;
				if ((header & HEADER_FLAGS_EXTRA) != 0)
				{ // the header is immediately followed by 32 extra flags
					extraFlags = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos));
					pos += 4;
				}

				return new JDocument(data, header, extraFlags, pos);
			}

			private readonly uint Header;

			public readonly uint ExtraFlags;

			public readonly int Offset;

			/// <summary>Size of the document (including the headers)</summary>
			public uint Size
			{
				[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => this.Header & HEADER_SIZE_MASK;
			}

			private JContainer GetRoot()
			{
				return new JContainer(this.Data[this.Offset..]);
			}

			public JsonValue ToJsonValue(StringTable? table)
			{
				var root = GetRoot();

				if (root.IsScalar)
				{ // Scalars are stored in an array of length 1
					if (!root.IsArray) throw ThrowHelper.FormatException("Jsonb scalar container must be an array");
					if (root.Count != 1) throw ThrowHelper.FormatException("Jsonb scalar container must have exactly one element");
					root.GetContainerEntry(0, 1, out JValue scalar); //TODO: buffer size
					return scalar.ToJsonValue(table);
				}

				return root.ToJsonValue(table);
			}

			public bool TryLookup(JsonPath path, out JValue value)
			{
				if (path.IsEmpty()) throw ThrowHelper.ArgumentException(nameof(path), "Path cannot be empty");

				// to reduce allocations, we directly compare the key (string) with the encoded bytes in the jsonb blob (utf-8)
				if (path.Value.Length > 16 * 1024) throw ThrowHelper.ArgumentException(nameof(path), "Path size is too large (> 16K)");

				// the keys are encoded as UTF-8 in the document, so we will have to pre-encode the path into utf-8 bytes, and compare these bytes with the names in the document
				// => pre-allocate a buffer with the proper capacity
				// note: if the path is composed of smaller segments, we will allocate too much, but we expect most paths to be single segment anyway!
				var enc = JsonEncoding.Utf8NoBom;
				Span<byte> keyBytes = stackalloc byte[enc.GetMaxByteCount(path.Value.Length)];

				var current = GetRoot();

				foreach(var x in path.Tokenize())
				{
					JValue child;

					if (x.Segment.TryGetName(out var name))
					{
						if (!current.IsObject)
						{ // current is not an object?
							break;
						}

						// encode the path
						int keyLen = enc.GetBytes(name.Span, keyBytes);

						var key = new JLookupKey(keyBytes.Slice(0, keyLen));

						if (!current.GetEntryByName(key, out child))
						{ // no field with this name
							break;
						}
					}
					else if (x.Segment.TryGetIndex(out var idx))
					{
						if (!current.IsArray)
						{ // current is not an array?
							break;
						}

						int length = current.Count;
						var index = idx.GetOffset(length);

						if ((uint) index >= length)
						{ // outside the bounds of the array!
							break;
						}

						current.GetContainerEntry(index, length, out child);
					}
					else
					{
						//??
						continue;
					}

					if (x.Last)
					{ // we found a value!
						value = child;
						return true;
					}

					if (child.Type != JType.Container)
					{ // we need a container in order to continue!
						break;
					}

					current = new JContainer(child.Value);
				}

				value = default;
				return false;

			}

			public bool TryLookup(JLookupSelector path, out JValue value)
			{
				var segments = path.Segments;
				if (segments.Length == 0)
				{
					throw ThrowHelper.ArgumentException(nameof(path), "Path cannot be empty");
				}

				var current = GetRoot();

				for(int i = 0; i < segments.Length; i++)
				{
					ref readonly var seg = ref segments[i];

					JValue child;
					if (seg.KeyLength != 0)
					{
						if (!current.IsObject)
						{ // current is not an object?
							break;
						}

						var key = new JLookupKey(path.Encoded.AsSpan(seg.KeyStart, seg.KeyLength));

						if (!current.GetEntryByName(key, out child))
						{ // no field with this name
							break;
						}
					}
					else
					{
						if (!current.IsArray)
						{ // current is not an array?
							break;
						}

						int length = current.Count;
						var index = seg.Index.GetOffset(length);

						if ((uint) index >= length)
						{ // outside the bounds of the array!
							break;
						}

						current.GetContainerEntry(index, length, out child);
					}

					if (i + 1 == segments.Length)
					{ // last segment, we found the value!
						value = child;
						return true;
					}

					if (child.Type != JType.Container)
					{ // we need a container in order to continue!
						break;
					}

					current = new(child.Value);
				}

				value = default;
				return false;

			}

			public JsonValue Select(string path, StringTable? table = null)
			{
				Contract.NotNull(path);

				if (!TryLookup(JsonPath.Create(path), out var value))
				{
					return JsonNull.Missing;
				}
				return value.ToJsonValue(table);
			}

			public JsonValue Select(ReadOnlyMemory<char> path, StringTable? table = null)
			{
				Contract.NotNull(path);

				if (!TryLookup(JsonPath.Create(path), out var value))
				{
					return JsonNull.Missing;
				}
				return value.ToJsonValue(table);
			}

			public JsonValue Select(JsonPath path, StringTable? table = null)
			{
				if (!TryLookup(path, out var value))
				{
					return JsonNull.Missing;
				}
				return value.ToJsonValue(table);
			}

			public JsonValue Select(JLookupSelector selector, StringTable? table = null)
			{
				if (!TryLookup(selector, out var value))
				{
					return JsonNull.Missing;
				}
				return value.ToJsonValue(table);
			}

		}

		#endregion

		#region Public Methods...

		/// <summary>Decodes a jsonb binary blob into the corresponding <see cref="JsonValue"/></summary>
		[Pure]
		public static JsonValue Decode(Slice data, StringTable? table = null)
		{
			if (data.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(data.Span).ToJsonValue(table);
		}

		/// <summary>Decodes a jsonb binary blob into the corresponding <see cref="JsonValue"/></summary>
		[Pure]
		public static JsonValue Decode(ReadOnlySpan<byte> data, StringTable? table = null)
		{
			if (data.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(data).ToJsonValue(table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(Slice buffer, string path, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Span).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(Slice buffer, ReadOnlyMemory<char> path, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Span).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(Slice buffer, JsonPath path, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Span).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(Slice buffer, JLookupSelector selector, StringTable? table = null)
		{
			if (buffer.Count == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer.Span).Select(selector, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(ReadOnlySpan<byte> buffer, string path, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(ReadOnlySpan<byte> buffer, ReadOnlyMemory<char> path, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(ReadOnlySpan<byte> buffer, JsonPath path, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).Select(path, table);
		}

		/// <summary>Reads a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		public static JsonValue Select(ReadOnlySpan<byte> buffer, JLookupSelector selector, StringTable? table = null)
		{
			if (buffer.Length == 0) return JsonNull.Missing;
			return JDocument.Parse(buffer).Select(selector, table);
		}

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static TValue? Get<TValue>(Slice buffer, string path, TValue? missing = default)
			=> buffer.Count != 0 && JDocument.Parse(buffer.Span).TryLookup(JsonPath.Create(path), out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static TValue? Get<TValue>(Slice buffer, ReadOnlyMemory<char> path, TValue? missing = default)
			=> buffer.Count != 0 && JDocument.Parse(buffer.Span).TryLookup(JsonPath.Create(path), out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(Slice buffer, JsonPath path, TValue? missing = default)
			=> buffer.Count != 0 && JDocument.Parse(buffer.Span).TryLookup(path, out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(Slice buffer, JLookupSelector selector, TValue? missing = default)
			=> buffer.Count != 0 && JDocument.Parse(buffer.Span).TryLookup(selector, out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(ReadOnlySpan<byte> buffer, string path, TValue? missing = default)
			=> buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(JsonPath.Create(path), out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(ReadOnlySpan<byte> buffer, ReadOnlyMemory<char> path, TValue? missing = default)
			=> buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(JsonPath.Create(path), out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(ReadOnlySpan<byte> buffer, JsonPath path, TValue? missing = default)
			=> buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(path, out var j) ? j.As(missing) : missing;

		/// <summary>Decodes the value of a specific field in a jsonb binary blob, without decoding the entire document</summary>
		[Pure]
		[return: NotNullIfNotNull(nameof(missing))]
		public static TValue? Get<TValue>(ReadOnlySpan<byte> buffer, JLookupSelector selector, TValue? missing = default)
			=> buffer.Length != 0 && JDocument.Parse(buffer).TryLookup(selector, out var j) ? j.As(missing) : missing;

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(Slice buffer, string path, TValue? value)
			=> buffer.Count != 0 && (JDocument.Parse(buffer.Span).TryLookup(JsonPath.Create(path), out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(Slice buffer, ReadOnlyMemory<char> path, TValue? value)
			=> buffer.Count != 0 && (JDocument.Parse(buffer.Span).TryLookup(JsonPath.Create(path), out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(Slice buffer, JsonPath path, TValue? value)
			=> buffer.Count != 0 && (JDocument.Parse(buffer.Span).TryLookup(path, out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(Slice buffer, JLookupSelector selector, TValue? value)
			=> buffer.Count != 0 && (JDocument.Parse(buffer.Span).TryLookup(selector, out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(ReadOnlySpan<byte> buffer, string path, TValue? value)
			=> buffer.Length != 0 && (JDocument.Parse(buffer).TryLookup(JsonPath.Create(path), out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(ReadOnlySpan<byte> buffer, ReadOnlyMemory<char> path, TValue? value)
			=> buffer.Length != 0 && (JDocument.Parse(buffer).TryLookup(JsonPath.Create(path), out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(ReadOnlySpan<byte> buffer, JsonPath path, TValue? value)
			=> buffer.Length != 0 && (JDocument.Parse(buffer).TryLookup(path, out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Tests if a specific field in a jsonb binary blob is equal to the expected value, without decoding the entire document</summary>
		[Pure]
		public static bool Test<TValue>(ReadOnlySpan<byte> buffer, JLookupSelector selector, TValue? value)
			=> buffer.Length != 0 && (JDocument.Parse(buffer).TryLookup(selector, out var j) ? j.ValueEquals(value) : value is null or JsonNull);

		/// <summary>Encodes a <see cref="JsonValue"/> into a jsonb binary blob</summary>
		public static Slice Encode(JsonValue value, JsonbWriterOptions options = default) //TODO: options!
		{
			Contract.NotNull(value);

			using (var writer = new Writer(options))
			{
				writer.WriteDocument(value);
				return writer.GetBuffer();
			}
		}

		/// <summary>Encodes a <see cref="JsonValue"/> into a jsonb binary blob</summary>
		public static Slice Encode(JsonValue value, [NotNull] ref byte[]? buffer, JsonbWriterOptions options = default) //TODO: options!
		{
			Contract.NotNull(value);

			using (var writer = new Writer(options, buffer))
			{
				writer.WriteDocument(value);
				var res = writer.GetBuffer();
				buffer = res.Array;
				return res;
			}
		}

		/// <summary>Encodes a <see cref="JsonValue"/> into a jsonb binary blob</summary>
		public static Slice EncodeTo(ref SliceWriter output, JsonValue value)
		{
			Contract.NotNull(value);

			using (var writer = new Writer(output))
			{
				writer.WriteDocument(value);
				output = writer.GetWriterAndClear();
				return output.ToSlice();
			}
		}

		#endregion

		#region Internal Helpers...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static JType GetEntryType(uint value)
		{
			return (JType)((value & JENTRY_TYPEMASK) >> JENTRY_OFFLENBITS);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetEntryOffsetOrLength(uint value)
		{
			return (int) (value & JENTRY_OFFLENMASK);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool GetEntryHasOffset(uint value)
		{
			return (value & JENTRY_HAS_OFF) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool IsDigit(int b)
		{
			const int BIAS = '0';
			return (uint)(b - BIAS) <= '9' - BIAS;
		}

		#endregion

		#region Writing Jsonb...

		/// <summary>Instance that outputs Jsonb values into a binary buffer</summary>
		public sealed class Writer : IDisposable
		{
			const int HEADER_SIZE = 4;

			/// <summary>Output buffer</summary>
			private SliceWriter m_output;

			/// <summary>Minimum number of key/value pairs before embedding a hashtable in objects</summary>
			private readonly int m_hashingThreshold;

			/// <summary>Start of the current fragment in the output buffer</summary>
			/// <remarks>Internal alignment is computed relative to this offset, and not the start of the buffer!</remarks>
			private int m_fragmentOffset;

			//TODO: OPTIMIZE: use a buffer pool?

			/// <summary>Constructs a <see cref="Writer"/> using an optional initial buffer</summary>
			public Writer(JsonbWriterOptions options, byte[]? buffer = null)
			{
				int capacity = options.Capacity ?? 0;
				if (capacity <= 0) capacity = JsonbWriterOptions.DefaultCapacity;

				buffer = buffer?.Length >= capacity ? buffer : new byte[capacity];
				m_output = new SliceWriter(buffer);
				m_hashingThreshold = options.HashingThreshold ?? JsonbWriterOptions.DefaultHashingThreshold;
			}

			/// <summary>Constructs a <see cref="Writer"/> using an already prepared <see cref="SliceWriter"/></summary>
			public Writer(SliceWriter buffer)
			{
				m_output = buffer;
				m_hashingThreshold = JsonbWriterOptions.DefaultHashingThreshold;
			}

			/// <inheritdoc />
			public void Dispose()
			{
				//TODO?
			}

			/// <summary>Exposes the internal writer</summary>
			[EditorBrowsable(EditorBrowsableState.Never)]
			public ref SliceWriter GetWriterUnsafe() => ref m_output;

			/// <summary>Returns a byte array filled with the contents of the buffer</summary>
			/// <remarks>The buffer is copied in the byte array. And change to one will not impact the other</remarks>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public byte[] GetBytes()
			{
				return m_output.GetBytes();
			}

			/// <summary>Returns a <see cref="Slice"/> pointing to the content of the buffer</summary>
			/// <remarks>Any change to the slice will change the buffer !</remarks>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Slice GetBuffer()
			{
				return m_output.ToSlice();
			}

			internal SliceWriter GetWriterAndClear()
			{
				var output = m_output;
				m_output = default;
				return output;
			}

			/// <summary>Clears the buffer, and reset the cursor its initial position</summary>
			/// <remarks>After this call, the writer can be reused to process another document.</remarks>
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Clear()
			{
				m_output.Position = 0;
			}

			/// <summary>Aligns an offset to the next 32-bit boundary</summary>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			private static int Aligned(int offset)
			{
				// 4 => 4
				// 5 => 8
				// 6 => 8
				// 7 => 8
				// 8 => 8
				return (offset + 3) & ~3;
			}

			/// <summary>Writes a <see cref="JsonValue"/> to this buffer</summary>
			/// <returns>Number of bytes written</returns>
			public int WriteDocument(JsonValue value)
			{
				Contract.NotNull(value);

				if (ReferenceEquals(value, JsonNull.Missing))
				{ // missing => empty
					return 0;
				}

				// reserve some space for the header (written at the end once we know the final size)
				int start = m_output.Skip(HEADER_SIZE);
				m_fragmentOffset = start;

				// serialize the value (recursive)
				WriteValue(value, 0);

				// Note: the JEntry of the root is discarded. Therefore, the root JsonbContainer struct must contain enough information to tell what kind of value it is.
				int len = m_output.Position - start;

				// varlena, uncompressed
				if (len > HEADER_SIZE_MASK) throw ThrowHelper.InvalidOperationException($"This jsonb document ({len} bytes) exceeds the maximum allowed size ({len} > {HEADER_SIZE_MASK} bytes).");
				m_output.PatchUInt32(start, ((uint)len & HEADER_SIZE_MASK));

				return len;
			}

			private uint WriteValue(JsonValue value, int level)
			{
				Contract.Debug.Requires(value is not null && level >= 0);

				JsonType type;
				switch ((type = value.Type))
				{

					case JsonType.Array:
					{
						return WriteArray(value, level);
					}

					case JsonType.Object:
					{
						return WriteObject((JsonObject) value, level);
					}

					default:
					{
						if (level == 0)
						{
							//HACKHACK: emulate an array of length 1
							return WriteArray(value, 0);
						}

						return WriteScalar(value, type);
					}
				}
			}

			private uint WriteScalar(JsonValue value, JsonType type)
			{
				Contract.Debug.Requires(value is not null);
				switch (value)
				{
					case JsonNull:
					{
						return JENTRY_TYPE_NULL;
					}
					case JsonString str:
					{
						int len = AppendUtf8String(str.Value);
						return JENTRY_TYPE_STRING | (uint) len;
					}
					case JsonDateTime dt:
					{
						//note: warning: this can be either a DateTime or a DateTimeOffset !
						Span<byte> tmp = m_output.GetSpan(40); // max 33 chars: "YYYY-MM-DDTHH:mm:ss.fffffff+HH:mm"
						if (!dt.TryFormat(tmp, out var len))
						{
							throw new InvalidOperationException("Failed to serialize JSON DateTime");
						}
						m_output.Advance(len);
						return JENTRY_TYPE_STRING | (uint) len;
					}
					case JsonBoolean b:
					{
						return b.Value ? JENTRY_TYPE_TRUE : JENTRY_TYPE_FALSE;
					}
					case JsonNumber num:
					{
						if (!num.IsDecimal)
						{
							if (num.IsBetween(long.MinValue, long.MaxValue))
							{ // Integer up to 64-bit (signed)
								long x = num.ToInt64();
								int sz = AppendVarInt64(x);
								return JENTRY_TYPE_INTEGER | (uint) sz;
							}
						}

						//note: the literal is already formatted by the JsonNumber, so we can copy it as-is
						//BUGBUG: and if the literal is "1.0" ?
						//note: contrary to hstore2, we do not pad numbers up to 32 bits!
						int len = AppendAsciiString(value.ToString());
						return JENTRY_TYPE_NUMERIC | (uint) len;
					}
					default:
					{
						throw ThrowHelper.NotSupportedException($"Invalid JSON scalar of type {type}");
					}
				}
			}

			private void Align(int size)
			{
				// relative to the start of the current fragment (which may not be aligned in the parent buffer!)
				int misalignment = (m_output.Position - m_fragmentOffset) % size;
				if (misalignment != 0)
				{
					m_output.Skip(size - misalignment, 0);
				}
			}

			private uint WriteArray(JsonValue arrayOrScalar, int level)
			{
				int baseOffset = m_output.Position;

				// containers are always aligned to 32-bit boundaries
				Align(4); // 32-bit

				JsonArray? array;
				int numElems;
				if (arrayOrScalar is not JsonArray jsonArr)
				{ // pretend that this is an array of length 1
					if (level > 0 || arrayOrScalar is JsonObject) throw ThrowHelper.InvalidOperationException("Should only be called with a scalar for the top level");
					array = null;
					numElems = 1;
				}
				else
				{
					array = jsonArr;
					numElems = array.Count;
				}
				Contract.Debug.Assert(array is not null || numElems == 1);

				if ((uint) numElems > JENTRY_OFFLENMASK) throw FailContainerTooManyElements();

				uint header = (uint) numElems | JCONTAINER_FLAG_ARRAY;
				if (array is null) header |= JCONTAINER_FLAG_SCALAR;
				m_output.WriteUInt32(header);

				// reserve some space for the entries of each element
				int jentryOffset = m_output.Skip(numElems * JENTRY_SIZEOF);

				// copy all the data sequentially, as we patch the offset in the table allocated before
				int totalLen = 0;
				for (int i = 0; i < numElems; i++)
				{
					var elem = array is not null ? array[i] : arrayOrScalar;

					uint meta = WriteValue(elem, level + 1);

					totalLen = checked(totalLen + GetEntryOffsetOrLength(meta));
					if (totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((i % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (meta & JENTRY_TYPEMASK) | (uint) totalLen | JENTRY_HAS_OFF;
					}

					m_output.PatchUInt32(jentryOffset, meta);
					jentryOffset += JENTRY_SIZEOF;
				}

				// update the total size (includes any padding that was required)
				totalLen = m_output.Position - baseOffset;

				if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

				return JENTRY_TYPE_CONTAINER | (uint) totalLen;
			}

			private uint WriteObject(JsonObject map, int level)
			{
				int baseOffset = m_output.Position;

				// An object is the equivalent of KeyValuePair<K,V>[] array, but is stored as two arrays: one for the keys, followed by another for the values:
				// Object ~= Array<Pair<K,V>> => [ K0, K1, ..., KN-1, V0, V1, ..., VN-1 ]

				// The maximum number of elements in an object is 2^24 - 1.
				// Objects containing a lot of keys already have a mini-hashtable appended at the end, which can speed up lookups for a single key.
				// The hashtable is composed of the ordered list of the HashCodes for each key (allows binary search), as well as their corresponding index in the key and value arrays).
				// - HashCodes are 16 bits
				// - The list of indexes is stored in either 1, 2 or 3 bytes, depending on the number of k/v pairs (<= 256 uses 1 byte per entry, <= 65536 uses 2 bytes, > 65536 uses 3 bytes).

				// containers are always aligned to 32-bit boundaries
				Align(4); // 32-bit

				int numPairs = map.Count;
				if ((uint) numPairs >= (1 << 24)) throw FailContainerTooManyElements(); // max 16 777 215 keys per object!

				// objects with 20 elements or more also specify hashcode for each key
				bool hashed = numPairs >= m_hashingThreshold;
				int hashIdxLen = 0, hashMapSize = 0;
				if (hashed)
				{
					// the size of the index depends on the number of items
					hashIdxLen = JContainer.GetIndexMapStride(numPairs);
					hashMapSize = numPairs * (4 + hashIdxLen); // 32 bit hashcode + index per entry
					hashMapSize = Aligned(hashMapSize); // the hashMap is padded to a multiple of 32 bits
				}

				// estimate the capacity required to store the object (heuristic)
				// - we try to resize the buffer in advance so that writing all the jentries + hashmap does not copy/resize too many times
				// - we assume 16 bytes per entry (key + value)
				int entriesSize = numPairs * 2 * JENTRY_SIZEOF;
				m_output.EnsureBytes(4 + entriesSize + hashMapSize + numPairs * 16);

				// header of the container
				uint header = (uint) numPairs | JCONTAINER_FLAG_OBJECT;
				if (hashed) header |= JCONTAINER_FLAG_HASHED;
				m_output.WriteUInt32(header);

				// reserve some space for the jentries
				int jEntryOffset = m_output.Position;
				m_output.Position += entriesSize;
				Contract.Debug.Assert(m_output.Position <= m_output.Capacity, "Buffer should already be large enough");

				// reserve some space for the (optional) hashmap
				int hashesOffset = 0;
				if (hashed)
				{ // the hashmap is after the jentries (but before the key+values)
					hashesOffset = m_output.Position;
					m_output.Position += hashMapSize;
					Contract.Debug.Assert(m_output.Position <= m_output.Capacity, "Buffer should already be large enough");
				}

				//TODO: OPTIMIZE: could we order the hash+idx "in place"
				// => for now, we use Array.Sort(..) to sort the array of the array of hashes and indexes separately, so we need two arrays :(
				int[]? hashBuffer = null;
				Span<int> hashes = default;
				Span<int> indexes = default;
				if (hashed)
				{
					hashBuffer = ArrayPool<int>.Shared.Rent(checked(numPairs * 2));
					hashes = hashBuffer.AsSpan(0, numPairs);
					indexes = hashBuffer.AsSpan(numPairs, numPairs);
				}

				// first copy all the keys
				int totalLen = 0;
				int index = 0;
				foreach (var key in map.Keys)
				{
					// note: to be able to efficiently compare keys in UTF-8, it is critical to store them in their canonical Unicode form!
					// Uusually, String.Normalize() returns the string as-is if this is pure ASCII, and the CLR optimizes the check via a flag in the COMString header (note: this may be runtime dependent!! was the case in .NET Framework last time I checked)
					// The cost of normalization is in theory zero in the usual case (ASCII keys), or if the key is already in canonical UTF-8.
					var normalizedKey = key.Normalize();

					// compute the hashcode
					if (hashed)
					{
						hashes[index] = ComputeKeyHash(normalizedKey.AsSpan());
					}

					//note: the source is any JsonObject arbitraire, which can contain UTF-8
					uint meta = (uint) m_output.WriteString(normalizedKey, JsonEncoding.Utf8NoBom);
					totalLen += (int) meta;
					if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((index % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (uint) totalLen | JENTRY_HAS_OFF | JENTRY_TYPE_STRING;
					}
					else
					{
						meta |= JENTRY_TYPE_STRING;
					}

					m_output.PatchUInt32(jEntryOffset, meta);
					jEntryOffset += JENTRY_SIZEOF;

					++index;
				}

				// copie ensuite toutes les valeurs
				foreach (var value in map.Values)
				{
					uint meta = WriteValue(value, level + 1);

					totalLen = checked(totalLen + GetEntryOffsetOrLength(meta));
					if ((uint) totalLen > JENTRY_OFFLENMASK) throw FailContainerSizeTooBig();

					if ((index % JCONTAINER_OFFSET_STRIDE) == 0)
					{ // Convert each JB_OFFSET_STRIDE'th length to an offset.
						meta = (meta & JENTRY_TYPEMASK) | (uint) totalLen | JENTRY_HAS_OFF;
					}

					m_output.PatchUInt32(jEntryOffset, meta);
					jEntryOffset += JENTRY_SIZEOF;

					++index;
				}

				// order+store the hashmap (optional)
				if (hashed)
				{
					Contract.Debug.Assert(hashes.Length > 0);

					m_output.EnsureOffsetAndSize(hashesOffset, hashMapSize);

					// sort the hashes (and idxmap)
					for (int i = 0; i < indexes.Length; i++)
					{
						indexes[i] = i;
					}

					hashes.Sort(indexes);

					var spanHashes = m_output.ToSpan().Slice(hashesOffset, hashMapSize);
					ref byte ptrHashes = ref MemoryMarshal.GetReference(spanHashes);

					// hashes (32 bits / entry)
					foreach(var h in hashes)
					{
						//m_output.PatchInt32(hashesOffset, h);
						//hashesOffset += 4;
						UnsafeHelpers.WriteInt32LE(ref ptrHashes, h);
						ptrHashes = ref Unsafe.Add(ref ptrHashes, 4);
					}

					// idxmap (8-16-24 bits / entry)
					switch (hashIdxLen)
					{
						case 1:
						{
							foreach(var idx in indexes)
							{
								//array[hashesOffset++] = (byte) idx;
								ptrHashes = (byte) idx;
								ptrHashes = ref Unsafe.Add(ref ptrHashes, 1);

							}
							break;
						}
						case 2:
						{
							foreach(var idx in indexes)
							{
								UnsafeHelpers.WriteUInt16LE(ref ptrHashes, (ushort) idx);
								ptrHashes = ref Unsafe.Add(ref ptrHashes, 2);
							}
							break;
						}
						default:
						{
							Contract.Debug.Assert(hashIdxLen == 3);
							foreach(var idx in indexes)
							{
								UnsafeHelpers.WriteInt24LE(ref ptrHashes, idx);
								ptrHashes = ref Unsafe.Add(ref ptrHashes, 3);
							}
							break;
						}
					}
				}

				// update the total size (including all padding)
				totalLen = m_output.Position - baseOffset;
				if ((uint) totalLen > JENTRY_OFFLENMASK)
				{
					throw FailContainerSizeTooBig();
				}

				if (hashBuffer is not null)
				{
					ArrayPool<int>.Shared.Return(hashBuffer);
				}

				//TODO: ajouter le flag HASHED !!!!
				return JENTRY_TYPE_CONTAINER | (uint) totalLen;
			}

			private int AppendVarInt64(long value)
			{
				// we want to store positive AND negative numbers using the least bytes as possible
				// -128..+127 => 1 byte
				// -32768..+32767 => 2 byte
				// etc...
				// For each value, the MSB is automatically expanded to the omitted bytes to rebuild the 64 bits value
				// examples:
				//   +1 = 0x0000 0000 0000 0001 =>    01 (1 byte  with MSB 0) => (0000 0000 0000 00) . 01 => +1
				//   -1 = 0xFFFF FFFF FFFF FFFF =>    FF (1 byte  with MSB 1) => (FFFF FFFF FFFF FF) . FF => -1
				// +256 = 0x0000 0000 0000 0100 => 00 01 (2 bytes with MSB 0) => (0000 0000 0000) . 01 00 => +1
				// -257 = 0xFFFF FFFF FFFF FEFF => FF FE (2 bytes with MSB 1) => (FFFF FFFF FFFF) . FE FF => -257

				if (value >= 0)
				{ // positive

					if (value <= (1L << 7) - 1)
					{
						m_output.WriteByte((byte) value);
						return 1;
					}
					if (value <= (1L << 15) - 1)
					{
						m_output.WriteInt16((short) value);
						return 2;
					}
					if (value <= (1L << 23) - 1)
					{
						m_output.WriteBytes(unchecked((byte) value), (byte) (value >> 8), (byte) (value >> 16));
						return 3;
					}
					if (value <= (1L << 31) - 1)
					{
						m_output.WriteInt32((int) value);
						return 4;
					}

					m_output.WriteInt64(value);
					return 8;
				}
				else
				{ // negative

					if (value >= -(1L << 7))
					{
						m_output.WriteByte(unchecked((byte) value));
						return 1;
					}
					if (value >= -(1L << 15))
					{
						m_output.WriteInt16((short) value);
						return 2;
					}
					if (value >= -(1L << 23))
					{
						m_output.WriteBytes(unchecked((byte) value), (byte) (value >> 8), (byte) (value >> 16));
						return 3;
					}
					if (value >= -(1L << 31))
					{
						m_output.WriteInt32((int) value);
						return 4;
					}

					m_output.WriteInt64(value);
					return 8;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private int AppendUtf8String(string value)
			{
				return m_output.WriteString(value, JsonEncoding.Utf8NoBom);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			private int AppendAsciiString(string value)
			{
				return m_output.WriteStringAscii(value);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception FailContainerTooManyElements()
			{
				return ThrowHelper.InvalidOperationException($"A jsonb container cannot have more than {JENTRY_OFFLENMASK} elements");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception FailContainerSizeTooBig()
			{
				return ThrowHelper.InvalidOperationException($"A jsonb container cannot exceed the maximum size of {JENTRY_OFFLENMASK} bytes");
			}
		}

		#endregion

		#region Decoding Helpers...

		private static int ComputeKeyHash(ReadOnlySpan<char> key)
		{
			return StringTable.Hash.GetFnvHashCode(key);
		}

		private static int ComputeKeyHash(ReadOnlySpan<byte> key)
		{
			return StringTable.Hash.GetFnvHashCode(key, out _);
		}

		internal static bool TextEqualsHelper(ReadOnlySpan<byte> token, ReadOnlySpan<char> text)
		{
			if (token.Length == 0) return text.Length == 0;
			if (text.Length == 0) return false;

			byte[]? array = null;
			int maxLength = checked(text.Length * 3);
			Span<byte> buf = maxLength > 256 ? (array = ArrayPool<byte>.Shared.Rent(maxLength)) : stackalloc byte[maxLength];

			bool isEqual = 
				Utf8.FromUtf16(text, buf, out int _, out int written, false) != OperationStatus.InvalidData
				&& token.SequenceEqual(buf.Slice(0, written));

			if (array is not null)
			{
				buf.Slice(0, written).Clear();
				ArrayPool<byte>.Shared.Return(array);
			}

			return isEqual;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static bool Fits(ReadOnlySpan<byte> data, int offset, int count)
		{
			return (ulong) (uint) offset + (ulong) (uint) count <= (ulong) (uint) data.Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void BoundCheck(ReadOnlySpan<byte> data, uint offset, uint count)
		{
			uint end = checked(offset + count);
			if (end > data.Length) throw FailDataOutOfBound(offset, count);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailDataOutOfBound(uint offset, uint count)
		{
			return new IndexOutOfRangeException($"Attempted to read outside of the data segment (@{offset:N0}+{count:N0})");
		}

		[Pure]
		private static string DecodeUtf8String(ReadOnlySpan<byte> data, StringTable? table = null)
		{
			if (data.Length == 0) return string.Empty;
			unsafe
			{
				if (table is null)
				{
					fixed (byte* ptr = data)
					{
						return new string((sbyte*) ptr, 0, data.Length, JsonEncoding.Utf8NoBom);
					}
				}
				return table.Add(data);
			}
		}

		[Pure]
		private static string DecodeAsciiString(ReadOnlySpan<byte> data, StringTable? table = null)
		{
			if (data.Length == 0) return string.Empty;
			unsafe
			{
				if (table is null)
				{
					fixed (byte* ptr = data)
					{
						return new string((sbyte*) ptr, 0, data.Length);
					}
				}
				//note: AddUtf8 detects if this is ASCII or not
				return table.Add(data);
			}
		}

		[Pure]
		private static long DecodeVarInt64(ReadOnlySpan<byte> data)
		{
			int len = data.Length;
			if (len == 0)
			{ // empty means 0
				return 0;
			}

			if ((uint) len > 8)
			{ // too big!
				throw ThrowHelper.FormatException("Jsonb integer value is too big");
			}

			long value;
			{
				ref byte ptr = ref Unsafe.AsRef(in data[0]);
				{
					--len;
					value = Unsafe.Add(ref ptr, len);
					while(len > 0)
					{
						--len;
						value = (value << 8) | Unsafe.Add(ref ptr, len);
					}
				}
			}

			// uses the MSB to restore negative numbers
			//  length   : msb_mask         : negatif_fill
			//   1 :   8 : --------------80 : FFFFFFFFFFFFFF00
			//   2 :  16 : ------------8000 : FFFFFFFFFFFF0000
			//   3 :  24 : ----------800000 : FFFFFFFFFF000000
			//   4 :  32 : --------80000000 : FFFFFFFF00000000
			//   5 :  40 : ------8000000000 : FFFFFF0000000000
			//   6 :  48 : ----800000000000 : FFFF000000000000
			//   7 :  56 : --80000000000000 : FF00000000000000
			//   8 :  64 : 8000000000000000 : 0000000000000000 (doesn't need sign expansion)

			int bits = data.Length << 3;
			if (bits < 64 && (value & (1L << (bits - 1))) != 0)
			{ // negative!
				value |= -1L << bits;
			}
			return value;
		}

		#endregion

	}

	/// <summary>Options to configure the behavior of the <see cref="Jsonb"/> parser</summary>
	public struct JsonbWriterOptions
	{

		/// <summary>Default initial buffer capacity</summary>
		public const int DefaultCapacity = 4096;

		/// <summary>Default threshold for hashing large objects</summary>
		public const int DefaultHashingThreshold = 20;

		/// <summary>Initial capacity (in bytes) allocated for the output buffer</summary>
		/// <remarks>
		/// <para>It is best to slightly overestimate the required size, since undershooting by even 1 byte would trigger an extra resize that would waste up to 50% of the allocated memory.</para></remarks>
		public int? Capacity { get; set; }

		/// <summary>All object with at least this number of elements will be hashed</summary>
		/// <remarks>
		/// <para>Object with an embedded hash table are faster when performing random key lookups, but will take more space (about 5 to 6 bytes per entry)</para>
		/// <para>Since computing the hashcode as some overhead, this can be slower for small object. Empirical testing shows that the cross-over point for performance is around 20 items.</para>
		/// <para>To completely disable hashing, set this value to <see cref="int.MaxValue"/>. To force hashing for all objects, set this value to <see langword="0"/></para>
		/// </remarks>
		public int? HashingThreshold { get; set; }

	}

}
