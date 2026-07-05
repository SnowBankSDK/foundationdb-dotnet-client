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

#if NET7_0_OR_GREATER // needs runtime support for static abstract members in interfaces

namespace SnowBank.Data.Json
{

	/// <summary>Wraps a <see cref="JsonValue"/> into a typed read-only proxy</summary>
	[PublicAPI]
	public interface IJsonReadOnlyProxy : IJsonSerializable, IJsonPackable, IEquatable<JsonValue>, IEquatable<ObservableJsonValue>
	{

		/// <summary>Returns the underlying observable value</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get();

		/// <summary>Returns the value of the field with the specified name</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get(string name);

		/// <summary>Returns the value of the field with the specified name</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get(ReadOnlyMemory<char> name);

		/// <summary>Returns the value of the element at the specified location</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get(int index);

		/// <summary>Returns the value of the element at the specified location</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get(Index index);

		/// <summary>Returns the value of the child at the specified path</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of read access.</remarks>
		ObservableJsonValue Get(JsonPath path);

		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>
		/// <para>The returned value is read-only and cannot be changed.</para>
		/// <para>The value will be marked as having been accessed by value.</para>
		/// </remarks>
		public JsonValue ToJsonValue();

		/// <summary>Returns the (optional) tracking context attached to this instance</summary>
		/// <remarks>If <c>non-null</c>, this context will record all read access made to this instance, or any of its children</remarks>
		IObservableJsonContext? GetContext();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<out TValue> : IJsonReadOnlyProxy
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TProxy">CRTP for the type that implements this interface</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<TValue, out TProxy> : IJsonReadOnlyProxy<TValue>
		where TProxy : IJsonReadOnlyProxy<TValue, TProxy>
	{

		/// <summary>Wraps a JSON Value into a read-only proxy for type <typeparamref name="TValue"/></summary>
		static abstract TProxy Create(ObservableJsonValue value, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into read-only proxy</summary>
		static abstract TProxy Create(JsonValue value, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into read-only proxy</summary>
		static abstract TProxy Create(IObservableJsonContext ctx, JsonValue value, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into read-only proxy</summary>
		static abstract TProxy Create(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into read-only proxy</summary>
		static abstract TProxy Create(IObservableJsonContext ctx, TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Returns the <see cref="IJsonConverter{TValue}"/> used by proxies of this type</summary>
		static abstract IJsonConverter<TValue> Converter { get; }

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed read-only proxy that emulates the type <typeparamref name="TValue"/> with support for copy-on-write.</summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TReadOnlyProxy">CRTP for the type that implements this interface</typeparam>
	/// <typeparam name="TWritableProxy">CRTP for the corresponding <see cref="IJsonWritableProxy{TValue,TWritableProxy}"/></typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonReadOnlyProxy<TValue, out TReadOnlyProxy, out TWritableProxy> : IJsonReadOnlyProxy<TValue, TReadOnlyProxy>
		where TReadOnlyProxy : IJsonReadOnlyProxy<TValue, TReadOnlyProxy, TWritableProxy>
		where TWritableProxy : IJsonWritableProxy<TValue, TWritableProxy>
	{

		/// <summary>Returns a mutable proxy that is able to mutate a <i>copy</i> of the wrapped JSON value</summary>
		public TWritableProxy ToMutable();

		/// <summary>Returns an updated read-only version of this proxy, after applying a set of mutations.</summary>
		/// <param name="modifier">Handler that is passed a mutable copy of the object, which will be frozen into a new read-only object after the handler returns.</param>
		/// <returns>Updated read-only instance</returns>
		/// <remarks>
		/// <para>The original read-only object will NOT be modified.</para>
		/// </remarks>
		public TReadOnlyProxy With(Action<TWritableProxy> modifier);

	}

}

#endif
