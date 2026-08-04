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
	using System.Text;
	using Microsoft.CodeAnalysis;
	
	public partial class CrystalJsonSourceGenerator
	{

		[SuppressMessage("ReSharper", "InconsistentNaming")]
		internal sealed partial class Emitter
		{

			#region Attributes Names ...

			private const string DebuggerNonUserCodeAttributeFullName = "global::System.Diagnostics.DebuggerNonUserCodeAttribute";

			private const string DisallowNullAttributeFullName = "global::System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

			private const string DoesNotReturnAttributeFullName = "global::System.Diagnostics.CodeAnalysis.DoesNotReturnAttribute";

			private const string DynamicallyAccessedMembersAttributeFullName = "global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute";

			private const string DynamicallyAccessedMemberTypesFullName = "global::System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes";

			private const string GeneratedCodeAttributeFullName = "global::System.CodeDom.Compiler.GeneratedCodeAttribute";

			private const string ExcludeFromCodeCoverageAttributeFullName = "global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute";

			private const string MaybeNullWhenAttributeFullName = "global::System.Diagnostics.CodeAnalysis.MaybeNullWhenAttribute";

			private const string NotNullIfNotNullAttributeFullName = "global::System.Diagnostics.CodeAnalysis.NotNullIfNotNullAttribute";

			private const string DictionaryFullName = "global::System.Collections.Generic.Dictionary";

			private const string FrozenDictionaryFullName = "global::System.Collections.Frozen.FrozenDictionary";

			private const string EditorBrowsableAttributeFullName = "global::System.ComponentModel.EditorBrowsableAttribute";

			private const string EditorBrowsableStateFullName = "global::System.ComponentModel.EditorBrowsableState";

			// BCL type names referenced by emitted bodies: the generated files carry no `using` directive, so every
			// name outside the consumer's own namespace must be fully qualified or the code only compiles when the
			// project happens to supply matching global usings (e.g. ImplicitUsings)
			private const string SystemTypeFullName = "global::System.Type";

			private const string ActionFullName = "global::System.Action";

			private const string ReadOnlyMemoryOfCharFullName = "global::System.ReadOnlyMemory<char>";

			private const string IndexFullName = "global::System.Index";

			private const string NotSupportedExceptionFullName = "global::System.NotSupportedException";

			private const string ArgumentNullExceptionFullName = "global::System.ArgumentNullException";

			private const string UnsafeFullName = "global::System.Runtime.CompilerServices.Unsafe";

			#endregion

			private SourceProductionContext Context { get; }

			private CrystalJsonContainerMetadata Metadata { get; }

			private Dictionary<TypeRef, CrystalJsonTypeMetadata> TypeMap { get; }

			private Dictionary<TypeRef, (CrystalJsonTypeMetadata Parent, object? Discriminator)> PolymorphicMap { get; }

			public Emitter(SourceProductionContext ctx, CrystalJsonContainerMetadata metadata)
			{
				this.Context = ctx;
				this.Metadata = metadata;

				var map = new Dictionary<TypeRef, CrystalJsonTypeMetadata>();
				foreach (var type in metadata.IncludedTypes)
				{
					map[type.Type.Ref] = type;
				}
				this.TypeMap = map;

				// build the set of derived types to their "base" type
				var polymorphicMap = new Dictionary<TypeRef, (CrystalJsonTypeMetadata Parent, object? Discriminator)>();
				foreach (var type in map.Values)
				{
					if (type.DerivedTypes.Count == 0) continue;
					foreach (var (_, derivedType, discriminator) in type.DerivedTypes)
					{
						polymorphicMap[derivedType.Ref] = (type, discriminator);
					}
				}

				this.PolymorphicMap = polymorphicMap;
			}

			#region Sample member (documentation only)...

			// The "How to use" blocks of the generated proxy helpers quote ONE member of the type as an example. A type with
			// no serialized member at all is legal (an ISerializable wrapper on the DataContract XML wire is exactly that),
			// and indexing the member list for those comments used to crash the whole emission for such a type. The three
			// helpers below answer with a placeholder instead: the sample is documentation, and documentation must never be
			// the thing that decides whether a type can be generated.

			/// <summary>Name of the member the generated documentation quotes as an example, or a placeholder when the type has none</summary>
			private static string GetSampleMemberName(CrystalJsonTypeMetadata typeDef) => typeDef.Members.Count > 0 ? typeDef.Members[0].MemberName : "SomeMember";

			/// <summary>Wire name of that same example member</summary>
			private static string GetSampleMemberWireName(CrystalJsonTypeMetadata typeDef) => typeDef.Members.Count > 0 ? typeDef.Members[0].Name : "someMember";

			/// <summary>Type of that same example member</summary>
			private static string GetSampleMemberType(CrystalJsonTypeMetadata typeDef) => typeDef.Members.Count > 0 ? typeDef.Members[0].Type.FullyQualifiedName : "global::System.Object";

			#endregion

			/// <summary>Returns the member's default-value literal, ready to embed in a value position of the generated code</summary>
			/// <remarks>The generated files force <c>#nullable enable</c> on themselves while the member's declared type may be non-nullable or oblivious, so a bare <c>null</c> default must be null-forgiving (<c>null!</c>): the <c>[NotNullIfNotNull]</c> contract on the <c>Get(name, defaultValue)</c> family then keeps the returned flow-state aligned with the declared member type.</remarks>
			private static string GetForgivingDefaultLiteral(CrystalJsonMemberMetadata member) => member.DefaultLiteral == "null" ? "null!" : member.DefaultLiteral;

			private static void AddFileHeaders(CSharpCodeBuilder sb)
			{
				sb.Comment("<auto-generated/>");
				sb.NewLine();
				sb.AppendLine("#nullable enable annotations");
				sb.AppendLine("#nullable enable warnings");
				sb.AppendLine("#pragma warning disable CS0612, CS0618");
				sb.NewLine();
			}

			public void GenerateCode()
			{
				this.Context.CancellationToken.ThrowIfCancellationRequested();

				var symbol = this.Metadata.Type;
				var includedTypes = this.Metadata.IncludedTypes;
				
				Kenobi($"Generating container {symbol.Name} with {includedTypes.Count} included types");
				Kenobi($"Name: '{symbol.FullyQualifiedName}'");
				Kenobi($"Types: {includedTypes.Count}");

				// first we generated a "primary" file for the container, that will include any static methods (that are not specific to a type)

				{
					var sb = new CSharpCodeBuilder();
					AddFileHeaders(sb);

					sb.AppendLine($"namespace {symbol.NameSpace}");
					sb.EnterBlock("namespace");

					if (!this.Metadata.IsSelfContained)
					{
						// note: in self mode, no XML doc or attribute is emitted on the entity's partial (they would apply
						// to the whole entity type, which is user code); they go on the nested Json scope instead
						sb.XmlComment("<summary>Generated source code for JSON operations on application types</summary>");
						sb.AppendLine($"[{DynamicallyAccessedMembersAttributeFullName}({DynamicallyAccessedMemberTypesFullName}.All)]");
						sb.AppendLine($"[{GeneratedCodeAttributeFullName}(\"{nameof(CrystalJsonSourceGenerator)}\", \"0.1\")]");
						sb.AppendLine($"[{DebuggerNonUserCodeAttributeFullName}]");
						sb.AppendLine($"[{ExcludeFromCodeCoverageAttributeFullName}]");
					}
					sb.AppendLine(GetContainerDeclaration());
					sb.EnterBlock("container");
					sb.NewLine();

					OpenSelfScope(sb, emitAttributes: true);

					sb.XmlComment($"<summary>Returns a <see cref=\"{KnownTypeSymbols.ICrystalJsonTypeResolverFullName}\">resolver</see> that exposes all the generated converters in this container</summary>");
					sb.AppendLine($"public static {KnownTypeSymbols.ICrystalJsonTypeResolverFullName} GetResolver() => TypeMapper.Default;");
					sb.NewLine();

					// TypeMapper
					sb.XmlComment("<summary>Mapper that bundles all the types that are managed by this custom serializer context</summary>");
					sb.AppendLine($"internal sealed class TypeMapper : {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}");
					sb.EnterBlock("TypeMapper");
					{
						sb.NewLine();

						sb.XmlComment("<summary>Default mapper for all types in this container</summary>");
						sb.AppendLine("public static readonly TypeMapper Default = new();");
						sb.NewLine();

						sb.AppendLine($"private {FrozenDictionaryFullName}<{SystemTypeFullName}, {KnownTypeSymbols.IJsonConverterInterfaceFullName}> ConvertersByType {{ get; }}");
						sb.NewLine();
						sb.AppendLine($"private {FrozenDictionaryFullName}<{SystemTypeFullName}, {KnownTypeSymbols.IJsonConverterInterfaceFullName}> ConvertersByTypeExtended {{ get; }}");
						sb.NewLine();

						// ctor()
						sb.InheritDoc();
						sb.AppendLine("private TypeMapper()");
						sb.EnterBlock("ctor");
						// map of all application types
						sb.AppendLine($"var map = new {DictionaryFullName}<{SystemTypeFullName}, {KnownTypeSymbols.IJsonConverterInterfaceFullName}>();");
						foreach (var type in includedTypes)
						{
							sb.AppendLine($"map[typeof({type.Type.FullyQualifiedName})] = {GetLocalSerializerRef(type)};");
						}
						sb.AppendLine($"this.ConvertersByType = {FrozenDictionaryFullName}.ToFrozenDictionary(map);");
						// extended maps that also includes the generated proxies
						foreach (var type in includedTypes)
						{
							sb.AppendLine($"map[typeof({GetReadOnlyProxyName(type.Type)})] = {GetLocalSerializerRef(type)};");
							sb.AppendLine($"map[typeof({GetWritableProxyName(type.Type)})] = {GetLocalSerializerRef(type)};");
						}
						sb.AppendLine($"this.ConvertersByTypeExtended = {FrozenDictionaryFullName}.ToFrozenDictionary(map);");
						sb.LeaveBlock("ctor");
						sb.NewLine();

						// TryGetConverterFor(Type)
						sb.InheritDoc();
						sb.AppendLine($"public bool TryGetConverterFor({SystemTypeFullName} type, [{MaybeNullWhenAttributeFullName}(false)] out {KnownTypeSymbols.IJsonConverterInterfaceFullName} converter)");
						sb.EnterBlock();
						{
							sb.AppendLine("return this.ConvertersByTypeExtended.TryGetValue(type, out converter);");
						}
						sb.LeaveBlock();
						sb.NewLine();

						// TryGetConverterFor<T>()
						sb.InheritDoc();
						sb.AppendLine($"public bool TryGetConverterFor<T>([{MaybeNullWhenAttributeFullName}(false)] out {KnownTypeSymbols.IJsonConverterInterfaceFullName}<T> converter)");
						sb.EnterBlock();
						{
							sb.AppendLine("if (!this.ConvertersByType.TryGetValue(typeof(T), out var instance))");
							sb.EnterBlock();
							sb.AppendLine("converter = null;");
							sb.AppendLine("return false;");
							sb.LeaveBlock();
							sb.AppendLine($"converter = {UnsafeFullName}.As<{KnownTypeSymbols.IJsonConverterInterfaceFullName}<T>>(instance);");
							sb.AppendLine("return true;");
						}
						sb.LeaveBlock();
						sb.NewLine();

						// GetConverterFor<T>
						sb.AppendLine($"public {KnownTypeSymbols.IJsonConverterInterfaceFullName}<T>? GetConverterFor<T>()");
						sb.EnterBlock();
						{
							sb.AppendLine("if (!this.ConvertersByType.TryGetValue(typeof(T), out var instance))");
							sb.EnterBlock();
							sb.AppendLine("return null;");
							sb.LeaveBlock();
							sb.AppendLine($"return {UnsafeFullName}.As<{KnownTypeSymbols.IJsonConverterInterfaceFullName}<T>>(instance);");
						}
						sb.LeaveBlock();
						sb.NewLine();

						// TryResolveTypeDefinition()
						sb.AppendLine($"public bool TryResolveTypeDefinition({SystemTypeFullName} type, [{MaybeNullWhenAttributeFullName}(false)] out {KnownTypeSymbols.CrystalJsonTypeDefinitionFullName} definition)");
						sb.EnterBlock();
						sb.AppendLine("if (!TryGetConverterFor(type, out var converter))");
						sb.EnterBlock();
						sb.AppendLine("definition = null;");
						sb.AppendLine("return false;");
						sb.LeaveBlock();
						sb.AppendLine("definition = converter.GetDefinition();");
						sb.AppendLine("return definition != null;");
						sb.LeaveBlock();
						sb.NewLine();

						// TryResolveTypeDefinition<T>()
						sb.AppendLine($"public bool TryResolveTypeDefinition<T>([{MaybeNullWhenAttributeFullName}(false)] out {KnownTypeSymbols.CrystalJsonTypeDefinitionFullName} definition)");
						sb.EnterBlock();
						sb.AppendLine("if (!TryGetConverterFor<T>(out var converter))");
						sb.EnterBlock();
						sb.AppendLine("definition = null;");
						sb.AppendLine("return false;");
						sb.LeaveBlock();
						sb.AppendLine("definition = converter.GetDefinition();");
						sb.AppendLine("return definition != null;");
						sb.LeaveBlock();
						sb.NewLine();
					}
					sb.LeaveBlock("TypeMapper");
					sb.NewLine();

					CloseSelfScope(sb);

					sb.LeaveBlock("container");
					sb.NewLine();

					sb.LeaveBlock("namespace");
					sb.NewLine();

					this.Context.AddSource($"{GetContainerHintName()}.g.cs", sb.ToString());
				}

				// then, we generate one file for each of the serialized type
				foreach (var typeDef in includedTypes)
				{
					Kenobi($"Generating code for {typeDef.Type.FullyQualifiedName}");
					var sb = new CSharpCodeBuilder();
					AddFileHeaders(sb);
#if DEBUG
					{
						sb.BeginRegion("Type Definition (DEBUG)");
						sb.Comment(typeDef.Name + ":");
						var buf = new System.Text.StringBuilder();
						typeDef.Explain(buf, "- ");
						sb.Comment(buf.ToString());
						sb.EndRegion();
						sb.NewLine();
					}
#endif

					sb.AppendLine($"namespace {symbol.NameSpace}");
					sb.EnterBlock("namespace");

					// we don't want to have to specify the namespace everytime
					sb.AppendLine($"using {KnownTypeSymbols.CrystalJsonNamespace};");
					// we also use a lot of helper static methods from this type
					sb.NewLine();

					sb.AppendLine(GetContainerDeclaration());
					sb.EnterBlock("Container");

					OpenSelfScope(sb, emitAttributes: false);

					try
					{
						GenerateCodeForType(sb, typeDef);
					}
					catch (Exception ex)
					{
						Kenobi("CRASH: failed to generate " + typeDef.Name + ": " + ex.ToString());

						var generated = sb.ToString();
						// to help with diagnosing the crash, we will include the code generated so far inside #if ... #endif

						sb.Clear();
						sb.NewLine();
						sb.Comment("ERROR: generator failed!");
						sb.Comment(ex.ToString());
						sb.NewLine();
						sb.Comment("Code generated until the crash:");
						sb.AppendLine("#if false");
						sb.NewLine();
						sb.Output.Append(generated.Replace("#region", "_#region").Replace("#endregion", "_#endregion").Replace("#if", "_#if").Replace("#endif", "_#endif").Replace("#else", "_#else").Replace("#elif", "_#elif"));
						sb.Comment(ex.ToString());
						sb.NewLine();
						sb.AppendLine("#endif");
						sb.NewLine();

						this.Context.AddSource($"{GetContainerHintName()}.{typeDef.Name}.g.cs", sb.ToString());

						this.Context.ReportDiagnostic(
							Diagnostic.Create(new(
								"CJSON0003",
								"Failed to emit JSON code",
								"Failed to emit the generate source-code for for type {0} in {1}: [{2}] {3}.",
								"SnowBank.Serialization.Json.CodeGen",
								DiagnosticSeverity.Error,
								isEnabledByDefault: true
							), null, [ typeDef.Name, this.Metadata.Name, ex.GetType().Name, ex.Message ])
						);
					}

					CloseSelfScope(sb);

					sb.LeaveBlock("Container");
					sb.NewLine();

					sb.LeaveBlock("namespace");
					sb.NewLine();

					var hintName = typeDef.Name;
					if (typeDef.Type.IsGenericType())
					{
						hintName = $"{hintName}`{typeDef.Type.TypeArguments.Count}";
						foreach (var arg in typeDef.Type.TypeArguments)
						{
							hintName += "_" + arg.Name;
						}
					}

					this.Context.AddSource($"{GetContainerHintName()}.{hintName}.g.cs", sb.ToString());
				}
				Kenobi("Done!");
			}

			private string GetSerializerName(TypeMetadata type)
			{
				if (!type.IsGenericType()) return type.Name;
				return DecorateGenericSerializerName(type);

				static string DecorateGenericSerializerName(TypeMetadata type)
				{
					var sb = new StringBuilder();
					sb.Append(type.Name);
					foreach (var t in type.TypeArguments)
					{
						sb.Append('_');
						sb.Append(t.Name);
					}
					return sb.ToString();
				}
			}

			/// <summary>Tests if this type is the self-serialized entity that acts as its own container</summary>
			private bool IsSelfType(TypeMetadata type) => this.Metadata.IsSelfContained && type.Ref.Equals(this.Metadata.Type.Ref);

			/// <summary>Returns the path of the generated members for this type, relative to the container</summary>
			/// <remarks>In self mode everything lives inside the entity's single reserved scope: <c>"Json."</c> for the entity itself, <c>"Json.TypeName."</c> for a referenced type (whose holder cannot shadow anything from inside the scope); in container mode, <c>"TypeName."</c> (the nested static holder class).</remarks>
			private string GetSerializerScope(TypeMetadata type)
				=> IsSelfType(type) ? (SelfScopeName + ".")
					: this.Metadata.IsSelfContained ? (SelfScopeName + "." + GetSerializerName(type) + ".")
					: (GetSerializerName(type) + ".");

			private string GetLocalSerializerRef(CrystalJsonTypeMetadata metadata) => GetLocalSerializerRef(metadata.Type);
			private string GetLocalSerializerRef(TypeMetadata metadata) => $"{this.Metadata.Type.Name}.{GetSerializerScope(metadata)}Default";

			private string GetConverterName(CrystalJsonTypeMetadata metadata) => GetSerializerScope(metadata.Type) + "JsonConverter";

			private string GetReadOnlyProxyName(TypeMetadata type) => GetSerializerScope(type) + "ReadOnly";
			private string GetLocalReadOnlyProxyRef(CrystalJsonTypeMetadata metadata) => $"{this.Metadata.Name}.{GetReadOnlyProxyName(metadata.Type)}";

			private string GetWritableProxyName(TypeMetadata type) => GetSerializerScope(type) + "Writable";
			private string GetLocalWritableProxyRef(CrystalJsonTypeMetadata metadata) => $"{this.Metadata.Name}.{GetWritableProxyName(metadata.Type)}";

			/// <summary>Returns the name of the generated const string with the serialized name of this member, from within the converter itself</summary>
			private string GetLocalPropertyNameRef(CrystalJsonMemberMetadata member) => "PropertyNames." + member.MemberName;

			/// <summary>Returns the name of the generated const string with the serialized name of this member, from another part of the generated code</summary>
			private string GetTargetPropertyNameRef(CrystalJsonTypeMetadata type, CrystalJsonMemberMetadata member) => $"{this.Metadata.Name}.{GetSerializerScope(type.Type)}PropertyNames.{member.MemberName}";

			/// <summary>Returns the declaration that (re)opens the container type in a generated file</summary>
			/// <remarks>The static container class in container mode; the entity's own partial in self mode (matching its kind, so the compiler can merge the parts).</remarks>
			private string GetContainerDeclaration()
			{
				var type = this.Metadata.Type;
				if (!this.Metadata.IsSelfContained)
				{
					return $"public static partial class {type.Name}";
				}
				var keyword = type.IsRecord
					? (type.IsValueType() ? "record struct" : "record")
					: (type.IsValueType() ? "struct" : "class");
				return $"{(type.IsReadOnly ? "readonly " : "")}partial {keyword} {type.Name}";
			}

			/// <summary>Returns the base name of the generated source files for this container</summary>
			/// <remarks>Self-serialized entities are namespace-qualified: unlike containers, distinct entities with the same name are common, and hint names must be unique.</remarks>
			private string GetContainerHintName() => this.Metadata.IsSelfContained ? $"{this.Metadata.Type.NameSpace}.{this.Metadata.Type.Name}" : this.Metadata.Type.Name;

			/// <summary>Opens the single reserved scope that hosts all the generated code in self mode (no-op in container mode)</summary>
			/// <remarks>The scope is entirely generated, so unlike the entity's partial (which is user code) it can carry the generated-code attributes.</remarks>
			private void OpenSelfScope(CSharpCodeBuilder sb, bool emitAttributes)
			{
				if (!this.Metadata.IsSelfContained) return;
				if (emitAttributes)
				{
					sb.XmlComment("<summary>Source-generated JSON converters and proxies for this type</summary>");
					sb.AppendLine($"[{DynamicallyAccessedMembersAttributeFullName}({DynamicallyAccessedMemberTypesFullName}.All)]");
					sb.AppendLine($"[{GeneratedCodeAttributeFullName}(\"{nameof(CrystalJsonSourceGenerator)}\", \"0.1\")]");
					sb.AppendLine($"[{DebuggerNonUserCodeAttributeFullName}]");
					sb.AppendLine($"[{ExcludeFromCodeCoverageAttributeFullName}]");
				}
				sb.AppendLine($"public static partial class {SelfScopeName}");
				sb.EnterBlock("self-scope");
				sb.NewLine();
			}

			/// <summary>Closes the scope opened by <see cref="OpenSelfScope"/> (no-op in container mode)</summary>
			private void CloseSelfScope(CSharpCodeBuilder sb)
			{
				if (!this.Metadata.IsSelfContained) return;
				sb.LeaveBlock("self-scope");
				sb.NewLine();
			}

			/// <summary>Returns the name of the generated static singleton with the definition of this member</summary>
			private string GetPropertyEncodedNameRef(CrystalJsonMemberMetadata member) => "PropertyEncodedNames." + member.MemberName;

			/// <summary>Generates all types required to serialize a specific type</summary>
			private void GenerateCodeForType(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				var typeName = typeDef.Type.Name;
				var typeFullName = typeDef.Type.FullyQualifiedName;
				var typeCref = CSharpCodeBuilder.EscapeCref(typeFullName);

				// we need to get back the type symbol from the compilation (which we do not store in the metadata, since it changes everytime)

				var serializerName = typeName;
				if (typeDef.Type.IsGenericType())
				{
					foreach (var t in typeDef.Type.TypeArguments)
					{
						serializerName += "_" + t.Name;
					}
				}

				var serializerTypeName = GetConverterName(typeDef);
				var jsonConverterInterfaceName = $"{KnownTypeSymbols.IJsonConverterInterfaceFullName}<{typeFullName}>";

				var readOnlyProxyTypeName = GetReadOnlyProxyName(typeDef.Type);
				var writableProxyTypeName = GetWritableProxyName(typeDef.Type);

				var readOnlyProxyInterfaceName = $"{KnownTypeSymbols.IJsonReadOnlyProxyFullName}<{typeFullName}, {readOnlyProxyTypeName}, {writableProxyTypeName}>";
				var writableProxyInterfaceName = $"{KnownTypeSymbols.IJsonWritableProxyFullName}<{typeFullName}, {writableProxyTypeName}>";

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(typeDef.Type.Ref, out var polymorphicMetadata);
#if FULL_DEBUG
				sb.Comment($"Generating for type {typeDef.Type.FullyQualifiedName}");
				foreach (var member in typeDef.Members)
				{
					sb.Comment($"- {member.Name}: {member.ToString()}");
				}
				sb.NewLine();
#endif

				bool selfType = IsSelfType(typeDef.Type);
				if (!selfType)
				{
					sb.XmlComment($"<summary>Set of JSON converters and other various helpers for type <see cref=\"{typeCref}\">{typeName}</see></summary>");
					sb.AppendLine("public static class " + serializerName);
					sb.EnterBlock();
				}
				// note: the self type has no holder class of its own: its members land directly in the enclosing Json scope
				sb.NewLine();

				sb.XmlComment($"<summary>JSON converter for type <see cref=\"{typeCref}\">{typeName}</see></summary>");
				sb.AppendLine($"public static {serializerTypeName} Default => m_cachedSerializer ??= new();");
				sb.NewLine();
				sb.AppendLine($"private static {serializerTypeName}? m_cachedSerializer;");
				sb.NewLine();

				sb.BeginRegion("Proxy Helpers...");
				sb.NewLine();

				WriteProxyStaticHelpers(sb, typeDef, typeCref);

				sb.EndRegion();
				sb.NewLine();

				if (this.WritesXml)
				{ // the eight XML outputs, symmetrical with the JSON ones (emission section: CrystalJsonGenerator.Emitter.Xml.cs)
					sb.BeginRegion("XML Output...");
					sb.NewLine();

					WriteXmlStaticHelpers(sb, typeDef, typeCref);

					sb.EndRegion();
					sb.NewLine();
				}

				#region Metadata...

				sb.BeginRegion("Metadata...");
				sb.NewLine();

				sb.XmlComment("<summary>Names of all serialized members for this type</summary>");
				sb.AppendLine("public static class PropertyNames");
				sb.EnterBlock("properties");
				sb.NewLine();
				if (typeDef.IsPolymorphicRoot)
				{
					sb.XmlComment($"<summary>Serialized name of the type discriminator property of types that derive from <see cref=\"{typeCref}\"/></summary>");
					sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
					sb.AppendLine($"public const string _TypeDiscriminatorProperty_ = {CSharpCodeBuilder.Constant(typeDef.TypeDiscriminatorPropertyName ?? "$type")};");
					sb.NewLine();
				}
				else if (hasPolymorphicDefinition)
				{
					sb.XmlComment($"<summary>Serialized name of the type discriminator property for type <see cref=\"{typeCref}\"/></summary>");
					sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
					sb.AppendLine($"public const string _TypeDiscriminatorProperty_ = {CSharpCodeBuilder.Constant(polymorphicMetadata.Parent.TypeDiscriminatorPropertyName ?? "$type")};");
					sb.NewLine();
					if (polymorphicMetadata.Discriminator is not null)
					{
						sb.XmlComment($"<summary>Cached JSON literal of the type discriminator value for type <see cref=\"{typeCref}\"/></summary>");
						sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
						switch (polymorphicMetadata.Discriminator)
						{
							case string s: sb.AppendLine($"public const string _TypeDiscriminatorValue_ = {CSharpCodeBuilder.Constant(s)};"); break;
							case int n: sb.AppendLine($"public const int _TypeDiscriminatorValue_ = {CSharpCodeBuilder.Constant(n)};"); break;
							default: sb.AppendLine($"#error Invalid discriminator value type for derived type {typeDef.Name} of parent type {polymorphicMetadata.Parent.Name}"); break;
						}
						sb.NewLine();
					}
				}
				foreach (var member in typeDef.Members)
				{
					sb.XmlComment($"<summary>Serialized name of the <see cref=\"{typeCref}.{member.MemberName}\"/> {(member.IsField ? "field" : "property")} of the <see cref=\"{typeCref}\"/> {(member.Type.IsValueType() ? "struct" : member.Type.IsRecord ? "record" : "class")}</summary>");
					sb.AppendLine($"public const string {member.MemberName} = {CSharpCodeBuilder.Constant(member.Name)};");
					sb.NewLine();
				}

				// a type with no serialized member at all is legal (an ISerializable wrapper on the DataContract XML wire is
				// exactly that), and `new [] { }` does not compile: the empty case needs the typed form
				sb.AppendLine(typeDef.Members.Count > 0
					? $"public static string[] GetAllNames() => new [] {{ {string.Join(", ", typeDef.Members.Select(this.GetLocalPropertyNameRef))} }};" //TODO: PERF!
					: "public static string[] GetAllNames() => [ ];");
				sb.NewLine();

				sb.LeaveBlock("properties");
				sb.NewLine();

				sb.XmlComment("<summary>Cached encoded names for all serialized members for this type</summary>");
				sb.AppendLine("public static class PropertyEncodedNames");
				sb.EnterBlock("properties");
				sb.NewLine();
				if (typeDef.IsPolymorphicRoot)
				{
					sb.XmlComment($"<summary>Encoded name of the type discriminator property of types that derive from <see cref=\"{typeCref}\"/></summary>");
					sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
					sb.AppendLine($"public static readonly {KnownTypeSymbols.JsonEncodedPropertyNameFullName} _TypeDiscriminatorProperty_ = new(PropertyNames._TypeDiscriminatorProperty_);");
					sb.NewLine();
				}
				else if (hasPolymorphicDefinition)
				{
					sb.XmlComment($"<summary>Encoded name of the type discriminator property for type <see cref=\"{typeCref}\"/></summary>");
					sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
					sb.AppendLine($"public static readonly {KnownTypeSymbols.JsonEncodedPropertyNameFullName} _TypeDiscriminatorProperty_ = new(PropertyNames._TypeDiscriminatorProperty_);");
					sb.NewLine();
				}
				if (polymorphicMetadata.Discriminator is not null)
				{
					sb.XmlComment($"<summary>Cached JSON literal of the type discriminator value for type <see cref=\"{typeCref}\"/></summary>");
					sb.AppendLine($"[{EditorBrowsableAttributeFullName}({EditorBrowsableStateFullName}.Never)]");
					sb.AppendLine($"public static readonly {KnownTypeSymbols.JsonValueFullName} _TypeDiscriminatorValue_ = {ConvertDiscriminatorValueToJsonLiteral(polymorphicMetadata.Discriminator)};");
					sb.NewLine();
				}
				foreach (var member in typeDef.Members)
				{
					sb.XmlComment($"<summary>Encoded name of the <see cref=\"{typeCref}.{member.MemberName}\"/> {(member.IsField ? "field" : "property")} of the <see cref=\"{typeCref}\"/> {(member.Type.IsValueType() ? "struct" : member.Type.IsRecord ? "record" : "class")}</summary>");
					sb.AppendLine($"public static readonly {KnownTypeSymbols.JsonEncodedPropertyNameFullName} {member.MemberName} = new({GetLocalPropertyNameRef(member)});");
					sb.NewLine();
				}
				sb.LeaveBlock("properties");
				sb.NewLine();

				sb.EndRegion();
				sb.NewLine();

				#endregion

				#region JsonConverter class...

				// when the container also produces XML, the SAME instance carries the XML facet: passing Default to code that
				// wants an ICrystalXmlSerializer<T> resolves statically, with no second converter to keep in sync
				sb.AppendLine($"public sealed class JsonConverter : {KnownTypeSymbols.IJsonConverterInterfaceFullName}<{typeFullName}, {readOnlyProxyTypeName}, {writableProxyTypeName}>{(this.WritesXml ? $", {ICrystalXmlSerializerFullName}<{typeFullName}>" : "")}"); //TODO: implements!
				sb.EnterBlock("JsonConverter");

				// custom converters attached to members ([JsonConverter(typeof(...))] or [JsonBooleanLiterals])
				// note: internal so that the sibling ReadOnly/Writable proxy records can route through them as well
				foreach (var member in typeDef.Members)
				{
					if (member.CustomConverterType != null)
					{
						sb.AppendLine($"internal static readonly {member.CustomConverterType} {GetMemberConverterRef(member)} = new {member.CustomConverterType}({member.CustomConverterArgs});");
						sb.NewLine();
					}
				}

				EmitNonPublicAccessorThunks(sb, typeDef);
				EmitCallbackThunks(sb, typeDef);

				#region Type Definition...

				sb.BeginRegion("Conversion Helpers...");
				sb.NewLine();

				WriteTypeDefinitionHelpers(sb, typeDef);

				sb.EndRegion();
				sb.NewLine();

				#endregion
				
				#region Helpers...

				sb.BeginRegion("Conversion Helpers...");
				sb.NewLine();

				sb.InheritDoc();
				sb.AppendLine($"public {SystemTypeFullName} GetTargetType() => typeof({typeDef.Type.FullyQualifiedName});");
				sb.NewLine();

				WriteProxyInstanceHelpers(sb, typeDef, typeCref);

				sb.EndRegion();
				sb.NewLine();

				#endregion

				#region Serialize...

				sb.BeginRegion($"IJsonSerializer<{typeName}>...");
				sb.NewLine();

				WriteSerializeMethod(sb, typeDef);

				sb.EndRegion();
				sb.NewLine();

				#endregion

				#region Pack...

				sb.BeginRegion($"IJsonPacker<{typeName}>...");
				sb.NewLine();

				WritePackMethod(sb, typeDef);

				sb.EndRegion();
				sb.NewLine();

				#endregion

				#region UnPack...

				sb.BeginRegion($"IJsonDeserializer<{typeName}>...");
				sb.NewLine();

				WriteUnpackMethod(sb, typeDef, typeCref);

				sb.EndRegion();
				sb.NewLine();

				#endregion

				#region WriteXml...

				if (this.WritesXml)
				{
					sb.BeginRegion($"ICrystalXmlSerializer<{typeName}>...");
					sb.NewLine();

					WriteXmlSerializer(sb, typeDef);

					sb.EndRegion();
					sb.NewLine();
				}

				#endregion

				sb.LeaveBlock("JsonConverter");
				sb.NewLine();

				#endregion

				#region Read-Only Proxy...

				// IJsonReadOnlyProxy<T>
				sb.XmlComment($"<summary>Wraps a <see cref=\"{KnownTypeSymbols.JsonObjectFullName}\"/> into a read-only type-safe view that emulates the type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<seealso cref=\"{KnownTypeSymbols.IJsonReadOnlyProxyFullName}{{T}}\"/>");
				sb.Struct(
					"public readonly",
					"ReadOnly",
					[ readOnlyProxyInterfaceName ],
					[],
					() =>
					{
						// m_value
						sb.XmlComment("<summary>Observable JSON Value wrapped by this instance</summary>");
						sb.AppendLine($"private readonly {KnownTypeSymbols.ObservableJsonValueFullName} m_value;");
						sb.NewLine();

						// ctor()
						sb.AppendLine($"public ReadOnly({KnownTypeSymbols.ObservableJsonValueFullName} value)");
						sb.EnterBlock();
						sb.AppendLine("m_value = value;");
						sb.LeaveBlock();
						sb.NewLine();

						#region Methods...

						sb.BeginRegion("Public Helpers...");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {readOnlyProxyTypeName} Create({KnownTypeSymbols.ObservableJsonValueFullName} value, {jsonConverterInterfaceName}? converter = null)");
						sb.AppendLine("\t=> new(value);");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {readOnlyProxyTypeName} Create({KnownTypeSymbols.JsonValueFullName} value, {jsonConverterInterfaceName}? converter = null)");
						sb.AppendLine($"\t=> new({KnownTypeSymbols.ObservableJsonValueFullName}.Untracked(value));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {readOnlyProxyTypeName} Create({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value, {jsonConverterInterfaceName}? converter = null)");
						sb.AppendLine($"\t=> new({KnownTypeSymbols.ObservableJsonValueFullName}.Tracked(ctx, value));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {readOnlyProxyTypeName} Create({typeDef.Type.FullyQualifiedNameAnnotated} value, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = null, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = null)");
						sb.AppendLine($"\t=> new({KnownTypeSymbols.ObservableJsonValueFullName}.Untracked({GetLocalSerializerRef(typeDef)}.Pack(value, settings.AsReadOnly(), resolver)));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {readOnlyProxyTypeName} Create({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} value, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = null, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = null)");
						sb.AppendLine($"\t=> new({KnownTypeSymbols.ObservableJsonValueFullName}.Tracked(ctx, {GetLocalSerializerRef(typeDef)}.Pack(value, settings.AsReadOnly(), resolver)));");
						sb.NewLine();

						// static Converter
						sb.InheritDoc();
						sb.AppendLine($"public static {jsonConverterInterfaceName} Converter => {GetLocalSerializerRef(typeDef)};");
						sb.NewLine();

						// TValue ToValue()
						sb.InheritDoc();
						sb.AppendLine($"public {typeDef.Type.FullyQualifiedName} ToValue() => {GetLocalSerializerRef(typeDef)}.Unpack(m_value.ToJsonValue());"); //TODO: resolver?
						sb.NewLine();

						// GetContext()
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.IObservableJsonContextFullName}? GetContext() => m_value.GetContext();");
						sb.NewLine();

						// bool IsNullOrMissing()
						sb.XmlComment("<summary>Tests if this object is either null or missing</summary>");
						sb.XmlComment("<returns><c>true</c> if the wrapped JSON value is null or empty; otherwise, <c>false</c>.</returns>");
						sb.AppendLine("public bool IsNullOrMissing() => m_value.IsNullOrMissing();");
						sb.NewLine();

						// bool Exists()
						sb.XmlComment("<summary>Tests if this object is present</summary>");
						sb.XmlComment("<returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>");
						sb.AppendLine("public bool Exists() => m_value.Exists();");
						sb.NewLine();

						// bool IsObject()
						sb.XmlComment("<summary>Tests if the wrapped value is a valid JSON Object.</summary>");
						sb.XmlComment("<returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>");
						sb.AppendLine("public bool IsObject() => m_value.IsOfType(JsonType.Object);");
						sb.NewLine();

						// bool IsObjectOrMissing()
						sb.XmlComment("<summary>Tests if the wrapped value is a valid JSON Object.</summary>");
						sb.XmlComment("<returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>");
						sb.AppendLine("public bool IsObjectOrMissing() => m_value.IsOfTypeOrNull(JsonType.Object);");
						sb.NewLine();

						// Get()
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get() => m_value;");
						sb.NewLine();

						// ToJsonValue()
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.JsonValueFullName} ToJsonValue() => m_value.ToJsonValue();");
						sb.NewLine();

						// ToMutable()
						sb.InheritDoc();
						sb.AppendLine($"public {writableProxyTypeName} ToMutable() => new({KnownTypeSymbols.MutableJsonValueFullName}.Untracked(m_value.GetJsonUnsafe().Copy()));");
						sb.NewLine();

						// this[string]
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} this[string name] => m_value.Get(name);");
						sb.NewLine();

						// Get(string)
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get(string name) => m_value.Get(name);");
						sb.NewLine();

						// Get<T>(string)
						sb.InheritDoc();
						sb.AppendLine("public T Get<T>(string name) where T : notnull => m_value.Get<T>(name);");
						sb.NewLine();

						// Get<T>(string, T)
						sb.InheritDoc();
						sb.AppendLine($"[return: {NotNullIfNotNullAttributeFullName}(nameof(defaultValue))]");
						sb.AppendLine("public T? Get<T>(string name, T defaultValue) => m_value.Get<T>(name, defaultValue);");
						sb.NewLine();

						// this[ReadOnlyMemory<char>]
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} this[{ReadOnlyMemoryOfCharFullName} name] => m_value.Get(name);");
						sb.NewLine();

						// Get(ReadOnlyMemory<char>)
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get({ReadOnlyMemoryOfCharFullName} name) => m_value.Get(name);");
						sb.NewLine();

						// Get<T>(ReadOnlyMemory<char>)
						sb.InheritDoc();
						sb.AppendLine($"public T Get<T>({ReadOnlyMemoryOfCharFullName} name) where T : notnull => m_value.Get<T>(name);");
						sb.NewLine();

						// Get<T>(ReadOnlyMemory<char>, T)
						sb.InheritDoc();
						sb.AppendLine($"[return: {NotNullIfNotNullAttributeFullName}(nameof(defaultValue))]");
						sb.AppendLine($"public T? Get<T>({ReadOnlyMemoryOfCharFullName} name, T defaultValue) => m_value.Get<T>(name, defaultValue);");
						sb.NewLine();

						// Get(JsonPath)
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get({KnownTypeSymbols.JsonPathFullName} path) => m_value.Get(path);");
						sb.NewLine();

						// Get<T>(JsonPath)
						sb.InheritDoc();
						sb.AppendLine($"public T Get<T>({KnownTypeSymbols.JsonPathFullName} path) where T : notnull => m_value.Get<T>(path);");
						sb.NewLine();

						// Get<T>(JsonPath, T)
						sb.InheritDoc();
						sb.AppendLine($"[return: {NotNullIfNotNullAttributeFullName}(nameof(defaultValue))]");
						sb.AppendLine($"public T? Get<T>({KnownTypeSymbols.JsonPathFullName} path, T defaultValue) => m_value.Get<T>(path, defaultValue);");
						sb.NewLine();

						// Get(int)
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get(int index) => m_value.Get(index);");
						sb.NewLine();

						// Get(Index)
						sb.InheritDoc();
						sb.AppendLine($"public {KnownTypeSymbols.ObservableJsonValueFullName} Get({IndexFullName} index) => m_value.Get(index);");
						sb.NewLine();

						// TReadOnly With(Action<TMutable>)
						sb.InheritDoc();
						sb.AppendLine($"public {readOnlyProxyTypeName} With({ActionFullName}<{writableProxyTypeName}> modifier)");
						sb.EnterBlock();
						sb.AppendLine("var copy = m_value.GetJsonUnsafe().Copy();");
						sb.AppendLine($"modifier(new({KnownTypeSymbols.MutableJsonValueFullName}.Untracked(copy)));");
						sb.AppendLine("return new(m_value.Visit(copy.Freeze()));");
						sb.LeaveBlock();
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine("public override bool Equals(object? other) => other switch");
						sb.EnterBlock();
						sb.AppendLine($"{readOnlyProxyTypeName} value => m_value.Equals(value.m_value),");
						sb.AppendLine($"{KnownTypeSymbols.ObservableJsonValueFullName} value => m_value.Equals(value),");
						sb.AppendLine($"{KnownTypeSymbols.JsonValueFullName} value => m_value.Equals(value),");
						sb.AppendLine("null => m_value.IsNullOrMissing(),");
						sb.AppendLine("_ => false,");
						sb.LeaveBlock(suffix: ';');
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine("public override int GetHashCode() => m_value.ToJsonValue().GetHashCode();");
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine($"public bool Equals({readOnlyProxyTypeName} value) => m_value.Equals(value.m_value);");
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine($"public bool Equals({KnownTypeSymbols.ObservableJsonValueFullName}? value) => m_value.Equals(value);");
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine($"public bool Equals({KnownTypeSymbols.JsonValueFullName}? value) => m_value.Equals(value);");
						sb.NewLine();

						sb.InheritDoc();
						sb.AppendLine($"public override string ToString() => \"({typeName}) \" + m_value.ToString();");
						sb.NewLine();

						// IJsonSerializable
						sb.InheritDoc();
						sb.AppendLine($"void {KnownTypeSymbols.IJsonSerializableFullName}.JsonSerialize({KnownTypeSymbols.CrystalJsonWriterFullName} writer) => m_value.ToJsonValue().JsonSerialize(writer);");
						sb.NewLine();

						// IJsonPackable
						sb.InheritDoc();
						sb.AppendLine($"{KnownTypeSymbols.JsonValueFullName} {KnownTypeSymbols.IJsonPackableFullName}.JsonPack({KnownTypeSymbols.CrystalJsonSettingsFullName} settings, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName} resolver) => m_value.ToJsonValue();");
						sb.NewLine();

						sb.EndRegion();
						sb.NewLine();

						#endregion

						#region Members

						sb.BeginRegion("Type Safe Members...");
						sb.NewLine();

						foreach (var member in typeDef.Members)
						{
							string? getterExpr = null;
							string proxyType = member.Type.FullyQualifiedNameAnnotated;

							if (member.CustomConverterType != null)
							{ // a custom converter takes over the member's wire form; the proxy must decode through it
								var converterRef = $"JsonConverter.{GetMemberConverterRef(member)}";
								if (!member.CustomConverterHasDeserializer)
								{ // asymmetric converter without the deserializing facet: an absent value binds to the default, anything else fails loudly
									getterExpr = $"/* member-converter (missing deserializer facet) */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingDeserializerFacet<{member.Type.FullyQualifiedNameAnnotated}>(m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), typeof({member.CustomConverterType}), {GetForgivingDefaultLiteral(member)})!";
								}
								else if (member.CustomConverterIsNullableForm)
								{ // converter declared for the T? form itself: it owns every PRESENT value, the pipeline still owns null/missing
									getterExpr = member.IsRequired
										? $"/* member-converter-nullable-form-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequiredNullableForm({converterRef}, m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), null, null, {CSharpCodeBuilder.Constant(member.MemberName)})"
										: $"/* member-converter-nullable-form */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullableForm({converterRef}, m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), null)";
								}
								else if (member.IsRequired)
								{
									getterExpr = $"/* member-converter-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired({converterRef}, m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), null, null, {CSharpCodeBuilder.Constant(member.MemberName)})";
								}
								else if (member.Type.NullableOfType is not null)
								{
									getterExpr = $"/* member-converter-nullable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullable({converterRef}, m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), null)";
								}
								else
								{
									getterExpr = $"/* member-converter */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack({converterRef}, m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), {GetForgivingDefaultLiteral(member)}, null)!";
								}
							}
							else if (IsLocallyGeneratedType(member.Type, out var target, out _))
							{
								getterExpr = $"/* local-deserializer */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
								proxyType = GetLocalReadOnlyProxyRef(target);
							}
							else if (member.Type.IsStringLike() || member.Type.IsBooleanLike() || member.Type.IsNumberLike() || member.Type.IsDateLike())
							{
								//use default getter
								getterExpr = null;
							}
							else if (member.Type.JsonType() is not JsonPrimitiveType.None)
							{
								getterExpr = member.Type.JsonType() switch
								{
									JsonPrimitiveType.Object => $"/* direct-json-object */ m_value[{this.GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue().{(member.IsNullableRefType() ? "AsObjectOrDefault" : member.IsRequired ? "AsObject" : "AsObjectOrEmpty")}()",
									JsonPrimitiveType.Array => $"/* direct-json-array */ m_value[{this.GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue().{(member.IsNullableRefType() ? "AsArrayOrDefault" : member.IsRequired ? "AsArray" : "AsArrayOrEmpty")}()",
									//TODO: JsonString, JsonNumber, ... (are they really used?)
									_ => $"/* direct-json-value */ m_value[{this.GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue()"
								};
							}
							else if (member.Type.IsJsonDeserializable())
							{
								getterExpr = $"/* json-deserializable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackJsonDeserializable<{member.Type.FullyQualifiedName}>(m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), {GetForgivingDefaultLiteral(member)}, null)"; //TODO: default value!
							}
							else if (member.Type.IsNullableOfT(out var underlyingType) && underlyingType.IsJsonDeserializable())
							{
								getterExpr = $"/* nullable-json-deserializable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullableJsonDeserializable<{underlyingType.FullyQualifiedName}>(m_value[{GetTargetPropertyNameRef(typeDef, member)}].ToJsonValue(), {GetForgivingDefaultLiteral(member)}, null)"; //TODO: default value!
							}
							else if (member.Type.IsDictionary(out var keyType, out var valueType))
							{
								if (keyType.IsString())
								{
									if (IsLocallyGeneratedType(valueType, out target, out _))
									{
										getterExpr = $"/* string-dict-local */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyDictionaryFullName}<{valueType.FullyQualifiedName}, {GetLocalReadOnlyProxyRef(target)}>";
									}
									else
									{
										getterExpr = $"/* string-dict-fallback */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyDictionaryFullName}<{valueType.FullyQualifiedName}>";
									}
								}
								else if (keyType.IsInt32())
								{
									if (IsLocallyGeneratedType(valueType, out target, out _))
									{
										getterExpr = $"/* int-dict-local */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyInt32DictionaryFullName}<{valueType.FullyQualifiedName}, {GetLocalReadOnlyProxyRef(target)}>";
									}
									else if (valueType.IsArray(out var elemType) && IsLocallyGeneratedType(elemType, out target, out _))
									{
										getterExpr = $"/* int-dict-local-array */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyInt32DictionaryOfArrayFullName}<{elemType.FullyQualifiedName}, {GetLocalReadOnlyProxyRef(target)}>";
									}
									else
									{
										getterExpr = $"/* int-dict-fallback */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyInt32DictionaryFullName}<{valueType.FullyQualifiedName}>";
									}
								}
							}
							else if (member.Type.IsEnumerable(out var elemType))
							{
								if (IsLocallyGeneratedType(elemType, out target, out _))
								{
									getterExpr = $"/* enumerable-local */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
									proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyArrayFullName}<{elemType.FullyQualifiedName}, {GetLocalReadOnlyProxyRef(target)}>";
								}
								else
								{
									getterExpr = $"/* enumerable-fallback */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
									proxyType = $"{KnownTypeSymbols.JsonReadOnlyProxyArrayFullName}<{elemType.FullyQualifiedName}>";
								}
							}

							if (getterExpr == null)
							{
								if (member.IsNullableRefType())
								{
									getterExpr = $"/* ref-nullable */ m_value.Get<{member.Type.FullyQualifiedNameAnnotated}>({GetTargetPropertyNameRef(typeDef, member)}, {GetForgivingDefaultLiteral(member)})";
								}
								else if (member.IsRequired)
								{
									getterExpr = $"/* required */ m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)})";
								}
								else if (member.Type.IsValueType() && !member.Type.IsNullableOfT())
								{ // TODO: BUGBUG: it is the same as the next statement?
									getterExpr = $"/* vt-not-null */ m_value.Get<{member.Type.FullyQualifiedNameAnnotated}>({GetTargetPropertyNameRef(typeDef, member)}, {GetForgivingDefaultLiteral(member)})";
								}
								else
								{
									getterExpr = $"/* else */ m_value.Get<{member.Type.FullyQualifiedNameAnnotated}>({GetTargetPropertyNameRef(typeDef, member)}, {GetForgivingDefaultLiteral(member)})";
								}
							}

							sb.InheritDoc(CSharpCodeBuilder.EscapeCref(typeFullName, member.MemberName));
							sb.AppendLine($"public {proxyType} {member.MemberName} => {getterExpr};");
							sb.NewLine();

							// for required member, we also generate a HasXYZ() method that will allow the caller to check if the field is valid (before calling the property that would throw if this is not the case)
							if (member.IsRequired)
							{
								sb.XmlComment($"<summary>Tests if the object has a valid value for the <see cref=\"{member.MemberName}\"/> property.</summary>");
								sb.AppendLine($"public bool Has{member.MemberName}() => m_value.ContainsKey({GetTargetPropertyNameRef(typeDef, member)});");
								sb.NewLine();
							}
						}

						sb.EndRegion();
						sb.NewLine();

						#endregion
					}
				);

				#endregion

				#region Writable Proxy...

				//note: we cannot generate a readonly struct, otherwise the following would not be allowed
				//  obj.Foo = 123; // this can work
				//	obj.Bar.Bar = 123; // this fails to compile because "obj.Bar" is not a valid 'this' for the Baz setter

				// IJsonWritableProxy<T>
				sb.XmlComment($"<summary>Wraps a <see cref=\"{KnownTypeSymbols.JsonObjectFullName}\"/> into a writable type-safe view that emulates the type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<seealso cref=\"{KnownTypeSymbols.IJsonWritableProxyFullName}{{T}}\"/>");
				sb.Record(
					"public sealed",
					"Writable",
					[
						KnownTypeSymbols.JsonWritableProxyObjectBaseFullName,
						writableProxyInterfaceName
					],
					[],
					() =>
					{
						// ctor()
						sb.AppendLine($"public Writable({KnownTypeSymbols.MutableJsonValueFullName} value) : base(value)");
						sb.EnterBlock();
						sb.LeaveBlock();
						sb.NewLine();

						#region Methods...

						sb.BeginRegion("Public Methods...");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {writableProxyTypeName} Create({KnownTypeSymbols.MutableJsonValueFullName} value, {jsonConverterInterfaceName}? converter = null) => new(value);");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {writableProxyTypeName} Create({KnownTypeSymbols.JsonValueFullName} value) => new({KnownTypeSymbols.MutableJsonValueFullName}.Untracked(value));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {writableProxyTypeName} Create({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value) => new({KnownTypeSymbols.MutableJsonValueFullName}.Tracked(ctx, value));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {writableProxyTypeName} Create({typeDef.Type.FullyQualifiedNameAnnotated} value, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = null, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = null) => new({KnownTypeSymbols.MutableJsonValueFullName}.Untracked({GetLocalSerializerRef(typeDef)}.Pack(value, settings.AsMutable(), resolver)));");
						sb.NewLine();

						// static Create()
						sb.InheritDoc();
						sb.AppendLine($"public static {writableProxyTypeName} Create({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} value, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = null, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = null) => new({KnownTypeSymbols.MutableJsonValueFullName}.Tracked(ctx, {GetLocalSerializerRef(typeDef)}.Pack(value, settings.AsMutable(), resolver)));");
						sb.NewLine();

						// static Converter
						sb.InheritDoc();
						sb.AppendLine($"public static {jsonConverterInterfaceName} Converter => {GetLocalSerializerRef(typeDef)};");
						sb.NewLine();

						// TMutable FromValue(TValue)
						sb.XmlComment($"<summary>Pack an instance of <see cref=\"{typeCref}\"/> into a mutable JSON proxy</summary>");
						sb.AppendLine($"public static {writableProxyTypeName} FromValue({typeDef.Type.FullyQualifiedName} value)");
						sb.EnterBlock();
						if (!typeDef.Type.IsValueType())
						{
							sb.AppendLine($"{ArgumentNullExceptionFullName}.ThrowIfNull(value);");
						}
						sb.AppendLine($"return new({KnownTypeSymbols.MutableJsonValueFullName}.Untracked({GetLocalSerializerRef(typeDef)}.Pack(value, {KnownTypeSymbols.CrystalJsonSettingsFullName}.Json)));");
						sb.LeaveBlock();
						sb.NewLine();

						// TMutable FromValue(TValue)
						sb.XmlComment($"<summary>Pack an instance of <see cref=\"{typeCref}\"/> into a mutable JSON proxy</summary>");
						sb.AppendLine($"public static {writableProxyTypeName} FromValue({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedName} value)");
						sb.EnterBlock();
						if (!typeDef.Type.IsValueType())
						{
							sb.AppendLine($"{ArgumentNullExceptionFullName}.ThrowIfNull(value);");
						}
						sb.AppendLine($"return new({KnownTypeSymbols.MutableJsonValueFullName}.Tracked(ctx, {GetLocalSerializerRef(typeDef)}.Pack(value, {KnownTypeSymbols.CrystalJsonSettingsFullName}.Json)));");
						sb.LeaveBlock();
						sb.NewLine();

						// ToValue()
						sb.InheritDoc();
						sb.AppendLine($"public {typeDef.Type.FullyQualifiedName} ToValue() => {GetLocalSerializerRef(typeDef)}.Unpack(m_value.ToJsonValue());"); //TODO: resolver?
						sb.NewLine();

						// TReadOnly ToReadOnly()
						sb.InheritDoc();
						sb.AppendLine($"public {readOnlyProxyTypeName} ToReadOnly() => new({KnownTypeSymbols.ObservableJsonValueFullName}.Untracked(m_value.ToJsonValue().ToReadOnly()));");
						sb.NewLine();

						// Set(TReadOnly)
						sb.XmlComment("<summary>Replaces the value of this instance</summary>");
						sb.AppendLine($"public void Set({readOnlyProxyTypeName} value) => m_value.Set(value.ToJsonValue());");
						sb.NewLine();

						// Set(TWritable)
						sb.XmlComment("<summary>Replaces the value of this instance</summary>");
						sb.AppendLine($"public void Set({writableProxyTypeName} value) => m_value.Set(value.ToJsonValue());");
						sb.NewLine();

						// Set(T)
						sb.XmlComment("<summary>Replaces the value of this instance</summary>");
						sb.AppendLine($"public void Set({typeDef.Type.FullyQualifiedName} instance) => m_value.Set({GetLocalSerializerRef(typeDef)}.Pack(instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}.Json));");
						sb.NewLine();

						// GetHashCode()
						sb.InheritDoc();
						sb.AppendLine($"[{DoesNotReturnAttributeFullName}]");
						sb.AppendLine($"public override int GetHashCode() => throw new {NotSupportedExceptionFullName}();");
						sb.NewLine();

						// Equals(TWritable)
						sb.InheritDoc();
						sb.AppendLine($"public bool Equals({writableProxyTypeName}? value) => m_value.Equals(value?.m_value);");
						sb.NewLine();

						// ToString()
						sb.InheritDoc();
						sb.AppendLine($"public override string ToString() => \"({typeName}) \" + m_value.ToString();");
						sb.NewLine();

						sb.EndRegion();
						sb.NewLine();

						#endregion

						#region Members

						sb.BeginRegion("Public Members...");
						sb.NewLine();
						foreach (var member in typeDef.Members)
						{
							var defaultValue = GetForgivingDefaultLiteral(member);

							string proxyType = member.Type.FullyQualifiedNameAnnotated;
							string? setterExpr = null;
							string? getterExpr = null;
							string? attributeExpr = null;

							if (member.CustomConverterType != null)
							{ // a custom converter takes over the member's wire form; the proxy must encode and decode through it
								var converterRef = $"JsonConverter.{GetMemberConverterRef(member)}";
								if (!member.CustomConverterHasDeserializer)
								{ // asymmetric converter without the deserializing facet: an absent value binds to the default, anything else fails loudly
									getterExpr = $"/* member-converter (missing deserializer facet) */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingDeserializerFacet<{member.Type.FullyQualifiedNameAnnotated}>(m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), typeof({member.CustomConverterType}), {defaultValue})!";
								}
								else if (member.CustomConverterIsNullableForm)
								{ // converter declared for the T? form itself: it owns every PRESENT value, the pipeline still owns null/missing
									getterExpr = member.IsRequired
										? $"/* member-converter-nullable-form-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequiredNullableForm({converterRef}, m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), null, null, {CSharpCodeBuilder.Constant(member.MemberName)})"
										: $"/* member-converter-nullable-form */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullableForm({converterRef}, m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), null)";
								}
								else if (member.IsRequired)
								{
									getterExpr = $"/* member-converter-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired({converterRef}, m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), null, null, {CSharpCodeBuilder.Constant(member.MemberName)})";
								}
								else if (member.Type.NullableOfType is not null)
								{
									getterExpr = $"/* member-converter-nullable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullable({converterRef}, m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), null)";
								}
								else
								{
									getterExpr = $"/* member-converter */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack({converterRef}, m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}), {defaultValue}, null)!";
								}
								if (!member.CustomConverterHasPacker)
								{ // asymmetric converter without the packing facet: any attempt to set the member fails loudly
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingPackerFacet(typeof({member.CustomConverterType}), typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName})))";
								}
								else if (member.Type.IsValueType() && member.Type.NullableOfType is null)
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, {converterRef}.Pack(value, null, null))";
								}
								else
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value is {{ }} v{member.MemberName} ? {converterRef}.Pack(v{member.MemberName}, null, null) : {KnownTypeSymbols.JsonNullFullName}.Null)";
								}
							}
							else if (IsLocallyGeneratedType(member.Type, out var target, out _))
							{
								proxyType = GetLocalWritableProxyRef(target);
								getterExpr = $"/* proxy */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
								setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value.ToJsonValue())";
							}
							else if (member.Type.IsStringLike() || member.Type.IsBooleanLike() || member.Type.IsNumberLike() || member.Type.IsDateLike())
							{
								if (member.Type.IsString())
								{
									if (member.IsRequired)
									{
										attributeExpr = $"[{DisallowNullAttributeFullName}]";
										if (!proxyType.EndsWith("?")) proxyType += "?";
									}
									// ToStringOrDefault has no [NotNullIfNotNull], so the flow-state must be forgiven to match the declared member type
									getterExpr ??= $"/* fast-string */ m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToStringOrDefault({defaultValue})!";
								}
								else if (member.IsNullableRefType())
								{
									getterExpr ??= $"/* fast-ref-nullable */ m_value.Get<{member.Type.FullyQualifiedName}?>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})";
								}
								else if (member.IsRequired)
								{
									if (!member.IsNotNull)
									{
										attributeExpr = $"[{DisallowNullAttributeFullName}]";
										if (!proxyType.EndsWith("?")) proxyType += "?";
									}
									getterExpr ??= member.Type.SpecialType switch
									{
										SpecialType.System_Char => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToChar({defaultValue})",
										SpecialType.System_Boolean => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToBoolean({defaultValue})",
										SpecialType.System_Int32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt32({defaultValue})",
										SpecialType.System_UInt32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt32({defaultValue})",
										SpecialType.System_Int64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt64({defaultValue})",
										SpecialType.System_UInt64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt64({defaultValue})",
										SpecialType.System_Single => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToSingle({defaultValue})",
										SpecialType.System_Double => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDouble({defaultValue})",
										SpecialType.System_Decimal => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDecimal({defaultValue})",
										SpecialType.System_DateTime => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDateTime({defaultValue})",
										_ => $"m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})"
									};
									getterExpr = "/* required */ " + getterExpr;
								}
								else if (member.Type.IsValueType() && !member.Type.IsNullableOfT())
								{
									getterExpr ??= member.Type.SpecialType switch
									{
										SpecialType.System_Boolean => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToBoolean({defaultValue})",
										SpecialType.System_Char => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToChar({defaultValue})",
										SpecialType.System_Int32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt32({defaultValue})",
										SpecialType.System_UInt32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt32({defaultValue})",
										SpecialType.System_Int64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt64({defaultValue})",
										SpecialType.System_UInt64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt64({defaultValue})",
										SpecialType.System_Single => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToSingle({defaultValue})",
										SpecialType.System_Double => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDouble({defaultValue})",
										SpecialType.System_Decimal => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDecimal({defaultValue})",
										SpecialType.System_DateTime => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDateTime({defaultValue})",
										_ => $"m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})"
									};
									getterExpr = "/* value-type */ " + getterExpr;
								}
								else if (member.Type.IsNullableOfT())
								{
									getterExpr ??= member.Type.SpecialType switch
									{
										SpecialType.System_Boolean => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToBooleanOrDefault({defaultValue})",
										SpecialType.System_Char => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToCharOrDefault({defaultValue})",
										SpecialType.System_Int32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt32OrDefault({defaultValue})",
										SpecialType.System_UInt32 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt32OrDefault({defaultValue})",
										SpecialType.System_Int64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToInt64OrDefault({defaultValue})",
										SpecialType.System_UInt64 => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToUInt64OrDefault({defaultValue})",
										SpecialType.System_Single => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToSingleOrDefault({defaultValue})",
										SpecialType.System_Double => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDoubleOrDefault({defaultValue})",
										SpecialType.System_Decimal => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDecimalOrDefault({defaultValue})",
										SpecialType.System_DateTime => $"m_value.GetValue({GetTargetPropertyNameRef(typeDef, member)}).ToDateTimeOrDefault({defaultValue})",
										_ => $"m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})"
									};
									getterExpr = "/* vt-nullable */ " + getterExpr;
								}
								else
								{
									getterExpr ??= $"/* else */ m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})";
								}

								if (member.Type.IsStringLike(allowNullables: true))
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
								else if (member.Type.IsBooleanLike(allowNullables: true))
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
								else if (member.Type.IsNumberLike(allowNullables: true))
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
								else if (member.Type.IsDateLike(allowNullables: true))
								{
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
							}
							else if (member.Type.JsonType() is not JsonPrimitiveType.None)
							{
								setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value?.ToJsonValue() ?? JsonNull.Null)";
								proxyType = KnownTypeSymbols.MutableJsonValueFullName;
								getterExpr = $"/* json */ m_value[{GetTargetPropertyNameRef(typeDef, member)}]";
							}
							else if (member.Type.IsDictionary(out var keyType, out var valueType))
							{
								if (keyType.IsString())
								{
									if (IsLocallyGeneratedType(valueType, out target, out _))
									{
										proxyType = $"{KnownTypeSymbols.JsonWritableProxyDictionaryFullName}<{valueType.FullyQualifiedName}, {this.GetLocalWritableProxyRef(target)}>";
										getterExpr = $"/* dict-proxy */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value.ToJsonValue())";
									}
									else if (valueType.IsStringLike() || valueType.IsBooleanLike() || valueType.IsNumberLike() || valueType.IsDateLike())
									{
										proxyType = $"{KnownTypeSymbols.JsonWritableProxyDictionaryFullName}<{valueType.FullyQualifiedNameAnnotated}>";
										getterExpr = $"/* dict */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
										setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value.ToJsonValue())";
									}
									//TODO: other types?
								}
							}
							else if (member.Type.IsEnumerable(out var elemType))
							{
								if (IsLocallyGeneratedType(elemType, out target, out _))
								{
									proxyType = $"{KnownTypeSymbols.JsonWritableProxyArrayFullName}<{elemType.FullyQualifiedName}, {this.GetLocalWritableProxyRef(target)}>";
									getterExpr = $"/* array-proxy */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value.ToJsonValue())";
								}
								else if (elemType.IsStringLike() || elemType.IsBooleanLike() || elemType.IsNumberLike() || elemType.IsDateLike())
								{
									proxyType = $"{KnownTypeSymbols.JsonWritableProxyArrayFullName}<{elemType.FullyQualifiedName}>";
									getterExpr = $"/* array */ new(m_value[{GetTargetPropertyNameRef(typeDef, member)}])";
									setterExpr = $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value.ToJsonValue())";
								}
								//TODO: other types?
							}

							if (getterExpr == null)
							{
								if (member.IsNullableRefType())
								{
									getterExpr = $"/* fallback-ref-nullable */ m_value.Get<{member.Type.FullyQualifiedName}?>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})";
								}
								else if (member.IsRequired)
								{
									getterExpr = $"/* fallback-required */ m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)})";
								}
								else
								{
									getterExpr = $"/* fallback-else */ m_value.Get<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, {defaultValue})";
								}
							}

							if (setterExpr == null && member.EnumFormat is "String" or "Number" && (member.Type.NullableOfType ?? member.Type).IsEnum())
							{ // [JsonProperty(EnumFormat = ...)] forces the wire form written by the proxy setter as well
								var packHelper = $"{KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnum{member.EnumFormat}";
								setterExpr = member.Type.NullableOfType is null
									? $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, {packHelper}(value))"
									: $"m_value.Set({GetTargetPropertyNameRef(typeDef, member)}, value is {{ }} v{member.MemberName} ? {packHelper}(v{member.MemberName}) : {KnownTypeSymbols.JsonNullFullName}.Null)";
							}

							if (setterExpr == null)
							{
								if (member.IsNullableRefType())
								{
									setterExpr ??= $"m_value.Set<{member.Type.FullyQualifiedName}?>({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
								else
								{
									setterExpr ??= $"m_value.Set<{member.Type.FullyQualifiedName}>({GetTargetPropertyNameRef(typeDef, member)}, value)";
								}
							}

							sb.InheritDoc(CSharpCodeBuilder.EscapeCref(typeDef.Type.FullyQualifiedName, member.MemberName));
							if (attributeExpr != null) sb.AppendLine(attributeExpr);
							sb.AppendLine($"public {proxyType} {member.MemberName}");
							sb.EnterBlock();
							sb.AppendLine($"get => {getterExpr};");
							sb.AppendLine($"set => {setterExpr};");
							sb.LeaveBlock();
							sb.NewLine();
						}

						sb.EndRegion();
						sb.NewLine();

						#endregion

					}
				);

				#endregion

				if (!selfType)
				{
					sb.LeaveBlock();
				}
				sb.NewLine();

			}

			private void WriteProxyStaticHelpers(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, string typeCref)
			{
				// Serialize(...)
				if (typeDef.Type.IsValueType())
				{
					sb.XmlComment($"<summary>Writes a JSON representation of a value of type <see cref=\"{typeCref}\" /> to the specified output</summary>");
					sb.AppendLine($"public static void Serialize({KnownTypeSymbols.CrystalJsonWriterFullName} writer, {typeDef.Type.FullyQualifiedName}? instance) => Default.Serialize(writer, instance);");
					sb.NewLine();
				}
				sb.XmlComment($"<summary>Writes a JSON representation of a value of type <see cref=\"{typeCref}\" /> to the specified output</summary>");
				sb.AppendLine($"public static void Serialize({KnownTypeSymbols.CrystalJsonWriterFullName} writer, {typeDef.Type.FullyQualifiedName}{(typeDef.Type.IsValueType() ? "" : "?")} instance) => Default.Serialize(writer, instance);");
				sb.NewLine();

				// ToJsonText(...)
				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a string literal</summary>");
				sb.AppendLine($"public static string ToJsonText({typeDef.Type.FullyQualifiedNameAnnotated} instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => Default.ToJson(instance, {GetSettingsFallbackExpr("settings", compact: false)}, resolver);");
				sb.NewLine();

				// ToJsonBytes(...)
				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a byte array</summary>");
				sb.AppendLine($"public static byte[] ToJsonBytes({typeDef.Type.FullyQualifiedNameAnnotated} instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => {KnownTypeSymbols.CrystalJsonFullName}.ToBytes(instance, Default, {GetSettingsFallbackExpr("settings", compact: true)}, resolver);");
				sb.NewLine();

				// ToJsonSlice(...)
				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a <see cref=\"{KnownTypeSymbols.SliceFullName}\"/></summary>");
				sb.AppendLine($"public static {KnownTypeSymbols.SliceFullName} ToJsonSlice({typeDef.Type.FullyQualifiedNameAnnotated} instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => {KnownTypeSymbols.CrystalJsonFullName}.ToSlice(instance, Default, {GetSettingsFallbackExpr("settings", compact: true)}, resolver);");
				sb.NewLine();

				// ToJsonSlice(...)
				sb.XmlComment($"<summary>Serializes a value of type <see cref=\"{typeCref}\" /> into a <see cref=\"{KnownTypeSymbols.SliceFullName}\"/>, using the specified <see cref=\"{KnownTypeSymbols.ArrayPoolFullName}{{T}}\"/></summary>");
				sb.AppendLine($"public static {KnownTypeSymbols.SliceOwnerFullName} ToJsonSlice({typeDef.Type.FullyQualifiedNameAnnotated} instance, {KnownTypeSymbols.ArrayPoolFullName}<byte>? pool, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => {KnownTypeSymbols.CrystalJsonFullName}.ToSlice(instance, Default, pool, {GetSettingsFallbackExpr("settings", compact: true)}, resolver);");
				sb.NewLine();

				// Deserialize(...)
				sb.AppendLine($"public static {typeDef.Type.FullyQualifiedName} Deserialize(string jsonText, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => Default.Deserialize(jsonText, settings, resolver);");
				sb.NewLine();

				// Pack(...)
				if (typeDef.Type.IsValueType())
				{
					sb.XmlComment($"<summary>Converts an instance of this type into the equivalent <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/></summary>");
					sb.AppendLine($"public static {KnownTypeSymbols.JsonValueFullName} Pack({typeDef.Type.FullyQualifiedName}? instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => Default.Pack(instance, settings, resolver);");
					sb.NewLine();
				}

				sb.XmlComment($"<summary>Converts an instance of this type into the equivalent <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/></summary>");
				sb.AppendLine($"public static {KnownTypeSymbols.JsonValueFullName} Pack({typeDef.Type.FullyQualifiedName}{(typeDef.Type.IsValueType() ? "" : "?")} instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => Default.Pack(instance, settings, resolver);");
				sb.NewLine();

				// Unpack(...)
				sb.XmlComment($"<summary>Deserializes a JSON value into an instance of type <see cref=\"{typeCref}\" /></summary>");
				sb.AppendLine($"public static {typeDef.Type.FullyQualifiedNameAnnotated} Unpack({KnownTypeSymbols.JsonValueFullName} value, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default) => Default.Unpack(value, resolver);");
				sb.NewLine();

				// ToReadOnly(JsonValue)
				sb.XmlComment($"<summary>Returns a read-only JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>The read-only view cannot modify the original JSON value but, unless <paramref name=\"value\"/> is itself read-only, any changes to the original will be reflected in the view.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(value);");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToMutable({KnownTypeSymbols.JsonValueFullName})\">If you need a writable view</seealso>");
				sb.AppendLine($"public static {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.JsonValueFullName} value) => {GetLocalReadOnlyProxyRef(typeDef)}.Create({KnownTypeSymbols.ObservableJsonValueFullName}.Untracked(value), Default);");
				sb.NewLine();

				// ToReadOnly(IObservableJsonContext, JsonValue)
				sb.XmlComment($"<summary>Returns a read-only JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>The read-only view cannot modify the original JSON value but, unless <paramref name=\"value\"/> is itself read-only, any changes to the original will be reflected in the view.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(value);");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToMutable({KnownTypeSymbols.JsonValueFullName})\">If you need a writable view</seealso>");
				sb.AppendLine($"public static {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value) => {GetLocalReadOnlyProxyRef(typeDef)}.Create({KnownTypeSymbols.ObservableJsonValueFullName}.Tracked(ctx, value), Default);");
				sb.NewLine();

				// ToReadOnly(TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
					sb.XmlComment("// ...");
					sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(instance);");
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = /* ... */; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public static {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalReadOnlyProxyRef(typeDef)}.Create(instance);");
				sb.NewLine();

				// ToReadOnly(IObservableJsonContext, TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
					sb.XmlComment("// ...");
					sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(instance);");
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = /* ... */; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public static {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalReadOnlyProxyRef(typeDef)}.Create(ctx, instance);");
				sb.NewLine();

				// ToMutable(MutableJsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // change the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public static {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.MutableJsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create(value, converter: Default);");
				sb.NewLine();

				// ToMutable(JsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // change the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public static {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.JsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create({KnownTypeSymbols.MutableJsonValueFullName}.Untracked(value), converter: Default);");
				sb.NewLine();

				// ToMutable(IMutableJsonContext, JsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // change the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public static {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create({KnownTypeSymbols.MutableJsonValueFullName}.Tracked(ctx, value), converter: Default);");
				sb.NewLine();

				// ToMutable(TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(instance);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue;");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public static {GetLocalWritableProxyRef(typeDef)} ToMutable({typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalWritableProxyRef(typeDef)}.Create(instance);");
				sb.NewLine();

				// ToMutable(IMutableJsonContext, TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(instance);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue;");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public static {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalWritableProxyRef(typeDef)}.Create(ctx, instance);");
				sb.NewLine();

			}

			private void WriteProxyInstanceHelpers(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, string typeCref)
			{

				// TryMapMemberToPropertyName()
				sb.InheritDoc();
				sb.AppendLine($"public bool TryMapMemberToPropertyName(string memberName, [{MaybeNullWhenAttributeFullName}(false)] out string propertyName)");
				sb.EnterBlock();
				sb.AppendLine("propertyName = memberName switch");
				sb.EnterBlock();
				foreach (var member in typeDef.Members)
				{
					sb.AppendLine($"{GetMemberNameExpr(typeDef, member)} => {GetLocalPropertyNameRef(member)},");
				}

				sb.AppendLine("_ => null,");
				sb.LeaveBlock(suffix: ';');
				sb.AppendLine("return propertyName != null;");
				sb.LeaveBlock();
				sb.NewLine();

				// TryMapMemberToPropertyName()
				sb.InheritDoc();
				sb.AppendLine($"public bool TryMapPropertyToMemberName(string propertyName, [{MaybeNullWhenAttributeFullName}(false)] out string memberName)");
				sb.EnterBlock();
				sb.AppendLine("memberName = propertyName switch");
				sb.EnterBlock();
				foreach (var member in typeDef.Members)
				{
					sb.AppendLine($"{GetLocalPropertyNameRef(member)} => {GetMemberNameExpr(typeDef, member)},");
				}

				sb.AppendLine("_ => null,");
				sb.LeaveBlock(suffix: ';');
				sb.AppendLine("return memberName != null;");
				sb.LeaveBlock();
				sb.NewLine();

				// ToReadOnly(JsonValue)
				sb.XmlComment($"<summary>Returns a read-only JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>The read-only view cannot modify the original JSON value but, unless <paramref name=\"value\"/> is itself read-only, any changes to the original will be reflected in the view.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(json);");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToMutable({KnownTypeSymbols.JsonValueFullName})\">If you need a writable view</seealso>");
				sb.AppendLine($"public {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.JsonValueFullName} value) => {GetLocalReadOnlyProxyRef(typeDef)}.Create({KnownTypeSymbols.ObservableJsonValueFullName}.Untracked(value), Default);");
				sb.NewLine();

				// ToReadOnly(IObservableJsonContext, JsonValue)
				sb.XmlComment($"<summary>Returns a read-only JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>The read-only view cannot modify the original JSON value but, unless <paramref name=\"value\"/> is itself read-only, any changes to the original will be reflected in the view.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(json);");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToMutable({KnownTypeSymbols.JsonValueFullName})\">If you need a writable view</seealso>");
				sb.AppendLine($"public {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value) => {GetLocalReadOnlyProxyRef(typeDef)}.Create({KnownTypeSymbols.ObservableJsonValueFullName}.Tracked(ctx, value), this);");
				sb.NewLine();

				// ToReadOnly(TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
					sb.XmlComment("// ...");
					sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(instance);");
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = /* ... */; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalReadOnlyProxyRef(typeDef)}.Create(instance);");
				sb.NewLine();

				// ToReadOnly(IObservableJsonContext, TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as getter-only properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				if (typeDef.Members.Count > 0)
				{ // the example names a real member, and a type can legitimately have none (every member excluded by its contract)
					sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
					sb.XmlComment("// ...");
					sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToReadOnly(instance);");
					sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
					sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = /* ... */; // ERROR: will not compile (there is no setter defined for this member)");
				}
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public {GetLocalReadOnlyProxyRef(typeDef)} ToReadOnly({KnownTypeSymbols.IObservableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalReadOnlyProxyRef(typeDef)}.Create(ctx, instance);");
				sb.NewLine();

				// ToMutable(MutableJsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // changes the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.MutableJsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create(value, converter: this);");
				sb.NewLine();

				// ToMutable(JsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // changes the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.JsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create({KnownTypeSymbols.MutableJsonValueFullName}.Untracked(value), converter: this);");
				sb.NewLine();

				// ToMutable(IMutableJsonContext, JsonValue)
				sb.XmlComment($"<summary>Returns a writable JSON Proxy that wraps a <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/> into a type-safe emulation of type <see cref=\"{typeCref}\"/></summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalWritableProxyRef(typeDef)}\"/> that wraps <paramref name=\"value\"/> and exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>If <paramref name=\"value\"/> is read-only, a mutable copy will be created and used instead.</para>");
				sb.XmlComment($"<para>If <paramref name=\"value\"/> is mutable, then it will be modified in-place. You can call <see cref=\"{KnownTypeSymbols.JsonValueFullName}.ToMutable\"/> if you need to make a copy in all cases.</para>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var json = {KnownTypeSymbols.JsonValueFullName}.Parse(/* JSON text */);");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(json);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)}; // returns the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field exposed as <see cref=\"{GetSampleMemberType(typeDef)}\"/>");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue; // changes the value of the {CSharpCodeBuilder.Constant(GetSampleMemberWireName(typeDef))} field");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.XmlComment($"<seealso cref=\"ToReadOnly({KnownTypeSymbols.JsonValueFullName})\">If you need a read-only view</seealso>");
				sb.AppendLine($"public {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {KnownTypeSymbols.JsonValueFullName} value) => {GetLocalWritableProxyRef(typeDef)}.Create({KnownTypeSymbols.MutableJsonValueFullName}.Tracked(ctx, value), converter: this);");
				sb.NewLine();

				// ToMutable(TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>\r\n");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(instance);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue;");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public {GetLocalWritableProxyRef(typeDef)} ToMutable({typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalWritableProxyRef(typeDef)}.Create(instance);");
				sb.NewLine();

				// ToMutable(IMutableJsonContext, TValue)
				sb.XmlComment($"<summary>Converts an instance of type <see cref=\"{typeCref}\"/> into a read-only type-safe JSON Proxy.</summary>");
				sb.XmlComment($"<returns>An instance of <see cref=\"{GetLocalReadOnlyProxyRef(typeDef)}\"/> that exposes all the original members of <see cref=\"{typeCref}\"/> as writable properties.</returns>\r\n");
				sb.XmlComment("<remarks>");
				sb.XmlComment("<para>How to use:<code>");
				sb.XmlComment($"var instance = new {typeDef.Name}() {{ {GetSampleMemberName(typeDef)} = ..., ... }};");
				sb.XmlComment("// ...");
				sb.XmlComment($"var proxy = {GetSerializerName(typeDef.Type)}.ToMutable(instance);");
				sb.XmlComment($"var value = proxy.{GetSampleMemberName(typeDef)};");
				sb.XmlComment($"proxy.{GetSampleMemberName(typeDef)} = newValue;");
				sb.XmlComment("</code></para>");
				sb.XmlComment("</remarks>");
				sb.AppendLine($"public {GetLocalWritableProxyRef(typeDef)} ToMutable({KnownTypeSymbols.IMutableJsonContextFullName} ctx, {typeDef.Type.FullyQualifiedNameAnnotated} instance) => {GetLocalWritableProxyRef(typeDef)}.Create(ctx, instance);");
				sb.NewLine();
			}

			private void WriteTypeDefinitionHelpers(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				// GetTypeDefinition()
				sb.XmlComment("<summary>Returns the definition for this type</summary>");
				sb.AppendLine($"public {KnownTypeSymbols.CrystalJsonTypeDefinitionFullName} GetDefinition() => m_typeDefinition ??= CreateDefinition();");
				sb.NewLine();
				sb.AppendLine($"private {KnownTypeSymbols.CrystalJsonTypeDefinitionFullName}? m_typeDefinition;");
				sb.NewLine();
				sb.XmlComment("<summary>Returns the definition for this type</summary>");
				sb.AppendLine($"private static {KnownTypeSymbols.CrystalJsonTypeDefinitionFullName} CreateDefinition()");
				sb.EnterBlock();

				sb.BeginRegion("Members...");
				sb.AppendLine($"{KnownTypeSymbols.CrystalJsonMemberDefinitionFullName}[] members =");
				sb.EnterCollection();
				List<string> flags = [ ];
				foreach (var member in typeDef.Members)
				{
					// construct the member's flags
					flags.Clear();
					flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".SourceGenerated");
					if (member.IsNotNull) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".NotNull");
					if (member.HasNonZeroDefault) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".NonZeroDefault");
					if (member.IsReadOnly) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".ReadOnly");
					if (member.IsInitOnly) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".InitOnly");
					if (member.IsRequired) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".Required");
					if (member.IsKey) flags.Add(KnownTypeSymbols.CrystalJsonMemberFlagsFullName + ".Key");

					sb.AppendLine("new()");
					sb.EnterBlock(comment: member.MemberName);
					sb.AppendLine($"Type = typeof({member.Type.FullyQualifiedName}),");
					sb.AppendLine($"Flags = {string.Join(" | ", flags)},");
					sb.AppendLine($"Name = {GetLocalPropertyNameRef(member)},");
					sb.AppendLine($"OriginalName = {GetMemberNameExpr(typeDef, member)},");
					sb.AppendLine($"EncodedName = {GetPropertyEncodedNameRef(member)},");
					if (member.Type.NullableOfType != null) sb.AppendLine($"NullableOfType = typeof({member.Type.NullableOfType.FullyQualifiedName}),");
					if (member.DefaultLiteral is "default")
					{
						sb.AppendLine($"DefaultValue = default({member.Type.FullyQualifiedNameAnnotated}),");
					}
					else
					{
						sb.AppendLine($"DefaultValue = {GetForgivingDefaultLiteral(member)},");
					}
					//TODO: Attributes? is it needed?
					string typedInstance = $"(({typeDef.Type.FullyQualifiedName}) instance)";
					if (!member.NeedsGetterThunk)
					{
						sb.AppendLine($"Getter = (instance) => {typedInstance}.{member.MemberName},");
					}
					else if (!typeDef.Type.IsValueType())
					{
						sb.AppendLine($"Getter = (instance) => __get_{member.MemberName}({typedInstance}),");
					}
					else
					{ // the thunk takes the struct by ref
						sb.AppendLine($"Getter = (instance) => {{ var typed = ({typeDef.Type.FullyQualifiedName}) instance; return __get_{member.MemberName}(ref typed); }},");
					}
					if (!member.IsReadOnly && !member.IsInitOnly)
					{
						// the value expression per shape (shared by the direct assignment and the thunk call)
						string valueExpr;
						string tag;
						if (member.Type.IsValueType())
						{ // struct that _could_ be null
							valueExpr = $"value is not null ? ({member.Type.FullyQualifiedName}) value : {GetForgivingDefaultLiteral(member)}";
							tag = "value-type";
						}
						else if (member.HasNonZeroDefault)
						{ // a ref type _could_ be null, but the setter does not allow it...
							valueExpr = $"value is not null ? ({member.Type.FullyQualifiedName}) value : {GetForgivingDefaultLiteral(member)}";
							tag = "has-default-value";
						}
						else if (member.IsNotNull && member.Type.IsString())
						{ // use string.Empty
							valueExpr = $"value is not null ? ({member.Type.FullyQualifiedName}) value : \"\"";
							tag = "not-null-string";
						}
						else if (member.IsNotNull && member.Type.IsEnumerable(out _))
						{ // not-null collection type without a default value, we will inject a default empty collection expression
							valueExpr = $"value is not null ? ({member.Type.FullyQualifiedName}) value : [ ]";
							tag = "not-null-collection";
						}
						else
						{
							valueExpr = $"({member.Type.FullyQualifiedNameAnnotated}) value!";
							tag = "fallback";
						}
						if (!member.NeedsSetterThunk)
						{
							sb.AppendLine($"Setter = (instance, value) => {typedInstance}.{member.MemberName} = {valueExpr} /* {tag} */,");
						}
						else if (!typeDef.Type.IsValueType())
						{
							sb.AppendLine($"Setter = (instance, value) => __set_{member.MemberName}({typedInstance}, {valueExpr}) /* {tag} */,");
						}
						else
						{ // same boxed-copy semantics as the direct struct assignment above
							sb.AppendLine($"Setter = (instance, value) => {{ var typed = ({typeDef.Type.FullyQualifiedName}) instance; __set_{member.MemberName}(ref typed, {valueExpr}); }} /* {tag} */,");
						}
					}

					// if we are deserializing a (non-nullable) ValueType, the "instance" arg could still be null!
					// => it will call the Nullable<T> variant of Default.Serialize(...) which should be generated automatically
					sb.AppendLine($"Visitor = (instance, declaredType, runtimeType, writer) => Default.Serialize(writer, ({typeDef.Type.FullyQualifiedName}?) instance),");

					if (IsLocallyGeneratedType(member.Type, out var target, out _))
					{
						sb.AppendLine($"Binder = (instance, type, resolver) => instance is not null ? {GetLocalSerializerRef(target)}.Unpack(instance, resolver) : {KnownTypeSymbols.JsonNullFullName}.Null,");
					}
					else if (member.Type.SpecialType == SpecialType.System_String)
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToStringOrDefault(),");
					}
					else if (member.Type.SpecialType == SpecialType.System_Boolean)
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToBooleanOrDefault(),");
					}
					else if (member.Type.SpecialType == SpecialType.System_Int32)
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToInt32OrDefault(),");
					}
					else if (member.Type.SpecialType == SpecialType.System_Int64)
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToInt64OrDefault(),");
					}
					else if (member.Type.IsGuid())
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToGuidOrDefault(),");
					}
					else if (member.Type.IsDateTime())
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToDateTimeOrDefault(),");
					}
					else if (member.Type.IsDateTimeOffset())
					{
						sb.AppendLine("Binder = (instance, type, resolver) => instance?.ToDateTimeOffsetOrDefault(),");
					}
					else
					{
						sb.AppendLine($"Binder = (instance, type, resolver) => (instance ?? {KnownTypeSymbols.JsonNullFullName}.Missing).Bind(type, resolver),");
					}
					sb.LeaveBlock(suffix: ',');
				}
				sb.LeaveCollection(suffix: ';');
				sb.EndRegion();

				sb.AppendLine($"{KnownTypeSymbols.CrystalJsonTypeVisitorFullName} visitor = (({KnownTypeSymbols.IJsonConverterInterfaceFullName}) {GetLocalSerializerRef(typeDef)}).Serialize;");

				sb.AppendLine($"{KnownTypeSymbols.CrystalJsonTypeBinderFullName} binder = (instance, type, resolver) => instance is not null ? Default.Unpack(instance, resolver) : {KnownTypeSymbols.JsonNullFullName}.Null;");
				if (typeDef.IsPolymorphicRoot)
				{
					sb.AppendLine($"var map = new {DictionaryFullName}<{KnownTypeSymbols.JsonValueFullName}, {SystemTypeFullName}>({KnownTypeSymbols.JsonValueComparerFullName}.Default);");
					foreach (var x in typeDef.DerivedTypes)
					{
						if (x.Discriminator is not null)
						{
							sb.AppendLine($"map[{ConvertDiscriminatorValueToJsonLiteral(x.Discriminator)}] = typeof({x.Type.FullyQualifiedName});");
						}
					}
					sb.AppendLine($"return new(typeof({typeDef.Type.FullyQualifiedName}), {KnownTypeSymbols.CrystalJsonTypeFlagsFullName}.SourceGenerated, binder, null, members, visitor, null, PropertyEncodedNames._TypeDiscriminatorProperty_, null, map);");
				}
				else if (this.PolymorphicMap.TryGetValue(typeDef.Type.Ref, out var polymorphicMetadata))
				{
					if (polymorphicMetadata.Discriminator is null)
					{ // derive type is also abstract
						sb.AppendLine($"return new(typeof({typeDef.Type.FullyQualifiedName}), {KnownTypeSymbols.CrystalJsonTypeFlagsFullName}.SourceGenerated, binder, null, members, visitor, null, PropertyEncodedNames._TypeDiscriminatorProperty_, null, null);");
					}
					else
					{
						sb.AppendLine($"return new(typeof({typeDef.Type.FullyQualifiedName}), {KnownTypeSymbols.CrystalJsonTypeFlagsFullName}.SourceGenerated, binder, null, members, visitor, null, PropertyEncodedNames._TypeDiscriminatorProperty_, PropertyEncodedNames._TypeDiscriminatorValue_, null);");
					}
				}
				else
				{
					sb.AppendLine($"return new(typeof({typeDef.Type.FullyQualifiedName}), {KnownTypeSymbols.CrystalJsonTypeFlagsFullName}.SourceGenerated, binder, null, members, visitor, null, null, null, null);");
				}
				sb.LeaveBlock();
				sb.NewLine();

			}

			private void WriteUnpackMethod(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, string typeCref)
			{
				sb.InheritDoc();
				sb.AppendLine($"object? {KnownTypeSymbols.IJsonConverterInterfaceFullName}.BindJsonValue({KnownTypeSymbols.JsonValueFullName} value, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver) => Unpack(value, default);");
				sb.NewLine();

				sb.XmlComment($"<summary>Deserializes a JSON value into an instance of type <see cref=\"{typeCref}\" /></summary>");
				sb.AppendLine($"public {typeDef.Type.FullyQualifiedName} Unpack({KnownTypeSymbols.JsonValueFullName} value, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default)");
				sb.EnterBlock();

				if (typeDef.IsPolymorphicRoot)
				{ // this is a polymorphic type, we have to dispatch to the corresponding derived type converter!

					// get the type discriminator
					sb.AppendLine("var discriminator = value[PropertyNames._TypeDiscriminatorProperty_];");
					foreach (var (_, derivedType, discriminator) in typeDef.DerivedTypes)
					{
						if (derivedType.IsAbstract)
						{ // don't include intermediary abstract types, we will only process the concrete types (the ones that we can actually create a runtime)
							continue;
						}
						switch (discriminator)
						{
							case null:
							{
								//REVIEW: how do we handle this case?
								break;
							}
							case string s:
							{
								sb.AppendLine($"if (discriminator.ValueEquals({CSharpCodeBuilder.Constant(s)})) return {GetLocalSerializerRef(derivedType)}.Unpack(value, resolver);");
								break;
							}
							case int n:
							{
								sb.AppendLine($"if (discriminator.ValueEquals({CSharpCodeBuilder.Constant(n)})) return {GetLocalSerializerRef(derivedType)}.Unpack(value, resolver);");
								break;
							}
							default:
							{
								sb.AppendLine($"#error Invalid discriminator value type for derived type {derivedType.Name} of parent type {typeDef.Name}");
								break;
							}
						}
					}

					sb.AppendLine($"throw {KnownTypeSymbols.JsonBindingExceptionFullName}.CannotDeserializeCustomTypeWithUnknownTypeDiscriminator(value, typeof({typeDef.Type.FullyQualifiedName}), discriminator);");
					sb.LeaveBlock();
					sb.NewLine();
					return;
				}

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(typeDef.Type.Ref, out var polymorphicMetadata);
				if (!typeDef.Type.IsSealed)
				{ // do we have a parent ?
					if (hasPolymorphicDefinition)
					{ // defer to the parent type which should have all the derived types under this one
						//PERF: we _could_ optimize by having a smaller switch with only de types under us?
						sb.AppendLine($"return ({typeDef.Type.FullyQualifiedName}) {GetLocalSerializerRef(polymorphicMetadata.Parent)}.Unpack(value, resolver);");
						sb.LeaveBlock();
						sb.NewLine();
						return;
					}
				}

				sb.AppendLine("var obj = value.AsObject();");

				//BUGBUG: we need to check that, if there is a $type, it matches with the expected value ?

				// members behind a setter thunk cannot appear in the object initializer: they are written after
				// construction, so the initializer form only remains when every bound member is directly reachable
				this.DeferredUnpackAssignments.Clear();
				bool hasThunkedWrites = false;
				foreach (var member in typeDef.Members)
				{
					if (member.NeedsSetterThunk) { hasThunkedWrites = true; break; }
				}

				// [DataMember(IsRequired = true)]: presence is independent of how the value decodes, so it is one guard per
				// member emitted before the initializer, rather than a variant of every decoding shape below.
				// An explicit null falls through to the normal (optional) decode, which is the DCJS contract.
				foreach (var member in typeDef.Members)
				{
					if (member.IsRequiredPresence)
					{
						sb.AppendLine($"{KnownTypeSymbols.JsonSerializerExtensionsFullName}.VerifyRequiredPresence(obj, {CSharpCodeBuilder.Constant(member.Name)});");
					}
				}

				// [OnDeserializing] must observe a constructed, unpopulated instance, which an object initializer cannot offer
				this.UnpackAsStatements = typeDef.OnDeserializing != null;
				if (this.UnpackAsStatements)
				{
					sb.AppendLine($"var instance = new {typeDef.Type.FullyQualifiedName}();");
					EmitCallbackInvocation(sb, typeDef.OnDeserializing, "instance", "obj");
					sb.NewLine();
				}
				else
				{ // a post-populate callback also needs a local to run against, so it forces the instance form (initializer kept, so init-only and required members still bind)
					bool needsLocal = hasThunkedWrites || typeDef.OnDeserialized != null;
					sb.AppendLine(needsLocal ? $"{typeDef.Type.FullyQualifiedName} instance = new ()" : "return new ()");
					sb.EnterBlock();
				}
				foreach (var member in typeDef.Members)
				{
					if (member.CustomConverterType != null)
					{ // a custom converter takes over the member's wire form
						var converterRef = GetMemberConverterRef(member);
						if (!member.CustomConverterHasDeserializer)
						{ // asymmetric converter without the deserializing facet: an absent value binds to the default, anything else fails loudly
							if (member.IsRequired)
							{
								EmitUnpackAssignment(sb, member, $"/* member-converter (missing deserializer facet) */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingDeserializerFacet<{member.Type.FullyQualifiedName}>(typeof({member.CustomConverterType}))");
							}
							else
							{
								EmitUnpackAssignment(sb, member, $"/* member-converter (missing deserializer facet) */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingDeserializerFacet<{member.Type.FullyQualifiedNameAnnotated}>(obj[{GetLocalPropertyNameRef(member)}], typeof({member.CustomConverterType}), {GetForgivingDefaultLiteral(member)})!");
							}
							continue;
						}
						if (member.CustomConverterIsNullableForm)
						{ // converter declared for the T? form itself: it owns every PRESENT value, the pipeline still owns null/missing
							EmitUnpackAssignment(sb, member, member.IsRequired
								? $"/* member-converter-nullable-form-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequiredNullableForm({converterRef}, obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})"
								: $"/* member-converter-nullable-form */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullableForm({converterRef}, obj[{GetLocalPropertyNameRef(member)}], resolver)");
						}
						else if (member.IsRequired)
						{
							EmitUnpackAssignment(sb, member, $"/* member-converter-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired({converterRef}, obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})");
						}
						else if (member.Type.NullableOfType is not null)
						{
							EmitUnpackAssignment(sb, member, $"/* member-converter-nullable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullable({converterRef}, obj[{GetLocalPropertyNameRef(member)}], resolver)");
						}
						else
						{
							EmitUnpackAssignment(sb, member, $"/* member-converter-optional */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack({converterRef}, obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)!");
						}
						continue;
					}

					if (IsLocallyGeneratedType(member.Type, out var target, out var isNullableOfT))
					{
						if (member.IsRequired)
						{
							// REVIEW: what if isNullableOfT is true ? this is a bit weird to have nullable value type that is also required??
							EmitUnpackAssignment(sb, member, $"/* local-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})");
						}
						else if (isNullableOfT)
						{
							EmitUnpackAssignment(sb, member, $"/* local-optional-nullable */ {GetLocalSerializerRef(target)}.UnpackNullable(obj[{GetLocalPropertyNameRef(member)}], resolver)");
						}
						else
						{
							EmitUnpackAssignment(sb, member, $"/* local-optional */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)");
						}
						continue;
					}

					if (member.Type.IsJsonDeserializable())
					{
						if (member.IsRequired)
						{
							EmitUnpackAssignment(sb, member, $"/* deserializable-required */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequiredJsonDeserializable<{member.Type.FullyQualifiedName}>(obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})");
						}
						else
						{
							EmitUnpackAssignment(sb, member, $"/* deserializable-optional */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackJsonDeserializable<{member.Type.FullyQualifiedNameAnnotated}>(obj[{GetLocalPropertyNameRef(member)}], resolver)");
						}
						continue;
					}

					if (member.Type.IsPrimitive)
					{
						if (member.IsRequired)
						{
							EmitUnpackAssignment(sb, member, $"/* fast-required */ obj.Get<{member.Type.FullyQualifiedName}>({GetLocalPropertyNameRef(member)})");
						}
						else
						{
							EmitUnpackAssignment(sb, member, $"/* fast-optional */ obj.Get<{member.Type.FullyQualifiedNameAnnotated}>({GetLocalPropertyNameRef(member)}, {GetForgivingDefaultLiteral(member)})");
						}
						continue;
					}

					if (member.Type.IsEnumerable(out var elemType))
					{
						// note: we have to also know the target enumerable type: array? list? other?

						string sequenceShape = member.Type.IsArray() ? "Array" : member.Type.IsList() ? "List" : member.Type.IsEnumerableInterface(out _) ? "Enumerable" : "Unknown"; //TODO: support more ?
						if (sequenceShape != "Unknown")
						{
							if (IsLocallyGeneratedType(elemType, out target, out isNullableOfT))
							{
								if (member.IsRequired)
								{
									// REVIEW: what if isNullableOfT is true ? this is a bit weird to have nullable value type that is also required??
									EmitUnpackAssignment(sb, member, $"/* local-required-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired{sequenceShape}({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})!");
								}
								else if (isNullableOfT)
								{
									EmitUnpackAssignment(sb, member, $"/* local-optional-nullable-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackNullable{sequenceShape}({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)!");
								}
								else
								{
									EmitUnpackAssignment(sb, member, $"/* local-optional-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack{sequenceShape}({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)!");
								}
								continue;
							}

							string elemShape = elemType.SpecialType switch
							{
								SpecialType.System_String => "String",
								SpecialType.System_Int32 => "Int32",
								SpecialType.System_Int64 => "Int64",
								SpecialType.System_Double => "Double",
								_ => "Unknown"
							};
							if (elemShape != "Unknown")
							{
								if (member.IsRequired)
								{
									EmitUnpackAssignment(sb, member, $"/* string-required-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired{elemShape}{sequenceShape}(obj[{GetLocalPropertyNameRef(member)}], obj, {CSharpCodeBuilder.Constant(member.MemberName)})!");
								}
								else
								{
									EmitUnpackAssignment(sb, member, $"/* string-optional-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack{elemShape}{sequenceShape}(obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)})!");
								}
								continue;
							}

							//TODO: support for Int32, Int64, Guid, etc...? (including their nullable variants)

							if (member.IsRequired)
							{
								EmitUnpackAssignment(sb, member, $"/* fallback-required-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequired{sequenceShape}<{elemType.FullyQualifiedName}>(obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})!");
							}
							else
							{
								EmitUnpackAssignment(sb, member, $"/* fallback-optional-{sequenceShape} */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.Unpack{sequenceShape}<{elemType.FullyQualifiedName}>(obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)!");
							}
							continue;
						}

						if (member.Type is { TypeKind: TypeKind.Class, IsAbstract: false, HasDefaultConstructor: true, ImplementsGenericICollection: true }
						 && IsLocallyGeneratedType(elemType, out target, out _))
						{ // custom addable collection (Collection<T>, ObservableCollection<T>, a KeyedCollection<,> subclass, ...) of a generated
						  // type: construct the declared collection type and Add() each element decoded by the local element serializer, so that
						  // the container's naming policy applies to the elements (the runtime As<> fallback would not know it)
							if (member.IsRequired)
							{
								EmitUnpackAssignment(sb, member, $"/* local-required-Collection */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackRequiredCollection<{member.Type.FullyQualifiedName}, {elemType.FullyQualifiedName}>({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], resolver, obj, {CSharpCodeBuilder.Constant(member.MemberName)})!");
							}
							else
							{
								EmitUnpackAssignment(sb, member, $"/* local-optional-Collection */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.UnpackCollection<{member.Type.FullyQualifiedName}, {elemType.FullyQualifiedName}>({GetLocalSerializerRef(target)}, obj[{GetLocalPropertyNameRef(member)}], {GetForgivingDefaultLiteral(member)}, resolver)!");
							}
							continue;
						}
					}

					string jsonExpr;
					if (member.Type.JsonType() is JsonPrimitiveType.Object)
					{
						jsonExpr = $"/* json-object */ obj.{(member.IsRequired ? "GetObject" : "GetObjectOrDefault")}({GetLocalPropertyNameRef(member)})";
					}
					else if (member.Type.JsonType() is JsonPrimitiveType.Array)
					{
						jsonExpr = $"/* json-array */ obj.{(member.IsRequired ? "GetArray" : "GetArrayOrDefault")}({GetLocalPropertyNameRef(member)})";
					}
					else if (member.IsRequired)
					{
						jsonExpr = $"/* required */ obj.GetValue({GetLocalPropertyNameRef(member)})";
					}
					else
					{
						jsonExpr = $"/* ref-nullable */ obj.GetValueOrDefault({GetLocalPropertyNameRef(member)})";
					}

					string getterExpr;
					if (member.Type.JsonType() is not JsonPrimitiveType.None)
					{
						getterExpr = member.Type.JsonType() switch
						{
							JsonPrimitiveType.Value or JsonPrimitiveType.Object or JsonPrimitiveType.Array => jsonExpr,
							_ => $"((Json{member.Type.JsonType()}) {jsonExpr})" //TODO: we don't have specialized "As" methods for them!
						};
					}
					else if (member.IsRequired)
					{
						getterExpr = $"{jsonExpr}.As<{member.Type.FullyQualifiedName}>({GetForgivingDefaultLiteral(member)})!";
					}
					else
					{
						getterExpr = $"{jsonExpr}.As<{member.Type.FullyQualifiedNameAnnotated}>({GetForgivingDefaultLiteral(member)})";
					}

					EmitUnpackAssignment(sb, member, $"{getterExpr}");

				}
				if (this.UnpackAsStatements)
				{
					sb.NewLine();
					EmitCallbackInvocation(sb, typeDef.OnDeserialized, "instance", "obj");
					sb.AppendLine("return instance;");
					this.UnpackAsStatements = false;
				}
				else
				{
					sb.LeaveBlock(suffix: ';');
					if (hasThunkedWrites || typeDef.OnDeserialized != null)
					{
						string instanceArg = typeDef.Type.IsValueType() ? "ref instance" : "instance";
						foreach (var (member, expr) in this.DeferredUnpackAssignments)
						{
							sb.AppendLine($"__set_{member.MemberName}({instanceArg}, {expr});");
						}
						this.DeferredUnpackAssignments.Clear();
						EmitCallbackInvocation(sb, typeDef.OnDeserialized, "instance", "obj");
						sb.AppendLine("return instance;");
					}
				}
				sb.LeaveBlock();
				sb.NewLine();
			}

			/// <summary>List of member bindings of the Unpack method being generated that must go through a setter thunk (filled while the object-initializer entries are emitted, flushed after construction)</summary>
			private List<(CrystalJsonMemberMetadata Member, string Expr)> DeferredUnpackAssignments { get; } = [ ];

			/// <summary>Emits one member binding of the Unpack method: an object-initializer entry for a directly-reachable member, or a deferred thunk write for a non-public one</summary>
			private void EmitUnpackAssignment(CSharpCodeBuilder sb, CrystalJsonMemberMetadata member, string expr)
			{
				if (this.UnpackAsStatements)
				{ // construct-then-assign form: the members are written as statements so a pre-populate callback can bracket them
					sb.AppendLine(member.NeedsSetterThunk
						? $"__set_{member.MemberName}(instance, {expr});"
						: $"instance.{member.MemberName} = {expr};");
					return;
				}

				if (!member.NeedsSetterThunk)
				{
					sb.AppendLine($"{member.MemberName} = {expr},");
				}
				else
				{
					this.DeferredUnpackAssignments.Add((member, expr));
				}
			}

			/// <summary>When set, Unpack constructs the instance first and writes members as statements, instead of using an object initializer</summary>
			/// <remarks>Required by <c>[OnDeserializing]</c>, which must run on a constructed but UNPOPULATED instance: an object initializer leaves no point between the two.</remarks>
			private bool UnpackAsStatements { get; set; }

			/// <summary>Returns the settings expression a generated entry point uses when the caller passed none: the container's baked wire profile, or the standard defaults</summary>
			/// <remarks>Explicitly passed settings always replace the profile ENTIRELY (no merging): a merged wire would be unauditable. Settings the baked names cannot honor are refused by the guard in the Serialize method.</remarks>
			private string GetSettingsFallbackExpr(string settingsVar, bool compact)
			{
				if (this.Metadata.WireProfile == "DataContractCompat")
				{
					var profile = $"{KnownTypeSymbols.CrystalJsonSettingsFullName}.DataContractCompat";
					return compact ? $"{settingsVar} ?? {profile}.Compacted()" : $"{settingsVar} ?? {profile}";
				}
				return compact ? $"{settingsVar} ?? {KnownTypeSymbols.CrystalJsonSettingsFullName}.JsonCompact" : settingsVar;
			}

			/// <summary>Returns the C# expression for the member's C# name: <c>nameof(...)</c> when the member is reachable, a plain string constant when it is not (<c>nameof</c> requires an accessible member)</summary>
			private static string GetMemberNameExpr(CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
				=> member.IsNonPublic
					? CSharpCodeBuilder.Constant(member.MemberName)
					: $"nameof({typeDef.Type.FullyQualifiedName}.{member.MemberName})";

			/// <summary>Returns the C# expression that reads a member from the local <c>instance</c>: a direct access, or the accessor thunk for a member the generated code cannot reach</summary>
			private static string GetInstanceMemberReadExpr(CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
				=> member.NeedsGetterThunk
					? $"__get_{member.MemberName}({(typeDef.Type.IsValueType() ? "ref " : "")}instance)"
					: $"instance.{member.MemberName}";

			/// <summary>Emits the accessor thunks for members that generated code cannot reach directly (a private/protected member, or a non-public accessor unlocked by <c>[JsonInclude]</c>)</summary>
			/// <remarks>Two flavors with identical call sites: zero-cost <c>[UnsafeAccessor]</c> thunks when the consuming compilation defines the attribute (net8+), and reflection-based accessors otherwise (correct wire, slower). Internal members never get a thunk: the generated code lives in the same assembly and reaches them directly.</remarks>
			/// <summary>Returns the CONCRETE registered subtypes of a polymorphic root, MOST DERIVED FIRST: the order a switch over runtime types has to case them in</summary>
			/// <remarks>
			/// <para>A <c>case</c> on a base type captures every subclass, so a concrete non-sealed intermediate registered before its
			/// own registered subclass would make that subclass's case unreachable - CS8120, which is a build FAILURE in the generated
			/// code, not a wrong wire. Registration order cannot be trusted for this, and inheritance depth is the property that fixes it.</para>
			/// <para>Depth is the only key, and <c>OrderByDescending</c> is stable, so two unrelated subtypes keep their registration
			/// order and the emission stays deterministic. Abstract types are dropped: they have no instance to match.</para>
			/// <para>Shared by all three wires (JSON <c>Pack</c> and <c>Serialize</c>, and the two XML profiles), which is the point:
			/// one ordering rule, spelled once. The same mechanism already orders the compat wire's <c>anyType</c> switch.</para>
			/// </remarks>
			private static IEnumerable<TypeMetadata> GetPolymorphicDispatchOrder(CrystalJsonTypeMetadata typeDef)
			{
				foreach (var (_, derivedType, _) in typeDef.DerivedTypes.OrderByDescending(static x => x.Type.InheritanceDepth))
				{
					if (derivedType.IsAbstract) continue;
					yield return derivedType;
				}
			}

			/// <summary>Emits the call to one lifecycle callback, or nothing when the type does not declare it</summary>
			/// <param name="documentExpr">Expression yielding the document being bound, for the deserialize pair; <see langword="null"/> on the serialize side, which has no document</param>
			/// <remarks>The parameter shapes were validated at parse time (CJSON0015), so the emitted call needs no runtime test.</remarks>
			private static void EmitCallbackInvocation(CSharpCodeBuilder sb, CrystalJsonCallbackMetadata? callback, string instanceExpr, string? documentExpr)
			{
				if (callback is null) return;

				var arg = callback.Argument switch
				{
					CrystalJsonCallbackArgument.JsonValue => documentExpr,
					CrystalJsonCallbackArgument.JsonObject => $"{documentExpr}.AsObject()",
					CrystalJsonCallbackArgument.JsonArray => $"{documentExpr}.AsArray()",
					_ => null,
				};

				sb.AppendLine(callback.IsNonPublic
					? $"__cb_{callback.MethodName}({instanceExpr}{(arg != null ? ", " + arg : "")});"
					: $"{instanceExpr}.{callback.MethodName}({arg ?? ""});");
			}

			/// <summary>Emits accessor thunks for the non-public lifecycle callbacks, mirroring the non-public MEMBER thunks</summary>
			private void EmitCallbackThunks(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				var callbacks = new[] { typeDef.OnSerializing, typeDef.OnSerialized, typeDef.OnDeserializing, typeDef.OnDeserialized };

				bool any = false;
				foreach (var cb in callbacks)
				{
					if (cb is { IsNonPublic: true }) { any = true; break; }
				}
				if (!any) return;

				var typeFullName = typeDef.Type.FullyQualifiedName;
				bool isStruct = typeDef.Type.IsValueType();
				string instanceParam = isStruct ? $"ref {typeFullName} instance" : $"{typeFullName} instance";

				sb.BeginRegion("Non-public lifecycle callbacks...");
				sb.NewLine();
				foreach (var cb in callbacks)
				{
					if (cb is not { IsNonPublic: true }) continue;

					var argType = cb.Argument switch
					{
						CrystalJsonCallbackArgument.JsonValue => KnownTypeSymbols.JsonValueFullName,
						CrystalJsonCallbackArgument.JsonObject => KnownTypeSymbols.JsonObjectFullName,
						CrystalJsonCallbackArgument.JsonArray => KnownTypeSymbols.JsonArrayFullName,
						_ => null,
					};
					var argParam = argType != null ? $", {argType} document" : "";
					var argCall = argType != null ? ", document" : "";

					if (this.Metadata.SupportsUnsafeAccessors)
					{
						sb.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {CSharpCodeBuilder.Constant(cb.MethodName)})]");
						sb.AppendLine($"private static extern void __cb_{cb.MethodName}({instanceParam}{argParam});");
					}
					else
					{ // no [UnsafeAccessor] on this target: reflection, same shape as the member accessors
						var types = argType != null ? $"[ typeof({argType}) ]" : "global::System.Type.EmptyTypes";
						sb.AppendLine($"private static readonly global::System.Reflection.MethodInfo __mi_{cb.MethodName} = typeof({typeFullName}).GetMethod({CSharpCodeBuilder.Constant(cb.MethodName)}, global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic, null, {types}, null)!;");
						sb.AppendLine($"private static void __cb_{cb.MethodName}({instanceParam}{argParam}) => __mi_{cb.MethodName}.Invoke(instance, {(argType != null ? "[ document ]" : "null")});");
					}
					sb.NewLine();
				}
				sb.EndRegion();
				sb.NewLine();
			}

			private void EmitNonPublicAccessorThunks(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				bool any = false;
				foreach (var member in typeDef.Members)
				{
					if (member.NeedsGetterThunk || member.NeedsSetterThunk) { any = true; break; }
				}
				if (!any) return;

				var typeFullName = typeDef.Type.FullyQualifiedName;
				bool isStruct = typeDef.Type.IsValueType();
				string instanceParam = isStruct ? $"ref {typeFullName} instance" : $"{typeFullName} instance";
				string instanceArg = isStruct ? "ref instance" : "instance";

				sb.BeginRegion("Non-public member accessors...");
				sb.NewLine();
				foreach (var member in typeDef.Members)
				{
					if (!member.NeedsGetterThunk && !member.NeedsSetterThunk) continue;
					var valueType = member.Type.FullyQualifiedNameAnnotated;
					bool needsSetter = member.NeedsSetterThunk && !member.IsReadOnly;
					if (this.Metadata.SupportsUnsafeAccessors)
					{
						if (member.IsField)
						{
							sb.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Field, Name = {CSharpCodeBuilder.Constant(member.MemberName)})]");
							sb.AppendLine($"private static extern ref {valueType} __ref_{member.MemberName}({instanceParam});");
							if (member.NeedsGetterThunk)
							{
								sb.AppendLine($"private static {valueType} __get_{member.MemberName}({instanceParam}) => __ref_{member.MemberName}({instanceArg});");
							}
							if (needsSetter)
							{
								sb.AppendLine($"private static void __set_{member.MemberName}({instanceParam}, {valueType} value) => __ref_{member.MemberName}({instanceArg}) = value;");
							}
						}
						else
						{
							if (member.NeedsGetterThunk)
							{
								sb.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {CSharpCodeBuilder.Constant("get_" + member.MemberName)})]");
								sb.AppendLine($"private static extern {valueType} __get_{member.MemberName}({instanceParam});");
							}
							if (needsSetter)
							{
								sb.AppendLine($"[global::System.Runtime.CompilerServices.UnsafeAccessor(global::System.Runtime.CompilerServices.UnsafeAccessorKind.Method, Name = {CSharpCodeBuilder.Constant("set_" + member.MemberName)})]");
								sb.AppendLine($"private static extern void __set_{member.MemberName}({instanceParam}, {valueType} value);");
							}
						}
					}
					else
					{ // no [UnsafeAccessor] on this target: reflection-based accessors (correct wire, slower)
						if (member.IsField)
						{
							sb.AppendLine($"private static readonly global::System.Reflection.FieldInfo __fi_{member.MemberName} = typeof({typeFullName}).GetField({CSharpCodeBuilder.Constant(member.MemberName)}, global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic)!;");
							if (member.NeedsGetterThunk)
							{
								sb.AppendLine($"private static {valueType} __get_{member.MemberName}({instanceParam}) => ({valueType}) __fi_{member.MemberName}.GetValue(instance)!;");
							}
							if (needsSetter)
							{
								EmitReflectionSetterThunk(sb, member, "__fi_" + member.MemberName, typeFullName, isStruct, instanceParam, valueType);
							}
						}
						else
						{
							sb.AppendLine($"private static readonly global::System.Reflection.PropertyInfo __pi_{member.MemberName} = typeof({typeFullName}).GetProperty({CSharpCodeBuilder.Constant(member.MemberName)}, global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic)!;");
							if (member.NeedsGetterThunk)
							{
								sb.AppendLine($"private static {valueType} __get_{member.MemberName}({instanceParam}) => ({valueType}) __pi_{member.MemberName}.GetValue(instance)!;");
							}
							if (needsSetter)
							{
								EmitReflectionSetterThunk(sb, member, "__pi_" + member.MemberName, typeFullName, isStruct, instanceParam, valueType);
							}
						}
					}
					sb.NewLine();
				}
				sb.EndRegion();
				sb.NewLine();
			}

			/// <summary>Emits the reflection-based setter thunk (with the box-mutate-unbox dance a struct needs)</summary>
			private static void EmitReflectionSetterThunk(CSharpCodeBuilder sb, CrystalJsonMemberMetadata member, string infoRef, string typeFullName, bool isStruct, string instanceParam, string valueType)
			{
				sb.AppendLine($"private static void __set_{member.MemberName}({instanceParam}, {valueType} value)");
				sb.EnterBlock();
				if (isStruct)
				{
					sb.AppendLine("object boxed = instance;");
					sb.AppendLine($"{infoRef}.SetValue(boxed, value);");
					sb.AppendLine($"instance = ({typeFullName}) boxed;");
				}
				else
				{
					sb.AppendLine($"{infoRef}.SetValue(instance, value);");
				}
				sb.LeaveBlock();
			}

			private void WritePackMethod(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				if (typeDef.Type.IsValueType())
				{
					sb.XmlComment($"<summary>Converts an instance of this type into the equivalent <see cref=\"{KnownTypeSymbols.JsonValueFullName}\"/></summary>");
					sb.AppendLine($"public {KnownTypeSymbols.JsonValueFullName} Pack({typeDef.Type.FullyQualifiedName}? instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default)");
					sb.EnterBlock();
					sb.AppendLine("if (instance is null)");
					sb.EnterBlock();
					sb.AppendLine($"return {KnownTypeSymbols.JsonNullFullName}.Null;");
					sb.LeaveBlock();
					sb.AppendLine("return Pack(instance.Value, settings, resolver);");
					sb.LeaveBlock();
					sb.NewLine();
				}

				sb.InheritDoc();
				sb.AppendLine($"public {KnownTypeSymbols.JsonValueFullName} Pack({typeDef.Type.FullyQualifiedName}{(!typeDef.Type.IsValueType() ? "?" : "")} instance, {KnownTypeSymbols.CrystalJsonSettingsFullName}? settings = default, {KnownTypeSymbols.ICrystalJsonTypeResolverFullName}? resolver = default)");
				sb.EnterBlock("Pack");
				if (this.Metadata.WireProfile == "DataContractCompat")
				{ // the container's baked wire profile is the "no settings" default; explicit settings replace it entirely
					sb.AppendLine($"settings ??= {KnownTypeSymbols.CrystalJsonSettingsFullName}.DataContractCompat;");
				}

				if (!typeDef.Type.IsValueType())
				{ // ref types can be null, we will return JsonNull.Null in this case
					sb.AppendLine("if (instance is null)");
					sb.EnterBlock();
					sb.AppendLine($"return {KnownTypeSymbols.JsonNullFullName}.Null;");
					sb.LeaveBlock();
					sb.NewLine();
				}

				// if the type is polymorphic, we have to dispatch to the corresponding serializer
				if (typeDef.IsPolymorphicRoot)
				{
					sb.AppendLine("switch(instance)");
					sb.EnterBlock();
					foreach (var derivedType in GetPolymorphicDispatchOrder(typeDef))
					{
						sb.AppendLine($"case {derivedType.FullyQualifiedName} x: return {GetLocalSerializerRef(derivedType)}.Pack(x, settings, resolver);");
					}
					sb.AppendLine($"default: throw {KnownTypeSymbols.JsonSerializationExceptionFullName}.CannotPackDerivedTypeWithUnknownTypeDiscriminator(instance.GetType(), typeof({typeDef.Type.FullyQualifiedName}));");
					sb.LeaveBlock();

					sb.LeaveBlock("Pack");
					sb.NewLine();
					return;
				}

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(typeDef.Type.Ref, out var polymorphicMetadata);

				if (typeDef.Type.IsAbstract)
				{ // do we have a parent ?
					if (hasPolymorphicDefinition)
					{ // defer to the parent type which should have all the derived types under this one
						//PERF: we _could_ optimize by having a smaller switch with only de types under us?
						sb.AppendLine($"return {GetLocalSerializerRef(polymorphicMetadata.Parent)}.Pack(instance, settings, resolver);");
						sb.LeaveBlock("Pack");
						sb.NewLine();
						return;
					}
				}

				// if the type is not sealed, we may have a derived type, we must defer serialization to this type!
				if (!typeDef.Type.IsSealed)
				{
					//BUGBUG: detect if we have a generated serialize for this derived type?
					sb.AppendLine($"if (instance.GetType() != typeof({typeDef.Type.FullyQualifiedName}))");
					sb.EnterBlock();
					sb.AppendLine($"throw {KnownTypeSymbols.JsonSerializationExceptionFullName}.CannotPackDerivedTypeWithUnknownTypeDiscriminator(instance.GetType(), typeof({typeDef.Type.FullyQualifiedName}));");
					sb.LeaveBlock("Pack");
					sb.NewLine();
				}

				sb.AppendLine($"var obj = new {KnownTypeSymbols.JsonObjectFullName}({typeDef.Members.Count});");
				sb.NewLine();

				if (hasPolymorphicDefinition)
				{
					sb.Comment("Add the discriminator property for this derived type");
					if (polymorphicMetadata.Discriminator is (string or int))
					{
						sb.AppendLine("obj[PropertyNames._TypeDiscriminatorProperty_] = PropertyEncodedNames._TypeDiscriminatorValue_;");
					}
					else if (polymorphicMetadata.Discriminator is null)
					{
						sb.AppendLine($"#error You must specify a valid type discriminator for derived type {typeDef.Name} of parent type {polymorphicMetadata.Parent.Name}");
					}
					else
					{
						sb.AppendLine($"#error Invalid discriminator value type for derived type {typeDef.Name} of parent type {polymorphicMetadata.Parent.Name}");
					}
					sb.NewLine();
				}

				EmitCallbackInvocation(sb, typeDef.OnSerializing, "instance", null);
				sb.NewLine();

				foreach (var member in typeDef.Members)
				{
					sb.Comment($"\"{member.Name}\" => {member.Type.FullName} {member.MemberName}{(member.IsKey ? ", KEY" : "")}{(member.IsField ? ", field" : ", prop")}{(member.IsRequired ? ", required" : "")}{(member.IsInitOnly ? ", initOnly" : member.IsReadOnly ? ", readOnly" : "")}{(member.IgnoreCondition != null ? $", [{member.IgnoreCondition}]" : "")}");

					var getterExpr = GetInstanceMemberReadExpr(typeDef, member);
					var packerExpr = GetMemberPackerExpression(member, getterExpr);

					if (member.IgnoreCondition == "Never")
					{ // always present, even as an explicit null
						sb.AppendLine($"obj.Add({GetLocalPropertyNameRef(member)}, {packerExpr});");
					}
					else if (member.IgnoreCondition == "WhenWritingNull")
					{ // omitted when null, regardless of the settings
						sb.AppendLine($"obj.AddIfNotNull({GetLocalPropertyNameRef(member)}, {packerExpr});");
					}
					else if (member.IgnoreCondition == "WhenWritingDefault")
					{ // omitted when equal to the member's default, regardless of the settings
						sb.AppendLine($"if (!global::System.Collections.Generic.EqualityComparer<{member.Type.FullyQualifiedNameAnnotated}>.Default.Equals({getterExpr}, {GetForgivingDefaultLiteral(member)}))");
						sb.EnterBlock();
						sb.AppendLine($"obj.Add({GetLocalPropertyNameRef(member)}, {packerExpr});");
						sb.LeaveBlock();
					}
					else if (member.Type.IsNullableOfT())
					{
						sb.AppendLine($"obj.AddIfNotNull({GetLocalPropertyNameRef(member)}, {packerExpr});");
					}
					else if (member.IsNotNull)
					{
						sb.AppendLine($"obj.Add({GetLocalPropertyNameRef(member)}, {packerExpr});");
					}
					else
					{
						sb.AppendLine($"obj.AddIfNotNull({GetLocalPropertyNameRef(member)}, {packerExpr});");
					}
					sb.NewLine();
				}
				EmitCallbackInvocation(sb, typeDef.OnSerialized, "instance", null);

				sb.AppendLine($"return settings.IsReadOnly() ? {KnownTypeSymbols.CrystalJsonMarshallFullName}.FreezeTopLevel(obj) : obj;");
				sb.LeaveBlock("Pack");
				sb.NewLine();
			}

			private void WriteSerializeMethod(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef)
			{
				sb.InheritDoc();
				sb.AppendLine($"void {KnownTypeSymbols.IJsonConverterInterfaceFullName}.Serialize(object? instance, {SystemTypeFullName} declaringType, {SystemTypeFullName}? runtimeType, {KnownTypeSymbols.CrystalJsonWriterFullName} writer)");
				sb.EnterBlock();
				if (typeDef.Type.IsValueType())
				{ // null cannot be cast into a value type, handle this specifically here
					sb.AppendLine("if (instance is null)");
					sb.EnterBlock();
					sb.AppendLine("writer.WriteNull();");
					sb.AppendLine("return;");
					sb.LeaveBlock();
				}
				// check that we have a compatible type
				sb.AppendLine($"if (instance is not {typeDef.Type.FullyQualifiedName} value)");
				sb.EnterBlock();
				sb.AppendLine($"throw {KnownTypeSymbols.CrystalJsonFullName}.Errors.Serialization_DoesNotKnowHowToSerializeType(runtimeType ?? declaringType);");
				sb.LeaveBlock();
				sb.AppendLine("Serialize(writer, value);");
				sb.LeaveBlock();
				sb.NewLine();

				if (typeDef.Type.IsValueType())
				{
					sb.XmlComment("/// <summary>Writes a JSON representation of a nullable value to the specified output</summary>");
					sb.AppendLine($"public void Serialize({KnownTypeSymbols.CrystalJsonWriterFullName} writer, {typeDef.Type.FullyQualifiedName}? instance)");
					sb.EnterBlock();
					sb.AppendLine("if (instance is null)");
					sb.EnterBlock();
					sb.AppendLine("writer.WriteNull();");
					sb.AppendLine("return;");
					sb.LeaveBlock();
					sb.AppendLine("Serialize(writer, instance.Value);");
					sb.LeaveBlock();
					sb.NewLine();
				}

				sb.InheritDoc();
				sb.AppendLine($"public void Serialize({KnownTypeSymbols.CrystalJsonWriterFullName} writer, {typeDef.Type.FullyQualifiedName}{(typeDef.Type.IsValueType() ? "" : "?")} instance)");
				sb.EnterBlock("Serialize()");

				// note: no incompatible-settings guard is needed here: the emitted names carry BOTH casings
				// (JsonEncodedPropertyName bakes the declared and camelCased literals, and the writer picks per
				// settings), so naming, like the value formats, is runtime-honorable - there is currently no
				// passed setting the generated code cannot honor. The no-silent-path-switch doctrine is enforced
				// where a real conflict exists: at generation time (CJSON0013, profile vs naming option).

				if (!typeDef.Type.IsValueType())
				{ // ref types can be null, we will write "null" in this case
					sb.AppendLine("if (instance is null)");
					sb.EnterBlock();
					sb.AppendLine("writer.WriteNull();");
					sb.AppendLine("return;");
					sb.LeaveBlock();
				}

				//TODO: handle IJsonSerializer<T> and IJsonSerializable

				// if the type is polymorphic, we have to dispatch to the corresponding serializer
				if (typeDef.IsPolymorphicRoot)
				{
					sb.AppendLine("switch(instance)");
					sb.EnterBlock();
					foreach (var derivedType in GetPolymorphicDispatchOrder(typeDef))
					{
						sb.AppendLine($"case {derivedType.FullyQualifiedName} x: {GetLocalSerializerRef(derivedType)}.Serialize(writer, x); break;");
					}
					sb.AppendLine($"default: throw {KnownTypeSymbols.JsonSerializationExceptionFullName}.CannotSerializeDerivedTypeWithoutTypeDiscriminator(instance.GetType(), typeof({typeDef.Type.FullyQualifiedName}));");
					sb.LeaveBlock();

					sb.LeaveBlock("Serialize()");
					sb.NewLine();
					return;
				}

				bool hasPolymorphicDefinition = this.PolymorphicMap.TryGetValue(typeDef.Type.Ref, out var polymorphicMetadata);

				if (typeDef.Type.IsAbstract)
				{ // do we have a parent ?
					if (hasPolymorphicDefinition)
					{ // defer to the parent type which should have all the derived types under this one
						//REVIEW: TODO: we _could_ optimize by having a smaller switch with only de types under us?
						sb.AppendLine($"{GetLocalSerializerRef(polymorphicMetadata.Parent)}.Serialize(writer, instance);");
						sb.LeaveBlock("Serialize()");
						sb.NewLine();
						return;
					}
				}

				// if the type is not sealed, we may have a derived type, we must defer serialization to this type!
				if (!typeDef.Type.IsSealed)
				{
					//TODO: we should have a local method that can dispatch known types!
					sb.AppendLine($"if (instance.GetType() != typeof({typeDef.Type.FullyQualifiedName}))");
					sb.EnterBlock();
					sb.AppendLine($"throw new {NotSupportedExceptionFullName}(\"Cannot serialize a polymorphic type. You must add at least one [JsonDerivedType] to the base class or interface.\");");
					//sb.AppendLine($"{KnownTypeSymbols.CrystalJsonVisitorFullName}.VisitValue(instance, typeof({typeFullName}), writer);");
					//sb.AppendLine("return;");
					sb.LeaveBlock();
				}

				sb.NewLine();
				sb.AppendLine("var state = writer.BeginObject();");

				if (hasPolymorphicDefinition)
				{
					sb.Comment("Add the discriminator property for this derived type");
					if (polymorphicMetadata.Discriminator is (string or int))
					{
						sb.AppendLine("writer.WriteField(PropertyEncodedNames._TypeDiscriminatorProperty_, PropertyNames._TypeDiscriminatorValue_);");
					}
					else
					{
						sb.AppendLine("#error Invalid discriminator value type");
					}
				}

				EmitCallbackInvocation(sb, typeDef.OnSerializing, "instance", null);

				foreach (var member in typeDef.Members)
				{
					this.WriteMemberSerializer(sb, typeDef, member);
				}
				sb.NewLine();

				EmitCallbackInvocation(sb, typeDef.OnSerialized, "instance", null);

				sb.AppendLine("writer.EndObject(state);");
				sb.LeaveBlock("Serialize()");
				sb.NewLine();
			}

			private string GetMemberPackerExpression(CrystalJsonMemberMetadata member, string getterExpr)
			{
				if (member.CustomConverterType != null)
				{ // a custom converter takes over the member's wire form
					if (!member.CustomConverterHasPacker)
					{ // asymmetric converter without the packing facet: any attempt to serialize fails loudly
						return $"/* member-converter (missing packer facet) */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingPackerFacet(typeof({member.CustomConverterType}), typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName}))";
					}
					if (member.Type.IsValueType() && member.Type.NullableOfType is null)
					{
						return $"/* member-converter */ {GetMemberConverterRef(member)}.Pack({getterExpr}, settings, resolver)";
					}
					return $"/* member-converter */ {getterExpr} is {{ }} v{member.MemberName} ? {GetMemberConverterRef(member)}.Pack(v{member.MemberName}, settings, resolver) : {KnownTypeSymbols.JsonNullFullName}.Null";
				}

				if (member.EnumFormat is "String" or "Number" && (member.Type.NullableOfType ?? member.Type).IsEnum())
				{ // [JsonProperty(EnumFormat = ...)] forces the wire form for this member, regardless of the settings
					var packHelper = $"{KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnum{member.EnumFormat}";
					if (member.Type.NullableOfType is null)
					{
						return $"/* enum-format */ {packHelper}({getterExpr})";
					}
					return $"/* enum-format */ {getterExpr} is {{ }} v{member.MemberName} ? {packHelper}(v{member.MemberName}) : {KnownTypeSymbols.JsonNullFullName}.Null";
				}

				if (IsLocallyGeneratedType(member.Type, out var target, out _))
				{
					return $"/* local-serializer */ {GetLocalSerializerRef(target)}.Pack({getterExpr}, settings, resolver)";
				}

				// unwrap any Nullable<T> (most packing methods handle both!)
				var concreteType = member.Type.NullableOfType ?? member.Type;
				if (concreteType.IsBooleanLike())
				{
					return $"/* fast-boolean */ {KnownTypeSymbols.JsonBooleanFullName}.Return({getterExpr})";
				}
				if (concreteType.IsStringLike())
				{
					return $"/* fast-string */ {KnownTypeSymbols.JsonStringFullName}.Return({getterExpr})";
				}
				if (concreteType.IsNumberLike())
				{
					return $"/* fast-number */ {KnownTypeSymbols.JsonNumberFullName}.Return({getterExpr})";
				}
				if (concreteType.IsDateLike())
				{
					return $"/* fast-date */ {KnownTypeSymbols.JsonDateTimeFullName}.Return({getterExpr})";
				}

				if (member.Type.JsonType() is not JsonPrimitiveType.None)
				{
					// it's already a JSON value, but we may need to convert it to readonly!
					return $"/* fast-json */ settings.IsReadOnly() ? ({getterExpr})?.ToReadOnly() : ({getterExpr})";
				}

				if (concreteType.JsonType() is not JsonPrimitiveType.None)
				{
					return $"/* direct-json-value */ {getterExpr}";
				}

				if (concreteType.IsJsonPackable())
				{
					return $"/* packable */ {KnownTypeSymbols.JsonValueFullName}.FromValue({getterExpr}, settings, resolver)";
				}

				if (concreteType.IsDictionary(out var keyType, out var valueType))
				{
					if (keyType.IsString())
					{
						if (IsLocallyGeneratedType(valueType, out target, out _))
						{
							return $"/* local-string-dict */ {GetLocalSerializerRef(target)}.PackObject({getterExpr}, settings, resolver)";
						}
						if (!valueType.IsValueType())
						{
							return $"/* fallback-string-dict */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnumerable({getterExpr}, settings, resolver)";
						}
					}
					else if (keyType.IsInt32())
					{
						if (IsLocallyGeneratedType(valueType, out target, out _))
						{
							return $"/* local-int-dict */ {GetLocalSerializerRef(target)}.PackObject({getterExpr}, settings, resolver)";
						}
						if (valueType.IsEnumerable(out var elemType))
						{
							if (this.IsLocallyGeneratedType(elemType, out target, out _))
							{
								return $"/* local-int-dict-of-array */ {this.GetLocalSerializerRef(target)}.PackObject({getterExpr}!, settings, resolver)";
							}
						}

						if (!valueType.IsValueType())
						{
							return $"/* fallback-dict-int */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnumerable({getterExpr}, settings, resolver)";
						}
					}
					//else: int? other well known type?
				}
				else if (concreteType.IsEnumerable(out var elemType))
				{
					// if the elem type is a local type, we will use the generated serializer
					if (IsLocallyGeneratedType(elemType, out target, out _))
					{
						if (concreteType.IsArray())
						{
							return $"/* local-pack-array */ {GetLocalSerializerRef(target)}.PackArray({getterExpr}, settings, resolver)";
						}
						if (concreteType.IsList())
						{
							return $"/* local-pack-list */ {GetLocalSerializerRef(target)}.PackList({getterExpr}, settings, resolver)";
						}
						if (!elemType.IsValueType())
						{
							return $"/* local-pack-enumerable */ {GetLocalSerializerRef(target)}.PackEnumerable({getterExpr}, settings, resolver)";
						}
					}
					else if (elemType.IsPrimitive)
					{ // for primitive types, we should have a fast direct implementation
						if (concreteType.IsArray())
						{
							return $"/* fast-pack-array */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackArray({getterExpr}, settings, resolver)";
						}
						if (concreteType.IsList())
						{
							return $"/* fast-pack-list */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackList({getterExpr}, settings, resolver)";
						}
						if (!concreteType.IsValueType())
						{
							return $"/* fast-pack-enumerable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnumerable({getterExpr}, settings, resolver)";
						}
					}
					else
					{ // otherwise, use runtime serialization
						if (concreteType.IsArray())
						{
							return $"/* fallback-pack-array */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackArray({getterExpr}, settings, resolver)";
						}
						if (concreteType.IsList())
						{
							return $"/* fallback-pack-list */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackList({getterExpr}, settings, resolver)";
						}
						if (!concreteType.IsValueType())
						{
							return $"/* fallback-pack-enumerable */ {KnownTypeSymbols.JsonSerializerExtensionsFullName}.PackEnumerable({getterExpr}, settings, resolver)";
						}
					}
				}

				return $"/* fallback */ {KnownTypeSymbols.JsonValueFullName}.FromValue({getterExpr}, settings, resolver)";
			}

			private static string ConvertDiscriminatorValueToJsonLiteral(object? value) => value switch
			{
				string s => $"{KnownTypeSymbols.JsonStringFullName}.Return({CSharpCodeBuilder.Constant(s)})",
				int n => $"{KnownTypeSymbols.JsonNumberFullName}.Return({CSharpCodeBuilder.Constant(n)})",
				_ => "null"
			};

			private static bool IsFastPathSerializable(TypeMetadata type)
			{
				// Note: we assume we always have Nullable<T> variants helpers in the fast path!
				type = type.NullableOfType ?? type;

				switch (type.SpecialType)
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
					case SpecialType.System_Decimal:
					case SpecialType.System_Single:
					case SpecialType.System_Double:
					case SpecialType.System_String:
					case SpecialType.System_DateTime:
					{
						return true;
					}
				}

				if (type.NameSpace == "System")
				{
					switch (type.Name)
					{
						case nameof(DateTimeOffset):
						case nameof(Guid):
						case "DateOnly":
						case "TimeOnly":
						case "Int128":
						case "UInt128":
						case "Half":
						{
							return true;
						}
					}
				}

				if (type.NameSpace == "NodaTime")
				{
					switch (type.Name)
					{
						case "Instant":
						case "Duration":
						//TODO: add more!
						{
							return true;
						}
					}
				}

				return false;
			}

			/// <summary>Name of the static field holding the custom converter instance for a member</summary>
			private static string GetMemberConverterRef(CrystalJsonMemberMetadata member) => $"{member.MemberName}Converter";

			/// <summary>Test if a type has some locally generated serialization methods</summary>
			private bool IsLocallyGeneratedType(TypeRef type, [MaybeNullWhen(false)] out CrystalJsonTypeMetadata metadata)
			{
				return this.TypeMap.TryGetValue(type, out metadata);
			}

			/// <summary>Test if a type has some locally generated serialization methods</summary>
			private bool IsLocallyGeneratedType(TypeMetadata type, [MaybeNullWhen(false)] out CrystalJsonTypeMetadata metadata, out bool nullableOfT)
			{
				nullableOfT = type.IsNullableOfT(out var underlyingType);

				return this.IsLocallyGeneratedType((underlyingType ?? type).Ref, out metadata);
			}

			private void WriteMemberSerializer(CSharpCodeBuilder sb, CrystalJsonTypeMetadata typeDef, CrystalJsonMemberMetadata member)
			{
				sb.NewLine();
				sb.Comment($"{member.Type.Name} {member.MemberName} => \"{member.Name}\"{(member.IgnoreCondition != null ? $" [{member.IgnoreCondition}]" : "")}");

				var propertyName = GetPropertyEncodedNameRef(member);
				var getterExpr = GetInstanceMemberReadExpr(typeDef, member);

				switch (member.IgnoreCondition)
				{
					case "Never" when member.CustomConverterType != null:
					{ // always emitted, and the member has a custom converter: pack through it, writing an explicit null
						sb.AppendLine($"writer.WriteName({propertyName});");
						if (!member.CustomConverterHasPacker)
						{ // asymmetric converter without the packing facet: any attempt to serialize fails loudly
							sb.AppendLine($"{KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingPackerFacet(typeof({member.CustomConverterType}), typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName})).JsonSerialize(writer); // member-converter (missing packer facet)");
						}
						else if (member.Type.IsValueType() && member.Type.NullableOfType is null)
						{
							sb.AppendLine($"{GetMemberConverterRef(member)}.Pack({getterExpr}, writer.Settings, writer.Resolver).JsonSerialize(writer); // member-converter");
						}
						else
						{
							sb.AppendLine($"({getterExpr} is {{ }} v{member.MemberName} ? {GetMemberConverterRef(member)}.Pack(v{member.MemberName}, writer.Settings, writer.Resolver) : {KnownTypeSymbols.JsonNullFullName}.Null).JsonSerialize(writer); // member-converter");
						}
						return;
					}
					case "Never":
					{ // always emitted, even null or default, bypassing the writer's settings-level discards
						// note: goes through the runtime visitor (correctness over speed: a pinned member is rare)
						sb.AppendLine($"writer.WriteName({propertyName});");
						sb.AppendLine($"{KnownTypeSymbols.CrystalJsonVisitorFullName}.VisitValue<{member.Type.FullyQualifiedNameAnnotated}>({getterExpr}, writer);");
						return;
					}
					case "WhenWritingNull":
					{ // omitted when null, regardless of the settings; a non-null value is always emitted
						sb.AppendLine($"if ({getterExpr} is not null)");
						sb.EnterBlock();
						this.WriteMemberSerializerCore(sb, member, propertyName, getterExpr);
						sb.LeaveBlock();
						return;
					}
					case "WhenWritingDefault":
					{ // omitted when equal to the member's default, regardless of the settings
						sb.AppendLine($"if (!global::System.Collections.Generic.EqualityComparer<{member.Type.FullyQualifiedNameAnnotated}>.Default.Equals({getterExpr}, {GetForgivingDefaultLiteral(member)}))");
						sb.EnterBlock();
						this.WriteMemberSerializerCore(sb, member, propertyName, getterExpr);
						sb.LeaveBlock();
						return;
					}
				}

				this.WriteMemberSerializerCore(sb, member, propertyName, getterExpr);
			}

			private void WriteMemberSerializerCore(CSharpCodeBuilder sb, CrystalJsonMemberMetadata member, string propertyName, string getterExpr)
			{
				if (member.CustomConverterType != null)
				{ // a custom converter takes over the member's wire form
					if (!member.CustomConverterHasPacker)
					{ // asymmetric converter without the packing facet: any attempt to serialize fails loudly
						sb.AppendLine($"writer.WriteField({propertyName}, {KnownTypeSymbols.JsonSerializerExtensionsFullName}.FailConverterMissingPackerFacet(typeof({member.CustomConverterType}), typeof({(member.Type.NullableOfType ?? member.Type).FullyQualifiedName}))); // member-converter (missing packer facet)");
					}
					else if (member.Type.IsValueType() && member.Type.NullableOfType is null)
					{
						sb.AppendLine($"writer.WriteField({propertyName}, {GetMemberConverterRef(member)}.Pack({getterExpr}, writer.Settings, writer.Resolver)); // member-converter");
					}
					else
					{
						sb.AppendLine($"writer.WriteField({propertyName}, {getterExpr} is {{ }} v{member.MemberName} ? {GetMemberConverterRef(member)}.Pack(v{member.MemberName}, writer.Settings, writer.Resolver) : null); // member-converter");
					}
					return;
				}

				if (member.EnumFormat is "String" or "Number" && (member.Type.NullableOfType ?? member.Type).IsEnum())
				{ // [JsonProperty(EnumFormat = ...)] forces the wire form for this member, regardless of the settings
					sb.AppendLine($"writer.WriteFieldEnum{member.EnumFormat}({propertyName}, {getterExpr}); // enum-format");
					return;
				}

				if (IsFastPathSerializable(member.Type))
				{
					// there is a dedicated method for this type
					sb.AppendLine($"writer.WriteField({propertyName}, {getterExpr}); // fast-path");
					return;
				}

				if (IsLocallyGeneratedType(member.Type, out var subDef, out _))
				{ // we have a local generated serializer for this!
					sb.AppendLine($"writer.WriteField({propertyName}, {getterExpr}, {this.GetLocalSerializerRef(subDef)}); // local-serializer");
					return;
				}

				if (member.Type.SpecialType == SpecialType.System_Nullable_T)
				{
					sb.AppendLine("// nullable!");
					//TODO?
				}

				//TODO: test if implements IJsonSerializable

				if (member.Type.JsonType() is not JsonPrimitiveType.None)
				{ // this is a JsonValue
					sb.AppendLine($"writer.WriteField({propertyName}, {getterExpr}); // fast-json");
					return;
				}

				if (member.Type.IsJsonSerializable())
				{ // the type has its own JsonSerialize method that we will call directly
					sb.AppendLine($"writer.WriteFieldJsonSerializable({propertyName}, {getterExpr}); // json-serializable");
					return;
				}

				if (member.Type.IsDictionary(out var keyType, out var valueType))
				{
					if (keyType.IsString())
					{
						if (IsLocallyGeneratedType(valueType, out subDef, out _))
						{
							sb.AppendLine($"writer.WriteFieldDictionary({propertyName}, {getterExpr}, {GetLocalSerializerRef(subDef)}); // dict-local-serializer");
						}
						else
						{
							sb.AppendLine($"writer.WriteFieldDictionary({propertyName}, {getterExpr}); // dict-fallback");
						}
						return;
					}
				}
				else if (member.Type.IsEnumerable(out var elemType))
				{
					if (IsLocallyGeneratedType(elemType, out subDef, out _))
					{ // we have a local generated serializer for this!
						sb.AppendLine($"writer.WriteFieldArray({propertyName}, {getterExpr}, {this.GetLocalSerializerRef(subDef)}); // enumerable-local-serializer");
						return;
					}

					sb.AppendLine($"writer.WriteFieldArray({propertyName}, {getterExpr}); // enumerable-fallback");
					return;
				}

				// fallback to invoking the generic WriteField<T>(...) method
				sb.AppendLine($"writer.WriteField({propertyName}, {getterExpr}); // fallback");
			}

		}

	}

}


