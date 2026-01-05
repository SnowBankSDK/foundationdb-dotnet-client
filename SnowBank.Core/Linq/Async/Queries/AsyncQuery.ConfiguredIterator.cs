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

namespace SnowBank.Linq
{
	public static partial class AsyncQuery
	{

		/// <summary>Adapts this query into the equivalent <see cref="IAsyncEnumerable{T}"/></summary>
		/// <param name="source">Source query that will be adapted into an <see cref="IAsyncEnumerable{T}"/></param>
		/// <param name="hint">Hint passed to the source provider.</param>
		/// <returns>Sequence that will asynchronously return the results of this query.</returns>
		/// <remarks>
		/// <para>For best performance, the caller should take care to provide a <paramref name="hint"/> that matches how this query will be consumed downstream.</para>
		/// <para>If the hint does not match, performance may be degraded.
		/// For example, if the caller will consumer this query using <c>await foreach</c> or <c>ToListAsync</c>, but uses <see cref="AsyncIterationHint.Iterator"/>, the provider may fetch small pages initially, before ramping up.
		/// The opposite is also true if the caller uses <see cref="AsyncIterationHint.All"/> but consumes the query using <c>AnyAsync()</c> or <c>FirstOrDefaultAsync</c>, the provider may fetch large pages and waste most of it except the first few elements.
		/// </para>
		/// </remarks>
		public static IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IAsyncQuery<T> source, AsyncIterationHint hint = AsyncIterationHint.Default)
			=> source is IAsyncLinqQuery<T> query ? query.ToAsyncEnumerable() : new ConfiguredIterator<T>(source, hint);

		/// <summary>Exposes an async query as a regular <see cref="IAsyncEnumerable{T}"/>, with en explicit <see cref="AsyncIterationHint"/></summary>
		internal sealed class ConfiguredIterator<T> : IAsyncEnumerable<T>
		{

			private IAsyncQuery<T> Source { get; }

			private AsyncIterationHint Hint { get; }

			public ConfiguredIterator(IAsyncQuery<T> source, AsyncIterationHint hint)
			{
				this.Source = source;
				this.Hint = hint;
			}

			public CancellationToken Cancellation => this.Source.Cancellation;

			[MustDisposeResource]
			public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken ct = default)
			{
				//BUGBUG: if ct is not None and not the same as the source, we should maybe mix them!?
				return this.Source.GetAsyncEnumerator(this.Hint);
			}

		}

	}

}
