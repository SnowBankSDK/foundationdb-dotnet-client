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

	/// <summary>A native client connected to a server of another major.minor version never completes a transaction</summary>
	/// <remarks>Run with a server image of another branch than the native client, ex: an 8.0 client with <c>FDB_TEST_DOCKER_TAG=7.4.10</c>.</remarks>
	[TestFixture]
	[Explicit("Requires a server image of another major.minor version than the native client")]
	[Category("Fdb-Mismatch-Server")]
	public sealed class FdbServerVersionMismatchFacts : FdbTest
	{

		protected override bool AllowServerVersionMismatch => true;

		[Test]
		public async Task Test_Transaction_Times_Out_When_The_Server_Version_Differs_From_The_Client()
		{
			var client = Version.Parse(Fdb.GetClientVersion().Version);
			var server = this.GetLocalServer().Tag;
			Log($"Native client {client}, server image {server}");
			Assume.That(Version.TryParse(server, out var serverVersion) && (serverVersion.Major != client.Major || serverVersion.Minor != client.Minor), "This test requires a server of another major.minor version than the native client.");

			using var db = await OpenTestDatabaseAsync();

			var error = (await Assert.ThrowsAsync<FdbException>(() => db.ReadAsync(tr =>
			{
				tr.Options.Timeout = 5_000; // ms
				return tr.GetReadVersionAsync();
			}, this.Cancellation)))!;
			Assert.That(error.Code, Is.EqualTo(FdbError.TransactionTimedOut));
		}

	}

	/// <summary>An API version above the maximum of the native client fails at <see cref="Fdb.Start(int)"/></summary>
	/// <remarks>The API version is selected once per process: run this fixture alone, with <c>FDB_TEST_API_VERSION</c> above the maximum of the client (ex: 800 with a 7.4 client).</remarks>
	[TestFixture]
	[Explicit("Requires an API version above the maximum of the native client, in a process of its own")]
	[Category("Fdb-Mismatch-Api")]
	public sealed class FdbApiVersionMismatchFacts
	{

		[Test]
		public void Test_Start_Fails_When_The_Api_Version_Exceeds_The_Native_Client()
		{
			Assume.That(Fdb.ApiVersion, Is.Zero, "The API version is selected once per process: run this fixture alone.");
			Assume.That(int.TryParse(Environment.GetEnvironmentVariable(FdbTest.ApiVersionVariable), out int requested), $"This test requires {FdbTest.ApiVersionVariable}.");

			var path = FdbTest.ProbeNativeLibrary(out var failure);
			Assert.That(path, Is.Not.Null, failure);
			Fdb.Options.NativeLibPath = path;

			int max = Fdb.GetMaxApiVersion();
			Assume.That(requested, Is.GreaterThan(max), $"The native client supports API version {max}, and {FdbTest.ApiVersionVariable} must be higher.");

			Assert.That(() => Fdb.Start(requested), Throws.ArgumentException.With.Message.Contains($"supports API version {max} at most"));
		}

	}

}
