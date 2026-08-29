// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// Category: collections and dictionaries.
// Scan-derived weights ([DataMember] members by declared type, DataContract-bearing product files):
//   Collection<T> 396 · List<T> 303 · interface-declared collections 280 · array 101 · Dictionary 58.
// Types deriving from a collection: Collection<T> 108, non-generic List<T> subclass 31,
// Dictionary 43. [CollectionDataContract] 217 (ItemName 124, KeyName/ValueName 28 each).
//
// The application never sets UseSimpleDictionaryFormat (0 occurrences in the entire source
// tree), and that shapes this whole category. Every dictionary in the output is therefore in
// DCJS's default key/value-pair array form.

namespace Acme.Zoo.Cases.CollectionOfTMember
{
	using System;
	using System.Collections.ObjectModel;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Collection&lt;T&gt; as a member. 395 occurrences in the application, and the
	/// exact shape that deserialized to null with no error on CrystalJson 7.4.2 (fixed in
	/// 7.4.3). Kept as a permanent regression witness, not as a live defect.</summary>
	[DataContract]
	public class CollectionMemberDto
	{
		[DataMember(Name = "ids")]
		public Collection<string> Ids { get; set; }

		[DataMember(Name = "codes")]
		public Collection<int> Codes { get; set; }

		[DataMember(Name = "emptyOne")]
		public Collection<string> EmptyOne { get; set; }

		[DataMember(Name = "nullOne")]
		public Collection<string> NullOne { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "collection-of-t-member"; } }
		public static Type RootType { get { return typeof(CollectionMemberDto); } }

		public static object Create()
		{
			return new CollectionMemberDto
			{
				Ids = new Collection<string> { "a", "b", "c" },
				Codes = new Collection<int> { 1, 2 },
				EmptyOne = new Collection<string>(),
				NullOne = null
			};
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
					"{\"codes\":[1,2],\"emptyOne\":[],\"ids\":[\"a\",\"b\",\"c\"],\"nullOne\":null}"
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.CollectionListAndInterfaces
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>List&lt;T&gt;, interface-declared members, and an array side by side.
	/// Interface-declared members are the interesting ones: the declared type carries no
	/// concrete constructor, so the reader has to choose one.</summary>
	[DataContract]
	public class MixedCollectionDto
	{
		[DataMember(Name = "list")]
		public List<string> AsList { get; set; }

		[DataMember(Name = "ilist")]
		public IList<string> AsIList { get; set; }

		[DataMember(Name = "enumerable")]
		public IEnumerable<string> AsEnumerable { get; set; }

		[DataMember(Name = "icollection")]
		public ICollection<int> AsICollection { get; set; }

		[DataMember(Name = "array")]
		public string[] AsArray { get; set; }

		[DataMember(Name = "nested")]
		public List<List<int>> Nested { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "collection-list-and-interfaces"; } }
		public static Type RootType { get { return typeof(MixedCollectionDto); } }

		public static object Create()
		{
			return new MixedCollectionDto
			{
				AsList = new List<string> { "l1" },
				AsIList = new List<string> { "i1", "i2" },
				AsEnumerable = new List<string> { "e1" },
				AsICollection = new List<int> { 9 },
				AsArray = new[] { "arr" },
				Nested = new List<List<int>> { new List<int> { 1, 2 }, new List<int>() }
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.DictionaryStringKey
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Dictionary with string keys, in DCJS's default key/value-pair array form.
	/// Highest blast radius of the corpus for browser consumers: a client reading
	/// <c>obj.map["k"]</c> and a client reading <c>obj.map[i].Key</c> are not
	/// interchangeable, yet both bind to the same C# Dictionary.</summary>
	[DataContract]
	public class StringKeyDictionaryDto
	{
		[DataMember(Name = "map")]
		public Dictionary<string, string> Map { get; set; }

		[DataMember(Name = "mapToInt")]
		public Dictionary<string, int> MapToInt { get; set; }

		[DataMember(Name = "emptyMap")]
		public Dictionary<string, string> EmptyMap { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "dictionary-string-key"; } }
		public static Type RootType { get { return typeof(StringKeyDictionaryDto); } }

		public static object Create()
		{
			var map = new Dictionary<string, string>();
			// Insertion order is deliberately not sorted, to expose whether the output order
			// follows insertion (it does, for DCJS) or is normalized.
			map["zeta"] = "last";
			map["alpha"] = "first";
			map["accented-e"] = "valeur accentuee";

			var toInt = new Dictionary<string, int>();
			toInt["one"] = 1;

			return new StringKeyDictionaryDto
			{
				Map = map,
				MapToInt = toInt,
				EmptyMap = new Dictionary<string, string>()
			};
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
					"{\"emptyMap\":[],\"map\":[{\"Key\":\"alpha\",\"Value\":\"first\"}],\"mapToInt\":[]}",
					// The object-map form a modern serializer emits is deliberately not listed: DCJS reads it as an
					// empty dictionary with no error, so no producer ever wrote that shape into this application's data.
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.DictionaryNonStringKey
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Dictionary with non-string keys. A JSON object cannot express these at all,
	/// so the key/value-pair array is not a stylistic choice here, it is the only option.</summary>
	[DataContract]
	public class NonStringKeyDictionaryDto
	{
		[DataMember(Name = "byInt")]
		public Dictionary<int, string> ByInt { get; set; }

		[DataMember(Name = "byGuid")]
		public Dictionary<Guid, string> ByGuid { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "dictionary-non-string-key"; } }
		public static Type RootType { get { return typeof(NonStringKeyDictionaryDto); } }

		public static object Create()
		{
			var byInt = new Dictionary<int, string>();
			byInt[2] = "two";
			byInt[1] = "one";

			var byGuid = new Dictionary<Guid, string>();
			byGuid[new Guid("11111111-2222-3333-4444-555555555555")] = "fixed";

			return new NonStringKeyDictionaryDto { ByInt = byInt, ByGuid = byGuid };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.CollectionDataContractNaming
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>[CollectionDataContract] with custom item, key and value names. 217
	/// occurrences in the application. Whether these names survive at all in JSON (as
	/// opposed to XML) is precisely what this case pins.</summary>
	[CollectionDataContract(Name = "ItemBag", ItemName = "entry")]
	public class ItemBag : List<string>
	{
	}

	[CollectionDataContract(Name = "LabelMap", ItemName = "pair", KeyName = "code", ValueName = "label")]
	public class LabelMap : Dictionary<string, string>
	{
	}

	[DataContract]
	public class NamedCollectionDto
	{
		[DataMember(Name = "bag")]
		public ItemBag Bag { get; set; }

		[DataMember(Name = "labels")]
		public LabelMap Labels { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "collection-data-contract-naming"; } }
		public static Type RootType { get { return typeof(NamedCollectionDto); } }

		public static object Create()
		{
			var bag = new ItemBag { "x", "y" };
			var labels = new LabelMap();
			labels["c1"] = "Label one";
			return new NamedCollectionDto { Bag = bag, Labels = labels };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.CollectionNonGenericSubclass
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Non-generic subclasses of List&lt;T&gt; and Collection&lt;T&gt;, used both as
	/// a member and as the root type. 31 List-derived and 105 Collection-derived declarations
	/// in the application. This family crashed on serialization with IndexOutOfRangeException
	/// on CrystalJson 7.4.2 (fixed in 7.4.3) because the element type was read from the
	/// subclass instead of the closed base.</summary>
	public class ProfileList : List<ProfileEntry>
	{
	}

	public class EntryCollection : Collection<ProfileEntry>
	{
	}

	[DataContract]
	public class ProfileEntry
	{
		[DataMember(Name = "code")]
		public string Code { get; set; }

		[DataMember(Name = "rank")]
		public int Rank { get; set; }
	}

	[DataContract]
	public class SubclassHolderDto
	{
		[DataMember(Name = "profiles")]
		public ProfileList Profiles { get; set; }

		[DataMember(Name = "entries")]
		public EntryCollection Entries { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "collection-non-generic-subclass"; } }
		public static Type RootType { get { return typeof(SubclassHolderDto); } }

		public static object Create()
		{
			var profiles = new ProfileList
			{
				new ProfileEntry { Code = "p1", Rank = 1 }
			};
			var entries = new EntryCollection
			{
				new ProfileEntry { Code = "e1", Rank = 2 }
			};
			return new SubclassHolderDto { Profiles = profiles, Entries = entries };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.CollectionSubclassAsRoot
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>The same family with the collection subclass as the root type rather than a
	/// member. Worth separating: root and member binding took different code paths in the
	/// 7.4.2 defect, failing with an exception in one and silently in the other.</summary>
	[DataContract]
	public class TagEntry
	{
		[DataMember(Name = "tag")]
		public string Tag { get; set; }
	}

	public class TagList : List<TagEntry>
	{
	}

	public static class Sample
	{
		public static string Id { get { return "collection-subclass-as-root"; } }
		public static Type RootType { get { return typeof(TagList); } }

		public static object Create()
		{
			return new TagList { new TagEntry { Tag = "t1" }, new TagEntry { Tag = "t2" } };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		public static string[] LegacyDocuments
		{
			get { return new[] { "[{\"tag\":\"t1\"},{\"tag\":\"t2\"}]" }; }
		}
	}
}
