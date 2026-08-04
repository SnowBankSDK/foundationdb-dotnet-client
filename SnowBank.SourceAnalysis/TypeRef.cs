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

namespace SnowBank.SourceAnalysis
{
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;
	using Microsoft.CodeAnalysis;

	/// <summary>A token that represents a type (with optional nullability annotations).</summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public sealed class TypeRef : IEquatable<TypeRef>
	{

		// private static readonly Dictionary<SpecialType, TypeRef> s_cachedTypes = new();
		// private static readonly ReaderWriterLockSlim s_lock = new();

		public static bool IsPrimitiveType(ITypeSymbol type)
		{
			switch (type.SpecialType)
			{
				case >= SpecialType.System_Boolean and <= SpecialType.System_String:
				case SpecialType.System_DateTime:
					return true;
			}

			switch(type.ContainingNamespace?.ToDisplayString())
			{
				case "System":
				{
					if (type.Name is "DateTimeOffset" or "Guid" or "Half" or "Int128" or "UInt128")
					{
						return true;
					}
					break;
				}
			}

			return false;
		}

		public static TypeRef Create(ITypeSymbol type)
		{
			return new(type);
			//return IsPrimitiveType(type) ? CachedPrimitiveType(type) : new(type);

			// static TypeRef CachedPrimitiveType(ITypeSymbol type)
			// {
			// 	s_lock.EnterUpgradeableReadLock();
			// 	if (!s_cachedTypes.TryGetValue(type.SpecialType, out var cached))
			// 	{
			// 		cached = new(type);
			// 		s_lock.EnterWriteLock();
			// 		s_cachedTypes[type.SpecialType] = cached;
			// 		s_lock.ExitWriteLock();
			// 	}
			// 	s_lock.ExitUpgradeableReadLock();
			// 	return cached;
			// }
		}

		public TypeRef(ITypeSymbol type)
		{
			if (type is null) throw new ArgumentNullException(nameof(type));

			this.Name = type.Name;
			this.NameSpace = type.ContainingNamespace?.ToDisplayString() ?? "";
			this.FullName = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
			this.FullyQualifiedName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
			this.Assembly = type.ContainingAssembly?.Name ?? "";
		}

		public string Name { get; }

		public string NameSpace { get; }

		/// <summary>Fully qualified assembly name, prefixed with "global::", e.g. <c>global::System.Numerics.BigInteger.</c></summary>
		public string FullyQualifiedName { get; }

		/// <summary>Full name of the type (with namespace and name)</summary>
		public string FullName { get; }

		public string Assembly { get; }

		public override bool Equals(object? obj) => this.Equals(obj as TypeRef);
	
		public bool Equals(TypeRef? other) => other != null && this.FullyQualifiedName == other.FullyQualifiedName;

		public override int GetHashCode() => this.FullyQualifiedName.GetHashCode();

		public override string ToString() => $"typeof({this.FullyQualifiedName})";

		public bool IsIEnumerableOf() => this.Name == "IEnumerable" && this.NameSpace == "System.Collections.Generic";

		public bool IsIDictionaryOf() => this.Name == "IDictionary" && this.NameSpace == "System.Collections.Generic";

	}

	/// <summary>Extracted metadata about one declared member of an enum type.</summary>
	/// <remarks>Captured so that a generator can emit a reflection-free label lookup (a <c>switch</c> over the declared values) instead of calling <c>Enum.ToString()</c>, which resolves names through the runtime's reflection-backed cache.</remarks>
	[DebuggerDisplay("{Name} = {Value}")]
	public sealed record EnumMemberMetadata
	{

		/// <summary>Name of the field, as declared in C#</summary>
		public required string Name { get; init; }

		/// <summary>Constant value of the field, formatted in the invariant culture (the underlying integer, as text)</summary>
		public required string Value { get; init; }

		/// <summary>Value of the <c>[EnumMember(Value = ...)]</c> attribute on the field, if any (the DataContract wire token)</summary>
		public string? EnumMemberValue { get; init; }

		/// <summary>The field carries an <c>[EnumMember]</c> attribute, with or without a <c>Value</c></summary>
		/// <remarks>Distinct from <see cref="EnumMemberValue"/>, which only records a renaming: on a <c>[DataContract]</c> enum the bare attribute is what makes the member serializable at all, so its PRESENCE has to survive on its own.</remarks>
		public bool HasEnumMemberAttribute { get; init; }

		/// <summary>Value of the <c>[JsonStringEnumMemberName("...")]</c> attribute on the field, if any (the System.Text.Json 9+ wire token)</summary>
		public string? JsonStringEnumMemberName { get; init; }

	}

	/// <summary>Extracted metadata about a type symbol, that can be used to detect changes.</summary>
	[DebuggerDisplay("Name = {Name}")]
	public sealed record TypeMetadata
	{

		//private static readonly Dictionary<(SpecialType Type, NullableAnnotation Annotation), TypeMetadata> s_cachedTypes = new();
		//private static readonly ReaderWriterLockSlim s_lock = new(LockRecursionPolicy.SupportsRecursion);

		public static TypeMetadata Create(ITypeSymbol type)
		{
			return new (type, TypeRef.IsPrimitiveType(type));
			//return TypeRef.IsPrimitiveType(type) ? CachedPrimitiveType(type) : new(type, primitive: false);

			// static TypeMetadata CachedPrimitiveType(ITypeSymbol type)
			// {
			// 	s_lock.EnterUpgradeableReadLock();
			// 	if (!s_cachedTypes.TryGetValue((type.SpecialType, type.NullableAnnotation), out var cached))
			// 	{
			// 		cached = new(type, true);
			// 		s_lock.EnterWriteLock();
			// 		s_cachedTypes[(type.SpecialType, type.NullableAnnotation)] = cached;
			// 		s_lock.ExitWriteLock();
			// 	}
			// 	s_lock.ExitUpgradeableReadLock();
			// 	return cached;
			// }
		}

		private static bool IsTopLevelType(INamedTypeSymbol type)
		{
			if (type.Name is "Object" or "ValueType" or "Array")
			{
				return type.ContainingNamespace?.ToDisplayString() is "System";
			}

			return false;
		}

		internal TypeMetadata(ITypeSymbol type, bool primitive)
		{
			if (type is null) throw new ArgumentNullException(nameof(type));

			this.Ref = TypeRef.Create(type);
			this.Name = type.Name;
			this.IsAbstract = type.IsAbstract;
			this.IsSealed = type.IsSealed;
			this.IsReadOnly = type.IsReadOnly;
			this.TypeKind = type.TypeKind;
			this.IsPrimitive = primitive;
			this.SpecialType = type.OriginalDefinition?.SpecialType ?? default;
			this.Nullability = type.NullableAnnotation;
			this.IsRecord = type.IsRecord;
			this.HasDefaultConstructor = type is INamedTypeSymbol { InstanceConstructors: var ctors } && ctors.Any(c => c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public);
			if (this.SpecialType == SpecialType.System_Nullable_T)
			{
				var underlyingType = (type as INamedTypeSymbol)?.TypeArguments.FirstOrDefault();
				if (underlyingType is not null)
				{
					this.Name = "Nullable<" + underlyingType.Name + ">";
					this.NullableOfType = Create(underlyingType);
				}
			}

			if (this.TypeKind == TypeKind.Enum)
			{
				CaptureEnumMembers(type, out var enumMembers, out var isFlags, out var underlying, out var dataContractEnum);
				this.EnumMembers = enumMembers;
				this.IsFlagsEnum = isFlags;
				this.EnumUnderlyingSpecialType = underlying;
				this.IsDataContractEnum = dataContractEnum;
			}
			else
			{
				this.EnumMembers = ImmutableEquatableArray<EnumMemberMetadata>.Empty;
			}

			// the chain of types this one is declared inside, outermost first, joined with '.' (null for a top-level type):
			// a wire that names a type after its DECLARATION (the DataContract wire spells a nested type "Outer.Inner")
			// cannot rebuild it from Name, which is the simple name only
			if (type.ContainingType is not null)
			{
				var chain = new List<string>();
				for (var declaring = type.ContainingType; declaring is not null; declaring = declaring.ContainingType)
				{
					chain.Add(declaring.Name);
				}
				chain.Reverse();
				this.DeclaringTypeNames = string.Join(".", chain);
			}

			foreach (var iface in type.AllInterfaces)
			{
				if (iface.Name == "ISerializable" && iface.ContainingNamespace?.ToDisplayString() == "System.Runtime.Serialization")
				{
					this.ImplementsISerializable = true;
					break;
				}
			}

			// we want to generate the hierarchy of derived classes
			var parent = type.BaseType;
			if (parent is not null && !IsTopLevelType(parent))
			{
				var parents = new List<TypeRef>();
				do
				{
					parents.Add(TypeRef.Create(parent));
					parent = parent.BaseType;
				}
				while (parent is not null && !IsTopLevelType(parent));
				this.Parents = parents.ToImmutableEquatableArray();
			}
			else
			{
				this.Parents = ImmutableEquatableArray<TypeRef>.Empty;
			}

			var ifaces = type.Interfaces;
			if (ifaces.Length > 0)
			{
				// capture this for debugging purpose!
				this.Interfaces = ifaces.Select(TypeRef.Create).ToImmutableEquatableArray();
			}
			else
			{
				this.Interfaces = ImmutableEquatableArray<TypeRef>.Empty;
			}

			if (type is IArrayTypeSymbol array)
			{
				this.ElementType = Create(array.ElementType);
				this.Name = this.ElementType.Name + "[]";
				//TODO: capture the nullability of the element type?
			}
			else if (type is INamedTypeSymbol named)
			{

				if (named.IsGenericType)
				{
					var typeArgs = new List<TypeRef>(named.Arity);
					foreach (var arg in named.TypeArguments)
					{
						//note: we cannot create TypeMetadata because there is a possibility of infinite recursion
						// when using CRTPs like `class Foo<TFoo> where TFoo: Foo<TFoo>`
						typeArgs.Add(TypeRef.Create(arg));
					}
					this.TypeArguments = typeArgs.ToImmutableEquatableArray();
				}

				if (this.Ref.IsIEnumerableOf())
				{ // this is IEnumerable<T> explicitly
					this.ElementType = Create(named.TypeArguments[0]);
				}
				else if (this.Ref.IsIDictionaryOf())
				{ // this is IDictionary<TKey, TValue> explicitly
					this.KeyType = Create(named.TypeArguments[0]);
					this.ValueType = Create(named.TypeArguments[1]);
				}
				else
				{
					// note: inspect ALL interfaces, including inherited ones: a user subclass of Collection<T> or
					// KeyedCollection<TKey, TItem> declares no interface directly, they all come from its base type
					foreach (var iface in named.AllInterfaces)
					{
						var ifaceRef = TypeRef.Create(iface);
						if (ifaceRef.IsIEnumerableOf())
						{
							this.ElementType = Create(iface.TypeArguments[0]);
						}
						else if (ifaceRef.IsIDictionaryOf())
						{
							this.KeyType = Create(iface.TypeArguments[0]);
							this.ValueType = Create(iface.TypeArguments[1]);
						}
						else if (ifaceRef.Name == "ICollection" && ifaceRef.NameSpace == "System.Collections.Generic")
						{
							this.ImplementsGenericICollection = true;
						}
					}
				}
			}

			this.TypeArguments ??= ImmutableEquatableArray<TypeRef>.Empty;

			this.FullyQualifiedNameAnnotated =
				(this.Nullability == NullableAnnotation.Annotated && this.NullableOfType is null)
					? (this.FullyQualifiedName + "?")
					: this.FullyQualifiedName;
		}

		/// <summary>Collects the declared members of an enum type, with any custom wire token declared on their fields</summary>
		/// <remarks>Tokens are matched by attribute NAME and namespace, so no reference (and no version floor) on System.Text.Json is required, and a hand-written or injected clone of either attribute is matched too. This mirrors what the runtime enum cache does on the JSON side.</remarks>
		private static void CaptureEnumMembers(ITypeSymbol type, out ImmutableEquatableArray<EnumMemberMetadata> members, out bool isFlags, out SpecialType underlyingSpecialType, out bool isDataContractEnum)
		{
			isFlags = false;
			isDataContractEnum = false;
			foreach (var attribute in type.GetAttributes())
			{
				var attributeClass = attribute.AttributeClass;
				string attributeNamespace = attributeClass?.ContainingNamespace?.ToDisplayString() ?? "";
				if (attributeClass?.Name == "FlagsAttribute" && attributeNamespace == "System")
				{
					isFlags = true;
				}
				else if (attributeClass?.Name == "DataContractAttribute" && attributeNamespace == "System.Runtime.Serialization")
				{
					isDataContractEnum = true;
				}
			}

			underlyingSpecialType = (type as INamedTypeSymbol)?.EnumUnderlyingType?.SpecialType ?? SpecialType.System_Int32;

			var list = new List<EnumMemberMetadata>();
			foreach (var member in type.GetMembers())
			{
				if (member is not IFieldSymbol { HasConstantValue: true, IsStatic: true } field) continue;

				string? enumMemberValue = null;
				string? stjName = null;
				bool hasEnumMember = false;
				foreach (var attribute in field.GetAttributes())
				{
					var attributeClass = attribute.AttributeClass;
					if (attributeClass is null) continue;
					string ns = attributeClass.ContainingNamespace?.ToDisplayString() ?? "";
					if (attributeClass.Name == "EnumMemberAttribute" && ns == "System.Runtime.Serialization")
					{ // a bare [EnumMember] (no Value) keeps the declared name, matching DataContract behavior
						hasEnumMember = true;
						foreach (var arg in attribute.NamedArguments)
						{
							if (arg.Key == "Value" && arg.Value.Value is string { Length: > 0 } value)
							{
								enumMemberValue = value;
							}
						}
					}
					else if (attributeClass.Name == "JsonStringEnumMemberNameAttribute" && ns == "System.Text.Json.Serialization")
					{
						if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string { Length: > 0 } name)
						{
							stjName = name;
						}
					}
				}

				list.Add(new()
				{
					Name = field.Name,
					Value = Convert.ToString(field.ConstantValue, CultureInfo.InvariantCulture) ?? "0",
					EnumMemberValue = enumMemberValue,
					HasEnumMemberAttribute = hasEnumMember,
					JsonStringEnumMemberName = stjName,
				});
			}

			members = list.ToImmutableEquatableArray();
		}

		private ImmutableEquatableArray<TypeRef> Parents { get; }

		public TypeRef Ref { get; }

		public string Name { get; }

		public string NameSpace => this.Ref.NameSpace;

		public string Assembly => this.Ref.Assembly;
		public string FullName => this.Ref.FullName;

		/// <summary>Fully qualified assembly name, prefixed with "global::", e.g. global::System.Numerics.BigInteger.</summary>
		public string FullyQualifiedName => this.Ref.FullyQualifiedName;

		/// <summary>Fully qualified assembly name, including optional nullability annotations</summary>
		/// <remarks>Returns the <see cref="FullyQualifiedName"/> with an extra <c>?</c> marker if this is a reference type.</remarks>
		public string FullyQualifiedNameAnnotated { get; }

		public bool IsAbstract { get; }

		public bool IsSealed { get; }

		/// <summary>The type has a public parameterless constructor</summary>
		public bool HasDefaultConstructor { get; }

		/// <summary>The type implements <see cref="System.Collections.Generic.ICollection{T}"/> (directly or through a base type), so an instance can be filled via <c>Add(..)</c></summary>
		public bool ImplementsGenericICollection { get; }

		/// <summary>If this is a struct, whether it is declared <c>readonly</c></summary>
		public bool IsReadOnly { get; }

		public bool IsRecord { get; }

		public TypeKind TypeKind { get; }

		/// <summary>Enumeration that identifies 'special' types</summary>
		public SpecialType SpecialType { get; }

		/// <summary>If this is a "primitive" type that is defined by mscorlib (like <c>string</c>, <c>bool</c>, <c>int</c>, <c>DateTime</c>, <c>Guid</c>, ...)</summary>
		public bool IsPrimitive { get; }

		/// <summary>If this is a <see cref="Nullable{T}"/> (ex: <c>int?</c>) references the underlying concrete type (ex: <c>int</c>); otherwise, <c>null</c></summary>
		public TypeMetadata? NullableOfType { get; }

		/// <summary>If this is a generic type, the list of type arguments</summary>
		public ImmutableEquatableArray<TypeRef> TypeArguments { get; }

		/// <summary>If this is an array or collection (ex: <c>int[]</c>, <c>List&lt;int&gt;</c>), references the type of the elements of the array or collection (ex: <c>int</c>)</summary>
		public TypeMetadata? ElementType { get; }

		/// <summary>If this is a dictionary or set type (ex: <c>Dictionary&lt;int, string&gt;</c>), references the type of the keys of the dictionary or set (ex: <c>int</c>)</summary>
		public TypeMetadata? KeyType { get; }

		/// <summary>If this is a dictionary type (ex: <c>Dictionary&lt;int, string&gt;</c>), references the type of the values of the dictionary (ex: <c>string</c>).</summary>
		public TypeMetadata? ValueType { get; }

		public NullableAnnotation Nullability { get; }

		public ImmutableEquatableArray<TypeRef> Interfaces { get; } //HACKHACK: temporary, while we are debugging this thing!

		/// <summary>Names of the types this one is declared inside, outermost first, joined with <c>'.'</c> (ex: <c>"Outer.Middle"</c> for <c>Outer.Middle.Inner</c>), or <see langword="null"/> for a top-level type</summary>
		/// <remarks>Captured because a wire can name a type after its DECLARATION rather than after its simple name: the DataContract wire spells a nested type <c>Outer.Inner</c>, which <see cref="Name"/> alone cannot rebuild.</remarks>
		public string? DeclaringTypeNames { get; }

		/// <summary>The type implements <see cref="System.Runtime.Serialization.ISerializable"/>, directly or through a base type or interface</summary>
		/// <remarks>Resolved over ALL interfaces, not just the declared ones: a type that inherits the interface from its base class serializes through the same dialect, and missing that would silently produce a different wire.</remarks>
		public bool ImplementsISerializable { get; }

		/// <summary>This is an enum type carrying <c>[DataContract]</c>, which makes <c>[EnumMember]</c> the opt-in signal of each of its members</summary>
		/// <remarks>Only ever set for an enum. On such a type the DataContract wire refuses a value whose member carries no <c>[EnumMember]</c>, instead of falling back to the declared name.</remarks>
		public bool IsDataContractEnum { get; }

		public override string ToString() => $"{{ Name = {this.Name}, Kind = {TypeKind}, Special = {SpecialType}{(IsSealed?", Sealed" : "")}{(IsRecord?", Record" : "")}, FullName = {this.Ref.FullName}}}";

		public void Explain(StringBuilder sb, string? indent = null)
		{
			sb.Append(indent).Append("Name = ").AppendLine(this.Name);
			sb.Append(indent).Append("NameSpace = ").AppendLine(this.Ref.NameSpace);
			sb.Append(indent).Append("Assembly = ").AppendLine(this.Ref.Assembly);
			sb.Append(indent).Append("FullName = ").AppendLine(this.Ref.FullName);
			sb.Append(indent).Append("FullyQualifiedName = ").AppendLine(this.Ref.FullyQualifiedName);
			sb.Append(indent).Append("FullyQualifiedNameAnnotated = ").AppendLine(this.FullyQualifiedNameAnnotated);
			if (this.NullableOfType != null)
			{
				sb.Append(indent).AppendLine("Underlying Type:");
				this.NullableOfType.Explain(sb, indent is null ? "- " : ("  " + indent));
				return;
			}
			sb.Append(indent).Append("TypeKind = ").AppendLine(this.TypeKind.ToString());
			if (this.SpecialType != SpecialType.None) sb.Append(indent).Append("SpecialType = ").AppendLine(this.SpecialType.ToString());
			if (this.Nullability != NullableAnnotation.None) sb.Append(indent).Append("Nullability = ").AppendLine(this.Nullability.ToString());
			if (this.IsPrimitive)
			{
				sb.Append(indent).AppendLine("IsPrimitive = true");
				// don't need to say more for basic types!
				return;
			}

			if (this.IsSealed) sb.Append(indent).AppendLine("IsSealed = true");
			if (this.IsRecord) sb.Append(indent).AppendLine("IsRecord = true");
			if (this.TypeArguments.Count > 0)
			{
				sb.Append(indent).AppendLine("TypeArguments:");
				var subIndent = indent is null ? "- " : ("  " + indent);
				foreach (var typeArg in this.TypeArguments)
				{
					sb.Append(subIndent).AppendLine(typeArg.FullName);
				}
			}
			if (this.ElementType is not null)
			{
				sb.Append(indent).AppendLine("ElementType:");
				this.ElementType.Explain(sb, indent is null ? "- " : ("  " + indent));
			}
			if (this.KeyType is not null)
			{
				sb.Append(indent).AppendLine("KeyType:");
				this.KeyType.Explain(sb, indent is null ? "- " : ("  " + indent));
			}
			if (this.ValueType is not null)
			{
				sb.Append(indent).AppendLine("ValueType:");
				this.ValueType.Explain(sb, indent is null ? "- " : ("  " + indent));
			}
			if (this.Parents.Count > 0)
			{
				sb.Append(indent).AppendLine("Parents:");
				var subIndent = indent is null ? "- " : ("  " + indent);
				foreach (var @class in this.Parents)
				{
					sb.Append(subIndent).AppendLine(@class.FullyQualifiedName);
				}
			}
			if (this.Interfaces.Count > 0)
			{
				sb.Append(indent).AppendLine("Interfaces:");
				var subIndent = indent is null ? "- " : ("  " + indent);
				foreach (var iface in this.Interfaces)
				{
					sb.Append(subIndent).AppendLine(iface.FullyQualifiedName);
				}
			}
		}

		public bool CanBeNull() => this.TypeKind is not (TypeKind.Struct or TypeKind.Enum) || this.SpecialType is SpecialType.System_Nullable_T;

		/// <summary>Tests if this is a generic type</summary>
		public bool IsGenericType() => this.TypeArguments.Count > 0;

		public bool IsValueType() => this.TypeKind is TypeKind.Struct or TypeKind.Enum;

		public bool IsEnum() => this.TypeKind is TypeKind.Enum;

		/// <summary>If this is an enum, its declared members in declaration order (with any custom wire token); otherwise empty</summary>
		public ImmutableEquatableArray<EnumMemberMetadata> EnumMembers { get; }

		/// <summary>If this is an enum, whether it carries <c>[Flags]</c>, so that an undeclared value may be a combination of declared ones</summary>
		public bool IsFlagsEnum { get; }

		/// <summary>If this is an enum, the <see cref="Microsoft.CodeAnalysis.SpecialType"/> of its underlying integer type</summary>
		public SpecialType EnumUnderlyingSpecialType { get; }

		public bool IsNullableOfT()
		{
			return this.SpecialType == SpecialType.System_Nullable_T;
		}

		public bool IsNullableOfT([MaybeNullWhen(false)] out TypeMetadata underlyingType)
		{
			if (this.SpecialType is SpecialType.System_Nullable_T)
			{
				underlyingType = this.NullableOfType!;
				return true;
			}

			underlyingType = null;
			return false;
		}

		/// <summary>Tests if this is the <see cref="string"/> type</summary>
		public bool IsString() => this.SpecialType is SpecialType.System_String;

		/// <summary>Tests if this is the <see cref="int"/> type</summary>
		public bool IsInt32() => this.SpecialType is SpecialType.System_Int32;

		/// <summary>Tests if this is an array (ex: <c>int[]</c>, <c>string?[]</c>)</summary>
		/// <remarks>If <c>true</c>, <see cref="ElementType"/> holds the type of the elements of this array</remarks>
		public bool IsArray() => this.TypeKind is TypeKind.Array;

		/// <summary>Tests if this is an array (ex: <c>int[]</c>, <c>string?[]</c>)</summary>
		/// <remarks>If <c>true</c>, <see cref="ElementType"/> holds the type of the elements of this array</remarks>
		public bool IsArray([MaybeNullWhen(false)] out TypeMetadata elementType)
		{
			if (this.TypeKind is not TypeKind.Array)
			{
				elementType = null!;
				return false;
			}

			elementType = this.ElementType!;
			return true;
		}

		/// <summary>Tests if this type is <see cref="List{T}"/> (ex: <c>List&lt;int&gt;</c>, <c>List&lt;string?&gt;</c>)</summary>
		/// <remarks>If <c>true</c>, <see cref="ElementType"/> holds the type of the elements of this list</remarks>
		public bool IsList() => this.Name is "List" && this.NameSpace is "System.Collections.Generic";

		public bool IsList([MaybeNullWhen(false)] out TypeMetadata elementType)
		{
			if (!IsList())
			{
				elementType = null;
				return false;
			}

			elementType = this.ElementType!;
			return true;
		}

		/// <summary>Tests if instances of this type will be packed into a <c>JsonString</c> value</summary>
		public bool IsStringLike(bool allowNullables = true)
		{
			if (allowNullables && this.NullableOfType is not null)
			{
				return this.NullableOfType.IsStringLike();
			}

			switch (this.SpecialType)
			{
				case SpecialType.System_String:
				{
					return true;
				}
			}

			if (this.NameSpace is "System")
			{
				return this.Name is "Guid" or "Uuid128" or "Uuid96" or "Uuid80" or "Uuid64";
			}

			return false;
		}


		/// <summary>Tests if this is the <see cref="Guid"/> type</summary>
		public bool IsGuid() => this.NameSpace is "System" && this.Name is "Guid";

		/// <summary>Tests if this is the <see cref="DateTime"/> type</summary>
		public bool IsDateTime() => this.SpecialType == SpecialType.System_DateTime;

		/// <summary>Tests if this is the <see cref="DateTimeOffset"/> type</summary>
		public bool IsDateTimeOffset() => this.NameSpace is "System" && this.Name is "DateTimeOffset";

		/// <summary>Tests if instances of this type will be packed into a <c>JsonBoolean</c> value</summary>
		public bool IsBooleanLike(bool allowNullables = true)
		{
			if (allowNullables && this.NullableOfType is not null)
			{
				return this.NullableOfType.IsBooleanLike();
			}

			return this.SpecialType == SpecialType.System_Boolean;
		}

		/// <summary>Tests if instances of this type will be packed into a <c>JsonNumber</c> value</summary>
		public bool IsNumberLike(bool allowNullables = true)
		{
			if (allowNullables && this.NullableOfType is not null)
			{
				return this.NullableOfType.IsNumberLike();
			}

			switch (this.SpecialType)
			{
				case SpecialType.System_UInt16:
				case SpecialType.System_Int16:
				case SpecialType.System_UInt32:
				case SpecialType.System_Int32:
				case SpecialType.System_Int64:
				case SpecialType.System_UInt64:
				case SpecialType.System_Single:
				case SpecialType.System_Double:
				case SpecialType.System_Decimal:
				{
					return true;
				}
			}

			if (this.NameSpace is "System")
			{
				return this.Name is ("Half" or "Int128" or "UInt128");
			}

			return false;
		}

		/// <summary>Tests if instances of this type will be packed into a <c>JsonDateTime</c> value</summary>
		public bool IsDateLike(bool allowNullables = true)
		{
			if (allowNullables && this.NullableOfType is not null)
			{
				return this.NullableOfType.IsDateLike();
			}

			if (this.SpecialType == SpecialType.System_DateTime)
			{
				return true;
			}

			if (this.NameSpace == "System")
			{
				return this.Name is (nameof(DateTimeOffset) or "DateOnly");
			}

			if (this.NameSpace == "NodaTime")
			{
				return this.Name is "Instant";
			}

			return false;
		}

		/// <summary>Tests if the type is exactly the <see cref="IEnumerable{T}"/> interface (and not any derived type)</summary>
		/// <remarks>
		/// <list type="table">
		///   <listheader><term>type</term><description>result</description></listheader>
		///   <item><term>DateTime</term><description>false</description></item>
		///   <item><term>MyCustomType</term><description>false</description></item>
		///   <item><term>IEnumerable&lt;char&gt;</term><description>true</description></item>
		///   <item><term>char[]</term><description>false</description></item>
		///   <item><term>List&lt;char&gt;</term><description>true</description></item>
		///   <item><term>string</term><description>false</description></item>
		/// </list>
		/// </remarks>
		public bool IsEnumerableInterface([MaybeNullWhen(false)] out TypeMetadata elemType)
		{
			elemType = this.ElementType;
			return elemType is not null && this.NameSpace == "System.Collections.Generic" && this.Name == "Enumerable`1";
		}

		/// <summary>Tests if this type implements <see cref="IEnumerable{T}"/></summary>
		/// <param name="elemType">Receives the type of the element</param>
		/// <remarks>
		/// <list type="table">
		///   <listheader><term>type</term><description>result</description></listheader>
		///   <item><term>DateTime</term><description>false</description></item>
		///   <item><term>MyCustomType</term><description>false</description></item>
		///   <item><term>IEnumerable&lt;char&gt;</term><description>true</description></item>
		///   <item><term>char[]</term><description>true</description></item>
		///   <item><term>List&lt;char&gt;</term><description>true</description></item>
		///   <item><term>string</term><description>true</description></item>
		/// </list>
		/// </remarks>
		public bool IsEnumerable(out TypeMetadata elemType)
		{
			if (this.ElementType is null)
			{
				elemType = null!;
				return false;
			}
			elemType = this.ElementType;
			return true;
		}

		/// <summary>Tests if this type is a dictionary</summary>
		/// <param name="keyType">Receives the type of the keys</param>
		/// <param name="valueType">Receives the type of the values</param>
		public bool IsDictionary(out TypeMetadata keyType, out TypeMetadata valueType)
		{
			if (this.ValueType is null)
			{
				keyType = null!;
				valueType = null!;
				return false;
			}
			keyType = this.KeyType!;
			valueType = this.ValueType;
			return true;
		}

	}

}
