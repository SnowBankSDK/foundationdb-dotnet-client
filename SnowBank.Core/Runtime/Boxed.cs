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

namespace SnowBank.Runtime
{
	using System.Globalization;

	/// <summary>Wraps any type of CLR objects</summary>
	/// <typeparam name="T">Type the wrapped object</typeparam>
	/// <remarks>Can be used to bpass a ref type to a generic type or method that only accept structs, or to workaround some limitations in generic type constraints</remarks>
	[DebuggerDisplay("[{Value}]")]
	public readonly struct Boxed<T> : IEquatable<Boxed<T>>, IComparable<Boxed<T>>, IEquatable<T>, IComparable<T>
	{
		public static Boxed<T> Default => default;

		public readonly T Value;

		public Boxed(T value)
		{
			this.Value = value;
		}

		public bool IsNull => this.Value == null;

		public T? OrDefault(T? defaultValue) => this.Value != null ? this.Value : defaultValue;

		/// <summary>Returns <see langword="true"/> if the type <typeparamref name="T"/> is compatible with the type <typeparamref name="TBase"/></summary>
		public bool IsA<TBase>() => typeof(TBase).IsAssignableFrom(typeof(T));

		/// <summary>Converts a value of type <typeparamref name="T"/> in to a value of type <typeparamref name="TTarget"/></summary>
		/// <remarks>If <typeparamref name="T"/> and <typeparamref name="TTarget"/> are parent/child we can do direct casting. Otherwise, we call <see cref="Convert.ChangeType(object?,System.Type)"/></remarks>
		public TTarget? Cast<TTarget>()
		{
			if (typeof(TTarget).IsAssignableFrom(typeof(T)))
			{
				return (TTarget?) (object?) this.Value;
			}
			return (TTarget?) Convert.ChangeType(this.Value, typeof(TTarget), CultureInfo.InvariantCulture);
		}

		public bool Equals(Boxed<T> other) => EqualityComparer<T>.Default.Equals(this.Value, other.Value);

		public bool Equals(T? other) => EqualityComparer<T>.Default.Equals(this.Value, other);

		public int CompareTo(Boxed<T> other) => Comparer<T>.Default.Compare(this.Value, other.Value);

		public int CompareTo(T? other) => Comparer<T>.Default.Compare(this.Value, other);

		public override bool Equals(object? obj) => obj switch
		{
			Boxed<T> boxed => Equals(boxed),
			T value => Equals(value),
			_ => false
		};

		public override int GetHashCode() => EqualityComparer<T>.Default.GetHashCode(this.Value!);

		public static bool operator==(Boxed<T> left, Boxed<T> right) => left.Equals(right);

		public static bool operator!=(Boxed<T> left, Boxed<T> right) => !left.Equals(right);

		public static bool operator ==(Boxed<T> left, T right) => left.Equals(right);

		public static bool operator !=(Boxed<T> left, T right) => !left.Equals(right);

		public static bool operator ==(T left, Boxed<T> right) => right.Equals(left);

		public static bool operator !=(T left, Boxed<T> right) => !right.Equals(left);

		public static implicit operator T(Boxed<T> value) => value.Value;

		public static implicit operator Boxed<T>(T value) => new(value);

	}

}
