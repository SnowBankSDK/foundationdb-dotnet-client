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

namespace FoundationDB.Analyzers
{
	using System.Collections.Immutable;
	using Microsoft.CodeAnalysis;
	using SnowBank.Analyzers;

	/// <summary>Descriptors of the FoundationDB.Client rules.</summary>
	public static class FdbDescriptors
	{

		/// <summary>FDB0001: IFdbDatabase requested from dependency injection, where nothing registers it.</summary>
		public static readonly DiagnosticDescriptor DatabaseInjection = RuleFactory.Create(
			"FDB0001",
			"IFdbDatabase requested from dependency injection",
			"IFdbDatabase is not registered in dependency injection. Inject IFdbDatabaseProvider, which has ReadAsync, WriteAsync, ReadWriteAsync and Root.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Error,
			WellKnownDiagnosticTags.CompilationEnd);

		/// <summary>FDB0002: Watch called with the cancellation token of its own transaction, which the method rejects.</summary>
		public static readonly DiagnosticDescriptor WatchTransactionToken = RuleFactory.Create(
			"FDB0002",
			"Watch created with the transaction's own token",
			"A watch outlives its transaction, so it cannot use tr.Cancellation. Pass the token of the code that awaits the watch.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>FDB0003: FdbWatch awaited inside the retry-loop handler that created it.</summary>
		public static readonly DiagnosticDescriptor WatchAwaitedInHandler = RuleFactory.Create(
			"FDB0003",
			"Watch awaited inside the handler that created it",
			"A watch fires only after its transaction commits, and this await blocks the commit. Return the FdbWatch from the handler and await it after WriteAsync returns.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>Every FoundationDB.Client descriptor, in ID order.</summary>
		public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
			DatabaseInjection,
			WatchTransactionToken,
			WatchAwaitedInHandler);

	}
}
