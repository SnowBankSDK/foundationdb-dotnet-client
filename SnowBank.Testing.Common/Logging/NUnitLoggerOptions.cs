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

namespace SnowBank.Testing
{
	using System.IO;
	using Microsoft.Extensions.Logging;

	/// <summary>Options used to configure the <see cref="NUnitLoggerProvider"/></summary>
	public sealed class NUnitLoggerOptions
	{

		/// <summary>Minimum log level during test execution</summary>
		public LogLevel LogLevel { get; set; } = LogLevel.Trace;

		public string? ActorId { get; set; }

		/// <summary>Output used to write the logs.</summary>
		/// <remarks>If null, will use <see cref="NUnit.Framework.TestContext.Out">TestContext.Out</see> by default.</remarks>
		public TextWriter? Output { get; set; }

		/// <summary>If not-null, write Errors (or above) to this output instead of <see cref="Output"/>.</summary>
		public TextWriter? OutputError { get; set; }

		/// <summary>If true, output the timestamp. If <see cref="DateOrigin"/> is not null, display the relative time from the origin.</summary>
		public bool TraceTimestamp { get; set; } = true;

		/// <summary>If non-null, origin date used to compute the elapsed time.</summary>
		public DateTime? DateOrigin { get; set; }

		/// <summary>If true, output the scope of the logger</summary>
		public bool IncludeScopes { get; set; }

		/// <summary>If true, output the event id</summary>
		public bool IncludeEventId { get; set; } = true;

		/// <summary>If true, outputs only the class name (ex: "Foo"). If false, outputs the full name (ex: "Acme.Frobs.Stuff.Foo")</summary>
		public bool UseShortName { get; set; } = true;

		/// <summary>Optional handler called for each log entry</summary>
		/// <remarks>The handler should not block the calling thread!</remarks>
		public Action<(LogLevel Level, string LogName, int EventId, string? EventName, string? Message, Exception? Exception)>? MessageHandler { get; set; }

	}

}
