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
	// THIS IS NOT AN OFFICIAL LAYER, JUST A PROTOTYPE TO TEST A FEW THINGS !

	/// <summary>Represents a collection of dictionaries of fields.</summary>
	[PublicAPI]
	public class FdbHashSetCollection
	{

		public FdbHashSetCollection(IKeySubspace subspace)
		{
			Contract.NotNull(subspace);

			this.Subspace = subspace;
		}

		/// <summary>Subspace used as a prefix for all hashsets in this collection</summary>
		public IKeySubspace Subspace { get; }

		#region Get

		/// <summary>Returns the value of a specific field of a hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="field">Name of the field to read</param>
		/// <returns>Value of the corresponding field, or <see cref="Slice.Nil"/> if it the hashset does not exist, or doesn't have a field with this name</returns>
		public Task<Slice> GetValueAsync(IFdbReadOnlyTransaction trans, IVarTuple id, string field)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNullOrEmpty(field);

			return trans.GetAsync(this.Subspace.Tuple(id).Key(field));
		}

		/// <summary>Returns all fields of a hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>Dictionary containing, for all fields, their associated values</returns>
		public async Task<IDictionary<string, Slice>> GetAsync(IFdbReadOnlyTransaction trans, IVarTuple id)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);

			var results = new Dictionary<string, Slice>(StringComparer.OrdinalIgnoreCase);

			await trans
				.GetRange(this.Subspace.Tuple(id).ToRange())
				.ForEachAsync((kvp) =>
				{
					string field = this.Subspace.DecodeLast<string>(kvp.Key)!;
					results[field] = kvp.Value;
				})
				.ConfigureAwait(false);

			return results;
		}

		/// <summary>Returns one or more fields of a hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <param name="fields">List of the fields to read</param>
		/// <returns>Dictionary containing the values of the selected fields, or <see cref="Slice.Empty"/> if that particular field does not exist.</returns>
		public async Task<IDictionary<string, Slice>> GetAsync(IFdbReadOnlyTransaction trans, IVarTuple id, params string[] fields)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNull(fields);

			var values = await trans.GetValuesAsync(fields, f => this.Subspace.Tuple(id).Key(f)).ConfigureAwait(false);
			Contract.Debug.Assert(values != null && values.Length == fields.Length);

			var results = new Dictionary<string, Slice>(values.Length, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < fields.Length; i++)
			{
				results[fields[i]] = values[i];
			}
			return results;
		}

		#endregion

		#region Set

		public void SetValue(IFdbTransaction trans, IVarTuple id, string field, Slice value)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNullOrEmpty(field);

			trans.Set(this.Subspace.Tuple(id).Key(field), value);
		}

		public void Set(IFdbTransaction trans, IVarTuple id, IEnumerable<KeyValuePair<string, Slice>> fields)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNull(fields);

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field.Key)) throw new ArgumentException("Field cannot have an empty name", nameof(fields));
				trans.Set(this.Subspace.Tuple(id).Key(field.Key), field.Value);
			}
		}

		#endregion

		#region Delete

		/// <summary>Removes a field of a hashset</summary>
		public void DeleteValue(IFdbTransaction trans, IVarTuple id, string field)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNullOrEmpty(field);

			trans.Clear(this.Subspace.Tuple(id).Key(field));
		}

		/// <summary>Removes all fields of a hashset</summary>
		public void Delete(IFdbTransaction trans, IVarTuple id)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);

			// remove all fields of the hash
			trans.ClearRange(this.Subspace.Tuple(id).ToRange());
		}

		/// <summary>Removes one or more fields of a hashset</summary>
		public void Delete(IFdbTransaction trans, IVarTuple id, params string[] fields)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);
			Contract.NotNull(fields);

			foreach (var field in fields)
			{
				if (string.IsNullOrEmpty(field)) throw new ArgumentException("Field cannot have an empty name", nameof(fields));
				trans.Clear(this.Subspace.Tuple(id).Key(field));
			}
		}

		#endregion

		#region Keys

		/// <summary>Returns the list the names of all fields of a hashset</summary>
		/// <param name="trans">Transaction that will be used for this request</param>
		/// <param name="id">Unique identifier of the hashset</param>
		/// <returns>List of all fields. If the list is empty, the hashset does not exist</returns>
		public Task<List<string>> GetKeys(IFdbReadOnlyTransaction trans, IVarTuple id)
		{
			Contract.NotNull(trans);
			Contract.NotNull(id);

			return trans
				.GetRangeKeys(this.Subspace.Tuple(id).ToRange())
				.Select((k) => this.Subspace.DecodeLast<string>(k)!)
				.ToListAsync();
		}

		#endregion

	}

}
