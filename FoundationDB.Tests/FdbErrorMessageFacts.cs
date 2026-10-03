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

		[OneTimeSetUp]
		public void PinNativeLibrary()
		{
			// pin the client library this repo redistributes before the first interop call: default probing
			// can fall back to an older system-wide install (ex: a 7.3 client from PATH), and the sweep would
			// then compare the table against the wrong client's messages
			var probe = FdbClientNativeExtensions.ProbeNativeLibraryPaths();
			if (probe.Path != null)
			{
				Fdb.Options.NativeLibPath = probe.Path;
			}
		}

		/// <summary>Codes removed in fdb_c 8.0: <c>fdb_get_error</c> returns <c>UNKNOWN_ERROR</c> for them, and 7.x clients and clusters still use them</summary>
		private static readonly HashSet<int> RemovedAt800 =
		[
			1003, 1057, 1061, 1063, 1064, 1065, 1067, 1077, 1079, 1223, 1225,
			2027, 2028, 2029, 2036, 2037, 2045,
			2130, 2131, 2132, 2133, 2134, 2135, 2136, 2138, 2139, 2140, 2141, 2142, 2143, 2144,
			2160, 2161, 2162, 2163, 2164, 2165, 2166, 2167, 2168, 2169, 2170, 2171, 2172, 2173, 2174, 2175,
		];

		/// <summary>Codes added in fdb_c 8.0: a 7.x <c>fdb_get_error</c> returns <c>UNKNOWN_ERROR</c> for them</summary>
		private static readonly HashSet<int> AddedAt800 = [ 1251 ];

		/// <summary>Codes with a new message in fdb_c 8.0 (the table has the 7.x text)</summary>
		private static readonly Dictionary<int, string> RewordedAt800 = new()
		{
			[2117] = "Api call through special keys failed. For more information, call get - within the same transaction - on special key 0xff0xff/error_message to get a json string of the error message.",
		};

		/// <summary>Every code of this build's <see cref="FdbError"/> enum answers from the table, with the exact <c>fdb_get_error</c> text</summary>
		[Test]
		public void Test_Managed_Message_Table_Matches_The_Native_Client()
		{
			var seen = new HashSet<int>();
			bool client800 = Fdb.GetMaxApiVersion() >= 800;
			Assert.Multiple(() =>
			{
				foreach (var code in Enum.GetValues(typeof(FdbError)).Cast<FdbError>().OrderBy(c => (int) c))
				{
					if (!seen.Add((int) code)) continue;
					if (client800 && RemovedAt800.Contains((int) code))
					{
						Assert.That(FdbErrorDebugger.GetErrorMessage(code), Is.EqualTo("UNKNOWN_ERROR"), $"native message for {code} ({(int) code}), removed in 8.0");
						continue;
					}
					if (!client800 && AddedAt800.Contains((int) code))
					{
						Assert.That(FdbErrorDebugger.GetErrorMessage(code), Is.EqualTo("UNKNOWN_ERROR"), $"native message for {code} ({(int) code}), added in 8.0");
						continue;
					}
					if (client800 && RewordedAt800.TryGetValue((int) code, out var reworded))
					{
						Assert.That(FdbErrorDebugger.GetErrorMessage(code), Is.EqualTo(reworded), $"native message for {code} ({(int) code}), reworded in 8.0");
						continue;
					}
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
