#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// netstandard2.0's System.Convert lacks the hex helpers (net5+) and the span-based Base64 helper.
// The netstandard2.0 build redirects the Convert name to this shim (via a file-local using alias); Base64 string
// helpers delegate to the real implementation.

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using BclConvert = System.Convert;

	internal static class ConvertCompat
	{

		private const string HexUpper = "0123456789ABCDEF";
		private const string HexLower = "0123456789abcdef";

		// --- delegated to the real BCL implementation ---

		public static string ToBase64String(byte[] inArray) => BclConvert.ToBase64String(inArray);

		public static string ToBase64String(ReadOnlySpan<byte> bytes) => BclConvert.ToBase64String(bytes.ToArray());

		public static byte[] FromBase64String(string s) => BclConvert.FromBase64String(s);

		// --- missing from netstandard2.0 ---

		public static string ToHexString(ReadOnlySpan<byte> bytes) => FormatHex(bytes, HexUpper);

		public static string ToHexString(byte[] inArray) => FormatHex(inArray, HexUpper);

		public static string ToHexStringLower(ReadOnlySpan<byte> bytes) => FormatHex(bytes, HexLower);

		public static string ToHexStringLower(byte[] inArray) => FormatHex(inArray, HexLower);

		private static string FormatHex(ReadOnlySpan<byte> bytes, string palette)
		{
			if (bytes.Length == 0) return string.Empty;
			var chars = new char[bytes.Length * 2];
			int p = 0;
			foreach (var b in bytes)
			{
				chars[p++] = palette[b >> 4];
				chars[p++] = palette[b & 0xF];
			}
			return new string(chars);
		}

		public static byte[] FromHexString(string s) => FromHexString(s.AsSpan());

		public static byte[] FromHexString(ReadOnlySpan<char> chars)
		{
			if ((chars.Length & 1) != 0) throw new FormatException("The input is not a valid hex string as its length is not a multiple of 2.");
			var result = new byte[chars.Length >> 1];
			for (int i = 0; i < result.Length; i++)
			{
				result[i] = (byte) ((FromHexNibble(chars[i * 2]) << 4) | FromHexNibble(chars[(i * 2) + 1]));
			}
			return result;
		}

		private static int FromHexNibble(char c)
			=> c switch
			{
				>= '0' and <= '9' => c - '0',
				>= 'a' and <= 'f' => c - 'a' + 10,
				>= 'A' and <= 'F' => c - 'A' + 10,
				_ => throw new FormatException("The input is not a valid hex string as it contains a non-hex character.")
			};

		public static bool TryToBase64Chars(ReadOnlySpan<byte> bytes, Span<char> chars, out int charsWritten, Base64FormattingOptions options = Base64FormattingOptions.None)
		{
			string encoded = BclConvert.ToBase64String(bytes.ToArray(), options);
			if (encoded.Length > chars.Length)
			{
				charsWritten = 0;
				return false;
			}
			encoded.AsSpan().CopyTo(chars);
			charsWritten = encoded.Length;
			return true;
		}

	}

}

#endif
