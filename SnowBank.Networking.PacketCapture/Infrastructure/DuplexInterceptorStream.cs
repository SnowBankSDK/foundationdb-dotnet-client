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

namespace SnowBank.Networking.PacketCapture
{
	using System.Diagnostics.CodeAnalysis;
	using Microsoft.IO;

	/// <summary>Stream that creates an in-memory copy of everything that was read (for input streams), or written (for output streams)</summary>
	internal sealed class DuplexInterceptorStream : Stream
	{
		public Stream Input { get; }

		public MemoryStream InputBuffer { get; }

		public Stream Output { get; }

		public MemoryStream OutputBuffer { get; }

		public DuplexInterceptorStream(Stream input, Stream output, RecyclableMemoryStreamManager pool)
		{
			Contract.Debug.Requires(input != null && output != null && pool != null);
			this.Input = input;
			this.Output = output;
			this.InputBuffer = pool.GetStream();
			this.OutputBuffer = pool.GetStream();
		}

		public bool TryGetInputBuffer([MaybeNullWhen(false)] out MemoryStream buffer)
		{
			if (this.InputBuffer.Length != 0)
			{
				buffer = this.InputBuffer;
				return true;
			}
			buffer = null;
			return false;
		}

		public bool TryGetOutputBuffer([MaybeNullWhen(false)] out MemoryStream buffer)
		{
			if (this.OutputBuffer.Length != 0)
			{
				buffer = this.OutputBuffer;
				return true;
			}
			buffer = null;
			return false;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				//note: on doit maintenir en vie les MemoryStream de capture, car ils sont utilisés après nous!
			}
		}

		public override ValueTask DisposeAsync()
		{
			Dispose(true);
			return default;
		}

		public override bool CanSeek => false;

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		#region Input...

		public override bool CanRead => this.Input.CanRead;

		public override int Read(byte[] buffer, int offset, int count)
		{
			int n = this.Input.Read(buffer, offset, count);
			if (n > 0) this.InputBuffer.Write(buffer, offset, n);
			return n;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			int n = await this.Input.ReadAsync(buffer, offset, count, cancellationToken);
			if (n > 0) this.InputBuffer.Write(buffer, offset, n);
			return n;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			int n = await this.Input.ReadAsync(buffer, cancellationToken);
			if (n > 0) this.InputBuffer.Write(buffer.Span[..n]);
			return n;
		}

		#endregion

		#region Output...

		public override bool CanWrite => this.Output.CanWrite;

		public override void Flush()
		{
			this.Output.Flush();
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			return this.Output.FlushAsync(cancellationToken);
		}

		public override void WriteByte(byte value)
		{
			this.OutputBuffer.WriteByte(value);
			this.Output.WriteByte(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			this.OutputBuffer.Write(buffer, offset, count);
			this.Output.Write(buffer, offset, count);
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			this.OutputBuffer.Write(buffer);
			this.Output.Write(buffer);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			this.OutputBuffer.Write(buffer, offset, count);
			return this.Output.WriteAsync(buffer, offset, count, cancellationToken);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			this.OutputBuffer.Write(buffer.Span);
			return this.Output.WriteAsync(buffer, cancellationToken);
		}

		#endregion

	}

	#region OutputStream

	// La raison de cette classe c'est de ne pas avoir de Stream mais un OutputStream dans PacketCaptureConnectionContext
	// Bien montrer le sens de transport des bytes
	internal abstract class OutputInterceptorStream : Stream { }
	
	/// <summary>Stream that creates an in-memory copy of everything that was read from an input stream and written to an output stream</summary>
	internal sealed class StandardOutputInterceptorStream : OutputInterceptorStream
	{
		public Stream Output { get; }

		private MemoryStream? Mirror { get; set; }

		public StandardOutputInterceptorStream(Stream output, MemoryStream mirror)
		{
			Contract.Debug.Requires(output != null && mirror != null);
			this.Output = output;
			this.Mirror = mirror;
		}

		protected override void Dispose(bool disposing)
		{
			this.Mirror = null;
			this.Output.Dispose();
		}

		public override ValueTask DisposeAsync()
		{
			Dispose(false);
			return default;
		}

		public override bool CanSeek => false;

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		#region Input...

		public override bool CanRead => false;

		private static Exception CannotReadFromStream() => ThrowHelper.InvalidOperationException($"Cannot read from {nameof(OutputInterceptorStream)}.");

		public override int Read(byte[] buffer, int offset, int count) => throw CannotReadFromStream();

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw CannotReadFromStream();

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw CannotReadFromStream();

		#endregion

		#region Output...

		public override bool CanWrite => this.Output.CanWrite;

		private MemoryStream EnsureCanWrite()
		{
			return this.Mirror ?? throw new ObjectDisposedException(this.GetType().Name);
		}

		public override void Flush()
		{
			this.Output.Flush();
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			return this.Output.FlushAsync(cancellationToken);
		}

		public override void WriteByte(byte value)
		{
			var mirror = EnsureCanWrite();
			mirror.WriteByte(value);
			this.Output.WriteByte(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer, offset, count);
			this.Output.Write(buffer, offset, count);
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer);
			this.Output.Write(buffer);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer, offset, count);
			return this.Output.WriteAsync(buffer, offset, count, cancellationToken);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer.Span);
			return this.Output.WriteAsync(buffer, cancellationToken);
		}

		#endregion

	}

	/// <summary>
	/// Stream that creates an in-memory copy of everything that was read from an input stream and written to an output stream
	/// Works specifically for gRPC : Reads the 5 first bytes and get the length of the following gRPC message
	/// Then, when we have copied it internally, invoke a Task that will then create the captured packet
	/// </summary>
	internal sealed class GrpcOutputInterceptorStream : OutputInterceptorStream
	{
		public Stream Output { get; }

		private MemoryStream? Mirror { get; set; }

		public Func<Task>? OnMessageWritten { get; set; }
		
		private HttpContext Context { get; set; }

		public GrpcOutputInterceptorStream(Stream output, MemoryStream mirror, HttpContext ctx)
		{
			Contract.Debug.Requires(output != null && mirror != null);
			this.Output = output;
			this.Mirror = mirror;
			this.Context = ctx;
		}

		protected override void Dispose(bool disposing)
		{
			this.Mirror = null;
			this.Output.Dispose();
		}

		public override ValueTask DisposeAsync()
		{
			Dispose(false);
			return default;
		}

		public override bool CanSeek => false;

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		#region Input...

		public override bool CanRead => false;

		private static Exception CannotReadFromStream() => ThrowHelper.InvalidOperationException($"Cannot read from {nameof(OutputInterceptorStream)}.");

		public override int Read(byte[] buffer, int offset, int count) => throw CannotReadFromStream();

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw CannotReadFromStream();

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw CannotReadFromStream();

		#endregion

		#region Output...

		public override bool CanWrite => this.Output.CanWrite;

		private MemoryStream EnsureCanWrite()
		{
			return this.Mirror ?? throw new ObjectDisposedException(this.GetType().Name);
		}

		private void OnFlush()
		{
			if (this.Mirror?.Length > 0)
			{
				// On emit le packet
				this.OnMessageWritten?.Invoke();
				// On clear le mirror, pour "vider" la memoire et faire la place pour le prochain packet
				this.Mirror?.SetLength(0);
			}
		}

		public override void Flush()
		{
			OnFlush();
			this.Output.Flush();
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			OnFlush();
			return this.Output.FlushAsync(cancellationToken);
		}

		public override void WriteByte(byte value)
		{
			var mirror = EnsureCanWrite();
			mirror.WriteByte(value);
			this.Output.WriteByte(value);
		}
		
		public override void Write(byte[] buffer, int offset, int count)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer, offset, count);
			this.Output.Write(buffer, offset, count);
		}

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer);
			this.Output.Write(buffer);
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer, offset, count);
			return this.Output.WriteAsync(buffer, offset, count, cancellationToken);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			var mirror = EnsureCanWrite();
			mirror.Write(buffer.Span);
			return this.Output.WriteAsync(buffer, cancellationToken);
		}
		
		#endregion

	}


	#endregion

	#region InputStream

	/// <summary>Stream that creates an in-memory copy of everything that was read from an input stream and written to an output stream</summary>
	internal sealed class InputInterceptorStream : Stream
	{
		public Stream Input { get; }

		private MemoryStream? Mirror { get; set; }

		public InputInterceptorStream(Stream input, MemoryStream mirror)
		{
			Contract.Debug.Requires(input != null && mirror != null);
			this.Input = input;
			this.Mirror = mirror;
		}

		protected override void Dispose(bool disposing)
		{
			this.Mirror = null;
			this.Input.Dispose();
		}

		public override ValueTask DisposeAsync()
		{
			Dispose(false);
			return default;
		}

		public override bool CanSeek => false;

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		public override void SetLength(long value) => throw new NotSupportedException();

		public override long Length => throw new NotSupportedException();

		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		#region Input...

		public override bool CanRead => this.Input.CanRead;

		private MemoryStream EnsureCanRead()
		{
			return this.Mirror ?? throw new ObjectDisposedException(this.GetType().Name);
		}

		public override int ReadByte()
		{
			var mirror = EnsureCanRead();
			int res = base.ReadByte();
			if (res >= 0) mirror.WriteByte((byte) res);
			return res;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var mirror = EnsureCanRead();
			int n = this.Input.Read(buffer, offset, count);
			if (n > 0) mirror.Write(buffer, offset, n);
			return n;
		}

		public override int Read(Span<byte> buffer)
		{
			var mirror = EnsureCanRead();
			int n = this.Input.Read(buffer);
			if (n > 0) mirror.Write(buffer.Slice(0, n));
			return n;
		}

		public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			var mirror = EnsureCanRead();
			int n = await this.Input.ReadAsync(buffer, offset, count, cancellationToken);
			if (n > 0) mirror.Write(buffer, offset, n);
			return n;
		}

		public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			var mirror = EnsureCanRead();
			int n = await this.Input.ReadAsync(buffer, cancellationToken);
			if (n > 0) mirror.Write(buffer.Span[..n]);
			return n;
		}

		#endregion

		#region Output...

		public override bool CanWrite => false;

		private static Exception CannotWriteToStream() => ThrowHelper.InvalidOperationException($"Cannot write to {nameof(InputInterceptorStream)}.");

		public override void Flush() => throw CannotWriteToStream();

		public override Task FlushAsync(CancellationToken cancellationToken) => throw CannotWriteToStream();

		public override void WriteByte(byte value) => throw CannotWriteToStream();

		public override void Write(byte[] buffer, int offset, int count) => throw CannotWriteToStream();

		public override void Write(ReadOnlySpan<byte> buffer) => throw CannotWriteToStream();

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw CannotWriteToStream();

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw CannotWriteToStream();

		#endregion

	}

	#endregion
}
