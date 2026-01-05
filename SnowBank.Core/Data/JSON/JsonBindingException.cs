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
	using System.ComponentModel;
	using System.Runtime.Serialization;
	using SnowBank.Runtime;

	/// <summary>Error that occurred while deserializing a JSON value back into a CLR type</summary>
	[Serializable]
	public class JsonBindingException : InvalidOperationException
	{

		public JsonValue? Value { get; }

		public JsonPath? Path { get; }

		public string? Reason { get; }

		public Type? TargetType { get; }

		public JsonBindingException()
		{ }

		public JsonBindingException(string message)
			: base(message)
		{ }

		public JsonBindingException(string message, Exception? innerException)
			: base(message, innerException)
		{ }

		public JsonBindingException(string message, JsonValue? value, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Value = value;
		}

		public JsonBindingException(string message, JsonPath? path, JsonValue? value, Type? targetType, Exception? innerException = null)
			: base(message, innerException)
		{
			this.Path = path;
			this.Value = value;
			this.TargetType = targetType;
		}

		public JsonBindingException(string message, string? reason, JsonPath? path, JsonValue? value, Type? targetType, Exception? innerException)
			: base(message, innerException)
		{
			this.Path = path;
			this.Value = value;
			this.Reason = reason;
			this.TargetType = targetType;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected JsonBindingException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Value = JsonValue.Parse(info.GetString("Value"));
			this.Path = JsonPath.Create(info.GetString("Path"));
			this.Reason = info.GetString("Reason");
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Value", this.Value?.ToJsonText());
			info.AddValue("Path", this.Path?.Value.ToString());
			info.AddValue("Reason", this.Reason);
		}

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CouldNotResolveClassId(string classId) => new($"Could not find any Type named '{classId}' during deserialization.");

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeNoBinderOrGenerator(JsonValue value, Type type) => new($"Cannot deserialize custom type '{type.GetFriendlyName()}' because it has no default generator and no custom binder.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeNoTypeDefinition(JsonValue value, Type type) => new($"Could not find any Type Definition while deserializing custom type '{type.GetFriendlyName()}'.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeNoConcreteClassFound(JsonValue value, Type type, string customClass) => new($"Could not find a concrete type to deserialize object of type '{type.GetFriendlyName()}' with custom class name '{customClass}'.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeBadType(JsonValue value, string customClass) => new($"Cannot bind custom class name '{customClass}' because it is not a safe type in this context.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeIncompatibleType(JsonValue value, Type type, string customClass) => new($"Cannot bind custom class name '{customClass}' into object of type '{type.GetFriendlyName()}' because there are no known valid cast between them.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeWithUnknownTypeDiscriminator(JsonValue value, Type type, JsonValue discriminator) => new($"Could not find a concrete type to deserialize object of base type '{type.GetFriendlyName()}' with discriminator '{discriminator}'.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException FailedToConstructTypeInstanceErrorOccurred(JsonValue value, Type type, Exception e) => new($"Failed to construct a new instance of type '{type.GetFriendlyName()}' while deserializing a {nameof(JsonObject)}.", value, e);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException FailedToConstructTypeInstanceReturnedNull(JsonValue value, Type type) => new($"Cannot deserialize custom type '{type.GetFriendlyName()}' because the generator returned a null instance.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeNoReaderForMember(JsonValue value, CrystalJsonMemberDefinition member, Type type) => new($"No reader found for member {member.Name} of type '{type.GetFriendlyName()}'.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotDeserializeCustomTypeNoBinderForMember(JsonValue value, CrystalJsonMemberDefinition member, Type type) => new($"No 'set' operation found for member {member.Name} of type '{type.GetFriendlyName()}'.", value);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonValueToThisType(JsonValue value, Type type, Exception? innerException = null) => new($"Cannot convert JSON {value.Type} to type '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonObjectToThisType(JsonValue? value, Type type, Exception? innerException = null) => new($"Cannot bind a JSON Object to type '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonArrayToThisType(JsonArray? value, Type type, Exception? innerException = null) => new($"Cannot bind a JSON Array to type '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonValueToArrayOfThisType(JsonValue value, Type type, Exception? innerException = null) => new($"Cannot bind a JSON {value.Type} to an array of '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonStringToThisType(JsonString value, Type type, Exception? innerException = null) => new($"Cannot convert JSON String to type '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindJsonNumberToThisType(JsonNumber value, Type type, Exception? innerException = null) => new($"Cannot convert JSON Number to type '{type.GetFriendlyName()}'.", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindMalformedOrInvalidJsonValue(JsonValue value, Type type, string message, Exception? innerException = null) => new($"Cannot bind malformed JSON {value.Type} to type '{type.GetFriendlyName()}': {message}", value, innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindArrayWithNullItemToCollectionOfType(JsonArray array, Type collectionType, Exception? innerException = null) => new($"Cannot bind a JSON Array to an instance of '{collectionType.GetFriendlyName()}' because it contains at least one null element.", innerException);

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonBindingException CannotBindArrayWithNullItemToCollectionOfType(JsonArray array, int index, Type collectionType, Exception? innerException = null) => new($"Cannot bind a JSON Array to an instance of '{collectionType.GetFriendlyName()}' because element at index {index} is null.", innerException);

	}

}
