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

	public class QueryStatistics<TData>
	{
		public QueryStatistics()
		{ }

		public QueryStatistics(TData? value)
		{
			this.Value = value;
		}

		public TData? Value { get; protected set; }

		public void Update(TData? newValue)
		{
			this.Value = newValue;
		}
	}

	public sealed class KeyValueSizeStatistics
	{

		/// <summary>Total number of pairs of keys and values that have flowed through this point</summary>
		public long Count { get; private set; }

		/// <summary>Total size of all keys and values combined</summary>
		public long Size => checked(this.KeySize + this.ValueSize);

		/// <summary>Total size of all keys combined</summary>
		public long KeySize { get; private set; }

		/// <summary>Total size of all values combined</summary>
		public long ValueSize { get; private set; }

		public void Add(int keySize, int valueSize)
		{
			this.Count++;
			this.KeySize = checked(keySize + this.KeySize);
			this.ValueSize = checked(valueSize + this.ValueSize);
		}

	}

	public sealed class DataSizeStatistics
	{

		/// <summary>Total number of items that have flowed through this point</summary>
		public long Count { get; private set; }

		/// <summary>Total size of all items that have flowed through this point</summary>
		public long Size { get; private set; }

		public void Add(int size)
		{
			this.Count++;
			this.Size = checked(size + this.Size);
		}

	}

}
