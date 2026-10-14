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

	/// <summary>Runs a test before the other tests of its fixture, as a canary whose failure explains the failures that follow.</summary>
	/// <remarks>
	/// <para>A canary checks an assumption that every other test of the fixture relies on (a hash algorithm, an overload resolution, the most basic operation of a store).
	/// Running it first puts the root cause at the top of the results, instead of after the dozens of failures it causes.</para>
	/// <para>The other tests still run and report their own results when the canary fails: this orders the tests, it does not make them depend on the canary.</para>
	/// <para>A canary runs before any test marked with <see cref="RunOrderAttribute"/>, and the tests without either attribute run last.
	/// It behaves the same under NUnit 4 and NUnit 5.</para>
	/// <para>The order only holds for tests that run one after the other: in a fixture marked <c>[Parallelizable(ParallelScope.Children)]</c> or <c>ParallelScope.All</c>, the tests overlap and finish in any order.</para>
	/// </remarks>
	/// <example><code lang="c#">
	/// [Test, Canary]
	/// public void Test_Hash_Algorithm_Did_Not_Change() { /*...*/ }
	/// </code></example>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
	public sealed class CanaryAttribute : NUnitAttribute, IApplyToTest
	{

		void IApplyToTest.ApplyToTest(NUnit.Framework.Internal.Test test)
		{
			RunOrderAttribute.Apply(test, int.MinValue);
		}

	}

}
