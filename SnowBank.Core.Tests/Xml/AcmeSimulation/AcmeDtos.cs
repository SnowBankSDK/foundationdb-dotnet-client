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

// This file is not compiled for the net472 validation target: same reason as Xml/DcsProbes.cs (it drives the
// CrystalXml source generator as an analyzer and exercises generated code only produced for the runtime targets).
#if !NETFRAMEWORK

// note: this is a stage-A end-to-end simulation of "Acme" (pseudonym for the consuming application whose back-office
// XSLT layer reads the DCS XML wire): a realistic ClientAccount-shaped DTO graph, big enough (30+ members, nested
// collections, nullable members, an ISerializable key-flattening wrapper, a [KnownType]/[JsonDerivedType] polymorphic
// hierarchy) to exercise the whole chain -- generated wire, all output sinks, and an XSLT transform -- at once. Naming
// is Acme-neutral: generic domain vocabulary only (Account, Loan, Service, Subscription, ...), no real customer or
// product names. Every member is declared as a nullable type (including value types, via T?) so a single instance can
// legitimately have every member absent at once: that all-null shape is what AcmeRenderFacts uses to pin the nil-guard
// parity between the CrystalXml wire and the live DCS reference wire.
namespace SnowBank.Data.Xml.Tests.Acme.Simulation
{
	using System.Runtime.Serialization;
	using System.Text.Json.Serialization;
	using SnowBank.Data.Json;
	using SnowBank.Data.Xml;

	#region Value types...

	[DataContract]
	public sealed class Address
	{
		[DataMember] public string? Street;
		[DataMember] public string? City;
		[DataMember] public string? PostalCode;
		[DataMember] public string? Country;
	}

	[DataContract]
	public sealed class Loan
	{
		[DataMember] public string? LoanId;
		[DataMember] public decimal? Amount;
		[DataMember] public bool? IsLate;
		[DataMember] public DateTime? DueDate;
	}

	public enum AccountStatus { Active = 0, Suspended = 1, Closed = 2 }

	#endregion

	#region Polymorphic Service hierarchy...

	// note: mirrors the DcsProbes.cs CatalogItem/AudioBook/PrintedBook dual-attribute pattern: [KnownType] is what the
	// LIVE DataContractSerializer oracle needs to resolve a derived instance sitting in a base-declared member or
	// collection; [JsonDerivedType] is what the CrystalXml generator's own polymorphic map is driven by instead. Both
	// are kept, one per consumer.
	[DataContract]
	[KnownType(typeof(Subscription))]
	[KnownType(typeof(InsuranceService))]
	[JsonDerivedType(typeof(Subscription), "subscription")]
	[JsonDerivedType(typeof(InsuranceService), "insurance")]
	public class Service
	{
		[DataMember] public string? ServiceId;
		[DataMember] public string? Name;
	}

	[DataContract]
	public sealed class Subscription : Service
	{
		[DataMember] public decimal? MonthlyFee;
	}

	[DataContract]
	public sealed class InsuranceService : Service
	{
		[DataMember] public decimal? CoverageAmount;
	}

	#endregion

	#region The ISerializable key-flattening AdditionalProperties wrapper...

	/// <summary>
	/// Clean-room equivalent of the measured key-flattening pattern (same dialect as DcsProbes.cs' <c>KeyedBag&lt;T&gt;</c>):
	/// an <see cref="ISerializable"/> wrapper whose <c>GetObjectData</c> turns each dictionary KEY into a
	/// <see cref="SerializationInfo"/> entry name, so the reference wire emits one element PER KEY with a type
	/// discriminator on the value.
	/// </summary>
	[Serializable]
	public sealed class AccountPropertyBag : ISerializable
	{
		private readonly Dictionary<string, string> inner = [];

		public AccountPropertyBag() { }

		public void Add(string key, string value) => this.inner.Add(key, value);

		void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
		{
			foreach (var (key, value) in this.inner)
			{
				info.AddValue(key, value);
			}
		}
	}

	#endregion

	#region The account graph...

	/// <summary>Realistic, Acme-neutral account DTO: 30+ members, nested collections, nullable members throughout, an
	/// ISerializable key-flattening wrapper, and a polymorphic collection.</summary>
	[DataContract]
	public sealed class ClientAccount
	{
		[DataMember] public string? AccountId;
		[DataMember] public string? AccountNumber;
		[DataMember] public string? OwnerName;
		[DataMember] public string? Nickname;
		[DataMember] public DateTime? OpenedDate;
		[DataMember] public DateTime? ClosedDate;
		[DataMember] public AccountStatus? Status;
		[DataMember] public decimal? CreditLimit;
		[DataMember] public bool? IsPremium;
		[DataMember] public decimal? Balance;
		[DataMember] public string? Currency;
		[DataMember] public decimal? InterestRate;
		[DataMember] public decimal? OverdraftLimit;

		/// <summary>Nested collection: each group is itself a list, so the wire names each inner list after its own
		/// item type (<c>ArrayOfstring</c>) rather than after this member -- the measured "ArrayOf*" corpus pattern.</summary>
		[DataMember] public List<List<string>>? TagGroups;

		[DataMember] public List<Loan>? Loans;

		/// <summary>Polymorphic collection: base <see cref="Service"/> items carry no discriminator, derived items
		/// (<see cref="Subscription"/>, <see cref="InsuranceService"/>) carry a <c>type</c> attribute -- the measured
		/// "Service[@type != '...']" corpus pattern.</summary>
		[DataMember] public List<Service>? Services;

		[DataMember] public List<string>? ContactEmails;
		[DataMember] public string[]? PhoneNumbers;
		[DataMember] public List<Address>? Addresses;
		[DataMember] public Address? PrimaryAddress;
		[DataMember] public Dictionary<string, string>? Metadata;
		[DataMember] public AccountPropertyBag? AdditionalProperties;
		[DataMember] public DateTime? LastLoginAt;
		[DataMember] public double? RiskScore;
		[DataMember] public string? Notes;
		[DataMember] public string? ReferralCode;
		[DataMember] public List<string>? Beneficiaries;
		[DataMember] public Dictionary<string, string>? SecurityQuestions;
		[DataMember] public string? PreferredLanguage;
		[DataMember] public bool? MarketingOptIn;
		[DataMember] public List<int>? ExternalRefs;
		[DataMember] public Guid? AccountManagerId;
		[DataMember] public string? CountryCode;
		[DataMember] public string? TimeZone;
		[DataMember] public DateTime? LastStatementDate;
	}

	#endregion

	#region Test container...

	[CrystalJsonConverter(CrystalJsonSerializerDefaults.DataContractCompat)]
	[CrystalXmlOutput]
	[CrystalJsonSerializable(typeof(ClientAccount))]
	[CrystalJsonSerializable(typeof(Address))]
	[CrystalJsonSerializable(typeof(Loan))]
	[CrystalJsonSerializable(typeof(Service))]
	[CrystalJsonSerializable(typeof(Subscription))]
	[CrystalJsonSerializable(typeof(InsuranceService))]
	public static partial class AcmeAccountSerializers
	{
	}

	#endregion

}

#endif
