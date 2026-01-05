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
	using SnowBank.Buffers;

	/// <summary>JSON Boolean (<see langword="true"/> or <see langword="false"/>)</summary>
	[DebuggerDisplay("JSON Boolean({m_value})")]
	[PublicAPI]
	[System.Text.Json.Serialization.JsonConverter(typeof(CrystalJsonCustomJsonConverter))]
	[DebuggerNonUserCode]
	public sealed class JsonBoolean : JsonValue, IEquatable<bool>, IEquatable<JsonBoolean>, IComparable<JsonBoolean>
	{

		/// <summary>JSON value that is equal to <see langword="true"/></summary>
		/// <remarks>This singleton is immutable and can be cached</remarks>
		public static readonly JsonBoolean True = new(true);

		/// <summary>JSON value that is equal to <see langword="false"/></summary>
		/// <remarks>This singleton is immutable and can be cached</remarks>
		public static readonly JsonBoolean False = new(false);

		private readonly bool m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal JsonBoolean(bool value) => m_value = value;

		/// <summary>Returns either <see cref="JsonBoolean.True"/> or <see cref="JsonBoolean.False"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonBoolean Create(bool value) => value ? True : False;

		/// <summary>Returns either <see cref="JsonBoolean.True"/> or <see cref="JsonBoolean.False"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(bool value) => value ? True : False;

		/// <summary>Returns either <see cref="JsonBoolean.True"/>, <see cref="JsonBoolean.False"/> or <see cref="JsonNull.Null"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Return(bool? value) => value is null ? JsonNull.Null : value.Value ? JsonBoolean.True : JsonBoolean.False;

		public bool Value => m_value;

		#region JsonValue Members...

		/// <inheritdoc />
		public override JsonType Type => JsonType.Boolean;

		/// <inheritdoc />
		public override bool IsDefault => !m_value;

		/// <inheritdoc />
		public override bool IsReadOnly => true; //note: booleans are immutable

		/// <inheritdoc />
		public override object ToObject() => m_value;

		/// <inheritdoc />
		public override TValue? Bind<TValue>(TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
			where TValue : default
		{
			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
#if !DEBUG
			if (typeof(TValue) == typeof(bool)) return (TValue) (object) ToBoolean();
			if (typeof(TValue) == typeof(byte)) return (TValue) (object) ToByte();
			if (typeof(TValue) == typeof(sbyte)) return (TValue) (object) ToSByte();
			if (typeof(TValue) == typeof(char)) return (TValue) (object) ToChar();
			if (typeof(TValue) == typeof(short)) return (TValue) (object) ToInt16();
			if (typeof(TValue) == typeof(ushort)) return (TValue) (object) ToUInt16();
			if (typeof(TValue) == typeof(int)) return (TValue) (object) ToInt32();
			if (typeof(TValue) == typeof(uint)) return (TValue) (object) ToUInt32();
			if (typeof(TValue) == typeof(ulong)) return (TValue) (object) ToUInt64();
			if (typeof(TValue) == typeof(long)) return (TValue) (object) ToInt64();
			if (typeof(TValue) == typeof(float)) return (TValue) (object) ToSingle();
			if (typeof(TValue) == typeof(double)) return (TValue) (object) ToDouble();
			if (typeof(TValue) == typeof(decimal)) return (TValue) (object) ToDecimal();
			if (typeof(TValue) == typeof(TimeSpan)) return (TValue) (object) ToTimeSpan();
			if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) ToDateTime();
			if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) ToDateTimeOffset();
			if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) ToDateOnly();
			if (typeof(TValue) == typeof(TimeOnly)) return (TValue) (object) ToTimeOnly();
			if (typeof(TValue) == typeof(Guid)) return (TValue) (object) ToGuid();
			if (typeof(TValue) == typeof(Uuid128)) return (TValue) (object) ToUuid128();
			if (typeof(TValue) == typeof(Uuid96)) return (TValue) (object) ToUuid96();
			if (typeof(TValue) == typeof(Uuid80)) return (TValue) (object) ToUuid80();
			if (typeof(TValue) == typeof(Uuid64)) return (TValue) (object) ToUuid64();
			if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) ToInstant();
			if (typeof(TValue) == typeof(NodaTime.Duration)) return (TValue) (object) ToDuration();
#endif
			#endregion

			return (TValue?) BindNative(this, m_value, typeof(TValue), resolver) ?? defaultValue;
		}

		/// <inheritdoc />
		public override object? Bind(Type? type, ICrystalJsonTypeResolver? resolver = null)
			=> BindNative(this, m_value, type, resolver);

		/// <inheritdoc />
		internal override bool IsSmallValue() => true;

		/// <inheritdoc />
		internal override bool IsInlinable() => true;

		#endregion

		#region IEquatable<...>

		public override bool Equals(object? value)
		{
			return value switch
			{
				JsonValue j => Equals(j),
				bool b => m_value == b,
				_ => false
			};
		}

		/// <inheritdoc />
		public override bool ValueEquals<TValue>(TValue? value, IEqualityComparer<TValue>? comparer = null) where TValue : default
		{
			if (default(TValue) is null)
			{
				if (value is null)
				{ // null != false
					return false;
				}

				if (typeof(TValue) == typeof(bool?))
				{ // we already know it's not null
					return m_value == (bool) (object) value;
				}

				if (value is JsonBoolean j)
				{ // only JsonBoolean would match...
					return j.m_value == (bool) (object) value;
				}
			}
			else
			{
				if (typeof(TValue) == typeof(bool))
				{ // direct match
					return m_value == (bool) (object) value!;
				}
			}

			return false;
		}

		public override bool Equals(JsonValue? value)
		{
			return value is JsonBoolean b && b.m_value == m_value;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(JsonBoolean? obj) => obj is not null && obj.m_value == m_value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(bool value) => m_value == value;

		/// <inheritdoc />
		public override bool StrictEquals(JsonValue? other) => other is JsonBoolean b && b.Value == this.Value;

		public bool StrictEquals(JsonBoolean? other) => other is not null && other.Value == this.Value;

		public override int GetHashCode() => m_value ? 1 : 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator bool(JsonBoolean? obj) => obj?.m_value == true;
		//note: we cannot implicit cast, because this creates possible infinite recursions when a method has bool overloads for 'boolean' and 'JsonBoolean'

		#endregion

		#region IComparable<...>

		public override int CompareTo(JsonValue? other)
		{
			if (other.IsNullOrMissing()) return +1;
			if (other is JsonBoolean b)
			{
				return m_value.CompareTo(b.m_value);
			}
			return base.CompareTo(other);
		}

		public int CompareTo(JsonBoolean? other)
		{
			return other is not null ? m_value.CompareTo(other.Value) : +1;
		}

		#endregion

		#region IJsonConvertible...

		/// <inheritdoc />
		public override string ToJsonText(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)=> m_value ? JsonTokens.True : JsonTokens.False;

		/// <inheritdoc />
		public override string ToString() => m_value ? JsonTokens.True : JsonTokens.False;

		/// <inheritdoc />
		public override bool ToBoolean(bool _ = false) => m_value;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override byte ToByte(byte _ = 0) => m_value ? (byte) 1 : default(byte);

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override sbyte ToSByte(sbyte _ = 0) => m_value ? (sbyte)1 : default(sbyte);

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override char ToChar(char _ = '\0') => m_value ? 'Y' : 'N';

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override short ToInt16(short _ = 0) => m_value ? (short) 1 : default(short);

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override ushort ToUInt16(ushort _ = 0) => m_value ? (ushort) 1 : default(ushort);

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override int ToInt32(int _ = 0) => m_value ? 1 : 0;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override uint ToUInt32(uint _ = 0) => m_value ? 1U : 0U;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override long ToInt64(long _ = 0) => m_value ? 1L : 0L;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override ulong ToUInt64(ulong _ = 0) => m_value ? 1UL : 0UL;

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Int128 ToInt128(Int128 _ = default) => m_value ? Int128.One : Int128.Zero;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override UInt128 ToUInt128(UInt128 _ = default) => m_value ? UInt128.One : UInt128.Zero;

#endif

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override float ToSingle(float _ = 0f) => m_value ? 1f : 0f;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override double ToDouble(double _ = 0d) => m_value ? 1d : 0d;


#if NET8_0_OR_GREATER
		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Half ToHalf(Half _ = default) => m_value ? Half.One : Half.Zero;
#else
		private static readonly Half HalfZero = (Half) 0;
		private static readonly Half HalfOne = (Half) 1;
		public override Half ToHalf(Half _ = default) => m_value ? HalfOne : HalfZero;
#endif

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override decimal ToDecimal(decimal _ = 0m) => m_value ? 1m : 0m;


		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Guid ToGuid(Guid _ = default)
#if NET9_0_OR_GREATER
			=> m_value ? Guid.AllBitsSet : Guid.Empty;
#else
			=> m_value ? new(-1, -1, -1, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : Guid.Empty;
#endif

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Uuid96 ToUuid96(Uuid96 _ = default) => m_value ? Uuid96.AllBitsSet : Uuid96.Empty;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Uuid80 ToUuid80(Uuid80 _ = default) => m_value ? Uuid80.AllBitsSet : Uuid80.Empty;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Uuid64 ToUuid64(Uuid64 _ = default) => m_value ? Uuid64.AllBitsSet : Uuid64.Empty;

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override Uuid48 ToUuid48(Uuid48 _ = default) => m_value ? Uuid48.AllBitsSet : Uuid48.Empty;

		#endregion

		#region IJsonSerializable

		public override void JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(m_value);
		}

		/// <inheritdoc />
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			return (m_value ? JsonTokens.True : JsonTokens.False).TryCopyTo(destination, out charsWritten);
		}

#if NET8_0_OR_GREATER

		/// <inheritdoc />
		public override bool TryFormat(Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			return (m_value ? "true"u8 : "false"u8).TryCopyTo(destination, out bytesWritten);
		}

#endif

		#endregion

		#region ISliceSerializable

		public override void WriteTo(ref SliceWriter writer)
		{
			if (m_value)
			{ // 'true' => 74 72 75 65
				writer.WriteBytes("true"u8);
			}
			else
			{ // 'false' => 66 61 6C 73 65
				writer.WriteBytes("false"u8);
			}
		}

		#endregion

	}

}
