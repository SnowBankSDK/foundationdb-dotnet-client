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

// netstandard2.0's System.Memory provides most of MemoryMarshal, but not CreateReadOnlySpan/CreateSpan/AsRef.
// The netstandard2.0 build redirects the MemoryMarshal name to this shim (via a file-local using alias) so those three
// can be added while the rest simply delegate to the real implementation.

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using BclMemoryMarshal = System.Runtime.InteropServices.MemoryMarshal;

	internal static class MemoryMarshalCompat
	{

		// --- delegated to the real BCL implementation (present on netstandard2.0) ---

		public static ref T GetReference<T>(Span<T> span) => ref BclMemoryMarshal.GetReference(span);

		public static ref T GetReference<T>(ReadOnlySpan<T> span) => ref BclMemoryMarshal.GetReference(span);

		public static T Read<T>(ReadOnlySpan<byte> source) where T : struct => BclMemoryMarshal.Read<T>(source);

		public static bool TryRead<T>(ReadOnlySpan<byte> source, out T value) where T : struct => BclMemoryMarshal.TryRead(source, out value);

		public static Span<TTo> Cast<TFrom, TTo>(Span<TFrom> span) where TFrom : struct where TTo : struct => BclMemoryMarshal.Cast<TFrom, TTo>(span);

		public static ReadOnlySpan<TTo> Cast<TFrom, TTo>(ReadOnlySpan<TFrom> span) where TFrom : struct where TTo : struct => BclMemoryMarshal.Cast<TFrom, TTo>(span);

		public static Span<byte> AsBytes<T>(Span<T> span) where T : struct => BclMemoryMarshal.AsBytes(span);

		public static ReadOnlySpan<byte> AsBytes<T>(ReadOnlySpan<T> span) where T : struct => BclMemoryMarshal.AsBytes(span);

		public static bool TryGetArray<T>(ReadOnlyMemory<T> memory, out ArraySegment<T> segment) => BclMemoryMarshal.TryGetArray(memory, out segment);

		public static bool TryGetString(ReadOnlyMemory<char> memory, out string text, out int start, out int length) => BclMemoryMarshal.TryGetString(memory, out text, out start, out length);

		// --- missing from netstandard2.0 ---

		// Write: equivalent to the BCL (no allocation); on modern targets this is Write<T>(Span<byte>, in T).
		public static void Write<T>(Span<byte> destination, in T value) where T : struct
		{
			if (Unsafe.SizeOf<T>() > destination.Length) throw new ArgumentOutOfRangeException(nameof(destination));
			Unsafe.WriteUnaligned(ref BclMemoryMarshal.GetReference(destination), value);
		}

		// CAVEAT: the BCL CreateReadOnlySpan/CreateSpan/AsRef produce a span that carries a *managed* interior pointer, so
		// the GC keeps tracking the target and it stays valid even if the backing object moves. This fallback goes through
		// a raw pointer (Unsafe.AsPointer), which the GC does NOT track: the result is only safe while the target is pinned,
		// on the stack, or otherwise non-movable. That holds for the current callers (spans over already-fixed buffers), but
		// a future caller passing a ref into a movable heap object would get a span that can dangle after a GC.
		public static unsafe ReadOnlySpan<T> CreateReadOnlySpan<T>(ref T reference, int length)
			=> new ReadOnlySpan<T>(Unsafe.AsPointer(ref reference), length);

		public static unsafe Span<T> CreateSpan<T>(ref T reference, int length)
			=> new Span<T>(Unsafe.AsPointer(ref reference), length);

		// AsRef: no allocation; reinterprets the span's storage in place, same as the BCL.
		public static ref T AsRef<T>(ReadOnlySpan<byte> span) where T : struct
		{
			if (Unsafe.SizeOf<T>() > span.Length) throw new ArgumentOutOfRangeException(nameof(span));
			return ref Unsafe.As<byte, T>(ref BclMemoryMarshal.GetReference(span));
		}

		public static ref T AsRef<T>(Span<byte> span) where T : struct
		{
			if (Unsafe.SizeOf<T>() > span.Length) throw new ArgumentOutOfRangeException(nameof(span));
			return ref Unsafe.As<byte, T>(ref BclMemoryMarshal.GetReference(span));
		}

	}

}

#endif
