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
	using System.ComponentModel;
	using System.Runtime.Serialization;

	/// <summary>Syntax error that occurred while parsing a JSON document</summary>
	[Serializable]
	public class JsonSyntaxException : FormatException
	{

		private readonly string? m_reason;
		private readonly long m_offset;
		private readonly int m_line;
		private readonly int m_position;

		public JsonSyntaxException()
		{ }

		public JsonSyntaxException(string message) : base(message) { }

		public JsonSyntaxException(ref DefaultInterpolatedStringHandler message) : base(message.ToStringAndClear()) { }

		public JsonSyntaxException(string message, Exception innerException) : base(message, innerException) { }

		public JsonSyntaxException(ref DefaultInterpolatedStringHandler message, Exception innerException) : base(message.ToStringAndClear(), innerException) { }

		public JsonSyntaxException(string message, string? reason) : base(reason == null ? message : $"{message}: {reason}")
		{
			m_reason = reason;
		}

		public JsonSyntaxException(string message, string? reason, long offset, int line, int position)
			: base(reason == null ? $"{message} at ln {line} col {position}" : $"{message} at ln {line} col {position}: {reason}")
		{
			m_reason = reason;
			m_offset = offset;
			m_line = line;
			m_position = position;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected JsonSyntaxException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			m_reason = info.GetString("Reason");
			m_offset = info.GetInt64("Offset");
			m_line = info.GetInt32("Line");
			m_position = info.GetInt32("Position");
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Reason", m_reason);
			info.AddValue("Offset", m_offset);
			info.AddValue("Line", m_line);
			info.AddValue("Position", m_position);
		}

		public string Reason => m_reason ?? string.Empty;

		public long Offset => m_offset;

		public int Line => m_line;

		public int Position => m_position;

	}

}
