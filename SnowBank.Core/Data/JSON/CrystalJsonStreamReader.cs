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

namespace SnowBank.Data.Json
{
	using System.IO;
	using System.Text;

	/// <summary>Type that can read JSON fragments from a stream</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonStreamReader : IDisposable //TODO: IAsyncDisposable !
	{

		/// <summary>Reads JSON fragments from a <see cref="Stream"/>, using UTF-8 as the text encoding</summary>
		public CrystalJsonStreamReader(Stream input, CrystalJsonSettings? settings, bool ownSource = false)
		{
			Contract.NotNull(input);
			if (!input.CanRead) throw new InvalidOperationException("The input stream must be readable");

			var source = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
			this.OwnSource = ownSource;
			this.Tokenizer = new(new(source), settings ?? CrystalJsonSettings.Json);
		}

		/// <summary>Reads JSON fragments from a <see cref="TextReader"/></summary>
		public CrystalJsonStreamReader(TextReader input, CrystalJsonSettings? settings, bool ownSource = false)
		{
			Contract.NotNull(input);
			this.OwnSource = ownSource;
			this.Tokenizer = new(new(input), settings ?? CrystalJsonSettings.Json);
		}

		/// <summary>Read the next JSON fragment from this stream</summary>
		/// <returns>Next fragment, or <see langword="null"/> if we reached the end of the stream</returns>
		[Pure]
		public JsonValue? ReadNextFragment()
		{
			if (this.Disposed) throw FailObjectDisposed();
			return CrystalJsonParser<JsonTextReader>.ParseJsonValue(ref this.Tokenizer);
		}

		/// <summary>If <see langword="true"/>, <see cref="Dispose"/> has already been called on this instance</summary>
		private bool Disposed { get; set; }

		/// <summary>JSON Reader used to read values from the source</summary>
		private CrystalJsonTokenizer<JsonTextReader> Tokenizer;

		/// <summary>If <see langword="true"/>, we must dispose the source when this instance is disposed</summary>
		private bool OwnSource { get; }

		#region IDisposable...

		public void Dispose()
		{
			if (!this.Disposed)
			{
				this.Disposed = true;
				if (this.OwnSource)
				{
					this.Tokenizer.Source.Reader.Dispose();
				}
				this.Tokenizer.Dispose();
			}
		}
		
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ObjectDisposedException FailObjectDisposed() => new(nameof(CrystalJsonStreamReader));

		#endregion

		#region Static Helpers...

		public static CrystalJsonStreamReader Open(string path, CrystalJsonSettings? settings = null)
		{
			Contract.NotNullOrEmpty(path);

			Stream? stream = null;
			CrystalJsonStreamReader? sw = null;
			bool failed = true;
			try
			{
				stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 0x400, FileOptions.SequentialScan);
				sw = new(stream, settings, true);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
					stream?.Dispose();
				}
			}
		}

		public static CrystalJsonStreamReader Create(Stream stream, CrystalJsonSettings? settings = null, bool ownStream = false)
		{
			Contract.NotNull(stream);

			CrystalJsonStreamReader? sr = null;
			bool failed = true;
			try
			{
				sr = new(stream, settings, ownStream);
				failed = false;
				return sr;
			}
			finally
			{
				if (failed)
				{
					sr?.Dispose();
				}
			}
		}

		#endregion

	}

}
