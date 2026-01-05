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

namespace FoundationDB.Layers.Blobs
{
	using System.Globalization;

	/// <summary>Represents a potentially large binary value in FoundationDB.</summary>
	[DebuggerDisplay("Location={Location}")]
	[PublicAPI]
	public class FdbBlob : IFdbLayer<FdbBlob.State>
	{

		private const long CHUNK_LARGE = 10000; // all chunks will be not greater than this size
		private const long CHUNK_SMALL = 200; // all adjacent chunks will sum to more than this size

		private static readonly Slice SizeSuffix = Slice.FromChar('S');
		private static readonly Slice AttributesSuffix = Slice.FromChar('A');
		private static readonly Slice DataSuffix = Slice.FromChar('D');

		/// <summary>
		/// Create a new object representing a binary large object (blob).
		/// Only keys within the subspace will be used by the object. 
		/// Other clients of the database should refrain from modifying the subspace.</summary>
		/// <param name="location">Subspace to be used for storing the blob data and metadata</param>
		public FdbBlob(ISubspaceLocation location)
		{
			Contract.NotNull(location);

			this.Location = location;
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public ISubspaceLocation Location { get; }

		private readonly struct Chunk
		{

			public readonly Slice Key;

			public readonly Slice Data;

			public readonly long Offset;

			public Chunk(Slice key, Slice data, long offset)
			{
				this.Key = key;
				this.Data = data;
				this.Offset = offset;
			}

		}

		/// <inheritdoc />
		public async ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			var subspace = await this.Location.Resolve(tr).ConfigureAwait(false);
			return new(subspace);
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbBlob);

		[PublicAPI]
		[DebuggerDisplay("Subspace={Subspace}")]
		public sealed class State
		{

			public IKeySubspace Subspace { get; }

			public State(IKeySubspace subspace)
			{
				Contract.Debug.Requires(subspace != null);
				this.Subspace = subspace;
			}

			/// <summary>Returns the key for data chunk at the specified offset</summary>
			/// <returns>123 => (subspace, 'D', "             123")</returns>
			private FdbTupleKey<Slice, string> DataKey(long offset)
			{
				//note: python code uses "%16d" % offset, which pads the value with spaces... Not sure why ?
				return this.Subspace.Key(DataSuffix, offset.ToString("D16", CultureInfo.InvariantCulture));
			}

			private long DataKeyOffset(Slice key)
			{
				long offset = long.Parse(this.Subspace.DecodeLast<string>(key)!, CultureInfo.InvariantCulture);
				if (offset < 0) throw new InvalidOperationException("Chunk offset value cannot be less than zero");
				return offset;
			}

			private FdbTupleKey<Slice> SizeKey()
			{
				return this.Subspace.Key(SizeSuffix);
			}

			#region Internal Helpers...

			private async Task<Chunk> GetChunkAtAsync(IFdbTransaction trans, long offset)
			{
				Contract.Debug.Requires(trans != null && offset >= 0);

				var chunkKey = await trans.GetKeyAsync(DataKey(offset).LastLessOrEqual()).ConfigureAwait(false);
				if (chunkKey.IsNull || !this.Subspace.Contains(chunkKey))
				{ // nothing before (sparse)
					return default(Chunk);
				}

				if (DataKey(0).CompareTo(chunkKey) > 0)
				{ // off beginning
					return default(Chunk);
				}

				long chunkOffset = DataKeyOffset(chunkKey);

				var chunkData = await trans.GetAsync(chunkKey).ConfigureAwait(false);

				if (chunkOffset + chunkData.Count <= offset)
				{ // in sparse region after chunk
					return default(Chunk);
				}

				return new Chunk(chunkKey, chunkData, chunkOffset);
			}

			private async Task MakeSplitPointAsync(IFdbTransaction trans, long offset)
			{
				Contract.Debug.Requires(trans != null && offset >= 0);

				var chunk = await GetChunkAtAsync(trans, offset).ConfigureAwait(false);
				if (chunk.Key == Slice.Nil) return; // already sparse
				if (chunk.Offset == offset) return; // already a split point

				int splitPoint;
				checked
				{
					splitPoint = (int) (offset - chunk.Offset);
				}

				trans.Set(DataKey(chunk.Offset), chunk.Data.Substring(0, splitPoint));
				trans.Set(DataKey(offset), chunk.Data.Substring(splitPoint));
			}

			private async Task MakeSparseAsync(IFdbTransaction trans, long start, long end)
			{
				await MakeSplitPointAsync(trans, start).ConfigureAwait(false);
				await MakeSplitPointAsync(trans, end).ConfigureAwait(false);
				trans.ClearRange(DataKey(start), DataKey(end));
			}

			private async Task<bool> TryRemoteSplitPointAsync(IFdbTransaction trans, long offset)
			{
				Contract.Debug.Requires(trans != null && offset >= 0);

				var b = await GetChunkAtAsync(trans, offset).ConfigureAwait(false);
				if (b.Offset == 0 || b.Key == Slice.Nil) return false; // in sparse region, or at beginning

				var a = await GetChunkAtAsync(trans, b.Offset - 1).ConfigureAwait(false);
				if (a.Key == Slice.Nil) return false; // no previous chunk

				if (a.Offset + a.Data.Count != b.Offset) return false; // chunks can't be joined
				if (a.Data.Count + b.Data.Count > CHUNK_SMALL) return false; // chunks shouldn't be joined
				// yay--merge chunks
				trans.Clear(b.Key);
				trans.Set(a.Key, a.Data + b.Data);
				return true;
			}

			private void WriteToSparse(IFdbTransaction trans, long offset, ReadOnlySpan<byte> data)
			{
				Contract.Debug.Requires(trans != null && offset >= 0);

				if (data.Length == 0) return;

				int chunks = (int) ((data.Length + CHUNK_LARGE - 1) / CHUNK_LARGE);
				int chunkSize = (data.Length + chunks) / chunks;

				for (int n = 0; n < data.Length; n += chunkSize)
				{
					int r = Math.Min(chunkSize, data.Length - n);
					trans.Set(DataKey(offset + n), data.Slice(n, r));
				}
			}

			private void SetSize(IFdbTransaction trans, long size)
			{
				Contract.Debug.Requires(trans != null && size >= 0);

				//note: python code converts the size into a string
				trans.Set(SizeKey(), Slice.FromString(size.ToString()));
			}

			#endregion

			/// <summary>Deletes all key-value pairs associated with the blob.</summary>
			public void Delete(IFdbTransaction trans)
			{
				Contract.NotNull(trans);

				trans.ClearRange(this.Subspace);
			}

			/// <summary>Gets the size (in bytes) of the blob.</summary>
			/// <returns>Return null if the blob does not exist, 0 if is empty, or the size in bytes</returns>
			public Task<long?> GetSizeAsync(IFdbReadOnlyTransaction trans)
			{
				Contract.NotNull(trans);

				return GetSizeInternalAsync(trans);
			}

			private async Task<long?> GetSizeInternalAsync(IFdbReadOnlyTransaction trans)
			{

				Slice value = await trans.GetAsync(SizeKey()).ConfigureAwait(false);

				if (value.IsNullOrEmpty) return null;

				//note: python code stores the size as a string
				long size = long.Parse(value.ToString());
				if (size < 0) throw new InvalidOperationException("The internal blob size cannot be negative");
				return size;
			}

			/// <summary>Reads from the blob, starting at <paramref name="offset"/>, retrieving up to <paramref name="n"/> bytes (fewer than n bytes are returned when the end of the blob is reached).</summary>
			public async Task<Slice> ReadAsync(IFdbReadOnlyTransaction trans, long offset, int n)
			{
				Contract.NotNull(trans);
				Contract.Positive(offset);

				long? size = await GetSizeInternalAsync(trans).ConfigureAwait(false);
				if (size == null) return Slice.Nil; // not found

				if (offset >= size.Value)
					return Slice.Empty;

				// read all chunks matching the segment we need, and copy them in our buffer
				var buffer = new byte[Math.Min(n, size.Value - offset)];

				await trans
					.GetRange(
						DataKey(offset).LastLessOrEqual(),
						DataKey(offset + n).FirstGreaterOrEqual()
					)
					.ForEachAsync((chunk) =>
					{
						// get offset of this chunk
						long chunkOffset = DataKeyOffset(chunk.Key);
						var chunkData = chunk.Value;

						checked
						{
							// intersect chunk bounds with output
							int delta = (int) (chunkOffset - offset);
							int start = delta;
							int end = delta + chunkData.Count;
							if (start < 0) start = 0;
							if (end > n) end = n;

							// compute the relative offsets in the chunk
							int rStart = start - delta;
							int rEnd = end - delta;

							var intersect = chunkData[rStart, rEnd];
							if (intersect.IsPresent)
							{ // copy the data that fits
								intersect.CopyTo(buffer, start);
							}
						}
					})
					.ConfigureAwait(false);

				return buffer.AsSlice(0, buffer.Length);
			}

			/// <summary>Writes <paramref name="data"/> to the blob, starting at <paramref name="offset"/> and overwriting any existing data at that location. The length of the blob is increased if necessary.</summary>
			public async Task WriteAsync(IFdbTransaction trans, long offset, ReadOnlyMemory<byte> data)
			{
				Contract.NotNull(trans);
				Contract.Positive(offset);

				if (data.Length == 0) return;

				long end = offset + data.Length;
				await MakeSparseAsync(trans, offset, end).ConfigureAwait(false);
				WriteToSparse(trans, offset, data.Span);
				await TryRemoteSplitPointAsync(trans, offset).ConfigureAwait(false);

				long oldLength = (await GetSizeInternalAsync(trans).ConfigureAwait(false)) ?? 0;
				if (end > oldLength)
				{ // lengthen file if necessary
					SetSize(trans, end);
				}
				else
				{ // write end needs to be merged
					await TryRemoteSplitPointAsync(trans, end).ConfigureAwait(false);
				}
			}

			/// <summary>Writes <paramref name="data"/> to the blob, starting at <paramref name="offset"/> and overwriting any existing data at that location. The length of the blob is increased if necessary.</summary>
			public Task WriteAsync(IFdbTransaction trans, long offset, Slice data)
			{
				return WriteAsync(trans, offset, data.Memory);
			}

			/// <summary>Appends the contents of <paramref name="data"/> onto the end of the blob.</summary>
			public async Task AppendAsync(IFdbTransaction trans, ReadOnlyMemory<byte> data)
			{
				Contract.NotNull(trans);

				if (data.Length == 0) return;

				long oldLength = (await GetSizeAsync(trans).ConfigureAwait(false)) ?? 0;
				WriteToSparse(trans, oldLength, data.Span);
				await TryRemoteSplitPointAsync(trans, oldLength).ConfigureAwait(false);
				SetSize(trans, oldLength + data.Length);
			}

			/// <summary>Appends the contents of <paramref name="data"/> onto the end of the blob.</summary>
			public Task AppendAsync(IFdbTransaction trans, Slice data)
			{
				return AppendAsync(trans, data.Memory);
			}

			/// <summary>Changes the blob length to <paramref name="newLength"/>, erasing any data when shrinking, and filling new bytes with 0 when growing.</summary>
			public async Task TruncateAsync(IFdbTransaction trans, long newLength)
			{
				Contract.NotNull(trans);
				Contract.Positive(newLength);

				long? length = await GetSizeAsync(trans).ConfigureAwait(false);
				if (length != null)
				{
					await MakeSparseAsync(trans, newLength, length.Value).ConfigureAwait(false);
				}

				SetSize(trans, newLength);
			}

		}

	}

}
