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

namespace SnowBank.Serialization.Json.CodeGen
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using Microsoft.CodeAnalysis;

	public partial class CrystalJsonSourceGenerator
	{

		/// <summary>XML emission section of the emitter: everything below is skipped unless the container asked for XML output</summary>
		/// <remarks>
		/// <para>Split out of <c>CrystalJsonGenerator.Emitter.cs</c> so that the XML wire stays legible next to the JSON one
		/// rather than interleaved with it, and so that the second profile (the DataContract wire) has an obvious home.</para>
		/// <para>The profile dispatch happens in exactly two places: <see cref="Emitter.WritesXml"/> decides whether a container
		/// produces XML at all, and <see cref="Emitter.WritesXmlDcs"/> picks between this section (the modern wire) and
		/// <c>CrystalJsonGenerator.Emitter.Xml.Dcs.cs</c> (the DataContract wire). What the two share - the name and enum
		/// tables, the holder helpers, the scalar formatter selection - lives here.</para>
		/// </remarks>
		internal sealed partial class Emitter
		{

			#region Emitted type names...

			private const string CrystalXmlNamespaceQualified = "global::" + KnownTypeSymbols.CrystalXmlNamespace;

			private const string XmlNameFullName = CrystalXmlNamespaceQualified + ".XmlName";

			private const string IXmlEmitterFullName = CrystalXmlNamespaceQualified + ".IXmlEmitter";

			private const string ICrystalXmlSerializerFullName = CrystalXmlNamespaceQualified + ".ICrystalXmlSerializer";

			private const string CrystalXmlHelperFullName = CrystalXmlNamespaceQualified + ".CrystalXml";

			private const string CrystalXmlFormattersFullName = CrystalXmlNamespaceQualified + ".CrystalXmlFormatters";

			private const string CrystalXmlUnknownTypeExceptionFullName = CrystalXmlNamespaceQualified + ".CrystalXmlUnknownTypeException";

			private const string CrystalXmlNotSupportedExceptionFullName = CrystalXmlNamespaceQualified + ".CrystalXmlNotSupportedException";

			private const string CrystalXmlCycleExceptionFullName = CrystalXmlNamespaceQualified + ".CrystalXmlCycleException";

			private const string CrystalXmlMaxDepthFullName = CrystalXmlHelperFullName + ".MaxDepth";

			private const string CrystalJsonSettingsExtensionsFullName = "global::" + KnownTypeSymbols.CrystalJsonNamespace + ".CrystalJsonSettingsExtensions";

			private const string XDocumentFullName = "global::System.Xml.Linq.XDocument";

			private const string XmlWriterFullName = "global::System.Xml.XmlWriter";

			private const string StreamFullName = "global::System.IO.Stream";

			private const string TextWriterFullName = "global::System.IO.TextWriter";

			private const string IBufferWriterOfByteFullName = "global::System.Buffers.IBufferWriter<byte>";

			private const string EqualityComparerFullName = "global::System.Collections.Generic.EqualityComparer";

			#endregion

			#region Resolved vocabulary (mirrors the parser's own constants)...

			private const string XmlProfileModern = "Modern";

			/// <inheritdoc cref="XmlProfileModern"/>
			private const string XmlProfileDataContract = "DataContract";

			private const string XmlDictionaryFormatDefault = "Default";

			private const string XmlDictionaryFormatDirect = "Direct";

			private const string XmlDictionaryFormatKeyAttribute = "KeyAttribute";

			private const string XmlDictionaryFormatKeyValueAttributes = "KeyValueAttributes";

			private const string XmlDictionaryFormatKeyValueElements = "KeyValueElements";

			/// <summary>Name of the attribute that marks a null element, on both wires</summary>
			private const string XmlNilAttributeName = "nil";

			/// <summary>Names of the key and value attributes of the attribute-shaped dictionary formats</summary>
			private const string XmlKeyAttributeName = "key";

			/// <inheritdoc cref="XmlKeyAttributeName"/>
			private const string XmlValueAttributeName = "value";

			/// <summary>Names of the child elements of <c>KeyValueElements</c>, capitalized as the compat wire spells them</summary>
			private const string XmlKeyElementName = "Key";

			/// <inheritdoc cref="XmlKeyElementName"/>
			private const string XmlValueElementName = "Value";

			/// <summary>Default name of a dictionary entry element, when the member declares no <c>ItemName</c></summary>
			private const string XmlDefaultEntryName = "entry";

			#endregion

			/// <summary>Whether this container emits an XML surface at all</summary>
			/// <remarks>Both profiles share the holder helpers, the <c>ICrystalXmlSerializer&lt;T&gt;</c> facet and the two write
			/// methods; only the BODY of those methods differs, which is what <see cref="WritesXmlDcs"/> selects.</remarks>
			private bool WritesXml => this.Metadata.XmlProfile is XmlProfileModern or XmlProfileDataContract;

			/// <summary>Whether this container emits the DataContract (compat) XML wire rather than the modern one</summary>
			/// <remarks>That wire derives every name from the data contract and has its own member order, null policy, dictionary
			/// shape and scalar forms, so it is emitted by its own section (<c>CrystalJsonGenerator.Emitter.Xml.Dcs.cs</c>).</remarks>
			private bool WritesXmlDcs => this.Metadata.XmlProfile == XmlProfileDataContract;

			#region Name table...

			/// <summary>Collects the distinct XML names a generated converter writes, one cached <c>static readonly XmlName</c> field each</summary>
			/// <remarks>Deduplicated by name TEXT: a document that repeats a name (an item name shared by two members, the same
			/// entry name on two dictionaries) transcodes the UTF-8 literal once, at type load, and never again.</remarks>
			private sealed class XmlNameTable
			{

				private readonly Dictionary<string, string> Fields = new(StringComparer.Ordinal);

				/// <summary>Identifiers already handed out, so that resolving a collision is a lookup and not a scan of every field declared so far</summary>
				/// <remarks>SHARED with the <see cref="XmlEnumTable"/> of the same converter: the two prefixes are not disjoint (a name spelling out to <c>__xml_enum_Color</c> collides with the lookup method of an enum named <c>Color</c>), and two members of the same generated class carrying one identifier is CS0102.</remarks>
				private readonly HashSet<string> Taken;

				private readonly List<KeyValuePair<string, string>> Order = [ ];

				public XmlNameTable(HashSet<string> taken) => this.Taken = taken;

				/// <summary>Returns the name of the field holding <paramref name="text"/>, declaring one if this is its first use</summary>
				public string Ref(string text)
				{
					if (this.Fields.TryGetValue(text, out var existing))
					{
						return existing;
					}

					var sb = new StringBuilder("__xml_");
					foreach (var c in text)
					{
						sb.Append((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ? c : '_');
					}

					// two different names can sanitize to the same identifier ("a-b" and "a.b"), so the first one keeps it
					// and the others get a numeric suffix: the field name is an implementation detail, the mapping must be 1:1
					string candidate = sb.ToString();
					if (this.Taken.Contains(candidate))
					{
						int n = 2;
						while (this.Taken.Contains(candidate + "_" + n.ToString(CultureInfo.InvariantCulture)))
						{
							++n;
						}
						candidate += "_" + n.ToString(CultureInfo.InvariantCulture);
					}

					this.Fields[text] = candidate;
					this.Taken.Add(candidate);
					this.Order.Add(new(candidate, text));
					return candidate;
				}

				/// <summary>Declared fields, in first-use order</summary>
				public List<KeyValuePair<string, string>> Entries => this.Order;

			}

			/// <summary>Emits the cached <see cref="XmlNameTable"/> fields of one converter</summary>
			/// <remarks>Emitted AFTER the bodies that reference them (C# does not care about declaration order inside a type),
			/// because the table is filled BY those bodies: any pre-pass would be a second implementation of the same walk,
			/// free to drift from it.</remarks>
			private static void WriteXmlNameFields(CSharpCodeBuilder sb, XmlNameTable names)
			{
				sb.Comment("Cached element and attribute names, in both representations: the char core copies the string, the byte core copies the frozen UTF-8 literal");
				foreach (var entry in names.Entries)
				{
					sb.AppendLine($"private static readonly {XmlNameFullName} {entry.Key} = new({CSharpCodeBuilder.Constant(entry.Value)}, {CSharpCodeBuilder.Constant(entry.Value)}u8.ToArray());");
				}
			}

			#endregion

			#region Enum label table...

			/// <summary>Collects the enum types whose labels a generated converter writes, one static lookup method each</summary>
			/// <remarks>
			/// <para>The labels come from the metadata the parser captured, and the lookup is a generated <c>switch</c>: nothing on this
			/// path calls <c>Enum.ToString()</c>, which resolves names through the runtime's reflection-backed enum cache.</para>
			/// <para>Deduplicated by fully qualified type name: an enum used by two members of the same type is emitted once.</para>
			/// </remarks>
			private sealed class XmlEnumTable
			{

				private readonly Dictionary<string, string> Methods = new(StringComparer.Ordinal);

				/// <summary>Identifiers already handed out (two enums of the same simple name live in different namespaces)</summary>
				/// <inheritdoc cref="XmlNameTable.Taken" path="/remarks"/>
				private readonly HashSet<string> Taken;

				private readonly List<KeyValuePair<string, TypeMetadata>> Order = [ ];

				public XmlEnumTable(HashSet<string> taken) => this.Taken = taken;

				/// <summary>Returns the name of the lookup method for <paramref name="type"/>, declaring one if this is its first use</summary>
				public string Ref(TypeMetadata type)
				{
					string key = type.FullyQualifiedName;
					if (this.Methods.TryGetValue(key, out var existing))
					{
						return existing;
					}

					var sb = new StringBuilder("__xml_enum_");
					foreach (var c in type.Name)
					{
						sb.Append((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ? c : '_');
					}

					string candidate = sb.ToString();
					if (this.Taken.Contains(candidate))
					{
						int n = 2;
						while (this.Taken.Contains(candidate + "_" + n.ToString(CultureInfo.InvariantCulture)))
						{
							++n;
						}
						candidate += "_" + n.ToString(CultureInfo.InvariantCulture);
					}

					this.Methods[key] = candidate;
					this.Taken.Add(candidate);
					this.Order.Add(new(candidate, type));
					return candidate;
				}

				/// <summary>Declared lookup methods, in first-use order</summary>
				public List<KeyValuePair<string, TypeMetadata>> Entries => this.Order;

			}

			/// <summary>Returns the wire label of one enum member: the custom token if it declares one, else its C# name</summary>
			/// <remarks>
			/// <para>The same order as the JSON side's runtime cache (the System.Text.Json spelling wins over the DataContract one), so a value renders identically on both wires.</para>
			/// <para>The label is decided HERE, at generation time, so it does not follow the RUNTIME naming settings: a call that asks
			/// the JSON side for camel-cased enum labels (<c>CrystalJsonSettings.UseCamelCasingForEnums</c>, as
			/// <c>WithEnumAsStrings(camelCased: true)</c> sets it) gets <c>sciFi</c> in JSON and <c>SciFi</c> in XML from the same value.
			/// Honoring it would mean emitting a second label table per enum; use <c>[JsonStringEnumMemberName]</c> or
			/// <c>[EnumMember(Value = ...)]</c> to pin one spelling for both wires.</para>
			/// </remarks>
			private static string GetXmlEnumLabel(EnumMemberMetadata member) => member.JsonStringEnumMemberName ?? member.EnumMemberValue ?? member.Name;

			/// <summary>Emits the generated label lookup of every enum this converter writes</summary>
			private void WriteXmlEnumHelpers(CSharpCodeBuilder sb, XmlEnumTable enums)
			{
				foreach (var entry in enums.Entries)
				{
					var type = entry.Value;
					string fullName = type.FullyQualifiedName;

					if (this.WritesXmlDcs)
					{ // the compat wire spells labels, filters members and combines flags by its own rules
						WriteXmlDcsEnumHelper(sb, entry.Key, type);
						continue;
					}

					sb.Comment($"Labels of {type.Name}, resolved by a switch: Enum.ToString() would go through the runtime's reflection-backed name cache");
					sb.AppendLine($"private static string {entry.Key}({fullName} value) => value switch");
					sb.EnterBlock("switch");

					// a duplicate constant value would be a duplicate case label (and the first declared name is the one
					// ToString() itself returns), so only the FIRST member of each value gets a case
					var seen = new HashSet<string>(StringComparer.Ordinal);
					foreach (var member in type.EnumMembers)
					{
						if (!seen.Add(member.Value)) continue;
						sb.AppendLine($"{fullName}.{member.Name} => {CSharpCodeBuilder.Constant(GetXmlEnumLabel(member))},");
					}

					string numeric = FormatXmlScalar(GetXmlEnumUnderlyingFamily(type), $"({GetXmlEnumUnderlyingKeyword(type)}) value");
					if (type.IsFlagsEnum)
					{ // an undeclared combination has no label of its own, and composing one would mean inventing a separator
						this.XmlNeedsFlagsHelper = true;
						sb.AppendLine($"_ => FailXmlUndeclaredFlags(typeof({fullName}), {numeric}),");
					}
					else
					{ // an undeclared value renders as its underlying integer, exactly like Enum.ToString() does
						sb.AppendLine($"_ => {numeric},");
					}

					sb.LeaveBlock("switch", ';');
					sb.NewLine();
				}
			}

			/// <summary>Emits the helper that refuses an undeclared combination of a <c>[Flags]</c> enum</summary>
			private static void WriteXmlFlagsHelper(CSharpCodeBuilder sb)
			{
				sb.Comment("a [Flags] value that is not a declared member has no label: composing one from the declared flags would mean picking a separator this profile never specified");
				sb.AppendLine($"private static string FailXmlUndeclaredFlags({SystemTypeFullName} type, string value)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {CrystalXmlNotSupportedExceptionFullName}(type, \"Cannot write the value '\" + value + \"' of enum '\" + type.Name + \"' to XML: it is not one of the declared members of this [Flags] enum, and this profile does not define how to combine several of them into one label. Declare the combination as a member of the enum, write the member as a number with [JsonProperty(EnumFormat = JsonEnumFormat.Number)], or take the member over with a custom converter that has an XML facet.\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			/// <summary>Whether the converter currently being emitted needs the undeclared-flags helper</summary>
			private bool XmlNeedsFlagsHelper { get; set; }

			/// <summary>Table of the enum lookups of the converter currently being emitted</summary>
			private XmlEnumTable XmlEnums { get; set; } = new(new(StringComparer.Ordinal));

			/// <summary>Returns the C# keyword of the underlying integer type of an enum</summary>
			private static string GetXmlEnumUnderlyingKeyword(TypeMetadata type) => type.EnumUnderlyingSpecialType switch
			{
				SpecialType.System_SByte => "sbyte",
				SpecialType.System_Byte => "byte",
				SpecialType.System_Int16 => "short",
				SpecialType.System_UInt16 => "ushort",
				SpecialType.System_UInt32 => "uint",
				SpecialType.System_Int64 => "long",
				SpecialType.System_UInt64 => "ulong",
				_ => "int",
			};

			/// <summary>Returns the scalar family that formats the underlying integer type of an enum</summary>
			private static string GetXmlEnumUnderlyingFamily(TypeMetadata type) => type.EnumUnderlyingSpecialType switch
			{
				SpecialType.System_SByte => "SByte",
				SpecialType.System_Byte => "Byte",
				SpecialType.System_Int16 => "Int16",
				SpecialType.System_UInt16 => "UInt16",
				SpecialType.System_UInt32 => "UInt32",
				SpecialType.System_Int64 => "Int64",
				SpecialType.System_UInt64 => "UInt64",
				_ => "Int32",
			};

			#endregion

			#region Holder helpers (the eight outputs)...

			/// <summary>Emits the eight XML output entry points on the type's holder, each delegating to the matching <c>CrystalXml</c> helper</summary>
			/// <remarks>None of them goes through another: every one owns its own sink lifecycle inside <c>CrystalXml</c>, so a
			/// text document never round-trips through UTF-8 (or the other way around) just to reach a different overload.</remarks>
			private void WriteXmlStaticHelpers(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, string typeCref)
			{
				string instanceType = typeDef.Type.FullyQualifiedName + (typeDef.Type.IsValueType() ? "" : "?");
				string tail = $"{KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, string? rootName = default";

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a string of XML text</summary>");
				sb.AppendLine($"public static string ToXmlText({instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.ToText(Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a UTF-8 encoded <see cref=\"{KnownTypeSymbols.SliceFullName}\"/> of XML</summary>");
				sb.AppendLine($"public static {KnownTypeSymbols.SliceFullName} ToXmlSlice({instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.ToSlice(Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a UTF-8 encoded byte array of XML</summary>");
				sb.AppendLine($"public static byte[] ToXmlBytes({instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.ToBytes(Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> as UTF-8 encoded XML into the specified stream</summary>");
				sb.XmlComment("<remarks>The stream is not owned by this call: the caller flushes and disposes it.</remarks>");
				sb.AppendLine($"public static void WriteXmlTo({StreamFullName} destination, {instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.WriteTo(destination, Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> as XML text into the specified writer</summary>");
				sb.XmlComment("<remarks>The writer is not owned by this call: the caller flushes and disposes it.</remarks>");
				sb.AppendLine($"public static void WriteXmlTo({TextWriterFullName} destination, {instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.WriteTo(destination, Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> as UTF-8 encoded XML into the specified buffer writer</summary>");
				sb.AppendLine($"public static void WriteXmlTo({IBufferWriterOfByteFullName} destination, {instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.WriteTo(destination, Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into the specified <see cref=\"T:System.Xml.XmlWriter\"/></summary>");
				sb.XmlComment("<remarks>Only infoset equivalence with the byte-exact wire is guaranteed here: the concrete bytes depend on how the writer was configured.</remarks>");
				sb.AppendLine($"public static void WriteXmlTo({XmlWriterFullName} destination, {instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.WriteTo(destination, Default, instance, settings, rootName);");
				sb.NewLine();

				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into an in-memory <see cref=\"T:System.Xml.Linq.XDocument\"/></summary>");
				sb.XmlComment("<remarks>Only infoset equivalence with the byte-exact wire is guaranteed here (no indentation or line-ending promise): this is the XML counterpart of <c>Pack</c>.</remarks>");
				sb.AppendLine($"public static {XDocumentFullName} ToXDocument({instanceType} instance, {tail}) => {CrystalXmlHelperFullName}.ToXDocument(Default, instance, settings, rootName);");
				sb.NewLine();
			}

			#endregion

			#region Serializer facet...

			/// <summary>Emits the <c>ICrystalXmlSerializer&lt;T&gt;</c> facet of one converter: the two write methods, then the names they cached</summary>
			private void WriteXmlSerializer(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				if (this.WritesXmlDcs)
				{ // the DataContract wire is a different emission from top to bottom (names, order, null policy, shapes)
					WriteXmlDcsSerializer(sb, typeDef);
					return;
				}

				// ONE taken-identifier set for both tables: they declare members of the SAME generated class
				var taken = new HashSet<string>(StringComparer.Ordinal);
				var names = new XmlNameTable(taken);
				this.XmlEnums = new(taken);
				this.XmlNeedsNotSupportedHelper = false;
				this.XmlNeedsFlagsHelper = false;
				this.XmlNeedsCycleHelper = false;
				var type = typeDef.Type;
				string valueType = type.FullyQualifiedName + (type.IsValueType() ? "" : "?");
				string settingsType = KnownTypeSymbols.CrystalJsonSettingsFullName;

				string rootRef = names.Ref(ResolveXmlRootName(sb, typeDef));

				// WriteXml(...): the interface entry point, which owns the rootName override
				sb.InheritDoc();
				sb.XmlComment("<remarks>The serialization lifecycle callbacks of this type (<c>OnSerializing</c>, <c>OnSerialized</c>) are NOT invoked on the XML path: they are part of the JSON contract, and whether writing a second format should re-fire them has not been decided. Do not rely on them running here.</remarks>");
				sb.XmlComment("<remarks>Enum labels are baked in at generation time, so they do NOT follow <c>CrystalJsonSettings.UseCamelCasingForEnums</c>: one call can render <c>sciFi</c> in JSON and <c>SciFi</c> in XML. Pin one spelling for both wires with <c>[JsonStringEnumMemberName]</c> or <c>[EnumMember(Value = ...)]</c>.</remarks>");
				sb.AppendLine($"public void WriteXml<TEmitter>(ref TEmitter emitter, {valueType} value, {settingsType}? settings = default, string? rootName = default) where TEmitter : struct, {IXmlEmitterFullName}");
				sb.EnterBlock("WriteXml");
				sb.AppendLine("if (rootName is null)");
				sb.EnterBlock();
				sb.AppendLine($"WriteXmlElement(ref emitter, in {rootRef}, value, settings, 0);");
				sb.LeaveBlock();
				sb.AppendLine("else");
				sb.EnterBlock("else");
				sb.Comment("a caller-supplied name is the one place user text becomes an XML name: Create validates it, and raises CrystalXmlInvalidNameException rather than corrupting the document");
				sb.AppendLine($"var __root = {XmlNameFullName}.Create(rootName);");
				sb.AppendLine("WriteXmlElement(ref emitter, in __root, value, settings, 0);");
				sb.LeaveBlock("else");
				sb.LeaveBlock("WriteXml");
				sb.NewLine();

				// WriteXmlElement(...): the nested entry point, so a parent can name the child element without re-validating a name
				sb.XmlComment("<summary>Writes this value as an element of the given name</summary>");
				sb.XmlComment("<remarks>The nested entry point: a parent converter writing this type as one of its members passes its own cached member name here, so no name is validated or transcoded at write time.</remarks>");
				sb.XmlComment($"<param name=\"{XmlDepthParameterName}\">Number of elements already open above this one. Every nested call within this generated recursion adds one, and reaching <c>CrystalXml.MaxDepth</c> raises <c>CrystalXmlCycleException</c> instead of recursing into a stack overflow; a cycle running through a custom <c>ICrystalXmlSerializer{{T}}.WriteXml</c> or <c>ICrystalXmlSerializable.WriteXml</c> hook is not covered, since the counter resets to zero across that call.</param>");
				sb.AppendLine($"public void WriteXmlElement<TEmitter>(ref TEmitter emitter, in {XmlNameFullName} name, {valueType} value, {settingsType}? settings, int {XmlDepthParameterName} = 0) where TEmitter : struct, {IXmlEmitterFullName}");
				sb.EnterBlock("WriteXmlElement");

				WriteXmlDepthGuard(sb, type);
				sb.NewLine();

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(type.Ref, out var polymorphicMetadata);

				if (!type.IsValueType())
				{
					sb.Comment("a null value still produces the element (a document needs a root), marked nil under the same rule as a null member: only when the settings ask for null members");
					sb.AppendLine("if (value is null)");
					sb.EnterBlock();
					sb.AppendLine("emitter.WriteStartElement(in name);");
					sb.AppendLine($"if ({CrystalJsonSettingsExtensionsFullName}.IncludesNullMembers(settings))");
					sb.EnterBlock();
					sb.AppendLine($"emitter.WriteAttribute(in {names.Ref(XmlNilAttributeName)}, \"true\");");
					sb.LeaveBlock();
					sb.AppendLine("emitter.WriteEndElement(in name);");
					sb.AppendLine("return;");
					sb.LeaveBlock();
					sb.NewLine();
				}

				if (typeDef.IsPolymorphicRoot)
				{
					sb.Comment("the runtime type decides which generated body writes the element (and the discriminator it carries)");
					sb.AppendLine("switch (value)");
					sb.EnterBlock("switch");
					foreach (var (_, derivedType, _) in typeDef.DerivedTypes)
					{
						if (derivedType.IsAbstract) continue;
						//BUGBUG: mirrors the JSON side: the cases are emitted in declaration order, so a base class declared before its own subclass would capture it first
						// the delegate writes THIS element, so it stands at the same depth: only a nested MEMBER adds a level
						sb.AppendLine($"case {derivedType.FullyQualifiedName} x: {GetLocalSerializerRef(derivedType)}.WriteXmlElement(ref emitter, in name, x, settings, {XmlDepthParameterName}); return;");
					}
					sb.AppendLine($"default: throw new {CrystalXmlUnknownTypeExceptionFullName}(value.GetType());");
					sb.LeaveBlock("switch");
					sb.LeaveBlock("WriteXmlElement");
					sb.NewLine();
					WriteXmlCycleHelper(sb);
					WriteXmlNameFields(sb, names);
					return;
				}

				if (type.IsAbstract && hasPolymorphicDefinition)
				{ // an abstract type in the middle of a hierarchy: the root of the hierarchy owns the switch
					sb.AppendLine($"{GetLocalSerializerRef(polymorphicMetadata.Parent)}.WriteXmlElement(ref emitter, in name, value, settings, {XmlDepthParameterName});");
					sb.LeaveBlock("WriteXmlElement");
					sb.NewLine();
					WriteXmlCycleHelper(sb);
					WriteXmlNameFields(sb, names);
					return;
				}

				if (!type.IsSealed && !type.IsValueType())
				{ // an unsealed type with no declared derived type: writing a subclass through this body would silently drop its own members
					sb.AppendLine($"if (value.GetType() != typeof({type.FullyQualifiedName}))");
					sb.EnterBlock();
					sb.AppendLine($"throw new {CrystalXmlUnknownTypeExceptionFullName}(value.GetType());");
					sb.LeaveBlock();
					sb.NewLine();
				}

				sb.AppendLine("emitter.WriteStartElement(in name);");

				if (hasPolymorphicDefinition)
				{
					WriteXmlDiscriminator(sb, names, typeDef, polymorphicMetadata);
				}

				if (typeDef.Type.IsCrystalXmlSerializable())
				{ // the type writes its own content: this body owns the element shell, and nothing else
					sb.NewLine();
					sb.Comment("the type implements ICrystalXmlSerializable: it writes its own content, inside the element opened here");
					sb.AppendLine("value.WriteXml(ref emitter);");
				}
				else
				{
					// attributes FIRST: they belong to the start tag, which the first content event closes
					foreach (var member in typeDef.Members)
					{
						if (!member.XmlIsAttribute) continue;
						WriteXmlAttributeMember(sb, names, typeDef, member);
					}

					foreach (var member in typeDef.Members)
					{
						if (member.XmlIsAttribute) continue;
						WriteXmlElementMember(sb, names, typeDef, member);
					}
				}

				sb.NewLine();
				sb.AppendLine("emitter.WriteEndElement(in name);");
				sb.LeaveBlock("WriteXmlElement");
				sb.NewLine();

				if (this.XmlNeedsNotSupportedHelper)
				{
					WriteXmlNotSupportedHelper(sb);
					this.XmlNeedsNotSupportedHelper = false;
				}

				WriteXmlCycleHelper(sb);

				// emitted BEFORE the flags helper is asked for: the lookups are what set that flag
				WriteXmlEnumHelpers(sb, this.XmlEnums);

				if (this.XmlNeedsFlagsHelper)
				{
					WriteXmlFlagsHelper(sb);
					this.XmlNeedsFlagsHelper = false;
				}

				WriteXmlNameFields(sb, names);
			}

			/// <summary>Resolves the name of the root element of a type: the data contract's own name, else the type name through the container's naming policy</summary>
			/// <remarks>BACKSTOP only: a <c>[DataContract(Name = ...)]</c> that is not a legal XML name is refused by the parser with <c>CXML0007</c>, which points at the declaration itself. The <c>#error</c> below stays so that a name reaching here through some future path still cannot produce a document that does not parse.</remarks>
			private string ResolveXmlRootName(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				string name = typeDef.DataContractName ?? Parser.FormatName(typeDef.Type.Name, this.Metadata.PropertyNamingPolicy);
				try
				{
					System.Xml.XmlConvert.VerifyNCName(name);
				}
				catch (Exception e) when (e is System.Xml.XmlException or ArgumentException)
				{
					sb.AppendLine($"#error The type {typeDef.Type.FullName} resolves to the XML root name '{name}', which is not a legal XML name: {e.Message.Replace("\r", " ").Replace("\n", " ")}");
					// keep going with a name that at least compiles: the build has already failed, and a cascade of
					// downstream errors would only bury the one line that says why
					name = "element";
				}
				return name;
			}

			/// <summary>Writes the type discriminator of a derived type: an ATTRIBUTE, written before any content</summary>
			/// <remarks>The XML name is the JSON discriminator property with its leading <c>'$'</c> removed (<c>$type</c> becomes <c>type</c>): the JSON default is not a legal XML name, and the two formats each keep their own convention for the same concept.</remarks>
			private void WriteXmlDiscriminator(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef, (CrystalJsonTypeMetadata Parent, object? Discriminator) polymorphicMetadata)
			{
				// resolved through the parser's own helper, so that the collision check (CXML0005) and this write site cannot
				// guard and emit two different names
				string declared = polymorphicMetadata.Parent.TypeDiscriminatorPropertyName ?? "$type";
				string name = Parser.GetXmlDiscriminatorName(polymorphicMetadata.Parent.TypeDiscriminatorPropertyName);

				string? value = polymorphicMetadata.Discriminator switch
				{
					string s => s,
					int n => n.ToString(CultureInfo.InvariantCulture),
					_ => null,
				};

				if (value is null)
				{
					sb.AppendLine($"#error Invalid type discriminator value for derived type {typeDef.Name} of parent type {polymorphicMetadata.Parent.Name}");
					return;
				}

				if (name.Length == 0)
				{
					sb.AppendLine($"#error The type discriminator property '{declared}' of type {polymorphicMetadata.Parent.Name} leaves no XML attribute name once its leading '$' is removed");
					return;
				}

				try
				{
					System.Xml.XmlConvert.VerifyNCName(name);
				}
				catch (Exception e) when (e is System.Xml.XmlException or ArgumentException)
				{
					sb.AppendLine($"#error The type discriminator property '{declared}' of type {polymorphicMetadata.Parent.Name} is not a legal XML attribute name: {e.Message.Replace("\r", " ").Replace("\n", " ")}");
					return;
				}

				sb.Comment("the discriminator is an ANNOTATION on this wire, so it is an attribute, and it comes first");
				sb.AppendLine($"emitter.WriteAttribute(in {names.Ref(name)}, {CSharpCodeBuilder.Constant(value)});");
			}

			/// <summary>Name of the depth parameter threaded through the generated element-writing recursion</summary>
			/// <remarks>An explicit parameter, and not state on the emitter, because the emitter is a caller-supplied struct with
			/// its own contract (an infoset emitter may be handed a writer that is already inside a document): the recursion the
			/// guard measures is the GENERATED one, so the generated code is what must carry the counter.</remarks>
			private const string XmlDepthParameterName = "__depth";

			/// <summary>Emits the depth guard at the top of a generated element-writing body</summary>
			/// <remarks>
			/// <para>A cycle in the object graph has no XML representation on either profile, and the generated emission has no
			/// visited-set to detect one with: left unguarded, it recurses until the native stack is exhausted, which raises a
			/// <c>StackOverflowException</c> that .NET cannot catch and that takes the whole process down. Counting the levels
			/// costs one comparison against a constant per element on the happy path, allocates nothing, and needs no reflection.</para>
			/// <para>Measured before this guard existed: a two-node cycle overflowed after ~2300 nested <c>WriteXmlElement</c>
			/// frames on the modern profile and ~4600 on the compat one (one frame per level). The cap lives in
			/// <c>CrystalXml.MaxDepth</c>, whose own documentation justifies the value.</para>
			/// </remarks>
			private void WriteXmlDepthGuard(CSharpCodeBuilder sb, TypeMetadata type)
			{
				this.XmlNeedsCycleHelper = true;
				sb.Comment("a reference cycle has no XML representation, and an unguarded recursion would die on a StackOverflowException that no caller can catch: count the levels and fail with a typed error instead");
				sb.AppendLine($"if ({XmlDepthParameterName} >= {CrystalXmlMaxDepthFullName}) FailXmlCycle(typeof({type.FullyQualifiedName}));");
			}

			/// <summary>Whether the converter currently being emitted needs the depth-guard helper</summary>
			private bool XmlNeedsCycleHelper { get; set; }

			/// <summary>Emits the helper that raises <c>CrystalXmlCycleException</c> once the depth cap is reached</summary>
			/// <remarks>A method call rather than an inline <c>throw</c>, so that the statements after the guard stay reachable for
			/// the compiler and the generated body carries no unreachable-code warning (same shape as <c>FailXmlNotSupported</c>).</remarks>
			private void WriteXmlCycleHelper(CSharpCodeBuilder sb)
			{
				if (!this.XmlNeedsCycleHelper) return;
				this.XmlNeedsCycleHelper = false;

				sb.Comment("the depth cap was reached: the graph either loops back on itself, or is deeper than this serializer supports - either way there is no document to write");
				sb.AppendLine($"private static void FailXmlCycle({SystemTypeFullName} type)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {CrystalXmlCycleExceptionFullName}(type, \"Cannot write an instance of type '\" + type.Name + \"' to XML: the emission reached the maximum nesting depth of \" + {CrystalXmlMaxDepthFullName}.ToString(global::System.Globalization.CultureInfo.InvariantCulture) + \" elements. The object graph either contains a reference cycle (which has no XML representation) or is nested deeper than this serializer supports.\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			/// <summary>Writes the <c>nil</c> marker on the element currently open, and closes it</summary>
			private static void WriteXmlNilElement(CSharpCodeBuilder sb, XmlNameTable names, string nameRef)
			{
				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				sb.AppendLine($"emitter.WriteAttribute(in {names.Ref(XmlNilAttributeName)}, \"true\");");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
			}

			#endregion

			#region Members...

			/// <summary>Returns the effective XML name of a member: its <c>[XmlProperty]</c> override, else the JSON name (already through the naming policy)</summary>
			private static string GetXmlMemberName(CrystalJsonMemberMetadata member) => member.XmlName ?? member.Name;

			/// <summary>Returns the expression that reads a member off the local <c>value</c>, through the accessor thunk when the member is out of reach</summary>
			private static string GetXmlMemberReadExpr(CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
				=> member.NeedsGetterThunk
					? $"__get_{member.MemberName}({(typeDef.Type.IsValueType() ? "ref " : "")}value)"
					: $"value.{member.MemberName}";

			/// <summary>What a member does when its value is <see langword="null"/></summary>
			private enum XmlNullPolicy
			{
				/// <summary>Nothing is written at all</summary>
				Omit,
				/// <summary>Nothing is written, unless the settings ask for null members</summary>
				NilWhenSettingsAsk,
				/// <summary>The nil element is always written</summary>
				Nil,
			}

			/// <summary>Writes one member projected as an XML ATTRIBUTE of the element currently open</summary>
			/// <remarks>
			/// <para>A null value makes the attribute ABSENT, whatever the null policy of the member says: an attribute has no
			/// nil form (there is no attribute of an attribute), so "present but null" is not a state this wire can express.</para>
			/// <para>A custom converter has no say here: its XML facet writes an ELEMENT and structurally cannot produce an attribute
			/// value, which is why the pair is refused at generation time (CXML0009) rather than silently bypassed.</para>
			/// </remarks>
			private void WriteXmlAttributeMember(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
			{
				sb.NewLine();
				sb.Comment($"{member.Type.Name} {member.MemberName} => @{GetXmlMemberName(member)}{(member.IgnoreCondition != null ? $" [{member.IgnoreCondition}]" : "")}");

				if (member.CustomConverterType is not null)
				{ // CXML0009 already reported this at generation time: emit something that compiles and fails loudly, and do NOT
				  // fall through to the scalar path, which would write the attribute the converter was supposed to own
					this.XmlNeedsNotSupportedHelper = true;
					sb.AppendLine($"FailXmlNotSupported(typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName}), {CSharpCodeBuilder.Constant(member.MemberName)}, \"an attribute-projected member cannot be written by a custom converter, whose XML facet produces an element\"); // see CXML0009");
					return;
				}

				string nameRef = names.Ref(GetXmlMemberName(member));
				string local = "__x_" + member.MemberName;

				sb.AppendLine($"var {local} = {GetXmlMemberReadExpr(typeDef, member)};");

				// CXML0003 already refused any attribute-projected member with no lexical form, so this cannot fail to resolve
				var text = GetXmlScalarText(member.Type, local, member.EnumFormat);
				if (text is null)
				{
					this.XmlNeedsNotSupportedHelper = true;
					sb.AppendLine($"FailXmlNotSupported(typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName}), {CSharpCodeBuilder.Constant(member.MemberName)}, \"an XML attribute value is text, and this type has no lexical form\"); // see CXML0003");
					return;
				}

				bool guarded = OpenXmlDefaultGuard(sb, member, local);

				if (member.Type.CanBeNull())
				{
					sb.AppendLine($"if ({local} is not null)");
					sb.EnterBlock();
					sb.AppendLine($"emitter.WriteAttribute(in {nameRef}, {text.Value.Text});");
					sb.LeaveBlock();
				}
				else
				{
					sb.AppendLine($"emitter.WriteAttribute(in {nameRef}, {text.Value.Text});");
				}

				if (guarded)
				{
					sb.LeaveBlock();
				}
			}

			/// <summary>Writes one member projected as a child ELEMENT of the element currently open</summary>
			private void WriteXmlElementMember(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
			{
				string name = GetXmlMemberName(member);
				string nameRef = names.Ref(name);
				string local = "__x_" + member.MemberName;

				sb.NewLine();
				sb.Comment($"{member.Type.Name} {member.MemberName} => <{name}>{(member.IgnoreCondition != null ? $" [{member.IgnoreCondition}]" : "")}");
				sb.AppendLine($"var {local} = {GetXmlMemberReadExpr(typeDef, member)};");

				// [JsonIgnore(Condition = ...)] is the per-member override of the settings-level rule, and it is the SAME
				// vocabulary as the JSON side: a member pinned there is pinned here, a member omitted there is omitted here
				var policy = member.IgnoreCondition switch
				{
					"Never" => XmlNullPolicy.Nil,
					"WhenWritingNull" => XmlNullPolicy.Omit,
					_ => XmlNullPolicy.NilWhenSettingsAsk,
				};

				bool guarded = OpenXmlDefaultGuard(sb, member, local);

				WriteXmlValueElement(sb, names, member, member.Type, local, nameRef, member.XmlItemName, ResolveXmlDictionaryFormat(member.XmlDictionaryFormat), member.MemberName, policy);

				if (guarded)
				{
					sb.LeaveBlock();
				}
			}

			/// <summary>Opens the <c>WhenWritingDefault</c> guard of a member, and returns whether a block was opened</summary>
			private static bool OpenXmlDefaultGuard(CSharpCodeBuilder sb, CrystalJsonMemberMetadata member, string local)
			{
				if (member.IgnoreCondition != "WhenWritingDefault") return false;

				sb.AppendLine($"if (!{EqualityComparerFullName}<{member.Type.FullyQualifiedNameAnnotated}>.Default.Equals({local}, {GetForgivingDefaultLiteral(member)}))");
				sb.EnterBlock();
				return true;
			}

			/// <summary>Writes an element of name <paramref name="nameRef"/> holding <paramref name="valueExpr"/>, handling the null case per <paramref name="policy"/></summary>
			private void WriteXmlValueElement(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonMemberMetadata? member, TypeMetadata type, string valueExpr, string nameRef, string? itemName, string dictionaryFormat, string scope, XmlNullPolicy policy)
			{
				if (!type.CanBeNull())
				{
					WriteXmlValueContent(sb, names, member, type, valueExpr, nameRef, itemName, dictionaryFormat, scope);
					return;
				}

				sb.AppendLine($"if ({valueExpr} is not null)");
				sb.EnterBlock();
				WriteXmlValueContent(sb, names, member, type, valueExpr, nameRef, itemName, dictionaryFormat, scope);
				sb.LeaveBlock();

				switch (policy)
				{
					case XmlNullPolicy.Nil:
					{
						sb.AppendLine("else");
						sb.EnterBlock("else");
						WriteXmlNilElement(sb, names, nameRef);
						sb.LeaveBlock("else");
						break;
					}
					case XmlNullPolicy.NilWhenSettingsAsk:
					{
						sb.AppendLine($"else if ({CrystalJsonSettingsExtensionsFullName}.IncludesNullMembers(settings))");
						sb.EnterBlock("else");
						WriteXmlNilElement(sb, names, nameRef);
						sb.LeaveBlock("else");
						break;
					}
				}
			}

			/// <summary>Writes the element of a value known to be non-null: the one place that decides between the shapes (scalar text, nested type, sequence, dictionary)</summary>
			private void WriteXmlValueContent(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonMemberMetadata? member, TypeMetadata type, string valueExpr, string nameRef, string? itemName, string dictionaryFormat, string scope)
			{
				var actual = type.NullableOfType ?? type;

				// 1) a member converter takes over the whole projection of the member (only ever at member level)
				if (member?.CustomConverterType is not null)
				{
					WriteXmlCustomConverterElement(sb, member, valueExpr, nameRef);
					return;
				}

				// 2) a scalar is text inside the element
				// the member's EnumFormat applies to the member's OWN value: an item of a collection, or an entry of a
				// dictionary, is not the member, and carries none of the member's vocabulary.
				// TypeMetadata is a value-equality record, so the test is value equality and not identity: the metadata of one
				// type is not guaranteed to be the same INSTANCE everywhere it is described. It stays exact all the same,
				// because an item type (or a dictionary value type) is never equal to the collection member's own type.
				var text = GetXmlScalarText(type, valueExpr, type == member?.Type ? member.EnumFormat : null);
				if (text is not null)
				{
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"emitter.{(text.Value.NeedsEscaping ? "WriteText" : "WriteRawAscii")}({text.Value.Text});");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				// 3) a type of this container writes itself, under the name given here
				if (IsLocallyGeneratedType(type, out var target, out _))
				{
					// one level deeper: this is a nested element, and the callee's own guard is what stops a cycle running through it
					sb.AppendLine($"{GetLocalSerializerRef(target)}.WriteXmlElement(ref emitter, in {nameRef}, {valueExpr}{(type.NullableOfType is not null ? ".Value" : "")}, settings, {XmlDepthParameterName} + 1);");
					return;
				}

				// 4) a type that writes its own content, inside the element opened here
				if (actual.IsCrystalXmlSerializable())
				{
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"{valueExpr}{(type.NullableOfType is not null ? ".Value" : "")}.WriteXml(ref emitter);");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				// 5) a JSON DOM value has no XML projection of its own (the two DOMs are not the same shape)
				if (actual.JsonType() is not JsonPrimitiveType.None)
				{
					WriteXmlNotSupported(sb, actual, scope);
					return;
				}

				// 6) a dictionary, in the resolved shape
				if (actual.KeyType is not null && actual.ValueType is not null)
				{
					WriteXmlDictionaryContent(sb, names, actual, valueExpr, nameRef, itemName, dictionaryFormat, scope);
					return;
				}

				// 7) a sequence: wrapped when it was given an item name, bare repetition otherwise
				if (actual.ElementType is not null)
				{
					WriteXmlSequenceContent(sb, names, actual, valueExpr, nameRef, itemName, dictionaryFormat, scope);
					return;
				}

				WriteXmlNotSupported(sb, actual, scope);
			}

			/// <summary>Writes a member whose projection is owned by a custom converter</summary>
			/// <remarks>
			/// <para>Routed through the interface so that an explicit implementation works too. This is the one write path that
			/// validates a name at write time: the converter facet only exposes <c>rootName</c> as text, so the member's name goes
			/// through <c>XmlName.Create</c> on every call (the name itself was already validated at generation time).</para>
			/// <para>A null member never reaches here: the profile decides the null case (absent, or <c>nil</c>) before this call, so
			/// a facet declared for <c>T?</c> only changes the type argument of the cast, not who answers for the absent value.</para>
			/// </remarks>
			private void WriteXmlCustomConverterElement(CSharpCodeBuilder sb, CrystalJsonMemberMetadata member, string valueExpr, string nameRef)
			{
				var facetType = member.CustomConverterXmlFacetDeclaredForNullable ? member.Type : (member.Type.NullableOfType ?? member.Type);
				string arg = valueExpr + (member.Type.NullableOfType is not null && !member.CustomConverterXmlFacetDeclaredForNullable ? ".Value" : "");

				if (!member.CustomConverterHasXmlSerializer)
				{ // CXML0008 already reported this at generation time: emit something that compiles and fails loudly
					this.XmlNeedsNotSupportedHelper = true;
					sb.AppendLine($"FailXmlNotSupported(typeof({facetType.FullyQualifiedName}), {CSharpCodeBuilder.Constant(member.MemberName)}, \"the custom converter declared for this member does not implement the ICrystalXmlSerializer<T> facet\"); // see CXML0008");
					return;
				}

				sb.AppendLine($"(({ICrystalXmlSerializerFullName}<{facetType.FullyQualifiedName}>) {GetMemberConverterRef(member)}).WriteXml(ref emitter, {arg}, settings, {nameRef}.Text); // member-converter");
			}

			/// <summary>Writes a sequence member: <c>&lt;tags&gt;a&lt;/tags&gt;&lt;tags&gt;b&lt;/tags&gt;</c> by default, wrapped when an item name was declared</summary>
			private void WriteXmlSequenceContent(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata type, string valueExpr, string nameRef, string? itemName, string dictionaryFormat, string scope)
			{
				var itemType = type.ElementType!;
				string item = "__i_" + scope;
				string itemScope = scope + "_i";

				if (itemName is null)
				{
					sb.Comment("no ItemName: the member name repeats for each item, and an empty sequence writes NOTHING at all");
					sb.AppendLine($"foreach (var {item} in {valueExpr})");
					sb.EnterBlock("foreach");
					WriteXmlItemElement(sb, names, itemType, item, nameRef, dictionaryFormat, itemScope);
					sb.LeaveBlock("foreach");
					return;
				}

				string itemRef = names.Ref(itemName);
				sb.Comment("ItemName declared: the wrapped form, whose empty case is the self-closing wrapper");
				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				sb.AppendLine($"foreach (var {item} in {valueExpr})");
				sb.EnterBlock("foreach");
				WriteXmlItemElement(sb, names, itemType, item, itemRef, dictionaryFormat, itemScope);
				sb.LeaveBlock("foreach");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
			}

			/// <summary>Writes one item of a sequence, or one value of a dictionary: the same rules as a member, minus the member vocabulary</summary>
			/// <remarks>An item carries no <c>[XmlProperty]</c> of its own, so it has no item name to give (a bare sequence of bare
			/// sequences is refused at generation time, CXML0006) and no null policy of its own: a null item follows the settings,
			/// exactly like an unannotated member.</remarks>
			private void WriteXmlItemElement(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata itemType, string valueExpr, string nameRef, string dictionaryFormat, string scope)
			{
				WriteXmlValueElement(sb, names, member: null, itemType, valueExpr, nameRef, itemName: null, dictionaryFormat, scope, XmlNullPolicy.NilWhenSettingsAsk);
			}

			/// <summary>Writes a dictionary member, in the resolved shape</summary>
			private void WriteXmlDictionaryContent(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata type, string valueExpr, string nameRef, string? itemName, string dictionaryFormat, string scope)
			{
				var keyType = type.KeyType!;
				var valueType = type.ValueType!;
				string entry = "__e_" + scope;
				string entryScope = scope + "_e";

				var keyText = GetXmlScalarText(keyType, entry + ".Key");
				if (keyText is null)
				{ // a key with no lexical form cannot name anything, in any of the four shapes
					WriteXmlNotSupported(sb, keyType, scope);
					return;
				}

				if (dictionaryFormat == XmlDictionaryFormatDirect)
				{
					sb.Comment("Direct: the KEY names the element, so it must be a legal XML name - an invalid key raises CrystalXmlInvalidNameException instead of corrupting the document");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"foreach (var {entry} in {valueExpr})");
					sb.EnterBlock("foreach");
					sb.AppendLine($"var __k_{entryScope} = {XmlNameFullName}.Create({keyText.Value.Text});");
					WriteXmlItemElement(sb, names, valueType, entry + ".Value", $"__k_{entryScope}", dictionaryFormat, entryScope);
					sb.LeaveBlock("foreach");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				string entryRef = names.Ref(itemName ?? XmlDefaultEntryName);

				if (dictionaryFormat == XmlDictionaryFormatKeyValueElements)
				{
					string keyRef = names.Ref(XmlKeyElementName);
					string valueRef = names.Ref(XmlValueElementName);
					sb.Comment("KeyValueElements: the entry holds the key and the value as two child elements");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"foreach (var {entry} in {valueExpr})");
					sb.EnterBlock("foreach");
					sb.AppendLine($"emitter.WriteStartElement(in {entryRef});");
					sb.AppendLine($"emitter.WriteStartElement(in {keyRef});");
					sb.AppendLine($"emitter.{(keyText.Value.NeedsEscaping ? "WriteText" : "WriteRawAscii")}({keyText.Value.Text});");
					sb.AppendLine($"emitter.WriteEndElement(in {keyRef});");
					WriteXmlItemElement(sb, names, valueType, entry + ".Value", valueRef, dictionaryFormat, entryScope);
					sb.AppendLine($"emitter.WriteEndElement(in {entryRef});");
					sb.LeaveBlock("foreach");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				// the two attribute shapes carry the VALUE as text, so only a scalar value has a projection there: a nested
				// element inside an entry, or a mangled value, would both be a shape nobody asked for.
				// BACKSTOP only: the parser already refused this pair with CXML0011, at the declaration that picked the shape
				var entryValueText = GetXmlScalarText(valueType, entry + ".Value");
				if (entryValueText is null)
				{
					sb.AppendLine($"#error A dictionary whose values are of type {valueType.FullName} cannot use the {dictionaryFormat} shape: that shape carries the value as text, and this type has no lexical form. Use Direct or KeyValueElements, which can hold a nested element.");
					return;
				}

				string keyAttrRef = names.Ref(XmlKeyAttributeName);

				if (dictionaryFormat == XmlDictionaryFormatKeyAttribute)
				{
					sb.Comment("KeyAttribute: the entry carries the key as an attribute, and the value as its text content");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"foreach (var {entry} in {valueExpr})");
					sb.EnterBlock("foreach");
					sb.AppendLine($"emitter.WriteStartElement(in {entryRef});");
					sb.AppendLine($"emitter.WriteAttribute(in {keyAttrRef}, {keyText.Value.Text});");
					WriteXmlEntryValueText(sb, names, valueType, entry + ".Value", entryValueText.Value, asAttribute: null);
					sb.AppendLine($"emitter.WriteEndElement(in {entryRef});");
					sb.LeaveBlock("foreach");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				if (dictionaryFormat == XmlDictionaryFormatKeyValueAttributes)
				{
					string valueAttrRef = names.Ref(XmlValueAttributeName);
					sb.Comment("KeyValueAttributes: the entry carries both the key and the value as attributes, and holds no content");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"foreach (var {entry} in {valueExpr})");
					sb.EnterBlock("foreach");
					sb.AppendLine($"emitter.WriteStartElement(in {entryRef});");
					sb.AppendLine($"emitter.WriteAttribute(in {keyAttrRef}, {keyText.Value.Text});");
					WriteXmlEntryValueText(sb, names, valueType, entry + ".Value", entryValueText.Value, asAttribute: valueAttrRef);
					sb.AppendLine($"emitter.WriteEndElement(in {entryRef});");
					sb.LeaveBlock("foreach");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				sb.AppendLine($"#error Unknown XML dictionary format '{dictionaryFormat}': this emitter handles Direct, KeyValueElements, KeyAttribute and KeyValueAttributes. A member added to the XmlDictionaryFormat enum needs its own branch in WriteXmlDictionaryContent (CrystalJsonGenerator.Emitter.Xml.cs), and a name for the entries it produces.");
			}

			/// <summary>Writes the value of a dictionary entry in one of the attribute shapes: as text content, or as an attribute</summary>
			/// <remarks>A null value makes the text (or the attribute) absent; the settings can still mark the entry itself nil, which
			/// is the only unambiguous marker either shape has room for.</remarks>
			private static void WriteXmlEntryValueText(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata valueType, string rawExpr, (string Text, bool NeedsEscaping) text, string? asAttribute)
			{
				string write = asAttribute is not null
					? $"emitter.WriteAttribute(in {asAttribute}, {text.Text});"
					: $"emitter.{(text.NeedsEscaping ? "WriteText" : "WriteRawAscii")}({text.Text});";

				if (!valueType.CanBeNull())
				{
					sb.AppendLine(write);
					return;
				}

				sb.AppendLine($"if ({rawExpr} is not null)");
				sb.EnterBlock();
				sb.AppendLine(write);
				sb.LeaveBlock();
				sb.AppendLine($"else if ({CrystalJsonSettingsExtensionsFullName}.IncludesNullMembers(settings))");
				sb.EnterBlock("else");
				sb.AppendLine($"emitter.WriteAttribute(in {names.Ref(XmlNilAttributeName)}, \"true\");");
				sb.LeaveBlock("else");
			}

			/// <summary>Emits the call that fails loudly for a type with no XML projection</summary>
			private void WriteXmlNotSupported(CSharpCodeBuilder sb, TypeMetadata type, string scope)
			{
				this.XmlNeedsNotSupportedHelper = true;
				sb.AppendLine($"FailXmlNotSupported(typeof({type.FullyQualifiedName}), {CSharpCodeBuilder.Constant(scope)}, \"no XML projection exists for this type: it has no lexical form, no generated serializer in this container, and does not implement ICrystalXmlSerializable\");");
			}

			/// <summary>Whether the converter currently being emitted needs the "no XML projection" helper</summary>
			private bool XmlNeedsNotSupportedHelper { get; set; }

			/// <summary>Emits the helper that raises <c>CrystalXmlNotSupportedException</c></summary>
			/// <remarks>A method call rather than an inline <c>throw</c>, so that the statements after it stay reachable for the
			/// compiler and the generated body carries no unreachable-code warning.</remarks>
			private static void WriteXmlNotSupportedHelper(CSharpCodeBuilder sb)
			{
				sb.Comment("this member has no XML projection: fail at the exact member, rather than write a document nobody asked for");
				sb.Comment("the REASON is passed in: the same helper backstops several distinct refusals, and a message naming only the most common one would be a lie on the others");
				sb.AppendLine($"private static void FailXmlNotSupported({SystemTypeFullName} type, string member, string reason)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {CrystalXmlNotSupportedExceptionFullName}(type, \"Cannot write member '\" + member + \"' to XML: \" + reason + \" (type '\" + type.Name + \"').\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			#endregion

			#region Scalars...

			/// <summary>Returns the resolved dictionary shape of a member: its own override, else the container's default, else the profile's</summary>
			private string ResolveXmlDictionaryFormat(string? memberFormat)
			{
				if (memberFormat is not (null or XmlDictionaryFormatDefault)) return memberFormat;
				if (this.Metadata.XmlDictionaryFormat is { } containerFormat && containerFormat != XmlDictionaryFormatDefault) return containerFormat;
				return XmlDictionaryFormatDirect;
			}

			/// <summary>Returns the expression yielding the lexical text of a scalar value, and whether the emitter must escape it</summary>
			/// <returns><see langword="null"/> when the type has no lexical form, which is what makes it not a scalar</returns>
			/// <param name="type">Type of the value to format</param>
			/// <param name="valueExpr">Expression that reads the value</param>
			/// <param name="enumFormat">The member's <c>[JsonProperty(EnumFormat = ...)]</c>, when the value IS the member (an item or a dictionary entry carries no member vocabulary of its own)</param>
			/// <remarks>
			/// <para>The set here is exactly <c>Parser.IsXmlScalar</c>'s: a member the parser accepted as an XML attribute is one this
			/// method can format, and a member it refused (CXML0003) never reaches an attribute position.</para>
			/// <para>Escaping is decided per type, not per value: a number, a date, a GUID or a base64 payload cannot contain a
			/// character that needs escaping, so they go through the raw path; text, a char, an enum label and a URI can.</para>
			/// </remarks>
			private (string Text, bool NeedsEscaping)? GetXmlScalarText(TypeMetadata type, string valueExpr, string? enumFormat = null)
			{
				var actual = type.NullableOfType ?? type;
				string expr = type.NullableOfType is null ? valueExpr : valueExpr + ".Value";

				if (actual.IsEnum())
				{
					if (enumFormat == "Number")
					{ // [JsonProperty(EnumFormat = Number)] forces the numeric form on BOTH wires: honoring it on one only is
					  // exactly the silent cross-wire divergence this surface refuses
						return (FormatXmlScalar(GetXmlEnumUnderlyingFamily(actual), $"({GetXmlEnumUnderlyingKeyword(actual)}) {expr}"), false);
					}

					// the label comes from a generated switch over the declared members: never ASCII-guaranteed (a C# identifier
					// may hold any letter, and a custom token any character at all), so it takes the escaping path
					return ($"{this.XmlEnums.Ref(actual)}({expr})", true);
				}

				switch (actual.SpecialType)
				{
					case SpecialType.System_String: return (expr, true);
					case SpecialType.System_Boolean: return (FormatXmlScalar("Boolean", expr), false);
					case SpecialType.System_Char: return (FormatXmlScalar("Char", expr), true);
					case SpecialType.System_SByte: return (FormatXmlScalar("SByte", expr), false);
					case SpecialType.System_Byte: return (FormatXmlScalar("Byte", expr), false);
					case SpecialType.System_Int16: return (FormatXmlScalar("Int16", expr), false);
					case SpecialType.System_UInt16: return (FormatXmlScalar("UInt16", expr), false);
					case SpecialType.System_Int32: return (FormatXmlScalar("Int32", expr), false);
					case SpecialType.System_UInt32: return (FormatXmlScalar("UInt32", expr), false);
					case SpecialType.System_Int64: return (FormatXmlScalar("Int64", expr), false);
					case SpecialType.System_UInt64: return (FormatXmlScalar("UInt64", expr), false);
					case SpecialType.System_Single: return (FormatXmlScalar("Single", expr), false);
					case SpecialType.System_Double: return (FormatXmlScalar("Double", expr), false);
					case SpecialType.System_Decimal: return (FormatXmlScalar("Decimal", expr), false);
					case SpecialType.System_DateTime: return (FormatXmlScalar("DateTime", expr), false);
				}

				if (actual.NameSpace == "System")
				{
					switch (actual.Name)
					{
						case "TimeSpan": return (FormatXmlScalar("Duration", expr), false);
						case "Guid": return (FormatXmlScalar("Guid", expr), false);
						// a URI can legally contain '&', which the emitter must escape
						case "Uri": return (FormatXmlScalar("Uri", expr), true);
					}
				}

				if (actual.TypeKind == TypeKind.Array && actual.ElementType is { SpecialType: SpecialType.System_Byte })
				{
					return (FormatXmlScalar("Base64", expr), false);
				}

				return null;
			}

			/// <summary>Returns the call to the profile's formatter for one scalar family</summary>
			/// <remarks>The two families are named per PROFILE, not per type, so the selection is one prefix here rather than a
			/// per-type table of "which types happen to format the same on both wires" (only <c>char</c> actually differs).</remarks>
			private string FormatXmlScalar(string family, string valueExpr) => $"{CrystalXmlFormattersFullName}.{(this.WritesXmlDcs ? "FormatDcs" : "FormatModern")}{family}({valueExpr})";

			#endregion

		}

	}

}
