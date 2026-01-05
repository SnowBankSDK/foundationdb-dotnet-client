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
	using System.Globalization;
	using System.Text;

	/// <summary>Very simple writer to dump query expressions into a statement useful for logging/debugging</summary>
	[PublicAPI]
	public sealed class DebugStatementWriter
	{

		public StringBuilder Buffer { get; } = new();

		public int IndentLevel { get; private set; }

		public bool StartOfLine { get; private set; } = true;

		public DebugStatementWriter Enter()
		{
			this.IndentLevel++;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public DebugStatementWriter Leave()
		{
			this.IndentLevel--;
			if (!this.StartOfLine) WriteLine();
			return this;
		}

		public DebugStatementWriter WriteLine(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.AppendLine(text);
			this.StartOfLine = true;
			return this;
		}

		public DebugStatementWriter WriteLine()
		{
			return WriteLine(string.Empty);
		}

		public DebugStatementWriter Write(string text)
		{
			if (this.StartOfLine) Indent();
			this.Buffer.Append(text);

			return this;
		}

		[JetBrains.Annotations.StringFormatMethod("format")]
		public DebugStatementWriter WriteLine(string format, params object?[] args)
		{
			return WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
		}

		[JetBrains.Annotations.StringFormatMethod("format")]
		public DebugStatementWriter Write(string format, params object?[] args)
		{
			return Write(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#if NET9_0_OR_GREATER

		[JetBrains.Annotations.StringFormatMethod("format")]
		public DebugStatementWriter WriteLine(string format, params ReadOnlySpan<object?> args)
		{
			return WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
		}

		[JetBrains.Annotations.StringFormatMethod("format")]
		public DebugStatementWriter Write(string format, params ReadOnlySpan<object?> args)
		{
			return Write(string.Format(CultureInfo.InvariantCulture, format, args));
		}

#endif

		private void Indent()
		{
			switch (this.IndentLevel)
			{
				case 0: break;
				case 1: this.Buffer.Append('\t'); break;
				case 2: this.Buffer.Append("\t\t"); break;
				case 3: this.Buffer.Append("\t\t\t"); break;
				default: this.Buffer.Append(new string('\t', this.IndentLevel)); break;
			}
			this.StartOfLine = false;
		}

		public override string ToString()
		{
			return this.Buffer.ToString();
		}

	}

}
