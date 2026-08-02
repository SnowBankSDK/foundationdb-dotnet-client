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

namespace FoundationDB.Storage.FdbLite
{

	/// <summary>How likely imported data is to be written again, which selects the fill ceiling its pages are packed to.</summary>
	/// <remarks>
	/// <para>The engine normally EARNS this classification by observation: only an interior insert bumps a page's
	/// volatility episode count, so a page that has never been written into carries 0. An import knows the answer up
	/// front and says so, which is the one thing about the data that no commit-time heuristic can infer.</para>
	/// <para>The values match the episode counts <c>MergedFillCeiling</c> keys off, so a grafted page and a
	/// vacuum-merged page of the same class come out at the same density.</para>
	/// </remarks>
	public enum FdbLiteVolatilityClass
	{
		/// <summary>Write-once data (an archive restore). Pages pack full.</summary>
		Stable = 0,

		/// <summary>Expected to take occasional writes. Pages keep 10% headroom.</summary>
		Occasional = 1,

		/// <summary>Expected to be churned (restoring a live dataset that resumes taking traffic). Pages keep 15% headroom.</summary>
		Volatile = 2,
	}

	/// <summary>Knobs for one bulk import.</summary>
	public readonly record struct FdbLiteImportOptions
	{

		/// <summary>Declared future mutability of the imported data.</summary>
		public FdbLiteVolatilityClass Volatility { get; init; }

		/// <summary>Bytes buffered before a chunk is applied and the buffer reused. 0 means unbounded.</summary>
		/// <remarks>Each chunk is its own generation, so this knob sits directly on the retention curve: the store's
		/// steady-state footprint is the live tree plus twice one chunk's churn. A large chunk leaves the file near
		/// three times the tree until the frees promote; a small one pays in total write volume.</remarks>
		public long ChunkSizeBytes { get; init; }

		public static FdbLiteImportOptions Default => new() { Volatility = FdbLiteVolatilityClass.Stable };

		/// <summary>Bytes of live content a grafted page is packed to, for a page of <paramref name="pageSize"/>.</summary>
		public int FillCeiling(int pageSize) => this.Volatility switch
		{
			FdbLiteVolatilityClass.Stable => pageSize,
			FdbLiteVolatilityClass.Occasional => (pageSize * 9) / 10,
			_ => (pageSize * 85) / 100,
		};

	}

}
