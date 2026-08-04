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

	/// <summary>Lexical formatters for XML scalar values, for both output profiles</summary>
	/// <remarks>
	/// <para>Every form below was measured against a live <see cref="System.Runtime.Serialization.DataContractSerializer"/>
	/// at the design spike (<c>XmlContracts.cs</c>, <c>PrimitiveContracts.Create()</c>) and is reproduced verbatim by the
	/// <c>FormatDcs*</c> methods: these return exactly the text the emitter writes through
	/// <see cref="IXmlEmitter.WriteRawAscii(ReadOnlySpan{char})"/> for numbers, booleans, dates, durations, GUIDs and
	/// base64, or through <see cref="IXmlEmitter.WriteText(ReadOnlySpan{char})"/> for a URI's raw component text.</para>
	/// <para>The <see cref="XmlOutputProfile.Modern"/> wire keeps the identical lexical space for every type here except
	/// <see cref="char"/>: numbers, booleans, dates (<c>RoundtripKind</c>), durations (ISO 8601) GUIDs and base64 are
	/// already invariant or ISO forms, so every <c>FormatModern*</c> below simply forwards to its <c>FormatDcs*</c>
	/// counterpart. Only <see cref="FormatModernChar"/> diverges from <see cref="FormatDcsChar"/>: it writes the
	/// character itself instead of its DCS code-point encoding.</para>
	/// <para>Both names are exposed for every type, even where the two profiles agree, rather than collapsing to a
	/// single shared method name: the source generator selects a method purely from the resolved
	/// <see cref="XmlOutputProfile"/> of the container, uniformly across every member, without special-casing which
	/// primitive types happen to format identically in both profiles. The forwarding is one line and costs nothing at
	/// the call site (it inlines).</para>
	/// <para>Not covered here: plain <see cref="string"/> content (written straight through <c>WriteText</c>, no lexical
	/// transformation needed) and enum labels (resolved by the generator from the declared member names, mirroring
	/// <c>XmlEnumContract.Format</c> in the spike, not a lexical formatting rule).</para>
	/// </remarks>
	[PublicAPI]
	public static class CrystalXmlFormatters
	{

		#region Boolean...

		/// <summary>Formats a <see cref="bool"/> as <c>"true"</c> or <c>"false"</c></summary>
		public static string FormatDcsBoolean(bool value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsBoolean"/></summary>
		public static string FormatModernBoolean(bool value) => FormatDcsBoolean(value);

		#endregion

		#region Integers (invariant-culture decimal text, identical in both profiles)...

		/// <summary>Formats an <see cref="int"/> as invariant-culture decimal text</summary>
		public static string FormatDcsInt32(int value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsInt32"/></summary>
		public static string FormatModernInt32(int value) => FormatDcsInt32(value);

		/// <summary>Formats a <see cref="long"/> as invariant-culture decimal text</summary>
		public static string FormatDcsInt64(long value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsInt64"/></summary>
		public static string FormatModernInt64(long value) => FormatDcsInt64(value);

		/// <summary>Formats a <see cref="short"/> as invariant-culture decimal text</summary>
		public static string FormatDcsInt16(short value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsInt16"/></summary>
		public static string FormatModernInt16(short value) => FormatDcsInt16(value);

		/// <summary>Formats an <see cref="sbyte"/> as invariant-culture decimal text</summary>
		public static string FormatDcsSByte(sbyte value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsSByte"/></summary>
		public static string FormatModernSByte(sbyte value) => FormatDcsSByte(value);

		/// <summary>Formats a <see cref="byte"/> as invariant-culture decimal text</summary>
		public static string FormatDcsByte(byte value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsByte"/></summary>
		public static string FormatModernByte(byte value) => FormatDcsByte(value);

		/// <summary>Formats a <see cref="ushort"/> as invariant-culture decimal text</summary>
		public static string FormatDcsUInt16(ushort value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsUInt16"/></summary>
		public static string FormatModernUInt16(ushort value) => FormatDcsUInt16(value);

		/// <summary>Formats a <see cref="uint"/> as invariant-culture decimal text</summary>
		public static string FormatDcsUInt32(uint value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsUInt32"/></summary>
		public static string FormatModernUInt32(uint value) => FormatDcsUInt32(value);

		/// <summary>Formats a <see cref="ulong"/> as invariant-culture decimal text</summary>
		public static string FormatDcsUInt64(ulong value) => value.ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: identical to <see cref="FormatDcsUInt64"/></summary>
		public static string FormatModernUInt64(ulong value) => FormatDcsUInt64(value);

		#endregion

		#region Floating point and decimal (XmlConvert round-trip forms: "1.2E-09", "INF", "-INF", "NaN")...

		/// <summary>Formats a <see cref="float"/> using its XML Schema round-trip lexical form</summary>
		public static string FormatDcsSingle(float value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsSingle"/></summary>
		public static string FormatModernSingle(float value) => FormatDcsSingle(value);

		/// <summary>Formats a <see cref="double"/> using its XML Schema round-trip lexical form</summary>
		public static string FormatDcsDouble(double value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsDouble"/></summary>
		public static string FormatModernDouble(double value) => FormatDcsDouble(value);

		/// <summary>Formats a <see cref="decimal"/> using its XML Schema lexical form, which preserves scale (trailing zeros)</summary>
		public static string FormatDcsDecimal(decimal value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsDecimal"/></summary>
		public static string FormatModernDecimal(decimal value) => FormatDcsDecimal(value);

		#endregion

		#region DateTime (ISO 8601; suffix depends on Kind: none for Unspecified, "Z" for Utc, an offset for Local)...

		/// <summary>Formats a <see cref="System.DateTime"/> using <see cref="XmlDateTimeSerializationMode.RoundtripKind"/>: no suffix for <see cref="DateTimeKind.Unspecified"/>, a trailing <c>Z</c> for <see cref="DateTimeKind.Utc"/>, and the local UTC offset for <see cref="DateTimeKind.Local"/></summary>
		public static string FormatDcsDateTime(DateTime value) => XmlConvert.ToString(value, XmlDateTimeSerializationMode.RoundtripKind);

		/// <summary>Modern form: identical to <see cref="FormatDcsDateTime"/></summary>
		public static string FormatModernDateTime(DateTime value) => FormatDcsDateTime(value);

		#endregion

		#region TimeSpan (ISO 8601 duration)...

		/// <summary>Formats a <see cref="TimeSpan"/> as an ISO 8601 duration (e.g. <c>"PT1H33M30S"</c>)</summary>
		public static string FormatDcsDuration(TimeSpan value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsDuration"/></summary>
		public static string FormatModernDuration(TimeSpan value) => FormatDcsDuration(value);

		#endregion

		#region Guid...

		/// <summary>Formats a <see cref="Guid"/> in its lowercase hyphenated lexical form</summary>
		public static string FormatDcsGuid(Guid value) => XmlConvert.ToString(value);

		/// <summary>Modern form: identical to <see cref="FormatDcsGuid"/></summary>
		public static string FormatModernGuid(Guid value) => FormatDcsGuid(value);

		#endregion

		#region char (the one true divergence between the two profiles)...

		/// <summary>DCS form: the character's UTF-16 code unit as a decimal integer (e.g. <c>'A'</c> -&gt; <c>"65"</c>)</summary>
		public static string FormatDcsChar(char value) => ((int) value).ToString(CultureInfo.InvariantCulture);

		/// <summary>Modern form: the character itself, as a one-character string (e.g. <c>'A'</c> -&gt; <c>"A"</c>)</summary>
		public static string FormatModernChar(char value) => value.ToString();

		#endregion

		#region byte[] (base64)...

		/// <summary>Formats a byte array as base64 text</summary>
		public static string FormatDcsBase64(byte[] value)
		{
#if NET5_0_OR_GREATER
			ArgumentNullException.ThrowIfNull(value);
#else
			// ArgumentNullException.ThrowIfNull is not on netstandard2.0
			if (value is null) throw new ArgumentNullException(nameof(value));
#endif
			return Convert.ToBase64String(value);
		}

		/// <summary>Modern form: identical to <see cref="FormatDcsBase64"/></summary>
		public static string FormatModernBase64(byte[] value) => FormatDcsBase64(value);

		#endregion

		#region Uri (raw component text; XML escaping of & / < / > is the writer's job, not this method's)...

		/// <summary>
		/// Formats a <see cref="Uri"/> as its raw serialization-info component text, percent-escaped by
		/// <see cref="UriFormat.UriEscaped"/>. XML-significant characters that a URI can still legally contain (notably
		/// <c>&amp;</c>) are returned raw: escaping them into <c>&amp;amp;</c> is the emitter's job when this text is
		/// written through <see cref="IXmlEmitter.WriteText(ReadOnlySpan{char})"/>, not this method's.
		/// </summary>
		public static string FormatDcsUri(Uri value)
		{
#if NET5_0_OR_GREATER
			ArgumentNullException.ThrowIfNull(value);
#else
			// ArgumentNullException.ThrowIfNull is not on netstandard2.0
			if (value is null) throw new ArgumentNullException(nameof(value));
#endif
			return value.GetComponents(UriComponents.SerializationInfoString, UriFormat.UriEscaped);
		}

		/// <summary>Modern form: identical to <see cref="FormatDcsUri"/></summary>
		public static string FormatModernUri(Uri value) => FormatDcsUri(value);

		#endregion

	}

}
