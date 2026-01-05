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

namespace FoundationDB.Layers.Documents
{

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	public class FdbDocumentCollection<TDocument, TId>
		where TDocument : class
	{

		public const int DefaultChunkSize = 1 << 20; // 1 MB

		public FdbDocumentCollection(ISubspaceLocation subspace, Func<TDocument, TId> selector, IValueEncoder<TDocument> valueEncoder)
		{
			Contract.NotNull(subspace);
			Contract.NotNull(selector);
			Contract.NotNull(valueEncoder);

			this.Location = subspace;
			this.IdSelector = selector;
			this.ValueEncoder = valueEncoder;
		}

		protected virtual Task<List<Slice>> LoadPartsAsync(IKeySubspace subspace, IFdbReadOnlyTransaction trans, TId id)
		{
			return trans
				.GetRange(subspace.Key(id).ToRange(inclusive: true)) //TODO: options ?
				.Select(kvp => kvp.Value)
				.ToListAsync();
		}

		protected virtual TDocument DecodeParts(List<Slice> parts)
		{
			var packed = Slice.Join(Slice.Empty, parts);
			return this.ValueEncoder.DecodeValue(packed)!;
		}

		/// <summary>Subspace used as a prefix for all hashset in this collection</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Encoder that packs/unpacks the documents</summary>
		public IValueEncoder<TDocument> ValueEncoder { get; }

		/// <summary>Lambda function used to extract the ID from a document</summary>
		public Func<TDocument, TId> IdSelector { get; }

		/// <summary>Maximum size of a document chunk (1 MB by default)</summary>
		public int ChunkSize { get; } = DefaultChunkSize;

		/// <summary>Inserts a new document in the collection</summary>
		public async Task InsertAsync(IFdbTransaction trans, TDocument document)
		{
			Contract.NotNull(trans);
			Contract.NotNull(document);

			var id = this.IdSelector(document);
			if (id == null) throw new InvalidOperationException("Cannot insert a document with a null identifier");

			// encode the document
			var packed = this.ValueEncoder.EncodeValue(document);

			var subspace = await this.Location.Resolve(trans);

			// Key Prefix = ...(id,)
			var key = subspace.Key(id);

			// clear previous value
			trans.ClearRange(key.ToRange(inclusive: true));

			int remaining = packed.Count;
			if (remaining <= this.ChunkSize)
			{ // stored as a single element

				// Key = ...(id,)
				trans.Set(key, packed);
			}
			else
			{ // splits in as many chunks as necessary

				// Key = ...(id, N) where N is the chunk index (0-based)
				int p = 0;
				int index = 0;
				while (remaining > 0)
				{
					int sz = Math.Max(remaining, this.ChunkSize);
					trans.Set(subspace.Key(id, index), packed.Substring(p, sz));
					++index;
					p += sz;
					remaining -= sz;
				}
			}
		}

		/// <summary>Loads a document from the collection</summary>
		public async Task<TDocument> LoadAsync(IFdbReadOnlyTransaction trans, TId id)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);

			var subspace = await this.Location.Resolve(trans);

			var parts = await LoadPartsAsync(subspace, trans, id).ConfigureAwait(false);

			return DecodeParts(parts);
		}

		/// <summary>Loads multiple documents from the collection</summary>
		public async Task<List<TDocument>> LoadMultipleAsync(IFdbReadOnlyTransaction trans, IEnumerable<TId> ids)
		{
			Contract.NotNull(trans);
			Contract.NotNull(ids);

			var subspace = await this.Location.Resolve(trans);

			var results = await Task.WhenAll(ids.Select(id => LoadPartsAsync(subspace, trans, id)));

			return results.Select(DecodeParts).ToList();
		}

		/// <summary>Deletes a document from the collection</summary>
		public async Task DeleteAsync(IFdbTransaction trans, TId id)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);

			var subspace = await this.Location.Resolve(trans);

			var key = subspace.Key(id);
			trans.ClearRange(key.ToRange(inclusive: true));
		}


		/// <summary>Deletes a document from the collection</summary>
		public async Task DeleteMultipleAsync(IFdbTransaction trans, IEnumerable<TId> ids)
		{
			Contract.NotNull(trans);
			Contract.NotNull(ids);

			var subspace = await this.Location.Resolve(trans);

			foreach (var id in ids)
			{
				var key = subspace.Key(id);
				trans.ClearRange(key.ToRange(inclusive: true));
			}
		}

		/// <summary>Deletes a document from the collection</summary>
		public Task DeleteAsync(IFdbTransaction trans, TDocument document)
		{
			Contract.NotNull(trans);
			Contract.NotNull(document);

			var id = this.IdSelector(document);
			if (id == null) throw new InvalidOperationException();

			return DeleteAsync(trans, id);
		}

		/// <summary>Deletes a document from the collection</summary>
		public Task DeleteMultipleAsync(IFdbTransaction trans, IEnumerable<TDocument> documents)
		{
			Contract.NotNull(trans);
			Contract.NotNull(documents);

			return DeleteMultipleAsync(trans, documents.Select(document => this.IdSelector(document)));
		}

	}

}
