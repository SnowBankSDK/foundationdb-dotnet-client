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

namespace FoundationDB.Client
{

	public static partial class Fdb
	{

		/// <summary>Static helper class to open named partitions</summary>
		[PublicAPI]
		public static class Directory
		{

			/// <summary>Opens a named partition, and change the root subspace of the database to the corresponding prefix</summary>
			internal static async Task SwitchToNamedPartitionAsync(FdbDatabase db, FdbPath root, CancellationToken ct)
			{
				Contract.Debug.Requires(db != null);
				ct.ThrowIfCancellationRequested();

				if (Logging.On) Logging.Verbose(typeof(Fdb.Directory), "OpenNamedPartitionAsync", $"Opened root layer using cluster file '{db.ClusterFile}'");

				if (root.Count != 0)
				{
					// create the root partition if it does not already exist
					var descriptor = await db.ReadWriteAsync(tr => db.DirectoryLayer.CreateOrOpenAsync(tr, root), ct).ConfigureAwait(false);
					if (Logging.On) Logging.Info(typeof(Fdb.Directory), "OpenNamedPartitionAsync", $"Opened partition {descriptor.Path} at {descriptor.GetPrefixUnsafe()}");
				}
			}

			/// <summary>List and open the subdirectories of the given directory</summary>
			/// <param name="tr">Transaction used for the operation</param>
			/// <param name="parent">Parent directory</param>
			/// <returns>Dictionary of all the subdirectories of the <paramref name="parent"/> directory.</returns>
			public static async Task<Dictionary<string, FdbDirectorySubspace>> BrowseAsync(IFdbReadOnlyTransaction tr, IFdbDirectory parent)
			{
				Contract.NotNull(tr);
				Contract.NotNull(parent);

				// read the names of all the subdirectories
				var children = await parent.ListAsync(tr).ConfigureAwait(false);

				// open all the subdirectories
				var folders = await children
					.ToAsyncQuery(tr.Cancellation)
					.SelectParallel((child, _) => parent.OpenAsync(tr, FdbPath.Relative(child.Name)))
					.ToListAsync()
					.ConfigureAwait(false);

				// map the result
				return folders.ToDictionary(ds => ds.Name);
			}
		}

	}

}
