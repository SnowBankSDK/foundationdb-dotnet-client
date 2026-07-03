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

// ReSharper disable MethodHasAsyncOverload
// ReSharper disable MethodSupportsCancellation

namespace FdbShell
{
	using FoundationDB.Client.Status;
	using SnowBank.Buffers.Binary;

	public static class BasicCommands
	{

		[Flags]
		public enum DirectoryBrowseOptions
		{
			Default = 0,
			ShowFirstKeys = 1,
			ShowCount = 2,
		}

		public static async Task<(IFdbDirectory? Directory, FdbDirectorySubspace? Subspace, Slice Prefix)> TryOpenCurrentDirectoryAsync(IFdbReadOnlyTransaction tr, FdbPath path)
		{
			var location = new FdbDirectorySubspaceLocation(path);
			if (location.Path.Count != 0)
			{
				var subspace = await location.TryResolve(tr);
				return (subspace, subspace, subspace?.Copy().GetPrefix() ?? Slice.Nil);
			}
			else
			{
				return (location, null, Slice.Empty);
			}
		}

		public static async Task Dir(FdbPath path, IVarTuple extras, DirectoryBrowseOptions options, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			terminal.Markup($"[gray]# Listing [cyan]{terminal.Escape(path.ToString("N"))}[/][/]");

			await db.ReadAsync(async tr =>
			{
				var (parent, folder, prefix) = await TryOpenCurrentDirectoryAsync(tr, path);
				if (parent == null)
				{
					terminal.Error("Directory not found.");
					return;
				}

				if (prefix.Count > 0)
				{
					terminal.Markup($"[gray]# Prefix: [cyan]{terminal.Escape(FdbKey.Dump(prefix))}[/] \u279c 0x[teal]{prefix.ToHexString('\ue621')}[/][/]");
				}
				else
				{
					terminal.Markup($"[gray]# Prefix: [cyan]{terminal.Escape(FdbKey.Dump(prefix))}[/][/]");
				}

				//if (!string.IsNullOrEmpty(parent.Layer))
				//{
				//	terminal.StdOut($"# Layer: {parent.Layer}");
				//}

				var folders = await Fdb.Directory.BrowseAsync(tr, parent);
				if (folders.Count > 0)
				{
					// to better align the names, we allow between 16 and 40 chars for the first column.
					// if there is a larger name, it will stick out!
					var maxLen = Math.Min(Math.Max(folders.Keys.Max(n => n.Length), 16), 40);

					foreach (var kvp in folders)
					{
						var name = kvp.Key;
						var subfolder = kvp.Value;
						if (subfolder != null!)
						{
							var symbol =
								  subfolder.Layer == FdbDirectoryPartition.LayerId ? "[bold cyan]\udb84\udee4"
								: subfolder.Layer == "tenant" ? "[bold #9BCFFF]\udb83\uddab"
								: subfolder.Layer == "table" ? "[bold #9BCFFF]\udb81\udcf1"
								: subfolder.Layer == "test" ? "[bold #9BCFFF]\udb80\udc96"
								: subfolder.Layer.Length != 0 ? "[bold yellow]\udb84\udc80"
								: "[silver]\udb80\ude56";
							if ((options & DirectoryBrowseOptions.ShowCount) != 0)
							{

								if (subfolder is not FdbDirectoryPartition)
								{
									long count = await Fdb.System.EstimateCountAsync(db, subfolder.ToRange(), ct);
									terminal.Markup($"  {symbol} {terminal.Escape(name).PadRight(maxLen)}[/] {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : terminal.Escape("[" + subfolder.Layer + "]")),-12} {count,9:N0}");
								}
								else
								{
									terminal.Markup($"  {symbol} {terminal.Escape(name).PadRight(maxLen)}[/] {FdbKey.Dump(subfolder.Copy().GetPrefix()),-12} {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : terminal.Escape("[" + subfolder.Layer + "]")),-12} {"-",9}");
								}
							}
							else
							{
								terminal.Markup($"  {symbol} {terminal.Escape(name).PadRight(maxLen)}[/] [gray]{FdbKey.Dump(subfolder.Copy().GetPrefix()),-12}[/] {(string.IsNullOrEmpty(subfolder.Layer) ? "-" : terminal.Escape("[" + subfolder.Layer + "]")),-12}");
							}
						}
						else
						{
							terminal.Error($"  WARNING: {name} seems to be missing!");
						}
					}

					terminal.StdOut($"  {folders.Count} sub-directories.");
				}
				else
				{
					//TODO: test if it contains data?
					terminal.StdOut("No sub-directories.");
				}

				//TODO: check if there is at least one key?
			}, ct);
		}

		/// <summary>Creates a new directory</summary>
		public static async Task CreateDirectory(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			string layer = (extras.Count > 0 ? extras.Get<string>(0)?.Trim() : null) ??string.Empty;

			if (path.LayerId != layer) path.WithLayer(layer);
			terminal.StdOut($"# Creating directory {path}");

			var (prefix, created) = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder != null)
				{
					return (folder.GetPrefix(), false);
				}

				folder = await db.DirectoryLayer.TryCreateAsync(tr, path);
				return (folder!.Copy().GetPrefix(), true);
			}, ct);

			if (!created)
			{
				terminal.StdOut($"- Directory {path} already exists at {FdbKey.Dump(prefix)} [{prefix.ToHexString(' ')}]");
				return;
			}
			terminal.StdOut($"- Created under {FdbKey.Dump(prefix)} [{prefix.ToHexString(' ')}]");

			// look if there is already stuff under there
			var stuff = await db.ReadAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				return await tr.GetRange(folder!.ToRange()).FirstOrDefaultAsync();
			}, ct);

			if (stuff.Key.IsPresent)
			{
				terminal.Error($"CAUTION: There is already some data under {path} !");
				terminal.Error($"  {FdbKey.Dump(stuff.Key)} = {stuff.Value:V}");
			}
		}

		/// <summary>Remove a directory and all its data</summary>
		public static async Task RemoveDirectory(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			// "-r|--recursive" is used to allow removing an entire subtree
			string[] args = extras.ToArray<string>();
			bool recursive = args.Contains("-r", StringComparer.Ordinal) || args.Contains("--recursive", StringComparer.Ordinal);
			bool force = args.Contains("-f", StringComparer.Ordinal) || args.Contains("--force", StringComparer.Ordinal);

			var res = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					terminal.Error($"# Directory {path} does not exist!");
					return false;
				}

				// are there any subdirectories ?
				if (!recursive)
				{
					var subDirs = await folder.TryListAsync(tr);
					if (subDirs != null && subDirs.Count > 0)
					{
						//TODO: "-r" flag ?
						terminal.Error($"# Cannot remove {path} because it still contains {subDirs.Count:N0} sub-directories.");
						terminal.StdOut("Use the -r|--recursive flag to override this warning.");
						return false;
					}
				}

				if (!force)
				{
					//TODO: ask for confirmation?
				}

				await folder.RemoveAsync(tr);
				return true;
			}, ct);
			if (res)
			{
				terminal.Success($"Deleted directory {path}");
			}
		}

		/// <summary>Move/Rename a directory</summary>
		public static async Task MoveDirectory(FdbPath source, FdbPath destination, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			await db.WriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, source);
				if (folder == null)
				{
					terminal.Error($"# Source directory {source} does not exist!");
					return;
				}

				folder = await db.DirectoryLayer.TryOpenAsync(tr, destination);
				if (folder != null)
				{
					terminal.Error($"# Destination directory {destination} already exists!");
					return;
				}

				await db.DirectoryLayer.MoveAsync(tr, source, destination);
			}, ct);
			terminal.Success($"Moved {source} to {destination}");
		}

		public static async Task ShowDirectoryLayer(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			var (_, dir, _) = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			if (dir == null)
			{
				terminal.Error($"# Directory {path} does not exist anymore");
			}
			else
			{
				if (dir.Layer == FdbDirectoryPartition.LayerId)
					terminal.StdOut($"# Directory {path} is a partition");
				else if (!string.IsNullOrEmpty(dir.Layer))
					terminal.StdOut($"# Directory {path} has layer '{dir.Layer}'");
				else
					terminal.StdOut($"# Directory {path} does not have a layer defined");
			}
		}

		public static Task ChangeDirectoryLayer(FdbPath path, string layer, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			return db.WriteAsync(async tr =>
			{
				var (_, dir, _) = await BasicCommands.TryOpenCurrentDirectoryAsync(tr, path);
				if (dir == null)
				{
					terminal.Error($"# Directory {path} does not exist anymore");
				}
				else
				{
					dir = await dir.ChangeLayerAsync(tr, layer);
					terminal.Success($"# Directory {path} layer changed to {dir.Layer}");
				}
			}, ct);
		}

		public static async Task Get(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{

			if (path.IsRoot)
			{
				terminal.Error("Cannot read keys of the Root Partition.");
				return;
			}

			if (extras.Count == 0)
			{
				terminal.Error("You must specify a key of range of keys!");
				return;
			}

			(var v, bool pathNotFound) = await db.ReadAsync(async tr =>
			{

				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					terminal.Error("The directory does not exist anymore");
					return (default, false);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					terminal.Error("Cannot clear the content of a Directory Partition!");
					return (default, false);
				}

				object? key = extras[0];
				var k = MakeKey(folder, key);

				terminal.Comment("# Reading key: " + k.ToString("K"));

				return (await tr.GetAsync(k), true);
			}, ct);

			if (pathNotFound) return;

			if (v.IsNull)
			{
				terminal.StdOut("# Key does not exist in the database.", ConsoleColor.Red);
				return;
			}
			if (v.IsEmpty)
			{
				terminal.StdOut("# Key exists but is empty.", ConsoleColor.Gray);
				return;
			}

			terminal.StdOut($"# Size: {v.Count:N0}", ConsoleColor.Gray);
			string? format = extras.Count > 1 ? extras.Get<string>(1) : null;
			switch (format)
			{
				case "--text":
				case "--json":
				case "--utf8":
				{
					terminal.StdOut(v.ToStringUtf8() ?? string.Empty, ConsoleColor.Gray);
					break;
				}
				case "--hex":
				case "--hexa":
				{
					terminal.StdOut(v.ToHexString(), ConsoleColor.White);
					break;
				}
				case "--dump":
				{
					var sb = new StringBuilder(v.Count * 3 + (v.Count >> 4) * 2 + 16);
					for (int i = 0; i < v.Count; i += 16)
					{
						sb.AppendLine(v.Substring(i, 16).ToHexString(' '));
					}
					terminal.StdOut(sb.ToString(), ConsoleColor.White);
					break;
				}
				case "--int":
				{
					if (v.Count <= 8)
					{
						long he = v.ToInt64BE();
						long le = v.ToInt64();
						terminal.StdOut($"BE: {he:X016} ({he:N0})", ConsoleColor.White);
						terminal.StdOut($"LE: {le:X016} ({le:N0})", ConsoleColor.White);
					}
					else
					{
						terminal.StdOut($"Value is too large ({v.Count} bytes)", ConsoleColor.DarkRed);
						terminal.StdOut(v.ToHexString(' '), ConsoleColor.Gray);
					}
					break;
				}
				case "--uint":
				{
					if (v.Count <= 8)
					{
						ulong he = v.ToUInt64BE();
						ulong le = v.ToUInt64();
						terminal.StdOut($"BE: {he:X016} ({he:N0})", ConsoleColor.White);
						terminal.StdOut($"LE: {le:X016} ({le:N0})", ConsoleColor.White);
					}
					else
					{
						terminal.StdOut($"Value is too large ({v.Count} bytes)", ConsoleColor.DarkRed);
						terminal.StdOut(v.ToHexString(' '), ConsoleColor.Gray);
					}
					break;
				}
				case "--tuple":
				{
					try
					{
						var t = TuPack.Unpack(v);
						terminal.StdOut(t.ToString() ?? string.Empty, ConsoleColor.Gray);
					}
					catch (Exception e)
					{
						terminal.Error("Key value does not seem to be a valid Tuple: " + e.Message);
						terminal.StdOut(v.ToHexString(' '), ConsoleColor.Gray);
					}
					break;
				}
				default:
				{
					terminal.StdOut(v.ToString("V"), ConsoleColor.White);
					break;
				}
			}
		}

		public static async Task Clear(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			if (path.Count == 0)
			{
				terminal.Error("Cannot directory list the content of Root Partition.");
				return;
			}

			if (extras.Count == 0)
			{
				terminal.Error("You must specify a key of range of keys!");
				return;
			}
			object? key = extras[0];

			var empty = await db.ReadWriteAsync(async tr =>
			{

				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					terminal.Error("The directory does not exist anymore");
					return default(bool?);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					terminal.Error("Cannot clear the content of a Directory Partition!");
					return null;
				}

				var k = MakeKey(folder, key);

				var v = await tr.GetAsync(k);
				if (v.IsNullOrEmpty)
				{
					return true;
				}

				tr.Clear(k);
				return false;
			}, ct);

			if (empty == null)
			{
				return;
			}

			if (empty.Value)
			{
				terminal.StdOut("Key did not exist in the database.", ConsoleColor.Cyan);
			}
			else
			{
				terminal.Success($"Key {key} has been cleared from Directory {path}.");
			}
		}

		private static Slice MakeKey(IKeySubspace folder, object? key) =>
			key switch
			{
				IVarTuple t => folder.Tuple(t).ToSlice(),
				string s => folder.Bytes(s).ToSlice(),
				_ => throw new FormatException("Unsupported key type: " + key)
			};

		public static async Task ClearRange(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			if (path.Count == 0)
			{
				terminal.Error("Cannot directory list the content of Root Partition.");
				return;
			}

			bool? empty = await db.ReadWriteAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					terminal.Error("The directory does not exist anymore");
					return default(bool?);
				}
				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					terminal.Error("Cannot clear the content of a Directory Partition!");
					return null;
				}

				if (extras.Count == 0)
				{
					terminal.Error("You must specify a key of range of keys!");
					return null;
				}

				KeyRange range;
				if (extras[0] is "*")
				{ // clear all!
					range = folder.ToRange().ToKeyRange();
				}
				else
				{
					object? from = extras[0];
					object? to = extras.Count > 1 ? extras[1] : null;

					if (to == null)
					{
						range = KeyRange.StartsWith(MakeKey(folder, from));
					}
					else
					{
						range = new KeyRange(
							MakeKey(folder, from),
							MakeKey(folder, to)
						);
					}
				}

				terminal.Comment("Clearing range " + range.ToString());

				var any = await tr.GetRangeAsync(range, new FdbRangeOptions { Limit = 1 });
				if (any.Count == 0) return true;
				tr.ClearRange(folder);
				return false;
			}, ct);

			if (empty == null) return;

			if (empty.Value)
			{
				terminal.StdOut($"Directory {path} was already empty.", ConsoleColor.Cyan);
			}
			else
			{
				terminal.Success($"Cleared all content of Directory {path}.");
			}
		}

		/// <summary>Counts the number of keys inside a directory</summary>
		public static async Task Count(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			// look if there is something under there
			var folder = (await db.ReadWriteAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct)).Subspace;
			if (folder == null)
			{
				terminal.Error($"# Directory {path} does not exist");
				return;
			}

			var copy = folder.Copy();
			terminal.StdOut($"# Counting keys under {FdbKey.Dump(copy.GetPrefix())} ...");

			var progress = new Progress<(long Count, Slice Current)>((state) =>
			{
				terminal.Progress($"\r# Found {state.Count:N0} keys...");
			});

			long count = await Fdb.System.EstimateCountAsync(db, copy.ToRange(), progress, ct);
			terminal.Progress("\r");
			terminal.Success($"Found {count:N0} keys in {folder.FullName}");
		}

		/// <summary>Shows the first few keys of a directory</summary>
		public static async Task Show(FdbPath path, IVarTuple extras, bool reverse, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			int count = 20;
			if (extras.Count > 0)
			{
				int x = extras.Get<int>(0);
				if (x > 0) count = x;
			}

			if (path.Count == 0)
			{
				terminal.Error("Cannot directory list the content of Root Partition.");
				return;
			}

			// look if there is something under there
			await db.ReadAsync(async tr =>
			{
				var folder = await db.DirectoryLayer.TryOpenAsync(tr, path);
				if (folder == null)
				{
					terminal.StdOut("Folder does not exist");
					return;
				}

				if (folder.Layer == FdbDirectoryPartition.LayerId)
				{
					terminal.Error("Cannot list the content of a Directory Partition!");
					return;
				}
				terminal.Markup($"[gray]# Content of [/][cyan]{terminal.Escape(path.ToString("N"))}[/]");
				terminal.Markup($"[gray]# Prefix: [cyan]{terminal.Escape(FdbKey.Dump(folder.GetPrefix()))}[/] \u279c 0x[teal]{folder.GetPrefix().ToHexString('\ue621')}[/][/]");
				if (folder.Layer.Length != 0)
				{
					terminal.Markup($"[gray]# Layer: [cyan]{terminal.Escape(folder.Layer)}[/][/]");
				}

				var query = tr.GetRange(folder.ToRange());
				var keys = await (reverse
					? query.Reverse().Take(count)
					: query.Take(count + 1)
				).ToListAsync();

				if (keys.Count > 0)
				{
					if (reverse) keys.Reverse();
					foreach (var key in keys.Take(count))
					{
						terminal.Markup($"[gray]...[/][white]{terminal.Escape(FdbKey.Dump(folder.ExtractKey(key.Key)))}[/] = [silver]{terminal.Escape(key.Value.ToString("V"))}[/]");
					}
					if (!reverse && keys.Count == count + 1)
					{
						terminal.StdOut("... more");
					}
				}
				else
				{
					terminal.StdOut("Folder is empty");
				}
			}, ct);
		}

		public static async Task Dump(FdbPath path, string output, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			var progress = terminal.IsInteractive;

			// look if there is something under there
			var folder = await db.ReadWriteAsync(async tr =>
			{
				var dir = await db.DirectoryLayer.TryOpenAsync(tr, path);
				return dir != null ? KeySubspace.CopyUnsafe(dir) : null;
			}, ct);

			if (folder == null)
			{
				terminal.Comment($"# Directory {path} does not exist");
				return;
			}

			//TODO: if file already exists, ask confirmation before overriding?
			output = Path.GetFullPath(output);
			File.Delete(output);

			using (var fs = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.Read, 64 * 1024))
			using (var sw = new StreamWriter(fs, Encoding.UTF8, 32 * 1024))
			{
				terminal.Comment($"# Dumping content of {FdbKey.Dump(folder.GetPrefix())} [{folder.GetPrefix().ToHexString(' ')}] to {output}");
				long bytes = 0;
				var kr = new[] { '|', '/', '-', '\\' };
				int p = 0;

				//BUGBUG: this is dangerous if the directory is moved/renamed/removed WHILE the bulk read is going!
				// => we should find a way to either detect the move, or fail the export if this happens!

				var count = await Fdb.Bulk.ExportAsync(
					db,
					folder.ToRange().ToKeyRange(),
					(batch, _, _) =>
					{
						if (progress) terminal.Progress($"\r{kr[(p++) % kr.Length]} {FormatSize(bytes)}");
						foreach (var kv in batch)
						{
							bytes += kv.Key.Count + kv.Value.Count;
							sw.WriteLine("{0} = {1:V}", FdbKey.Dump(folder.ExtractKey(kv.Key)), kv.Value);
						}
						ct.ThrowIfCancellationRequested();
						return sw.FlushAsync(ct);
					},
					ct
				);
				if (progress) terminal.Progress("\r");
				if (count > 0)
				{
					terminal.Success($"Found {count:N0} keys ({FormatSize(bytes)})");
				}
				else
				{
					terminal.StdOut("Folder is empty.");
				}
			}
		}

		/// <summary>Display a tree of a directory's children</summary>
		public static async Task Tree(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			terminal.Comment($"# Tree of {path}:");

			FdbDirectorySubspace? root = null;
			if (path.Count != 0)
			{
				root = await db.ReadAsync(tr => db.DirectoryLayer.TryOpenAsync(tr, path), ct);
				if (root == null)
				{
					terminal.StdOut("Folder not found.");
					return;
				}
			}

			await TreeDirectoryWalk(root?.Path ?? FdbPath.Root, new List<bool>(), db, terminal, ct);

			terminal.Comment("# done");
		}

		private static async Task TreeDirectoryWalk(FdbPath folder, List<bool> last, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			ct.ThrowIfCancellationRequested();

			var sb = new StringBuilder(last.Count * 4);
			if (last.Count > 0)
			{
				for (int i = 0; i < last.Count - 1; i++) sb.Append(last[i] ? "    " : "|   ");
				sb.Append(last[^1] ? "`-- " : "|-- ");
			}

			if (folder == FdbPath.Root)
			{
				sb.Append("<root>");
			}
			else
			{
				sb.AppendInvariant($"{(folder.LayerId == "partition" ? ("<" + folder.Name + ">") : folder.Name)}{(string.IsNullOrEmpty(folder.LayerId) ? string.Empty : (" [" + folder.LayerId + "]"))}");
			}
			terminal.StdOut(sb.ToString());

			var children = await db.ReadAsync(tr => db.DirectoryLayer.ListAsync(tr, folder), ct);
			int n = children.Count;
			foreach (var child in children)
			{
				last.Add((n--) == 1);
				await TreeDirectoryWalk(child, last, db, terminal, ct);
				last.RemoveAt(last.Count - 1);
			}
		}

		public static async Task Map(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			// we want to merge the map of shards, with the map of directories from the Directory Layer, and count for each directory how many shards intersect
			bool progress = terminal.IsInteractive;

			var (directory, folder, prefix) = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			if (folder == null)
			{
				terminal.Error("# Directory not found");
				return;
			}

			terminal.StdOut("Listing all shards...");

			var dirLayer = path.Count > 0 ? folder.DirectoryLayer : db.DirectoryLayer;
			// note: this may break in future versions of the DL! Maybe we need a custom API to get a flat list of all directories in a DL that span a specific range ?
			var span = await db.ReadAsync(async tr => (await dirLayer.Content.Resolve(tr)).ToRange().ToKeyRange(), ct);

			var shards = await Fdb.System.GetChunksAsync(db, span, ct);
			int totalShards = shards.Count;
			terminal.StdOut($"> Found {totalShards} shard(s) in partition {dirLayer.FullName}", ConsoleColor.Gray);

			terminal.StdOut("Listing all directories...");
			var map = new Dictionary<string, int>(StringComparer.Ordinal);

			void Account(FdbPath p, int c)
			{
				for (int i = 1; i <= p.Count; i++)
				{
					var s = FdbPath.Absolute(p.Segments[..i]).ToString();
					map[s] = map.TryGetValue(s, out int x) ? (x + c) : c;
				}
			}

			var work = new Stack<IFdbDirectory>();
			work.Push(folder);

			var dirs = new List<IFdbDirectory>();
			int n = 0;
			while(work.Count > 0)
			{
				var cur = work.Pop();
				// skip sub partitions

				var names = await db.ReadAsync(tr => cur.ListAsync(tr), ct);
				foreach(var name in names)
				{
					var sub = await db.ReadAsync(tr => cur.TryOpenAsync(tr, name), ct);
					if (sub != null)
					{
						var p = sub.FullName;
						if (sub is FdbDirectoryPartition)
						{
							if (progress) terminal.Progress("\r");
							terminal.StdOut($"! Skipping partition {sub.Name}     ", ConsoleColor.DarkRed);
							n = 0;
							continue;
						}
						if (progress) terminal.Progress($"\r{p}{(p.Length > n ? string.Empty : new string(' ', n - p.Length))}");
						n = p.Length;
						work.Push(sub);
						dirs.Add(sub);
					}
				}
			}
			if (progress) terminal.Progress("\r" + new string(' ', n + 2) + "\r");
			terminal.StdOut($"> Found {dirs.Count} sub-directories", ConsoleColor.Gray);

			terminal.StdOut();
			terminal.StdOut("Estimating size of each directory...");
			int foundShards = 0;
			n = 0;
			int max = 0;
			IFdbDirectory? bigBad = null;
			foreach (var dir in dirs)
			{
				if (progress) terminal.Progress($"\r> {dir.Name}{(dir.Name.Length > n ? String.Empty : new string(' ', n - dir.Name.Length))}");
				n = dir.Name.Length;

				var p = dir.Path;
				var key = ((KeySubspace)dir).GetPrefix();

				// verify that the subspace has at least one key inside
				var bounds = await db.ReadAsync(async (tr) =>
				{
					var kvs = await Task.WhenAll(
						tr.GetRange(KeyRange.StartsWith(key)).FirstOrDefaultAsync(),
						tr.GetRange(KeyRange.StartsWith(key)).LastOrDefaultAsync()
					);
					return new { Min = kvs[0].Key, Max = kvs[1].Key };
				}, ct);

				if (bounds.Min.HasValue)
				{ // folder is not empty
					shards = await Fdb.System.GetChunksAsync(db, KeyRange.StartsWith(key), ct);
					//TODO: we still need to check if the first and last shard really intersect the subspace

					// we need to check if the shards actually contain data
					foundShards += shards.Count;
					Account(p, shards.Count);
					if (shards.Count > max) { max = shards.Count; bigBad = dir; }
				}
				else
				{
					Account(p, 0);
				}
			}
			if (progress) terminal.Progress("\r" + new string(' ', n + 2) + "\r");
			terminal.StdOut($"> Found a total of {foundShards:N0} shard(s) in {dirs.Count:N0} folder(s)", ConsoleColor.Gray);
			terminal.StdOut();

			terminal.StdOut("Shards %Total              Path");
			foreach(var kvp in map.OrderBy(x => x.Key))
			{
				terminal.StdOut($"{kvp.Value,6} {RobustHistogram.FormatHistoBar((double)kvp.Value / foundShards, 20),-20} {kvp.Key}", ConsoleColor.Gray);
			}
			terminal.StdOut();

			if (bigBad != null)
			{
				terminal.StdOut($"Biggest folder is {bigBad.FullName} with {max} shards ({100.0 * max / totalShards:N1}% total, {100.0 * max / foundShards:N1}% subtree)");
				terminal.StdOut();
			}
		}

		private static string FormatSize(long size)
		{
			return ByteSize.Bytes(size).ToApproximateString();
		}

		/// <summary>Find the DCs, machines and processes in the cluster</summary>
		public static async Task Topology(FdbDirectorySubspaceLocation? location, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			var coords = await Fdb.System.GetCoordinatorsAsync(db, ct);
			terminal.StdOut($"[Cluster] {coords.Id}");

			// sample output:
			//	[Cluster] UIlUIUmJGbxoJawtqPAieNnwXODIPtbO
			//		`- [DataCenter] ..... (#0)
			//		`- [Machine] 192.168.1.23, .....
			//		|- [Process] 192.168.1.23:4500, .....
			//		|- [Process] 192.168.1.23:4501, .....
			//		|- [Process] 192.168.1.23:4502, .....
			//		`- [Process] 192.168.1.23:4503, .....

			var status = await Fdb.System.GetStatusAsync(db, ct);

			if (status == null)
			{
				terminal.StdErr("Failed to get the status of the cluster!");
				return;
			}

			var machinesByDatacenter = status.Cluster
				.Machines.Values
				.GroupBy(x => x.DataCenterId)
				.ToDictionary(x => x.Key, x => x.ToArray())
				;

			var processesByMachine = status.Cluster
				.Processes.Values
				.GroupBy(x => x.MachineId)
				.ToDictionary(x => x.Key, x => x.ToArray());

			int numDataCenters = 0;
			int numMachines = 0;
			int numProcesses = 0;
			foreach (var (dcId, machines) in machinesByDatacenter)
			{
				++numDataCenters;
				terminal.StdOut($"{(numDataCenters == machinesByDatacenter.Count ? "`-" : "|-")} [DataCenter] {dcId}");

				string dcPrefix = numDataCenters == machinesByDatacenter.Count ? "   " : "|  ";

				for(int machineIdx = 0; machineIdx < machines.Length; machineIdx++)
				{
					var machine = machines[machineIdx];
					++numMachines;

					terminal.StdOut($"{dcPrefix}{(machineIdx + 1 == machines.Length ? "`-" : "|-")}", newLine: false);
					terminal.StdOut($"{machine.Address}", machine.Excluded ? ConsoleColor.Red : ConsoleColor.Cyan, newLine: false);
					terminal.StdOut($", {machine.ContributingWorkers} workers, {machine.Id}");

					string machinePrefix = dcPrefix + (machineIdx + 1 == machines.Length ? "   " : "|  ");
					var processes = processesByMachine[machine.Id];

					for(int procIdx = 0; procIdx < processes.Length; procIdx++)
					{
						var process = processes[procIdx];
						++numProcesses;

						terminal.StdOut($"{machinePrefix}{(procIdx + 1 == processes.Length ? "`-" : "|-")}", newLine: false);
						terminal.StdOut($"{process.Address}", ConsoleColor.White, newLine: false);
						terminal.StdOut($", v{process.Version}, {process.Id}");

						string processPrefix = machinePrefix + (procIdx + 1 == processes.Length ? "   " : "|  ");

						var roles = process.Roles;

						for(int roleIdx = 0; roleIdx < roles.Length; roleIdx++)
						{
							var role = roles[roleIdx];

							terminal.StdOut($"{processPrefix}{(roleIdx + 1 == roles.Length ? "`-" : "|-")}", newLine: false);
							switch (role)
							{ 
								case StorageRoleMetrics storage:
								{
									terminal.StdOut($"{role.Role}", ConsoleColor.DarkCyan, newLine: false);
									terminal.StdOut($", {FormatSize(storage.KVStoreUsedBytes)} / {FormatSize(storage.KVStoreTotalBytes)}, {storage.StorageMetadata?.StorageEngine}");
									break;
								}
								case LogRoleMetrics log:
								{
									terminal.StdOut($"{role.Role}", ConsoleColor.DarkCyan, newLine: false);
									terminal.StdOut($", {FormatSize(log.KVStoreTotalBytes)}");
									break;
								}
								case CommitProxyRoleMetrics:
								{
									terminal.StdOut($"{role.Role}", ConsoleColor.DarkCyan);
									break;
								}
								default:
								{
									terminal.StdOut($"{role.Role}", ConsoleColor.DarkGreen);
									break;
								}
							}

						}
					}
				}
			}

			terminal.StdOut();
			terminal.StdOut($"Found {numProcesses:N0} process(es) on {numMachines:N0} machine(s) in {numDataCenters:N0} datacenter(s)", ConsoleColor.White);
			terminal.StdOut();
		}

		public static async Task Shards(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			var ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
			terminal.StdOut($"Found {ranges.Count} shards in the whole cluster");

			// look if there is something under there
			var (directory, folder, prefix) = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);

			if (folder != null)
			{
				var r = KeyRange.StartsWith(folder.Copy().GetPrefix());
				terminal.StdOut($"Searching for shards that intersect with {path} ...");
				ranges = await Fdb.System.GetChunksAsync(db, r, ct);
				terminal.StdOut($"> Found {ranges.Count} ranges intersecting {r}:");
				var last = Slice.Empty;
				foreach (var range in ranges)
				{
					Console.Write($"> {FdbKey.Dump(range.Begin)} ...");
					long count = await Fdb.System.EstimateCountAsync(db, range, ct);
					terminal.StdOut($" {count:N0}");
					last = range.End;
					//TODO: we can probably get more details on this shard looking in the system keyspace (where it is, how many replicas, ...)
				}
				terminal.StdOut($"> \uea7c {FdbKey.Dump(last)}");
			}

			//terminal.StdOut("Found " + ranges.Count + " shards in the cluster");
			//TODO: shards that intersect the current directory
		}

		public static async Task Sampling(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			double ratio = 0.1d;
			bool auto = true;
			if (extras.Count > 0)
			{
				double x = extras.Get<double>(0);
				if (x > 0 && x <= 1) ratio = x;
				auto = false;
			}

			var (directory, folder, prefix) = await db.ReadAsync(tr => TryOpenCurrentDirectoryAsync(tr, path), ct);
			KeyRange span;
			if (folder != null)
			{
				span = KeyRange.StartsWith(folder.Copy().GetPrefix());
				terminal.StdOut($"Reading list of shards for {path} under {FdbKey.Dump(span.Begin)} ...");
			}
			else
			{
				terminal.StdOut("Reading list of shards for the whole cluster ...");
				span = KeyRange.All;
			}

			// dump keyServers
			var ranges = await Fdb.System.GetChunksAsync(db, span, ct);
			terminal.StdOut($"> Found {ranges.Count:N0} shard(s)");

			// take a sample
			var samples = new List<KeyRange>();

			if (ranges.Count <= 32)
			{ // small enough to scan it all
				samples.AddRange(ranges);
				terminal.StdOut($"Sampling all {samples.Count:N0} shards ...");
			}
			else
			{ // need to take a random subset
				var rnd = new Random();
				int sz = Math.Max((int)Math.Ceiling(ratio * ranges.Count), 1);
				if (auto)
				{
					if (sz > 100) sz = 100; //SAFETY
					if (sz < 32) sz = Math.Max(sz, Math.Min(32, ranges.Count));
				}

				var population = new List<KeyRange>(ranges);
				for (int i = 0; i < sz; i++)
				{
					int p = rnd.Next(population.Count);
					samples.Add(population[p]);
					population.RemoveAt(p);
				}
				terminal.StdOut($"Sampling {samples.Count:N0} out of {ranges.Count:N0} shards ({(100.0 * samples.Count / ranges.Count):N1}%) ...");
			}

			terminal.StdOut();
			const string SCALE_KEY = "....--------========########M";
			const string SCALE_VAL = "....--------========########@@@@@@@@M";
			terminal.StdOut($"{"Count",9} ║{"Keys",10}{"med.",6} {SCALE_KEY,-29} ║{"Values",10}{"med.",7} {SCALE_VAL,-37} ║{"Total",10}");

			var rangeOptions = new FdbRangeOptions { Streaming = FdbStreamingMode.WantAll };

			samples = samples.OrderBy(x => x.Begin).ToList();

			long globalSize = 0;
			long globalCount = 0;
			int workers = 8; // Math.Max(4, Environment.ProcessorCount);

			var sw = Stopwatch.StartNew();
			var tasks = new List<Task>();
			int n = samples.Count;
			while (samples.Count > 0)
			{
				while (tasks.Count < workers && samples.Count > 0)
				{
					var range = samples[0];
					samples.RemoveAt(0);
					tasks.Add(Task.Run(async () =>
					{
						var kk = new RobustHistogram(RobustHistogram.TimeScale.Ticks);
						var vv = new RobustHistogram(RobustHistogram.TimeScale.Ticks);

						#region Method 1: get_range everything...

						using (var tr = db.BeginTransaction(ct))
						{
							long keySize = 0;
							long valueSize = 0;
							long count = 0;

							int iter = 0;
							var beginSelector = KeySelector.FirstGreaterOrEqual(range.Begin);
							var endSelector = KeySelector.FirstGreaterOrEqual(range.End);
							while (true)
							{
								FdbRangeChunk? data = null;
								FdbException? error = null;
								try
								{
									data = await tr.Snapshot.GetRangeAsync(
										beginSelector,
										endSelector,
										rangeOptions,
										iter
									).ConfigureAwait(false);
								}
								catch (FdbException e)
								{
									error = e;
								}

								if (error != null)
								{
									await tr.OnErrorAsync(error.Code).ConfigureAwait(false);
									continue;
								}

								if (data == null || data.Count == 0) break;

								count += data.Count;
								foreach (var kvp in data)
								{
									keySize += kvp.Key.Count;
									valueSize += kvp.Value.Count;

									kk.Add(TimeSpan.FromTicks(kvp.Key.Count));
									vv.Add(TimeSpan.FromTicks(kvp.Value.Count));
								}

								if (!data.HasMore) break;

								beginSelector = KeySelector.FirstGreaterThan(data.Last);
								++iter;
							}

							long totalSize = keySize + valueSize;
							Interlocked.Add(ref globalSize, totalSize);
							Interlocked.Add(ref globalCount, count);

							terminal.StdOut($"{count,9:N0} ║{FormatSize(keySize),10}{FormatSize((int) Math.Ceiling(kk.Median)),6} {kk.GetDistribution(begin: 1, end: 12000, fold: 2),-29} ║{FormatSize(valueSize),10}{FormatSize((int) Math.Ceiling(vv.Median)),7} {vv.GetDistribution(begin: 1, end: 120000, fold: 2),-37} ║{FormatSize(totalSize),10}");
						}
						#endregion

						#region Method 2: estimate the count using key selectors...

						//long counter = await Fdb.System.EstimateCountAsync(db, range, ct);
						//terminal.StdOut("COUNT = " + counter.ToString("N0"));

						#endregion
					}, ct));
				}

				var done = await Task.WhenAny(tasks);
				tasks.Remove(done);
			}

			await Task.WhenAll(tasks);
			sw.Stop();

			terminal.StdOut();
			if (n != ranges.Count)
			{
				terminal.StdOut($"Sampled {FormatSize(globalSize)} ({globalSize:N0} bytes) and {globalCount:N0} keys in {sw.Elapsed.TotalSeconds:N1} sec");
				terminal.StdOut($"> Estimated total size is {FormatSize(globalSize * ranges.Count / n)}");
			}
			else
			{
				terminal.StdOut($"Found {FormatSize(globalSize)} ({globalSize:N0} bytes) and {globalCount:N0} keys in {sw.Elapsed.TotalSeconds:N1} sec");
				// compare to the whole cluster
				ranges = await Fdb.System.GetChunksAsync(db, FdbKey.MinValue, FdbKey.MaxValue, ct);
				terminal.StdOut($"> This directory contains ~{(100.0 * n / ranges.Count):N2}% of all data");
			}
			terminal.StdOut();
		}

		public static async Task Find(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			if (extras.Count == 0)
			{
				terminal.Error("You must specify a query!");
				return;
			}

			var txt = extras.Get<string>(0) ?? "";
			if (!txt.StartsWith('.') && !txt.StartsWith('/')) txt = "./" + txt;
			var query = FqlQueryParser.Parse(txt);
			
			//terminal.Comment(query.Explain());

			var root = db.Root;
			var current = root[path];
			
			var res = await db.QueryAsync(tr => query.FindDirectories(tr, db.Root[path]), ct);

			terminal.StdOut(res.Count switch
			{
				0 => "Found no matching directory",
				1 => "Found 1 matching directory",
				_ => $"Found {res.Count:N0} matching directories:"
			});
			
			foreach (var x in res)
			{
				var subPath = x.Path.GetRelativePath(current.Path);
				terminal.StdOut($"- {subPath}", ConsoleColor.White);
			}
			
			terminal.StdOut();
		}

		public static async Task Query(FdbPath path, IVarTuple extras, IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			if (extras.Count == 0)
			{
				terminal.Error("You must specify a query!");
				return;
			}

			var txt = extras.Get<string>(0) ?? "";
			if (!txt.StartsWith('.') && !txt.StartsWith('/')) txt = "./" + txt;
			var query = FqlQueryParser.Parse(txt);
			
			//terminal.Comment(query.Explain());

			var current = db.Root[path];
			
			var res = await db.QueryAsync(tr => query.Scan(tr, current), ct);

			terminal.StdOut(res.Count switch
			{
				0 => "Found no results",
				1 => "Found 1 matching result",
				_ => $"Found {res.Count:N0} matching results:"
			});
			
			foreach (var x in res)
			{
				terminal.StdOut($"- {x.Path}{x.Tuple} = {x.Value:P}", ConsoleColor.White);
			}
			
			terminal.StdOut();
		}
		
	}
	
}
