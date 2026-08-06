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

namespace FoundationDB.Client.Tests
{

	/// <summary>Pins the managed <see cref="FdbErrorMessages"/> table against the deployed native client</summary>
	[TestFixture]
	[Category("Fdb-Client-InProc")]
	public sealed class FdbErrorMessageFacts
	{

		/// <summary>Every code of this build's <see cref="FdbError"/> enum answers from the table, with the exact <c>fdb_get_error</c> text</summary>
		[Test]
		public void Test_Managed_Message_Table_Matches_The_Native_Client()
		{
			var seen = new HashSet<int>();
			Assert.Multiple(() =>
			{
				foreach (var code in Enum.GetValues(typeof(FdbError)).Cast<FdbError>().OrderBy(c => (int) c))
				{
					if (!seen.Add((int) code)) continue;
					Assert.That(FdbErrorMessages.TryGetMessage(code), Is.EqualTo(FdbErrorDebugger.GetErrorMessage(code)), $"table entry for {code} ({(int) code})");
				}
			});
			Assert.That(seen.Count, Is.GreaterThan(300), "the sweep must have covered the whole enum");
		}

		/// <summary>The code-only exception constructor never needs the native client</summary>
		[Test]
		public void Test_Code_Only_Constructor_Uses_The_Table()
		{
			Assert.That(new FdbException(FdbError.NotCommitted).Message, Is.EqualTo("Transaction not committed due to conflict with another transaction"));
			// a code outside the enum falls back to name-and-number formatting (here, the integer literal)
			Assert.That(new FdbException((FdbError) 999999).Message, Is.EqualTo("999999 (999999)"));
		}

	}

}
