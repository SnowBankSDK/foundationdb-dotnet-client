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

namespace SnowBank.Data.Tuples.Binary
{

	/// <summary>Represent a custom user type for the TuPack encoding</summary>
	[DebuggerDisplay("{ToString()},nq")]
	[PublicAPI]
	public sealed class TuPackUserType : IEquatable<TuPackUserType>, ISpanFormattable
	{

		/// <summary>Prefix for the Directory Layer subspace (0xFE)</summary>
		public const byte TypeDirectory = 0xFE;

		/// <summary>Prefix for the System subspace (0xFF)</summary>
		public const byte TypeSystem = 0xFF;

		/// <summary>Directory Layer (<c>`\xFE`</c>)</summary>
		public static readonly TuPackUserType Directory = new(TypeDirectory);

		/// <summary>Start of the System subspace (<c>`\xFF`</c>)</summary>
		public static readonly TuPackUserType System = new(TypeSystem);

		/// <summary>Start of the Special Key subspace (<c>`\xFF\xFF`</c>)</summary>
		public static readonly TuPackUserType Special = new(TypeSystem, Slice.FromByte(255));

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TuPackUserType(int type)
		{
			this.Type = type;
			this.Value = Slice.Nil;
		}

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public TuPackUserType(int type, Slice value)
		{
			this.Type = type;
			this.Value = value;
		}

		/// <summary>Byte header of this custom user type</summary>
		public readonly int Type;

		/// <summary>Payload bytes of this custom user type (optional)</summary>
		public readonly Slice Value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override string ToString() => ToString(null);

		public string ToString(string? format, IFormatProvider? formatProvider = null)
		{
			return string.Create(formatProvider, $"{this}");
		}

		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
		{
			switch (this.Type)
			{
				case TypeDirectory: return this.Value.IsNull ? "|Directory|".TryCopyTo(destination, out charsWritten) : destination.TryWrite($"|Directory:{this.Value:K}|", out charsWritten);
				case TypeSystem: return this.Value.IsNull ? "|System|".TryCopyTo(destination, out charsWritten) : destination.TryWrite($"|System:{this.Value:K}|", out charsWritten);
			}

			return this.Value.IsNull
				? destination.TryWrite($"|User-{this.Type:X02}|", out charsWritten)
				: destination.TryWrite($"|User-{this.Type:X02}:{this.Value:N}|", out charsWritten);
		}

		/// <summary>Returns a type that matches a System key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(Slice name) => new(TypeSystem, name);

		/// <summary>Returns a type that matches a System key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(ReadOnlySpan<byte> name) => new(TypeSystem, name.ToSlice());

		/// <summary>Returns a type that matches a System key (ex: <c>'\xFF/metadataVersion'</c>)</summary>
		/// <param name="name">Name of key, excluding the initial <c>\xFF</c> byte (ex: "/metadataVersion" instead of "\xFF/metadataVersion")</param>
		public static TuPackUserType SystemKey(string name) => new(TypeSystem, Slice.FromByteString(name));

		/// <summary>Returns a type that matches a Special key (ex: <c>'\xFF\xFF/status/json'</c>)</summary>
		/// <param name="name">Key, excluding the two initial <c>\xFF</c> bytes (ex: "/status/json" instead of "\xFF\xFF/status/json")</param>
		public static TuPackUserType SpecialKey(Slice name) => new(TypeSystem, Slice.FromByte(255).Concat(name));

		/// <summary>Returns a type that matches a Special key (ex: <c>'\xFF\xFF/status/json'</c>)</summary>
		/// <param name="name">Key, excluding the two initial <c>\xFF</c> bytes (ex: "/status/json" instead of "\xFF\xFF/status/json")</param>
		public static TuPackUserType SpecialKey(ReadOnlySpan<byte> name) => new(TypeSystem, Slice.FromByte(255).Concat(name));

		/// <summary>Returns a type that matches a Special key (ex: <c>'\xFF\xFF/status/json'</c>)</summary>
		/// <param name="name">Key, excluding the two initial <c>\xFF</c> bytes (ex: "/status/json" instead of "\xFF\xFF/status/json")</param>
		public static TuPackUserType SpecialKey(string name) => new(TypeSystem, Slice.FromByteString("\xFF" + name));

		/// <summary>Returns a custom user type with a given prefix (ex: <c>40</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		public static TuPackUserType Custom(byte type) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type) : throw ErrorTypeOutOfRange(type);

		/// <summary>Returns a custom user type with a given prefix and payload (ex: <c>40 XX YY ZZ ...</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		/// <param name="value">Payload for this type</param>
		public static TuPackUserType Custom(byte type, Slice value) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type, value) : throw ErrorTypeOutOfRange(type);

		/// <summary>Returns a custom user type with a given prefix and payload (ex: <c>40 XX YY ZZ ...</c>)</summary>
		/// <param name="type">Prefix for this type, which must be between <see cref="TupleTypes.UserType0"/> (0x40) and <see cref="TupleTypes.UserTypeF"/> (0x4F)</param>
		/// <param name="value">Payload for this type</param>
		public static TuPackUserType Custom(byte type, ReadOnlySpan<byte> value) => type is >= TupleTypes.UserType0 and <= TupleTypes.UserTypeF ? new(type, value.ToSlice()) : throw ErrorTypeOutOfRange(type);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static ArgumentOutOfRangeException ErrorTypeOutOfRange(byte type) => new(nameof(type), type, "Custom user types must be between 0x40 and 0x4F");

		#region Equality...

		public override bool Equals(object? obj) => obj is TuPackUserType ut && Equals(ut);

		public bool Equals(TuPackUserType? other)
		{
			if (other == null) return false;
			if (ReferenceEquals(this, other)) return true;
			return this.Type == other.Type && this.Value.Equals(other.Value);
		}

		public override int GetHashCode() => HashCode.Combine(this.Type, this.Value.GetHashCode());

		#endregion

	}

}
