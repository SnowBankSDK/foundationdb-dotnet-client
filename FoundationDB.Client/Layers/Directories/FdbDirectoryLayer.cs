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
	using FoundationDB.Layers.Allocators;

	/// <summary>Provides a FdbDirectoryLayer class for managing directories in FoundationDB.
	/// Directories are a recommended approach for administering layers and applications. Directories work in conjunction with subspaces. Each layer or application should create or open at least one directory with which to manage its subspace(s).
	/// Directories are identified by paths (specified as tuples) analogous to the paths in a Unix-like file system. Each directory has an associated subspace that is used to store content. The layer uses a high-contention allocator to efficiently map each path to a short prefix for its corresponding subspace.
	/// <see cref="FdbDirectoryLayer"/> exposes methods to create, open, move, remove, or list directories. Creating or opening a directory returns the corresponding subspace.
	/// The <see cref="FdbDirectorySubspace"/> class represents subspaces that store the contents of a directory. An instance of <see cref="FdbDirectorySubspace"/> can be used for all the usual subspace operations. It can also be used to operate on the directory with which it was opened.
	/// </summary>
	[DebuggerDisplay("Content={Content}")]
	[PublicAPI]
	public class FdbDirectoryLayer : IFdbDirectory, IFdbLayer<FdbDirectoryLayer.State>
	{
		private const int SUBDIRS = 0;

		// Internal structure:
		// - All keys used by the Directory Layer are encoded using the Tuple Encoding
		// - The "Nodes" subspace is located under the 0xFE subspace relative to the Content subspace (by default)
		// - The 'version' and 'metadata' attributes of the partition are located under Nodes + Pack(Nodes, '...')
		// - All the metadata relative to directory with prefix X are located under Nodes + Pack(X, ...)
		//   - The layer of directory at prefix X is located at Key = Nodes + Pack(X, 'layer') = Empty or layer id
		//   - The subdirectories of directory at prefix X are located under Key = Nodes + Pack(X, 0, SUBDIR_NAME), Value = Prefix of subdirectory
		//   - For the root of the directory partition, X = the prefix of the nodes subspace

		internal static readonly Version LayerVersion = new(1, 0, 0);
		internal static readonly Slice LayerAttribute = Slice.FromStringAscii("layer");
		internal static readonly Slice HcaAttribute = Slice.FromStringAscii("hca");
		internal static readonly Slice VersionAttribute = Slice.FromStringAscii("version");
		internal static readonly Slice StampAttribute = Slice.FromStringAscii("stamp");

		/// <summary>Use this flag to make the Directory Layer start annotating the transactions with a descriptions of all operations.</summary>
		/// <remarks>
		/// This is only useful if you want to diagnose performance or read conflict issues.
		/// This will only work with logged transactions, obtained by applying the Logging Filter on a database instance
		/// </remarks>
		public static bool AnnotateTransactions { get; set; }

		/// <summary>Subspace where the content of each folder will be stored</summary>
		public ISubspaceLocation Content { get; }

		/// <summary>Random generator used by the internal allocators</summary>
		internal Random AllocatorRng { get; }

		/// <summary>Most current cache context</summary>
		internal CacheContext? Cache;

		/// <summary>Name of root directory of this layer</summary>
		/// <remarks>Returns String.Empty for the root Directory Layer, or the name of the partition</remarks>
		public string Name => this.Path.Name;

		/// <summary>Formatted path of the root directory of this layer</summary>
		public string FullName => this.Path.ToString();

		/// <summary>Gets the path for the root node of this <code>FdbDirectoryLayer</code></summary>
		/// <remarks>Normally constructed <code>DirectoryLayer</code>s have an empty path, but <code>DirectoryLayer</code>s returned by <see cref="IFdbDirectory.DirectoryLayer"/> for <see cref="IFdbDirectory"/>s inside of a <see cref="FdbDirectoryPartition"/> could have non-empty paths.</remarks>
		public FdbPath Path => FdbPath.Root;

		FdbDirectorySubspaceLocation IFdbDirectory.Location => new(this.Path);

		/// <summary>Returns the layer id for this <code>FdbDirectoryLayer</code>, which is always <see cref="string.Empty"/>.</summary>
		string IFdbDirectory.Layer => string.Empty;

		/// <summary>Self reference</summary>
		FdbDirectoryLayer IFdbDirectory.DirectoryLayer => this;

		/// <summary>Convert a relative path in this Directory Layer, into an absolute path from the root of partition of the database</summary>
		internal FdbPath PartitionSubPath(FdbPath path = default)
		{
			// If the DL is the root, the path is already absolute
			// If the DL is used by a partition, then the path of the partition will be prepended to the path
			return path.Count != 0 ? this.Path.Add(path) : this.Path;
		}

		void IFdbDirectory.CheckLayer(string? layer)
		{
			if (!string.IsNullOrEmpty(layer))
			{
				throw ThrowHelper.InvalidOperationException($"The directory layer {this.FullName} is not compatible with layer {layer}.");
			}
		}

		Task<FdbDirectorySubspace> IFdbDirectory.ChangeLayerAsync(IFdbTransaction trans, string newLayer)
		{
			throw ThrowHelper.NotSupportedException("You cannot change the layer of a Directory Layer.");
		}

		#region Constructors...

		/// <summary>Creates a new instance that will manage directories in FoundationDB.</summary>
		/// <param name="location">Location of the root of all the directories managed by this Directory Layer. Usually empty for the root partition of the database.</param>
		internal FdbDirectoryLayer(ISubspaceLocation location)
		{
			Contract.Debug.Requires(location != null);

			this.Content = location;
			this.AllocatorRng = new Random();
		}

		/// <summary>Create an instance of a Directory Layer located under a specific subspace and path</summary>
		/// <param name="location">Location of the Directory Layer's content. The nodes will be stored under the &lt;FE&gt; subspace</param>
		public static FdbDirectoryLayer Create(ISubspaceLocation location)
		{
			Contract.NotNull(location);
			return new(location);
		}

		#endregion

		#region Public Methods...

		#region CreateOrOpen / Open / Create

		/// <summary>Opens the directory with the given path. If the directory does not exist, it is created (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create or open</param>
		public async Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);

			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, Slice.Nil, allowCreate: true, allowOpen: true, throwOnError: true).ConfigureAwait(false))!;
		}

		/// <summary>Opens the directory with the given <paramref name="path"/>.
		/// An exception is thrown if the directory does not exist, or if a layer is specified and a different layer was specified when the directory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to open.</param>
		public async Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.CreateOrOpenInternalAsync(trans, null, location, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true).ConfigureAwait(false))!;
		}

		/// <summary>Creates a directory with the given <paramref name="path"/> (creating parent directories if necessary).
		/// An exception is thrown if the given directory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		public async Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: true).ConfigureAwait(false))!;
		}

		/// <inheritdoc />
		public async Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.CreateOrOpenInternalAsync(trans, null, location, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: false).ConfigureAwait(false))!;
		}

		/// <inheritdoc />
		public async ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.OpenCachedInternalAsync(trans, path, throwOnError: false).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public async ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbPath> paths)
		{
			Contract.NotNull(trans);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.OpenCachedInternalAsync(trans, (paths as FdbPath[]) ?? paths.ToArray(), throwOnError: false).ConfigureAwait(false);
		}

		/// <summary>Attempts to create a directory with the given <paramref name="path"/> (creating parent directories if necessary).</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		public async Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: Slice.Nil, allowCreate: true, allowOpen: false, throwOnError: false).ConfigureAwait(false);
		}

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public async Task<FdbDirectorySubspace> RegisterAsync(IFdbTransaction trans, FdbPath path, Slice prefix)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: true).ConfigureAwait(false))!;
		}

		/// <summary>Attempts to register an existing prefix as a directory with the given <paramref name="path"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to create</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		public async Task<FdbDirectorySubspace?> TryRegisterAsync(IFdbTransaction trans, FdbPath path, Slice prefix)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.CreateOrOpenInternalAsync(null, trans, location, prefix: prefix, allowCreate: true, allowOpen: false, throwOnError: false).ConfigureAwait(false);
		}

		#endregion

		#region Move / TryMove

		/// <summary>Moves the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		public async Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath)
		{
			Contract.NotNull(trans);

			EnsureAbsolutePath(in oldPath);
			EnsureAbsolutePath(in newPath);

			var oldLocation = VerifyPath(oldPath, nameof(oldPath));
			var newLocation = VerifyPath(newPath, nameof(newPath));

			if (oldLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be moved.");
			if (newLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be replaced.");

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: true).ConfigureAwait(false))!;
		}

		/// <summary>Attempts to move the directory found at <paramref name="oldPath"/> to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// Returns null if the old directory does not exist, a directory already exists at `new_path`, or the parent directory of `new_path` does not exist.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Path of the directory to move</param>
		/// <param name="newPath">New path of the directory</param>
		public async Task<FdbDirectorySubspace?> TryMoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath)
		{
			Contract.NotNull(trans);

			var oldLocation = VerifyPath(oldPath, nameof(oldPath));
			var newLocation = VerifyPath(newPath, nameof(newPath));

			if (oldLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be moved.");
			if (newLocation.IsRoot) throw new InvalidOperationException("The root directory cannot be replaced.");

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.MoveInternalAsync(trans, oldLocation, newLocation, throwOnError: false).ConfigureAwait(false);
		}

		#endregion

		#region MoveTo / TryMoveTo

		Task<FdbDirectorySubspace> IFdbDirectory.MoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			throw new InvalidOperationException("The root directory cannot be moved.");
		}

		Task<FdbDirectorySubspace?> IFdbDirectory.TryMoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath)
		{
			throw new InvalidOperationException("The root directory cannot be moved.");
		}

		#endregion

		#region Remove / TryRemove

		/// <summary>Removes the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public async Task RemoveAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);
			if (location.IsRoot) throw new InvalidOperationException("Cannot remove a directory layer");

			var metadata = await Resolve(trans).ConfigureAwait(false);
			await metadata.RemoveInternalAsync(trans, location, throwIfMissing: true).ConfigureAwait(false);
		}

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		public async Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);
			if (location.IsRoot) return false; // cannot remove directory layer itself

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.RemoveInternalAsync(trans, location, throwIfMissing: false).ConfigureAwait(false);
		}

		#endregion

		#region Exists

		/// <summary>Checks if a directory already exists</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to remove (including any subdirectories)</param>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		public async Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbPath path)
		{
			Contract.NotNull(trans);
			// no reason to disallow checking for the root directory (could be used to check if a directory layer is initialized?)

			var location = VerifyPath(path);
			if (location.Count == 0) return true;

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.ExistsInternalAsync(trans, location).ConfigureAwait(false);
		}

		#endregion

		#region List / TryList

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		public async Task<List<FdbPath>> ListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return (await metadata.ListInternalAsync(trans, location, throwIfMissing: true).ConfigureAwait(false))!;
		}

		/// <summary>Returns the list of subdirectories of directory at <paramref name="path"/>, if it exists.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to list</param>
		public async Task<List<FdbPath>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbPath path = default)
		{
			Contract.NotNull(trans);

			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);
			return await metadata.ListInternalAsync(trans, location, throwIfMissing: false).ConfigureAwait(false);
		}

		#endregion

		/// <summary>Change the layer id of the directory at <paramref name="path"/></summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Path of the directory to change</param>
		/// <param name="newLayer">New layer id of the directory</param>
		public async Task<FdbDirectorySubspace> ChangeLayerAsync(IFdbTransaction trans, FdbPath path, string newLayer)
		{
			Contract.NotNull(trans);
			Contract.NotNull(newLayer);
			var location = VerifyPath(path);

			var metadata = await Resolve(trans).ConfigureAwait(false);

			// Set the layer to the new value
			await metadata.ChangeLayerInternalAsync(trans, location, newLayer).ConfigureAwait(false);
			var newPath = path.WithLayer(newLayer);

			// And re-open the directory subspace
			return (await metadata.CreateOrOpenInternalAsync(null, trans, newPath, prefix: Slice.Nil, allowCreate: false, allowOpen: true, throwOnError: true).ConfigureAwait(false))!;
		}

		public override string ToString()
		{
			return $"DirectoryLayer({this.Content})";
		}

		#endregion

		#region Internal Helpers...

		[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Layer={Layer}")]
		internal readonly struct Node
		{

			public Node(FdbPath path, Slice prefix, string? layer, PartitionDescriptor partition, PartitionDescriptor parentPartition, Slice prefixInParentPartition, List<KeyValuePair<Slice, Slice>> validationChain)
			{
				Contract.Debug.Requires(partition != null && parentPartition != null);
				this.Prefix = prefix;
				this.Path = path;
				this.Layer = layer;
				this.Partition = partition;
				this.ParentPartition = parentPartition;
				this.PrefixInParentPartition = prefixInParentPartition;
				this.ValidationChain = validationChain;
			}

			public readonly Slice Prefix;
			public readonly FdbPath Path;
			public readonly string? Layer;
			public readonly PartitionDescriptor Partition;
			public readonly PartitionDescriptor ParentPartition;
			public readonly Slice PrefixInParentPartition;
			public readonly List<KeyValuePair<Slice, Slice>> ValidationChain;

			public bool Exists => !this.Prefix.IsNull;

		}

		public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
		{
			//note: we use the directory layer itself has the "token" key for the local data cache
			if (tr.Context.TryGetLocalData(this, out State? metadata))
			{
				return new(metadata);
			}
			return ResolveMetadata(tr);
		}

		private async ValueTask<State> ResolveMetadata(IFdbReadOnlyTransaction tr)
		{
			var content = await this.Content.TryResolve(tr).ConfigureAwait(false);
			if (content == null) throw new InvalidOperationException("Directory Layer content subspace was not found");

			var partition = new PartitionDescriptor(this.Path, content.GetPrefix(), null);

			var metadata = new State(this, partition, this.AllocatorRng);
			//TODO: locking?

			return tr.Context.GetOrCreateLocalData(this, metadata);
		}

		public sealed class State : ISubspaceContext, IHandleTransactionLifecycle
		{

			// We start at the state "neutral", and can either transition to "mutable" or "cached" state, but once there, the transaction is "locked" in only these types of operations
			// In state neutral, we can do anything, but then attempt at performing incompatible operations in this transaction should throw.
			// - "Neutral" operations can be performed in any state.
			// - "Mutating" operations can only be performed in the mutated or neutral state (and end up in the mutated state)
			// - "Cached" operations can only be performed in the cached or neutral state (and end up in the cached state)

			/// <summary>This transaction hasn't yet used a method that could mutate the tree</summary>
			internal const int STATUS_NEUTRAL = 0;

			/// <summary>This transaction has called an API that mutated the tree</summary>
			internal const int STATUS_MUTATED = 1;

			/// <summary>This transaction has called an API that used a cached tree </summary>
			internal const int STATUS_CACHED = 2;

			/// <summary>This transaction has been disposed</summary>
			internal const int STATUS_DEAD = 3;

			/// <summary>Current "state" of the transaction in regard to the Directory Layer</summary>
			/// <remarks>Once a transaction has called an API that mutates or use the cache, other kinds of operations are disallowed!</remarks>
			internal int Status;

			public FdbDirectoryLayer Layer { get; }

			internal PartitionDescriptor Partition { get; }

			public Random Allocator { get; }

			internal State(FdbDirectoryLayer layer, PartitionDescriptor partition, Random rng)
			{
				this.Layer = layer;
				this.Partition = partition;
				this.Allocator = rng;
			}

			private static void SetLayer(IFdbTransaction trans, PartitionDescriptor partition, Slice prefix, string layer)
			{
				Contract.Debug.Requires(layer != null);
				trans.Set(partition.GetLayerIdKey(prefix), FdbValue.ToTextUtf8(layer));
			}

			/// <summary>Finds a node subspace, given its path, by walking the tree from the root.</summary>
			/// <returns>Node if it was found, or null</returns>
			private static async Task<Node> FindAsync(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, FdbPath path)
			{
				Contract.Debug.Requires(tr != null && partition != null);

				// look for the node by traversing from the root down. jumping over when crossing a partition...

				var current = partition.Nodes.Data;

				var chain = new List<KeyValuePair<Slice, Slice>>();

				//TODO: maybe cache this?
				var partitionMetadataValue = await partition.GetStampValue(tr).ConfigureAwait(false);
				chain.Add(new(partition.GetStampKey().ToSlice(), partitionMetadataValue));

				int i = 0;
				var layer = FdbDirectoryPartition.LayerId; // the root is by convention a "partition"
				var parent = partition;
				var prefixInParentPartition  = current;
				while (i < path.Count)
				{
					if (AnnotateTransactions) tr.Annotate($"Looking for child {path[i]} under node {FdbKey.Dump(current)}...");

					// maybe use the node cache, if allowed
					current = await tr.GetAsync(partition.Nodes.Key(current, SUBDIRS, path[i].Name)).ConfigureAwait(false);

					if (current.IsNull)
					{
						return new Node(path, Slice.Nil, null, partition, parent, Slice.Nil, chain);
					}

					// get the layer id of this node
					layer = await tr.GetAsync(
						partition.GetLayerIdKey(current),
						(value, found)
							=> !found || value.Length == 0 ? ""
							 : value.SequenceEqual("partition"u8) ? FdbDirectoryPartition.LayerId
							 : Encoding.UTF8.GetString(value)
					).ConfigureAwait(false);
					if (AnnotateTransactions) tr.Annotate($"Found subfolder '{path[i].Name}' at {FdbKey.Dump(current)} ({layer})");

					parent = partition;

					prefixInParentPartition = current;
					if (layer == FdbDirectoryPartition.LayerId)
					{ // jump to that partition's node subspace
						partition = partition.CreateChild(path.Substring(0, i + 1), current);
						current = partition.Nodes.Data;

						partitionMetadataValue = await partition.GetStampValue(tr).ConfigureAwait(false);
						chain.Add(new(partition.GetStampKey().ToSlice(), partitionMetadataValue));
					}

					++i;
				}

				// patch the layer id, if it is missing from the last segment (can be omitted by caller)
				if (path.Count > 0 && !string.IsNullOrEmpty(layer))
				{
					var lastSeg = path[^1];
					if (lastSeg.LayerId != layer)
					{
						path = path.GetParent()[lastSeg.Name, layer];
					}
				}

				return new Node(path, current, layer, partition, parent, prefixInParentPartition, chain);
			}

			/// <summary>Open a subspace using the local cache</summary>
			internal async ValueTask<FdbDirectorySubspace?> OpenCachedInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path, bool throwOnError)
			{
				Contract.Debug.Requires(trans != null);

				EnsureAbsolutePath(in path);

				var ctx = await GetContext(trans).ConfigureAwait(false);
				Contract.Debug.Assert(ctx != null);

				if (EnsureCanCache(trans))
				{
					if (ctx.TryGetSubspace(trans, this, path, out var subspace, out var validationChain))
					{
						Contract.Debug.Assert(validationChain != null);
						if (validationChain.Count != 0)
						{
							trans.Context.AddValueChecks("DirectoryLayer", validationChain);
						}

						return subspace;
					}
				}

				return await OpenCachedInternalSlow(ctx, trans, path, throwOnError).ConfigureAwait(false);
			}

			private async Task<FdbDirectorySubspace?> OpenCachedInternalSlow(CacheContext context, IFdbReadOnlyTransaction readTrans, FdbPath path, bool throwOnError)
			{
				var existingNode = await FindAsync(readTrans, this.Partition, path).ConfigureAwait(false);

				// Path of the partition that contains the target directory (updated whenever we traverse partitions)

				if (existingNode.Exists)
				{
					var layer = path.LayerId;
					if (!string.IsNullOrEmpty(layer) && layer != existingNode.Layer)
					{
						throw new InvalidOperationException($"The directory {path} was created with incompatible layer '{layer}' instead of expected '{existingNode.Layer}'.");
					}
					var subspace = ContentsOfNode(existingNode.Path, existingNode.Prefix, existingNode.Layer, existingNode.ValidationChain, existingNode.Partition, existingNode.ParentPartition, context);

					if (AnnotateTransactions) readTrans.Annotate($"Add {path} to the cache with prefix {existingNode.Prefix}");
					context.AddSubspace(subspace, existingNode.ValidationChain);
					return subspace.WithContext(this);
				}
				else
				{
					if (throwOnError) throw new InvalidOperationException($"The directory {path} does not exist.");
					Contract.Debug.Ensures(existingNode.ValidationChain != null);
					return null;
				}
			}

			/// <summary>Open a subspace using the local cache</summary>
			internal async ValueTask<FdbDirectorySubspace?[]> OpenCachedInternalAsync(IFdbReadOnlyTransaction trans, ReadOnlyMemory<FdbPath> paths, bool throwOnError)
			{
				Contract.Debug.Requires(trans != null);

				var ctx = await GetContext(trans).ConfigureAwait(false);
				Contract.Debug.Assert(ctx != null);

				if (EnsureCanCache(trans))
				{
					var results = new FdbDirectorySubspace?[paths.Length];
					List<(Task<FdbDirectorySubspace?> Task, int Index)>? tasks = null;
					for (int i = 0; i < paths.Length; i++)
					{
						var path = paths.Span[i];
						EnsureAbsolutePath(in path);
						if (ctx.TryGetSubspace(trans, this, path, out var subspace, out var validationChain))
						{
							Contract.Debug.Assert(validationChain != null);
							if (validationChain.Count != 0)
							{
								trans.Context.AddValueChecks("DirectoryLayer", validationChain);
							}
							results[i] = subspace;
						}
						else
						{
							(tasks ??= [ ]).Add((OpenCachedInternalSlow(ctx, trans, path, throwOnError), i));
						}
					}

					if (tasks != null)
					{ // some of the directories were not in the cache!
						foreach (var (t, i) in tasks)
						{
							results[i] = await t.ConfigureAwait(false);
						}
					}
					return results;
				}
				else
				{
					var tasks = new List<Task<FdbDirectorySubspace?>>(paths.Length);
					for (int i = 0; i < paths.Length; i++)
					{
						var path = paths.Span[i];
						tasks.Add(CreateOrOpenInternalAsync(trans, null, path, Slice.Nil, false, true, throwOnError));
					}

					return await Task.WhenAll(tasks).ConfigureAwait(false);
				}
			}

			internal async Task<FdbDirectorySubspace?> CreateOrOpenInternalAsync(IFdbReadOnlyTransaction? readTrans, IFdbTransaction? trans, FdbPath path, Slice prefix, bool allowCreate, bool allowOpen, bool throwOnError)
			{
				Contract.Debug.Requires(readTrans != null || trans != null, "Need at least one transaction");
				Contract.Debug.Requires(readTrans == null || trans == null || ReferenceEquals(readTrans, trans), "The write transaction should be the same as the read transaction.");

				EnsureAbsolutePath(in path);

				if (path.IsRoot)
				{ // Root directory contains node metadata and so may not be opened.
					throw new InvalidOperationException("The root directory may not be opened.");
				}

				// to open an existing directory, we only need the read transaction
				// if none was specified, we can use the writable transaction
				readTrans ??= trans;
				if (readTrans == null) throw new ArgumentNullException(nameof(readTrans), "You must either specify either a read or write transaction!");

				await CheckReadVersionAsync(readTrans).ConfigureAwait(false);

				if (prefix.HasValue && this.Layer.Path.Count > 0)
				{
					throw new InvalidOperationException("Cannot specify a prefix in a partition.");
				}

				string layer = path.LayerId;

				var existingNode = await FindAsync(readTrans, this.Partition, path).ConfigureAwait(false);

				if (existingNode.Exists)
				{
					if (!allowOpen)
					{
						if (throwOnError) throw new InvalidOperationException($"The directory {path} already exists.");
						return null;
					}

					if (!string.IsNullOrEmpty(layer) && layer != existingNode.Layer)
					{
						bool fail = true;
						if (string.IsNullOrEmpty(existingNode.Layer))
						{ // path was created without any layer... should we create it?
							if (allowCreate && trans is not null)
							{
								// note: this is a very common case where some helper or test framework clears or re-create the path, without knowing the expected Layer,
								//  and the actual layer will fail otherwise when attempting to create the same path, this time with the correct Layer id.
								// => we will simply fill it now (since we are called via CreateOrOpenAsync, we would have set the layer anyway if the folder was not found

								if (AnnotateTransactions) trans.Annotate($"Patching missing layer '{layer}' for existing directory {path}.");
								SetLayer(trans, existingNode.Partition, existingNode.Prefix, layer);
								fail = false;
							}
						}
						if (fail)
						{
							throw new InvalidOperationException($"The directory {path} was created with incompatible layer '{existingNode.Layer}' instead of expected '{layer}'.");
						}
					}
					return ContentsOfNode(existingNode.Path, existingNode.Prefix, existingNode.Layer!, existingNode.ValidationChain, existingNode.Partition, existingNode.ParentPartition, null);
				}

				if (!allowCreate)
				{
					if (throwOnError) throw new InvalidOperationException($"The directory {path} does not exist.");
					return null;
				}

				// from there, we actually do need a writable transaction
				if (trans == null) throw new InvalidOperationException("A writable transaction is needed to create a new directory.");

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				// we need to recursively create any missing parents
				Slice parentPrefix;
				var partition  = this.Partition;
				var chain = existingNode.ValidationChain;
				if (path.Count > 1)
				{
					var parentSubspace = await CreateOrOpenInternalAsync(readTrans, trans, path.GetParent(), prefix: Slice.Nil, allowCreate: true, allowOpen: true, throwOnError: true).ConfigureAwait(false);
					Contract.Debug.Assert(parentSubspace != null);

					var parentNode = await FindAsync(readTrans, this.Partition, path.GetParent()).ConfigureAwait(false);
					partition = parentNode.Partition;
					parentPrefix = parentNode.Prefix;
					chain = parentNode.ValidationChain;
				}
				else
				{
					parentPrefix = partition.Nodes.Data;
				}

				if (prefix.IsNull)
				{ // automatically allocate a new prefix inside the ContentSubspace
					long id = await FdbHighContentionAllocator.AllocateAsync(trans, partition.Nodes.Key(partition.Nodes.Data, HcaAttribute).ToSubspace(), this.Allocator).ConfigureAwait(false);
					prefix = TupleEncoder.EncodeKey(partition.Content.Span, id);

					// ensure that there is no data already present under this prefix
					if (AnnotateTransactions) trans.Annotate($"Ensure that there is no data already present under prefix {prefix:K}");
					if (await trans.GetRange(KeyRange.StartsWith(prefix)).AnyAsync().ConfigureAwait(false))
					{
						throw new InvalidOperationException($"The database has keys stored at the prefix chosen by the automatic prefix allocator: {prefix:K}.");
					}

					// ensure that the prefix has not already been allocated
					if (AnnotateTransactions) trans.Annotate($"Ensure that the prefix {prefix:K} has not already been allocated");
					if (!(await IsPrefixFree(trans.Snapshot, partition, prefix).ConfigureAwait(false)))
					{
						throw new InvalidOperationException("The directory layer has manually allocated prefixes that conflict with the automatic prefix allocator.");
					}
				}
				else
				{
					if (AnnotateTransactions) trans.Annotate($"Ensure that the prefix {prefix:K} hasn't already been allocated");
					// ensure that the prefix has not already been allocated
					if (!(await IsPrefixFree(trans, partition, prefix).ConfigureAwait(false)))
					{
						throw new InvalidOperationException("The given prefix is already in use.");
					}
				}

				// initialize the metadata for this new directory

				if (AnnotateTransactions) trans.Annotate($"Registering the new prefix {prefix:K} into the folder sub-tree");

				trans.Set(partition.Nodes.Key(parentPrefix, SUBDIRS, path.Name), prefix);

				// initialize the new folder
				SetLayer(trans, partition, prefix, layer);

				if (layer == FdbDirectoryPartition.LayerId)
				{
					InitializePartition(trans, existingNode.Partition);
				}

				// note: creating a NEW folder has no impact on any cached node, since they only cache existing nodes.
				// => there is no need to touch the partition stamp key.

				// BUT, we could look into the cache if there was a previous version of this node (or children) at a different location,
				// which is very frequent in Delete -> Recreate operations when doing unit testing, or re-initializing something.
				var context = await GetContext(trans).ConfigureAwait(false);
				if (context.RemoveSubspace(path))
				{
					if (AnnotateTransactions) trans.Annotate($"Busted local cache for newly created directory {path}");
				}

				return ContentsOfNode(path, prefix, layer, chain, existingNode.Partition, existingNode.ParentPartition, null);
			}

			internal async Task<FdbDirectorySubspace?> MoveInternalAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath, bool throwOnError)
			{
				Contract.NotNull(trans);

				EnsureAbsolutePath(in oldPath);
				EnsureAbsolutePath(in newPath);

				if (oldPath.IsRoot)
				{
					throw new InvalidOperationException("The root directory may not be moved.");
				}
				if (newPath.IsRoot)
				{
					throw new InvalidOperationException("The root directory cannot be overwritten.");
				}
				if (newPath.StartsWith(oldPath))
				{
					throw new InvalidOperationException($"The destination directory '{newPath}' cannot be a sub-directory of the source directory '{oldPath}'.");
				}

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var oldNode = await FindAsync(trans, this.Partition, oldPath).ConfigureAwait(false);
				if (!oldNode.Exists)
				{
					if (throwOnError) throw new InvalidOperationException($"The source directory '{oldPath}' does not exist.");
					return null;
				}
				var newNode = await FindAsync(trans, this.Partition, newPath).ConfigureAwait(false);

				if (newNode.Exists)
				{
					if (throwOnError) throw new InvalidOperationException($"The destination directory '{newPath}' already exists. Remove it first.");
					return null;
				}

				Slice parentPrefix;
				PartitionDescriptor parentPartition;
				if (!newPath.IsRoot)
				{
					var parentNode = await FindAsync(trans, this.Partition, newPath.GetParent()).ConfigureAwait(false);
					if (!parentNode.Exists)
					{
						if (throwOnError) throw new InvalidOperationException($"The parent of the destination directory '{newPath}' does not exist. Create it first.");
						return null;
					}

					parentPartition = parentNode.Partition;
					parentPrefix = (parentPartition == parentNode.ParentPartition) ? parentNode.Prefix : parentNode.Partition.Nodes.Data;
				}
				else
				{
					parentPartition = this.Partition;
					parentPrefix = parentPartition.Nodes.Data;
				}

				// we have already checked that old and new are under this partition path, but one of them (or both?) could be under a sub-partition...
				if (oldNode.Partition != null && !oldNode.ParentPartition.Path.Equals(parentPartition.Path))
				{
					throw new InvalidOperationException($"Cannot move '{oldNode.Path}' to '{newNode.Path}' between partitions ('{oldNode.ParentPartition.Path}' != '{parentPartition.Path}').");
				}

				if (AnnotateTransactions) trans.Annotate($"Register the prefix {oldNode.Prefix} to its new location in the folder sub-tree");

				// make sure that the transaction is safe for mutation, and update the global metadata version if required
				if (EnsureCanMutate())
				{
					trans.TouchMetadataVersionKey();
				}

				trans.Set(parentPartition.Nodes.Key(parentPrefix, SUBDIRS, newPath.Name), oldNode.PrefixInParentPartition);

				await RemoveFromParent(trans, oldPath).ConfigureAwait(false);

				//REVIEW: should we consider moving a node a "safe" or "unsafe" operation relative to cached nodes?
				// => we assume that this is a rather rare operation (only during maintenance?) and is acceptable if this bust the cache for all nodes in the partition

				var context = await GetContext(trans).ConfigureAwait(false);
				if (context.RemoveSubspace(oldPath))
				{
					if (AnnotateTransactions) trans.Annotate($"Busted local cache for previous path {oldPath}");
				}
				if (context.RemoveSubspace(newPath))
				{
					if (AnnotateTransactions) trans.Annotate($"Busted local cache for new path {newPath}");
				}

				TouchPartitionMetadataKey(trans, this.Partition);

				//BUGBUG: we need to recalculate the "validation chain" with the new path!
				return ContentsOfNode(newPath, oldNode.Prefix, oldNode.Layer, oldNode.ValidationChain, newNode.Partition, newNode.ParentPartition, null);
			}

			internal async Task<bool> RemoveInternalAsync(IFdbTransaction trans, FdbPath path, bool throwIfMissing)
			{
				Contract.NotNull(trans);

				EnsureAbsolutePath(in path);

				// We don't allow removing the root directory, because it would probably end up wiping out all the database.
				if (path.IsRoot) throw new InvalidOperationException("The root directory may not be removed.");

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var n = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);
				if (!n.Exists)
				{
					if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
					return false;
				}

				// make sure that the transaction is safe for mutation, and update the global metadata version if required
				if (EnsureCanMutate())
				{
					trans.TouchMetadataVersionKey();
				}

				// Delete the node subtree and all the data
				await RemoveRecursive(trans, n.Partition, n.Prefix).ConfigureAwait(false);

				// Remove the node from the tree
				await RemoveFromParent(trans, path).ConfigureAwait(false);

				// It is CRITICAL that any other process drops any cache entry for this node, so we will touch the partition stamp.
				// => this will bust ALL the cached nodes in this partition on ALL processes, but we assume that deleting a node is
				//    a lot less frequent than creating a new node.

				var context = await GetContext(trans).ConfigureAwait(false);
				if (context.RemoveSubspace(path))
				{
					if (AnnotateTransactions) trans.Annotate($"Busted local cache for removed directory {path}");
				}

				TouchPartitionMetadataKey(trans, n.Partition);

				return true;
			}

			internal async Task<List<FdbPath>?> ListInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path, bool throwIfMissing)
			{
				Contract.NotNull(trans);

				EnsureAbsolutePath(in path);

				await CheckReadVersionAsync(trans).ConfigureAwait(false);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists)
				{
					if (throwIfMissing) throw new InvalidOperationException($"The directory '{path}' does not exist.");
					return null;
				}

				return (await SubdirNamesAndNodes(trans, node.Partition, node.Prefix, includeLayers: true).ConfigureAwait(false))
					.Select(kvp => node.Path[new FdbPathSegment(kvp.Name, kvp.LayerId)])
					.ToList()
					;
			}

			internal async Task<bool> ExistsInternalAsync(IFdbReadOnlyTransaction trans, FdbPath path)
			{
				Contract.Debug.Requires(trans != null);

				EnsureAbsolutePath(in path);

				await CheckReadVersionAsync(trans).ConfigureAwait(false);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists) return false;

				return true;
			}

			internal async Task ChangeLayerInternalAsync(IFdbTransaction trans, FdbPath path, string newLayer)
			{
				Contract.Debug.Requires(trans != null && newLayer != null);

				EnsureAbsolutePath(in path);

				await CheckWriteVersionAsync(trans).ConfigureAwait(false);

				var node = await FindAsync(trans, this.Partition, path).ConfigureAwait(false);

				if (!node.Exists)
				{
					throw new InvalidOperationException($"The directory '{path}' does not exist, or as already been removed.");
				}

				SetLayer(trans, node.Partition, node.Prefix, newLayer);

				//REVIEW: changing the layer id is very rare, do we really need to bust the cache of all nodes in this partition?
				var context = await GetContext(trans).ConfigureAwait(false);
				if (context.RemoveSubspace(path))
				{
					if (AnnotateTransactions) trans.Annotate($"Busted local cache for removed directory {path}");
				}

				TouchPartitionMetadataKey(trans, node.Partition);
			}

			private async Task<Slice> CheckReadVersionAsync(IFdbReadOnlyTransaction trans)
			{
				//TODO: we could defer the check using the cache context!

				var value = await trans.GetAsync(this.Partition.GetVersionKey()).ConfigureAwait(false);
				if (!value.IsNullOrEmpty)
				{
					CheckVersion(value, false);
				}

				return value;
			}

			private async Task CheckWriteVersionAsync(IFdbTransaction trans)
			{
				var version = await CheckReadVersionAsync(trans).ConfigureAwait(false);

				if (version.IsNullOrEmpty)
				{
					InitializePartition(trans, this.Partition);
				}
			}

			private void InitializePartition(IFdbTransaction trans, PartitionDescriptor partition)
			{
				// Set the version key
				trans.Set(partition.GetVersionKey(), MakeVersionValue());
				trans.Set(partition.GetStampKey(), FdbValue.Zero32);
			}

			private static void TouchPartitionMetadataKey(IFdbTransaction trans, PartitionDescriptor partition)
			{
				trans.Annotate($"Bump the stamp key of partition {partition.Path}");
				trans.AtomicIncrement32(partition.GetStampKey());
			}

			private static Slice MakeVersionValue()
			{
				var writer = new SliceWriter(3 * 4);
				writer.WriteInt32(LayerVersion.Major);
				writer.WriteInt32(LayerVersion.Minor);
				writer.WriteInt32(LayerVersion.Build);
				return writer.ToSlice();
			}

			private async Task<bool> NodeContainingKey(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice key)
			{
				// Right now this is only used for _is_prefix_free(), but if we add
				// parent pointers to directory nodes, it could also be used to find a
				// path based on a key.

				if (partition.Nodes.Contains(key))
				{ 
					return true;
				}

				var kvp = await tr
					.GetRange(
						partition.Nodes,
						partition.Nodes.Key(key).Successor()
					)
					.LastOrDefaultAsync()
					.ConfigureAwait(false);

				if (kvp.Key.HasValue)
				{
					var prevPrefix = TuPack.DecodeFirst<Slice>(partition.Nodes.GetSuffix(kvp.Key));
					if (key.StartsWith(prevPrefix))
					{
						return true;
					}
				}

				return false;
			}

			/// <summary>Returns a new Directory Subspace given its node subspace, path and layer id</summary>
			private FdbDirectorySubspace ContentsOfNode(FdbPath path, Slice prefix, string? layer, IReadOnlyList<KeyValuePair<Slice, Slice>> validationChain, PartitionDescriptor partition, PartitionDescriptor parentPartition, ISubspaceContext? context)
			{
				Contract.Debug.Requires(partition != null && parentPartition != null);

				if (layer == FdbDirectoryPartition.LayerId)
				{
					var descriptor = new DirectoryDescriptor(this.Layer, path, partition.Content.ToSlice(), FdbDirectoryPartition.LayerId, partition, validationChain);
					return new FdbDirectoryPartition(descriptor, parentPartition, context, false);
				}
				else
				{
					var descriptor = new DirectoryDescriptor(this.Layer, path, prefix, layer, partition, validationChain);
					return new FdbDirectorySubspace(descriptor, context, false);
				}
			}

			/// <summary>Returns the list of names and nodes of all children of the specified node</summary>
			private async Task<List<(string Name, string? LayerId, Slice Prefix)>> SubdirNamesAndNodes(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice prefix, bool includeLayers)
			{
				Contract.Debug.Requires(tr != null && partition != null);

				var items = await tr
					.GetRange(partition.Nodes.Key(prefix, SUBDIRS).ToRange())
					.Select(kvp => (Name: TuPack.DecodeLast<string>(kvp.Key) ?? string.Empty, Prefix: kvp.Value))
					.ToListAsync()
					.ConfigureAwait(false);

				if (items.Count == 0)
				{
					return [ ];
				}

				// fetch the layers from the corresponding directories
				var layers = includeLayers
					? await tr.GetValuesAsync(items, partition, static (p, item) => p.GetLayerIdKey(item.Prefix)).ConfigureAwait(false)
					: null;

				var res = new List<(string, string?, Slice)>(items.Count);
				for (int i = 0; i < items.Count; i++)
				{
					res.Add((items[i].Name, layers != null ? (layers[i].ToStringUtf8() ?? string.Empty) : null, items[i].Prefix));
				}
				return res;
			}

			/// <summary>Remove an existing node from its parents</summary>
			/// <returns>True if the parent node was found, otherwise false</returns>
			private async Task RemoveFromParent(IFdbTransaction tr, FdbPath path)
			{
				Contract.Debug.Requires(tr != null);

				var parent = await FindAsync(tr, this.Partition, path.GetParent()).ConfigureAwait(false);
				if (parent.Exists)
				{
					if (AnnotateTransactions) tr.Annotate($"Removing path {path} from its parent folder at {parent.Prefix}");
					tr.Clear(GetSubDirKey(parent.Partition, parent.Prefix, path.Name));
				}
			}

			/// <summary>Recursively remove a node (including the content), all its children</summary>
			private async Task RemoveRecursive(IFdbTransaction tr, PartitionDescriptor partition, Slice prefix)
			{
				Contract.Debug.Requires(tr != null && partition != null);

				//note: we could use Task.WhenAll to remove the children, but there is a risk of task explosion if the subtree is very large...
				var children = await SubdirNamesAndNodes(tr, partition, prefix, includeLayers: false).ConfigureAwait(false);

				await Task.WhenAll(children.Select(child => RemoveRecursive(tr, partition, child.Prefix))).ConfigureAwait(false);

				// remove ALL the contents
				if (AnnotateTransactions) tr.Annotate($"Removing all content located under {KeyRange.StartsWith(prefix)}");
				//TODO: REVIEW: we could get the prefix without calling ContentsOfNode here!
				tr.ClearRange(KeyRange.StartsWith(prefix));

				// and all the metadata for this folder
				if (AnnotateTransactions) tr.Annotate($"Removing all metadata for folder under {partition.Nodes.Key(prefix)}");
				tr.ClearRange(partition.Nodes.Key(prefix).ToRange(inclusive: true));
			}

			private async Task<bool> IsPrefixFree(IFdbReadOnlyTransaction tr, PartitionDescriptor partition, Slice prefix)
			{
				Contract.Debug.Requires(tr != null);

				// Returns true if the given prefix does not "intersect" any currently
				// allocated prefix (including the root node). This means that it neither
				// contains any other prefix nor is contained by any other prefix.

				if (prefix.IsNullOrEmpty) return false;

				if (await NodeContainingKey(tr, partition, prefix).ConfigureAwait(false))
				{
					return false;

				}

				return !(await tr
					.GetRange(
						partition.Nodes.Key(prefix),
						partition.Nodes.Key(prefix).NextSibling()
					)
					.AnyAsync()
					.ConfigureAwait(false));
			}

			private static Slice GetSubDirKey(PartitionDescriptor partition, Slice prefix, string segment)
			{
				Contract.Debug.Requires(partition != null);

				// for a path equal to ("foo","bar","baz") and index = -1, we need to generate (parent, SUBDIRS, "baz")
				// but since the last item of path can be of any type, we will use tuple splicing to copy the last item without changing its type
				return partition.Nodes.Key(prefix, SUBDIRS, segment).ToSlice();
			}

			/// <summary>Ensure that this transaction can perform mutation operations</summary>
			/// <returns>True the first time a DL mutation is performed with this transaction, of false if it already did</returns>
			/// <exception cref="InvalidOperationException">If this transaction was already used for cached operations</exception>
			internal bool EnsureCanMutate()
			{
				while (true)
				{
					var state = Volatile.Read(ref this.Status);
					switch (state)
					{
						case STATUS_CACHED:
						{
							return true;
						}
						case STATUS_NEUTRAL:
						{ // first mutation?
							if (Interlocked.CompareExchange(ref this.Status, STATUS_MUTATED, STATUS_NEUTRAL) != STATUS_NEUTRAL)
							{ // another thread is racing with us! try again
								continue;
							}

							// yes, update the global metadata version!
							return true;
						}
						case STATUS_MUTATED:
						{ // already mutated
							return false;
						}
					}
				}
			}

			/// <summary>Ensure that this transaction can perform caching operations</summary>
			/// <returns>True the first time a DL cached operation is performed with this transaction, of false if it already did</returns>
			/// <exception cref="InvalidOperationException">If this transaction was already used for mutating operations</exception>
			internal bool EnsureCanCache(IFdbReadOnlyTransaction tr)
			{
				while (true)
				{
					var state = Volatile.Read(ref this.Status);
					switch (state)
					{
						case STATUS_MUTATED:
						{
							//throw new InvalidOperationException("Cannot perform a cache operation on the Directory Layer inside a transaction that has already mutated the tree.");
							return false;
						}
						case STATUS_NEUTRAL:
						{ // first cache?
							if (Interlocked.CompareExchange(ref this.Status, STATUS_CACHED, STATUS_NEUTRAL) != STATUS_NEUTRAL)
							{ // another thread is racing with us! try again
								continue;
							}
							return true;
						}
						case STATUS_CACHED:
						{ // already cached
							return true;
						}
					}
				}
			}

			public void Dispose()
			{
				if (Interlocked.Exchange(ref this.Status, STATUS_DEAD) != STATUS_DEAD)
				{
					this.Context = null;
				}
			}

			internal async ValueTask<CacheContext> GetContext(IFdbReadOnlyTransaction trans)
			{
				Contract.NotNull(trans);

				// We will first check the value of the global \xff/metadataVersion key.
				// - If it hasn't changed, then we assume that the current context is still valid
				// - If it has changed, then we will have to read our local metadata version (stored in the partition)
				//  - If it hasn't changed, the current context is still valid, and can be updated to the last global metadata version
				//  - If it has changed, then we (for now) throw away the entire cache and start a new one.

				var context = Volatile.Read(ref this.Context) ?? this.Layer.Cache;
				if (context != null)
				{
					if (trans.Context.TestValueCheckFromPreviousAttempt("DirectoryLayer") != FdbValueCheckResult.Failed)
					{ // all good!
						if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context #{context.ReadVersion} likely still valid (no failed value-checks at attempt #{trans.Context.Retries})");

						// we may need to re-register the callback on the transaction
						if (Interlocked.Exchange(ref this.CallbackRegistered, 1) == 0)
						{
							trans.Context.Observe(this);
						}

						return context;
					}
					// the previous attempt yielded _at least_ one difference!
					// => right now we don't distinguish *which* directory changed, so we throw out the entire cache.

					if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context #{context.ReadVersion} failed a value-check in previous transaction attempt!");

					// discard this cache (if it hasn't been changed by another thread yet)
					Interlocked.CompareExchange(ref this.Context, null, context);
				}

				//TODO: if this is the first call on this transaction, we must check the read layer version!

				// we need to know the read-version where this cache context was created
				long rv = await trans.GetReadVersionAsync().ConfigureAwait(false);

				// create a new context!
				context = new CacheContext(this.Layer, rv);

				var global = this.Layer.Cache;
				if (global == null || global.ReadVersion < rv)
				{ // make this the most recent context!
					this.Layer.Cache = context;
					if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context #{context.ReadVersion} set as new global context.");
				}
				else
				{
					if (AnnotateTransactions) trans.Annotate($"{this.Layer} cache context #{context.ReadVersion} has been superseded by newer global context #{global.ReadVersion}.");
				}

				// attach that context to the transaction
				if (Interlocked.Exchange(ref this.Context, context) == null)
				{
					if (Interlocked.Exchange(ref this.CallbackRegistered, 1) == 0)
					{
						trans.Context.Observe(this);
					}
				}

				return context;
			}

			private volatile int CallbackRegistered;

			/// <summary>Called when the transaction succeeds or fails, in order to valid/invalidate the cache</summary>
			void IHandleTransactionLifecycle.OnTransactionStateChanged(FdbOperationContext ctx, FdbTransactionState state)
			{
				if (state is not (FdbTransactionState.Commit or FdbTransactionState.Completed))
				{ // reset the context in the initial state
					Interlocked.Exchange(ref this.Status, STATUS_NEUTRAL);
					this.Context = null;
				}
				else
				{ // poison the context
					Dispose();
				}
			}

			private CacheContext? Context;

			/// <inheritdoc />
			string ISubspaceContext.Name => this.Context?.DirectoryLayer.FullName ?? "<invalid>";

			/// <inheritdoc />
			bool ISubspaceContext.IsValid => Volatile.Read(ref this.Status) != STATUS_DEAD;

			/// <inheritdoc />
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			void ISubspaceContext.EnsureIsValid()
			{
				if (Volatile.Read(ref this.Status) == STATUS_DEAD) throw CannotUseCacheContext();
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			private static Exception CannotUseCacheContext() => new InvalidOperationException("Directory Subspace context cannot be used outside its origination transaction.");

		}

		internal static void EnsureAbsolutePath(in FdbPath path)
		{
			if (!path.IsAbsolute)
			{
				throw new ArgumentException("The Directory Layer cannot process relative paths.", nameof(path));
			}
		}

		internal FdbPath VerifyPath(in FdbPath path, string? argName = null)
		{
			if (path.IsAbsolute)
			{
				if (!path.StartsWith(this.Path)) throw new ArgumentException("The specified path is outside the current Directory Layer", argName ?? nameof(path));
				return path;
			}
			return this.Path[path];
		}

		private static void CheckVersion(Slice value, bool writeAccess)
		{
			// the version is stored as 3 x 32-bit unsigned int, so (1, 0, 0) will be "<01><00><00><00> <00><00><00><00> <00><00><00><00>"
			var reader = new SliceReader(value);
			var major = reader.ReadUInt32();
			var minor = reader.ReadUInt32();
			var upgrade = reader.ReadUInt32();

			if (major > LayerVersion.Major) throw new InvalidOperationException($"Cannot load directory with version {major}.{minor}.{upgrade} using directory layer {FdbDirectoryLayer.LayerVersion}.");
			if (writeAccess && minor > LayerVersion.Minor) throw new InvalidOperationException($"Directory with version {major}.{minor}.{upgrade} is read-only when opened using directory layer {FdbDirectoryLayer.LayerVersion}.");
		}


		#endregion

		/// <summary>Represents the cached state used by the DirectoryLayer to map key prefixes to subspaces</summary>
		[DebuggerDisplay("Id={Id}, RV={ReadVersion}")]
		internal sealed class CacheContext : ISubspaceContext
		{
			// Multiple transactions can link to the same cache context
			// The directory layer keeps the last N contexts in cache
			// It will be reclaimed by the GC once nobody points to it (no cached by the DL, and no more active transactions)

			public FdbDirectoryLayer DirectoryLayer { get; }

			/// <summary>Read version at which this context was last used successfully</summary>
			public long ReadVersion { get; }

			/// <summary>Version of the Directory Layer in the database</summary>
			// ReSharper disable once MemberHidesStaticFromOuterClass
			public Slice LayerVersion { get; set; }
			// content of [PARTITION, "version"] key

			/// <summary>Cache for subspaces that have been resolved successfully</summary>
			/// <remarks>Missing subspaces are NOT cached</remarks>
			public Dictionary<FdbPath, (FdbDirectorySubspace Subspace, IReadOnlyList<KeyValuePair<Slice, Slice>> ValidationChain)> CachedSubspaces { get; } = new();

			private ReaderWriterLockSlim Lock { get; } = new();

			private static int IdCounter;

			/// <summary>Unique ID of this Cache Context</summary>
			public readonly int Id = Interlocked.Increment(ref IdCounter);

			public CacheContext(FdbDirectoryLayer dl, long readVersion)
			{
				Contract.NotNull(dl);

				this.DirectoryLayer = dl;
				this.ReadVersion = readVersion;
			}

			/// <summary>Looks up a subspace in the cache</summary>
			/// <param name="tr">Parent transaction</param>
			/// <param name="state"></param>
			/// <param name="path">Absolute path to the subspace</param>
			/// <param name="subspace">If the method returns <see langword="true"/>, receives the cached subspace instance (or null if the cache knows that the subspace does not exist).</param>
			/// <param name="validationCain">If the method returns <see langword="true"/>, receives the validation chain</param>
			/// <returns>True if the cache contains information about the subspace.</returns>
			public bool TryGetSubspace(IFdbReadOnlyTransaction tr, State state, FdbPath path, out FdbDirectorySubspace? subspace, [MaybeNullWhen(false)] out IReadOnlyList<KeyValuePair<Slice, Slice>> validationCain)
			{
				Contract.Debug.Requires(tr != null);
				subspace = null;
				validationCain = null;

				(FdbDirectorySubspace Subspace, IReadOnlyList<KeyValuePair<Slice, Slice>> ValidationChain) candidate;

				using (this.Lock.GetReadLock())
				{
					if (!this.CachedSubspaces.TryGetValue(path, out candidate))
					{ // not in the cache => we don't know
						if (AnnotateTransactions) tr.Annotate($"{this.DirectoryLayer} subspace MISS for {path}");
						return false;
					}
				}

				if (AnnotateTransactions) tr.Annotate($"{this.DirectoryLayer} subspace HIT for {path}: {candidate.Subspace}");

				// the subspace was created with another context, we must migrate it to the current transaction's context
				subspace = candidate.Subspace.WithContext(state);
				validationCain = candidate.ValidationChain;
				return true;
			}

			/// <summary>Adds a resolved subspace to the cache</summary>
			public void AddSubspace(FdbDirectorySubspace subspace, IReadOnlyList<KeyValuePair<Slice, Slice>> validationChain)
			{
				Contract.Debug.Requires(subspace != null && validationChain != null);

				using (this.Lock.GetWriteLock())
				{
					this.CachedSubspaces[subspace.Path] = (subspace, validationChain);
				}
			}

			/// <summary>Removes a subspace from the cache</summary>
			public bool RemoveSubspace(FdbPath path)
			{
				// find the list of children of this path
				List<FdbPath>? pathsToRemove = null;
				using (this.Lock.GetUpgradableReadLock())
				{
					foreach (var kv in this.CachedSubspaces)
					{
						if (kv.Key.Equals(path) || kv.Key.IsChildOf(path))
						{
							(pathsToRemove ??= []).Add(kv.Key);
						}
					}

					if (pathsToRemove != null)
					{ // clear obsolete entries

						using (this.Lock.GetWriteLock())
						{
							foreach (var p in pathsToRemove)
							{
								this.CachedSubspaces.Remove(p);
							}
							return true;
						}
					}

					return false;
				}
			}

			/// <inheritdoc />
			public string Name => this.DirectoryLayer.FullName;

			/// <inheritdoc />
			public bool IsValid => true;

			/// <inheritdoc />
			public void EnsureIsValid()
			{
				//TODO?
			}

		}

		[DebuggerDisplay("Path={Path}, Prefix={Content}, Parent=({Parent})")]
		internal sealed class PartitionDescriptor
		{

			public FdbPath Path { get; }

			public PartitionDescriptor? Parent { get; }

			public FdbRawKey Content { get; }

			public FdbRawKey Nodes { get; }

			private Slice CachedStampValue { get; set; }

			public PartitionDescriptor(FdbPath path, Slice content, PartitionDescriptor? parent)
			{
				Contract.Debug.Requires(path.IsAbsolute && !content.IsNull);

				// the last segment must have the expected layer id
				if (path.Count > 0 && string.IsNullOrEmpty(path[^1].LayerId))
				{
					path = path.GetParent()[path[^1].Name, FdbDirectoryPartition.LayerId];
				}

				this.Path = path;
				this.Parent = parent;
				this.Content = FdbKey.FromBytes(content);
				this.Nodes = FdbKey.FromBytes(content + 0xFE);
			}

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public FdbTupleSuffixKey<STuple<Slice>> GetVersionKey() => this.Nodes.Key(VersionAttribute);

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public FdbTupleSuffixKey<STuple<Slice, Slice>> GetStampKey() => this.Nodes.Key(this.Nodes.Data, StampAttribute);

			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public FdbTupleSuffixKey<STuple<Slice, Slice>> GetLayerIdKey(Slice prefix) => this.Nodes.Key(prefix, LayerAttribute);

			/// <summary>Return a child partition of the current partition</summary>
			public PartitionDescriptor CreateChild(FdbPath path, Slice prefix)
			{
				Contract.Debug.Requires(path.IsChildOf(this.Path), "Partition path is outside parent");
				return new(path, prefix, this);
			}

			public ValueTask<Slice> GetStampValue(IFdbReadOnlyTransaction tr)
			{
				var value = this.CachedStampValue;
				if (value.IsNull)
				{
					return ReadStampValue(tr);
				}
				return new(value);

			}

			private async ValueTask<Slice> ReadStampValue(IFdbReadOnlyTransaction tr)
			{
				var value = await tr.GetAsync(this.GetStampKey()).ConfigureAwait(false);
				this.CachedStampValue = value;
				return value;
			}

			public override string ToString() => $"PartitionDescriptor(Path={this.Path}, Prefix={this.Content:K}, Parent=({this.Parent?.Path}, {this.Parent?.Content:K}))";

		}

		[DebuggerDisplay("Path={Path}, Prefix={Prefix}, Layer={Layer}, Partition={Partition.Path}|{Partition.Content}")]
		internal sealed class DirectoryDescriptor
		{

			public DirectoryDescriptor(FdbDirectoryLayer directoryLayer, FdbPath path, Slice prefix, string? layer, PartitionDescriptor partition, IReadOnlyList<KeyValuePair<Slice, Slice>> validationChain)
			{
				Contract.Debug.Requires(directoryLayer != null && partition != null && path.StartsWith(partition.Path));

				this.DirectoryLayer = directoryLayer;
				this.Path = path;
				this.Prefix = prefix;
				this.Layer = layer ?? string.Empty;
				this.Partition = partition;
				this.ValidationChain = validationChain;

				Contract.Debug.Ensures(this.DirectoryLayer != null);
			}

			/// <summary>Absolute path of this directory, from the root directory</summary>
			public FdbPath Path { get; }

			public Slice Prefix { get; }

			public PartitionDescriptor Partition { get; }

			/// <summary>Instance of the DirectoryLayer that was used to create or open this directory</summary>
			public FdbDirectoryLayer DirectoryLayer { get; }

			/// <summary>Layer id of this directory</summary>
			public string Layer { get; }

			/// <summary>List of all (key, value) pairs that were used to look up this directory</summary>
			/// <remarks>These keys can be used as "value checks" to support caching of subspaces</remarks>
			public IReadOnlyList<KeyValuePair<Slice, Slice>> ValidationChain { get; }

			public override string ToString() => $"DirectoryDescriptor(Path={this.Path}, Prefix={this.Prefix:K}, Layer={this.Layer}, Partition=({this.Partition.Path}, {this.Partition.Content:K}))";

		}

	}

	/// <summary>Helper interface that can map keys to their corresponding directories</summary>
	[PublicAPI]
	public interface IFdbDirectoryLayerMapper
	{

		/// <summary>Returns the map of all known directories</summary>
		IReadOnlyDictionary<FdbPath, Slice> GetPaths();

		/// <summary>Returns the prefix that corresponds to a given path, if it is known</summary>
		/// <param name="path">Path to map</param>
		/// <param name="prefix">Receives the key prefix for this path</param>
		/// <returns><c>true</c> if the path is known by this mapper; otherwise, <c>false</c></returns>
		bool TryMapPath(FdbPath path, out Slice prefix);

		/// <summary>Finds the path that contains a given key in the database</summary>
		/// <param name="key">Key in the database snapshot</param>
		/// <param name="path">Receives the path to the directory subspace that contains this key</param>
		/// <param name="mappedKey">Receives the tail of the key, minus the path prefix.</param>
		/// <returns><c>true</c> if key belongs to a known path; otherwise, <c>false</c></returns>
		bool TryMapKey(Slice key, out FdbPath path, out Slice mappedKey);

	}

}
