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

namespace SnowBank.IO
{
	using System.Globalization;
	using System.IO;
	using System.Runtime;
	using System.Text;
	using SnowBank.Runtime.Converters;

	/// <summary>"Fast" version of StringWriter, that performs fewer checks, but is a good fit for specific use cases (serialization, ...)</summary>
	/// <remarks>This type is "unsafe" and should only be used internally, and not exposed to the caller.</remarks>
	public sealed class FastStringWriter : TextWriter
	{
		// "bare metal" version of StringWriter that:
		// * Disable all checks if the stream is still open/closed (ie: can still write Dispose has been called!)
		// * Optimize the hot paths that are frequently called by a serializer (strings, numbers, ...) that already checks its own inputs
		// * Rely on the inner StringBuilder to do param checks
		// * All operations are InvariantCulture by default
		// * Attempt to better inline code

		public StringBuilder Buffer { get; }

		private static readonly UnicodeEncoding s_encoding = new(bigEndian: false, byteOrderMark: false);

		#region Constructors...

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public FastStringWriter()
			: this(new StringBuilder())
		{ }

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public FastStringWriter(int capacity)
			: this(new StringBuilder(capacity))
		{ }

		public FastStringWriter(StringBuilder buffer)
			 : base(CultureInfo.InvariantCulture)
		{
			Contract.NotNull(buffer);

			this.Buffer = buffer;
		}

		#endregion

		public void Reset()
		{
			this.Buffer.Clear();
		}

		/// <summary>Return the underlying buffer used by this writer</summary>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public StringBuilder GetStringBuilder()
		{
			return this.Buffer;
		}

		/// <summary>Return the current buffer as a string</summary>
		/// <returns>Text that has been written so far</returns>
		/// <remarks>Caution: please don't call this method if more text will be written later, because it will cause a lot of extra memory allocations!</remarks>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override string ToString()
		{
			return this.Buffer.ToString();
		}

		/// <summary>Return the current buffer as an array of char</summary>
		/// <returns>Text that has been written so far</returns>
		/// <remarks>Caution: please don't call this method if more text will be written later, because it will cause a lot of extra memory allocations!</remarks>
		public char[] ToCharArray()
		{
			var buffer = this.Buffer;
			char[] data = new char[buffer.Length];
			buffer.CopyTo(0, data, 0, buffer.Length);
			return data;
		}

		/// <summary>Return the content of the buffer, as bytes, using the specified encoding</summary>
		/// <param name="encoding">Encoding used to convert text into bytes (ex: Encoding.UTF8)</param>
		/// <returns>Content of the buffer, encoded into bytes</returns>
		public byte[] GetBytes(Encoding encoding)
		{
			return encoding.GetBytes(this.Buffer.ToString());
		}

		/// <summary>Copy the content of the buffer to another text writer</summary>
		/// <param name="output">Writer where to write the content of the buffer</param>
		/// <param name="buffer">Optional buffer used for the copy (if not <c>null</c>)</param>
		/// <remarks>Perform an "optimized" copy by preventing the string allocation from the inner StringBuilder</remarks>
		public void CopyTo(TextWriter output, char[]? buffer = null)
		{
			Contract.NotNull(output);

			if (this.Buffer.Length == 0) return;

			buffer ??= new char[Math.Min(this.Buffer.Length, 0x400)];
			if (buffer.Length == 0) throw new ArgumentException("Buffer cannot be empty", nameof(buffer));

			int remaining = this.Buffer.Length;
			int p = 0;
			while (remaining > 0)
			{
				int n = Math.Min(remaining, buffer.Length);
				this.Buffer.CopyTo(p, buffer, 0, remaining);
				output.Write(buffer, 0, n);
				p += n;
				remaining -= n;
			}
		}

		#region TextWriter Implementation...

		public override Encoding Encoding
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			get => s_encoding;
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Close()
		{
			// NOP
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char value)
		{
			this.Buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(string? value)
		{
			this.Buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char[]? value)
		{
			this.Buffer.Append(value);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(char[] buffer, int index, int count)
		{
			this.Buffer.Append(buffer, index, count);
		}

		public override void Write(int value)
		{
			this.Buffer.Append(StringConverters.ToString(value));
		}

		public override void Write(long value)
		{
			this.Buffer.Append(StringConverters.ToString(value));
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void Write(ReadOnlySpan<char> buffer)
		{
			this.Buffer.Append(buffer);
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void WriteLine()
		{
			this.Buffer.AppendLine();
		}

		/// <inheritdoc />
		public override void WriteLine(char value)
		{
			this.Buffer.Append(value).AppendLine();
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void WriteLine(string? value)
		{
			this.Buffer.AppendLine(value);
		}

		/// <inheritdoc />
		public override void WriteLine(char[] buffer, int index, int count)
		{
			this.Buffer.Append(buffer, index, count).AppendLine();
		}

		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		public override void WriteLine(ReadOnlySpan<char> buffer)
		{
			this.Buffer.Append(buffer).AppendLine();
		}

		#endregion

		#region Async Implementation...

		public override Task FlushAsync()
		{
			return Task.CompletedTask;
		}

		public override Task WriteAsync(string? value)
		{
			this.Buffer.Append(value);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(char value)
		{
			this.Buffer.Append(value);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(char[] value, int index, int count)
		{
			this.Buffer.Append(value, index, count);
			return Task.CompletedTask;
		}

		/// <inheritdoc />
		public override Task WriteAsync(StringBuilder? value, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);

			this.Buffer.Append(value);
			return Task.CompletedTask;
		}

		public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
			this.Buffer.Append(buffer.Span);
			return Task.CompletedTask;
		}

		#endregion

	}

}
