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

namespace SnowBank.Testing.Framework.Tests
{
	using System.Collections.Generic;
	using System.IO;
	using System.IO.Compression;
	using System.Text;
	using Microsoft.Extensions.Primitives;
	using NUnit.Framework;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Tests for the diagnostic packet dump (<see cref="CapturedPacket.GetBasicDump"/>): it renders arbitrary
	/// captured wire bytes for a human and MUST NEVER throw, whatever the body contains.</summary>
	[TestFixture]
	public class CapturedPacketFacts : SimpleTest
	{

		private static CapturedPacket MakeResponsePacket(byte[] responseBody, string? contentType, string? contentEncoding = null)
		{
			var headers = new Dictionary<string, StringValues>(StringComparer.Ordinal);
			if (contentType != null) headers["Content-Type"] = contentType;
			if (contentEncoding != null) headers["Content-Encoding"] = contentEncoding;

			return new CapturedPacket
			{
				Id = new CapturedPacketId("TEST", 1),
				RequestBody = Slice.Empty,
				ResponseBody = responseBody.AsSlice(),
				Metadata = new CapturedPacketMetadata
				{
					TraceId = "trace-1",
					Role = CapturedPacketMetadata.ROLE_SERVER,
					StartedAt = default,
					Fields = CapturedHttpFields.ResponseStatusCode | CapturedHttpFields.ResponseHeaders | CapturedHttpFields.ResponseBody | CapturedHttpFields.RequestPropertiesAndHeaders,
					Connection = new CapturedPacketMetadata.ConnectionInfo { Id = "cnx-1", RemoteHost = "acme.example", RemotePort = 443 },
					Request = new CapturedPacketMetadata.RequestInfo { Method = "GET", Path = "/data", Headers = new CapturedHttpHeaders(), HasBody = false },
					Response = new CapturedPacketMetadata.ResponseInfo { Status = 200, ReasonPhrase = "OK", Headers = CapturedHttpHeaders.Create(headers), HasBody = true },
				},
			};
		}

		private static byte[] Gzip(string text)
		{
			using var ms = new MemoryStream();
			using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
			{
				var bytes = Encoding.UTF8.GetBytes(text);
				gz.Write(bytes, 0, bytes.Length);
			}
			return ms.ToArray();
		}

		[Test]
		public void Test_GetBasicDump_Honors_Charset_Hint_For_Legacy_Codepage()
		{
			// the reported case: a text body from a server on a legacy code page. "Hi " + 0xE9 (é in Windows-1252) is not valid UTF-8.
			// the dump must NOT throw, and honoring the declared charset (windows-1252, decoded via dependency-free Latin-1) recovers the 'é'.
			var packet = MakeResponsePacket([ (byte) 'H', (byte) 'i', (byte) ' ', 0xE9 ], "text/plain; charset=windows-1252");

			string dump = null!;
			Assert.That(() => dump = packet.GetBasicDump(includeBody: true), Throws.Nothing, "a diagnostic dump must never throw on invalid UTF-8");
			Assert.That(dump, Does.Contain("Hi é"), "the declared windows-1252 charset must decode 0xE9 as é");
		}

		[Test]
		public void Test_GetBasicDump_Defaults_To_Lenient_Utf8_When_No_Charset()
		{
			// same invalid byte but no charset hint: the automatic dump defaults to lenient UTF-8, so 0xE9 becomes the replacement char, and it must not throw.
			var packet = MakeResponsePacket([ (byte) 'H', (byte) 'i', (byte) ' ', 0xE9 ], "text/plain");

			string dump = null!;
			Assert.That(() => dump = packet.GetBasicDump(includeBody: true), Throws.Nothing);
			Assert.That(dump, Does.Contain("Hi �"), "with no charset hint, invalid bytes become the replacement char");
		}

		[Test]
		public void Test_GetBasicDump_Decompresses_Gzip_Body()
		{
			// a gzip-encoded text body must be inflated before decoding, otherwise the compressed bytes are meaningless (and would throw on strict decode).
			var packet = MakeResponsePacket(Gzip("hello world"), "text/plain", contentEncoding: "gzip");

			string dump = null!;
			Assert.That(() => dump = packet.GetBasicDump(includeBody: true), Throws.Nothing);
			Assert.That(dump, Does.Contain("hello world"), "a gzip body must be decompressed then decoded");
		}

		[Test]
		public void Test_GetBasicDump_Falls_Back_To_Hex_For_Binary_Mislabeled_As_Text()
		{
			// a binary body mislabeled text/plain (many control bytes) must fall back to a hex dump instead of rendering line-noise.
			// 20 NUL bytes (control) push the control-char ratio well over threshold; the trailing DE AD BE EF proves hex mode (the hex column shows them).
			var body = new byte[24];
			body[20] = 0xDE; body[21] = 0xAD; body[22] = 0xBE; body[23] = 0xEF;
			var packet = MakeResponsePacket(body, "text/plain");

			string dump = null!;
			Assert.That(() => dump = packet.GetBasicDump(includeBody: true), Throws.Nothing);
			Assert.That(dump, Does.Contain("DE AD BE EF").IgnoreCase, "a control-char-heavy body must be hex-dumped, not decoded as text");
		}

	}

}
