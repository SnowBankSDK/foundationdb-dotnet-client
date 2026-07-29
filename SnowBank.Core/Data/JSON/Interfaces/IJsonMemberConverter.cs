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

	/// <summary>Convenience bundle for a symmetric custom converter attached to a member (or a type) via <see cref="JsonConvertWithAttribute"/> or <c>[JsonConverter(typeof(...))]</c></summary>
	/// <typeparam name="T">Type of the converted values</typeparam>
	/// <remarks>
	/// <para>Converter recognition is per facet: a named converter is honored for whichever of <see cref="IJsonPacker{T}"/> (value to JSON)
	/// and <see cref="IJsonDeserializer{T}"/> (JSON to value) it implements. A type that is only ever written (or only ever read) can
	/// implement a single facet; any attempt to use the missing direction fails with an exception naming the facet to implement.
	/// This interface is the convenience bundle for the common symmetric case, never a requirement.</para>
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

	/// <summary>Bridges a typed converter's facet(s) to the boxed calls of the reflection path, centralizing the null handling</summary>
	/// <remarks>Recognition is per facet: either side may be absent, in which case any attempt to use that direction fails with an exception naming the facet to implement.</remarks>
	internal sealed class JsonMemberConverterBridge<T> : IJsonMemberConverterBridge
	{

		private Type ConverterType { get; }

		private IJsonPacker<T>? Packer { get; }

		private IJsonDeserializer<T>? Deserializer { get; }

		public JsonMemberConverterBridge(Type converterType, IJsonPacker<T>? packer, IJsonDeserializer<T>? deserializer)
		{
			Contract.NotNull(converterType);
			Contract.Requires(packer != null || deserializer != null);
			this.ConverterType = converterType;
			this.Packer = packer;
			this.Deserializer = deserializer;
		}

		public Type TargetType => typeof(T);

		public JsonValue PackBoxed(object? value, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (value is null)
			{ // never invokes the converter (this also lifts a converter for T over a T? member: a boxed T? is either null or a boxed T)
				return JsonNull.Null;
			}
			if (this.Packer == null)
			{
				return JsonSerializerExtensions.FailConverterMissingPackerFacet(this.ConverterType, typeof(T));
			}
			return this.Packer.Pack((T) value, settings, resolver);
		}

		public object? UnpackBoxed(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			if (value.IsNullOrMissing())
			{ // never invokes the converter
				return null;
			}
			if (this.Deserializer == null)
			{
				return JsonSerializerExtensions.FailConverterMissingDeserializerFacet<T>(this.ConverterType);
			}
			return this.Deserializer.Unpack(value, resolver);
		}

	}

}
