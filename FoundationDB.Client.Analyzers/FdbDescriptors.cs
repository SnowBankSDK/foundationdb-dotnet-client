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

		/// <summary>FDB0100: member removed in version 7, reported next to the compiler error with its replacement.</summary>
		public static readonly DiagnosticDescriptor RemovedApi = RuleFactory.Create(
			"FDB0100",
			"Removed API",
			"'{0}' was removed in version 7. {1}",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>FDB1001: a field, auto-property, or singleton registration keeps a subspace or a layer state across transactions.</summary>
		public static readonly DiagnosticDescriptor SubspaceStoredAcrossTransactions = RuleFactory.Create(
			"FDB1001",
			"Subspace or layer state stored across transactions",
			"A directory can move, so '{0}' may point at a stale prefix in a later transaction. Store the location (db.Root[...]) and call Resolve(tr) inside each transaction.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning,
			WellKnownDiagnosticTags.CompilationEnd);

		/// <summary>FDB1002: a ReadAsync or ReadWriteAsync handler returns a subspace or a layer state.</summary>
		public static readonly DiagnosticDescriptor SubspaceReturnedFromHandler = RuleFactory.Create(
			"FDB1002",
			"Subspace returned out of a retry-loop handler",
			"The subspace returned by this handler belongs to its transaction and can be stale in the next one. Return the location, and call Resolve(tr) inside each transaction.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning,
			WellKnownDiagnosticTags.CompilationEnd);

		/// <summary>FDB1003: AtomicMax or AtomicMin on a value whose encoding compares byte by byte, not as a little-endian integer.</summary>
		public static readonly DiagnosticDescriptor ByteOrderAtomic = RuleFactory.Create(
			"FDB1003",
			"Byte-order atomic on a value that is not a little-endian number",
			"AtomicMax compares little-endian integers, and '{0}' is not one, so the stored value can be wrong. Use tr.Atomic(key, value, FdbMutationType.ByteMax) for a byte-order comparison (API level 520 or higher).",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>FDB1004: an id or clock read inside a retry-loop handler, whose value changes on every attempt.</summary>
		public static readonly DiagnosticDescriptor RetryLoopValue = RuleFactory.Create(
			"FDB1004",
			"Id or clock read inside a retry-loop handler",
			"The handler runs again on every retry, so '{0}' produces a different value on each attempt. Compute it before the call and capture it.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>FDB1005: a GetAsync result tested against Slice.Empty or with IsEmpty, which a missing key (Slice.Nil) never satisfies.</summary>
		public static readonly DiagnosticDescriptor MissingKeyEmptyTest = RuleFactory.Create(
			"FDB1005",
			"Missing key tested with Slice.Empty",
			"A missing key reads as Slice.Nil, which is not equal to Slice.Empty. Test it with IsNull, or IsNullOrEmpty to also accept an empty value.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>FDB1006: Set called with a key that holds a version stamp placeholder, which only SetVersionStampedKey fills in.</summary>
		public static readonly DiagnosticDescriptor VersionStampedKeySet = RuleFactory.Create(
			"FDB1006",
			"Version-stamped key written with Set",
			"The key contains a version stamp placeholder, and Set does not fill in the stamp. Use tr.SetVersionStampedKey(key, value) so the database fills it in at commit.",
			AnalyzerCategories.FdbCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>FDB2001: a transaction write whose value is built by a Core factory into a buffer that the transaction copies again.</summary>
		public static readonly DiagnosticDescriptor IntermediateValueBuffer = RuleFactory.Create(
			"FDB2001",
			"Value built with a Core factory in a transaction write",
			"'{0}' allocates an intermediate buffer that the transaction copies again. Pass {1} instead.",
			AnalyzerCategories.FdbPerformance,
			DiagnosticSeverity.Warning);

		/// <summary>FDB2002: a typed key serialized with ToSlice() before a transaction call that encodes typed keys itself.</summary>
		public static readonly DiagnosticDescriptor EagerKeySlice = RuleFactory.Create(
			"FDB2002",
			"Key serialized with .ToSlice() before a transaction call",
			"The transaction encodes a typed key into its own buffer. Remove .ToSlice() and pass '{0}' directly.",
			AnalyzerCategories.FdbPerformance,
			DiagnosticSeverity.Warning);

		/// <summary>FDB2003: a database awaited from the provider only to run a retry loop that the provider runs itself.</summary>
		public static readonly DiagnosticDescriptor ProviderUnwrapped = RuleFactory.Create(
			"FDB2003",
			"Database obtained only to run a retry loop",
			"IFdbDatabaseProvider has ReadAsync, WriteAsync, ReadWriteAsync and Root. Call them on the provider and drop the GetDatabase call.",
			AnalyzerCategories.FdbPerformance,
			DiagnosticSeverity.Warning);

		/// <summary>Every FoundationDB.Client descriptor, in ID order.</summary>
		public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
			DatabaseInjection,
			WatchTransactionToken,
			WatchAwaitedInHandler,
			RemovedApi,
			SubspaceStoredAcrossTransactions,
			SubspaceReturnedFromHandler,
			ByteOrderAtomic,
			RetryLoopValue,
			MissingKeyEmptyTest,
			VersionStampedKeySet,
			IntermediateValueBuffer,
			EagerKeySlice,
			ProviderUnwrapped);

	}
}
