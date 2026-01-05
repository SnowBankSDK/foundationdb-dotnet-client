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

	/// <summary>Type that can serialize instances of <typeparamref name="T"/> into JSON</summary>
	/// <typeparam name="T">Type of the values to serialize</typeparam>
	/// <remarks>Types can serialize themselves, but this interface can also be used on "helper types" that are separate (manually written, or via source code generation)</remarks>
	[PublicAPI]
	public interface IJsonSerializer<in T>
	{

		/// <summary>Writes a JSON representation of a value to the specified output</summary>
		/// <param name="writer">Writer that will output the JSON in the desired format</param>
		/// <param name="instance">Instance to serialize</param>
		/// <exception cref="JsonSerializationException">If an error occured during the serialization</exception>
		void Serialize(CrystalJsonWriter writer, T? instance);

	}

	internal sealed class DefaultJsonSerializer<T> : IJsonSerializer<T>
	{

		public Action<CrystalJsonWriter, T?> Handler { get; }

		public DefaultJsonSerializer(Action<CrystalJsonWriter, T?>? handler)
		{
			this.Handler = handler ?? (static (_, _) => throw new NotSupportedException("Operation not supported"));
		}

		/// <inheritdoc />
		public void Serialize(CrystalJsonWriter writer, T? instance) => this.Handler(writer, instance);

	}


}
