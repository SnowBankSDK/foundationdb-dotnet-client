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

			/// <summary>Table of known symbols from this compilation</summary>
			private KnownTypeSymbols KnownSymbols { get; }

			public List<DiagnosticInfo> Diagnostics { get; } = [ ];
			
			private Location? ContextClassLocation { get; set; }

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
										wireProfile = "DataContractCompat";
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

				Kenobi($"Found {work.Count} root types to include");

				CrawlIncludedTypes(work, mappedTypes, includedTypes, propertyNamingPolicy);

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

				this.ContextClassLocation = null;

				return new()
				{
					Name = containerName,
					Type = TypeMetadata.Create(symbol),
					IncludedTypes = includedTypes.ToImmutableEquatableArray(),
					PropertyNameCaseInsensitive = caseInsensitiveNames,
					PropertyNamingPolicy = propertyNamingPolicy,
					SupportsUnsafeAccessors = this.KnownSymbols.HasUnsafeAccessor,
					WireProfile = wireProfile,
				};
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

				var includedTypes = new List<CrystalJsonTypeMetadata>();
				var mappedTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { symbol };

				Queue<INamedTypeSymbol> work = [];
				work.Enqueue(symbol);

				CrawlIncludedTypes(work, mappedTypes, includedTypes, propertyNamingPolicy: null);

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
			private void CrawlIncludedTypes(Queue<INamedTypeSymbol> work, HashSet<INamedTypeSymbol> mappedTypes, List<CrystalJsonTypeMetadata> includedTypes, string? propertyNamingPolicy)
			{
				while(work.Count > 0)
				{
					var type = work.Dequeue();

					Kenobi($"Inspect type {type}");
					try
					{
						var typeDef = ParseTypeMetadata(type, mappedTypes, work, propertyNamingPolicy);
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

			public CrystalJsonTypeMetadata? ParseTypeMetadata(INamedTypeSymbol type, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work, string? namingPolicy)
			{
				// we have to extract all the properties that will be required later during the code generation phase

				bool isPolymorphic = false;
				string? typeDiscriminatorPropertyName = null;
				List<(INamedTypeSymbol, TypeMetadata, object?)>? derivedTypes = null;

				var members = new List<CrystalJsonMemberMetadata>();
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
				bool hasDataContract = HasDataContractAttribute(type);

				var callbacks = ParseSerializationCallbacks(type);

				// if this is a derived type, we need to enumerate the symbols starting from the top (interface or base class)
				// we also want to have "id" as the first member
				int indexOfId = -1;
				foreach (var current in GetTypeHierarchy(type))
				{
					foreach (var member in current.GetMembers())
					{
						if (member.Kind is (SymbolKind.Property or SymbolKind.Field or SymbolKind.Method))
						{
							Kenobi($"Inspecting member {member.Name}...");
							var (memberDef, memberType) = ParseMemberMetadata(member, mappedTypes, work, namingPolicy, hasDataContract);
							if (memberDef is not null)
							{
								Kenobi($"Inspected member {member.Name} with type {memberDef.Type.FullName}, N={memberDef.Type.NullableOfType?.FullName}, E={memberDef.Type.ElementType?.FullName}, K={memberDef.Type.KeyType?.FullName}, V={memberDef.Type.ValueType?.FullName}");
								if (member.Name == "Id")
								{
									indexOfId = members.Count;
								}
								members.Add(memberDef);
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

				return new()
				{
					Type = TypeMetadata.Create(type),
					Members = members.ToImmutableEquatableArray(),
					IsPolymorphicRoot = isPolymorphic,
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

			private static string FormatName(string name, string? policy)
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

			private static bool HasDataContractAttribute(ISymbol type)
			{
				foreach (var attribute in type.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() == DataContractAttributeFullName)
					{
						return true;
					}
				}
				return false;
			}

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

			public (CrystalJsonMemberMetadata? Metadata, ITypeSymbol Type) ParseMemberMetadata(ISymbol member, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work, string? namingPolicy, bool hasDataContract)
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
				string? nativeConverterType = null;
				bool nativeConverterHasPacker = true;
				bool nativeConverterHasDeserializer = true;
				bool nativeConverterIsNullableForm = false;

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
						{ // [DataMember(Name = "fooBar", IsRequired = true)]. Note Order= and EmitDefaultValue= are deliberately
							// NOT read: the reflection path ignores both, and matching it is the acceptance bar for this type.
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
				
				return (
					new()
					{
						Type = type,
						Name = name!,
						MemberName = memberName,
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
