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


namespace SnowBank.Testing
{
	using System.Buffers;
	using System.Collections.Generic;
	using System.Linq;
	using System.Numerics;
	using System.Runtime.CompilerServices;

	/// <summary>Extension methods for <see cref="HistoryRecordingArrayPool{T}"/></summary>
	[PublicAPI]
	public static class HistoryRecordingArrayPool
	{

		/// <summary>Creates a new history recording array pool that allocates the exact size requested</summary>
		/// <remarks>This is not typical of <see cref="ArrayPool{T}.Shared"/>, but can make testing easier.</remarks>
		public static HistoryRecordingArrayPool<T> CreateExactSize<T>(string? label = null)
			=> new(new ExactSizeHistoryRecordingPoolStrategy<T>(), label);

		/// <summary>Creates a new history recording array pool that rounds requested sizes to the next power of 2</summary>
		/// <remarks>This is close to the behavior of <see cref="ArrayPool{T}.Shared"/></remarks>
		public static HistoryRecordingArrayPool<T> CreateRoundNextPow2<T>(string? label = null)
			=> new(new NextPowerOfTwoHistoryRecordingPoolStrategy<T>(), label);

		/// <summary>Creates a new history recording array pool that delegates to another pool</summary>
		public static HistoryRecordingArrayPool<T> CreateFromPool<T>(ArrayPool<T> pool, string? label = null)
			=> new(new DelegatingHistoryRecordingPoolStrategy<T>(pool), label);

		/// <summary>Creates a new history recording array pool that delegates to an isolated pool</summary>
		public static HistoryRecordingArrayPool<T> Create<T>(string? label = null)
			=> new(new DelegatingHistoryRecordingPoolStrategy<T>(ArrayPool<T>.Create()), label);

		/// <summary>Creates a new history recording array pool that delegates to an isolated pool</summary>
		public static HistoryRecordingArrayPool<T> Create<T>(int maxArraysPerBucket, string? label = null)
			=> new(new DelegatingHistoryRecordingPoolStrategy<T>(ArrayPool<T>.Create(1024 * 1024, maxArraysPerBucket)), label);

	}

	[PublicAPI]
	public interface IHistoryRecordingPoolStrategy<T>
	{

		T[] Rent(int minimumSize);

		void Return(T[] array, bool clearArray);

	}

	internal sealed class NextPowerOfTwoHistoryRecordingPoolStrategy<T> : IHistoryRecordingPoolStrategy<T>
	{

		/// <inheritdoc />
		public T[] Rent(int minimumSize) => new T[checked((int) BitOperations.RoundUpToPowerOf2((uint) minimumSize))];

		/// <inheritdoc />
		public void Return(T[] array, bool clearArray)
		{
			if (clearArray) Array.Clear(array, 0, array.Length);
		}

	}

	internal sealed class DelegatingHistoryRecordingPoolStrategy<T> : IHistoryRecordingPoolStrategy<T>
	{

		public DelegatingHistoryRecordingPoolStrategy(ArrayPool<T> pool)
		{
			Contract.NotNull(pool);
			this.Pool = pool;
		}

		public ArrayPool<T> Pool { get; }

		/// <inheritdoc />
		public T[] Rent(int minimumSize) => this.Pool.Rent(minimumSize);

		/// <inheritdoc />
		public void Return(T[] array, bool clearArray) => this.Pool.Return(array, clearArray);

	}

	internal sealed class ExactSizeHistoryRecordingPoolStrategy<T> : IHistoryRecordingPoolStrategy<T>
	{
		/// <inheritdoc />
		public T[] Rent(int minimumSize) => new T[minimumSize];

		/// <inheritdoc />
		public void Return(T[] array, bool clearArray)
		{
			if (clearArray) Array.Clear(array, 0, array.Length);
		}
	}

	[PublicAPI]
	public interface IHistoryRecodingPoolFilter<T>
	{
		void OnRent(HistoryRecordingArrayPool<T>.RentedBuffer evt);

		void OnReturn(HistoryRecordingArrayPool<T>.ReturnedBuffer evt);
	}

	internal sealed class DefaultHistoryRecordingPoolFilter<T> : IHistoryRecodingPoolFilter<T>
	{

		public DefaultHistoryRecordingPoolFilter(
			Action<HistoryRecordingArrayPool<T>.RentedBuffer>? rentHandler,
			Action<HistoryRecordingArrayPool<T>.ReturnedBuffer>? returnHandler
		)
		{
			this.RentHandler = rentHandler ?? ((_) => { });
			this.ReturnHandler = returnHandler ?? ((_) => { });
		}

		public Action<HistoryRecordingArrayPool<T>.RentedBuffer> RentHandler { get; }

		public Action<HistoryRecordingArrayPool<T>.ReturnedBuffer> ReturnHandler { get; }

		/// <inheritdoc />
		public void OnRent(HistoryRecordingArrayPool<T>.RentedBuffer evt) => this.RentHandler(evt);

		/// <inheritdoc />
		public void OnReturn(HistoryRecordingArrayPool<T>.ReturnedBuffer evt) => this.ReturnHandler(evt);
	}

	/// <summary>Implementation of <see cref="ArrayPool{T}"/> that records all rented and returned arrays</summary>
	/// <remarks>This pool <b>DOES NOT</b> reuse returned arrays, and is not appropriate for testing concurrency or performance.</remarks>
	[PublicAPI]
	public class HistoryRecordingArrayPool<T> : ArrayPool<T>
	{

		[PublicAPI]
		public sealed record RentedBuffer
		{
			public RentedBuffer(HistoryRecordingArrayPool<T> pool, int token, int requestedSize, int allocatedSize, T[] array, int tag, StackTrace? callstack)
			{
				this.Pool = pool;
				this.Token = token;
				this.RequestedSize = requestedSize;
				this.AllocatedSize = allocatedSize;
				this.Array = array;
				this.Tag = tag;
				this.Callstack = callstack;
			}

			public HistoryRecordingArrayPool<T> Pool { get; }

			public int Token { get; }

			public int RequestedSize { get; }

			public int AllocatedSize { get; }

			public T[] Array { get; }

			public int Tag { get; }

			public StackTrace? Callstack { get; }

			public void AssertSame(T[]? array, string? message = null)
			{
				if (message != null) message += "\r\n";

				if (array == null)
				{
					Assert.That(array, Is.Not.Null, message + "Null arrays never come from a pool");
					return;
				}
				if (array.Length == 0)
				{
					Assert.That(array, Is.Not.Empty, message + "Empty arrays never come from a pool");
					return;
				}
				if (!ReferenceEquals(array, this.Array))
				{
					Assert.That(array, Is.SameAs(this.Array), $"{message}The buffer of length {array.Length} (tag <{GetTag(array)}>) does match the rented buffer of length {this.AllocatedSize} (tag <{this.Tag}>)");
				}
			}

		}

		[PublicAPI]
		public sealed record ReturnedBuffer
		{
			public ReturnedBuffer(HistoryRecordingArrayPool<T> pool, int token, int requestedSize, int allocatedSize, T[] array, int tag, T[] snapshot, bool cleared, bool allZeroes, StackTrace? callstack)
			{
				this.Pool = pool;
				this.Token = token;
				this.RequestedSize = requestedSize;
				this.AllocatedSize = allocatedSize;
				this.Array = array;
				this.Tag = tag;
				this.Snapshot = snapshot;
				this.Cleared = cleared;
				this.AllZeroes = allZeroes;
				this.Callstack = callstack;
			}

			public HistoryRecordingArrayPool<T> Pool { get; }

			public int Token { get; }

			public int RequestedSize { get; }

			public int AllocatedSize { get; }

			public T[] Array { get; }

			public int Tag { get; }

			public T[] Snapshot { get; }

			public bool Cleared { get; }

			public bool AllZeroes { get; }

			public StackTrace? Callstack { get; }

			/// <summary>Should have called <c>pool.Return(..., clearArray: false)</c></summary>
			public void AssertZeroed()
			{
				if (!this.AllZeroes)
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been zeroed manually before being returned.");
				}
			}

			/// <summary>Should have called <c>pool.Return(..., clearArray: false)</c></summary>
			public void AssertCleared()
			{
				if (!this.Cleared)
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been returned with clearArray == true.");
				}
			}

			/// <summary>Should have called <c>pool.Return(..., clearArray: false)</c>, and the array should not have been cleared manually</summary>
			public void AssertNotClearedAndNotZeroed()
			{
				if (this.Cleared)
				{
					if (this.AllZeroes)
					{
						Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been returned with clearArray == false, and should not have been zeroed manually.");
					}
					else
					{
						Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been returned with clearArray == false.");
					}
				}
				else if (this.AllZeroes)
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should not have been zeroed manually before being returned.");
				}
			}

			/// <summary>Should have called <c>pool.Return(..., clearArray: true)</c>, but the array should not have been cleared manually</summary>
			public void AssertClearedButNotZeroed()
			{
				if (this.Cleared)
				{
					if (this.AllZeroes)
					{
						Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should not have been zeroed manually before being returned.");
					}
				}
				else
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been returned with clearArray == true.");
				}
			}

			/// <summary>Should have called <c>pool.Return(..., clearArray: false)</c></summary>
			public void AssertZeroedButNotCleared()
			{
				if (this.Cleared)
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been returned with clearArray == false.");
				}
				else if (!this.AllZeroes)
				{
					Assert.Fail($"The buffer of length {this.AllocatedSize} (tag <{this.Tag}>) should have been zeroed manually before being returned.");
				}
			}


		}

		public HistoryRecordingArrayPool(IHistoryRecordingPoolStrategy<T> strategy, string? label = null)
		{
			this.Strategy = strategy;
			this.Label = label;
		}

		public IHistoryRecordingPoolStrategy<T> Strategy { get; }

		public string? Label { get; }

		public List<IHistoryRecodingPoolFilter<T>> Filters { get; } = [ ];

		public bool CaptureStackTraces { get; set; }

		public Dictionary<T[], int> NotReturned { get; } = [ ];

		public List<RentedBuffer> Rented { get; } = [ ];

		public List<ReturnedBuffer> Returned { get; } = [ ];

#if NET9_0_OR_GREATER
		private readonly Lock PadLock = new();
#else
		private readonly object PadLock = new();
#endif

		public HistoryRecordingArrayPool<T> WithFilter(IHistoryRecodingPoolFilter<T> filter)
		{
			Contract.NotNull(filter);
			this.Filters.Add(filter);
			return this;
		}

		public HistoryRecordingArrayPool<T> WithFilter(
			Action<RentedBuffer>? onRent,
			Action<ReturnedBuffer>? onReturn = null
		)
		{
			this.Filters.Add(new DefaultHistoryRecordingPoolFilter<T>(onRent, onReturn));
			return this;
		}

		public HistoryRecordingArrayPool<T> WithStackTraces(bool enabled = true)
		{
			this.CaptureStackTraces = enabled;
			return this;
		}

		private static int GetTag(T[] array) => RuntimeHelpers.GetHashCode(array);

		/// <inheritdoc />
		public override T[] Rent(int minimumLength)
		{
			lock (this.PadLock)
			{
				var array = this.Strategy.Rent(minimumLength);
				if (array == null!)
				{
					Assert.Fail("Internal pool returned a null array!");
					throw null!;
				}

				if (array.Length < minimumLength)
				{
					Assert.Fail("Internal pool returned an array smaller than requested!");
					throw null!;
				}

				if (this.NotReturned.ContainsKey(array))
				{
					Assert.Fail("Internal pool returned an array that should has been borrowed but not yet returned ?!");
					throw null!;
				}

				var tag = GetTag(array);
				var token = this.Rented.Count;

				var evt = new RentedBuffer(this, token, minimumLength, array.Length, array, tag, this.CaptureStackTraces ? new StackTrace() : null);
				foreach (var filter in this.Filters)
				{
					filter.OnRent(evt);
				}

				this.NotReturned.Add(array, token);
				this.Rented.Add(evt);

				return array;
			}
		}

		/// <inheritdoc />
		public override void Return(T[] array, bool clearArray = false)
		{
			lock (this.PadLock)
			{
				Assert.That(array, Is.Not.Null);

				if (array.Length == 0)
				{
					Assert.Fail($"Attempted to return an buffer buffer !!!");
					return;
				}

				if (!this.NotReturned.Remove(array, out var token))
				{
					Assert.Fail($"Attempted to return a buffer of size {array.Length} (tag <{GetTag(array)}>) not originally from this pool !!!");
					return;
				}

				// create a snapshot of the array as it was when returned
				var snapshot = array.ToArray();

#if NET5_0_OR_GREATER
				// this is the magic spell to be able to do IndexOfAny(defaut(T)) on a Span<T> without the IEquatable<T> constraint!
				var asBytes = System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in array[0])), Unsafe.SizeOf<T>() * array.Length);
#if NET8_0_OR_GREATER
				bool allZeroes = asBytes.IndexOfAnyExcept((byte) 0) < 0;
#else
				bool allZeroes = true;
				foreach (var b in asBytes)
				{
					if (b != 0) { allZeroes = false; break; }
				}
#endif
#else
				// MemoryMarshal.CreateReadOnlySpan is not available: compare each element to default(T) instead of
				// reinterpreting the array as raw bytes (equivalent for this all-zeroes diagnostic, just slower)
				bool allZeroes = true;
				var zeroCmp = EqualityComparer<T>.Default;
				foreach (var item in array)
				{
					if (!zeroCmp.Equals(item, default!)) { allZeroes = false; break; }
				}
#endif

				this.Strategy.Return(array, clearArray);


				var rent = this.Rented[token];
				var evt = new ReturnedBuffer(this, token, rent.RequestedSize, rent.AllocatedSize, array, rent.Tag, snapshot, clearArray, allZeroes, this.CaptureStackTraces ? new StackTrace() : null);

				foreach (var filter in this.Filters)
				{
					filter.OnReturn(evt);
				}

				this.Returned.Add(evt);
				this.NotReturned.Remove(array);
			}
		}

		/// <summary>Assert that everything has been returned properly, and that there are no un-returned buffers</summary>
		public void AssertAllReturned()
		{
			lock (this.PadLock)
			{
				if (this.NotReturned.Count != 0)
				{
					var sb = new StringBuilder();
					sb.AppendLine("At least one buffer was not returned the pool!");
					Dump(sb);
					Assert.Fail(sb.ToString());
				}
			}
		}

		public void Dump()
		{
			var sb = Dump(new());
			sb.Remove(sb.Length - 2, 2);
			SimpleTest.Log(sb);
		}
		public StringBuilder Dump(StringBuilder sb)
		{
			var typeName = typeof(T).GetFriendlyName();
			lock (this.PadLock)
			{
				sb.AppendLineInvariant($"# ArrayPool<{typeName}>: {this.Rented.Count:N0} rented, {this.Returned.Count:N0} returned, {this.NotReturned.Count:N0} borrowed");

				if (this.Returned.Count > 0)
				{
					foreach (var ret in this.Returned)
					{
						var cs = ret.Callstack?.FrameCount > 0 ? (" | caller: " + FormatCallstack(ret.Callstack, 2)) : null;
						sb.AppendLineInvariant($"# - | returned | {ret.Token,3} | tag: <{ret.Tag:X08}> | req: {ret.RequestedSize,9:N0} | alloc: {ret.AllocatedSize,9:N0} | {(ret.Cleared ? "cleared" : ret.AllZeroes ? "zeroed " : "LEAKED!")}{cs}");
					}
				}

				if (this.NotReturned.Count > 0)
				{
					foreach (var token in this.NotReturned.Values)
					{
						var rent = this.Rented[token];
						var cs = rent.Callstack?.FrameCount > 0 ? (" | caller: " + FormatCallstack(rent.Callstack, 2)) : null;
						sb.AppendLineInvariant($"# - | PENDING  | {rent.Token,3} | tag: <{rent.Tag:X08}> | req: {rent.RequestedSize,9:N0} | alloc: {rent.AllocatedSize,9:N0}{cs}");
					}
				}
				return sb;
			}
		}

		private string FormatCallstack(StackTrace st, int depth)
		{
			var self = this.GetType().GetGenericTypeDefinition();

			// we have to find the first useful frame
			// - the top is internal runtime stuff to create the stacktrace
			// - then we have the internals of this object
			// - then we have the usual suspects, like SliceWriter that we want to skip

			bool usualSuspects = true;

			string? res = null;

			Type? last = null;

			for (int i = 0; i < st.FrameCount; i++)
			{
				var frame = st.GetFrame(i);
				if (frame == null || !frame.HasMethod()) continue;

				var method = frame.GetMethod()!;
				var declaringType = method.DeclaringType!;

				if (usualSuspects)
				{
					if (declaringType != self && declaringType != typeof(SliceWriter) && declaringType != typeof(SliceOwner))
					{
						// we want to keep the entry method!
						if (last == self)
						{
							--i;
						}
						else
						{
							res = "[...]";
							i -= 2;
						}
						usualSuspects = false;
					}
					last = declaringType;
					continue;
				}

				if (depth <= 0)
				{
					break;
				}

				res = res + (res != null ? " <- " : null) + method.GetFriendlyName();
				--depth;
			}
			return res ?? "<unknown?!>";
		}

		public RentedBuffer LastRented
		{
			get
			{
				lock (this.PadLock)
				{
					if (this.Rented.Count == 0)
					{
						Assert.Fail("The pool has not rented any buffer yet");
						throw null!;
					}

					return this.Rented[^1];
				}
			}
		}

		public void AssertIsRentedFromPool(T[]? array)
		{
			lock (this.PadLock)
			{
				if (array is null)
				{
					Assert.That(array, Is.Not.Null, "Null arrays do not come from a pool");
					return;
				}

				if (array.Length == 0)
				{
					Assert.That(array, Is.Not.Empty, "Empty arrays do not come from a pool");
					return;
				}

				if (this.Rented.Count == 0)
				{
					Assert.Fail("The pool has not been called yet");
					return;
				}

				foreach (var rent in this.Rented)
				{
					if (ReferenceEquals(array, rent.Array))
					{
						return;
					}
				}

				Dump();
				Assert.Fail("The array was not rented from this pool!");
			}
		}

		public void AssertRented(int count)
		{
			lock (this.PadLock)
			{
				if (this.Rented.Count != count)
				{
					Assert.That(this.Rented, Has.Count.EqualTo(count), $"The pool should have rented {count} buffer(s)");
				}
			}
		}

		public void AssertPending(int count)
		{
			lock (this.PadLock)
			{
				if (this.NotReturned.Count != count)
				{
					Assert.That(this.NotReturned, Has.Count.EqualTo(count), $"The pool should have {count} pending buffer(s)");
				}
			}
		}

		public void AssertReturned(int count)
		{
			lock (this.PadLock)
			{
				if (this.Returned.Count != count)
				{
					Assert.That(this.Returned, Has.Count.EqualTo(count), $"The pool should have {count} returned buffer(s)");
				}
			}
		}

	}

}
