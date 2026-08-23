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


	/// <summary>Error thrown when the runtime type of a value is not part of the XML serialization graph known at generation time</summary>
	/// <remarks>Mirrors the check the JSON generator already performs for polymorphic members: a concrete type encountered at write time that was not declared (ex: via <c>[CrystalSerializable(typeof(...))]</c> or a <c>DerivedTypes</c> declaration) has no generated <c>WriteXml</c> body to call into.</remarks>
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
	/// <remarks>A genuine cycle has no representation in either XML profile (never <c>z:Id</c>/<c>z:Ref</c> references), so it is always a typed error. The guard cannot tell a cycle from a graph nested deeper than <see cref="CrystalXml.MaxDepth"/>: both raise this exception.</remarks>
	[Serializable]
	public sealed class CrystalXmlCycleException : InvalidOperationException
	{

		/// <summary>Type of the instance at which the reference cycle was detected</summary>
		public Type Type { get; }

		/// <summary>Reports that a reference cycle was detected while serializing an instance of <paramref name="type"/></summary>
		public CrystalXmlCycleException(Type type)
			: base($"Cannot write an instance of type '{type.GetFriendlyName()}' to XML: the object graph either contains a reference cycle or is nested deeper than this serializer supports.")
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

	/// <summary>Error thrown when a root element has no name: the serializer declares no default, and the caller passed none</summary>
	/// <remarks>A name is never guessed. The General profile writes no default name for a collection root, so a caller on that
	/// profile passes an explicit <c>rootName</c>; the DataContract profile falls back to its <c>ArrayOfX</c> convention when
	/// the item contract can express one.</remarks>
	[Serializable]
	public sealed class CrystalXmlRootNameException : InvalidOperationException
	{

		/// <summary>Type for which no root element name could be resolved</summary>
		public Type Type { get; }

		/// <summary>Reports that no root element name could be resolved for a document rooted in <paramref name="type"/></summary>
		public CrystalXmlRootNameException(Type type)
			: base($"No name for a root element of type '{type.GetFriendlyName()}': the serializer declares no default root name, and the caller passed none. Pass an explicit rootName.")
		{
			this.Type = type;
		}

		/// <summary>Reports that no root element name could be resolved for a document rooted in <paramref name="type"/>, with a custom explanation</summary>
		public CrystalXmlRootNameException(Type type, string message)
			: base(message)
		{
			this.Type = type;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
		private CrystalXmlRootNameException(SerializationInfo info, StreamingContext context)
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
