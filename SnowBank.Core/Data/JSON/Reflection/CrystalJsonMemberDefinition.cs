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
	[Flags]
	public enum CrystalJsonMemberFlags
	{
		None = 0,
		SourceGenerated = 1 << 0,
		NotNull = 1 << 1,
		NonZeroDefault = 1 << 2,
		ReadOnly = 1 << 3,
		InitOnly = 1 << 4,
		Required = 1 << 5,
		Key = 1 << 6,
		/// <summary>The member is always serialized, even when the settings discard nulls or default values (<c>[JsonIgnore(Condition = JsonIgnoreCondition.Never)]</c>)</summary>
		AlwaysEmit = 1 << 7,
		/// <summary>The member is omitted when its value is <see langword="null"/>, regardless of the settings (<c>[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]</c>)</summary>
		OmitNullValues = 1 << 8,
		/// <summary>The member is omitted when its value equals its default, regardless of the settings (<c>[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]</c>)</summary>
		OmitDefaultValues = 1 << 9,
	}

	/// <summary>Structure that holds the cached serialization metadata for a field or property of a class or struct</summary>
	[DebuggerDisplay("Name={Name}, Type={Type}")]
	[PublicAPI]
	public sealed record CrystalJsonMemberDefinition
	{

		/// <summary>Declared type of the member</summary>
		public required Type Type { get; init; }

		/// <summary>Name of the member</summary>
		public required string Name { get; init; }

		/// <summary>Name in the enclosing type</summary>
		/// <remarks>Represent the original name in the c# code, while <see cref="Name"/> is the name in the JSON object</remarks>
		public required string OriginalName { get; init; }

		/// <summary>Flags for this member</summary>
		/// <returns>The flags are used by the various helper properties like <see cref="IsReadOnly"/>, <see cref="IsReadOnly"/> and so on</returns>
		public required CrystalJsonMemberFlags Flags { get; init; }

		/// <summary>Optional <see cref="JsonPropertyAttribute"/> attribute that was applied to this member</summary>
		public JsonPropertyAttribute? Attributes { get; init; }
		//REVIEW: this is only used by the writer to "remember" how to format enums inside a Dictionary (attribute set on the property that holds the dictionary)
		//TODO: get rid of this, and expose the settings directly on this type?

		/// <summary>If <see langword="true"/>, the field has a <see cref="DefaultValue"/> that is not the default for this type.</summary>
		/// <remarks>This is <see langword="false"/> if the default is <see langword="null"/>, <see langword="false"/>, <see langword="0"/>, etc...</remarks>
		public bool HasDefaultValue => this.Flags.HasFlag(CrystalJsonMemberFlags.NonZeroDefault);

		/// <summary>Default value for this member (when it is missing)</summary>
		public object? DefaultValue { get; init; }

		/// <summary>Func that can return the value of this member in an instance of the containing type</summary>
		public required Func<object, object?> Getter { get; init; }

		/// <summary>Func that can change the value of this member in an instance of the containing type</summary>
		public Action<object, object?>? Setter { get; init; }

		/// <summary>Delegate that can serialize values of this member into JSON</summary>
		public required CrystalJsonTypeVisitor Visitor { get; init; }

		/// <summary>Delegate that can bind JSON values to a type that is assignable to this member</summary>
		public required CrystalJsonTypeBinder Binder { get; init; }

		/// <summary>Returns <see langword="true"/> if a possible value for this member is the default value for this member's type (<see langword="null"/> for ref types or Nullable&lt;T&gt;, <see langword="0"/> for numbers, <see langword="false"/> for booleans, ...)</summary>
		public bool IsDefaultValue(object? value) => this.DefaultValue?.Equals(value) ?? (value is null);

		/// <summary>Cache for the various encoded versions of a property name</summary>
		public required JsonEncodedPropertyName EncodedName { get; init; }

		/// <summary>If not <see langword="null"/>, the member is an instance of <see cref="Nullable{T}"/> and this property contains the base value type</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }    // NullableOfType == null
		/// int? Foo { get; ... }   // NullableOfType == typeof(int)
		/// string Foo { get; ... } // NullableOfType == null
		/// </code></remarks>
		public Type? NullableOfType { get; init; }

		/// <summary>Flag set to <see langword="true"/> when the member is read-only or init-only</summary>
		public bool IsReadOnly  => this.Flags.HasFlag(CrystalJsonMemberFlags.ReadOnly);

		/// <summary>The member has the required keyword and cannot be null</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }              // IsRequired == false
		/// string Foo { get; ... }           // IsRequired == false
		/// required string? Foo { get; ... } // IsRequired == false
		/// 
		/// required int Foo { get; ... }     // IsRequired == true
		/// required string Foo { get; ... }  // IsRequired == true
		/// </code></remarks>
		public bool IsRequired => this.Flags.HasFlag(CrystalJsonMemberFlags.Required);

		/// <summary>The member has the <see cref="System.ComponentModel.DataAnnotations.KeyAttribute"/> attribute</summary>
		/// <remarks>Examples: <code>
		/// int Id { get; ... } // IsKey == false
		/// 
		/// [Key]
		/// int Id { get; ... } // IsKey == true
		/// </code></remarks>
		public bool IsKey => this.Flags.HasFlag(CrystalJsonMemberFlags.Key);

		/// <summary>The member is a read-only field, or a property with an init-only setter</summary>
		/// <remarks>Examples: <code>
		/// int Id;               // IsInitOnly == false
		/// int Id { get; set; }  // IsInitOnly == false
		/// int Id { get; }       // IsInitOnly == false
		/// 
		/// readonly int Id;      // IsInitOnly == true
		/// int Id { get; init; } // IsInitOnly == true
		/// </code></remarks>
		public bool IsInitOnly => this.Flags.HasFlag(CrystalJsonMemberFlags.InitOnly);

		/// <summary>The member cannot be null, or is annotated with <see cref="System.Diagnostics.CodeAnalysis.NotNullAttribute"/></summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNotNull == true
		/// int? Foo { get; ... }    // IsNotNull == false
		/// string Foo { get; ... }  // IsNotNull == true
		/// string? Foo { get; ... } // IsNotNull == false
		/// </code></remarks>
		public bool IsNotNull  => this.Flags.HasFlag(CrystalJsonMemberFlags.NotNull);

		/// <summary>The member if a reference type that is declared as nullable in its parent type</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNullableRefType == false
		/// int? Foo { get; ... }    // IsNullableRefType == false
		/// string Foo { get; ... }  // IsNullableRefType == false
		/// string? Foo { get; ... } // IsNullableRefType == true
		/// </code></remarks>
		public bool IsNullableRefType => !this.IsNotNull && this.NullableOfType == null;

		/// <summary>The member if a nullable value type</summary>
		/// <remarks>Examples: <code>
		/// int Foo { get; ... }     // IsNullableValueType == false
		/// int? Foo { get; ... }    // IsNullableValueType == true
		/// string Foo { get; ... }  // IsNullableValueType == false
		/// string? Foo { get; ... } // IsNullableValueType == false
		/// </code></remarks>
		public bool IsNullableValueType => this.NullableOfType != null;

		public bool IsNonNullableValueType => this.NullableOfType == null && this.Type.IsValueType;

	}

}
