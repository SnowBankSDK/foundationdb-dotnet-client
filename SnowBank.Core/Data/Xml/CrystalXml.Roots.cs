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
	using System.Buffers;
	using System.Text;
	using System.Xml;
	using System.Xml.Linq;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Text;

	public static partial class CrystalXml
	{

		// The entry points for documents whose root is not a generated contract type: a bare sequence of contract items
		// (composed out of the item type's ICrystalXmlElementSerializer<T> facet), and a bare scalar. Both write the output
		// the reference DataContractSerializer writes for the same declared type, as pinned by the root facts of
		// DcsNamespaceReferenceFacts: a sequence root is ArrayOfX in the item's namespace, a scalar root is the xsd
		// lexical name in the Serialization namespace. A root name is resolved, never guessed: the caller's rootName
		// wins, the profile's own convention is the fallback, and a profile with neither raises a typed error.

		/// <summary>The <c>i:nil</c> attribute name, for the roots this file writes itself</summary>
		private static readonly CrystalXmlName XmlNilName = CrystalXmlName.Create("nil", CrystalXmlNamespaces.XmlSchemaInstanceUri);

		#region Collection roots (ICrystalXmlElementSerializer<T> composition)...

		/// <summary>Serializes a sequence of <paramref name="items"/> to a <see cref="string"/> of XML text, as a single collection root element</summary>
		/// <typeparam name="T">Type of the items being serialized</typeparam>
		/// <param name="itemSerializer">Serializer of the item type, which names the item elements and, on the DataContract profile, the root</param>
		/// <param name="items">Items to serialize, or <see langword="null"/> for the empty root element, marked nil when the item serializer's profile marks nulls</param>
		/// <param name="settings">Optional settings passed through to <paramref name="itemSerializer"/></param>
		/// <param name="rootName">Optional name for the root element, in place of the profile's <c>ArrayOfX</c> convention; required on the General profile, which has no convention</param>
		/// <param name="itemName">Optional name for the item elements, in place of the item type's own element name</param>
		/// <exception cref="CrystalXmlRootNameException">If <paramref name="rootName"/> is <see langword="null"/> and the item serializer declares no <see cref="ICrystalXmlElementSerializer{T}.CollectionRootName"/></exception>
		public static string ToText<T>(ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null)
		{
			Contract.NotNull(itemSerializer);
			var sink = new ValueStringWriter();
			var emitter = new CrystalXmlWriter<char, ValueStringWriter>(ref sink, settings: settings ?? CrystalXmlSettings.General);
			try
			{
				WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
				return emitter.Writer.ToStringAndDispose();
			}
			catch
			{
				// the serializer threw before ToStringAndDispose(): return the pooled buffer instead of leaking it
				emitter.Writer.Dispose();
				throw;
			}
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> to a <see cref="Slice"/> of XML, as a single collection root element</summary>
		/// <param name="encoding">Encoding of the returned bytes, defaulting to UTF-8 with no byte-order mark. The writer
		/// always produces UTF-8 internally; a non-default encoding transcodes the finished buffer once, so the UTF-8 path
		/// stays untouched. When <see cref="CrystalXmlSettings.WriteXmlDeclaration"/> is set, the declaration names this
		/// encoding.</param>
		/// <inheritdoc cref="ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
		public static Slice ToSlice<T>(ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null, Encoding? encoding = null)
		{
			Contract.NotNull(itemSerializer);
			bool isUtf8 = IsDefaultOrUtf8(encoding);
			var sink = new SliceWriter();
			var emitter = new CrystalXmlWriter<byte, SliceWriter>(ref sink, settings: settings ?? CrystalXmlSettings.General, declarationEncoding: isUtf8 ? null : encoding!.WebName);
			WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
			var slice = emitter.Writer.ToSlice();
			return isUtf8 ? slice : TranscodeFromUtf8(slice, encoding!);
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> to a <see cref="byte"/> array of XML, as a single collection root element</summary>
		/// <inheritdoc cref="ToSlice{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?,Encoding?)"/>
		public static byte[] ToBytes<T>(ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null, Encoding? encoding = null)
			=> ToSlice(itemSerializer, items, settings, rootName, itemName, encoding).ToArray();

		/// <summary>Serializes a sequence of <paramref name="items"/> as XML into <paramref name="destination"/>, as a single collection root element</summary>
		/// <param name="destination">Destination stream; ownership stays with the caller, who is responsible for flushing and disposing it</param>
		/// <remarks>Same buffering and failure-path contract as <see cref="WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>.
		/// A non-default <paramref name="encoding"/> takes the simpler
		/// <see cref="ToSlice{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?,Encoding?)"/>
		/// path instead, building the whole buffer before writing it out in one call.</remarks>
		/// <inheritdoc cref="ToSlice{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?,Encoding?)"/>
		public static void WriteTo<T>(Stream destination, ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null, Encoding? encoding = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(itemSerializer);

			if (!IsDefaultOrUtf8(encoding))
			{
				var slice = ToSlice(itemSerializer, items, settings, rootName, itemName, encoding);
				destination.Write(slice.Array, slice.Offset, slice.Count);
				return;
			}

			var sink = new StreamBufferProxy(destination);
			var emitter = new CrystalXmlWriter<byte, StreamBufferProxy>(ref sink, settings: settings ?? CrystalXmlSettings.General);
			try
			{
				WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
				emitter.Writer.Drain();
			}
			catch
			{
				emitter.Writer.Abandon();
				throw;
			}
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> as XML text into <paramref name="destination"/>, as a single collection root element</summary>
		/// <param name="destination">Destination writer; ownership stays with the caller, who is responsible for flushing and disposing it</param>
		/// <remarks>Same buffering and failure-path contract as <see cref="WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>.</remarks>
		/// <inheritdoc cref="ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
		public static void WriteTo<T>(TextWriter destination, ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(itemSerializer);
			var sink = new TextWriterBufferProxy(destination);
			var emitter = new CrystalXmlWriter<char, TextWriterBufferProxy>(ref sink, settings: settings ?? CrystalXmlSettings.General);
			try
			{
				WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
				emitter.Writer.Drain();
			}
			catch
			{
				emitter.Writer.Abandon();
				throw;
			}
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> as UTF-8 encoded XML into <paramref name="destination"/>, as a single collection root element</summary>
		/// <param name="destination">Destination buffer writer</param>
		/// <remarks>Same ownership contract as <see cref="WriteTo{T}(IBufferWriter{byte},ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>: this overload owns no pooled resource of its own.</remarks>
		/// <inheritdoc cref="ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
		public static void WriteTo<T>(IBufferWriter<byte> destination, ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(itemSerializer);
			var sink = new BufferWriterProxy<byte>(destination);
			var emitter = new CrystalXmlWriter<byte, BufferWriterProxy<byte>>(ref sink, settings: settings ?? CrystalXmlSettings.General);
			WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> to an in-memory <see cref="XDocument"/>, as a single collection root element</summary>
		/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here, not a byte-exact document: see
		/// the remarks on <see cref="CrystalXDocumentEmitter"/>.</remarks>
		/// <inheritdoc cref="ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
		public static XDocument ToXDocument<T>(ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null)
		{
			Contract.NotNull(itemSerializer);
			var emitter = new CrystalXDocumentEmitter();
			WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
			return emitter.ToDocument();
		}

		/// <summary>Serializes a sequence of <paramref name="items"/> into <paramref name="destination"/>, as a single collection root element</summary>
		/// <param name="destination">Destination writer, not owned: the caller flushes and disposes it, and configures its <see cref="XmlWriterSettings"/></param>
		/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here: see the remarks on <see cref="CrystalXmlWriterEmitter"/>.</remarks>
		/// <inheritdoc cref="ToText{T}(ICrystalXmlElementSerializer{T},IEnumerable{T},CrystalXmlSettings?,string?,string?)"/>
		public static void WriteTo<T>(XmlWriter destination, ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings = null, string? rootName = null, string? itemName = null)
		{
			Contract.NotNull(destination);
			Contract.NotNull(itemSerializer);
			var emitter = new CrystalXmlWriterEmitter(destination);
			WriteCollectionRoot(ref emitter, itemSerializer, items, settings, rootName, itemName);
		}

		/// <summary>Writes a sequence of items as one collection root element, one item element per item</summary>
		private static void WriteCollectionRoot<TEmitter, T>(ref TEmitter emitter, ICrystalXmlElementSerializer<T> itemSerializer, IEnumerable<T>? items, CrystalXmlSettings? settings, string? rootName, string? itemName)
			where TEmitter : struct, ICrystalXmlEmitter
		{
			var elementName = itemSerializer.ElementName;
			var ns = elementName.Namespace;

			// the caller's name wins, the profile's ArrayOfX convention is the fallback, and a name is never guessed;
			// either way the root stays in the namespace the item contract lives in (the name changes, the namespace does not)
			string? name = rootName ?? itemSerializer.CollectionRootName;
			if (name is null)
			{
				throw new CrystalXmlRootNameException(typeof(T), $"No name for a collection root of '{typeof(T).Name}' items: this profile has no root-name convention, and the caller passed no rootName.");
			}
			var rootElement = CrystalXmlName.Create(name, ns.Text);

			if (items is null)
			{
				if (default(T) is null)
				{ // the item serializer owns its profile's null policy: nil on the DataContract profile, a plain empty element on General
					itemSerializer.WriteXmlElement(ref emitter, in rootElement, default, settings, 0);
				}
				else
				{ // a value-type default is not null, so the null SEQUENCE is written here: empty, marked nil when the settings ask for null members
					emitter.WriteStartElement(in rootElement);
					if (settings.IncludesNullMembers())
					{
						emitter.WriteAttribute(in XmlNilName, "true");
					}
					emitter.WriteEndElement(in rootElement);
				}
				return;
			}

			var itemElement = itemName is null ? elementName : CrystalXmlName.Create(itemName, ns.Text);

			emitter.WriteStartElement(in rootElement);
			if (!ns.IsNone)
			{ // the items below live in this namespace: declared once here, so no item declares it again
				emitter.WriteNamespaceDeclaration(in ns);
			}
			foreach (var item in items)
			{
				itemSerializer.WriteXmlElement(ref emitter, in itemElement, item, settings, 1);
			}
			emitter.WriteEndElement(in rootElement);
		}

		#endregion

		#region Scalar roots...

		/// <summary>Entry points for a document whose root is a bare scalar (a string, a number, a date, ...)</summary>
		/// <remarks>Nested rather than overloaded on <see cref="CrystalXml"/> itself: a generic method taking a bare
		/// <c>T?</c> value would be a catch-all that captures every call the serializer overloads do not, and a mistyped
		/// argument must fail to compile rather than fail at write time.</remarks>
		[PublicAPI]
		public static class Scalar
		{

			/// <summary>Serializes a scalar <paramref name="value"/> to a <see cref="string"/> of XML text, as a single root element</summary>
			/// <typeparam name="T">Type of the value being serialized: one of the xsd lexical scalar types (<see cref="string"/>, the numbers, <see cref="bool"/>, <see cref="DateTime"/>, <see cref="TimeSpan"/>, <see cref="Guid"/>, <see cref="char"/>, <c>byte[]</c>, <see cref="Uri"/>)</typeparam>
			/// <param name="value">Value to serialize, or <see langword="null"/> for the empty root element, marked nil when the settings ask for null members</param>
			/// <param name="settings">Optional settings; the reference DataContract behavior is the default</param>
			/// <param name="rootName">Optional name for the root element, in place of the type's xsd lexical name; the root stays in the Serialization namespace either way</param>
			/// <remarks>These entry points write the output the reference <c>DataContractSerializer</c> writes for the same declared
			/// type: the xsd lexical name in the built-in Serialization namespace, as pinned by the root facts of
			/// <c>DcsNamespaceReferenceFacts</c>. A type outside the lexical set has no scalar output and is refused.</remarks>
			/// <exception cref="CrystalXmlUnknownTypeException">If <typeparamref name="T"/> is not one of the lexical scalar types</exception>
			public static string ToText<T>(T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			{
				var sink = new ValueStringWriter();
				var emitter = new CrystalXmlWriter<char, ValueStringWriter>(ref sink, settings: settings ?? CrystalXmlSettings.General);
				try
				{
					WriteScalarRoot(ref emitter, value, settings, rootName);
					return emitter.Writer.ToStringAndDispose();
				}
				catch
				{
					// the write threw before ToStringAndDispose(): return the pooled buffer instead of leaking it
					emitter.Writer.Dispose();
					throw;
				}
			}

			/// <summary>Serializes a scalar <paramref name="value"/> to a <see cref="Slice"/> of XML, as a single root element</summary>
			/// <param name="encoding">Encoding of the returned bytes, defaulting to UTF-8 with no byte-order mark. The writer
			/// always produces UTF-8 internally; a non-default encoding transcodes the finished buffer once, so the UTF-8 path
			/// stays untouched. When <see cref="CrystalXmlSettings.WriteXmlDeclaration"/> is set, the declaration names this
			/// encoding.</param>
			/// <inheritdoc cref="ToText{T}(T,CrystalXmlSettings?,string?)"/>
			public static Slice ToSlice<T>(T? value, CrystalXmlSettings? settings = null, string? rootName = null, Encoding? encoding = null)
			{
				bool isUtf8 = IsDefaultOrUtf8(encoding);
				var sink = new SliceWriter();
				var emitter = new CrystalXmlWriter<byte, SliceWriter>(ref sink, settings: settings ?? CrystalXmlSettings.General, declarationEncoding: isUtf8 ? null : encoding!.WebName);
				WriteScalarRoot(ref emitter, value, settings, rootName);
				var slice = emitter.Writer.ToSlice();
				return isUtf8 ? slice : TranscodeFromUtf8(slice, encoding!);
			}

			/// <summary>Serializes a scalar <paramref name="value"/> to a <see cref="byte"/> array of XML, as a single root element</summary>
			/// <inheritdoc cref="ToSlice{T}(T,CrystalXmlSettings?,string?,Encoding?)"/>
			public static byte[] ToBytes<T>(T? value, CrystalXmlSettings? settings = null, string? rootName = null, Encoding? encoding = null)
				=> ToSlice(value, settings, rootName, encoding).ToArray();

			/// <summary>Serializes a scalar <paramref name="value"/> as XML into <paramref name="destination"/>, as a single root element</summary>
			/// <param name="destination">Destination stream; ownership stays with the caller, who is responsible for flushing and disposing it</param>
			/// <remarks>Same buffering and failure-path contract as <see cref="WriteTo{T}(Stream,ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>.
			/// A non-default <paramref name="encoding"/> takes the simpler <see cref="ToSlice{T}(T,CrystalXmlSettings?,string?,Encoding?)"/>
			/// path instead, building the whole buffer before writing it out in one call.</remarks>
			/// <inheritdoc cref="ToSlice{T}(T,CrystalXmlSettings?,string?,Encoding?)"/>
			public static void WriteTo<T>(Stream destination, T? value, CrystalXmlSettings? settings = null, string? rootName = null, Encoding? encoding = null)
			{
				Contract.NotNull(destination);

				if (!IsDefaultOrUtf8(encoding))
				{
					var slice = ToSlice(value, settings, rootName, encoding);
					destination.Write(slice.Array, slice.Offset, slice.Count);
					return;
				}

				var sink = new StreamBufferProxy(destination);
				var emitter = new CrystalXmlWriter<byte, StreamBufferProxy>(ref sink, settings: settings ?? CrystalXmlSettings.General);
				try
				{
					WriteScalarRoot(ref emitter, value, settings, rootName);
					emitter.Writer.Drain();
				}
				catch
				{
					emitter.Writer.Abandon();
					throw;
				}
			}

			/// <summary>Serializes a scalar <paramref name="value"/> as XML text into <paramref name="destination"/>, as a single root element</summary>
			/// <param name="destination">Destination writer; ownership stays with the caller, who is responsible for flushing and disposing it</param>
			/// <remarks>Same buffering and failure-path contract as <see cref="WriteTo{T}(TextWriter,ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>.</remarks>
			/// <inheritdoc cref="ToText{T}(T,CrystalXmlSettings?,string?)"/>
			public static void WriteTo<T>(TextWriter destination, T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			{
				Contract.NotNull(destination);
				var sink = new TextWriterBufferProxy(destination);
				var emitter = new CrystalXmlWriter<char, TextWriterBufferProxy>(ref sink, settings: settings ?? CrystalXmlSettings.General);
				try
				{
					WriteScalarRoot(ref emitter, value, settings, rootName);
					emitter.Writer.Drain();
				}
				catch
				{
					emitter.Writer.Abandon();
					throw;
				}
			}

			/// <summary>Serializes a scalar <paramref name="value"/> as UTF-8 encoded XML into <paramref name="destination"/>, as a single root element</summary>
			/// <param name="destination">Destination buffer writer</param>
			/// <remarks>Same ownership contract as <see cref="WriteTo{T}(IBufferWriter{byte},ICrystalXmlSerializer{T},T,CrystalXmlSettings?,string?)"/>: this overload owns no pooled resource of its own.</remarks>
			/// <inheritdoc cref="ToText{T}(T,CrystalXmlSettings?,string?)"/>
			public static void WriteTo<T>(IBufferWriter<byte> destination, T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			{
				Contract.NotNull(destination);
				var sink = new BufferWriterProxy<byte>(destination);
				var emitter = new CrystalXmlWriter<byte, BufferWriterProxy<byte>>(ref sink, settings: settings ?? CrystalXmlSettings.General);
				WriteScalarRoot(ref emitter, value, settings, rootName);
			}

			/// <summary>Serializes a scalar <paramref name="value"/> to an in-memory <see cref="XDocument"/>, as a single root element</summary>
			/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here, not a byte-exact document: see
			/// the remarks on <see cref="CrystalXDocumentEmitter"/>.</remarks>
			/// <inheritdoc cref="ToText{T}(T,CrystalXmlSettings?,string?)"/>
			public static XDocument ToXDocument<T>(T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			{
				var emitter = new CrystalXDocumentEmitter();
				WriteScalarRoot(ref emitter, value, settings, rootName);
				return emitter.ToDocument();
			}

			/// <summary>Serializes a scalar <paramref name="value"/> into <paramref name="destination"/>, as a single root element</summary>
			/// <param name="destination">Destination writer, not owned: the caller flushes and disposes it, and configures its <see cref="XmlWriterSettings"/></param>
			/// <remarks>Only infoset equivalence with the byte-exact output is guaranteed here: see the remarks on <see cref="CrystalXmlWriterEmitter"/>.</remarks>
			/// <inheritdoc cref="ToText{T}(T,CrystalXmlSettings?,string?)"/>
			public static void WriteTo<T>(XmlWriter destination, T? value, CrystalXmlSettings? settings = null, string? rootName = null)
			{
				Contract.NotNull(destination);
				var emitter = new CrystalXmlWriterEmitter(destination);
				WriteScalarRoot(ref emitter, value, settings, rootName);
			}

			/// <summary>Writes one scalar value as the root element of a document</summary>
			private static void WriteScalarRoot<TEmitter, T>(ref TEmitter emitter, T? value, CrystalXmlSettings? settings, string? rootName)
				where TEmitter : struct, ICrystalXmlEmitter
			{
				string? lexicalName = GetLexicalRootName<T>();
				if (lexicalName is null)
				{
					throw new CrystalXmlUnknownTypeException(typeof(T), $"Type '{typeof(T).Name}' is not one of the xsd lexical scalar types, so it has no scalar root output. A contract type roots a document through its own serializer.");
				}

				// this is the reference DataContract output, so its defaults apply: a null root is marked nil unless the settings drop null members
				settings ??= CrystalXmlSettings.DataContractCompat;

				// the caller names the root element, not the shape: the name changes and the Serialization namespace does not
				var rootElement = CrystalXmlName.Create(rootName ?? lexicalName, CrystalXmlNamespaces.SerializationUri);

				if (value is null)
				{
					emitter.WriteStartElement(in rootElement);
					if (settings.IncludesNullMembers())
					{
						emitter.WriteAttribute(in XmlNilName, "true");
					}
					emitter.WriteEndElement(in rootElement);
					return;
				}

				emitter.WriteStartElement(in rootElement);
				WriteScalarRootContent(ref emitter, value);
				emitter.WriteEndElement(in rootElement);
			}

			/// <summary>Returns the xsd lexical name of a scalar type, or <see langword="null"/> when the type is not a lexical scalar</summary>
			private static string? GetLexicalRootName<T>()
			{
				var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
				if (type == typeof(string)) return "string";
				if (type == typeof(bool)) return "boolean";
				if (type == typeof(int)) return "int";
				if (type == typeof(long)) return "long";
				if (type == typeof(short)) return "short";
				// the xsd names of the byte-sized pair are crossed: sbyte is the signed xsd "byte", byte is "unsignedByte"
				if (type == typeof(sbyte)) return "byte";
				if (type == typeof(byte)) return "unsignedByte";
				if (type == typeof(ushort)) return "unsignedShort";
				if (type == typeof(uint)) return "unsignedInt";
				if (type == typeof(ulong)) return "unsignedLong";
				if (type == typeof(float)) return "float";
				if (type == typeof(double)) return "double";
				if (type == typeof(decimal)) return "decimal";
				if (type == typeof(DateTime)) return "dateTime";
				if (type == typeof(TimeSpan)) return "duration";
				if (type == typeof(Guid)) return "guid";
				if (type == typeof(char)) return "char";
				if (type == typeof(byte[])) return "base64Binary";
				if (type == typeof(Uri)) return "anyURI";
				return null;
			}

			/// <summary>Writes the text content of a non-null scalar root, in its DataContract lexical form</summary>
			private static void WriteScalarRootContent<TEmitter, T>(ref TEmitter emitter, T value)
				where TEmitter : struct, ICrystalXmlEmitter
			{
				switch (value)
				{
					case string text: emitter.WriteText(text); break;
					case bool x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatBoolean(x)); break;
					case int x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatInt32(x)); break;
					case long x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatInt64(x)); break;
					case short x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatInt16(x)); break;
					case sbyte x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatSByte(x)); break;
					case byte x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatByte(x)); break;
					case ushort x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatUInt16(x)); break;
					case uint x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatUInt32(x)); break;
					case ulong x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatUInt64(x)); break;
					case float x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatSingle(x)); break;
					case double x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatDouble(x)); break;
					case decimal x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatDecimal(x)); break;
					case DateTime x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatDateTime(x)); break;
					case TimeSpan x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatDuration(x)); break;
					case Guid x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatGuid(x)); break;
					case char x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatDcsChar(x)); break;
					case byte[] x: emitter.WriteRawAscii(CrystalXmlFormatters.FormatBase64(x)); break;
					case Uri x: emitter.WriteText(CrystalXmlFormatters.FormatUri(x)); break;
					default: throw new CrystalXmlUnknownTypeException(value!.GetType());
				}
			}

		}

		#endregion

	}

}
