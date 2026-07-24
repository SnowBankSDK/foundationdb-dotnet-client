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

// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable AccessToDisposedClosure
// ReSharper disable ReplaceAsyncWithTaskReturn

namespace FoundationDB.Client.Tests
{
	using FoundationDB.Testing;

	/// <summary>Retry-loop conformance suite (db.ReadAsync / WriteAsync / ReadWriteAsync semantics), shared by every backend head below.</summary>
	public abstract class RetryableConformanceFacts : FdbTest
	{
		[Test]
		public async Task Test_ReadAsync_Should_Normally_Execute_Only_Once()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			string secret = Guid.NewGuid().ToString();

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("Hello"), Text(secret));
			}, this.Cancellation);

			int called = 0;
			var result = await db.ReadAsync<Slice>(async (tr) =>
			{
				++called;
				Assert.That(tr, Is.Not.Null);
				Assert.That(tr.Context, Is.Not.Null);
				Assert.That(tr.Context.Database, Is.SameAs(db));
				// (the original suite also asserts the internal tr.Context.Shared flag; internals are not visible to this project)

				var subspace = await location.Resolve(tr);
				return await tr.GetAsync(subspace.Key("Hello"));
			}, this.Cancellation);

			Assert.That(called, Is.EqualTo(1)); // note: if this assertion fails, first ensure that you did not get a transient error while running this test!
			Assert.That(result.ToUnicode(), Is.EqualTo(secret));
		}

		[Test]
		public async Task Test_Retryable_Rethrows_Regular_Exceptions()
		{
			using var db = await OpenTestPartitionAsync();

			int called = 0;

			// ReadAsync should return a failed Task, and not bubble up the exception.
			var task = db.ReadAsync<int>((_) =>
			{
				Assert.That(called, Is.Zero, "ReadAsync should not retry on regular exceptions");
				++called;
				throw new InvalidOperationException("Boom");
			}, this.Cancellation);
			Assert.That(task, Is.Not.Null);
			// the exception should be unwrapped (ie: we should not see an AggregateException, but the actual exception)
			Assert.That(async () => await task, Throws.InstanceOf<InvalidOperationException>(), "ReadAsync should rethrow any regular exceptions");
		}

		[Test]
		public async Task Test_Retryable_Retries_On_Transient_Errors()
		{
			using var db = await OpenTestPartitionAsync();
			using var go = new CancellationTokenSource();

			int called = 0;
			int? id = null;

			// ReadAsync should return a failed Task, and not bubble up the exception.
			var res = await db.ReadAsync((tr) =>
			{
				try
				{
					++called;

					Assert.That(tr, Is.Not.Null);
					Assert.That(tr.Context, Is.Not.Null, "tr.Context should not be null");
					Assert.That(tr.Context.Retries, Is.EqualTo(called - 1), "tr.Context.Retries should equal the number of calls to the handler, minus one");

					id ??= tr.Id;
					Assert.That(tr.Id, Is.EqualTo(id.Value), "The same transaction should be passed multiple times");

					if (called < 3)
					{ // fool the retry loop into thinking that a retryable error occurred
						throw new FdbException(FdbError.TransactionTooOld, "Fake error");
					}
					Assert.That(called, Is.GreaterThanOrEqualTo(3), "The handler should not be called again if it completed previously");
					return Task.FromResult(123);
				}
				catch(AssertionException)
				{ // protection against infinite loops
					go.Cancel();
					throw;
				}
			}, go.Token);

			Assert.That(res, Is.EqualTo(123));
			Assert.That(called, Is.EqualTo(3));
		}

		[Test]
		public async Task Test_Retryable_Should_Not_Execute_If_Already_Canceled()
		{
			using var db = await OpenTestPartitionAsync();
			using var go = new CancellationTokenSource();
			go.Cancel();

			bool called = false;

			// ReadAsync should return a canceled Task, and never call the handler
			var t = db.ReadAsync<int>((_) =>
			{
				called = true;
				Log("FAILED");
				throw new InvalidOperationException("Failed");
			}, go.Token);

			Assert.That(t.IsCompleted, "Returned task should already be canceled");
			Assert.That(t.Status, Is.EqualTo(TaskStatus.Canceled), "Returned task should be in the canceled state");
			Assert.That(called, Is.False, "Handler should not be called with an already canceled token");
			_ = t.Exception;
		}

		[Test]
		public async Task Test_Retryable_ReadOnly_Should_Deny_Write_Attempts()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			var t = db.ReadAsync(async (tr) =>
			{
				Assert.That(tr, Is.Not.Null);

				// force the read-only into a writable interface
				var hijack = (tr as IFdbTransaction)!;
				Assume.That(hijack, Is.Not.Null, "This test requires the transaction to implement IFdbTransaction !");

				var subspace = await location.Resolve(tr);

				// this call should fail !
				hijack.Set(subspace.Key("Hello"), Text("Hijacked"));

				Assert.Fail("Calling Set() on a read-only transaction should fail");
				return Task.FromResult(123);
			}, this.Cancellation);

			Assert.That(async () => await t, Throws.InstanceOf<InvalidOperationException>(), "Forcing writes on a read-only transaction should fail");
		}

		[Test]
		public async Task Test_ReadOnly_Retryable_Do_Not_See_Changes_From_Other_Transactions()
		{
			// A read-only transaction will never see changes that have been committed after its read-version, whether it uses Snapshot reads or not.

			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			// setup the keys from 0 to 9
			await db.WriteAsync(async (tr) =>
			{
				var subspace = await location.Resolve(tr);
				for (int i = 0; i < 10; i++)
				{
					tr.Set(subspace.Key(i), Slice.FromInt32(i));
				}
			}, this.Cancellation);


			// simulate a slow scan that will sequentially read the values, while they are modified by other transactions in the background

			var results = await db.ReadAsync(async (tr) =>
			{
				var subspace = await location.Resolve(tr);

				var values = new int[10];

				// read 0..2
				for (int i = 0; i < 3; i++)
				{
					values[i] = (await tr.GetAsync(subspace.Key(i))).ToInt32();
				}

				// another transaction commits a change to 3 before we read it
				await db.WriteAsync((tr2) => tr2.Set(subspace.Key(3), FdbValue.ToCompactLittleEndian(42)), this.Cancellation);

				// read 3 to 7
				for (int i = 3; i < 7; i++)
				{
					values[i] = (await tr.GetAsync(subspace.Key(i))).ToInt32();
				}

				// another transaction commits a change to 6 after it has been read
				await db.WriteAsync((tr2) => tr2.Set(subspace.Key(6), FdbValue.ToCompactLittleEndian(66)), this.Cancellation);

				// read 7 to 9
				for (int i = 7; i < 10; i++)
				{
					values[i] = (await tr.GetAsync(subspace.Key(i))).ToInt32();
				}

				return values;
			}, this.Cancellation);

			// The values of 3 and 6 should be the initial values
			Assert.That(results, Is.EqualTo(Enumerable.Range(0, 10).ToList()));
		}

		[Test]
		public async Task Test_Retryable_Execute_Success_Handler_Only_Once()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			string secret = Guid.NewGuid().ToString();

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("Hello"), Text(secret));
			}, this.Cancellation);

			int called = 0;
			var result = await db.ReadWriteAsync(
				async (tr) =>
				{
					if (tr.Context.Retries == 0) throw new FdbException(FdbError.NotCommitted, "Fake Not Committed!");
					var subspace = await location.Resolve(tr);
					tr.Set(subspace.Key("World"), Slice.Empty);
					return await tr.GetAsync(subspace.Key("Hello"));
				},
				(tr, res) =>
				{
					called++;
					Assert.That(tr.Context.Retries == 1, "Transaction should only have retried once!");
					Assert.That(res.ToUnicode(), Is.EqualTo(secret), "Argument passed to success callback does not match expected value.");
				},
				this.Cancellation);

			Assert.That(called, Is.EqualTo(1), "Success callback should only have been called once");
			Assert.That(result.ToUnicode(), Is.EqualTo(secret), "Result does not match expected value.");
		}

		[Test]
		public async Task Test_Retryable_Never_Execute_Success_Handler_If_Failed()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			string secret = Guid.NewGuid().ToString();

			await db.WriteAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				tr.Set(subspace.Key("Hello"), Text(secret));
			}, this.Cancellation);

			int called = 0;
			Assert.That(
				async () => await db.ReadWriteAsync<Slice>(
					(_) => throw new InvalidOperationException("KAPOW!"),
					(_, _) =>
					{
						called++;
						Assert.Fail("Success callback should never have been called!");
					},
					this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("KAPOW!"),
				"Success callback should only have been called once"
			);

			Assert.That(called, Is.Zero, "Success callback should never have been called!");
		}

		[Test]
		public async Task Test_Retryable_Mutating_Transaction_In_Success_Handler_Should_Fail()
		{
			// Verify that attempting to keep using the transaction instance in the success handler will throw and InvalidOperationException

			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			// Cannot set a key after commit
			//note: we assume that nobody will move the directories during the test execution!
			Slice key = await db.ReadAsync(async tr => (await location.Resolve(tr)).Key("hello").ToSlice(), this.Cancellation);
			Log("Using key: " + key);

			Assert.That(
				async () => await db.WriteAsync(
					(tr) => tr.Set(key, Text("Set")),
					(tr) => tr.Set(key, Slice.Empty),
					this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
				"Trying to write in success handler should fail"
			);
			//note: since the transaction is already committed, we should observe its result
			Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Set")));

			// Cannot double-commit!
			Assert.That(
				async () => await db.WriteAsync(
					(tr) => tr.Set(key, Text("Commit")),
					(tr) => tr.CommitAsync(),
					this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
				"Trying to double-Commit in success handler should fail"
			);
			//note: since the transaction is already committed, we should observe its result
			Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Commit")));

			// Cannot read a key after commit
			Assert.That(
				async () => await db.ReadWriteAsync( //TODO: replace with a simpler variant!
					(tr) =>
					{
						tr.Set(key, Text("Get"));
						return Task.CompletedTask;
					},
					async (tr) => await tr.GetAsync(key),
					this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
				"Trying to read a key in success handler should fail"
			);
			//note: since the transaction is already committed, we should observe its result
			Assert.That(await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation), Is.EqualTo(Slice.FromString("Get")));

			// GetCommitVersion() is allowed to be executed AFTER the commit!
			var cv = await db.ReadWriteAsync( //TODO: replace with a simpler variant!
				(tr) =>
				{
					tr.Set(key, Text("GetCommitVersion"));
					return Task.CompletedTask;
				},
				(tr) => tr.GetCommittedVersion(),
				this.Cancellation
			);
			Assert.That(cv, Is.GreaterThan(0));
			Assert.That(
				await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation),
				Is.EqualTo(Slice.FromString("GetCommitVersion"))
			);

			// GetReadVersionAsync() is allowed to be executed AFTER the commit!
			var rv = await db.ReadWriteAsync( //TODO: replace with a simpler variant!
				(tr) =>
				{
					tr.Set(key, Text("GetReadVersion"));
					return Task.CompletedTask;
				},
				(tr) => tr.GetReadVersionAsync(),
				this.Cancellation
			);
			Assert.That(rv, Is.GreaterThan(0));
			Assert.That(
				await db.ReadAsync(tr => tr.GetAsync(key), this.Cancellation),
				Is.EqualTo(Slice.FromString("GetReadVersion"))
			);
		}

		[Test]
		public async Task Test_Retryable_GetVersionStamp_Pattern()
		{
			using var db = await OpenTestPartitionAsync();
			var location = db.Root;
			await CleanLocation(db, location);

			// Cannot get version stamp after commit
			Assert.That(
				async () => await db.ReadWriteAsync(
					async (tr) =>
					{
						var subspace = await location.Resolve(tr);
						tr.Set(subspace.Key("Hello"), Text("GetVersionStamp"));
						tr.SetVersionStampedKey(subspace.Key("Hello", tr.CreateVersionStamp()), Slice.Empty);
					},
					(tr) => tr.GetVersionStampAsync(),
					this.Cancellation),
				Throws.InstanceOf<InvalidOperationException>().With.Message.EqualTo("The transaction has already been committed"),
				"Trying to read a key in success handler should fail"
			);
			//note: since the transaction is already committed, we should observe its result
			Assert.That(await db.ReadAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				return await tr.GetAsync(subspace.Key("Hello"));
			}, this.Cancellation), Is.EqualTo(Slice.FromString("GetVersionStamp")));

			// but getting stamp before commit and awaiting it after should work
			VersionStamp st = await db.ReadWriteAsync(
				async (tr) =>
				{
					var subspace = await location.Resolve(tr);
					var prev = await tr.GetAsync(subspace.Key("Hello"));
					tr.Set(subspace.Key("Hello"), Text("GetVersionStamp2"));
					tr.SetVersionStampedKey(subspace.Key("Hello", VersionStamp.Incomplete()), prev);
					return new { Stamp = tr.GetVersionStampAsync() }; //REVIEW: "return tr.GetVersionStampAsync()" will deadlock because 'ReadWrite' will try to await it!
				},
				(_, res) => res.Stamp,
				this.Cancellation
			);
			Assert.That(st.IsIncomplete, Is.False, "Stamp should be completed");
			Assert.That(st.TransactionVersion, Is.Not.Zero.And.Not.EqualTo(ulong.MaxValue), "Stamp should be completed");
			Assert.That(await db.ReadAsync(async tr =>
			{
				var subspace = await location.Resolve(tr);
				return await tr.GetAsync(subspace.Key("Hello"));
			}, this.Cancellation), Is.EqualTo(Slice.FromString("GetVersionStamp2")));
		}

	}

	/// <summary>Runs the retry-loop conformance suite against the FakeDb emulator.</summary>
	[TestFixture]
	public class RetryableFakeDbFacts : RetryableConformanceFacts
	{

		/// <summary>Store shared by all databases opened during a single test, reset between tests.</summary>
		private FakeDbStore? Store { get; set; }

		protected override bool UseRealServer => false;

		[TearDown]
		public void ResetFakeDbStore() => this.Store = null;

		protected override Task<IFdbDatabase> OpenTestDatabaseAsync(bool readOnly = false)
		{
			// mirror FdbTest.OpenTestDatabaseAsync: the real-cluster head seeds a 15s default timeout via FdbConnectionOptions.DefaultTimeout
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(FdbPath.Root, readOnly);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

		protected override Task<IFdbDatabase> OpenTestPartitionAsync(string? testMethod = null)
		{
			var db = (this.Store ??= TestBuggify.ChaosStore()).OpenDatabase(GetTestPartitionPath(testMethod), readOnly: false);
			db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
			return Task.FromResult<IFdbDatabase>(db);
		}

	}

	/// <summary>Runs the retry-loop conformance suite against a real FoundationDB cluster (Testcontainers). Run explicitly from the Unit Test Sessions UI; requires a local Docker daemon.</summary>
	[TestFixture, Explicit("Requires a local Docker daemon"), Category("RealCluster")]
	public class RetryableRealClusterFacts : RetryableConformanceFacts
	{
	}

}
