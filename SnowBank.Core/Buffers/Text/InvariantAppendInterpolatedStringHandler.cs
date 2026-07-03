#region Copyright (c) 2023-2026 SnowBank SAS

// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.

#endregion

// ReSharper disable MethodOverloadWithOptionalParameter

namespace System.Text
{
	using System.Globalization;

	/// <summary>
	/// Interpolated string handler that appends to a <see cref="StringBuilder"/> in a culture-invariant manner.
	/// Wrapper around <see cref="StringBuilder.AppendInterpolatedStringHandler"/>.
	/// </summary>
	[InterpolatedStringHandler]
	public ref struct InvariantAppendInterpolatedStringHandler
	{

		/// <summary>Handler that handles the actual implementation.</summary>
		private StringBuilder.AppendInterpolatedStringHandler Inner;

		/// <summary>Creates a handler used to translate an interpolated string into a <see cref="string"/>, using the invariant culture.</summary>
		/// <param name="literalLength">The number of constant characters outside of interpolation expressions in the interpolated string.</param>
		/// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
		/// <param name="sb">The associated StringBuilder to which to append.</param>
		/// <remarks>This is intended to be called only by compiler-generated code. Arguments are not validated as they'd otherwise be for members intended to be used directly.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public InvariantAppendInterpolatedStringHandler(int literalLength, int formattedCount, StringBuilder sb)
		{
			this.Inner = new(literalLength, formattedCount, sb, CultureInfo.InvariantCulture);
		}

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendLiteral" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendLiteral(string value) => this.Inner.AppendLiteral(value);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted{T}(T)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value) => this.Inner.AppendFormatted(value);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted{T}(T)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, string? format)
			=> this.Inner.AppendFormatted(value, format);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted{T}(T, int)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, int alignment)
			=> this.Inner.AppendFormatted(value, alignment);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted{T}(T, int, string?)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted<T>(T value, int alignment, string? format)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char})" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(scoped ReadOnlySpan<char> value)
			=> this.Inner.AppendFormatted(value);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(ReadOnlySpan{char}, int, string?)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(string?)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(string? value)
			=> this.Inner.AppendFormatted(value);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(string?, int, string?)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(string? value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

		/// <inheritdoc cref="StringBuilder.AppendInterpolatedStringHandler.AppendFormatted(object?, int, string?)" />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AppendFormatted(object? value, int alignment = 0, string? format = null)
			=> this.Inner.AppendFormatted(value, alignment, format);

	}

	/// <summary>
	/// Extension methods for <see cref="StringBuilder"/> which format interpolated strings with
	/// <see cref="CultureInfo.InvariantCulture"/>, without having to pass the provider on each call.
	/// </summary>
	/// <remarks>
	/// Replaces the pattern <c>sb.Append(CultureInfo.InvariantCulture, $"...")</c> (verbose, false positives CA1305
	/// when all the holes are strings) with <c>sb.AppendInvariant($"...")</c>. The values are written
	/// directly in the internal buffer of the <see cref="StringBuilder"/> (no intermediate string allocation),
	/// via the <see cref="StringBuilder.AppendInterpolatedStringHandler"/> machinery of the BCL, by forcing the invariant culture
	/// in the handler's constructor.
	/// </remarks>
	public static class StringBuilderInvariantExtensions
	{

		/// <summary>Appends an interpolated string formatted with <see cref="CultureInfo.InvariantCulture"/>.</summary>
		/// <remarks>Allows avoiding having to specify <c>CultureInfo.InvariantCulture</c> systematically when calling <see cref="StringBuilder.Append(ref StringBuilder.AppendInterpolatedStringHandler)"/>:
		/// <code>
		/// // This method uses the invariant culture, and does not generate false positives for CA1305
		/// sb.AppendInvariant($"hello {name}, temperature is {temperature:D01}°C and current date is {DateTime.Now:yyyy-MM-dd}.")
		/// </code></remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static StringBuilder AppendInvariant(this StringBuilder sb, [InterpolatedStringHandlerArgument(nameof(sb))] ref InvariantAppendInterpolatedStringHandler handler)
			=> sb;

		/// <summary>Appends an interpolated string formatted with <see cref="CultureInfo.InvariantCulture"/>, followed by a line terminator.</summary>
		/// <remarks>Avoids having to specify <c>CultureInfo.InvariantCulture</c> systematically when calling <see cref="StringBuilder.AppendLine(ref StringBuilder.AppendInterpolatedStringHandler)"/>:
		/// <code>
		/// // This method uses the invariant culture, and does not generate false positives for CA1305
		/// sb.AppendLineInvariant($"hello {name}, temperature is {temperature:D01}°C and current date is {DateTime.Now:yyyy-MM-dd}.")
		/// </code></remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static StringBuilder AppendLineInvariant(this StringBuilder sb, [InterpolatedStringHandlerArgument(nameof(sb))] ref InvariantAppendInterpolatedStringHandler handler)
			=> sb.AppendLine();

	}

}
