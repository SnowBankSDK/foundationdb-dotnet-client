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

namespace FoundationDB.Layers.Counters
{

	/// <summary>Represents an integer value which can be incremented without conflict.
	/// Uses a sharded representation (which scales with contention) along with background coalescing...
	/// </summary>
	/// <remarks>This is obsoleted for most practical purposes by the addition of atomic to FoundationDB v2.x, which do the same thing more efficiently.</remarks>
	[PublicAPI]
	public class FdbHighContentionCounter
	{
		// based on the lost implementation that used to be at https://github.com/FoundationDB/python-layers/blob/master/lib/counter.py
		// => this version as been "lost to time", and only this c# port remains (archive.org does not have a copy)

		// TODO: should we use a PRNG ? If two counter instances are created at the same moment, they could share the same seed ?
		private readonly Random Rng = new();

		/// <summary>Flag use to know if a background coalescing is already running</summary>
		private int m_coalesceRunning;

		private const int NOT_RUNNING = 0;
		private const int RUNNING = 1;

		/// <summary>Create a new High Contention counter.</summary>
		/// <param name="location">Subspace to be used for storing the counter</param>
		public FdbHighContentionCounter(ISubspaceLocation location)
		{
			Contract.NotNull(location);

			this.Location = location;
		}

		/// <summary>Subspace used as a prefix for all items in this table</summary>
		public ISubspaceLocation Location { get; }

		/// <summary>Generate a new random slice</summary>
		protected virtual Slice RandomId()
		{
			lock (this.Rng) //note: the Rng is not thread-safe
			{
				return Slice.Random(this.Rng, 20);
			}
		}

		private Task Coalesce(IFdbDatabase db, int limit, CancellationToken ct)
		{
			return db.WriteAsync(async tr =>
			{
				long total = 0;
				var subspace = await this.Location.Resolve(tr);

				// read N writes from a random place in ID space
				var loc = RandomId();

				bool right;
				lock (this.Rng) { right = this.Rng.NextDouble() < 0.5; }
				var query = right
					? tr.Snapshot.GetRange(FdbKeyRange.Between(subspace.Key(loc), subspace.Last()), new() { Limit = limit })
					: tr.Snapshot.GetRange(FdbKeyRange.Between(subspace.First(), subspace.Key(loc)), new() { Limit = limit , IsReversed = true });
				var shards = await query.ToListAsync().ConfigureAwait(false);

				if (shards.Count > 0)
				{
					// remove read shards transaction
					foreach (var shard in shards)
					{
						checked { total += TuPack.DecodeKey<long>(shard.Value); }
						await tr.GetAsync(shard.Key).ConfigureAwait(false); // real read for isolation
						tr.Clear(shard.Key);
					}

					tr.Set(subspace.Key(RandomId()), FdbValue.ToTuple(total));

				}
			}, ct);
		}

		private void BackgroundCoalesce(IFdbDatabase db, int n, CancellationToken ct)
		{
			// only coalesce if it is not already running
			if (Interlocked.CompareExchange(ref m_coalesceRunning, RUNNING, NOT_RUNNING) == NOT_RUNNING)
			{
				try
				{
					// fire and forget
					var _ = Task
						.Run(() => Coalesce(db, n, ct), ct)
						.ContinueWith((t) =>
						{
							// reset the flag
							Volatile.Write(ref m_coalesceRunning, NOT_RUNNING);

							// observe any exceptions
							if (t.IsFaulted)
							{
								var x = t.Exception;
								//TODO: logging ?
								System.Diagnostics.Debug.WriteLine($"Background Coalesce error: {x}");
							}
						}, ct);
				}
				catch (Exception)
				{ // something went wrong starting the background coalesce
					Volatile.Write(ref m_coalesceRunning, NOT_RUNNING);
					throw;
				}
			}
		}

		/// <summary>Get the value of the counter.
		/// Not recommended for use with read/write transactions when the counter is being frequently updated (conflicts will be very likely).
		/// </summary>
		public async Task<long> GetTransactional(IFdbReadOnlyTransaction trans)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			var subspace = await this.Location.Resolve(trans);
			if (subspace == null) throw new InvalidOperationException($"Location '{this.Location} referenced by High Contention Counter Layer was not found.");

			long total = 0;
			await trans
				.GetRange(subspace.ToRange())
				.ForEachAsync((kvp) => { checked { total += TuPack.DecodeKey<long>(kvp.Value); } })
				.ConfigureAwait(false);

			return total;
		}

		public Task<long> GetSnapshot(IFdbReadOnlyTransaction trans)
		{
			return GetTransactional(trans.Snapshot);
		}

		/// <summary>Add the value x to the counter.</summary>
		public async ValueTask Add(IFdbTransaction trans, long x)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			var subspace = await this.Location.TryResolve(trans);
			if (subspace == null) throw new InvalidOperationException($"Location '{this.Location} referenced by High Contention Counter Layer was not found.");

			trans.Set(subspace.Key(RandomId()), FdbValue.ToTuple(x));

			// decide if we must coalesce
			//note: Random() is not thread-safe so we must lock
			bool coalesce;
			lock (this.Rng) { coalesce = this.Rng.NextDouble() < 0.1; }
			if (coalesce)
			{
				//REVIEW: 20 is too small if there is a lot of activity on the counter !
				BackgroundCoalesce(trans.Context.Database, 20, CancellationToken.None);
			}
		}

		/// <summary>Set the counter to value x.</summary>
		public async ValueTask SetTotal(IFdbTransaction trans, long x)
		{
			if (trans == null) throw new ArgumentNullException(nameof(trans));

			long value = await GetSnapshot(trans).ConfigureAwait(false);
			await Add(trans, x - value);
		}

	}

}
