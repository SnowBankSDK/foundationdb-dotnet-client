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

namespace SnowBank.Data.Xml.Tests
{
	using System.Buffers;

	/// <summary>Event sequences and pin cases shared by every <see cref="IXmlEmitter"/> conformance fixture</summary>
	/// <remarks>
	/// <para>Drives an emitter exactly the way generated code does: through a <c>where TEmitter : struct, IXmlEmitter</c>
	/// constraint, where only interface members are visible. This is what proves the null-tolerant
	/// <see cref="IXmlEmitter.WriteText(string?)"/> / <see cref="IXmlEmitter.WriteRawAscii(string?)"/> members are reachable
	/// and bind correctly, on every emitter family - not merely on whichever one a hand-written test happened to hold as
	/// its concrete type.</para>
	/// <para><see cref="TextCases"/> and <see cref="RawCases"/> are the same three pins for all three emitter families
	/// (<see cref="CrystalXmlWriter{TRune,TWriter}"/>, <see cref="Data.Xml.XDocumentEmitter"/>,
	/// <see cref="Data.Xml.XmlWriterEmitter"/>): <see langword="null"/> must self-close, an empty string must force the
	/// expanded form, and a normal value must go through the escaper/passthrough as usual. Each fixture renders and
	/// compares its own family's output however is appropriate for it (exact string for the byte-exact writer, DOM/re-parse
	/// equivalence for the infoset emitters), but they all replay the identical case data, so the three families cannot
	/// silently drift apart.</para>
	/// </remarks>
	internal static class XmlEmitterConformance
	{

		/// <summary>Root element name used by every conformance scenario</summary>
		public static readonly XmlName Root = XmlName.Create("r");

		/// <summary><see cref="IXmlEmitter.WriteText(string?)"/> pin cases: <see langword="null"/> self-closes, <c>""</c> expands, ordinary text escapes</summary>
		public static readonly (string? Text, string ExpectedWire)[] TextCases =
		[
			(null, "<r />"),
			("", "<r></r>"),
			("a<b", "<r>a&lt;b</r>"),
		];

		/// <summary><see cref="IXmlEmitter.WriteRawAscii(string?)"/> pin cases: <see langword="null"/> self-closes, <c>""</c> expands, pre-validated ASCII passes through</summary>
		public static readonly (string? Ascii, string ExpectedWire)[] RawCases =
		[
			(null, "<r />"),
			("", "<r></r>"),
			("42", "<r>42</r>"),
		];

		/// <summary>Writes <c>&lt;r&gt;...&lt;/r&gt;</c> through the interface-constrained <see cref="IXmlEmitter.WriteText(string?)"/> member</summary>
		public static void EmitTextThroughInterface<TEmitter>(ref TEmitter emitter, string? text)
			where TEmitter : struct, IXmlEmitter
		{
			emitter.WriteStartElement(in Root);
			emitter.WriteText(text);
			emitter.WriteEndElement(in Root);
		}

		/// <summary>Writes <c>&lt;r&gt;...&lt;/r&gt;</c> through the interface-constrained <see cref="IXmlEmitter.WriteRawAscii(string?)"/> member</summary>
		public static void EmitRawThroughInterface<TEmitter>(ref TEmitter emitter, string? ascii)
			where TEmitter : struct, IXmlEmitter
		{
			emitter.WriteStartElement(in Root);
			emitter.WriteRawAscii(ascii);
			emitter.WriteEndElement(in Root);
		}

		/// <summary>Struct adapter that lets an <see cref="ArrayBufferWriter{T}"/> (a class) satisfy a <c>TWriter : struct</c> constraint</summary>
		/// <remarks>Holds a reference to the buffer, so every copy of the struct appends to the same underlying array. The
		/// production sinks (<c>ValueStringWriter</c>, <c>SliceWriter</c>) keep their state inline instead, which is exactly
		/// why an emitter must always be passed by ref; this adapter is test-only glue, not a shape to imitate.</remarks>
		internal readonly struct SinkRef<T> : IBufferWriter<T>
		{

			private readonly ArrayBufferWriter<T> Buffer;

			public SinkRef(ArrayBufferWriter<T> buffer) => this.Buffer = buffer;

			public void Advance(int count) => this.Buffer.Advance(count);

			public Memory<T> GetMemory(int sizeHint = 0) => this.Buffer.GetMemory(sizeHint);

			public Span<T> GetSpan(int sizeHint = 0) => this.Buffer.GetSpan(sizeHint);

		}

	}

}
