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

namespace FoundationDB.Layers.Experimental.Indexing
{
	using System.Buffers;
	using System.Globalization;

	/// <summary>Simple index that maps values of type <typeparamref name="TValue"/> into lists of numerical ids</summary>
	/// <typeparam name="TValue">Type of the value being indexed</typeparam>
	[DebuggerDisplay("Name={Name}, Subspace={Subspace}, IndexNullValues={IndexNullValues})")]
	[PublicAPI]
	public class FdbCompressedBitmapIndex<TValue>
	{

		public FdbCompressedBitmapIndex(string name, IKeySubspace subspace, IEqualityComparer<TValue>? valueComparer = null, bool indexNullValues = false)
		{
			Contract.NotNull(name);
			Contract.NotNull(subspace);

			this.Name = name;
			this.Subspace = subspace;
			this.ValueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
			this.IndexNullValues = indexNullValues;
		}

		public string Name { get; }

		public IKeySubspace Subspace { get; }

		public IEqualityComparer<TValue> ValueComparer { get; }

		/// <summary>If <see langword="true"/>, null values are inserted in the index. If <see langword="false"/> (default), they are ignored</summary>
		/// <remarks>This has no effect if <typeparamref name="TValue" /> is not a reference type</remarks>
		public bool IndexNullValues { get; }

		/// <summary>Inserts a newly created entity to the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the new entity (that was never indexed before)</param>
		/// <param name="value">Value of this entity in the index</param>
		/// <returns>True if a value was inserted into the index; or false if <paramref name="value"/> is null and <see cref="IndexNullValues"/> is false, or if this <paramref name="id"/> was already indexed at this <paramref name="value"/>.</returns>
		public async Task<bool> AddAsync(IFdbTransaction trans, long id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			if (this.IndexNullValues || value != null)
			{
				var key = this.Subspace.Key(value);
				var data = await trans.GetAsync(key).ConfigureAwait(false);

				using var builder = new CompressedBitmapBuilder(data, ArrayPool<CompressedWord>.Shared);
				if (builder.Set(id))
				{
					trans.Set(key, builder.ToSlice());
					return true;
				}
			}

			return false;
		}

		/// <summary>Updates the indexed values of an entity</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has changed</param>
		/// <param name="newValue">Previous value of this entity in the index</param>
		/// <param name="previousValue">New value of this entity in the index</param>
		/// <returns>True if a change was performed in the index; otherwise false (if <paramref name="previousValue"/> and <paramref name="newValue"/>)</returns>
		/// <remarks>If <paramref name="newValue"/> and <paramref name="previousValue"/> are identical, then nothing will be done. Otherwise, the old index value will be deleted and the new value will be added</remarks>
		public async Task<bool> UpdateAsync(IFdbTransaction trans, long id, TValue newValue, TValue previousValue)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			if (!this.ValueComparer.Equals(newValue, previousValue))
			{
				// remove previous value
				if (this.IndexNullValues || previousValue != null)
				{
					var key = this.Subspace.Key(previousValue);
					var data = await trans.GetAsync(key).ConfigureAwait(false);
					if (data.HasValue)
					{
						using var builder = new CompressedBitmapBuilder(data, ArrayPool<CompressedWord>.Shared);
						if (builder.Clear(id))
						{
							trans.Set(key, builder.ToSlice());
						}
					}
				}

				// add new value
				if (this.IndexNullValues || newValue != null)
				{
					var key = this.Subspace.Key(newValue);
					var data = await trans.GetAsync(key).ConfigureAwait(false);

					using var builder = new CompressedBitmapBuilder(data, ArrayPool<CompressedWord>.Shared);
					if (builder.Set(id))
					{
						trans.Set(key, builder.ToSlice());
					}
				}

				// cannot be both null, so we did at least something
				return true;
			}
			return false;
		}

		/// <summary>Removes an entity from the index</summary>
		/// <param name="trans">Transaction to use</param>
		/// <param name="id">Id of the entity that has been deleted</param>
		/// <param name="value">Previous value of the entity in the index</param>
		public async Task<bool> RemoveAsync(IFdbTransaction trans, long id, TValue value)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			var key = this.Subspace.Key(value);
			var data = await trans.GetAsync(key).ConfigureAwait(false);
			if (data.HasValue)
			{
				using var builder = new CompressedBitmapBuilder(data, ArrayPool<CompressedWord>.Shared);
				if (builder.Clear(id))
				{
					trans.Set(key, builder.ToSlice());
					return true;
				}
			}
			return false;
		}

		/// <summary>Returns a list of ids matching a specific value</summary>
		/// <param name="trans"></param>
		/// <param name="value">Value to lookup</param>
		/// <param name="reverse"></param>
		/// <returns>List of document ids matching this value for this particular index (can be empty if no document matches)</returns>
		public async Task<IEnumerable<long>?> LookupAsync(IFdbReadOnlyTransaction trans, TValue value, bool reverse = false)
		{
			var key = this.Subspace.Key(value);
			var data = await trans.GetAsync(key).ConfigureAwait(false);
			if (data.IsNull) return null;
			if (data.IsEmpty) return [ ];
			var bitmap = new CompressedBitmap(data);
			if (reverse) throw new NotImplementedException(); //TODO: GetView(reverse:true) !
			return bitmap.GetView();
		}
		
		public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"BitmapIndex['{this.Name}']");

	}

}
