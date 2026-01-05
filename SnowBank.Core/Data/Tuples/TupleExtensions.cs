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

#pragma warning disable IL2091 // Target generic argument does not satisfy 'DynamicallyAccessedMembersAttribute' in target method or type. The generic parameter of the source method or type does not have matching annotations.

namespace SnowBank.Data.Tuples
{
	using System.ComponentModel;
	using SnowBank.Data.Tuples.Binary;

	/// <summary>Add extensions methods that deal with tuples on various types</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class TupleExtensions
	{

		#region ITuple extensions...

		/// <summary>Returns true if the tuple is either null or empty</summary>
		[ContractAnnotation("null => true")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty(this IVarTuple? tuple)
		{
			return tuple == null || tuple.Count == 0;
		}

		/// <summary>Returns an array containing all the objects of a tuple</summary>
		public static object?[] ToArray(this IVarTuple tuple)
		{
			Contract.NotNull(tuple);

			var items = new object[tuple.Count];
			if (items.Length > 0)
			{
				tuple.CopyTo(items, 0);
			}
			return items;
		}

		/// <summary>Returns a typed array containing all the items of a tuple</summary>
		public static T[] ToArray<T>(this IVarTuple tuple)
		{
			Contract.NotNull(tuple);

			var items = new T[tuple.Count];
			if (items.Length > 0)
			{
				for (int i = 0; i < items.Length; i++)
				{
					items[i] = tuple.Get<T>(i)!;
				}
			}
			return items;
		}

		/// <summary>Returns the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="T">Expected type of the item</typeparam>
		/// <param name="tuple">Tuple instance</param>
		/// <param name="index">Position of the item, with <c>0</c> for the first element, and <c>^1</c> for the last element</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="T"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the tuple</exception>
		/// <example>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^3) => "Hello"</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^2) => "World"</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;int&gt;(^1) => 123</c></para>
		/// <para><c>("Hello", "World", 123,).Get&lt;string&gt;(^1) => "123"</c></para>
		/// </example>
		[Pure]
		public static T? Get<T>(this IVarTuple tuple, Index index)
		{
			return tuple.Get<T>(index.GetOffset(tuple.Count));
		}

		/// <summary>Appends two values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2>(this IVarTuple tuple, T1 value1, T2 value2)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2>>(tuple, STuple.Create(value1, value2));
		}

		/// <summary>Appends two values at the end of a tuple</summary>
		public static IVarTuple Append<THead, T1, T2>(this THead tuple, T1 value1, T2 value2)
			where THead : IVarTuple
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<THead, STuple<T1, T2>>(tuple, STuple.Create(value1, value2));
		}

		/// <summary>Appends three values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3>>(tuple, STuple.Create(value1, value2, value3));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3, T4>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3, T4>>(tuple, STuple.Create(value1, value2, value3, value4));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3, T4, T5>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3, T4, T5>>(tuple, STuple.Create(value1, value2, value3, value4, value5));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3, T4, T5, T6>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3, T4, T5, T6>>(tuple, STuple.Create(value1, value2, value3, value4, value5, value6));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3, T4, T5, T6, T7>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3, T4, T5, T6, T7>>(tuple, STuple.Create(value1, value2, value3, value4, value5, value6, value7));
		}

		/// <summary>Appends four values at the end of a tuple</summary>
		public static IVarTuple Append<T1, T2, T3, T4, T5, T6, T7, T8>(this IVarTuple tuple, T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8)
		{
			Contract.NotNull(tuple);
			return new JoinedTuple<IVarTuple, STuple<T1, T2, T3, T4, T5, T6, T7, T8>>(tuple, STuple.Create(value1, value2, value3, value4, value5, value6, value7, value8));
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <returns>Tuple that contains only the items past the first <paramref name="offset"/> items of the current tuple</returns>
		public static IVarTuple Substring<TTuple>(this TTuple tuple, int offset) where TTuple : IVarTuple
		{
			Contract.NotNull(tuple);
			return tuple[Range.StartAt(offset)];
		}

		/// <summary>Returns a substring of the current tuple</summary>
		/// <param name="tuple">Current tuple</param>
		/// <param name="offset">Offset from the start of the current tuple (negative value means from the end)</param>
		/// <param name="count">Number of items to keep</param>
		/// <returns>Tuple that contains only the selected items from the current tuple</returns>
		public static IVarTuple Substring<TTuple>(this TTuple tuple, int offset, int count) where TTuple : IVarTuple
		{
			Contract.NotNull(tuple);
			Contract.Positive(count);

			if (count == 0) return STuple.Empty;
			return tuple[offset, offset + count];
		}

		/// <summary>Returns a tuple with only the first (or last) items of this tuple</summary>
		/// <param name="tuple">Tuple to truncate</param>
		/// <param name="count">Number of items to keep. If positive, items will be taken from the start of the tuple. If negative, items will be taken from the end of the tuple</param>
		/// <returns>New tuple of size |<paramref name="count"/>|.</returns>
		/// <example>
		/// (a, b, c).Truncate(2) => (a, b)
		/// (a, b, c).Truncate(-2) => (b, c)
		/// </example>
		public static IVarTuple Truncate<TTuple>(this TTuple tuple, int count) where TTuple : IVarTuple
		{
			tuple.OfSizeAtLeast(Math.Abs(count));
			return count < 0
				? Substring(tuple, tuple.Count + count, -count)
				: tuple[Range.EndAt(count)];
		}

		/// <summary>Test if the start of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the beginning of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool StartsWith(this IVarTuple left, IVarTuple right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			//REVIEW: move this on ITuple interface ?
			return TupleHelpers.StartsWith(left, right);
		}

		/// <summary>Test if the end of current tuple is equal to another tuple</summary>
		/// <param name="left">Larger tuple</param>
		/// <param name="right">Smaller tuple</param>
		/// <returns>True if the end of <paramref name="left"/> is equal to <paramref name="right"/> or if both tuples are identical</returns>
		public static bool EndsWith(this IVarTuple left, IVarTuple right)
		{
			Contract.NotNull(left);
			Contract.NotNull(right);

			//REVIEW: move this on ITuple interface ?
			return TupleHelpers.EndsWith(left, right);
		}

		/// <summary>Transform a tuple of N elements into a list of N singletons</summary>
		/// <param name="tuple">Tuple that contains any number of elements</param>
		/// <returns>Sequence of tuples that contains a single element</returns>
		/// <example>(123, ABC, false,).Explode() => [ (123,), (ABC,), (false,) ]</example>
		public static IEnumerable<IVarTuple> Explode(this IVarTuple tuple)
		{
			Contract.NotNull(tuple);

			int p = 0;
			int n = tuple.Count;
			while (p < n)
			{
				yield return tuple[p, p + 1];
				++p;
			}
		}

		/// <summary>Ensures that the tuple has the expected size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller or larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTuple OfSize<TTuple>(this TTuple? tuple, int size) where TTuple : IVarTuple
		{
			return tuple != null && tuple.Count == size ? tuple : TupleInvalidSize(tuple, size);

			[DoesNotReturn, StackTraceHidden]
			static TTuple TupleInvalidSize(TTuple? tuple, int expected)
			{
				if (tuple == null) throw new ArgumentNullException(nameof(tuple));
				throw new InvalidOperationException($"This operation requires a tuple of size {expected}, but this tuple has {tuple.Count} elements");
			}
		}

		/// <summary>Ensures that the tuple has the expected size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller or larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SpanTuple OfSize(this SpanTuple tuple, int size)
		{
			return tuple.Count == size ? tuple : TupleInvalidSize(tuple.Count, size);

			[DoesNotReturn, StackTraceHidden]
			static SpanTuple TupleInvalidSize(int size, int expected)
			{
				throw new InvalidOperationException($"This operation requires a tuple of size {expected}, but this tuple has {size} elements");
			}
		}

		/// <summary>Ensures that the tuple has at least a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected minimum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTuple OfSizeAtLeast<TTuple>(this TTuple? tuple, int size) where TTuple : IVarTuple
		{
			return tuple != null && tuple.Count >= size ? tuple : TupleTooSmall(tuple, size);

			[DoesNotReturn, StackTraceHidden]
			static TTuple TupleTooSmall(TTuple? tuple, int expected)
			{
				if (tuple == null) throw new ArgumentNullException(nameof(tuple));
				throw new InvalidOperationException($"This operation requires a tuple of size {expected} or more, but this tuple has {tuple.Count} elements");
			}
		}

		/// <summary>Ensures that the tuple has at least a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected minimum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is smaller than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SpanTuple OfSizeAtLeast(this SpanTuple tuple, int size)
		{
			return tuple.Count >= size ? tuple : TupleTooSmall(tuple.Count, size);

			[DoesNotReturn, StackTraceHidden]
			static SpanTuple TupleTooSmall(int size, int expected)
			{
				throw new InvalidOperationException($"This operation requires a tuple of size {expected} or more, but this tuple has {size} elements");
			}
		}

		/// <summary>Ensures that the tuple has at most a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected maximum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="ArgumentNullException">If <paramref name="tuple"/> is null</exception>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TTuple OfSizeAtMost<TTuple>(this TTuple? tuple, int size) where TTuple : IVarTuple
		{
			return tuple != null && tuple.Count <= size ? tuple : TupleTooLarge(tuple, size);

			[DoesNotReturn, StackTraceHidden]
			static TTuple TupleTooLarge(TTuple? tuple, int expected)
			{
				if (tuple == null) throw new ArgumentNullException(nameof(tuple));
				throw new InvalidOperationException($"This operation requires a tuple of size {expected} or less, but this tuple has {tuple.Count} elements");
			}
		}

		/// <summary>Ensures that the tuple has at most a certain size</summary>
		/// <param name="tuple">Tuple which must be of a specific size</param>
		/// <param name="size">Expected maximum number of items in this tuple</param>
		/// <returns>The <paramref name="tuple"/> itself, if it has the correct size; otherwise, an exception is thrown</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> is larger than <paramref name="size"/></exception>
		[ContractAnnotation("halt <= tuple:null")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SpanTuple OfSizeAtMost(this SpanTuple tuple, int size)
		{
			return tuple.Count <= size ? tuple : TupleTooLarge(tuple.Count, size);

			[DoesNotReturn, StackTraceHidden]
			static SpanTuple TupleTooLarge(int size, int expected)
			{
				throw new InvalidOperationException($"This operation requires a tuple of size {expected} or less, but this tuple has {size} elements");
			}
		}

		/// <summary>Returns a typed version of a tuple of size 1</summary>
		/// <typeparam name="T1">Expected type of the single element</typeparam>
		/// <param name="tuple">Tuple that must be of size 1</param>
		/// <returns>Equivalent tuple, with its element converted to the specified type</returns>
		public static ValueTuple<T1?> As<T1>(this IVarTuple tuple)
		{
			tuple.OfSize(1);
			return new ValueTuple<T1>(tuple.Get<T1>(0)!);
		}

		/// <summary>Returns a typed version of a tuple of size 2</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <param name="tuple">Tuple that must be of size 2</param>
		/// <returns>Equivalent tuple, with its elements converted to the specified types</returns>
		public static ValueTuple<T1?, T2?> As<T1, T2>(this IVarTuple tuple)
		{
			tuple.OfSize(2);
			return (
				tuple.Get<T1>(0),
				tuple.Get<T2>(1)
			);
		}

		/// <summary>Returns a typed version of a tuple of size 3</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <param name="tuple">Tuple that must be of size 3</param>
		/// <returns>Equivalent tuple, with its elements converted to the specified types</returns>
		public static ValueTuple<T1?, T2?, T3?> As<T1, T2, T3>(this IVarTuple tuple)
		{
			tuple.OfSize(3);
			return (
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2)
			);
		}

		/// <summary>Returns a typed version of a tuple of size 4</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <param name="tuple">Tuple that must be of size 4</param>
		/// <returns>Equivalent tuple, with its elements converted to the specified types</returns>
		public static ValueTuple<T1?, T2?, T3?, T4?> As<T1, T2, T3, T4>(this IVarTuple tuple)
		{
			tuple.OfSize(4);
			return (
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3)
			);
		}

		/// <summary>Returns a typed version of a tuple of size 5</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <typeparam name="T5">Expected type of the fifth element</typeparam>
		/// <param name="tuple">Tuple that must be of size 5</param>
		/// <returns>Equivalent tuple, with its elements converted to the specified types</returns>
		public static ValueTuple<T1?, T2?, T3?, T4?, T5?> As<T1, T2, T3, T4, T5>(this IVarTuple tuple)
		{
			tuple.OfSize(5);
			return (
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3),
				tuple.Get<T5>(4)
			);
		}

		/// <summary>Returns a typed version of a tuple of size 5</summary>
		/// <typeparam name="T1">Expected type of the first element</typeparam>
		/// <typeparam name="T2">Expected type of the second element</typeparam>
		/// <typeparam name="T3">Expected type of the third element</typeparam>
		/// <typeparam name="T4">Expected type of the fourth element</typeparam>
		/// <typeparam name="T5">Expected type of the fifth element</typeparam>
		/// <typeparam name="T6">Expected type of the sixth element</typeparam>
		/// <param name="tuple">Tuple that must be of size 5</param>
		/// <returns>Equivalent tuple, with its elements converted to the specified types</returns>
		public static ValueTuple<T1?, T2?, T3?, T4?, T5?, T6?> As<T1, T2, T3, T4, T5, T6>(this IVarTuple tuple)
		{
			tuple.OfSize(6);
			return (
				tuple.Get<T1>(0),
				tuple.Get<T2>(1),
				tuple.Get<T3>(2),
				tuple.Get<T4>(3),
				tuple.Get<T5>(4),
				tuple.Get<T6>(5)
			);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 1</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1>(this IVarTuple tuple, Action<T1> lambda)
		{
			OfSize(tuple, 1);
			lambda(tuple.Get<T1>(0)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 2</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2>(this IVarTuple tuple, Action<T1, T2> lambda)
		{
			OfSize(tuple, 2);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 3</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3>(this IVarTuple tuple, Action<T1, T2, T3> lambda)
		{
			OfSize(tuple, 3);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 4</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3, T4>(this IVarTuple tuple, Action<T1, T2, T3, T4> lambda)
		{
			OfSize(tuple, 4);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 5</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3, T4, T5>(this IVarTuple tuple, Action<T1, T2, T3, T4, T5> lambda)
		{
			OfSize(tuple, 5);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 6</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3, T4, T5, T6>(this IVarTuple tuple, Action<T1, T2, T3, T4, T5, T6> lambda)
		{
			OfSize(tuple, 6);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 7</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3, T4, T5, T6, T7>(this IVarTuple tuple, Action<T1, T2, T3, T4, T5, T6, T7> lambda)
		{
			OfSize(tuple, 7);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!, tuple.Get<T7>(6)!);
		}

		/// <summary>Executes a lambda Action with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 8</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static void With<T1, T2, T3, T4, T5, T6, T7, T8>(this IVarTuple tuple, Action<T1, T2, T3, T4, T5, T6, T7, T8> lambda)
		{
			OfSize(tuple, 8);
			lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!, tuple.Get<T7>(6)!, tuple.Get<T8>(7)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 1</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, TResult>(this IVarTuple tuple, Func<T1, TResult> lambda)
		{
			return lambda(tuple.OfSize(1).Get<T1>(0)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 2</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, TResult>(this IVarTuple tuple, Func<T1, T2, TResult> lambda)
		{
			OfSize(tuple, 2);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 3</param>
		/// <param name="lambda">Action that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, TResult>(this IVarTuple tuple, Func<T1, T2, T3, TResult> lambda)
		{
			OfSize(tuple, 3);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 4</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, T4, TResult>(this IVarTuple tuple, Func<T1, T2, T3, T4, TResult> lambda)
		{
			OfSize(tuple, 4);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 5</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, T4, T5, TResult>(this IVarTuple tuple, Func<T1, T2, T3, T4, T5, TResult> lambda)
		{
			OfSize(tuple, 5);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 6</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, T4, T5, T6, TResult>(this IVarTuple tuple, Func<T1, T2, T3, T4, T5, T6, TResult> lambda)
		{
			OfSize(tuple, 6);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 7</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, T4, T5, T6, T7, TResult>(this IVarTuple tuple, Func<T1, T2, T3, T4, T5, T6, T7, TResult> lambda)
		{
			OfSize(tuple, 7);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!, tuple.Get<T7>(6)!);
		}

		/// <summary>Executes a lambda Function with the content of this tuple</summary>
		/// <param name="tuple">Tuple of size 8</param>
		/// <param name="lambda">Function that will be passed the content of this tuple as parameters</param>
		/// <returns>Result of calling <paramref name="lambda"/> with the items of this tuple</returns>
		/// <exception cref="InvalidOperationException">If <paramref name="tuple"/> has not the expected size</exception>
		public static TResult With<T1, T2, T3, T4, T5, T6, T7, T8, TResult>(this IVarTuple tuple, Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult> lambda)
		{
			OfSize(tuple, 8);
			return lambda(tuple.Get<T1>(0)!, tuple.Get<T2>(1)!, tuple.Get<T3>(2)!, tuple.Get<T4>(3)!, tuple.Get<T5>(4)!, tuple.Get<T6>(5)!, tuple.Get<T7>(6)!, tuple.Get<T8>(7)!);
		}

		#endregion

		#region Deconstruction

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1>(this IVarTuple value, out T1 item1)
		{
			item1 = value.OfSize(1).Get<T1>(0)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2>(this IVarTuple value, out T1 item1, out T2 item2)
		{
			value.OfSize(2);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3)
		{
			value.OfSize(3);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3, T4>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3, out T4 item4)
		{
			value.OfSize(4);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
			item4 = value.Get<T4>(3)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3, T4, T5>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5)
		{
			value.OfSize(5);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
			item4 = value.Get<T4>(3)!;
			item5 = value.Get<T5>(4)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3, T4, T5, T6>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6)
		{
			value.OfSize(6);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
			item4 = value.Get<T4>(3)!;
			item5 = value.Get<T5>(4)!;
			item6 = value.Get<T6>(5)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7)
		{
			value.OfSize(7);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
			item4 = value.Get<T4>(3)!;
			item5 = value.Get<T5>(4)!;
			item6 = value.Get<T6>(5)!;
			item7 = value.Get<T7>(6)!;
		}

		/// <summary>Deconstructs this tuple into its individual elements</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void Deconstruct<T1, T2, T3, T4, T5, T6, T7, T8>(this IVarTuple value, out T1 item1, out T2 item2, out T3 item3, out T4 item4, out T5 item5, out T6 item6, out T7 item7, out T8 item8)
		{
			value.OfSize(8);
			item1 = value.Get<T1>(0)!;
			item2 = value.Get<T2>(1)!;
			item3 = value.Get<T3>(2)!;
			item4 = value.Get<T4>(3)!;
			item5 = value.Get<T5>(4)!;
			item6 = value.Get<T6>(5)!;
			item7 = value.Get<T7>(6)!;
			item8 = value.Get<T8>(7)!;
		}

		#endregion

		#region ValueTuple

		/// <summary>Returns the equivalent <see cref="STuple"/></summary>
		[Pure]
		public static STuple ToSTuple(this ValueTuple _)
		{
			return default;
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1> ToSTuple<T1>(this ValueTuple<T1> tuple)
		{
			return new(tuple.Item1);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2> ToSTuple<T1, T2>(this (T1, T2) tuple)
		{
			return new(tuple.Item1, tuple.Item2);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3> ToSTuple<T1, T2, T3>(this (T1, T2, T3) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3,T4}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4> ToSTuple<T1, T2, T3, T4>(this (T1, T2, T3, T4) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3,T4,T5}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5> ToSTuple<T1, T2, T3, T4, T5>(this (T1, T2, T3, T4, T5) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6> ToSTuple<T1, T2, T3, T4, T5, T6>(this (T1, T2, T3, T4, T5, T6) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7> ToSTuple<T1, T2, T3, T4, T5, T6, T7>(this (T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7);
		}

		/// <summary>Returns the equivalent <see cref="STuple{T1,T2,T3,T4,T5,T6,T7,T8}"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1, T2, T3, T4, T5, T6, T7, T8> ToSTuple<T1, T2, T3, T4, T5, T6, T7, T8>(this (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return new(tuple.Item1, tuple.Item2, tuple.Item3, tuple.Item4, tuple.Item5, tuple.Item6, tuple.Item7, tuple.Item8);
		}

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2> Concat<T1, T2>(this ValueTuple<T1> t1, ValueTuple<T2> t2) => (t1.Item1, t2.Item1);

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2, T3> Concat<T1, T2, T3>(this ValueTuple<T1> t1, ValueTuple<T2, T3> t2) => (t1.Item1, t2.Item1, t2.Item2);

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2, T3> Concat<T1, T2, T3>(this ValueTuple<T1, T2> t1, ValueTuple<T3> t2) => (t1.Item1, t1.Item2, t2.Item1);

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2, T3, T4> Concat<T1, T2, T3, T4>(this ValueTuple<T1> t1, ValueTuple<T2, T3, T4> t2) => (t1.Item1, t2.Item1, t2.Item2, t2.Item3);

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2, T3, T4> Concat<T1, T2, T3, T4>(this ValueTuple<T1, T2> t1, ValueTuple<T3, T4> t2) => (t1.Item1, t1.Item2, t2.Item1, t2.Item2);

		/// <summary>Returns a new tuple with the concatenation of this tuple and another tuple</summary>
		[Pure]
		public static ValueTuple<T1, T2, T3, T4> Concat<T1, T2, T3, T4>(this ValueTuple<T1, T2, T3> t1, ValueTuple<T4> t2) => (t1.Item1, t1.Item2, t1.Item3, t2.Item1);

		#endregion

	}

}
