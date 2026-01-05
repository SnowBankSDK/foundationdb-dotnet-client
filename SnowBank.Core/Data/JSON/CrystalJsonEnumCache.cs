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
	using SnowBank.Runtime;

	/// <remarks>Cache pre-allocated <see cref="JsonString"/> for each values of an enum type</remarks>
	internal static class CrystalJsonEnumCache
	{

		/// <summary>Root table for all enum types cached so far</summary>
		/// <remarks>New enums are added by replacing this instance with another dictionary with the added data, using a retry loop.</remarks>
		private static Dictionary<Type, EnumCache> TypeCache = new(TypeEqualityComparer.Default);

		public sealed record EnumCache
		{

			public required Type Type { get; init; }

			public required Dictionary<Enum, JsonString> Literals { get; init; }
			//TODO: should we cache the camelCased variants as well?

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

			var data = new Dictionary<Enum, JsonString>(names.Length, EqualityComparer<Enum>.Default);
			//note: in case of duplicate value, we must keep only the first enrtry, to be inline with the behavior of ToString()
			// => the fastest way is to iterate the list in reverse order
			for (int i = names.Length - 1; i >= 0; i--)
			{
				var value = (Enum) values.GetValue(i)!;
				data[value] = new JsonString(enumType.GetEnumName(value)!);
			}
			var cache = new EnumCache { Type = enumType, Literals = data };

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
			var cache = GetCacheForType(enumType);
			if (cache.Literals.TryGetValue(value, out var literal))
			{
				return literal;
			}

			// for unknown values, still generate a string with the numerical value
			return new JsonString(value.ToString("d"));
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
			return GetName(typeof(TEnum), value);
		}

	}

}
