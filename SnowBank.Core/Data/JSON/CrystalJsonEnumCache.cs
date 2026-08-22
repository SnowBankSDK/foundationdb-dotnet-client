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

namespace SnowBank.Data.Json
{
	using System.Globalization;
	using System.Reflection;
	using SnowBank.Runtime;

	/// <summary>Per-enum-type cache used by both serialization routes (text writer and DOM) and by the string-to-enum bind path.</summary>
	/// <remarks>
	/// <para>Caches pre-allocated <see cref="JsonString"/> literals for each value of an enum type, in both the canonical and camelCased forms,
	/// plus a case-insensitive reverse map used to parse strings back into values.</para>
	/// <para>Custom output tokens are recognized on the enum's own fields: <c>[JsonStringEnumMemberName("...")]</c> (System.Text.Json 9+, matched
	/// by name so that no reference or version floor is required on any target) and <c>[EnumMember(Value = "...")]</c> (the DataContract spelling,
	/// which Newtonsoft's <c>StringEnumConverter</c> also honors). The STJ spelling wins when both are present. Names explicitly set via attributes
	/// are never camelCased, consistent with <c>JsonPropertyNameAttribute</c> semantics in System.Text.Json.</para>
	/// <para>The string form of a value without custom tokens is exactly <c>value.ToString("G")</c>: declared values render their name, undeclared
	/// flags combinations compose from declared fields (largest values first, rendered in ascending order), and anything else renders the numeric
	/// value as a string.</para>
	/// </remarks>
	internal static class CrystalJsonEnumCache
	{

		/// <summary>Root table for all enum types cached so far</summary>
		/// <remarks>New enums are added by replacing this instance with another dictionary with the added data, using a retry loop.</remarks>
		private static Dictionary<Type, EnumCache> TypeCache = new(TypeEqualityComparer.Default);

		public sealed record EnumCache
		{

			public required Type Type { get; init; }

			/// <summary>TypeCode of the enum's underlying integer type</summary>
			public required TypeCode UnderlyingCode { get; init; }

			/// <summary>The enum carries <c>[Flags]</c>, so undeclared values may compose from declared fields</summary>
			public required bool IsFlags { get; init; }

			/// <summary>At least one field carries a custom output token (<c>[JsonStringEnumMemberName]</c> or <c>[EnumMember(Value=...)]</c>)</summary>
			public required bool HasCustomTokens { get; init; }

			/// <summary>Canonical literal for each declared value: the custom token if any, otherwise the .NET name</summary>
			public required Dictionary<Enum, JsonString> Literals { get; init; }

			/// <summary>camelCased variant of <see cref="Literals"/>; attribute-set tokens are NOT camelCased</summary>
			public required Dictionary<Enum, JsonString> CamelCased { get; init; }

			/// <summary>Case-insensitive reverse map: tokens and .NET names to values</summary>
			public required Dictionary<string, Enum> Parser { get; init; }

			/// <summary>Non-zero declared fields, sorted by ascending unsigned value, used to compose flags combinations</summary>
			public required FlagField[] SortedFields { get; init; }

		}

		public readonly struct FlagField
		{
			public required ulong Bits { get; init; }
			public required string Literal { get; init; }
			public required string CamelCasedLiteral { get; init; }
		}

		/// <summary>Get the literal cache for a specific enum type</summary>
		public static EnumCache GetCacheForType(Type enumType)
		{
			var types = TypeCache;
			if (!types.TryGetValue(enumType, out var cache))
			{
				cache = AddEnumToCache(enumType);
			}
			return cache;
		}

		/// <summary>Get the literal cache for a specific enum type</summary>
		public static EnumCache GetCacheForType<TEnum>()
			where TEnum : struct, Enum
		{
			return GetCacheForType(typeof(TEnum));
		}

		/// <summary>Looks for custom output tokens on an enum field</summary>
		/// <remarks>
		/// <para>Recognition is by attribute name and namespace, so it works on every target (no STJ reference or version floor required),
		/// and also matches hand-written or generator-injected definitions of the same attributes.</para>
		/// <para>When both spellings are present, the System.Text.Json one is the canonical (written) token; the other is still accepted on read.</para>
		/// </remarks>
		private static void FindCustomTokens(FieldInfo field, out string? stjName, out string? enumMemberValue)
		{
			stjName = null;
			enumMemberValue = null;
			foreach (var attr in field.GetCustomAttributes(inherit: false))
			{
				var attrType = attr.GetType();
				if (attrType.Name == "JsonStringEnumMemberNameAttribute" && attrType.Namespace == "System.Text.Json.Serialization")
				{ // the System.Text.Json 9+ spelling
					if (attrType.GetProperty("Name")?.GetValue(attr) is string name && name.Length != 0)
					{
						stjName = name;
					}
				}
				else if (attr is System.Runtime.Serialization.EnumMemberAttribute em)
				{ // a bare [EnumMember] (no Value) keeps the .NET name, matching DataContract behavior
					if (em.Value is { Length: > 0 } value)
					{
						enumMemberValue = value;
					}
				}
				else if (attrType.Name == "EnumMemberAttribute" && attrType.Namespace == "System.Runtime.Serialization")
				{ // a hand-written or injected clone of the DataContract spelling
					if (attrType.GetProperty("Value")?.GetValue(attr) is string value && value.Length != 0)
					{
						enumMemberValue = value;
					}
				}
			}
		}

		/// <summary>Generates the literal cache for all the values of a specific Enum type</summary>
		private static EnumCache AddEnumToCache(Type enumType)
		{
			Contract.Debug.Requires(enumType != null);
			if (!typeof(Enum).IsAssignableFrom(enumType)) throw new InvalidOperationException($"Type {enumType.Name} is not a valid Enum type");

			var names = Enum.GetNames(enumType);
#pragma warning disable IL3050
			var values = Enum.GetValues(enumType);
#pragma warning restore IL3050
			Contract.Debug.Assert(names != null && values != null && names.Length == values.Length);

			int count = names.Length;
			var underlyingCode = Type.GetTypeCode(Enum.GetUnderlyingType(enumType));

			// collect the custom output tokens, if any: the canonical one (written), and any losing spelling (still accepted on read)
			var tokens = new string?[count];
			var altTokens = new string?[count];
			bool hasCustomTokens = false;
			for (int i = 0; i < count; i++)
			{
				var field = enumType.GetField(names[i], BindingFlags.Public | BindingFlags.Static);
				if (field == null) continue;
				FindCustomTokens(field, out var stjName, out var enumMemberValue);
				tokens[i] = stjName ?? enumMemberValue;
				altTokens[i] = stjName != null ? enumMemberValue : null;
				hasCustomTokens |= tokens[i] != null;
			}

			var literals = new Dictionary<Enum, JsonString>(count, EqualityComparer<Enum>.Default);
			var camelCased = new Dictionary<Enum, JsonString>(count, EqualityComparer<Enum>.Default);
			//note: in case of duplicate value, we must keep only the first entry, to be inline with the behavior of ToString()
			// => the fastest way is to iterate the list in reverse order
			for (int i = count - 1; i >= 0; i--)
			{
				var value = (Enum) values.GetValue(i)!;
				var token = tokens[i];
				var literal = new JsonString(token ?? names[i]);
				literals[value] = literal;
				// attribute-set tokens are never camelCased (JsonPropertyNameAttribute semantics)
				camelCased[value] = token != null ? literal : new JsonString(CrystalJsonWriter.CamelCase(names[i]));
			}

			// reverse map: tokens (all spellings) and names, case-insensitive; first declared spelling wins on collision
			var parser = new Dictionary<string, Enum>(count * 2, StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < count; i++)
			{
				var value = (Enum) values.GetValue(i)!;
				if (tokens[i] is string token && !parser.ContainsKey(token))
				{
					parser[token] = value;
				}
				if (altTokens[i] is string alt && !parser.ContainsKey(alt))
				{
					parser[alt] = value;
				}
				if (!parser.ContainsKey(names[i]))
				{
					parser[names[i]] = value;
				}
			}

			// composition table for flags: GetValues() is already sorted by ascending unsigned magnitude
			var fields = new List<FlagField>(count);
			for (int i = 0; i < count; i++)
			{
				ulong bits = ToBits((Enum) values.GetValue(i)!, underlyingCode);
				if (bits == 0) continue;
				fields.Add(new FlagField
				{
					Bits = bits,
					Literal = tokens[i] ?? names[i],
					CamelCasedLiteral = tokens[i] ?? CrystalJsonWriter.CamelCase(names[i]),
				});
			}

			var cache = new EnumCache
			{
				Type = enumType,
				UnderlyingCode = underlyingCode,
				IsFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false),
				HasCustomTokens = hasCustomTokens,
				Literals = literals,
				CamelCased = camelCased,
				Parser = parser,
				SortedFields = fields.ToArray(),
			};

			var sw = new SpinWait();
			while (true)
			{
				// ensure that no other thread has changed the root
				var types = TypeCache;
				if (types.TryGetValue(enumType, out var other))
				{ // someone added a table for the same type!
					return other;
				}

				// create a new root with the added enum table
				var update = new Dictionary<Type, EnumCache>(types, types.Comparer)
				{
					[enumType] = cache
				};

				// publish this as the new root
				if (Interlocked.CompareExchange(ref TypeCache, update, types) == types)
				{
					break;
				}

				// some other thread changed the root table, retry!
				sw.SpinOnce();
			}

			return cache;
		}

		#region Bits helpers...

		/// <summary>Extracts the bits of an enum value as an unsigned 64-bit pattern, regardless of the underlying type</summary>
		private static ulong ToBits(Enum value, TypeCode code) => code switch
		{
			TypeCode.SByte => unchecked((ulong) (sbyte) (object) value),
			TypeCode.Byte => (byte) (object) value,
			TypeCode.Int16 => unchecked((ulong) (short) (object) value),
			TypeCode.UInt16 => (ushort) (object) value,
			TypeCode.Int32 => unchecked((ulong) (int) (object) value),
			TypeCode.UInt32 => (uint) (object) value,
			TypeCode.Int64 => unchecked((ulong) (long) (object) value),
			TypeCode.UInt64 => (ulong) (object) value,
			_ => throw new InvalidOperationException($"Unsupported underlying type code {code}")
		};

		/// <summary>Converts an unsigned 64-bit pattern back into a (boxed) value of the enum type</summary>
		private static Enum FromBits(Type enumType, ulong bits, TypeCode code) => (Enum) (code switch
		{
			TypeCode.UInt64 => Enum.ToObject(enumType, bits),
			_ => Enum.ToObject(enumType, unchecked((long) bits))
		});

		#endregion

		#region Write side...

		/// <summary>Returns the string form of an enum value, as a (possibly cached) <see cref="JsonString"/></summary>
		/// <remarks>Equal to <c>value.ToString("G")</c> for enums without custom tokens; tokens replace the .NET names everywhere, including inside flags compositions.</remarks>
		[Pure]
		public static JsonString GetLiteral(Type enumType, Enum value, bool camelCased = false)
		{
			return GetLiteral(GetCacheForType(enumType), value, camelCased);
		}

		/// <summary>Returns the string form of an enum value, as a (possibly cached) <see cref="JsonString"/></summary>
		[Pure]
		public static JsonString GetLiteral(EnumCache cache, Enum value, bool camelCased = false)
		{
			var map = camelCased ? cache.CamelCased : cache.Literals;
			if (map.TryGetValue(value, out var literal))
			{
				return literal;
			}

			if (cache.IsFlags && TryComposeFlags(cache, value, camelCased) is string composed)
			{
				return new JsonString(composed);
			}

			// for unknown values, still generate a string with the numerical value
			return new JsonString(value.ToString("d"));
		}

		/// <summary>Returns the numeric form of an enum value, as a <see cref="JsonNumber"/> of its underlying type</summary>
		[Pure]
		public static JsonNumber GetNumber(Type enumType, Enum value)
		{
			return Type.GetTypeCode(Enum.GetUnderlyingType(enumType)) switch
			{
				TypeCode.SByte => JsonNumber.Create((sbyte) (object) value),
				TypeCode.Byte => JsonNumber.Create((byte) (object) value),
				TypeCode.Int16 => JsonNumber.Create((short) (object) value),
				TypeCode.UInt16 => JsonNumber.Create((ushort) (object) value),
				TypeCode.Int32 => JsonNumber.Create((int) (object) value),
				TypeCode.UInt32 => JsonNumber.Create((uint) (object) value),
				TypeCode.Int64 => JsonNumber.Create((long) (object) value),
				TypeCode.UInt64 => JsonNumber.Create((ulong) (object) value),
				var code => throw new InvalidOperationException($"Unsupported underlying type code {code}")
			};
		}

		/// <summary>Composes an undeclared flags combination from the declared fields, or returns <c>null</c> if some bits match no field</summary>
		/// <remarks>Replicates the <c>Enum.ToString("G")</c> algorithm: greedy match from the largest declared value down, rendered in ascending order.</remarks>
		private static string? TryComposeFlags(EnumCache cache, Enum value, bool camelCased)
		{
			ulong remaining = ToBits(value, cache.UnderlyingCode);
			if (remaining == 0) return null;

			var fields = cache.SortedFields;
			List<string>? found = null;
			for (int i = fields.Length - 1; i >= 0; i--)
			{
				ref readonly var field = ref fields[i];
				if ((remaining & field.Bits) == field.Bits)
				{
					(found ??= new(4)).Add(camelCased ? field.CamelCasedLiteral : field.Literal);
					remaining &= ~field.Bits;
					if (remaining == 0) break;
				}
			}
			if (remaining != 0 || found == null) return null;

			if (found.Count == 1) return found[0];
			found.Reverse();
			return string.Join(", ", found);
		}

		#endregion

		#region Read side...

		/// <summary>Parses the string form of an enum value: custom tokens, .NET names, comma-separated combinations of both, and numeric strings, all case-insensitively</summary>
		public static bool TryParse(EnumCache cache, string literal, [MaybeNullWhen(false)] out Enum result)
		{
			literal = literal.Trim();
			if (literal.Length == 0)
			{
				result = null;
				return false;
			}

			if (cache.Parser.TryGetValue(literal, out result!))
			{
				return true;
			}

			if (literal.IndexOf(',') >= 0)
			{ // a comma-separated combination of tokens, names and/or numbers
				ulong bits = 0;
				foreach (var part in literal.Split(','))
				{
					var p = part.Trim();
					if (p.Length == 0)
					{
						result = null;
						return false;
					}
					if (cache.Parser.TryGetValue(p, out var one))
					{
						bits |= ToBits(one, cache.UnderlyingCode);
					}
					else if (TryParseNumeric(cache, p, out one))
					{
						bits |= ToBits(one, cache.UnderlyingCode);
					}
					else
					{
						result = null;
						return false;
					}
				}
				result = FromBits(cache.Type, bits, cache.UnderlyingCode);
				return true;
			}

			return TryParseNumeric(cache, literal, out result);
		}

		private static bool TryParseNumeric(EnumCache cache, string literal, [MaybeNullWhen(false)] out Enum result)
		{
			char c = literal[0];
			if (char.IsDigit(c) || c == '-' || c == '+')
			{ // numeric strings are accepted, even for undeclared values (same as Enum.Parse)
				if (long.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var signed))
				{
					result = (Enum) Enum.ToObject(cache.Type, signed);
					return true;
				}
				if (ulong.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unsigned))
				{
					result = (Enum) Enum.ToObject(cache.Type, unsigned);
					return true;
				}
			}
			result = null;
			return false;
		}

		/// <summary>Parses the string form of an enum value, honoring custom tokens, and throwing on failure like <see cref="Enum.Parse(Type, string, bool)"/></summary>
		/// <remarks>For enums without custom tokens this is exactly <see cref="Enum.Parse(Type, string, bool)"/>, preserving its tolerance and its failure surface.</remarks>
		[Pure]
		public static object ParseBoxed(Type enumType, string literal)
		{
			var cache = GetCacheForType(enumType);
			if (cache.HasCustomTokens && TryParse(cache, literal, out var result))
			{
				return result;
			}
			return Enum.Parse(enumType, literal, ignoreCase: true);
		}

		/// <summary>Parses the string form of an enum value, honoring custom tokens, and throwing on failure like <see cref="Enum.Parse(Type, string, bool)"/></summary>
		/// <remarks>For enums without custom tokens this is exactly <see cref="Enum.Parse(Type, string, bool)"/>, preserving its tolerance and its failure surface.</remarks>
		[Pure]
		public static TEnum Parse<TEnum>(string literal)
			where TEnum : struct, Enum
		{
			var cache = GetCacheForType(typeof(TEnum));
			if (cache.HasCustomTokens && TryParse(cache, literal, out var result))
			{
				return (TEnum) (object) result;
			}
#if NET5_0_OR_GREATER
			return Enum.Parse<TEnum>(literal, ignoreCase: true);
#else
			// generic Enum.Parse<TEnum> is not on netstandard2.0
			return (TEnum) Enum.Parse(typeof(TEnum), literal, ignoreCase: true);
#endif
		}

		#endregion

		#region Legacy surface...

		/// <summary>Returns a <see cref="JsonValue"/> that corresponds to the text literal of an enum value</summary>
		/// <param name="enumType">Type of the enum</param>
		/// <param name="value">Value of the enum</param>
		/// <param name="literal">Cached of <see cref="JsonString"/> with the name of the value (if the value is defined in this enum)</param>
		/// <returns><c>true</c> if <paramref name="value"/> is defined in <paramref name="enumType"/>; otherwise, <c>false</c></returns>
		public static bool TryGetName(Type enumType, Enum value, [MaybeNullWhen(false)] out JsonString literal)
		{
			return GetCacheForType(enumType).Literals.TryGetValue(value, out literal);
		}

		/// <summary>Returns a <see cref="JsonValue"/> that corresponds to the text literal of an enum value</summary>
		/// <typeparam name="TEnum">Type of the enum</typeparam>
		/// <param name="value">Value of the enum</param>
		/// <param name="literal">Cached of <see cref="JsonString"/> with the name of the value (if the value is defined in this enum)</param>
		/// <returns><c>true</c> if <paramref name="value"/> is defined in <typeparamref name="TEnum"/>; otherwise, <c>false</c></returns>
		[Pure]
		public static bool TryGetName<TEnum>(TEnum value, [MaybeNullWhen(false)] out JsonString literal)
			where TEnum : struct, Enum
		{
			return GetCacheForType(typeof(TEnum)).Literals.TryGetValue(value, out literal);
		}

		/// <summary>Returns a <see cref="JsonValue"/> that corresponds to the text literal of an enum value</summary>
		/// <param name="enumType">Type of the enum</param>
		/// <param name="value">Value of the enum</param>
		/// <returns>Cached of <see cref="JsonString"/> with the name of the value (if the value is defined in this enum)</returns>
		/// <remarks>If <paramref name="value"/> is not defined in <paramref name="enumType"/>, a string with the numerical value is returned instead, which may or may not be cached!</remarks>
		[Pure]
		public static JsonString GetName(Type enumType, Enum value)
		{
			return GetLiteral(enumType, value);
		}

		/// <summary>Returns a <see cref="JsonValue"/> that corresponds to the text literal of an enum value</summary>
		/// <typeparamref name="TEnum">Type of the enum</typeparamref>
		/// <param name="value">Value of the enum</param>
		/// <returns>Cached of <see cref="JsonString"/> with the name of the value (if the value is defined in this enum)</returns>
		/// <remarks>If <paramref name="value"/> is not defined in <typeparamref name="TEnum"/>, a string with the numerical value is returned instead, which may or may not be cached!</remarks>
		[Pure]
		public static JsonString GetName<TEnum>(TEnum value)
			where TEnum : struct, Enum
		{
			return GetLiteral(typeof(TEnum), value);
		}

		#endregion

	}

}
