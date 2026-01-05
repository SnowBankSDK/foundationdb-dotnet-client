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

namespace SnowBank.Runtime.Converters
{
	using System.Collections;

	/// <summary>Helper that compares instances for "similarity"</summary>
	/// <remarks>
	/// <para>This comparer SHOULD NOT be used in a Dictionary, because it violates on of the conditions: Some pairs of objects could be considered equal, but have different hashcode!</para>
	/// </remarks>
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class SimilarValueComparer : IEqualityComparer<object?>, IEqualityComparer, IComparer<object?>, IComparer
	{

		/// <summary>Comparer that uses relaxed rules when comparing objects for similarity.</summary>
		/// <remarks>
		/// <para>This comparer adds more type conversions to the <seealso cref="Default"/> base.</para>
		/// <para>For example, in relaxed mode, both the string <c>"123"</c> and double <c>123d</c> are considered equal to integer <c>123</c>, and <c>false</c>/<c>true</c> are equal to <c>0</c>/<c>1</c>.</para>
		/// </remarks>
		/// <seealso cref="Default"/>
		public static readonly SimilarValueComparer Relaxed = new(relaxed: true);

		/// <summary>Comparer that uses stricter rules when comparison objects for similarity.</summary>
		/// <remarks>
		/// <para>In strict mode, double <c>123d</c> is considered equal to the integer <c>123</c>, but the string <c>"123"</c> is not.</para>
		/// <para>All types of integers will be compared together, for example <c>123</c> == <c>123L</c> == <c>123UL</c> == <c>123.0</c> == <c>123m</c></para>
		/// <para>All UUIDs of similar size will also be compared together, for example <see cref="Guid"/> and <see cref="Uuid128"/>.</para>
		/// <para>When ordering values of different types that are not similar, they will be ordered according to their types, with the following ranking (from smaller to greater):
		/// <c>null</c>, <c>bytes</c>, <c>strings</c>, <c>tuples</c>, <c>integers</c>, <c>decimals</c>, <c>booleans</c>, <c>guids</c>, <c>VersionStamps</c></para>
		/// </remarks>
		/// <seealso cref="Relaxed"/>
		public static readonly SimilarValueComparer Default = new(relaxed: false);

		/// <summary>Gets a value that specifies whether the relaxed rules are used (<c>true</c>) or not (<c>false</c>)</summary>
		public bool UsesRelaxedRules { get; }

		private SimilarValueComparer(bool relaxed)
		{
			this.UsesRelaxedRules = relaxed;
		}

		/// <inheritdoc cref="IEqualityComparer.Equals(object?,object?)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public new bool Equals(object? x, object? y) => this.UsesRelaxedRules ? ComparisonHelper.AreSimilarRelaxed(x, y) : ComparisonHelper.AreSimilarStrict(x, y);

		/// <inheritdoc cref="IEqualityComparer.GetHashCode(object)" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetHashCode(object obj) => StructuralComparisons.StructuralEqualityComparer.GetHashCode(obj);

		/// <inheritdoc cref="IComparer.Compare" />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int Compare(object? x, object? y) => this.UsesRelaxedRules ? ComparisonHelper.CompareSimilarRelaxed(x, y) : ComparisonHelper.CompareSimilarStrict(x, y);

	}

}
