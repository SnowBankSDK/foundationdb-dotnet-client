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

	/// <summary>Represents a directory in the <code>DirectoryLayer</code>. A <code>Directory</code> stores the path at which it is located and the layer that was used to create it.</summary>
	/// <remarks>The IFdbDirectory interface contains methods to operate on itself and its subdirectories.</remarks>
	[PublicAPI]
	public interface IFdbDirectory
	{
		/// <summary>Name of this <code>Directory</code>.</summary>
		string Name { get; }

		/// <summary>Full name of this <code>Directory</code></summary>
		/// <remarks>This string does not include the layer id of each path segments. Please use <c>dir.<see cref="Path">Path</see>.<see cref="FdbPath.ToString()">ToString()</see></c> in order to get a roundtrip-able string representation of the path of this subspace.</remarks>
		string FullName { get; }

		/// <summary>Gets the location that points to this <code>Directory</code></summary>
		FdbDirectorySubspaceLocation Location { get; }

		/// <summary>Gets the path represented by this <code>Directory</code>.</summary>
		/// <remarks>This path includes the layers id of the directory and all its parent.</remarks>
		FdbPath Path { get; }

		/// <summary>Gets the layer id that was stored when this <code>Directory</code> was created.</summary>
		string Layer { get; }

		/// <summary>Get the <code>DirectoryLayer</code> that was used to create this <code>Directory</code>.</summary>
		FdbDirectoryLayer DirectoryLayer { get; }

		/// <summary>Opens a subdirectory with the given path.
		/// If the subdirectory does not exist, it is created (creating intermediate subdirectories if necessary).
		/// If layer is specified, it is checked against the layer of an existing subdirectory or set as the layer of a new subdirectory.
		/// </summary>
		Task<FdbDirectorySubspace> CreateOrOpenAsync(IFdbTransaction trans, FdbPath subPath);

		/// <summary>Opens a subdirectory with the given <paramref name="path"/>.
		/// An exception is thrown if the subdirectory does not exist, or if a layer is specified and a different layer was specified when the subdirectory was created.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		Task<FdbDirectorySubspace> OpenAsync(IFdbReadOnlyTransaction trans, FdbPath path);

		/// <summary>Attempts to open the directory with the given <paramref name="path"/>.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="path">Relative path of the subdirectory to open</param>
		/// <returns>Returns the directory if it exists, or <c>null</c> if it was not found</returns>
		Task<FdbDirectorySubspace?> TryOpenAsync(IFdbReadOnlyTransaction trans, FdbPath path);

		/// <summary>Attempts to open a subdirectory with the given <paramref name="path"/>, using the partition's cache context.</summary>
		/// <returns>Returns the directory if it exists, or null if it was not found</returns>
		/// <remarks>The instance returned MUST NOT be stored or kept outside the context of the transaction!
		/// You must call <see cref="TryOpenCachedAsync(IFdbReadOnlyTransaction, FdbPath)"/> on every new transaction to obtain either the previously cached instance, or a new instance.
		/// Attempting to use a cached instance outside the transaction that produced it may throw exceptions!
		/// </remarks>
		ValueTask<FdbDirectorySubspace?> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, FdbPath path);

		/// <summary>Opens multiple subdirectories with the given <paramref name="paths"/>, using the partition's cache context.</summary>
		/// <returns>Returns the list directories, in the same order. If a directory does not exist, the corresponding slot will contain <c>null</c></returns>
		/// <remarks>The instances returned MUST NOT be stored or kept outside the context of the transaction!
		/// You must call <see cref="TryOpenCachedAsync(IFdbReadOnlyTransaction, IEnumerable{FdbPath})"/> on every new transaction to obtain either the previously cached instances, or a new instances.
		/// Attempting to use a cached instances outside the transaction that produced them may throw exceptions!
		/// </remarks>
		ValueTask<FdbDirectorySubspace?[]> TryOpenCachedAsync(IFdbReadOnlyTransaction trans, IEnumerable<FdbPath> paths);
		//REVIEW: only keep the version that accept layers, and use an extension method instead?

		/// <summary>Creates a subdirectory with the given <paramref name="subPath"/> (creating intermediate subdirectories if necessary).
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		Task<FdbDirectorySubspace> CreateAsync(IFdbTransaction trans, FdbPath subPath);

		/// <summary>Creates a subdirectory with the given <paramref name="subPath"/> (creating intermediate subdirectories if necessary), unless it already exists.
		/// An exception is thrown if the given subdirectory already exists.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Relative path of the subdirectory to create</param>
		Task<FdbDirectorySubspace?> TryCreateAsync(IFdbTransaction trans, FdbPath subPath);

		/// <summary>Registers an existing prefix as a directory with the given <paramref name="subPath"/> (creating parent directories if necessary). This method is only indented for advanced use cases.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to create</param>
		/// <param name="prefix">The directory will be created with the given physical prefix; otherwise a prefix is allocated automatically.</param>
		Task<FdbDirectorySubspace> RegisterAsync(IFdbTransaction trans, FdbPath subPath, Slice prefix);

		/// <summary>Moves the specified subdirectory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the subdirectory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the subdirectory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful.</returns>
		Task<FdbDirectorySubspace> MoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath);

		/// <summary>Attempts to move the specified subdirectory to <paramref name="newPath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="oldPath">Relative path under this directory of the subdirectory to be moved</param>
		/// <param name="newPath">Relative path under this directory where the subdirectory will be moved to</param>
		/// <returns>Returns the directory at its new location if successful. If the directory doesn't exist, then null is returned.</returns>
		Task<FdbDirectorySubspace?> TryMoveAsync(IFdbTransaction trans, FdbPath oldPath, FdbPath newPath);
		//TODO: merge MoveAsync and TryMoveAsync into a single method!

		/// <summary>Moves the current directory to <paramref name="newAbsolutePath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newAbsolutePath">Full path (from the root) where this directory will be moved</param>
		/// <returns>Returns the directory at its new location if successful.</returns>
		Task<FdbDirectorySubspace> MoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath);

		/// <summary>Attempts to move the current directory to <paramref name="newAbsolutePath"/>.
		/// There is no effect on the physical prefix of the given directory, or on clients that already have the directory open.
		/// An error is raised if a directory already exists at `new_path`, or if the new path points to a child of the current directory.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newAbsolutePath">Full path (from the root) where this directory will be moved</param>
		/// <returns>Returns the directory at its new location if successful. If the directory doesn't exist, then null is returned.</returns>
		Task<FdbDirectorySubspace?> TryMoveToAsync(IFdbTransaction trans, FdbPath newAbsolutePath);
		//TODO: merge MoveToAsync and TryMoveToAsync into a single method!

		/// <summary>Removes a directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to remove. Will remove the current directory if <paramref name="subPath"/> is empty</param>
		Task RemoveAsync(IFdbTransaction trans, FdbPath subPath = default);

		/// <summary>Attempts to remove the directory, its contents, and all subdirectories.
		/// Warning: Clients that have already opened the directory might still insert data into its contents after it is removed.
		/// </summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to remove. Will remove the current directory if <paramref name="subPath"/> is empty</param>
		Task<bool> TryRemoveAsync(IFdbTransaction trans, FdbPath subPath = default);
		//TODO: merge RemoveAsync and TryRemoveAsync into a single method!

		/// <summary>Checks if this directory exists</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to test</param>
		/// <returns>Returns true if the directory exists, otherwise false.</returns>
		Task<bool> ExistsAsync(IFdbReadOnlyTransaction trans, FdbPath subPath = default);

		/// <summary>Returns the list of all the subdirectories of the current directory.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to list</param>
		Task<List<FdbPath>> ListAsync(IFdbReadOnlyTransaction trans, FdbPath subPath = default);
		//TODO: return a List<FdbDirectoryPath> instead?

		/// <summary>Returns the list of all the subdirectories of the current directory, if it exists.</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="subPath">Path of the directory to list</param>
		Task<List<FdbPath>?> TryListAsync(IFdbReadOnlyTransaction trans, FdbPath subPath = default);
		//TODO: merge ListAsync and TryListAsync into a single method!

		//TODO: Add BrowseAsync(...) which is the same as ListAsync(...) but returns the FdbDirectorySubspace instances directly?

		/// <summary>Ensure that this directory was registered with the correct layer id</summary>
		/// <param name="layer">Expected layer id (if not empty)</param>
		/// <exception cref="System.InvalidOperationException">If the directory was registered with a different layer id</exception>
		void CheckLayer(string? layer);

		/// <summary>Change the layer id of this directory</summary>
		/// <param name="trans">Transaction to use for the operation</param>
		/// <param name="newLayer">New layer id of this directory</param>
		Task<FdbDirectorySubspace> ChangeLayerAsync(IFdbTransaction trans, string newLayer);

	}

}
