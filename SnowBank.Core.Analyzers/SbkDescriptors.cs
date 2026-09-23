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
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;

	/// <summary>Descriptors of the SnowBank.Core rules.</summary>
	public static class SbkDescriptors
	{

		/// <summary>SBK1003: null test on a JsonValue expression that is never a null reference.</summary>
		public static readonly DiagnosticDescriptor JsonValueNullTest = RuleFactory.Create(
			"SBK1003",
			"Null test on a JSON value",
			"A JsonValue is never a null reference: a missing member reads as JsonNull.Missing. Test it with IsNullOrMissing().",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>Every SnowBank.Core descriptor, in ID order.</summary>
		public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
			JsonValueNullTest);

	}
}
