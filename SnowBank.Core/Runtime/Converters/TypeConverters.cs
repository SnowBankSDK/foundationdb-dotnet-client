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

#pragma warning disable IL3050

namespace SnowBank.Runtime.Converters
{
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Reflection;

	/// <summary>Helper class for converting instances from one type to another</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class TypeConverters
	{

		/// <summary>Cache used to make the JIT inline all converters from ValueType to ValueType</summary>
		private static class Cache<TIn, TOut>
		{
			public static readonly ITypeConverter<TIn, TOut> Converter = GetConverter<TIn, TOut>();
		}

		#region Identity<T>

		/// <summary>Simple converter where the source and destination types are the same</summary>
		/// <typeparam name="T">Source and Destination type</typeparam>
		private sealed class Identity<T> : ITypeConverter<T, T>
		{

			public static readonly ITypeConverter<T, T> Default = new Identity<T>();

			public static readonly Func<object?, T> FromObject = (Func<object?, T>) CreateCaster(typeof(T));

			public Type Source => typeof(T);

			public Type Destination => typeof(T);

			public T Convert(T value)
			{
				return value;
			}

			public object? ConvertBoxed(object? value)
			{
				return FromObject(value);
			}

		}

		#endregion

		#region Anonymous<T>

		/// <summary>Simple converter that wraps a lambda function</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		private sealed class Anonymous<TInput, TOutput> : ITypeConverter<TInput, TOutput>
		{
			private Func<TInput, TOutput> Converter { get; }

			public Anonymous(Func<TInput, TOutput> converter)
			{
				Contract.NotNull(converter);
				this.Converter = converter;
			}

			public Type Source => typeof(TInput);

			public Type Destination => typeof(TOutput);

			public TOutput Convert(TInput value)
			{
				return this.Converter(value);
			}

			public object? ConvertBoxed(object? value)
			{
				return this.Converter(Identity<TInput>.FromObject(value));
			}
		}

		private sealed class SubClass<TInput, TOutput> : ITypeConverter<TInput, TOutput>
		{
			public static readonly ITypeConverter<TInput, TOutput> Default = new SubClass<TInput, TOutput>();

			private SubClass()
			{
				if (!typeof(TOutput).IsAssignableFrom(typeof(TInput))) throw new InvalidOperationException($"Type {typeof(TInput).Name} is not a subclass of {typeof(TOutput).Name}");
			}

			public TOutput Convert(TInput value)
			{
				return (TOutput) (object) value!;
			}

			public Type Source => typeof(TInput);

			public Type Destination => typeof(TOutput);

			public object? ConvertBoxed(object? value)
			{
				return value;
			}
		}

		#endregion

		/// <summary>Static ctor that initialize the default converters</summary>
		static TypeConverters()
		{
			RegisterDefaultConverters();
		}

		/// <summary>Map of all known converters from T to R</summary>
		/// <remarks>No locking required, because all changes will replace this instance with a new Dictionary</remarks>
		private static Dictionary<ComparisonHelper.TypePair, ITypeConverter> Converters = new(ComparisonHelper.TypePairComparer.Default);

		/// <summary>Register all the default converters</summary>
		private static void RegisterDefaultConverters()
		{
			//TODO: there is too much generic type combinations! need to refactor this ...

			RegisterUnsafe<bool, Slice>((value) => Slice.FromByte(value ? (byte) 1 : default(byte)));
			RegisterUnsafe<bool, byte[]?>((value) => Slice.FromByte(value ? (byte) 1 : default(byte)).GetBytes());
			RegisterUnsafe<bool, string?>((value) => value ? "true" : "false");
			RegisterUnsafe<bool, sbyte>((value) => value ? (sbyte) 1 : default(sbyte));
			RegisterUnsafe<bool, byte>((value) => value ? (byte) 1 : default(byte));
			RegisterUnsafe<bool, short>((value) => value ? (short) 1 : default(short));
			RegisterUnsafe<bool, ushort>((value) => value ? (ushort) 1 : default(ushort));
			RegisterUnsafe<bool, int>((value) => value ? 1 : default(int));
			RegisterUnsafe<bool, uint>((value) => value ? 1U : default(uint));
			RegisterUnsafe<bool, long>((value) => value ? 1L : default(long));
			RegisterUnsafe<bool, ulong>((value) => value ? 1UL : default(ulong));
			RegisterUnsafe<bool, double>((value) => value ? 1.0d : default(double));
			RegisterUnsafe<bool, float>((value) => value ? 1.0f : default(float));
			RegisterUnsafe<bool, decimal>((value) => value ? 1m : default(decimal));
#if NET8_0_OR_GREATER
			RegisterUnsafe<bool, Half>((value) => value ? Half.One : Half.Zero);
			RegisterUnsafe<bool, Int128>((value) => value ? Int128.One : Int128.Zero);
			RegisterUnsafe<bool, UInt128>((value) => value ? UInt128.One : UInt128.Zero);
#endif

			RegisterUnsafe<int, Slice>(Slice.FromInt32);
			RegisterUnsafe<int, byte[]?>((value) => Slice.FromInt32(value).GetBytes());
			RegisterUnsafe<int, string?>(StringConverters.ToString);
			RegisterUnsafe<int, bool>((value) => value != 0);
			RegisterUnsafe<int, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<int, byte>((value) => checked((byte) value));
			RegisterUnsafe<int, short>((value) => checked((short) value));
			RegisterUnsafe<int, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<int, uint>((value) => (uint) value);
			RegisterUnsafe<int, long>((value) => value);
			RegisterUnsafe<int, ulong>((value) => (ulong) value);
			RegisterUnsafe<int, double>((value) => value);
			RegisterUnsafe<int, float>((value) => value); // possible loss of precision
			RegisterUnsafe<int, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<int, Half>((value) => (Half) value);
			RegisterUnsafe<int, Int128>((value) => value);
			RegisterUnsafe<int, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<uint, Slice>(Slice.FromUInt32);
			RegisterUnsafe<uint, byte[]?>((value) => Slice.FromUInt32(value).GetBytes());
			RegisterUnsafe<uint, string?>(StringConverters.ToString);
			RegisterUnsafe<uint, bool>((value) => value != 0);
			RegisterUnsafe<uint, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<uint, byte>((value) => checked((byte) value));
			RegisterUnsafe<uint, short>((value) => checked((short) value));
			RegisterUnsafe<uint, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<uint, int>((value) => (int) value);
			RegisterUnsafe<uint, long>((value) => value);
			RegisterUnsafe<uint, ulong>((value) => value);
			RegisterUnsafe<uint, double>((value) => value);
			RegisterUnsafe<uint, float>((value) => value); // possible loss of precision
			RegisterUnsafe<uint, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<uint, Half>((value) => (Half) value);
			RegisterUnsafe<uint, Int128>((value) => value);
			RegisterUnsafe<uint, UInt128>((value) => value);
#endif

			RegisterUnsafe<long, Slice>(Slice.FromInt64);
			RegisterUnsafe<long, byte[]?>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<long, string?>(StringConverters.ToString);
			RegisterUnsafe<long, bool>((value) => value != 0);
			RegisterUnsafe<long, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<long, byte>((value) => checked((byte) value));
			RegisterUnsafe<long, short>((value) => checked((short) value));
			RegisterUnsafe<long, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<long, int>((value) => checked((int) value));
			RegisterUnsafe<long, uint>((value) => (uint) value);
			RegisterUnsafe<long, ulong>((value) => (ulong) value);
			RegisterUnsafe<long, double>((value) => value); // possible loss of precision
			RegisterUnsafe<long, float>((value) => value); // possible loss of precision
			RegisterUnsafe<long, TimeSpan>(TimeSpan.FromTicks);
			RegisterUnsafe<long, Uuid64>((value) => new Uuid64(value));
			RegisterUnsafe<long, Uuid48>((value) => Uuid48.FromInt64(value));
			RegisterUnsafe<long, System.Net.IPAddress>((value) => new System.Net.IPAddress(value));
			RegisterUnsafe<long, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<long, Half>((value) => (Half) value);
			RegisterUnsafe<long, Int128>((value) => value);
			RegisterUnsafe<long, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<ulong, Slice>(Slice.FromUInt64);
			RegisterUnsafe<ulong, byte[]?>((value) => Slice.FromUInt64(value).GetBytes());
			RegisterUnsafe<ulong, string?>(StringConverters.ToString);
			RegisterUnsafe<ulong, bool>((value) => value != 0);
			RegisterUnsafe<ulong, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<ulong, byte>((value) => checked((byte) value));
			RegisterUnsafe<ulong, short>((value) => checked((short) value));
			RegisterUnsafe<ulong, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<ulong, int>((value) => checked((int) value));
			RegisterUnsafe<ulong, uint>((value) => checked((uint) value));
			RegisterUnsafe<ulong, long>((value) => checked((long) value));
			RegisterUnsafe<ulong, double>((value) => value); // possible loss of precision
			RegisterUnsafe<ulong, float>((value) => value); // possible loss of precision
			RegisterUnsafe<ulong, Uuid64>((value) => new Uuid64(value));
			RegisterUnsafe<ulong, Uuid48>((value) => Uuid48.FromUInt64(value));
			RegisterUnsafe<ulong, TimeSpan>((value) => TimeSpan.FromTicks(checked((long) value)));
			RegisterUnsafe<ulong, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<ulong, Half>((value) => (Half) value);
			RegisterUnsafe<ulong, Int128>((value) => value);
			RegisterUnsafe<ulong, UInt128>((value) => value);
#endif

			RegisterUnsafe<short, Slice>(Slice.FromInt16);
			RegisterUnsafe<short, byte[]?>((value) => Slice.FromInt16(value).GetBytes());
			RegisterUnsafe<short, string?>(StringConverters.ToString);
			RegisterUnsafe<short, bool>((value) => value != 0);
			RegisterUnsafe<short, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<short, byte>((value) => checked((byte) value));
			RegisterUnsafe<short, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<short, int>((value) => value);
			RegisterUnsafe<short, uint>((value) => checked((uint) value));
			RegisterUnsafe<short, long>((value) => value);
			RegisterUnsafe<short, ulong>((value) => checked((ulong) value));
			RegisterUnsafe<short, double>((value) => value);
			RegisterUnsafe<short, float>((value) => value);
			RegisterUnsafe<short, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<short, Half>((value) => (Half) value);
			RegisterUnsafe<short, Int128>((value) => value);
			RegisterUnsafe<short, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<ushort, Slice>(Slice.FromUInt16);
			RegisterUnsafe<ushort, byte[]?>((value) => Slice.FromUInt16(value).GetBytes());
			RegisterUnsafe<ushort, string?>(StringConverters.ToString);
			RegisterUnsafe<ushort, bool>((value) => value != 0);
			RegisterUnsafe<ushort, byte>((value) => checked((byte) value));
			RegisterUnsafe<ushort, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<ushort, short>((value) => checked((short) value));
			RegisterUnsafe<ushort, int>((value) => value);
			RegisterUnsafe<ushort, uint>((value) => value);
			RegisterUnsafe<ushort, long>((value) => value);
			RegisterUnsafe<ushort, ulong>((value) => value);
			RegisterUnsafe<ushort, double>((value) => value);
			RegisterUnsafe<ushort, float>((value) => value);
			RegisterUnsafe<ushort, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<ushort, Half>((value) => (Half) value);
			RegisterUnsafe<ushort, Int128>((value) => value);
			RegisterUnsafe<ushort, UInt128>((value) => value);
#endif

			RegisterUnsafe<byte, Slice>(Slice.FromByte);
			RegisterUnsafe<byte, byte[]?>((value) => Slice.FromByte(value).GetBytes());
			RegisterUnsafe<byte, string?>(StringConverters.ToString);
			RegisterUnsafe<byte, bool>((value) => value != 0);
			RegisterUnsafe<byte, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<byte, short>((value) => value);
			RegisterUnsafe<byte, ushort>((value) => value);
			RegisterUnsafe<byte, int>((value) => value);
			RegisterUnsafe<byte, uint>((value) => value);
			RegisterUnsafe<byte, long>((value) => value);
			RegisterUnsafe<byte, ulong>((value) => value);
			RegisterUnsafe<byte, double>((value) => value);
			RegisterUnsafe<byte, float>((value) => value);
			RegisterUnsafe<byte, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<byte, Half>((value) => value);
			RegisterUnsafe<byte, Int128>((value) => value);
			RegisterUnsafe<byte, UInt128>((value) => value);
#endif

			RegisterUnsafe<sbyte, Slice>((value) => Slice.FromInt64(value));
			RegisterUnsafe<sbyte, byte[]?>((value) => Slice.FromInt64(value).GetBytes());
			RegisterUnsafe<sbyte, string?>((value) => value.ToString(CultureInfo.InvariantCulture));	//TODO: string table!
			RegisterUnsafe<sbyte, bool>((value) => value != 0);
			RegisterUnsafe<sbyte, byte>((value) => checked((byte) value));
			RegisterUnsafe<sbyte, short>((value) => value);
			RegisterUnsafe<sbyte, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<sbyte, int>((value) => value);
			RegisterUnsafe<sbyte, uint>((value) => checked((uint) value));
			RegisterUnsafe<sbyte, long>((value) => value);
			RegisterUnsafe<sbyte, ulong>((value) => checked((ulong) value));
			RegisterUnsafe<sbyte, double>((value) => value);
			RegisterUnsafe<sbyte, float>((value) => value);
			RegisterUnsafe<sbyte, decimal>((value) => value);
#if NET8_0_OR_GREATER
			RegisterUnsafe<sbyte, Half>((value) => value);
			RegisterUnsafe<sbyte, Int128>((value) => value);
			RegisterUnsafe<sbyte, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<float, Slice>(Slice.FromSingle);
			RegisterUnsafe<float, byte[]?>((value) => Slice.FromSingle(value).GetBytes());
			RegisterUnsafe<float, string?>(StringConverters.ToString);
			RegisterUnsafe<float, bool>((value) => !(value == 0f || float.IsNaN(value)));
			RegisterUnsafe<float, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<float, byte>((value) => checked((byte) value));
			RegisterUnsafe<float, short>((value) => checked((short) value));
			RegisterUnsafe<float, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<float, int>((value) => checked((int) value));
			RegisterUnsafe<float, uint>((value) => (uint) value);
			RegisterUnsafe<float, long>((value) => checked((long) value));
			RegisterUnsafe<float, ulong>((value) => (ulong) value);
			RegisterUnsafe<float, double>((value) => value);
			RegisterUnsafe<float, decimal>((value) => (decimal) value); // possible loss of precision
#if NET8_0_OR_GREATER
			RegisterUnsafe<float, Half>((value) => (Half) value);
			RegisterUnsafe<float, Int128>((value) => (Int128) value);
			RegisterUnsafe<float, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<double, Slice>(Slice.FromDouble);
			RegisterUnsafe<double, byte[]?>((value) => Slice.FromDouble(value).GetBytes());
			RegisterUnsafe<double, string?>(StringConverters.ToString);
			RegisterUnsafe<double, bool>((value) => !(value == 0d || double.IsNaN(value)));
			RegisterUnsafe<double, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<double, byte>((value) => checked((byte) value));
			RegisterUnsafe<double, short>((value) => checked((short) value));
			RegisterUnsafe<double, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<double, int>((value) => checked((int) value));
			RegisterUnsafe<double, uint>((value) => (uint) value);
			RegisterUnsafe<double, long>((value) => checked((long) value));
			RegisterUnsafe<double, ulong>((value) => (ulong) value);
			RegisterUnsafe<double, float>((value) => (float) value); // possible loss of precision
			RegisterUnsafe<double, decimal>((value) => (decimal) value); // possible loss of precision
#if NET8_0_OR_GREATER
			RegisterUnsafe<double, Half>((value) => (Half) value);
			RegisterUnsafe<double, Int128>((value) => (Int128) value);
			RegisterUnsafe<double, UInt128>((value) => (UInt128) value);
#endif

			RegisterUnsafe<decimal, Slice>(Slice.FromDecimal);
			RegisterUnsafe<decimal, byte[]?>((value) => Slice.FromDecimal(value).GetBytes());
			RegisterUnsafe<decimal, string?>((value) => value.ToString(CultureInfo.InvariantCulture));
			RegisterUnsafe<decimal, bool>((value) => value != 0m);
			RegisterUnsafe<decimal, sbyte>((value) => (sbyte) value);
			RegisterUnsafe<decimal, byte>((value) => (byte) value);
			RegisterUnsafe<decimal, short>((value) => (short) value);
			RegisterUnsafe<decimal, ushort>((value) => (ushort) value);
			RegisterUnsafe<decimal, int>((value) => (int) value);
			RegisterUnsafe<decimal, uint>((value) => (uint) value);
			RegisterUnsafe<decimal, long>((value) => (long) value);
			RegisterUnsafe<decimal, ulong>((value) => (ulong) value);
			RegisterUnsafe<decimal, double>((value) => (double) value); // possible loss of precision
			RegisterUnsafe<decimal, float>((value) => (float) value); // possible loss of precision
#if NET8_0_OR_GREATER
			RegisterUnsafe<decimal, Half>((value) => (Half) value);
			RegisterUnsafe<decimal, Int128>((value) => (Int128) value);
			RegisterUnsafe<decimal, UInt128>((value) => (UInt128) value);
#endif

#if NET8_0_OR_GREATER

			RegisterUnsafe<Half, Slice>(Slice.FromHalf);
			RegisterUnsafe<Half, byte[]?>((value) => Slice.FromHalf(value).GetBytes());
			RegisterUnsafe<Half, string?>(StringConverters.ToString);
			RegisterUnsafe<Half, bool>((value) => !(value == default || Half.IsNaN(value)));
			RegisterUnsafe<Half, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<Half, byte>((value) => checked((byte) value));
			RegisterUnsafe<Half, short>((value) => checked((short) value));
			RegisterUnsafe<Half, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<Half, int>((value) => checked((int) value));
			RegisterUnsafe<Half, uint>((value) => (uint) value);
			RegisterUnsafe<Half, long>((value) => checked((long) value));
			RegisterUnsafe<Half, ulong>((value) => (ulong) value);
			RegisterUnsafe<Half, double>((value) => (double) value);
			RegisterUnsafe<Half, float>((value) => (float) value);
			RegisterUnsafe<Half, decimal>((value) => (decimal) value); // possible loss of precision
			RegisterUnsafe<Half, Int128>((value) => checked((Int128) value));
			RegisterUnsafe<Half, UInt128>((value) => checked((UInt128) value));

			RegisterUnsafe<Int128, Slice>(Slice.FromInt128);
			RegisterUnsafe<Int128, byte[]?>((value) => Slice.FromInt128(value).GetBytes());
			RegisterUnsafe<Int128, string?>(StringConverters.ToString);
			RegisterUnsafe<Int128, bool>((value) => value != 0);
			RegisterUnsafe<Int128, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<Int128, byte>((value) => checked((byte) value));
			RegisterUnsafe<Int128, short>((value) => checked((short) value));
			RegisterUnsafe<Int128, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<Int128, int>((value) => checked((int) value));
			RegisterUnsafe<Int128, uint>((value) => (uint) value);
			RegisterUnsafe<Int128, long>((value) => (long) value);
			RegisterUnsafe<Int128, ulong>((value) => (ulong) value);
			RegisterUnsafe<Int128, double>((value) => (double)value); // possible loss of precision
			RegisterUnsafe<Int128, float>((value) => (float) value); // possible loss of precision
			RegisterUnsafe<Int128, Uuid128>((value) => new Uuid128(value));
			RegisterUnsafe<Int128, decimal>((value) => (decimal) value);

			RegisterUnsafe<UInt128, Slice>(Slice.FromUInt128);
			RegisterUnsafe<UInt128, byte[]?>((value) => Slice.FromUInt128(value).GetBytes());
			RegisterUnsafe<UInt128, string?>(StringConverters.ToString);
			RegisterUnsafe<UInt128, bool>((value) => value != 0);
			RegisterUnsafe<UInt128, sbyte>((value) => checked((sbyte) value));
			RegisterUnsafe<UInt128, byte>((value) => checked((byte) value));
			RegisterUnsafe<UInt128, short>((value) => checked((short) value));
			RegisterUnsafe<UInt128, ushort>((value) => checked((ushort) value));
			RegisterUnsafe<UInt128, int>((value) => checked((int) value));
			RegisterUnsafe<UInt128, uint>((value) => checked((uint) value));
			RegisterUnsafe<UInt128, long>((value) => checked((long) value));
			RegisterUnsafe<UInt128, ulong>((value) => checked((ulong) value));
			RegisterUnsafe<UInt128, double>((value) => (double) value); // possible loss of precision
			RegisterUnsafe<UInt128, float>((value) => (float) value); // possible loss of precision
			RegisterUnsafe<UInt128, Uuid128>((value) => new Uuid128(value));
			RegisterUnsafe<UInt128, TimeSpan>((value) => TimeSpan.FromTicks(checked((long) value)));
			RegisterUnsafe<UInt128, decimal>((value) => (decimal) value);

#endif

			RegisterUnsafe<string?, Slice>(Slice.FromString);
			RegisterUnsafe<string?, byte[]?>((value) => Slice.FromString(value).GetBytes()); //REVIEW: string=>byte[] use UTF-8, but byte[]=>string uses Base64 ?
			RegisterUnsafe<string?, bool>((value) => !string.IsNullOrEmpty(value));
			RegisterUnsafe<string?, sbyte>((value) => string.IsNullOrEmpty(value) ? default(sbyte) : sbyte.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, byte>((value) => string.IsNullOrEmpty(value) ? default(byte) : byte.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, short>((value) => string.IsNullOrEmpty(value) ? default(short) : short.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, ushort>((value) => string.IsNullOrEmpty(value) ? default(ushort) : ushort.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, int>((value) => string.IsNullOrEmpty(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, uint>((value) => string.IsNullOrEmpty(value) ? 0U : uint.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, long>((value) => string.IsNullOrEmpty(value) ? 0L : long.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, ulong>((value) => string.IsNullOrEmpty(value) ? 0UL : ulong.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, float>((value) => string.IsNullOrEmpty(value) ? 0f : float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, double>((value) => string.IsNullOrEmpty(value) ? 0d : double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, decimal>((value) => string.IsNullOrEmpty(value) ? 0m : decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, Guid>((value) => string.IsNullOrEmpty(value) ? Guid.Empty : Guid.Parse(value));
			RegisterUnsafe<string?, Uuid128>((value) => string.IsNullOrEmpty(value) ? default(Uuid128) : Uuid128.Parse(value));
			RegisterUnsafe<string?, Uuid96>((value) => string.IsNullOrEmpty(value) ? default(Uuid96) : Uuid96.Parse(value));
			RegisterUnsafe<string?, Uuid80>((value) => string.IsNullOrEmpty(value) ? default(Uuid80) : Uuid80.Parse(value));
			RegisterUnsafe<string?, Uuid64>((value) => string.IsNullOrEmpty(value) ? default(Uuid64) : Uuid64.Parse(value));
			RegisterUnsafe<string?, Uuid48>((value) => string.IsNullOrEmpty(value) ? default(Uuid48) : Uuid48.Parse(value));
			RegisterUnsafe<string?, System.Net.IPAddress?>((value) => string.IsNullOrEmpty(value) ? default(System.Net.IPAddress) : System.Net.IPAddress.Parse(value));
#if NET8_0_OR_GREATER
			RegisterUnsafe<string?, Half>((value) => string.IsNullOrEmpty(value) ? default(Half) : Half.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, Int128>((value) => string.IsNullOrEmpty(value) ? default(Int128) : Int128.Parse(value, CultureInfo.InvariantCulture));
			RegisterUnsafe<string?, UInt128>((value) => string.IsNullOrEmpty(value) ? default(UInt128) : UInt128.Parse(value, CultureInfo.InvariantCulture));
#endif

			RegisterUnsafe<byte[]?, Slice>((value) => value.AsSlice());
			RegisterUnsafe<byte[]?, string?>((value) => value == null ? default(string) : value.Length == 0 ? string.Empty : System.Convert.ToBase64String(value)); //REVIEW: string=>byte[] use UTF-8, but byte[]=>string uses Base64 ?
			RegisterUnsafe<byte[]?, bool>((value) => value != null && value.Length > 0);
			RegisterUnsafe<byte[]?, sbyte>((value) => value?.AsSlice().ToSByte() ?? default(sbyte));
			RegisterUnsafe<byte[]?, byte>((value) => value?.AsSlice().ToByte() ?? default(byte));
			RegisterUnsafe<byte[]?, short>((value) => value?.AsSlice().ToInt16() ?? default(short));
			RegisterUnsafe<byte[]?, ushort>((value) => value?.AsSlice().ToUInt16() ?? default(ushort));
			RegisterUnsafe<byte[]?, int>((value) => value?.AsSlice().ToInt32() ?? 0);
			RegisterUnsafe<byte[]?, uint>((value) => value?.AsSlice().ToUInt32() ?? 0U);
			RegisterUnsafe<byte[]?, long>((value) => value?.AsSlice().ToInt64() ?? 0L);
			RegisterUnsafe<byte[]?, ulong>((value) => value?.AsSlice().ToUInt64() ?? 0UL);
			RegisterUnsafe<byte[]?, float>((value) => value?.AsSlice().ToSingle() ?? 0f);
			RegisterUnsafe<byte[]?, double>((value) => value?.AsSlice().ToDouble() ?? 0d);
			RegisterUnsafe<byte[]?, decimal>((value) => value?.AsSlice().ToDecimal() ?? 0m);
			RegisterUnsafe<byte[]?, Guid>((value) => value == null || value.Length == 0 ? Guid.Empty : Uuid128.ReadGuidExact(value));
			RegisterUnsafe<byte[]?, Uuid128>((value) => value == null || value.Length == 0 ? Uuid128.Empty : Uuid128.ReadExact(value));
			RegisterUnsafe<byte[]?, Uuid96>((value) => value != null ? Uuid96.Read(value) : Uuid96.Empty);
			RegisterUnsafe<byte[]?, Uuid80>((value) => value != null ? Uuid80.Read(value) : Uuid80.Empty);
			RegisterUnsafe<byte[]?, Uuid64>((value) => value != null ? Uuid64.Read(value) : Uuid64.Empty);
			RegisterUnsafe<byte[]?, Uuid48>((value) => value != null ? Uuid48.Read(value) : Uuid48.Empty);
			RegisterUnsafe<byte[]?, TimeSpan>((value) => value == null ? TimeSpan.Zero : TimeSpan.FromTicks(value.AsSlice().ToInt64()));
			RegisterUnsafe<byte[]?, System.Net.IPAddress?>((value) => value == null || value.Length == 0 ? default(System.Net.IPAddress) : new System.Net.IPAddress(value));
#if NET8_0_OR_GREATER
			RegisterUnsafe<byte[]?, Half>((value) => value?.AsSlice().ToHalf() ?? default);
			RegisterUnsafe<byte[]?, Int128>((value) => value?.AsSlice().ToInt128() ?? default);
			RegisterUnsafe<byte[]?, UInt128>((value) => value?.AsSlice().ToUInt128() ?? default);
#endif

			RegisterUnsafe<Guid, Slice>(Slice.FromGuid);
			RegisterUnsafe<Guid, byte[]?>((value) => Slice.FromGuid(value).GetBytes());
			RegisterUnsafe<Guid, string?>((value) => value.ToString("D", null));
			RegisterUnsafe<Guid, Uuid128>((value) => new Uuid128(value));
			RegisterUnsafe<Guid, System.Net.IPAddress?>((value) => new System.Net.IPAddress(new Uuid128(value).ToByteArray())); //REVIEW: custom converter for Guid=>IPv6?

			RegisterUnsafe<Uuid128, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid128, byte[]?>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid128, string?>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid128, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Uuid128, System.Net.IPAddress>((value) => new System.Net.IPAddress(value.ToByteArray())); //REVIEW: custom converter for Guid=>IPv6?
#if NET8_0_OR_GREATER
			RegisterUnsafe<Uuid128, Int128>((value) => value.ToInt128());
			RegisterUnsafe<Uuid128, UInt128>((value) => value.ToUInt128());
#endif

			RegisterUnsafe<Uuid96, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid96, byte[]?>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid96, string?>((value) => value.ToString("D", null));

			RegisterUnsafe<Uuid80, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid80, byte[]?>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid80, string?>((value) => value.ToString("D", null));

			RegisterUnsafe<Uuid64, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid64, byte[]?>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid64, string?>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid64, long>((value) => value.ToInt64());
			RegisterUnsafe<Uuid64, ulong>((value) => value.ToUInt64());

			RegisterUnsafe<Uuid48, Slice>((value) => value.ToSlice());
			RegisterUnsafe<Uuid48, byte[]?>((value) => value.ToByteArray());
			RegisterUnsafe<Uuid48, string?>((value) => value.ToString("D", null));
			RegisterUnsafe<Uuid48, long>((value) => value.ToInt64());
			RegisterUnsafe<Uuid48, ulong>((value) => value.ToUInt64());

			RegisterUnsafe<TimeSpan, Slice>((value) => Slice.FromInt64(value.Ticks));
			RegisterUnsafe<TimeSpan, byte[]?>((value) => Slice.FromInt64(value.Ticks).GetBytes());
			RegisterUnsafe<TimeSpan, long>((value) => value.Ticks);
			RegisterUnsafe<TimeSpan, ulong>((value) => checked((ulong)value.Ticks));
			RegisterUnsafe<TimeSpan, double>((value) => value.TotalSeconds);

			RegisterUnsafe<System.Net.IPAddress?, Slice>((value) => (value?.GetAddressBytes()).AsSlice());
			RegisterUnsafe<System.Net.IPAddress?, byte[]?>((value) => value?.GetAddressBytes());
			RegisterUnsafe<System.Net.IPAddress?, string?>((value) => value?.ToString());
#pragma warning disable 618
			RegisterUnsafe<System.Net.IPAddress?, int>((value) => (int) (value?.Address ?? 0));
#pragma warning restore 618

			RegisterUnsafe<Slice, byte[]?>((value) => value.GetBytes());
			RegisterUnsafe<Slice, string?>((value) => value.ToUnicode());
			RegisterUnsafe<Slice, bool>((value) => value.ToBool());
			RegisterUnsafe<Slice, sbyte>((value) => value.ToSByte());
			RegisterUnsafe<Slice, byte>((value) => value.ToByte());
			RegisterUnsafe<Slice, short>((value) => value.ToInt16());
			RegisterUnsafe<Slice, ushort>((value) => value.ToUInt16());
			RegisterUnsafe<Slice, int>((value) => value.ToInt32());
			RegisterUnsafe<Slice, uint>((value) => value.ToUInt32());
			RegisterUnsafe<Slice, long>((value) => value.ToInt64());
			RegisterUnsafe<Slice, ulong>((value) => value.ToUInt64());
			RegisterUnsafe<Slice, float>((value) => value.ToSingle());
			RegisterUnsafe<Slice, double>((value) => value.ToDouble());
			RegisterUnsafe<Slice, decimal>((value) => value.ToDecimal());
			RegisterUnsafe<Slice, Guid>((value) => value.ToGuid());
			RegisterUnsafe<Slice, Uuid128>((value) => value.ToUuid128());
			RegisterUnsafe<Slice, Uuid96>((value) => value.ToUuid96());
			RegisterUnsafe<Slice, Uuid80>((value) => value.ToUuid80());
			RegisterUnsafe<Slice, Uuid64>((value) => value.ToUuid64());
			RegisterUnsafe<Slice, Uuid48>((value) => value.ToUuid48());
			RegisterUnsafe<Slice, TimeSpan>((value) => TimeSpan.FromTicks(value.ToInt64()));
			RegisterUnsafe<Slice, System.Net.IPAddress?>((value) => !value.IsNullOrEmpty ? new System.Net.IPAddress(value.ToArray()) : null);
#if NET8_0_OR_GREATER
			RegisterUnsafe<Slice, Half>((value) => value.ToHalf());
			RegisterUnsafe<Slice, Int128>((value) => value.ToInt128());
			RegisterUnsafe<Slice, UInt128>((value) => value.ToUInt128());
#endif
		}

		/// <summary>Helper method to throw an exception when we don't know how to convert from <paramref name="source"/> to <paramref name="destination"/></summary>
		/// <param name="source">Type of the source object</param>
		/// <param name="destination">Target type of the conversion</param>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailCannotConvert(Type source, Type destination)
		{
			// prettyprint nullable type names to have something more useful than "Nullable`1"
			//TODO: extend this to all generic types ?
			var nt = Nullable.GetUnderlyingType(source);
			string sourceName = nt == null ? source.Name : ("Nullable<" + nt.Name + ">");
			nt = Nullable.GetUnderlyingType(destination);
			string destinationName = nt == null ? destination.Name : ("Nullable<" + nt.Name + ">");

			return new InvalidOperationException($"Cannot convert values of type {sourceName} into {destinationName}");
		}

		/// <summary>Create a new delegate that cast a boxed valued of type T (object) into a T</summary>
		/// <returns>Delegate that is of type Func&lt;object, <param name="type"/>&gt;</returns>
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static Delegate CreateCaster(Type type)
		{
			var prm = Expression.Parameter(typeof(object), "value");
			//TODO: valuetype vs ref type ?
			var body = Expression.Convert(prm, type);
			var lambda = Expression.Lambda(body, true, prm);
			return lambda.Compile();
		}

		/// <summary>Helper method that wraps a lambda function into a converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		/// <returns>Converters that wraps the lambda</returns>
		public static ITypeConverter<TInput, TOutput> Create<TInput, TOutput>(Func<TInput, TOutput> converter)
		{
			Contract.NotNull(converter);
			return new Anonymous<TInput, TOutput>(converter);
		}

		/// <summary>Add a new known converter (without locking)</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		internal static void RegisterUnsafe<TInput, TOutput>(Func<TInput, TOutput> converter)
		{
			Contract.Debug.Requires(converter != null);
			Converters[new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput))] = new Anonymous<TInput, TOutput>(converter);
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Lambda that converts a value of type <typeparamref name="TInput"/> into a value of type <typeparamref name="TOutput"/></param>
		public static void Register<TInput, TOutput>(Func<TInput, TOutput> converter)
		{
			Contract.Debug.Requires(converter != null);
			Register(new Anonymous<TInput, TOutput>(converter));
		}

		/// <summary>Registers a new type converter</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Instance that can convert values of type <typeparamref name="TInput"/> into a values of type <typeparamref name="TOutput"/></param>
		public static void Register<TInput, TOutput>(ITypeConverter<TInput, TOutput> converter)
		{
			Contract.NotNull(converter);
			while (true)
			{
				var previous = Converters;
				var dic = new Dictionary<ComparisonHelper.TypePair, ITypeConverter>(previous, previous.Comparer)
				{
					[new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput))] = converter
				};
				if (Interlocked.CompareExchange(ref Converters, dic, previous) == previous)
				{
					break;
				}
			}
		}

		/// <summary>Returns a converter that converts <typeparamref name="TInput"/>s into <typeparamref name="TOutput"/>s</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <returns>Valid converter for these types, or an exception if there are no known conversions</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		public static ITypeConverter<TInput, TOutput> GetConverter<TInput, TOutput>()
		{
			if (typeof(TInput) == typeof(TOutput))
			{ // R == T : identity function
				return (ITypeConverter<TInput, TOutput>) Identity<TInput>.Default;
			}

			// Try to get from the known converters
			if (!Converters.TryGetValue(new ComparisonHelper.TypePair(typeof(TInput), typeof(TOutput)), out var converter))
			{
				if (typeof(TOutput).IsAssignableFrom(typeof(TInput)))
				{ // T is a subclass of R, so it should work fine
					return SubClass<TInput, TOutput>.Default;
				}

				//TODO: ..?
				throw FailCannotConvert(typeof(TInput), typeof(TOutput));
			}

			return (ITypeConverter<TInput, TOutput>) converter;
		}

		/// <summary>Wrap a Tye Converter into a corresponding Func&lt;....&gt;</summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="converter">Instance that can convert from <typeparamref name="TInput"/> to <typeparamref name="TOutput"/></param>
		/// <returns>Lambda function that, when called, invokes <paramref name="converter"/></returns>
		[Pure]
		public static Func<TInput, TOutput> AsFunc<TInput, TOutput>(this ITypeConverter<TInput, TOutput> converter)
		{
			return converter.Convert;
		}

		/// <summary>Convert a value of type <typeparamref name="TInput"/> into type <typeparamref name="TOutput"/></summary>
		/// <typeparam name="TInput">Source type</typeparam>
		/// <typeparam name="TOutput">Destination type</typeparam>
		/// <param name="value">Value to convert</param>
		/// <returns>Converted value</returns>
		[Pure, ContractAnnotation("null=>null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TOutput Convert<TInput, TOutput>(TInput value)
		{
#if !DEBUG
			//note: we expect that, in a lot of calls, TInput == TOutput so expect the JIT to optimize this away completely (only in Release builds)
			if (typeof(TInput) == typeof(TOutput)) return (TOutput) (object) value!;
#endif
			return Cache<TInput, TOutput>.Converter.Convert(value);
		}

		/// <summary>Cast a boxed value (known to be of type <typeparamref name="T"/>) into an unboxed value</summary>
		/// <typeparam name="T">Runtime type of the value</typeparam>
		/// <param name="value">Value that is known to be of type <typeparamref name="T"/>, but is boxed into an object</param>
		/// <returns>Original value cast into its runtime type</returns>
		[Pure, ContractAnnotation("null=>null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T Unbox<T>(object value)
		{
			return Identity<T>.FromObject(value);
		}

		/// <summary>Convert a boxed value into type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Destination type</typeparam>
		/// <param name="value">Boxed value</param>
		/// <returns>Converted value, or an exception if there are no known conversions. The value null is converted into default(<typeparamref name="T"/>) by convention</returns>
		/// <exception cref="System.InvalidOperationException">No valid converter for these types was found</exception>
		[Pure]
		[MethodImpl(MethodImplOptions.NoInlining)]
		public static T? ConvertBoxed<T>(object? value)
		{
			if (value == null) return default;

			var type = value.GetType();
			var targetType = typeof(T);

			// cast !
			if (targetType.IsAssignableFrom(type))
			{
				return (T) value;
			}

			if (Converters.TryGetValue(new ComparisonHelper.TypePair(type, targetType), out var converter))
			{
				return (T) converter.ConvertBoxed(value)!;
			}
			// maybe it is a nullable type ?
			var nullableType = Nullable.GetUnderlyingType(targetType);
			if (nullableType != null)
			{
				// we already null-checked value above, so we just have to convert it to the underlying type...

				// shortcut for converting a T into a Nullable<T> ...
				if (type == nullableType) return (T) value;

				// maybe we have a converter for the underlying type ?
				if (Converters.TryGetValue(new ComparisonHelper.TypePair(type, nullableType), out converter))
				{
					return (T) converter.ConvertBoxed(value)!;
				}
			}

			throw FailCannotConvert(type, targetType);
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static MethodInfo GetConverterMethod(Type input, Type output)
		{
			var m = typeof(TypeConverters)
			        .GetMethod(nameof(GetConverter), BindingFlags.Static | BindingFlags.Public)!
			        .MakeGenericMethod(input, output);
			Contract.Debug.Assert(m != null);
			return m;
		}

		/// <summary>Create a boxed converter from <typeparamref name="TInput"/> to <paramref name="outputType"/></summary>
		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		public static Func<TInput, object?> CreateBoxedConverter<TInput>(Type outputType)
		{
			var converter = (ITypeConverter) GetConverterMethod(typeof(TInput), outputType).Invoke(null, [ ])!;
			return (x) => converter.ConvertBoxed(x);
		}

		/// <summary>Converts all the elements of a sequence</summary>
		/// <returns>New sequence with all the converted elements</returns>
		[Pure]
		public static IEnumerable<TOutput> ConvertAll<TInput, TOutput>(this ITypeConverter<TInput, TOutput> converter, IEnumerable<TInput> items)
		{
			Contract.NotNull(converter);
			Contract.NotNull(items);

			foreach (var item in items)
			{
				yield return converter.Convert(item);
			}
		}

		/// <summary>Converts all the elements of a list</summary>
		/// <returns>New list with all the converted elements</returns>
		public static List<TOutput> ConvertAll<TInput, TOutput>(this ITypeConverter<TInput, TOutput> converter, List<TInput> items)
		{
			Contract.NotNull(converter);
			Contract.NotNull(items);

			return items.ConvertAll<TOutput>(converter.Convert);
		}

		/// <summary>Converts all the elements of an array</summary>
		/// <returns>New array with all the converted elements</returns>
		public static TOutput[] ConvertAll<TInput, TOutput>(this ITypeConverter<TInput, TOutput> converter, TInput[] items)
		{
			Contract.NotNull(converter);
			Contract.NotNull(items);

			var results = new TOutput[items.Length];
			for (int i = 0; i < items.Length; i++)
			{
				results[i] = converter.Convert(items[i]);
			}
			return results;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull("value")]
		public static string? ToString<TInput>(TInput value)
		{
			//note: shortcut for Convert<TInput, string>(..) whose purpose is to be inlined by the JIT in release
			return Cache<TInput, string?>.Converter.Convert(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: NotNullIfNotNull("value")]
		public static string? ToString(object? value)
		{
			return value as string ?? ConvertBoxed<string>(value);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TOutput FromString<TOutput>(string? text)
		{
			//note: shortcut for Convert<TInput, string>(..) whose purpose is to be inlined by the JIT in release
			return Cache<string?, TOutput>.Converter.Convert(text);
		}

	}

}
