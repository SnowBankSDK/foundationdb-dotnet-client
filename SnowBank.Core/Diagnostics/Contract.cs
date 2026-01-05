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
	using System.Reflection;
	using SDC = System.Diagnostics.Contracts;
	using AssertionMethodAttribute = JetBrains.Annotations.AssertionMethodAttribute;
	using AssertionConditionAttribute = JetBrains.Annotations.AssertionConditionAttribute;
	using AssertionConditionType = JetBrains.Annotations.AssertionConditionType;

	internal static class ContractMessages
	{

		public const string ValueCannotBeNull = "Value cannot be null.";
		public const string StringCannotBeEmpty = "String cannot be empty.";
		public const string StringCannotBeWhiteSpaces = "String cannot contain only whitespaces.";
		public const string CollectionCannotBeEmpty = "Collection cannot be empty.";
		public const string BufferCannotBeNull = "Buffer cannot be null.";
		public const string BufferCannotBeEmpty = "Buffer cannot be empty.";
		public const string PositiveNumberRequired = "Positive number required.";
		public const string PowerOfTwoRequired = "Power of two number required.";
		public const string AboveZeroNumberRequired = "Non-Zero Positive number required.";
		public const string ValueIsTooSmall = "The specified value is too small.";
		public const string ValueIsTooBig = "The specified value is too big.";
		public const string ValueIsForbidden = "The specified value is not allowed.";
		public const string ValueIsExpected = "The specified value is not the expected value.";
		public const string ValueMustBeBetween = "The specified value was outside the specified range.";
		public const string ValueMustBeMultiple = "The specified value must be a multiple of another value.";
		public const string NonNegativeNumberRequired = "Non-negative number required.";
		public const string OffsetMustBeWithinBuffer = "Offset and length were out of bounds for the buffer or count is greater than the number of elements from index to the end of the buffer.";

		public const string ConditionNotNull = "{0} != null";
		public const string ConditionNotEmptyLength = "{0}.Length > 0";
		public const string ConditionNotWhiteSpace = "{0}.All(c => !char.IsWhiteSpace(c))";
		public const string ConditionNotEmptyCount = "{0}.Count > 0";
		public const string ConditionArgPositive = "{0} >= 0";
		public const string ConditionArgNotEqualTo = "{0} != x";
		public const string ConditionArgEqualTo = "{0} == x";
		public const string ConditionArgMultiple = "{0} % x == 0";
		public const string ConditionArgBetween = "min <= {0} <= max";
		public const string ConditionArgBufferOverflow = "(buffer.Length - offset) < count";
	}

	/// <summary>Helper type to check for pre-requisites, invariants, assertions, ...</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	[ExcludeFromCodeCoverage]
	public static partial class Contract
	{

		private static readonly (ConstructorInfo? One, ConstructorInfo? Two) s_constructorNUnitException = GetAssertionExceptionCtor();

		public static bool IsUnitTesting { get; set; }

		private static (ConstructorInfo? One, ConstructorInfo? Two) GetAssertionExceptionCtor()
		{
			// check if we are inside a unit test runner (to mute all breakpoints and other intrusive actions that would block or crash an unattended CI)

			var nUnitAssert = Type.GetType("NUnit.Framework.AssertionException,nunit.framework");
			if (nUnitAssert != null)
			{
				// Convert all "soft" failures into NUnit assertions
				IsUnitTesting = true;
				return (nUnitAssert.GetConstructor([ typeof (string) ]), nUnitAssert.GetConstructor([ typeof (string), typeof(Exception) ]));
			}
			return (null, null);
		}

		private static Exception? MapToNUnitAssertion(string message, Exception? exception)
		{
			// => new NUnit.Framework.AssertionException(...)
			return exception is null
				? (Exception?) s_constructorNUnitException.One?.Invoke([ message ])
				: (Exception?) s_constructorNUnitException.Two?.Invoke([ message, exception ])
				;
		}

		#region DEBUG checks...

		/// <summary>Test if a pre-condition is true, at the start of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Requires(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Precondition, userMessage, conditionText);
		}

		/// <summary>Test if a condition is true, inside the body of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Assert(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Assert, userMessage, conditionText);
		}

		/// <summary>Test if a post-condition is true, at the end of a method.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Ensures(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Postcondition, userMessage, conditionText);
		}

		/// <summary>Test that an invariant is met.</summary>
		/// <param name="condition">Condition that should never be false</param>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="conditionText">Text of the condition (optional, injected by the compiler)</param>
		/// <remarks>No-op if <paramref name="condition"/> is <c>true</c>. Otherwise, throws a ContractException, after attempting to breakpoint (if a debugger is attached)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Invariant(
			[AssertionCondition(AssertionConditionType.IS_TRUE)]
			[System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]
			bool condition,
			string? userMessage = null,
			[CallerArgumentExpression("condition")] string? conditionText = null
		)
		{
			if (!condition) throw RaiseContractFailure(SDC.ContractFailureKind.Invariant, userMessage, conditionText);
		}

		/// <summary>Unconditionally trigger an assertion fault</summary>
		/// <param name="userMessage">Message that describes the failed assertion (optional)</param>
		/// <param name="exception">Optional exception linked to the issue</param>
		/// <remarks>Throws a <see cref="ContractException"/>, after attempting to breakpoint (if a debugger is attached)</remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.NoInlining)]
		[System.Diagnostics.CodeAnalysis.DoesNotReturn, StackTraceHidden]
		public static void Fail(string? userMessage, Exception? exception = null)
		{
			throw RaiseContractFailure(SDC.ContractFailureKind.Invariant, userMessage, null, exception);
		}

		#region Contract.NotNull

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		/// <remarks>
		/// <para>Note that, even if <paramref name="value"/> is a Value Type, the JIT will optimize the method away, and no boxing should occur</para>
		/// </remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNull(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] object? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value is null)
			{
				ThrowArgumentNull(paramName, message);
			}
		}

		/// <summary>The specified instance must not be null (assert: value != null)</summary>
		/// <remarks>
		/// <para>This method allow structs (that can never be null)</para>
		/// <para>OBSOLETE: please call <see cref="NotNull(object?,string?,string?)"/>.</para>
		/// </remarks>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[Obsolete("Use Contract.NotNull() instead.")]
		public static void NotNullAllowStructs<TValue>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] TValue? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value is null)
			{
				ThrowArgumentNull(paramName, message);
			}
		}

		/// <summary>The specified pointer must not be null (assert: pointer != null)</summary>
		/// <exception cref="ArgumentNullException">if <paramref name="pointer"/> is null</exception>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void PointerNotNull(
			[System.Diagnostics.CodeAnalysis.AllowNull, System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] void* pointer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(pointer))] string? paramName = null)
		{
			if (pointer is null)
			{
				ThrowArgumentNull(paramName, message);
			}
		}

		/// <summary>The specified value cannot be null (assert: value != null)</summary>
		/// <returns>Passed value, or throws an exception if it was null</returns>
		/// <exception cref="ArgumentNullException">if <paramref name="value"/> is null</exception>
		/// <remarks>This method is intended for use in single-line property setters</remarks>
		/// <example><code>
		/// private string m_fooThatIsNeverNull;
		/// public string Foo
		/// {
		///     get => return m_fooThatIsNeverNull;
		///     set => m_fooThatIsNeverNull = Contract.ValueNotNull(value, "Foo cannot be set to null");
		/// }
		/// </code> </example>
		[Pure, AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T ValueNotNull<T>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL), NoEnumeration] T? value,
			string? message = null
		)
		{
			//note: if T is a value type, this should be optimized away but the JIT
			if (value is null) ThrowArgumentNull(nameof(value), message);
			return value;
		}

		#endregion

		#region Contract.NotNullOrEmpty

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailStringNullOrEmpty(string? value, string? paramName, string? message = null)
			=> value is null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyLength);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowStringNullOrEmpty(string? value, string? paramName, string? message = null)
			=> throw FailStringNullOrEmpty(value, paramName, message);

		/// <summary>The specified string must not be null or empty (assert: value != null &amp;&amp; value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrEmpty(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (string.IsNullOrEmpty(value)) ThrowStringNullOrEmpty(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailStringNullOrWhiteSpace(string? value, string? paramName, string? message = null)
			=> value is null ? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
			 : value.Length == 0 ? ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyLength)
			 : ReportFailure(typeof(ArgumentException), ContractMessages.StringCannotBeWhiteSpaces, message, paramName, ContractMessages.ConditionNotWhiteSpace);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowStringNullOrWhiteSpace(string? value, string? paramName, string? message = null)
			=> throw FailStringNullOrWhiteSpace(value, paramName, message);

		/// <summary>The specified string must not be null, empty or contain only whitespaces (assert: value != null &amp;&amp; value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrWhiteSpace(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (string.IsNullOrWhiteSpace(value)) ThrowStringNullOrWhiteSpace(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArrayNullOrEmpty(object? collection, string? paramName, string? message = null)
			=> collection is null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.CollectionCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArrayNullOrEmpty(object? collection, string? paramName, string? message = null)
			=> throw FailArrayNullOrEmpty(collection, paramName, message);

		/// <summary>The specified span must not be empty (assert: value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEmpty<T>(
			Span<T> value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value.Length == 0) ThrowBufferEmpty(paramName, message);
		}

		/// <summary>The specified span must not be empty (assert: value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEmpty<T>(
			ReadOnlySpan<T> value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value.Length == 0) ThrowBufferEmpty(paramName, message);
		}

		/// <summary>The specified memory must not be empty (assert: value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEmpty<T>(
			Memory<T> value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value.Length == 0) ThrowBufferEmpty(paramName, message);
		}

		/// <summary>The specified memory must not be empty (assert: value.Length != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEmpty<T>(
			ReadOnlyMemory<T> value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value.Length == 0) ThrowBufferEmpty(paramName, message);
		}

		/// <summary>The specified array must not be null or empty (assert: value != null &amp;&amp; value.Count != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrEmpty<T>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] T[]? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value is null || value.Length == 0) ThrowArrayNullOrEmpty(value, paramName, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailCollectionNullOrEmpty(object? collection, string? paramName, string? message = null)
			=> collection is null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.CollectionCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowCollectionNullOrEmpty(object? collection, string? paramName, string? message = null)
			=> throw FailCollectionNullOrEmpty(collection, paramName, message);

		/// <summary>The specified collection must not be null or empty (assert: value != null &amp;&amp; value.Count != 0)</summary>
		[AssertionMethod, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrEmpty<T>(
			[System.Diagnostics.CodeAnalysis.NotNull, AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<T>? value,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null
		)
		{
			if (value is null || value.Count == 0)
			{
				ThrowCollectionNullOrEmpty(value, paramName, message);
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferNull(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentNullException), ContractMessages.BufferCannotBeNull, message, paramName, ContractMessages.ConditionNotNull);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowBufferNull(string? paramName, string? message = null)
			=> throw FailBufferNull(paramName, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferEmpty(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.BufferCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowBufferEmpty(string? paramName, string? message = null)
			=> throw FailBufferEmpty(paramName, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferNullOrEmpty(object? array, string? paramName, string? message = null)
			=> array is null
				? ReportFailure(typeof(ArgumentNullException), ContractMessages.BufferCannotBeNull, message, paramName, ContractMessages.ConditionNotNull)
				: ReportFailure(typeof(ArgumentException), ContractMessages.BufferCannotBeEmpty, message, paramName, ContractMessages.ConditionNotEmptyCount);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowBufferNullOrEmpty(object? array, string? paramName, string? message = null)
			=> throw FailBufferNullOrEmpty(array, paramName, message);

		/// <summary>The specified buffer must not be null or empty (assert: buffer.Array != null &amp;&amp; buffer.Count != 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrEmpty(Slice buffer, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null)
		{
			// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
			if (buffer.Array is null || buffer.Count == 0)
			{
				ThrowBufferNullOrEmpty(buffer.Array, paramName, message);
			}
		}

		/// <summary>The specified buffer must not be null or empty (assert: buffer.Array != null &amp;&amp; buffer.Count != 0)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotNullOrEmpty<T>(ArraySegment<T> buffer, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null)
		{
			if (buffer.Array is null || buffer.Count == 0)
			{
				ThrowBufferNullOrEmpty(buffer.Array, paramName, message);
			}
		}

		#endregion

		#region Contract.Positive, LessThan[OrEqual], GreaterThen[OrEqual], EqualTo, Between, ...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotPositive(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.PositiveNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotPositive(string? paramName, string? message = null)
			=> throw ReportFailure(typeof(ArgumentException), ContractMessages.PositiveNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotNonNegative(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.NonNegativeNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotNonNegative(string? paramName, string? message = null)
			=> throw ReportFailure(typeof(ArgumentException), ContractMessages.NonNegativeNumberRequired, message, paramName, ContractMessages.ConditionArgPositive);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotPowerOfTwo(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.PowerOfTwoRequired, message, paramName, ContractMessages.ConditionArgPositive);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentForbidden<T>(string? paramName, T forbidden, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsForbidden, message, paramName, ContractMessages.ConditionArgNotEqualTo);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentForbidden<T>(string? paramName, T forbidden, string? message = null)
			=> throw ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsForbidden, message, paramName, ContractMessages.ConditionArgNotEqualTo);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentExpected<T>(string? paramName, T expected, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsExpected, message, paramName, ContractMessages.ConditionArgEqualTo);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentExpected<T>(string? paramName, T expected, string? message = null)
			=> throw ReportFailure(typeof(ArgumentException), ContractMessages.ValueIsExpected, message, paramName, ContractMessages.ConditionArgEqualTo);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotGreaterThan<T>(T value, string valueExpression, string thresholdExpression, bool zero, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), zero ? ContractMessages.AboveZeroNumberRequired : ContractMessages.ValueIsTooSmall, message, valueExpression, "{0} > " + thresholdExpression, details: value);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotGreaterThan<T>(T value, string valueExpression, string thresholdExpression, bool zero, string? message = null)
			=> throw FailArgumentNotGreaterThan(value, valueExpression, thresholdExpression, zero, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotGreaterOrEqual<T>(T value, string valueExpression, string thresholdExpression, bool zero, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), zero ? ContractMessages.PositiveNumberRequired : ContractMessages.ValueIsTooSmall, message, valueExpression, "{0} >= " + thresholdExpression, details: value);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotGreaterOrEqual<T>(T value, string valueExpression, string thresholdExpression, bool zero, string? message = null)
			=> throw FailArgumentNotGreaterOrEqual(value, valueExpression, thresholdExpression, zero, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotLessThan<T>(T value, string valueExpression, string thresholdExpression, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), ContractMessages.ValueIsTooBig, message, valueExpression, "{0} < " + thresholdExpression, details: value);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotLessThan<T>(T value, string valueExpression, string thresholdExpression,string? message = null)
			=> throw FailArgumentNotLessThan(value, valueExpression, thresholdExpression, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotLessOrEqual<T>(T value, string valueExpression, string thresholdExpression, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), ContractMessages.ValueIsTooBig, message, valueExpression, "{0} <= " + thresholdExpression, details: value);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNotLessOrEqual<T>(T value, string valueExpression, string thresholdExpression,string? message = null)
			=> throw FailArgumentNotLessOrEqual(value, valueExpression, thresholdExpression, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentOutOfBounds(string paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), ContractMessages.ValueMustBeBetween, message, paramName, ContractMessages.ConditionArgBetween);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentOutOfBounds(string valueExpression, string minExpression, string maxExpression, string? message = null)
			=> ReportFailure(typeof(ArgumentOutOfRangeException), ContractMessages.ValueMustBeBetween, message, valueExpression, minExpression + " <= {0} <= " + maxExpression);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNotMultiple(string paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.ValueMustBeMultiple, message, paramName, ContractMessages.ConditionArgMultiple);

		#region Positive...

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		/// <exception cref="System.ArgumentOutOfRangeException">If the value is negative</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Positive(int value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0) ThrowArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		/// <exception cref="System.ArgumentOutOfRangeException">If the value is negative</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Positive(long value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0) ThrowArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		/// <exception cref="System.ArgumentOutOfRangeException">If the value is negative</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Positive(double value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (Math.Sign(value) != 1) ThrowArgumentNotPositive(paramName, message);
		}

		/// <summary>The specified value must not be a negative number (assert: value >= 0)</summary>
		/// <exception cref="System.ArgumentOutOfRangeException">If the value is negative</exception>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Positive(float value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (MathF.Sign(value) != 1) ThrowArgumentNotPositive(paramName, message);
		}

		#endregion

		#region PowerOfTwo...

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void PowerOfTwo(int value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void PowerOfTwo(uint value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void PowerOfTwo(long value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value < 0 || unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		/// <summary>The specified value must be a power of two (assert: NextPowerOfTwo(value) == value)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void PowerOfTwo(ulong value, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (unchecked((value & (value - 1)) != 0)) throw FailArgumentNotPowerOfTwo(paramName!, message);
		}

		#endregion

		#region EqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo(int value, int expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) ThrowArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo(long value, long expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) ThrowArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo(uint value, uint expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) ThrowArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo(ulong value, ulong expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) ThrowArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo(string? value, string? expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value != expected) ThrowArgumentExpected(paramName, expected, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void EqualTo<T>(T value, T expected, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (!value.Equals(expected)) ThrowArgumentExpected(paramName, expected, message);
		}

		#endregion

		#region NotEqualTo...

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo(int value, int forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo(long value, long forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo(uint value, uint forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo(ulong value, ulong forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo(string value, string forbidden, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value == forbidden) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		/// <summary>The specified value must not equal to the specified constant (assert: value != forbidden)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void NotEqualTo<T>(T value, T forbidden, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
			where T : struct, IEquatable<T>
		{
			if (value.Equals(forbidden)) ThrowArgumentForbidden(paramName, forbidden, message);
		}

		#endregion

		#region GreaterThan...

#if NET8_0_OR_GREATER
		/// <summary>The specified value must not be less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value <= threshold) ThrowArgumentNotGreaterThan(value, valueExpression!, thresholdExpression!, T.IsZero(threshold), message);
		}
#else
		/// <summary>The specified value must not be less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) <= 0) ThrowArgumentNotGreaterThan(value, valueExpression!, thresholdExpression!, threshold.CompareTo(default) == 0, message);
		}
#endif

		/// <summary>The specified value must not be less than or equal to the specified lower bound (assert: value > threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterThan<T>(TimeSpan value, TimeSpan threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value <= threshold) ThrowArgumentNotGreaterThan(value, valueExpression!, thresholdExpression!, threshold == TimeSpan.Zero, message);
		}

		#endregion

		#region GreaterOrEqual...

#if NET8_0_OR_GREATER
		/// <summary>The specified value must not be less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value < threshold) ThrowArgumentNotGreaterOrEqual(value, valueExpression!, thresholdExpression!, T.IsZero(threshold), message);
		}
#else
		/// <summary>The specified value must not less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) < 0) ThrowArgumentNotGreaterOrEqual(value, valueExpression!, thresholdExpression!, threshold.CompareTo(default) == 0, message);
		}
#endif

		/// <summary>The specified <see cref="TimeSpan"/> must not be less than the specified lower bound (assert: value >= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void GreaterOrEqual(TimeSpan value, TimeSpan threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
		{
			if (value < threshold) ThrowArgumentNotGreaterOrEqual(value, valueExpression!, thresholdExpression!, threshold == TimeSpan.Zero, message);
		}

		#endregion

		#region LessThan...

#if NET8_0_OR_GREATER
		/// <summary>The specified value must not be greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void LessThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value >= threshold) ThrowArgumentNotLessThan(value, valueExpression!, thresholdExpression!, message);
		}
#else
		/// <summary>The specified value must not be greater than or equal to the specified upper bound (assert: value &lt; threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void LessThan<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) >= 0) ThrowArgumentNotLessThan(value, valueExpression!, thresholdExpression!, message);
		}
#endif

		#endregion

		#region LessOrEqual...

#if NET8_0_OR_GREATER
		/// <summary>The specified value must not be greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void LessOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value > threshold) ThrowArgumentNotLessOrEqual(value, valueExpression!, thresholdExpression!, message);
		}
#else
		/// <summary>The specified value must not be greater than the specified upper bound (assert: value &lt;= threshold)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void LessOrEqual<T>(T value, T threshold, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(threshold))] string? thresholdExpression = null)
			where T : struct, IComparable<T>
		{
			if (value.CompareTo(threshold) > 0) ThrowArgumentNotLessOrEqual(value, valueExpression!, thresholdExpression!, message);
		}
#endif

		#endregion

		#region Between...

#if NET8_0_OR_GREATER
		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Between<T>(T value, T minimumInclusive, T maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
			where T : System.Numerics.INumber<T>
		{
			if (value < minimumInclusive || value > maximumInclusive) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}
#else
		/// <summary>The specified value must not be outside the specified bounds (assert: min &lt;= value &lt;= max)</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Between<T>(T value, T minimumInclusive, T maximumInclusive, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? valueExpression = null, [InvokerParameterName, CallerArgumentExpression(nameof(minimumInclusive))] string? minExpression = null, [InvokerParameterName, CallerArgumentExpression("maximumInclusive")] string? maxExpression = null)
			where T : IComparable<T>
		{
			if (value.CompareTo(minimumInclusive) < 0 || value.CompareTo(maximumInclusive) > 0) throw FailArgumentOutOfBounds(valueExpression!, minExpression!, maxExpression!, message);
		}
#endif

		#endregion

		#region Multiple...

#if NET8_0_OR_GREATER
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Multiple<T>(T value, T multiple, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
			where T : System.Numerics.INumber<T>
		{
			if ((value % multiple) != T.Zero) throw FailArgumentNotMultiple(paramName!, message);
		}
#else
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Multiple(int value, int multiple, string? message = null, [InvokerParameterName] [CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Multiple(uint value, uint multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Multiple(long value, long multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[StackTraceHidden]
		public static void Multiple(ulong value, ulong multiple, string? message = null, [InvokerParameterName, CallerArgumentExpression(nameof(value))] string? paramName = null)
		{
			if (value % multiple != 0) throw FailArgumentNotMultiple(paramName!, message);
		}
#endif

		#endregion

		#endregion

		#region Contract.DoesNotOverflow

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] string? buffer,
			int index,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(index))] string? paramIndex = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer is null) { ThrowArgumentNull(paramBuffer!, message); }
			if (index < 0 || count < 0) ThrowArgumentNotNonNegative(index < 0 ? paramIndex! : paramCount!, message);
			if ((buffer.Length - index) < count) ThrowBufferTooSmall(paramCount!, message);
		}

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow(int bufferLength, int offset, int count, string? message = null)
		{
			if (offset < 0 || count < 0) ThrowArgumentNotNonNegative(offset < 0 ? nameof(offset) : nameof(count), message);
			if ((bufferLength - offset) < count) ThrowBufferTooSmall(nameof(count), message);
		}

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow(long bufferLength, long offset, long count, string? message = null)
		{
			if (offset < 0 || count < 0) ThrowArgumentNotNonNegative(offset < 0 ? nameof(offset) : nameof(count), message);
			if ((bufferLength - offset) < count) ThrowBufferTooSmall(nameof(count), message);
		}

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] TElement[]? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(offset))] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer is null) ThrowArgumentNull(paramBuffer!, message);
			if (offset < 0 || count < 0) ThrowArgumentNotNonNegative(offset < 0 ? paramOffset! : paramCount!, message);
			if ((buffer.Length - offset) < count) ThrowBufferTooSmall(paramCount!, message);
		}

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow<TElement>(
			ArraySegment<TElement> buffer,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramName = null
		)
		{
			if (buffer.Offset < 0 || buffer.Count < 0)
			{
				ThrowIfNegative(buffer, message, paramName);
			}
			if (buffer.Count > 0)
			{
				if (buffer.Array is null) ThrowBufferNull(paramName, message);
				if ((buffer.Array.Length - buffer.Offset) < buffer.Count) ThrowTooSmall(paramName, message);
			}
			else
			{
				if (buffer.Array is not null && buffer.Array.Length < buffer.Offset) ThrowTooSmall(paramName, message);
			}

			[DoesNotReturn, StackTraceHidden]
			static void ThrowIfNegative(ArraySegment<TElement> buffer, string? message, string? paramName)
			{
				throw FailArgumentNotNonNegative(paramName + (buffer.Offset < 0 ? ".Offset" : ".Count"), message);
			}

			[DoesNotReturn, StackTraceHidden]
			static void ThrowTooSmall(string? paramName, string? message)
			{
				throw FailBufferTooSmall(paramName + ".Count", message);
			}

		}

		/// <summary>The specified region must not be outside the specified buffer</summary>
		[AssertionMethod]
		[StackTraceHidden]
		public static void DoesNotOverflow<TElement>(
			[System.Diagnostics.CodeAnalysis.NotNull]
			[AssertionCondition(AssertionConditionType.IS_NOT_NULL)] ICollection<TElement>? buffer,
			int offset,
			int count,
			string? message = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(buffer))] string? paramBuffer = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(offset))] string? paramOffset = null,
			[InvokerParameterName, CallerArgumentExpression(nameof(count))] string? paramCount = null
		)
		{
			if (buffer is null) ThrowArgumentNull(paramBuffer!, message);
			if (offset < 0 || count < 0) ThrowArgumentNotNonNegative(offset < 0 ? paramOffset! : paramCount!, message);
			if ((buffer.Count - offset) < count) ThrowBufferTooSmall(paramCount!, message);
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailBufferTooSmall(string paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentException), ContractMessages.OffsetMustBeWithinBuffer, message, paramName, ContractMessages.ConditionArgBufferOverflow);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowBufferTooSmall(string paramName, string? message = null)
			=> throw FailBufferTooSmall(paramName, message);

		#endregion

		#endregion

		#region Internal Helpers...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception FailArgumentNull(string? paramName, string? message = null)
			=> ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNull(string? paramName, string? message = null)
			=> throw ReportFailure(typeof(ArgumentNullException), ContractMessages.ValueCannotBeNull, message, paramName, ContractMessages.ConditionNotNull);

		/// <summary>Throws an exception, following a failed assertion</summary>
		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		internal static Exception ReportFailure(Type exceptionType, string msg, string? userMessage, string? paramName, string? conditionTxt, object? details = null)
		{
			if (conditionTxt != null && conditionTxt.IndexOf('{') >= 0)
			{ // Replace any occurence of "{0}" in the condition, by the name of the parameter
				conditionTxt = string.Format(conditionTxt, paramName ?? "value");
			}

			string? str = ContractHelper.RaiseContractFailedEvent(SDC.ContractFailureKind.Precondition, userMessage ?? msg, conditionTxt, null);
			// If this returns null, then the issue has already been handled (popup on the screen, ...)
			// But we still need to stop the execution!
#if DEBUG
			if (str != null)
			{
				// note: do not spam the logs if in the context of a unit test runner!
				if (!IsUnitTesting)
				{
					System.Diagnostics.Debug.Fail(str);
				}
			}
#endif
			string description = userMessage ?? str ?? msg;

			var exception = ThrowHelper.TryMapToKnownException(exceptionType, description, paramName, details);

			if (exception is null)
			{ // Is this a complex exception type ?
#pragma warning disable IL2067
				exception = ThrowHelper.TryMapToComplexException(exceptionType, description, paramName);
#pragma warning restore IL2067
			}

			if (exception is null)
			{ // still not know? we'll try to wrap it with a proxy exception
				exception = FallbackForUnknownException(description, paramName);
			}

			return exception;
		}

		private static Exception FallbackForUnknownException(string description, string? paramName)
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break(); // README: If you breakpoint here, it means the caller has requested a type of Exception that we cannot instantiate! Please change the type to something easier
#endif
			return paramName != null
				? new ArgumentException(description, paramName)
				: new InvalidOperationException(description);
		}

		/// <summary>Creates the <seealso cref="ContractException"/> that matches a failed assertion.</summary>
		/// <remarks>
		/// <para>If a debugger is attached to the process, a breakpoint will be triggered.</para>
		/// <para>If the method detects that it is running inside a supported a Unit Test Runner, it will instead create an exception that matches the type expected by the framework (ex: AssertionException for NUnit)</para>
		/// </remarks>
		[Pure]
		[MethodImpl(MethodImplOptions.NoInlining)]
		[StackTraceHidden]
		internal static Exception RaiseContractFailure(SDC.ContractFailureKind kind, string? msg, string? conditionText = null, Exception? exception = null)
		{
			//note: currently in .NET Core 3.x, if conditionText is null, the formatted message will not have the "kind" section!
			// => the workaround is to pass the message itself, instead of the condition text, which will slightly change the generated string, but will still be readable!
			string? str = conditionText is null
				? ContractHelper.RaiseContractFailedEvent(kind, msg, null, exception)
				: ContractHelper.RaiseContractFailedEvent(kind, msg, conditionText, exception);
			if (str is not null)
			{
				if (IsUnitTesting)
				{
					// throws an AssertionException if we were able to connect with NUnit
					var ex = MapToNUnitAssertion(str, exception);
#if DEBUG
					// README: If you breakpoint here, you are too deep and must go up the stack, until you find the method that invoked any of the Contract.xxx(...) helpers!
					if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
					// note: starting from VS 2015 Up2, [DebuggerNonUserCode] has no effect anymore, if the registry key AlwaysEnableExceptionCallbacksOutsideMyCode is not set to 1, for performance reasons.
					// cf "How to Suppress Ignorable Exceptions with DebuggerNonUserCode" dans https://blogs.msdn.microsoft.com/visualstudioalm/2016/02/12/using-the-debuggernonusercode-attribute-in-visual-studio-2015/
#endif
					if (ex is not null) return ex;
					// if not null, continue
				}
#if DEBUG
				else if (kind == SDC.ContractFailureKind.Assert && System.Diagnostics.Debugger.IsAttached)
				{
					// only when debugging from VS, or else it will cause issues with Resharper's TestRunner (that will display a popup for each failed assertion!)
					System.Diagnostics.Debug.Fail(str);
				}
#endif
			}
			else
			{ // use default message
				msg = "Contract Failed";
			}

			return new ContractException(kind, str, msg, conditionText, null);
		}


		/// <summary>
		/// Allows a managed application environment such as an interactive interpreter (IronPython)
		/// to be notified of contract failures and
		/// potentially "handle" them, either by throwing a particular exception type, etc.  If any of the
		/// event handlers sets the Cancel flag in the ContractFailedEventArgs, then the Contract class will
		/// not pop up an assert dialog box or trigger escalation policy.  Hooking this event requires
		/// full trust, because it will inform you of bugs in the appdomain and because the event handler
		/// could allow you to continue execution.
		/// </summary>
		public static event EventHandler<SDC.ContractFailedEventArgs>? ContractFailed
		{
			add => SDC.Contract.ContractFailed += value;
			remove => SDC.Contract.ContractFailed -= value;
		}

		#endregion

	}

}
