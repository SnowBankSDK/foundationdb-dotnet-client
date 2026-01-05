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

namespace SnowBank.Numerics
{
	using System.Globalization;
	using System.Runtime.InteropServices;

	/// <summary>Distributed global counter made up of individual counters</summary>
	[DebuggerDisplay("Count={Counters.Length}, Total={GetTotal()}")]
	[PublicAPI]
	public sealed class RobustCounters
	{

		/// <summary>Array of individual counters</summary>
		public RobustCounter[] Counters { get; }

		/// <summary>Constructs a new <see cref="RobustCounters"/> instance</summary>
		public RobustCounters(int count)
		{
			var counters = new RobustCounter[count];
			for (int i = 0; i < counters.Length; i++)
			{
				counters[i] = new RobustCounter();
			}
			this.Counters = counters;
		}

		/// <summary>Return the global tally of all individual counters</summary>
		/// <remarks>
		/// The value is only considered "stable" if all workers threads have stopped./
		/// While the workers are running, the tally may not be accurate</remarks>
		public long GetTotal()
		{
			long total = 0;
			foreach (var counter in this.Counters)
			{
				total += counter.SafeRead();
			}
			return total;
		}

	}

	/// <summary>Isolated counter that will not introduce false-sharing between concurrent threads</summary>
	[DebuggerDisplay("{Counter.Value}")]
	public sealed class RobustCounter
	{

		private PaddedInt32 Counter;

		/// <summary>Constructs a new <see cref="RobustCounter"/>, initialized to <c>0</c></summary>
		public RobustCounter()
		{ }

		/// <summary>Constructs a new <see cref="RobustCounter"/> with the given initial value</summary>
		public RobustCounter(int initialValue)
		{
			Volatile.Write(ref this.Counter.Value, initialValue);
		}

		/// <summary>Increment from inside the worker thread, when no one else is expected to write to this counter</summary>
		/// <returns>Incremented value</returns>
		/// <example>new RobustCounter(42).Increment() == 43</example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int UnsafeIncrement()
		{
			return ++this.Counter.Value;
		}

		/// <summary>Read the value from inside the worker thread, when no one else is expected to write to this counter</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int UnsafeRead()
		{
			return this.Counter.Value;
		}

		/// <summary>Reset the value from inside the worker thread, when no one else is expected to write to this counter</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void UnsafeReset()
		{
			this.Counter.Value = 0;
		}

		/// <summary>Read the value from outside</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int SafeRead()
		{
			return Volatile.Read(ref this.Counter.Value);
		}

		/// <summary>Holds a 32-bit integer that is guaranteed to not cause false sharing issues</summary>
		/// <remarks>CAUTION: this struct has a size of 128 bytes</remarks>
		[StructLayout(LayoutKind.Explicit, Size = 128)]
		[DebuggerDisplay("{Value}")]
		private struct PaddedInt32
		{
			// The layout of in memory should be:
			// - PADDING: 64 bytes
			// - VALUE:    4 bytes
			// - PADDING: 60 bytes

			[FieldOffset(64)]
			public int Value;

			public override string ToString()
			{
				return this.Value.ToString(CultureInfo.InvariantCulture);
			}

		}

	}

}
