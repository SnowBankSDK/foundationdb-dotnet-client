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

namespace FoundationDB.Client
{
	/// <summary>Query describing an ongoing GetRange operation</summary>
	internal sealed class FdbKeyValueRangeQuery : FdbRangeQuery<SliceBuffer, KeyValuePair<Slice, Slice>>, IFdbKeyValueRangeQuery
	{

		/// <inheritdoc />
		internal FdbKeyValueRangeQuery(IFdbReadOnlyTransaction transaction, KeySelector begin, KeySelector end, FdbKeyValueDecoder<SliceBuffer, KeyValuePair<Slice, Slice>> transform, bool snapshot, FdbRangeOptions? options)
			: base(transaction, begin, end, null, () => new(), transform, snapshot, options)
		{ }

		public IFdbRangeQuery<TResult> Decode<TState, TResult>(TState state, FdbKeyValueDecoder<TState, TResult> decoder)
		{
			return new FdbRangeQuery<TState, TResult>(
				this.Transaction,
				this.Begin,
				this.End,
				state,
				null,
				decoder,
				this.IsSnapshot,
				this.Options
			);
		}

		public IFdbRangeQuery<TResult> Decode<TResult>(FdbKeyValueDecoder<TResult> decoder)
		{
			return new FdbRangeQuery<FdbKeyValueDecoder<TResult>, TResult>(
				this.Transaction,
				this.Begin,
				this.End,
				decoder,
				null,
				(s, k, v) => s(k, v),
				this.IsSnapshot,
				this.Options
			);
		}

	}
}
