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

	/// <summary>Error that occurred while serializing a value or object into a JSON document</summary>
	[Serializable]
	public class JsonSerializationException : InvalidOperationException
	{

		public JsonSerializationException()
		{ }

		public JsonSerializationException(string message)
			: base(message)
		{ }

		public JsonSerializationException(string message, Exception? innerException)
			: base(message, innerException)
		{ }

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		[EditorBrowsable(EditorBrowsableState.Never)]
		protected JsonSerializationException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{ }

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonSerializationException CannotSerializeDerivedTypeWithoutTypeDiscriminator(Type concreteType, Type baseType)
			=> new($"Could not serialize concrete type '{concreteType.GetFriendlyName()}' because it is not declared on parent abstract type '{baseType.GetFriendlyName()}'. Please add a JsonDerivedTypeAttribute annotation on the parent type to include this derived type.");

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonSerializationException CannotPackDerivedTypeWithUnknownTypeDiscriminator(Type concreteType, Type baseType)
			=> new($"Could not converter concrete type '{concreteType.GetFriendlyName()}' to a JSON value because it is not declared on parent abstract type '{baseType.GetFriendlyName()}'. Please add a JsonDerivedTypeAttribute annotation on the parent type to include this derived type.");

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonSerializationException CannotPackCustomTypeWithoutPolymorphicDeclaration(Type type)
			=> new($"Could not serialize polymorphic type '{type.GetFriendlyName()}' because it does not declare any derived types. Please add one [JsonDerivedType] attribute declaration for each derived type to include.");

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonSerializationException CannotPackTaskLikeType(Type type)
			=> new($"Cannot pack task-like type {type.GetFriendlyName()}. Did you forget the 'await' keyword?");

		[MustUseReturnValue, Pure, MethodImpl(MethodImplOptions.NoInlining)]
		public static JsonSerializationException CanonicalOutputRequiresPackableSerializer(Type serializerType)
			=> new($"Serializer '{serializerType.GetFriendlyName()}' cannot produce canonical output because it does not implement IJsonPacker<T>. Canonical output requires a packable serializer.");

	}

}
