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
	using FoundationDB.FdbLite;

	/// <summary>Reusable FdbLite backend for a dual-backend layer fixture: the persistent engine over the heap, through the binding.</summary>
	/// <remarks>The sibling of <see cref="FakeDbTestBackend"/>: same lifecycle (lazy store, reset per test, fresh store after a dispose), different storage. Watch buggify is enabled the same way, since the chaos machinery lives on the shared base.</remarks>
	internal sealed class FdbLiteTestBackend
	{

		private FdbLiteStore? Store { get; set; }

		public void Reset() => this.Store = null;

		public Task<IFdbDatabase> OpenAsync(FdbPath path, bool readOnly = false)
		{
			try
			{
				var db = (this.Store ??= NewStore()).OpenDatabase(path, readOnly);
				db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
				return Task.FromResult<IFdbDatabase>(db);
			}
			catch (ObjectDisposedException)
			{
				this.Store = NewStore();
				var db = this.Store.OpenDatabase(path, readOnly);
				db.Options.WithDefaultTimeout(TimeSpan.FromSeconds(15));
				return Task.FromResult<IFdbDatabase>(db);
			}
		}

		private static FdbLiteStore NewStore()
		{
			var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default);
			store.Buggify.EnableChaos(NUnit.Framework.TestContext.CurrentContext.Test.FullName);
			return store;
		}

	}

}
