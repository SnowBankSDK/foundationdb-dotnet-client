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

// ReSharper disable StringLiteralTypo

namespace SnowBank.Runtime.Converters
{
	using System.Globalization;
	using System.Numerics;
	using System.Runtime.InteropServices;
	using NodaTime;

	/// <summary>Helper methods for converting common types into string literals</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class StringConverters
	{

		#region Formatting...

		[DoesNotReturn, StackTraceHidden]
		internal static void ReportInternalFormattingError()
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			throw ThrowHelper.InvalidOperationException("Internal formatting error");
		}

		#region 8-bits...

		/// <summary>Converts a 16-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(sbyte value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, sbyte value)
		{
#if NET9_0_OR_GREATER
			if (value >= 0)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt8];

			bool success = value.TryFormat(buf, out int written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		/// <summary>Converts a 16-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(byte value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, byte value)
		{
#if NET9_0_OR_GREATER
			// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
			output.Write(value.ToString(default(IFormatProvider)));
#else

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt8];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
#endif
		}

		#endregion

		#region 16-bits...

		/// <summary>Converts a 16-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(short value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, short value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt16];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		/// <summary>Converts a 16-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(ushort value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 16-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, ushort value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt16];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region 32-bits...

		/// <summary>Converts a 32-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(int value)
		{
			//perf: as of .NET 8, Int32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}
			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 32-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, int value)
		{
#if NET9_0_OR_GREATER
			if ((uint) value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt32];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt32ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		/// <summary>Converts a 32-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(uint value)
		{
			//perf: as of .NET 8, UInt32.ToString(null) calls Number.UInt32ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 32-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, uint value)
		{
#if NET9_0_OR_GREATER
			if (value < 300U)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt32];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt32ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region 64-bits...

		/// <summary>Converts a 64-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(long value)
		{
			//perf: as of .NET 8, Int64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			if (value >= 0)
			{
				return value.ToString(default(IFormatProvider));
			}

			// for negative numbers, we have to pass an invariant culture, otherwise it will use the NegativeSign of the current culture
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, long value)
		{
#if NET9_0_OR_GREATER
			if ((ulong) value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityInt64];

			bool success = value >= 0
				? value.TryFormat(buf, out var written) // will be inlined as Number.TryUInt64ToDecStr
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		/// <summary>Converts a 64-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(ulong value)
		{
			//perf: as of .NET 8, UInt64.ToString(null) calls Number.UInt64ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, ulong value)
		{
#if NET9_0_OR_GREATER
			if (value < 300UL)
			{
				// small integers from 0 to 299 are cached by the runtime, so there will be no string allocation
				output.Write(value.ToString(default(IFormatProvider)));
				return;
			}
#endif

			Span<char> buf = stackalloc char[Base10MaxCapacityUInt64];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region 128-bits...

#if NET8_0_OR_GREATER

		/// <summary>Converts a 64-bit signed integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(Int128 value)
		{
			//perf: for positive numbers, Int128.ToString(null) calls Number.UInt128ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit signed integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, Int128 value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityInt128];

			bool success = value >= 0
				? value.TryFormat(buf, out int written)
				: value.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();
			
			output.Write(buf[..written]);
		}

		/// <summary>Converts a 64-bit unsigned integer into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToString(UInt128 value)
		{
			//perf: UInt128.ToString(null) calls Number.UInt128ToDecStr(...) which already manage a cache for small numbers (less than 300)
			return value.ToString(default(IFormatProvider));
		}

		/// <summary>Writes the text representation of a 64-bit unsigned integer, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, UInt128 value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityUInt128];

			bool success = value.TryFormat(buf, out int written); // will be inlined as Number.TryUInt128ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

#endif

		#endregion

		#region Single...

		/// <summary>Converts a 32-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(float value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 32-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, float value)
		{

			Span<char> buf = stackalloc char[Base10MaxCapacitySingle];

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success = x != value
				? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
					: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region Double...

		/// <summary>Converts a 64-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(double value)
		{
			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			return x != value
				? value.ToString("R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.ToString(default(IFormatProvider))
				: x.ToString(NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 64-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, double value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityDouble];

			long x = unchecked((long) value);
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			bool success = x != value
				? value.TryFormat(buf, out var written, "R", NumberFormatInfo.InvariantInfo)
				: x >= 0 ? x.TryFormat(buf, out written) // will be inlined as Number.TryUInt64ToDecStr
					: x.TryFormat(buf, out written, default, NumberFormatInfo.InvariantInfo); // will be inlined as Number.TryNegativeInt64ToDecStr
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region Decimal...

		/// <summary>Converts a 64-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(decimal value)
		{
#if NET8_0_OR_GREATER
			return value.ToString("R", NumberFormatInfo.InvariantInfo);
#else
			return value.ToString("G", NumberFormatInfo.InvariantInfo);
#endif
		}

		/// <summary>Writes the text representation of a 128-bit decimal floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, decimal value)
		{
			Span<char> buf = stackalloc char[Base10MaxCapacityDecimal];

			bool success = value.TryFormat(buf, out var written, default, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region Half...

		/// <summary>Converts a 16-bit IEEE floating point number into a decimal string literal, using the Invariant culture</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(Half value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a 16-bit IEEE floating point number, using the Invariant culture</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, Half value)
		{
			Span<char> buf = stackalloc char[StringConverters.Base10MaxCapacityHalf];

			//note: I'm not sure how to optimize for this type...
			bool success = value.TryFormat(buf, out var written, null, NumberFormatInfo.InvariantInfo);
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region DateTime...

		/// <summary>Writes the text representation of a <see cref="DateTime"/> using the ISO 8601 format</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(DateTime value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a <see cref="DateTime"/> using the ISO 8601 format</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, DateTime value)
		{
			// "YYYY-MM-DDTHH:MM:SS.FFFFFFFZ" = 28
			Span<char> buf = stackalloc char[32];

			bool success = value.TryFormat(buf, out var written, "O", CultureInfo.InvariantCulture);
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#region DateTimeOffset...

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="value">Value to convert</param>
		/// <returns>Corresponding string literal</returns>
		[Pure]
		public static string ToString(DateTimeOffset value)
		{
			//note: I'm not sure how to optimize for this type...
			return value.ToString(null, NumberFormatInfo.InvariantInfo);
		}

		/// <summary>Writes the text representation of a <see cref="DateTimeOffset"/> using the ISO 8601 format</summary>
		/// <param name="output">Destination</param>
		/// <param name="value">Value to write</param>
		public static void WriteTo(TextWriter output, DateTimeOffset value)
		{
			// "YYYY-MM-DDTHH:MM:SS.FFFFFFF+HH:MM" = 33
			Span<char> buf = stackalloc char[36];

			bool success = value.TryFormat(buf, out var written, "O", CultureInfo.InvariantCulture);
			if (!success) ReportInternalFormattingError();

			output.Write(buf[..written]);
		}

		#endregion

		#endregion

		#region Parsing...

		/// <summary>Converts a string literal into a boolean, using relaxed rules</summary>
		/// <param name="value">Literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <param name="defaultValue">Default value, if the string is empty or not recognized</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <paramref name="defaultValue"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>, the null or empty string.</para>
		/// </remarks>
		[Pure]
		public static bool ToBoolean(string? value, bool defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			return char.ToLowerInvariant(value[0]) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' || defaultValue), // "on"
					3 => ((char.ToLowerInvariant(value[1]) != 'f' || char.ToLowerInvariant(value[2]) != 'f') && defaultValue), // "off"
					_ => defaultValue
				},
				'1' => true, // "1"
				'0' => false, // "0"
				_ => defaultValue
			};
		}

		/// <summary>Converts a string literal into its boolean equivalent, using relaxed rules</summary>
		/// <param name="value">Literal containing either a "truthy" or "falsy" boolean (ex: "true", "on", "1" vs "false", "off", "0", ...)</param>
		/// <returns>Corresponding boolean value if it matches the set of recognized tokens; otherwise, <see langword="null"/>.</returns>
		/// <remarks>
		/// <para>The recognized "truthy" literals are: <c>"true"</c>, <c>"yes"</c>, <c>"on"</c>, <c>"1"</c>.</para>
		/// <para>The recognized "falsy" literals are: <c>"false"</c>, <c>"no"</c>, <c>"off"</c>, <c>"0"</c>.</para>
		/// <para>The null and empty strings will return <see langword="null"/>.</para>
		/// </remarks>
		/// <example>
		/// <code>StringConverters.ToBoolean("true", false) == true</code>
		/// <code>StringConverters.ToBoolean("false", true) == false</code>
		/// <code>StringConverters.ToBoolean("hello", false) == false</code>
		/// </example>
		[Pure]
		public static bool? ToBoolean(string? value)
		{
			if (string.IsNullOrEmpty(value)) return null;
			char c = value[0];
			return char.ToLowerInvariant(c) switch
			{
				't' => true,  // "true"
				'f' => false, // "false"
				'y' => true,  // "yes"
				'n' => false, // "no"
				'o' => value.Length switch
				{
					2 => (char.ToLowerInvariant(value[1]) == 'n' ? true : null), // "on"
					3 => (char.ToLowerInvariant(value[1]) == 'f' && char.ToLowerInvariant(value[2]) == 'f' ? false : null), // "off"
					_ => null
				},
				'1' => true,
				'0' => false,
				_ => null
			};
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt32("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static int ToInt32(string? value, int defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? c - 48 : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}
			
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">string literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt32("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static int ToInt32(ReadOnlySpan<char> value, int defaultValue)
		{
			if (value.Length == 0)
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? c - 48 : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}
			
			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234") == 1234</code>
		/// <code>StringConverters.ToInt32("hello") == null</code>
		/// </example>
		[Pure]
		public static int? ToInt32(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? (c - 48) : null;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return null;
			}

			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : null;
		}

		/// <summary>Converts a string literal into its 32-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt32("1234") == 1234</code>
		/// <code>StringConverters.ToInt32("hello") == null</code>
		/// </example>
		[Pure]
		public static int? ToInt32(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? (c - 48) : null;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return null;
			}

			return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res) ? res : null;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt64("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static long ToInt64(string? value, long defaultValue)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid integer</param>
		/// <returns>Corresponding integer value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234", 0) == 1234</code>
		/// <code>StringConverters.ToInt64("hello", 0) == 0</code>
		/// </example>
		[Pure]
		public static long ToInt64(ReadOnlySpan<char> value, long defaultValue)
		{
			if (value.Length == 0)
			{
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : defaultValue;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return defaultValue;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234") == 1234</code>
		/// <code>StringConverters.ToInt64("hello") == null</code>
		/// </example>
		[Pure]
		public static long? ToInt64(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : null;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return null;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : null;
		}

		/// <summary>Converts a string literal into its 64-bit signed integer equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1234" or "-5")</param>
		/// <returns>Corresponding integer value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToInt64("1234") == 1234</code>
		/// <code>StringConverters.ToInt64("hello") == null</code>
		/// </example>
		[Pure]
		public static long? ToInt64(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{
				return char.IsDigit(c) ? ((long) c - 48) : null;
			}

			if (!char.IsDigit(c) && c != '-' && c != '+' && c != ' ')
			{
				return null;
			}

			return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long res) ? res : null;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding double value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello", 0) => 0</code>
		/// <code>StringConverters.ToDouble("NaN", 0) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞", 0) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞", 0) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double ToDouble(string? value, double defaultValue, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out double result)
				? result
				: value == "-∞" ? double.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding double value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello", 0) => 0</code>
		/// <code>StringConverters.ToDouble("NaN", 0) => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞", 0) => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞", 0) => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double ToDouble(ReadOnlySpan<char> value, double defaultValue, IFormatProvider? provider = null)
		{
			if (value.Length == 0)
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out double result)
				? result
				: value is "-∞" ? double.NegativeInfinity
					: defaultValue;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding double value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23") => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello") => null</code>
		/// <code>StringConverters.ToDouble("NaN") => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞") => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞") => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double? ToDouble(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result
				: value == "-∞" ? double.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 64-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding double value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToDouble("1.23") => 1.23d</code>
		/// <code>StringConverters.ToDouble("hello") => null</code>
		/// <code>StringConverters.ToDouble("NaN") => double.NaN</code>
		/// <code>StringConverters.ToDouble("∞") => double.PositiveInfinity</code>
		/// <code>StringConverters.ToDouble("-∞") => double.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static double? ToDouble(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? double.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result
				: value is "-∞" ? double.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding single value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello", 0) => 0</code>
		/// <code>StringConverters.ToSingle("NaN", 0) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞", 0) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞", 0) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float ToSingle(string? value, float defaultValue, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out float result) ? result
				: value == "-∞" ? float.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding single value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23", 0) => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello", 0) => 0</code>
		/// <code>StringConverters.ToSingle("NaN", 0) => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞", 0) => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞", 0) => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float ToSingle(ReadOnlySpan<char> value, float defaultValue, IFormatProvider? provider = null)
		{
			if (value.Length == 0)
			{ // empty
				return defaultValue;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : defaultValue;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, provider ?? CultureInfo.InvariantCulture, out float result) ? result
				: value is "-∞" ? float.NegativeInfinity
				: defaultValue;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding single value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23") => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello") => null</code>
		/// <code>StringConverters.ToSingle("NaN") => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞") => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞") => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float? ToSingle(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result
				: value == "-∞" ? float.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 32-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0" or "123" or "123.456e7")</param>
		/// <returns>Corresponding single value, or <see langword="null"/> if it could not be decoded</returns>
		/// <example>
		/// <code>StringConverters.ToSingle("1.23") => 1.23d</code>
		/// <code>StringConverters.ToSingle("hello") => null</code>
		/// <code>StringConverters.ToSingle("NaN") => float.NaN</code>
		/// <code>StringConverters.ToSingle("∞") => float.PositiveInfinity</code>
		/// <code>StringConverters.ToSingle("-∞") => float.NegativeInfinity</code>
		/// </example>
		[Pure]
		public static float? ToSingle(ReadOnlySpan<char> value)
		{
			if (value.Length == 0)
			{ // empty
				return null;
			}

			char c = value[0];
			if (value.Length == 1)
			{ // single-digit number
				return char.IsDigit(c) ? c - '0' : c == '∞' ? float.PositiveInfinity : null;
			}

			// note: TryParse with InvariantCulture will handle "NaN" but not "∞", "+∞", "-∞"
			return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ? result
				: value is "-∞" ? float.NegativeInfinity
				: null;
		}

		/// <summary>Converts a string literal into its 128-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0", "123.456e7")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid decimal number</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding decimal value, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		[Pure]
		public static decimal ToDecimal(string? value, decimal defaultValue, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ')
			{
				return defaultValue;
			}

			provider ??= CultureInfo.InvariantCulture;
			if (provider.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0)
			{
				value = value.Replace(',', '.');
			}

			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, provider, out decimal result) ? result : defaultValue;
		}

		/// <summary>Converts a string literal into its 128-bit floating-point number equivalent, using invariant-culture format</summary>
		/// <param name="value">Literal to convert (ex: "1.0", "123.456e7")</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding single value, or <see langword="null"/> if it could not be decoded</returns>
		[Pure]
		public static decimal? ToDecimal(string? value, IFormatProvider? provider = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return null;
			}

			char c = value[0];
			if (!char.IsDigit(c) && c != '+' && c != '-' && c != '.' && c != ' ')
			{
				return null;
			}

			provider ??= CultureInfo.InvariantCulture;
			if (provider.Equals(CultureInfo.InvariantCulture) && value.IndexOf(',') >= 0)
			{
				value = value.Replace(',', '.');
			}

			return decimal.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, provider, out decimal result) ? result : null;
		}

		/// <summary>Converts a string literal into its <see cref="DateTime"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "2025-05-27T16:17:89.456")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid date</param>
		/// <param name="provider">Optional Format provider</param>
		[Pure]
		public static DateTime ToDateTime(string value, DateTime defaultValue, CultureInfo? provider = null)
		{
			return ParseDateTime(value, defaultValue, provider);
		}

		/// <summary>Converts a string literal into its <see cref="DateTime"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "2025-05-27T16:17:89.456")</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding <see cref="DateTime"/>, or <see langword="null"/> if it could not be decoded</returns>
		[Pure]
		public static DateTime? ToDateTime(string? value, CultureInfo? provider = null)
		{
			if (string.IsNullOrEmpty(value)) return null;
			DateTime result = ParseDateTime(value, DateTime.MaxValue, provider);
			return result == DateTime.MaxValue ? null : result;
		}

		/// <summary>Converts a string literal into its <see cref="NodaTime.Instant"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "2025-05-27T16:17:89.456")</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding <see cref="NodaTime.Instant"/>, or <see langword="null"/> if it could not be decoded</returns>
		[Pure]
		public static Instant? ToInstant(string? value, CultureInfo? provider = null)
		{
			if (string.IsNullOrEmpty(value)) return null;
			if (!TryParseInstant(value, provider, out Instant res, false)) return null;
			return res;
		}

		/// <summary>Converts a string literal into its <see cref="NodaTime.Instant"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "2025-05-27T16:17:89.456")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or not a valid date</param>
		/// <param name="provider">Optional Format provider</param>
		/// <returns>Corresponding <see cref="NodaTime.Instant"/>, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		[Pure]
		public static Instant ToInstant(string? value, Instant defaultValue, CultureInfo? provider = null)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			if (!TryParseInstant(value, provider, out Instant res, false)) return defaultValue;
			return res;
		}

		/// <summary>Converts a string literal into its <see cref="Guid"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "01234567-89AB-CDEF-0123-456789ABCDEF")</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or invalid</param>
		/// <returns>Corresponding <see cref="Guid"/>, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		[Pure]
		public static Guid ToGuid(string? value, Guid defaultValue)
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Guid.TryParse(value, out Guid result) ? result : defaultValue;
		}

		/// <summary>Converts a string literal into its <see cref="Guid"/> equivalent</summary>
		/// <param name="value">Literal to convert (ex: "01234567-89AB-CDEF-0123-456789ABCDEF")</param>
		/// <returns>Corresponding <see cref="Guid"/>, or <see langword="null"/> if it could not be decoded</returns>
		[Pure]
		public static Guid? ToGuid(string? value)
		{
			if (string.IsNullOrEmpty(value)) return null;
			return Guid.TryParse(value, out Guid result) ? result : null;
		}

		/// <summary>Converts a string literal into its <typeparamref name="TEnum"/> enum equivalent</summary>
		/// <typeparam name="TEnum">Type de l'Enum</typeparam>
		/// <param name="value">Literal to convert (ex: <c>"Red"</c>, <c>"2"</c>, ...)</param>
		/// <param name="defaultValue">Fallback value returned if the string literal is empty or invalid</param>
		/// <returns>Corresponding <typeparamref name="TEnum"/>, or <paramref name="defaultValue"/> if it could not be decoded</returns>
		[Pure]
		public static TEnum ToEnum<TEnum>(string? value, TEnum defaultValue)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return defaultValue;
			return Enum.TryParse(value, true, out TEnum result) ? result : defaultValue;
		}

		/// <summary>Converts a string literal into its <typeparamref name="TEnum"/> enum equivalent</summary>
		/// <typeparam name="TEnum">Type de l'Enum</typeparam>
		/// <param name="value">Literal to convert (ex: <c>"Red"</c>, <c>"2"</c>, ...)</param>
		/// <returns>Corresponding <typeparamref name="TEnum"/>, or <see langword="null"/> if it could not be decoded</returns>
		[Pure]
		public static TEnum? ToEnum<TEnum>(string? value)
			where TEnum : struct, Enum
		{
			if (string.IsNullOrEmpty(value)) return null;
			return Enum.TryParse(value, true, out TEnum result) ? result : null;
		}

		/// <summary>Maximum number of characters required to safely format any signed 8-bit integer in base 10</summary>
		/// <remarks><see cref="sbyte.MinValue"/> needs 4 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt8 = 4;

		/// <summary>Maximum number of characters required to safely format any unsigned 8-bit integer in base 10</summary>
		/// <remarks><see cref="byte.MaxValue"/> needs 3 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt8 = 3;

		/// <summary>Maximum number of characters required to safely format any signed 16-bit integer in base 10</summary>
		/// <remarks><see cref="short.MinValue"/> needs 6 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt16 = 6;

		/// <summary>Maximum number of characters required to safely format any unsigned 16-bit integer in base 10</summary>
		/// <remarks><see cref="ushort.MaxValue"/> needs 5 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt16 = 5;

		/// <summary>Maximum number of characters required to safely format any signed 32-bit integer in base 10</summary>
		/// <remarks><see cref="int.MinValue"/> needs 11 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt32 = 11;

		/// <summary>Maximum number of characters required to safely format any unsigned 32-bit integer in base 10</summary>
		/// <remarks><see cref="uint.MaxValue"/> needs 10 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt32 = 10;

		/// <summary>Maximum number of characters required to safely format any signed 64-bit integer in base 10</summary>
		/// <remarks><see cref="long.MinValue"/> needs 20 characters in base 10 (including the negative sign)</remarks>
		/// 
		public const int Base10MaxCapacityInt64 = 20;

		/// <summary>Maximum number of characters required to safely format any unsigned 64-bit integer in base 10</summary>
		/// <remarks><see cref="ulong.MaxValue"/> needs 20 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt64 = 20;

		/// <summary>Maximum number of characters required to safely format any 32-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in <c>Number.TryFormatDouble(...)</c></remarks>
		public const int Base10MaxCapacitySingle = 32;

		/// <summary>Maximum number of characters required to safely format any 64-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in <c>Number.TryFormatDouble(...)</c></remarks>
		public const int Base10MaxCapacityDouble = 32;

		/// <summary>Maximum number of characters required to safely format any 128-bit decimal floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in <c>Number.TryFormatDouble(...)</c></remarks>
		public const int Base10MaxCapacityDecimal = 32;

		/// <summary>Maximum number of characters required to safely format any 16-bit IEEE floating point number in base 10</summary>
		/// <remarks>This the value used by the runtime in <c>Number.TryFormatDouble(...)</c></remarks>
		public const int Base10MaxCapacityHalf = 32;

		/// <summary>Maximum number of characters required to safely format any 128-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffff-ffff-ffff-ffffffffffff" requires 36 characters</remarks>
		public const int Base16MaxCapacityGuid = 36;

		/// <summary>Maximum number of characters required to safely format any 128-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffff-ffff-ffff-ffffffffffff" requires 36 characters</remarks>
		public const int Base16MaxCapacityUuid128 = 36;

		/// <summary>Maximum number of characters required to safely format any 64-bit using the standard format</summary>
		/// <remarks>"ffffffff-ffffffff" requires 17 characters</remarks>
		public const int Base16MaxCapacityUuid64 = 17;

#if NET8_0_OR_GREATER

		/// <summary>Maximum number of characters required to safely format any signed 64-bit integer in base 10</summary>
		/// <remarks><see cref="Int128.MinValue"/> needs 40 characters in base 10 (including the negative sign)</remarks>
		public const int Base10MaxCapacityInt128 = 40;

		/// <summary>Maximum number of characters required to safely format any unsigned 64-bit integer in base 10</summary>
		/// <remarks><see cref="UInt128.MaxValue"/> needs 39 characters in base 10</remarks>
		public const int Base10MaxCapacityUInt128 = 39;

#endif

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(long value)
		{
			return value >= 0 ? CountDigits((ulong) value) : value != long.MinValue ? (1 + CountDigits((ulong)(-value))) : Base10MaxCapacityInt64;
		}


		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(ulong value)
		{
			// Copied from System.Buffers.Text.FormattingHelpers.CountDigits(ulong) that is marked internal
			// Based on do_count_digits from https://github.com/fmtlib/fmt/blob/662adf4f33346ba9aba8b072194e319869ede54a/include/fmt/format.h#L1124

			if (value < 10) return 1;

			// Map the log2(value) to a power of 10.
			ReadOnlySpan<byte> log2ToPow10 =
			[
				1,  1,  1,  2,  2,  2,  3,  3,  3,  4,  4,  4,  4,  5,  5,  5,
				6,  6,  6,  7,  7,  7,  7,  8,  8,  8,  9,  9,  9,  10, 10, 10,
				10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
				15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
			];

			uint index = Unsafe.Add(ref MemoryMarshal.GetReference(log2ToPow10), BitOperations.Log2(value));

			// Read the associated power of 10.
			ReadOnlySpan<ulong> powersOf10 =
			[
				0, // unused entry to avoid needing to subtract
				0, 10, 100,
				1_000, 10_000, 100_000,
				1_000_000, 10_000_000, 100_000_000,
				1_000_000_000, 10_000_000_000, 100_000_000_000,
				1_000_000_000_000, 10_000_000_000_000, 100_000_000_000_000,
				1_000_000_000_000_000, 10_000_000_000_000_000, 100_000_000_000_000_000,
				1_000_000_000_000_000_000, 10_000_000_000_000_000_000,
			];
			ulong powerOf10 = Unsafe.Add(ref MemoryMarshal.GetReference(powersOf10), index);

			// Return the number of digits based on the power of 10, shifted by 1 if it falls below the threshold.
			bool lessThan = value < powerOf10;
			return (int) (index - Unsafe.As<bool, byte>(ref lessThan)); // while arbitrary bools may be non-0/1, comparison operators are expected to return 0/1
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(short value) => CountDigits((int) value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(ushort value) => CountDigits((uint) value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(int value)
		{
			return value >= 0 ? CountDigits((uint) value) : value != int.MinValue ? (1 + CountDigits((uint) (-value))) : Base10MaxCapacityInt32;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int CountDigits(uint value)
		{

			if (value < 10) return 1;
#if NET8_0_OR_GREATER
			// Copied from System.Buffers.Text.FormattingHelpers.CountDigits(ulong) that is marked internal
			// Algorithm based on https://lemire.me/blog/2021/06/03/computing-the-number-of-digits-of-an-integer-even-faster.
			ReadOnlySpan<long> table =
			[
				4294967296, 8589934582, 8589934582, 8589934582, 12884901788, 12884901788, 12884901788, 17179868184,
				17179868184, 17179868184, 21474826480, 21474826480, 21474826480, 21474826480, 25769703776, 25769703776,
				25769703776, 30063771072, 30063771072, 30063771072, 34349738368, 34349738368, 34349738368, 34349738368,
				38554705664, 38554705664, 38554705664, 41949672960, 41949672960, 41949672960, 42949672960, 42949672960,
			];

			long tableValue = Unsafe.Add(ref MemoryMarshal.GetReference(table), uint.Log2(value));
			return (int) ((value + tableValue) >> 32);
#else
			// slow path for .NET 6
			if (value < 100) return 2;
			if (value < 1000) return 3;
			if (value < 10000) return 4;
			if (value < 100000) return 5;
			if (value < 1000000) return 6;
			if (value < 10000000) return 7;
			if (value < 100000000) return 8;
			if (value < 1000000000) return 9;
			return 10;
#endif
		}

		#endregion

		#region Dates...

		/// <summary>Converts a <see cref="DateTime"/> value into a string literal with format <c>YYYYMMDDHHMMSS</c></summary>
		/// <param name="date">Date to format</param>
		/// <returns>Formated date of length 14, with format <c>YYYYMMDDHHMMSS</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(DateTime date)
		{
			//PERF: is there a faster way to encode to this format?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Converts a <see cref="NodaTime.Instant"/> value into a string literal with format <c>YYYYMMDDHHMMSS</c></summary>
		/// <param name="date">Date to format</param>
		/// <returns>Formated date of length 14, with format <c>YYYYMMDDHHMMSS</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(Instant date)
		{
			//PERF: is there a faster way to encode to this format?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Converts a <see cref="NodaTime.ZonedDateTime"/> value into a string literal with format <c>YYYYMMDDHHMMSS</c></summary>
		/// <param name="date">Date to format</param>
		/// <returns>Formated date of length 14, with format <c>YYYYMMDDHHMMSS</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateTimeString(ZonedDateTime date)
		{
			//PERF: is there a faster way to encode to this format?
			return date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Converts the date component of a <see cref="DateTime"/> value into a string literal with format <c>YYYYMMDD</c></summary>
		/// <param name="date">Date to format</param>
		/// <returns>Formated date of length 8, with format <c>YYYYMMDD</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToDateString(DateTime date)
		{
			//PERF: is there a faster way to encode to this format?
			return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
		}

		/// <summary>Converts the time component of a <see cref="DateTime"/> value into a string literal with format <c>HHMMSS</c></summary>
		/// <param name="date">Time to format</param>
		/// <returns>Formated time of length 6, with format <c>HHMMSS</c></returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string ToTimeString(DateTime date)
		{
			//PERF: is there a faster way to encode to this format?
			return date.ToString("HHmmss", CultureInfo.InvariantCulture);
		}

		/// <summary>Parses a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <returns>Corresponding <see cref="DateTime"/> value if successful</returns>
		/// <exception cref="System.ArgumentException">If the date is malformed</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date)
		{
			return ParseDateTime(date, null);
		}

		/// <summary>Parses a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="culture">Optional culture provider</param>
		/// <returns>Corresponding <see cref="DateTime"/> value if successful</returns>
		/// <exception cref="System.ArgumentException">If the date is malformed</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, true)) throw FailInvalidDateFormat();
			return result;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentException FailInvalidDateFormat()
		{
			// ReSharper disable once NotResolvedInText
			return new("Invalid date format", "date");
		}

		/// <summary>Parses a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="defaultValue">Default value if null, empty, or malformed</param>
		/// <returns>Corresponding <see cref="DateTime"/> value if successful; otherwise, <paramref name="defaultValue"/></returns>
		/// <exception cref="System.ArgumentException">If the date is malformed</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime defaultValue)
		{
			if (string.IsNullOrEmpty(date)) return defaultValue;
			if (!TryParseDateTime(date, null, out DateTime result, false)) return defaultValue;
			return result;
		}

		/// <summary>Parses a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="defaultValue">Default value if null, empty, or malformed</param>
		/// <param name="culture">Optional culture provider</param>
		/// <returns>Corresponding <see cref="DateTime"/> value if successful; otherwise, <paramref name="defaultValue"/></returns>
		/// <exception cref="System.ArgumentException">If the date is malformed</exception>
		[Pure]
		public static DateTime ParseDateTime(string? date, DateTime defaultValue, CultureInfo? culture)
		{
			if (!TryParseDateTime(date, culture, out DateTime result, false)) return defaultValue;
			return result;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int ParseDateSegment(ReadOnlySpan<char> source)
		{
			return int.TryParse(source, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : -1;
		}

		/// <summary>Attempts to parse a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="culture">Optional culture provider</param>
		/// <param name="result">Receives the parsed <see cref="DateTime"/>, if successful.</param>
		/// <param name="throwsFail">If <c>false</c>, catch any parsing exception; otherwise, let them bubble up.</param>
		/// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryParseDateTime(string? date, CultureInfo? culture, out DateTime result, bool throwsFail)
		{
			if (date == null)
			{
				if (throwsFail) throw new ArgumentNullException(nameof(date));
				result = DateTime.MinValue;
				return false;
			}
			return TryParseDateTime(date.AsSpan(), culture, out result, throwsFail);
		}

		/// <summary>Attempts to parse a string literal with any compatible format into a <see cref="DateTime"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="culture">Optional culture provider</param>
		/// <param name="result">Receives the parsed <see cref="DateTime"/>, if successful.</param>
		/// <param name="throwsFail">If <c>false</c>, catch any parsing exception; otherwise, let them bubble up.</param>
		/// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryParseDateTime(ReadOnlySpan<char> date, CultureInfo? culture, out DateTime result, bool throwsFail)
		{
			result = DateTime.MinValue;

			if (date.Length < 4)
			{
				return !throwsFail ? false : throw new FormatException("Date must be at least 4 characters long");
			}

			try
			{
				if (char.IsDigit(date[0]))
				{ // starts with a digit, maybe a timestamp?
					switch (date.Length)
					{
						case 4:
						{ // YYYY -> YYYY/01/01 00:00:00.000
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							result = new DateTime(y, 1, 1);
							return true;
						}
						case 6:
						{ // YYYYMM -> YYYY/MM/01 00:00:00.000
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							result = new DateTime(y, m, 1);
							return true;
						}
						case 8:
						{ // YYYYMMDD -> YYYY/MM/DD 00:00:00.000
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							result = new DateTime(y, m, d);
							return true;
						}
						case 10:
						{ // "YYYY-MM-DD" -> YYYY/MM/DD 00:00:00.000
							if (date[4] != '-' || date[7] != '-') break;
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date[5..7]);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date[8..]);
							if (d < 1 || d > 31) break;
							result = new DateTime(y, m, d, 0, 0, 0);
							return true;
						}
						case 14:
						{ // YYYYMMDDHHMMSS -> YYYY/MM/DD HH:MM:SS.000
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date.Slice(8, 2));
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date.Slice(10, 2));
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date.Slice(12, 2));
							if (s < 0 || s > 59) break;
							result = new DateTime(y, m, d, h, n, s);
							return true;
						}
						case 17:
						{ // YYYYMMDDHHMMSSFFF -> YYYY/MM/DD HH:MM:SS.FFF
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date.Slice(4, 2));
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date.Slice(6, 2));
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date.Slice(8, 2));
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date.Slice(10, 2));
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date.Slice(12, 2));
							if (s < 0 || s > 59) break;
							int f = ParseDateSegment(date.Slice(14, 3));
							result = new DateTime(y, m, d, h, n, s, f);
							return true;
						}
						case 19:
						{ // "YYYY-MM-DDTHH:mm:ss" -> YYYY/MM/DD HH:mm:ss.000
							if (date[4] != '-' || date[7] != '-' || date[10] != 'T' || date[13] != ':' || date[16] != ':') break;
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date[5..7]);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date[8..10]);
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date[11..13]);
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date[14..16]);
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date[17..]);
							if (s < 0 || s > 60) break; // note: extra UTC second at 23:59:60 !
							result = new DateTime(y, m, d, h, m, s, DateTimeKind.Unspecified);
							return true;
						}
						case 20:
						{ // "YYYY-MM-DDTHH:mm:ssZ" -> YYYY/MM/DD HH:mm:ss.000
							if (date[19] != 'Z' || date[4] != '-' || date[7] != '-' || date[10] != 'T' || date[13] != ':' || date[16] != ':') break;
							int y = ParseDateSegment(date[..4]);
							if (y < 1 || y > 9999) break;
							int m = ParseDateSegment(date[5..7]);
							if (m < 1 || m > 12) break;
							int d = ParseDateSegment(date[8..10]);
							if (d < 1 || d > 31) break;
							int h = ParseDateSegment(date[11..13]);
							if (h < 0 || h > 23) break;
							int n = ParseDateSegment(date[14..16]);
							if (n < 0 || n > 59) break;
							int s = ParseDateSegment(date[17..19]);
							if (s < 0 || s > 60) break; // note: extra UTC second at 23:59:60 !
							result = new DateTime(y, m, d, h, m, s, DateTimeKind.Utc);
							return true;
						}
					}
				}
				else if (char.IsLetter(date[0]))
				{ // let's try a ParseExact for weird localized dates (ex: "Vendredi, 37 Trumaire 1789 à 3 heures moins le quart")
					result = DateTime.ParseExact(date, [ "D", "F", "f" ], culture ?? CultureInfo.InvariantCulture);
					return true;
				}

				// Fallback to standard parsing...
				result = DateTime.Parse(date, culture ?? CultureInfo.InvariantCulture);
				return true;
			}
			catch (FormatException)
			{ // malformed input
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // happens for invalid dates (Feb-31, Jan-42, ...)
				if (throwsFail) throw;
				return false;
			}
		}

		/// <summary>Attempts to parse a string literal with any compatible format into a <see cref="NodaTime.Instant"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="culture">Optional culture provider</param>
		/// <param name="result">Receives the parsed <see cref="NodaTime.Instant"/>, if successful.</param>
		/// <param name="throwsFail">If <c>false</c>, catch any parsing exception; otherwise, let them bubble up.</param>
		/// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryParseInstant(string? date, CultureInfo? culture, out Instant result, bool throwsFail)
		{

			if (date == null)
			{
				if (throwsFail) throw new ArgumentNullException(nameof(date));
				result = default(Instant);
				return false;
			}

			return TryParseInstant(date.AsSpan(), culture, out result, throwsFail);
		}

		/// <summary>Attempts to parse a string literal with any compatible format into a <see cref="NodaTime.Instant"/> (<c>"YYYY"</c>, <c>"YYYYMM"</c>, <c>"YYYYMMDD"</c>, <c>"YYYY-MM-DD"</c>, <c>"YYYYMMDDHHMMSS"</c>, or <c>"YYYY-MM-DDTHH:MM:SS"</c>)</summary>
		/// <param name="date">String literal to parse</param>
		/// <param name="culture">Optional culture provider</param>
		/// <param name="result">Receives the parsed <see cref="NodaTime.Instant"/>, if successful.</param>
		/// <param name="throwsFail">If <c>false</c>, catch any parsing exception; otherwise, let them bubble up.</param>
		/// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryParseInstant(ReadOnlySpan<char> date, CultureInfo? culture, out Instant result, bool throwsFail)
		{
			result = default(Instant);

			if (date.Length < 4)
			{
				if (throwsFail) throw new FormatException("Date must be at least 4 characters long");
				return false;
			}
			if (!char.IsDigit(date[0]))
			{
				if (throwsFail) throw new FormatException("Date must contains only digits");
				return false;
			}
			try
			{
				switch (date.Length)
				{
					case 4:
					{ // "YYYY" -> YYYY/01/01 00:00:00.000
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						result = Instant.FromUtc(y, 1, 1, 0, 0);
						return true;
					}
					case 6:
					{ // "YYYYMM" -> YYYY/MM/01 00:00:00.000
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						result = Instant.FromUtc(y, m, 1, 0, 0);
						return true;
					}
					case 8:
					{ // "YYYYMMDD" -> YYYY/MM/DD 00:00:00.000
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date.Slice(4, 2));
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date.Slice(6, 2));
						if (d < 1 || d > 31) break;
						result = Instant.FromUtc(y, m, d, 0, 0);
						return true;
					}
					case 10:
					{ // "YYYY-MM-DD" -> YYYY/MM/DD 00:00:00.000
						if (date[4] != '-' || date[7] != '-') break;
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date[5..7]);
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date[8..]);
						if (d < 1 || d > 31) break;
						result = Instant.FromUtc(y, m, d, 0, 0);
						return true;
					}
					case 14:
					{ // "YYYYMMDDHHMMSS" -> YYYY/MM/DD HH:MM:SS.000
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date[4..6]);
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date[6..8]);
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date[8..10]);
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date[10..12]);
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date[12..]);
						if (s < 0 || s > 59) break;
						result = Instant.FromUtc(y, m, d, h, n, s);
						return true;
					}
					case 17:
					{ // "YYYYMMDDHHMMSSFFF" -> YYYY/MM/DD HH:MM:SS.FFF
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date[4..6]);
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date[6..8]);
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date[8..10]);
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date[10..12]);
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date[12..14]);
						if (s < 0 || s > 60) break; // note: extra UTC second at 23:59:60 !
						int f = ParseDateSegment(date[14..]);
						if (f < 0 || f > 999) break;
						result = Instant.FromUtc(y, m, d, h, n, s) + Duration.FromMilliseconds(f);
						return true;
					}
					case 19:
					{ // "YYYY-MM-DDTHH:mm:ss" -> YYYY/MM/DD HH:mm:ss.000
						if (date[4] != '-' || date[7] != '-' || date[10] != 'T' || date[13] != ':' || date[16] != ':') break;
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date[5..7]);
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date[8..10]);
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date[11..13]);
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date[14..16]);
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date[17..]);
						if (s < 0 || s > 60) break; // note: extra UTC second at 23:59:60 !
						result = Instant.FromUtc(y, m, d, h, m, s);
						return true;
					}
					case 20:
					{ // "YYYY-MM-DDTHH:mm:ssZ" -> YYYY/MM/DD HH:mm:ss.000
						if (date[19] != 'Z' || date[4] != '-' || date[7] != '-' || date[10] != 'T' || date[13] != ':' || date[16] != ':') break;
						int y = ParseDateSegment(date[..4]);
						if (y < 1 || y > 9999) break;
						int m = ParseDateSegment(date[5..7]);
						if (m < 1 || m > 12) break;
						int d = ParseDateSegment(date[8..10]);
						if (d < 1 || d > 31) break;
						int h = ParseDateSegment(date[11..13]);
						if (h < 0 || h > 23) break;
						int n = ParseDateSegment(date[14..16]);
						if (n < 0 || n > 59) break;
						int s = ParseDateSegment(date[17..19]);
						if (s < 0 || s > 60) break; // note: extra UTC second at 23:59:60 !
						result = Instant.FromUtc(y, m, d, h, m, s);
						return true;
					}
				}
			}
			catch (FormatException)
			{ // malformed input
				if (throwsFail) throw;
				return false;
			}
			catch (ArgumentOutOfRangeException)
			{ // happens for invalid dates (Feb-31, Jan-42, ...)
				if (throwsFail) throw;
				return false;
			}
			if (throwsFail) throw new FormatException("Date must only contain digits");
			return false;
		}

		/// <summary>Converts a string literal containing a "human friendly" representation into a <see cref="TimeOnly"/>, for example <c>"11"</c>,<c>"11h"</c>,<c>"11h00"</c>,<c>"11:00"</c> -> <c>{11:00:00.000}</c></summary>
		/// <param name="time">String to parse</param>
		/// <returns>Corresponding <see cref="TimeOnly"/> value.</returns>
		[Pure]
		public static TimeOnly ParseTime(string time)
		{
			Contract.NotNullOrEmpty(time);

			time = time.ToLowerInvariant();

			int hour;
			int minute = 0;
			int second = 0;

			int p = time.IndexOf('h');
			if (p > 0)
			{
				hour = short.Parse(time[..p], NumberStyles.Integer, CultureInfo.InvariantCulture);
				if (p + 1 >= time.Length)
				{
					minute = 0;
				}
				else
				{
					minute = short.Parse(time[(p + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
				}
			}
			else
			{
				p = time.IndexOf(':');
				if (p > 0)
				{
					hour = short.Parse(time[..p], NumberStyles.Integer, CultureInfo.InvariantCulture);
					if (p + 1 >= time.Length)
					{
						minute = 0;
					}
					else
					{
						minute = short.Parse(time[(p + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture);
					}
				}
				else
				{
					hour = short.Parse(time, NumberStyles.Integer, CultureInfo.InvariantCulture);
				}
			}
			return new TimeOnly(hour, minute, second, 0);
		}

		#endregion

	}

}
