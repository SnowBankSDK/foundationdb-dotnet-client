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

namespace SnowBank.Data.Xml
{
	using System.Globalization;
	using System.Xml;

	/// <summary>Lexical formatters for XML scalar values</summary>
	/// <remarks>
	/// <para>Every form below was measured against a live <see cref="System.Runtime.Serialization.DataContractSerializer"/>
	/// and reproduces its text exactly. The two output profiles share every lexical form except <see cref="char"/>, which is
	/// the only type with a per-profile pair (<see cref="FormatDcsChar"/> / <see cref="FormatModernChar"/>).</para>
	/// <para>Not covered here: plain <see cref="string"/> content (written straight through <c>WriteText</c>) and enum
	/// labels (resolved by the generator from the declared member names, not a lexical formatting rule).</para>
	/// </remarks>
	[PublicAPI]
	public static class CrystalXmlFormatters
	{

		/// <summary>Formats a <see cref="bool"/> as <c>"true"</c> or <c>"false"</c></summary>
		public static string FormatBoolean(bool value) => value ? "true" : "false";

		#region Integers (invariant-culture decimal text)...

		/// <summary>Formats an <see cref="sbyte"/> as invariant-culture decimal text</summary>
		public static string FormatSByte(sbyte value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="byte"/> as invariant-culture decimal text</summary>
		public static string FormatByte(byte value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="short"/> as invariant-culture decimal text</summary>
		public static string FormatInt16(short value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="ushort"/> as invariant-culture decimal text</summary>
		public static string FormatUInt16(ushort value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats an <see cref="int"/> as invariant-culture decimal text</summary>
		public static string FormatInt32(int value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="uint"/> as invariant-culture decimal text</summary>
		public static string FormatUInt32(uint value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="long"/> as invariant-culture decimal text</summary>
		public static string FormatInt64(long value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Formats a <see cref="ulong"/> as invariant-culture decimal text</summary>
		public static string FormatUInt64(ulong value) => value.ToString(CultureInfo.InvariantCulture);

		#endregion

		#region Floating point and decimal (XmlConvert round-trip forms: "1.2E-09", "INF", "-INF", "NaN")...

		/// <summary>Formats a <see cref="float"/> using its XML Schema round-trip lexical form</summary>
		public static string FormatSingle(float value) => XmlConvert.ToString(value);

		/// <summary>Formats a <see cref="double"/> using its XML Schema round-trip lexical form</summary>
		public static string FormatDouble(double value) => XmlConvert.ToString(value);

		/// <summary>Formats a <see cref="decimal"/> using its XML Schema lexical form, which preserves scale (trailing zeros)</summary>
		public static string FormatDecimal(decimal value) => XmlConvert.ToString(value);

		#endregion

		/// <summary>Formats a <see cref="System.DateTime"/> using <see cref="XmlDateTimeSerializationMode.RoundtripKind"/>: no suffix for <see cref="DateTimeKind.Unspecified"/>, a trailing <c>Z</c> for <see cref="DateTimeKind.Utc"/>, and the local UTC offset for <see cref="DateTimeKind.Local"/></summary>
		public static string FormatDateTime(DateTime value) => XmlConvert.ToString(value, XmlDateTimeSerializationMode.RoundtripKind);

		/// <summary>Formats a <see cref="TimeSpan"/> as an ISO 8601 duration (e.g. <c>"PT1H33M30S"</c>)</summary>
		public static string FormatDuration(TimeSpan value) => XmlConvert.ToString(value);

		/// <summary>Formats a <see cref="Guid"/> in its lowercase hyphenated lexical form</summary>
		public static string FormatGuid(Guid value) => XmlConvert.ToString(value);

		#region char (the one divergence between the two profiles)...

		/// <summary>DCS form: the character's UTF-16 code unit as a decimal integer (e.g. <c>'A'</c> -&gt; <c>"65"</c>)</summary>
		public static string FormatDcsChar(char value) => ((int) value).ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: the character itself, as a one-character string (e.g. <c>'A'</c> -&gt; <c>"A"</c>)</summary>
		public static string FormatModernChar(char value) => value.ToString();

		#endregion

		/// <summary>Formats a byte array as base64 text</summary>
		public static string FormatBase64(byte[] value)
		{
			Contract.NotNull(value);
			return Convert.ToBase64String(value);
		}

		/// <summary>
		/// Formats a <see cref="Uri"/> as its raw serialization-info component text, percent-escaped by
		/// <see cref="UriFormat.UriEscaped"/>. XML-significant characters that a URI can still legally contain (notably
		/// <c>&amp;</c>) are returned raw: escaping them into <c>&amp;amp;</c> is the emitter's job when this text is
		/// written through <see cref="ICrystalXmlEmitter.WriteText(ReadOnlySpan{char})"/>, not this method's.
		/// </summary>
		public static string FormatUri(Uri value)
		{
			Contract.NotNull(value);
			return value.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped);
		}

	}

}
