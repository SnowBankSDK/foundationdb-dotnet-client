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

namespace FoundationDB.Client
{
	/// <summary>Represents the name of a <see cref="IFdbTenant">Tenant</see> in the database</summary>
	/// <remarks>A tenant is represented by a binary name, that is mapped into a common key prefix by the database cluster</remarks>
	/// <example><code>
	/// var name1 = FdbTenantName.Create(Slice.FromStringUtf8("HelloWorld"));
	/// var name2 = FdbTenantName.FromParts("AnotherApp", "Contoso", 456));
	/// var name3 = FdbTenantName.FromTuple(("MyAwesomeApp", "ACME", 123));
	/// </code></example>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct FdbTenantName : IEquatable<FdbTenantName>, IComparable<FdbTenantName>
	{

		public static readonly FdbTenantName None = default;

		/// <summary>Packed binary representation of the name</summary>
		internal readonly Slice Value;

		/// <summary>If the name was produced from a tuple, copy of this tuple</summary>
		/// <remarks>Used to produce a human-readable ToString() and helps during debugging</remarks>
		private readonly IVarTuple? Tuple;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private FdbTenantName(Slice value, IVarTuple? tuple)
		{
			this.Value = value;
			this.Tuple = tuple;
		}

		/// <summary>Test if this name is a valid tenant name</summary>
		/// <returns><see langword="true"/> if the name is not empty and does not start with <c>\xFF</c></returns>
		public bool IsValid => IsValidName(this.Value.Span);

		public static Slice EnsureIsValidName(ReadOnlySpan<byte> name, string? paramName = null)
		{
			if (!IsValidName(name))
			{
				if (name.Length == 0) throw new ArgumentException("Tenant name cannot be empty.", paramName ?? nameof(name));
				if (name[0] == 0xFF) throw new ArgumentException("Tenant name cannot start with byte literal 0xFF.", paramName ?? nameof(name));
				throw new ArgumentException("Tenant nant is invalid", paramName ?? nameof(name));
			}
			return Slice.FromBytes(name);
		}

		public static Slice EnsureIsValidName(Slice name, string? paramName = null)
		{
			if (!IsValidName(name.Span))
			{
				if (name.Count == 0) throw new ArgumentException("Tenant name cannot be empty.", paramName ?? nameof(name));
				if (name[0] == 0xFF) throw new ArgumentException("Tenant name cannot start with byte literal 0xFF.", paramName ?? nameof(name));
				throw new ArgumentException("Tenant nant is invalid", paramName ?? nameof(name));
			}
			return name.Copy();
		}

		/// <summary>Test if a tenant name is valid</summary>
		/// <returns><see langword="true"/> if the name is not empty and does not start with <c>\xFF</c></returns>
		public static bool IsValidName(ReadOnlySpan<byte> name)
		{
			// cannot be empty
			if (name.Length == 0) return false;

			// cannot start with \xFF
			if (name[0] == 0xFF) return false;

			return true;
		}

		/// <summary>Try decoding the name as a Tuple</summary>
		/// <param name="tuple">Receives the decoded tuple, if the method returns <see langword="true"/></param>
		/// <returns>Returns <see langword="true"/> if the name is a valid tuple encoding; otherwise, <see langword="false"/></returns>
		public bool TryGetTuple([MaybeNullWhen(false)] out IVarTuple tuple)
		{
			// if we already know the tuple, return it as-is
			if (this.Tuple != null)
			{
				tuple = this.Tuple;
				return true;
			}

			// maybe the slice is a valid tuple?
			return TuPack.TryUnpack(this.Value, out tuple);
		}

		#region Factory methods...

		/// <summary>Create a tenant name from an opaque sequence of bytes</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(Slice name)
		{
			var copy = EnsureIsValidName(name, nameof(name));
			return new FdbTenantName(copy, TuPack.TryUnpack(copy, out var tuple) ? tuple : null);
		}

		/// <summary>Create a tenant name from an opaque sequence of bytes</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(ReadOnlyMemory<byte> name) => MemoryMarshal.TryGetArray(name, out var seg) ? Create(seg.AsSlice()) : Create(name.Span);

		/// <summary>Create a tenant name from an opaque sequence of bytes</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Create(ReadOnlySpan<byte> name)
		{
			var copy = EnsureIsValidName(name, nameof(name));
			return new FdbTenantName(copy, TuPack.TryUnpack(copy, out var tuple) ? tuple : null);
		}

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<TTuple>([NotNull] in TTuple? items) where TTuple : IVarTuple
		{
			return items?.Count > 0
				? new(TuPack.Pack(in items), items)
				: throw new ArgumentException("Tenant name cannot be empty", nameof(items));
		}

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<T1>(ValueTuple<T1> items)=> FromTuple((STuple<T1>) items);

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<T1, T2>(ValueTuple<T1, T2> items)=> FromTuple((STuple<T1, T2>) items);

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<T1, T2, T3>(ValueTuple<T1, T2, T3> items)=> FromTuple((STuple<T1, T2, T3>) items);

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<T1, T2, T3, T4>(ValueTuple<T1, T2, T3, T4> items)=> FromTuple((STuple<T1, T2, T3, T4>) items);

		/// <summary>Create a tenant name from an N-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromTuple<T1, T2, T3, T4, T5>(ValueTuple<T1, T2, T3, T4, T5> items)=> FromTuple((STuple<T1, T2, T3, T4, T5>) items);

		/// <summary>Create a tenant name from a 1-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromParts<T1>(T1 item1) => FromTuple(STuple.Create(item1));

		/// <summary>Create a tenant name from a 2-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromParts<T1, T2>(T1 item1, T2 item2) => FromTuple(STuple.Create(item1, item2));

		/// <summary>Create a tenant name from a 3-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromParts<T1, T2, T3>(T1 item1, T2 item2, T3 item3) => FromTuple(STuple.Create(item1, item2, item3));

		/// <summary>Create a tenant name from a 4-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromParts<T1, T2, T3, T4>(T1 item1, T2 item2, T3 item3, T4 item4) => FromTuple(STuple.Create(item1, item2, item3, item4));

		/// <summary>Create a tenant name from a 5-tuple</summary>
		/// <remarks>The tuple is <see cref="TuPack">packed</see> to generate the actual name</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName FromParts<T1, T2, T3, T4, T5>(T1 item1, T2 item2, T3 item3, T4 item4, T5 item5) => FromTuple(STuple.Create(item1, item2, item3, item4, item5));

		/// <summary>Create a copy of this name</summary>
		/// <returns>Name that contains the same bytes as the original</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal FdbTenantName Copy() => new(this.Value.Copy(), this.Tuple);

		/// <summary>Create a copy of a tenant name</summary>
		/// <returns>Name that contains the same bytes as the original</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static FdbTenantName Copy(FdbTenantName name) => new (name.Value.Copy(), name.Tuple);

		#endregion

		/// <summary>Return a copy of the binary representation of the name</summary>
		public Slice ToSlice() => this.Value.Copy();

		/// <summary>Return a hyman-readable name for this tenant name</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => this.Tuple?.ToString() ?? (this.Value.HasValue ? this.Value.PrettyPrint() : "global");

		#region Equality / Comparison ...

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbTenantName other) => this.Value.Equals(other.Value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object? obj) => obj is FdbTenantName other && Equals(other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode() => this.Value.GetHashCode();

		public int CompareTo(FdbTenantName other) => Value.CompareTo(other.Value);

		public sealed class Comparer : IEqualityComparer<FdbTenantName>, IComparer<FdbTenantName>
		{

			public static readonly Comparer Default = new Comparer();

			private Comparer() { }

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(FdbTenantName x, FdbTenantName y) => x.Value.Equals(y.Value);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(FdbTenantName obj) => obj.Value.GetHashCode();

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(FdbTenantName x, FdbTenantName y) => x.Value.CompareTo(y.Value);

		}

		#endregion

	}

}
