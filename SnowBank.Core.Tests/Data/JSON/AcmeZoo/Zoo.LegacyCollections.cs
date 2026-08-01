// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// A shape we really have and the corpus did not cover: non-generic ArrayList as a serialized
// member. Measured at 6 member positions in a statistics DTO. Added because the replacement
// changes how non-generic collections bind, and without a case a re-run would ASSUME rather
// than verify.
//
// THIS CASE HAS AN EXPIRY DATE, and that is deliberate. ArrayList and Hashtable are not shapes
// to preserve: they have no place in a modern codebase and are slated for removal (to List<T>
// and Dictionary<K,V>). What the case pins is therefore narrow and temporary: documents already
// written in this shape must stay READABLE for as long as it takes to convert the members and
// migrate the stored data. Once those 6 members are List<T> and the data is rewritten, delete
// this case rather than maintaining it.
//
// Note on the shapes rc.1 deliberately made loud: multi-dimensional arrays (previously flattened
// silently), NameValueCollection (previously keys-only), ImmutableSortedSet (previously handed
// back as an ImmutableHashSet) and reversed Stack<T> round-trips. Acme has ZERO of those in any
// serialized member position, verified twice by two different methods, so no case is included for
// them and our re-run will not exercise those paths. Stated rather than left silent, so nobody
// reads their absence as coverage.

namespace Acme.Zoo.Cases.LegacyArrayList
{
	using System;
	using System.Collections;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Non-generic <c>ArrayList</c> members, holding heterogeneous values the way an
	/// untyped collection invites. The interesting question for a replacement is not whether the
	/// list round-trips but what happens to the ELEMENT types, since nothing on the wire records
	/// what they were: a reader has only the JSON scalar kinds to work from.</summary>
	[DataContract]
	public class CounterSeriesDto
	{
		[DataMember(Name = "labels")]
		public ArrayList Labels { get; set; }

		[DataMember(Name = "values")]
		public ArrayList Values { get; set; }

		/// <summary>Deliberately heterogeneous, which is the whole hazard of an untyped list.</summary>
		[DataMember(Name = "mixed")]
		public ArrayList Mixed { get; set; }

		[DataMember(Name = "emptyOne")]
		public ArrayList EmptyOne { get; set; }

		[DataMember(Name = "nullOne")]
		public ArrayList NullOne { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "legacy-arraylist-members"; } }
		public static Type RootType { get { return typeof(CounterSeriesDto); } }

		public static object Create()
		{
			return new CounterSeriesDto
			{
				Labels = new ArrayList { "week 1", "week 2" },
				Values = new ArrayList { 10, 20, 30 },
				Mixed = new ArrayList { "text", 42, true, 1.5d, null },
				EmptyOne = new ArrayList(),
				NullOne = null,
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		/// <summary>Shapes that exist at rest, so the replacement must still read them.</summary>
		public static string[] LegacyDocuments
		{
			get
			{
				return new[]
				{
					"{\"labels\":[\"week 1\",\"week 2\"],\"values\":[10,20,30],\"mixed\":[\"text\",42,true,1.5,null],\"emptyOne\":[],\"nullOne\":null}",
				};
			}
		}
	}
}
