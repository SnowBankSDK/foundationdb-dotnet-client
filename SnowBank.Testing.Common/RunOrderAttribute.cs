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
	using NUnit.Framework.Interfaces;
	using NUnit.Framework.Internal;

	/// <summary>Sets the rank of a test in the run order of its fixture, so that the basic tests run, and fail, before the advanced ones.</summary>
	/// <remarks>
	/// <para>Tests run by ascending rank, after the <see cref="CanaryAttribute">canaries</see> and before the tests without a rank.</para>
	/// <para>Each test must still set up its own state: this orders the tests, it does not make a test depend on the outcome or the side effects of another.</para>
	/// <para>It behaves the same under NUnit 4 and NUnit 5, and replaces NUnit's <c>[Order]</c> attribute, which NUnit 5 marks as obsolete.</para>
	/// <para>The order only holds for tests that run one after the other: in a fixture marked <c>[Parallelizable(ParallelScope.Children)]</c> or <c>ParallelScope.All</c>, the tests overlap and finish in any order.</para>
	/// </remarks>
	/// <example><code lang="c#">
	/// [Test, RunOrder(10)]
	/// public async Task Test_Can_Read_And_Write() { /*...*/ }
	///
	/// [Test, RunOrder(20)]
	/// public async Task Test_Can_Read_Your_Writes() { /*...*/ }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class RunOrderAttribute : NUnitAttribute, IApplyToTest
	{

		/// <summary>Sets the rank of a test in the run order of its fixture</summary>
		/// <param name="order">Rank of the test: lower ranks run first</param>
		public RunOrderAttribute(int order)
		{
			this.Order = order;
		}

		/// <summary>Rank of the test in the run order of its fixture</summary>
		public int Order { get; }

		void IApplyToTest.ApplyToTest(NUnit.Framework.Internal.Test test)
		{
			Apply(test, this.Order);
		}

		internal static void Apply(NUnit.Framework.Internal.Test test, int order)
		{
			// NUnit sorts the children of a fixture on this property, which is all that its own [Order] attribute sets
			if (!test.Properties.ContainsKey(PropertyNames.Order))
			{
				test.Properties.Set(PropertyNames.Order, order);
			}
		}

	}

}
