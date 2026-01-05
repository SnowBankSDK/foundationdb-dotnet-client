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

	/// <summary>Range that matches all the keys inside a subspace</summary>
	public readonly struct FdbSubspaceKeyRange : IFdbKeyRange
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange(IKeySubspace subspace, bool inclusive = false)
		{
			this.Subspace = subspace;
			this.IsInclusive = inclusive;
		}

		/// <summary>Subspace that contains the keys</summary>
		public readonly IKeySubspace Subspace;

		/// <summary>If <c>true</c>, the subspace prefix is also included in the range</summary>
		public readonly bool IsInclusive;

		/// <summary>Returns a version of this range that includes the subspace prefix in the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange Inclusive() => new(this.Subspace, inclusive: true);

		/// <summary>Returns a version of this range that excludes the subspace prefix from the results</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbSubspaceKeyRange Exclusive() => new(this.Subspace, inclusive: false);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Slice key) => Contains(key.Span);

		/// <inheritdoc />
		[Pure]
		public bool Contains(ReadOnlySpan<byte> key)
		{
			var prefix = this.Subspace.GetPrefix();
			return key.StartsWith(prefix.Span) && (this.IsInclusive || key.Length > prefix.Count);
		}

		/// <inheritdoc />
		[Pure]
		public bool Contains<TKey>(in TKey key)
			where TKey : struct, IFdbKey
		{
			if (typeof(TKey) == typeof(FdbRawKey))
			{
				return Contains(((FdbRawKey)(object)key).Data);
			}
			if (typeof(TKey) == typeof(FdbSubspaceKey))
			{
				return Contains(((FdbSubspaceKey)(object)key).Subspace.GetPrefix().Span);
			}

			int cmp = key.CompareTo(this.Subspace.GetPrefix());
			if (this.IsInclusive)
			{
				if (cmp < 0) return false;
			}
			else
			{
				if (cmp <= 0) return false;
			}
			return this.Subspace.NextSibling().FastCompareTo(in key) > 0;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToBeginKey() => this.IsInclusive ? this.Subspace.GetPrefix() : FdbKeyHelpers.ToSlice(this.Subspace.First());

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Slice ToEndKey() => FdbKeyHelpers.ToSlice(Subspace.NextSibling());

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public KeyRange ToKeyRange() => this.IsInclusive
			//PERF: optimize this!
			? KeyRange.StartsWith(this.Subspace.GetPrefix())
			: KeyRange.PrefixedBy(this.Subspace.GetPrefix());

		/// <inheritdoc />
		public KeySelector ToBeginSelector() => this.IsInclusive
			? FdbKeySelector.FirstGreaterOrEqual(this.Subspace.GetPrefix()).ToSelector()
			: FdbKeySelector.FirstGreaterThan(this.Subspace.GetPrefix()).ToSelector();

		/// <inheritdoc />
		public KeySelector ToEndSelector()
			=> FdbKeySelector.FirstGreaterOrEqual(this.Subspace.NextSibling()).ToSelector();

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => format switch
		{
			
			null or "" or "D" or "d" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:D}"),
			"K" or "k" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:K}"),
			"P" or "p" => string.Create(formatProvider ?? CultureInfo.InvariantCulture, $"{this:P}"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null) => format switch
		{
			"" or "D" or "d" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace}+1 }}", out charsWritten),
			"K" or "k" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace:K}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace:K}+1 }}", out charsWritten),
			"P" or "p" => destination.TryWrite(provider ?? CultureInfo.InvariantCulture, $"{{ {this.Subspace:P}{(this.IsInclusive ? "" : ".<00>")} <= k < {this.Subspace:P}+1 }}", out charsWritten),
			_ => throw new FormatException(),
		};

	}

}
