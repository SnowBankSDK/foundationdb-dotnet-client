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

namespace SnowBank.Buffers.Text
{
	using System.Buffers;
	using System.Globalization;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Runtime.Converters;

	/// <summary>Span-based implementation of a <see cref="StringBuilder"/>, with reduced safety checks</summary>
	/// <remarks>All formatting uses the invariant culture.</remarks>
	[PublicAPI]
	public ref struct FastStringBuilder : ISpanBufferWriter<char>, IDisposable
	{

		/// <summary>Buffer that must be returned to the shared array pool</summary>
		private char[]? PooledBuffer;

		/// <summary>Current allocated buffer</summary>
		private Span<char> Chars;

		/// <summary>Current position in the buffer</summary>
		private int Position;

		/// <summary>Constructs a builder using a pre-allocated buffer</summary>
		/// <param name="initialBuffer">Pre-allocated buffer (usually on the stack)</param>
		public FastStringBuilder(Span<char> initialBuffer)
		{
			this.PooledBuffer = null;
			this.Chars = initialBuffer;
			this.Position = 0;
		}

		/// <summary>Constructs a builder that will rent a buffer with the specified initial capacity</summary>
		/// <param name="initialCapacity">Initial minimum capacity for the buffer</param>
		public FastStringBuilder(int initialCapacity)
		{
			this.PooledBuffer = ArrayPool<char>.Shared.Rent(initialCapacity);
			this.Chars = new Span<char>(this.PooledBuffer);
			this.Position = 0;
		}

		/// <summary>Gets or sets the length of the current <see cref="T:System.Text.StringBuilder" /> object.</summary>
		/// <returns>The length of this instance.</returns>
		public int Length
		{
			get => this.Position;
			set
			{
				Contract.Debug.Assert((uint) value <= this.Chars.Length);
				this.Position = value;
			}
		}

		/// <summary>Gets the maximum number of characters that can be contained in the memory allocated by the current instance.</summary>
		public int Capacity => this.Chars.Length;

		/// <summary>Gets a pinnable reference to the builder.</summary>
		/// <remarks>
		/// <para>Does not ensure there is a null char after <see cref="Length"/>.</para>
		/// <para>This overload is pattern matched in the C# 7.3+ compiler so you can omit the explicit method call, and write e.g. <c>fixed (char* c = builder)</c></para>
		/// </remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureCapacity(int capacity)
		{
			Contract.Debug.Requires(capacity >= 0);
			if ((uint) capacity > (uint) this.Chars.Length)
			{
				Grow(capacity - Position);
			}
		}

		/// <summary>Gets a pinnable reference to the first character in the buffer.</summary>
		public ref char GetPinnableReference() => ref MemoryMarshal.GetReference(this.Chars);

		/// <summary>Gets a pinnable reference to the first character in the buffer.</summary>
		/// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
		public ref char GetPinnableReference(bool terminate)
		{
			if (terminate)
			{
				EnsureCapacity(this.Length + 1);
				this.Chars[this.Length] = '\0';
			}
			return ref MemoryMarshal.GetReference(this.Chars);
		}


		/// <summary>Returns a reference to the character at the specified index</summary>
		/// <param name="index">Index of the character</param>
		/// <returns>Reference to this character</returns>
		/// <exception cref="IndexOutOfRangeException"> when <paramref name="index"/> is outside the bounds of the currently allocated buffer</exception>
		public ref char this[int index]
		{
			get
			{
				Contract.Debug.Assert(index < this.Position);
				return ref this.Chars[index];
			}
		}

		/// <summary>Returns a string with all the characters that have been written to this builder</summary>
		/// <remarks>The buffer is reset after this call, meaning that any rented buffer will be returned to the pool.</remarks>
		public override string ToString()
		{
			string s = this.Chars[..this.Position].ToString();
			Dispose();
			return s;
		}

		/// <summary>Returns the underlying storage of the builder.</summary>
		public Span<char> RawChars => this.Chars;

		/// <summary>Returns a span around the contents of the builder.</summary>
		/// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
		public ReadOnlySpan<char> AsSpan(bool terminate)
		{
			if (terminate)
			{
				EnsureCapacity(this.Length + 1);
				this.Chars[this.Length] = '\0';
			}
			return this.Chars[..this.Position];
		}

		/// <summary>Returns a span around the contents of the builder.</summary>
		public ReadOnlySpan<char> AsSpan() => this.Chars[..this.Position];

		/// <summary>Returns a span around the contents of the builder, starting at the specified offset.</summary>
		public ReadOnlySpan<char> AsSpan(int start) => this.Chars.Slice(start, this.Position - start);

		/// <summary>Returns a span around parts of the contents of the builder.</summary>
		public ReadOnlySpan<char> AsSpan(int start, int length) => this.Chars.Slice(start, length);

		/// <summary>Copies the content of this builder, if the destination is large enough</summary>
		public bool TryCopyTo(Span<char> destination, out int charsWritten)
		{
			if (this.Chars[..this.Position].TryCopyTo(destination))
			{
				charsWritten = this.Position;
				Dispose();
				return true;
			}
			else
			{
				charsWritten = 0;
				Dispose();
				return false;
			}
		}

		/// <summary>Inserts a character at the specified location</summary>
		public void Insert(int index, char value, int count)
		{
			if (this.Position > this.Chars.Length - count)
			{
				Grow(count);
			}

			int remaining = this.Position - index;
			this.Chars.Slice(index, remaining).CopyTo(this.Chars[(index + count)..]);
			this.Chars.Slice(index, count).Fill(value);
			this.Position += count;
		}

		/// <summary>Inserts a string at the specified location</summary>
		public void Insert(int index, string? s)
		{
			if (s != null)
			{
				Insert(index, s.AsSpan());
			}
		}

		/// <summary>Inserts a string at the specified location</summary>
		public void Insert(int index, ReadOnlySpan<char> s)
		{
			int count = s.Length;
			if (count == 0) return;

			if (this.Position > (this.Chars.Length - count))
			{
				Grow(count);
			}

			int remaining = this.Position - index;
			this.Chars.Slice(index, remaining).CopyTo(this.Chars[(index + count)..]);
			s.CopyTo(this.Chars[index..]);
			this.Position += count;
		}

		/// <summary>Appends a character at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(char c)
		{
			int pos = this.Position;
			var chars = this.Chars;
			if ((uint) pos < (uint) chars.Length)
			{
				chars[pos] = c;
				this.Position = pos + 1;
			}
			else
			{
				GrowAndAppend(c);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void GrowAndAppend(char c)
		{
			Grow(1);
			Append(c);
		}

		/// <summary>Appends a string at the end of the buffer</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Append(string? s)
		{
			if (s != null)
			{
				Append(s.AsSpan());
			}
		}

		private void AppendSlow(ReadOnlySpan<char> s)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - s.Length)
			{
				Grow(s.Length);
			}

			s.CopyTo(this.Chars[pos..]);
			this.Position += s.Length;
		}

		/// <summary>Appends an interpolated string at the end of the buffer</summary>
		public void Append(ref InvariantInterpolatedStringHandler handler)
		{
#if NET10_0_OR_GREATER
			Append(handler.Text);
			handler.Clear();
#else
			Append(handler.ToStringAndClear());
#endif
		}

		/// <summary>Appends an interpolated string at the end of the buffer</summary>
		public void Append(IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(provider))] ref DefaultInterpolatedStringHandler handler)
		{
#if NET10_0_OR_GREATER
			Append(handler.Text);
			handler.Clear();
#else
			Append(handler.ToStringAndClear());
#endif
		}

		/// <summary>Appends an interpolated string, followed by a line return, at the end of the buffer</summary>
		/// <remarks>This method uses CRLF (<c>\r\n</c>) as the new line, independent of the current operating system.</remarks>
		public void AppendLine(ref InvariantInterpolatedStringHandler handler)
		{
#if NET10_0_OR_GREATER
			AppendLine(handler.Text);
			handler.Clear();
#else
			AppendLine(handler.ToStringAndClear());
#endif
		}

		/// <summary>Appends an interpolated string, followed by a line return, at the end of the buffer</summary>
		/// <remarks>This method uses CRLF (<c>\r\n</c>) as the new line, independent of the current operating system.</remarks>
		public void AppendLine(IFormatProvider? provider, [InterpolatedStringHandlerArgument(nameof(provider))] ref DefaultInterpolatedStringHandler handler)
		{
#if NET10_0_OR_GREATER
			AppendLine(handler.Text);
			handler.Clear();
#else
			AppendLine(handler.ToStringAndClear());
#endif
		}

		/// <summary>Appends a value that implements <see cref="IFormattable"/> or <see cref="ISpanFormattable"/> at the end of the buffer</summary>
		public void AppendFormatted<TFormattable>(TFormattable value)
		{
			if (value is null) return;

			if (typeof(TFormattable).IsAssignableFrom(typeof(IFormattable)))
			{
				if (typeof(TFormattable).IsAssignableTo(typeof(ISpanFormattable)))
				{
					int charsWritten;
					while (!((ISpanFormattable) value).TryFormat(this.Chars[this.Position..], out charsWritten, default, null))
					{
						Grow();
					}

					Advance(charsWritten);
					return;
				}

				Append(((IFormattable) value).ToString(null, null));
				return;
			}

			Append(value.ToString());
		}

		/// <summary>Appends a value that implements <see cref="IFormattable"/> or <see cref="ISpanFormattable"/> at the end of the buffer</summary>
		public void AppendFormatted<TFormattable>(TFormattable value, string? format, IFormatProvider? provider = null)
		{
			if (value is null) return;

			if (typeof(TFormattable).IsAssignableFrom(typeof(IFormattable)))
			{
				if (typeof(TFormattable).IsAssignableTo(typeof(ISpanFormattable)))
				{
					int charsWritten;
					while (!((ISpanFormattable) value).TryFormat(this.Chars[this.Position..], out charsWritten, format, provider))
					{
						Grow();
					}

					Advance(charsWritten);
					return;
				}

				Append(((IFormattable) value).ToString(format, provider));
				return;
			}

			Append(value.ToString());
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Grow()
		{
			const uint ARRAY_MAX_LENGTH = 0x7FFFFFC7; // same as Array.MaxLength

			// the buffer is not large enough, we need to resize it
			uint newCapacity = Math.Max((uint) this.Chars.Length + 1, Math.Min((uint) this.Chars.Length * 2, ARRAY_MAX_LENGTH));
#if NET5_0_OR_GREATER
			int arraySize = (int) Math.Clamp(newCapacity, 256, int.MaxValue);
#else
			// Math.Clamp is not on netstandard2.0
			int arraySize = (int) Math.Min(Math.Max(newCapacity, 256), int.MaxValue);
#endif
			if (arraySize <= this.Chars.Length)
			{
				throw new InvalidOperationException("Could not format value because it would exceed the maximum allowed buffer size.");
			}

			char[] poolArray = ArrayPool<char>.Shared.Rent(arraySize);
			this.Chars[..this.Position].CopyTo(poolArray);

			char[]? toReturn = this.PooledBuffer;
			this.Chars = this.PooledBuffer = poolArray;
			if (toReturn is not null)
			{
				ArrayPool<char>.Shared.Return(toReturn);
			}
		}

#if NET9_0_OR_GREATER

		/// <summary>Appends a formatting string at the end of the buffer</summary>
		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params ReadOnlySpan<object?> args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#elif NET8_0_OR_GREATER

		/// <summary>Appends a formatting string at the end of the buffer</summary>
		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		/// <summary>Appends a formatting string at the end of the buffer</summary>
		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		/// <summary>Appends a formatting string at the end of the buffer</summary>
		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, object? arg0, object? arg1, object? arg2)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
		}

		/// <summary>Appends a formatting string at the end of the buffer</summary>
		[StringFormatMethod(nameof(format))]
		public void AppendFormat([StringSyntax(StringSyntaxAttribute.CompositeFormat)] string format, params object?[] args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#else

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0, object? arg1)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, object? arg0, object? arg1, object? arg2)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, arg0, arg1, arg2));
		}

		[StringFormatMethod(nameof(format))]
		public void AppendFormat(string format, params object?[] args)
		{
			AppendSlow(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#endif

		/// <summary>Appends a repeated character at the end of the buffer</summary>
		public void Append(char c, int count)
		{
			if (this.Position > this.Chars.Length - count)
			{
				Grow(count);
			}

			var dst = this.Chars.Slice(this.Position, count);
			for (int i = 0; i < dst.Length; i++)
			{
				dst[i] = c;
			}
			this.Position += count;
		}

		/// <summary>Appends the contents of an unmanaged string at the end of the buffer</summary>
		public unsafe void Append(char* value, int length)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - length)
			{
				Grow(length);
			}

			var dst = this.Chars.Slice(this.Position, length);
			for (int i = 0; i < dst.Length; i++)
			{
				dst[i] = *value++;
			}
			this.Position += length;
		}

		/// <summary>Appends a span of characters at the end of the buffer</summary>
		public void Append(scoped ReadOnlySpan<char> value)
		{
			int pos = this.Position;
			if (pos > this.Chars.Length - value.Length)
			{
				Grow(value.Length);
			}

			value.CopyTo(this.Chars[this.Position..]);
			this.Position += value.Length;
		}

		/// <summary>Allocates a new chunk of characters at the end of the buffer</summary>
		/// <param name="length">Number of characters to allocate</param>
		/// <returns>Span that points to the newly allocated chunk</returns>
		/// <remarks>This method may resize the buffer, but will not advance the cursor. You must call <see cref="Advance"/> after filling the buffer, to effectively "commit" the new content.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Span<char> GetSpan(int length)
		{
			int origPos = this.Position;
			if (origPos > this.Chars.Length - length)
			{
				Grow(length);
			}

			return this.Chars.Slice(origPos, length);
		}

		/// <summary>Advances the cursor by the specified number of characters, after a call to <see cref="GetSpan"/></summary>
		/// <param name="count">Number of characters effectively written to the buffer</param>
		public void Advance(int count)
		{
			int origPos = this.Position;
			if (origPos > this.Chars.Length - count)
			{
				Grow(count);
			}
			this.Position = origPos + count;
		}

		/// <summary>Appends a boolean literal (<c>"true"</c> or <c>"false"</c>) to the end of the buffer</summary>
		public void Append(bool value)
		{
			AppendSlow(value ? "true" : "false");
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(int value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(uint value)
		{
			//note: uint.TryFormat will call Number.TryUInt32ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(long value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(ulong value)
		{
			//note: ulong.TryFormat will call Number.TryUInt64ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value));
		}

		/// <summary>Appends a base 10 floating point number to the end of the buffer</summary>
		public void Append(double value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "R", CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like Math.PI.ToString("R") use 17 characters, which would require up to 18 with a terminal zero
			AppendSpanFormattable(value, 18); // "3.141592653589793" + NUL
		}

		/// <summary>Appends a base 10 floating point number to the end of the buffer</summary>
		public void Append(float value)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "R", CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like ((float) Math.PI).ToString("R") use 9 characters, which would require up to 10 with a terminal zero, so we will use 12, and live with a potential double resize!
			AppendSpanFormattable(value, 10); // "3.1415927" + NUL
		}

		/// <summary>Appends a base 10 floating point number to the end of the buffer</summary>
		public void Append(decimal value)
		{
#if NET8_0_OR_GREATER
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "R", NumberFormatInfo.InvariantInfo))
#else
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, "G", NumberFormatInfo.InvariantInfo))
#endif
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, 32);
		}

#if NET6_0_OR_GREATER
		internal void AppendSpanFormattable<T>(T value, int sizeHint) where T : ISpanFormattable
		{
			if (this.Position > Chars.Length - sizeHint)
			{
				Grow(sizeHint);
			}

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, CultureInfo.InvariantCulture));
			}
		}
#else
		// the BCL primitives do not implement the polyfilled ISpanFormattable on this target:
		// => relax to IFormattable and format through a transient string (one allocation per append)
		internal void AppendSpanFormattable<T>(T value, int sizeHint) where T : IFormattable
		{
			Append(value.ToString(null, CultureInfo.InvariantCulture));
		}
#endif

		/// <summary>Appends a formattable value to the end of the buffer</summary>
		/// <typeparam name="T">Type of the value, which must implement <see cref="ISpanFormattable"/></typeparam>
		public void Append<T>(T value) where T : ISpanFormattable
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, default, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, CultureInfo.InvariantCulture));
			}
		}

		/// <summary>Appends a formattable value to the end of the buffer</summary>
		/// <typeparam name="T">Type of the value, which must implement <see cref="ISpanFormattable"/></typeparam>
		public void Append<T>(T value, string? format) where T : ISpanFormattable
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(format, CultureInfo.InvariantCulture));
			}
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(int value, string? format, IFormatProvider? provider = null)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(uint value, string? format, IFormatProvider? provider = null)
		{
			//note: uint.TryFormat will call Number.TryUInt32ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(long value, string? format, IFormatProvider? provider = null)
		{
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		/// <summary>Appends a base 10 integer to the end of the buffer</summary>
		public void Append(ulong value, string? format, IFormatProvider? provider = null)
		{
			//note: ulong.TryFormat will call Number.TryUInt64ToDecStr that does not use 'provider', when 'format' is null

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider ?? CultureInfo.InvariantCulture))
			{
				this.Position += charsWritten;
				return;
			}

			AppendSpanFormattable(value, StringConverters.CountDigits(value), format, provider);
		}

		/// <summary>Appends a base 10 decimal number to the end of the buffer</summary>
		public void Append(double value, string? format, IFormatProvider? provider = null)
		{
			format ??= "R";
			provider ??= CultureInfo.InvariantCulture;

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like Math.PI.ToString("R") use 17 characters, which would require up to 18 with a terminal zero
			AppendSpanFormattable(value, 18, format, provider); // "3.141592653589793" + NUL
		}

		/// <summary>Appends a base 10 decimal number to the end of the buffer</summary>
		public void Append(float value, string? format, IFormatProvider? provider = null)
		{
			format ??= "R";
			provider ??= CultureInfo.InvariantCulture;

			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
				return;
			}

			// To be safe, we would need close to 32 chars (value used in the runtime internal implementation), but this is somewhat overkill!
			// Values like ((float) Math.PI).ToString("R") use 9 characters, which would require up to 10 with a terminal zero, so we will use 12, and live with a potential double resize!
			AppendSpanFormattable(value, 10, format, provider); // "3.1415927" + NUL
		}

#if NET6_0_OR_GREATER
		internal void AppendSpanFormattable<T>(T value, int sizeHint, string? format, IFormatProvider? provider = null) where T : ISpanFormattable
		{
			if (this.Position > Chars.Length - sizeHint)
			{
				Grow(sizeHint);
			}

			provider ??= CultureInfo.InvariantCulture;
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, null, provider))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(null, provider));
			}
		}
#else
		// the BCL primitives do not implement the polyfilled ISpanFormattable on this target: relax to IFormattable and format through a transient string (one allocation per append)
		internal void AppendSpanFormattable<T>(T value, int sizeHint, string? format, IFormatProvider? provider = null) where T : IFormattable
		{
			Append(value.ToString(format, provider ?? CultureInfo.InvariantCulture));
		}
#endif

		/// <summary>Appends a formattable value to the end of the buffer</summary>
		/// <typeparam name="T">Type of the value, which must implement <see cref="ISpanFormattable"/></typeparam>
		public void Append<T>(T value, string? format, IFormatProvider? provider) where T : ISpanFormattable
		{
			provider ??= CultureInfo.InvariantCulture;
			if (value.TryFormat(this.Chars[this.Position..], out int charsWritten, format, provider))
			{
				this.Position += charsWritten;
			}
			else
			{
				Append(value.ToString(format, provider));
			}
		}

		/// <summary>Appends an empty line at the end of the buffer</summary>
		/// <remarks>This method uses CRLF (<c>\r\n</c>) as the new line, independent of the current operating system.</remarks>
		public void AppendLine()
		{
			AppendSlow("\r\n");
		}

		/// <summary>Appends a string, followed by a line return, at the end of the buffer</summary>
		/// <remarks>This method uses CRLF (<c>\r\n</c>) as the new line, independent of the current operating system.</remarks>
		public void AppendLine(string? s)
		{
			Append(s);
			AppendSlow("\r\n");
		}

		/// <summary>Appends a string, followed by a line return, at the end of the buffer</summary>
		/// <remarks>This method uses CRLF (<c>\r\n</c>) as the new line, independent of the current operating system.</remarks>
		public void AppendLine(ReadOnlySpan<char> s)
		{
			Append(s);
			AppendSlow("\r\n");
		}

		/// <summary>Appends a base 16 integer, using a fixed number of digits, at the end of the buffer</summary>
		/// <param name="value">Value to encode (in hexadecimal)</param>
		/// <param name="digits">Number of digits to use.</param>
		/// <param name="lowerCase">Set to <c>true</c> if you want to use lowercase digits.</param>
		/// <remarks>
		/// <para>If <paramref name="digits"/> is less than 8, then the number may only be partially written to the buffer! Ex: <c>AppendHex(0x1234, 2)</c> will only write "34" to the buffer.</para>
		/// </remarks>
		public void AppendHex(int value, int digits, bool lowerCase = false)
		{
			Contract.Positive(digits);
			Contract.Debug.Requires(digits <= 8);

			if (this.Position > this.Chars.Length - digits)
			{
				Grow(digits);
			}

			int pos = this.Position;
			ref char ptr = ref this.Chars[pos];
			int alphaBase = lowerCase ? 87 : 55;

			for (int i = digits - 1; i >= 0; i--)
			{
				int n = value & 0xF;
				Unsafe.Add(ref ptr, i) = (char) (n < 10 ? ('0' + n) : (alphaBase + n));
				value >>= 4;
			}

			this.Position = pos + digits;
		}

		/// <summary>Appends a base 16 integer, using a fixed number of digits, at the end of the buffer</summary>
		/// <param name="value">Value to encode (in hexadecimal)</param>
		/// <param name="digits">Number of digits to use.</param>
		/// <param name="lowerCase">Set to <c>true</c> if you want to use lowercase digits.</param>
		/// <remarks>
		/// <para>If <paramref name="digits"/> is less than 16, then the number may only be partially written to the buffer! Ex: <c>AppendHex(0x1234, 2)</c> will only write "34" to the buffer.</para>
		/// </remarks>
		public void AppendHex(long value, int digits, bool lowerCase = false)
		{
			Contract.Positive(digits);
			Contract.Debug.Requires(digits <= 16);

			if (this.Position > this.Chars.Length - digits)
			{
				Grow(digits);
			}

			int pos = this.Position;
			ref char ptr = ref this.Chars[pos];
			int alphaBase = lowerCase ? 87 : 55;

			for (int i = digits - 1; i >= 0; i--)
			{
				int n = (int) (value & 0xF);
				Unsafe.Add(ref ptr, i) = (char) (n < 10 ? ('0' + n) : (alphaBase + n));
				value >>= 4;
			}

			this.Position = pos + digits;
		}

		/// <summary>
		/// Resize the internal buffer either by doubling current buffer size or
		/// by adding <paramref name="additionalCapacityBeyondPos"/> to
		/// <see cref="Position"/> whichever is greater.
		/// </summary>
		/// <param name="additionalCapacityBeyondPos">
		/// Number of chars requested beyond current position.
		/// </param>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private void Grow(int additionalCapacityBeyondPos)
		{
			Contract.Debug.Assert(additionalCapacityBeyondPos > 0);
			Contract.Debug.Assert(this.Position > this.Chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

			const uint ARRAY_MAX_LENGTH = 0x7FFFFFC7; // same as Array.MaxLength

			// Increase to at least the required size (this.Position + additionalCapacityBeyondPos), but try
			// to double the size if possible, bounding the doubling to not go beyond the max array length.
			int newCapacity = (int) Math.Max(
				checked((uint) (this.Position + additionalCapacityBeyondPos)),
				Math.Min((uint) this.Chars.Length * 2, ARRAY_MAX_LENGTH)
			);

			// Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative.
			// This could also go negative if the actual required length wraps around.
			char[] poolArray = ArrayPool<char>.Shared.Rent(newCapacity);

			this.Chars[..this.Position].CopyTo(poolArray);

			char[]? toReturn = this.PooledBuffer;
			this.Chars = this.PooledBuffer = poolArray;
			if (toReturn != null)
			{
				ArrayPool<char>.Shared.Return(toReturn);
			}
		}

		/// <summary>Releases any rented buffer to the pool</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Dispose()
		{
			char[]? toReturn = this.PooledBuffer;
			this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
			if (toReturn != null)
			{
				ArrayPool<char>.Shared.Return(toReturn);
			}
		}

	}

}
