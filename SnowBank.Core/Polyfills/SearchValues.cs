#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// Minimal netstandard2.0 backport of System.Buffers.SearchValues (.NET 8+), covering the subset the SDK uses
// (Create + Contains). The real type compiles the value set into SIMD-friendly lookup structures; this fallback
// does a linear scan over the original values — same results, slower for large sets (ours are < 10 chars).
// Public: BCL-shaped, so consumer code using SearchValues compiles identically once retargeted to modern .NET.

#if NETSTANDARD2_0

namespace System.Buffers
{

	/// <summary>netstandard2.0 stand-in for <c>System.Buffers.SearchValues&lt;T&gt;</c> (subset: <see cref="Contains"/>).</summary>
	public sealed class SearchValues<T> where T : IEquatable<T>
	{

		private readonly T[] Values;

		internal SearchValues(T[] values) => this.Values = values;

		/// <summary>Tests whether the specified value is in the set.</summary>
		public bool Contains(T value)
		{
			foreach (var candidate in this.Values)
			{
				if (candidate.Equals(value)) return true;
			}
			return false;
		}

	}

	/// <summary>netstandard2.0 stand-in for the <c>SearchValues.Create</c> factories.</summary>
	public static class SearchValues
	{

		/// <summary>Creates an immutable set of characters, optimized for search operations.</summary>
		public static SearchValues<char> Create(string values) => new(values.ToCharArray());

		/// <summary>Creates an immutable set of characters, optimized for search operations.</summary>
		public static SearchValues<char> Create(ReadOnlySpan<char> values) => new(values.ToArray());

		/// <summary>Creates an immutable set of bytes, optimized for search operations.</summary>
		public static SearchValues<byte> Create(ReadOnlySpan<byte> values) => new(values.ToArray());

	}

}

#endif
