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

	/// <summary>Represents the path to a specific subspace in the database</summary>
	/// <remarks>A path can be <see cref="ISubspaceLocation{TSubspace}.Resolve">resolved</see> into the actual <see cref="IKeySubspace"/> implementation that will be valid within the context of a transaction.</remarks>
	[PublicAPI]
	public interface ISubspaceLocation : IEquatable<ISubspaceLocation>
	{

		// Path and/or Prefix can be empty!
		// - Path: empty, Prefix: empty => no prefix at all (root of the database!)
		// - Path: empty, Prefix: not empty => a traditional subspace located under the root of the database (not using the Directory Layer)
		// - Path: not empty, Prefix: empty => the content of a directory subspace (no subspaces)
		// - Path: not empty, Prefix: not empty => a directory subspace that has been divided into another local subspace

		/// <summary>Path of the <see cref="FdbDirectorySubspace">directory subspace</see> that contains the subspace.</summary>
		/// <remarks>
		/// This path is always absolute. Relative paths are not allowed.
		/// Can be <see cref="FdbPath.Root"/> if this subspace is located at the root of the database (ie: not using the Directory Layer)</remarks>
		FdbPath Path { get; }

		/// <summary>Optional key prefix of the subspace, added immediately after the prefix of the <see cref="FdbDirectorySubspace">subspace</see> resolved by the <see cref="Path"/>.</summary>
		Slice Prefix { get; }

		/// <summary>Resolves the subspace for this location, if it exists</summary>
		/// <param name="tr">Transaction used for this operation.</param>
		/// <param name="directory">Optional <see cref="FdbDirectoryLayer">Directory Layer</see> to use; otherwise, uses the global instance for the transaction's database.</param>
		/// <returns>Resolved subspace if found; otherwise, <see langword="null"/></returns>
		/// <remarks>The subspace may be cached, and be obsolete. The cache will add deferred value checks to the transaction, that will cause it to conflict and retry, if the directory has been deleted or has moved.</remarks>
		ValueTask<IKeySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		/// <summary>Resolves the subspace for this location, or throws if it does not exist.</summary>
		/// <param name="tr">Transaction used for this operation.</param>
		/// <param name="directory">Optional <see cref="FdbDirectoryLayer">Directory Layer</see> to use; otherwise, uses the global instance for the transaction's database.</param>
		/// <returns>Resolved subspace if found</returns>
		/// <exception cref="InvalidOperationException">If this location does not exist in the database.</exception>
		/// <remarks>The subspace may be cached, and be obsolete. The cache will add deferred value checks to the transaction, that will cause it to conflict and retry, if the directory has been deleted or has moved.</remarks>
		ValueTask<IKeySubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

	}

	/// <summary>Represents the path to a typed subspace in the database</summary>
	/// <typeparam name="TSubspace">Type of the <see cref="IKeySubspace"/> implementation that this path will resolve into.</typeparam>
	[PublicAPI]
	public interface ISubspaceLocation<TSubspace> : ISubspaceLocation
		where TSubspace : class, IKeySubspace
	{

		/// <summary>Returns the actual subspace that corresponds to this location, if it exists.</summary>
		/// <param name="tr">Current transaction</param>
		/// <param name="directory"><see cref="FdbDirectoryLayer">DirectoryLayer</see> instance to use for the resolve. If null, uses the default database directory layer.</param>
		/// <returns>Key subspace using the resolved key prefix of this location in the context of the current transaction, or <c>null</c> if the directory does not exist</returns>
		/// <remarks>
		/// <para>The instance resolved for this transaction <b>SHOULD NOT</b> be used in the context of a different transaction, because its location in the Directory Layer may have been changed concurrently!</para>
		/// <para>Re-using cached subspace instances <b>MAY</b> lead to <b>DATA CORRUPTION</b> if not used carefully! The best practice is to call <see cref="TryResolve"/>() every time it is needed by a new transaction.</para>
		/// </remarks>
		/// <seealso cref="Resolve"/>
		new ValueTask<TSubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		/// <summary>Returns the actual subspace that corresponds to this location, or throws if it does not exist.</summary>
		/// <param name="tr">Current transaction</param>
		/// <param name="directory"><see cref="FdbDirectoryLayer">DirectoryLayer</see> instance to use for the resolve. If null, uses the default database directory layer.</param>
		/// <returns>Key subspace using the resolved key prefix of this location in the context of the current transaction</returns>
		/// <exception cref="InvalidOperationException">If this location does not exist in the database.</exception>
		/// <remarks>
		/// <para>The instance resolved for this transaction <b>SHOULD NOT</b> be used in the context of a different transaction, because its location in the Directory Layer may have been changed concurrently!</para>
		/// <para>Re-using cached subspace instances <b>MAY</b> lead to <b>DATA CORRUPTION</b> if not used carefully! The best practice is to call <see cref="Resolve"/>() every time it is needed by a new transaction.</para>
		/// </remarks>
		/// <seealso cref="TryResolve"/>
		new ValueTask<TSubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

	}

	/// <summary>Default implementation of a subspace location</summary>
	/// <typeparam name="TSubspace">Type of the concrete <see cref="IKeySubspace"/> implementation that this location will resolve to</typeparam>
	[DebuggerDisplay("Path={Path}, Prefix={Prefix}")]
	[PublicAPI]
	public abstract class SubspaceLocation<TSubspace> : ISubspaceLocation<TSubspace>, IFdbLayer<TSubspace, FdbDirectoryLayer?>
		where TSubspace : class, IKeySubspace
	{

		/// <inheritdoc />
		public FdbPath Path { get; } // can be empty

		/// <inheritdoc />
		public Slice Prefix { get; } // can be empty

		protected SubspaceLocation(FdbPath path, Slice prefix)
		{
			this.Path = path;
			this.Prefix = prefix.IsNull ? Slice.Empty : prefix;
		}

		/// <inheritdoc />
		public override string ToString()
		{
			if (this.Path.Count == 0)
			{
				return this.Prefix.ToString();
			}
			if (this.Prefix.Count == 0)
			{
				return "[" + this.Path.ToString() + "]";
			}
			return "[" + this.Path.ToString() + "]+" + this.Prefix.ToString();
		}

		public bool IsTopLevel => this.Path.Count == 0;

		async ValueTask<IKeySubspace?> ISubspaceLocation.TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await TryResolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.TryResolve"/>
		public abstract ValueTask<TSubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null);

		async ValueTask<IKeySubspace> ISubspaceLocation.Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			return await Resolve(tr, directory).ConfigureAwait(false);
		}

		/// <inheritdoc cref="ISubspaceLocation{T}.Resolve"/>
		public virtual async ValueTask<TSubspace> Resolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			var subspace = await TryResolve(tr, directory).ConfigureAwait(false);
			if (subspace == null)
			{
				throw new InvalidOperationException($"The required location '{this.Path}' does not exist in the database.");
			}
			return subspace;
		}

		/// <inheritdoc />
		string IFdbLayer.Name => nameof(SubspaceLocation<>);

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is ISubspaceLocation path && Equals(path);

		/// <inheritdoc />
		public abstract bool Equals(ISubspaceLocation? other);

		/// <inheritdoc />
		public abstract override int GetHashCode();

	}

	/// <summary>Extension methods for <see cref="ISubspaceLocation"/></summary>
	[PublicAPI]
	public static class SubspaceLocationExtensions
	{

		/// <summary>Returns a location that will always add the given prefix to all the keys, relative to the current location</summary>
		/// <param name="self">Existing subspace path</param>
		/// <param name="prefix">Prefix added between the resolved path prefix, and the rest of the key</param>
		/// <remarks>
		/// <para>If, for example, location <c>"/Foo/Bar"</c> will resolve to the prefix <c>(123,)</c>, call <c>WithPrefix(TuPack.EncodeKey(0))</c> will create a new location that will use <c>(123, 0)</c> for all keys, without having to explicitly repeat <c>subspace.GetKey(0, ...)</c> everytime.</para>
		/// </remarks>
		public static ISubspaceLocation WithPrefix(this ISubspaceLocation self, Slice prefix)
		{
			return prefix.Count == 0 ? self : new SubspaceLocation(self.Path, self.Prefix + prefix);
		}

		/// <summary>Return a directory version of the current location</summary>
		/// <param name="self">Existing subspace location</param>
		/// <returns>A <see cref="FdbDirectorySubspaceLocation"/> that points to the same location as <paramref name="self"/>.</returns>
		/// <exception cref="ArgumentException">If the location has a non-zero <see cref="ISubspaceLocation.Prefix"/></exception>
		[Pure]
		public static FdbDirectorySubspaceLocation AsDirectory(this ISubspaceLocation self)
		{
			Contract.NotNull(self);

			if (self is FdbDirectorySubspaceLocation dsl)
			{
				return dsl;
			}

			return self.Prefix.Count == 0
				? new FdbDirectorySubspaceLocation(self.Path)
				: throw new ArgumentException($"Cannot convert location '{self}' into a directory location, because it has a non-empty prefix.");
		}

	}

	/// <summary>Factory methods for creating <see cref="ISubspaceLocation">locations</see> in the keyspace of a FoundationDB database.</summary>
	[PublicAPI]
	public class SubspaceLocation : SubspaceLocation<IKeySubspace>, IEquatable<SubspaceLocation>
	{

		/// <summary>Represent the root directory of the Directory Layer</summary>
		public static readonly SubspaceLocation Root
			= new(FdbPath.Root, Slice.Empty);

		/// <summary>Represent a location without any prefix, and outside the jurisdiction of the Directory Layer</summary>
		public static readonly SubspaceLocation Empty
			= new(FdbPath.Empty, Slice.Empty);

		#region FromPath...

		/// <inheritdoc />
		public SubspaceLocation(FdbPath path, Slice prefix = default) : base(path, prefix)
		{
		}

		[Pure]
		public static ISubspaceLocation FromPath(FdbPath path) => new SubspaceLocation(path);

		#endregion

		#region FromKey...

		/// <summary>Creates a location that uses a fixed prefix given by a tuple</summary>
		[Pure]
		public static ISubspaceLocation WithPrefix(IVarTuple items)
		{
			return new SubspaceLocation(FdbPath.Empty, TuPack.Pack(items));
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static SubspaceLocation WithPrefix(ReadOnlySpan<byte> key)
		{
			return new(FdbPath.Empty, Slice.FromBytes(key));
		}

		/// <summary>Creates a location for dynamic keys, that uses a fixed binary prefix</summary>
		[Pure]
		public static SubspaceLocation FromPrefix(Slice key)
		{
			return !key.IsNull ? new(FdbPath.Empty, key.Copy()) : throw new ArgumentException("Key cannot be nil.", nameof(key));
		}

		#endregion

		/// <inheritdoc />
		public override int GetHashCode() => HashCode.Combine(this.Path.GetHashCode(), this.Prefix.GetHashCode(), 0x12344321);

		/// <inheritdoc />
		public override bool Equals(ISubspaceLocation? other) => other is SubspaceLocation loc && Equals(loc);

		public bool Equals(SubspaceLocation? other) =>
			ReferenceEquals(other, this)
			|| (other is not null
			 && other.Path == this.Path
			 && other.Prefix.Equals(this.Prefix)
			);

		/// <inheritdoc />
		public override ValueTask<IKeySubspace?> TryResolve(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory = null)
		{
			Contract.NotNull(tr);

			if (this.IsTopLevel)
			{ // not contained in a directory subspace
				return new(new KeySubspace(this.Prefix, SubspaceContext.Default));
			}

			return ResolveWithDirectory(tr, directory);
		}

		private async ValueTask<IKeySubspace?> ResolveWithDirectory(IFdbReadOnlyTransaction tr, FdbDirectoryLayer? directory)
		{
			// located inside a directory subspace!
			var folder = await (directory ?? tr.Context.Database.DirectoryLayer).TryOpenCachedAsync(tr, this.Path).ConfigureAwait(false);
			if (folder == null) return null;
			return this.Prefix.Count == 0
				? folder
				: new KeySubspace(folder.GetPrefix() + this.Prefix, folder.Context);
		}

	}

}
