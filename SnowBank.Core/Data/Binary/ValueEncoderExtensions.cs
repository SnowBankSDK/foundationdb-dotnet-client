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

namespace SnowBank.Data.Binary
{

	/// <summary>Extension methods for working with value encoders</summary>
	public static class ValueEncoderExtensions
	{

		#region Encoding...

		/// <summary>Encodes an array of <typeparamref name="TValue"/> into an array of <typeparamref name="TStorage"/></summary>
		public static TStorage[] EncodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, params TValue[] values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			var slices = new TStorage[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}

			return slices;
		}

		/// <summary>Encodes a span of <typeparamref name="TValue"/> into an array of <typeparamref name="TStorage"/></summary>
		public static TStorage[] EncodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, params ReadOnlySpan<TValue> values)
		{
			Contract.NotNull(encoder);

			var slices = new TStorage[values.Length];
			for (int i = 0; i < values.Length; i++)
			{
				slices[i] = encoder.EncodeValue(values[i]);
			}

			return slices;
		}

		/// <summary>Encodes the values of a sequence of Key/Value pairs into a list of <typeparamref name="TStorage"/>, discarding the keys in the process</summary>
		[LinqTunnel]
		public static List<TStorage> EncodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, [InstantHandle] IEnumerable<TValue> values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			if (values is ICollection<TValue> coll)
			{
				var res = new List<TStorage>(coll.Count);
				foreach (var value in values)
				{
					res.Add(encoder.EncodeValue(value));
				}
				return res;
			}

			return values.Select(encoder.EncodeValue).ToList();
		}

		/// <summary>Encodes a sequence of <paramref name="items"/> into a list of <typeparamref name="TStorage"/> by extracting one field using the specified <paramref name="selector"/></summary>
		public static List<TStorage> EncodeValues<TValue, TStorage, TElement>(this IValueEncoder<TValue, TStorage> encoder, [InstantHandle] IEnumerable<TElement> items, [InstantHandle] Func<TElement, TValue> selector)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);
			Contract.NotNull(selector);

			if (items is ICollection<TElement> coll)
			{
				var res = new List<TStorage>(coll.Count);
				foreach (var item in items)
				{
					res.Add(encoder.EncodeValue(selector(item)));
				}
				return res;
			}

			return items.Select(item => encoder.EncodeValue(selector(item))).ToList();
		}

		/// <summary>Transforms a sequence of <typeparamref name="TValue"/> into a sequence of <typeparamref name="TStorage"/></summary>
		[LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<TValue> values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			return values.Select(encoder.EncodeValue);
		}

		/// <summary>Transforms the values a sequence of Key/Value pairs into a sequence of <typeparamref name="TStorage"/>, discarding the keys in the process</summary>
		[LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage, TAny>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<KeyValuePair<TAny, TValue>> items)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);

			return items.Select(item => encoder.EncodeValue(item.Value));
		}

		/// <summary>Transforms a sequence of <paramref name="items"/> into a sequence of <typeparamref name="TStorage"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[LinqTunnel]
		public static IEnumerable<TStorage> SelectValues<TValue, TStorage, TElement>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<TElement> items, Func<TElement, TValue> selector)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);
			Contract.NotNull(selector);

			return items.Select(item => encoder.EncodeValue(selector(item)));
		}

		#endregion

		#region Decoding...

		/// <summary>Decodes a span of <typeparamref name="TStorage"/> into an array of <typeparamref name="TValue"/></summary>
		public static TValue[] DecodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, params TStorage[] values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			var res = new TValue[values.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = encoder.DecodeValue(values[i])!;
			}
			return res;
		}

		/// <summary>Decodes an array of <typeparamref name="TStorage"/> into an array of <typeparamref name="TValue"/></summary>
		public static TValue[] DecodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, params ReadOnlySpan<TStorage> values)
		{
			Contract.NotNull(encoder);

			var res = new TValue[values.Length];
			for (int i = 0; i < res.Length; i++)
			{
				res[i] = encoder.DecodeValue(values[i])!;
			}
			return res;
		}

		/// <summary>Decodes the values from a sequence of Key/Value pairs into a list of <typeparamref name="TValue"/>, discarding the keys in the process.</summary>
		public static TValue[] DecodeValues<TValue, TStorage, TAny>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<KeyValuePair<TAny, TStorage>> items)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);

			switch (items)
			{
				case KeyValuePair<TAny, TStorage>[] array:
				{
					var res = new TValue[array.Length];
					for(int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(array[i].Value)!;
					}
					return res;
				}
				case ICollection<KeyValuePair<TAny, TStorage>> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var item in items)
					{
						res[i++] = encoder.DecodeValue(item.Value)!;
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var item in items)
					{
						res.Add(encoder.DecodeValue(item.Value)!);
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Decodes a sequence of <typeparamref name="TStorage"/> into a list of <typeparamref name="TValue"/></summary>
		public static TValue[] DecodeValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, [InstantHandle] IEnumerable<TStorage> values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			switch (values)
			{
				case TStorage[] arr:
				{
					var res = new TValue[arr.Length];
					for (int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(arr[i])!;
					}
					return res;
				}
				case ICollection<TStorage> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var value in values)
					{
						res[i++] = encoder.DecodeValue(value)!;
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var value in values)
					{
						res.Add(encoder.DecodeValue(value)!);
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Decodes a sequence of <paramref name="items"/> into a list of <typeparamref name="TValue"/> by extracting one field using the specified <paramref name="selector"/></summary>
		public static TValue[] DecodeValues<TValue, TStorage, TElement>(this IValueEncoder<TValue, TStorage> encoder, [InstantHandle] IEnumerable<TElement> items, [InstantHandle] Func<TElement, TStorage> selector)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);

			switch (items)
			{
				case TElement[] arr:
				{
					var res = new TValue[arr.Length];
					for (int i = 0; i < res.Length; i++)
					{
						res[i] = encoder.DecodeValue(selector(arr[i]))!;
					}
					return res;
				}
				case ICollection<TElement> coll:
				{
					var res = new TValue[coll.Count];
					int i = 0;
					foreach (var item in items)
					{
						res[i++] = encoder.DecodeValue(selector(item))!;
					}
					if (i != res.Length) throw new InvalidOperationException();
					return res;
				}
				default:
				{
					var res = new List<TValue>();
					foreach (var item in items)
					{
						res.Add(encoder.DecodeValue(selector(item))!);
					}
					return res.ToArray();
				}
			}
		}

		/// <summary>Transforms a sequence of slices back into a sequence of <typeparamref name="TValue"/>s, using a serializer (or the default serializer if none is provided)</summary>
		[LinqTunnel]
		public static IEnumerable<TValue?> SelectValues<TValue, TStorage>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<TStorage> values)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(values);

			return values.Select(encoder.DecodeValue);
		}

		/// <summary>Transforms the values from a sequence of Key/Value pairs, into another sequence of <typeparamref name="TValue"/>, discarding the keys in the process.</summary>
		[LinqTunnel]
		public static IEnumerable<TValue> SelectValues<TValue, TStorage, TAny>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<KeyValuePair<TAny, TStorage>> items)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);

			return items.Select(item => encoder.DecodeValue(item.Value)!);
		}

		/// <summary>Transform a sequence of <paramref name="items"/> into another sequence of <typeparamref name="TValue"/> by extracting one field using the specified <paramref name="selector"/></summary>
		[LinqTunnel]
		public static IEnumerable<TValue> SelectValues<TValue, TStorage, TElement>(this IValueEncoder<TValue, TStorage> encoder, IEnumerable<TElement> items, Func<TElement, TStorage> selector)
		{
			Contract.NotNull(encoder);
			Contract.NotNull(items);

			return items.Select(x => encoder.DecodeValue(selector(x))!);
		}

		#endregion

	}

}
