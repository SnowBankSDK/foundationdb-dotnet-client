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
	using FoundationDB.Client;

	/// <summary>Atomic-operation completeness matrix: every mutation type alone and pairwise-coalesced, over absent and committed values, with a read-back, the actually-throwing combinations map themselves.</summary>
	public static class AtomicsCorpus
	{

		/// <summary>The coalescable mutation types (the versionstamped ops have their own corpus and cannot be pairwise-combined).</summary>
		private static readonly FdbMutationType[] Ops =
		[
			FdbMutationType.Add,
			FdbMutationType.BitAnd,
			FdbMutationType.BitOr,
			FdbMutationType.BitXor,
			FdbMutationType.AppendIfFits,
			FdbMutationType.Max,
			FdbMutationType.Min,
			FdbMutationType.ByteMin,
			FdbMutationType.ByteMax,
			FdbMutationType.CompareAndClear,
		];

		/// <summary>The committed value seeded under half of the matrix keys: 2 bytes of ascii, so width-mismatch arithmetic (8-byte operands over short values) gets pinned too.</summary>
		private const string Seeded = "s7";

		/// <summary>A deterministic operand per mutation type.</summary>
		private static Slice OperandFor(FdbMutationType op) => op switch
		{
			FdbMutationType.Add => Slice.FromFixed64(5),
			FdbMutationType.BitAnd => Slice.FromFixed64(0x0F0F0F0F),
			FdbMutationType.BitOr => Slice.FromFixed64(0xF0F0),
			FdbMutationType.BitXor => Slice.FromFixed64(0xFF00FF),
			FdbMutationType.AppendIfFits => Slice.FromStringAscii("+a"),
			FdbMutationType.Max => Slice.FromFixed64(0x60),
			FdbMutationType.Min => Slice.FromFixed64(0x10),
			FdbMutationType.ByteMin => Slice.FromStringAscii("m1"),
			FdbMutationType.ByteMax => Slice.FromStringAscii("zz"),
			FdbMutationType.CompareAndClear => Slice.FromStringAscii(Seeded), // matches the seeded value, so it clears it
			_ => throw new ArgumentOutOfRangeException(nameof(op)),
		};

		/// <summary>All the scenarios of the atomics matrix.</summary>
		public static IEnumerable<Scenario> All()
		{
			yield return Singles();
			yield return Pairs();
		}

		private static Scenario Singles()
		{
			var b = new ScenarioBuilder();

			// seed committed values under the "c" keys
			b.Begin("A");
			for (int i = 0; i < Ops.Length; i++)
			{
				b.Set("A", $"c{i:D2}", Seeded);
			}
			b.Commit("A");

			// each op once over an absent key ("a" keys) and once over the committed value ("c" keys), then read back
			b.Begin("A");
			for (int i = 0; i < Ops.Length; i++)
			{
				b.Atomic("A", $"a{i:D2}", OperandFor(Ops[i]), Ops[i]);
				b.Get("A", $"a{i:D2}");
				b.Atomic("A", $"c{i:D2}", OperandFor(Ops[i]), Ops[i]);
				b.Get("A", $"c{i:D2}");
			}
			b.Commit("A");
			return b.Build("atomic_matrix_singles", "every atomic mutation type over an absent key and over a committed 2-byte value, with read-backs");
		}

		private static Scenario Pairs()
		{
			var b = new ScenarioBuilder();

			// seed committed values under the "c" keys
			b.Begin("A");
			for (int i = 0; i < Ops.Length; i++)
			{
				for (int j = 0; j < Ops.Length; j++)
				{
					b.Set("A", $"c{i:D2}x{j:D2}", Seeded);
				}
			}
			b.Commit("A");

			// each ordered pair coalesced on one key, over absent ("a") and committed ("c"), then read back
			b.Begin("A");
			for (int i = 0; i < Ops.Length; i++)
			{
				for (int j = 0; j < Ops.Length; j++)
				{
					b.Atomic("A", $"a{i:D2}x{j:D2}", OperandFor(Ops[i]), Ops[i]);
					b.Atomic("A", $"a{i:D2}x{j:D2}", OperandFor(Ops[j]), Ops[j]);
					b.Get("A", $"a{i:D2}x{j:D2}");
					b.Atomic("A", $"c{i:D2}x{j:D2}", OperandFor(Ops[i]), Ops[i]);
					b.Atomic("A", $"c{i:D2}x{j:D2}", OperandFor(Ops[j]), Ops[j]);
					b.Get("A", $"c{i:D2}x{j:D2}");
				}
			}
			b.Commit("A");
			return b.Build("atomic_matrix_pairs", "every ordered pair of atomic mutation types coalesced on one key, over absent and committed values, with read-backs");
		}

	}

}
