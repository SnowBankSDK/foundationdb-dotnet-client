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
	using System.Runtime.Serialization;
	using SnowBank.Runtime;

	/// <summary>Error thrown when a name cannot be used as an XML element or attribute name (not a valid NCName)</summary>
	/// <remarks>Raised for an explicit <see cref="XmlPropertyAttribute.Name"/> that is not a valid XML name, and at write time for a dictionary key that is not a valid NCName under <see cref="XmlDictionaryFormat.Direct"/>.</remarks>
	[Serializable]
	public sealed class CrystalXmlInvalidNameException : InvalidOperationException
	{

		/// <summary>Offending name</summary>
		public string Name { get; }

		/// <summary>Reports that <paramref name="name"/> cannot be used as an XML name</summary>
		public CrystalXmlInvalidNameException(string name)
			: base($"'{name}' is not a valid XML name.")
		{
			this.Name = name;
		}

		/// <summary>Reports that <paramref name="name"/> cannot be used as an XML name, with a custom explanation</summary>
		public CrystalXmlInvalidNameException(string name, string message)
			: base(message)
		{
			this.Name = name;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		private CrystalXmlInvalidNameException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Name = info.GetString(nameof(this.Name)) ?? string.Empty;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(nameof(this.Name), this.Name);
		}

	}

	/// <summary>Error thrown when the runtime type of a value is not part of the XML serialization graph known at generation time</summary>
	/// <remarks>Mirrors the check the JSON generator already performs for polymorphic members: a concrete type encountered at write time that was not declared (ex: via <c>[CrystalJsonSerializable(typeof(...))]</c> or a <c>DerivedTypes</c> declaration) has no generated <c>WriteXml</c> body to call into.</remarks>
	[Serializable]
	public sealed class CrystalXmlUnknownTypeException : InvalidOperationException
	{

		/// <summary>Runtime type that is not part of the known XML serialization graph</summary>
		public Type Type { get; }

		/// <summary>Reports that <paramref name="type"/> is not part of the known XML serialization graph</summary>
		public CrystalXmlUnknownTypeException(Type type)
			: base($"Type '{type.GetFriendlyName()}' is not part of the known XML serialization graph for this container.")
		{
			this.Type = type;
		}

		/// <summary>Reports that <paramref name="type"/> is not part of the known XML serialization graph, with a custom explanation</summary>
		public CrystalXmlUnknownTypeException(Type type, string message)
			: base(message)
		{
			this.Type = type;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		private CrystalXmlUnknownTypeException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Type = (Type) info.GetValue(nameof(this.Type), typeof(Type))!;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(nameof(this.Type), this.Type);
		}

	}

	/// <summary>Error thrown when a reference cycle is detected while serializing an object graph to XML</summary>
	/// <remarks>Unlike <see cref="System.Runtime.Serialization.DataContractSerializer"/>, which duplicates a shared, non-cyclic graph in its entirety on the compat wire, a genuine cycle has no representation in either XML profile: it is always a typed error, never <c>z:Id</c>/<c>z:Ref</c> references.</remarks>
	[Serializable]
	public sealed class CrystalXmlCycleException : InvalidOperationException
	{

		/// <summary>Type of the instance at which the reference cycle was detected</summary>
		public Type Type { get; }

		/// <summary>Reports that a reference cycle was detected while serializing an instance of <paramref name="type"/></summary>
		public CrystalXmlCycleException(Type type)
			: base($"A reference cycle was detected while serializing an instance of type '{type.GetFriendlyName()}' to XML.")
		{
			this.Type = type;
		}

		/// <summary>Reports that a reference cycle was detected while serializing an instance of <paramref name="type"/>, with a custom explanation</summary>
		public CrystalXmlCycleException(Type type, string message)
			: base(message)
		{
			this.Type = type;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		private CrystalXmlCycleException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Type = (Type) info.GetValue(nameof(this.Type), typeof(Type))!;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(nameof(this.Type), this.Type);
		}

	}

	/// <summary>Error thrown when a type or construct has no supported XML representation</summary>
	/// <remarks>Covers the constructs that are deliberately never inferred silently (ex: a naked nesting of collections, an unsupported member shape): the generator either rejects them at compile time with a CXML diagnostic, or, when only knowable at runtime, this exception is raised instead of guessing an output.</remarks>
	[Serializable]
	public sealed class CrystalXmlNotSupportedException : InvalidOperationException
	{

		/// <summary>Type or construct that is not supported by the XML output generator</summary>
		public Type Type { get; }

		/// <summary>Reports that <paramref name="type"/> is not supported by the XML output generator</summary>
		public CrystalXmlNotSupportedException(Type type)
			: base($"Type '{type.GetFriendlyName()}' is not supported by the XML output generator.")
		{
			this.Type = type;
		}

		/// <summary>Reports that <paramref name="type"/> is not supported by the XML output generator, with a custom explanation</summary>
		public CrystalXmlNotSupportedException(Type type, string message)
			: base(message)
		{
			this.Type = type;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		private CrystalXmlNotSupportedException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Type = (Type) info.GetValue(nameof(this.Type), typeof(Type))!;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(nameof(this.Type), this.Type);
		}

	}

}
