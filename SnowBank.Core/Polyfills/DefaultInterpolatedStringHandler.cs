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

namespace System.Runtime.CompilerServices
{
	using System.Globalization;
	using System.Text;

	/// <summary>Minimal <c>netstandard2.0</c> backport of the BCL <c>DefaultInterpolatedStringHandler</c>.</summary>
	/// <remarks>
	/// <para>Backed by a <see cref="StringBuilder"/> instead of a pooled <c>char[]</c>: this trades the zero-allocation
	/// fast-path of the modern runtime for a simple, correct implementation. This is acceptable for the netstandard2.0 build,
	/// whose callers (e.g. <c>ThrowHelper</c>) only build exception messages on the (cold) failure path.</para>
	/// <para>This type only exists in the netstandard2.0 build; on modern targets the real BCL type is used.</para>
	/// </remarks>
	[InterpolatedStringHandler]
	public ref struct DefaultInterpolatedStringHandler
	{

		private readonly StringBuilder Builder;

		private readonly IFormatProvider? Provider;

		public DefaultInterpolatedStringHandler(int literalLength, int formattedCount)
			: this(literalLength, formattedCount, null)
		{ }

		public DefaultInterpolatedStringHandler(int literalLength, int formattedCount, IFormatProvider? provider)
		{
			// mirror the BCL's initial capacity heuristic (literal chars + ~11 chars per hole)
			this.Builder = new StringBuilder(literalLength + (formattedCount * 11));
			this.Provider = provider;
		}

		public void AppendLiteral(string value) => this.Builder.Append(value);

		public void AppendFormatted<T>(T value) => this.AppendFormatted(value, 0, null);

		public void AppendFormatted<T>(T value, string? format) => this.AppendFormatted(value, 0, format);

		public void AppendFormatted<T>(T value, int alignment) => this.AppendFormatted(value, alignment, null);

		public void AppendFormatted<T>(T value, int alignment, string? format)
		{
			string? text;
			if (value is ISpanFormattable spanFormattable)
			{
				// mirror the modern handler's ISpanFormattable fast path. This is not just an optimization: some types
				// (e.g. JsonDateTime) implement ToString(format) WITH an invariant interpolated string, so dispatching
				// through IFormattable.ToString here would recurse until the stack overflows.
				char[]? rented = null;
				Span<char> buffer = stackalloc char[128];
				int written;
				while (!spanFormattable.TryFormat(buffer, out written, format.AsSpan(), this.Provider))
				{
					// TryFormat only returns false when the destination is too small: grow and retry
					int newSize = buffer.Length * 2;
					if (rented != null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
					rented = System.Buffers.ArrayPool<char>.Shared.Rent(newSize);
					buffer = rented;
				}
				text = buffer.Slice(0, written).ToString();
				if (rented != null) System.Buffers.ArrayPool<char>.Shared.Return(rented);
			}
			else if (string.IsNullOrEmpty(format) && value is double d)
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
			this.AppendWithAlignment(text, alignment);
		}

		public void AppendFormatted(scoped ReadOnlySpan<char> value)
			=> this.Builder.Append(value.ToString());

		public void AppendFormatted(scoped ReadOnlySpan<char> value, int alignment = 0, string? format = null)
			=> this.AppendWithAlignment(value.ToString(), alignment);

		public void AppendFormatted(string? value) => this.Builder.Append(value);

		public void AppendFormatted(string? value, int alignment = 0, string? format = null)
			=> this.AppendWithAlignment(value, alignment);

		public void AppendFormatted(object? value, int alignment = 0, string? format = null)
		{
			string? text = value is IFormattable formattable
				? formattable.ToString(format, this.Provider)
				: value?.ToString();
			this.AppendWithAlignment(text, alignment);
		}

		private void AppendWithAlignment(string? text, int alignment)
		{
			text ??= string.Empty;
			if (alignment == 0)
			{
				this.Builder.Append(text);
				return;
			}

			int padding = Math.Abs(alignment) - text.Length;
			if (padding <= 0)
			{
				this.Builder.Append(text);
				return;
			}

			if (alignment < 0)
			{ // left-justified
				this.Builder.Append(text);
				this.Builder.Append(' ', padding);
			}
			else
			{ // right-justified
				this.Builder.Append(' ', padding);
				this.Builder.Append(text);
			}
		}

		public string ToStringAndClear()
		{
			string result = this.Builder.ToString();
			this.Builder.Clear();
			return result;
		}

		public override string ToString() => this.Builder.ToString();

	}

}

#endif
