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

namespace SnowBank.Data.Json.Binary
{
	using SnowBank.Data.Json;
	using SnowBank.Data.Tuples;
	using SnowBank.Data.Binary;
	using SnowBank.Buffers;
	using SnowBank.Data.Tuples.Binary;

	/// <summary>Codec that encodes <see cref="JsonValue"/> instances into either database keys (ordered) or values (unordered)</summary>
	[PublicAPI]
	public sealed class JsonValueCodec : IValueEncoder<JsonValue>
	{
		/// <summary>Default settings</summary>
		/// <remarks>We use a compact representation, with ISO8601 dates, and always return read-only arrays</remarks>
		private static readonly CrystalJsonSettings s_defaultSettings = CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates().AsReadOnly();

		public static readonly JsonValueCodec Default = new();

		public JsonValueCodec()
			: this(null)
		{ }

		public JsonValueCodec(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			this.Settings = settings ?? s_defaultSettings;
			this.Resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		public CrystalJsonSettings Settings { get; }

		public ICrystalJsonTypeResolver Resolver { get; }

		private static readonly IVarTuple CachedNull = STuple.Create(default(string));
		private static readonly IVarTuple CachedZero = STuple.Create(0);
		private static readonly IVarTuple CachedOne = STuple.Create(1);

		/// <summary>Appends a JSON value onto a <see cref="IVarTuple">tuple</see></summary>
		/// <param name="prefix">Prefix onto which the key will be added (use <see cref="STuple.Empty"/> if not prefix is needed)</param>
		/// <param name="value">JSON value to encode. Only primitive types and arrays are supported. Objects are not allowed.</param>
		/// <returns>Tuple that represents this value</returns>
		/// <remarks>
		/// <para>Primitive types (like string, number, boolean...) are converted as a single item of a similar type, for ex: <c>"hello" => (...prefix, "hello", )</c></para>
		/// <para>Arrays are converted as embedded tuples, for ex: <c>[ "hello", "world", 123 ] => (...prefix, ( "hello", "world", 123 ), )</c>.</para></remarks>
		public static IVarTuple Append(IVarTuple? prefix, JsonValue? value)
		{
			value ??= JsonNull.Null;
			switch (value)
			{
				case JsonNull:
				{
					// note: we do not preserve the difference between Null and Empty
					return prefix?.Append(default(string)) ?? CachedNull;
				}
				case JsonBoolean b:
				{
					// False => (int) 0
					// True => (int) 1
					return prefix?.Append(b.Value ? 1 : 0) ?? (b.Value ? CachedOne : CachedZero);
				}
				case JsonNumber num:
				{
					// 123 => (int) 123
					// 12.3 => (double) 12.3
					if (!num.IsDecimal)
					{
						if (prefix != null)
						{
							return num.IsUnsigned ? prefix.Append(num.ToUInt64()) : prefix.Append(num.ToInt64());
						}
						else
						{
							return num.IsUnsigned ? STuple.Create(num.ToUInt64()) : STuple.Create(num.ToInt64());
						}
					}
					else
					{
						return prefix?.Append(num.ToDouble()) ?? STuple.Create(num.ToDouble());
					}
				}
				case JsonDateTime dt:
				{
					// note: to maintain round-tripping, we encode dates as string
					return prefix?.Append(dt.ToString()) ?? STuple.Create(dt.ToString());
				}
				case JsonString str:
				{
					return prefix?.Append(str.Value) ?? STuple.Create(str.Value);
				}
				case JsonArray arr:
				{
					// [1, 2, 3] => (1, 2, 3)
					var tuple = EncodeJsonArray(arr);
					return prefix?.Append(tuple) ?? STuple.Create(tuple);
				}
				default:
				{
					throw new NotSupportedException($"Cannot convert JSON value of type {value.Type} into a tuple");
				}
			}
		}

		/// <summary>Encodes a JSON value into a <see cref="IVarTuple">tuple</see></summary>
		/// <param name="value">JSON value to encode. Only primitive types and arrays are supported. Objects are not allowed.</param>
		/// <returns>Tuple that represents this value</returns>
		/// <remarks>
		/// <para>Primitive types (like string, number, boolean...) are converted as a single item of a similar type, for ex: <c>"hello" => ( "hello", )</c></para>
		/// <para>Arrays are converted as embedded tuples, for ex: <c>[ "hello", "world", 123 ] => ( ( "hello", "world", 123 ), )</c>.</para></remarks>
		public static IVarTuple ToTuple(JsonValue? value)
		{
			return Append(null, value);
		}

		public static IVarTuple EncodeJsonArray(JsonArray? value)
		{
			IVarTuple? tuple = null;
			if (value is not null)
			{
				foreach (var item in value)
				{
					tuple = Append(tuple, item);
				}
			}
			return tuple ?? STuple.Empty;
		}

		public static JsonValue FromTuple(IVarTuple? tuple)
		{
			return DecodeNext(tuple, out _);
		}

		public static JsonValue DecodeJsonValue(object? o) => o switch
		{
			null => JsonNull.Null,
			string s => JsonString.Return(s),
			bool b => JsonBoolean.Return(b),
			int i => JsonNumber.Return(i),
			long l => JsonNumber.Return(l),
			double d => JsonNumber.Return(d),
			decimal m => JsonNumber.Return(m),
			float f => JsonNumber.Return(f),
			VersionStamp vs => JsonString.Return(vs.ToString()),
			IVarTuple tuple => DecodeJsonArray(tuple),
			_ => JsonValue.FromValue(o)
		};

		public static JsonArray DecodeJsonArray(IVarTuple? tuple)
		{
			if (tuple is null || tuple.Count == 0)
			{
				return JsonArray.ReadOnly.Empty;
			}

			var arr = new JsonArray(tuple.Count);
			foreach (var item in tuple)
			{
				arr.Add(DecodeJsonValue(item));
			}

			// we always return read-only arrays
			return arr.FreezeUnsafe();
		}

		/// <summary>Decodes the first element of a tuple, back into a JSON value</summary>
		/// <param name="tuple">Tuple with a JSON value encoded in the first element</param>
		/// <param name="remaining">Receives the rest of the tuple, or <c>null</c> if this was the last element</param>
		/// <returns>Decoded JSON Value, or <see cref="JsonNull.Missing"/> if <paramref name="tuple"/> was already empty</returns>
		/// <remarks>This is intended to decode tuples that where previously encoded by <see cref="ToTuple"/> or <see cref="Append"/></remarks>
		public static JsonValue DecodeNext(IVarTuple? tuple, out IVarTuple? remaining)
		{

			if (tuple is null || tuple.Count == 0)
			{
				remaining = null;
				return JsonNull.Missing;
			}

			remaining = tuple.Count > 1 ? tuple.Substring(1) : null;
			return DecodeJsonValue(tuple[0]);
		}

		/// <summary>Decodes the element at the given location in a tuple, back into a JSON value</summary>
		/// <param name="tuple">Tuple with a JSON value encoded at some position</param>
		/// <param name="offset">Position, in the tuple, where the encoded value is located.</param>
		/// <returns>Decoded JSON Value, or <see cref="JsonNull.Missing"/> if <paramref name="offset"/> falls outside of the tuple.</returns>
		/// <remarks>This is intended to decode tuples that where previously encoded by <see cref="ToTuple"/> or <see cref="Append"/></remarks>
		public static JsonValue DecodeNext(IVarTuple tuple, int offset)
		{
			Contract.NotNull(tuple);

			return tuple.Count > offset ? DecodeJsonValue(tuple[offset]) : JsonNull.Missing;
		}

		public void WriteKeyTo(TupleWriter writer, JsonValue? value)
		{
			TuPack.PackTo(writer, ToTuple(value));
		}

		public bool TryWriteKeyTo(ref TupleSpanWriter writer, JsonValue? value)
		{
			return TuPack.TryPackTo(ref writer, ToTuple(value));
		}

		public void WriteKeyTo(ref SliceWriter writer, JsonValue? value)
		{
			TuPack.PackTo(ref writer, ToTuple(value));
		}

		public void ReadKeyFrom(ref SliceReader reader, out JsonValue value)
		{
			IVarTuple? tuple = TuPack.Unpack(reader.ReadToEnd());
			Contract.Debug.Assert(tuple != null);
			value = DecodeNext(tuple, out tuple);
			if (tuple != null) throw new FormatException("Found extra items at the encoded of the encoded JSON value");
		}

		public bool TryReadKeyFrom(ref SliceReader reader, [MaybeNullWhen(false)] out JsonValue value)
		{
			if (!TuPack.TryUnpack(reader.ReadToEnd(), out var tuple))
			{
				value = null;
				return false;
			}
			Contract.Debug.Assert(tuple != null);
			value = DecodeNext(tuple, out tuple);
			return tuple is null;
		}

		public Slice EncodeValue(JsonValue? value)
		{
			return value is null ? Slice.Nil
				: value.IsNull ? Slice.Empty
				: value.ToJsonSlice(CrystalJsonSettings.JsonCompact);
		}

		public JsonValue DecodeValue(Slice value)
		{
			return value.Count == 0
				? (value.IsNull ? JsonNull.Missing : JsonNull.Null)
				: CrystalJson.Parse(value);
		}

	}

}
