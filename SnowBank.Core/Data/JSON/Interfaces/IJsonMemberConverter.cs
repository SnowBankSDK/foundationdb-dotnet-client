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

	/// <summary>Custom converter that can be attached to a member (or a type) via <c>[JsonConverter(typeof(...))]</c></summary>
	/// <typeparam name="T">Type of the converted values</typeparam>
	/// <remarks>
	/// <para>This is the smallest contract a custom converter has to fulfill: <see cref="IJsonPacker{T}.Pack"/> (value to JSON)
	/// and <see cref="IJsonDeserializer{T}.Unpack"/> (JSON to value). Both the System.Text.Json and the Newtonsoft spellings of
	/// <c>[JsonConverter(typeof(...))]</c> are recognized, on a member or on a type, matched by attribute name so that no
	/// package reference is required.</para>
	/// <para>Implementations never see <c>null</c>: the serialization pipeline handles null and missing values (and the lifting
	/// of a converter written for <c>T</c> over a <c>T?</c> member) before invoking the converter.</para>
	/// <para>A source-generated <see cref="IJsonConverter{T}"/> also satisfies this contract (it implements both parents), so a
	/// generated whole-type converter can be named on a member without any adapter.</para>
	/// </remarks>
	[PublicAPI]
	public interface IJsonMemberConverter<T> : IJsonPacker<T>, IJsonDeserializer<T>
	{
		// this is just a bundle: Pack + Unpack is the whole contract
	}

	/// <summary>Non-generic bridge used by the reflection path to invoke a <see cref="IJsonMemberConverter{T}"/> on boxed values</summary>
	internal interface IJsonMemberConverterBridge
	{

		/// <summary>Type of the values handled by the wrapped converter</summary>
		Type TargetType { get; }

		/// <summary>Converts a (boxed) value into JSON; <c>null</c> maps to <see cref="JsonNull.Null"/> without invoking the converter</summary>
		JsonValue PackBoxed(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver);

		/// <summary>Converts JSON back into a (boxed) value; null or missing maps to <c>null</c> without invoking the converter</summary>
		object? UnpackBoxed(JsonValue value, ICrystalJsonTypeResolver? resolver);

	}

	/// <summary>Bridges a typed packer/deserializer pair to the boxed calls of the reflection path, centralizing the null handling</summary>
	internal sealed class JsonMemberConverterBridge<T> : IJsonMemberConverterBridge
	{

		private IJsonPacker<T> Packer { get; }

		private IJsonDeserializer<T> Deserializer { get; }

		public JsonMemberConverterBridge(IJsonPacker<T> packer, IJsonDeserializer<T> deserializer)
		{
			Contract.NotNull(packer);
			Contract.NotNull(deserializer);
			this.Packer = packer;
			this.Deserializer = deserializer;
		}

		public Type TargetType => typeof(T);

		public JsonValue PackBoxed(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			// note: a boxed T? is either null or a boxed T, so this also lifts a converter for T over a T? member
			return value is null ? JsonNull.Null : this.Packer.Pack((T) value, settings, resolver);
		}

		public object? UnpackBoxed(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			return value.IsNullOrMissing() ? null : this.Deserializer.Unpack(value, resolver);
		}

	}

}
