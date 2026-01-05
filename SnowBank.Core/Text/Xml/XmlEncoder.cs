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

namespace SnowBank.Text.Xml
{
	using System.IO;
	using System.Text;

	/// <summary>XML Codec</summary>
	public static class XmlEncoder
	{
		#region Static Members...

		/// <summary>Map indiquant si un caractère est clean</summary>
		private static readonly byte[] s_xmlCharMap = InitializeCharMap();

		private const string TokenAmp = "&amp;";
		private const string TokenLt = "&lt;";
		private const string TokenGt = "&gt;";
		private const string TokenQuote = "&quot;";
		private const string TokenEntityHexaPrefix = "&#x";
		private const string TokenEntityDecPrefix = "&#";

		public const byte CLEAN = 0;
		public const byte ENTITY = 1;
		public const byte AMP = 2;
		public const byte LT = 3;
		public const byte GT = 4;
		public const byte QUOTE = 5;
		public const byte ILLEGAL = 6;
		public const byte HIGH_SURROGATE = 7;
		public const byte LOW_SURROGATE = 8;

		#endregion

		/// <summary>HTMLEncode une chaine de caractère (&amp; => &amp;amp; , etc...)</summary>
		/// <param name="text">Chaîne à encoder</param>
		/// <returns>Chaîne encodée</returns>
		/// <remarks>Version optimisée qui n'alloue pas de mémoire si la chaîne est deja clean</remarks>
		public static string Encode(string? text)
		{
			if (string.IsNullOrEmpty(text)) return String.Empty;

			if (IsCleanXml(text)) return text; // rien a modifier, retourne la chaîne initiale (fast, no memory used)

			return XmlEncodeSlow(new StringBuilder(text.Length + 16), text, true).ToString();
		}

		/// <summary>HTMLEncode une chaîne de caractère (&amp; => &amp;amp; , etc...)</summary>
		/// <param name="sb">Buffer où écrire le résultat</param>
		/// <param name="text">Chaîne à encoder</param>
		public static StringBuilder AppendTo(StringBuilder sb, string? text)
		{
			if (string.IsNullOrEmpty(text)) return sb;
			if (IsCleanXml(text)) return sb.Append(text);
			return XmlEncodeSlow(sb, text, true);
		}

		/// <summary>Extension méthode sur StringBuilder</summary>
		/// <param name="sb"></param>
		/// <param name="text"></param>
		/// <returns></returns>
		public static StringBuilder AppendXmlEncoded(this StringBuilder sb, string? text)
		{
			return AppendTo(sb, text);
		}

		public static TextWriter WriteTo(TextWriter output, string? text)
		{
			if (!string.IsNullOrEmpty(text))
			{
				if (IsCleanXml(text))
					output.Write(text);
				else
					output.Write(XmlEncodeSlow(new StringBuilder(text.Length + 16), text, true).ToString());
			}
			return output;
		}


		#region Internal Helpers...

		private static byte[] InitializeCharMap()
		{
			var map = new byte[65536];
			for (int i = 0; i < map.Length; i++)
			{
				char c = (char)i;
				if (IsLegalChar(c))
					map[i] = CLEAN;
				else if (c == '&')
					map[i] = AMP;
				else if (c == '<')
					map[i] = LT;
				else if (c == '>')
					map[i] = GT;
				else if (c == '"')
					map[i] = QUOTE;
				else if (IsIllegalChar(c))
					map[i] = ILLEGAL;
				else if (c >= 0xD800 && c <= 0xDBFF)
					map[i] = HIGH_SURROGATE;
				else if (c >= 0xDC00 && c <= 0xDFFF)
					map[i] = LOW_SURROGATE;
				else
					map[i] = ENTITY;
			}
			return map;
		}

		public static bool IsIllegalChar(char c)
		{
			return c <= 0x1F || c >= 0xFFFE;
		}

		public static bool IsRestrictedChar(char c)
		{
			// http://www.w3.org/TR/2006/REC-xml-20060816/#charsets
			//	Char	   ::=   	#x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]

			return (c >= 0x1 && c <= 0x8) || c == 0xB || c == 0xC || (c >= 0xE && c <= 0x1F) || (c >= 0x7F && c <= 0x84) || (c >= 0x86 && c <= 0x9F);
		}

		private static bool IsLegalChar(char c)
		{
			// On traite comme "légal" tout ce qui est compris entre ' ' (32) et '~' (126), *SAUF* les caractères '<' '>' '&' '"', ainsi que de 161 a 255

			// Les specs de XML 1.0 conseille d'encoder les caractères suivants: 
			// cf http://www.w3.org/TR/2006/REC-xml-20060816/#charsets
			//
			// [#x7F-#x84], [#x86-#x9F], [#xFDD0-#xFDDF],
			// [#x1FFFE-#x1FFFF], [#x2FFFE-#x2FFFF], [#x3FFFE-#x3FFFF],
			// [#x4FFFE-#x4FFFF], [#x5FFFE-#x5FFFF], [#x6FFFE-#x6FFFF],
			// [#x7FFFE-#x7FFFF], [#x8FFFE-#x8FFFF], [#x9FFFE-#x9FFFF],
			// [#xAFFFE-#xAFFFF], [#xBFFFE-#xBFFFF], [#xCFFFE-#xCFFFF],
			// [#xDFFFE-#xDFFFF], [#xEFFFE-#xEFFFF], [#xFFFFE-#xFFFFF],
			// [#x10FFFE-#x10FFFF] // <= dans la pratique on ne peut jamais voir cette range en C# qui est en UCS-2 (donc sera encodé avec une pair de Hi/Lo Surrogates!)

			// A-Z, a-z, 0-9, ' ', '!', '#',
			if (c <= 0x007F)
			{
				// U0000-U007F : Basic Latin, http://www.unicode.org/charts/PDF/U0000.pdf
				// exception: on laisse passer les retours chariots et les tabulations !
				if (c == '\r' || c == '\n' || c == '\t') return true;
				// on rejette les <,>,&," et aussi le &#127;
				return (c >= ' ' && c <= '~') && (c != '<' && c != '>' && c != '&' && c != '"' && c != 0x7F);
			}

			#region Latin...

			if (c <= 0x00FF)
			{
				// U0080-U00FF : Latin-1 Supplement, http://www.unicode.org/charts/PDF/U0080.pdf
				// on rejette tout ce qui est entre &#128; et &#160; ansi que &#173;
				return c > 0xA0 && c != 0xAD;
			}

			if (c <= 0x036F)
			{
				// U0100-U017F : Latin Extended-A, http://www.unicode.org/charts/PDF/U0100.pdf
				// U0180-U024F : Latin Extended-B, http://www.unicode.org/charts/PDF/U0180.pdf
				// U0250-U02AF : IPA Extensions, http://www.unicode.org/charts/PDF/U0250.pdf
				// U02B0-U02FF : Spacing Modifier Letters, http://www.unicode.org/charts/PDF/U02B0.pdf
				// U0300-U036F : Diacritical Marks, http://www.unicode.org/charts/PDF/U0300.pdf
				return true;
			}

			#endregion

			#region Arabic..
			if (c >= 0x0600 && c <= 0x06FF)
			{
				// U0600-U06FF : Arabic, http://www.unicode.org/charts/PDF/U0600.pdf
				// Exclusions:
				// - U0604, U0605, U061C, U061D : RESERVED
				// - U0620 : ARABIC LETTER KASHMIRI YEH
				// - U065F : ARABIC WAVY HAMZA BELOW
				return c != 0x604 && c != 0x605 && c != 0x61C && c != 0x61D && c != 0x620 && c != 0x65F;
			}
			#endregion

			#region Japanese...
			if (c >= 0x3040 && c <= 0x30FF)
			{
				// U3040-U309F : Hiragana, http://www.unicode.org/charts/PDF/U3040.pdf
				// U30A0-U30FF : Katakana, http://www.unicode.org/charts/PDF/U30A0.pdf
				// Exclusions:
				// - U3040, U3097, U3098 : Reserved
				return c != 0x3040 && c != 0x3097 && c != 0x3098;
			}
			if (c >= 0x4E00 && c <= 0x9FCB)
			{
				// U4E00-U9FBD : CJK Unified Ideographs
				return true;
			}
			#endregion

			return c == '\u0CA0';
		}

		private static unsafe bool IsCleanXml(string s)
		{
			int n = s.Length;
			var mask = s_xmlCharMap;
			fixed (char* p = s)
			{
				char* ptr = p;
				while (n-- > 0)
				{
					if (mask[*ptr++] != 0)
					{ // pas autorisé!
						return false;
					}
				}
			}
			return true;
		}

		/// <summary>Détermine si le texte HTML est "clean" (ie: ne nécessite pas d'encodage)</summary>
		/// <param name="sb">Buffer dans lequel écrire le résultat encodé</param>
		/// <param name="s">Chaîne à vérifier</param>
		/// <param name="replaceIllegal">Si true, remplace les caractères illegaux par du text équivalent (lossy!)</param>
		/// <returns>Instance de <paramref name="sb"/></returns>
		private static unsafe StringBuilder XmlEncodeSlow(StringBuilder sb, string s, bool replaceIllegal = false)
		{
			var mask = s_xmlCharMap;
			fixed (char* p = s) // JustCode warning (compile sous MS.NET)
			{
				char* ptr = p;
				char* end = p + s.Length;
				while (ptr < end)
				{
					char c = *ptr++;
					switch (mask[c])
					{
						case CLEAN:
						{
							sb.Append(c);
							break;
						}
						case AMP:
						{
							sb.Append(TokenAmp);
							break;
						}
						case LT:
						{
							sb.Append(TokenLt);
							break;
						}
						case GT:
						{
							sb.Append(TokenGt);
							break;
						}
						case QUOTE:
						{
							sb.Append(TokenQuote);
							break;
						}
						case ENTITY:
						{
							EncodeEntity(sb, c);
							break;
						}
						case ILLEGAL:
						{
							DealWithBadCodePoint(sb, c, replaceIllegal);
							break;
						}
						case HIGH_SURROGATE:
						{
							// doit normalement être suivi d'un autre caractère de type "LOW_SURROGATE"!
							if (ptr < end)
							{
								char c2 = *ptr;
								// c2 doit être un Low Surrogate!
								if (mask[c2] == LOW_SURROGATE)
								{ // combine les deux!
									++ptr; // consome le low surrogate également
									EncodeSurrogatePair(sb, c, c2);
									break;
								}
							}
							// il y a un problème soit il n'y a rien derrière, soit ce n'est pas un Low Surrogate?
							// => on n'a rien vu, rien entendu!
							DealWithBadCodePoint(sb, c, replaceIllegal);
							break;
						}
						case LOW_SURROGATE:
						{
							// on n'est pas censé tomber sur ce caractère directement, car il doit normalement être précédé d'un HIGH_SURROGATE, et donc géré par le case plus haut?
							DealWithBadCodePoint(sb, c, replaceIllegal);
							break;
						}
						default:
						{
							throw new InvalidOperationException("Unexpected state");
						}
					}
				}
			}
			return sb;
		}

		private static void DealWithBadCodePoint(StringBuilder sb, char c, bool replaceIllegal)
		{
			// Quand on arrive ici, c'est qu'on est confronté à un cas "invalide".
			// On pourrait throw une Exception, mais cela n'est pas forcément prévu par l'appelant a cet endroit, et puis dans tous les cas cela "brique" le code (si c'est lié au data, on peut refresh tant qu'on veut ca ne changera rien)
			// => On doit décider si on remplace le caractère par autre chose (fallback), au risque de "corrompre" silencieusement les données, ou alors passer la patate chaude au récipient qui devra se débrouiller avec!

			if (replaceIllegal)
			{ // Compatibilité MSXML, évite que LoadXML provoque une erreur "Invalid Unicode character"
				EncodeIllegal(sb, c);
			}
			else
			{ // Autre XML encoder normalement constitué
				EncodeEntity(sb, c);
			}
		}

		private static void EncodeEntity(StringBuilder sb, char c)
		{
			// dec 4660 => "&#x1234;"
			sb.Append(TokenEntityHexaPrefix).Append(((int)c).ToString("X")).Append(';');
		}

		private static void EncodeSurrogatePair(StringBuilder sb, char hi, char lo)
		{
			Contract.Debug.Requires(hi >= 0xD800 & hi <= 0xDBFF & lo >= 0xDC00 & lo <= 0xDFFF);
			long cp = 0x10000 + ((hi - 0xD800) * 0x400) + (lo - 0xDC00);
			// dec 128169 => "&#128169;"
			sb.Append(TokenEntityDecPrefix).Append(cp).Append(';');
		}

		private static readonly string[] s_asciiControlCodes =
		[
			"NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "HT", "LF", "VT", "FF", "CR", "SO", "SI", "DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS", "RS", "US"
		];

		private static void EncodeIllegal(StringBuilder sb, char c)
		{
			// On ne peut pas les représenter, on les remplaces par une version de substitution (plutot que '?')
			if (c >= 0 && c < 32)
			{ // \0 => "<NUL>", \x1B => "<ESC>"
				sb.Append(TokenLt).Append(s_asciiControlCodes[c]).Append(TokenGt);
			}
			else
			{ // \x1234 => "<1234>"
				sb.Append(TokenLt).Append(((int)c).ToString("X")).Append(TokenGt);
			}
		}

		#endregion

	}
}

