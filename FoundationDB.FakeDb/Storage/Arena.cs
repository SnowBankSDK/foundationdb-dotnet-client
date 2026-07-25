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

namespace FoundationDB.Storage
{
	using FoundationDB.Client;

	[PublicAPI]
	public sealed record Arena : IDisposable
	{

		private PooledSliceAllocator Keys { get; }

		private PooledSliceAllocator Values { get; }

		public Arena(int keySize, int valueSize, ArrayPool<byte> pool)
		{
			this.Keys = new(keySize, pool);
			this.Values = new(valueSize, pool);
		}

		public void Dispose()
		{
			this.Keys.Dispose();
			this.Values.Dispose();
		}

		internal void Clear()
		{
			this.Keys.Clear();
			this.Values.Clear();
		}

		/// <summary>Allocates a key in this arena (without its content known yet)</summary>
		public Key AllocateKey(int count, bool clear = false)
		{
			var tmp = this.Keys.Allocate(count);
			if (clear) tmp.AsSpan().Clear();
			return new Key(tmp.AsSlice(), this);
		}

		/// <summary>Allocates a value in this arena (without its content known yet)</summary>
		public Value AllocateValue(int count, bool clear = false)
		{
			var tmp = this.Values.Allocate(count);
			if (clear) tmp.AsSpan().Clear();
			return new Value(tmp.AsSlice(), this);
		}

		/// <summary>Copies a <see cref="Key"/> to this Arena, unless it already belongs to it</summary>
		public Key InternKey(Key key)
		{
			return key.Arena == this || key.Arena == null ? key : new Key(this.Keys.Intern(key.Slice), this);
		}

		public Key InternKey(Slice data)
		{
			return data.Count != 0 ? new Key(this.Keys.Intern(data), this) : new Key(data);
		}

		/// <summary>Copies a pair of keys to this Arena, unless they already belong to it</summary>
		public KeyRange InternKeyRange(Key begin, Key end)
		{
			if (begin.Arena == this && end.Arena == this)
			{
				return new KeyRange(begin, end);
			}

			if (end.Slice.StartsWith(begin.Slice))
			{ // begin = ABC, end = ABCDEF, we can merge both!
				if (end.Arena == this)
				{
					return new KeyRange(new Key(end.Slice.Substring(0, begin.Count), this), end);
				}

				var tmp = this.Keys.Intern(end.Slice);
				return new KeyRange(new Key(tmp.Substring(0, begin.Count), this), new Key(tmp, this));
			}

			// need to intern both of them
			return new KeyRange(InternKey(begin), InternKey(end));
		}

		public Key InternKeyZero(Slice data)
		{
			int n = data.Count;
			if (n == 0) return new Key(Slice.FromByte(0));
			var tmp = this.Keys.Allocate(n + 1);
			data.CopyTo(tmp);
			// note: writing through AsSpan() because the ArraySegment indexer does not exist on netstandard2.0
			tmp.AsSpan()[n] = 0;
			return new Key(tmp, this);
		}

		public Key InternKey(ReadOnlySpan<byte> data)
		{
			return data.Length != 0 ? new Key(this.Keys.Intern(data), this) : Key.Empty;
		}

		/// <summary>Copies a <see cref="Value"/> to this Arena, unless it already belongs to it</summary>
		public Value InternValue(Value value)
		{
			return value.Arena == this || value.Arena == null ? value : new Value(this.Values.Intern(value.Slice), this);
		}

		public Value InternValue(Slice data)
		{
			return data.Count != 0 ? new Value(this.Values.Intern(data), this) : new Value(data, null);
		}

		public Value InternValue(ReadOnlySpan<byte> data)
		{
			return data.Length != 0 ? new Value(this.Values.Intern(data), this) : Value.Empty;
		}

		internal KeyRange InternKeyRange(Slice key)
		{
			var x = this.Keys.Intern(key, 0);
			return new(new Key(x[..^1], this), new Key(x, this));
		}

		internal KeyRange InternKeyRange(ReadOnlySpan<byte> key)
		{
			var x = this.Keys.Intern(key, 0);
			return new(new Key(x[..^1], this), new Key(x, this));
		}

		internal KeyRange[] InternKeyRanges(ReadOnlySpan<Slice> keys)
		{
			var res = new KeyRange[keys.Length];
			for (int i = 0; i < keys.Length; i++)
			{
				var x = this.Keys.Intern(keys[i], 0);
				res[i] = new (new Key(x[..^1], this), new Key(x, this));
			}
			return res;
		}

		internal Selector InternSelector(in KeySelector selector)
		{
			return new Selector(InternKey(selector.Key), selector.OrEqual, selector.Offset);
		}

		internal Selector InternSelector(in KeySpanSelector selector)
		{
			return new Selector(InternKey(selector.Key), selector.OrEqual, selector.Offset);
		}

		internal Selector[] InternSelectors(ReadOnlySpan<KeySelector> selectors)
		{
			var res = new Selector[selectors.Length];
			for (int i = 0; i < selectors.Length; i++)
			{
				res[i] = InternSelector(in selectors[i]);
			}
			return res;
		}

	}
}
