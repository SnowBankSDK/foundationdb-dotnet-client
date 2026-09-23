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

		/// <summary>SBK0001: Slice.FromStringAscii on a literal with a character above 0xFF, which the method rejects at run time.</summary>
		public static readonly DiagnosticDescriptor AsciiLiteralNotEncodable = RuleFactory.Create(
			"SBK0001",
			"Slice.FromStringAscii on text that cannot be encoded",
			"'{0}' contains characters above 0xFF, which this method rejects at run time. Use Slice.FromStringUtf8.",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>SBK0002: mutation of a local assigned once from a read-only JSON factory, ToReadOnly(), or Freeze().</summary>
		public static readonly DiagnosticDescriptor ReadOnlyJsonMutation = RuleFactory.Create(
			"SBK0002",
			"Mutation of a read-only JSON value",
			"'{0}' is read-only, and this call throws InvalidOperationException. Call ToMutable() to get an editable copy.",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>SBK0100: Slice factory removed in version 7, reported next to the compiler error with its replacement.</summary>
		public static readonly DiagnosticDescriptor RemovedSliceApi = RuleFactory.Create(
			"SBK0100",
			"Removed API",
			"'{0}' was removed in version 7. {1}",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Error);

		/// <summary>SBK1001: a view into a pooled buffer (Data, Span, ToSlice(), ...) that leaves the using scope of its owner.</summary>
		public static readonly DiagnosticDescriptor PooledBufferEscape = RuleFactory.Create(
			"SBK1001",
			"Pooled buffer escaping its using scope",
			"'{0}' points into a pooled buffer that returns to the pool at the end of this using block. Copy it with ToArray(), or return the owner with ToSliceOwner().",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>SBK1002: Slice.FromString or FromStringUtf8 on a literal whose first character is a binary prefix (0x80 to 0xFF).</summary>
		public static readonly DiagnosticDescriptor BinaryPrefixAsUtf8 = RuleFactory.Create(
			"SBK1002",
			"Binary prefix encoded as UTF-8 text",
			"'{0}' starts with a binary prefix, and UTF-8 encodes that character as two bytes. Use Slice.FromByteString to write one byte per character.",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>SBK1003: null test on a JsonValue expression that is never a null reference.</summary>
		public static readonly DiagnosticDescriptor JsonValueNullTest = RuleFactory.Create(
			"SBK1003",
			"Null test on a JSON value",
			"A JsonValue is never a null reference: a missing member reads as JsonNull.Missing. Test it with IsNullOrMissing().",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>SBK1004: Contract.Requires, Assert, or Debug.Requires used to check an argument of a public member.</summary>
		public static readonly DiagnosticDescriptor ContractOnPublicArgument = RuleFactory.Create(
			"SBK1004",
			"Contract.Requires used to validate a public argument",
			"Contract.Requires reports a bug in this code, not a bad argument from the caller. Use {0}, which throws the matching ArgumentException.",
			AnalyzerCategories.SnowBankCorrectness,
			DiagnosticSeverity.Warning);

		/// <summary>SBK2001: a pooled writer, allocator, or owner local that is never disposed and never leaves the method.</summary>
		public static readonly DiagnosticDescriptor PooledBufferNeverReturned = RuleFactory.Create(
			"SBK2001",
			"Pooled buffer never returned",
			"'{0}' rents a buffer from a pool and is never disposed, so the buffer never returns. Declare it with 'using'.",
			AnalyzerCategories.SnowBankPerformance,
			DiagnosticSeverity.Warning);

		/// <summary>SBK2002: a Slice decoded or encoded through a temporary byte array, where a Slice method does the same without the copy.</summary>
		public static readonly DiagnosticDescriptor SliceRoundTrip = RuleFactory.Create(
			"SBK2002",
			"Slice to byte[] round trip",
			"'{0}' copies the bytes into a temporary array. Call {1} instead.",
			AnalyzerCategories.SnowBankPerformance,
			DiagnosticSeverity.Warning);

		/// <summary>Every SnowBank.Core descriptor, in ID order.</summary>
		public static ImmutableArray<DiagnosticDescriptor> All { get; } = ImmutableArray.Create(
			AsciiLiteralNotEncodable,
			ReadOnlyJsonMutation,
			RemovedSliceApi,
			PooledBufferEscape,
			BinaryPrefixAsUtf8,
			JsonValueNullTest,
			ContractOnPublicArgument,
			PooledBufferNeverReturned,
			SliceRoundTrip);

	}
}
