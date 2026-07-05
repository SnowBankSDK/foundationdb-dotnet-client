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

	/// <summary>Wraps a <see cref="JsonValue"/> into a typed mutable proxy</summary>
	[PublicAPI]
	public interface IJsonWritableProxy : IJsonSerializable, IJsonPackable
	{

		/// <summary>Returns the underlying mutable value</summary>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get();

		/// <summary>Returns the value of the field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get(string name);

		/// <summary>Returns the value of the field with the specified name</summary>
		/// <param name="name">Name of the field</param>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get(ReadOnlyMemory<char> name);

		/// <summary>Returns the value of the element at the specified location</summary>
		/// <param name="index">Index of the item</param>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get(int index);

		/// <summary>Returns the value of the element at the specified location</summary>
		/// <param name="index">Index of the item</param>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get(Index index);

		/// <summary>Returns the value of the child at the specified path</summary>
		/// <param name="path">Path to the child</param>
		/// <remarks>This value can be used to "escape" the type safety of the proxy, while still allowing for tracking of mutations.</remarks>
		MutableJsonValue Get(JsonPath path);

		/// <summary>Returns the proxied JSON Value</summary>
		/// <remarks>
		/// <para>The returned value may not be mutable and should only not be changed directly.</para>
		/// <para>Any mutations made to this value will not be captured by the attached context!</para>
		/// </remarks>
		JsonValue ToJsonValue();

		/// <summary>Returns the (optional) tracking context attached to this instance</summary>
		/// <remarks>If <c>non-null</c>, this context will record all changed made to this instance, or any of its children</remarks>
		IMutableJsonContext? GetContext();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonWritableProxy<out TValue> : IJsonWritableProxy
	{

		/// <summary>Returns an instance of <typeparamref name="TValue"/> with the same content as this proxy.</summary>
		public TValue ToValue();

	}

	/// <summary>Wraps a <see cref="JsonValue"/> into typed mutable proxy that emulates the type <typeparamref name="TValue"/></summary>
	/// <typeparam name="TValue">Emulated data type</typeparam>
	/// <typeparam name="TProxy">CRTP for the type that implements this interface</typeparam>
	/// <remarks>
	/// <para>This interface is a marker for "wrapper types" that replicate the same set of properties and fields as <typeparamref name="TValue"/>, using a wrapped <see cref="JsonValue"/> as source.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonWritableProxy<TValue, out TProxy> : IJsonWritableProxy<TValue>
		where TProxy : IJsonWritableProxy<TValue, TProxy>
	{

		/// <summary>Wraps a JSON Value into a mutable proxy for type <typeparamref name="TValue"/></summary>
		static abstract TProxy Create(MutableJsonValue obj, IJsonConverter<TValue>? converter = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into mutable proxy</summary>
		static abstract TProxy Create(JsonValue value);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into mutable proxy</summary>
		static abstract TProxy Create(IMutableJsonContext tr, JsonValue value);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into mutable proxy</summary>
		static abstract TProxy Create(TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Wraps an instance type <typeparamref name="TValue"/> into mutable proxy</summary>
		static abstract TProxy Create(IMutableJsonContext tr, TValue value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null);

		/// <summary>Returns the <see cref="IJsonConverter{TValue}"/> used by proxies of this type</summary>
		static abstract IJsonConverter<TValue> Converter { get; }

	}

}

#endif
