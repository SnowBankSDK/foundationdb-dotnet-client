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
	/// <summary>Adds a prefix on every key, to group them inside a common subspace</summary>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	public class KeySubspace : IKeySubspace
	{

		/// <summary>Prefix common to all keys in this subspace</summary>
		private readonly Slice Key;

		public ISubspaceContext Context { get; }

		#region Constructors...

		/// <summary>Root keyspace of the database (with no prefix)</summary>
		public static readonly IKeySubspace Empty = new KeySubspace(Slice.Empty, SubspaceContext.Default);

		#region FromKey...

		/// <summary>Initializes a new generic subspace with the given prefix.</summary>
		[Pure]
		public static IKeySubspace FromKey(Slice prefix, ISubspaceContext? context = null)
		{
			return new KeySubspace(prefix.Copy(), context ?? SubspaceContext.Default);
		}

		/// <summary>Create an unsafe copy of a directory by discarding its context</summary>
		/// <param name="subspace">Subspace that lives in a temporary context (ex: transaction context)</param>
		/// <returns>Subspace with the same key prefix, and disconnected from the original context.</returns>
		/// <remarks>THIS IS VERY DANGEROUS! Any change to the original subspace may cause the prefix stored in the copy to become obsolete, and MAY introduce data corruption!</remarks>
		public static IKeySubspace CopyUnsafe(IKeySubspace subspace)
		{
			return new KeySubspace(subspace.GetPrefix(), SubspaceContext.Default);
		}

		#endregion

		public KeySubspace(Slice prefix, ISubspaceContext context)
		{
			if (prefix.IsNull) throw new ArgumentException("Subspace prefix cannot be Nil.", nameof(prefix));
			this.Key = prefix;
			this.Context = context ?? throw new ArgumentNullException(nameof(context));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected void EnsureIsValid()
		{
			// we must ensure that the context is still alive
			this.Context.EnsureIsValid();
		}

		/// <inheritdoc />
		public virtual FdbPath GetPath() => FdbPath.Empty;

		/// <summary>Returns the raw prefix of this subspace</summary>
		/// <remarks>Will throw if the prefix is not publicly visible, as is the case for Directory Partitions</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice GetPrefix()
		{
			return GetKeyPrefix();
		}

		/// <summary>Returns the key to use when creating direct keys that are inside this subspace</summary>
		/// <returns>Prefix that must be added to all keys created by this subspace</returns>
		/// <remarks>Subspaces that disallow the creation of keys should override this method and throw an exception</remarks>
		[Pure, DebuggerStepThrough]
		protected virtual Slice GetKeyPrefix()
		{
			EnsureIsValid();
			return this.Key;
		}

		/// <summary>Returns the master instance of the prefix, without any safety checks</summary>
		/// <remarks>This instance should NEVER be exposed to anyone else, and should ONLY be used for logging/troubleshooting</remarks>
		internal Slice GetPrefixUnsafe()
		{
			return this.Key;
		}


		/// <inheritdoc />
		public virtual FdbSubspaceKeyRange ToRange(bool inclusive = false) => new(this, inclusive);

		/// <summary>Tests whether the specified <paramref name="absoluteKey">key</paramref> starts with this subspace prefix, indicating that the Subspace logically contains <paramref name="absoluteKey">key</paramref>.</summary>
		/// <param name="absoluteKey">The key to be tested</param>
		/// <remarks>The key <see cref="Slice.Nil"/> is not contained by any Subspace, so <c>subspace.Contains(Slice.Nil)</c> will always return false</remarks>
		public virtual bool Contains(ReadOnlySpan<byte> absoluteKey)
		{
			EnsureIsValid();
			return absoluteKey.StartsWith(this.Key.Span);
		}

		/// <summary>Manually append a binary suffix to the subspace prefix</summary>
		/// <remarks>Appending a binary literal that does not comply with the subspace key encoding may produce a result that cannot be decoded!</remarks>
		public bool TryAppend(Span<byte> destination, out int bytesWritten, ReadOnlySpan<byte> relativeKey)
		{
			//note: we don't want to leak our key!
			var prefix = GetKeyPrefix().Span;
			if (!prefix.TryCopyTo(destination))
			{
				goto too_small;
			}

			if (!relativeKey.TryCopyTo(destination[prefix.Length..]))
			{
				goto too_small;
			}

			bytesWritten = prefix.Length + relativeKey.Length;
			return true;

		too_small:
			bytesWritten = 0;
			return false;
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or <see cref="Slice.Nil"/> if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or <see cref="Slice.Empty"/> is the key is exactly equal to the subspace prefix). If the key is outside the subspace, returns <see cref="Slice.Nil"/></returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="absoluteKey"/> is outside the current subspace.</exception>
		public virtual Slice ExtractKey(Slice absoluteKey, bool boundCheck = false)
		{
			if (absoluteKey.IsNull) return Slice.Nil;

			var key = GetKeyPrefix();
			if (!absoluteKey.StartsWith(key))
			{
				if (boundCheck) FailKeyOutOfBound(absoluteKey);
				return Slice.Nil;
			}
			return absoluteKey.Substring(key.Count);
		}

		/// <summary>Remove the subspace prefix from a binary key, and only return the tail, or <see cref="Slice.Nil"/> if the key does not fit inside the namespace</summary>
		/// <param name="absoluteKey">Complete key that contains the current subspace prefix, and a binary suffix</param>
		/// <param name="boundCheck">If true, verify that <paramref name="absoluteKey"/> is inside the bounds of the subspace</param>
		/// <returns>Binary suffix of the key (or <see cref="Slice.Empty"/> is the key is exactly equal to the subspace prefix). If the key is outside the subspace, returns <see cref="Slice.Nil"/></returns>
		/// <exception cref="System.ArgumentException">If <paramref name="boundCheck"/> is true and <paramref name="absoluteKey"/> is outside the current subspace.</exception>
		public virtual ReadOnlySpan<byte> ExtractKey(ReadOnlySpan<byte> absoluteKey, bool boundCheck = false)
		{
			if (absoluteKey.Length == 0)
			{
				return default;
			}

			var key = GetKeyPrefix();
			if (!absoluteKey.StartsWith(key.Span))
			{
				if (boundCheck) FailKeyOutOfBound(Slice.FromBytes(absoluteKey));
				return default;
			}
			return absoluteKey[key.Count..];
		}

		/// <inheritdoc/>
		public virtual string PrettyPrint(Slice packedKey)
		{
			if (packedKey.IsNull) return "<null>";
			var key = ExtractKey(packedKey, boundCheck: true);
			return key.PrettyPrint();
		}

		#endregion

		#region IEquatable / IComparable...

		/// <summary>Compare this subspace with another subspace</summary>
		public int CompareTo(IKeySubspace? other)
		{
			if (object.ReferenceEquals(this, other)) return 0;
			return other switch
			{
				null => +1,
				KeySubspace sub => this.Key.CompareTo(sub.Key),
				_ => this.Key.CompareTo(other.GetPrefix())
			};
		}

		/// <summary>Test if both subspaces have the same prefix</summary>
		public bool Equals(IKeySubspace? other)
		{
			if (object.ReferenceEquals(this, other)) return true;
			return other switch
			{
				null => false,
				KeySubspace sub => this.Key.Equals(sub.Key),
				_ => this.Key.Equals(other.GetPrefix())
			};
		}

		/// <summary>Test if an object is a subspace with the same prefix</summary>
		public override bool Equals(object? obj)
		{
			return Equals(obj as KeySubspace);
		}

		/// <summary>Compute a hashcode based on the prefix of this subspace</summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return this.Key.GetHashCode();
		}

		#endregion

		#region Helpers...

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public Slice BoundCheck(Slice key, bool allowSystemKeys)
		{
			EnsureIsValid();

			//note: Since this is needed to make GetRange/GetKey work properly, this should work for all subspace, include directory partitions
			var prefix = this.Key;

			// don't touch nil and keys that are inside the subspace
			if (key.IsNull || key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Count > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			return key < prefix
				? Slice.Empty
				: FdbKey.SystemPrefix;
		}

		/// <summary>Check that a key fits inside this subspace, and return '' or '\xFF' if it is outside the bounds</summary>
		/// <param name="key">Key that needs to be checked</param>
		/// <param name="allowSystemKeys">If true, allow keys that starts with \xFF even if this subspace is not the Empty subspace or System subspace itself.</param>
		/// <returns>The <paramref name="key"/> unchanged if it is contained in the namespace, <see cref="Slice.Empty"/> if it was before the subspace, or FdbKey.MaxValue if it was after.</returns>
		public ReadOnlySpan<byte> BoundCheck(ReadOnlySpan<byte> key, bool allowSystemKeys)
		{
			EnsureIsValid();

			//note: Since this is needed to make GetRange/GetKey work properly, this should work for all subspace, include directory partitions
			var prefix = this.Key.Span;

			// don't touch nil and keys that are inside the subspace
			if (key.StartsWith(prefix)) return key;

			// let the system keys pass
			if (allowSystemKeys && key.Length > 0 && key[0] == 255) return key;

			// The key is outside the bounds, and must be corrected
			// > return empty if we are before
			// > return \xFF if we are after
			return key.SequenceCompareTo(prefix) < 0
				? default
				: FdbKey.SystemPrefixSpan;
		}

		/// <summary>Throw an exception for a key that is out of the bounds of this subspace</summary>
		/// <param name="key"></param>
		[ContractAnnotation("=> halt")]
		protected void FailKeyOutOfBound(Slice key)
		{
#if DEBUG
			// only in debug mode, because have the key and subspace in the exception message could leak sensitive information
			string msg = $"The key {FdbKey.Dump(key)} does not belong to subspace {this}";
#else
			string msg = "The specified key does not belong to this subspace";
#endif
			throw new ArgumentException(msg, nameof(key));
		}

		/// <summary>Return a user-friendly representation of a key from this subspace</summary>
		/// <param name="key">Key that is contained in this subspace</param>
		/// <param name="absolute"></param>
		/// <returns>Printable version of this key, minus the subspace prefix</returns>
		public virtual string DumpKey(Slice key, bool absolute = false)
		{
			// note: we can't use ExtractAndCheck(...) because it may throw in derived classes
			var prefix = this.Key;
			if (!key.StartsWith(prefix)) FailKeyOutOfBound(key);

			return FdbKey.Dump(absolute ? key : key.Substring(prefix.Count));
		}

		/// <summary>Printable representation of this subspace</summary>
		public override string ToString() => ToString(null);

		public virtual string ToString(string? format, IFormatProvider? provider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.Dump(this.Key),
			"X" or "x" => this.Key.ToString(format),
			"G" or "g" => $"{this.GetType().Name}(prefix={FdbKey.Dump(this.Key)})",
			_ => throw new FormatException()
		};

		/// <inheritdoc />
		public virtual bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)=> format switch
		{
			"" or "D" or "d" or "K" or "k" or "P" or "p" => FdbKey.Dump(this.Key).TryCopyTo(destination, out charsWritten),
			"X" or "x" => this.Key.TryFormat(destination, out charsWritten, format, provider),
			"G" or "g" => destination.TryWrite($"{this.GetType().Name}(prefix={FdbKey.Dump(this.Key)})", out charsWritten),
			_ => throw new FormatException()
		};

		#endregion

		#region ISpanEncodable...

		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span)
		{
			if (!this.Context.IsValid)
			{
				span = default;
				return false;
			}
			span = this.Key.Span;
			return true;
		}

		bool ISpanEncodable.TryGetSizeHint(out int sizeHint)
		{
			if (!this.Context.IsValid)
			{
				sizeHint = 0;
				return false;
			}
			sizeHint = this.Key.Count;
			return true;
		}

		bool ISpanEncodable.TryEncode(Span<byte> destination, out int bytesWritten)
		{
			var prefix = GetPrefix(); // throws if context is not valid anymore
			var span = prefix.Span;
			if (destination.Length < span.Length)
			{
				bytesWritten = 0;
				return false;
			}

			span.CopyTo(destination);
			bytesWritten = span.Length;
			return true;
		}

		#endregion

	}

}
