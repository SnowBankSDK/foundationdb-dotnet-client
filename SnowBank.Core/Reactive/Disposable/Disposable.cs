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

namespace SnowBank.Reactive.Disposables
{
	using SnowBank.Runtime;

	/// <summary>Token that wraps any value into an <see cref="IDisposable"/> instance</summary>
	/// <typeparam name="T">Type of the wrapped value</typeparam>
	/// <remarks>The encapsulated value may or may not implement <see cref="IDisposable"/>. If it does, the wrapper can be configured to forward or block the caller to the inner <see cref="Dispose"/> method.</remarks>
	[DebuggerDisplay("Value={m_value}, Disposed={Disposed}")]
	public sealed class Disposable<T> : IDisposable<T>
	{

		#region Private Members...

		/// <summary>Placeholder used when there is nothing to do when the instance is disposed</summary>
		private static readonly Action<T> s_nop = (_) => { };

		/// <summary>Placeholder used when the encapsulated should also be disposed (if it implements <see cref="IDisposable"/>)</summary>
		private static readonly Action<T> s_dispose = (t) => (t as IDisposable)?.Dispose();

		/// <summary>Encapsulated value</summary>
		/// <remarks>The reference to the value is kept even after <see cref="Dispose"/> is called, so that we can still call <c>Equals</c> or <c>GetHashcode</c>!</remarks>
		private readonly T m_value;

		/// <summary>Action invoked when this instance is disposed.</summary>
		/// <remarks>
		/// <para>The value is either <see cref="s_nop"/> (nothing to do), <see cref="s_dispose"/> (forward Dispose to the encapsulated value), a user-provided custom callback, or <c>null</c> if <see cref="Dispose"/> has already been called once</para>
		/// <para>Set to <c>null</c> when <see cref="Dispose"/> is called the first time</para>
		/// </remarks>
		private Delegate? m_action;

		/// <summary>Additional user-provided state</summary>
		/// <remarks>Will be passed as parameter to the user-provided action, and set to <c>null</c> when <see cref="Dispose"/> is called the first time</remarks>
		private object? m_state;

		/// <summary>0 = alive, 1 = disposed</summary>
		private int m_disposed;

		#endregion

		#region Constructors...

		/// <summary>Encapsulates an object, with a lifetime that is tied to the wrapper's lifetime</summary>
		/// <param name="value">Instance that should be disposed (if it implements <see cref="IDisposable"/>) when the wrapper is itself disposed.</param>
		public Disposable(T value)
			: this(value, preventDisposition: false)
		{ }

		/// <summary>Encapsulates an object, with a lifetime that may or may not be tied to the wrapper's lifetime</summary>
		/// <param name="value">Instance that should be disposed (if it implements <see cref="IDisposable"/>) when the wrapper is itself disposed and <paramref name="preventDisposition"/> is <c>false</c>.</param>
		/// <param name="preventDisposition">If <c>true</c>, disposing the wrapper will <b>NOT</b> call <see cref="Dispose"/> on <paramref name="value"/></param>
		public Disposable(T value, bool preventDisposition)
		{
			m_value = value;
			m_action = !preventDisposition && value is IDisposable ? s_dispose : s_nop;
		}

		/// <summary>Encapsulates an object, with a custom cleanup handler</summary>
		/// <param name="value">Instance that will be encapsulated</param>
		/// <param name="onDispose">Action that will be called (only once) when the wrapper is disposed, with <paramref name="value"/> as its first argument.</param>
		public Disposable(T value, Action<T> onDispose)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
		}

		/// <summary>Encapsulates an object, with a custom cleanup handler</summary>
		/// <param name="value">Instance that will be encapsulated</param>
		/// <param name="onDispose">Action that will be called (only once) when the wrapper is disposed, with <paramref name="value"/> as its first argument, and <paramref name="state"/> as its second arguemnt.</param>
		/// <param name="state">Optional state that will be passed to the callback.</param>
		public Disposable(T value, Action<T, object?> onDispose, object? state)
		{
			Contract.NotNull(onDispose);
			m_value = value;
			m_action = onDispose;
			m_state = state;
		}

		#endregion

		#region Public Properties...

		/// <summary>Encapsulated value</summary>
		/// <exception cref="ObjectDisposedException">If the instance has already been disposed.</exception>
		public T Value
		{
			get
			{
				ThrowIfDisposed();
				return m_value;
			}
		}

		/// <summary>Tests if the instance has already been disposed (<c>true</c>), or if it is still alive (<c>false</c>)</summary>
		public bool Disposed => m_disposed != 0;

		#endregion

		#region Operators...

		public static implicit operator T?(Disposable<T>? disposable)
		{
			if (disposable == null) return default(T);
			return disposable.Value;
		}

		#endregion

		#region IDisposable Interface...

		public void Dispose()
		{
			if (Interlocked.CompareExchange(ref m_disposed, 1, 0) == 0)
			{
				var action = m_action;
				var state = m_state;
				m_action = null;
				m_state = null;
				if (action != null)
				{
					// the delegate can be either `Action<T>` or `Action<T, object?>`
					if (action is Action<T> simple)
					{
						simple(m_value);
					}
					else
					{
						((Action<T, object?>) action)(m_value, state);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfDisposed()
		{
			if (m_disposed != 0) throw FailObjectAlreadyDisposed();
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static Exception FailObjectAlreadyDisposed()
		{
			return ThrowHelper.ObjectDisposedException($"Cannot use already disposed object of type {typeof(T).GetFriendlyName()}");
		}

		#endregion

		#region IEquatable<...>

		public override int GetHashCode() => m_value?.GetHashCode() ?? -1;

		public override bool Equals(object? obj)
		{
			return obj switch
			{
				null => false,
				Disposable<T> disposable => EqualityComparer<T>.Default.Equals(m_value, disposable.m_value),
				T value => EqualityComparer<T>.Default.Equals(m_value, value),
				_ => false
			};
		}

		public override string ToString() => m_value?.ToString() ?? string.Empty;

		#endregion

	}

	/// <summary>Helper type that can create <see cref="IDisposable"/> wrappers</summary>
	public static class Disposable
	{

		/// <summary>Creates an <see cref="IDisposable" /> instance that invokes a custom callback when it is disposed</summary>
		[Pure]
		public static IDisposable Create(Action action)
		{
			return new AnonymousDisposable(action);
		}

		/// <summary>Creates an <see cref="IDisposable{T}"/> instance that wraps a value, and invokes a custom callback when it is disposed</summary>
		[Pure]
		public static IDisposable<T> Create<T>(T value, Action<T> action)
		{
			return new Disposable<T>(value, action);
		}

		/// <summary>Creates an <see cref="IDisposable{T}"/> instance that wraps a value, and invokes a custom callback when it is disposed</summary>
		[Pure]
		public static IDisposable<T> Create<T>(T value, Action<T, object?> action, object? state)
		{
			return new Disposable<T>(value, action, state);
		}

		/// <summary>Returns a "dummy" <see cref="IDisposable"/> instance that does nothing when it is disposed</summary>
		[Pure]
		public static IDisposable Empty()
		{
			return NullDisposable.Instance;
		}

	}

}
