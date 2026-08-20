// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// Smoke test for the zoo toolchain: one trivial case and one that exercises the
// non-equivalence pair the rubric refuses to absorb. If these two behave, the
// discovery-by-convention, the invariant-culture setup and the two build legs all work.

namespace Acme.Zoo.Cases.SmokeMinimal
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	[DataContract]
	public class MinimalDto
	{
		[DataMember(Name = "id")]
		public string Id { get; set; }

		[DataMember(Name = "count")]
		public int Count { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "smoke-minimal"; } }

		public static Type RootType { get { return typeof(MinimalDto); } }

		public static object Create()
		{
			return new MinimalDto { Id = "ord-001", Count = 7 };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.SmokeNullVersusEmpty
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>The four states the equivalence rubric deliberately does not treat as
	/// interchangeable: null string vs empty string, and null collection vs empty
	/// collection. Everything else about this DTO is uninteresting on purpose.</summary>
	[DataContract]
	public class NullVersusEmptyDto
	{
		[DataMember(Name = "nullString")]
		public string NullString { get; set; }

		[DataMember(Name = "emptyString")]
		public string EmptyString { get; set; }

		[DataMember(Name = "nullList")]
		public List<string> NullList { get; set; }

		[DataMember(Name = "emptyList")]
		public List<string> EmptyList { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "null-versus-empty"; } }

		public static Type RootType { get { return typeof(NullVersusEmptyDto); } }

		public static object Create()
		{
			return new NullVersusEmptyDto
			{
				NullString = null,
				EmptyString = "",
				NullList = null,
				EmptyList = new List<string>()
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		/// <summary>Shapes that exist at rest, so the replacement must still read them.
		/// Hand-written, never copied from real data.</summary>
		public static string[] LegacyDocuments
		{
			get
			{
				return new[]
				{
					"{\"nullString\":null,\"emptyString\":\"\",\"nullList\":null,\"emptyList\":[]}",
					"{\"emptyString\":\"\"}"
				};
			}
		}
	}
}
