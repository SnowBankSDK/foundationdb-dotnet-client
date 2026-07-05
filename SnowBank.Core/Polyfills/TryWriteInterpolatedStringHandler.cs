#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

#if NETSTANDARD2_0

// ReSharper disable MethodOverloadWithOptionalParameter

namespace SnowBank.Compat
{
	using System.Runtime.CompilerServices;

	/// <summary>Minimal <c>netstandard2.0</c> backport of the BCL <c>MemoryExtensions.TryWriteInterpolatedStringHandler</c>.</summary>
	/// <remarks>
	/// <para>Formats directly into the destination span, like the modern handler, but without the <c>ICustomFormatter</c>
	/// and <c>IUtf8SpanFormattable</c> paths (the format providers used by this codebase are always plain cultures).</para>
	/// <para>Values that do not implement <see cref="ISpanFormattable"/> (which, on this target, includes all the BCL
	/// primitives: the polyfilled interface only exists in our own assemblies) are formatted through
	/// <see cref="IFormattable.ToString(string, IFormatProvider)"/>, which allocates a temporary string.</para>
	/// <para>This type only exists in the netstandard2.0 build; on modern targets the real BCL type is used.</para>
	/// </remarks>
	[InterpolatedStringHandler]
	public ref struct TryWriteInterpolatedStringHandler
	{

		private readonly Span<char> Destination;

		private readonly IFormatProvider? Provider;

		private int Position;

		private bool Success;

		public TryWriteInterpolatedStringHandler(int literalLength, int formattedCount, Span<char> destination, out bool shouldAppend)
			: this(literalLength, formattedCount, destination, null, out shouldAppend)
		{ }

		public TryWriteInterpolatedStringHandler(int literalLength, int formattedCount, Span<char> destination, IFormatProvider? provider, out bool shouldAppend)
		{
			this.Destination = destination;
			this.Provider = provider;
			this.Position = 0;
			// mirror the BCL: give up immediately if the literal characters alone cannot fit
			this.Success = shouldAppend = destination.Length >= literalLength;
		}

		internal readonly bool IsSuccessful => this.Success;

		internal readonly int Count => this.Position;

		public bool AppendLiteral(string value) => AppendFormatted(value.AsSpan());

		public bool AppendFormatted(scoped ReadOnlySpan<char> value)
		{
			if (value.TryCopyTo(this.Destination.Slice(this.Position)))
			{
				this.Position += value.Length;
				return true;
			}
			this.Success = false;
			return false;
		}

		public bool AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
			=> AppendWithAlignment(value, alignment);

		public bool AppendFormatted(string? value)
			=> AppendFormatted(value.AsSpan());

		public bool AppendFormatted(string? value, int alignment = 0, string? format = null)
			=> AppendWithAlignment(value.AsSpan(), alignment);

		public bool AppendFormatted(object? value, int alignment = 0, string? format = null)
			=> AppendFormatted<object?>(value, alignment, format);

		public bool AppendFormatted<T>(T value) => AppendFormatted(value, 0, null);

		public bool AppendFormatted<T>(T value, string? format) => AppendFormatted(value, 0, format);

		public bool AppendFormatted<T>(T value, int alignment) => AppendFormatted(value, alignment, null);

		public bool AppendFormatted<T>(T value, int alignment, string? format)
		{
			// note: the type test boxes value types, like the DefaultInterpolatedStringHandler backport does
			if (value is ISpanFormattable spanFormattable)
			{
				// mirror the modern handler's ISpanFormattable fast path. This is not just an optimization: some types
				// implement ToString(format, provider) WITH an interpolated string, so dispatching through
				// IFormattable.ToString here would recurse until the stack overflows.
				if (alignment == 0)
				{
					if (spanFormattable.TryFormat(this.Destination.Slice(this.Position), out int written, format.AsSpan(), this.Provider))
					{
						this.Position += written;
						return true;
					}
					this.Success = false;
					return false;
				}
				return AppendSpanFormattableWithAlignment(spanFormattable, alignment, format);
			}

			string? text;
			if (string.IsNullOrEmpty(format) && value is double d)
			{
				// the netfx default double formatting is lossy (G15): use the round-trippable form,
				// matching the shortest-roundtrip output of the modern handler
				text = d.ToString("R", this.Provider);
			}
			else if (string.IsNullOrEmpty(format) && value is float f)
			{
				text = f.ToString("R", this.Provider);
			}
			else
			{
				text = value is IFormattable formattable
					? formattable.ToString(format, this.Provider)
					: value?.ToString();
			}
			return AppendWithAlignment(text.AsSpan(), alignment);
		}

		private bool AppendSpanFormattableWithAlignment(ISpanFormattable value, int alignment, string? format)
		{
			// alignment requires knowing the formatted length before writing: format to a temporary buffer first
			// (this rents/allocates, which is acceptable: alignment is virtually never used on this code path)
			char[]? rented = null;
			Span<char> buffer = stackalloc char[128];
			int written;
			while (!value.TryFormat(buffer, out written, format.AsSpan(), this.Provider))
			{
				// TryFormat only returns false when the destination is too small: grow and retry
				int newSize = buffer.Length * 2;
				if (rented != null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
				rented = System.Buffers.ArrayPool<char>.Shared.Rent(newSize);
				buffer = rented;
			}
			bool res = AppendWithAlignment(buffer.Slice(0, written), alignment);
			if (rented != null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
			return res;
		}

		private bool AppendWithAlignment(scoped ReadOnlySpan<char> text, int alignment)
		{
			if (alignment == 0)
			{
				return AppendFormatted(text);
			}

			int padding = Math.Abs(alignment) - text.Length;
			if (padding <= 0)
			{
				return AppendFormatted(text);
			}

			var target = this.Destination.Slice(this.Position);
			if (target.Length < text.Length + padding)
			{
				this.Success = false;
				return false;
			}

			if (alignment < 0)
			{ // left-justified
				text.CopyTo(target);
				target.Slice(text.Length, padding).Fill(' ');
			}
			else
			{ // right-justified
				target.Slice(0, padding).Fill(' ');
				text.CopyTo(target.Slice(padding));
			}
			this.Position += text.Length + padding;
			return true;
		}

	}

	/// <summary>Minimal <c>netstandard2.0</c> backport of the interpolated-string overloads of <c>MemoryExtensions.TryWrite</c>.</summary>
	public static class TryWriteCompatExtensions
	{

		/// <summary>Writes the specified interpolated string to the character span.</summary>
		public static bool TryWrite(this Span<char> destination, [InterpolatedStringHandlerArgument(nameof(destination))] ref TryWriteInterpolatedStringHandler handler, out int charsWritten)
		{
			if (handler.IsSuccessful)
			{
				charsWritten = handler.Count;
				return true;
			}
			charsWritten = 0;
			return false;
		}

		/// <summary>Writes the specified interpolated string to the character span, using the specified format provider.</summary>
		public static bool TryWrite(this Span<char> destination, IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(destination), nameof(provider))] ref TryWriteInterpolatedStringHandler handler, out int charsWritten)
			=> TryWrite(destination, ref handler, out charsWritten);

	}

}

#endif
