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

//#define FULL_DEBUG

namespace SnowBank.Serialization.Json.CodeGen
{
	using System.Text;
	using Microsoft.CodeAnalysis;

	/// <summary>Metadata about the container type that will host the generated code for one or more types</summary>
	public sealed record CrystalJsonContainerMetadata
	{

		/// <summary>Name of the container</summary>
		public required string Name { get; init; }
		
		/// <summary>Type of the container</summary>
		public required TypeMetadata Type { get; init; }

		/// <summary>List of all application types that will be part of the generated source code</summary>
		public required ImmutableEquatableArray<CrystalJsonTypeMetadata> IncludedTypes { get; init; }

		/// <summary>Specifies whether property names are case-insensitive (true) or not (false).</summary>
		public bool PropertyNameCaseInsensitive { get; init; }

		/// <summary>Default naming policy for all the properties of types in this container</summary>
		public string? PropertyNamingPolicy { get; init; }

		/// <summary>The consuming compilation defines <c>[UnsafeAccessor]</c> (net8+): non-public members are reached through zero-cost accessor thunks; otherwise the generated code falls back to reflection-based accessors</summary>
		public bool SupportsUnsafeAccessors { get; init; }

		/// <summary>The consuming compilation can host the generated JSON proxies (the proxy interfaces are visible, and the language version supports the static abstract members they implement)</summary>
		/// <remarks>False on the <c>netstandard2.0</c>/<c>net472</c> "lite" path, and in any consumer below C# 11: the container then gets its converters, its <c>TypeMapper</c> and its XML output, but no <c>ReadOnly</c>/<c>Writable</c> proxy types.</remarks>
		public bool SupportsJsonProxies { get; init; }

		/// <summary>The consuming compilation can see a usable <c>[DynamicallyAccessedMembers]</c>; when false the trimming annotations are left out of the generated code</summary>
		public bool SupportsDynamicallyAccessedMembers { get; init; }

		/// <summary>Name of the output profile baked into the container's generated entry points (<c>"DataContractCompat"</c>), or <see langword="null"/> for the standard format</summary>
		/// <remarks>The profile only replaces the "caller passed no settings" fallback of the generated entry points; explicitly passed settings always win entirely.</remarks>
		public string? OutputProfile { get; init; }

		/// <summary>The container produces the JSON format: the entry points (<c>Serialize</c>, <c>Pack</c>, <c>Unpack</c>, <c>ToJsonText</c>, ...), the <c>IJsonConverter</c> facet, the type definition, the proxies and the container's <c>TypeMapper</c></summary>
		/// <remarks>
		/// <para>False for an XML-only container (<c>[CrystalXmlConverter]</c>, or <c>[CrystalConverter]</c> with only <c>[CrystalXmlOutput]</c>): its holders keep their <c>Default</c> singleton and their XML output, and carry no JSON surface at all.</para>
		/// <para>A self-serializable type always produces JSON (its marker is a JSON one), so this is <c>true</c> on that path.</para>
		/// </remarks>
		public bool GeneratesJson { get; init; } = true;

		/// <summary>Name of the resolved XML format profile of this container (<c>"General"</c> or <c>"DataContract"</c>), or <see langword="null"/> when the container produces no XML output</summary>
		/// <remarks>
		/// <para>XML output is strictly opt-in: only a container decorated with <c>[CrystalXmlOutput]</c> gets a non-null profile. The value is already RESOLVED: an explicit <c>Profile</c> wins, otherwise it is derived from <see cref="OutputProfile"/> (the DCJS JSON format derives the DataContract XML format, anything else derives the General one).</para>
		/// <para>Typed as a string, like <see cref="OutputProfile"/>: the metadata layer stays symbol-free, so the whole record keeps a cheap structural equality for the incremental pipeline.</para>
		/// </remarks>
		public string? XmlProfile { get; init; }

		/// <summary>Name of the container's default representation for dictionary-like members (<c>"Default"</c> when the container did not override it), or <see langword="null"/> when the container produces no XML output</summary>
		/// <remarks><c>"Default"</c> is carried as-is rather than flattened into the profile's shape: the per-profile default and the per-member override both resolve downstream, and collapsing them here would lose the "unset" state.</remarks>
		public string? CrystalXmlDictionaryFormat { get; init; }

		/// <summary>Whether the container's DataContract XML output is stripped of its namespaces and prefixes</summary>
		/// <remarks>Already RESOLVED: the option is inert on the General format, where the parser reports <c>CXML0012</c> and
		/// clears it, so <see langword="true"/> here always means the DataContract format with its namespaces stripped.</remarks>
		public bool CrystalXmlOmitNamespaces { get; init; }

		/// <summary>Specifies whether the container is the serialized type itself (self-serializable mode)</summary>
		/// <remarks>
		/// <para>When <c>true</c>, <see cref="Type"/> is a partial application type that acts as its own container: all its generated code lives inside a single reserved nested scope (ex: <c>Widget.Json.ReadOnly</c>), and any other included type (crawled from its members) is hosted inside that scope under its own name (ex: <c>Widget.Json.WidgetPart.ReadOnly</c>; inside the scope, holders cannot shadow the referenced types in the entity's own source).</para>
		/// <para>When <c>false</c>, the container is a static partial class decorated with <c>[CrystalJsonConverter]</c>, and every included type gets a nested static holder class (ex: <c>AcmeConverters.Widget.ReadOnly</c>).</para>
		/// </remarks>
		public bool IsSelfContained { get; init; }

	}
	
	/// <summary>Metadata about a serialized type</summary>
	public sealed record CrystalJsonTypeMetadata
	{
		
		/// <summary>Symbol for the serialized type</summary>
		public required TypeMetadata Type { get; init; }

		/// <summary>Friendly name of the type, used as the prefix of the generated converters (ex: "User", "Account", "Order", ...)</summary>
		public string Name => this.Type.Name;

		/// <summary>For objects, list of included members in this type</summary>
		public required ImmutableEquatableArray<CrystalJsonMemberMetadata> Members { get; init; }

		/// <summary>Indicates if this is the top-most base type for a tree of derived types</summary>
		public required bool IsPolymorphicRoot { get; init; }

		/// <summary>The generated converter writes this type by calling its own <c>IJsonSerializable.JsonSerialize</c>, instead of walking its members</summary>
		/// <remarks>The three facets are resolved independently: a type implementing only one of the interfaces keeps a generated body for the other two.</remarks>
		public bool DefersSerialize { get; init; }

		/// <summary>The generated converter packs this type by calling its own <c>IJsonPackable.JsonPack</c>, instead of walking its members</summary>
		/// <inheritdoc cref="DefersSerialize" path="/remarks"/>
		public bool DefersPack { get; init; }

		/// <summary>The generated converter reads this type by calling its own <c>IJsonDeserializable&lt;T&gt;.JsonDeserialize</c>, instead of binding its members</summary>
		/// <inheritdoc cref="DefersSerialize" path="/remarks"/>
		public bool DefersUnpack { get; init; }

		/// <summary>The author declared a <c>Serialize</c> method in this type's scope, which the generated converter calls</summary>
		/// <remarks>A hook outranks <see cref="DefersSerialize"/>: it is the way a second container gives a bespoke format to a type that already has one of its own.</remarks>
		public bool HasSerializeHook { get; init; }

		/// <summary>The author declared a <c>Pack</c> method in this type's scope, which the generated converter calls</summary>
		/// <inheritdoc cref="HasSerializeHook" path="/remarks"/>
		public bool HasPackHook { get; init; }

		/// <summary>The author declared an <c>Unpack</c> method in this type's scope, which the generated converter calls</summary>
		/// <inheritdoc cref="HasSerializeHook" path="/remarks"/>
		public bool HasUnpackHook { get; init; }

		/// <summary>The generated code does not control this type's format on at least one facet, so no proxy can describe it</summary>
		/// <remarks>A <c>ReadOnly</c>/<c>Writable</c> proxy is a typed view of a shape the generator knows: it reads and writes the JSON by the member names it chose. Once a type answers a facet itself, or an author hooks one, that shape is decided elsewhere and the generator cannot describe it, so it emits no proxy rather than one that reads the wrong names.</remarks>
		public bool SuppressesProxies => this.DefersSerialize || this.DefersPack || this.DefersUnpack || this.HasSerializeHook || this.HasPackHook || this.HasUnpackHook;

		/// <summary>The type's serialization lifecycle callbacks, if any</summary>
		public CrystalJsonCallbackMetadata? OnSerializing { get; init; }

		/// <inheritdoc cref="OnSerializing"/>
		public CrystalJsonCallbackMetadata? OnSerialized { get; init; }

		/// <inheritdoc cref="OnSerializing"/>
		public CrystalJsonCallbackMetadata? OnDeserializing { get; init; }

		/// <inheritdoc cref="OnSerializing"/>
		public CrystalJsonCallbackMetadata? OnDeserialized { get; init; }

		/// <summary>Specifies if this type declares at least one lifecycle callback that generated code must invoke</summary>
		public bool HasCallbacks => this.OnSerializing != null || this.OnSerialized != null || this.OnDeserializing != null || this.OnDeserialized != null;

		/// <summary>Name of the field added to the JSON output, that holds the type discriminator value</summary>
		/// <remarks>If <c>null</c>, the default name is <c>$type</c>.</remarks>
		public required string? TypeDiscriminatorPropertyName { get; init; }

		/// <summary>If this type is polymorphic, list of all the known derived types</summary>
		public required ImmutableEquatableArray<(INamedTypeSymbol Symbol, TypeMetadata Type, object? Discriminator)> DerivedTypes { get; init; }

		/// <summary>The type carries a <c>[DataContract]</c> attribute</summary>
		/// <remarks>Kept next to <see cref="DataContractName"/>, which only records a renaming: the DataContract XML format needs the PRESENCE on its own, because a data contract outranks every other way of serializing a type (in particular the <c>ISerializable</c> dialect, which only applies to a type that declares no contract).</remarks>
		public bool HasDataContract { get; init; }

		/// <summary>Value of <c>[DataContract(Name = "...")]</c>, or <see langword="null"/> when the type carries no data contract, or one that does not rename it</summary>
		/// <remarks>The DataContract XML format names the type's element after the contract, so the VALUE is needed, not just the attribute's presence (which is all the JSON format ever needed). Left <see langword="null"/> rather than defaulted to the type name: the default is a format rule, and resolving it here would make an explicit <c>Name</c> that happens to match indistinguishable from an absent one.</remarks>
		public string? DataContractName { get; init; }

		/// <summary>Value of <c>[DataContract(Namespace = "...")]</c>, or <see langword="null"/> when the type carries no data contract, or one that does not set a namespace</summary>
		/// <inheritdoc cref="DataContractName" path="/remarks"/>
		public string? DataContractNamespace { get; init; }

		public void Explain(StringBuilder sb, string? indent = null)
		{
			var subIndent = indent is null ? "- " : ("  " + indent);
			var subIndent2 = ("  " + subIndent);

			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("Type = ").AppendLine(this.Type.Ref.ToString());
			this.Type.Explain(sb, indent is null ? "- " : ("  " + indent));
			if (this.IsPolymorphicRoot)
			{
				sb.Append(indent).AppendLine("IsPolymorphicRoot = true");
			}

			if (!string.IsNullOrEmpty(this.TypeDiscriminatorPropertyName))
			{
				sb.Append(indent).Append("TypeDiscriminatorPropertyName = ").AppendLine(this.TypeDiscriminatorPropertyName);
			}
			if (this.DerivedTypes.Count > 0)
			{
				sb.Append(indent).Append("DerivedTypes = [").Append(this.DerivedTypes.Count).AppendLine("]");
				foreach (var derivedType in this.DerivedTypes)
				{
					switch (derivedType.Discriminator)
					{
						case null: sb.Append(subIndent).AppendLine($"{derivedType.Type.Name}: null"); break;
						case string s: sb.Append(subIndent).AppendLine($"{derivedType.Type.Name}: \"{s}\""); break;
						case int n: sb.Append(subIndent).AppendLine($"{derivedType.Type.Name}: {n}"); break;
						default: sb.Append(subIndent).AppendLine($"{derivedType.Type.Name}: ({derivedType.Discriminator.GetType().Name}) `{derivedType.Discriminator}`"); break;
					}
				}
			}
			sb.Append(indent).Append("Members = [").Append(this.Members.Count).AppendLine("]");
			foreach (var member in this.Members)
			{
				sb.Append(subIndent).AppendLine(member.Name);
				member.Explain(sb, subIndent2);
			}
		}

	}

	/// <summary>Metadata about a member (field or property) of a serialized type</summary>
	/// <summary>What the deserialize-side callbacks accept as their single argument</summary>
	public enum CrystalJsonCallbackArgument
	{
		/// <summary><c>void M()</c></summary>
		None = 0,
		/// <summary><c>void M(JsonValue)</c></summary>
		JsonValue,
		/// <summary><c>void M(JsonObject)</c></summary>
		JsonObject,
		/// <summary><c>void M(JsonArray)</c></summary>
		JsonArray,
	}

	/// <summary>One serialization lifecycle callback that generated code must invoke</summary>
	public sealed record CrystalJsonCallbackMetadata
	{
		/// <summary>Name of the method to call</summary>
		public required string MethodName { get; init; }

		/// <summary>Whether the method needs an accessor thunk (it is not reachable from generated code)</summary>
		public required bool IsNonPublic { get; init; }

		/// <summary>The single argument the method takes, if any</summary>
		public required CrystalJsonCallbackArgument Argument { get; init; }
	}

	public sealed record CrystalJsonMemberMetadata
	{
		
		/// <summary>Name, as serialized in the JSON output</summary>
		/// <example><c>[JsonProperty("helloWorld")] public string HelloWorld { ... }</c> has name <c>"helloWorld"</c></example>
		public required string Name { get; init; }
		
		/// <summary>Type of the member</summary>
		public required TypeMetadata Type { get; init; }
		
		/// <summary>Name of the member in the container type</summary>
		/// <example><c>public string HelloWorld { get; init;}</c> has member name <c>"HelloWorld"</c></example>
		public required string MemberName { get; init; }

		/// <summary>Name given to the member by the data contract (<c>[DataMember(Name = "...")]</c> on a <c>[DataContract]</c> type), or <see langword="null"/> when the contract does not rename it</summary>
		/// <remarks>
		/// <para>Carried apart from <see cref="Name"/> so that the DataContract XML format writes the contract's own name (<see cref="DataMemberName"/>, falling back to <see cref="MemberName"/>) instead of the resolved JSON one. On a <c>[DataContract]</c> type the two agree, because CJSON0011 refuses a member whose JSON naming attribute disagrees with its contract name. On a plain DTO they do not: a <c>[JsonProperty]</c> renames the JSON member of a type that has no contract, and the reference serializer still writes the member's own name.</para>
		/// <para>Only ever set on a <c>[DataContract]</c> type: the attribute is inert everywhere else, exactly as it is for the reference serializer.</para>
		/// </remarks>
		public string? DataMemberName { get; init; }

#if FULL_DEBUG
		/// <summary>Captured attributes on the member</summary>
		public required ImmutableEquatableArray<string> Attributes { get; init; }
#endif

		/// <summary><c>true</c> if the member is a field, <c>false</c> if it is a property</summary>
		public required bool IsField { get; init; } // true = field, false = prop

		/// <summary><c>true</c> if the member is read-only</summary>
		/// <remarks>For properties, this means there is not SetMethod.</remarks>
		/// <example><c>public string HelloWorld { get; }</c> is read-only</example>
		public required bool IsReadOnly { get; init; }

		/// <summary><c>true</c> if the member is init-only</summary>
		/// <example><c>public string HelloWorld { get; init; }</c> is init-only</example>
		public required bool IsInitOnly { get; init; }
		
		/// <summary><c>true</c> if the member is annotated with the <c>required</c> keyword</summary>
		/// <example><c>public required string Id { ... }</c> is required</example>
		public required bool IsRequired { get; init; }

		/// <summary><c>true</c> if the member must be PRESENT in the document when binding (<c>[DataMember(IsRequired = true)]</c>)</summary>
		/// <remarks>Deliberately distinct from <see cref="IsRequired"/>: an ABSENT member throws, but an explicit <c>null</c> satisfies it, which is what <c>DataContractJsonSerializer</c> does. The <c>required</c> keyword refuses null as well.</remarks>
		public bool IsRequiredPresence { get; init; }

		/// <summary><c>true</c> if <see cref="DefaultLiteral"/> is not the default for this type</summary>
		public required bool HasNonZeroDefault { get; init; }

		/// <summary>Serialization condition from <c>[JsonIgnore(Condition = ...)]</c>: <c>"Never"</c>, <c>"WhenWritingNull"</c>, <c>"WhenWritingDefault"</c>, or <see langword="null"/> when unconditional</summary>
		/// <remarks>A member ignored with <c>JsonIgnoreCondition.Always</c> is not part of the member list at all.</remarks>
		public string? IgnoreCondition { get; init; }

		/// <summary>Per-member enum format from <c>[JsonProperty(EnumFormat = ...)]</c>: <c>"String"</c>, <c>"Number"</c>, or <see langword="null"/> when inherited from the settings</summary>
		public string? EnumFormat { get; init; }

		/// <summary>Per-member number format from <c>[JsonProperty(NumberFormat = ...)]</c>: <c>"String"</c>, or <see langword="null"/> for the numeric default</summary>
		public string? NumberFormat { get; init; }

		/// <summary>XML name of this member from <c>[XmlProperty]</c>, or <see langword="null"/> when the member does not override it (the XML name then derives from <see cref="Name"/>)</summary>
		/// <remarks>
		/// <para>Already NORMALIZED: the <c>"@id"</c> sugar has been split into <c>"id"</c> plus <see cref="XmlIsAttribute"/>, so nothing downstream ever sees a <c>'@'</c> in a name, and the value is known to be a legal XML NCName.</para>
		/// <para>Only ever set when the container produces XML: on a JSON-only container the whole XML vocabulary is inert.</para>
		/// </remarks>
		public string? CrystalXmlName { get; init; }

		/// <summary>The member is projected as an XML ATTRIBUTE instead of a nested element (from <c>[XmlProperty(Attribute = true)]</c> or the <c>"@name"</c> sugar)</summary>
		public bool XmlIsAttribute { get; init; }

		/// <summary>Name given to the items of a collection member, or the entries of a dictionary member, from <c>[XmlProperty(ItemName = "...")]</c>; <see langword="null"/> when not overridden</summary>
		/// <inheritdoc cref="CrystalXmlName" path="/remarks/para[2]"/>
		public string? XmlItemName { get; init; }

		/// <summary>Per-member dictionary representation from <c>[XmlProperty(DictionaryFormat = ...)]</c>, as the enum MEMBER NAME (ex: <c>"KeyValueAttributes"</c>); <see langword="null"/> when not overridden</summary>
		/// <inheritdoc cref="CrystalXmlName" path="/remarks/para[2]"/>
		public string? CrystalXmlDictionaryFormat { get; init; }

		/// <summary>Value of <c>[DataMember(Order = n)]</c> when <c>n</c> is zero or greater, or <see langword="null"/> when the member declares no order</summary>
		/// <remarks>A NEGATIVE order is carried as <see langword="null"/>, like <c>DataContractSerializer</c> does: an unordered member sorts by name, which is a different rule from ordering at zero.</remarks>
		public int? DataMemberOrder { get; init; }

		/// <summary>Contract namespace of the type that DECLARES this member, as its <c>[DataContract(Namespace = ...)]</c> spells it; <see langword="null"/> when that type declares none</summary>
		/// <remarks>Captured because a member element lives in the namespace of the contract that DECLARES it, not of the type
		/// being written: a derived type in another namespace still writes its inherited members in the base contract's
		/// namespace. The empty string is a value here, and means the contract asked for no namespace at all.</remarks>
		public string? DeclaringDataContractNamespace { get; init; }

		/// <summary>CLR namespace of the type that DECLARES this member, which the DataContract default rule derives a namespace from</summary>
		/// <remarks>Carried next to <see cref="DeclaringDataContractNamespace"/> and not folded into it, because the defaulting
		/// rule belongs to the format: this layer records what the declaration says, and the emitter applies the rule.</remarks>
		public string? DeclaringTypeNameSpace { get; init; }

		/// <summary>Depth of the type that DECLARES this member in the inheritance chain, counted from the topmost base (<c>0</c>) down to the type being serialized</summary>
		/// <remarks>Captured for the DataContract XML format, whose member order is "every member of the base level first, then the next level": the flat member list cannot be regrouped by level once that fact is lost. The JSON format ignores it.</remarks>
		public int InheritanceLevel { get; init; }

		/// <summary>The member is written even when its value is the type's default (<see langword="false"/> only for <c>[DataMember(EmitDefaultValue = false)]</c>)</summary>
		/// <remarks>The RAW flag, carried as the attribute spells it: how it combines with <see cref="IgnoreCondition"/> is a format rule resolved downstream, and folding it in here would make the two indistinguishable.</remarks>
		public bool EmitDefaultValue { get; init; } = true;

		/// <summary>Fully qualified name of a custom converter attached to this member (<c>[JsonConverter(typeof(...))]</c> naming a type with the Pack/Unpack pair, or the built-in converter for <c>[JsonBooleanLiterals]</c>), or <see langword="null"/></summary>
		public string? CustomConverterType { get; init; }

		/// <summary>C# argument list for the custom converter's constructor (empty for a parameterless converter)</summary>
		public string? CustomConverterArgs { get; init; }

		/// <summary>The custom converter implements the packing facet (<c>IJsonPacker&lt;T&gt;</c>); when <see langword="false"/>, any attempt to serialize the member fails loudly</summary>
		public bool CustomConverterHasPacker { get; init; } = true;

		/// <summary>The custom converter implements the deserializing facet (<c>IJsonDeserializer&lt;T&gt;</c>); when <see langword="false"/>, any attempt to deserialize a present value for the member fails loudly</summary>
		public bool CustomConverterHasDeserializer { get; init; } = true;

		/// <summary>The custom converter is declared for the member's <c>Nullable&lt;T&gt;</c> form itself (e.g. <c>IJsonDeserializer&lt;DateTime?&gt;</c> on a <c>DateTime?</c> member), so the emitter must call the nullable-form unpack helpers instead of unwrap-then-lift</summary>
		public bool CustomConverterIsNullableForm { get; init; }

		/// <summary>The custom converter implements the XML facet (<c>ICrystalXmlSerializer&lt;T&gt;</c>) for this member's type</summary>
		/// <remarks>Only ever resolved when the container produces XML: a converter without the facet is a build error there (CXML0008), because the member's XML form would otherwise be written by rules the converter was declared to replace.</remarks>
		public bool CustomConverterHasXmlSerializer { get; init; }

		/// <summary>The custom converter's XML facet is declared for the member's <c>Nullable&lt;T&gt;</c> form itself (<c>ICrystalXmlSerializer&lt;DateTime?&gt;</c> on a <c>DateTime?</c> member), which is the form the emitter casts to when it hands the member over</summary>
		/// <remarks>
		/// <para>This selects the CAST's type argument (and therefore whether the value is unwrapped before the call), nothing more:
		/// it does NOT make the converter responsible for the null case. On the XML format a null member is decided by the profile
		/// before any converter runs (absent, or <c>nil</c> under the settings), so a facet declared for <c>T?</c> is only ever
		/// called with a value that has one.</para>
		/// <para>Resolved separately from <see cref="CustomConverterIsNullableForm"/>, which is the JSON side's answer to the same
		/// question: one converter can perfectly well declare the nullable form on one format and not on the other.</para>
		/// </remarks>
		public bool CustomConverterXmlFacetDeclaredForNullable { get; init; }

		/// <summary>C# literal for the expression that represents the default value for this member, when it is missing</summary>
		/// <remarks>This should be a valid C# constant expression, like <c>123</c>, <c>"hello"</c>, <c>true</c>, <c>global::System.Guid.Empty</c>, ...</remarks>
		public required string DefaultLiteral { get; init; }

		/// <summary>The member is annotated with the <c>[System.ComponentModel.DataAnnotations.Key]</c> attribute</summary>
		/// <remarks>Examples: <code>
		/// int Id { get; ... } // IsKey == false
		///
		/// [Key]
		/// int Id { get; ... } // IsKey == true
		/// </code></remarks>
		public required bool IsKey { get; init; }

		/// <summary>The member itself is not reachable from the generated code (private, protected, or private protected): every read and write goes through an accessor thunk</summary>
		/// <remarks>Only members carrying <c>[JsonInclude]</c> reach this state; <c>internal</c> and <c>protected internal</c> members are reachable directly (the generated code lives in the same assembly).</remarks>
		public bool IsNonPublic { get; init; }

		/// <summary>The member is reachable but its set/init accessor is not (e.g. a public property with a private setter): writes go through an accessor thunk</summary>
		public bool HasNonPublicSetter { get; init; }

		/// <summary>The member is reachable but its get accessor is not (e.g. a public property with a private getter): reads go through an accessor thunk</summary>
		public bool HasNonPublicGetter { get; init; }

		/// <summary>Reads of this member go through the <c>__get_X</c> accessor thunk</summary>
		public bool NeedsGetterThunk => this.IsNonPublic || this.HasNonPublicGetter;

		/// <summary>Writes of this member go through the <c>__set_X</c> accessor thunk (instead of an object-initializer entry)</summary>
		public bool NeedsSetterThunk => this.IsNonPublic || this.HasNonPublicSetter;

		/// <summary>The member cannot be <c>null</c>, or is annotated with the <c>[System.Diagnostics.CodeAnalysis.NotNull]</c> attribute</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNotNull == true
		/// int? Foo { get; ... }    // IsNotNull == false
		/// string Foo { get; ... }  // IsNotNull == true
		/// string? Foo { get; ... } // IsNotNull == false
		/// </code></remarks>
		public required bool IsNotNull { get; init; }

		/// <summary>The member if a reference type that is declared as nullable in its parent type</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNullableRefType == false
		/// int? Foo { get; ... }    // IsNullableRefType == false
		/// string Foo { get; ... }  // IsNullableRefType == false
		/// string? Foo { get; ... } // IsNullableRefType == true
		/// </code></remarks>
		public bool IsNullableRefType() => !this.IsNotNull && this.Type.NullableOfType is null;

		public void Explain(StringBuilder sb, string? indent = null)
		{
			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("MemberName = ").AppendLine(this.MemberName);
			if (this.DataMemberName is not null) sb.Append(indent).Append("DataMemberName = ").AppendLine(this.DataMemberName);
			if (this.IsField) sb.Append(indent).AppendLine("IsField = true");
			if (this.IsNotNull) sb.Append(indent).AppendLine("IsNotNull = true");
			if (this.IsReadOnly) sb.Append(indent).AppendLine("IsReadOnly = true");
			if (this.IsInitOnly) sb.Append(indent).AppendLine("IsInitOnly = true");
			if (this.IsRequired) sb.Append(indent).AppendLine("IsRequired = true");
			if (this.IsKey) sb.Append(indent).AppendLine("IsKey = true");
			if (this.DefaultLiteral is not ("null" or "default")) sb.Append(indent).Append("DefaultValue = ").AppendLine(this.DefaultLiteral);
			var subIndent = indent is null ? "- " : ("  " + indent);
#if FULL_DEBUG
			sb.Append(indent).AppendLine("Attributes:");
			foreach (var attr in this.Attributes)
			{
				sb.Append(subIndent).AppendLine(attr);
			}
#endif
			sb.Append(indent).AppendLine("Type:");
			this.Type.Explain(sb, subIndent);
		}

	}
	
}
