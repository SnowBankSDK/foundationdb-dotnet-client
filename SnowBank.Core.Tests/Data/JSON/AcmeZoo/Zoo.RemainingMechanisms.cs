// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// The two DataContract mechanisms the rest of the corpus did not represent. Both are rare,
// and both were measured rather than assumed:
//   IsReference=true : exactly 1 occurrence in the whole source tree
//   ISerializable on a type that also carries [DataContract] : zero types (7 files carry both
//     mechanisms on two distinct types each: property-set system, directory user, security
//     identifier, portal history)
// Rare does not mean safe: each produces an output shape nothing else in the corpus produces.

namespace Acme.Zoo.Cases.ContractIsReference
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>IsReference=true makes DCJS emit object identity in the output: the first
	/// occurrence carries an id, later occurrences become a back-reference instead of a repeated
	/// copy. That turns a shared reference into an output-level construct, and it means the document
	/// cannot be understood one member at a time.
	/// <para>One occurrence in the application, so the question for the replacement is not
	/// "reproduce this" but "what did the author intend": either genuine shared identity that
	/// must survive a round trip, or merely an attempt to avoid duplicating a large object. The
	/// two have very different answers, and only the code around the single site can say which.</para></summary>
	[DataContract(IsReference = true)]
	public class SharedNodeDto
	{
		[DataMember(Name = "label")]
		public string Label { get; set; }
	}

	[DataContract]
	public class GraphDto
	{
		[DataMember(Name = "first")]
		public SharedNodeDto First { get; set; }

		/// <summary>Deliberately the same instance as First.</summary>
		[DataMember(Name = "second")]
		public SharedNodeDto Second { get; set; }

		[DataMember(Name = "list")]
		public List<SharedNodeDto> List { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "contract-is-reference"; } }
		public static Type RootType { get { return typeof(GraphDto); } }

		public static object Create()
		{
			var shared = new SharedNodeDto { Label = "shared" };
			return new GraphDto
			{
				First = shared,
				Second = shared,
				List = new List<SharedNodeDto> { shared, shared },
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.SerializableWithDataContract
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;
	using System.Security.Permissions;

	/// <summary>A type that implements ISerializable and carries [DataContract]. Seven files in
	/// the application do this, including the property-set system, so it is not an accident of
	/// one class.
	/// <para>What this case pins is which contract wins. The two describe different shapes:
	/// GetObjectData writes the names it chooses, the [DataMember] declarations say something
	/// else. Only one of them reaches the output, and knowing which one tells the replacement
	/// whether the hand-written ISerializable code is load-bearing or dead weight that can be
	/// dropped. The members are deliberately given different names in the two mechanisms so the
	/// answer is unambiguous in the captured output.</para></summary>
	[Serializable]
	[DataContract]
	public class DualContractDto : ISerializable
	{
		[DataMember(Name = "fromDataMember")]
		public string Value { get; set; }

		[DataMember(Name = "countFromDataMember")]
		public int Count { get; set; }

		public DualContractDto()
		{
		}

		/// <summary>The deserialization constructor ISerializable requires.</summary>
		protected DualContractDto(SerializationInfo info, StreamingContext context)
		{
			this.Value = info.GetString("fromGetObjectData");
			this.Count = info.GetInt32("countFromGetObjectData");
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue("fromGetObjectData", this.Value);
			info.AddValue("countFromGetObjectData", this.Count);
		}
	}

	public static class Sample
	{
		public static string Id { get { return "serializable-with-data-contract"; } }
		public static Type RootType { get { return typeof(DualContractDto); } }

		public static object Create()
		{
			return new DualContractDto { Value = "which-wins", Count = 7 };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}
