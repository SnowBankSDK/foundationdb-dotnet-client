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
	using Microsoft.CodeAnalysis;
	using Microsoft.CodeAnalysis.CSharp;

	[SuppressMessage("ReSharper", "InconsistentNaming")]
	internal sealed class KnownTypeSymbols
	{

		#region FullNames constants...

		public const string CrystalJsonNamespace = "SnowBank.Data.Json";

		/// <summary>Namespace of the XML output vocabulary (opt-in: a container only produces XML when it asks for it)</summary>
		public const string CrystalXmlNamespace = "SnowBank.Data.Xml";

		public const string CrystalXmlOutputAttributeFullName = CrystalXmlNamespace + ".CrystalXmlOutputAttribute";

		/// <summary>Member-level XML vocabulary: every XML-only concern lives here, so the JSON attributes are never modified by the XML feature</summary>
		public const string XmlPropertyAttributeFullName = CrystalXmlNamespace + ".XmlPropertyAttribute";

		public const string CrystalJsonFullName = CrystalJsonNamespace + ".CrystalJson";
		public const string CrystalJsonConverterAttributeFullName = CrystalJsonNamespace + ".CrystalJsonConverterAttribute";
		public const string JsonPropertyAttributeFullName = CrystalJsonNamespace + ".JsonPropertyAttribute";

		public const string JsonEncodedPropertyNameFullName = CrystalJsonNamespace + ".JsonEncodedPropertyName";
		public const string CrystalJsonSettingsFullName = CrystalJsonNamespace + ".CrystalJsonSettings";
		public const string CrystalJsonVisitorFullName = CrystalJsonNamespace + ".CrystalJsonVisitor";
		public const string CrystalJsonWriterFullName = CrystalJsonNamespace + ".CrystalJsonWriter";
		public const string CrystalJsonMarshallFullName = CrystalJsonNamespace + ".CrystalJsonMarshall";
		public const string JsonSerializerExtensionsFullName = CrystalJsonNamespace + ".JsonSerializerExtensions";

		public const string ICrystalJsonTypeResolverFullName = CrystalJsonNamespace + ".ICrystalJsonTypeResolver";
		public const string CrystalJsonTypeDefinitionFullName = CrystalJsonNamespace + ".CrystalJsonTypeDefinition";
		public const string CrystalJsonTypeFlagsFullName = CrystalJsonNamespace + ".CrystalJsonTypeFlags";
		public const string CrystalJsonMemberDefinitionFullName = CrystalJsonNamespace + ".CrystalJsonMemberDefinition";
		public const string CrystalJsonMemberFlagsFullName = CrystalJsonNamespace + ".CrystalJsonMemberFlags";
		public const string CrystalJsonTypeBinderFullName = CrystalJsonNamespace + ".CrystalJsonTypeBinder";
		public const string CrystalJsonTypeVisitorFullName = CrystalJsonNamespace + ".CrystalJsonTypeVisitor";
		public const string JsonValueComparerFullName = CrystalJsonNamespace + ".JsonValueComparer";

		public const string IJsonSerializableFullName = CrystalJsonNamespace + ".IJsonSerializable";
		public const string IJsonPackableFullName = CrystalJsonNamespace + ".IJsonPackable";
		public const string IJsonObservableProxyFullName = CrystalJsonNamespace + ".IJsonObservableProxy";
		public const string IJsonConverterInterfaceFullName = CrystalJsonNamespace + ".IJsonConverter";
		public const string IJsonProxyNodeFullName = CrystalJsonNamespace + ".IJsonProxyNode";

		public const string JsonValueName = "JsonValue";
		public const string JsonNullName = "JsonNull";
		public const string JsonObjectName = "JsonObject";
		public const string JsonArrayName = "JsonArray";
		public const string JsonNumberName = "JsonNumber";
		public const string JsonBooleanName = "JsonBoolean";
		public const string JsonStringName = "JsonString";
		public const string JsonDateTimeName = "JsonDateTime";

		public const string JsonPathFullName = CrystalJsonNamespace + ".JsonPath";
		public const string JsonPathSegmentFullName = CrystalJsonNamespace + ".JsonPathSegment";
		public const string JsonPathBuilderFullName = CrystalJsonNamespace + ".JsonPathBuilder";

		public const string JsonValueFullName = CrystalJsonNamespace + "." + JsonValueName;
		public const string JsonNullFullName = CrystalJsonNamespace + "." + JsonNullName;
		public const string JsonObjectFullName = CrystalJsonNamespace + "." + JsonObjectName;
		public const string JsonArrayFullName = CrystalJsonNamespace + "." + JsonArrayName;
		public const string JsonNumberFullName = CrystalJsonNamespace + "." + JsonNumberName;
		public const string JsonBooleanFullName = CrystalJsonNamespace + "." + JsonBooleanName;
		public const string JsonStringFullName = CrystalJsonNamespace + "." + JsonStringName;
		public const string JsonDateTimeFullName = CrystalJsonNamespace + "." + JsonDateTimeName;

		public const string JsonSerializationExceptionFullName = CrystalJsonNamespace + ".JsonSerializationException";
		public const string JsonBindingExceptionFullName = CrystalJsonNamespace + ".JsonBindingException";

		// JSON writable proxies are mutable typed wrappers other a JsonValue
		public const string IJsonWritableProxyFullName = CrystalJsonNamespace + ".IJsonWritableProxy";
		public const string JsonWritableProxyObjectBaseFullName = CrystalJsonNamespace + ".JsonWritableProxyObjectBase";
		public const string JsonWritableProxyDictionaryFullName = CrystalJsonNamespace + ".JsonWritableProxyDictionary";
		public const string JsonWritableProxyArrayFullName = CrystalJsonNamespace + ".JsonWritableProxyArray";

		// JSON readonly proxies are immutable typed wrappers other a JsonValue
		public const string IJsonReadOnlyProxyFullName = CrystalJsonNamespace + ".IJsonReadOnlyProxy";
		public const string JsonReadOnlyProxyDictionaryFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyDictionary";
		public const string JsonReadOnlyProxyInt32DictionaryFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyInt32Dictionary";
		public const string JsonReadOnlyProxyInt32DictionaryOfArrayFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyInt32DictionaryOfArray";
		public const string JsonReadOnlyProxyArrayFullName = CrystalJsonNamespace + ".JsonReadOnlyProxyArray";

		// "mutable" json values will record all changes into a transaction object
		public const string IMutableJsonContextFullName = CrystalJsonNamespace + ".IMutableJsonContext";
		public const string MutableJsonObjectBaseFullName = CrystalJsonNamespace + ".MutableJsonObjectBase";
		public const string MutableJsonDictionaryFullName = CrystalJsonNamespace + ".MutableJsonDictionary";
		public const string MutableJsonArrayFullName = CrystalJsonNamespace + ".MutableJsonArray";
		public const string MutableJsonValueFullName = CrystalJsonNamespace + ".MutableJsonValue";

		// "observable" json values will record all reads into a context object
		public const string IObservableJsonContextFullName = CrystalJsonNamespace + ".IObservableJsonContext";
		public const string ObservableJsonValueFullName = CrystalJsonNamespace + ".ObservableJsonValue";
		public const string ObservableJsonDictionaryFullName = CrystalJsonNamespace + ".ObservableJsonDictionary";
		public const string ObservableJsonArrayFullName = CrystalJsonNamespace + ".ObservableJsonArray";

		// buffer types
		public const string SliceFullName = "System.Slice";
		public const string SliceOwnerFullName = "System.SliceOwner";
		public const string ArrayPoolFullName = "System.Buffers.ArrayPool";

		#endregion
		
		public Compilation Compilation { get; }

		/// <summary>The compilation's core library defines <c>[UnsafeAccessor]</c> (net8+): non-public members get zero-cost accessor thunks; otherwise the generated code falls back to reflection-based accessors</summary>
		public bool HasUnsafeAccessor { get; }

		/// <summary>The consuming compilation can host the generated JSON proxies: the proxy interfaces are visible AND the language version supports the static abstract members they implement (C# 11)</summary>
		/// <remarks>
		/// <para>Both halves are real exclusions rather than preferences. <c>IJsonReadOnlyProxy</c> and friends are absent from the <c>netstandard2.0</c> build of <c>SnowBank.Core</c>, because static abstract interface members need runtime support the .NET Framework CLR does not have; and a consumer below C# 11 cannot implement them even when they are visible.</para>
		/// <para>When this is false, the container still gets its converters, its <c>TypeMapper</c> and (if it asked for it) its XML output: only the proxy surface is left out.</para>
		/// </remarks>
		public bool SupportsJsonProxies { get; }

		/// <summary>The consuming compilation can see a usable <c>[DynamicallyAccessedMembers]</c></summary>
		/// <remarks>On the lite path the attribute only exists as an <c>internal</c> polyfill inside the referenced assembly, so generated code cannot apply it: the trimming annotation is dropped, which costs nothing on a runtime that does not trim.</remarks>
		public bool HasDynamicallyAccessedMembers { get; }

		#region JSON Serialization Attributes...

		public INamedTypeSymbol? CrystalJsonConverterAttribute { get; }
		
		#endregion

		#region JSON Serilization Interfaces...
		public INamedTypeSymbol? IJsonSerializable { get; }

		public INamedTypeSymbol? IJsonPackable { get; }

		#endregion
		
		#region JsonValue types...
		
		public INamedTypeSymbol? JsonValue { get; }
		public INamedTypeSymbol? JsonObject { get; }
		public INamedTypeSymbol? JsonArray { get; }
		public INamedTypeSymbol? JsonNull { get; }
		public INamedTypeSymbol? JsonBoolean { get; }
		public INamedTypeSymbol? JsonNumber { get; }
		public INamedTypeSymbol? JsonString { get; }
		
		#endregion
		
		public KnownTypeSymbols(Compilation compilation)
		{
			this.Compilation = compilation;

			this.CrystalJsonConverterAttribute = compilation.GetBestTypeByMetadataName(CrystalJsonConverterAttributeFullName);

			this.HasUnsafeAccessor = compilation.GetBestTypeByMetadataName("System.Runtime.CompilerServices.UnsafeAccessorAttribute") is not null;

			this.SupportsJsonProxies =
				compilation.GetBestTypeByMetadataName(IJsonReadOnlyProxyFullName + "`3") is not null
				&& (compilation as CSharpCompilation)?.LanguageVersion.MapSpecifiedToEffectiveVersion() >= LanguageVersion.CSharp11;

			this.HasDynamicallyAccessedMembers =
				compilation.GetBestTypeByMetadataName("System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute") is { } dam
				&& compilation.IsSymbolAccessibleWithin(dam, compilation.Assembly);

			this.IJsonPackable = compilation.GetBestTypeByMetadataName(IJsonPackableFullName);
			this.IJsonSerializable = compilation.GetBestTypeByMetadataName(IJsonSerializableFullName);

			this.JsonValue = compilation.GetBestTypeByMetadataName(JsonValueFullName);
			this.JsonObject = compilation.GetBestTypeByMetadataName(JsonObjectFullName);
			this.JsonArray = compilation.GetBestTypeByMetadataName(JsonArrayFullName);
			this.JsonNull = compilation.GetBestTypeByMetadataName(JsonNullFullName);
			this.JsonBoolean = compilation.GetBestTypeByMetadataName(JsonBooleanFullName);
			this.JsonNumber = compilation.GetBestTypeByMetadataName(JsonNumberFullName);
			this.JsonString = compilation.GetBestTypeByMetadataName(JsonStringFullName);
		}
		
	}
	
}
