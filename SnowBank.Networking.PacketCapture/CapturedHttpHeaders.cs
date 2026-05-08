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

namespace SnowBank.Networking.PacketCapture
{
	using System.Collections;
	using System.Globalization;
	using System.Runtime.CompilerServices;
	using Microsoft.Extensions.Primitives;
	using Microsoft.Net.Http.Headers;

	[DebuggerDisplay("Count={Items.Count}")]
	[PublicAPI]
	public class CapturedHttpHeaders : IHeaderDictionary, IJsonPackable, IJsonDeserializable<CapturedHttpHeaders>
	{

		internal static CapturedHttpHeaders Empty = new(new(StringComparer.Ordinal));

		private Dictionary<string, StringValues> Items { get; }

		public CapturedHttpHeaders(Dictionary<string, StringValues>? items = null)
		{
			this.Items = items ?? [ ];
		}

		public static void JsonSerialize(CapturedHttpHeaders headers, CrystalJsonWriter writer)
		{
			var state = writer.BeginObject();
			foreach (var kv in headers.Items)
			{
				// we always serialize as arrays of strings
				writer.WriteUnsafeName(kv.Key);
				var state2 = writer.BeginInlineArray();
				foreach (var s in kv.Value)
				{
					writer.WriteInlineFieldSeparator();
					writer.WriteValue(s);
				}

				writer.EndInlineArray(state2);
			}
			writer.EndObject(state);
		}

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			var obj = new JsonObject(this.Count, StringComparer.Ordinal);
			foreach (var kv in this.Items)
			{
				// we always serialize as arrays of strings
				obj[kv.Key] = kv.Value.Count switch
				{
					0 => JsonNull.Null, // not supposed to happen!
					1 => JsonArray.Create(JsonString.Return(kv.Value.ToString())),
					_ => JsonArray.FromValues(kv.Value.ToArray()),
				};
			}
			return obj;
		}

		static CapturedHttpHeaders IJsonDeserializable<CapturedHttpHeaders>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value.IsNullOrMissing()) return Empty;

			var obj = value.AsObject();
			if (obj.Count == 0) return Empty;

			var items = new Dictionary<string, StringValues>(StringComparer.Ordinal);
			foreach (var kv in obj)
			{
				// we allow single strings instead of arrays when deserializing
				switch (kv.Value)
				{
					case JsonArray arr:
					{
						items[kv.Key] = arr.Count switch
						{
							0 => StringValues.Empty,
							1 => new StringValues(arr.Get<string>(0)),
							_ => new StringValues(arr.ToArray<string>())
						};
						break;
					}
					case JsonString str:
					{
						items[kv.Key] = new StringValues(str.Value);
						break;
					}
					case JsonNull:
					{ // not supposed to happen!
						items[kv.Key] = StringValues.Empty;
						break;
					}
					case JsonNumber num:
					{ // not supposed to happen!
						items[kv.Key] = new StringValues(num.Literal);
						break;
					}
					default:
					{
						throw new JsonBindingException($"Expected array while deserializing {nameof(CapturedHttpHeaders)} but got {kv.Value.Type} instead.", kv.Value);
					}
				}
			}
			return new(items);
		}

		public int Count => this.Items.Count;

		public bool IsReadOnly => true;

		public StringValues this[string key]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.TryGetValue(key, out var values) ? values : StringValues.Empty;
		}

		StringValues IHeaderDictionary.this[string key]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.TryGetValue(key, out var values) ? values : StringValues.Empty;
			set => throw new NotSupportedException();
		}

		StringValues IDictionary<string, StringValues>.this[string key]
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Items.TryGetValue(key, out var values) ? values : StringValues.Empty;
			set => throw new NotSupportedException();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetValue(string key, out StringValues value) => this.Items.TryGetValue(key, out value);


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool ContainsKey(string key) => this.Items.ContainsKey(key);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public StringValues GetValue(string name) => this.Items.TryGetValue(name, out var values) ? values : StringValues.Empty;

		public void CopyTo(KeyValuePair<string, StringValues>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, StringValues>>) this.Items).CopyTo(array, arrayIndex);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ICollection<KeyValuePair<string, StringValues>>.Contains(KeyValuePair<string, StringValues> item) => this.Items.Contains(item);

		void IDictionary<string, StringValues>.Add(string key, StringValues value) => throw new NotSupportedException();

		bool IDictionary<string, StringValues>.Remove(string key) => throw new NotSupportedException();

		void ICollection<KeyValuePair<string, StringValues>>.Add(KeyValuePair<string, StringValues> item) => throw new NotSupportedException();

		void ICollection<KeyValuePair<string, StringValues>>.Clear() => throw new NotSupportedException();

		bool ICollection<KeyValuePair<string, StringValues>>.Remove(KeyValuePair<string, StringValues> item) => throw new NotSupportedException();

		public Dictionary<string, StringValues>.Enumerator GetEnumerator() => this.Items.GetEnumerator();
		IEnumerator<KeyValuePair<string, StringValues>> IEnumerable<KeyValuePair<string, StringValues>>.GetEnumerator() => this.Items.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => this.Items.GetEnumerator();

		public Dictionary<string, StringValues>.KeyCollection Keys => this.Items.Keys;
		ICollection<string> IDictionary<string, StringValues>.Keys => this.Items.Keys;

		public Dictionary<string, StringValues>.ValueCollection Values => this.Items.Values;
		ICollection<StringValues> IDictionary<string, StringValues>.Values => this.Items.Values;

		private long? m_contentLength;

		long? IHeaderDictionary.ContentLength
		{
			get => this.ContentLength;
			set => throw new NotSupportedException();
		}

		public long? ContentLength => m_contentLength ??= ParseLength(GetValue(HeaderNames.ContentLength));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static long? ParseLength(StringValues literal) => literal.Count == 1 ? long.Parse(literal[0]!, CultureInfo.InvariantCulture) : null;

		#region Accelerators...

		public StringValues CacheControl => this.Items.GetValueOrDefault(HeaderNames.CacheControl);

		public StringValues Connection => this.Items.GetValueOrDefault(HeaderNames.Connection);

		public StringValues ContentType => this.Items.GetValueOrDefault(HeaderNames.ContentType);

		public StringValues Date => this.Items.GetValueOrDefault(HeaderNames.Date);

		public StringValues ETag => this.Items.GetValueOrDefault(HeaderNames.ETag);

		public StringValues Host => this.Items.GetValueOrDefault(HeaderNames.Host);

		public StringValues IfModifiedSince => this.Items.GetValueOrDefault(HeaderNames.IfModifiedSince);

		public StringValues IfRange => this.Items.GetValueOrDefault(HeaderNames.IfRange);

		public StringValues Location => this.Items.GetValueOrDefault(HeaderNames.Location);

		public StringValues Range => this.Items.GetValueOrDefault(HeaderNames.Range);

		public StringValues Referrer => this.Items.GetValueOrDefault(HeaderNames.Referer);

		public StringValues UserAgent => this.Items.GetValueOrDefault(HeaderNames.UserAgent);

		// ReSharper disable once InconsistentNaming
		public StringValues TE => this.Items.GetValueOrDefault(HeaderNames.TE);

		public StringValues Via => this.Items.GetValueOrDefault(HeaderNames.Via);

		#endregion

		internal static StringValues ToStringValues(IEnumerable<string> values) => values switch
		{
			string[] arr => arr.Length switch
			{
				1 => new(arr[0]),
				0 => StringValues.Empty,
				_ => new(arr),
			},
			IList<string> list => list.Count switch
			{
				1 => new(list[0]),
				0 => StringValues.Empty,
				_ => new(list.ToArray()),
			},
			_ => new(values.ToArray()),
		};

		public static CapturedHttpHeaders Create(IHeaderDictionary items)
		{
			var res = new Dictionary<string, StringValues>(items.Count, StringComparer.Ordinal);
			foreach (var kv in items)
			{
				res.Add(kv.Key, kv.Value);
			}
			return new(res);
		}

		public static CapturedHttpHeaders Create(IEnumerable<KeyValuePair<string, StringValues>> items) => new(new(items, StringComparer.Ordinal));

		public static CapturedHttpHeaders Create(IEnumerable<KeyValuePair<string, IEnumerable<string>>> items)
		{
			var res = new Dictionary<string, StringValues>(StringComparer.Ordinal);
			foreach (var kv in items)
			{
				res[kv.Key] = ToStringValues(kv.Value);
			}
			return new(res);
		}

		public static Builder CreateBuilder() => new(new(StringComparer.Ordinal));

		public Builder ToBuilder() => new(new(this.Items, StringComparer.OrdinalIgnoreCase));

		public readonly struct Builder
		{

			public readonly Dictionary<string, StringValues> Items;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Builder(Dictionary<string, StringValues> items) => this.Items = items;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void AddValues(string name, IEnumerable<string> values) => this.Items[name] = ToStringValues(values);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void AddValues(string name, StringValues values) => this.Items[name] = values;

			public void Add(string name, string value)
			{
				if (!this.Items.TryGetValue(name, out var slot))
				{
					slot = new(value);
				}
				else
				{
					slot = StringValues.Concat(slot, value);
				}
				this.Items[name] = slot;
			}

			public CapturedHttpHeaders ToHeaders() => new(this.Items);

		}

	}

}
