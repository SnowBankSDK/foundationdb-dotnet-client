// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// A landmine found while building the corpus, kept as its own case because it makes the
// serializer throw and would otherwise mask every other member of the date case.

namespace Acme.Zoo.Cases.DateTimeMinValueLandmine
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>DCJS cannot serialize <c>DateTime.MinValue</c> when its kind is Unspecified or
	/// Local and the machine sits at a positive UTC offset: converting to UTC pushes the value
	/// below <c>DateTime.MinValue</c> and it throws
	/// <c>SerializationException</c> wrapping <c>ArgumentOutOfRangeException</c>.
	/// <para>Why this matters rather than being a curiosity: <c>DateTime.MinValue</c> is the
	/// default value of an unset <c>DateTime</c> member, the application has 343 DateTime
	/// members, and <c>EmitDefaultValue</c> defaults to true. So any DTO with an
	/// unassigned non-nullable DateTime throws on serialization anywhere east of Greenwich,
	/// and works in London. The mitigations visible in the wild are exactly the two members
	/// below: mark the member <c>EmitDefaultValue = false</c>, or make it nullable.</para>
	/// <para>Behaviour confirmed identical on .NET Framework 4.7.2 and .NET 10.</para></summary>
	[DataContract]
	public class MinValueDto
	{
		[DataMember(Name = "unspecifiedMin")]
		public DateTime UnspecifiedMin { get; set; }
	}

	/// <summary>The same shape, mitigated the two ways the application really uses.</summary>
	[DataContract]
	public class MinValueMitigatedDto
	{
		[DataMember(Name = "droppedWhenDefault", EmitDefaultValue = false)]
		public DateTime DroppedWhenDefault { get; set; }

		[DataMember(Name = "nullableInstead")]
		public DateTime? NullableInstead { get; set; }

		[DataMember(Name = "utcMinIsFine")]
		public DateTime UtcMinIsFine { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "datetime-minvalue-landmine"; } }
		public static Type RootType { get { return typeof(MinValueDto); } }

		/// <summary>Expected to throw on serialization. The recorded error is the witness.</summary>
		public static object Create()
		{
			return new MinValueDto { UnspecifiedMin = DateTime.MinValue };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.DateTimeMinValueMitigated
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>The mitigated counterpart of <c>datetime-minvalue-landmine</c>: the same
	/// default DateTime, but declared the two ways that survive. Kept as a separate case so
	/// the landmine case can be expected to fail while this one is expected to succeed.</summary>
	[DataContract]
	public class SafeDateDto
	{
		[DataMember(Name = "droppedWhenDefault", EmitDefaultValue = false)]
		public DateTime DroppedWhenDefault { get; set; }

		[DataMember(Name = "nullableInstead")]
		public DateTime? NullableInstead { get; set; }

		[DataMember(Name = "utcMinIsFine")]
		public DateTime UtcMinIsFine { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "datetime-minvalue-mitigated"; } }
		public static Type RootType { get { return typeof(SafeDateDto); } }

		public static object Create()
		{
			return new SafeDateDto
			{
				DroppedWhenDefault = default(DateTime),
				NullableInstead = null,
				// MinValue is serializable when it is explicitly UTC: no offset conversion.
				UtcMinIsFine = new DateTime(DateTime.MinValue.Ticks, DateTimeKind.Utc)
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}
