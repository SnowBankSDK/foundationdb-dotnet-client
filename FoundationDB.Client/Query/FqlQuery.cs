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

//#define FULL_DEBUG

#if NET8_0_OR_GREATER

namespace FoundationDB.Client
{

	[DebuggerDisplay("{this.Text,nq}")]
	public sealed class FqlQuery : IFqlQuery, IFormattable, IJsonSerializable, IJsonPackable, IJsonDeserializable<FqlQuery>
	{

		/// <summary>Text of the query (has it was parsed)</summary>
		public required string Text { get; init; }

		/// <summary>Expression used to filter directories (or null if there is none)</summary>
		public FqlDirectoryExpression? Directory { get; init; }

		/// <summary>Expression used to filter tuples (or null if there is none)</summary>
		public FqlTupleExpression? Tuple { get; init; }

		/// <inheritdoc />
		public bool IsPattern => (this.Directory?.IsPattern ?? false) || (this.Tuple?.IsPattern ?? false);

		public override string ToString() => this.Text;

		public string ToString(string? format, IFormatProvider? formatProvider) => format switch
		{
			null or "" or "D" or "d" => this.Text,
			"P" or "p" => this.Directory?.ToString() ?? "",
			"T" or "t" => this.Tuple?.ToString() ?? "",
			_ => throw new ArgumentException("Invalid format", nameof(format))
		};

		public static FqlQuery JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value is not JsonString str) throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(FqlQuery));
			//TODO: is this a good idea? we could get injected some badly formatted query on purpose?
			return FqlQueryParser.Parse(str.Value);
		}

		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => JsonString.Return(this.Text);

		public void JsonSerialize(CrystalJsonWriter writer) => writer.WriteValue(this.Text);

		/// <inheritdoc />
		public void Explain(ExplanationBuilder builder)
		{
			builder.WriteLine($"Query: `{this.Text}`");
			if (!builder.Recursive)
			{
				return;
			}

			if (this.Directory != null)
			{
				builder.ExplainChild(this.Directory);
			}
			else
			{
				builder.WriteChildrenLine("Directory: <none>");
			}

			if (this.Tuple != null)
			{
				builder.ExplainChild(this.Tuple);
			}
			else
			{
				builder.WriteChildrenLine("Tuple: <none>");
			}
		}

		[Conditional("FULL_DEBUG")]
		private static void Kenobi(string message)
		{
#if FULL_DEBUG
			System.Diagnostics.Debug.WriteLine(message);
			Console.WriteLine(message);
#endif
		}

		public async IAsyncEnumerable<(FdbPath Path, IVarTuple Tuple, Slice Key, Slice Value)> Scan(IFdbReadOnlyTransaction tr, FdbDirectorySubspaceLocation root) //TODO: settings?
		{
			await foreach (var match in FindDirectories(tr, root).WithCancellation(tr.Cancellation).ConfigureAwait(false))
			{
				Kenobi($"- Directory: {match}");
				await foreach (var result in EnumerateKeys(tr, match, root.Path).WithCancellation(tr.Cancellation).ConfigureAwait(false))
				{
					yield return result;
				}
			}
		}

		public IAsyncEnumerable<FdbDirectorySubspace> FindDirectories(IFdbReadOnlyTransaction tr, FdbDirectorySubspaceLocation root)
		{
			return AsyncQuery.Pump<FdbDirectorySubspace>(async (channel) =>
			{
				if (this.Directory == null)
				{
					return;
				}

				FdbDirectorySubspace? subspace;

				if (this.Directory.TryGetPath(out FdbPath path))
				{ // this is a fixed path, ex: "/foo/bar/baz", we can open it directly

					subspace = await root[path].TryResolve(tr).ConfigureAwait(false);
					if (subspace != null)
					{
						await channel.WriteAsync(subspace).ConfigureAwait(false);
					}
					return;
				}

				List<FdbDirectorySubspace> batch = [ ];
				List<FdbDirectorySubspace> nextBatch = [ ];

				int fromIndex = 0;
				var segments = this.Directory.Segments;

				while (fromIndex < segments.Count)
				{
					Kenobi($"from:{fromIndex}, batch:[{batch.Count}] {{ {string.Join(", ", batch.Select(x => x.Path))} }}");

					var (chunk, nextIndex) = this.Directory.GetFixedPrefix(fromIndex);
					Kenobi($"- chunk: {chunk}, nextIndex={nextIndex}");

					if (fromIndex == 0)
					{
						if (chunk.Count == 0)
						{ // starts immediately with any, ex: "/<>/bar/baz/..."
							chunk = root.Path;
						}
						else
						{
							chunk = root.Path[chunk];
						}

						Kenobi($"Load first chunk {chunk}...");
						subspace = await tr.Database.DirectoryLayer.TryOpenAsync(tr, chunk).ConfigureAwait(false);
						if (subspace == null)
						{
							Kenobi($"No match for first chunk {chunk}");
							// nothing for the first chunk
							return;
						}
						Kenobi($"Found match for first chunk {chunk} => {subspace}");
						batch.Add(subspace);
					}
					else
					{
						if (batch.Count == 0)
						{
							Kenobi("No more match!");
							return;
						}
					}

					if (nextIndex < segments.Count && segments[nextIndex].IsAny)
					{
						Kenobi($"Next is <>, listing child for {batch.Count} candidates...");

						foreach (var candidate in batch)
						{
							// list its children
							var names = await candidate.ListAsync(tr).ConfigureAwait(false);
							Kenobi($"- found {names.Count} children for {candidate.Path}");
							foreach (var name in names)
							{
								var child = await tr.Database.DirectoryLayer.TryOpenAsync(tr, name).ConfigureAwait(false);
								if (child != null)
								{
									Kenobi($"- queueing {name} for next batch");
									nextBatch.Add(child);
								}
							}
						}
						Kenobi($"- Went from {batch.Count} to {nextBatch.Count} candidates");
					}
					else
					{ // we have to add the chunk to each candidate
						Kenobi($"This is the final chunk, completing {batch.Count} candidates...");
						foreach (var candidate in batch)
						{
							var child = await tr.Database.DirectoryLayer.TryOpenAsync(tr, candidate.Path[chunk]).ConfigureAwait(false);
							if (child != null)
							{
								nextBatch.Add(child);
							}
						}
						Kenobi($"- Went from {batch.Count} to {nextBatch.Count} candidates");
					}

					batch.Clear();
					batch.AddRange(nextBatch);
					nextBatch.Clear();
					fromIndex = nextIndex + 1;
				}

				Kenobi($"Victory! We have {batch.Count} matching directories");

				foreach (var s in batch)
				{
					await channel.WriteAsync(s).ConfigureAwait(false);
				}

			}, tr.Cancellation);
		}

		internal async IAsyncEnumerable<(FdbPath Path, IVarTuple Tuple, Slice Key, Slice Value)> EnumerateKeys(IFdbReadOnlyTransaction tr, FdbDirectorySubspace subspace, FdbPath root)
		{
			Contract.NotNull(tr);
			Contract.NotNull(subspace);
			
			//HACKHACK: TODO: this is a VERY NAIVE approach that will not work for large dataset
			// => this is only something to start with, and should replace with something that uses GetRangeSplitPointsAsync and //ize the chunks
			
			var tupleExpr = this.Tuple;

			var path = subspace.Path.GetRelativePath(root);
			
			await foreach (var kv in tr.GetRange(subspace.ToRange()))
			{
				// decode the tuple
				var tuple = subspace.Unpack(kv.Key);
				
				// test if it matches the 
				if (tupleExpr != null && !tupleExpr.Match(tuple))
				{
					continue;
				}

				yield return (path, tuple, kv.Key, kv.Value);
			}

		}
		
	}

}

#endif
