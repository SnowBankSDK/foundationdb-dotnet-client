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
	using System.Linq;
	using Microsoft.CodeAnalysis;

	public partial class CrystalJsonSourceGenerator
	{

		/// <summary>DataContract (compat) XML emission: the output a <c>DataContractSerializer</c> writing through a namespace-stripping writer produces</summary>
		/// <remarks>
		/// <para>Every rule below was measured against a live <c>DataContractSerializer</c> and is TRANSPOSED to generation
		/// time: the contract names, the member order and the element shapes are all computed by the generator, and the
		/// emitted body is a flat sequence of write events with no contract lookup left at run time.</para>
		/// <para>Three deviations from that format are deliberate, and are requirements rather than options:</para>
		/// <list type="number">
		/// <item>dictionary entry names are emitted UNHASHED (<c>KeyValueOfstringShelf</c>, never <c>KeyValueOfstringShelfRgT45H4A</c>):
		/// the digest is an undocumented .NET internal, and no measured consumer reads any <c>KeyValueOf*</c> element;</item>
		/// <item>C0 control characters are dropped at the value level rather than emitted as the character references that make the
		/// legacy format unparseable (this one lives in the writer, <c>CrystalXmlWriter</c>, not here);</item>
		/// <item>failures raise the typed <c>CrystalXml*</c> exceptions instead of <c>SerializationException</c>.</item>
		/// </list>
		/// </remarks>
		internal sealed partial class Emitter
		{

			#region Resolved names of the compat format...

			/// <summary>Name of the attribute carrying the contract name of the runtime type, when it differs from the declared one</summary>
			/// <remarks>The reference format writes <c>i:type</c>, and the namespace-stripping writer this profile replicates leaves
			/// the local name only. It is a FIXED name here, unlike the general format's, which follows the JSON discriminator.</remarks>
			private const string XmlDcsTypeAttributeName = "type";

			/// <summary>Names of the two child elements of a dictionary entry</summary>
			private const string XmlDcsKeyElementName = "Key";

			/// <inheritdoc cref="XmlDcsKeyElementName"/>
			private const string XmlDcsValueElementName = "Value";

			/// <summary>Contract name of <c>object</c>, and of anything whose static type carries no contract of its own</summary>
			private const string XmlDcsAnyTypeName = "anyType";

			/// <summary>Settings the generated entry points fall back on when the caller passes none</summary>
			/// <remarks>The compat XML format inherits the null policy of its JSON profile, whose <c>ShowNullMembers</c> is ON: a null
			/// member is <c>&lt;X nil="true" /&gt;</c> by default. A caller passing <c>WithoutNullMembers()</c> drops those elements,
			/// which changes what an XSLT existence test sees and has to be audited before use.</remarks>
			private const string XmlDcsDefaultSettings = KnownTypeSymbols.CrystalXmlSettingsFullName + ".DataContractCompat";

			private const string SerializationInfoFullName = "global::System.Runtime.Serialization.SerializationInfo";

			private const string SerializationEntryFullName = "global::System.Runtime.Serialization.SerializationEntry";

			private const string FormatterConverterFullName = "global::System.Runtime.Serialization.FormatterConverter";

			private const string ISerializableFullName = "global::System.Runtime.Serialization.ISerializable";

			private const string XmlConvertFullName = "global::System.Xml.XmlConvert";

			private const string StringBuilderFullName = "global::System.Text.StringBuilder";

			#endregion

			#region Contract names...

			/// <summary>One lexical type of the DataContract format: how it is recognized, what it is called, and how it is written</summary>
			/// <param name="Special">Roslyn <see cref="SpecialType"/> of the type, or <see cref="SpecialType.None"/> when it has none (<c>TimeSpan</c>, <c>Guid</c>, <c>Uri</c>, <c>DateTimeOffset</c>, <c>byte[]</c>)</param>
			/// <param name="ClrName">Simple CLR name of the type inside the <c>System</c> namespace, or <see langword="null"/> when it is not a named <c>System</c> type (<c>byte[]</c>)</param>
			/// <param name="Keyword">C# type this lexical family is matched by in a generated <c>anyType</c> switch, or <see langword="null"/> when it takes no case there</param>
			/// <param name="Contract">The xsd contract name that appears in the output</param>
			/// <param name="Family">Formatter family of <c>FormatXmlScalar</c>, or <see langword="null"/> for a string, which is written raw</param>
			/// <param name="Escaped">Whether the rendered text has to go through the escaping writer</param>
			private readonly record struct XmlDcsLexicalType(SpecialType Special, string? ClrName, string? Keyword, string Contract, string? Family, bool Escaped);

			/// <summary>THE table of the lexical types of this format: one row per type, read through three different keys</summary>
			/// <remarks>
			/// <para>All three lookups (by <see cref="SpecialType"/>, by the CLR name of a generic argument, by the C# keyword of an
			/// <c>anyType</c> case) project THIS table, so they cannot desynchronize; the <c>sbyte</c> / <c>byte</c> transposition
			/// below (<c>sbyte</c> IS <c>byte</c> on the xsd format, and <c>byte</c> is <c>unsignedByte</c>) is the classic way
			/// independent copies drift.</para>
			/// <para>The order of the rows is the order the <c>anyType</c> cases are emitted in.</para>
			/// </remarks>
			private static readonly XmlDcsLexicalType[] XmlDcsLexicalTypes =
			[
				new(SpecialType.System_String, "String", "string", "string", null, true),
				new(SpecialType.System_Boolean, "Boolean", "bool", "boolean", "Boolean", false),
				new(SpecialType.System_Int32, "Int32", "int", "int", "Int32", false),
				new(SpecialType.System_Int64, "Int64", "long", "long", "Int64", false),
				new(SpecialType.System_Int16, "Int16", "short", "short", "Int16", false),
				new(SpecialType.System_SByte, "SByte", "sbyte", "byte", "SByte", false),
				new(SpecialType.System_Byte, "Byte", "byte", "unsignedByte", "Byte", false),
				new(SpecialType.System_UInt16, "UInt16", "ushort", "unsignedShort", "UInt16", false),
				new(SpecialType.System_UInt32, "UInt32", "uint", "unsignedInt", "UInt32", false),
				new(SpecialType.System_UInt64, "UInt64", "ulong", "unsignedLong", "UInt64", false),
				new(SpecialType.System_Single, "Single", "float", "float", "Single", false),
				new(SpecialType.System_Double, "Double", "double", "double", "Double", false),
				new(SpecialType.System_Decimal, "Decimal", "decimal", "decimal", "Decimal", false),
				new(SpecialType.System_DateTime, "DateTime", "global::System.DateTime", "dateTime", "DateTime", false),
				new(SpecialType.None, "TimeSpan", "global::System.TimeSpan", "duration", "Duration", false),
				new(SpecialType.None, "Guid", "global::System.Guid", "guid", "Guid", false),
				new(SpecialType.System_Char, "Char", "char", "char", "Char", false),
				// byte[] is a base64 SCALAR on this format, not a sequence of unsignedByte, so it belongs here and not in the sequence path
				new(SpecialType.None, null, "byte[]", "base64Binary", "Base64", false),
				new(SpecialType.None, "Uri", "global::System.Uri", "anyURI", "Uri", true),
				// the two rows below take no anyType case: a bare object is written by the closed switch's own last case, and a
				// DateTimeOffset is not a lexical form at all (it has a built-in two-member contract, see WriteXmlDcsDateTimeOffsetElement)
				new(SpecialType.System_Object, "Object", null, XmlDcsAnyTypeName, null, false),
				new(SpecialType.None, "DateTimeOffset", null, "DateTimeOffset", null, false),
			];

			/// <summary>Returns the contract name of a lexical type, matched on its <see cref="SpecialType"/> first and on its <c>System.*</c> name otherwise</summary>
			private static string? GetXmlDcsLexicalContractName(SpecialType special, string nameSpace, string name)
			{
				if (special != SpecialType.None)
				{
					foreach (var entry in XmlDcsLexicalTypes)
					{
						if (entry.Special == special) return entry.Contract;
					}
				}

				if (nameSpace == "System")
				{ // the types the BCL gives no SpecialType to (TimeSpan, Guid, Uri, DateTimeOffset), matched by name
					foreach (var entry in XmlDcsLexicalTypes)
					{
						if (entry.ClrName == name) return entry.Contract;
					}
				}

				return null;
			}

			/// <summary>Returns the DataContract name of a type, XML-encoded and format-ready, or <see langword="null"/> when this emission cannot derive one</summary>
			/// <remarks>
			/// <para>The DataContract naming rules, computed at generation time: the xsd name for a primitive, the CLR
			/// type name (nested types joined with <c>'.'</c>, generics as <c>XOfArgs</c>) for anything else, <c>ArrayOfX</c> for a
			/// sequence and <c>ArrayOfKeyValueOfKV</c> for a dictionary, all through <c>XmlConvert.EncodeLocalName</c>.</para>
			/// <para><see langword="null"/> means INEXPRESSIBLE, not "no name": the caller turns it into a <c>#error</c>, because a
			/// document written under a guessed name is exactly the silent divergence this profile exists to prevent.</para>
			/// </remarks>
			private string? GetXmlDcsContractName(TypeMetadata type)
			{
				var actual = type.NullableOfType ?? type;

				if (GetXmlDcsLexicalContractName(actual.SpecialType, actual.NameSpace, actual.Name) is { } lexical)
				{
					return lexical;
				}

				if (IsXmlDcsByteArray(actual))
				{ // byte[] is a scalar on this format, NOT a sequence of unsignedByte
					return "base64Binary";
				}

				if (actual.KeyType is not null && actual.ValueType is not null)
				{
					string? key = GetXmlDcsContractName(actual.KeyType);
					string? value = GetXmlDcsContractName(actual.ValueType);
					if (key is null || value is null) return null;
					// DEVIATION 1: the reference format appends an 8-character digest of the argument namespaces here when one of
					// them is not built-in; this emission never does
					return "ArrayOfKeyValueOf" + key + value;
				}

				if (actual.ElementType is not null)
				{
					string? item = GetXmlDcsContractName(actual.ElementType);
					return item is null ? null : "ArrayOf" + item;
				}

				return GetXmlDcsDeclaredContractName(actual);
			}

			/// <summary>Returns the contract name of a user type: its <c>[DataContract(Name = ...)]</c>, else its declaration name</summary>
			/// <remarks>
			/// <para>The declared name is read from the container's own metadata when the type is registered there, and from the type
			/// reference otherwise - which is how a RENAMED ENUM gets its contract name: an enum is never registered as a container
			/// type (it has no members), yet <c>[DataContract(Name = "Support")]</c> on it renames every element the output derives from
			/// it (a list item, both sides of a <c>KeyValueOfXY</c>, a generic argument).</para>
			/// <para>Encoding happens exactly ONCE, on the parts that come from source text. The pieces composed into a generic name
			/// are contract names that are already encoded, and re-encoding them would turn <c>with_x0020_space</c> into
			/// <c>with_x005F_x0020_space</c>; the reference format writes <c>BoxOfwith_x0020_space</c>.</para>
			/// </remarks>
			private string? GetXmlDcsDeclaredContractName(TypeMetadata type)
			{
				string? declared = (IsLocallyGeneratedType(type.Ref, out var typeDef) ? typeDef.DataContractName : null) ?? type.Ref.DataContractName;

				if (declared is not null)
				{
					if (!type.IsGenericType())
					{
						return EncodeXmlDcsNamePart(declared);
					}

					string? expanded = ExpandXmlDcsGenericName(declared, type);
					return expanded is { Length: > 0 } ? expanded : null;
				}

				string name = EncodeXmlDcsNamePart(type.DeclaringTypeNames is not null ? type.DeclaringTypeNames + "." + type.Name : type.Name);

				if (type.IsGenericType())
				{
					var args = new List<string>();
					foreach (var arg in type.TypeArguments)
					{
						string? argName = GetXmlDcsContractNameOfArgument(arg);
						if (argName is null) return null;
						args.Add(argName);
					}
					// DEVIATION 1 again: the reference format appends the argument-namespace digest here too (BoxOfSupportCmwZw7JZ)
					name += "Of" + string.Concat(args);
				}

				return name;
			}

			/// <summary>Encodes one part of a contract name that comes from SOURCE TEXT (a declared name, a type name), which is the only kind that is ever encoded</summary>
			private static string EncodeXmlDcsNamePart(string part) => part.Length == 0 ? "" : System.Xml.XmlConvert.EncodeLocalName(part);

			/// <summary>Expands the <c>{0}</c> / <c>{#}</c> placeholders of a <c>[DataContract(Name = "XOf{0}")]</c> on a generic type</summary>
			/// <remarks>The <c>{#}</c> placeholder asks for the argument-namespace digest, which this emission never writes
			/// (deviation 1), so it expands to nothing. An out-of-range index, or an argument with no contract name, makes the whole
			/// name inexpressible. The LITERAL runs of the format are encoded, the substituted arguments are not: they are contract
			/// names, which are already encoded.</remarks>
			private string? ExpandXmlDcsGenericName(string format, TypeMetadata type)
			{
				var sb = new System.Text.StringBuilder(format.Length);
				var literal = new System.Text.StringBuilder(format.Length);

				for (int i = 0; i < format.Length; i++)
				{
					char c = format[i];
					if (c == '{')
					{
						int close = format.IndexOf('}', i + 1);
						if (close > i)
						{
							string token = format.Substring(i + 1, close - i - 1);
							if (token == "#")
							{ // the digest, deliberately omitted
								i = close;
								continue;
							}
							if (int.TryParse(token, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index))
							{
								if (index >= type.TypeArguments.Count) return null;
								string? argName = GetXmlDcsContractNameOfArgument(type.TypeArguments[index]);
								if (argName is null) return null;
								sb.Append(EncodeXmlDcsNamePart(literal.ToString()));
								literal.Clear();
								sb.Append(argName);
								i = close;
								continue;
							}
						}
					}
					literal.Append(c);
				}

				sb.Append(EncodeXmlDcsNamePart(literal.ToString()));
				return sb.ToString();
			}

			/// <summary>Returns the contract name of one generic ARGUMENT, which the metadata carries as a reference rather than as a full type</summary>
			/// <remarks>A type argument is a <see cref="TypeRef"/>, so its own element/key/value types are unknown here: an argument
			/// that is itself a closed generic (a <c>List&lt;string&gt;</c> inside an <c>Envelope&lt;&gt;</c>) is reported as
			/// inexpressible rather than named from its simple name, which would produce <c>EnvelopeList</c> where the reference format
			/// writes <c>EnvelopeArrayOfstring</c>.</remarks>
			private string? GetXmlDcsContractNameOfArgument(TypeRef arg)
			{
				// a TypeRef carries no SpecialType, so the lexical table is read through its CLR-name projection only
				if (GetXmlDcsLexicalContractName(SpecialType.None, arg.NameSpace, arg.Name) is { } lexical)
				{
					return lexical;
				}

				if (IsLocallyGeneratedType(arg, out var typeDef))
				{ // the argument is one of the container's own types: it has a full description, so use it
					return GetXmlDcsContractName(typeDef.Type);
				}

				if (arg.DataContractName is not null)
				{ // a renamed enum: the one kind of type that names itself from a reference alone
					return EncodeXmlDcsNamePart(arg.DataContractName);
				}

				if (arg.FullName.IndexOf('<') >= 0 || arg.FullName.IndexOf('[') >= 0)
				{ // a closed generic, or an array: naming it needs its arguments, which a TypeRef does not carry
					return null;
				}

				return EncodeXmlDcsNamePart(arg.Name);
			}

			/// <summary>Resolves the contract name of the type a converter is being emitted for, emitting a <c>#error</c> when it cannot be derived or is not a legal XML name</summary>
			/// <remarks>The name is already <c>XmlConvert.EncodeLocalName</c>-encoded, which is what the reference serializer does
			/// too, so a declared name like <c>"with-dash"</c> is honored rather than rejected. The verification below therefore only
			/// catches what encoding cannot repair (an empty name), and the <c>#error</c> says so at the exact container.</remarks>
			private string ResolveXmlDcsRootName(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				string? name = GetXmlDcsContractName(typeDef.Type);

				if (name is not null)
				{
					try
					{
						System.Xml.XmlConvert.VerifyNCName(name);
						return name;
					}
					catch (Exception e) when (e is System.Xml.XmlException or ArgumentException)
					{
						sb.AppendLine($"#error The type {typeDef.Type.FullName} resolves to the DataContract XML name '{name}', which is not a legal XML name: {e.Message.Replace("\r", " ").Replace("\n", " ")}");
						return "element";
					}
				}

				sb.AppendLine($"#error The DataContract XML name of type {typeDef.Type.FullName} cannot be derived: one of its parts (a generic argument, or the item type of a collection) has no contract name this emission can compute. Name the type explicitly with [DataContract(Name = \"...\")], or introduce a named intermediate type.");
				return "element";
			}

			/// <summary>Returns the output name of one member: its data-contract name, XML-encoded</summary>
			/// <remarks>The data contract owns this name: <c>[DataMember(Name = ...)]</c> when present, the declared member name
			/// otherwise, which is what the reference serializer writes. The resolved JSON name is not read here. On a
			/// <c>[DataContract]</c> type the two are the same name, because CJSON0011 rejects a declaration where they differ; on a
			/// plain DTO they are not, because a <c>[JsonProperty]</c> renames the JSON member of a type that has no data contract,
			/// and the reference serializer still writes the member's own name. The compat profile also rejects the
			/// <c>[XmlProperty]</c> renaming surface (CXML0004) and any naming policy on the container (CXML0001), so this name has
			/// exactly one source.</remarks>
			private static string GetXmlDcsMemberName(CrystalJsonMemberMetadata member) => System.Xml.XmlConvert.EncodeLocalName(member.DataMemberName ?? member.MemberName);

			/// <summary>Whether a type is the <c>byte[]</c> that this format treats as a base64 scalar</summary>
			private static bool IsXmlDcsByteArray(TypeMetadata type) => type.TypeKind == TypeKind.Array && type.ElementType is { SpecialType: SpecialType.System_Byte };

			#endregion

			#region Contract namespaces...

			/// <summary>Returns the expression naming the contract namespace of a type, or <see langword="null"/> when it has none</summary>
			/// <remarks>
			/// <para>The one place the profile's two outputs differ: under the omit-namespaces option this returns
			/// <see langword="null"/> for every type, so every name the emission bakes is a local name and the document carries
			/// no namespace, no prefix and no declaration. Everything else about the emission is the same code.</para>
			/// <para><see langword="null"/> is also what the empty contract namespace resolves to, which
			/// <c>[DataContract(Namespace = "")]</c> asks for: a name in no namespace, written with no prefix.</para>
			/// </remarks>
			private string? GetXmlDcsNamespaceRef(TypeMetadata type)
			{
				if (this.WritesXmlDcsOmitNamespaces)
				{
					return null;
				}

				string uri = GetXmlDcsContractNamespaceUri(type);
				return uri.Length == 0 ? null : this.XmlNamespaces.Ref(uri);
			}

			/// <summary>Returns the expression naming the namespace one MEMBER element lives in, or <see langword="null"/> when it has none</summary>
			/// <param name="member">Member being written</param>
			/// <param name="fallback">Type being written, used when the metadata carries no declaring type</param>
			/// <remarks>
			/// <para>The namespace of the contract that DECLARES the member, which is not always the contract being written:
			/// a derived type in another namespace writes its INHERITED members in the base contract's namespace and only its
			/// own members in its own. Measured against the live oracle, which puts an inherited element where the base
			/// declares it. Reading the namespace off the type being written instead moves every inherited element, silently,
			/// on exactly the shape polymorphism produces.</para>
			/// <para>For a member the type declares itself, the two are the same namespace, so this is a refinement of that
			/// rule and never a different answer.</para>
			/// </remarks>
			private string? GetXmlDcsMemberNamespaceRef(CrystalJsonMemberMetadata member, TypeMetadata fallback)
			{
				if (this.WritesXmlDcsOmitNamespaces)
				{
					return null;
				}

				if (member.DeclaringTypeNameSpace is null)
				{ // nothing captured about the declaring type: the type being written is the closest thing to it
					return GetXmlDcsNamespaceRef(fallback);
				}

				string uri = member.DeclaringDataContractNamespace ?? (XmlDataContractNamespacePrefix + member.DeclaringTypeNameSpace);
				return uri.Length == 0 ? null : this.XmlNamespaces.Ref(uri);
			}

			/// <summary>Returns the expression naming one of the format's built-in namespaces, or <see langword="null"/> under the omit-namespaces option</summary>
			private string? GetXmlDcsBuiltinNamespaceRef(string uri) => this.WritesXmlDcsOmitNamespaces ? null : this.XmlNamespaces.Ref(uri);

			/// <summary>Returns the expression naming the XML Schema instance namespace, which carries <c>nil</c> and <c>type</c></summary>
			private string? GetXmlDcsInstanceNamespaceRef() => GetXmlDcsBuiltinNamespaceRef(XmlSchemaInstanceNamespaceUri);

			/// <summary>Emits the type annotation of one contract: a qualified name by default, a bare local name under the omit-namespaces option</summary>
			/// <param name="sb">Destination builder</param>
			/// <param name="names">Name table of the converter being emitted</param>
			/// <param name="contractName">Local contract name of the runtime type</param>
			/// <param name="contractNamespace">Expression naming the contract's namespace, or <see langword="null"/> when it has none</param>
			/// <remarks>
			/// <para>A type annotation is the one attribute of this format whose VALUE is a name, so by default it goes through
			/// the emitter's qualified-name member: the pair (namespace, local name) IS the discriminator a reader matches, and
			/// the prefix it appears under is the emitter's to pick. Formatting the pair into a string here would name a prefix
			/// this code cannot know.</para>
			/// <para>Under the omit-namespaces option the value keeps its local half only, which is what the stored documents carry.
			/// The cost is stated on the option: two derived types with the same local name in different contract namespaces
			/// become one annotation.</para>
			/// </remarks>
			private void WriteXmlDcsTypeAnnotation(CSharpCodeBuilder sb, XmlNameTable names, string contractName, string? contractNamespace)
			{
				if (this.WritesXmlDcsOmitNamespaces)
				{
					sb.AppendLine($"emitter.WriteAttribute(in {names.Ref(XmlDcsTypeAttributeName)}, {CSharpCodeBuilder.Constant(contractName)});");
					return;
				}

				string attributeRef = names.Ref(XmlDcsTypeAttributeName, GetXmlDcsInstanceNamespaceRef());
				string valueRef = names.Ref(contractName, contractNamespace);
				sb.AppendLine($"emitter.WriteQNameAttribute(in {attributeRef}, in {valueRef});");
			}

			/// <summary>Returns the contract namespace URI of a type, which is a function of its SHAPE first and of its declaration second</summary>
			/// <remarks>
			/// <para>Four rules, in this order:</para>
			/// <list type="number">
			/// <item>a lexical type takes the XML Schema namespace, which is where its xsd contract name lives;</item>
			/// <item><c>DateTimeOffset</c> takes the namespace of the built-in <c>System</c> contracts, because that is where its
			/// two-member contract is declared and not where the type holding it is;</item>
			/// <item>a dictionary takes the collections namespace, always, because that is where its entry contract is
			/// declared; a sequence takes it too when its items are built-in types, and takes the ITEM contract's namespace
			/// when they carry one. The asymmetry is measured, not chosen;</item>
			/// <item>anything else takes its <c>[DataContract(Namespace = ...)]</c> when it declares one, and otherwise the
			/// standard prefix plus its CLR namespace. A nested type contributes its declaring names to the NAME and nothing to
			/// the namespace, which is the rule the name resolution already applies.</item>
			/// </list>
			/// </remarks>
			private string GetXmlDcsContractNamespaceUri(TypeMetadata type)
			{
				var actual = type.NullableOfType ?? type;

				if (IsXmlDcsByteArray(actual))
				{ // a base64 scalar, so a lexical type and not a sequence of bytes
					return XmlSchemaNamespaceUri;
				}

				if (actual.IsDateTimeOffset())
				{
					return XmlSystemContractNamespaceUri;
				}

				if (GetXmlDcsLexicalContractName(actual.SpecialType, actual.NameSpace, actual.Name) is not null)
				{
					return XmlSchemaNamespaceUri;
				}

				if (actual.KeyType is not null && actual.ValueType is not null)
				{ // a dictionary is in the collections namespace whatever its key and value are, which is where a
				  // KeyValueOfKV entry is declared. Measured: a Dictionary<string, Shelf> puts its entries there and not in
				  // Shelf's contract namespace, unlike a List<Shelf>, whose items DO move to the item contract's namespace
					return XmlArraysNamespaceUri;
				}

				if (actual.ElementType is not null)
				{
					return GetXmlDcsCollectionNamespaceUri(actual.ElementType) ?? XmlArraysNamespaceUri;
				}

				return GetXmlDcsDeclaredNamespaceUri(actual);
			}

			/// <summary>Returns the namespace a collection borrows from one of its argument types, or <see langword="null"/> when that type contributes none</summary>
			/// <remarks>A collection of built-in types is in the collections namespace, and a collection whose items carry a
			/// contract of their own is in the items' namespace, so an argument contributes a namespace only when it is a user
			/// type.</remarks>
			private string? GetXmlDcsCollectionNamespaceUri(TypeMetadata argument)
			{
				var actual = argument.NullableOfType ?? argument;

				if (IsXmlDcsByteArray(actual) || actual.IsDateTimeOffset() || GetXmlDcsLexicalContractName(actual.SpecialType, actual.NameSpace, actual.Name) is not null)
				{
					return null;
				}

				if (actual.KeyType is not null || actual.ElementType is not null)
				{ // a nested collection borrows from its own arguments, so ask it rather than read its CLR namespace
					string nested = GetXmlDcsContractNamespaceUri(actual);
					return nested == XmlArraysNamespaceUri ? null : nested;
				}

				return GetXmlDcsDeclaredNamespaceUri(actual);
			}

			/// <summary>Returns the declared contract namespace of a user type: its <c>[DataContract(Namespace = ...)]</c>, else the standard prefix plus its CLR namespace</summary>
			private string GetXmlDcsDeclaredNamespaceUri(TypeMetadata type)
			{
				if (IsLocallyGeneratedType(type.Ref, out var typeDef) && typeDef.DataContractNamespace is { } declared)
				{ // including the EMPTY string, which asks for no namespace at all
					return declared;
				}

				return XmlDataContractNamespacePrefix + type.NameSpace;
			}

			#endregion

			/// <summary>Emits the declaration that puts the XML Schema instance namespace in scope for a whole document, when a document is likely to need it more than once</summary>
			/// <remarks>
			/// <para>The emitter declares a namespace on the first element that uses it, which is what keeps a document free of
			/// declarations nothing reads. That is the right answer for a namespace one subtree uses, and the wrong one for a
			/// namespace several sibling subtrees use: siblings are not inside each other, so each one declares its own copy.</para>
			/// <para>So when the contract can carry a null marker or a type annotation in two or more places, the declaration goes
			/// on the root instead, once. <see cref="CountXmlDcsInstanceNamespaceUsers"/> is what "two or more places" means, and it
			/// counts the type's OWN members only: it can therefore miss a deeper case, which costs a repeated declaration and
			/// nothing else. What it cannot do is hoist a declaration into a document that never writes either marker at all, since
			/// a contract with fewer than two such members takes this branch not at all.</para>
			/// <para>Guarded on the depth, because this body writes the root element and every nested element of its type, and only
			/// the root is an ancestor of the whole document.</para>
			/// </remarks>
			private void WriteXmlDcsInstanceNamespaceHoist(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				if (GetXmlDcsInstanceNamespaceRef() is not { } instanceNamespace) return;
				if (CountXmlDcsInstanceNamespaceUsers(typeDef) < 2) return;

				sb.Comment("two or more members of this contract can carry a nil or a type marker, and none of them is inside another: one declaration on the root serves them all");
				sb.AppendLine($"if ({XmlDepthParameterName} == 0) emitter.WriteNamespaceDeclaration(in {instanceNamespace});");
			}

			/// <summary>Emits the declaration that puts a type's OWN contract namespace in scope for the element it is written in</summary>
			/// <remarks>
			/// <para>A member element lives in the namespace of the type that DECLARES it, while the members of its own type live
			/// in that type's namespace. When the two differ, every one of those member elements needs a prefix, and each would
			/// declare the namespace again: one declaration on the element that holds them replaces all of them, and is where the
			/// reference serializer puts it too.</para>
			/// <para>Emitted unconditionally for a type that writes members, because the emitter writes nothing when the namespace
			/// is already in scope. That is what covers the root element, whose own namespace its caller declared, and a nested
			/// element in the same namespace as its parent, with the same statement.</para>
			/// <para>Skipped for a type that writes NO member element in its own namespace: an empty contract, the
			/// <c>ISerializable</c> dialect (whose entry elements are in no namespace at all) and a type that writes its own
			/// content all get an element with no declaration on it.</para>
			/// </remarks>
			private void WriteXmlDcsOwnNamespaceDeclaration(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, string? contractNamespace)
			{
				if (contractNamespace is null) return;
				if (typeDef.Type.IsCrystalXmlSerializable() || IsXmlDcsSerializableDialect(typeDef)) return;
				if (GetXmlDcsOrderedMembers(typeDef).Count == 0) return;

				sb.Comment("the members below live in this contract's namespace: declared once here, so none of them declares it again");
				sb.AppendLine($"emitter.WriteNamespaceDeclaration(in {contractNamespace});");
			}

			/// <summary>Emits the declaration that puts the namespace of a wrapper's children in scope on the wrapper itself</summary>
			/// <param name="sb">Destination builder</param>
			/// <param name="childNamespace">Expression naming the children's namespace, or <see langword="null"/> when they have none</param>
			/// <param name="what">What the declaration serves, for the comment in the generated code</param>
			/// <remarks>Same reason as the one above, for the shapes whose children this body writes itself: a collection's items,
			/// a dictionary's entries, and the two members of the <c>DateTimeOffset</c> contract are all in one namespace, and a
			/// collection can hold thousands of them.</remarks>
			private static void WriteXmlDcsWrapperNamespaceDeclaration(CSharpCodeBuilder sb, string? childNamespace, string what)
			{
				if (childNamespace is null) return;

				sb.Comment($"{what} is in one namespace: declared once on the wrapper, so no child declares it again");
				sb.AppendLine($"emitter.WriteNamespaceDeclaration(in {childNamespace});");
			}

			/// <summary>Counts the members of a type whose emission can write a null marker or a type annotation</summary>
			/// <remarks>Three shapes qualify: an <c>object</c> slot, which annotates every value it holds; a member whose declared
			/// type is a polymorphic root, which annotates a derived value; and a member that can be null under a policy that marks
			/// it rather than omitting it. The type itself counts too when it is a polymorphic root, because the element this body
			/// writes is then annotated.</remarks>
			private int CountXmlDcsInstanceNamespaceUsers(CrystalJsonTypeMetadata typeDef)
			{
				int count = typeDef.IsPolymorphicRoot ? 1 : 0;

				foreach (var (member, _) in GetXmlDcsOrderedMembers(typeDef))
				{
					var actual = member.Type.NullableOfType ?? member.Type;

					if (actual.SpecialType == SpecialType.System_Object
					 || (IsLocallyGeneratedType(actual, out var target, out _) && target.IsPolymorphicRoot)
					 || (member.Type.CanBeNull() && ResolveXmlNullPolicy(member) != XmlNullPolicy.Omit))
					{
						++count;
					}
				}

				return count;
			}

			#region Member order...

			/// <summary>Returns the members of a type in the exact order the DataContract format writes them</summary>
			/// <remarks>
			/// <para>Base level first (recursively), then, inside each level, the members with no declared <c>Order</c> sorted by
			/// their output name in ordinal order, then the <c>Order</c> groups ascending with ordinal ties. Ordering by the output name
			/// and not by the C# name matters: <c>[DataMember(Name = "renamed_member")]</c> sorts where the output spells it.</para>
			/// <para>Read-only (get-only, or non-public-setter with no opt-in) properties are dropped: the reference serializer's
			/// no-set-method check is property-only. On a POCO it just omits them (only public get+set members are taken); on a
			/// <c>[DataContract]</c> type a read-only <c>[DataMember]</c> property is not a valid contract at all
			/// (<c>InvalidDataContractException</c>, "No set method for property"), which <c>CrystalJsonSourceGenerator.Parser.ReportReadOnlyDataMemberProperty</c>
			/// rejects with <c>CXML0013</c> at generation time; this filter is only the emission-side backstop for that rejection.
			/// A serialize-only property is the exception kept in: a referenced-assembly <c>[DataMember]</c> whose non-public
			/// setter the default metadata import hides reads as read-only, but the write-only DataContract format needs its
			/// getter alone, so <see cref="CrystalJsonMemberMetadata.SerializeOnly"/> keeps it in the output.
			/// A read-only field is a different shape: that check does
			/// not look at fields, so DCS emits one, and this filter keeps it in for a <c>[DataContract]</c> type. Init-only members
			/// are unaffected either way, because <see cref="CrystalJsonMemberMetadata.IsReadOnly"/> is <see langword="false"/> for
			/// them (a separate <see cref="CrystalJsonMemberMetadata.IsInitOnly"/> flag), matching DCS emitting them.</para>
			/// </remarks>
			private static List<(CrystalJsonMemberMetadata Member, string OutputName)> GetXmlDcsOrderedMembers(CrystalJsonTypeMetadata typeDef)
			{
				return typeDef.Members
					.Where(m => !m.IsReadOnly || m.SerializeOnly || (m.IsField && typeDef.HasDataContract))
					.Select(m => (Member: m, OutputName: GetXmlDcsMemberName(m)))
					.OrderBy(x => x.Member.InheritanceLevel)
					.ThenBy(x => x.Member.DataMemberOrder.HasValue ? 1 : 0)
					.ThenBy(x => x.Member.DataMemberOrder ?? 0)
					.ThenBy(x => x.OutputName, StringComparer.Ordinal)
					.ToList();
			}

			#endregion

			#region Serializer facet...

			/// <summary>Emits the <c>ICrystalXmlSerializer&lt;T&gt;</c> facet of one converter, on the DataContract format</summary>
			private void WriteXmlDcsSerializer(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				// ONE taken-identifier set for both tables: they declare members of the SAME generated class
				var taken = new HashSet<string>(StringComparer.Ordinal);
				var names = new XmlNameTable(taken);
				this.XmlEnums = new(taken);
				this.XmlNamespaces = new(taken);
				this.XmlNeedsNotSupportedHelper = false;
				this.XmlNeedsFlagsHelper = false;
				this.XmlNeedsUndeclaredEnumHelper = false;
				this.XmlNeedsAnyTypeHelper = false;
				this.XmlNeedsCycleHelper = false;

				var type = typeDef.Type;
				string valueType = type.FullyQualifiedName + (type.IsValueType() ? "" : "?");
				string settingsType = KnownTypeSymbols.CrystalXmlSettingsFullName;

				string contractName = ResolveXmlDcsRootName(sb, typeDef);
				string? contractNamespace = GetXmlDcsNamespaceRef(type);
				string rootRef = names.Ref(contractName, contractNamespace);

				// WriteXml(...): the interface entry point, which owns the rootName override
				sb.InheritDoc();
				sb.XmlComment("<remarks>The serialization lifecycle callbacks of this type (<c>OnSerializing</c>, <c>OnSerialized</c>) ARE invoked here, exactly as on the JSON path, and on the <c>ISerializable</c> dialect they bracket the <c>GetObjectData</c> call the way the reference serializer brackets it.</remarks>");
				sb.XmlComment("<remarks>This is the DataContract (compat) format: element names come from the data contract, member order follows the DataContract rule, and scalars use the DCS lexical forms. The JSON-side vocabulary that shapes VALUES rather than membership (<c>[JsonProperty(EnumFormat = ...)]</c>, the enum naming settings) has no effect here, because the output this profile reproduces never saw it.</remarks>");
				sb.AppendLine($"public void WriteXml<TEmitter>(ref TEmitter emitter, {valueType} value, {settingsType}? settings = default, string? rootName = default) where TEmitter : struct, {IXmlEmitterFullName}");
				sb.EnterBlock("WriteXml");
				sb.Comment("the container's baked format profile is the \"no settings\" default; explicit settings replace it entirely");
				sb.AppendLine($"settings ??= {XmlDcsDefaultSettings};");
				sb.AppendLine("if (rootName is null)");
				sb.EnterBlock();
				sb.AppendLine($"WriteXmlElement(ref emitter, in {rootRef}, value, settings, 0);");
				sb.LeaveBlock();
				sb.AppendLine("else");
				sb.EnterBlock("else");
				sb.Comment("a caller-supplied name is the one place user text becomes an XML name: Create validates it, and raises XmlException rather than corrupting the document");
				sb.Comment("the caller names the ROOT ELEMENT, not the contract: the name changes and the namespace it lives in does not");
				sb.AppendLine($"var __root = {XmlNameFullName}.Create(rootName{(contractNamespace is null ? "" : $", {contractNamespace}.Text")});");
				sb.AppendLine("WriteXmlElement(ref emitter, in __root, value, settings, 0);");
				sb.LeaveBlock("else");
				sb.LeaveBlock("WriteXml");
				sb.NewLine();

				// the two names the collection root entry points compose with
				sb.InheritDoc();
				sb.AppendLine($"public {XmlNameFullName} ElementName => {rootRef};");
				sb.NewLine();
				sb.InheritDoc();
				sb.XmlComment("<remarks>The ArrayOfX convention of the reference format, in the contract namespace of <see cref=\"ElementName\"/>.</remarks>");
				sb.AppendLine($"public string? CollectionRootName => {CSharpCodeBuilder.Constant("ArrayOf" + contractName)};");
				sb.NewLine();

				// WriteXmlElement(...): the nested entry point, for a parent that already knows the element name
				sb.XmlComment("<summary>Writes this value as an element of the given name</summary>");
				sb.XmlComment("<remarks>The nested entry point: a parent converter writing this type as one of its members passes its own cached member name here, so no name is validated or transcoded at write time. The declared type IS this type here, so no type annotation is written.</remarks>");
				WriteXmlDepthParameterDoc(sb);
				sb.AppendLine($"public void WriteXmlElement<TEmitter>(ref TEmitter emitter, in {XmlNameFullName} name, {valueType} value, {settingsType}? settings, int {XmlDepthParameterName} = 0) where TEmitter : struct, {IXmlEmitterFullName}");
				sb.EnterBlock("WriteXmlElement");
				sb.AppendLine($"WriteXmlDcsElement(ref emitter, in name, value, settings, {CSharpCodeBuilder.Constant(contractName)}, {XmlDepthParameterName});");
				sb.LeaveBlock("WriteXmlElement");
				sb.NewLine();

				// WriteXmlDcsElement(...): the real body, which also knows what the DECLARED contract at the call site was
				sb.XmlComment("<summary>Writes this value as an element of the given name, annotated with its contract name when the caller's declared type is a different contract</summary>");
				sb.XmlComment("<param name=\"declaredContractName\">Contract name of the type the call site DECLARED. The reference format writes a <c>type</c> annotation exactly when the runtime contract differs from it; <see langword=\"null\"/> suppresses the annotation entirely.</param>");
				sb.XmlComment($"<param name=\"{XmlDepthParameterName}\">Number of elements already open above this one; see the same parameter on <c>WriteXmlElement</c>.</param>");
				// INTERNAL, unlike its WriteXmlElement twin: that one is public because it implements ICrystalXmlSerializer<T>,
				// while this one is part of no interface. Every caller is another generated serializer of the SAME container
				// (GetLocalSerializerRef never leaves it), so nothing outside the assembly has any use for it.
				sb.AppendLine($"internal void WriteXmlDcsElement<TEmitter>(ref TEmitter emitter, in {XmlNameFullName} name, {valueType} value, {settingsType}? settings, string? declaredContractName, int {XmlDepthParameterName} = 0) where TEmitter : struct, {IXmlEmitterFullName}");
				sb.EnterBlock("WriteXmlDcsElement");
				sb.AppendLine($"settings ??= {XmlDcsDefaultSettings};");
				sb.NewLine();
				WriteXmlDepthGuard(sb, type);

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(type.Ref, out var polymorphicMetadata);

				if (!type.IsValueType())
				{
					sb.NewLine();
					WriteXmlNullRootElement(sb, names, "a null value still produces the element (a document needs a root), marked nil unless the settings dropped null members");
				}

				if (typeDef.IsPolymorphicRoot)
				{
					sb.NewLine();
					sb.Comment("the runtime type decides which generated body writes the element; the declared contract travels unchanged, so each body can tell whether it must annotate itself");
					sb.AppendLine("switch (value)");
					sb.EnterBlock("switch");
					foreach (var derivedType in GetPolymorphicDispatchOrder(typeDef))
					{
						// the delegate writes THIS element, so it stands at the same depth: only a nested MEMBER adds a level
						sb.AppendLine($"case {derivedType.FullyQualifiedName} x: {GetLocalSerializerRef(derivedType)}.WriteXmlDcsElement(ref emitter, in name, x, settings, declaredContractName, {XmlDepthParameterName}); return;");
					}
					if (!type.IsAbstract)
					{ // an instance of the root type itself falls through to the body below
						sb.AppendLine($"case {type.FullyQualifiedName}: break;");
					}
					sb.AppendLine($"default: throw new {CrystalXmlUnknownTypeExceptionFullName}(value.GetType());");
					sb.LeaveBlock("switch");

					if (type.IsAbstract)
					{ // no case falls through: an ABSTRACT root has no instance of its own to write, so a body below the switch
					  // would be code no path can reach (CS0162). Close here, exactly as the abstract-intermediate branch does.
						sb.LeaveBlock("WriteXmlDcsElement");
						sb.NewLine();
						WriteXmlCycleHelper(sb);
						WriteXmlNameFields(sb, names);
						return;
					}
				}
				else if (type.IsAbstract && hasPolymorphicDefinition)
				{ // an abstract type in the middle of a hierarchy: the root of the hierarchy owns the switch
					sb.AppendLine($"{GetLocalSerializerRef(polymorphicMetadata.Parent)}.WriteXmlDcsElement(ref emitter, in name, value, settings, declaredContractName, {XmlDepthParameterName});");
					sb.LeaveBlock("WriteXmlDcsElement");
					sb.NewLine();
					WriteXmlCycleHelper(sb);
					WriteXmlNameFields(sb, names);
					return;
				}
				else if (!type.IsSealed && !type.IsValueType())
				{ // an unsealed type with no declared derived type: writing a subclass through this body would silently drop its own members
					sb.NewLine();
					sb.AppendLine($"if (value.GetType() != typeof({type.FullyQualifiedName}))");
					sb.EnterBlock();
					sb.AppendLine($"throw new {CrystalXmlUnknownTypeExceptionFullName}(value.GetType());");
					sb.LeaveBlock();
				}

				sb.NewLine();
				sb.AppendLine("emitter.WriteStartElement(in name);");
				WriteXmlDcsInstanceNamespaceHoist(sb, typeDef);
				WriteXmlDcsOwnNamespaceDeclaration(sb, typeDef, contractNamespace);
				sb.NewLine();
				sb.Comment("the type annotation is written exactly when the runtime contract differs from the declared one");
				sb.AppendLine($"if (declaredContractName is not null && declaredContractName != {CSharpCodeBuilder.Constant(contractName)})");
				sb.EnterBlock();
				WriteXmlDcsTypeAnnotation(sb, names, contractName, contractNamespace);
				sb.LeaveBlock();

				// the same bracket the JSON side puts around its member loop, in the same place: after the element is
				// opened and its annotation written, before anything reads the value. On the ISerializable dialect this
				// lands around the GetObjectData call, which is where the reference serializer fires them too.
				EmitXmlCallbackInvocation(sb, typeDef.OnSerializing);

				WriteXmlDcsBody(sb, names, typeDef);

				sb.NewLine();
				EmitXmlCallbackInvocation(sb, typeDef.OnSerialized);
				sb.AppendLine("emitter.WriteEndElement(in name);");
				sb.LeaveBlock("WriteXmlDcsElement");
				sb.NewLine();

				if (this.XmlNeedsNotSupportedHelper)
				{
					WriteXmlNotSupportedHelper(sb);
					this.XmlNeedsNotSupportedHelper = false;
				}

				if (this.XmlNeedsAnyTypeHelper)
				{
					WriteXmlDcsAnyTypeHelper(sb);
					this.XmlNeedsAnyTypeHelper = false;
				}

				WriteXmlCycleHelper(sb);

				// emitted BEFORE the enum helpers are asked for: the lookups are what set those flags
				WriteXmlEnumHelpers(sb, this.XmlEnums);

				if (this.XmlNeedsFlagsHelper)
				{
					WriteXmlDcsFlagsHelper(sb);
					this.XmlNeedsFlagsHelper = false;
				}

				if (this.XmlNeedsUndeclaredEnumHelper)
				{
					WriteXmlDcsUndeclaredEnumHelper(sb);
					this.XmlNeedsUndeclaredEnumHelper = false;
				}

				WriteXmlNameFields(sb, names);
			}

			/// <summary>Writes the content of a type's element: its own content hook, the <c>ISerializable</c> dialect, or its members</summary>
			/// <remarks>
			/// <para>Three branches, in this order: a type that writes its own content, the <c>ISerializable</c> dialect, and the
			/// members. There is deliberately NO collection branch here: this method writes the body of a type REGISTERED in the
			/// container, and a collection is only ever reached as the type of a member, where
			/// <see cref="WriteXmlDcsValueContent"/> gives it its own shape (and applies the reference serializer's priority: a data
			/// contract first, then the collection shapes, then <c>ISerializable</c>, so a <c>Dictionary&lt;K,V&gt;</c> serializes as
			/// entries even though it implements <c>ISerializable</c>).</para>
			/// <para>A registered type that DERIVES from a collection therefore gets the collection's contract name as its root
			/// (<c>ArrayOfstring</c>) and an empty body, since it has no serialized member of its own. That is the same shape the
			/// general profile produces for it, and the reference format would write the items.</para>
			/// </remarks>
			private void WriteXmlDcsBody(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef)
			{
				var type = typeDef.Type;

				if (type.IsCrystalXmlSerializable())
				{ // the type writes its own content: this body owns the element shell, and nothing else
					sb.NewLine();
					sb.Comment("the type implements ICrystalXmlSerializable: it writes its own content, inside the element opened here");
					sb.AppendLine("value.WriteXml(ref emitter);");
					return;
				}

				if (IsXmlDcsSerializableDialect(typeDef))
				{
					WriteXmlDcsSerializableDialect(sb, names, typeDef);
					return;
				}

				foreach (var (member, outputName) in GetXmlDcsOrderedMembers(typeDef))
				{
					WriteXmlDcsMember(sb, names, typeDef, member, outputName);
				}
			}

			/// <summary>Whether a type is written through the <c>ISerializable</c> dialect rather than through its members</summary>
			private static bool IsXmlDcsSerializableDialect(CrystalJsonTypeMetadata typeDef)
				=> typeDef.Type.ImplementsISerializable
				&& !typeDef.HasDataContract
				&& typeDef.Type.ElementType is null
				&& typeDef.Type.KeyType is null;

			/// <summary>Writes the <c>ISerializable</c> dialect: one element per <c>SerializationInfo</c> entry, NAMED AFTER THE ENTRY</summary>
			/// <remarks>
			/// <para>This is how the reference format serializes a key-flattening wrapper: the element name IS the data, and the value
			/// is declared as <c>object</c>, so every non-null value carries a contract annotation.</para>
			/// <para><c>GetObjectData</c> is called directly on the instance, so nothing here reflects over the type. The entry name
			/// is the one place a name is built at run time: it goes through the same <c>XmlConvert.EncodeLocalName</c> the reference
			/// serializer applies, so a key that is not an XML name comes out escaped rather than rejected.</para>
			/// </remarks>
			private void WriteXmlDcsSerializableDialect(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef)
			{
				sb.NewLine();
				sb.Comment("the ISerializable dialect: each SerializationInfo entry becomes an element named after the ENTRY, holding an anyType value");
				sb.Comment("SYSLIB0050 marks this API as \"do not build new things on it\"; this profile REPRODUCES an existing format that goes through it, so the call is the contract");
				sb.AppendLine("#pragma warning disable SYSLIB0050");
				sb.AppendLine($"var __info = new {SerializationInfoFullName}(typeof({typeDef.Type.FullyQualifiedName}), new {FormatterConverterFullName}());");
				sb.AppendLine($"(({ISerializableFullName}) value).GetObjectData(__info, default);");
				sb.AppendLine("#pragma warning restore SYSLIB0050");
				sb.AppendLine($"foreach ({SerializationEntryFullName} __entry in __info)");
				sb.EnterBlock("foreach");
				sb.Comment("the entry name is DATA and not a contract member, so it lands in NO namespace: measured, the reference format writes xmlns=\"\" on each of these elements to take it out of the contract's own");
				sb.AppendLine($"var __n = {XmlNameFullName}.Create({XmlConvertFullName}.EncodeLocalName(__entry.Name));");
				WriteXmlDcsAnyTypeElement(sb, names, "__entry.Value", "__n", "entry");
				sb.LeaveBlock("foreach");
			}

			#endregion

			#region Members...

			/// <summary>Writes one member of a type, in the shape its declared type calls for</summary>
			private void WriteXmlDcsMember(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member, string outputName)
			{
				// a member element lives in the namespace of the type that DECLARES it, so the namespace of the member's own
				// type never reaches this element: it reaches the elements the nested body writes inside it
				string nameRef = names.Ref(outputName, GetXmlDcsMemberNamespaceRef(member, typeDef.Type));
				string local = "__x_" + member.MemberName;

				sb.NewLine();
				sb.Comment($"{member.Type.Name} {member.MemberName} => <{outputName}>{(member.DataMemberOrder is { } order ? $" [Order = {order}]" : "")}{(!member.EmitDefaultValue ? " [EmitDefaultValue = false]" : "")}");
				sb.AppendLine($"var {local} = {GetXmlMemberReadExpr(typeDef, member)};");

				// [DataMember(EmitDefaultValue = false)] and [JsonIgnore(Condition = WhenWritingDefault)] both mean "a value equal
				// to the default writes NOTHING at all, not even a nil element" - but they do NOT agree on which default.
				// DCS compares against the CLR default of the type, full stop, and this format reproduces DCS: hence default!,
				// where the general emitter compares against the member's DECLARED default (GetForgivingDefaultLiteral).
				// A member declaring `= 5` is therefore omitted at 0 here, and omitted at 5 there. Deliberate: byte
				// compatibility is what this profile exists for, and the reference format has no notion of a declared default.
				bool guarded = !member.EmitDefaultValue || member.IgnoreCondition == "WhenWritingDefault";
				if (guarded)
				{
					sb.AppendLine($"if (!{EqualityComparerFullName}<{member.Type.FullyQualifiedNameAnnotated}>.Default.Equals({local}, default!))");
					sb.EnterBlock();
				}

				var policy = ResolveXmlNullPolicy(member);

				WriteXmlDcsValueElement(sb, names, member, member.Type, local, nameRef, member.MemberName, policy);

				if (guarded)
				{
					sb.LeaveBlock();
				}
			}

			/// <summary>Writes an element of name <paramref name="nameRef"/> holding <paramref name="valueExpr"/>, handling the null case per <paramref name="policy"/></summary>
			private void WriteXmlDcsValueElement(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonMemberMetadata? member, TypeMetadata type, string valueExpr, string nameRef, string scope, XmlNullPolicy policy)
			{
				if (!type.CanBeNull())
				{
					WriteXmlDcsValueContent(sb, names, member, type, valueExpr, nameRef, scope);
					return;
				}

				sb.AppendLine($"if ({valueExpr} is not null)");
				sb.EnterBlock();
				WriteXmlDcsValueContent(sb, names, member, type, valueExpr, nameRef, scope);
				sb.LeaveBlock();

				WriteXmlNullPolicyBranch(sb, names, nameRef, policy);
			}

			/// <summary>Writes the element of a value known to be non-null: the one place that decides between the shapes of this format</summary>
			private void WriteXmlDcsValueContent(CSharpCodeBuilder sb, XmlNameTable names, CrystalJsonMemberMetadata? member, TypeMetadata type, string valueExpr, string nameRef, string scope)
			{
				var actual = type.NullableOfType ?? type;
				string expr = type.NullableOfType is null ? valueExpr : valueExpr + ".Value";

				// 1) a member converter takes over the whole projection of the member (only ever at member level)
				if (member?.CustomConverterType is not null)
				{
					WriteXmlCustomConverterElement(sb, member, valueExpr, nameRef);
					return;
				}

				// 2) a scalar is text inside the element
				if (WriteXmlDcsScalarElement(sb, actual, expr, nameRef, scope))
				{
					return;
				}

				// 3) DateTimeOffset has a built-in contract of its own: a two-member structure, not a lexical form
				if (actual.IsDateTimeOffset())
				{
					WriteXmlDcsDateTimeOffsetElement(sb, names, expr, nameRef);
					return;
				}

				// 4) an anyType slot: the runtime value decides both the contract annotation and the content
				if (actual.SpecialType == SpecialType.System_Object)
				{
					WriteXmlDcsAnyTypeElement(sb, names, expr, nameRef, scope);
					return;
				}

				var target = IsLocallyGeneratedType(actual, out var generated, out _) ? generated : null;

				// 5) a data contract wins over the collection shapes, exactly as it does in the reference serializer
				if (target is not null && target.HasDataContract)
				{
					WriteXmlDcsLocalElement(sb, target, expr, nameRef);
					return;
				}

				// 6) a dictionary, as a sequence of key/value entries
				if (actual.KeyType is not null && actual.ValueType is not null)
				{
					WriteXmlDcsDictionaryContent(sb, names, actual, expr, nameRef, scope);
					return;
				}

				// 7) a sequence, whose items are named after the ITEM's contract
				if (actual.ElementType is not null)
				{
					WriteXmlDcsSequenceContent(sb, names, actual, expr, nameRef, scope);
					return;
				}

				// 8) a type of this container writes itself, under the name given here
				if (target is not null)
				{
					WriteXmlDcsLocalElement(sb, target, expr, nameRef);
					return;
				}

				// 9) a type that writes its own content, inside the element opened here
				if (actual.IsCrystalXmlSerializable())
				{
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.AppendLine($"{expr}.WriteXml(ref emitter);");
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return;
				}

				WriteXmlNotSupported(sb, actual, scope);
			}

			/// <summary>Writes a value handled by another generated body of this container, passing the DECLARED contract along</summary>
			private void WriteXmlDcsLocalElement(CSharpCodeBuilder sb, CrystalJsonTypeMetadata target, string expr, string nameRef)
			{
				string? declared = GetXmlDcsContractName(target.Type);
				if (declared is null)
				{
					sb.AppendLine($"#error The DataContract XML name of type {target.Type.FullName} cannot be derived, so a member declared with that type cannot be annotated.");
					return;
				}

				// one level deeper: this is a nested element, and the callee's own guard is what stops a cycle running through it
				sb.AppendLine($"{GetLocalSerializerRef(target.Type)}.WriteXmlDcsElement(ref emitter, in {nameRef}, {expr}, settings, {CSharpCodeBuilder.Constant(declared)}, {XmlDepthParameterName} + 1);");
			}

			/// <summary>Writes a scalar element, and returns whether the type had a lexical form at all</summary>
			/// <remarks>An empty rendering keeps the self-closing form for every type except <see cref="string"/>, which is the one
			/// measured case where the reference format writes the expanded <c>&lt;X&gt;&lt;/X&gt;</c>. Only base64 can actually render
			/// empty, so that is the only type that pays for the test.</remarks>
			private bool WriteXmlDcsScalarElement(CSharpCodeBuilder sb, TypeMetadata actual, string expr, string nameRef, string scope)
			{
				// the compat format never reads the JSON-side enum settings: its labels come from the data contract
				var text = GetXmlScalarText(actual, expr);
				if (text is null) return false;

				if (IsXmlDcsByteArray(actual))
				{
					string local = "__b_" + scope;
					sb.AppendLine($"var {local} = {text.Value.Text};");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					sb.Comment("an empty base64 rendering is NO content, which keeps the self-closing form (measured: <EmptyBytes />)");
					sb.AppendLine($"if ({local}.Length != 0)");
					sb.EnterBlock();
					sb.AppendLine($"emitter.WriteRawAscii({local});");
					sb.LeaveBlock();
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					return true;
				}

				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				sb.AppendLine($"emitter.{(text.Value.NeedsEscaping ? "WriteText" : "WriteRawAscii")}({text.Value.Text});");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
				return true;
			}

			/// <summary>Writes the built-in contract of <see cref="DateTimeOffset"/>: the instant normalized to UTC, plus the offset in minutes</summary>
			private void WriteXmlDcsDateTimeOffsetElement(CSharpCodeBuilder sb, XmlNameTable names, string expr, string nameRef)
			{
				string? offsetNamespace = GetXmlDcsBuiltinNamespaceRef(XmlSystemContractNamespaceUri);
				string dateRef = names.Ref("DateTime", offsetNamespace);
				string offsetRef = names.Ref("OffsetMinutes", offsetNamespace);

				sb.Comment("DateTimeOffset has a built-in DataContract of its own: { DateTime (UTC), OffsetMinutes }");
				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				WriteXmlDcsWrapperNamespaceDeclaration(sb, offsetNamespace, "both members of this contract");
				sb.AppendLine($"emitter.WriteStartElement(in {dateRef});");
				sb.AppendLine($"emitter.WriteRawAscii({FormatXmlScalar("DateTime", expr + ".UtcDateTime")});");
				sb.AppendLine($"emitter.WriteEndElement(in {dateRef});");
				sb.AppendLine($"emitter.WriteStartElement(in {offsetRef});");
				sb.AppendLine($"emitter.WriteRawAscii({FormatXmlScalar("Int16", $"(short) {expr}.Offset.TotalMinutes")});");
				sb.AppendLine($"emitter.WriteEndElement(in {offsetRef});");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
			}

			/// <summary>Writes a sequence: one element per item, named after the ITEM type's contract</summary>
			/// <remarks>An empty sequence writes the self-closing wrapper, and a nested sequence names its items after the inner
			/// collection's own contract (<c>ArrayOfstring</c>), which is why this format has no equivalent of the general profile's
			/// rejection to nest bare collections (CXML0006).</remarks>
			private void WriteXmlDcsSequenceContent(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata type, string valueExpr, string nameRef, string scope)
			{
				var itemType = type.ElementType!;
				string? itemName = GetXmlDcsContractName(itemType);
				if (itemName is null)
				{
					sb.AppendLine($"#error The items of a collection of type {type.FullName} have no DataContract XML name this emission can derive. Introduce a named intermediate type for the item type.");
					return;
				}

				string? itemNamespace = GetXmlDcsNamespaceRef(type);
				string itemRef = names.Ref(itemName, itemNamespace);
				string item = "__i_" + scope;
				string itemScope = scope + "_i";

				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				WriteXmlDcsWrapperNamespaceDeclaration(sb, itemNamespace, "every item of this collection");
				sb.AppendLine($"foreach (var {item} in {valueExpr})");
				sb.EnterBlock("foreach");
				WriteXmlDcsValueElement(sb, names, member: null, itemType, item, itemRef, itemScope, XmlNullPolicy.NilWhenSettingsAsk);
				sb.LeaveBlock("foreach");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
			}

			/// <summary>Writes a dictionary as a sequence of <c>KeyValueOfKV</c> entries, each holding a <c>Key</c> and a <c>Value</c> element</summary>
			/// <remarks>DEVIATION 1: the entry name carries no namespace digest. The reference format appends one when either argument's
			/// contract namespace is not built-in (<c>KeyValueOfstringShelfRgT45H4A</c>); the algorithm is an undocumented internal,
			/// and no measured consumer reads any <c>KeyValueOf*</c> element.</remarks>
			private void WriteXmlDcsDictionaryContent(CSharpCodeBuilder sb, XmlNameTable names, TypeMetadata type, string valueExpr, string nameRef, string scope)
			{
				var keyType = type.KeyType!;
				var valueType = type.ValueType!;

				string? keyName = GetXmlDcsContractName(keyType);
				string? valueName = GetXmlDcsContractName(valueType);
				if (keyName is null || valueName is null)
				{
					sb.AppendLine($"#error The entries of a dictionary of type {type.FullName} have no DataContract XML name this emission can derive. Introduce a named intermediate type for the key or the value type.");
					return;
				}

				string? entryNamespace = GetXmlDcsNamespaceRef(type);
				string entryRef = names.Ref("KeyValueOf" + keyName + valueName, entryNamespace);
				string keyRef = names.Ref(XmlDcsKeyElementName, entryNamespace);
				string valueRef = names.Ref(XmlDcsValueElementName, entryNamespace);
				string entry = "__e_" + scope;
				string entryScope = scope + "_e";

				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				WriteXmlDcsWrapperNamespaceDeclaration(sb, entryNamespace, "every entry of this dictionary");
				sb.AppendLine($"foreach (var {entry} in {valueExpr})");
				sb.EnterBlock("foreach");
				sb.AppendLine($"emitter.WriteStartElement(in {entryRef});");
				WriteXmlDcsValueElement(sb, names, member: null, keyType, entry + ".Key", keyRef, entryScope + "_k", XmlNullPolicy.NilWhenSettingsAsk);
				WriteXmlDcsValueElement(sb, names, member: null, valueType, entry + ".Value", valueRef, entryScope + "_v", XmlNullPolicy.NilWhenSettingsAsk);
				sb.AppendLine($"emitter.WriteEndElement(in {entryRef});");
				sb.LeaveBlock("foreach");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
			}

			#endregion

			#region anyType...

			/// <summary>Writes a value whose declared type is <c>object</c>: the runtime type picks the contract, which is always annotated</summary>
			/// <remarks>
			/// <para>A closed <c>switch</c> over the types this emission can name: the DCS primitive set, plus every type of this
			/// container. It is reflection-free by construction, and that is also its limit - a runtime type outside that closed
			/// world (a <c>List&lt;string&gt;</c>, say, whose contract name would be <c>ArrayOfstring</c>) has no case to land in, so
			/// it raises a typed exception naming the type instead of guessing a shape.</para>
			/// <para>The annotation is unconditional for every case: the declared contract of the slot is <c>anyType</c>, which no
			/// value's own contract ever equals - except a bare <c>object</c>, which the last case writes as an empty element.</para>
			/// </remarks>
			private void WriteXmlDcsAnyTypeElement(CSharpCodeBuilder sb, XmlNameTable names, string valueExpr, string nameRef, string scope)
			{
				int index = 0;

				sb.AppendLine($"switch ({valueExpr})");
				sb.EnterBlock("switch");

				sb.AppendLine("case null:");
				sb.EnterBlock("case");
				sb.Comment("a null in an anyType slot is nil, under the same rule as a null member");
				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				sb.AppendLine($"if ({CrystalXmlSettingsExtensionsFullName}.IncludesNullMembers(settings))");
				sb.EnterBlock();
				sb.AppendLine($"emitter.WriteAttribute(in {XmlNilNameRef(names)}, \"true\");");
				sb.LeaveBlock();
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
				sb.AppendLine("break;");
				sb.LeaveBlock("case");

				foreach (var (_, _, keyword, contract, family, escaped) in XmlDcsLexicalTypes)
				{
					// a row with no keyword takes no case here (a bare object is the switch's own last case, and a
					// DateTimeOffset is a structure rather than a lexical form)
					if (keyword is null) continue;

					// one local per case, even though a switch section is its own declaration space: two identically named
					// pattern variables in one switch read like a mistake to the next person editing this
					string local = "__a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + scope;
					++index;

					sb.AppendLine($"case {keyword} {local}:");
					sb.EnterBlock("case");
					sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
					WriteXmlDcsTypeAnnotation(sb, names, contract, GetXmlDcsBuiltinNamespaceRef(XmlSchemaNamespaceUri));
					if (family is null)
					{ // a string is written straight through, with no lexical transformation
						sb.AppendLine($"emitter.WriteText({local});");
					}
					else if (contract == "base64Binary")
					{
						sb.AppendLine($"var {local}_t = {FormatXmlScalar(family, local)};");
						sb.AppendLine($"if ({local}_t.Length != 0)");
						sb.EnterBlock();
						sb.AppendLine($"emitter.WriteRawAscii({local}_t);");
						sb.LeaveBlock();
					}
					else
					{
						sb.AppendLine($"emitter.{(escaped ? "WriteText" : "WriteRawAscii")}({FormatXmlScalar(family, local)});");
					}
					sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
					sb.AppendLine("break;");
					sb.LeaveBlock("case");
				}

				// a DECLARED derived type gets no case of its own: its base's case captures it, and the base's own dispatch
				// switch hands it to the right body with the declared contract intact. Two registered types in an UNDECLARED
				// base/derived relationship both get one, though, and a base case emitted before its subclass would make the
				// subclass's case unreachable (CS8120, a build failure in the generated code): the most derived case comes
				// first. Inheritance depth is the only key, and OrderByDescending is stable, so two unrelated types keep
				// their declaration order and the emission stays deterministic.
				foreach (var included in this.Metadata.IncludedTypes.OrderByDescending(static t => t.Type.InheritanceDepth))
				{
					if (included.Type.IsAbstract) continue;
					if (this.PolymorphicMap.ContainsKey(included.Type.Ref)) continue;
					string? contract = GetXmlDcsContractName(included.Type);
					if (contract is null) continue;

					string local = "__a" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + scope;
					++index;

					sb.AppendLine($"case {included.Type.FullyQualifiedName} {local}:");
					sb.EnterBlock("case");
					sb.AppendLine($"{GetLocalSerializerRef(included.Type)}.WriteXmlDcsElement(ref emitter, in {nameRef}, {local}, settings, {CSharpCodeBuilder.Constant(XmlDcsAnyTypeName)}, {XmlDepthParameterName} + 1);");
					sb.AppendLine("break;");
					sb.LeaveBlock("case");
				}

				sb.AppendLine($"case {{ }} when {valueExpr}.GetType() == typeof(object):");
				sb.EnterBlock("case");
				sb.Comment("a bare object: its own contract IS anyType, so no annotation, and no content");
				sb.AppendLine($"emitter.WriteStartElement(in {nameRef});");
				sb.AppendLine($"emitter.WriteEndElement(in {nameRef});");
				sb.AppendLine("break;");
				sb.LeaveBlock("case");

				this.XmlNeedsAnyTypeHelper = true;
				sb.AppendLine("default:");
				sb.EnterBlock("case");
				sb.AppendLine($"FailXmlAnyType({valueExpr}.GetType(), {CSharpCodeBuilder.Constant(scope)});");
				sb.AppendLine("break;");
				sb.LeaveBlock("case");

				sb.LeaveBlock("switch");
			}

			/// <summary>Whether the converter currently being emitted needs the anyType rejection helper</summary>
			private bool XmlNeedsAnyTypeHelper { get; set; }

			/// <summary>Emits the helper that rejects a runtime type an <c>anyType</c> slot cannot name</summary>
			private static void WriteXmlDcsAnyTypeHelper(CSharpCodeBuilder sb)
			{
				sb.Comment("an anyType slot names its value after the value's own contract, and the set of contracts this container can name is closed at generation time");
				sb.AppendLine($"private static void FailXmlAnyType({SystemTypeFullName} type, string slot)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {NotSupportedExceptionFullName}(\"Cannot write a value of type '\" + type.Name + \"' into the object-typed slot '\" + slot + \"': the DataContract XML format names it after its own contract, and this container can only name the built-in lexical types and its own serialized types. Declare the value's type in this container with [CrystalSerializable], or change the member to a concrete type.\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			#endregion

			#region Enums...

			/// <summary>Emits the label lookup of one enum, on the DataContract format</summary>
			/// <remarks>
			/// <para>The label is the <c>[EnumMember(Value = ...)]</c> token when the member declares one, and the declared name
			/// otherwise. The System.Text.Json token that the general format prefers is deliberately NOT read here: the output this
			/// profile reproduces never saw it.</para>
			/// <para>On a <c>[DataContract]</c> enum only the members carrying <c>[EnumMember]</c> are serializable, so the others get
			/// no case at all and land in the rejection arm, exactly as the reference serializer rejects them.</para>
			/// </remarks>
			private void WriteXmlDcsEnumHelper(CSharpCodeBuilder sb, string methodName, TypeMetadata type)
			{
				string fullName = type.FullyQualifiedName;

				WriteXmlEnumLabelSwitch(
					sb,
					methodName,
					type,
					$"Labels of {type.Name} on the DataContract format, resolved by a switch: Enum.ToString() would go through the runtime's reflection-backed name cache",
					// on a [DataContract] enum only the [EnumMember]-annotated members are serializable: the others get no case at
					// all, and land in the rejection arm exactly as the reference serializer rejects them
					member => !type.IsDataContractEnum || member.HasEnumMemberAttribute,
					static member => member.EnumMemberValue ?? member.Name,
					() =>
					{
						if (type.IsFlagsEnum)
						{
							this.XmlNeedsFlagsHelper = true;
							sb.AppendLine($"_ => {methodName}__flags(value),");
						}
						else
						{
							this.XmlNeedsUndeclaredEnumHelper = true;
							sb.AppendLine($"_ => FailXmlUndeclaredEnum(typeof({fullName}), {FormatXmlScalar(GetXmlEnumUnderlyingFamily(type), $"({GetXmlEnumUnderlyingKeyword(type)}) value")}),");
						}
					});

				if (type.IsFlagsEnum)
				{
					WriteXmlDcsFlagsCombiner(sb, methodName + "__flags", type);
				}
			}

			/// <summary>Emits the combiner of one <c>[Flags]</c> enum: the declared labels of the set bits, joined by a space, in declaration order</summary>
			/// <remarks>Reflection-free by construction: the bits and their labels are constants baked in at generation time. A value
			/// with a bit no declared member covers, or a zero value with no declared member, is rejected rather than approximated.</remarks>
			private void WriteXmlDcsFlagsCombiner(CSharpCodeBuilder sb, string methodName, TypeMetadata type)
			{
				string fullName = type.FullyQualifiedName;

				sb.Comment($"Combination of the declared flags of {type.Name}: the reference format joins the labels with a space, in declaration order");
				sb.AppendLine($"private static string {methodName}({fullName} value)");
				sb.EnterBlock();
				sb.AppendLine("long __rest = unchecked((long) value);");
				sb.AppendLine($"var __sb = new {StringBuilderFullName}();");

				int index = 0;
				var seen = new HashSet<string>(StringComparer.Ordinal);
				foreach (var member in type.EnumMembers)
				{
					if (type.IsDataContractEnum && !member.HasEnumMemberAttribute) continue;
					if (!seen.Add(member.Value)) continue;
					if (member.Value == "0") continue; // the zero member cannot be a bit of a combination

					string bit = "__bit" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
					++index;
					sb.AppendLine($"long {bit} = unchecked((long) {fullName}.{member.Name});");
					sb.AppendLine($"if ((__rest & {bit}) == {bit})");
					sb.EnterBlock();
					sb.AppendLine("if (__sb.Length != 0) __sb.Append(' ');");
					sb.AppendLine($"__sb.Append({CSharpCodeBuilder.Constant(member.EnumMemberValue ?? member.Name)});");
					sb.AppendLine($"__rest &= ~{bit};");
					sb.LeaveBlock();
				}

				sb.AppendLine("if (__rest != 0 || __sb.Length == 0)");
				sb.EnterBlock();
				this.XmlNeedsFlagsHelper = true;
				sb.AppendLine($"{XmlDcsUndeclaredFlagsHelperName}(typeof({fullName}), {FormatXmlScalar(GetXmlEnumUnderlyingFamily(type), $"({GetXmlEnumUnderlyingKeyword(type)}) value")});");
				sb.LeaveBlock();
				sb.AppendLine("return __sb.ToString();");
				sb.LeaveBlock();
				sb.NewLine();
			}

			/// <summary>Whether the converter currently being emitted needs the undeclared-enum rejection helper</summary>
			private bool XmlNeedsUndeclaredEnumHelper { get; set; }

			/// <summary>Emits the helper that rejects an enum value the data contract does not declare</summary>
			private static void WriteXmlDcsUndeclaredEnumHelper(CSharpCodeBuilder sb)
			{
				sb.Comment("the reference serializer rejects a value that is not a declared (and, on a [DataContract] enum, [EnumMember]-annotated) member; it raises SerializationException, this format raises its own typed exception");
				sb.AppendLine($"private static string FailXmlUndeclaredEnum({SystemTypeFullName} type, string value)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {NotSupportedExceptionFullName}(\"Cannot write the value '\" + value + \"' of enum '\" + type.Name + \"' to XML: it is not one of the members the data contract of this enum declares. Declare it as a member of the enum, or add [EnumMember] to the member that carries this value.\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			/// <summary>Name of the emitted undeclared-flags rejection of THIS format</summary>
			/// <remarks>Deliberately not the general format's <c>FailXmlUndeclaredFlags</c>, which carries the same concept under an
			/// INCOMPATIBLE signature (it returns the <c>string</c> a switch arm evaluates to, where this one is a statement that
			/// returns nothing). No generated class holds both today - a container resolves to exactly one profile - but two same-named
			/// helpers with different signatures is a collision waiting for the first piece of code that emits both.</remarks>
			private const string XmlDcsUndeclaredFlagsHelperName = "FailXmlDcsUndeclaredFlags";

			/// <summary>Emits the helper that rejects a combination of <c>[Flags]</c> the declared members do not cover</summary>
			private static void WriteXmlDcsFlagsHelper(CSharpCodeBuilder sb)
			{
				sb.Comment("a [Flags] value with a bit no declared member covers has no label the reference format would produce either");
				sb.AppendLine($"private static void {XmlDcsUndeclaredFlagsHelperName}({SystemTypeFullName} type, string value)");
				sb.EnterBlock();
				sb.AppendLine($"throw new {NotSupportedExceptionFullName}(\"Cannot write the value '\" + value + \"' of enum '\" + type.Name + \"' to XML: it combines bits that the declared members of this [Flags] enum do not cover, so the data contract has no label for it. Declare the missing member.\");");
				sb.LeaveBlock();
				sb.NewLine();
			}

			#endregion

		}

	}

}
