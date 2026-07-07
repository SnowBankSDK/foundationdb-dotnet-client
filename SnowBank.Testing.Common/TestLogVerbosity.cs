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

	/// <summary>Controls how a test's diagnostic output is rendered: streamed live, consolidated into a single end-of-test journal, or both.</summary>
	/// <remarks>
	/// <para>Two consumers pull in opposite directions: a human watching an interactive runner (ReSharper, Visual Studio)
	/// wants each event <b>as it happens</b>, while a CI console or an AI agent reads the <b>final</b> log and wants ONE clean
	/// representation without the live stream duplicating the journal.</para>
	/// <para>Resolved once per process from the <c>SNOWBANK_TEST_LOG</c> environment variable
	/// (<c>stream</c> / <c>report</c> / <c>both</c>); when it is absent, an interactive runner defaults to
	/// <see cref="Stream"/> and everything else (CI, plain <c>dotnet</c> runs, AI agents) to <see cref="Report"/>.
	/// See <see cref="SimpleTest.LogVerbosity"/>.</para>
	/// <para>Regardless of the mode, a FAILING test always emits the consolidated journal: the post-mortem is exactly what
	/// is needed when something breaks, so no mode can hide it.</para>
	/// </remarks>
	public enum TestLogVerbosity
	{
		/// <summary>Stream each event live as it happens; the consolidated journal is emitted only when the test fails (interactive runners).</summary>
		Stream = 0,

		/// <summary>Suppress the live per-event stream; emit only the consolidated journal at the end (CI consoles and AI agents reading the file).</summary>
		Report = 1,

		/// <summary>Emit both the live per-event stream and the consolidated journal (deep debugging).</summary>
		Both = 2,
	}

}
