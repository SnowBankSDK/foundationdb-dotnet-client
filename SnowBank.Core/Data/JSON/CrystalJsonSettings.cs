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
	using System.Collections.Frozen;
	using System.Text;
	using SnowBank.Collections.Caching;

	/// <summary>JSON serialization settings</summary>
	/// <remarks>Instances of this type are immutable and can be cached</remarks>
	[DebuggerDisplay("<{ToString(),nq}> (Flags = {(int) m_flags,h})")]
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonSettings : IEquatable<CrystalJsonSettings>
	{

		#region Nested Enums ...

		/// <summary>Rules for indentation and spacing</summary>
		public enum Layout
		{
			// IMPORTANT: maximum 4 values, because it needs to fit in 2 bits

			/// <summary>Outputs as single-line, but with spacing between items, field names and values.</summary>
			Formatted = 0,
			/// <summary>Outputs as multi-line, with proper indentation and spacing between field names and values.</summary>
			Indented = 1,
			/// <summary>Outputs as single-line, and without any spacing between items, fields names or values.</summary>
			Compact = 2,

			//Reserved = 3
		}

		/// <summary>Rules for serializing dates and times</summary>
		public enum DateFormat
		{
			// IMPORTANT: maximum 4 values, because it needs to fit in 2 bits

			/// <summary>Use the global default for the dates, which is <see cref="TimeStampIso8601"/> by default</summary>
			Default = 0,
			/// <summary>Use the ISO 8601 format ("YYYY-MM-DDTHH:MM:SS.fffff") </summary>
			TimeStampIso8601 = 1,
			/// <summary>Use the Microsoft date representation ("\/Date(#####)\/" for UTC, or "\/Date(####+HHMM)\/" for LocalTime)</summary>
			Microsoft = 2,
			/// <summary>Use the JavaScript date representation "new Date(123456789)"</summary>
			/// <remarks>This will produce an invalid JSON document, and is only possible when formatting for JavaScript.</remarks>
			JavaScript = 3,
		}

		/// <summary>Rules for interning strings, and reduce memory allocations</summary>
		/// <remarks>Interning can reduce the memory footprint of a JSON Object, by using the same instance for similar strings, at the cost of some cpu overhead</remarks>
		public enum StringInterning
		{
			// IMPORTANT: this enum is stored in the flags using 2 bits, so can only have 4 values!

			/// <summary>Only the names of objects fields, as well as small numbers (3 digits or fewer), will be interned</summary>
			Default = 0,
			/// <summary>No string will be interned</summary>
			Disabled = 1,
			/// <summary>Same as <see cref="Default"/>, but include all number literals</summary>
			IncludeNumbers = 2,
			/// <summary>All field types (names, string literals, guid, numbers), EXCEPT dates, will be interned</summary>
			IncludeValues = 3,
		}

		/// <summary>Rules for serializing special floating points numbers, like <see cref="double.NaN"/> or <see cref="double.PositiveInfinity"/></summary>
		public enum FloatFormat
		{
			/// <summary>Use the global default for floating points, which is <see cref="Symbol"/> by default</summary>
			Default = 0,
			/// <summary>Use symbols like <c>`NaN`</c>, <c>`Infinity`</c> or <c>`-Infinity`</c> (without quotes). Note: The generated JSON will not strictly conform to RFC7159, which does not specify these symbols</summary>
			Symbol = 1,
			/// <summary>Use string literals like <c>"NaN"</c>, <c>"Infinity"</c> or <c>"-Infinity"</c> (with double quotes), similarly to what JSON.NET is doing. The generated JSON will conform to RFC7159, but the caller may not expect a JSON string literal instead of a JSON number, and may either fail or replace all values by NaN !</summary>
			String = 2,
			/// <summary>Use the <c>`null`</c> token when serializing <see cref="double.NaN"/>, <see cref="double.PositiveInfinity"/> or <see cref="double.NegativeInfinity"/>. The generated JSON will be conforming to RFC7159, but some information may be lost (all NaN and Inifinities will be replaced by null which may be deserialized as 0)</summary>
			Null = 3,
			/// <summary>Use the JavaScript notation (<c>Number.NaN</c>, <c>Number.POSITIVE_INFINITY</c>, ...). This will produce invalid JSON and is only valid when targeting JavaScript</summary>
			JavaScript = 4,
		}

		// ReSharper disable InconsistentNaming
		[Flags]
		[PublicAPI]
		public enum OptionFlags
		{
			None = 0,

			UseCamelCasingForName = 0x1,
			ShowNullMembers = 0x2,
			HideDefaultValues = 0x4,
			/// <summary>Serialize enums as their numeric value, instead of their string literal (which is the default)</summary>
			EnumsAsNumbers = 0x8,
			[Obsolete("Enums serialize as strings by default since 7.4.3: this bit now selects the NUMERIC form, under its new name EnumsAsNumbers. Use EnumsAsNumbers, or the WithEnumAsStrings()/WithEnumAsNumbers() fluent methods.", error: true)]
			EnumsAsString = EnumsAsNumbers,

			OptimizeForLargeData = 0x10,
			HideClassId = 0x20,
			FieldsIgnoreCase = 0x40,
			DoNotTrackVisited = 0x80,

			// Layout Enum
			Layout_Formatted = 0x00,
			Layout_Indented = 0x100,
			Layout_Compact = 0x200,
			Layout_Reserved = 0x300, // NOT USED
			Layout_Mask = 0x300, // all bits set

			// DateFormat Enum
			DateFormat_Default = 0x000,
			DateFormat_TimeStampIso8601 = 0x400,
			DateFormat_Microsoft = 0x800,
			DateFormat_JavaScript = 0xC00,
			DateFormat_Mask = 0xC00, // all bits set

			// StringInterning Enum
			StringInterning_Default = 0x0000,
			StringInterning_Disabled = 0x1000,
			StringInterning_IncludeNumbers = 0x2000,
			StringInterning_IncludeValues = 0x3000,
			StringInterning_Mask = 0x3000, // all bits set

			// Parsing
			AllowTrailingData = 0x4000,

			/// <summary>Serialize dictionaries as an array of <c>{ "Key": ..., "Value": ... }</c> objects (the legacy DataContractJsonSerializer wire shape) instead of a JSON object map</summary>
			DictionariesAsPairArrays = 0x8000,

			// Target Enum
			Target_Json = 0x00000,
			Target_JavaScript = 0x10000,
			Target_Reserved1 = 0x20000,
			Target_Reserved2 = 0x30000,
			Target_Mask = 0x30000,

			// Misc
			UseCamelCasingForEnums = 0x100000,
			DenyTrailingComma = 0x200000,
			OverwriteDuplicateFields = 0x400000,
			DoNotAllowComments = 0x800000,

			// Number Formatting
			FloatFormat_Default    = 0x0_0_000000,
			FloatFormat_Symbol     = 0x0_1_000000,
			FloatFormat_String     = 0x0_2_000000,
			FloatFormat_Null       = 0x0_3_000000,
			FloatFormat_JavaScript = 0x0_4_000000,
			FloatFormat_Mask       = 0x0_7_000000, // all bits set

			// Mutability
			Mutability_Mutable     = 0x00_000000,
			Mutability_ReadOnly    = 0x10_000000,
		}
		// ReSharper restore InconsistentNaming

		public enum Target
		{
			Json = 0,
			JavaScript = 1,

			//Reserved1 = 2,
			//Reserved2 = 3,
		}

		#endregion

		#region Private Members...

		/// <summary>Flags corresponding to the serialization rules that will be used</summary>
		private readonly OptionFlags m_flags;

		#endregion

		#region Constructors...

		public CrystalJsonSettings()
		{ }

		private CrystalJsonSettings(OptionFlags flags)
		{
			m_flags = flags;
		}

		#endregion

		#region Public Properties...

		/// <summary>Flags corresponding to the serialization rules that will be used</summary>
		public OptionFlags Flags
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_flags;
		}

		/// <summary>Target language (JSON, JavaScript, ...)</summary>
		public Target TargetLanguage
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (Target) (((int) m_flags >> 16) & 0x3);
		}

		private static OptionFlags SetTargetLanguage(OptionFlags flags, Target value)
		{
			return value is >= Target.Json and <= Target.JavaScript ? (flags & ~OptionFlags.Target_Mask) | (OptionFlags) (((int) value & 0x3) << 16) : FailInvalidTargetLanguage();

			[DoesNotReturn, StackTraceHidden]
			static OptionFlags FailInvalidTargetLanguage() => throw new ArgumentException("Invalid target language mode", nameof(value));
		}

		/// <summary>Rules for spacing and indentation</summary>
		public Layout TextLayout
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (Layout) (((int)m_flags >> 8) & 0x3);
		}

		private static OptionFlags SetTextLayout(OptionFlags flags, Layout value)
		{
			return value is >= Layout.Formatted and <= Layout.Compact ? (flags & ~OptionFlags.Layout_Mask) | (OptionFlags) (((int) value & 0x3) << 8) : FailInvalidTextLayout();

			[DoesNotReturn, StackTraceHidden]
			static OptionFlags FailInvalidTextLayout() => throw new ArgumentException("Invalid text layout mode", nameof(value));
		}

		/// <summary>Rules for serialization of dates and times</summary>
		public DateFormat DateFormatting
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (DateFormat) (((int) m_flags >> 10) & 0x3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetDateFormatting(OptionFlags flags, DateFormat value)
		{
			return value is >= DateFormat.Default and <= DateFormat.JavaScript ? (flags & ~OptionFlags.DateFormat_Mask) | (OptionFlags) (((int) value & 0x3) << 10) : FailInvalidDateFormatting();

			[DoesNotReturn, StackTraceHidden]
			static OptionFlags FailInvalidDateFormatting() => throw new ArgumentException("Invalid date format mode", nameof(value));
		}

		/// <summary>Rules for interning of strings (when parsing JSON documents)</summary>
		public StringInterning InterningMode
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (StringInterning)(((int)m_flags >> 12) & 0x3);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetInterningMode(OptionFlags flags, StringInterning value)
		{
			return value is >= StringInterning.Default and <= StringInterning.IncludeValues ? (flags & ~OptionFlags.StringInterning_Mask) | (OptionFlags) (((int) value & 0x3) << 12) : FailInvalidInterningMode();

			static OptionFlags FailInvalidInterningMode() => throw new ArgumentException("Invalid string interning mode", nameof(value));
		}

		/// <summary>If <see langword="true"/>, convert all field names to use camelCasing (ex: "userId", "familyName", ...)</summary>
		public bool UseCamelCasingForNames
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.UseCamelCasingForName) != 0;
		}

		private static OptionFlags SetUseCamelCasingForNames(OptionFlags flags, bool value)
		{
			return value ? flags | OptionFlags.UseCamelCasingForName : flags & ~OptionFlags.UseCamelCasingForName;
		}

		/// <summary>If <see langword="true"/>, ignore the case of field names during parsing (ex: "userId", "UserId", "USERID" will be considered the same field)</summary>
		public bool IgnoreCaseForNames
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.FieldsIgnoreCase) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetIgnoreCaseForNames(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.FieldsIgnoreCase : flags & ~OptionFlags.FieldsIgnoreCase;

		/// <summary>If <see langword="true"/>, outputs all fields of an object, including all fields that are null.</summary>
		/// <remarks>
		/// <para>By default, all null fields are omitted, to reduce the size of the generated JSON document.</para>
		/// <para>This setting is ignored if HideDefaultValues is used.</para>
		/// </remarks>
		public bool ShowNullMembers
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.ShowNullMembers) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetShowNullMembers(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.ShowNullMembers : flags & ~OptionFlags.ShowNullMembers;

		/// <summary>If <see langword="true"/>, omit all fields that are equal to the default value of their type, including <see langword="null"/>, <see langword="0"/>, <see langword="false"/>, <c>DateTime.MinValue</c>, etc...</summary>
		/// <remarks>
		/// <para>By default, all Value Types will be serialized, and only Ref Types or <see cref="Nullable{T}"/> are omitted (unless <see cref="ShowNullMembers"/> is set)</para></remarks>
		public bool HideDefaultValues
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.HideDefaultValues) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetHideDefaultValues(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.HideDefaultValues : flags & ~OptionFlags.HideDefaultValues;

		/// <summary>If <see langword="true"/> (the default), serialize all <see cref="System.Enum">enum types</see> as a string. If <see langword="false"/> serialize them as a number</summary>
		/// <remarks>
		/// <para>The string form uses the custom wire tokens declared on the enum's fields when present, otherwise the equivalent of <see cref="Enum.ToString()"/>; the casing is controlled by <see cref="UseCamelCasingForEnums"/>.</para>
		/// <para>Reading is always tolerant of both forms (names and tokens case-insensitively, numbers, and numeric strings), so this setting only affects what is written.</para>
		/// </remarks>
		public bool EnumsAsString
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.EnumsAsNumbers) == 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetEnumsAsString(OptionFlags flags, bool value)
			=> value ? flags & ~OptionFlags.EnumsAsNumbers : flags | OptionFlags.EnumsAsNumbers;

		/// <summary>If <see langword="true"/>, convert all enum string literal to camelCasing (ex: "someValue"). If <see langword="false"/>, use the same casing as used in the C# source code.</summary>
		public bool UseCamelCasingForEnums
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.UseCamelCasingForEnums) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetUseCamelCasingForEnums(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.UseCamelCasingForEnums : flags & ~OptionFlags.UseCamelCasingForEnums;

		/// <summary>If <see langword="true"/>, do not track the graph of visited objects, and disable any protection against cyclic references</summary>
		/// <remarks><b>CAUTION</b>: Attempting to serialize an object that contains cyclic references will either throw a <see cref="StackOverflowException"/> or an <see cref="OutOfMemoryException"/>, which may destabilize the system!</remarks>
		public bool DoNotTrackVisitedObjects
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.DoNotTrackVisited) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetDoNotTrackVisitedObjects(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.DoNotTrackVisited : flags & ~OptionFlags.DoNotTrackVisited;

		/// <summary>If <see langword="true"/>, expect the generated JSON to be large, and pre-allocated large buffers. If <see langword="false"/>, expect the JSON to be small and do not pre-allocated buffers</summary>
		/// <remarks>Can have an impact on the memory footprint and memory allocations/copies.</remarks>
		public bool OptimizeForLargeData
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.OptimizeForLargeData) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetOptimizeForLargeData(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.OptimizeForLargeData : flags & ~OptionFlags.OptimizeForLargeData;

		/// <summary>If <see langword="true"/>, do not include the "_class" field in generated JSON objects</summary>
		/// <remarks>
		/// <para>This may have an impact when attempting to deserialized abstract classes or interfaces.</para>
		/// <para>Please note that the content of the <c>_class</c> field has meaning only for compatible .NET applications, and should only be used in a "closed" ecosystem where all parties that have to serialize/deserialize abstract JSON objects use the same type names for the same objects!</para>
		/// </remarks>
		public bool HideClassId
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.HideClassId) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetHideClassId(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.HideClassId : flags & ~OptionFlags.HideClassId;

		/// <summary>If <see langword="true"/>, reject any trailing ',' at the end of an array or object. </summary>
		/// <remarks>
		/// <para>By default (<see langword="false"/>), all trailing commas are silently ignored.</para>
		/// <para>The official specification does not allow trailing commas, and compliant parsers will throw a syntax error. Enable this setting to replicate the same behavior.</para>
		/// <para>This only impacts deserialization. The serializer will never output a trailing commas when generation JSON text documents.</para>
		/// </remarks>
		public bool DenyTrailingCommas
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.DenyTrailingComma) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetDenyTrailingComma(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.DenyTrailingComma : flags & ~OptionFlags.DenyTrailingComma;

		/// <summary>Specifies whether JavaScript-style comments (<c>// ...</c> and <c>/* ... */</c>) are rejected while parsing.</summary>
		/// <remarks>
		/// <para>By default (<see langword="false"/>), comments are allowed and silently skipped.</para>
		/// <para>Comments are a non-standard extension: enable this setting (via <see cref="WithoutComments"/> or the <see cref="JsonStrict"/> preset) to reject them with a syntax error. This only impacts deserialization.</para>
		/// </remarks>
		public bool DenyComments
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.DoNotAllowComments) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetDenyComments(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.DoNotAllowComments : flags & ~OptionFlags.DoNotAllowComments;

		/// <summary>Specifies whether extra non-whitespace content after the top-level JSON value is tolerated while parsing.</summary>
		/// <remarks>
		/// <para>By default (<see langword="false"/>), any content following the first complete value (a second document, or trailing garbage) is a syntax error.</para>
		/// <para>Enable this (via <see cref="WithTrailingData"/>) to parse only the first value and ignore the rest. To consume consecutive fragments from a buffer, prefer <see cref="CrystalJson.ParseFragment"/> or the streaming reader instead. This only impacts deserialization.</para>
		/// </remarks>
		public bool AllowTrailingData
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.AllowTrailingData) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetAllowTrailingData(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.AllowTrailingData : flags & ~OptionFlags.AllowTrailingData;

		/// <summary>If <see langword="true"/>, overwrite any duplicate field in an object, by keeping only the last value. If <see langword="false"/>, throws an exception in case of duplicates</summary>
		/// <remarks><para>Please note that is <see cref="IgnoreCaseForNames"/> is set, this will also include casing (ex: "userId" and "UserId" would be considered duplicates)</para></remarks>
		public bool OverwriteDuplicateFields
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.OverwriteDuplicateFields) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static OptionFlags SetOverwriteDuplicateFields(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.OverwriteDuplicateFields : flags & ~OptionFlags.OverwriteDuplicateFields;

		/// <summary>Rules for serializing special floating point numbers, like <see langword="NaN"/> or <see cref="double.PositiveInfinity"/></summary>
		public FloatFormat FloatFormatting
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (FloatFormat) (((int) m_flags >> 24) & 0x7);
		}

		private static OptionFlags SetFloatFormatting(OptionFlags flags, FloatFormat value)
		{
			return value is >= FloatFormat.Default and <= FloatFormat.Null ? (flags & ~OptionFlags.FloatFormat_Mask) | (OptionFlags) (((int) value & 0x7) << 24) : FailInvalidFloatFormatting();

			[DoesNotReturn, StackTraceHidden]
			static OptionFlags FailInvalidFloatFormatting() => throw new ArgumentException("Invalid float formatting mode", nameof(value));
		}

		/// <summary>If <see langword="true"/>, parsed JSON documents will be read-only. If <see langword="false"/>, they will be mutable by default.</summary>
		/// <remarks>
		/// <para>Parsed read-only documents will be immutable, and can be safely cached, shared or used as singleton. Mutable documents can be modified, but may require deep copy to prevent side effects.</para>
		/// <para>This setting as no effect when serializing.</para>
		/// </remarks>
		public bool ReadOnly
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.Mutability_ReadOnly) != 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal CrystalJsonSettings UpdateReadOnly(bool readOnly)
			=> new(readOnly ? m_flags | OptionFlags.Mutability_ReadOnly : m_flags & ~OptionFlags.Mutability_ReadOnly);

		#endregion

		#region Equality ...

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.Append(this.TargetLanguage switch { Target.Json => "Json", _ => "JavaScript" });
			sb.Append(this.TextLayout switch { Layout.Indented => "_Indented", Layout.Compact => "_Compact", _ => "" });
			sb.Append(this.UseCamelCasingForNames ? "_CamelCase" : "");
			sb.Append(this.IgnoreCaseForNames ? "_IgnoreCase" : "");
			sb.Append(this.ReadOnly ? "_ReadOnly" : "");
			return sb.ToString();
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as CrystalJsonSettings);
		}

		public bool Equals(CrystalJsonSettings? other)
		{
			return other != null && other.m_flags == m_flags;
		}

		public override int GetHashCode()
		{
			return (int) m_flags;
		}

		#endregion

		#region Fluent API...

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal CrystalJsonSettings Update(OptionFlags flags) => flags == m_flags ? this : Create(flags);

		[Pure]
		public CrystalJsonSettings WithTextLayout(Layout layout) => Update(SetTextLayout(m_flags, layout));

		/// <summary>The generated JSON will be single-line and without any spacing between items and fields.</summary>
		[Pure]
		public CrystalJsonSettings Compacted() => Update(SetTextLayout(m_flags, Layout.Compact));

		/// <summary>The generated JSON will be single-line, but with spacing between items and fields.</summary>
		[Pure]
		public CrystalJsonSettings Formatted() => Update(SetTextLayout(m_flags, Layout.Formatted));

		/// <summary>The generated JSON will be multi-line and with indentation.</summary>
		[Pure]
		public CrystalJsonSettings Indented() => Update(SetTextLayout(m_flags, Layout.Indented));

		/// <summary>Null values for members (ref types and <see cref="Nullable{T}"/>) will be omitted.</summary>
		[Pure]
		public CrystalJsonSettings WithoutNullMembers() => Update(SetShowNullMembers(m_flags, false));

		/// <summary>Null values for members (ref types and <see cref="Nullable{T}"/>) will be included.</summary>
		[Pure]
		public CrystalJsonSettings WithNullMembers() => Update(SetShowNullMembers(m_flags, true));

		/// <summary>Default values for all members will be omitted</summary>
		[Pure]
		public CrystalJsonSettings WithoutDefaultValues() => Update(SetHideDefaultValues(m_flags, true));

		/// <summary>Default values for value types members will be included.</summary>
		[Pure]
		public CrystalJsonSettings WithDefaultValues(bool show = false) => Update(SetHideDefaultValues(m_flags, show));

		/// <summary>Specify the format used to serialized dates and times</summary>
		[Pure]
		public CrystalJsonSettings WithDateFormat(DateFormat format) => Update(SetDateFormatting(m_flags, format));

		/// <summary>Serialize dates using the ISO 8601 format: <c>"YYYY-MM-DDTHH:mm:ss.ffff+TZ"</c></summary>
		/// <returns></returns>
		[Pure]
		public CrystalJsonSettings WithIso8601Dates() => Update(SetDateFormatting(m_flags, DateFormat.TimeStampIso8601));

		/// <summary>Tests if dictionaries are serialized as an array of <c>{ "Key": ..., "Value": ... }</c> objects (the legacy DataContractJsonSerializer wire shape) instead of a JSON object map</summary>
		/// <remarks>This only affects serialization: on read, both shapes are always accepted.</remarks>
		public bool DictionariesAsPairArrays
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (m_flags & OptionFlags.DictionariesAsPairArrays) != 0;
		}

		private static OptionFlags SetDictionariesAsPairArrays(OptionFlags flags, bool value)
			=> value ? flags | OptionFlags.DictionariesAsPairArrays : flags & ~OptionFlags.DictionariesAsPairArrays;

		/// <summary>Serialize dictionaries as an array of <c>{ "Key": ..., "Value": ... }</c> objects, the wire shape produced by the legacy DataContractJsonSerializer</summary>
		/// <remarks>Only for interoperability with clients that cannot read a JSON object map; the shape is also always accepted on read, without this setting.</remarks>
		[Pure]
		public CrystalJsonSettings WithDictionariesAsPairArrays() => Update(SetDictionariesAsPairArrays(m_flags, true));

		/// <summary>Serialize dictionaries as a JSON object map (the default)</summary>
		[Pure]
		public CrystalJsonSettings WithDictionariesAsMaps() => Update(SetDictionariesAsPairArrays(m_flags, false));

		/// <summary>Serialize dates using the Microsoft notation: <c>"\/Date(xxxxx)\/"</c></summary>
		[Pure]
		public CrystalJsonSettings WithMicrosoftDates() => Update(SetDateFormatting(m_flags, DateFormat.Microsoft));

		/// <summary>Serialize dates using the Javascript notation: <c>new Date(xxxx)</c></summary>
		[Pure]
		public CrystalJsonSettings WithJavaScriptDates() => Update(SetDateFormatting(m_flags, DateFormat.JavaScript));

		/// <summary>Generate a native Javascript object or array: <c>{ hello: 'world', items: [ 1, 2, 3 ] }</c></summary>
		[Pure]
		public CrystalJsonSettings ForJavaScript() => Update(SetDateFormatting(SetTargetLanguage(m_flags, Target.JavaScript), DateFormat.JavaScript));

		/// <summary>Serialize all field names using the same literal as in the original C# source code (which traditionally is using PascalCasing)</summary>
		/// <remarks>If the original source code use a different casing, then this will be used without any change.</remarks>
		[Pure]
		public CrystalJsonSettings PascalCased() => Update(SetUseCamelCasingForNames(m_flags, false));

		/// <summary>Serialize all field names using camelCase ("firstName", like JavaScript)</summary>
		[Pure]
		public CrystalJsonSettings CamelCased() => Update(SetUseCamelCasingForNames(m_flags, true));

		/// <summary>Serialize all enums as numbers (the default is their string literal)</summary>
		[Pure]
		public CrystalJsonSettings WithEnumAsNumbers() => Update(SetEnumsAsString(m_flags, false));

		/// <summary>Serialize all enums as string literals (the default)</summary>
		[Pure]
		public CrystalJsonSettings WithEnumAsStrings() => Update(SetEnumsAsString(m_flags, true));

		/// <summary>Specify the way field names should be serialized</summary>
		/// <param name="camelCased">If <see langword="true"/>, using camelCasing. If <see langword="false"/>, use the same literal as in the original source code</param>
		[Pure]
		public CrystalJsonSettings WithEnumAsStrings(bool camelCased) => Update(SetUseCamelCasingForEnums(SetEnumsAsString(m_flags, true), camelCased));

		/// <summary>Disable tracking of visited objects, and inhibit any protection against cyclic references</summary>
		/// <remarks>
		/// <para>This should be used with extreme caution, because it could lead to stack overflow or out of memory errors!</para>
		/// <para>In some extreme circumstances, with a very large and deep object tree, this can give more performances, but should only be used when the constructed document is an acyclic graph.</para>
		/// </remarks>
		[Pure]
		public CrystalJsonSettings WithoutObjectTracking() => Update(SetDoNotTrackVisitedObjects(m_flags, true));

		/// <summary>Specify whether to track visited objects, and protect against cyclic references</summary>
		[Pure]
		public CrystalJsonSettings WithObjectTracking(bool enabled = false) => Update(SetDoNotTrackVisitedObjects(m_flags, enabled));

		/// <summary>Specify the way strings should be interned when parsing documents</summary>
		[Pure]
		public CrystalJsonSettings WithInterning(StringInterning mode) => Update(SetInterningMode(m_flags, mode));

		/// <summary>Disable any form of string interning</summary>
		[Pure]
		public CrystalJsonSettings DisableInterning() => Update(SetInterningMode(m_flags, StringInterning.Disabled));

		/// <summary>Optimize memory allocations for a large JSON document</summary>
		[Pure]
		public CrystalJsonSettings ExpectLargeData() => Update(SetOptimizeForLargeData(m_flags, true));

		/// <summary>Optimize memory allocations depending on the expected size of the generated JSON document</summary>
		[Pure]
		public CrystalJsonSettings OptimizedFor(bool largeData) => Update(SetOptimizeForLargeData(m_flags, largeData));

		/// <summary>Specify whether the <c>_class</c> field should be included or not in the generated JSON</summary>
		[Pure]
		public CrystalJsonSettings WithClassId(bool enabled = false) => Update(SetHideClassId(m_flags, enabled));

		/// <summary>Do not include the <c>_class</c> field in the generated JSON</summary>
		[Pure]
		public CrystalJsonSettings WithoutClassId() => Update(SetHideClassId(m_flags, true));

		/// <summary>Treat all field names as case-sensitive (by default)</summary>
		/// <remarks>This setting has no effect when serializing to JSON.</remarks>
		[Pure]
		public CrystalJsonSettings WithCaseOnFields(bool ignoreCase = false) => Update(SetIgnoreCaseForNames(m_flags, ignoreCase));

		/// <summary>Treat all field names as case-insensitive</summary>
		/// <remarks>This setting has no effect when serializing to JSON.</remarks>
		[Pure]
		public CrystalJsonSettings WithoutCaseOnFields() => Update(SetIgnoreCaseForNames(m_flags, true));

		/// <summary>Allow trailing commas at the end of objects or arrays (by default)</summary>
		/// <remarks>This setting has no effect when serializing to JSON.</remarks>
		[Pure]
		public CrystalJsonSettings WithTrailingCommas() => Update(SetDenyTrailingComma(m_flags, false));

		/// <summary>Disallow trailing commas at the end of objects or arrays, which will be considered as syntax errors</summary>
		[Pure]
		public CrystalJsonSettings WithoutTrailingCommas() => Update(SetDenyTrailingComma(m_flags, true));

		/// <summary>Allow JavaScript-style comments (<c>// ...</c> and <c>/* ... */</c>) inside parsed JSON documents (default)</summary>
		/// <remarks>Comments are a non-standard extension; this setting has no effect when serializing to JSON.</remarks>
		[Pure]
		public CrystalJsonSettings WithComments() => Update(SetDenyComments(m_flags, false));

		/// <summary>Reject JavaScript-style comments (<c>// ...</c> and <c>/* ... */</c>), which will be considered as syntax errors</summary>
		[Pure]
		public CrystalJsonSettings WithoutComments() => Update(SetDenyComments(m_flags, true));

		/// <summary>Tolerate extra content after the top-level JSON value (parse only the first value and ignore the rest)</summary>
		/// <remarks>To consume consecutive fragments from a buffer, prefer <see cref="CrystalJson.ParseFragment"/> or the streaming reader.</remarks>
		[Pure]
		public CrystalJsonSettings WithTrailingData() => Update(SetAllowTrailingData(m_flags, true));

		/// <summary>Reject any content after the top-level JSON value as a syntax error (default)</summary>
		[Pure]
		public CrystalJsonSettings WithoutTrailingData() => Update(SetAllowTrailingData(m_flags, false));

		/// <summary>If an object has duplicate field names, only the last value will be kept (default)</summary>
		[Pure]
		public CrystalJsonSettings FlattenDuplicateFields() => Update(SetOverwriteDuplicateFields(m_flags, true));

		/// <summary>If an object has duplicate field names, an error will be thrown</summary>
		[Pure]
		public CrystalJsonSettings ThrowOnDuplicateFields() => Update(SetOverwriteDuplicateFields(m_flags, false));

		/// <summary>Specify the wait special floating point numbers are serialized (<see cref="double.NaN"/>, <see cref="double.PositiveInfinity"/>, <see cref="double.NegativeInfinity"/>, ...)</summary>
		[Pure]
		public CrystalJsonSettings WithFloatFormat(FloatFormat format) => Update(SetFloatFormatting(m_flags, format));

		#endregion

		#region Default Globals...

		#region JSON...

		/// <summary>Parse or serialize JSON, with only minimum formatting</summary>
		/// <remarks>
		/// <para>This will produce a single line, but keep spaces between items: <c>{ "hello": "world", "foo": [ 1, 2, 3 ] }</c></para>
		/// </remarks>
		public static CrystalJsonSettings Json { get; } = new CrystalJsonSettings();

		/// <summary>Serialize JSON into the most compact possible form</summary>
		/// <remarks>
		/// <para>This will remove all extra white spaces and new lines: <c>{"hello":"world","foo":[1,2,3]}</c></para>
		/// </remarks>
		public static CrystalJsonSettings JsonCompact { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.Layout_Compact);

		/// <summary>Serialize JSON into a form readable by humans</summary>
		/// <remarks>
		/// <para>This will produce an indented multi-line output, suitable for log files or debug consoles:
		/// <code>{
		///	  "hello": "world",
		///	  "foo": [
		///	    1,
		///	    2,
		///	    3
		///	  ]
		/// }</code></para>
		/// </remarks>
		public static CrystalJsonSettings JsonIndented { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.Layout_Indented);

		/// <summary>Parse JSON using strict rules (no support for trailing commas, comments, ...)</summary>
		public static CrystalJsonSettings JsonStrict { get; } = new CrystalJsonSettings(OptionFlags.Target_Json | OptionFlags.DenyTrailingComma | OptionFlags.DoNotAllowComments);

		/// <summary>Parse JSON values, with case-insensitive field names in objects</summary>
		/// <remarks>
		/// <para>These three forms are all equivalent: <c>{ "hello": "world" } == { "HELLO": "world" } == { "HeLLo": "world" }</c></para>
		/// <para>The casing of the field names will be the same as the original. In case of duplicate keys with different case, the last value will be used, but the casing of the key will be unspecified</para>
		/// </remarks>
		public static CrystalJsonSettings JsonIgnoreCase { get; } = new CrystalJsonSettings(OptionFlags.FieldsIgnoreCase);

		/// <summary>Parse JSON read-only immutable values</summary>
		/// <remarks>
		/// <para>Any object or array will be read-only and immutable. As such, they can be safely shared, cached, or used as a singleton.</para>
		/// <para>If you need to modify the parsed result, either use a <see cref="Json">non-readonly variant</see>, or create a new mutable copy.</para>
		/// </remarks>
		public static CrystalJsonSettings JsonReadOnly { get; } = new CrystalJsonSettings(OptionFlags.Mutability_ReadOnly);

		/// <summary>Parse JSON read-only immutable values, with case-insensitive field names in JSON objects</summary>
		/// <remarks>
		/// <para>These three forms are all equivalent: <c>{ "hello": "world" } == { "HELLO": "world" } == { "HeLLo": "world" }</c></para>
		/// <para>The casing of the field names will be the same as the original. In case of duplicate keys with different case, the last value will be used, but the casing of the key will be unspecified</para>
		/// <para>Any object or array will be read-only and immutable. As such, they can be safely shared, cached, or used as a singleton.</para>
		/// <para>If you need to modify the parsed result, either use a <see cref="JsonIgnoreCase">non-readonly variant</see>, or create a new mutable copy.</para>
		/// </remarks>
		public static CrystalJsonSettings JsonReadOnlyIgnoreCase { get; } = new CrystalJsonSettings(OptionFlags.Mutability_ReadOnly | OptionFlags.FieldsIgnoreCase);

		#endregion

		#region JavaScript...

		/// <summary>Parse or serialize JavaScript objects, with minimum formatting</summary>
		/// <remarks>
		/// <para>This will produce a single line, but keep spaces between items: <c>{ hello: 'world', foo: [ 1, 2, 3 ] }</c></para>
		/// </remarks>
		public static CrystalJsonSettings JavaScript { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript);

		/// <summary>Serialize Javascript into the most compact possible form</summary>
		/// <remarks>
		/// <para>This will remove all extra white spaces and new lines: <c>{hello:'world',foo:[1,2,3]}</c></para>
		/// </remarks>
		public static CrystalJsonSettings JavaScriptCompact { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Layout_Compact);

		/// <summary>Serialize JavaScript into a form readable by humans</summary>
		/// <remarks>
		/// <para>This will produce an indented multi-line output, suitable for log files or debug consoles:
		/// <code>{
		///	  hello: 'world',
		///	  foo: [
		///	    1,
		///	    2,
		///	    3
		///	  ]
		/// }</code></para>
		/// </remarks>
		public static CrystalJsonSettings JavaScriptIndented { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Layout_Indented);

		/// <summary>Parse JSON values, with case-insensitive field names in objects</summary>
		/// <remarks>
		/// <para>These three forms are all equivalent: <c>{ hello: 'world'} == { HELLO: 'world' } == { HeLLo: "world" }</c></para>
		/// <para>The casing of the field names will be the same as the original. In case of duplicate keys with different case, the last value will be used, but the casing of the key will be unspecified</para>
		/// </remarks>
		public static CrystalJsonSettings JavaScriptIgnoreCase { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.FieldsIgnoreCase);

		/// <summary>Parse JavaScript read-only immutable values</summary>
		/// <remarks>
		/// <para>Any object or array will be read-only and immutable. As such, they can be safely shared, cached, or used as a singleton.</para>
		/// <para>If you need to modify the parsed result, either use a <see cref="JavaScript">non-readonly variant</see>, or create a new mutable copy.</para>
		/// </remarks>
		public static CrystalJsonSettings JavaScriptReadOnly { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Mutability_ReadOnly);

		/// <summary>Parse JavaScript read-only immutable values, with case-insensitive field names in objects</summary>
		/// <remarks>
		/// <para>These three forms are all equivalent: <c>{ hello: 'world' } == { HELLO: 'world' } == { HeLLo: 'world' }</c></para>
		/// <para>The casing of the field names will be the same as the original. In case of duplicate keys with different case, the last value will be used, but the casing of the key will be unspecified</para>
		/// <para>Any object or array will be read-only and immutable. As such, they can be safely shared, cached, or used as a singleton.</para>
		/// <para>If you need to modify the parsed result, either use a <see cref="JavaScriptIgnoreCase">non-readonly variant</see>, or create a new mutable copy.</para>
		/// </remarks>
		public static CrystalJsonSettings JavaScriptReadOnlyIgnoreCase { get; } = new CrystalJsonSettings(OptionFlags.Target_JavaScript | OptionFlags.Mutability_ReadOnly | OptionFlags.FieldsIgnoreCase);

		#endregion

		private static readonly QuasiImmutableCache<int, CrystalJsonSettings> Cached;

		static CrystalJsonSettings()
		{
			// create the initial cache for most defaults
			var defaults = new Dictionary<int, CrystalJsonSettings>();
			foreach (var s in new[] { Json, JsonCompact, JsonIndented, JsonStrict, JsonIgnoreCase, JsonReadOnly, JsonReadOnlyIgnoreCase, JavaScript, JavaScriptCompact, JavaScriptIndented, JavaScriptIgnoreCase, JavaScriptReadOnly, JavaScriptReadOnlyIgnoreCase })
			{
				defaults[(int) s.Flags] = s;
				// also cache the versions with enums as numbers
				var s2 = new CrystalJsonSettings(s.Flags | OptionFlags.EnumsAsNumbers);
				defaults[(int) s2.Flags] = s2;
			}
			Cached = new(defaults.ToFrozenDictionary(), valueFactory: (v) => new((OptionFlags) v));
		}

		internal static CrystalJsonSettings Create(OptionFlags flags) => Cached.GetOrAdd((int) flags);

		#endregion

	}

	public static class CrystalJsonSettingsExtensions
	{

		/// <summary>Returns a variant of these settings that parses JSON documents as read-only</summary>
		/// <remarks>All other settings are unmodified</remarks>
		[Pure]
		public static CrystalJsonSettings AsReadOnly(this CrystalJsonSettings? settings)
			=> settings == null ? CrystalJsonSettings.JsonReadOnly : settings.ReadOnly ? settings : settings.UpdateReadOnly(true);

		/// <summary>Returns a variant of these settings that parses JSON documents as mutable</summary>
		/// <remarks>All other settings are unmodified</remarks>
		[Pure]
		public static CrystalJsonSettings AsMutable(this CrystalJsonSettings? settings)
			=> settings == null ? CrystalJsonSettings.Json : !settings.ReadOnly ? settings : settings.UpdateReadOnly(false);

		/// <summary>Returns a variant of these settings that parses JSON documents as either read-only or mutable</summary>
		/// <remarks>All other settings are unmodified</remarks>
		[Pure]
		public static CrystalJsonSettings AsReadOnly(this CrystalJsonSettings? settings, bool readOnly)
			=> readOnly ? AsReadOnly(settings) : AsMutable(settings);

		/// <summary>Tests if the settings specify whether the parsed JSON document will be read-only or mutable</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsReadOnly(this CrystalJsonSettings? settings)
			=> settings is not null && settings.ReadOnly;

		/// <summary>Returns either <see cref="StringComparer.Ordinal"/> or <see cref="StringComparer.OrdinalIgnoreCase"/> depending on the value of <see cref="CrystalJsonSettings.IgnoreCaseForNames"/></summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IEqualityComparer<string> GetKeyComparer(this CrystalJsonSettings? settings)
			=> settings is not null && settings.IgnoreCaseForNames ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		/// <summary>Tests if the settings specify whether reference type properties and fields should be included when <see langword="null"/></summary>
		/// <remarks>Example: The property <c>string? Name { get; ... }</c>, when its value is null, should always be written when the return value is <see langword="true"/>, or skipped if <see langword="false"/></remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IncludesNullMembers(this CrystalJsonSettings? settings)
			=> settings is not null && settings.ShowNullMembers;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsCompactLayout(this CrystalJsonSettings? settings)
			=> settings is not null && settings.TextLayout == CrystalJsonSettings.Layout.Compact;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsIndentedLayout(this CrystalJsonSettings? settings)
			=> settings is not null && settings.TextLayout == CrystalJsonSettings.Layout.Indented;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsFormattedLayout(this CrystalJsonSettings? settings)
			=> settings is null || settings.TextLayout == CrystalJsonSettings.Layout.Formatted;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsJsonTarget(this CrystalJsonSettings? settings)
			=> settings is null || settings.TargetLanguage == CrystalJsonSettings.Target.Json;

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsJavascriptTarget(this CrystalJsonSettings? settings)
			=> settings is not null && settings.TargetLanguage == CrystalJsonSettings.Target.JavaScript;

	}

}
