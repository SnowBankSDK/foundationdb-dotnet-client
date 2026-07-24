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

namespace SnowBank.Benchmarks
{
	using System.Threading.Tasks.Sources;
	using BenchmarkDotNet.Attributes;

	/// <summary>Isolates the per-operation cost of surfacing a completed FDBFuture to an awaiting caller: the current
	/// TaskCompletionSource-per-future model (the wrapper IS a TCS, so every operation allocates one TCS + one Task)
	/// against a reusable <see cref="IValueTaskSource{T}"/> backed by
	/// <see cref="ManualResetValueTaskSourceCore{T}"/> (zero allocation when the consumer awaits exactly once).
	/// The result is set before the await, mirroring a future that fires promptly under saturation.</summary>
	[Config(typeof(FdbBenchConfig))]
	[MemoryDiagnoser]
	public class CompletionSourceBenchmarks
	{

		[Benchmark(Baseline = true)]
		public async Task<long> TaskCompletionSourcePerOp()
		{
			var tcs = new TaskCompletionSource<long>();
			tcs.TrySetResult(42);
			return await tcs.Task;
		}

		private sealed class ReusableSource : IValueTaskSource<long>
		{

			/// <summary>Mutable struct: must not be exposed as a readonly member</summary>
			private ManualResetValueTaskSourceCore<long> Core;

			public short Version => this.Core.Version;

			public void SetResult(long value) => this.Core.SetResult(value);

			public void Reset() => this.Core.Reset();

			public long GetResult(short token) => this.Core.GetResult(token);

			public ValueTaskSourceStatus GetStatus(short token) => this.Core.GetStatus(token);

			public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) => this.Core.OnCompleted(continuation, state, token, flags);

		}

		private ReusableSource Source { get; } = new();

		[Benchmark]
		public async ValueTask<long> PooledValueTaskSourcePerOp()
		{
			var source = this.Source;
			source.SetResult(42);
			long result = await new ValueTask<long>(source, source.Version);
			source.Reset();
			return result;
		}

	}

}
