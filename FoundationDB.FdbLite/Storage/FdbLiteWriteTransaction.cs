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

namespace FoundationDB.Storage.FdbLite
{

	/// <summary>A write generation with deterministic cleanup: commit it, or its disposal rolls every side effect back.</summary>
	/// <remarks>
	/// <para>The safe shape of <see cref="FdbLiteEngine.BeginWrite"/> for code that holds a writer across its own control flow (a binding's transaction handler, a batching loop): <c>using var tx = engine.Write();</c> guarantees that an exception between the first mutation and the commit abandons the generation instead of leaking its allocations and blocking the single-writer engine.</para>
	/// <para>For a self-contained mutation, the handler forms (<see cref="FdbLiteEngine.Write(ulong, System.Action{FdbLiteTreeWriter})"/> and friends) say the same thing in one call.</para>
	/// </remarks>
	public sealed class FdbLiteWriteTransaction : IDisposable
	{

		internal FdbLiteWriteTransaction(FdbLiteEngine engine, FdbLiteTreeWriter writer)
		{
			this.Engine = engine;
			this.Writer = writer;
		}

		private FdbLiteEngine Engine { get; }

		/// <summary>The writer of this generation; valid until <see cref="Commit"/> or disposal.</summary>
		public FdbLiteTreeWriter Writer { get; }

		private bool Completed { get; set; }

		/// <summary>Commits the generation at <paramref name="databaseVersion"/> (the caller owns the version counter, exactly as for <see cref="FdbLiteEngine.Commit"/>).</summary>
		public void Commit(ulong databaseVersion)
		{
			this.Engine.Commit(this.Writer, databaseVersion);
			this.Completed = true;
		}

		/// <summary>Abandons the generation when it was not committed: allocations, buffered pages, and recorded frees all roll back.</summary>
		public void Dispose()
		{
			if (!this.Completed)
			{
				this.Engine.Abandon(this.Writer);
				this.Completed = true;
			}
		}

	}

}
