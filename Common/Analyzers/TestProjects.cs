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

namespace SnowBank.Analyzers
{
	using System;
	using System.Collections.Immutable;
	using System.Linq;
	using Microsoft.CodeAnalysis;

	/// <summary>Recognizes the compilation of a test project, where the performance rules stay quiet.</summary>
	/// <remarks>A test feeds the plain factories on purpose, to exercise them, and uses the BCL encoders as oracles: the cost of an intermediate buffer is not what a test measures. The correctness rules still run.</remarks>
	internal static class TestProjects
	{

		/// <summary>Assemblies of the test frameworks: a compilation that references one of them is a test project.</summary>
		private static readonly ImmutableHashSet<string> FrameworkAssemblies = ImmutableHashSet.Create(
			StringComparer.OrdinalIgnoreCase,
			"nunit.framework",
			"xunit.core",
			"xunit.v3.core",
			"Microsoft.VisualStudio.TestPlatform.TestFramework",
			"MSTest.TestFramework",
			"TUnit.Core"
		);

		/// <summary>The compilation references NUnit, xUnit, MSTest or TUnit.</summary>
		public static bool IsTestProject(Compilation compilation) => compilation.ReferencedAssemblyNames.Any(a => FrameworkAssemblies.Contains(a.Name));

	}
}
