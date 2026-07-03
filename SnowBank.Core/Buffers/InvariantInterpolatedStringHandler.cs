#region Copyright (c) 2023-2026 SnowBank SAS

// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.

#endregion

// ReSharper disable MethodOverloadWithOptionalParameter

namespace System
{

	public static class SystemStringExtensions
	{
		/// <param name="source">The string to copy from</param>
		extension(string source)
		{

			/// <summary>Calls <see cref="Span{T}.TryCopyTo"/> and, if successful, sets the number of copied items in <paramref name="written"/></summary>
			/// <param name="destination">The span to copy items into</param>
			/// <param name="written">Number of characters copied, or <c>0</c> if <paramref name="destination"/> is too small</param>
			/// <returns>If the destination span is shorter than the source span, this method return false and no data is written to the destination.</returns>
			/// <remarks><para>This helper method is very useful when implementing <see cref="ISpanFormattable"/>.</para></remarks>
			[MustUseReturnValue, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool TryCopyTo(Span<char> destination, out int written)
			{
				if (!source.TryCopyTo(destination))
				{
					written = 0;
					return false;
				}

				written = source.Length;
				return true;
			}

			/// <summary>Creates a new string by using the <see cref="System.Globalization.CultureInfo.InvariantCulture"/> to control the formatting of the specified interpolated string.</summary>
			/// <param name="handler">The interpolated string.</param>
			/// <returns>The string that results for formatting the interpolated string using the invariant format provider.</returns>
			/// <remarks><para>This is a shortcut to <c>string.Create(CultureInfo.InvariantCulture, $"...");</c></para></remarks>
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static string CreateInvariant(ref InvariantInterpolatedStringHandler handler)
			{
				return handler.ToStringAndClear();
			}

		}

	}

}

namespace System.Runtime.CompilerServices
{
	using System.Globalization;

	/// <summary>
	/// Interpolated string handler that uses <see cref="CultureInfo.InvariantCulture"/>, instead of the thread current culture.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Can be used as a method parameter (<c>ref InvariantInterpolatedStringHandler</c>) when you want to guarantee an invariant culture formatting at the callsite, in replacement of the pattern <c>FormattableString.Invariant($"...")</c> or <c>string.Create(CultureInfo.InvariantCulture, $"...")</c>.
	/// </para>
	/// <para>
	/// ATTENTION : a parameter of type <c>ref DefaultInterpolatedStringHandler</c> does NOT offer this guarantee : the compiler
	/// builds it without <see cref="IFormatProvider"/>, so the holes are formatted with the current culture before even
	/// entering the method. Calling <c>string.Create(CultureInfo.InvariantCulture, ref handler)</c> afterward has no
	/// effect on the culture : the provider parameter of <c>string.Create</c> is only consumed by the compiler (via
	/// <c>[InterpolatedStringHandlerArgument]</c>) when the interpolated string appears literally at the call of
	/// <c>string.Create</c> ; for a handler that is already filled, the runtime body reduces to <c>ToStringAndClear()</c>.
	/// </para>
	/// </remarks>
	[InterpolatedStringHandler]
	public ref struct InvariantInterpolatedStringHandler
	{

		/// <summary>Handler that handles the actual implementation.</summary>
		private DefaultInterpolatedStringHandler Inner;

		/// <summary>Creates a handler used to translate an interpolated string into a <see cref="string"/>, using the invariant culture.</summary>
		/// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
		/// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
		/// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
		public InvariantInterpolatedStringHandler(int literalLength, int formattedCount)
			=> this.Inner = new(literalLength, formattedCount, CultureInfo.InvariantCulture);

#if NET10_0_OR_GREATER

		/// <see cref="DefaultInterpolatedStringHandler.Text"/>
		public ReadOnlySpan<char> Text => this.Inner.Text;

#endif

		/// <see cref="DefaultInterpolatedStringHandler.AppendLiteral"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendLiteral(string value)
			=> Inner.AppendLiteral(value);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted{T}(T)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value)
			=> Inner.AppendFormatted(value);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted{T}(T, string)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, string? format)
			=> this.Inner.AppendFormatted(value, format);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted{T}(T, int)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, int alignment)
			=> this.Inner.AppendFormatted(value, alignment);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted{T}(T, int, string?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, int alignment, string? format)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char})"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(scoped ReadOnlySpan<char> value)
			=> this.Inner.AppendFormatted(value);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char}, int, string?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted(string)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(string? value)
			=> this.Inner.AppendFormatted(value);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted(string, int, string?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(string? value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <see cref="DefaultInterpolatedStringHandler.AppendFormatted(object, int, string?)"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(object? value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <see cref="DefaultInterpolatedStringHandler.ToStringAndClear"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public string ToStringAndClear() => this.Inner.ToStringAndClear();

#if NET10_0_OR_GREATER

		/// <see cref="DefaultInterpolatedStringHandler.Clear"/>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Clear() => this.Inner.Clear();

#endif

		public override string ToString() => this.Inner.ToString();

	}
}
