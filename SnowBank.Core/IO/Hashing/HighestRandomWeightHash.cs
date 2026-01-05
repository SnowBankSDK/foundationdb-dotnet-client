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

namespace SnowBank.IO.Hashing
{
	/// <summary>Uses the the <strong>Highest Random Weight Hashing</strong> strategy (<em>HRW</em>, also called <em>Rendezvous Hashing</em>) to deterministically choose a <typeparamref name="TBucket">bucket</typeparamref> for any <typeparamref name="TKey">key</typeparamref></summary>
	/// <typeparam name="TKey">Type of the keys that must be assigned to a bucket</typeparam>
	/// <typeparam name="TBucket">Type of the bucket "ids" or "names"</typeparam>
	public class HighestRandomWeightHash<TKey, TBucket>
	{
		// Rendezvous Hashing (aka HRW) is an algorithm used to assign a bucket (from a fixed pool of candiates) for each key in a deterministic way:
		// - It requires a Hash Function 'H(key, bucket)' that compute a 'weight' for each pair (k, buckets[i]).
		//   note: in practice, we first reduce down the bucket ids into a 64-bit "seed" (using any other hash function) to speed up the processing
		// - For each key, we compute all N scores using H(key, buckets[i]), and the bucket with the highest score for this key will be selected.
		// - Each bucket can be weighted up or down using a coefficient (1.0 by default), representing the "processing power" of this node.
		//
		// Example: Buckets = { ID1, ID2, ID3, ID4 }, HF(Key, ID): random distribution over 0..1
		// 
		//  1. Choose("Foo") => ID2              //  2. Choose("Bar") => ID3              //  3. Choose("Baz") => ID1              //
		//                                       //                                       //                                       //
		//       0        ½        1             //       0        ½        1             //       0        ½        1             //
		//       +-----------------|-> Score     //       +-----------------|-> Score     //       +-----------------|-> Score     //
		//   ID1 |        *                      //   ID1 |      *                        //  *ID1 |               #               //
		//  *ID2 |              #                //   ID2 |   *                           //   ID2 |        *                      //
		//   ID3 |    *                          //  *ID3 |           #                   //   ID3 |       *                       //
		//   ID4 |       *                       //   ID4 |         *                     //   ID4 |            *                  //

		// The advantages are:
		// - If the HashFunction is well behaved, all keys will distributed randomly accross all buckets.
		// - Given the same key, same the list of bucket, and same Hash Function, all clients will always reach the same result and select the same bucket.
		// - If a bucket is removed from the pool, only the keys assigned to this bucket will be re-assigned to other buckets. Keys assigned to other buckets will not be impacted.
		// - If a bucket is added to the pool, then only the keys that are now closer to it will need to be reassigned.
		//
		// The inconventients are:
		// - All clients MUST have the up-to-date list of available buckets, including all seeds and parameters needed to compute the scores.
		// - Selecting a bucket for a key is O(n) (for n = number of buckets). But in practice, if the Hash Function is cheap, this is not an issue for "small" n (up to a few hundreds?)
		//   - note: if n is very large, a solution is to use the Skeleton-based variant which is O(log n) (cf https://en.wikipedia.org/wiki/Rendezvous_hashing#Skeleton-based_variant_for_very_large_n)
		// - In case of a score tie (very low probably), another strategy must be used to select one of the candidate bucket (ex: by name order?)
		//
		// Extra bonus:
		// - If the "primary" bucket for a key is down, it is easy to decide on a "fallback" bucket by choosing the second closest weight, and so on (ie: primary/secondary server selection can be built on top of Rendezvous Hashing)
		// - The algorithm can be used to randomly order servers in a deterministic way (ie: using a custom key, and sorting buckets by weight). This could be used to decide on a connection order between hosts
		// - If the bucket's seed are changed, then the new distribution will be completely different. Thus it is easy to add a "global seed" that is used to change each bucket seed, and act as a "generation number".
		//   - ie: given another system that uses the exact same bucket ids (ex: "1", "2", "3",...), but a different global seed, then the key distribution will be completely different.
		//   -Seen from another angle, if two systems uses the same list of bucket ids, and use the same global seed, they will agree on the distribution of keys (but if used for security of encryption, one would need to use crytographically strong hash functions!)

		[DebuggerDisplay("Name={Name} [{Weight}]")]
		public readonly struct Bucket
		{
			/// <summary>Name the bucket</summary>
			public readonly TBucket Name;

			/// <summary>Precomputed seed for the bucket</summary>
			public readonly ulong Seed;

			/// <summary>Weight coefficient for the bucket (1.0 = base, > 1 = higher chance, &lt; 1 = lower chance)</summary>
			public readonly double Weight;

			public Bucket(TBucket name, ulong seed, double weight)
			{
				this.Name = name;
				this.Seed = seed;
				this.Weight = weight;
			}

			private static double Normalize(ulong h)
			{
				const ulong A = (0xFFFFFFFFFFFFFFFFUL >> (64 - 53));
				const double B = (1UL << 53);
				return (h & A) / B;
			}

			[Pure]
			public double Score(Func<ulong, TKey, ulong> hf, TKey key)
			{
				// compute the hash for the key using the bucket's seed
				ulong hash = hf(this.Seed, key);

				// normalize the hash between 0.0 and 1.0
				double norm = Normalize(hash);

				// generate the final score
				double score = 1.0 / -Math.Log(norm);

				// use the weight to "boost" the score
				return score * this.Weight;
			}
		}

		/// <summary>Create a HRW Hasher using a list of Buckets</summary>
		/// <remarks>All nodes will use weight = 1.0</remarks>
		public HighestRandomWeightHash(IEnumerable<TBucket> buckets, Func<ulong, TKey, ulong> kf, [InstantHandle] Func<TBucket, ulong> nf, ulong seed = 0)
		{
			this.Buckets = buckets.Select(n => new Bucket(n, seed ^ nf(n), 1.0)).ToArray();
			if (this.Buckets.Length == 0) throw new ArgumentException("List of buckets cannot be empty.", nameof(buckets));
			this.HashFunction = kf;
		}

		/// <summary>Create a HRW Hasher using a list of weighted Buckets</summary>
		public HighestRandomWeightHash(IEnumerable<(TBucket Name, double Weight)> buckets, Func<ulong, TKey, ulong> kf, [InstantHandle] Func<TBucket, ulong> nf, ulong seed = 0)
		{
			this.Buckets = buckets.Select(n => new Bucket(n.Name, seed ^ nf(n.Name), n.Weight)).ToArray();
			if (this.Buckets.Length == 0) throw new ArgumentException("List of buckets cannot be empty.", nameof(buckets));
			this.HashFunction = kf;
		}

		/// <summary>Create a HRW Hasher using a list of weighted Buckets (with already computed Seed)</summary>
		public HighestRandomWeightHash(IEnumerable<(TBucket Name, ulong Seed, double Weight)> buckets, Func<ulong, TKey, ulong> kf)
		{
			this.Buckets = buckets.Select(n => new Bucket(n.Name, n.Seed, n.Weight)).ToArray();
			if (this.Buckets.Length == 0) throw new ArgumentException("List of buckets cannot be empty.", nameof(buckets));
			this.HashFunction = kf;
		}

		/// <summary>Unordered list of candidates</summary>
		private readonly Bucket[] Buckets;

		/// <summary>Return the number of buckets available</summary>
		public int Count => this.Buckets.Length;

		/// <summary>Hash function used to compute the score</summary>
		/// <remarks>HF(BUCKET_SEED, KEY) => 0..ulong.MaxValue</remarks>
		public Func<ulong, TKey, ulong> HashFunction { get; }

		/// <summary>Choose the best bucket for the given key</summary>
		/// <returns>Bucket with the highest score for this key</returns>
		public TBucket Choose(TKey key)
		{
			var hf = this.HashFunction;
			double weight = 0;
			int p = -1;
			for (int i = 0; i < this.Buckets.Length; i++)
			{
				double score = this.Buckets[i].Score(hf, key);
				if (p == -1 || score > weight)
				{
					p = i;
					weight = score;
				}
			}

			return this.Buckets[p].Name;
		}

		/// <summary>Choose the first <paramref name="n"/> server responsible for a given <paramref name="key"/>, in descending priority</summary>
		/// <param name="key"></param>
		/// <param name="n">Number of servers to select (>= 1)</param>
		/// <returns>Array of length up to <paramref name="n"/> with the first server, then the second and so on. If <paramref name="n"/> is larger than the number of buckets, then all the buckets will be returned.</returns>
		/// <remarks><see cref="ChooseMultiple"/>(key, 1) is equivalent to calling <see cref="Choose"/>(key), and <see cref="ChooseMultiple"/>(key, Buckets.Count) is equivalent to calling <see cref="RankBy"/>(key).ToArray()</remarks>
		public TBucket[] ChooseMultiple(TKey key, int n)
		{
			if (n == 1) return [ Choose(key) ];
			return RankBy(key).Take(n).ToArray();
		}

		/// <summary>Rank all buckets for a key, from the highest score to the lowest score</summary>
		/// <remarks>Returns the list of all buckets from the highest to the lowest</remarks>
		/// <example>
		/// <para>Top 3 server: <code>List&lt;BUCKET&gt; topThree = hrw.RankBy(KEY).Take(3).ToList();</code></para>
		/// <para>Secondary server:<code>BUCKET secondary = hrw.RankBy(KEY).Skip(1).FirstOrDefault();</code></para>
		/// </example>
		public IEnumerable<TBucket> RankBy(TKey key)
		{
			return Score(key).OrderByDescending(x => x.Score).Select(x => x.Bucket);
		}

		/// <summary>Compute and return the score of all buckets for a given key</summary>
		/// <returns>Unordered list of (Bucket, Score) for this key</returns>
		public IEnumerable<(TBucket Bucket, double Score)> Score(TKey key)
		{
			var hf = this.HashFunction;
			var buckets = this.Buckets;
			for (int i = 0; i < buckets.Length; i++)
			{
				yield return (buckets[i].Name, buckets[i].Score(hf, key));
			}
		}

	}
}
