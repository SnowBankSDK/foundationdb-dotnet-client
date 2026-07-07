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

namespace SnowBank.Text
{
	using System.Collections.Specialized;
	using System.Globalization;
	using System.Text;
	using System.Text.Encodings.Web;
	using System.Web;
	using SnowBank.Runtime.Converters;

	/// <summary>Helper for encoding/decoding URIs</summary>
	/// <remarks>This method is intended to be used when System.Web.HttpUtility.dll is not available</remarks>
	[PublicAPI]
	public static class UrlEncoding
	{

		private static class Tokens
		{
			public const string True = "true";
			public const string False = "false";

			public const string FormatR = "R";
			public const string FormatDate = "yyyyMMdd";
			public const string FormatDateTime = "yyyyMMddHHmmss";
			public const string FormatDateTimeMillis = "yyyyMMddHHmmssfff";
		}

		#region Static Members...

		private const byte CLEAN = 0; // Never modified
		private const byte PATH = 1; // Normally Percent-encoded, but special handling?
		private const byte SPACE = 2; // Either '+' or '%20'
		private const byte DELIM = 3; // Path delimiter ('/', ':', ...)
		private const byte INVALID = 4; // "%XX"
		private const byte UNICODE = 5;

		#endregion

		#region Public Methods...

		/// <summary>Decodes a text string encoded as a URL (%XX)</summary>
		/// <param name="value">String containing encoded text</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		/// <returns>Decoded string</returns>
		[Pure]
		public static string Decode(string? value, Encoding? encoding = null)
		{
			return Decode(value, 0, value?.Length ?? 0, encoding);
		}

		/// <summary>Decodes a section of a text string encoded as a URL (%XX)</summary>
		/// <param name="value">String containing a URI or any other text encoded as a URL</param>
		/// <param name="offset">Offset from the start of the string</param>
		/// <param name="count">Number of characters to decode</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		/// <returns>Decoded section of the string</returns>
		[Pure]
		public static string Decode(string? value, int offset, int count, Encoding? encoding = null)
		{
			if (value == null || count <= 0)
			{
				return string.Empty;
			}
			if (NeedsDecoding(value, offset, count))
			{
				return DecodeString(value, offset, count, encoding);
			}
			if (offset == 0 && count == value.Length)
			{
				return value;
			}
			return value.Substring(offset, count);
		}

		/// <summary>Parses a QueryString, and passes each (attribute, value) pair to a lambda</summary>
		/// <typeparam name="TState">Type of the state passed to the handler (buffer, list, ...)</typeparam>
		/// <param name="qs">QueryString to parse (in the form 'name1=value1&amp;name2=value2&amp;...')</param>
		/// <param name="state">Variable passed to each call of the handler</param>
		/// <param name="handler">Action called for each parameter, with the name/value pair (decoded). The value is null if the parameter has no '=xxxx' section</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		[Pure]
		internal static TState ParseQueryString<TState>(string? qs, TState state, Action<TState, string, string?> handler, Encoding? encoding = null)
		{
			int length;
			if (qs == null || (length = qs.Length) == 0) return state;

			// start from the beginning, unless there is a '?'
			int start = 0;
			if (qs[0] == '?') ++start; // skip

			for (int i = start; i < length; i++)
			{
				start = i;
				int end = -1;

				// look for the end of the 'attr=name' pair (terminated by a '&' or the end of the string)
				while (i < length)
				{
					char c = qs[i];
					if (c == '=')
					{ // end of the name, start of the value
						if (end < 0) end = i;
					}
					else if (c == '&')
					{ // end of the pair
						break;
					}
					++i;
				}

				if (start == i)
				{ // a "&" wandering around on its own ??
					continue;
				}

				if (end < 0)
				{ // no value
					handler(state, Decode(qs, start, i - start, encoding), null);
				}
				else
				{ // value present
					handler(state, Decode(qs, start, end - start, encoding), Decode(qs, end + 1, i - end - 1, encoding));
				}
			}
			return state;
		}

		/// <summary>Parses a QueryString, and returns the list of parameters found</summary>
		/// <param name="qs">QueryString to parse (in the form 'name1=value1&amp;name2=value2&amp;...')</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		/// <returns>NameValueCollection containing the parameters of the querystring</returns>
		/// <remarks>"foo&amp;..." will contain null, "foo=&amp;..." will contain String.Empty</remarks>
		[Pure]
		public static NameValueCollection ParseQueryString(string? qs, Encoding? encoding = null)
		{
			return ParseQueryString(qs, new NameValueCollection(), (values, name, value) => values.Add(name, value), encoding);
		}

		/// <summary>Decodes a text string containing a URL</summary>
		/// <param name="value">String to decode</param>
		/// <param name="offset">Offset from the start of the string</param>
		/// <param name="count">Number of characters to decode</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		/// <returns>Decoded section of the url</returns>
		[Pure]
		private static string DecodeString(string value, int offset, int count, Encoding? encoding)
		{
			encoding ??= Encoding.UTF8;

			// if there is nothing to decode, the output buffer size is the same as the string's
			// if there is something, it will be smaller, down to 1/3 of the size in the worst case

			unsafe
			{
				fixed (char* chars = value)
				{
					if (count > 1024)
					{ // too big to allocate on the stack
						// => allocate on the heap
						var buffer = new byte[count];
						int size;
						fixed (byte* bytes = buffer)
						{
							size = DecodeBytes(chars, offset, count, bytes, encoding);
						}
						return encoding.GetString(buffer, 0, size);
					}
					else
					{ // this can fit on the stack
						// decode into a buffer on the stack
						byte* bytes = stackalloc byte[count];
						int numBytes = DecodeBytes(chars, offset, count, bytes, encoding);
						// determine the number of characters
						int numChars = encoding.GetCharCount(bytes, numBytes);
						// allocate the char buffer (on the stack as well)
						char* result = stackalloc char[numChars];
						int n = encoding.GetChars(bytes, numBytes, result, numChars);
						// return the corresponding string
						return new string(result, 0, n);
					}
				}
			}
		}

		/// <summary>Determines whether the string needs to be decoded (pessimistically)</summary>
		/// <param name="value">Text string present in a URL</param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns>True if the string (possibly) contains characters to encode, false if it is clean.</returns>
		[ContractAnnotation("value:null => false")]
		private static bool NeedsDecoding(string? value, int offset, int count)
		{
			if (value != null)
			{
				int p = offset;
				while (count-- > 0)
				{
					char c = value[p++];
					if (c == '%' || c == '+') return true;
				}
			}
			return false;
		}

		/// <summary>Returns the decimal value of a hexadecimal digit, or -1 if it is not one</summary>
		/// <param name="c">0-9, A-F, a-f</param>
		/// <returns>0-15, or -1 if it is not a hexadecimal digit</returns>
		private static int DecodeHexDigit(char c)
		{
			// we accept A-F, a-f and 0-9
			if (c < '0') return -1;
			if (c <= '9') return c - 48;
			if (c >= 'A' && c <= 'F') return c - 55;
			if (c >= 'a' && c <= 'f') return c - 87;
			return -1;
		}

		/// <summary>Decodes a character buffer containing a URL, into a byte buffer (for UTF-8 decoding)</summary>
		/// <param name="value">Buffer containing the characters of the URL</param>
		/// <param name="offset">Offset from the start of the buffer</param>
		/// <param name="count">Number of characters to decode</param>
		/// <param name="bytes">Output buffer where the decoded bytes are written</param>
		/// <param name="encoding">Encoding used (defaults to UTF-8 if null)</param>
		/// <returns>Number of bytes written to the output buffer</returns>
		private static unsafe int DecodeBytes(char* value, int offset, int count, byte* bytes, Encoding? encoding)
		{
			encoding ??= Encoding.UTF8;

			//IMPORTANT: we rely on the caller having sized 'bytes' large enough that there is no overflow !!!

			int pDst = 0;
			int pSrc = offset;
			while(count-- > 0)
			{
				byte val = (byte) value[pSrc++];
				if (val == '+')
				{ // Space
					val = 32;
				}
				else if (val == '%' && count >= 2)
				{ // Percent-Encoded ?

					// three possibilities:
					// - '%XX' : percent encoded
					// - '%uXXXX' : unicode encoded
					// - a badly-encoded '%' that we must let through

					if (value[pSrc] == 'u' && count >= 5)
					{ // '%uXXXX' ?
						// values[pSrc] == 'u'
						int a = DecodeHexDigit(value[pSrc + 1]);
						int b = DecodeHexDigit(value[pSrc + 2]);
						int c = DecodeHexDigit(value[pSrc + 3]);
						int d = DecodeHexDigit(value[pSrc + 4]);
						if (a >= 0 && b >= 0 && c >= 0 && d >= 0)
						{ // both are hex, we accept the character

							// grah, the problem is that we have to add the bytes corresponding to UTF-8 :(
							char ch = (char) ((a << 12) | (b << 8)  | (c << 4) | d);
							// "%uXXXX" is 6 bytes, and normally nothing can be more than 5 bytes once encoded as UTF-8
							int n = encoding.GetBytes(&ch, 1, bytes + pDst, count);
							pDst += n;
							pSrc += 5;
							count -= 5;
							continue; // => next
						}
					}
					else
					{ // '%XX'
						// the next two must be hex
						int hi = DecodeHexDigit(value[pSrc]);
						int lo = DecodeHexDigit(value[pSrc + 1]);
						if (hi >= 0 && lo >= 0)
						{ // both are hex, we accept the character
							bytes[pDst++] = (byte)((hi << 4) | lo);
							pSrc += 2;
							count -= 2;
							continue; // => next
						}
					}
					// otherwise it is a broken encoding, we let it through as-is
				}
				bytes[pDst++] = val;
			}

			return pDst;
		}

		#region Uri...

		/// <summary>Properly encodes a URI that may be malformed</summary>
		/// <param name="value">Uri to encode correctly</param>
		/// <returns>Correctly-encoded Uri</returns>
		/// <remarks>Does not touch the query string if there is one!</remarks>
		/// <example>EncodeUri("http://server/path to the/file.ext?blah=xxxx") => "http://server/path%20to%20the/file.ext?blah=xxx"</example>
		[Pure]
		public static string EncodeUri(string? value)
		{

			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			// WARNING: we must not touch the QueryString!
			int p = value.IndexOf('?');
			if (p >= 0)
			{ // recursive call to encode only the path, then re-attach the QueryString
				return EncodeUri(value[..p]) + value[p..];
			}

			return HttpUtility.UrlPathEncode(value);
		}

		#endregion

		#region Path...

		/// <summary>Encodes a value that will be used as a segment of a URI path</summary>
		/// <param name="value">Value to encode correctly (' ' => '%20')</param>
		/// <returns>Text that can be embedded in a URI path</returns>
		/// <example>EncodePath("foo bar/baz") => "foo%20bar%2fbaz"</example>
		[Pure]
		public static string EncodePath(string? value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			return UrlEncoder.Default.Encode(value);
		}

		[Pure]
		public static string EncodePathObject(object? value)
		{
			return EncodePath(ObjectToString(value));
		}

		#endregion

		#region Data...

		[Pure]
		private static string ObjectToString(object? value)
		{
			// most frequent types
			if (value == null) return string.Empty;
			if (value is string s) return s;

			var type = value.GetType();
			if (type.IsPrimitive)
			{
				// Warning: GetTypeCode returns 'TypeCode.Int32' for an Enum!
				switch (Type.GetTypeCode(type))
				{
					case TypeCode.Boolean: return ((bool)value) ? Tokens.True : Tokens.False;
					case TypeCode.Char: return new string((char)value, 1);
					case TypeCode.SByte: return StringConverters.ToString((sbyte) value);
					case TypeCode.Byte: return StringConverters.ToString((byte) value);
					case TypeCode.Int16: return StringConverters.ToString((short) value);
					case TypeCode.UInt16: return StringConverters.ToString((ushort) value);
					case TypeCode.Int32: return StringConverters.ToString((int) value);
					case TypeCode.UInt32: return StringConverters.ToString((uint) value);
					case TypeCode.Int64: return StringConverters.ToString((long) value);
					case TypeCode.UInt64: return ((ulong)value).ToString(null, CultureInfo.InvariantCulture);
					case TypeCode.Single: return ((float)value).ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
					case TypeCode.Double: return ((double)value).ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
					//note: decimal is not primitive!
				}
			}

			if (value is TimeSpan ts)
			{ // TimeSpan => number of seconds
				return ts.TotalSeconds.ToString(Tokens.FormatR, CultureInfo.InvariantCulture);
			}

			if (value is DateTime date)
			{ // Date => YYYYMMDD[HHMMSS[fff]]
				var time = date.TimeOfDay;
				if (time == TimeSpan.Zero) return date.ToString(Tokens.FormatDate);
				if (time.Milliseconds == 0) return date.ToString(Tokens.FormatDateTime);
				return date.ToString(Tokens.FormatDateTimeMillis);
			}

			if (value is decimal dec)
			{
				return dec.ToString(null, CultureInfo.InvariantCulture);
			}

			if (value is Enum e)
			{
				return e.ToString();
			}

			if (value is IFormattable fmt)
			{
				return fmt.ToString(null, CultureInfo.InvariantCulture);
			}

			// fingers crossed...
			return value.ToString() ?? string.Empty;
		}

		/// <summary>Encodes a value that will be used as a value in a QueryString</summary>
		/// <param name="value">Value to encode correctly (' ' => '+')</param>
		/// <param name="encoding">Optional encoding (UTF-8 by default)</param>
		/// <returns>Text that can be used as a value in a QueryString</returns>
		/// <example>EncodeData("foo bar/baz") => "foo+bar%2fbaz"</example>
		[Pure]
		public static string EncodeData(string? value, Encoding? encoding = null)
		{
			if (string.IsNullOrEmpty(value))
			{
				return string.Empty;
			}

			return HttpUtility.UrlEncode(value, encoding ?? Encoding.UTF8);
		}

		[Pure]
		public static string EncodeDataObject(object? value, Encoding? encoding = null)
		{
			return EncodeData(ObjectToString(value), encoding);
		}

		#endregion

		#endregion

	}

}
