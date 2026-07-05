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

	/// <summary>Base class for custom or source generated implementations of a <see cref="IJsonWritableProxy"/></summary>
	/// <remarks>This contains all the boilerplate implementation that is common to most custom implementations</remarks>
	public abstract record JsonWritableProxyObjectBase
		: IJsonWritableProxy, IJsonProxyNode, IEquatable<JsonValue>, IEquatable<MutableJsonValue>
	{

		/// <summary>Wrapped JSON Object</summary>
		protected readonly MutableJsonValue m_value;

		protected JsonWritableProxyObjectBase(MutableJsonValue value)
		{
			m_value = value;
		}

		/// <inheritdoc />
		IJsonProxyNode? IJsonProxyNode.Parent => m_value.Parent;

		/// <inheritdoc />
		JsonPathSegment IJsonProxyNode.Segment => m_value.Segment;

		/// <inheritdoc />
		int IJsonProxyNode.Depth => m_value.Depth;

		/// <inheritdoc />
		JsonType IJsonProxyNode.Type => m_value.Json.Type;

		/// <inheritdoc />
		void IJsonProxyNode.WritePath(ref JsonPathBuilder builder) => m_value.WritePath(ref builder);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableJsonValue Get() => m_value;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableJsonValue Get(string name) => m_value.Get(name);
		
		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MutableJsonValue Get(ReadOnlyMemory<char> name) => m_value.Get(name);

		/// <inheritdoc />
		MutableJsonValue IJsonWritableProxy.Get(int index) => m_value.Get(index);

		/// <inheritdoc />
		MutableJsonValue IJsonWritableProxy.Get(Index index) => m_value.Get(index);

		/// <inheritdoc />
		MutableJsonValue IJsonWritableProxy.Get(JsonPath path) => m_value.Get(path);

		public MutableJsonValue this[string key]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_value.Get(key);
		}

		public MutableJsonValue this[ReadOnlyMemory<char> key]
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => m_value.Get(key);
		}

		/// <summary>Tests if the object is present.</summary>
		/// <returns><c>false</c> if the wrapped JSON value is null or empty; otherwise, <c>true</c>.</returns>
		public bool Exists() => m_value.Exists();

		/// <summary>Tests if the object is null or missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null or missing; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrMissing() => m_value.IsNullOrMissing();

		/// <summary>Tests if the object is null, missing, or empty.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is null, missing or an empty object; otherwise, <c>false</c>.</returns>
		/// <remarks>This can return <c>false</c> if the wrapped value is an empty object, or another type, like an array, string literal, etc...</remarks>
		public bool IsNullOrEmpty() => m_value.Json switch { JsonArray arr =>  arr.Count != 0, JsonNull => true, _ => false };

		/// <summary>Tests if the wrapped value is a valid JSON Object.</summary>
		/// <returns><c>true</c> if the wrapped JSON value is a non-null Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObject() => m_value.Json is JsonObject;

		/// <summary>Tests if the wrapped value is a valid JSON Object, or is null-or-missing.</summary>
		/// <returns><c>true</c> if the wrapped JSON value either null-or-missing, or an Object; otherwise, <c>false</c></returns>
		/// <remarks>This can be used to protect against malformed JSON document that would have a different type (array, string literal, ...).</remarks>
		public bool IsObjectOrMissing() => m_value.Json is (JsonObject or JsonNull);

		/// <summary>Changes the value of this object</summary>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(MutableJsonValue value) => m_value.Set(value);

		/// <summary>Changes the value of this object</summary>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(JsonValue value) => m_value.Set(value);

		/// <summary>Sets or changes the value of the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(string name, JsonValue value) => m_value.Set(name, value);

		/// <summary>Sets or changes the value of the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(ReadOnlyMemory<char> name, JsonValue value) => m_value.Set(name, value);

		/// <summary>Sets or changes the value of the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(string name, MutableJsonValue value) => m_value.Set(name, value);

		/// <summary>Sets or changes the value of the field with the given name</summary>
		/// <param name="name">Name of the field</param>
		/// <param name="value">New value that replaces the content of this object</param>
		public void Set(ReadOnlyMemory<char> name, MutableJsonValue value) => m_value.Set(name, value);

		/// <inheritdoc />
		public void JsonSerialize(CrystalJsonWriter writer) => m_value.Json.JsonSerialize(writer);

		/// <inheritdoc />
		public JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => m_value.Json;

		/// <inheritdoc />
		public JsonValue ToJsonValue() => m_value.Json;

		/// <inheritdoc />
		public IMutableJsonContext? GetContext() => m_value.Context;

		/// <inheritdoc />
		public JsonPath GetPath() => m_value.GetPath();

		/// <inheritdoc />
		public JsonPath GetPath(JsonPathSegment child) => m_value.GetPath(child);

		/// <inheritdoc />
		public bool Equals(JsonValue? value) => m_value.Equals(value);

		/// <inheritdoc />
		public bool Equals(MutableJsonValue? value) => m_value.Equals(value);

		/// <inheritdoc />
		public override string ToString() => m_value.ToString();

	}

}

#endif
