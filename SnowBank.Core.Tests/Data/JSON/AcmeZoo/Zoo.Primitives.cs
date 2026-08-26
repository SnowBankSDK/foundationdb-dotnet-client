// Imported verbatim from the Acme DCJS sample zoo (synthetic corpus, anonymized at the source).
// These cases are deliberately written in the legacy pre-nullable style of the application they mirror.
#nullable disable
#pragma warning disable CS0649 // fields only ever assigned by the serializer

// Category: primitives, numbers at the edges, strings and escaping, dates, enums.
// Scan-derived weights ([DataMember] members by declared type):
//   string 6739 · bool 1909 · int 1706 · nullable 1045 · DateTime 343 · long 154
//   double 93 · decimal 28 · Uri 12 · Guid 10 · byte[] 4 · TimeSpan 4 · DateTimeOffset 3.
//   Enums: [EnumMember] 810 (76 with Value=), [Flags] 28.
// All literal values are escaped rather than typed directly, so the source file's own
// encoding can never influence what the case asserts.

namespace Acme.Zoo.Cases.PrimitiveScalars
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	[DataContract]
	public class ScalarDto
	{
		[DataMember(Name = "aBool")]
		public bool ABool { get; set; }

		[DataMember(Name = "aChar")]
		public char AChar { get; set; }

		[DataMember(Name = "intMin")]
		public int IntMin { get; set; }

		[DataMember(Name = "intMax")]
		public int IntMax { get; set; }

		/// <summary>Above 2^53: exact in C#, not exactly representable by a JavaScript number.</summary>
		[DataMember(Name = "longAbove2Pow53")]
		public long LongAbove2Pow53 { get; set; }

		[DataMember(Name = "longMax")]
		public long LongMax { get; set; }

		/// <summary>Round-trip-sensitive formatting. .NET Framework and .NET Core format
		/// doubles differently by default, which is why this member is here.</summary>
		[DataMember(Name = "doubleTenth")]
		public double DoubleTenth { get; set; }

		[DataMember(Name = "doubleTiny")]
		public double DoubleTiny { get; set; }

		[DataMember(Name = "doubleNegativeZero")]
		public double DoubleNegativeZero { get; set; }

		/// <summary>Trailing zeros: scale is meaningful for money and invisible to a
		/// numeric equality check.</summary>
		[DataMember(Name = "decimalTrailingZeros")]
		public decimal DecimalTrailingZeros { get; set; }

		[DataMember(Name = "aGuid")]
		public Guid AGuid { get; set; }

		[DataMember(Name = "aUri")]
		public Uri AUri { get; set; }

		[DataMember(Name = "aTimeSpan")]
		public TimeSpan ATimeSpan { get; set; }

		[DataMember(Name = "someBytes")]
		public byte[] SomeBytes { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "primitive-scalars"; } }
		public static Type RootType { get { return typeof(ScalarDto); } }

		public static object Create()
		{
			return new ScalarDto
			{
				ABool = true,
				AChar = 'A',
				IntMin = int.MinValue,
				IntMax = int.MaxValue,
				LongAbove2Pow53 = 9007199254740993L,
				LongMax = long.MaxValue,
				DoubleTenth = 0.1d,
				DoubleTiny = 1e-7d,
				DoubleNegativeZero = -0.0d,
				DecimalTrailingZeros = 1.100m,
				AGuid = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
				AUri = new Uri("https://example.invalid/path?q=1&r=2"),
				ATimeSpan = new TimeSpan(1, 2, 3, 4, 5),
				SomeBytes = new byte[] { 0, 1, 127, 128, 255 }
			};
		}

		public static DataContractJsonSerializer CreateSerializer()
		{
			return new DataContractJsonSerializer(RootType);
		}
	}
}

namespace Acme.Zoo.Cases.PrimitiveDateTimeKinds
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Every DateTime kind the application really stores, plus DateTimeOffset.
	/// Timezone sensitive: DCJS embeds the generating machine's UTC offset for Local and
	/// Unspecified kinds, so these two members only reproduce byte-for-byte in the timezone
	/// recorded in the manifest. That dependency is the point of the case, not a flaw.
	/// Consequence for the replacement: an offsetless ISO 8601 rendering of an Unspecified
	/// date is interpreted as local time by a browser, so the client computes an instant
	/// shifted by its own UTC offset. No C# binding test can observe that.</summary>
	[DataContract]
	public class DateKindsDto
	{
		[DataMember(Name = "utc")]
		public DateTime Utc { get; set; }

		[DataMember(Name = "local")]
		public DateTime Local { get; set; }

		[DataMember(Name = "unspecified")]
		public DateTime Unspecified { get; set; }

		[DataMember(Name = "offsetPlusTwo")]
		public DateTimeOffset OffsetPlusTwo { get; set; }

		[DataMember(Name = "offsetUtc")]
		public DateTimeOffset OffsetUtc { get; set; }

		[DataMember(Name = "nullableSet")]
		public DateTime? NullableSet { get; set; }

		[DataMember(Name = "nullableNull")]
		public DateTime? NullableNull { get; set; }

		/// <summary>Sub-second precision: does it survive the round trip?</summary>
		[DataMember(Name = "withMilliseconds")]
		public DateTime WithMilliseconds { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "primitive-datetime-kinds"; } }
		public static Type RootType { get { return typeof(DateKindsDto); } }

		public static object Create()
		{
			return new DateKindsDto
			{
				Utc = new DateTime(2026, 7, 30, 12, 34, 56, DateTimeKind.Utc),
				Local = new DateTime(2026, 7, 30, 12, 34, 56, DateTimeKind.Local),
				Unspecified = new DateTime(2026, 7, 30, 12, 34, 56, DateTimeKind.Unspecified),
				OffsetPlusTwo = new DateTimeOffset(2026, 7, 30, 12, 34, 56, TimeSpan.FromHours(2)),
				OffsetUtc = new DateTimeOffset(2026, 7, 30, 12, 34, 56, TimeSpan.Zero),
				NullableSet = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				NullableNull = null,
				WithMilliseconds = new DateTime(2026, 7, 30, 12, 34, 56, 789, DateTimeKind.Utc)
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
					// The Microsoft form the application's stored data and its ExtJS client know.
					"{\"utc\":\"\\/Date(1784507696000)\\/\",\"local\":\"\\/Date(1784507696000+0200)\\/\"}",
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.PrimitiveStringEscaping
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>Everything that makes a JSON writer choose an escaping style. Escaping style
	/// is absorbed by the equivalence rubric, so these are here to prove the decoded value
	/// survives, not to compare escape sequences.</summary>
	[DataContract]
	public class EscapingDto
	{
		[DataMember(Name = "quotes")]
		public string Quotes { get; set; }

		[DataMember(Name = "backslash")]
		public string Backslash { get; set; }

		[DataMember(Name = "forwardSlash")]
		public string ForwardSlash { get; set; }

		[DataMember(Name = "controlChars")]
		public string ControlChars { get; set; }

		[DataMember(Name = "accentedLatin")]
		public string AccentedLatin { get; set; }

		[DataMember(Name = "nonLatinScript")]
		public string NonLatinScript { get; set; }

		[DataMember(Name = "rightToLeft")]
		public string RightToLeft { get; set; }

		/// <summary>A single astral character, i.e. a UTF-16 surrogate pair. This is where
		/// "sort keys ordinally" stops being a single well-defined instruction, because
		/// UTF-16 code-unit order and UTF-8 byte order disagree above the BMP.</summary>
		[DataMember(Name = "astralChar")]
		public string AstralChar { get; set; }

		[DataMember(Name = "combiningVersusPrecomposed")]
		public string CombiningVersusPrecomposed { get; set; }

		[DataMember(Name = "htmlLike")]
		public string HtmlLike { get; set; }

		[DataMember(Name = "looksLikeADate")]
		public string LooksLikeADate { get; set; }

		[DataMember(Name = "looksLikeMicrosoftDate")]
		public string LooksLikeMicrosoftDate { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "primitive-string-escaping"; } }
		public static Type RootType { get { return typeof(EscapingDto); } }

		public static object Create()
		{
			return new EscapingDto
			{
				Quotes = "he said \"stop\" twice",
				Backslash = "C:\\path\\to\\file",
				ForwardSlash = "a/b/c",
				ControlChars = "tab\there\nnewline\u0001unit",
				// Escaped rather than typed literally, so this file's encoding cannot matter.
				AccentedLatin = "m\u00e9diath\u00e8que \u00e0 c\u00f4t\u00e9",
				NonLatinScript = "\u0394\u03b5\u03bb\u03c4\u03b1",
				RightToLeft = "\u05e1\u05e4\u05e8\u05d9\u05d4 \u0645\u0643\u062a\u0628\u0629",
				AstralChar = "\ud83d\ude00",
				// Same visible text twice: precomposed then decomposed. Equal to a human,
				// different byte sequences, and therefore different hashes.
				CombiningVersusPrecomposed = "\u00e9 vs e\u0301",
				HtmlLike = "<script>alert('x')</script>",
				LooksLikeADate = "2026-07-30T12:34:56Z",
				LooksLikeMicrosoftDate = "/Date(1784507696000)/"
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
					// A string member whose content looks like a date. It must come back a
					// string, not silently become a DateTime.
					"{\"looksLikeADate\":\"2026-07-30T12:34:56Z\",\"looksLikeMicrosoftDate\":\"\\/Date(1784507696000)\\/\"}"
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.EnumPlainAndEnumMember
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	[DataContract]
	public enum LoanState
	{
		[EnumMember]
		Unknown = 0,

		[EnumMember(Value = "on-shelf")]
		OnShelf = 1,

		[EnumMember(Value = "on-loan")]
		OnLoan = 5,

		[EnumMember]
		Withdrawn = 9
	}

	/// <summary>An enum with no [DataContract] and no [EnumMember] at all, for contrast.</summary>
	public enum PlainKind
	{
		First = 0,
		Second = 2
	}

	[DataContract]
	public class EnumDto
	{
		[DataMember(Name = "stateWithValue")]
		public LoanState StateWithValue { get; set; }

		[DataMember(Name = "stateWithoutValue")]
		public LoanState StateWithoutValue { get; set; }

		[DataMember(Name = "plain")]
		public PlainKind Plain { get; set; }

		[DataMember(Name = "nullableSet")]
		public LoanState? NullableSet { get; set; }

		[DataMember(Name = "nullableNull")]
		public LoanState? NullableNull { get; set; }

		[DataMember(Name = "inList")]
		public List<LoanState> InList { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "enum-plain-and-enum-member"; } }
		public static Type RootType { get { return typeof(EnumDto); } }

		public static object Create()
		{
			return new EnumDto
			{
				StateWithValue = LoanState.OnLoan,
				StateWithoutValue = LoanState.Withdrawn,
				Plain = PlainKind.Second,
				NullableSet = LoanState.OnShelf,
				NullableNull = null,
				InList = new List<LoanState> { LoanState.OnShelf, LoanState.OnLoan }
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
					"{\"stateWithValue\":5,\"stateWithoutValue\":9,\"plain\":2}",
					// A numeric value outside the declared set: DCJS accepts it silently, and a member cast to an
					// undeclared value is how one reaches the output. The string form a modern serializer emits is
					// deliberately not listed: DCJS throws on it, so no producer wrote it into this application's data.
					"{\"stateWithValue\":4242}"
				};
			}
		}
	}
}

namespace Acme.Zoo.Cases.EnumFlags
{
	using System;
	using System.Runtime.Serialization;
	using System.Runtime.Serialization.Json;

	/// <summary>[Flags]. The rubric treats numeric and string enum renderings as equivalent
	/// for a scalar enum; for a flags enum it does not, because a client doing a bitwise
	/// test against a combined numeric value cannot do anything with "Read, Write".</summary>
	[DataContract]
	[Flags]
	public enum AccessRights
	{
		[EnumMember]
		None = 0,

		[EnumMember]
		Read = 1,

		[EnumMember]
		Write = 2,

		[EnumMember]
		Delete = 4,

		[EnumMember]
		All = Read | Write | Delete
	}

	[DataContract]
	public class FlagsDto
	{
		[DataMember(Name = "combined")]
		public AccessRights Combined { get; set; }

		[DataMember(Name = "single")]
		public AccessRights Single { get; set; }

		[DataMember(Name = "none")]
		public AccessRights None { get; set; }

		[DataMember(Name = "namedCombination")]
		public AccessRights NamedCombination { get; set; }

		[DataMember(Name = "undeclaredBit")]
		public AccessRights UndeclaredBit { get; set; }
	}

	public static class Sample
	{
		public static string Id { get { return "enum-flags"; } }
		public static Type RootType { get { return typeof(FlagsDto); } }

		public static object Create()
		{
			return new FlagsDto
			{
				Combined = AccessRights.Read | AccessRights.Write,
				Single = AccessRights.Delete,
				None = AccessRights.None,
				NamedCombination = AccessRights.All,
				UndeclaredBit = (AccessRights) 64
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
					"{\"combined\":3,\"single\":4,\"none\":0,\"namedCombination\":7,\"undeclaredBit\":64}"
				};
			}
		}
	}
}
