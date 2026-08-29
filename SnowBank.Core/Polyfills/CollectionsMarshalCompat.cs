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

// netstandard2.0 has no System.Runtime.InteropServices.CollectionsMarshal. The netstandard2.0 build redirects the
// CollectionsMarshal name to this shim via file-local aliases in the sources that need it.
// Internal: plumbing for the sources only (Compat-branded names must never appear in application code).

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Reflection;
	using System.Runtime.CompilerServices;

	internal static class CollectionsMarshalCompat
	{

		private static class ListAccessor<T>
		{
			// CAVEAT: reaches into List<T>'s private backing-array field by name. This is safe for the netstandard2.0 build's
			// actual audience (.NET Framework 4.7.2+, whose BCL is frozen and names the field "_items", same as .NET Core),
			// but would break on an unusual netstandard2.0 runtime that names it differently, in which case we throw
			// at first use instead of silently returning a non-live copy (callers may WRITE through the span).
			public static readonly FieldInfo Items = typeof(List<T>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)
				?? throw new PlatformNotSupportedException($"Cannot locate the backing array field of List<{typeof(T).Name}> on this runtime.");
		}

		/// <summary>Returns a <see cref="Span{T}"/> over the live backing array of the list (same contract as the real <c>CollectionsMarshal.AsSpan</c>: writes are visible in the list, and the span is invalidated if the list grows).</summary>
		public static Span<T> AsSpan<T>(List<T>? list)
		{
			if (list is null || list.Count == 0) return default;
			var items = (T[]) ListAccessor<T>.Items.GetValue(list)!;
			return new Span<T>(items, 0, list.Count);
		}

		/// <summary>Sets the count of the list (same contract as the real <c>CollectionsMarshal.SetCount</c>, except that new elements are <c>default</c> instead of undefined).</summary>
		public static void SetCount<T>(List<T> list, int count)
		{
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
			if (count < list.Count)
			{
				list.RemoveRange(count, list.Count - count);
			}
			else
			{
				if (list.Capacity < count) list.Capacity = count;
				while (list.Count < count) list.Add(default!);
			}
		}

		/// <summary>Wraps an array as an <see cref="ImmutableArray{T}"/> without copying (same contract as <c>ImmutableCollectionsMarshal.AsImmutableArray</c>: the caller must not mutate the array afterwards).</summary>
		public static ImmutableArray<T> AsImmutableArray<T>(T[]? array)
		{
			// ImmutableArray<T> is a struct with a single T[] field: reinterpret in place, exactly like the real marshal does.
			return Unsafe.As<T[]?, ImmutableArray<T>>(ref array);
		}

	}

}

#endif
