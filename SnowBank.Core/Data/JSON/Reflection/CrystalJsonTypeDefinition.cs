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
	using SnowBank.Runtime;

	[Flags]
	public enum CrystalJsonTypeFlags
	{
		None = 0,
		SourceGenerated = 1 << 0,
		DefaultIsNull = 1 << 1,
		Polymorphic = 1 << 2,
		NonConstructible = 1 << 3,
		Sealed = 1 << 4,
		Anonymous = 1 << 5,
		/// <summary>A framework value type whose state no member can carry, so a JSON object is not a valid representation of it</summary>
		OpaqueValueType = 1 << 6,
	}

	[DebuggerDisplay("Type={Type.FullName}")]
	[PublicAPI]
	public sealed record CrystalJsonTypeDefinition
	{

		/// <summary>Type of the object</summary>
		public Type Type { get; init; }

		public CrystalJsonTypeFlags Flags { get; init; }

		public Type? BaseType { get; init; }

		public JsonEncodedPropertyName? TypeDiscriminatorProperty { get; init; }

		/// <summary>Custom ClassId for this type</summary>
		public JsonValue? TypeDiscriminatorValue { get; init; }

		/// <summary>Custom ClassId for this type</summary>
		public FrozenDictionary<JsonValue, Type>? DerivedTypeMap { get; init; }

		/// <summary>Specifies if the type can have a value of <see langword="null"/> (ref types, or <see cref="Nullable{T}"/>)</summary>
		public Type? NullableOfType { get; init; }

		/// <summary>Factory method that can instantiate a new value of this type</summary>
		public Func<object>? Generator { get; init; }

		/// <summary>Custom handler for deserializing <see cref="JsonValue"/> into instances of this type.</summary>
		public CrystalJsonTypeBinder? CustomBinder { get; init; }

		/// <summary>Custom converter attached to the type itself via <c>[JsonConverter(typeof(...))]</c>, if any</summary>
		/// <remarks>When set, <see cref="CustomBinder"/> already routes through it; the DOM route consults it directly.</remarks>
		internal IJsonMemberConverterBridge? CustomConverter { get; init; }

		/// <summary>Definitions of the fields and properties of this type</summary>
		public CrystalJsonMemberDefinition[] Members { get; init; }

		/// <summary>Custom visitor for serializing instances of this type</summary>
		public CrystalJsonTypeVisitor? Visitor { get; init; }

		/// <summary><c>[OnSerializing]</c> callback, invoked before this type's members are written</summary>
		public Action<object>? OnSerializing { get; init; }

		/// <summary><c>[OnSerialized]</c> callback, invoked after this type's members are written</summary>
		public Action<object>? OnSerialized { get; init; }

		/// <summary><c>[OnDeserializing]</c> callback, invoked on a freshly constructed instance before its members are populated</summary>
		/// <remarks>The second argument is the document being bound, for callbacks declared as <c>void M(JsonValue|JsonObject|JsonArray)</c>; a parameterless callback ignores it.</remarks>
		public Action<object, JsonValue?>? OnDeserializing { get; init; }

		/// <summary><c>[OnDeserialized]</c> callback, invoked after this type's members have been populated</summary>
		/// <inheritdoc cref="OnDeserializing"/>
		public Action<object, JsonValue?>? OnDeserialized { get; init; }

		/// <summary>Specifies if this type declares at least one serialization lifecycle callback</summary>
		public bool HasSerializationCallbacks => this.OnSerializing != null || this.OnSerialized != null || this.OnDeserializing != null || this.OnDeserialized != null;

		public CrystalJsonTypeDefinition(Type type, CrystalJsonTypeFlags flags, CrystalJsonTypeBinder? customBinder, Func<object>? generator, CrystalJsonMemberDefinition[] members, CrystalJsonTypeVisitor? visitor, Type? baseType, JsonEncodedPropertyName? typeDiscriminatorProperty, JsonValue? typeDiscriminatorValue, IReadOnlyDictionary<JsonValue, Type>? derivedTypeMap)
		{
			Contract.NotNull(type);
			Contract.NotNull(members);

			var nullableOfType = CrystalJsonTypeResolver.GetNullableType(type);

			if (type.IsAnonymousType()) flags |= CrystalJsonTypeFlags.Anonymous;
			if (type.IsSealed) flags |= CrystalJsonTypeFlags.Sealed;
			if (!type.IsValueType || nullableOfType != null) flags |= CrystalJsonTypeFlags.DefaultIsNull;
			if (type.IsInterface || type.IsAbstract) flags |= CrystalJsonTypeFlags.NonConstructible;
			if (typeDiscriminatorProperty != null) flags |= CrystalJsonTypeFlags.Polymorphic;

			this.Type = type;
			this.Flags = flags;
			this.Visitor = visitor;
			this.BaseType = baseType;
			this.TypeDiscriminatorProperty = typeDiscriminatorProperty;
			this.TypeDiscriminatorValue = typeDiscriminatorValue;
			this.DerivedTypeMap = derivedTypeMap?.ToFrozenDictionary();
			this.NullableOfType = nullableOfType;
			this.CustomBinder = customBinder;
			this.Generator = generator;
			this.Members = members;
		}

		public bool IsPolymorphic => this.Flags.HasFlag(CrystalJsonTypeFlags.Polymorphic);

		/// <summary>Specifies that a JSON object is not a valid representation of this type, so the member binder must reject one</summary>
		/// <remarks>Classified once, when the contract is built. See <see cref="CrystalJsonTypeFlags.OpaqueValueType"/>.</remarks>
		public bool IsOpaqueValueType => this.Flags.HasFlag(CrystalJsonTypeFlags.OpaqueValueType);

		/// <summary>Specifies if this is an anonymous type that does not have a valid name</summary>
		/// <remarks>ex: <c>CrystalJson.Serialize(new { "Hello": "World" })</c></remarks>
		public bool IsAnonymousType => this.Flags.HasFlag(CrystalJsonTypeFlags.Anonymous);

		/// <summary>Specifies if the type cannot possibly be derived (sealed class, or value type)</summary>
		/// <remarks>If <see langword="false"/>, a type-check will have to be performed during serialization, which add some overhead</remarks>
		public bool IsSealed => this.Flags.HasFlag(CrystalJsonTypeFlags.Sealed);

		/// <summary>Types with a default value that is <see langword="null"/></summary>
		/// <remarks><see langword="true"/> for Reference Types, or for <see cref="Nullable{T}"/>.</remarks>
		public bool DefaultIsNull => this.Flags.HasFlag(CrystalJsonTypeFlags.DefaultIsNull);

		/// <summary>Specifies if the type cannot be constructed directly (interface, abstract class, no public ctor, ...)</summary>
		/// <remarks><see langword="true"/> for interfaces or abstract types</remarks>
		public bool IsNonConstructible => this.Flags.HasFlag(CrystalJsonTypeFlags.NonConstructible);

	}

}
