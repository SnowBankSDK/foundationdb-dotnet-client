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

namespace SnowBank.Data.Json.Binary
{
	using SnowBank.Data.Json;
	using SnowBank.Data.Binary;

	/// <summary>Codecs that encodes CLR types into either database keys (ordered) or values (unordered)</summary>
	[PublicAPI]
	public static class CrystalJsonCodec
	{

		internal static readonly CrystalJsonSettings DefaultJsonSettings = CrystalJsonSettings.JsonCompact.WithEnumAsStrings().WithIso8601Dates();

		/// <summary>Returns a codec that will encode instances of type <typeparamref name="T"/> into JSON</summary>
		/// <param name="settings">Custom JSON serialization settings</param>
		/// <param name="resolver">Custom JSON resolver</param>
		public static CrystalJsonCodec<T> GetEncoder<T>(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

		/// <summary>Returns a codec that will encode instances of type <typeparamref name="T"/> into JSON</summary>
		/// <param name="converter">Custom JSON converter for type <typeparamref name="T"/></param>
		/// <param name="settings">Custom JSON serialization settings</param>
		/// <param name="resolver">Custom JSON resolver</param>
		public static CrystalJsonCodec<T> GetEncoder<T>(IJsonConverter<T> converter, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) => new(settings, resolver);

	}

	/// <summary>Codec that encodes <typeparamref name="T"/> instances into either database keys (ordered) or values (unordered)</summary>
	[PublicAPI]
	public class CrystalJsonCodec<T> : IValueEncoder<T>
	{

		private readonly CrystalJsonSettings m_settings;
		private readonly ICrystalJsonTypeResolver m_resolver;
		private readonly IJsonConverter<T>? m_converter;

		/// <summary>Constructs a <see cref="CrystalJsonCodec"/> using the default behavior</summary>
		public CrystalJsonCodec()
			: this(null, null)
		{ }

		/// <summary>Constructs a <see cref="CrystalJsonCodec"/> using custom settings and resolver</summary>
		public CrystalJsonCodec(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			m_settings = settings ?? CrystalJsonCodec.DefaultJsonSettings;
			m_resolver = resolver ?? CrystalJson.DefaultResolver;
		}

		/// <summary>Constructs a <see cref="CrystalJsonCodec"/> using a custom converter</summary>
		public CrystalJsonCodec(IJsonConverter<T> converter, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			m_converter = converter;
			m_settings = settings ?? CrystalJsonCodec.DefaultJsonSettings;
			m_resolver = CrystalJson.DefaultResolver;
		}

		/// <summary>Default JSON serialization settings used by this codec</summary>
		public CrystalJsonSettings Settings => m_settings;

		/// <summary>Default JSON type resolver used by this codec</summary>
		public ICrystalJsonTypeResolver Resolver => m_resolver;

		#region Value...

		/// <inheritdoc />
		public Slice EncodeValue(T? value)
		{
			return m_converter?.ToSlice(value, m_settings, m_resolver) ?? CrystalJson.ToSlice(value, m_settings, m_resolver);
		}

		/// <inheritdoc />
		public T? DecodeValue(Slice encoded)
		{
			return encoded.IsNullOrEmpty ? default!
				: m_converter != null ? m_converter.Unpack(encoded, m_resolver)
				: CrystalJson.Deserialize<T?>(encoded, default, m_settings, m_resolver);
		}

		#endregion

	}

}
