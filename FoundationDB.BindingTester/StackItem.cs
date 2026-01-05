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

namespace FoundationDB.Client.Testing
{

	[DebuggerDisplay("{ToString(),nq}")]
	public sealed record StackItem
	{

		public StackItem(TestInstruction instr, IVarTuple value, int index)
		{
			this.Result = new () { Index = index, Value = value };
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, Task<IVarTuple> future, int index)
		{
			this.Future = new ValueTask<IVarTuple>(future);
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, ValueTask<IVarTuple> future, int index)
		{
			this.Future = future;
			this.Index = index;
			this.Instruction = instr;
		}

		public StackItem(TestInstruction instr, Exception error, int index)
		{
			this.Result = new() { Index = index, Value = MapError(error), Error = error, };
			this.Index = index;
			this.Instruction = instr;
		}

		internal static IVarTuple MapError(Exception e) => e switch
		{
			FdbException fdbEx => STuple.Create("ERROR", fdbEx.Code.ToString()), //BUGBUG: text version of error code, which case and format???
			_ => STuple.Create("ERROR", "Internal Error", e.Message)
		};

		public ValueTask<IVarTuple> Future { get; private set; }

		public StackResult? Result { get; private set; }

		public int Index { get; init; }

		public TestInstruction Instruction { get; init; }

		public bool IsCompleted => this.Result != null;

		public ValueTask<StackResult> Resolve()
		{
			if (this.Result != null)
			{
				return ValueTask.FromResult(this.Result);
			}

			return AwaitResult();

			async ValueTask<StackResult> AwaitResult()
			{
				IVarTuple tuple;
				Exception? error = null;
				try
				{
					tuple = await this.Future;
				}
				catch (Exception e)
				{
					error = e;
					tuple = MapError(e);
				}

				var res = new StackResult() { Value = tuple, Index = this.Index, Error = error, };
				this.Result = res;
				this.Future = default;
				return res;
			}
		}

		public bool TryResolve([MaybeNullWhen(false)] out StackResult res)
		{
			if (this.Result != null)
			{
				res = this.Result;
				return true;
			}

			if (this.Future.IsCompleted)
			{
				res = GetResult();
				return true;
			}

			res = default;
			return false;

			StackResult GetResult()
			{
				IVarTuple tuple;
				Exception? error = null;
				try
				{
					tuple = this.Future.GetAwaiter().GetResult();
				}
				catch (FdbException e)
				{
					error = e;
					tuple = STuple.Create("ERROR", e.Code.ToString()); //BUGBUG: text version of error code, which case and format???
				}
				catch (Exception e)
				{
					error = e;
					tuple = STuple.Create("ERROR", "Internal Error", e.Message);
				}

				var res = new StackResult() { Value = tuple, Index = this.Index, Error = error, };
				this.Result = res;
				this.Future = default;
				return res;
			}
		}

		public override string ToString() =>
			TryResolve(out var res)
				? $"{res.Index}: {res.Value} # {this.Instruction}"
				: $"{this.Index}: PENDING # {this.Instruction}";

	}

	public sealed record StackResult
	{
		public required IVarTuple Value { get; init; }

		public required int Index { get; init; }

		public Exception? Error { get; init; }

		public T? As<T>() => this.Value.Get<T>(0);

		public object? AsObject() => this.Value[0];

	}

}
