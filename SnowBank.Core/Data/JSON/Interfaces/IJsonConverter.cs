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
	using System.Diagnostics.CodeAnalysis;

	/// <summary>Base non-generic interface for <see cref="IJsonConverter{T}"/> implementations</summary>
	[PublicAPI]
	public interface IJsonConverter
	{

		/// <summary>Type of the instances that are compatible with this converter</summary>
		Type GetTargetType();

		/// <summary>Returns the definition metadata for the type handled by this converter</summary>
		CrystalJsonTypeDefinition? GetDefinition();

		/// <summary>Returns the JSON property name for the corresponding member of the target type of this converter</summary>
		/// <param name="memberName">Name of the member of the target type of this converter, as in the ".NET" name for the property of field.</param>
		/// <param name="propertyName">Receives the corresponding name of the property, as found in the JSON serialized form.</param>
		/// <returns><c>true</c> if a member with this name exists in the target type of this converter; otherwise, <c>false</c></returns>
		bool TryMapMemberToPropertyName(string memberName, [MaybeNullWhen(false)] out string propertyName);

		/// <summary>Returns the member name in the target type of this converter for the corresponding JSON property</summary>
		/// <param name="propertyName">Name of the property, as found in the JSON serialized form.</param>
		/// <param name="memberName">Name of the corresponding member of the target type of this converter, as in the ".NET" name for the property of field.</param>
		/// <returns><c>true</c> if the property name matches a member in the target type of this converter; otherwise, <c>false</c></returns>
		bool TryMapPropertyToMemberName(string propertyName, [MaybeNullWhen(false)] out string memberName);

		/// <summary>Binds a JSON value into the corresponding CLR type</summary>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks><para>For best performance, prefer using the <see cref="IJsonDeserializer{T}.Unpack"/> method on the generic <see cref="IJsonConverter{T}"/> interface, if this converter implements it!</para></remarks>
		object? BindJsonValue(JsonValue value, ICrystalJsonTypeResolver? resolver);

		/// <summary>Untyped visitor that can serialize instances of the supported type</summary>
		/// <param name="value">Value to serialize</param>
		/// <param name="declaringType">Type has declared in the parent type (ex: type of the member that contains this value, which could be abstract)</param>
		/// <param name="runtimeType">Actual type of the value at runtime</param>
		/// <param name="writer">Output where the instances will be written to</param>
		void Serialize(object? value, Type declaringType, Type? runtimeType, CrystalJsonWriter writer);

	}

	/// <summary>Bundle interface that is implemented by source-generated encoders</summary>
	/// <remarks>
	/// <para>This interfaces bundles <see cref="IJsonSerializer{T}"/>, <see cref="IJsonDeserializer{T}"/> and <see cref="IJsonPacker{T}"/>,
	/// so that it is easier to provide APIs that take a single instance, that is able to both encode and decode JSON without needing two or three different parameters.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonConverter<T> : IJsonConverter, IJsonSerializer<T>, IJsonDeserializer<T>, IJsonPacker<T>
	{
		// this is just a marker interface
	}

#if !NETSTANDARD2_0
	// the source-gen proxy interfaces (IJsonReadOnlyProxy/IJsonWritableProxy) are not part of the netstandard2.0 backport (they need static abstract interface members)
	/// <summary>Bundle interface that is implemented by source-generated encoders for read-only proxies.</summary>
	/// <typeparam name="TValue">Beacon type for the exposed proxy</typeparam>
	/// <typeparam name="TReadOnlyProxy">Type of the generated read-only proxy that mimics the properties on type <typeparamref name="TValue"/></typeparam>
	[PublicAPI]
	public interface IJsonReadOnlyConverter<TValue, out TReadOnlyProxy> : IJsonConverter<TValue>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue>
	{
		/// <summary>Wraps the specified JSON value into a read-only proxy</summary>
		/// <param name="value">Underlying JSON value</param>
		/// <returns>Proxy that exposes the value using typed properties.</returns>
		TReadOnlyProxy ToReadOnly(JsonValue value);

		/// <summary>Wraps the specified JSON value into a read-only proxy</summary>
		/// <param name="context">Context that will record all access to the read-only proxy</param>
		/// <param name="value">Underlying JSON value</param>
		/// <returns>Proxy that exposes the value using typed properties.</returns>
		TReadOnlyProxy ToReadOnly(IObservableJsonContext context, JsonValue value);

		/// <summary>Wraps a <typeparamref name="TValue"/> instance with a read-only proxy</summary>
		/// <param name="instance">Instance that will be converted to JSON</param>
		/// <returns>Proxy that exposes the equivalent JSON value, using typed properties.</returns>
		TReadOnlyProxy ToReadOnly(TValue instance);

		/// <summary>Wraps a <typeparamref name="TValue"/> instance with a read-only proxy</summary>
		/// <param name="context">Context that will record all access to the read-only proxy</param>
		/// <param name="instance">Instance that will be converted to JSON</param>
		/// <returns>Proxy that exposes the equivalent JSON value, using typed properties.</returns>
		TReadOnlyProxy ToReadOnly(IObservableJsonContext context, TValue instance);

	}

	/// <summary>Bundle interface that is implemented by source-generated encoders for writable proxies.</summary>
	/// <typeparam name="TValue">Beacon type for the exposed proxy</typeparam>
	/// <typeparam name="TWritableProxy">Type of the generated writable proxy that mimics the properties on type <typeparamref name="TValue"/></typeparam>
	[PublicAPI]
	public interface IJsonWritableConverter<TValue, out TWritableProxy> : IJsonConverter<TValue>
		where TWritableProxy : IJsonWritableProxy<TValue>
	{
		//REVIEW: rename ToMutable() to ToWritable() ?

		/// <summary>Wraps the specified JSON value into a writable proxy</summary>
		/// <param name="value">Underlying JSON value</param>
		/// <returns>Proxy that exposes the value using typed properties.</returns>
		TWritableProxy ToMutable(JsonValue value);

		/// <summary>Wraps the specified JSON value into a writable proxy</summary>
		/// <param name="context">Context that will record all access to the writable proxy</param>
		/// <param name="value">Underlying JSON value</param>
		/// <returns>Proxy that exposes the value using typed properties.</returns>
		TWritableProxy ToMutable(IMutableJsonContext context, JsonValue value);

		/// <summary>Wraps a <typeparamref name="TValue"/> instance with a writable proxy</summary>
		/// <param name="instance">Instance that will be converted to JSON</param>
		/// <returns>Proxy that exposes the equivalent JSON value, using typed properties.</returns>
		TWritableProxy ToMutable(TValue instance);

		/// <summary>Wraps a <typeparamref name="TValue"/> instance with a writable proxy</summary>
		/// <param name="context">Context that will record all access to the writable proxy</param>
		/// <param name="instance">Instance that will be converted to JSON</param>
		/// <returns>Proxy that exposes the equivalent JSON value, using typed properties.</returns>
		TWritableProxy ToMutable(IMutableJsonContext context, TValue instance);

	}

	/// <summary>Bundle interface that is implemented by source-generated encoders, with support for read-only and writable proxies.</summary>
	/// <typeparam name="TValue">Beacon type for the exposed proxy</typeparam>
	/// <typeparam name="TReadOnlyProxy">Type of the generated read-only proxy that mimics the properties on type <typeparamref name="TValue"/></typeparam>
	/// <typeparam name="TWritableProxy">Type of the generated writable proxy that mimics the properties on type <typeparamref name="TValue"/></typeparam>
	[PublicAPI]
	public interface IJsonConverter<TValue, out TReadOnlyProxy, out TWritableProxy> :
		IJsonReadOnlyConverter<TValue, TReadOnlyProxy>,
		IJsonWritableConverter<TValue, TWritableProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue>
		where TWritableProxy : IJsonWritableProxy<TValue>
	{
		// this is just a marker interface
	}
#endif

}
