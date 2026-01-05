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

	public static partial class Contract
	{

		// ReSharper disable MemberHidesStaticFromOuterClass

		/// <summary>Contracts that are only evaluated in Debug builds</summary>
		[PublicAPI]
		[DebuggerNonUserCode]
		[ExcludeFromCodeCoverage]
		public static class Debug
		{

			// contains most of the same contracts as the main class, but only for Debug builds.
			// ie: Contract.NotNull(...) will run in both Debug and Release builds, while Contract.Debug.NotNull(...) will NOT be evaluated in Release builds

			#region DEBUG checks...

			/// <summary>[DEBUG ONLY] Test if a pre-condition is true, at the start of a method.</summary>
			/// <param name="condition">Condition that should never be false</param>
			/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
			/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
			/// <remarks>No-op if <paramref name="condition"/> is <c>true</c> or if running a Release build. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached).</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			[StackTraceHidden]
			public static void Requires(
				[AssertionCondition(AssertionConditionType.IS_TRUE)]
				[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
				bool condition,
				string? userMessage = null,
				[CallerArgumentExpression("condition")] string? conditionText = null
			)
			{
				if (!condition)
				{
					throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Precondition, userMessage, conditionText);
				}
			}

			/// <summary>[DEBUG ONLY] Test if a condition is true, inside the body of a method.</summary>
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
				[CallerArgumentExpression("condition")] string? conditionText = null
			)
			{
				if (!condition)
				{
					throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Assert, userMessage, conditionText);
				}
			}

			/// <summary>[DEBUG ONLY] Test if a post-condition is true, at the end of a method.</summary>
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
				if (!condition)
				{
					throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Postcondition, userMessage, conditionText);
				}
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
				if (!condition)
				{
					throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Invariant, userMessage, conditionText);
				}
			}

			/// <summary>[DEBUG ONLY] Unconditionally trigger an assertion fault</summary>
			/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
			/// <param name="exception">Optional exception linked to the issue</param>
			/// <remarks>Throws a <see cref="ContractException"/>, after attempting to breakpoint (if a debugger is attached)</remarks>
			[Conditional("DEBUG")]
			[AssertionMethod]
			[System.Diagnostics.CodeAnalysis.DoesNotReturn]
			[StackTraceHidden]
			public static void Fail(string? userMessage, Exception? exception = null)
			{
				throw RaiseContractFailure(System.Diagnostics.Contracts.ContractFailureKind.Invariant, userMessage, null, exception);
			}

			#endregion

		}

		// ReSharper restore MemberHidesStaticFromOuterClass

	}

}
