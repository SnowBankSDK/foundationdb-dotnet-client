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

namespace SnowBank.Networking
{
	using System.Numerics;
	using System.Runtime.CompilerServices;

	/// <summary>Helper for computing a moving average of the last N samples for some metric</summary>
	/// <typeparam name="T">Type of the samples</typeparam>
	/// <remarks>This struct maintains the sum of the last N samples, which can then be used to compute the average</remarks>
	[DebuggerDisplay("Total={Total}, Last={Last}, Count={Count}/{Data.Length}")]
	public struct MovingSum<T> where T : IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>
	{

		/// <summary>Constructs a <see cref="MovingSum{T}"/></summary>
		/// <param name="length">Maximum number of samples stored</param>
		public MovingSum(int length)
		{
			Contract.GreaterThan(length, 0);
			this.Data = new T[length];
			this.Count = 0;
			this.Next = 0;
			this.Total = default!;
		}

		/// <summary>Constructs a <see cref="MovingSum{T}"/> with an initial value.</summary>
		/// <param name="length">Maximum number of samples stored</param>
		/// <param name="initialValue">First measure value</param>
		public MovingSum(int length, T initialValue)
		{
			Contract.GreaterThan(length, 0);
			this.Data = new T[length];
			this.Data[0] = initialValue;
			this.Count = 1;
			this.Next = 1;
			this.Total = initialValue;
		}

		/// <summary>Buffer for last remembered samples</summary>
		public readonly T[] Data;

		/// <summary>Number of filled slots</summary>
		public int Count;

		/// <summary>Index of the next slot that will be overwritten</summary>
		public int Next;

		/// <summary>Running total of the last samples (or Zero if no samples yet)</summary>
		public T? Total;

		/// <summary>Last inserted sample (or Zero if no samples yet)</summary>
		public T? Last
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Next != 0 ? this.Data[this.Next - 1] : this.Count != 0 ? this.Data[this.Count - 1] : default;
		}

		/// <summary>Reads the current moving sum and number of samples</summary>
		/// <returns>Current sum and sample count</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (T? Total, int Samples) Read() => (this.Total, this.Count);

		/// <summary>Adds a new sample</summary>
		/// <param name="sample">Measured value</param>
		/// <returns>Updated sum and sample count</returns>
		public (T Total, int Samples) Add(T sample)
		{
			var data = this.Data;
			var p = this.Next;
			var c = this.Count;
			var t = this.Total!;
			if (c < data.Length)
			{ // growing phase
				c = c + 1;
				t = t + sample;
				this.Count = c;
			}
			else
			{ // crusing phase
				t = t - data[p] + sample;
			}
			data[p] = sample;
			this.Next = (p + 1) % this.Data.Length;
			this.Total = t;
			return (t, c);
		}

	}

}
