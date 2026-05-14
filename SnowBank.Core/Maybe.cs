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

namespace System
{
	using System.Runtime;
	using System.Runtime.ExceptionServices;

	/// <summary>Represents a value that may or may not be present, along with an optional error state.</summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	[PublicAPI]
	[DebuggerDisplay("{ToString(),nq}")]
	[DebuggerNonUserCode]
	public readonly struct Maybe<T> : IEquatable<Maybe<T>>, IEquatable<T>, IComparable<Maybe<T>>, IComparable<T>, IFormattable
	{

		/// <summary>Represents an empty result (no computation)</summary>
		public static readonly Maybe<T> Nothing;

		/// <summary>Represents a result that is equal to the default of a type (<see langword="0"/>, <see langword="false"/>, <see langword="null"/>, ...)</summary>
		public static readonly Maybe<T> Default = new(default!);

		/// <summary>Cached completed Task that always return an empty value</summary>
		public static readonly Task<Maybe<T>> EmptyTask = Task.FromResult(default(Maybe<T>));

		#region Private Fields...

		// ==================================================================================
		//  m_hasValue |   m_value   |  m_error     | description
		// ==================================================================================
		//     True    |   Résultat  |   null       | Le calcul a produit un résultat (qui peut être le défaut du type, mais qui n'est pas "vide")
		//     False   |      -      |   null       | Le calcul n'a pas produit de résultat
		//     False   |      -      |   Exception  | Le calcul a provoqué une exception

		/// <summary>If <see langword="true"/>, there is a value. If <see langword="false"/>, either no value or an exception</summary>
		private readonly T? m_value;

		/// <summary>If HasValue is <see langword="true"/>, holds the value. Else, contains default(T)</summary>
		private readonly bool m_hasValue;

		/// <summary>If <see cref="HasValue"/>> is <see langword="false"/> optionally holds an error that was captured</summary>
		private readonly object? m_errorContainer; // either an Exception, or an ExceptionDispatchInfo

		#endregion

		public Maybe(T value)
		{
			m_hasValue = true;
			m_value = value;
			m_errorContainer = null;
		}

		internal Maybe(bool hasValue, T value, object? errorContainer)
		{
			Contract.Debug.Requires(errorContainer is null or Exception or ExceptionDispatchInfo);

			m_hasValue = hasValue;
			m_value = value;
			m_errorContainer = errorContainer;
		}

		/// <summary>There is a value</summary>
		/// <remarks>!(IsEmpty || HasFailed)</remarks>
		public bool HasValue
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_hasValue;
		}

		/// <summary>Returns the value if the computation succeeded</summary>
		/// <exception cref="InvalidOperationException">If the value is empty</exception>
		/// <exception cref="AggregateException">If the value has failed to compute</exception>
		public T Value
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			[return: MaybeNull]
			get => m_hasValue ? m_value! : ThrowInvalidState();
		}

		/// <summary>Returns the value if the computation succeeded, or default(<typeparamref name="T"/>) in all other cases</summary>
		[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T? GetValueOrDefault()
		{
			return m_value;
		}

		/// <summary>Returns the capture Exception, or null if no error occurred.</summary>
		public Exception? Error
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure]
			get => m_errorContainer is ExceptionDispatchInfo edi
				? edi.SourceException
				: m_errorContainer as Exception;
		}

		/// <summary>Tests if the value failed to compute</summary>
		/// <param name="result">Receives the result of the computation, if it completed successfully</param>
		/// <param name="error">Receives the captured error if the computation has failed.</param>
		/// <returns> <see langword="true"/> if the computation has completed successfully; otherwise, <see langword="false"/>.</returns>
		/// <example><code>
		/// Maybe&lt;int&gt; ComputeInner() { .... }
		///
		/// Maybe&lt;string&gt; ComputeOuter()
		/// {
		///   if (!ComputeSomething().Check(out var result, out var error))
		///   {
		///     return error; // auto-cast to Maybe&lt;string&gt; with the error
		///   }
		///   return "inner = " + result;
		/// }
		/// </code></example>
		public bool Check([MaybeNullWhen(false)] out T result, out MaybeError error)
		{
			if (m_hasValue)
			{
				result = m_value!;
				error = default;
				return true;
			}
			else
			{
				result = default;
				error = new(m_errorContainer);
				return false;
			}
		}

		/// <summary>Returns the captured error context, or null if there wasn't any</summary>
		public ExceptionDispatchInfo? CapturedError => m_errorContainer is Exception exception ? ExceptionDispatchInfo.Capture(exception) : m_errorContainer as ExceptionDispatchInfo;

		/// <summary>The value failed to compute</summary>
		/// <returns>!(HasValue || IsEmpty)</returns>
		public bool Failed
		{
			[TargetedPatchingOptOut("Performance critical to inline this type of method across NGen image boundaries")]
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_errorContainer != null;
		}

		/// <summary>Returns the result of the computation, or re-throws any captured exception</summary>
		/// <example><code>
		/// Maybe&lt;string&gt; ComputeSomething() { .... }
		///
		/// var result = ComputeSometing().Resolve(); // throws if failed
		/// Console.WriteLine("Result is: " + result);
		/// </code></example>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public T Resolve()
		{
			return m_hasValue ? m_value! : HandleNonSuccess();
		}

		[StackTraceHidden, MethodImpl(MethodImplOptions.NoInlining)]
		private T HandleNonSuccess()
		{
			switch (m_errorContainer)
			{
				case ExceptionDispatchInfo edi:
				{
					edi.Throw();
					throw null!; // never reached, but helps with code analysis
				}
				case Exception ex:
				{
					throw ex;
				}
				default:
				{
					throw new InvalidOperationException("Computation yielded no result");
				}
			}
		}

		/// <summary>Rethrows any captured error, if there was one.</summary>
		public void ThrowForNonSuccess()
		{
			if (m_errorContainer != null)
			{
				if (m_errorContainer is not Exception exception)
				{
					((ExceptionDispatchInfo) m_errorContainer).Throw();
					return; // never reached, but helps with code analysis
				}
				throw exception;
			}
		}

		internal object? ErrorContainer
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_errorContainer;
		}

		/// <summary>No value was returned</summary>
		/// <remarks>!(HasValue || Failed)</remarks>
		public bool IsEmpty
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => !m_hasValue && m_errorContainer == null;
		}

		[DoesNotReturn, StackTraceHidden]
		private T ThrowInvalidState()
		{
			if (m_errorContainer != null) throw new AggregateException("A computation has triggered an exception.", this.Error!);
			if (!m_hasValue) throw new InvalidOperationException("This computation has no value.");
			throw new InvalidOperationException("This computation already has a value.");
		}

		[Pure]
		public static Func<Maybe<T>, Maybe<TResult>> Return<TResult>(Func<T, TResult> computation)
		{
			return Bind(x => new Maybe<TResult>(computation(x)));
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Failure(Exception error) => new(false, default!, error);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Failure(ExceptionDispatchInfo error) => new(false, default!, error);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Failure(MaybeError error) => new(false, default!, error.Container);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator T(Maybe<T> m) => m.Value;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Maybe<T>(T value) => new(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Maybe<T>(Exception error) => Failure(error);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Maybe<T>(MaybeError error) => Failure(error);

		public bool Equals(Maybe<T> other)
		{
			if (m_hasValue) return other.m_hasValue && EqualityComparer<T>.Default.Equals(m_value, other.m_value);
			if (m_errorContainer != null) return !m_hasValue && m_errorContainer.Equals(other.m_errorContainer);
			return !other.m_hasValue && other.m_errorContainer == null;
		}
		public bool Equals(T? other)
		{
			return m_hasValue && EqualityComparer<T>.Default.Equals(m_value, other);
		}

		public override bool Equals(object? obj)
		{
			switch (obj)
			{
				case null: return !m_hasValue;
				case T value: return Equals(value);
				case Maybe<T> maybe: return Equals(maybe);
				case Exception err: return !m_hasValue && err.Equals(m_errorContainer);
				default: return false;
			}
		}

		public override int GetHashCode()
		{
			return m_hasValue ? EqualityComparer<T>.Default.GetHashCode(m_value!) : m_errorContainer?.GetHashCode() ?? -1;
		}

		public int CompareTo(Maybe<T> other)
		{
			// in order: "nothing", then values, then errors

			if (m_hasValue)
			{ // Some
				if (other.m_hasValue) return Comparer<T>.Default.Compare(m_value, other.m_value);
				if (other.m_errorContainer != null) return -1; // values come before errors
				return +1; // values come after nothing
			}

			if (m_errorContainer != null)
			{ // Error
				if (other.m_hasValue || other.m_errorContainer == null) return +1; // errors come after everything except errors
				//note: this is tricky, because we cannot really sort Exceptions, so this sort may not be stable :(
				// => the "only" way would be to compare their hash codes!
				return ReferenceEquals(m_errorContainer, other.m_errorContainer) ? 0 : m_errorContainer.GetHashCode().CompareTo(other.m_errorContainer.GetHashCode());
			}

			// Nothing comes before everything except nothing
			return other.m_hasValue || other.m_errorContainer != null ? -1 : 0;
		}

		public int CompareTo(T? other)
		{
			// in order: "nothing", then values, then errors
			if (!m_hasValue)
			{
				return m_errorContainer != null ? +1 : -1;
			}
			return Comparer<T>.Default.Compare(m_value, other);
		}

		public string ToString(string? format, IFormatProvider? formatProvider)
		{
			if (this.Failed) return "<error>";
			if (!this.HasValue) return "<none>";
			var value = m_value;
			if (value == null) return "<null>"; //REVIEW: => "<nothing>" ?
			if (value is IFormattable fmt) return fmt.ToString(format, formatProvider);
			return value.ToString() ?? string.Empty;
		}

		public override string ToString()
		{
			return ToString(null, null);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Maybe<T> left, T right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Maybe<T> left, T right)
		{
			return !left.Equals(right);
		}

		public static bool operator >(Maybe<T> left, T right)
		{
			return left.CompareTo(right) > 0;
		}

		public static bool operator >=(Maybe<T> left, T right)
		{
			return left.CompareTo(right) >= 0;
		}

		public static bool operator <(Maybe<T> left, T right)
		{
			return left.CompareTo(right) < 0;
		}

		public static bool operator <=(Maybe<T> left, T right)
		{
			return left.CompareTo(right) <= 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(Maybe<T> left, Maybe<T> right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(Maybe<T> left, Maybe<T> right)
		{
			return !left.Equals(right);
		}

		public static bool operator >(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) > 0;
		}

		public static bool operator >=(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) >= 0;
		}

		public static bool operator <(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) < 0;
		}

		public static bool operator <=(Maybe<T> left, Maybe<T> right)
		{
			return left.CompareTo(right) <= 0;
		}

		#region Function Binding...

		public static Func<Maybe<T>, Maybe<TResult>> Bind<TResult>(Func<T, Maybe<TResult>> computation)
		{
			return (x) =>
			{
				if (x.m_errorContainer != null) return new Maybe<TResult>(false, default!, x.m_errorContainer);
				if (!x.m_hasValue) return Maybe<TResult>.Nothing;

				try
				{
					return computation(x.m_value!);
				}
				catch (Exception e)
				{
					return Maybe<TResult>.Failure(e);
				}
			};
		}

		public static Func<Maybe<T>, Maybe<T>, Maybe<TResult>> Bind<TResult>(Func<T, T, Maybe<TResult>> computation)
		{
			return (x, y) =>
			{
				if (x.m_errorContainer != null || y.m_errorContainer != null) return Maybe.Error(default(TResult)!, x.Error, y.Error);
				if (x.m_hasValue && y.m_hasValue)
				{
					try
					{

						return computation(x.m_value!, y.m_value!);
					}
					catch (Exception e)
					{
						return Maybe<TResult>.Failure(e);
					}
				}
				return Maybe<TResult>.Nothing;
			};
		}

		#endregion

	}

	/// <summary>Provides a set of static methods to work with the <see cref="Maybe{T}"/> monad, enabling functional programming patterns in C#.</summary>
	[PublicAPI]
	public static class Maybe
	{

		/// <summary>Returns a <see cref="Maybe{T}"/> instance containing the specified <typeparamref name="T"/>.</summary>
		/// <typeparam name="T">A type of the value, which must be a Reference Type.</typeparam>
		/// <param name="value">The value to wrap in a <see cref="Maybe{T}"/>.</param>
		/// <returns>A <see cref="Maybe{T}"/> encapsulating the value.</returns>
		/// <remarks>
		/// <para>If <paramref name="value"/> is <see langword="null"/>, the <see cref="Maybe{T}"/> still has a value, and is not equal to <see cref="Maybe{T}.Nothing"/>.</para>
		/// <para>If you need to map null values to <see cref="Maybe{T}.Nothing"/>, use <see cref="ReturnNotNull{T}(T?)"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Return<T>(T value)
		{
			// ENTER THE MONAD !
			return new Maybe<T>(value);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> instance containing the specified <typeparamref name="T"/>, if it is not null.</summary>
		/// <typeparam name="T">A type of the value, which must be a Reference Type.</typeparam>
		/// <param name="value">The value to wrap in a <see cref="Maybe{T}"/>, which could be null.</param>
		/// <returns><see cref="Maybe{T}.Nothing"/> if the instance is null, otherwise a <see cref="Maybe{T}"/> encapsulating the value.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> ReturnNotNull<T>(T? value)
			where T : class
		{
			// ENTER THE MONAD
			return value == null ? Maybe<T>.Nothing : new Maybe<T>(value);
		}

		/// <summary>Returns a <see cref="Maybe{T}"/> instance containing the specified <see cref="Nullable{T}"/>.</summary>
		/// <typeparam name="T">A type of the value, which must be a Value Type.</typeparam>
		/// <param name="value">The value to wrap in a <see cref="Maybe{T}"/>.</param>
		/// <returns><see cref="Maybe{T}.Nothing"/> if the instance is null, otherwise a <see cref="Maybe{T}"/> encapsulating the value.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> ReturnNotNull<T>(T? value)
			where T : struct
		{
			return value.HasValue ? new(value.Value) : default(Maybe<T>);
		}

		/// <summary>Creates an empty <see cref="Maybe{T}"/> instance.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <returns>An empty <see cref="Maybe{T}"/> instance.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Nothing<T>()
		{
			// ENTER THE MONAD !
			return default(Maybe<T>);
		}

		/// <summary>
		/// Creates an empty <see cref="Maybe{T}"/> instance.
		/// </summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="_">A parameter whose value is ignored, used only to help the compiler infer the type.</param>
		/// <returns>An empty <see cref="Maybe{T}"/> instance.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Nothing<T>(T? _)
		{
			// ENTER THE MONAD !
			return default(Maybe<T>);
		}

		/// <summary>Creates a <see cref="Maybe{T}"/> instance representing an error.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="error">The exception to encapsulate.</param>
		/// <returns>A <see cref="Maybe{T}"/> instance encapsulating the error.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(Exception error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Creates a <see cref="Maybe{T}"/> instance representing an error.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="error">The exception to encapsulate.</param>
		/// <returns>A <see cref="Maybe{T}"/> instance encapsulating the error.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(ExceptionDispatchInfo error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Creates a <see cref="Maybe{T}"/> instance representing an error, using the compiler to infer the type of the value.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="_">A parameter whose value is ignored, used only to help the compiler infer the type.</param>
		/// <param name="error">The exception to encapsulate.</param>
		/// <returns>A <see cref="Maybe{T}"/> encapsulating the error.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(T? _, Exception error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Creates a <see cref="Maybe{T}"/> instance representing an exception, using the compiler to infer the type of the value.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="_">A parameter whose value is ignored, used only to help the compiler infer the type.</param>
		/// <param name="error">The exception to encapsulate.</param>
		/// <returns>A <see cref="Maybe{T}"/> instance encapsulating the error.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Maybe<T> Error<T>(T? _, ExceptionDispatchInfo error)
		{
			// ENTER THE MONAD !
			return Maybe<T>.Failure(error);
		}

		/// <summary>Creates a <see cref="Maybe{T}"/> instance that encapsulates one or two exceptions.</summary>
		/// <typeparam name="T">The type of the value that is ignored.</typeparam>
		/// <param name="_">A parameter whose value is ignored, used to help the compiler infer the type.</param>
		/// <param name="error0">The first exception, which can be null.</param>
		/// <param name="error1">The second exception, which can be null.</param>
		/// <returns>A <see cref="Maybe{T}"/> instance encapsulating the error(s). If both errors are present, they are combined into an <see cref="AggregateException"/>.</returns>
		[Pure]
		public static Maybe<T> Error<T>(T? _, Exception? error0, Exception? error1)
		{
			Contract.Debug.Requires(error0 != null || error1 != null);

			if (error1 == null)
			{
				return Maybe<T>.Failure(error0!);
			}
			if (error0 == null)
			{
				return Maybe<T>.Failure(error1);
			}
			return Maybe<T>.Failure(new AggregateException(error0, error1));
		}

		/// <summary>Converts a <see cref="Maybe{T}"/> into a <see cref="Nullable{T}"/>.</summary>
		/// <typeparam name="T">The type of the value to convert, which must be a value type.</typeparam>
		/// <param name="m">The instance to convert.</param>
		/// <returns>Returns either <see langword="null"/> if the instance is <see cref="Maybe{T}.Nothing"/>,  or a <typeparamref name="T"/> if the value is present. </returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? ToNullable<T>(this Maybe<T> m)
			where T : struct
		{
			// EXIT THE MONAD
			return m.HasValue ? m.Value : null;
		}

		/// <summary>Returns the value of the <see cref="Maybe{T}"/> if it has one, or the specified default value.</summary>
		/// <typeparam name="T">The type of the value.</typeparam>
		/// <param name="m">The <see cref="Maybe{T}"/> instance.</param>
		/// <param name="default">The default value to return if the <see cref="Maybe{T}"/> does not have a value.</param>
		/// <returns>The value of the <see cref="Maybe{T}"/> if it has one; otherwise, the specified default value.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? OrDefault<T>(this Maybe<T> m, T? @default = default)
		{
			// EXIT THE MONAD
			return m.HasValue ? m.Value : @default;
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(T value, [InstantHandle] Func<T, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			try
			{
				return Return(lambda(value));
			}
			catch (Exception e)
			{
				return Error<TResult>(ExceptionDispatchInfo.Capture(e));
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(T value, [InstantHandle] Func<T, Maybe<TResult>> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			try
			{
				return lambda(value);
			}
			catch (Exception e)
			{
				return Error<TResult>(ExceptionDispatchInfo.Capture(e));
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(Maybe<T> value, [InstantHandle] Func<T, TResult> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			if (!value.HasValue)
			{
				var errorContainer = value.ErrorContainer;
				if (errorContainer != null)
				{
					// keep the original error untouched
					return new Maybe<TResult>(false, default!, errorContainer);
				}
				return Nothing<TResult>();
			}
			try
			{
				return Return(lambda(value.Value));
			}
			catch (Exception e)
			{
				return Error<TResult>(e);
			}
		}

		/// <summary>Immediately apply a function to a value, and capture the result into a <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<TResult> Apply<T, TResult>(Maybe<T> value, [InstantHandle] Func<T, Maybe<TResult>> lambda)
		{
			Contract.Debug.Requires(lambda != null);
			if (!value.HasValue)
			{
				var errorContainer = value.ErrorContainer;
				if (errorContainer != null)
				{
					// keep the original error untouched
					return new Maybe<TResult>(false, default!, errorContainer);
				}
				return Nothing<TResult>();
			}
			try
			{
				return lambda(value.Value);
			}
			catch (Exception e)
			{
				return Error<TResult>(e);
			}
		}

		/// <summary>Convert a completed <see cref="Task{T}"/> into an equivalent <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<T> FromTask<T>(Task<T> task)
		{
			//REVIEW: should we return Maybe<T>.Empty if task == null ?
			Contract.Debug.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return Return(task.Result);
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception!.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Error<T>(aggEx.InnerException!);
					}
					return Error<T>(aggEx);
				}
				case TaskStatus.Canceled:
				{
					return Error<T>(new OperationCanceledException());
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

		/// <summary>Convert a completed <see cref="Task{T}"/> with <typeparamref name="T"/> being a <see cref="Maybe{T}"/>, into an equivalent <see cref="Maybe{T}"/></summary>
		[Pure]
		public static Maybe<T> FromTask<T>(Task<Maybe<T>> task)
		{
			Contract.Debug.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return task.Result;
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception!.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Error<T>(aggEx.InnerException!);
					}
					return Error<T>(aggEx);
				}
				case TaskStatus.Canceled:
				{
					return Error<T>(new OperationCanceledException());
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

		/// <summary>Streamline a potentially failed Task&lt;Maybe&lt;T&gt;&gt; into a version that capture the error into the <see cref="Maybe{T}"/> itself</summary>
		[Pure]
		public static Task<Maybe<T>> Unwrap<T>(Task<Maybe<T>> task)
		{
			Contract.Debug.Requires(task != null);
			switch (task.Status)
			{
				case TaskStatus.RanToCompletion:
				{
					return task;
				}
				case TaskStatus.Faulted:
				{
					//TODO: pass the failed task itself as the error container? (we would keep the original callstack that way...)
					var aggEx = task.Exception!.Flatten();
					if (aggEx.InnerExceptions.Count == 1)
					{
						return Task.FromResult(Maybe.Error<T>(aggEx.InnerException!));
					}
					return Task.FromResult(Maybe.Error<T>(aggEx));
				}
				case TaskStatus.Canceled:
				{
					return Task.FromResult(Error<T>(new OperationCanceledException()));
				}
				default:
				{
					throw new InvalidOperationException("Task must be in the completed state");
				}
			}
		}

		[Pure]
		private static Func<Maybe<T>, Maybe<TResult>> Combine<T, TIntermediate, TResult>(Func<Maybe<T>, Maybe<TIntermediate>> f, Func<Maybe<TIntermediate>, Maybe<TResult>> g)
		{
			return (mt) => g(f(mt));
		}

		[Pure]
		public static Func<Maybe<T>, Maybe<TResult>> Bind<T, TIntermediate, TResult>(Func<T, Maybe<TIntermediate>> f, Func<TIntermediate, Maybe<TResult>> g)
		{
			return Combine(Maybe<T>.Bind(f), Maybe<TIntermediate>.Bind(g));
		}

		[Pure]
		public static Func<Maybe<TU>, Maybe<TResult>> Bind<TU, TIntermediate, TResult>(Func<TU, Maybe<TIntermediate>> f, Func<Maybe<TIntermediate>, Maybe<TResult>> g)
		{
			return Combine(Maybe<TU>.Bind(f), g);
		}

	}

	/// <summary>Represents an error captured during a computation</summary>
	/// <remarks>Wraps any exception captured or returned by a called method</remarks>
	public readonly struct MaybeError
	{

		private readonly object? m_errorContainer; // either an Exception, or an ExceptionDispatchInfo

		internal MaybeError(object? errorContainer)
		{
			Contract.Debug.Requires(errorContainer is null or Exception or ExceptionDispatchInfo);
			m_errorContainer = errorContainer;
		}

		public MaybeError(Exception error)
		{
			Contract.Debug.Requires(error != null);
			m_errorContainer = error;
		}

		public MaybeError(ExceptionDispatchInfo capturedError)
		{
			Contract.Debug.Requires(capturedError != null);
			m_errorContainer = capturedError;
		}

		internal object? Container => m_errorContainer;

		public Exception? Error => m_errorContainer switch
		{
			ExceptionDispatchInfo edi => edi.SourceException,
			Exception ex => ex,
			_ => null,
		};

		[StackTraceHidden]
		public void Throw()
		{
			switch (m_errorContainer)
			{
				case ExceptionDispatchInfo edi:
				{
					edi.Throw();
					throw null!;
				}
				case Exception ex:
				{
					throw ex;
				}
				default:
				{
					throw new InvalidOperationException("Computation yielded not results");
				}
			}
		}

	}

}
