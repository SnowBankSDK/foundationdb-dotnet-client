// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// Category: member-attributes. The highest-frequency material in the corpus.
// Scan-derived weights (occurrences on [DataMember] in DataContract-bearing product files):
//   Name= 5644 of 13883 (41%) · EmitDefaultValue=false 1062 (7.6%) · Order= 510 (3.7%)
//   IsRequired=true 11 (rare but real) · [IgnoreDataMember] 198 · non-public members 765
//   fields (rather than properties) 148.

namespace Acme.Zoo.Cases.MemberNameRename
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>The single most common shape in the application: every member renamed to a
	/// short lowercase wire name. Note the wire order is alphabetical on the RENAMED name,
	/// not declaration order.</summary>
	[DataContract]
	public class RenamedMemberDto
	{
		[DataMember(Name = "zulu")]
		public string DeclaredFirst { get; set; }

		[DataMember(Name = "alpha")]
		public string DeclaredSecond { get; set; }

		[DataMember]
		public string NotRenamed { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-name-rename"; } }
		public static Type RootType { get { return typeof(RenamedMemberDto); } }

		public static object Create()
		{
			return new RenamedMemberDto
			{
				DeclaredFirst = "first",
				DeclaredSecond = "second",
				NotRenamed = "third"
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberEmitDefaultFalse
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>EmitDefaultValue=false across every category of default, next to members
	/// that keep the DEFAULT setting (true) for contrast. This pair is what makes the
	/// omitted-versus-explicit-null difference visible in one document.</summary>
	[DataContract]
	public class EmitDefaultDto
	{
		[DataMember(Name = "keptInt")]
		public int KeptInt { get; set; }

		[DataMember(Name = "droppedInt", EmitDefaultValue = false)]
		public int DroppedInt { get; set; }

		[DataMember(Name = "keptString")]
		public string KeptString { get; set; }

		[DataMember(Name = "droppedString", EmitDefaultValue = false)]
		public string DroppedString { get; set; }

		[DataMember(Name = "keptBool")]
		public bool KeptBool { get; set; }

		[DataMember(Name = "droppedBool", EmitDefaultValue = false)]
		public bool DroppedBool { get; set; }

		[DataMember(Name = "keptList")]
		public List<string> KeptList { get; set; }

		[DataMember(Name = "droppedList", EmitDefaultValue = false)]
		public List<string> DroppedList { get; set; }

		[DataMember(Name = "keptNullableInt")]
		public int? KeptNullableInt { get; set; }

		[DataMember(Name = "droppedNullableInt", EmitDefaultValue = false)]
		public int? DroppedNullableInt { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-emit-default-false"; } }
		public static Type RootType { get { return typeof(EmitDefaultDto); } }

		/// <summary>Everything left at its default value, so the contrast is total:
		/// the "kept" members appear, the "dropped" members do not.</summary>
		public static object Create()
		{
			return new EmitDefaultDto();
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberOrder
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Order= mixed with unordered members. DCJS sorts unordered members first
	/// (alphabetically), then ordered ones by Order, ties broken alphabetically.</summary>
	[DataContract]
	public class OrderedDto
	{
		[DataMember(Name = "third", Order = 3)]
		public string Third { get; set; }

		[DataMember(Name = "first", Order = 1)]
		public string First { get; set; }

		[DataMember(Name = "sameOrderB", Order = 2)]
		public string SameOrderB { get; set; }

		[DataMember(Name = "sameOrderA", Order = 2)]
		public string SameOrderA { get; set; }

		[DataMember(Name = "unordered")]
		public string Unordered { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-order"; } }
		public static Type RootType { get { return typeof(OrderedDto); } }

		public static object Create()
		{
			return new OrderedDto
			{
				Third = "c",
				First = "a",
				SameOrderB = "bb",
				SameOrderA = "ba",
				Unordered = "u"
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberOrderAcrossInheritance
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Member ordering across a contract hierarchy: DCJS emits base-type members
	/// before derived-type members regardless of name or Order.</summary>
	[DataContract]
	public class BaseContract
	{
		[DataMember(Name = "zBaseMember")]
		public string ZBaseMember { get; set; }
	}

	[DataContract]
	public class DerivedContract : BaseContract
	{
		[DataMember(Name = "aDerivedMember")]
		public string ADerivedMember { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-order-across-inheritance"; } }
		public static Type RootType { get { return typeof(DerivedContract); } }

		public static object Create()
		{
			return new DerivedContract { ZBaseMember = "base", ADerivedMember = "derived" };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberIsRequired
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>IsRequired=true. Rare in the application (11 occurrences) but kept because
	/// its READ behaviour is the interesting part: a document missing the member throws
	/// rather than defaulting. The second input pins that.</summary>
	[DataContract]
	public class RequiredMemberDto
	{
		[DataMember(Name = "mandatory", IsRequired = true)]
		public string Mandatory { get; set; }

		[DataMember(Name = "optional")]
		public string Optional { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-is-required"; } }
		public static Type RootType { get { return typeof(RequiredMemberDto); } }

		public static object Create()
		{
			return new RequiredMemberDto { Mandatory = "present", Optional = null };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		public static string[] LegacyDocuments
		{
			get
			{
				return new[]
				{
					"{\"mandatory\":\"present\",\"optional\":\"o\"}",
					// mandatory absent: expected to FAIL on read. The recorded error is the witness.
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.MemberIgnoreDataMember
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>[IgnoreDataMember] on a type that is otherwise opt-out (no [DataContract]),
	/// which is the only configuration where the attribute does anything: on a
	/// [DataContract] type, members are opt-IN and the attribute is redundant.</summary>
	public class IgnoredMemberDto
	{
		public string Included { get; set; }

		[IgnoreDataMember]
		public string Excluded { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "member-ignore-data-member"; } }
		public static Type RootType { get { return typeof(IgnoredMemberDto); } }

		public static object Create()
		{
			return new IgnoredMemberDto { Included = "in", Excluded = "out" };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberNonPublic
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Non-public members carrying [DataMember] DO serialize. 765 occurrences in
	/// the application, so this is not an exotic shape. Visibility must be preserved when
	/// anonymizing or the case stops testing anything.</summary>
	[DataContract]
	public class NonPublicMemberDto
	{
		[DataMember(Name = "privateProp")]
		private string PrivateProp { get; set; }

		[DataMember(Name = "internalProp")]
		internal string InternalProp { get; set; }

		[DataMember(Name = "protectedProp")]
		protected string ProtectedProp { get; set; }

		[DataMember(Name = "publicProp")]
		public string PublicProp { get; set; }

		public static NonPublicMemberDto Build()
		{
			return new NonPublicMemberDto
			{
				PrivateProp = "priv",
				InternalProp = "int",
				ProtectedProp = "prot",
				PublicProp = "pub"
			};
		}
	}

	public static class Sample
	{
		public static string Id { get { return "member-non-public"; } }
		public static Type RootType { get { return typeof(NonPublicMemberDto); } }

		public static object Create()
		{
			return NonPublicMemberDto.Build();
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.MemberFieldsVersusProperties
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Fields carrying [DataMember], including a readonly one, next to properties.
	/// 148 field occurrences in the application.</summary>
	[DataContract]
	public class FieldMemberDto
	{
		[DataMember(Name = "publicField")]
		public string PublicField;

		[DataMember(Name = "privateField")]
		private int privateField;

		[DataMember(Name = "readonlyField")]
		public readonly string ReadonlyField;

		[DataMember(Name = "property")]
		public string Property { get; set; }

		public FieldMemberDto()
		{
		}

		public static FieldMemberDto Build()
		{
			var dto = new FieldMemberDto { PublicField = "pf", Property = "p" };
			dto.privateField = 42;
			return dto;
		}
	}

	public static class Sample
	{
		public static string Id { get { return "member-fields-versus-properties"; } }
		public static Type RootType { get { return typeof(FieldMemberDto); } }

		public static object Create()
		{
			return FieldMemberDto.Build();
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.PocoNoDataContract
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization.Json;

	/// <summary>The opt-out world: no [DataContract] and no [DataMember] anywhere. DCJS
	/// serializes all public read/write members. Getter-only members are NOT emitted,
	/// which is the trap worth pinning.</summary>
	public class PlainPocoDto
	{
		public string Name { get; set; }

		public int Count { get; set; }

		public string GetterOnly { get { return "computed"; } }

		public List<string> Items { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "poco-no-data-contract"; } }
		public static Type RootType { get { return typeof(PlainPocoDto); } }

		public static object Create()
		{
			return new PlainPocoDto
			{
				Name = "poco",
				Count = 3,
				Items = new List<string> { "a", "b" }
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}
