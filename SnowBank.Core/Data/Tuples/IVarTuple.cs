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

namespace SnowBank.Data.Tuples
{
	using System.Collections;
	using System.ComponentModel;

	/// <summary>Represents a Tuple of variable length and elements of different types</summary>
	[ImmutableObject(true)]
	[JetBrains.Annotations.CannotApplyEqualityOperator]
	[PublicAPI]
#if !NETSTANDARD2_0
	public interface IVarTuple : IEquatable<IVarTuple>, IComparable<IVarTuple>, IReadOnlyList<object?>, IStructuralEquatable, IStructuralComparable, ITuple, IFormattable
#else
	// ITuple is not visible to netstandard2.0
	public interface IVarTuple : IEquatable<IVarTuple>, IComparable<IVarTuple>, IReadOnlyList<object?>, IStructuralEquatable, IStructuralComparable, IFormattable
#endif
	{
		// Tuples should, by default, behave as closely to Python's tuples as possible. See http://docs.python.org/2/tutorial/datastructures.html#tuples-and-sequences

		// Implementation notes:
		// - Tuples are equivalent to an immutable list of "objects"
		// - Unless specified otherwise, end offsets are usually EXCLUDED.
		// - Appending to a tuple returns a new tuple (does not mutate the previous)
		// - Getting the substring of a tuple return a new tuple that tries to reuse the objects of the parent tuple
		// - There are no guarantees that two different tuples containing the "same" values return the same HashCode, meaning that it should not be used as keys in a Dictionary

		// Performance notes:
		// - Accessing the Count and Last item should be fast, if possible in O(1)
		// - Appending should also be fast, if possible O(1)
		// - Getting the substring of a tuple should as fast as possible, if possible O(1). For list-based tuples, it should return a view of the list (offset/count) and avoid copying the list
		// - If an operation returns an empty tuple, then it should return the STuple.Empty singleton instance
		// - If an operation does not change the tuple (like Append(STuple.Empty), or tuple.Substring(0)), then the tuple should return itself
		// - If the same tuple will be packed frequently, it should be memoized (converted into a MemoizedTuple)

		//TODO: BUGBUG: the old name (ITuple) collides with System.Runtime.CompilerServices.ITuple which is made public in .NET 4.7.1+
		// This interfaces defines an indexer, and the property "Length", but we are using "Count" which comes from IReadOnlyList<object> ...

		/// <summary>Returns the element at the specified index</summary>
		/// <param name="index">Index of the element to return.</param>
		//TODO: REVIEW: consider dropping the negative indexing? We have Index now for this use-case!
		//note: we need to repeat this property getter, otherwise "((IVarTuple) x)[123]" will call this[Index] instead!
		new object? this[int index] { get; }

		/// <summary>Returns a section of the tuple</summary>
		/// <param name="fromIncluded">Starting offset of the sub-tuple to return, or null to select from the start.</param>
		/// <param name="toExcluded">Ending offset (excluded) of the sub-tuple to return or null to select until the end.</param>
		/// <returns>
		/// Tuple that include all items in the current tuple whose offset are greater than or equal to <paramref name="fromIncluded"/> and strictly less than <paramref name="toExcluded"/>.
		/// <para>The tuple may be smaller than expected if the range is larger than the parent tuple.</para>
		/// <para>If the range does not intersect with the tuple, the <see cref="STuple.Empty">empty tuple</see> will be returned.</para>
		/// </returns>
		//TODO: REVIEW: consider marking this overload as obsolete or even removing it, since we now have Range for this use case?
		[EditorBrowsable(EditorBrowsableState.Never)]
		IVarTuple this[int? fromIncluded, int? toExcluded] { [Pure] get; }

		/// <summary>Returns the element at the specified index</summary>
		object? this[Index index] { get; }

		/// <summary>Returns a section of the tuple</summary>
		/// <param name="range">Range of sub-tuple to return</param>
		/// <returns>Tuple that include all items in the current tuple whose index are in the specified <paramref name="range"/>. If the range does not intersect with the tuple, the Empty tuple will be returned.</returns>
		IVarTuple this[Range range] { get;}

		/// <summary>Returns the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the tuple</exception>
		/// <remarks><code lang="c#">
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;string&gt;(0) => "Hello"</c></para>
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;int&gt;(2) => 123</c></para>
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;string&gt;(2) => "123"</c></para>
		/// </code></remarks>
		//REVIEW: consider dropping the negative indexing? We have Index now for this use-case!
		[Pure]
		TItem? Get<TItem>(int index);

		/// <summary>Returns the typed value of an item of the tuple, given its position</summary>
		/// <typeparam name="TItem">Expected type of the item</typeparam>
		/// <param name="index">Position of the item</param>
		/// <returns>Value of the item at position <paramref name="index"/>, adapted into type <typeparamref name="TItem"/>.</returns>
		/// <exception cref="System.IndexOutOfRangeException">If <paramref name="index"/> is outside the bounds of the tuple</exception>
		/// <remarks><code lang="c#">
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;string&gt;(0) => "Hello"</c></para>
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;int&gt;(^1) => 123</c></para>
		/// <para><c>STuple.Create("Hello", "World", 123,).Get&lt;string&gt;(^1) => "123"</c></para>
		/// </code></remarks>
		[Pure]
		TItem? Get<TItem>(Index index);

		/// <summary>Returns the type value of the first item of the tuple</summary>
		TItem? GetFirst<TItem>();

		/// <summary>Returns the type value of the last item of the tuple</summary>
		TItem? GetLast<TItem>();

		/// <summary>Returns a new Tuple by that contains the element of this tuple, with an extra item at the end</summary>
		/// <typeparam name="TItem">Type of the new item</typeparam>
		/// <param name="value">Value of the item that will be appended at the end</param>
		/// <returns>New tuple with the extra item</returns>
		/// <remarks>
		/// <para>If <typeparamref name="TItem"/> is an <see cref="IVarTuple"/>, then it will be appended as a single element. If you need to append the *items* of a tuple, you must call <see cref="IVarTuple.Concat"/></para>
		/// <para>Example: <code><c>STuple.Create("Hello").Append("World")</c> => <c>("Hello", "World")</c></code></para>
		/// </remarks>
		[Pure]
		IVarTuple Append<TItem>(TItem value);

		/// <summary>Returns a new Tuple by appending the items of another tuple at the end of this tuple</summary>
		/// <param name="tuple">Tuple whose items must be appended at the end of the current tuple</param>
		/// <returns>New tuple, or the same instance if <paramref name="tuple"/> is empty.</returns>
		[Pure]
		IVarTuple Concat(IVarTuple tuple);

		/// <summary>Copies all items of the tuple into an array at a specific location.</summary>
		/// <param name="array">The destination array, which must be large enough to contain all the items.</param>
		/// <param name="offset">The offset at which to start copying items.</param>
		/// <example><code>
		/// var tmp = new object[3];
		/// ("Hello", "World", 123).CopyTo(tmp, 0);
		/// </code></example>
		void CopyTo(object?[] array, int offset);

		/// <summary>Computes a stable hash code for the item at the specific location</summary>
		int GetItemHashCode(int index, IEqualityComparer comparer);

	}

}
