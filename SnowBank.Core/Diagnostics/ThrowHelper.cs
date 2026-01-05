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

	[DebuggerNonUserCode, PublicAPI, StackTraceHidden]
	public static class ThrowHelper
	{

		#region ArgumentNullException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentNullException ArgumentNullException([InvokerParameterName] string paramName) => new(paramName);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentNullException ArgumentNullException([InvokerParameterName] string paramName, string message) => new(paramName, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentNullException ArgumentNullException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => new(paramName, message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName) => throw ArgumentNullException(paramName);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName, string message) => throw ArgumentNullException(paramName, message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentNullException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => throw ArgumentNullException(paramName, message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentNullException<T>([InvokerParameterName] string paramName) => throw ArgumentNullException(paramName);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentNullException<T>([InvokerParameterName] string paramName, string message) => throw ArgumentNullException(paramName, message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentNullException<T>([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => throw ArgumentNullException(paramName, ref message);

		#endregion

		#region ArgumentException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentException ArgumentException([InvokerParameterName] string paramName, string? message = null) => new(message, paramName);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentException ArgumentException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => new(message.ToStringAndClear(), paramName);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentException([InvokerParameterName] string paramName, string? message = null) => throw ArgumentException(paramName, message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => throw ArgumentException(paramName, ref message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentException<T>([InvokerParameterName] string paramName, string? message = null) => throw ArgumentException(paramName, message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentException<T>([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => throw ArgumentException(paramName, ref message);

		#endregion

		#region ArgumentOutOfRangeException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeException([InvokerParameterName] string paramName) => new(paramName);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeException([InvokerParameterName] string paramName, string? message) => new(paramName, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => new(paramName, message.ToStringAndClear());

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, string? message) => new(paramName, actualValue, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, ref DefaultInterpolatedStringHandler message) => new(paramName, actualValue, message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		// ReSharper disable once NotResolvedInText
		public static void ThrowArgumentOutOfRangeException() => throw ArgumentOutOfRangeException("index", "Index was out of range. Must be non-negative and less than the size of the collection.");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName) => throw ArgumentOutOfRangeException(paramName);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, string message) => throw ArgumentOutOfRangeException(paramName, null, message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, ref DefaultInterpolatedStringHandler message) => throw ArgumentOutOfRangeException(paramName, null, ref message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, string message) => throw ArgumentOutOfRangeException(paramName, actualValue, message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeException([InvokerParameterName] string paramName, object? actualValue, ref DefaultInterpolatedStringHandler message) => throw ArgumentOutOfRangeException(paramName, actualValue, ref message);

		#endregion

		#region ObjectDisposedException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed) => new(disposed?.GetType().Name);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type) => new(type.Name);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type, string message) => new(type.Name, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(Type type, ref DefaultInterpolatedStringHandler message) => new(type.Name, message.ToStringAndClear());

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed, string message) => new((disposed?.GetType() ?? typeof(TDisposed)).Name, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(TDisposed disposed, ref DefaultInterpolatedStringHandler message) => new((disposed?.GetType() ?? typeof(TDisposed)).Name, message.ToStringAndClear());

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(string message) => new(typeof(TDisposed).Name, message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException<TDisposed>(ref DefaultInterpolatedStringHandler message) => new(typeof(TDisposed).Name, message.ToStringAndClear());

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(string message, Exception? innerException) => new(message, innerException);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ObjectDisposedException ObjectDisposedException(ref DefaultInterpolatedStringHandler message, Exception? innerException) => new(message.ToStringAndClear(), innerException);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException(Type type) => throw ObjectDisposedException(type);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException(Type type, string message) => throw ObjectDisposedException(type, message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException(Type type, ref DefaultInterpolatedStringHandler message) => throw ObjectDisposedException(type, ref message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException(string message, Exception innerException) => throw ObjectDisposedException(message, innerException);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException(ref DefaultInterpolatedStringHandler message, Exception innerException) => throw ObjectDisposedException(ref message, innerException);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed) where TDisposed : IDisposable => throw ObjectDisposedException(disposed.GetType());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed, string message) where TDisposed : IDisposable => throw ObjectDisposedException(disposed.GetType(), message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowObjectDisposedException<TDisposed>(TDisposed disposed, ref DefaultInterpolatedStringHandler message) where TDisposed : IDisposable => throw ObjectDisposedException(disposed.GetType(), ref message);

		#endregion

		#region InvalidOperationException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(string message) => new(message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationException(ref DefaultInterpolatedStringHandler message) => new(message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowInvalidOperationException(string message) => throw InvalidOperationException(message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowInvalidOperationException(ref DefaultInterpolatedStringHandler message) => throw InvalidOperationException(message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowInvalidOperationException<T>(string message) => throw InvalidOperationException(message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowInvalidOperationException<T>(ref DefaultInterpolatedStringHandler message) => throw InvalidOperationException(message.ToStringAndClear());

		#endregion

		#region FormatException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(string message) => new(message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static FormatException FormatException(ref DefaultInterpolatedStringHandler message) => new(message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowFormatException(string message) => throw FormatException(message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowFormatException(ref DefaultInterpolatedStringHandler message) => throw FormatException(ref message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowFormatException<T>(string message) => throw FormatException(message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowFormatException<T>(ref DefaultInterpolatedStringHandler message) => throw FormatException(ref message);

		#endregion

		#region OperationCanceledException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(string message) => new(message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static OperationCanceledException OperationCanceledException(ref DefaultInterpolatedStringHandler message) => new(message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowOperationCanceledException(string message) => throw OperationCanceledException(message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowOperationCanceledException(ref DefaultInterpolatedStringHandler message) => throw OperationCanceledException(ref message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowOperationCanceledException<T>(string message) => throw OperationCanceledException(message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowOperationCanceledException<T>(ref DefaultInterpolatedStringHandler message) => throw OperationCanceledException(ref message);

		#endregion

		#region NotSupportedException...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(string message) => new(message);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedException(ref DefaultInterpolatedStringHandler message) => new(message.ToStringAndClear());

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowNotSupportedException(string message) => throw NotSupportedException(message);

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowNotSupportedException(ref DefaultInterpolatedStringHandler message) => throw NotSupportedException(ref message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowNotSupportedException<T>(string message) => throw NotSupportedException(message);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowNotSupportedException<T>(ref DefaultInterpolatedStringHandler message) => throw NotSupportedException(ref message);

		#endregion

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception? TryMapToKnownException(Type exceptionType, string message, string? paramName, object? details)
		{
			// first check if this is a "simple" type
			if (exceptionType == typeof(ArgumentNullException))
			{
				return new ArgumentNullException(paramName, message);
			}
			if (exceptionType == typeof(InvalidOperationException))
			{
				return new InvalidOperationException(message);
			}
			if (exceptionType == typeof(ArgumentException))
			{
				return new ArgumentException(message, paramName);
			}
			if (exceptionType == typeof(ArgumentOutOfRangeException))
			{
				return new ArgumentOutOfRangeException(paramName, details, message);
			}
			if (exceptionType == typeof(ObjectDisposedException))
			{
				return new ObjectDisposedException(paramName, message);
			}
			if (exceptionType == typeof (FormatException))
			{
				return new FormatException(message);
			}
			return null;
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static Exception? TryMapToComplexException([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type exceptionType, string message, string? paramName)
		{
			ConstructorInfo? constructor;

			if (paramName != null)
			{ // look for a ctor that takes two strings with one of them called "paramName"
				constructor = exceptionType.GetConstructor([ typeof(string), typeof(string) ]);
				if (constructor != null)
				{
					if (constructor.GetParameters()[0].Name == "paramName")
					{
						return constructor.Invoke([ paramName, message ]) as Exception;
					}
					if (constructor.GetParameters()[1].Name == "paramName")
					{
						return constructor.Invoke([ message, paramName ]) as Exception;
					}
				}
			}

			// look for a ctor that takes only one string
			constructor = exceptionType.GetConstructor([ typeof(string) ]);
			if (constructor != null)
			{
				return constructor.Invoke([ message ]) as Exception;
			}

			// is this a parameterless ctor?
			constructor = exceptionType.GetConstructor(Type.EmptyTypes);
			if (constructor != null)
			{
				return constructor.Invoke(null) as Exception;
			}

			return null;
		}

		#region Collection Errors...

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoElements() => new("Sequence contains no elements.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static InvalidOperationException InvalidOperationNoMatchingElements() => new("Sequence contains no matching element.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static IndexOutOfRangeException IndexOutOfRangeException() => new("Index was out of range. Must be non-negative and less than the size of the collection.");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowIndexOutOfRangeException() => throw IndexOutOfRangeException();

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowIndexOutOfRangeException<T>() => throw IndexOutOfRangeException();

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeIndex(int index) => new(nameof(index), index, "Index was out of range. Must be non-negative and less than the size of the collection.");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeIndex(int index) => throw ArgumentOutOfRangeIndex(index);

		[DoesNotReturn, StackTraceHidden]
		public static T ThrowArgumentOutOfRangeIndex<T>(int index) => throw ArgumentOutOfRangeIndex(index);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentOutOfRangeException ArgumentOutOfRangeNeedNonNegNum([InvokerParameterName] string paramName) => new(paramName, "Non-negative number required");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowArgumentOutOfRangeNeedNonNegNum([InvokerParameterName] string paramName) => throw ArgumentOutOfRangeNeedNonNegNum(paramName);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static ArgumentException ArgumentInvalidOffLen() => new("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static NotSupportedException NotSupportedReadOnlyCollection() => new("Collection is read-only.");

		[DoesNotReturn, StackTraceHidden]
		public static void ThrowNotSupportedReadOnlyCollection() => throw NotSupportedReadOnlyCollection();

		#endregion

	}

}
