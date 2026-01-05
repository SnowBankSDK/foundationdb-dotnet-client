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

namespace FoundationDB.Layers.Experimental.Indexing
{

	/// <summary>View that reads the indexes of all the set bits in a bitmap</summary>
	public class CompressedBitmapBitView : IEnumerable<long>
	{

		private readonly CompressedBitmap m_bitmap;

		public CompressedBitmapBitView(CompressedBitmap bitmap)
		{
			Contract.NotNull(bitmap);
			m_bitmap = bitmap;
		}

		public CompressedBitmap Bitmap => m_bitmap;

		public IEnumerator<long> GetEnumerator()
		{
			long offset = 0;
			foreach (var word in m_bitmap)
			{
				if (word.IsLiteral)
				{
					int value = word.Literal;
					if (value > 0)
					{
						for (int i = 0; i < 31; i++)
						{
							if ((value & (1 << i)) != 0) yield return offset + i;
						}
					}
					offset += 31;
				}
				else if (word.FillBit == 0)
				{ // skip it
					offset += word.FillCount * 31;
				}
				else
				{ // all ones
					int n = word.FillCount * 31;
					for (int i = 0; i < n; i++)
					{
						yield return offset + i;
					}
					offset += n;
				}
			}
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.GetEnumerator();

	}

}
