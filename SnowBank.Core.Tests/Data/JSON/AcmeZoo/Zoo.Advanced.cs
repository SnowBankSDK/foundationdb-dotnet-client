// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// Categories: polymorphism, object graphs, lifecycle callbacks, extension data, and the
// first diagnostic case, which is expected to fail loudly rather than to match.
// Scan-derived weights: [KnownType] 1146 · IExtensibleDataObject 29 · lifecycle callbacks 103
// (OnDeserialized 52, OnSerializing 27, OnDeserializing 23, OnSerialized 1) · ISerializable 5.
// Serializer configurations in the whole application: exactly two, both represented here.

namespace Acme.Zoo.Cases.PolyKnownTypeAbstract
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>[KnownType] with an abstract-typed member, which is what makes DCJS emit its
	/// __type hint. What matters beyond its presence is its value, which encodes
	/// "TypeName:DataContractNamespace": a consumer that switches on __type sees a different
	/// document if either the spelling or the value format changes.</summary>
	[DataContract]
	[KnownType(typeof(TextCriterion))]
	[KnownType(typeof(RangeCriterion))]
	public abstract class SearchCriterion
	{
		[DataMember(Name = "field")]
		public string Field { get; set; }
	}

	[DataContract(Name = "TextCriterion", Namespace = "http://acme.invalid/zoo")]
	public class TextCriterion : SearchCriterion
	{
		[DataMember(Name = "term")]
		public string Term { get; set; }
	}

	[DataContract(Name = "RangeCriterion", Namespace = "http://acme.invalid/zoo")]
	public class RangeCriterion : SearchCriterion
	{
		[DataMember(Name = "from")]
		public int From { get; set; }

		[DataMember(Name = "to")]
		public int To { get; set; }
	}

	[DataContract]
	public class QueryDto
	{
		[DataMember(Name = "primary")]
		public SearchCriterion Primary { get; set; }

		[DataMember(Name = "all")]
		public List<SearchCriterion> All { get; set; }

		/// <summary>Declared as object: the loosest possible declared type.</summary>
		[DataMember(Name = "loose")]
		public object Loose { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "poly-known-type-abstract"; } }
		public static Type RootType { get { return typeof(QueryDto); } }

		public static object Create()
		{
			return new QueryDto
			{
				Primary = new TextCriterion { Field = "title", Term = "atlas" },
				All = new List<SearchCriterion>
				{
					new TextCriterion { Field = "title", Term = "atlas" },
					new RangeCriterion { Field = "year", From = 1900, To = 1950 }
				},
				Loose = new TextCriterion { Field = "any", Term = "loose" }
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
					// DCJS always emitted the hint first and with this value format, so this is
					// the shape that exists at rest and in already-delivered payloads.
					"{\"primary\":{\"__type\":\"TextCriterion:http:\\/\\/acme.invalid\\/zoo\",\"field\":\"title\",\"term\":\"atlas\"}}"
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.PolySerializerKnownTypes
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>The application's only other serializer configuration: known types passed
	/// through the constructor instead of declared by attribute. Measured across the whole
	/// source tree: 55 call sites use the type-only constructor, 4 use this one. There is no
	/// DataContractJsonSerializerSettings anywhere, no surrogate, no DataContractResolver,
	/// no UseSimpleDictionaryFormat and no EmitTypeInformation. So the entire application's
	/// serializer surface is these two lines.</summary>
	[DataContract]
	public abstract class NotificationPayload
	{
		[DataMember(Name = "kind")]
		public string Kind { get; set; }
	}

	[DataContract(Name = "MailPayload", Namespace = "http://acme.invalid/zoo")]
	public class MailPayload : NotificationPayload
	{
		[DataMember(Name = "address")]
		public string Address { get; set; }
	}

	[DataContract]
	public class EnvelopeDto
	{
		[DataMember(Name = "payload")]
		public NotificationPayload Payload { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "poly-serializer-known-types"; } }
		public static Type RootType { get { return typeof(EnvelopeDto); } }

		public static object Create()
		{
			return new EnvelopeDto
			{
				Payload = new MailPayload { Kind = "mail", Address = "someone@example.invalid" }
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			var knownTypes = new List<Type> { typeof(MailPayload) };
			return new DataContractJsonSerializer(RootType, knownTypes);
		}
	}
}

namespace Acme.Zoo.Cases.LifecycleCallbacks
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Callbacks that mutate state, which is the only interesting kind. The
	/// application has 103 of them, dominated by OnDeserialized (52). If a replacement
	/// serializer does not run these, the object comes back structurally correct and
	/// semantically wrong, with nothing to signal it.</summary>
	[DataContract]
	public class CallbackDto
	{
		[DataMember(Name = "stored")]
		public string Stored { get; set; }

		[DataMember(Name = "trace")]
		public string Trace { get; set; }

		/// <summary>Not serialized: rebuilt by OnDeserialized. The classic shape.</summary>
		public string Derived { get; set; }

		[OnSerializing]
		private void OnSerializingMethod(StreamingContext context)
		{
			this.Trace = (this.Trace ?? "") + "|onSerializing";
		}

		[OnSerialized]
		private void OnSerializedMethod(StreamingContext context)
		{
			this.Trace = (this.Trace ?? "") + "|onSerialized";
		}

		[OnDeserializing]
		private void OnDeserializingMethod(StreamingContext context)
		{
			this.Derived = "set-by-onDeserializing";
		}

		[OnDeserialized]
		private void OnDeserializedMethod(StreamingContext context)
		{
			this.Derived = "rebuilt:" + (this.Stored ?? "(none)");
		}
	}

	public static class Sample
	{
		public static string Id { get { return "lifecycle-callbacks"; } }
		public static Type RootType { get { return typeof(CallbackDto); } }

		public static object Create()
		{
			return new CallbackDto { Stored = "value", Trace = "start" };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		public static string[] LegacyDocuments
		{
			get { return new[] { "{\"stored\":\"from-wire\",\"trace\":\"t\"}" }; }
		}
	}
}

namespace Acme.Zoo.Cases.ExtensibleDataObject
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>IExtensibleDataObject: unknown members are retained and re-emitted, which is
	/// how a contract survives a version it does not know about. 29 occurrences.
	/// <para>This case doubles as the duplicate-key probe. The second input carries an
	/// unknown member whose name equals a declared member's output name in a different case
	/// ("Known" vs "known"), which is the only realistic path to a duplicate key in DCJS
	/// output that I could construct. If it stays clean, key-order irrelevance can be
	/// asserted without reservation.</para></summary>
	[DataContract]
	public class ExtensibleDto : IExtensibleDataObject
	{
		[DataMember(Name = "known")]
		public string Known { get; set; }

		public ExtensionDataObject ExtensionData { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "extensible-data-object"; } }
		public static Type RootType { get { return typeof(ExtensibleDto); } }

		public static object Create()
		{
			return new ExtensibleDto { Known = "declared" };
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
					"{\"known\":\"declared\",\"addedInALaterVersion\":\"kept?\",\"nestedUnknown\":{\"a\":1}}",
					"{\"known\":\"declared\",\"Known\":\"different-case\"}"
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.DiagnosticDoubleContract
{
	using System;
	using Newtonsoft.Json;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Diagnostic case. The expected outcome is a loud failure, not a match.
	/// <para>Found in the wild: 2307 members carry both [DataMember] and [JsonProperty],
	/// and 1726 of those give each attribute a different output name. One DTO, two serializers,
	/// two different documents: the lowercase name is the client-facing contract served by
	/// DCJS, the uppercase one is the field name of a legacy back-end output served by
	/// Newtonsoft.</para>
	/// <para>This is an architecture defect rather than a compatibility target. It cannot be
	/// carried over as-is, because a serializer that understands both attribute families will
	/// see both and honour only one, silently. The remedy is to split the DTO, and the "after"
	/// shape below is the worked example for the migration guide. What CrystalJson should do
	/// with the "before" shape is refuse it with an explicit message naming the property and
	/// the two conflicting attributes.</para></summary>
	[DataContract]
	public class TwoFacedDto
	{
		[DataMember(Name = "enabled"), JsonProperty(PropertyName = "ACTIF")]
		public bool Enabled { get; set; }

		[DataMember(Name = "label"), JsonProperty(PropertyName = "LIBELLE")]
		public string Label { get; set; }

		/// <summary>Same name on both sides: harmless, and worth including so the diagnostic
		/// can distinguish "both attributes present" from "both present and disagreeing".</summary>
		[DataMember(Name = "code"), JsonProperty(PropertyName = "code")]
		public string Code { get; set; }
	}

	// ---- the remedy, quoted by the migration guide ----

	/// <summary>Client-facing half of the split.</summary>
	[DataContract]
	public class ClientFacingDto
	{
		[DataMember(Name = "enabled")]
		public bool Enabled { get; set; }

		[DataMember(Name = "label")]
		public string Label { get; set; }

		[DataMember(Name = "code")]
		public string Code { get; set; }
	}

	/// <summary>Legacy back-end half of the split.</summary>
	public class LegacyOutputDto
	{
		[JsonProperty(PropertyName = "ACTIF")]
		public bool Enabled { get; set; }

		[JsonProperty(PropertyName = "LIBELLE")]
		public string Label { get; set; }

		[JsonProperty(PropertyName = "code")]
		public string Code { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "diagnostic-double-contract"; } }
		public static Type RootType { get { return typeof(TwoFacedDto); } }

		public static object Create()
		{
			return new TwoFacedDto { Enabled = true, Label = "Ouvrage", Code = "c-1" };
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}

		/// <summary>The same instance serialized by the other library. Put the two outputs side
		/// by side and the defect stops being an assertion: DCJS honours its own attribute and
		/// ignores the other, Newtonsoft does the reverse, so one DTO has two incompatible output
		/// contracts and no single serializer can serve both.</summary>
		public static string NewtonsoftJson()
		{
			return JsonConvert.SerializeObject(Create());
		}
	}
}
