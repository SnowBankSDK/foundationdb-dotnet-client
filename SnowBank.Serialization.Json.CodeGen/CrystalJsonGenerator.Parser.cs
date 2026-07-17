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

			private const string RequiredMemberAttributeFullName = "System.Runtime.CompilerServices.RequiredMemberAttribute";

			private const string KeyAttributeFullName = "System.ComponentModel.DataAnnotations.KeyAttribute";

			public const string JsonPropertyNameAttributeFullName = "System.Text.Json.Serialization.JsonPropertyNameAttribute";

			public const string JsonIgnoreAttributeFullName = "System.Text.Json.Serialization.JsonIgnoreAttribute";

			public const string JsonIncludeAttributeFullName = "System.Text.Json.Serialization.JsonIncludeAttribute";

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
					return null;
				}

				var converterAttribute = attributes[0];
				//TODO: extract some settings from this?

				bool caseInsensitiveNames = false;
				string? propertyNamingPolicy = null;

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
								switch (defaults)
								{
									case 1: // CrystalJsonSerializerDefaults.Web
									{
										caseInsensitiveNames = true;
										propertyNamingPolicy = "camel";
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

				Kenobi($"Found {work.Count} root types to include");

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
				};
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
							var (memberDef, memberType) = ParseMemberMetadata(member, mappedTypes, work, namingPolicy);
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

				return new()
				{
					Type = TypeMetadata.Create(type),
					Members = members.ToImmutableEquatableArray(),
					IsPolymorphicRoot = isPolymorphic,
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
				if (metadata.JsonType is not JsonPrimitiveType.None) return false;
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

			/// <summary>Warns when a non-public member carries <c>[JsonInclude]</c>, which the generator does not support yet (the reflection path does)</summary>
			private void ReportJsonIncludeNotSupported(ISymbol member)
			{
				foreach (var attribute in member.GetAttributes())
				{
					if (attribute.AttributeClass?.ToDisplayString() == JsonIncludeAttributeFullName)
					{
						ReportDiagnostic(
							new(
								"SYSLIB1038", //note: we use the same ID as System.Text.Json, since this is the same situation
								"The member annotated with JsonIncludeAttribute is not supported by the source generator.",
								"The member '{0}' has been annotated with JsonIncludeAttribute, but the source generator does not support non-public members yet: it will NOT be part of the generated converter (the reflection path does honor it).",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Warning,
								isEnabledByDefault: true
							),
							member.Locations.Length > 0 ? member.Locations[0] : null,
							member.ToDisplayString());
						return;
					}
				}
			}

			public (CrystalJsonMemberMetadata? Metadata, ITypeSymbol Type) ParseMemberMetadata(ISymbol member, HashSet<INamedTypeSymbol> mappedTypes, Queue<INamedTypeSymbol> work, string? namingPolicy)
			{
				var memberName = member.Name;
				bool isField;
				ITypeSymbol typeSymbol;
				bool isReadOnly;
				bool isInitOnly;
				bool isRequired;

				switch (member)
				{
					case IPropertySymbol property:
					{
						if (property.IsImplicitlyDeclared || property.DeclaredAccessibility is (Accessibility.Private or Accessibility.Protected))
						{
							// REVIEW: what should we do with private properties?
							// - if they have a backing field, the object may not incomplete when deserialized
							if (!property.IsImplicitlyDeclared)
							{
								ReportJsonIncludeNotSupported(property);
							}
							return default;
						}
						isField = false;
						typeSymbol = property.Type;
						isReadOnly = property.IsReadOnly;
						isRequired = property.IsRequired;

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
						}

						break;
					}
					case IFieldSymbol field:
					{
						if (field.IsConst)
						{ // do not include constants
							return default;
						}
						if (field.IsImplicitlyDeclared || field.DeclaredAccessibility is (Accessibility.Private or Accessibility.Protected))
						{
							//note: we see the backing fields here, we could maybe capture them somewhere in order to generate optimized unsafe accessors?return null;
							if (!field.IsImplicitlyDeclared)
							{
								ReportJsonIncludeNotSupported(field);
							}
							return default;
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

				// parameters that can be modified via attributes or keywords on the member
				string? name = null;
				bool isKey = false;
				string? ignoreCondition = null;

				string defaultLiteral = GetDefaultLiteral(type);
				bool hasNonZeroDefault = false;

				foreach (var attribute in memberAttributes)
				{
					var attributeType = attribute.AttributeClass;
					if (attributeType is null) continue;

					switch (attributeType.ToDisplayString())
					{
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
							}
							//TODO: check if a default value was provided!
							break;
						}
						case JsonPropertyNameAttributeFullName:
						{ // [JsonPropertyName("fooBar")]
							if (attribute.ConstructorArguments.Length > 0)
							{
								name = (string) attribute.ConstructorArguments[0].Value!;
							}
							break;
						}
						case KeyAttributeFullName:
						{
							isKey = true;
							break;
						}
						//TODO: any other argument for setting a default value?
					}
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
						IsNotNull = isNotNull,
						IsKey = isKey,
						HasNonZeroDefault = hasNonZeroDefault,
						DefaultLiteral = defaultLiteral,
						IgnoreCondition = ignoreCondition,
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
				SpecialType.System_IntPtr => "IntPtr.Zero",
				SpecialType.System_UIntPtr => "UIntPtr.Zero",
				SpecialType.System_DateTime => "DateTime.MinValue",
				SpecialType.System_Enum => "0",
				_ => !type.IsValueType() || type.IsNullableOfT() ? "null" : "default"
			};

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
