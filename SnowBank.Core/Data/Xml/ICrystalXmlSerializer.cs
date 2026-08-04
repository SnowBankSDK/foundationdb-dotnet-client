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

namespace SnowBank.Data.Xml
{
	using SnowBank.Data.Json;

	/// <summary>Serializes instances of <typeparamref name="T"/> to XML, through any <see cref="IXmlEmitter"/></summary>
	/// <typeparam name="T">Type of the values that this serializer can write</typeparam>
	/// <remarks>
	/// <para>Implemented by the source-generated holder types (mirroring the JSON generator's per-type converters), and
	/// callable by hand for types that do not go through the generator. The <c>where TEmitter : struct, IXmlEmitter</c>
	/// constraint on <see cref="WriteXml{TEmitter}"/> is what lets the same body target the byte-exact wire writer or
	/// either infoset emitter without any virtual dispatch: the emitter type is only known at the call site, never
	/// boxed to the interface.</para>
	/// <para>The five public entry points in <see cref="CrystalXml"/> (<see cref="CrystalXml.ToText{T}"/>,
	/// <see cref="CrystalXml.ToSlice{T}"/>, and so on) are the only callers most code needs: each owns the whole sink
	/// lifecycle (construct the sink, construct the emitter over it, invoke <see cref="WriteXml{TEmitter}"/>, read the
	/// result back), so nothing outside those helpers ever touches an emitter's writer directly.</para>
	/// </remarks>
	[PublicAPI]
	public interface ICrystalXmlSerializer<T>
	{

		/// <summary>Writes <paramref name="value"/> to <paramref name="emitter"/></summary>
		/// <typeparam name="TEmitter">Concrete emitter type, reached through the <see cref="IXmlEmitter"/> constraint so every call devirtualizes</typeparam>
		/// <param name="emitter">Destination emitter, passed by <see langword="ref"/> per the <see cref="IXmlEmitter"/> contract</param>
		/// <param name="value">Value to write, or <see langword="null"/> to write the empty/self-closing form of the root element</param>
		/// <param name="settings">Optional settings controlling the output (for example, which lexical profile - <c>Dcs</c> or <c>Modern</c> - scalar members use)</param>
		/// <param name="rootName">Optional override for the name of the root element written by this call, in place of the type's own default name</param>
		/// <remarks>The generated emission's cycle/depth guard (<see cref="CrystalXml.MaxDepth"/>) cannot see across this call: the depth counter resets to zero on the other side of it. A reference cycle that runs through this method's own recursion is not caught by that guard and overflows the native stack instead.</remarks>
		void WriteXml<TEmitter>(ref TEmitter emitter, T? value, CrystalJsonSettings? settings = null, string? rootName = null)
			where TEmitter : struct, IXmlEmitter;

	}

	/// <summary>Instance hook letting a type write its own XML representation</summary>
	/// <remarks>Named with the <c>Crystal</c> prefix, rather than <c>IXmlSerializable</c>, to avoid any collision with
	/// <see cref="System.Xml.Serialization.IXmlSerializable"/>: that BCL interface is built around <c>XmlReader</c>/
	/// <c>XmlWriter</c> and an <c>XmlSchema</c> callback, an entirely different shape from the <see cref="IXmlEmitter"/>
	/// vocabulary this type is written against.</remarks>
	[PublicAPI]
	public interface ICrystalXmlSerializable
	{

		/// <summary>Writes this instance to <paramref name="emitter"/></summary>
		/// <typeparam name="TEmitter">Concrete emitter type, reached through the <see cref="IXmlEmitter"/> constraint so every call devirtualizes</typeparam>
		/// <param name="emitter">Destination emitter, passed by <see langword="ref"/> per the <see cref="IXmlEmitter"/> contract</param>
		/// <remarks>The generated emission's cycle/depth guard (<see cref="CrystalXml.MaxDepth"/>) cannot see across this call: the depth counter resets to zero on the other side of it. A reference cycle that runs through this method's own recursion is not caught by that guard and overflows the native stack instead.</remarks>
		void WriteXml<TEmitter>(ref TEmitter emitter)
			where TEmitter : struct, IXmlEmitter;

	}

}
