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
	using System.IO;

	/// <summary>Stream that exposes a writable subsection of another stream <see cref="Stream"/>.</summary>
	[PublicAPI]
	public sealed class PartialWritableStream : Stream
	{

		/// <summary>Start offset of the chunk (included)</summary>
		private long ChunkOffset { get; }

		/// <summary>End offset of the chunk (excluded)</summary>
		private long ChunkEnd { get; }

		/// <summary>Length of the chunk</summary>
		private long ChunkLength { get; }

		/// <summary>Inner stream</summary>
		private Stream? Inner { get; set; }

		/// <summary>If <c>true</c>, calling <see cref="Dispose"/> or <see cref="DisposeAsync"/> will also dispose the <see cref="Inner">inner stream</see>.</summary>
		private bool OwnsStream { get; }

		public PartialWritableStream(Stream fs, long offset, long length, bool ownsStream)
		{
			Contract.NotNull(fs);
			Contract.Positive(offset);
			Contract.Positive(length);

			if (!fs.CanSeek) throw new InvalidOperationException("Underlying stream must be seekable");
			fs.Seek(offset, SeekOrigin.Begin);

			this.Inner = fs;
			this.ChunkOffset = offset;
			this.ChunkLength = length;
			this.ChunkEnd = checked(offset + length);
			this.OwnsStream = ownsStream;
		}

		public override bool CanRead => false;

		public override bool CanSeek => this.Inner?.CanSeek ?? false;

		public override bool CanWrite => this.Inner?.CanWrite ?? false;

		public override bool CanTimeout => this.Inner?.CanTimeout ?? false;

		[MethodImpl(MethodImplOptions.NoInlining)]
		private ObjectDisposedException ErrorDisposed() => new(this.GetType().Name);

		public override int WriteTimeout
		{
			get => this.Inner?.WriteTimeout ?? throw ErrorDisposed();
			set
			{
				var inner = this.Inner;
				if (inner == null) throw ErrorDisposed();
				inner.WriteTimeout = value;
			}
		}

		public override long Length => this.ChunkLength;

		public override long Position
		{
			get
			{
				var inner = this.Inner ?? throw ErrorDisposed();
				return Math.Max(inner.Position - this.ChunkOffset, 0);
			}
			set => Seek(value, SeekOrigin.Begin);
		}

		/// <summary>Number of bytes reamining before the end of this stream</summary>
		public long? Remaining => this.Inner != null ? Math.Max(0, this.ChunkEnd - this.Inner.Position) : null;

		/// <summary>Mapped position in the inner stream (for debugging purpose only)</summary>
		public long? InnerPosition => this.Inner?.Position;

		public override long Seek(long offset, SeekOrigin origin)
		{
			var inner = this.Inner ?? throw ErrorDisposed();

			long relPos;

			switch (origin)
			{
				case SeekOrigin.Begin:
				{
					relPos = offset;
					break;
				}
				case SeekOrigin.Current:
				{
					relPos = checked(inner.Position - this.ChunkOffset + offset);
					break;
				}
				case SeekOrigin.End:
				{
					relPos = checked(this.ChunkLength - offset);
					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException(nameof(origin));
				}
			}

			if (relPos < 0)
			{ // cannot read before the start of the chunk
				throw new IOException("An attempt was made to move the file pointer before the beginning of the file.");
				//TODO: hresult?
			}
			//note: it _is_ possible to seek "after" the end of the chunk, but in this case any Read() should return 0

			// compute the absolute position, and seek the inner stream to it!
			var absPos = checked(relPos + this.ChunkOffset);

			var actualAbsPos = inner.Seek(absPos, SeekOrigin.Begin);
			var actualRelPos = checked(actualAbsPos - this.ChunkOffset);

			// note: if we end up "outside of bounds", we will deal with it when reading

			return actualRelPos;
		}

		private bool ComputeBound(int count, [MaybeNullWhen(false)] out Stream stream)
		{
			var inner = this.Inner ?? throw ErrorDisposed();

			// get the current relative position
			var relPos = checked(inner.Position - this.ChunkOffset);
			if (relPos < 0 || relPos >= this.ChunkLength)
			{ // outside the bounds of the chunk
				stream = null;
				return false;
			}

			if (count == 0)
			{
				stream = inner;
				return true;
			}

			// compute where the write would end
			var relEnd = checked(relPos + count);

			// test if we overrun the end of the chunk
			if (relEnd > this.ChunkLength)
			{
				// we will only attempt to write what remains before the end of the chunk
				stream = null;
				return false;
			}

			stream = inner;
			return true;
		}

		private static Exception ErrorCannotWritePastEndOfStream(int count)
		{
			return new InvalidOperationException("Cannot write past the end of the stream.");
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("Cannot set the length of a fixed segment stream.");
		}

		public override void WriteByte(byte value)
		{
			if (!ComputeBound(1, out var stream))
			{
				throw ErrorCannotWritePastEndOfStream(1);
			}
			stream.WriteByte(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (!ComputeBound(count, out var stream))
			{
				throw ErrorCannotWritePastEndOfStream(count);
			}
			stream.Write(buffer, offset, count);
		}

#if NET5_0_OR_GREATER

		public override void Write(ReadOnlySpan<byte> buffer)
		{
			if (!ComputeBound(buffer.Length, out var stream))
			{
				throw ErrorCannotWritePastEndOfStream(buffer.Length);
			}
			stream.Write(buffer);
		}

#endif

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
			if (!ComputeBound(count, out var stream))
			{
				return Task.FromException(ErrorCannotWritePastEndOfStream(count));
			}
			return stream.WriteAsync(buffer, offset, count, cancellationToken);
		}

#if NET5_0_OR_GREATER

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested) return ValueTask.FromCanceled(cancellationToken);
			if (!ComputeBound(buffer.Length, out var stream))
			{
				return ValueTask.FromException(ErrorCannotWritePastEndOfStream(buffer.Length));
			}
			return stream.WriteAsync(buffer, cancellationToken);
		}

#endif

		public override void Flush()
		{
			var inner = this.Inner ?? throw ErrorDisposed();
			inner.Flush();
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			var inner = this.Inner ?? throw ErrorDisposed();
			return inner.FlushAsync(cancellationToken);
		}

		protected override void Dispose(bool disposing)
		{
			var inner = this.Inner;
			this.Inner = null;

			if (this.OwnsStream && inner != null)
			{
				inner.Dispose();
			}
		}

#if NET5_0_OR_GREATER

		public override ValueTask DisposeAsync()
		{
			var inner = this.Inner;
			this.Inner = null;

			return this.OwnsStream && inner != null ? inner.DisposeAsync() : default;
		}

#endif

		#region Unsupported Methods...

		public override int ReadTimeout
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override int ReadByte() => throw new NotSupportedException();

		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

#if NET5_0_OR_GREATER

		public override int Read(Span<byte> buffer) => throw new NotSupportedException();

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

#endif

		#endregion

	}

}
