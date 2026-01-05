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

namespace FoundationDB
{
	internal static class Batched<TValue, TState>
	{

		public delegate void Handler(ref SliceWriter writer, TValue item, TState state);

		public static Slice[] Convert(SliceWriter writer, ReadOnlySpan<TValue> values, Handler handler, TState state)
		{
			Contract.Debug.Requires(handler != null);

			//Note on performance:
			// - we will reuse the same buffer for each temp key, and copy them into a slice buffer
			// - doing it this way adds a memory copy (writer => buffer) but reduce the number of byte[] allocations (and reduce the GC overhead)

			int start = writer.Position;

			var buffer = new SliceBuffer();

			// pre-allocate the final array with the correct size
			var res = new Slice[values.Length];
			for(int i = 0; i < values.Length; i++)
			{
				// reset position to just after the subspace prefix
				writer.Position = start;

				handler(ref writer, values[i], state);

				// copy full key in the buffer
				res[i] = buffer.Intern(writer.ToSlice());
			}
			return res;
		}

		public static Slice[] Convert(SliceWriter writer, IEnumerable<TValue> values, Handler handler, TState state)
		{
			Contract.Debug.Requires(values != null && handler != null);

			// if we can extract a span, we can use a faster method!
			switch (values)
			{
				case TValue[] arr:
				{
					return Convert(writer, arr.AsSpan(), handler, state);
				}
				case List<TValue> list:
				{
					return Convert(writer, CollectionsMarshal.AsSpan(list), handler, state);
				}
			}

			//Note on performance:
			// - we will reuse the same buffer for each temp key, and copy them into a slice buffer
			// - doing it this way adds a memory copy (writer => buffer) but reduce the number of byte[] allocations (and reduce the GC overhead)

			int start = writer.Position;

			var buffer = new SliceBuffer();

			if (values is ICollection<TValue> coll)
			{ // pre-allocate the final array with the correct size
				var res = new Slice[coll.Count];
				int p = 0;
				foreach (var tuple in coll)
				{
					// reset position to just after the subspace prefix
					writer.Position = start;

					handler(ref writer, tuple, state);

					// copy full key in the buffer
					res[p++] = buffer.Intern(writer.ToSlice());
				}
				Contract.Debug.Assert(p == res.Length);
				return res;
			}
			else
			{ // we won't now the array size until the end...
				var res = new List<Slice>();
				foreach (var tuple in values)
				{
					// reset position to just after the subspace prefix
					writer.Position = start;

					handler(ref writer, tuple, state);

					// copy full key in the buffer
					res.Add(buffer.Intern(writer.ToSlice()));
				}
				return res.ToArray();
			}
		}

	}

}
