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

namespace SnowBank.Buffers
{
	using System.Text;

	/// <summary>Bi-directional mapping table between the binary (<see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;<typeparamref name="TRune"/>&gt;</see>) and literal (<typeparamref name="TLiteral"/>) representation of a <typeparamref name="TToken">token</typeparamref></summary>
	/// <typeparam name="TToken">Type of the token</typeparam>
	/// <typeparam name="TLiteral">Type of the literal representation of the token.</typeparam>
	/// <typeparam name="TRune">Element of the binary representation of the token.</typeparam>
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public abstract class TokenMap<TToken, TLiteral, TRune> : IEnumerable<KeyValuePair<TLiteral, TToken>>
		where TLiteral : IEquatable<TLiteral>
		where TRune : struct, IEquatable<TRune>
	{

		/// <summary>Map: byte[] => Token</summary>
		protected TokenDictionary<TToken, TLiteral, TRune> Tokens { get; }

		/// <summary>Map: string => Token </summary>
		protected Dictionary<TLiteral, TToken> Literals { get; }

		protected TokenMap(TokenDictionary<TToken, TLiteral, TRune> tokens, Dictionary<TLiteral, TToken> literals)
		{
			this.Tokens = tokens;
			this.Literals = literals;
		}

		public int Count => this.Literals.Count;

		public IEnumerable<TLiteral> Keys => this.Literals.Keys;

		public IEnumerable<TToken> Values => this.Literals.Values;

		public bool ContainsKey(TLiteral token) => this.Literals.ContainsKey(token);

		public bool ContainsKey(ReadOnlySpan<TRune> token) => this.Tokens.ContainsKey(token);

		/// <summary>Looks up a token from its binary representation</summary>
		public bool TryGetValue(in ReadOnlyMemory<TRune> token, [MaybeNullWhen(false)] out TToken value) => this.Tokens.TryGetValue(token, out value);

		/// <summary>Looks up a token from its binary representation</summary>
		public bool TryGetValue(in ReadOnlySpan<TRune> token, [MaybeNullWhen(false)] out TToken value) => this.Tokens.TryGetValue(token, out value);

		/// <summary>Looks up a token from its textual representation</summary>
		public bool TryGetValue(TLiteral literal, [MaybeNullWhen(false)] out TToken value) => this.Literals.TryGetValue(literal, out value);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		protected abstract TLiteral GetLiteral(in ReadOnlyMemory<TRune> token);

		protected abstract ReadOnlyMemory<TRune> GetRunes(TLiteral token);

		public void Add(in ReadOnlyMemory<TRune> token, TToken value)
		{
			var key = GetLiteral(token);
			this.Literals.Add(key, value);
			this.Tokens.Add(token, value);
		}

		public void Add(TLiteral token, TToken value)
		{
			var key = GetRunes(token);
			this.Literals.Add(token, value);
			this.Tokens.Add(key, value);
		}
		
		public Dictionary<TLiteral, TToken>.Enumerator GetEnumerator() => this.Literals.GetEnumerator();

		IEnumerator<KeyValuePair<TLiteral, TToken>> IEnumerable<KeyValuePair<TLiteral, TToken>>.GetEnumerator() => this.Literals.GetEnumerator();

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => this.Literals.GetEnumerator();

	}

	/// <summary>Bi-directional mapping table between the in-memory (<see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;<see cref="char"/>&gt;</see>) and literal (<see cref="string"/>) representation of a <typeparamref name="TToken">token</typeparamref></summary>
	/// <typeparam name="TToken">Type of the token</typeparam>
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public class ByteStringTokenMap<TToken> : TokenMap<TToken, string, byte>
	{

		public new ByteStringTokenDictionary<TToken> Tokens => (ByteStringTokenDictionary<TToken>) base.Tokens;
		
		public ByteStringTokenMap() : this(0, Encoding.UTF8)
		{ }

		public ByteStringTokenMap(ByteStringTokenMap<TToken> parent)
			: base(
				new ByteStringTokenDictionary<TToken>(parent.Tokens),
				new Dictionary<string, TToken>(parent.Literals, parent.Literals.Comparer)
			)
		{ }
		
		public ByteStringTokenMap(int capacity, Encoding? encoding = null)
			: base(
				new ByteStringTokenDictionary<TToken>(capacity, encoding),
				new Dictionary<string, TToken>(capacity, StringComparer.Ordinal)
			)
		{ }

		public ByteStringTokenMap(TToken[] items, Func<TToken, string> literal, Encoding? encoding = null)
			: base(
				new ByteStringTokenDictionary<TToken>(items, literal, encoding),
				items.ToDictionary(literal, StringComparer.Ordinal)
			)
		{ }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add(Slice token, TToken value)
		{
			Add(token.Memory, value);
		}
		
		protected override string GetLiteral(in ReadOnlyMemory<byte> token)
		{
			return token.Length == 0 ? string.Empty : this.Tokens.Encoding.GetString(token.Span);
		}

		protected override ReadOnlyMemory<byte> GetRunes(string token)
		{
			return this.Tokens.Encoding.GetBytes(token).AsMemory();
		}
		
	}

	/// <summary>Bi-directional mapping table between the encoded (<see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;<see cref="byte"/>&gt;</see>) and literal (<see cref="string"/>) representation of a <typeparamref name="TToken">token</typeparamref></summary>
	/// <typeparam name="TToken">Type of the token</typeparam>
#if NET9_0_OR_GREATER
	[Obsolete("Use Dictionary<TKey, TValue>.AlternateLookup<TSpan> (.NET 9+) instead.", error: true)]
#endif
	public class CharStringTokenMap<TToken> : TokenMap<TToken, string, char>
	{

		public CharStringTokenMap()
			: base(
				new CharStringTokenDictionary<TToken>(),
				new Dictionary<string, TToken>(StringComparer.Ordinal)
			)
		{ }

		public CharStringTokenMap(TToken[] items, Func<TToken, string> literal)
			: base(
				new CharStringTokenDictionary<TToken>(items, literal),
				items.ToDictionary(literal, StringComparer.Ordinal)
			)
		{ }

		protected override string GetLiteral(in ReadOnlyMemory<char> token)
		{
			return token.ToString();
		}

		protected override ReadOnlyMemory<char> GetRunes(string token)
		{
			return token.AsMemory();
		}
		
	}

}
