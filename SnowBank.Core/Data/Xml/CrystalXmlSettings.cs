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

	/// <summary>Structural line ending written between XML elements when <see cref="CrystalXmlSettings.Indented"/> is set</summary>
	/// <remarks>The value is fixed, never derived from <see cref="System.Environment.NewLine"/>, so the output does not depend on the host. There is no platform-dependent option: a caller who wants the host's line ending passes <see cref="Crlf"/> or <see cref="Lf"/> explicitly, matching whatever it resolves to on that host.</remarks>
	public enum CrystalXmlNewLine
	{
		/// <summary>Windows line ending <c>\r\n</c> between indented elements (the default)</summary>
		Crlf = 0,
		/// <summary>Unix line ending <c>\n</c> between indented elements</summary>
		Lf = 1,
	}

	/// <summary>How an element with no children or text is written</summary>
	public enum CrystalXmlEmptyElementStyle
	{
		/// <summary>A self-closing tag: <c>&lt;foo/&gt;</c> (the default)</summary>
		SelfClosing = 0,
		/// <summary>A separate open and close tag: <c>&lt;foo&gt;&lt;/foo&gt;</c></summary>
		Paired = 1,
	}

	/// <summary>Runtime options a CrystalXml writer reads while it turns a value into text or bytes</summary>
	/// <remarks>
	/// <para>A <see langword="readonly"/> <see langword="struct"/> that wraps a single flags field, so it is passed by value in a register. Build one from a preset and the fluent <c>With...</c> methods:
	/// <code>CrystalXmlSettings.DataContractCompat.WithOmitNamespaces().WithNewLine(CrystalXmlNewLine.Lf)</code></para>
	/// <para><see cref="Profile"/> and <see cref="OmitNamespaces"/> name the element names, which the source generator bakes into the converter, so they come from the container's <c>[CrystalXmlOutput]</c> attribute at generation time. The writer-level options (<see cref="Indented"/>, <see cref="NewLine"/>, <see cref="EmptyElementStyle"/>, <see cref="WriteXmlDeclaration"/>) are applied while writing and can be overridden per call.</para>
	/// <para><c>default(CrystalXmlSettings)</c> is <see cref="General"/>: the general profile, compact output (not indented), self-closing empty elements, no declaration.</para>
	/// </remarks>
	public readonly struct CrystalXmlSettings : IEquatable<CrystalXmlSettings>
	{

		/// <summary>Bit layout of <see cref="CrystalXmlSettings"/></summary>
		[Flags]
		public enum OptionFlags : long
		{
			/// <summary>All options at their default (the general profile, compact, self-closing, no declaration)</summary>
			None = 0,

			/// <summary>General profile: the standard, neutral XML format</summary>
			Profile_General = 0x0,
			/// <summary>DataContract profile: the format <see cref="System.Runtime.Serialization.DataContractSerializer"/> writes</summary>
			Profile_DataContract = 0x1,
			/// <summary>Bits that hold the profile</summary>
			Profile_Mask = 0x1,

			/// <summary>Strip namespaces and prefixes from the DataContract output (see <see cref="CrystalXmlSettings.OmitNamespaces"/>)</summary>
			OmitNamespaces = 0x2,

			/// <summary>Indent the output across multiple lines instead of writing it compact on one line (see <see cref="CrystalXmlSettings.Indented"/>)</summary>
			Indented = 0x4,

			/// <summary>Structural line ending is <c>\r\n</c> (the default); consulted only when <see cref="Indented"/> is set</summary>
			NewLine_Crlf = 0x00,
			/// <summary>Structural line ending is <c>\n</c>; consulted only when <see cref="Indented"/> is set</summary>
			NewLine_Lf = 0x8,
			/// <summary>Bit that holds the structural line ending</summary>
			NewLine_Mask = 0x8,

			/// <summary>Write an empty element as a self-closing tag (the default)</summary>
			EmptyElement_SelfClosing = 0x00,
			/// <summary>Write an empty element as an open and close tag</summary>
			EmptyElement_Paired = 0x10,

			/// <summary>Write the <c>&lt;?xml ...?&gt;</c> declaration at the start of the document</summary>
			WriteXmlDeclaration = 0x20,

			/// <summary>Write a member whose value is <see langword="null"/> instead of omitting it (see <see cref="CrystalXmlSettings.ShowNullMembers"/>)</summary>
			ShowNullMembers = 0x40,
		}

		private readonly OptionFlags m_flags;

		private CrystalXmlSettings(OptionFlags flags) => m_flags = flags;

		/// <summary>The raw flags</summary>
		public OptionFlags Flags => m_flags;

		#region Presets...

		/// <summary>General profile, compact output: the default for new documents</summary>
		public static CrystalXmlSettings General => default;

		/// <summary>DataContract profile: the output <see cref="System.Runtime.Serialization.DataContractSerializer"/> writes</summary>
		public static CrystalXmlSettings DataContractCompat => new(OptionFlags.Profile_DataContract | OptionFlags.ShowNullMembers);

		#endregion

		#region Accessors...

		/// <summary>Format the names follow: <see cref="CrystalXmlSerializerDefaults.General"/> or <see cref="CrystalXmlSerializerDefaults.DataContractCompat"/></summary>
		/// <remarks>Never <see cref="CrystalXmlSerializerDefaults.Inherit"/>: that value is resolved away at generation time, before it ever reaches a runtime <see cref="CrystalXmlSettings"/>.</remarks>
		public CrystalXmlSerializerDefaults Profile
			=> (m_flags & OptionFlags.Profile_Mask) == OptionFlags.Profile_DataContract ? CrystalXmlSerializerDefaults.DataContractCompat : CrystalXmlSerializerDefaults.General;

		/// <summary><see langword="true"/> when the DataContract output drops namespaces and prefixes, keeping the rest of that format</summary>
		public bool OmitNamespaces => (m_flags & OptionFlags.OmitNamespaces) != 0;

		/// <summary><see langword="true"/> when the output is indented across multiple lines instead of written compact on one line (the default is compact)</summary>
		/// <remarks><see cref="NewLine"/> is consulted only when this is set.</remarks>
		public bool Indented => (m_flags & OptionFlags.Indented) != 0;

		/// <summary>Structural line ending written between elements when <see cref="Indented"/> is set</summary>
		public CrystalXmlNewLine NewLine => (m_flags & OptionFlags.NewLine_Mask) switch
		{
			OptionFlags.NewLine_Lf => CrystalXmlNewLine.Lf,
			_ => CrystalXmlNewLine.Crlf,
		};

		/// <summary>How an element with no content is written</summary>
		public CrystalXmlEmptyElementStyle EmptyElementStyle
			=> (m_flags & OptionFlags.EmptyElement_Paired) != 0 ? CrystalXmlEmptyElementStyle.Paired : CrystalXmlEmptyElementStyle.SelfClosing;

		/// <summary><see langword="true"/> when the <c>&lt;?xml ...?&gt;</c> declaration is written</summary>
		public bool WriteXmlDeclaration => (m_flags & OptionFlags.WriteXmlDeclaration) != 0;

		/// <summary><see langword="true"/> when a member whose value is <see langword="null"/> is written instead of omitted</summary>
		public bool ShowNullMembers => (m_flags & OptionFlags.ShowNullMembers) != 0;

		#endregion

		#region Builders...

		private CrystalXmlSettings With(OptionFlags mask, OptionFlags value) => new((m_flags & ~mask) | value);

		/// <summary>Returns a copy with <see cref="OmitNamespaces"/> set</summary>
		public CrystalXmlSettings WithOmitNamespaces(bool value = true) => new(value ? m_flags | OptionFlags.OmitNamespaces : m_flags & ~OptionFlags.OmitNamespaces);

		/// <summary>Returns a copy with <see cref="Indented"/> set</summary>
		public CrystalXmlSettings WithIndented(bool value = true) => new(value ? m_flags | OptionFlags.Indented : m_flags & ~OptionFlags.Indented);

		/// <summary>Returns a copy with the structural line ending set</summary>
		public CrystalXmlSettings WithNewLine(CrystalXmlNewLine value) => With(OptionFlags.NewLine_Mask, value switch
		{
			CrystalXmlNewLine.Lf => OptionFlags.NewLine_Lf,
			_ => OptionFlags.NewLine_Crlf,
		});

		/// <summary>Returns a copy with the empty-element style set</summary>
		public CrystalXmlSettings WithEmptyElementStyle(CrystalXmlEmptyElementStyle value) => With(OptionFlags.EmptyElement_Paired, value == CrystalXmlEmptyElementStyle.Paired ? OptionFlags.EmptyElement_Paired : OptionFlags.None);

		/// <summary>Returns a copy that writes (or omits) the <c>&lt;?xml ...?&gt;</c> declaration</summary>
		public CrystalXmlSettings WithXmlDeclaration(bool value = true) => new(value ? m_flags | OptionFlags.WriteXmlDeclaration : m_flags & ~OptionFlags.WriteXmlDeclaration);

		/// <summary>Returns a copy that omits a member whose value is <see langword="null"/></summary>
		public CrystalXmlSettings WithoutNullMembers() => new(m_flags & ~OptionFlags.ShowNullMembers);

		/// <summary>Returns a copy that writes a member whose value is <see langword="null"/> instead of omitting it</summary>
		public CrystalXmlSettings WithNullMembers() => new(m_flags | OptionFlags.ShowNullMembers);

		#endregion

		#region Equality...

		/// <inheritdoc />
		public bool Equals(CrystalXmlSettings other) => m_flags == other.m_flags;

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj is CrystalXmlSettings other && Equals(other);

		/// <inheritdoc />
		public override int GetHashCode() => m_flags.GetHashCode();

		/// <summary>Compares two settings by their flags</summary>
		public static bool operator ==(CrystalXmlSettings left, CrystalXmlSettings right) => left.m_flags == right.m_flags;

		/// <summary>Compares two settings by their flags</summary>
		public static bool operator !=(CrystalXmlSettings left, CrystalXmlSettings right) => left.m_flags != right.m_flags;

		#endregion

	}

	/// <summary>Extension methods for <see cref="CrystalXmlSettings"/></summary>
	public static class CrystalXmlSettingsExtensions
	{

		/// <summary>Tests whether the given settings write a member whose value is <see langword="null"/> instead of omitting it</summary>
		public static bool IncludesNullMembers(this CrystalXmlSettings? settings) => settings.HasValue && settings.Value.ShowNullMembers;

	}

}
