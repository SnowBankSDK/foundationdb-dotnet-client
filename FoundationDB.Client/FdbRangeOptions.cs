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

	/// <summary>Container class for options in a Range query</summary>
	[DebuggerDisplay("Limit={Limit}, Reverse={IsReversed}, TargetBytes={TargetBytes}, Mode={Streaming}, Read={Fetch}")]
	[PublicAPI]
	public sealed record FdbRangeOptions
	{

		#region Public Properties...

		/// <summary>Maximum number of items to return</summary>
		public int? Limit { get; init; }

		/// <summary>If <see langword="true"/>, results are returned in reverse order (from last to first)</summary>
		public bool IsReversed { get; init; }

		/// <summary>Maximum number of bytes to read</summary>
		/// <remarks>The number is not exact, and a batch of result may overshoot this value by a small margin.</remarks>
		public int? TargetBytes { get; init; }

		/// <summary>Streaming mode</summary>
		public FdbStreamingMode? Streaming { get; init; }

		/// <summary>Read only the keys, only the values, or both (default)</summary>
		public FdbFetchMode? Fetch { get; init; }

		/// <summary>If specified, pool used to allocate the buffer that will hold the keys and values</summary>
		/// <remarks>
		/// <para>If a pool is provided, then the <see cref="FdbRangeChunk"/> instance <b>MUST</b> be disposed, and the keys and values <b>CANNOT</b> be exposed outside the scope of the read operation.</para>
		/// <para>Incorrect usage of pooled data may lead to data corruption and overall instability. Use with caution!</para>
		/// </remarks>
		public ArrayPool<byte>? Pool { get; init; }

		#endregion

		#region Singletons...

		/// <summary>Default range options</summary>
		public static readonly FdbRangeOptions Default = new();

		/// <summary>Read in reverse order</summary>
		/// <remarks>Options with <see cref="IsReversed"/> set to <see langword="true"/></remarks>
		public static readonly FdbRangeOptions Reversed = new() { IsReversed = true };

		/// <summary>Read only the first result in the range</summary>
		/// <remarks>Options with <see cref="Limit"/> set to <see langword="1"/></remarks>
		public static readonly FdbRangeOptions First = new() { Limit = 1 };

		/// <summary>Read only the last result in the range</summary>
		/// <remarks>Options with <see cref="Limit"/> set to <see langword="1"/>, and <see cref="IsReversed"/> set to <see langword="true"/></remarks>
		public static readonly FdbRangeOptions Last = new() { Limit = 1, IsReversed = true };

		/// <summary>Fetch only the values</summary>
		/// <remarks>Options with <see cref="Fetch"/> set to <see cref="FdbFetchMode.ValuesOnly"/></remarks>
		public static readonly FdbRangeOptions ValuesOnly = new() { Fetch = FdbFetchMode.ValuesOnly };

		/// <summary>Fetch only the keys</summary>
		/// <remarks>Options with <see cref="Fetch"/> set to <see cref="FdbFetchMode.KeysOnly"/></remarks>
		public static readonly FdbRangeOptions KeysOnly = new() { Fetch = FdbFetchMode.KeysOnly };

		/// <summary>Will read all the keys and values in the range, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/></remarks>
		public static readonly FdbRangeOptions WantAll = new() { Streaming = FdbStreamingMode.WantAll };

		/// <summary>Will read all the values in the range, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/>, and <see cref="Fetch"/> set to <see cref="FdbFetchMode.ValuesOnly"/></remarks>
		public static readonly FdbRangeOptions WantAllValuesOnly = new() { Streaming = FdbStreamingMode.WantAll, Fetch = FdbFetchMode.ValuesOnly };

		/// <summary>Will read all the keys in the range, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/>, and <see cref="Fetch"/> set to <see cref="FdbFetchMode.KeysOnly"/></remarks>
		public static readonly FdbRangeOptions WantAllKeysOnly = new() { Streaming = FdbStreamingMode.WantAll, Fetch = FdbFetchMode.KeysOnly };

		/// <summary>Will read all the keys and values in the range, in reverse order, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/>, and <see cref="IsReversed"/> set to <see langword="true"/></remarks>
		public static readonly FdbRangeOptions WantAllReversed = new() { Streaming = FdbStreamingMode.WantAll, IsReversed = true };

		/// <summary>Will read all the values in the range, in reverse order, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/>, <see cref="IsReversed"/> set to <see langword="true"/>, and <see cref="Fetch"/> set to <see cref="FdbFetchMode.ValuesOnly"/></remarks>
		public static readonly FdbRangeOptions WantAllReversedValuesOnly = new() { Streaming = FdbStreamingMode.WantAll, Fetch = FdbFetchMode.ValuesOnly, IsReversed = true };

		/// <summary>Will read all the keys in the range, in reverse order, without filtering</summary>
		/// <remarks>Options with <see cref="Streaming"/> set to <see cref="FdbStreamingMode.WantAll"/>, <see cref="IsReversed"/> set to <see langword="true"/>, and <see cref="Fetch"/> set to <see cref="FdbFetchMode.KeysOnly"/></remarks>
		public static readonly FdbRangeOptions WantAllReversedKeysOnly = new() { Streaming = FdbStreamingMode.WantAll, Fetch = FdbFetchMode.KeysOnly, IsReversed = true };

		#endregion

		/// <summary>Add all missing values from the provided defaults</summary>
		/// <param name="options">Options provided by the caller (can be null)</param>
		/// <param name="mode">Default value for <see cref="Streaming"/> if not provided</param>
		/// <param name="fetch">Default value for <see cref="Fetch"/> if not provided</param>
		/// <returns>Options with all the values filled</returns>
		public static FdbRangeOptions EnsureDefaults(FdbRangeOptions? options, FdbStreamingMode mode, FdbFetchMode fetch)
		{
			if (options == null)
			{
				if (mode == FdbStreamingMode.Iterator && fetch == FdbFetchMode.KeysAndValues)
				{
					options = FdbRangeOptions.Default;
				}
				else if (mode == FdbStreamingMode.WantAll && fetch == FdbFetchMode.KeysAndValues)
				{
					options = FdbRangeOptions.Default;
				}
				else
				{
					options = new()
					{
						Streaming = mode,
						Fetch = fetch,
					};
				}
			}

			if ((options.Streaming == null && mode != FdbStreamingMode.Iterator)
			 || (options.Fetch == null && fetch != FdbFetchMode.KeysAndValues))
			{
				// the default is Iterator, so only change if that is not what we want
				options = options with
				{
					Streaming = options.Streaming ?? mode,
					Fetch = options.Fetch ?? fetch,
				};
			}

			Contract.Debug.Ensures((options.Limit ?? 0) >= 0, "Limit cannot be negative");
			Contract.Debug.Ensures((options.TargetBytes ?? 0) >= 0, "TargetBytes cannot be negative");
#if NET5_0_OR_GREATER
			Contract.Debug.Ensures(options.Streaming == null || Enum.IsDefined(options.Streaming.Value), "Streaming mode must be valid");
			Contract.Debug.Ensures(options.Fetch == null || Enum.IsDefined(options.Fetch.Value), "Reading mode must be valid");
#else
			// the generic Enum.IsDefined<T>(T) overload is not available, using the legacy (Type, object) overload instead (which boxes)
			Contract.Debug.Ensures(options.Streaming == null || Enum.IsDefined(typeof(FdbStreamingMode), options.Streaming.Value), "Streaming mode must be valid");
			Contract.Debug.Ensures(options.Fetch == null || Enum.IsDefined(typeof(FdbFetchMode), options.Fetch.Value), "Reading mode must be valid");
#endif

			return options;
		}

		/// <summary>Throws if values are not legal</summary>
		public void EnsureLegalValues(int iteration)
		{
			EnsureLegalValues(this.Limit, this.TargetBytes, this.Streaming, this.Fetch, iteration);
		}

		internal static void EnsureLegalValues(int? limit, int? targetBytes, FdbStreamingMode? mode, FdbFetchMode? read, int iteration)
		{
			if (limit < 0) throw InvalidOptionValue("Range Limit cannot be negative.");
			if (targetBytes < 0) throw InvalidOptionValue("Range TargetBytes cannot be negative.");
			if (mode is < FdbStreamingMode.WantAll or > FdbStreamingMode.Serial) throw InvalidOptionValue("Range StreamingMode must be valid.");
			if (read is < FdbFetchMode.KeysAndValues or > FdbFetchMode.ValuesOnly) throw InvalidOptionValue("Range ReadMode must be valid.");
			if (iteration < 0) throw InvalidOptionValue("Iteration counter cannot be negative.");
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static FdbException InvalidOptionValue(string message) => new(FdbError.InvalidOptionValue, message);

		#region Fluent API...

		/// <summary>Sets the maximum number of results to return</summary>
		[Pure]
		public FdbRangeOptions WithLimit(int limit)
			=> this.Limit == limit ? this
			: limit == 1 && ReferenceEquals(this, FdbRangeOptions.Default) ? FdbRangeOptions.First
			: limit == 1 && ReferenceEquals(this, FdbRangeOptions.Reversed) ? FdbRangeOptions.Last
			: this with { Limit = limit };

		/// <summary>Sets a limit of bytes to read</summary>
		[Pure]
		public FdbRangeOptions WithTargetBytes(int targetBytes)
			=> this.TargetBytes == targetBytes ? this
			: this with { TargetBytes = targetBytes };

		/// <summary>Sets the <see cref="FdbStreamingMode"/> for range read</summary>
		[Pure]
		public FdbRangeOptions WithStreamingMode(FdbStreamingMode mode)
			=> this.Streaming == mode ? this
			: mode == FdbStreamingMode.WantAll && ReferenceEquals(this, FdbRangeOptions.Default) ? FdbRangeOptions.WantAll
			: this with { Streaming = mode };

		/// <summary>Returns the results in reverse order (starting from the end key)</summary>
		[Pure]
		public FdbRangeOptions InReverse()
			=> this.IsReversed ? this
			: ReferenceEquals(this, FdbRangeOptions.Default) ? FdbRangeOptions.WantAllReversed
			: ReferenceEquals(this, FdbRangeOptions.WantAll) ? FdbRangeOptions.WantAllReversed
			: ReferenceEquals(this, FdbRangeOptions.First) ? FdbRangeOptions.Last
			: ReferenceEquals(this, FdbRangeOptions.Last) ? FdbRangeOptions.First
			: ReferenceEquals(this, FdbRangeOptions.WantAllValuesOnly) ? FdbRangeOptions.WantAllReversedValuesOnly
			: ReferenceEquals(this, FdbRangeOptions.WantAllKeysOnly) ? FdbRangeOptions.WantAllReversedKeysOnly
			: this with { IsReversed = true };

		/// <summary>Only read the keys, and discard the values</summary>
		[Pure]
		public FdbRangeOptions OnlyKeys()
			=> this.Fetch == FdbFetchMode.KeysOnly ? this
			: ReferenceEquals(this, FdbRangeOptions.WantAll) ? FdbRangeOptions.WantAllKeysOnly
			: this with { Fetch = FdbFetchMode.KeysOnly };

		/// <summary>Only read the values, and discard the keys</summary>
		[Pure]
		public FdbRangeOptions OnlyValues()
			=> this.Fetch == FdbFetchMode.ValuesOnly ? this
			: ReferenceEquals(this, FdbRangeOptions.WantAll) ? FdbRangeOptions.WantAllValuesOnly
			: this with { Fetch = FdbFetchMode.ValuesOnly };

		/// <summary>Select an <see cref="ArrayPool{T}"/> to allocate the keys and/or values in memory.</summary>
		/// <remarks>
		/// <para>When using a pool, the caller MUST guarantee that the results are released to the pool; otherwise, the performances will be degraded.</para>
		/// <para>Incorrect usage of pooled data may lead to data corruption and overall instability. Use with caution!</para>
		/// </remarks>
		public FdbRangeOptions WithPool(ArrayPool<byte> pool)
			=> ReferenceEquals(this.Pool, pool) ? this : this with { Pool = pool };

		#endregion

	}

}
