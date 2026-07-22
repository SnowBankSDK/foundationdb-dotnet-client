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

	/// <summary>Pins the native client's error-translation answers through <see cref="FdbErrorDebugger"/>: the oracle every emulator error behavior is held against.</summary>
	/// <remarks>These facts need the native client library (they call fdb_c's error APIs, no cluster involved); they self-skip where it is not deployed.</remarks>
	[TestFixture]
	[Category("Fdb-Coverage")]
	public class FdbErrorDebuggerFacts : SimpleTest
	{

		[SetUp]
		public void CheckNativeLibrary()
		{
			try
			{
				_ = FdbErrorDebugger.GetErrorMessage(FdbError.Success);
			}
			catch (DllNotFoundException)
			{
				Assert.Ignore("The native client library (fdb_c) is not deployed on this machine.");
			}
		}

		[Test]
		public void Test_Can_Get_Error_Messages()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbErrorDebugger.GetErrorMessage(FdbError.Success), Is.EqualTo("Success"));
				Assert.That(FdbErrorDebugger.GetErrorMessage(FdbError.NotCommitted), Is.EqualTo("Transaction not committed due to conflict with another transaction"));
			});
		}

		[Test]
		[CoversCells("errors/not-committed", "errors/transaction-too-old")]
		public void Test_Retryable_Predicate_Matches_The_Emulator_Retry_Set()
		{
			// the FakeDb retry loop retries exactly on NotCommitted / TransactionTooOld / FutureVersion: the native predicate must agree
			Assert.Multiple(() =>
			{
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.NotCommitted), Is.True);
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.TransactionTooOld), Is.True);
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.FutureVersion), Is.True);
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.ClientInvalidOperation), Is.False);
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.CommitUnknownResult), Is.True);
				Assert.That(FdbErrorDebugger.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.NotCommitted), Is.False);
			});
		}

		[Test]
		public void Test_Exceptions_Map_From_Error_Codes()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbErrorDebugger.MapToException(FdbError.Success), Is.Null);
				Assert.That(FdbErrorDebugger.MapToException(FdbError.TimedOut), Is.InstanceOf<TimeoutException>());
				var ex = FdbErrorDebugger.MapToException(FdbError.NotCommitted);
				Assert.That(ex, Is.InstanceOf<FdbException>());
				Assert.That(((FdbException) ex!).Code, Is.EqualTo(FdbError.NotCommitted));
			});
		}

	}

}
