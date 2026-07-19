
namespace SnowBank.SourceAnalysis
{
	using System.Collections;
	using System.Collections.Generic;

	public static class ImmutableEquatableArray
	{
		public static ImmutableEquatableArray<T> ToImmutableEquatableArray<T>(this IEnumerable<T>? values) where T : IEquatable<T>
			=> values != null ? new(values) : ImmutableEquatableArray<T>.Empty;
	}
	
	/// <summary>Provides an immutable list implementation which implements sequence equality.</summary>
	public sealed class ImmutableEquatableArray<T> : IEquatable<ImmutableEquatableArray<T>>, IReadOnlyList<T>
		where T : IEquatable<T>
	{
		public static ImmutableEquatableArray<T> Empty { get; } = new([ ]);

		private readonly T[] Values;
		
		public T this[int index] => this.Values[index];
		public int Count => this.Values.Length;

		public ImmutableEquatableArray(T[]? values)
			=> this.Values = values ?? [ ];

		public ImmutableEquatableArray(IEnumerable<T>? values)
			=> this.Values = values?.ToArray() ?? [ ];

		public bool Equals(ImmutableEquatableArray<T>? other)
			=> other is not null && ((ReadOnlySpan<T>) this.Values).SequenceEqual(other.Values);

		public override bool Equals(object? obj)
			=> obj is ImmutableEquatableArray<T> other && this.Equals(other);

		public override int GetHashCode()
		{
			int hash = 0;
			foreach (var value in this.Values)
			{
				hash = CombineHash(hash, value?.GetHashCode() ?? 0);
			}

			return hash;
		}
        
		private static int CombineHash(int h1, int h2)
		{
			// RyuJIT optimizes this to use the ROL instruction
			// Related GitHub pull request: https://github.com/dotnet/coreclr/pull/1830
			uint rol5 = ((uint) h1 << 5) | ((uint) h1 >> 27);
			return ((int) rol5 + h1) ^ h2;
		}

		public bool Contains(T element) => Contains(element, EqualityComparer<T>.Default);

		public bool Contains(T element, IEqualityComparer<T> comparer)
		{
			foreach (var value in this.Values)
			{
				if (comparer.Equals(value, element))
				{
					return true;
				}
			}
			return false;
		}

		public Enumerator GetEnumerator() => new(this.Values);
		
		IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)this.Values).GetEnumerator();
		
		IEnumerator IEnumerable.GetEnumerator() => this.Values.GetEnumerator();

		public struct Enumerator : IEnumerator<T>
		{
			private readonly T[] Values;
			private int Index;

			internal Enumerator(T[] values)
			{
				this.Values = values;
				this.Index = -1;
			}

			public bool MoveNext()
			{
				int newIndex = this.Index + 1;

				if ((uint) newIndex < (uint) this.Values.Length)
				{
					this.Index = newIndex;
					return true;
				}

				return false;
			}

			public readonly T Current => this.Values[this.Index];
            
			object IEnumerator.Current => this.Current;

			void IEnumerator.Reset() => this.Index = -1;

			public void Dispose() { }
			
		}
		
	}
	
}
