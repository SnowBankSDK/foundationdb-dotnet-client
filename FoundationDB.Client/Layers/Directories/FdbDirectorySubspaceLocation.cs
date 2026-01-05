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

	[DebuggerDisplay("Path={Path}, Layer={Layer}")]
	public sealed class FdbDirectorySubspaceLocation : ISubspaceLocation<FdbDirectorySubspace>, IFdbDirectory, IFdbLayer<FdbDirectorySubspace, FdbDirectoryLayer?>
	{

		/// <inheritdoc cref="ISubspaceLocation.Path" />
		public FdbPath Path { get; }

		/// <inheritdoc />
		public string Layer => this.Path.LayerId;

		/// <inheritdoc />
		Slice ISubspaceLocation.Prefix => Slice.Nil;

		/// <summary>Returns <see langword="true"/> if this location points to a directory partition.</summary>
		public bool IsPartition { get; }

		FdbDirectorySubspaceLocation IFdbDirectory.Location => this;

		/// <summary>Creates a new location that points to the Directory Subspace at the given path.</summary>
		/// <param name="path">Absolute path of the target Directory Subspace</param>
		public FdbDirectorySubspaceLocation(FdbPath path)
		{
			if (!path.IsAbsolute) throw new ArgumentException("Directory Subspace path must absolute.", nameof(path));
			this.Path = path;
			this.IsPartition = path.LayerId == FdbDirectoryPartition.LayerId;
		}

		/// <inheritdoc />
		async ValueTask<IKeySubspace?> ISubspaceLocation.TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await TryResolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.TryResolve" />
		public ValueTask<FdbDirectorySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);
			directory ??= tr.Context.Database.DirectoryLayer;
			return directory.TryOpenCachedAsync(tr, this.Path);
		}

		/// <inheritdoc />
		async ValueTask<IKeySubspace> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.Resolve"/>
		public async ValueTask<FdbDirectorySubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);
			directory ??= tr.Context.Database.DirectoryLayer;
			var subspace = await directory.TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (subspace == null)
			{
				throw new InvalidOperationException($"The required directory '{this.Path}' does not exist in the database.");
			}
			return subspace;
		}

		/// <summary>Returns the same location, but with an explicit <see cref="Layer"/> identifier</summary>
		/// <param name="layerId">Layer Identifier to use for this location</param>
		/// <remarks>This can be used to force a specific LayerId, or to add one if it is missing</remarks>
		public FdbDirectorySubspaceLocation WithLayer(string layerId)
		{
			if (this.Path.LayerId == layerId)
			{ // already the correct value
				return this;
			}

			return new(this.Path.WithLayer(layerId));
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(FdbDirectorySubspaceLocation);

		/// <summary>Returns the actual subspace that corresponds to this location, or create it if it does not exist.</summary>
		/// <remarks>
		/// <para>This should only be called by infrastructure code that needs to initialize the database or perform maintenance operation.</para>
		/// <para>Regular business logic should mostly call <see cref="TryResolve"/> or <see cref="Resolve"/> and not attempt to initialize the database by themselves!</para>
		/// </remarks>
		public async ValueTask<FdbDirectorySubspace> ResolveOrCreate(IFdbTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);
			directory ??= tr.Context.Database.DirectoryLayer;
			var subspace = await directory.TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);

			if (subspace == null)
			{
				subspace = await directory.CreateAsync(tr, this.Path).ConfigureAwait(false);
				//TODO: how to handle the cache?
			}

			return subspace;
		}

		/// <inheritdoc />
		public override string ToString() => this.Path.ToString();

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Layer.GetHashCode());

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is FdbDirectorySubspaceLocation loc && Equals(loc);

		/// <inheritdoc />
		public bool Equals(ISubspaceLocation? other) => other != null && other.Path == this.Path && other.Prefix.Count == 0;

		internal FdbPath GetSafePath()
			=> !this.IsPartition || this.Path.Count == 0
				? this.Path
				: throw ThrowHelper.InvalidOperationException($"Cannot create a binary subspace under the root of directory partition '{this.Path}'.");

		/// <summary>Appends a segment to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbPathSegment segment] => new(this.Path.Add(segment));

		/// <summary>Appends one or more segments to the current path</summary>
		public FdbDirectorySubspaceLocation this[ReadOnlySpan<FdbPathSegment> segments] => segments.Length != 0 ? new(this.Path.Add(segments)) : this;

		/// <summary>Appends a segment to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <remarks>The new segment will not have a layer id.</remarks>
		public FdbDirectorySubspaceLocation this[string name] => new(this.Path.Add(FdbPathSegment.Create(name)));

		/// <summary>Appends a segment - composed of a name and layer id - to the current path</summary>
		/// <param name="name">Name of the segment</param>
		/// <param name="layerId">Layer Id of the segment</param>
		public FdbDirectorySubspaceLocation this[string name, string layerId] => new(this.Path.Add(new FdbPathSegment(name, layerId)));

		/// <summary>Appends a relative path to the current path</summary>
		public FdbDirectorySubspaceLocation this[FdbPath relativePath] => !relativePath.IsEmpty ? new(this.Path.Add(relativePath)) : this;

		#region IFdbDirectory...

		/// <inheritdoc />
		string IFdbDirectory.Name => this.Path.Name;

		/// <inheritdoc />
		string IFdbDirectory.FullName => this.Path.ToString();

		/// <inheritdoc />
		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => throw new NotSupportedException();

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateOrOpenAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.OpenAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbPath> paths)
		{
			return trans.Context.Database.DirectoryLayer.TryOpenCachedAsync(trans, paths.Select(p => this.Path.Add(p)));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.CreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbPath subPath = default)
		{
			return trans.Context.Database.DirectoryLayer.TryCreateAsync(trans, this.Path.Add(subPath));
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.MoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task<FdbDirectorySubspace?> TryMoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			return trans.Context.Database.DirectoryLayer.TryMoveAsync(trans, this.Path, newAbsolutePath);
		}

		/// <inheritdoc />
		public Task RemoveAsync(IFdbTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.RemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryRemoveAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ExistsAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<FdbPath>> ListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.ListAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		public Task<List<FdbPath>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			return trans.Context.Database.DirectoryLayer.TryListAsync(trans, this.Path.Add(path));
		}

		/// <inheritdoc />
		void IFdbDirectory.CheckLayer(string? layer) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, string newLayer) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.RegisterAsync(IFdbTransaction trans, FdbPath subPath, Slice prefix) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace> IFdbDirectory.MoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath) => throw new NotSupportedException();

		/// <inheritdoc />
		Task<FdbDirectorySubspace?> IFdbDirectory.TryMoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath) => throw new NotSupportedException();

		#endregion

	}
}
