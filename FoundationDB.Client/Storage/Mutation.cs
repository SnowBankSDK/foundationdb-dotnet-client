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

namespace FoundationDB.Storage
{
	using FoundationDB.Client;

	[PublicAPI]
	public enum Operation
	{
		Invalid = 0,
		Set,
		Clear,
		ClearRange,

		//note: FdbMutationType offset by 10
		Add = 10 + FdbMutationType.Add,
		BitAnd = 10 + FdbMutationType.BitAnd,
		BitOr = 10 + FdbMutationType.BitOr,
		BitXor = 10 + FdbMutationType.BitXor,
		AppendIfFits = 10 + FdbMutationType.AppendIfFits,
		Max = 10 + FdbMutationType.Max,
		Min = 10 + FdbMutationType.Min,
		VersionStampedKey = 10 + FdbMutationType.VersionStampedKey,
		VersionStampedValue = 10 + FdbMutationType.VersionStampedValue,
		ByteMin = 10 + FdbMutationType.ByteMin,
		ByteMax = 10 + FdbMutationType.ByteMax,
		CompareAndClear = 10 + FdbMutationType.CompareAndClear,
	}

	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed record Mutation
	{
		public Operation Op { get; }

		public Value Parameter { get; }

		public Mutation? Next { get; internal set; }

		public Mutation? Tail { get; internal set; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Value GetEffectiveValue() => this.Op is Operation.Clear or Operation.ClearRange ? default : this.Parameter;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private Mutation(Operation op, Value parameter)
		{
			this.Op = op;
			this.Parameter = parameter;
		}

		public bool IsKv() => (this.Op is Operation.Set or Operation.Clear) && this.Next == null;

		public bool IsRange() => this.Op is Operation.ClearRange;

		public bool IsAtomic() => (this.Op >= Operation.Add) || this.Next != null;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Mutation Set(Value value) => new(Operation.Set, value);

		public static Mutation Clear() => new(Operation.Clear, default);

		public static Mutation ClearRange() => new(Operation.ClearRange, default);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Mutation Atomic(FdbMutationType type, Value parameter) => new((Operation) (10 + type), parameter);

		public override string ToString()
		{
			string literal = this.Op switch
			{
				Operation.Clear => "Clear()",
				Operation.ClearRange => "ClearRange(...)",
				_ => $"{this.Op}({this.Parameter:V})",
			};
			return this.Next is null ? literal : literal + " + " + this.Next.ToString();
		}
	}
}
