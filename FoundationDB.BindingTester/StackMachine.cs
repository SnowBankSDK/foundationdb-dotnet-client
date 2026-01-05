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

// ReSharper disable MethodHasAsyncOverload
namespace FoundationDB.Client.Testing
{
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	public sealed class StackMachine : IDisposable
	{

		public StackMachine(IFdbDatabase db, Slice prefix, ILogger<StackMachine>? log)
		{
			this.Db = db;
			this.Log = log ?? NullLogger<StackMachine>.Instance;
			this.Prefix = prefix;
			this.TransactionName = prefix.ToStringUtf8();
		}
		
		public IFdbDatabase Db { get; }

		private ILogger<StackMachine> Log { get; }

		public FdbTenant? Tenant { get; private set; }

		internal ContextStack<StackItem> Stack { get; } = new();

		public Slice Prefix { get; }

		public string? TransactionName { get; private set; }

		public Dictionary<string, TransactionState> Transactions { get; } = new(StringComparer.Ordinal);

		public long? LastVersion { get; private set; }

		public long DirectoryIndex { get; set; }

		public long ErrorIndex { get; set; }

		private IFdbTransaction CreateNewTransaction(CancellationToken ct)
		{
			var tr = this.Tenant != null ? this.Tenant.BeginTransaction(ct) : this.Db.BeginTransaction(ct);
			tr.Options.WithTransactionLog("CSHARP"); //REVIEW: add the name and/or counter?
			return tr;
		}

		private IFdbTransaction GetCurrentTransaction()
		{
			var name = this.TransactionName;
			if (string.IsNullOrEmpty(name))
			{
				throw new InvalidOperationException("No transaction name specified");
			}
			if (!this.Transactions.TryGetValue(name, out var state))
			{
				throw new InvalidOperationException($"No transaction with name '{name}'");
			}
			if (state.Dead)
			{
				throw new InvalidOperationException($"Transaction '{name}' has already been disposed");
			}
			return state.Transaction;
		}

		private void Push(int index, TestInstruction instr, IVarTuple arg)
		{
			this.Stack.Push(new(instr, arg, index));
		}

		private void Push<T>(int index, TestInstruction instr, T value)
		{
			this.Stack.Push(new(instr, STuple.Create(value), index));
		}

		private void PushFuture<TState>(int index, TestInstruction instr, TState state, Func<TState, Task<IVarTuple>> handler)
		{
			PushFuture(index, instr, handler(state));
		}

		private void PushFuture(int index, TestInstruction instr, Task<IVarTuple> future)
		{
			this.Stack.Push(new(instr, future, index));
		}

		private async ValueTask<T> PopAsync<T>()
		{
			var item = await this.Stack.Pop().Resolve().ConfigureAwait(false);
			return item.As<T>()!;
		}

		private ValueTask<StackResult> PopAsync()
		{
			return this.Stack.Pop().Resolve();
		}

		private ValueTask<StackResult> PeekAsync()
		{
			return this.Stack.Peek().Resolve();
		}

		private async Task<IVarTuple> MapVoidFuture(Task t)
		{
			await t.ConfigureAwait(false);
			return STuple.Create("RESULT_NOT_PRESENT");
		}

		public async Task RunTest(CancellationToken ct)
		{
			var db = this.Db;

			this.Stack.Clear();

			var instructions = await db.QueryAsync(
				tr => tr
					.GetRangeValues(KeyRange.StartsWith(this.Prefix))
					.Select((v) => TestInstruction.Parse(v))
				, ct);

			int index = 0;
			foreach (var instr in instructions)
			{
				await Execute(instr, index, ct);
				++index;
			}
		}

		public async Task Run(TestSuite suite, CancellationToken ct)
		{
			var instructions = suite.Instructions;
			this.Log.LogInformation($"Execute Test '{suite.Name}' with {instructions.Length:N0} instructions");
			for (int i = 0; i < instructions.Length; i++)
			{
				ct.ThrowIfCancellationRequested();
				try
				{
					await Execute(instructions[i], i, ct);
				}
				catch (Exception e)
				{
					this.Stack.Push(new(instructions[i], e, i));
					if (ct.IsCancellationRequested && e is OperationCanceledException)
					{
						throw;
					}
				}
				//Dump();
			}
			this.Log.LogInformation("Test executed");
		}

		public async Task Execute(TestInstruction instr, int index, CancellationToken ct)
		{
			this.Log.LogDebug($"> @{index}: {instr}");

			switch (instr.Code)
			{
				case InstructionCode.Push:
				{ // PUSH <item>
					if (instr.Args == null) throw new InvalidOperationException("Missing argument for PUSH command");
					Push(index, instr, instr.Args);
					break;
				}
				case InstructionCode.Dup:
				{ // DUP
					this.Stack.Dup();
					break;
				}
				case InstructionCode.EmptyStack:
				{ // EMPTY_STACK
					this.Stack.Clear();
					break;
				}
				case InstructionCode.Swap:
				{ // SWAP
					var depth = await PopAsync<int>();
					this.Stack.Swap(0, depth);
					break;
				}
				case InstructionCode.Pop:
				{ // POP
					this.Stack.Pop();
					break;
				}
				case InstructionCode.Sub:
				{ // SUB
					var a = await PopAsync<long>();
					var b = await PopAsync<long>();
					var r = a - b;
					Push(index, instr, r);
					break;
				}
				case InstructionCode.Concat:
				{ // CONCAT
					var a = await PopAsync();
					var b = await PopAsync();
					switch (a.Value[0], b.Value[0])
					{
						case (string strA, string strB):
						{
							var r = strA + strB;
							Push(index, instr, r);
							break;
						}
						case (Slice sliceA, Slice sliceB):
						{
							var r = sliceA + sliceB;
							Push(index, instr, r);
							break;
						}
						default:
						{
							throw new InvalidOperationException("Can only CONCAT byte or unicode strings");
						}
					}
					break;
				}
				case InstructionCode.LogStack:
				{ // LOG_STACK

					var prefix = await PopAsync<Slice>();

					var results = this.Stack.ToArray();
					var stack = new List<(StackItem Item, StackResult Result)>(results.Length);
					foreach (var result in results)
					{
						stack.Add((result, await result.Resolve()));
					}

#if true
					{
						var sb = new StringBuilder();
						sb.AppendLine($"LogStack: {prefix}");
						for(int i = 0; i < stack.Count; i++)
						{
							var item = stack[i].Item;
							var result = stack[i].Result;
							var (k, v) = KeyValuePair.Create(prefix + TuPack.EncodeKey(i, result.Index), TuPack.Pack(result.Value).Truncate(40_000));
							sb.AppendLine($"- {TuPack.Unpack(k)} = {TuPack.Unpack(v)} // {item.Instruction}");
						}
						this.Log.LogInformation(sb.ToString());
					}
#else
					{
						var kvs = stack.Select((entry, i) => KeyValuePair.Create(prefix + TuPack.EncodeKey(i, entry.Result.Index), TuPack.Pack(entry.Result.Value).Truncate(40_000))).ToArray();
						await this.Db.WriteAsync((tr) => tr.SetValues(kvs), ct);
					}
#endif

					this.Stack.Clear();
					break;
				}
				case InstructionCode.NewTransaction:
				{ // NEW_TRANSACTION

					var name = this.TransactionName;
					Contract.Debug.Assert(!string.IsNullOrWhiteSpace(name));

					if (this.Transactions.TryGetValue(name, out var state))
					{
						state.Dispose();
					}

					state = new TransactionState()
					{
						Transaction = CreateNewTransaction(ct),
						Tenant = this.Tenant,
					};

					this.Transactions[name] = state;
					break;
				}
				case InstructionCode.UseTransaction:
				{ // USE_TRANSACTION

					var name = await PopAsync<string>();
					if (string.IsNullOrEmpty(name)) throw new InvalidOperationException("No transaction name specified for USE_TRANSACTION");

					if (!this.Transactions.TryGetValue(name, out var state))
					{
						state = new TransactionState()
						{
							Transaction = CreateNewTransaction(ct),
							Tenant = this.Tenant,
						};

						this.Transactions[name] = state;
					}
					this.TransactionName = name;
					break;
				}
				case InstructionCode.GetReadVersion:
				{
					PushFuture(index, instr, GetCurrentTransaction(),
						async (tr) =>
						{
							var rv = await tr.GetReadVersionAsync();
							this.LastVersion = rv;
							return STuple.Create("GOT_READ_VERSION");
						});
					break;
				}
				case InstructionCode.SetReadVersion:
				{
					var tr = GetCurrentTransaction();
					if (this.LastVersion == null)
					{
						throw new InvalidOperationException("Cannot set read version because without a prior call to GetReadVersion.");
					}
					tr.SetReadVersion(this.LastVersion.Value);
					break;
				}
				case InstructionCode.Reset:
				{
					// RESET
					// Resets the current transaction.
					var tr = GetCurrentTransaction();
					tr.Reset();
					break;
				}
				case InstructionCode.Cancel:
				{
					// CANCEL
					// Cancels the current transaction.
					var tr = GetCurrentTransaction();
					tr.Cancel();
					break;
				}
				case InstructionCode.Commit:
				{
					// COMMIT
					// Commits the current transaction (with no retry behavior). May optionally
					// push a future onto the stack.
					var tr = GetCurrentTransaction();
					var t = tr.CommitAsync();
					PushFuture(index, instr, MapVoidFuture(t));
					break;
				}
				case InstructionCode.OnError:
				{
					// ON_ERROR
					// Pops the top item off of the stack as ERROR_CODE. Passes ERROR_CODE in a
					// language-appropriate way to the on_error method of current transaction
					// object and blocks on the future. If on_error re-raises the error, bubbles
					// the error out as indicated above. May optionally push a future onto the
					// stack.
					var errorCode = await PopAsync<FdbError>();

					var tr = GetCurrentTransaction();
					var t = tr.OnErrorAsync(errorCode);

					PushFuture(index, instr, MapVoidFuture(t));
					break;
				}

				case InstructionCode.Get:
				{
					// GET (_SNAPSHOT, _DATABASE)
					// Pops the top item off of the stack as KEY and then looks up KEY in the
					// database using the get() method. May optionally push a future onto the
					// stack.
					var key = await PopAsync<Slice>();

					Task<Slice> future;
					if (instr.IsDatabase())
					{
						future = this.Db.ReadAsync(tr => instr.IsSnapshot() ? tr.Snapshot.GetAsync(key) : tr.GetAsync(key), ct);
					}
					else
					{
						var tr = GetCurrentTransaction();
						future = instr.IsSnapshot()
							? tr.Snapshot.GetAsync(key)
							: tr.GetAsync(key);
					}

					PushFuture(index, instr, future,
						async (t) =>
						{
							var res = await t.ConfigureAwait(false);
							if (res.IsNull) return STuple.Create("RESULT_NOT_PRESENT");
							return STuple.Create(res);
						});
					break;
				}
				case InstructionCode.GetRange:
				{
					KeySelector begin;
					KeySelector end;
					int limit;
					bool reverse;
					FdbStreamingMode streamingMode;
					if (instr.IsSelector())
					{
						// GET_RANGE_SELECTOR (_SNAPSHOT, _DATABASE)
						// Pops the top ten items off of the stack as BEGIN_KEY, BEGIN_OR_EQUAL,
						// BEGIN_OFFSET, END_KEY, END_OR_EQUAL, END_OFFSET, LIMIT, REVERSE,
						// STREAMING_MODE, and PREFIX. Constructs key selectors BEGIN and END from
						// the first six parameters, and then performs a range read in a language-
						// appropriate way using BEGIN, END, LIMIT, REVERSE and STREAMING_MODE. Output
						// is pushed onto the stack as with GET_RANGE, excluding any keys that do not
						// begin with PREFIX.
						var beginKey = await PopAsync<Slice>();
						var beginOrEqual = await PopAsync<bool>();
						var beginOffset = await PopAsync<int>();
						begin = new KeySelector(beginKey, beginOrEqual, beginOffset);
						var endKey = await PopAsync<Slice>();
						var endOrEqual = await PopAsync<bool>();
						var endOffset = await PopAsync<int>();
						end = new KeySelector(endKey, endOrEqual, endOffset);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}
					else if (instr.IsStartsWith())
					{
						// GET_RANGE_STARTS_WITH (_SNAPSHOT, _DATABASE)
						// Pops the top four items off of the stack as PREFIX, LIMIT, REVERSE and
						// STREAMING_MODE. Performs a prefix range read in a language-appropriate way
						// using these parameters. Output is pushed onto the stack as with GET_RANGE.
						var prefix = await PopAsync<Slice>();
						(begin, end) = KeySelectorPair.StartsWith(prefix);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}
					else
					{
						// GET_RANGE (_SNAPSHOT, _DATABASE)
						// Pops the top five items off of the stack as BEGIN_KEY, END_KEY, LIMIT,
						// REVERSE and STREAMING_MODE. Performs a range read in a language-appropriate
						// way using these parameters. The resulting range of n key-value pairs are
						// packed into a tuple as [k1,v1,k2,v2,...,kn,vn], and this single packed value
						// is pushed onto the stack.
						var beginKey = await PopAsync<Slice>();
						begin = KeySelector.FirstGreaterOrEqual(beginKey);
						var endKey = await PopAsync<Slice>();
						end = KeySelector.FirstGreaterOrEqual(endKey);
						limit = await PopAsync<int>();
						reverse = await PopAsync<bool>();
						streamingMode = (FdbStreamingMode) await PopAsync<int>();
					}

					Task<FdbRangeChunk> future;
					if (instr.IsDatabase())
					{
						future = this.Db.ReadAsync(
							tr => instr.IsSnapshot()
								? tr.Snapshot.GetRangeAsync(begin, end, new() { Limit = limit, IsReversed = reverse, Streaming = streamingMode })
								: tr.GetRangeAsync(begin, end, new() { Limit = limit, IsReversed = reverse, Streaming = streamingMode }),
							ct
						);
					}
					else
					{
						var tr = GetCurrentTransaction();
						future = instr.IsSnapshot()
							? tr.Snapshot.GetRangeAsync(begin, end, new() { Limit = limit, IsReversed = reverse, Streaming = streamingMode })
							: tr.GetRangeAsync(begin, end, new () { Limit = limit, IsReversed = reverse, Streaming = streamingMode });
					}

					PushFuture(index, instr, future,
						async (t) =>
						{
							var chunk = await t;
							IVarTuple res = STuple.Empty;
							foreach (var (k, v) in chunk.Items)
							{
								res = res.Append(k, v);
							}
							return res;
						});

					break;
				}

				case InstructionCode.Set:
				{ // SET (_DATABASE)

					// Pops the top two items off of the stack as KEY and VALUE. Sets KEY to have
					// the value VALUE. A SET_DATABASE call may optionally push a future onto the
					// stack.
					var key = await PopAsync<Slice>();
					var value = await PopAsync<Slice>();

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.Set(key, value), ct);
						PushFuture(index, instr, MapVoidFuture(res));
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.Set(key, value);
					}
					break;
				}
				case InstructionCode.Clear:
				{
					// Pops the top item off of the stack as KEY and then clears KEY from the
					// database. A CLEAR_DATABASE call may optionally push a future onto the stack.
					var key = await PopAsync<Slice>();

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.Clear(key), ct);
						PushFuture(index, instr, MapVoidFuture(res));
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.Clear(key);
					}
					break;
				}
				case InstructionCode.ClearRange:
				{
					KeyRange range;
					if (instr.IsStartsWith())
					{
						// Pops the top item off of the stack as PREFIX and then clears all keys from
						// the database that begin with PREFIX. A CLEAR_RANGE_STARTS_WITH_DATABASE call
						// may optionally push a future onto the stack.
						var prefix = await PopAsync<Slice>();
						range = KeyRange.StartsWith(prefix);
					}
					else
					{
						// Pops the top two items off of the stack as BEGIN_KEY and END_KEY. Clears the
						// range of keys from BEGIN_KEY to END_KEY in the database. A
						// CLEAR_RANGE_DATABASE call may optionally push a future onto the stack.
						var beginKey = await PopAsync<Slice>();
						var endKey = await PopAsync<Slice>();
						range = new KeyRange(beginKey, endKey);
					}

					if (instr.IsDatabase())
					{
						var res = this.Db.WriteAsync(tr => tr.ClearRange(range), ct);
						PushFuture(index, instr, MapVoidFuture(res));
					}
					else
					{
						var tr = GetCurrentTransaction();
						tr.ClearRange(range);
					}
					break;
				}

				case InstructionCode.ReadConflictKey:
				{
					// Pops the top item off of the stack as KEY. Adds KEY as a read conflict key
					// or write conflict key. Pushes the byte string "SET_CONFLICT_KEY" onto the stack.
					var key = await PopAsync<Slice>();
					var tr = GetCurrentTransaction();
					tr.AddReadConflictKey(key);
					Push(index, instr, "SET_CONFLICT_KEY");
					break;
				}

				case InstructionCode.ReadConflictRange:
				{
					// Pops the top two items off of the stack as BEGIN_KEY and END_KEY. Adds a
					// read conflict range or write conflict range from BEGIN_KEY to END_KEY.
					// Pushes the byte string "SET_CONFLICT_RANGE" onto the stack.
					var beginKey = await PopAsync<Slice>();
					var endKey = await PopAsync<Slice>();
					var tr = GetCurrentTransaction();
					tr.AddReadConflictRange(beginKey, endKey);
					Push(index, instr, "SET_CONFLICT_RANGE");
					break;
				}

				case InstructionCode.WriteConflictKey:
				{
					// Pops the top item off of the stack as KEY. Adds KEY as a read conflict key
					// or write conflict key. Pushes the byte string "SET_CONFLICT_KEY" onto the stack.
					var key = await PopAsync<Slice>();
					var tr = GetCurrentTransaction();
					tr.AddWriteConflictKey(key);
					Push(index, instr, "SET_CONFLICT_KEY");
					break;
				}

				case InstructionCode.WriteConflictRange:
				{
					// Pops the top two items off of the stack as BEGIN_KEY and END_KEY. Adds a
					// read conflict range or write conflict range from BEGIN_KEY to END_KEY.
					// Pushes the byte string "SET_CONFLICT_RANGE" onto the stack.
					var beginKey = await PopAsync<Slice>();
					var endKey = await PopAsync<Slice>();
					var tr = GetCurrentTransaction();
					tr.AddWriteConflictRange(beginKey, endKey);
					Push(index, instr, "SET_CONFLICT_RANGE");
					break;
				}

				case InstructionCode.DisableWriteConflict:
				{
					//Sets the NEXT_WRITE_NO_WRITE_CONFLICT_RANGE transaction option on the
					// current transaction. Does not modify the stack.
					var tr = GetCurrentTransaction();
					tr.Options.WithNextWriteNoWriteConflictRange();
					break;
				}

				case InstructionCode.EncodeFloat:
				{
					var bytes = await PopAsync<Slice>();
					var x = bytes.ToSingleBE();
					Push(index, instr, x);
					break;
				}
				case InstructionCode.DecodeFloat:
				{
					var x = await PopAsync<float>();
					var bytes = Slice.FromSingleBE(x);
					Push(index, instr, bytes);
					break;
				}

				case InstructionCode.EncodeDouble:
				{
					var bytes = await PopAsync<Slice>();
					var x = bytes.ToDoubleBE();
					Push(index, instr, x);
					break;
				}
				case InstructionCode.DecodeDouble:
				{
					var x = await PopAsync<double>();
					var bytes = Slice.FromDoubleBE(x);
					Push(index, instr, bytes);
					break;
				}

				case InstructionCode.WaitFuture:
				{
					var item = this.Stack.Peek();
					if (!item.IsCompleted)
					{
						await item.Resolve();
					}
					break;
				}
				default:
				{
					throw new NotSupportedException($"Unsupported operation {instr.Code}");
				}
			}
		}

		public void Dump()
		{
			var log = this.Log;
			log.LogInformation($"Stack: [{this.Stack.Count}]");
			int p = 0;
			foreach (var item in this.Stack.ToArray())
			{
				if (item.TryResolve(out var res))
				{
					log.LogInformation($"  [{p}] {res.Value}, @{res.Index}");
				}
				else
				{
					log.LogInformation($"  [{p}] FUTURE<{item.Future.AsTask().Status}>, @{item.Index}");
				}
				++p;
			}
		}

		public void Dispose()
		{
			// TODO release managed resources here
		}

	}

}
