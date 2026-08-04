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
	using System;
	using System.Collections.Generic;
	using System.Collections.Immutable;
	using System.Threading;
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;
	using Microsoft.CodeAnalysis.CSharp.Syntax;
	
	public partial class CrystalJsonSourceGenerator
	{

		/// <summary>Parses the symbols from the compilation, in order to extract metadata for the serialization of application types</summary>
		internal sealed class Parser
		{

			/// <summary>Member names of the generated self-mode scope: a referenced type with one of these names would collide with the generated members inside the scope</summary>
			private static readonly HashSet<string> SelfScopeReservedNames = new(StringComparer.Ordinal)
			{
				SelfScopeName, "ReadOnly", "Writable", "JsonConverter", "PropertyNames", "PropertyEncodedNames", "TypeMapper",
				"Default", "GetResolver", "Serialize", "ToJsonText", "ToJsonBytes", "ToJsonSlice", "Deserialize", "Pack", "Unpack", "ToReadOnly", "ToMutable",
			};

			private const string RequiredMemberAttributeFullName = "System.Runtime.CompilerServices.RequiredMemberAttribute";

			private const string KeyAttributeFullName = "System.ComponentModel.DataAnnotations.KeyAttribute";

			public const string JsonPropertyNameAttributeFullName = "System.Text.Json.Serialization.JsonPropertyNameAttribute";

			public const string JsonIgnoreAttributeFullName = "System.Text.Json.Serialization.JsonIgnoreAttribute";

			public const string JsonIncludeAttributeFullName = "System.Text.Json.Serialization.JsonIncludeAttribute";

			public const string DataContractAttributeFullName = "System.Runtime.Serialization.DataContractAttribute";

			public const string DataMemberAttributeFullName = "System.Runtime.Serialization.DataMemberAttribute";

			public const string IgnoreDataMemberAttributeFullName = "System.Runtime.Serialization.IgnoreDataMemberAttribute";


			public const string JsonConverterAttributeFullName = "System.Text.Json.Serialization.JsonConverterAttribute";

			public const string NewtonsoftJsonConverterAttributeFullName = "Newtonsoft.Json.JsonConverterAttribute";

			public const string NewtonsoftJsonPropertyAttributeFullName = "Newtonsoft.Json.JsonPropertyAttribute";

			public const string JsonBooleanLiteralsAttributeFullName = "SnowBank.Data.Json.JsonBooleanLiteralsAttribute";

			public const string JsonConvertWithAttributeFullName = "SnowBank.Data.Json.JsonConvertWithAttribute";

			public const string JsonPolymorphicAttributeFullName = "System.Text.Json.Serialization.JsonPolymorphicAttribute";

			public const string JsonDerivedTypeAttributeFullName = "System.Text.Json.Serialization.JsonDerivedTypeAttribute";

			/// <summary>Name of the JSON wire profile that serves the legacy DCJS wire, and the only one the default XML profile derives the DataContract wire from</summary>
			private const string WireProfileDataContractCompat = "DataContractCompat";

			/// <summary>Members of <c>XmlOutputProfile</c>, as stored in the metadata (the enum lives in SnowBank.Core, which an analyzer cannot reference)</summary>
			private const string XmlProfileDefault = "Default";

			/// <inheritdoc cref="XmlProfileDefault"/>
			private const string XmlProfileModern = "Modern";

			/// <inheritdoc cref="XmlProfileDefault"/>
			private const string XmlProfileDataContract = "DataContract";

			/// <summary>Member of <c>XmlDictionaryFormat</c> meaning "not overridden by this container"</summary>
			private const string XmlDictionaryFormatDefault = "Default";

			/// <summary>Members of <c>XmlDictionaryFormat</c> that carry the entry VALUE as text (an attribute, or the entry's own text content)</summary>
			/// <remarks>Mirrors the emitter's own constants: a shape named here is one whose value position has no room for a nested element.</remarks>
			private const string XmlDictionaryFormatKeyAttribute = "KeyAttribute";

			/// <inheritdoc cref="XmlDictionaryFormatKeyAttribute"/>
			private const string XmlDictionaryFormatKeyValueAttributes = "KeyValueAttributes";

			/// <summary>The dictionary shape of the modern profile when neither the member nor the container overrides it</summary>
			private const string XmlDictionaryFormatDirect = "Direct";

			/// <summary>Table of known symbols from this compilation</summary>
			private KnownTypeSymbols KnownSymbols { get; }

			public List<DiagnosticInfo> Diagnostics { get; } = [ ];

			private Location? ContextClassLocation { get; set; }

			/// <summary>The container's <c>[CrystalXmlOutput(DictionaryFormat = ...)]</c>, for the whole crawl of that container</summary>
			/// <remarks>Carried as parser state rather than threaded through every parse signature, exactly like <see cref="ContextClassLocation"/>: the member-level rules that need it (the attribute-shaped dictionary refusal) sit four calls below the container, and none of the intermediate steps has any business knowing about it.</remarks>
			private string? ContextXmlDictionaryFormat { get; set; }

			public Parser(KnownTypeSymbols knownSymbols)
			{
				this.KnownSymbols = knownSymbols;
			}

			public void ReportDiagnostic(DiagnosticDescriptor descriptor, Location? location, params object?[]? messageArgs)
			{
				Debug.Assert(this.ContextClassLocation != null);

				if (location is null || !ContainsLocation(this.KnownSymbols.Compilation, location))
				{
					// If location is null or is a location outside the current compilation, fall back to the location of the context class.
					location = this.ContextClassLocation;
				}

				this.Diagnostics.Add(DiagnosticInfo.Create(descriptor, location, messageArgs));

				static bool ContainsLocation(Compilation compilation, Location location)
					=> location.SourceTree != null && compilation.ContainsSyntaxTree(location.SourceTree);

			}

			public CrystalJsonContainerMetadata? ParseContainerMetadata(ClassDeclarationSyntax contextClassDeclaration, SemanticModel semanticModel, ImmutableArray<AttributeData> attributes, CancellationToken cancellationToken)
			{
				// we are inspecting the "container" type that will host all the generated serialization code
				// - the container should be a partial class
				// - it should have the [CrystalJsonConverter] attribute applied to it (which is the marker for triggering this code generator)
				// - it should have one or more [CrystalJsonSerializable(typeof(...))] attributes for each of the "root" application types to serialize
				
				var symbol = semanticModel.GetDeclaredSymbol(contextClassDeclaration, cancellationToken);
				if (symbol == null) return null;

				this.ContextClassLocation = contextClassDeclaration.GetLocation();

				Kenobi($"ParseContainerMetadata({symbol.Name}, [{attributes.Length}])");

				if (!EnsureSupportedLanguageVersion())
				{
					return null;
				}

				var converterAttribute = attributes[0];
				//TODO: extract some settings from this?

				bool caseInsensitiveNames = false;
				string? propertyNamingPolicy = null;
				string? wireProfile = null;
				AttributeData? xmlOutputAttribute = null;

				// key: fullyQualifiedName
				var includedTypes = new List<CrystalJsonTypeMetadata>();
				var mappedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

				// the application only needs to specify "root" types, and we will crawl any nested or referenced types,
				// in order to construct the full graph of custom serializers to generate

				Queue<INamedTypeSymbol> work = [];

				foreach (var typeAttribute in symbol.GetAttributes())
				{
					var ac = typeAttribute.AttributeClass;
					if (ac == null) continue;

					switch (ac.ToDisplayString())
					{
						case CrystalJsonSerializableAttributeFullName:
						{
							if (typeAttribute.ConstructorArguments.Length < 1)
							{
								continue;
							}

							if (typeAttribute.ConstructorArguments[0].Value is not INamedTypeSymbol type)
							{
								continue;
							}

							if (!mappedTypes.Add(type))
							{
								//TODO: report a diagnostic about a duplicated type?
								continue;
							}

							if (IsNativelySerializedType(TypeMetadata.Create(type), out var nativeKind))
							{ // CrystalJson serializes collections, dictionaries and scalars natively, root included: there is nothing for a generated converter to add, and enumerating such a type as a POCO produces code that does not compile
								ReportNativelySerializedEnrolment(typeAttribute, symbol, type, nativeKind);
								continue;
							}

							work.Enqueue(type);
							break;
						}
						case CrystalJsonConverterAttributeFullName:
						{
							// we want to extract the generation defaults (like property naming policy, etc...)
							// => they can be specified by the CrystalJsonSerializerDefaults (General or Web), or by overriding specific named properties

							if (typeAttribute.ConstructorArguments.Length > 0)
							{
								var defaults = (int) typeAttribute.ConstructorArguments[0].Value!;
								Kenobi($"Found defaults for container {symbol.Name}: {typeAttribute.ConstructorArguments[0].Value} => {defaults}");

								// 0 == General
								// 1 == Web
								// 2 == DataContractCompat
								switch (defaults)
								{
									case 1: // CrystalJsonSerializerDefaults.Web
									{
										caseInsensitiveNames = true;
										propertyNamingPolicy = "camel";
										break;
									}
									case 2: // CrystalJsonSerializerDefaults.DataContractCompat
									{ // the profile governs value formats only: the DCJS wire uses the declared member names
										wireProfile = WireProfileDataContractCompat;
										break;
									}
								}
							}

							foreach (var kv in typeAttribute.NamedArguments)
							{
								if (kv.Key == "PropertyNameCaseInsensitive")
								{
									caseInsensitiveNames = (bool) kv.Value.Value!;
								}
								else if (kv.Key == "PropertyNamingPolicy")
								{
									switch (kv.Value.Value)
									{
										case 0: // CrystalJsonKnownNamingPolicy.Unspecified
										{
											propertyNamingPolicy = null;
											break;
										}
										case 1: // CrystalJsonKnownNamingPolicy.CamelCase
										{
											propertyNamingPolicy = "camel";
											break;
										}
										// others?
									}
								}
							}

							Kenobi($"Using defaults for container {symbol.Name}: caseInsensitive={caseInsensitiveNames}; namingPolicy={propertyNamingPolicy}");

							break;
						}
						case KnownTypeSymbols.CrystalXmlOutputAttributeFullName:
						{ // XML output is opt-in; the wire it resolves to depends on the JSON profile, which is only known once the whole attribute list has been read
							xmlOutputAttribute = typeAttribute;
							break;
						}
					}
				}

				if (wireProfile != null && (caseInsensitiveNames || propertyNamingPolicy != null))
				{ // the DCJS wire has no naming policy: a naming option next to the profile is a contradiction, refused at build time
					ReportDiagnostic(
						new(
							"CJSON0013",
							"A wire profile cannot be combined with a naming option",
							"The container '{0}' bakes the {1} profile, whose wire uses the declared member names; combining it with a camelCase or case-insensitive naming option is refused. Remove the naming option, or serialize the modern wire through a separate container (the dual-container pattern).",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						symbol.ToDisplayString(), wireProfile);
					return null;
				}

				var (xmlProfile, xmlDictionaryFormat) = ResolveXmlOutput(symbol, xmlOutputAttribute, wireProfile, propertyNamingPolicy);
				this.ContextXmlDictionaryFormat = xmlDictionaryFormat;

				Kenobi($"Found {work.Count} root types to include");

				CrawlIncludedTypes(work, mappedTypes, includedTypes, propertyNamingPolicy, xmlProfile);

				if (xmlProfile is not null)
				{ // a collision with the discriminator can only be seen once every type of the hierarchy has resolved its members
					ReportDiscriminatorXmlNameCollisions(includedTypes, mappedTypes, xmlProfile);
				}

				if (includedTypes.Count == 0)
				{
					ReportDiagnostic(
						new(
							"CJSON0002",
							"At least one type must be included",
							"The container type {0} must specify at least one type to include, using the [CrystalJsonSerializable] attribute",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Warning,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						[ symbol.ToDisplayString() ]
					);
				}

				Kenobi($"Found {includedTypes.Count} total types to generate");

				var containerName = symbol.Name;

				MaybeReportProxySurfaceNotGenerated(symbol);

				this.ContextClassLocation = null;

				return new()
				{
					Name = containerName,
					Type = TypeMetadata.Create(symbol),
					IncludedTypes = includedTypes.ToImmutableEquatableArray(),
					PropertyNameCaseInsensitive = caseInsensitiveNames,
					PropertyNamingPolicy = propertyNamingPolicy,
					SupportsUnsafeAccessors = this.KnownSymbols.HasUnsafeAccessor,
					SupportsJsonProxies = this.KnownSymbols.SupportsJsonProxies,
					SupportsDynamicallyAccessedMembers = this.KnownSymbols.HasDynamicallyAccessedMembers,
					WireProfile = wireProfile,
					XmlProfile = xmlProfile,
					XmlDictionaryFormat = xmlDictionaryFormat,
				};
			}

			/// <summary>Shapes that CrystalJson already serializes natively, and for which no converter is ever source-generated</summary>
			private enum NativeShape
			{
				/// <summary>A scalar or built-in type (<c>string</c>, <c>int</c>, <c>Guid</c>, <c>DateTime</c>, ...), or a <see cref="Nullable{T}"/> of one</summary>
				Scalar,

				/// <summary>A sequence (array, <c>List&lt;T&gt;</c>, <c>IEnumerable&lt;T&gt;</c>, ...)</summary>
				Collection,

				/// <summary>An associative container (<c>Dictionary&lt;TKey, TValue&gt;</c>, <c>IDictionary&lt;TKey, TValue&gt;</c>, ...)</summary>
				Dictionary,
			}

			/// <summary>Tests whether an ENROLLED type is one CrystalJson serializes natively, root included, and which therefore gets no generated converter</summary>
			/// <param name="metadata">Metadata of the type named by a <c>[CrystalJsonSerializable]</c> attribute</param>
			/// <param name="shape">Receives the shape that made the type native, when the method returns <see langword="true"/></param>
			/// <remarks>
			/// <para>The generator emits converters for POCO types ONLY. Enumerating a collection, a dictionary or a scalar as if it were a POCO walks its indexer as a member, and the emitted holder ends up declaring a nameless indexer that does not compile.</para>
			/// <para>This governs the ENROLMENT decision only. Types reached transitively as MEMBERS never take this route: <see cref="MaybeAddLinkedType"/> already descends through a collection or dictionary member to its element / key / value types and never enqueues the container type itself, so member paths are unaffected.</para>
			/// <para>Enums are deliberately NOT part of this set: they are user-declared types whose generated label tables are the reflection-free lookup the runtime path would otherwise pay for.</para>
			/// </remarks>
			private static bool IsNativelySerializedType(TypeMetadata metadata, out NativeShape shape)
			{
				// scalars first: 'string' is both a built-in and an IEnumerable<char>, and it is the former that names it best
				if (metadata.IsPrimitive || (metadata.NullableOfType is { IsPrimitive: true }))
				{
					shape = NativeShape.Scalar;
					return true;
				}

				if (metadata.ValueType is not null)
				{
					shape = NativeShape.Dictionary;
					return true;
				}

				if (metadata.ElementType is not null)
				{
					shape = NativeShape.Collection;
					return true;
				}

				shape = default;
				return false;
			}

			/// <summary>Reports <c>CJSON0019</c> on a <c>[CrystalJsonSerializable]</c> attribute that enrolls a natively serialized type</summary>
			/// <remarks>A WARNING rather than an error: the enrollment is harmless but inert, and the application still serializes the type correctly through the native path. Silence is the wrong answer though, since the author asked for a converter that is not there (the same reasoning that makes <c>CJSON0007</c> a warning).</remarks>
			private void ReportNativelySerializedEnrolment(AttributeData attribute, INamedTypeSymbol container, INamedTypeSymbol type, NativeShape shape)
			{
				var (what, remedy) = shape switch
				{
					NativeShape.Dictionary => ("dictionaries", "Enroll the key and value types instead of the dictionary type."),
					NativeShape.Collection => ("collections", "Enroll the element type instead of the collection type."),
					_ => ("scalars", "Remove the enrollment."),
				};

				ReportDiagnostic(
					new(
						"CJSON0019",
						"Enrolled type is serialized natively",
						"The type '{0}' is enrolled in container '{1}', but it is not needed: CrystalJson serializes {2} natively, root included. No converter is source-generated for it. {3}",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Warning,
						isEnabledByDefault: true
					),
					attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? this.ContextClassLocation,
					[ type.ToDisplayString(), container.ToDisplayString(), what, remedy ]
				);
			}

			/// <summary>Resolves the XML wire of a container from its <c>[CrystalXmlOutput]</c> attribute, reporting <c>CXML0001</c> when the resolved wire cannot honor the container's naming policy</summary>
			/// <param name="symbol">Container being parsed (used to name it in the diagnostic)</param>
			/// <param name="xmlOutputAttribute">The container's <c>[CrystalXmlOutput]</c> attribute, or <see langword="null"/> when it has none (XML output is opt-in)</param>
			/// <param name="wireProfile">The container's resolved JSON wire profile, which the default XML profile derives from</param>
			/// <param name="propertyNamingPolicy">The container's <c>PropertyNamingPolicy</c> option, or <see langword="null"/> for the declared names</param>
			/// <returns>The resolved profile name and dictionary format name, or <c>(null, null)</c> when the container produces no XML</returns>
			/// <remarks><c>PropertyNameCaseInsensitive</c> is deliberately not an input: it is a deserialization option, and this overlay never reads XML.</remarks>
			private (string? Profile, string? DictionaryFormat) ResolveXmlOutput(INamedTypeSymbol symbol, AttributeData? xmlOutputAttribute, string? wireProfile, string? propertyNamingPolicy)
			{
				if (xmlOutputAttribute is null)
				{ // no opt-in: the container is JSON-only, and nothing else about it changes
					return (null, null);
				}

				// the attribute only exposes named properties (its single constructor is parameterless)
				string? explicitProfile = null;
				string? dictionaryFormat = null;
				foreach (var kv in xmlOutputAttribute.NamedArguments)
				{
					if (kv.Key == "Profile")
					{
						explicitProfile = GetEnumMemberName(kv.Value);
					}
					else if (kv.Key == "DictionaryFormat")
					{
						dictionaryFormat = GetEnumMemberName(kv.Value);
					}
				}

				// an explicit profile wins; 'Default' (or unspecified) derives the XML wire from the JSON one
				string profile =
					explicitProfile is null or XmlProfileDefault
						? (wireProfile == WireProfileDataContractCompat ? XmlProfileDataContract : XmlProfileModern)
						: explicitProfile;

				if (profile == XmlProfileDataContract && propertyNamingPolicy != null)
				{ // the DataContract wire names its elements after the data contract: a NAMING POLICY next to it cannot be honored, and honoring neither silently is worse
					//note: this is only reachable through an EXPLICIT profile: the derived one requires the DCJS JSON profile, which already refuses naming options (CJSON0013)
					//note: PropertyNameCaseInsensitive is deliberately NOT a trigger. It decides how an INCOMING name is matched
					// when READING JSON, and CrystalXml is write-only: it names nothing on this wire, so there is no element name
					// for the data contract to disagree with. Refusing it here only cost the author a container they could not write.
					ReportDiagnostic(
						new(
							"CXML0001",
							"The DataContract XML wire cannot be combined with a naming policy",
							"The container '{0}' produces the DataContract XML wire, whose element names come from the data contract; combining it with a camelCase (or other) naming policy is refused. Remove the naming policy, use the Modern XML wire (which follows the naming policy), or produce the DataContract wire from a separate container (the dual-container pattern).",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						symbol.ToDisplayString());

					// the XML request is dropped, and the container keeps generating its JSON: one error to read, plus the
					// missing-member errors at whatever XML call sites the application already had (the degraded container
					// emits no XML member at all), which is still smaller than abandoning the container entirely
					return (null, null);
				}

				if (profile == XmlProfileDataContract && dictionaryFormat is not (null or XmlDictionaryFormatDefault))
				{ // an explicitly spelled 'Default' asks to INHERIT, which every profile honors: only a real choice is inert here
					ReportInertXmlSetting(
						this.ContextClassLocation,
						symbol.ToDisplayString(),
						"DictionaryFormat = " + dictionaryFormat,
						"the DataContract XML wire has exactly one dictionary shape (the KeyValueOfKV entries the reference serializer writes), so there is nothing for this option to select between. Its member-level twin is refused outright on this wire (CXML0004)");
				}

				Kenobi($"Resolved XML output for container {symbol.Name}: profile={profile}; dictionaryFormat={dictionaryFormat ?? XmlDictionaryFormatDefault}");

				return (profile, dictionaryFormat ?? XmlDictionaryFormatDefault);
			}

			/// <summary>Returns the name of the enum member an attribute argument was set to, or <see langword="null"/> when the argument is not a known enum member</summary>
			/// <remarks>Reads the NAME rather than the ordinal: the metadata layer stores these names verbatim, so reordering the runtime enum cannot silently change what the generator resolves.</remarks>
			private static string? GetEnumMemberName(TypedConstant value)
			{
				if (value.Value is null || value.Type is not INamedTypeSymbol { TypeKind: TypeKind.Enum } enumType)
				{
					return null;
				}

				foreach (var member in enumType.GetMembers())
				{
					if (member is IFieldSymbol { HasConstantValue: true } field && Equals(field.ConstantValue, value.Value))
					{
						return field.Name;
					}
				}

				return null;
			}

			/// <summary>Returns the type's <c>[CrystalXmlOutput]</c> attribute, or <see langword="null"/> when it has none</summary>
			private static AttributeData? FindXmlOutputAttribute(INamedTypeSymbol symbol)
			{
				foreach (var attribute in symbol.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() == KnownTypeSymbols.CrystalXmlOutputAttributeFullName)
					{
						return attribute;
					}
				}
				return null;
			}

			/// <summary>Reports <c>CXML0002</c> on a type that asks for XML output but hosts no generated serializer</summary>
			/// <remarks>Neither generation pipeline ever reaches such a type, so without this diagnostic the attribute would be silently inert.</remarks>
			public void ReportOrphanXmlOutput(TypeDeclarationSyntax typeDeclaration, INamedTypeSymbol symbol)
			{
				this.ContextClassLocation = typeDeclaration.GetLocation();

				ReportDiagnostic(
					new(
						"CXML0002",
						"XML output requires a serializer container",
						"The type '{0}' is decorated with [CrystalXmlOutput], but hosts no generated serializer: no XML output is produced for it. Add [CrystalJsonConverter] (with one [CrystalJsonSerializable] per type to serialize), or decorate the type with a self-serializable attribute.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					this.ContextClassLocation,
					symbol.ToDisplayString());

				this.ContextClassLocation = null;
			}

			/// <summary>Parses a self-serializable type: a partial type decorated with an attribute that carries the <c>[CrystalJsonSelfSerializable]</c> meta-marker</summary>
			/// <remarks>The type acts as its own container: the generated code is nested under the entity itself (ex: <c>Widget.ReadOnly</c>).</remarks>
			public CrystalJsonContainerMetadata? ParseSelfSerializableMetadata(TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel, CancellationToken cancellationToken)
			{
				var symbol = semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken);
				if (symbol == null) return null;

				this.ContextClassLocation = typeDeclaration.GetLocation();

				Kenobi($"ParseSelfSerializableMetadata({symbol.Name})");

				if (!EnsureSupportedLanguageVersion())
				{
					return null;
				}

				if (!typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword))
				{
					ReportDiagnostic(
						new(
							"CJSON0004",
							"Self-serializable type must be partial",
							"The type {0} is marked for JSON self-serialization, and must be declared 'partial' so that the generated code can be added to it.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						[ symbol.ToDisplayString() ]
					);
					this.ContextClassLocation = null;
					return null;
				}

				if (symbol.IsGenericType || symbol.ContainingType is not null)
				{
					ReportDiagnostic(
						new(
							"CJSON0005",
							"Self-serializable type must be a non-generic top-level type",
							"The type {0} is marked for JSON self-serialization, but generic or nested types are not supported: no code will be generated for it.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Warning,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						[ symbol.ToDisplayString() ]
					);
					this.ContextClassLocation = null;
					return null;
				}

				if (symbol.GetMembers(SelfScopeName).Length > 0)
				{
					// the generated code would collide with the user's member, with errors pointing inside the generated file
					ReportDiagnostic(
						new(
							"CJSON0006",
							"Self-serializable type must not declare a member named '" + SelfScopeName + "'",
							"The type {0} is marked for JSON self-serialization, but already declares a member named '" + SelfScopeName + "', which is reserved for the generated container scope. Rename the member (a [JsonProperty] attribute can keep the serialized name), or use a [CrystalJsonConverter] container class instead.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						[ symbol.ToDisplayString() ]
					);
					this.ContextClassLocation = null;
					return null;
				}

				// a self-serializable type hosts its own generated code, so it can opt into XML output the same way a
				// container does; it declares no JSON wire profile, so an unspecified profile derives the Modern wire
				var (xmlProfile, xmlDictionaryFormat) = ResolveXmlOutput(symbol, FindXmlOutputAttribute(symbol), wireProfile: null, propertyNamingPolicy: null);
				this.ContextXmlDictionaryFormat = xmlDictionaryFormat;

				var includedTypes = new List<CrystalJsonTypeMetadata>();
				var mappedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { symbol };

				Queue<INamedTypeSymbol> work = [];
				work.Enqueue(symbol);

				CrawlIncludedTypes(work, mappedTypes, includedTypes, propertyNamingPolicy: null, xmlProfile);

				if (xmlProfile is not null)
				{ // a collision with the discriminator can only be seen once every type of the hierarchy has resolved its members
					ReportDiscriminatorXmlNameCollisions(includedTypes, mappedTypes, xmlProfile);
				}

				// a referenced type named like a member of the generated scope would collide inside it: exclude it
				// from generation (the emitted code falls back to runtime serialization for it) and warn
				for (int i = includedTypes.Count - 1; i >= 0; i--)
				{
					var includedType = includedTypes[i];
					// the self type itself never gets a holder (its members ARE the scope), so only referenced types can collide
					if (includedType.Name != symbol.Name && SelfScopeReservedNames.Contains(includedType.Name))
					{
						ReportDiagnostic(
							new(
								"CJSON0007",
								"Referenced type is named like a reserved member of the generated scope",
								"The type {0}, referenced by self-serializable type {1}, is named like a reserved member of the generated '" + SelfScopeName + "' scope: no converter is source-generated for it (runtime serialization is used instead).",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Warning,
								isEnabledByDefault: true
							),
							this.ContextClassLocation,
							[ includedType.Type.FullName.TrimEnd('?'), symbol.ToDisplayString() ]
						);
						includedTypes.RemoveAt(i);
					}
				}

				Kenobi($"Found {includedTypes.Count} total types to generate for self type {symbol.Name}");

				MaybeReportProxySurfaceNotGenerated(symbol);

				this.ContextClassLocation = null;

				return new()
				{
					Name = symbol.Name,
					Type = TypeMetadata.Create(symbol),
					IncludedTypes = includedTypes.ToImmutableEquatableArray(),
					PropertyNameCaseInsensitive = false,
					PropertyNamingPolicy = null,
					IsSelfContained = true,
					SupportsUnsafeAccessors = this.KnownSymbols.HasUnsafeAccessor,
					SupportsJsonProxies = this.KnownSymbols.SupportsJsonProxies,
					SupportsDynamicallyAccessedMembers = this.KnownSymbols.HasDynamicallyAccessedMembers,
					XmlProfile = xmlProfile,
					XmlDictionaryFormat = xmlDictionaryFormat,
				};
			}

			/// <summary>Ensures that the compilation's language version supports source generation, reporting a diagnostic if not</summary>
			private bool EnsureSupportedLanguageVersion()
			{
				var langVersion = (this.KnownSymbols.Compilation as CSharpCompilation)?.LanguageVersion;
				if (langVersion is null or < LanguageVersion.CSharp9)
				{
					// Unsupported lang version should be the first (and only) diagnostic emitted by the generator.
					ReportDiagnostic(
						new(
							"SYSLIB1221", //note: we use the same ID as System.Text.Json, since this is the same error
							"C# language version not supported by the source generator.",
							"The JSON source generator is not available in C# {0}. Please use language version {1} or greater.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						this.ContextClassLocation,
						langVersion?.ToDisplayString(), LanguageVersion.CSharp9.ToDisplayString());
					return false;
				}
				return true;
			}

			/// <summary>Drains the work queue, parsing each type and crawling any nested, derived or referenced type into the included list</summary>
			/// <param name="xmlProfile">The container's RESOLVED XML wire profile, or <see langword="null"/> when it produces no XML (which makes the whole member-level XML vocabulary inert, diagnostics included)</param>
			private void CrawlIncludedTypes(Queue<INamedTypeSymbol> work, HashSet<INamedTypeSymbol> mappedTypes, List<CrystalJsonTypeMetadata> includedTypes, string? propertyNamingPolicy, string? xmlProfile)
			{
				while(work.Count > 0)
				{
					var type = work.Dequeue();

					Kenobi($"Inspect type {type}");
					try
					{
						var typeDef = ParseTypeMetadata(type, mappedTypes, work, propertyNamingPolicy, xmlProfile);
						if (typeDef is not null)
						{
							includedTypes.Add(typeDef);

							foreach (var (derivedSymbol, _, _) in typeDef.DerivedTypes)
							{
								if (mappedTypes.Add(derivedSymbol))
								{
									work.Enqueue(derivedSymbol);
								}
							}

						}

						// are there any nested types?
						foreach (var memberType in type.GetTypeMembers())
						{
							Kenobi($"Inspected nested type {memberType}");
							if (memberType.DeclaredAccessibility is Accessibility.Public)
							{
								if (mappedTypes.Add(memberType))
								{
									work.Enqueue(memberType);
								}
							}
						}
					}
					catch (Exception ex)
					{
						Kenobi($"CRASH for {type}: {ex.ToString()}");
						ReportDiagnostic(
							new(
								"CJSON0001",
								"Failed to parse JSON metadata",
								"Failed to extract the JSON serialization metadata for type {0}: {1}",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							this.ContextClassLocation,
							[ type.ToDisplayString(), ex.ToString() ]
						);
					}
				}
			}

			/// <param name="xmlProfile"><inheritdoc cref="CrawlIncludedTypes" path="/param[@name='xmlProfile']"/></param>
			public CrystalJsonTypeMetadata? ParseTypeMetadata(INamedTypeSymbol type, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work, string? namingPolicy, string? xmlProfile)
			{
				// we have to extract all the properties that will be required later during the code generation phase

				bool isPolymorphic = false;
				string? typeDiscriminatorPropertyName = null;
				List<(INamedTypeSymbol, TypeMetadata, object?)>? derivedTypes = null;

				var members = new List<CrystalJsonMemberMetadata>();
				// kept beside the metadata, which carries no symbol (it must stay equatable for the incremental pipeline): the
				// type-level XML rules below report ON the member, and a diagnostic pointing at the container instead of at the
				// offending property is one the author has to go hunting for
				var memberSymbols = new Dictionary<string, ISymbol>(StringComparer.Ordinal);
				foreach (var attribute in type.GetAttributes())
				{
					var attributeType = attribute.AttributeClass;
					if (attributeType is null) continue;

					switch (attributeType.ToDisplayString())
					{
						case JsonDerivedTypeAttributeFullName:
						{
							if (attribute.ConstructorArguments.Length > 0)
							{
								// first is the derived type
								var derivedType = (INamedTypeSymbol) attribute.ConstructorArguments[0].Value!;

								object? typeDiscriminator = null;
								if (attribute.ConstructorArguments.Length > 1)
								{ // either a string or a number
									typeDiscriminator = attribute.ConstructorArguments[1].Value!;
								}

								var derivedTypeMetadata = TypeMetadata.Create(derivedType);

								(derivedTypes ??= [ ]).Add((derivedType, derivedTypeMetadata, typeDiscriminator));
								isPolymorphic = true;
							}
							break;
						}
						case JsonPolymorphicAttributeFullName:
						{
							// it is either the first ctor arg, or a named argument
							foreach (var arg in attribute.NamedArguments)
							{
								if (arg.Key == "TypeDiscriminatorPropertyName" && arg.Value.Value is string s)
								{
									typeDiscriminatorPropertyName = s;
								}
							}
							break;
						}
					}
				}

				// [DataContract] switches membership from "every public member unless excluded" to "only what [DataMember] opts in",
				// and makes accessibility stop filtering. That is a TYPE-level fact, so it has to reach the per-member step.
				var dataContract = GetDataContractInfo(type);
				bool hasDataContract = dataContract.Present;

				var callbacks = ParseSerializationCallbacks(type);

				// if this is a derived type, we need to enumerate the symbols starting from the top (interface or base class)
				// we also want to have "id" as the first member
				int indexOfId = -1;
				// the hierarchy comes back topmost-base first, so the index of the level IS the inheritance depth the
				// DataContract wire orders by (see CrystalJsonMemberMetadata.InheritanceLevel)
				int inheritanceLevel = -1;
				foreach (var current in GetTypeHierarchy(type))
				{
					++inheritanceLevel;
					foreach (var member in current.GetMembers())
					{
						if (member.Kind is (SymbolKind.Property or SymbolKind.Field or SymbolKind.Method))
						{
							Kenobi($"Inspecting member {member.Name}...");
							var (memberDef, memberType) = ParseMemberMetadata(member, mappedTypes, work, namingPolicy, hasDataContract, xmlProfile);
							if (memberDef is not null)
							{
								memberDef = memberDef with { InheritanceLevel = inheritanceLevel };
								Kenobi($"Inspected member {member.Name} with type {memberDef.Type.FullName}, N={memberDef.Type.NullableOfType?.FullName}, E={memberDef.Type.ElementType?.FullName}, K={memberDef.Type.KeyType?.FullName}, V={memberDef.Type.ValueType?.FullName}");
								if (member.Name == "Id")
								{
									indexOfId = members.Count;
								}
								members.Add(memberDef);
								// a member shadowed by a 'new' one appears twice: the most derived wins, which is the one that lands in the document
								memberSymbols[memberDef.MemberName] = member;
							}
							else
							{
								Kenobi($"Skipped member {member.Name}");
							}
						}
					}
				}

				if (indexOfId > 0)
				{ // move "Id" to the first position
					var memberId = members[indexOfId];
					members.RemoveAt(indexOfId);
					members.Insert(0, memberId);
				}

				if (typeDiscriminatorPropertyName == null && isPolymorphic)
				{
					typeDiscriminatorPropertyName = "$type";
				}

				ReportPrePopulateCallbackConflicts(type, callbacks.OnDeserializing, members);

				if (xmlProfile is not null)
				{ // a name collision can only be seen once every member of the type has resolved its own XML name
					ReportDuplicateXmlNames(type, members, memberSymbols, xmlProfile);
					ReportInvalidRootXmlName(type, dataContract.Name, xmlProfile);
					// same reason it runs here: what makes a setting inert is the member's RESOLVED projection, not the attribute as written
					ReportInertXmlMemberSettings(members, memberSymbols);
				}

				return new()
				{
					Type = TypeMetadata.Create(type),
					Members = members.ToImmutableEquatableArray(),
					IsPolymorphicRoot = isPolymorphic,
					HasDataContract = hasDataContract,
					DataContractName = dataContract.Name,
					DataContractNamespace = dataContract.Namespace,
					OnSerializing = callbacks.OnSerializing,
					OnSerialized = callbacks.OnSerialized,
					OnDeserializing = callbacks.OnDeserializing,
					OnDeserialized = callbacks.OnDeserialized,
					TypeDiscriminatorPropertyName = typeDiscriminatorPropertyName,
					DerivedTypes = derivedTypes.ToImmutableEquatableArray(),
				};
			}

			private void MaybeAddLinkedType(TypeMetadata metadata, INamedTypeSymbol type, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work)
			{
				if (metadata.IsPrimitive)
				{
					return;
				}

				Kenobi($"Should we include `{type}` ?");

				if (metadata.NullableOfType is not null)
				{ // unwrap nullables, we want to inspect the concrete type
					if (!metadata.NullableOfType.IsPrimitive)
					{
						Kenobi($"--> Nullable<{metadata.NullableOfType.FullName}>");
						MaybeAddLinkedType(metadata.NullableOfType, (INamedTypeSymbol) type.TypeArguments[0], mappedTypes, work);
					}
					return;
				}

				// is this a dictionary, or a set?
				if (metadata.KeyType is not null)
				{
					if (!metadata.KeyType.IsPrimitive)
					{
						var target = this.KnownSymbols.Compilation.GetBestTypeByMetadataName(metadata.KeyType.FullName);
						Kenobi($"--> KeyType<{metadata.KeyType.FullName}>: {(target?.ToString() ?? "<no target>")}");
						if (target is not null)
						{
							MaybeAddLinkedType(metadata.KeyType, target, mappedTypes, work);
						}
					}
					if (metadata.ValueType is not null && !metadata.ValueType.IsPrimitive)
					{
						var target = this.KnownSymbols.Compilation.GetBestTypeByMetadataName(metadata.ValueType.FullName);
						Kenobi($"--> ValueType<{metadata.ValueType.FullName}>: {(target?.ToString() ?? "<no target>")}");
						if (target is not null)
						{
							MaybeAddLinkedType(metadata.ValueType, target, mappedTypes, work);
						}
						else if (metadata.ValueType.IsEnumerable(out var elemType))
						{ // try again if the type is an enumerable (array, list, ...)
							target = this.KnownSymbols.Compilation.GetBestTypeByMetadataName(elemType.FullName);
							Kenobi($"--> ValueType<Enumerable of {elemType.FullName}>: {(target?.ToString() ?? "<no target>")}");
							if (target is not null)
							{
								MaybeAddLinkedType(elemType, target, mappedTypes, work);
							}
						}
					}
					return;
				}

				// is this a collection of something that we could be interested in?
				if (metadata.ElementType is not null)
				{
					if (!metadata.ElementType.IsPrimitive)
					{
						var target = this.KnownSymbols.Compilation.GetBestTypeByMetadataName(metadata.ElementType.FullName);
						Kenobi($"--> ElementType<{metadata.ElementType.FullName}>: {target}");
						if (target is not null)
						{
							MaybeAddLinkedType(metadata.ElementType, target, mappedTypes, work);
						}
					}
					return;
				}

				if (!IsTypeOfInterest(metadata, type))
				{
					Kenobi("---> ignore " + type);
					return;
				}

				// add this type to the list!
				if (mappedTypes.Add(type))
				{
					Kenobi("### Include " + type);
					work.Enqueue(type);
				}
			}

			public static bool IsTypeOfInterest(TypeMetadata metadata, INamedTypeSymbol type)
			{
				if (metadata.IsPrimitive) return false;
				if (metadata.IsEnum()) return false;
				if (metadata.JsonType() is not JsonPrimitiveType.None) return false;
				if (metadata.NameSpace == "System" || metadata.NameSpace.StartsWith("System.")) return false;
				if (metadata.NameSpace == "Microsoft" || metadata.NameSpace.StartsWith("Microsoft.")) return false;
				if (metadata.NameSpace == "NodaTime" || metadata.NameSpace.StartsWith("NodaTime.")) return false;
				if (metadata.NameSpace == KnownTypeSymbols.CrystalJsonNamespace) return false;
				return true;
			}

			internal static string FormatName(string name, string? policy)
			{
				switch (policy)
				{
					case null: return name;
					case "camel": return char.IsLower(name[0]) ? name : (char.ToLowerInvariant(name[0]) + name.Substring(1));
					default:
					{
						Kenobi("### Invalid JSON Naming Policy: " + policy);
						return name;
					}
				}
			}

			/// <summary>Tests whether generated code (living in the same assembly, outside the type) can access a member with this accessibility directly</summary>
			/// <remarks>Private, protected and private-protected members need an accessor thunk; internal and protected-internal members are reachable directly.</remarks>
			private static bool IsReachableFromGeneratedCode(Accessibility accessibility)
				=> accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

			private static bool HasJsonIncludeAttribute(ISymbol member)
			{
				foreach (var attribute in member.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() == JsonIncludeAttributeFullName)
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>Collects the type's serialization lifecycle callbacks, reporting <c>CJSON0015</c> on any signature generated code cannot invoke</summary>
			/// <remarks>
			/// <para>Reported at the CALLSITE, because that is where the fix is applied. The reflection path refuses the same shapes when it builds the type's contract, with the same message for the legacy one.</para>
			/// <para>Refusing at compile time is what lets generated code invoke the callback directly, with no runtime signature test.</para>
			/// </remarks>
			private (CrystalJsonCallbackMetadata? OnSerializing, CrystalJsonCallbackMetadata? OnSerialized, CrystalJsonCallbackMetadata? OnDeserializing, CrystalJsonCallbackMetadata? OnDeserialized) ParseSerializationCallbacks(INamedTypeSymbol type)
			{
				CrystalJsonCallbackMetadata? onSerializing = null, onSerialized = null, onDeserializing = null, onDeserialized = null;

				for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
				{
					foreach (var member in current.GetMembers())
					{
						if (member is not IMethodSymbol method) continue;

						string? kind = null;
						foreach (var attribute in method.GetAttributes())
						{
							switch (attribute.AttributeClass?.Name)
							{
								case "OnSerializingAttribute": kind = "OnSerializing"; break;
								case "OnSerializedAttribute": kind = "OnSerialized"; break;
								case "OnDeserializingAttribute": kind = "OnDeserializing"; break;
								case "OnDeserializedAttribute": kind = "OnDeserialized"; break;
							}
							if (kind is not null) break;
						}
						if (kind is null) continue;

						bool isDeserialize = kind is "OnDeserializing" or "OnDeserialized";
						if (!TryClassifyCallbackArgument(method, isDeserialize, out var argument))
						{
							// the legacy shape keeps its own message: it is the migration recipe, and people grep for it
							bool isLegacyShape = method.Parameters.Length == 1 && method.Parameters[0].Type.Name == "StreamingContext";
							ReportDiagnostic(
								new(
									"CJSON0015",
									"A serialization callback has a signature this serializer cannot invoke",
									isLegacyShape ? CallbackStreamingContextNotSupportedMessage : CallbackSignatureNotSupportedMessage,
									"SnowBank.Serialization.Json.CodeGen",
									DiagnosticSeverity.Error,
									isEnabledByDefault: true
								),
								method.Locations.Length > 0 ? method.Locations[0] : null,
								method.Name);
							continue;
						}

						var entry = new CrystalJsonCallbackMetadata
						{
							MethodName = method.Name,
							IsNonPublic = !IsReachableFromGeneratedCode(method.DeclaredAccessibility),
							Argument = argument,
						};
						switch (kind)
						{
							case "OnSerializing": onSerializing ??= entry; break;
							case "OnSerialized": onSerialized ??= entry; break;
							case "OnDeserializing": onDeserializing ??= entry; break;
							case "OnDeserialized": onDeserialized ??= entry; break;
						}
					}
				}

				return (onSerializing, onSerialized, onDeserializing, onDeserialized);
			}

			/// <summary>Reports <c>CJSON0016</c> when a pre-populate callback cannot coexist with how a member must be assigned</summary>
			/// <remarks>
			/// <para><c>[OnDeserializing]</c> must observe a constructed but UNPOPULATED instance, so generated code constructs first and assigns members as statements. An <c>init</c>-only or <c>required</c> member cannot be assigned that way, and without this diagnostic the consumer would get a compiler error inside generated source they never wrote.</para>
			/// <para>Fires only when both are genuinely present on the same type: neither construct is a problem on its own.</para>
			/// </remarks>
			private void ReportPrePopulateCallbackConflicts(INamedTypeSymbol type, CrystalJsonCallbackMetadata? onDeserializing, List<CrystalJsonMemberMetadata> members)
			{
				if (onDeserializing is null) return;

				foreach (var member in members)
				{
					// the two remedies differ, so the messages do
					string? message = member switch
					{
						{ IsRequired: true } => "Remove the 'required' modifier from member '{1}' of type '{0}', or remove [OnDeserializing] from that type. A pre-populate callback must observe an unpopulated instance, so members are assigned after construction, and a 'required' member can only be set in an object initializer.",
						{ IsInitOnly: true } => "Change the 'init' accessor of member '{1}' of type '{0}' to 'set', or remove [OnDeserializing] from that type. A pre-populate callback must observe an unpopulated instance, so members are assigned after construction, which an init-only member does not allow.",
						_ => null,
					};
					if (message is null) continue;

					ReportDiagnostic(
						new(
							"CJSON0016",
							"A pre-populate callback cannot coexist with a required or init-only member",
							message,
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						type.Locations.Length > 0 ? type.Locations[0] : null,
						type.ToDisplayString(), member.MemberName);
				}
			}

			/// <summary>Reports <c>CJSON0017</c> when a <c>[JsonBooleanLiterals]</c> argument has a type with no JSON wire form</summary>
			/// <remarks>The attribute takes <c>object</c> parameters so that <see langword="null"/> can mean "do not emit", which moves type checking off the compiler. This restores it at compile time, with the same message the runtime guard throws.</remarks>
			private bool ValidateBooleanLiteral(ISymbol member, TypedConstant argument, string parameterName)
			{
				if (argument.IsNull) return true;

				switch (argument.Type?.SpecialType)
				{
					case SpecialType.System_String:
					case SpecialType.System_Boolean:
					case SpecialType.System_SByte:
					case SpecialType.System_Byte:
					case SpecialType.System_Int16:
					case SpecialType.System_UInt16:
					case SpecialType.System_Int32:
					case SpecialType.System_UInt32:
					case SpecialType.System_Int64:
					case SpecialType.System_UInt64:
					case SpecialType.System_Single:
					case SpecialType.System_Double:
					{
						return true;
					}
				}

				ReportDiagnostic(
					new(
						"CJSON0017",
						"A [JsonBooleanLiterals] argument has a type with no JSON wire form",
						BooleanLiteralTypeNotSupportedMessage,
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					parameterName, argument.Type?.Name ?? "?");
				return false;
			}

			/// <summary>Classifies a callback's parameter list, rejecting anything the runtime path would also reject</summary>
			private static bool TryClassifyCallbackArgument(IMethodSymbol method, bool isDeserialize, out CrystalJsonCallbackArgument argument)
			{
				argument = CrystalJsonCallbackArgument.None;

				if (!method.ReturnsVoid || method.IsStatic || method.Parameters.Length > 1)
				{
					return false;
				}
				if (method.Parameters.Length == 0)
				{
					return true;
				}
				if (!isDeserialize)
				{ // the serialize pair has no document to hand over: parameterless only
					return false;
				}

				switch (method.Parameters[0].Type.ToDisplayString())
				{
					case KnownTypeSymbols.JsonValueFullName: argument = CrystalJsonCallbackArgument.JsonValue; return true;
					case KnownTypeSymbols.JsonObjectFullName: argument = CrystalJsonCallbackArgument.JsonObject; return true;
					case KnownTypeSymbols.JsonArrayFullName: argument = CrystalJsonCallbackArgument.JsonArray; return true;
					default: return false;
				}
			}

			private static bool HasDataMemberAttribute(ISymbol member)
			{
				foreach (var attribute in member.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() == DataMemberAttributeFullName)
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>Reads the type's <c>[DataContract]</c> attribute: whether it is present (which switches membership to opt-in), and the contract's own <c>Name</c> / <c>Namespace</c> values (which name the type's element on the DataContract XML wire)</summary>
			private static (bool Present, string? Name, string? Namespace) GetDataContractInfo(ISymbol type)
			{
				foreach (var attribute in type.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() != DataContractAttributeFullName) continue;

					string? name = null;
					string? ns = null;
					foreach (var kv in attribute.NamedArguments)
					{
						if (kv.Key == "Name" && kv.Value.Value is string dcName)
						{
							name = dcName;
						}
						else if (kv.Key == "Namespace" && kv.Value.Value is string dcNamespace)
						{
							ns = dcNamespace;
						}
					}
					return (true, name, ns);
				}
				return (false, null, null);
			}

			#region XML member vocabulary...

			/// <summary>Resolves the member-level XML settings of a <c>[XmlProperty]</c> attribute, reporting <c>CXML0003</c>, <c>CXML0004</c> and <c>CXML0007</c></summary>
			/// <param name="member">Member being parsed (named in every diagnostic, and where they are reported)</param>
			/// <param name="type">Type of the member, which decides whether it can be projected as an XML attribute</param>
			/// <param name="xmlProfile">The container's RESOLVED XML wire profile (never <see langword="null"/>: the caller only resolves when the container produces XML)</param>
			/// <param name="rawName">The attribute's <c>Name</c> as written, <c>'@'</c> prefix included</param>
			/// <param name="attributeSpelled"><see langword="true"/> when <c>Attribute =</c> was written at all (which is what makes an explicit <see langword="false"/> a contradiction rather than a default)</param>
			/// <param name="attributeValue">The value <c>Attribute =</c> was set to</param>
			/// <param name="itemName">The attribute's <c>ItemName</c> as written</param>
			/// <param name="dictionaryFormat">The attribute's <c>DictionaryFormat</c>, as its enum member name</param>
			/// <param name="refused"><see langword="true"/> when the member's shape was refused here, so that no further member-level diagnostic stacks on top of the one already reported</param>
			/// <returns>The normalized settings; every refused shape returns them EMPTY, so that one build error is not followed by a cascade of downstream ones</returns>
			private (string? Name, bool IsAttribute, string? ItemName, string? DictionaryFormat) ResolveXmlMember(ISymbol member, TypeMetadata type, string xmlProfile, string? rawName, bool attributeSpelled, bool attributeValue, string? itemName, string? dictionaryFormat, out bool refused)
			{
				refused = false;

				if (xmlProfile == XmlProfileDataContract)
				{
					// The compat wire derives EVERY name from the data contract, has no notion of a user-data XML
					// attribute, and has exactly one dictionary shape. So none of these settings can be honored, and
					// honoring none of them silently would be a config that changes nothing without saying so.
					// All the present ones are named in ONE diagnostic: an author who wrote two of them has to see both.
					var settings = new List<string>(4);
					if (rawName is not null) settings.Add("Name = \"" + rawName + "\"");
					if (attributeSpelled && attributeValue) settings.Add("Attribute = true");
					if (itemName is not null) settings.Add("ItemName = \"" + itemName + "\"");
					// an explicitly spelled 'Default' asks to INHERIT, which the compat wire can honor: it is not a refusal
					if (dictionaryFormat is not (null or XmlDictionaryFormatDefault)) settings.Add("DictionaryFormat = " + dictionaryFormat);

					if (settings.Count > 0)
					{
						refused = true;
						ReportDiagnostic(
							new(
								"CXML0004",
								"The member-level XML vocabulary cannot be combined with the DataContract XML wire",
								"The member '{0}' carries [XmlProperty({1})], but its container produces the DataContract XML wire, whose names all come from the data contract, which has no notion of a user-data XML attribute, and which has a single dictionary shape: the setting cannot be honored. Remove it (the contract already decides), or publish the Modern XML wire, which does honor it, from a separate container (the dual-container pattern).",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString(), string.Join(", ", settings));
					}

					return (null, false, null, null);
				}

				bool isAttribute = attributeSpelled && attributeValue;
				string? name = rawName;

				if (name is not null && name.Length > 0 && name[0] == '@')
				{ // the sugar: "@id" means an XML attribute named "id", resolved HERE so nothing downstream ever sees a '@'
					if (name.Length == 1)
					{
						ReportDiagnostic(
							new(
								"CXML0007",
								"An XML name is not valid",
								"The member '{0}' declares [XmlProperty(\"@\")]: the leading '@' is the sugar that projects a member as an XML attribute, and it needs a name after it (ex: \"@id\"). Write the attribute name, or use [XmlProperty(Attribute = true)] to keep the member's own name.",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString());
						refused = true;
						return default;
					}

					if (attributeSpelled && !attributeValue)
					{ // the two spellings genuinely disagree, and silently picking either gives a wire the author did not ask for
						ReportDiagnostic(
							new(
								"CXML0007",
								"An XML name is not valid",
								"The member '{0}' declares [XmlProperty(\"{1}\", Attribute = false)]: the leading '@' asks for the member to be projected as an XML attribute, while Attribute = false refuses one. Keep only one of the two: drop the '@' to project a nested element, or drop Attribute = false.",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString(), rawName);
						refused = true;
						return default;
					}

					isAttribute = true;
					name = name.Substring(1);
				}

				// both names land in the document verbatim, so both get the same validation
				if (name is not null && !ValidateXmlName(member, name, "element or attribute"))
				{
					refused = true;
					return default;
				}
				if (itemName is not null && !ValidateXmlName(member, itemName, "item"))
				{
					refused = true;
					return default;
				}

				if (isAttribute && !IsXmlScalar(type))
				{ // an attribute value is text: a type with no lexical form could only ever be mangled into one
					ReportDiagnostic(
						new(
							"CXML0003",
							"Only a scalar member can be projected as an XML attribute",
							"The member '{0}' asks to be projected as an XML attribute, but its type '{1}' has no lexical form: an XML attribute value is text, so only scalars (booleans, numbers, strings, enums, dates, durations, GUIDs, URIs, byte arrays) can become one. Drop the '@' prefix (or Attribute = true) to project the member as a nested element instead.",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						member.Locations.Length > 0 ? member.Locations[0] : null,
						member.ToDisplayString(), type.FullName);
					refused = true;
					return default;
				}

				return (name, isAttribute, itemName, dictionaryFormat);
			}

			/// <summary>Reports <c>CXML0007</c> when a name is not a legal XML NCName, and returns whether it is</summary>
			/// <remarks>Uses the same probe as <c>XmlName.Create</c>, so that a name accepted at compile time cannot be refused at runtime: <c>XmlConvert.VerifyNCName</c> throws instead of returning, so it is caught and translated here (an exception escaping the parser would surface as the generic CJSON0001 crash instead of a message the author can act on).</remarks>
			private bool ValidateXmlName(ISymbol member, string name, string role)
			{
				if (IsValidXmlName(name, out var why)) return true;

				ReportDiagnostic(
					new(
						"CXML0007",
						"An XML name is not valid",
						"The member '{0}' declares the XML {1} name '{2}', which is not a legal XML name: {3}",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), role, name, why);
				return false;
			}

			/// <summary>Probes whether <paramref name="name"/> is a legal XML NCName, returning the reason it is not</summary>
			/// <remarks>The same probe <c>XmlName.Create</c> uses at run time, so a name accepted at compile time cannot be refused later. <c>XmlConvert.VerifyNCName</c> throws instead of returning, so the exception is caught and translated here: one escaping the parser would surface as the generic CJSON0001 crash instead of a message the author can act on.</remarks>
			private static bool IsValidXmlName(string name, out string? why)
			{
				try
				{
					System.Xml.XmlConvert.VerifyNCName(name);
					why = null;
					return true;
				}
				catch (Exception ex) when (ex is System.Xml.XmlException or ArgumentException)
				{
					// XmlException for a malformed name (space, leading digit, colon, ...); ArgumentException for an
					// empty one specifically. Either way, forward VerifyNCName's own message so the diagnostic says WHY.
					why = ex.Message;
					return false;
				}
			}

			/// <summary>Reports <c>CXML0007</c> when a member's XML name was DERIVED from its JSON name and is not a legal XML NCName</summary>
			/// <remarks>Its own message, under the same id: the remedy differs from the declared-name case (which is "fix the name you wrote"), because here nothing was written for XML at all and the author has to be told that adding <c>[XmlProperty]</c> is what separates the two wires.</remarks>
			private void ValidateDerivedXmlName(ISymbol member, string name)
			{
				if (IsValidXmlName(name, out var why)) return;

				ReportDiagnostic(
					new(
						"CXML0007",
						"An XML name is not valid",
						"The member '{0}' has no XML name of its own, so it takes its JSON name '{1}', which is not a legal XML name: {2}. Give it one with [XmlProperty(\"...\")], which leaves the JSON name untouched.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), name, why);
			}

			/// <summary>Reports <c>CXML0007</c> when a type's <c>[DataContract(Name = ...)]</c> would name the ROOT element with something no XML parser accepts</summary>
			/// <remarks>
			/// <para>MODERN wire only, and only for a contract name: the compat wire runs every name through <c>XmlConvert.EncodeLocalName</c>, and a name derived from the C# type name is a legal NCName by construction.</para>
			/// <para>Decidable from the declaration alone, which is why it is a diagnostic and not the <c>#error</c> the emitter used to carry (the member-level equivalent has been CXML0007 all along, and the two shapes deserve the same treatment). The emitter keeps its <c>#error</c> as an unreachable backstop.</para>
			/// </remarks>
			private void ReportInvalidRootXmlName(INamedTypeSymbol type, string? dataContractName, string xmlProfile)
			{
				if (xmlProfile != XmlProfileModern || dataContractName is null) return;
				if (IsValidXmlName(dataContractName, out var why)) return;

				ReportDiagnostic(
					new(
						"CXML0007",
						"An XML name is not valid",
						"The type '{0}' declares [DataContract(Name = \"{1}\")], which names its XML root element and is not a legal XML name: {2}. Rename the contract, or drop the Name so the root takes the type name.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					type.Locations.Length > 0 ? type.Locations[0] : null,
					type.ToDisplayString(), dataContractName, why);
			}

			/// <summary>Reports <c>CXML0006</c> on a sequence whose items are themselves bare sequences, with no intermediate type to name the inner items</summary>
			/// <remarks>
			/// <para>MODERN wire only. The DataContract wire derives a name for every level from the contract (an inner sequence of strings becomes <c>ArrayOfstring</c> holding <c>string</c> items), so the shape is decidable there and the compat emitter names it instead of refusing it; refusing it would block porting a legacy DTO that <c>DataContractSerializer</c> serializes today.</para>
			/// <para>A DICTIONARY is not a bare sequence on either side of this test: its entries are named by the resolved dictionary format, so it always has names to give, which is exactly what a bare sequence lacks. Nor is a <c>byte[]</c> or a <c>string</c>, which are scalars on this wire however enumerable C# considers them.</para>
			/// </remarks>
			private void ReportBareNestedCollection(ISymbol member, TypeMetadata type)
			{
				if (!IsBareXmlSequence(type, out var item)) return;
				if (!IsBareXmlSequence(item, out _)) return;

				ReportDiagnostic(
					new(
						"CXML0006",
						"A sequence of sequences has no name for its inner items",
						"The member '{0}' is a sequence of type '{1}' whose items are themselves sequences: the inner sequence has no element name to give its own items, so the shape has no XML projection. Introduce an intermediate type for the inner sequence (a small record holding one collection member), which gives the inner items a name.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), type.FullName);
			}

			/// <summary>Reports <c>CXML0011</c> on a dictionary member whose resolved shape carries the VALUE as text, when the value type has no lexical form</summary>
			/// <remarks>
			/// <para>MODERN wire only: the compat wire has exactly one dictionary shape (<c>KeyValueOfXY</c> with <c>Key</c>/<c>Value</c> child ELEMENTS), which always has room for a nested value.</para>
			/// <para>Decidable from the declarations alone (the member's <c>DictionaryFormat</c>, else the container's, else the profile default), which is why it is a diagnostic and not the <c>#error</c> the emitter used to carry. The <c>#error</c> stays in the emitter as an unreachable backstop, so a future shape that forgets this check still cannot emit a mangled document.</para>
			/// <para>The KEY position is NOT checked here: a key's lexical form is fixed by the key type, and a key that has none is already refused elsewhere; what varies per shape is only where the VALUE lands.</para>
			/// </remarks>
			private void ReportUnprojectableDictionaryValue(ISymbol member, TypeMetadata type, string? memberDictionaryFormat)
			{
				var actual = type.NullableOfType ?? type;
				if (actual.KeyType is null || actual.ValueType is not { } valueType) return;

				string format =
					memberDictionaryFormat is not (null or XmlDictionaryFormatDefault) ? memberDictionaryFormat
					: this.ContextXmlDictionaryFormat is { } containerFormat && containerFormat != XmlDictionaryFormatDefault ? containerFormat
					: XmlDictionaryFormatDirect;

				if (format is not (XmlDictionaryFormatKeyAttribute or XmlDictionaryFormatKeyValueAttributes)) return;
				if (IsXmlScalar(valueType)) return;

				ReportDiagnostic(
					new(
						"CXML0011",
						"A dictionary value has no lexical form for the shape that was asked for",
						"The member '{0}' asks for the {1} dictionary shape, which carries the entry VALUE as text, but its value type '{2}' has no lexical form: only scalars (booleans, numbers, strings, enums, dates, durations, GUIDs, URIs, byte arrays) can land in a text position. Use the Direct or KeyValueElements shape instead, which hold the value as a nested element.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), format, valueType.FullName);
			}

			/// <summary>Reports <c>CXML0012</c>: a setting that was written explicitly, resolved, and then never consulted by the wire the container produces</summary>
			/// <param name="location">Where the setting was written (the member, or the container's own declaration)</param>
			/// <param name="owner">Display name of the member or container carrying the setting</param>
			/// <param name="setting">The setting as written, ex: <c>ItemName = "tag"</c></param>
			/// <param name="why">Why the wire never consults it, and what would make it meaningful; phrased as a sentence with no trailing period</param>
			/// <remarks>
			/// <para>INFO, deliberately, and the only member of the CXML range that is not an error. Every other CXML diagnostic
			/// refuses a shape whose document would be wrong or unwritable; an inert setting produces a document that is entirely
			/// correct, and only the DECLARATION is misleading. Failing a build over a correct wire would be the wrong trade, and a
			/// warning would land in the build log of consumers who did nothing wrong (a container inherited from a template, a
			/// setting that stopped applying when a member's type changed).</para>
			/// <para>It exists because the no-silent-configuration doctrine of this overlay cuts both ways: a setting that changes
			/// the wire without being asked for is refused, and a setting that asks for something the wire never delivers should not
			/// pass unmentioned either. It is the same reasoning that made CXML0004 refuse an inert member-level option on the compat
			/// wire, applied where the resulting document is right and only the author's expectation is not.</para>
			/// </remarks>
			private void ReportInertXmlSetting(Location? location, string owner, string setting, string why)
			{
				ReportDiagnostic(
					new(
						"CXML0012",
						"An XML setting has no effect here",
						"The XML setting '{1}' declared on '{0}' has no effect: {2}. The document produced is correct; the setting simply does nothing, so remove it (or change what makes it inert).",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Info,
						isEnabledByDefault: true
					),
					location,
					owner, setting, why);
			}

			/// <summary>Reports <c>CXML0012</c> on the member-level settings a type's resolved wire will never consult</summary>
			/// <remarks>
			/// <para>Run once every member of the type has resolved, for the same reason <see cref="ReportDuplicateXmlNames"/> is: what
			/// makes a setting inert is the member's RESOLVED projection (attribute versus element, scalar versus sequence), not the
			/// attribute as written.</para>
			/// <para>The compat wire needs no case of its own here: <c>ResolveXmlMember</c> refuses the whole member-level vocabulary
			/// on it (CXML0004) and hands back empty settings, so nothing this method reads is ever set on that profile.</para>
			/// </remarks>
			private void ReportInertXmlMemberSettings(List<CrystalJsonMemberMetadata> members, Dictionary<string, ISymbol> memberSymbols)
			{
				foreach (var member in members)
				{
					if (!memberSymbols.TryGetValue(member.MemberName, out var symbol)) continue;
					var location = symbol.Locations.Length > 0 ? symbol.Locations[0] : null;

					if (member.XmlItemName is not null && !HasXmlItems(member.Type))
					{ // ItemName names the ITEMS of a sequence, or the ENTRIES of a dictionary: a member that has neither has nothing to name
						ReportInertXmlSetting(
							location,
							symbol.ToDisplayString(),
							"ItemName = \"" + member.XmlItemName + "\"",
							$"the member's type '{member.Type.FullName}' is written as a single value, not as items: there is nothing for this name to apply to");
					}

					if (member.XmlIsAttribute && member.IgnoreCondition == "Never")
					{ // an attribute has no nil form (there is no attribute of an attribute), so a null one is absent whatever was asked
						ReportInertXmlSetting(
							location,
							symbol.ToDisplayString(),
							"JsonIgnore(Condition = Never)",
							"the member is projected as an XML attribute, and an attribute has no nil form: a null value makes it absent whatever the condition asks for. Project the member as a nested element to get the explicit nil the condition is asking for (it stays honored on the JSON wire either way)");
					}
				}
			}

			/// <summary>Tests whether a member's type is written as a series of ITEMS on the XML wire (a sequence's items, or a dictionary's entries), which is what an <c>ItemName</c> can name</summary>
			/// <remarks>Deliberately built on the same two predicates the emitter dispatches on, so that a member this reports as having no items is one the emitter writes as a single value.</remarks>
			private static bool HasXmlItems(TypeMetadata type)
			{
				var actual = type.NullableOfType ?? type;
				return (actual.KeyType is not null && actual.ValueType is not null) || IsBareXmlSequence(actual, out _);
			}

			/// <summary>Reports <c>CXML0005</c> when two members of a type resolve to the same effective XML name, once the <c>'@'</c> sugar has been normalized</summary>
			/// <remarks>
			/// <para>Elements and attributes are checked SEPARATELY, because in XML they do not share a namespace: an attribute and a child element may legitimately carry the same name, and refusing that pair would be a false positive on a perfectly readable document.</para>
			/// <para>Only member-versus-member: a collision with a polymorphic type's discriminator cannot be seen from here, because a derived type does not know its own hierarchy. It is checked over the whole container instead, by <see cref="ReportDiscriminatorXmlNameCollisions"/>, and reported under the same id.</para>
			/// <para>The REMEDY is profile-aware, because the obvious one is not available on both: on the DataContract wire an <c>[XmlProperty]</c> rename is itself refused (CXML0004), so the fix has to point at the <c>[DataMember(Name = ...)]</c> that owns the colliding name.</para>
			/// <para>This is also where the EFFECTIVE XML name of every member is validated on the modern wire (CXML0007). <c>ResolveXmlMember</c> only sees the names an <c>[XmlProperty]</c> declares, so a member that inherits its XML name from its JSON one (<c>[JsonPropertyName("$id")]</c>, or a raw member name the naming policy leaves alone) would otherwise reach the emitter unchecked and land in the document verbatim. The compat wire is immune: it runs every name through <c>XmlConvert.EncodeLocalName</c>, which has a legal spelling for any input.</para>
			/// </remarks>
			private void ReportDuplicateXmlNames(INamedTypeSymbol type, List<CrystalJsonMemberMetadata> members, Dictionary<string, ISymbol> memberSymbols, string xmlProfile)
			{
				// naming the remedy the OTHER wire uses would send the author straight into CXML0004
				string remedy =
					xmlProfile == XmlProfileDataContract
						? "The names come from the data contract on this wire, so rename one of them there, with [DataMember(Name = \"...\")]."
						: "Rename one of them for XML with [XmlProperty(\"...\")], which leaves the JSON name untouched.";

				var elements = new Dictionary<string, string>(StringComparer.Ordinal);
				var attributes = new Dictionary<string, string>(StringComparer.Ordinal);

				foreach (var member in members)
				{
					// the JSON name is the fallback: it is what the XML name derives from when the member does not override it
					string effective = member.XmlName ?? member.Name;
					var seen = member.XmlIsAttribute ? attributes : elements;

					if (xmlProfile == XmlProfileModern && member.XmlName is null && memberSymbols.TryGetValue(member.MemberName, out var symbol))
					{ // the name was NOT declared for XML: it fell back to the JSON one, which nothing has validated yet
						ValidateDerivedXmlName(symbol, effective);
					}

					if (!seen.TryGetValue(effective, out var previous))
					{
						seen.Add(effective, member.MemberName);
						continue;
					}

					// a member shadowing an inherited one of the same name (the 'new' keyword) appears twice in the
					// hierarchy walk: that is one member, not a collision
					if (previous == member.MemberName) continue;

					ReportDiagnostic(
						new(
							"CXML0005",
							"Two members resolve to the same XML name",
							"The members '{1}' and '{2}' of type '{0}' both resolve to the XML {3} name '{4}': one of the two would silently win in the document, which parses either way. {5}",
							"SnowBank.Serialization.Json.CodeGen",
							DiagnosticSeverity.Error,
							isEnabledByDefault: true
						),
						type.Locations.Length > 0 ? type.Locations[0] : null,
						type.ToDisplayString(), previous, member.MemberName, member.XmlIsAttribute ? "attribute" : "element", effective, remedy);
				}
			}

			/// <summary>Returns the XML name the type discriminator of a polymorphic root occupies: its JSON property name, with the leading <c>'$'</c> removed</summary>
			/// <param name="declaredPropertyName">The root's <c>TypeDiscriminatorPropertyName</c>, or <see langword="null"/> when it declares none (the JSON default, <c>$type</c>)</param>
			/// <returns>The attribute name, which may be EMPTY when the declared name is nothing but the <c>'$'</c> (a shape the emitter refuses with a <c>#error</c>)</returns>
			/// <remarks><b>Shared with the emitter</b> (hence <c>internal</c>): the collision check below and <c>Emitter.WriteXmlDiscriminator</c> must resolve the SAME name, or the check would guard a name the document never carries. Spelling the rule twice is what would let them drift.</remarks>
			internal static string GetXmlDiscriminatorName(string? declaredPropertyName)
			{
				string declared = declaredPropertyName ?? "$type";
				return declared.StartsWith("$", StringComparison.Ordinal) ? declared.Substring(1) : declared;
			}

			/// <summary>Reports <c>CXML0005</c> when a member of a derived type resolves to the XML name the type discriminator will occupy</summary>
			/// <remarks>
			/// <para>MODERN wire only: this is the wire that writes the discriminator as an XML ATTRIBUTE on every derived element,
			/// named after the JSON discriminator property with its leading <c>'$'</c> removed. Two attributes of one name on one
			/// element is not even a well-formed document, and nothing downstream would say so.</para>
			/// <para>Run over the WHOLE container rather than per type, because a derived type does not know its own hierarchy: the
			/// discriminator is declared on the polymorphic root, which is a different type, parsed separately.</para>
			/// <para>Only ATTRIBUTE-projected members collide: a child element of the same name shares no namespace with an attribute,
			/// exactly as in the member-versus-member check.</para>
			/// </remarks>
			private void ReportDiscriminatorXmlNameCollisions(List<CrystalJsonTypeMetadata> includedTypes, HashSet<INamedTypeSymbol> mappedTypes, string xmlProfile)
			{
				if (xmlProfile != XmlProfileModern) return;

				var byRef = new Dictionary<TypeRef, CrystalJsonTypeMetadata>();
				foreach (var includedType in includedTypes)
				{
					byRef[includedType.Type.Ref] = includedType;
				}

				// This pass and the EMITTER must cover the same set, or the document would carry an attribute nothing checked.
				// The emitter annotates every type its PolymorphicMap has an entry for; that map is built (Emitter's ctor) by
				// walking the DerivedTypes of every included type, which is exactly what this loop walks. The IsPolymorphicRoot
				// guard below costs nothing today because the two are equivalent by construction: the flag is set by the very
				// [JsonDerivedType] parse that fills DerivedTypes, so a non-empty DerivedTypes implies the flag and vice-versa.
				// If either side ever stops implying the other (a derived-type list built from something other than that
				// attribute, or a root flagged without one), THIS is the pair to re-align.
				foreach (var root in includedTypes)
				{
					if (!root.IsPolymorphicRoot || root.DerivedTypes.Count == 0) continue;

					string name = GetXmlDiscriminatorName(root.TypeDiscriminatorPropertyName);
					if (name.Length == 0) continue; // the emitter refuses that shape on its own

					foreach (var (_, derivedType, _) in root.DerivedTypes)
					{
						if (!byRef.TryGetValue(derivedType.Ref, out var derived)) continue;

						foreach (var member in derived.Members)
						{
							if (!member.XmlIsAttribute) continue;
							if ((member.XmlName ?? member.Name) != name) continue;

							ReportDiagnostic(
								new(
									"CXML0005",
									"Two members resolve to the same XML name",
									"The member '{1}' of type '{0}' resolves to the XML attribute name '{2}', which is also where the type discriminator of '{3}' is written on this wire: the element would carry that attribute twice, which is not a well-formed document. Rename the member for XML with [XmlProperty(\"...\")], which leaves the JSON name untouched, or rename the discriminator with [JsonPolymorphic(TypeDiscriminatorPropertyName = \"...\")].",
									"SnowBank.Serialization.Json.CodeGen",
									DiagnosticSeverity.Error,
									isEnabledByDefault: true
								),
								FindLocation(mappedTypes, derived.Type.Ref),
								derived.Type.FullName, member.MemberName, name, root.Type.FullName);
						}
					}
				}
			}

			/// <summary>Returns the declaration location of one of the crawled types, or the container's own when the symbol is out of reach</summary>
			private Location? FindLocation(HashSet<INamedTypeSymbol> mappedTypes, TypeRef type)
			{
				foreach (var symbol in mappedTypes)
				{
					if (symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == type.FullyQualifiedName)
					{
						return symbol.Locations.Length > 0 ? symbol.Locations[0] : this.ContextClassLocation;
					}
				}
				return this.ContextClassLocation;
			}

			/// <summary>Reports <c>CXML0008</c> when a member's custom converter has no XML facet, on a container that produces XML</summary>
			/// <remarks>
			/// <para>A converter attached to a member REPLACES the rules that would otherwise decide the member's wire form. On a
			/// container that publishes two formats, a converter that only answers for one of them leaves the other to be written by
			/// the very rules it was declared to replace: the JSON says <c>"0"</c>/<c>"1"</c> while the XML says <c>true</c>/<c>false</c>,
			/// with nothing in the source saying so. That is the silently-divergent wire this range exists to refuse.</para>
			/// <para>This fires for the built-in converters too (<c>[JsonBooleanLiterals]</c>): they are JSON converters, and there is
			/// no XML form of "write this boolean as 0 or 1" that the generator may assume on the author's behalf.</para>
			/// </remarks>
			private void ReportMissingXmlConverterFacet(ISymbol member, TypeMetadata type, string converterType)
			{
				ReportDiagnostic(
					new(
						"CXML0008",
						"A member's custom converter has no XML facet",
						"The member '{0}' is written through the custom converter '{1}', but its container also produces XML, and that converter does not implement ICrystalXmlSerializer<{2}>: the converter owns the member's JSON form while its XML form would be written by the rules the converter replaced, so the two wires would disagree with nothing in the source saying so. Implement the XML facet on the converter, drop the converter (the member's own type is then written directly on both wires), or publish XML from a separate container (the dual-container pattern).",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), converterType, (type.NullableOfType ?? type).FullName);
			}

			/// <summary>Reports <c>CXML0009</c> when a member is projected as an XML ATTRIBUTE and also carries a custom converter</summary>
			/// <remarks>
			/// <para>A refusal by CONSTRUCTION, not a missing feature: the XML facet's only entry point (<c>WriteXml</c>) writes an
			/// ELEMENT, so there is no call a generated body could make that would turn a converter into an attribute value. The
			/// attribute path would therefore format the member with the very rules the converter was declared to replace, and the
			/// two wires would disagree with nothing in the source saying so - exactly what CXML0008 refuses, one door further on.</para>
			/// <para>Reported for ANY converter, including one that does implement the XML facet: having the facet is what makes the
			/// bypass silent rather than merely wrong, since the author has every reason to believe it is being used.</para>
			/// </remarks>
			private void ReportConvertedAttributeMember(ISymbol member, string converterType)
			{
				ReportDiagnostic(
					new(
						"CXML0009",
						"A member projected as an XML attribute cannot go through a custom converter",
						"The member '{0}' is projected as an XML attribute and is written through the custom converter '{1}': the XML facet of a converter writes an ELEMENT, so it cannot produce an attribute value, and the attribute would be written by the rules the converter replaced - the JSON and the XML of this member would disagree with nothing in the source saying so. Keep one of the two: drop the '@' projection, or drop the converter.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), converterType);
			}

			/// <summary>Reports <c>CXML0010</c> on a DataContract-profile member whose type carries <c>[CollectionDataContract]</c></summary>
			/// <remarks>
			/// <para>That attribute renames the collection's contract, its items and (for a dictionary) its key and value elements.
			/// This emission derives every one of those names from the item types instead, so honoring the attribute is not a matter
			/// of degree: a member carrying it would silently get a DIFFERENT wire from the one the application reads today, which is
			/// the single failure mode this whole profile exists to prevent.</para>
			/// <para>Only the member's OWN type is probed. A collection nested inside another collection, or a dictionary value type,
			/// carrying the attribute is not seen here, which is a gap this diagnostic does not close.</para>
			/// </remarks>
			private void ReportUnsupportedCollectionDataContract(ISymbol member, ITypeSymbol memberType)
			{
				var named = GetUnderlyingValueType(memberType);
				bool present = false;
				foreach (var attribute in named.GetAttributes())
				{
					var attributeClass = attribute.AttributeClass;
					if (attributeClass?.Name == "CollectionDataContractAttribute" && attributeClass.ContainingNamespace?.ToDisplayString() == "System.Runtime.Serialization")
					{
						present = true;
						break;
					}
				}

				if (!present) return;

				ReportDiagnostic(
					new(
						"CXML0010",
						"[CollectionDataContract] is not honored by the generated DataContract XML wire",
						"The member '{0}' is of type '{1}', which carries [CollectionDataContract]. The generated DataContract XML wire derives the collection's element names from the item types, and does not read that attribute: the member would be written under names that differ from the ones DataContractSerializer produces for it. Replace the annotated collection type with a plain one (List<T>, T[], Dictionary<K,V>) on this DTO, or keep this member off the XML container.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Error,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString(), named.ToDisplayString());
			}

			/// <summary>Returns whether a converter type implements the XML facet for the member's type, and whether it took the <c>Nullable&lt;T&gt;</c> form itself</summary>
			/// <remarks>The same probe order as <see cref="GetConverterFacets"/> on the JSON side: the EXACT form first (a converter declared for <c>T?</c> answers for the absent case itself), then the nullable-unwrapped lift.</remarks>
			private static (bool Serializer, bool NullableForm) GetXmlConverterFacet(INamedTypeSymbol? converterType, ITypeSymbol memberType)
			{
				if (converterType is null)
				{ // a converter with no symbol of its own is one of the built-ins, which are JSON-only by construction
					return (false, false);
				}

				var underlying = GetUnderlyingValueType(memberType);
				if (!ReferenceEquals(underlying, memberType) && ImplementsXmlSerializerFor(converterType, memberType))
				{
					return (true, true);
				}
				return (ImplementsXmlSerializerFor(converterType, underlying), false);
			}

			/// <inheritdoc cref="GetXmlConverterFacet"/>
			private static bool ImplementsXmlSerializerFor(INamedTypeSymbol converterType, ITypeSymbol valueType)
			{
				foreach (var iface in converterType.AllInterfaces)
				{
					if (iface.TypeArguments.Length != 1 || !SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], valueType)) continue;
					if (iface.Name == "ICrystalXmlSerializer" && iface.ContainingNamespace?.ToDisplayString() == KnownTypeSymbols.CrystalXmlNamespace)
					{
						return true;
					}
				}
				return false;
			}

			/// <summary>Tests whether a type has a lexical form on the XML wire: the set the <c>CrystalXmlFormatters</c> cover, plus strings and enums</summary>
			/// <remarks>
			/// <para>This is what an XML ATTRIBUTE can hold, since an attribute value is text and nothing else. <c>Nullable&lt;T&gt;</c> is unwrapped (the absent case is a presence question, not a formatting one); a <c>byte[]</c> counts, because it renders as base64 text; a <c>string</c> counts even though C# makes it enumerable.</para>
			/// <para><b>Shared with the emitter</b> (hence <c>internal</c>): <c>Emitter.GetXmlScalarText</c> resolves a formatter for exactly this set, so a member the parser accepted as an attribute is one the emitter can format, and a member it refused (CXML0003) never reaches an attribute position. Widening one without the other is what would break that.</para>
			/// </remarks>
			internal static bool IsXmlScalar(TypeMetadata type)
			{
				var actual = type.NullableOfType ?? type;

				if (actual.IsEnum()) return true;

				switch (actual.SpecialType)
				{
					case SpecialType.System_Boolean:
					case SpecialType.System_Char:
					case SpecialType.System_SByte:
					case SpecialType.System_Byte:
					case SpecialType.System_Int16:
					case SpecialType.System_UInt16:
					case SpecialType.System_Int32:
					case SpecialType.System_UInt32:
					case SpecialType.System_Int64:
					case SpecialType.System_UInt64:
					case SpecialType.System_Single:
					case SpecialType.System_Double:
					case SpecialType.System_Decimal:
					case SpecialType.System_String:
					case SpecialType.System_DateTime:
					{
						return true;
					}
				}

				if (actual.NameSpace == "System" && actual.Name is "TimeSpan" or "Guid" or "Uri")
				{ // the three the formatters cover that have no SpecialType of their own
					return true;
				}

				// a byte[] is base64 TEXT on both wires; a List<byte> is not (it renders as a sequence of numbers)
				return actual.TypeKind == TypeKind.Array && actual.ElementType is { SpecialType: SpecialType.System_Byte };
			}

			/// <summary>Tests whether a type is projected as a BARE sequence of unnamed children on the XML wire, and hands back its item type</summary>
			/// <remarks>Bare means "brings no names of its own": a scalar is not a sequence at all (a <c>string</c> and a <c>byte[]</c> included, however enumerable C# considers them), and a dictionary is a sequence whose entries the dictionary format names.</remarks>
			private static bool IsBareXmlSequence(TypeMetadata type, [MaybeNullWhen(false)] out TypeMetadata itemType)
			{
				var actual = type.NullableOfType ?? type;
				if (!IsXmlScalar(actual) && actual.KeyType is null && actual.ElementType is { } element)
				{
					itemType = element;
					return true;
				}
				itemType = null;
				return false;
			}

			#endregion

			/// <summary>Warns (CJSON0012, suppressible) on an internal member with no include/exclude signal: the generated converter serializes it while the reflection path does not see it</summary>
			/// <remarks>The generated-only inclusion is kept for wire compatibility (existing applications depend on it); the warning makes the cross-path divergence observable so the intent gets pinned explicitly ([JsonInclude] includes the member on both paths, [JsonIgnore] excludes it on both).</remarks>
			private void MaybeReportInternalUnannotated(ISymbol member)
			{
				if (member.DeclaredAccessibility is not (Accessibility.Internal or Accessibility.ProtectedOrInternal))
				{
					return;
				}
				foreach (var attribute in member.GetAttributes())
				{
					switch (attribute.AttributeClass?.Name)
					{
						case "JsonIncludeAttribute":
						case "JsonIgnoreAttribute":
						case "IgnoreDataMemberAttribute":
						{ // the intent is pinned: both paths resolve the member the same way
							return;
						}
					}
				}
				ReportDiagnostic(
					new(
						"CJSON0012",
						"An internal member is serialized by the generated converter but invisible to the reflection path",
						"The internal member '{0}' is included by the generated converter, but the reflection path does not see it: the two paths disagree on this member. Pin the intent explicitly: [JsonInclude] includes it on both paths, [JsonIgnore] excludes it on both. Suppress this warning to keep the generated-only inclusion.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Warning,
						isEnabledByDefault: true
					),
					member.Locations.Length > 0 ? member.Locations[0] : null,
					member.ToDisplayString());
			}

			/// <summary>Reports CJSON0020 (informational, once per container) when the generated JSON proxy surface (ToReadOnly/ToMutable, the ReadOnly/Writable proxy types) is left out</summary>
			/// <remarks>
			/// <para>Generating the proxy surface needs static abstract interface members, which require both a .NET 7+ runtime (the proxy interfaces are absent from the lite <c>netstandard2.0</c> build) and C# 11 (a consumer below that floor cannot implement them even when they are visible). <see cref="KnownTypeSymbols.SupportsJsonProxies"/> is the single source of truth for that combined test.</para>
			/// <para>Without this diagnostic, the loss is silent: the container still gets its converter, its <c>TypeMapper</c>, and (if requested) XML output, and a consumer only discovers the missing surface downstream, as a bare CS0117/CS1061 at whatever call site references <c>ToReadOnly</c>/<c>ToMutable</c> or the proxy types.</para>
			/// </remarks>
			private void MaybeReportProxySurfaceNotGenerated(ISymbol symbol)
			{
				if (this.KnownSymbols.SupportsJsonProxies)
				{
					return;
				}

				ReportDiagnostic(
					new(
						"CJSON0020",
						"The generated JSON proxy surface is not generated for this container",
						"The container '{0}' does not get its ToReadOnly/ToMutable methods or its ReadOnly/Writable proxy types: generating them requires a .NET 7+ runtime (static abstract interface members) and C# 11 or greater, and this compilation does not satisfy both. The converter, the TypeMapper, and any requested XML output are unaffected.",
						"SnowBank.Serialization.Json.CodeGen",
						DiagnosticSeverity.Info,
						isEnabledByDefault: true
					),
					this.ContextClassLocation,
					symbol.ToDisplayString());
			}

			/// <param name="xmlProfile"><inheritdoc cref="CrawlIncludedTypes" path="/param[@name='xmlProfile']"/></param>
			public (CrystalJsonMemberMetadata? Metadata, ITypeSymbol Type) ParseMemberMetadata(ISymbol member, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work, string? namingPolicy, bool hasDataContract, string? xmlProfile)
			{
				// On a [DataContract] type the model is opt-in and accessibility-blind: [DataMember] is the ONLY membership
				// signal, so a member without it is out whatever its accessibility, and a member with it is in whatever its
				// accessibility. Mirrors CrystalJsonTypeResolver.FilterMemberByAttributes, which is the reference behaviour.
				bool dataContractMember = hasDataContract && HasDataMemberAttribute(member);
				if (hasDataContract && !dataContractMember)
				{
					return default;
				}

				var memberName = member.Name;
				bool isField;
				ITypeSymbol typeSymbol;
				bool isReadOnly;
				bool isInitOnly;
				bool isRequired;
				bool isNonPublic = false;
				bool hasNonPublicGetter = false;
				bool hasNonPublicSetter = false;

				switch (member)
				{
					case IPropertySymbol property:
					{
						if (property.IsImplicitlyDeclared)
						{
							return default;
						}
						if (!IsReachableFromGeneratedCode(property.DeclaredAccessibility))
						{
							// non-public membership needs an opt-in, and there are two: [JsonInclude] in STJ mode, or
							// [DataMember] on a [DataContract] type. Either way every access goes through an accessor thunk.
							if (!HasJsonIncludeAttribute(property) && !dataContractMember)
							{
								return default;
							}
							isNonPublic = true;
						}
						else if (!dataContractMember)
						{ // in DataContract mode [DataMember] already pins the intent, so the two paths cannot disagree
							MaybeReportInternalUnannotated(property);
						}
						isField = false;
						typeSymbol = property.Type;
						isReadOnly = property.IsReadOnly;
						isRequired = property.IsRequired;

						if (!isNonPublic && property.GetMethod is { } getMethod && !IsReachableFromGeneratedCode(getMethod.DeclaredAccessibility))
						{
							// a member whose value cannot be read cannot be serialized: [JsonInclude], or [DataMember] on a
							// [DataContract] type, unlocks the accessor through a thunk; otherwise the member stays
							// invisible (same as the reflection path)
							if (!HasJsonIncludeAttribute(property) && !dataContractMember)
							{
								return default;
							}
							hasNonPublicGetter = true;
						}

						var setMethod = property.SetMethod;
						isInitOnly = false;
						if (setMethod is not null)
						{
							foreach (var mod in setMethod.ReturnTypeCustomModifiers)
							{
								if (mod.Modifier.Name == "IsExternalInit")
								{
									isInitOnly = true;
								}
							}
							if (!isNonPublic && !IsReachableFromGeneratedCode(setMethod.DeclaredAccessibility))
							{
								if (HasJsonIncludeAttribute(property) || dataContractMember)
								{ // [JsonInclude], or [DataMember] on a [DataContract] type, unlocks the non-public accessor (through a thunk)
									hasNonPublicSetter = true;
								}
								else
								{ // same as the reflection path without the opt-in: the member is serialize-only
									isReadOnly = true;
								}
							}
						}

						break;
					}
					case IFieldSymbol field:
					{
						if (field.IsConst)
						{ // do not include constants
							return default;
						}
						if (field.IsImplicitlyDeclared)
						{
							return default;
						}
						if (!IsReachableFromGeneratedCode(field.DeclaredAccessibility))
						{
							if (!HasJsonIncludeAttribute(field) && !dataContractMember)
							{
								return default;
							}
							isNonPublic = true;
						}
						else if (!dataContractMember)
						{ // in DataContract mode [DataMember] already pins the intent, so the two paths cannot disagree
							MaybeReportInternalUnannotated(field);
						}
						isField = true;
						//Debug.Assert(field.Type is INamedTypeSymbol);
						typeSymbol = field.Type;
						isReadOnly = field.IsReadOnly;
						isInitOnly = false; // not possible on a field
						isRequired = field.IsRequired;
						break;
					}
					default:
					{
						return default;
					}
				}

				var type = TypeMetadata.Create(typeSymbol);

				if (typeSymbol is INamedTypeSymbol named)
				{
					MaybeAddLinkedType(type, named, mappedTypes, work);
				}
				else if (type.ElementType is not null && typeSymbol is IArrayTypeSymbol array && array.ElementType is INamedTypeSymbol elemType)
				{
					MaybeAddLinkedType(type.ElementType, elemType, mappedTypes, work);
				}

				var memberAttributes = member.GetAttributes();
#if FULL_DEBUG
				var attributes = memberAttributes.Select(attr => attr.ToString()).ToImmutableEquatableArray();
#endif

				bool isNotNull;
				if (type.IsValueType())
				{ 
					// only Nullable<T> is nullable
					isNotNull = type.NullableOfType == null;
				}
				else
				{
					// look for nullability annotations
					//TODO: should we check if nullability annotations are enabled for ths type/assembly?
					isNotNull = type.Nullability == NullableAnnotation.NotAnnotated;
				}

				// an "ignore this member" signal combined with an "include this member" signal is an application bug:
				// [JsonIgnore] still wins (both paths resolve it the same way), but silently letting it win hides a real defect,
				// so say so loudly (the reflection path has no build step and cannot warn; this diagnostic is its only surface)
				{
					string? includeSignal = null;
					bool hasUnconditionalJsonIgnore = false;
					bool hasIgnoreDataMember = false;
					foreach (var attribute in memberAttributes)
					{
						switch (attribute.AttributeClass?.ToDisplayString())
						{
							case JsonIgnoreAttributeFullName:
							{ // only the exclusion form contradicts an include signal; a Condition of Never/WhenWritingNull/WhenWritingDefault is a serialization rule, not an exclusion
								int condition = 1;
								foreach (var kv in attribute.NamedArguments)
								{
									if (kv.Key == "Condition" && kv.Value.Value is int n) condition = n;
								}
								hasUnconditionalJsonIgnore |= condition == 1;
								break;
							}
							case IgnoreDataMemberAttributeFullName: hasIgnoreDataMember = true; break;
							case DataMemberAttributeFullName: includeSignal ??= "DataMember"; break;
							case JsonIncludeAttributeFullName: includeSignal ??= "JsonInclude"; break;
							case KnownTypeSymbols.JsonPropertyAttributeFullName: includeSignal ??= "JsonProperty"; break;
							default:
							{
								// a [JsonIgnore] spelled by another library (e.g. Newtonsoft) has no Condition property;
								// the reflection path matches it by name, so the conflict check must too
								if (attribute.AttributeClass?.Name == "JsonIgnoreAttribute") hasUnconditionalJsonIgnore = true;
								break;
							}
						}
					}
					if (hasUnconditionalJsonIgnore && includeSignal != null)
					{
						// an ERROR, not a warning: a mid-port project carries thousands of interim warnings, and a
						// warning drowns where an error gets read. The dual-output DTO is not supported, so the
						// message leads with the split and never suggests a Condition (a Condition would flip the
						// member to included-with-a-write-rule and ship it onto the second wire for the first time).
						// The reflection path refuses the same conflict when it builds the type's contract.
						ReportDiagnostic(
							new(
								"CJSON0008",
								"A member mixes an unconditional [JsonIgnore] with an attribute that includes it",
								"The member '{0}' carries an unconditional [JsonIgnore] next to [{1}]. In a DCJS-era two-serializer setup this pair put the member on one wire and kept it off the other; that dual-output pattern is not supported here, because one type cannot serve two wire contracts at once: split the type into one DTO per serializer, each with a single coherent set of attributes. If one of the two attributes is simply a mistake, remove it.",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString(), includeSignal);
					}
					else if (hasIgnoreDataMember && includeSignal != null)
					{
						ReportDiagnostic(
							new(
								"CJSON0008",
								"A member mixes [IgnoreDataMember] with an attribute that includes it",
								"The member '{0}' carries both [IgnoreDataMember] and [{1}]: the two contradict each other. The ignore signal wins (the member is excluded), but this is almost certainly a bug: keep only one of the two attributes.",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Warning,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString(), includeSignal);
					}
				}

				// parameters that can be modified via attributes or keywords on the member
				string? name = null;
				string? dataMemberName = null;
				bool dataMemberIsRequired = false;
				int? dataMemberOrder = null;
				bool emitDefaultValue = true;
				bool hasXmlProperty = false;
				string? xmlRawName = null;
				bool xmlAttributeSpelled = false;
				bool xmlAttributeValue = false;
				string? xmlItemName = null;
				string? xmlDictionaryFormat = null;
				string? stjPropertyName = null;
				string? newtonsoftPropertyName = null;
				bool isKey = false;
				string? ignoreCondition = null;
				string? enumFormat = null;
				string? customConverterType = null;
				string? customConverterArgs = null;
				bool customConverterHasPacker = true;
				bool customConverterHasDeserializer = true;
				bool customConverterIsNullableForm = false;
				// the converter's SYMBOL, kept next to its name so that a second format can probe its OWN facet on it (the XML one, below)
				INamedTypeSymbol? customConverterSymbol = null;
				string? nativeConverterType = null;
				bool nativeConverterHasPacker = true;
				bool nativeConverterHasDeserializer = true;
				bool nativeConverterIsNullableForm = false;
				INamedTypeSymbol? nativeConverterSymbol = null;

				string defaultLiteral = GetDefaultLiteral(type);
				bool hasNonZeroDefault = false;

				foreach (var attribute in memberAttributes)
				{
					var attributeType = attribute.AttributeClass;
					if (attributeType is null) continue;

					switch (attributeType.ToDisplayString())
					{
						case IgnoreDataMemberAttributeFullName:
						{ // [IgnoreDataMember]: the DataContract opt-out; the generator treats it like an unconditional [JsonIgnore]
							return default;
						}
						case JsonIgnoreAttributeFullName:
						{ // [JsonIgnore] or [JsonIgnore(Condition = ...)]
							// JsonIgnoreCondition values: 0 = Never, 1 = Always, 2 = WhenWritingDefault, 3 = WhenWritingNull
							// (the attribute's Condition property defaults to Always when not specified)
							int condition = 1;
							foreach (var kv in attribute.NamedArguments)
							{
								if (kv.Key == "Condition" && kv.Value.Value is int n)
								{
									condition = n;
								}
							}
							switch (condition)
							{
								case 0: ignoreCondition = "Never"; break;
								case 2: ignoreCondition = "WhenWritingDefault"; break;
								case 3: ignoreCondition = "WhenWritingNull"; break;
								default: return default; // Always: excluded from both serialization and deserialization
							}
							break;
						}
						case KnownTypeSymbols.JsonPropertyAttributeFullName:
						{ // [JsonProperty("fooBar", ...)]
							if (attribute.ConstructorArguments.Length > 0)
							{
								name = (string) attribute.ConstructorArguments[0].Value!;
							}

							foreach(var kv in attribute.NamedArguments)
							{
								if (kv.Key == "DefaultValue")
								{
									hasNonZeroDefault = true; // BUGBUG: detect if value is still the default for this type??
									if (type.IsPrimitive)
									{
										defaultLiteral = kv.Value.ToCSharpString();
									}
									else
									{
										defaultLiteral = $"({type.FullyQualifiedName}) {kv.Value.ToCSharpString()}";
									}
								}
								else if (kv.Key == "EnumFormat" && kv.Value.Value is int ef)
								{ // JsonEnumFormat: 0 = Inherits, 1 = Number, 2 = String
									enumFormat = ef switch { 1 => "Number", 2 => "String", _ => null };
								}
							}
							//TODO: check if a default value was provided!
							break;
						}
						case JsonPropertyNameAttributeFullName:
						{ // [JsonPropertyName("fooBar")]
							if (attribute.ConstructorArguments.Length > 0)
							{
								name = (string) attribute.ConstructorArguments[0].Value!;
								stjPropertyName = name;
							}
							break;
						}
						case DataMemberAttributeFullName:
						{ // [DataMember(Name = "fooBar", IsRequired = true, Order = 3, EmitDefaultValue = false)]
							// Order= and EmitDefaultValue= are read for the XML wire, which honours both; the JSON side
							// keeps ignoring them, exactly as the reflection path does.
							foreach (var kv in attribute.NamedArguments)
							{
								if (kv.Key == "Name" && kv.Value.Value is string dmName)
								{
									dataMemberName = dmName;
								}
								else if (kv.Key == "IsRequired" && kv.Value.Value is bool dmRequired)
								{
									dataMemberIsRequired = dmRequired;
								}
								else if (kv.Key == "Order" && kv.Value.Value is int dmOrder)
								{ // a negative order means "unordered" to DataContractSerializer, and unordered is a different rule from ordered-at-zero
									dataMemberOrder = dmOrder >= 0 ? dmOrder : null;
								}
								else if (kv.Key == "EmitDefaultValue" && kv.Value.Value is bool dmEmitDefault)
								{
									emitDefaultValue = dmEmitDefault;
								}
							}
							break;
						}
						case KnownTypeSymbols.XmlPropertyAttributeFullName:
						{ // [XmlProperty("@id")], [XmlProperty(Name = "tags", ItemName = "tag", Attribute = false, DictionaryFormat = ...)]
							// captured RAW here; the '@' sugar, the name validation and the refusals resolve below, once the
							// whole attribute list has been read (and only if the container actually produces XML)
							hasXmlProperty = true;
							if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string xmlCtorName)
							{
								xmlRawName = xmlCtorName;
							}
							foreach (var kv in attribute.NamedArguments)
							{
								switch (kv.Key)
								{
									case "Name":
									{
										if (kv.Value.Value is string xmlNamedName) xmlRawName = xmlNamedName;
										break;
									}
									case "Attribute":
									{
										if (kv.Value.Value is bool xmlAttr)
										{ // remember that it was SPELLED: 'Attribute = false' next to the '@' sugar is a contradiction, while an absent one is not
											xmlAttributeSpelled = true;
											xmlAttributeValue = xmlAttr;
										}
										break;
									}
									case "ItemName":
									{
										if (kv.Value.Value is string xmlItem) xmlItemName = xmlItem;
										break;
									}
									case "DictionaryFormat":
									{
										xmlDictionaryFormat = GetEnumMemberName(kv.Value);
										break;
									}
								}
							}
							break;
						}
						case NewtonsoftJsonPropertyAttributeFullName:
						{ // Newtonsoft [JsonProperty("fooBar")]: not honoured, only observed for conflict detection
							if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string njName)
							{
								newtonsoftPropertyName = njName;
							}
							foreach (var kv in attribute.NamedArguments)
							{
								if (kv.Key == "PropertyName" && kv.Value.Value is string njName2)
								{
									newtonsoftPropertyName = njName2;
								}
							}
							break;
						}
						case KeyAttributeFullName:
						{
							isKey = true;
							break;
						}
						case JsonConvertWithAttributeFullName:
						{ // the native [JsonConvertWith(typeof(...))]: wins over every other converter signal, and an
							// invalid converter type is a build ERROR (our own attribute has no legacy meaning to preserve)
							if (attribute.ConstructorArguments.Length > 0
								&& attribute.ConstructorArguments[0].Value is INamedTypeSymbol nativeSymbol)
							{
								var facets = GetConverterFacets(nativeSymbol, typeSymbol);
								if (facets.Packer || facets.Deserializer)
								{
									nativeConverterType = nativeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
									nativeConverterSymbol = nativeSymbol;
									nativeConverterHasPacker = facets.Packer;
									nativeConverterHasDeserializer = facets.Deserializer;
									nativeConverterIsNullableForm = facets.NullableForm;
								}
								else
								{
									ReportDiagnostic(
										new(
											"CJSON0010",
											"[JsonConvertWith] names a type that is not a valid CrystalJson converter",
											"The member '{0}' carries [JsonConvertWith(typeof({1}))], but '{1}' implements neither IJsonPacker<T> nor IJsonDeserializer<T> for the member's type.",
											"SnowBank.Serialization.Json.CodeGen",
											DiagnosticSeverity.Error,
											isEnabledByDefault: true
										),
										member.Locations.Length > 0 ? member.Locations[0] : null,
										member.ToDisplayString(), nativeSymbol.Name);
								}
							}
							break;
						}
						case JsonConverterAttributeFullName:
						case NewtonsoftJsonConverterAttributeFullName:
						{ // [JsonConverter(typeof(...))], either spelling
							// honored for whichever facet(s) the named type implements for the member's type;
							// a type with neither facet (e.g. a real STJ or Newtonsoft converter) is ignored, same as the reflection path
							if (attribute.ConstructorArguments.Length > 0
								&& attribute.ConstructorArguments[0].Value is INamedTypeSymbol converterSymbol)
							{
								var facets = GetConverterFacets(converterSymbol, typeSymbol);
								if (facets.Packer || facets.Deserializer)
								{
									customConverterType = converterSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
									customConverterSymbol = converterSymbol;
									customConverterArgs = "";
									customConverterHasPacker = facets.Packer;
									customConverterHasDeserializer = facets.Deserializer;
									customConverterIsNullableForm = facets.NullableForm;
								}
							}
							break;
						}
						case JsonBooleanLiteralsAttributeFullName:
						{ // [JsonBooleanLiterals("0", "1")] or [JsonBooleanLiterals(0, 1)], with optional StrictLiterals
							if (GetUnderlyingValueType(typeSymbol) is not { SpecialType: SpecialType.System_Boolean })
							{
								ReportDiagnostic(
									new(
										"CJSON0009",
										"[JsonBooleanLiterals] can only be applied to boolean members",
										"The member '{0}' carries [JsonBooleanLiterals] but is not of type bool or bool?: the attribute is ignored.",
										"SnowBank.Serialization.Json.CodeGen",
										DiagnosticSeverity.Error,
										isEnabledByDefault: true
									),
									member.Locations.Length > 0 ? member.Locations[0] : null,
									member.ToDisplayString());
								break;
							}
							if (attribute.ConstructorArguments.Length == 2)
							{
								// the arguments are declared as `object`, so the COMPILER no longer type-checks them at the
								// callsite. Without this the change would be a net safety regression for generated
								// containers, which used to get a compile error for a bad literal type.
								if (!ValidateBooleanLiteral(member, attribute.ConstructorArguments[0], "whenFalse")
								 || !ValidateBooleanLiteral(member, attribute.ConstructorArguments[1], "whenTrue"))
								{
									break;
								}

								bool strict = false;
								foreach (var kv in attribute.NamedArguments)
								{
									if (kv.Key == "StrictLiterals" && kv.Value.Value is bool b) strict = b;
								}

								if (strict && attribute.ConstructorArguments[0].IsNull)
								{ // both arguments are known at compile time, so the contradiction can be pointed out where it is written
									ReportDiagnostic(
										new(
											"CJSON0018",
											"StrictLiterals has nothing to enforce when there is no false literal",
											"The member '{0}' combines StrictLiterals with a null false literal. Strict mode rejects genuine JSON true/false in favour of the configured literals, but with no false literal there is nothing on the false side to enforce: absence is what carries false. Remove StrictLiterals, or give the member a real false literal.",
											"SnowBank.Serialization.Json.CodeGen",
											DiagnosticSeverity.Warning,
											isEnabledByDefault: true
										),
										member.Locations.Length > 0 ? member.Locations[0] : null,
										member.ToDisplayString());
								}

								customConverterType = "global::SnowBank.Data.Json.JsonBooleanLiteralsConverter";
								customConverterArgs = $"{attribute.ConstructorArguments[0].ToCSharpString()}, {attribute.ConstructorArguments[1].ToCSharpString()}{(strict ? ", strictLiterals: true" : "")}";

								if (attribute.ConstructorArguments[0].IsNull)
								{ // "do not emit for false" is the same rule as [JsonIgnore(Condition = WhenWritingDefault)], resolved here so the writer side needs no special case
									ignoreCondition ??= "WhenWritingDefault";
								}
							}
							break;
						}
						//TODO: any other argument for setting a default value?
					}
				}

				if (dataMemberName is not null)
				{
					// one member giving DIFFERENT wire names to different serializers is two contracts on one type
					// (ex: [DataMember(Name="code")] for the legacy wire plus [JsonProperty("ACTIF")] for another
					// consumer): report a build error instead of silently picking one; the fix is to split the DTO
					foreach (var (foreignName, foreignFamily) in new[] { (stjPropertyName, "JsonPropertyName"), (newtonsoftPropertyName, "JsonProperty") })
					{
						if (foreignName is not null && foreignName != dataMemberName)
						{
							ReportDiagnostic(
								new(
									"CJSON0011",
									"A member declares two different wire names for two different serializers",
									"The member '{0}' declares two different wire names: [DataMember(Name=\"{1}\")] and [{2}(\"{3}\")]. One type cannot serve two wire contracts at once: split it into one DTO per serializer, each carrying a single naming attribute.",
									"SnowBank.Serialization.Json.CodeGen",
									DiagnosticSeverity.Error,
									isEnabledByDefault: true
								),
								member.Locations.Length > 0 ? member.Locations[0] : null,
								member.ToDisplayString(), dataMemberName, foreignFamily, foreignName);
						}
					}
				}

				if (nativeConverterType != null)
				{ // the native attribute wins over [JsonBooleanLiterals] and the foreign [JsonConverter] spellings
					customConverterType = nativeConverterType;
					customConverterSymbol = nativeConverterSymbol;
					customConverterArgs = "";
					customConverterHasPacker = nativeConverterHasPacker;
					customConverterHasDeserializer = nativeConverterHasDeserializer;
					customConverterIsNullableForm = nativeConverterIsNullableForm;
				}

				if (dataContractMember && dataMemberName is not null)
				{ // on a [DataContract] type the DataMember rename wins, as it does on the reflection path (attr.Name ?? name)
					name = dataMemberName;
				}

				if (string.IsNullOrEmpty(name))
				{
					name = FormatName(memberName, namingPolicy);
				}

				string? xmlName = null;
				bool xmlIsAttribute = false;
				bool customConverterHasXmlSerializer = false;
				bool customConverterXmlFacetDeclaredForNullable = false;
				if (xmlProfile is null)
				{ // the container produces no XML: the whole member-level vocabulary is inert, diagnostics included
					xmlItemName = null;
					xmlDictionaryFormat = null;
				}
				else
				{
					bool xmlRefused = false;
					if (hasXmlProperty)
					{
						(xmlName, xmlIsAttribute, xmlItemName, xmlDictionaryFormat) = ResolveXmlMember(member, type, xmlProfile, xmlRawName, xmlAttributeSpelled, xmlAttributeValue, xmlItemName, xmlDictionaryFormat, out xmlRefused);
					}

					// structural, so it applies to every member of the MODERN wire, annotated or not; skipped for a member
					// whose settings were already refused, so that one member never collects two stacked errors
					if (!xmlRefused && xmlProfile == XmlProfileModern)
					{
						ReportBareNestedCollection(member, type);
						ReportUnprojectableDictionaryValue(member, type, xmlDictionaryFormat);
					}

					if (!xmlRefused && xmlProfile == XmlProfileDataContract)
					{ // structural, and compat-only: the modern wire never reads [CollectionDataContract] in the first place
						ReportUnsupportedCollectionDataContract(member, typeSymbol);
					}

					if (customConverterType is not null)
					{ // the converter took the member's wire form over; on an XML container it has to answer for BOTH wires
						(customConverterHasXmlSerializer, customConverterXmlFacetDeclaredForNullable) = GetXmlConverterFacet(customConverterSymbol, typeSymbol);
						if (xmlIsAttribute)
						{ // no converter can answer for an ATTRIBUTE, facet or not: reported instead of the missing-facet rule,
						  // because one member gets one error, and fixing the facet would not make this shape work
							ReportConvertedAttributeMember(member, customConverterType);
						}
						else if (!customConverterHasXmlSerializer)
						{
							ReportMissingXmlConverterFacet(member, type, customConverterType);
						}
					}
				}

				return (
					new()
					{
						Type = type,
						Name = name!,
						MemberName = memberName,
						XmlName = xmlName,
						XmlIsAttribute = xmlIsAttribute,
						XmlItemName = xmlItemName,
						XmlDictionaryFormat = xmlDictionaryFormat,
						DataMemberOrder = dataMemberOrder,
						EmitDefaultValue = emitDefaultValue,
#if FULL_DEBUG
						Attributes = attributes,
#endif
						IsField = isField,
						IsReadOnly = isReadOnly,
						IsInitOnly = isInitOnly,
						IsRequired = isRequired,
						IsRequiredPresence = dataContractMember && dataMemberIsRequired,
						IsNotNull = isNotNull,
						IsKey = isKey,
						HasNonZeroDefault = hasNonZeroDefault,
						DefaultLiteral = defaultLiteral,
						IgnoreCondition = ignoreCondition,
						EnumFormat = enumFormat,
						CustomConverterType = customConverterType,
						CustomConverterArgs = customConverterArgs,
						CustomConverterHasPacker = customConverterHasPacker,
						CustomConverterHasDeserializer = customConverterHasDeserializer,
						CustomConverterIsNullableForm = customConverterIsNullableForm,
						CustomConverterHasXmlSerializer = customConverterHasXmlSerializer,
						CustomConverterXmlFacetDeclaredForNullable = customConverterXmlFacetDeclaredForNullable,
						IsNonPublic = isNonPublic,
						HasNonPublicGetter = hasNonPublicGetter,
						HasNonPublicSetter = hasNonPublicSetter,
					},
					typeSymbol
				);
			}

			private static string GetDefaultLiteral(TypeMetadata type) => type.SpecialType switch
			{
				SpecialType.System_Boolean => "false",
				SpecialType.System_Char => "'\0'",
				SpecialType.System_SByte => "default(sbyte)",
				SpecialType.System_Byte => "default(byte)",
				SpecialType.System_Int16 => "default(short)",
				SpecialType.System_UInt16 => "default(ushort)",
				SpecialType.System_Int32 => "0",
				SpecialType.System_UInt32 => "0U",
				SpecialType.System_Int64 => "0L",
				SpecialType.System_UInt64 => "0UL",
				SpecialType.System_Decimal => "0m",
				SpecialType.System_Single => "0f",
				SpecialType.System_Double => "0d",
				SpecialType.System_String => "null",
				SpecialType.System_IntPtr => "global::System.IntPtr.Zero",
				SpecialType.System_UIntPtr => "global::System.UIntPtr.Zero",
				SpecialType.System_DateTime => "global::System.DateTime.MinValue",
				SpecialType.System_Enum => "0",
				_ => !type.IsValueType() || type.IsNullableOfT() ? "null" : "default"
			};

			/// <summary>Unwraps <c>Nullable&lt;T&gt;</c>: returns <c>T</c> for a <c>T?</c> member, the type itself otherwise</summary>
			private static ITypeSymbol GetUnderlyingValueType(ITypeSymbol type)
			{
				return type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments.Length: 1 } named
					? named.TypeArguments[0]
					: type;
			}

			/// <summary>Returns which of <c>IJsonPacker&lt;T&gt;</c> / <c>IJsonDeserializer&lt;T&gt;</c> a converter type implements for the member's type: the EXACT form first (a converter declared for <c>T?</c> takes responsibility for the nullable case itself), then the nullable-unwrapped lift - the same probe order as the reflection bridge</summary>
			/// <remarks>Recognition is per facet: a converter for a type that is only ever written (or only ever read) may implement a single facet. <c>NullableForm</c> is <see langword="true"/> when the exact probe matched on a <c>Nullable&lt;T&gt;</c> member, which changes which unpack helper the emitter calls.</remarks>
			private static (bool Packer, bool Deserializer, bool NullableForm) GetConverterFacets(INamedTypeSymbol converterType, ITypeSymbol memberType)
			{
				var underlying = GetUnderlyingValueType(memberType);
				if (!ReferenceEquals(underlying, memberType))
				{ // Nullable<T> member: probe the exact T? form first, with precedence over the lift
					var exact = GetConverterFacetsFor(converterType, memberType);
					if (exact.Packer || exact.Deserializer)
					{
						return (exact.Packer, exact.Deserializer, NullableForm: true);
					}
				}
				var lifted = GetConverterFacetsFor(converterType, underlying);
				return (lifted.Packer, lifted.Deserializer, NullableForm: false);
			}

			private static (bool Packer, bool Deserializer) GetConverterFacetsFor(INamedTypeSymbol converterType, ITypeSymbol valueType)
			{
				bool packer = false, deserializer = false;
				foreach (var iface in converterType.AllInterfaces)
				{
					if (iface.TypeArguments.Length != 1 || !SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], valueType)) continue;
					if (iface.ContainingNamespace?.ToDisplayString() != "SnowBank.Data.Json") continue;
					switch (iface.Name)
					{
						case "IJsonPacker": packer = true; break;
						case "IJsonDeserializer": deserializer = true; break;
					}
					if (packer && deserializer) break;
				}
				return (packer, deserializer);
			}

			private static INamedTypeSymbol[] GetTypeHierarchy(ITypeSymbol type)
			{
				if (type is not INamedTypeSymbol namedType)
				{
					return [ ];
				}

				if (type.TypeKind != TypeKind.Interface)
				{
					var list = new List<INamedTypeSymbol>();
					for (INamedTypeSymbol? current = namedType; current != null; current = current.BaseType)
					{
						list.Add(current);
					}
					list.Reverse();
					return list.ToArray();
				}
				else
				{
					return [ namedType ];
				}
			}
		}

	}

}
