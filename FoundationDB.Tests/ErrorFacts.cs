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
	using FoundationDB.Client.Native;

	[TestFixture]
	[Category("Fdb-Client-InProc")]
	[Parallelizable(ParallelScope.All)]
	public class ErrorFacts : FdbSimpleTest
	{

		[Test]
		public void Test_Fdb_GetErrorMessage()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbNative.GetErrorMessage(FdbError.Success), Is.EqualTo("Success"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.OperationFailed), Is.EqualTo("Operation failed"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionTooOld), Is.EqualTo("Transaction is too old to perform reads or be committed"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.FutureVersion), Is.EqualTo("Request for future version"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.TimedOut), Is.EqualTo("Operation timed out"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.NotCommitted), Is.EqualTo("Transaction not committed due to conflict with another transaction"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.CommitUnknownResult), Is.EqualTo("Transaction may or may not have committed"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionCancelled), Is.EqualTo("Operation aborted because the transaction was cancelled"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.TransactionTimedOut), Is.EqualTo("Operation aborted because the transaction timed out"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.ClientInvalidOperation), Is.EqualTo("Invalid API call"));
				Assert.That(FdbNative.GetErrorMessage(FdbError.LargeAllocFailed), Is.EqualTo("Large block allocation failed"));
			});
		}

		[Test]
		public void Test_Fdb_MapToException()
		{
			Assert.Multiple(() =>
			{
				Assert.That(FdbNative.MapToException(FdbError.Success), Is.Null, "Success");
				Assert.That(FdbNative.MapToException(FdbError.OperationFailed), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.OperationFailed));
				Assert.That(FdbNative.MapToException(FdbError.TransactionTooOld), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionTooOld));
				Assert.That(FdbNative.MapToException(FdbError.FutureVersion), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.FutureVersion));
				Assert.That(FdbNative.MapToException(FdbError.TimedOut), Is.InstanceOf<TimeoutException>(), "TimedOut");
				Assert.That(FdbNative.MapToException(FdbError.NotCommitted), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.NotCommitted));
				Assert.That(FdbNative.MapToException(FdbError.CommitUnknownResult), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.CommitUnknownResult));
				Assert.That(FdbNative.MapToException(FdbError.TransactionCancelled), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionCancelled)); //REVIEW => OperationCancelledException?
				Assert.That(FdbNative.MapToException(FdbError.TransactionTimedOut), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.TransactionTimedOut)); //REVIEW => TimeoutException ?
				Assert.That(FdbNative.MapToException(FdbError.ClientInvalidOperation), Is.InstanceOf<FdbException>().And.Property("Code").EqualTo(FdbError.ClientInvalidOperation)); //REVIEW => InvalidOperationException?
				Assert.That(FdbNative.MapToException(FdbError.LargeAllocFailed), Is.InstanceOf<OutOfMemoryException>());
				Assert.That(FdbNative.MapToException(FdbError.InvalidOption), Is.InstanceOf<ArgumentException>());
			});
		}

		[Test]
		public void Test_Fdb_Error_Predicate()
		{
			// Retryable

			Assert.Multiple(() =>
			{
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.NotCommitted), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.FutureVersion), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.TransactionTooOld), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.ApiVersionInvalid), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.Retryable, FdbError.CommitUnknownResult), Is.True); // may have committed => true (but check on retries!)
			});

			// MaybeCommitted

			Assert.Multiple(() =>
			{
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.NotCommitted), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.FutureVersion), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.TransactionTooOld), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.ApiVersionInvalid), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.MaybeCommitted, FdbError.CommitUnknownResult), Is.True); // may have committed => true
			});

			// RetryableNotCommited

			Assert.Multiple(() =>
			{
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.NotCommitted), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.FutureVersion), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.TransactionTooOld), Is.True);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.ApiVersionInvalid), Is.False);
				Assert.That(FdbNative.TestErrorPredicate(FdbErrorPredicate.RetryableNotCommited, FdbError.CommitUnknownResult), Is.False); // may have committed => false
			});

		}

	}
}
