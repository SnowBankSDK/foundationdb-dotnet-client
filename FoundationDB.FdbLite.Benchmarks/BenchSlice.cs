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

namespace FoundationDB.FdbLite.Benchmarks
{

	/// <summary>Slice selection for the disk-heavy series: <c>--geometry=</c> / <c>--tier=</c> restrict the Params matrix to one cell per invocation, so a full matrix is assembled from short runs instead of one hour-long run (a cell disturbed by background activity is then re-run alone).</summary>
	public static class BenchSlice
	{

		/// <summary>Selected geometry (<see langword="null"/> = all); <c>split</c> is accepted as an alias of <c>b16K/p64K</c>.</summary>
		public static string? Geometry { get; set; }

		/// <summary>Selected thermal tier (<see langword="null"/> = all).</summary>
		public static string? Tier { get; set; }

		/// <summary>Filters a Params value list down to the selected slice; unrestricted when nothing was selected.</summary>
		/// <exception cref="ArgumentException">If the selected value matches nothing in the list (a typo would otherwise silently run the full matrix).</exception>
		public static IEnumerable<string> Filter(string[] all, string? selected)
		{
			if (selected is null) return all;
			if (string.Equals(selected, "split", StringComparison.OrdinalIgnoreCase)) selected = "b16K/p64K";
			var matches = all.Where(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase)).ToArray();
			return matches.Length > 0 ? matches : throw new ArgumentException($"unknown slice value '{selected}' (expected one of: {string.Join(", ", all)})");
		}

	}

}
