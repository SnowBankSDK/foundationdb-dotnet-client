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

namespace FoundationDB.Storage.FdbLite.Tests
{
	using FoundationDB.Storage.FdbLite;

	[TestFixture]
	[Category("FdbLite")]
	public class FdbLiteGraftFacts : SimpleTest
	{

		[Test]
		public void Test_Import_Options_Fill_Ceiling_Follows_Volatility_Class()
		{
			const int PAGE = 65536;

			// the ceilings are the ones MergedFillCeiling already uses for merged runs, so that a grafted page
			// and a vacuum-merged page of the same declared class come out the same density
			Assert.That(FdbLiteImportOptions.Default.Volatility, Is.EqualTo(FdbLiteVolatilityClass.Stable));
			Assert.That(FdbLiteImportOptions.Default.FillCeiling(PAGE), Is.EqualTo(PAGE), "Stable packs full");

			var occasional = FdbLiteImportOptions.Default with { Volatility = FdbLiteVolatilityClass.Occasional };
			Assert.That(occasional.FillCeiling(PAGE), Is.EqualTo((PAGE * 9) / 10));

			var volatile_ = FdbLiteImportOptions.Default with { Volatility = FdbLiteVolatilityClass.Volatile };
			Assert.That(volatile_.FillCeiling(PAGE), Is.EqualTo((PAGE * 85) / 100));
		}

	}

}
