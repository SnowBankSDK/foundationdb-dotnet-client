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

	/// <summary>Serializes instances of <typeparamref name="T"/> to XML, through any <see cref="ICrystalXmlEmitter"/></summary>
	/// <typeparam name="T">Type of the values that this serializer can write</typeparam>
	/// <remarks>
	/// <para>Implemented by the source-generated holder types (mirroring the JSON generator's per-type converters), and
	/// callable by hand for types that do not go through the generator. The <c>where TEmitter : struct, ICrystalXmlEmitter</c>
	/// constraint on <see cref="WriteXml{TEmitter}"/> is what lets the same body target the byte-exact output writer or
	/// either infoset emitter without any virtual dispatch: the emitter type is only known at the call site, never
	/// boxed to the interface.</para>
	/// <para>The eight public entry points in <see cref="CrystalXml"/> (<see cref="CrystalXml.ToText{T}"/>,
	/// <see cref="CrystalXml.ToSlice{T}"/>, and so on) are the only callers most code needs: each owns the whole sink
	/// lifecycle (construct the sink, construct the emitter over it, invoke <see cref="WriteXml{TEmitter}"/>, read the
	/// result back), so nothing outside those helpers ever touches an emitter's writer directly.</para>
	/// </remarks>
	[PublicAPI]
	public interface ICrystalXmlSerializer<T>
	{

		/// <summary>Writes <paramref name="value"/> to <paramref name="emitter"/></summary>
		/// <typeparam name="TEmitter">Concrete emitter type, reached through the <see cref="ICrystalXmlEmitter"/> constraint so every call devirtualizes</typeparam>
		/// <param name="emitter">Destination emitter, passed by <see langword="ref"/> per the <see cref="ICrystalXmlEmitter"/> contract</param>
		/// <param name="value">Value to write, or <see langword="null"/> to write the empty/self-closing form of the root element</param>
		/// <param name="settings">Optional settings controlling the output (for example, which lexical profile - <c>Dcs</c> or <c>General</c> - scalar members use)</param>
		/// <param name="rootName">Optional override for the name of the root element written by this call, in place of the type's own default name</param>
		/// <remarks>The depth counter of the generated cycle guard resets to zero across this call: see <see cref="CrystalXml.MaxDepth"/>.</remarks>
		void WriteXml<TEmitter>(ref TEmitter emitter, T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			where TEmitter : struct, ICrystalXmlEmitter;

	}

	/// <summary>Serializes instances of <typeparamref name="T"/> as a named element inside a larger document, and names itself</summary>
	/// <typeparam name="T">Type of the values that this serializer can write</typeparam>
	/// <remarks>
	/// <para>The composition surface of a serializer: <see cref="ICrystalXmlSerializer{T}.WriteXml{TEmitter}"/> writes a whole
	/// document, while <see cref="WriteXmlElement{TEmitter}"/> writes one element a caller has already named, at a depth the
	/// caller states. The generated per-type serializers implement this interface; the collection root entry points of
	/// <see cref="CrystalXml"/> (<see cref="CrystalXml.ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
	/// and its seven siblings) compose a document out of it, one element per item.</para>
	/// <para>The two names let a caller compose without guessing: <see cref="ElementName"/> is what this type calls itself,
	/// and <see cref="CollectionRootName"/> is what a bare sequence of it is called, when the profile has such a convention.</para>
	/// </remarks>
	[PublicAPI]
	public interface ICrystalXmlElementSerializer<T> : ICrystalXmlSerializer<T>
	{

		/// <summary>Element name this type writes when the caller does not name it</summary>
		/// <remarks>The contract name, carrying the contract namespace on the DataContract profile and no namespace on the
		/// General one. This is the name a bare item of this type takes inside a collection root.</remarks>
		CrystalXmlName ElementName { get; }

		/// <summary>Name of the root element of a bare sequence of this type, or <see langword="null"/> when there is none</summary>
		/// <remarks>The DataContract profile names such a root by its <c>ArrayOfX</c> convention, in the namespace of
		/// <see cref="ElementName"/>. The General profile has no convention: a collection root requires an explicit name from
		/// the caller, and a <see langword="null"/> here is what makes the entry points refuse to guess one
		/// (<see cref="CrystalXmlRootNameException"/>).</remarks>
		string? CollectionRootName { get; }

		/// <summary>Writes <paramref name="value"/> as an element named <paramref name="name"/></summary>
		/// <typeparam name="TEmitter">Concrete emitter type, reached through the <see cref="ICrystalXmlEmitter"/> constraint so every call devirtualizes</typeparam>
		/// <param name="emitter">Destination emitter, passed by <see langword="ref"/> per the <see cref="ICrystalXmlEmitter"/> contract</param>
		/// <param name="name">Name of the element to write; the caller owns the name, this serializer owns the content</param>
		/// <param name="value">Value to write, or <see langword="null"/> to write the empty element, marked nil when the settings ask for null members</param>
		/// <param name="settings">Optional settings controlling the output, passed through unchanged to nested serializers</param>
		/// <param name="depth">Number of elements already open above this one, counted against <see cref="CrystalXml.MaxDepth"/></param>
		void WriteXmlElement<TEmitter>(ref TEmitter emitter, in CrystalXmlName name, T? value, CrystalXmlSettings? settings, int depth = 0)
			where TEmitter : struct, ICrystalXmlEmitter;

	}

	/// <summary>Instance hook letting a type write its own XML representation</summary>
	/// <remarks>Named with the <c>Crystal</c> prefix, rather than <c>IXmlSerializable</c>, to avoid any collision with
	/// <see cref="System.Xml.Serialization.IXmlSerializable"/>: that BCL interface is built around <c>XmlReader</c>/
	/// <c>XmlWriter</c> and an <c>XmlSchema</c> callback, an entirely different shape from the <see cref="ICrystalXmlEmitter"/>
	/// events this type is written against.</remarks>
	[PublicAPI]
	public interface ICrystalXmlSerializable
	{

		/// <summary>Writes this instance to <paramref name="emitter"/></summary>
		/// <typeparam name="TEmitter">Concrete emitter type, reached through the <see cref="ICrystalXmlEmitter"/> constraint so every call devirtualizes</typeparam>
		/// <param name="emitter">Destination emitter, passed by <see langword="ref"/> per the <see cref="ICrystalXmlEmitter"/> contract</param>
		/// <remarks>The depth counter of the generated cycle guard resets to zero across this call: see <see cref="CrystalXml.MaxDepth"/>.</remarks>
		void WriteXml<TEmitter>(ref TEmitter emitter)
			where TEmitter : struct, ICrystalXmlEmitter;

	}

}
