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

namespace FoundationDB.Layers.Experimental.Indexing.Tests
{
	using System.Runtime.CompilerServices;

	/// <summary>Super Naive and Slow reference implementation of a 'Compressed' Bitmap</summary>
	/// <remarks>This is basically a bool[] that is used to verify other implementations</remarks>
	[DebuggerDisplay("{LowestBit}..{HighestBit}")]
	public sealed class SuperSlowUncompressedBitmap : IEquatable<SuperSlowUncompressedBitmap>, IEnumerable<bool>
	{

		public bool[] Bits;
		public long LowestBit = long.MaxValue;
		public long HighestBit = -1;

		public SuperSlowUncompressedBitmap()
		{
			this.Bits = [ ];
		}

		public SuperSlowUncompressedBitmap(CompressedBitmap bitmap)
		{
			int p = 0;
			var bits = new bool[bitmap.Bounds.Highest + 32];
			foreach (var word in bitmap)
			{
				if (word.IsLiteral)
				{
					var w = word.Literal;
					int n = 31;
					while (n-- > 0)
					{
						if ((w & 1) == 1) bits[p] = true;
						++p;
						w >>= 1;
					}
				}
				else if (word.FillBit == 1)
				{
					//int n = word.FillCount;
					throw new NotImplementedException();
				}
			}
			this.Bits = bits;
			this.LowestBit = bitmap.Bounds.Lowest;
			this.HighestBit = bitmap.Bounds.Highest;
		}

		internal SuperSlowUncompressedBitmap(bool[] bits, int lowest, int highest)
		{
			this.Bits = bits;
			this.LowestBit = lowest;
			this.HighestBit = highest;
		}

		public bool this[int index]
		{
			get => Test(index);
			set { if (value) Set(index); else Clear(index); }
		}

		public bool Test(long index)
		{
			Assert.That(index, Is.GreaterThanOrEqualTo(0));
			return index < this.Bits.Length && this.Bits[index];
		}

		public bool Set(long index)
		{
			Assert.That(index, Is.GreaterThanOrEqualTo(0));

			if (this.HighestBit < 0)
			{
				this.HighestBit = index;
				this.LowestBit = index;
			}
			else
			{
				if (index > this.HighestBit) this.HighestBit = index;
				if (index < this.LowestBit) this.LowestBit = index;
			}

			if (index >= this.Bits.Length)
			{
				Array.Resize(ref this.Bits, checked((int) (index + 1)));
			}
			else
			{
				if (this.Bits[index]) return false;
			}
			this.Bits[index] = true;
			return true;
		}

		public bool Clear(int index)
		{
			Assert.That(index, Is.GreaterThanOrEqualTo(0));

			var bits = this.Bits;

			if (index >= bits.Length) return false;

			if (!bits[index]) return false;

			bits[index] = false;
			if (index == this.HighestBit)
			{
				// need to recompute highestbit

				this.HighestBit = FindHighestBit(bits, index);
				//Console.WriteLine("new highest {0}", this.HighestBit);
			}

			if (index == this.LowestBit)
			{
				this.LowestBit = FindLowestBit(bits, index);
				//Console.WriteLine("new lowest {0}", this.LowestBit);
			}

			return true;
		}

		/// <summary>Count the number of bits set to 1 in this bitmap</summary>
		public int CountBits()
		{
			int count = 0;
			foreach(var bit in this.Bits)
			{
				if (bit) ++count;

			}
			return count;
		}

		private static long FindLowestBit(bool[] bits, int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (bits[i]) return i;
			}
			return long.MaxValue;
		}

		private static long FindHighestBit(bool[] bits, int count)
		{
			int p = count - 1;
			while (p >= 0)
			{
				if (bits[p]) return p;
				--p;
			}
			return -1;
		}

		private static uint GetLiteralAt(bool[] bits, int p)
		{
			int n = bits.Length;
			if (p >= n) return 0;
			uint w = 0;
			uint m = 1;
			for (int i = 0; i < 31; i++)
			{
				if (p < n && bits[p++]) w |= m;
				m <<= 1;
			}
			return w;
		}

		public CompressedBitmap ToBitmap()
		{
			var writer = new CompressedBitmapWriter();

			int p = 0;
			var bits = this.Bits;

			while (p < bits.Length)
			{
				writer.Write(GetLiteralAt(bits, p));
				p += 31;
			}

			return writer.GetBitmap();
		}

		public string ToBitString()
		{

			var bits = this.Bits;
			var sb = new StringBuilder(bits.Length + 30);
			int hsb = 0;
			for (int i = 0; i < bits.Length; i++)
			{
				if (bits[i])
				{
					sb.Append('1');
					hsb = i;
				}
				else
				{
					sb.Append('0');
				}
			}
			int m = ((hsb + 30) / 31) * 31;
			while (sb.Length < m) sb.Append('0');
			return sb.ToString(0, m);
		}

		public static SuperSlowUncompressedBitmap FromBitString(string bitString)
		{
			var bits = new List<bool>(bitString.Length);
			int hsb = 0;
			int lsb = int.MaxValue;
			foreach(var c in bitString)
			{
				if (c == '1')
				{
					bits.Add(true);
					hsb = bits.Count;
					if (lsb == int.MaxValue) lsb = bits.Count;
				}
				else if (c == '0')
				{
					bits.Add(false);
				}
				//else ignore
			}
			return new(bits.ToArray(), lsb, hsb);
		}

		public StringBuilder Dump(StringBuilder? sb = null)
		{
			return Dump(this.Bits, sb);
		}

		public static StringBuilder Dump(bool[] bits, StringBuilder? sb = null)
		{
			sb ??= new StringBuilder();

			int x = 0;
			for (int p = 0; p <= bits.Length; p += 31)
			{
				if (x == 0) sb.AppendInvariant($"{p,7} ");
				else if ((x & 7) == 0) sb.AppendInvariant($"\r\n{p,7} ");
				else sb.Append(' ');

				bool noBits = true;
				for (int i = 0; i < 31; i++)
				{
					if (p + i >= bits.Length)
					{
						sb.Append('?');
						noBits = false;
					}
					else if (bits[p + i])
					{
						sb.Append('#');
						noBits = false;
					}
					else
					{
						sb.Append('_');
					}
				}
				if (noBits) sb.Remove(sb.Length - 31, 31).Append('.', 31);
				x++;
			}

			return sb;
		}


		public override bool Equals(object? obj) => obj is SuperSlowUncompressedBitmap b && Equals(b);

		public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);

		public bool Equals(SuperSlowUncompressedBitmap? other)
		{
			if (ReferenceEquals(other, this)) return true;
			if (other == null) return false;

			var mBits = this.Bits;
			var oBits = other.Bits;
			int n = Math.Max(mBits.Length, oBits.Length);

			for (int i = 0; i < n; i++)
			{
				if ((i < mBits.Length && mBits[i]) != (i < oBits.Length && oBits[i]))
				{
					return false;
				}
			}
			return true;
		}

		public bool[] ToBooleanArray()
		{
			var tmp = new bool[this.HighestBit + 1];
			Array.Copy(this.Bits, 0, tmp, 0, tmp.Length);
			return tmp;
		}

		public IEnumerator<bool> GetEnumerator()
		{
			return ((IList<bool>)ToBooleanArray()).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return this.GetEnumerator();
		}
	}


}

