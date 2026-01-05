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

// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToConstant.Local
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local

#pragma warning disable CA1069 // Enums values should not be duplicated
#pragma warning disable CA1822 // Mark members as static
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SnowBank.Data.Json.Tests
{
	using System.Runtime.Serialization;
	using System.Xml.Serialization;
	using STJ = System.Text.Json;
	using NJ = Newtonsoft.Json;

	public enum DummyJsonEnum
	{
		None,
		Foo = 1,
		Bar = 42,
	}

	[Flags]
	public enum DummyJsonEnumFlags
	{
		None,
		Foo = 1,
		Bar = 2,
		Narf = 4
	}

	public enum DummyJsonEnumInt32
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = 65535
	}

	public enum DummyJsonEnumShort : ushort
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = 65535
	}

	public enum DummyJsonEnumInt64 : long
	{
		None,
		One = 1,
		Two = 2,
		MaxValue = long.MaxValue
	}

	public enum DummyJsonEnumTypo
	{
		None,
		Foo,
		Bar = 2,   // new name, with correct spelling
		Barrh = 2, // old name, with a typo, but is still referenced (in old code, in old JSON documents, etc...)
		Baz
	}

#pragma warning disable 169, 649
	public struct DummyJsonStruct
	{
		public bool Valid;
		public string Name;
		public int Index;
		public long Size;
		public float Height;
		public double Amount;
		public DateTime Created;
		public DateTime? Modified;
		public DateOnly? DateOfBirth;
		public DummyJsonEnum State;
		public readonly double RatioOfStuff => this.Amount * this.Size;

		private string Invisible;
		private readonly string DotNotCall => "ShouldNotBeCalled";
	}
#pragma warning restore 169, 649

	public struct DummyNullableStruct
	{
		public bool? Bool;
		public int? Int32;
		public long? Int64;
		public float? Single;
		public double? Double;
		public DateTime? DateTime;
		public TimeSpan? TimeSpan;
		public Guid? Guid;
		public DummyJsonEnum? Enum;
		public DummyJsonStruct? Struct;
	}

	public class DummyJsonClass
	{
		private string m_invisible = "ShouldNotBeVisible";
		private string? m_name;
		public bool Valid => m_name is not null;
		public string? Name { get => m_name; set => m_name = value; }
		public int Index { get; set; }
		public long Size { get; set; }
		public float Height { get; set; }
		public double Amount { get; set; }
		public DateTime Created { get; set; }
		public DateTime? Modified { get; set; }
		public DateOnly? DateOfBirth { get; set; }
		public DummyJsonEnum State { get; set; }

		public double RatioOfStuff => this.Amount * this.Size;

		// ReSharper disable once UnusedMember.Local
		private string Invisible => m_invisible;

		public string MustNotBeCalled() { return "ShouldNotBeCalled"; }

		public override bool Equals(object? obj)
		{
			if (obj is not DummyJsonClass other) return false;
			return this.Index == other.Index
			       && m_name == other.m_name
			       && this.Size == other.Size
			       && this.Height == other.Height
			       && this.Amount == other.Amount
			       && this.Created == other.Created
			       && this.Modified == other.Modified
			       && this.State == other.State;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Index;
		}
	}

	[STJ.Serialization.JsonPolymorphic()]
	[STJ.Serialization.JsonDerivedType(typeof(DummyJsonBaseClass), "agent")]
	[STJ.Serialization.JsonDerivedType(typeof(DummyDerivedJsonClass), "spy")]
	public interface IDummyCustomInterface
	{
		string? Name { get; }
		int Index { get; }
		long Size { get; }
		float Height { get; }
		double Amount { get; }
		DateTime Created { get; }
		DateTime? Modified { get; }
		DummyJsonEnum State { get; }
	}

	public class DummyOuterClass
	{
		public int Id { get; set; }
		public IDummyCustomInterface Agent { get; set; }
	}

	public class DummyOuterDerivedClass
	{
		public int Id { get; set; }
		public DummyJsonBaseClass Agent { get; set; }
	}

	public class DummyJsonBaseClass : IDummyCustomInterface
	{
		// same as "DummyJsonClass", but part of a polymorphic chain

		private string m_invisible = "ShouldNotBeVisible";
		private string? m_name;
		public bool Valid => m_name is not null;
		public string? Name { get => m_name; set => m_name = value; }
		public int Index { get; set; }
		public long Size { get; set; }
		public float Height { get; set; }
		public double Amount { get; set; }
		public DateTime Created { get; set; }
		public DateTime? Modified { get; set; }
		public DateOnly? DateOfBirth { get; set; }
		public DummyJsonEnum State { get; set; }

		public double RatioOfStuff => this.Amount * this.Size;

		// ReSharper disable once UnusedMember.Local
		private string Invisible => m_invisible;

		public string MustNotBeCalled() { return "ShouldNotBeCalled"; }

		public override bool Equals(object? obj)
		{
			if (obj is not DummyJsonBaseClass other) return false;
			return this.Index == other.Index
			       && m_name == other.m_name
			       && this.Size == other.Size
			       && this.Height == other.Height
			       && this.Amount == other.Amount
			       && this.Created == other.Created
			       && this.Modified == other.Modified
			       && this.State == other.State;
		}

		public override int GetHashCode()
		{
			// ReSharper disable once NonReadonlyMemberInGetHashCode
			return this.Index;
		}
	}

	public class DummyDerivedJsonClass : DummyJsonBaseClass
	{

		private string? m_doubleAgentName;

		public DummyDerivedJsonClass() { }

		public DummyDerivedJsonClass(string doubleAgentName)
		{
			m_doubleAgentName = doubleAgentName;
		}

		public string? DoubleAgentName { get => m_doubleAgentName; set => m_doubleAgentName = value; }
	}

	public sealed class DummyJsonCustomClass : IJsonSerializable, IJsonPackable, IJsonDeserializable<DummyJsonCustomClass>
	{
		public string DontCallThis => "ShouldNotSeeThat";

		private DummyJsonCustomClass()
		{ }

		public DummyJsonCustomClass(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(m_secret) + " }");
		}

		#endregion

		#region IJsonBindable Members

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(settings, Is.Not.Null, "settings");
			Assert.That(resolver, Is.Not.Null, "resolver");

			return JsonObject.Create("custom", m_secret);
		}

		static DummyJsonCustomClass IJsonDeserializable<DummyJsonCustomClass>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			Assert.That(value, Is.Not.Null, "value");
			Assert.That(resolver, Is.Not.Null, "resolver");

			Assert.That(value.Type, Is.EqualTo(JsonType.Object));
			var obj = (JsonObject)value;

			var secret = obj.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyJsonCustomClass(secret);
		}

		#endregion

	}

	public sealed class DummyStaticLegacyJson
	{
		public string DontCallThis => "ShouldNotSeeThat";

		private DummyStaticLegacyJson()
		{ }

		public DummyStaticLegacyJson(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		/// <summary>Méthode static utilisée pour sérialiser un objet</summary>
		/// <param name="instance"></param>
		/// <param name="writer"></param>
		public static void JsonSerialize(DummyStaticLegacyJson instance, CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);
			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(instance.m_secret) + " }");
		}

		/// <summary>Méthode statique utilisée pour désérialiser un objet</summary>
		public static DummyStaticLegacyJson JsonDeserialize(JsonObject value, ICrystalJsonTypeResolver resolver)
		{
			Assert.That(value, Is.Not.Null, "value");

			// doit contenir une string "custom"
			var customString = value.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyStaticLegacyJson(customString);

		}

		#endregion
	}

	public sealed record DummyStaticCustomJson : IJsonSerializable, IJsonDeserializable<DummyStaticCustomJson>
	{
		public string DontCallThis => "ShouldNotSeeThat";

		public DummyStaticCustomJson(string secret)
		{
			m_secret = secret;
		}

		private string m_secret;

		public string GetSecret() { return m_secret; }

		#region IJsonSerializable Members

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			Assert.That(writer, Is.Not.Null);
			Assert.That(writer.Settings, Is.Not.Null);
			Assert.That(writer.Resolver, Is.Not.Null);

			writer.WriteRaw("{ \"custom\":" + JsonEncoding.Encode(m_secret) + " }");
		}

		static DummyStaticCustomJson IJsonDeserializable<DummyStaticCustomJson>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? _)
		{
			Assert.That(value, Is.Not.Null);

			var customString = value.Get<string>("custom", message: "Missing 'custom' value for DummyCustomJson");
			return new DummyStaticCustomJson(customString);
		}

		#endregion
	}

	[DataContract]
	class DummyDataContractClass
	{
		[DataMember(Name = "Id")]
		public int AgentId;

		[DataMember]
		public string Name;

		[DataMember]
		public int Age;

		[DataMember(Name = "IsFemale")]
		public bool Female;

		// no attributes!
		public string InvisibleField;

		[DataMember]
		public string CurrentLoveInterest { get; set; }

		[DataMember]
		public string VisibleProperty => "CanBeSeen";

		// no attributes!
		public string InvisibleProperty => "ShouldNotBeSeen";
	}

#pragma warning disable 649
	class DummyXmlSerializableContractClass
	{
		[XmlAttribute(AttributeName = "Id")]
		public int AgentId;

		public string Name;

		public int Age;

		[XmlElement(ElementName = "IsFemale")]
		public bool Female;

		[XmlIgnore]
		public string InvisibleField;

		public string CurrentLoveInterest { get; set; }

		public string VisibleProperty => "CanBeSeen";

		[XmlIgnore]
		public string InvisibleProperty => "ShouldNotBeSeen";
	}
#pragma warning restore 649

	public sealed class DummyCrystalJsonTextPropertyNames
	{
		// test that we recognize our own JsonPropertyAttribute

		[JsonProperty("helloWorld")]
		public string? HelloWorld { get; set; }

		[JsonProperty("bar")]
		public string? Foo { get; set; }
	}

	public sealed class DummySystemJsonTextPropertyNames
	{
		// test that we recognize System.Text.Json.Serialization.JsonPropertyNameAttribute

		[STJ.Serialization.JsonPropertyName("helloWorld")]
		public string? HelloWorld { get; set; }

		[STJ.Serialization.JsonPropertyName("bar")]
		public string? Foo { get; set; }
	}

	public sealed class DummyNewtonsoftJsonPropertyNames
	{
		// test that we recognize Newtonsoft.Json.JsonPropertyAttribute

		[NJ.JsonProperty("helloWorld")]
		public string? HelloWorld { get; set; }

		[NJ.JsonProperty("bar")]
		public string? Foo { get; set; }
	}

}
