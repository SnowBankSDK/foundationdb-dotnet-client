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

namespace SnowBank.Diagnostics.Contracts
{
	using AssertionMethodAttribute = JetBrains.Annotations.AssertionMethodAttribute;
	using AssertionConditionAttribute = JetBrains.Annotations.AssertionConditionAttribute;
	using AssertionConditionType = JetBrains.Annotations.AssertionConditionType;

	/// <summary>Helper class present only in Paranoid mode, for checking pre-requisites, invariants, assertions, ...</summary>
	/// <remarks>The methods of this class are only compiled if the PARANOID_ANDROID flag is defined</remarks>
	[DebuggerNonUserCode]
	public static class Paranoid
	{
		// https://www.youtube.com/watch?v=rF8khJ7P4Wg

		/// <summary>Returns <see langword="true"/> at runtime if Paranoid checks are enforced</summary>
		/// <remarks>Things may get a _lot_ slower!</remarks>
		public static bool IsParanoid
		{
			get
			{
#if PARANOID_ANDROID
				return true;
#else
				return false;
#endif
			}
		}

		/// <summary>[PARANOID ANDROID] Test if a pre-condition is true, at the start of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c> or if running a Release build. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached).</remarks>
		[Conditional("PARANOID_ANDROID")]
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[AssertionMethod]
		public static void Requires(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
#if PARANOID_ANDROID
			if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Precondition, userMessage, conditionText);
#endif
		}

		/// <summary>[PARANOID ANDROID] Test if a condition is true, inside the body of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c> or if running a Release build. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached).</remarks>
		[Conditional("DEBUG")]
		[AssertionMethod]
		[StackTraceHidden]
		public static void Assert(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null)
		{
#if PARANOID_ANDROID
				if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Assert, userMessage, conditionText);
#endif
		}

		/// <summary>[PARANOID ANDROID] Test if a post-condition is true, at the end of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c> or if running a Release build. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached).</remarks>
		[Conditional("DEBUG")]
		[AssertionMethod]
		[StackTraceHidden]
		public static void Ensures(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
#if PARANOID_ANDROID
			if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Postcondition, userMessage, conditionText);
#endif
		}

		/// <summary>[DEBUG ONLY] Test that an invariant is met.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c> or if running a Release build. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached).</remarks>
		[Conditional("DEBUG")]
		[AssertionMethod]
		[StackTraceHidden]
		public static void Invariant(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
#if PARANOID_ANDROID
			if (!condition) throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Invariant, userMessage, conditionText);
#endif
		}

	}

}
