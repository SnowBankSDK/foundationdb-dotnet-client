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

// This file IS compiled for the net472 validation target: same reason as Xml/AcmeDtos.cs (the generated XML code
// compiles on the lite path, so the end-to-end simulation is validated on the .NET Framework CLR too).

namespace SnowBank.Data.Xml.Tests.Acme.Simulation
{
	using System.Xml;
	using System.Xml.Linq;
	using System.Xml.Xsl;
	using NUnit.Framework;
	using SnowBank.Data.Xml.Tests;

	/// <summary>
	/// Stage-A Acme end-to-end simulation: proves the whole chain on a realistic, account-sized DTO graph
	/// (<see cref="ClientAccount"/>) -- the generated CrystalXml DataContract-compat wire, all five output sinks
	/// agreeing with one another, and an XSLT transform (using the exact XPath patterns measured in the real corpus)
	/// rendering IDENTICAL HTML from the CrystalXml wire and from the live-DCS reference wire, including the nil-guard
	/// behavior on an all-null account.
	/// </summary>
	/// <remarks>Write-only: there is no FromXml in the public surface. The XSLT transform's input is the produced
	/// text, read back through <see cref="XmlReader"/> -- test-side consumption of CrystalXml's own output, not a
	/// round-trip deserializer.</remarks>
	[TestFixture]
	[Category("Core-SDK")]
	[Category("Core-XML")]
	public sealed class AcmeRenderFacts : SimpleTest
	{

		#region Fixture data...

		private static ClientAccount MakePopulatedAccount() => new()
		{
			AccountId = "ACC-0001",
			AccountNumber = "ZZ00-TEST-0000-0000-0000-000",
			OwnerName = "Jean Dupont",
			Nickname = "jdupont",
			OpenedDate = new DateTime(2018, 3, 12, 0, 0, 0, DateTimeKind.Utc),
			ClosedDate = null,
			Status = AccountStatus.Active,
			CreditLimit = 5000.00m,
			IsPremium = true,
			Balance = 12345.67m,
			Currency = "EUR",
			InterestRate = 0.015m,
			OverdraftLimit = 500.00m,
			TagGroups =
			[
				["vip", "long-term"],
				["mobile-app"],
			],
			Loans =
			[
				new Loan { LoanId = "L-1", Amount = 10000.00m, IsLate = false, DueDate = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
				new Loan { LoanId = "L-2", Amount = 2500.00m, IsLate = true, DueDate = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc) },
				new Loan { LoanId = "L-3", Amount = 750.00m, IsLate = false, DueDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
			],
			Services =
			[
				new Service { ServiceId = "S-1", Name = "Basic Access" },
				new Subscription { ServiceId = "S-2", Name = "Premium Newsletter", MonthlyFee = 4.99m },
				new InsuranceService { ServiceId = "S-3", Name = "Card Protection", CoverageAmount = 1000.00m },
			],
			ContactEmails = ["jean.dupont@example.com", "jd.backup@example.com"],
			PhoneNumbers = ["+33 1 23 45 67 89", "+33 6 12 34 56 78"],
			Addresses =
			[
				new Address { Street = "12 rue de la Paix", City = "Paris", PostalCode = "75002", Country = "FR" },
			],
			PrimaryAddress = new Address { Street = "12 rue de la Paix", City = "Paris", PostalCode = "75002", Country = "FR" },
			Metadata = new() { ["segment"] = "retail", ["channel"] = "branch" },
			AdditionalProperties = MakePropertyBag(),
			LastLoginAt = new DateTime(2026, 8, 1, 9, 30, 0, DateTimeKind.Utc),
			RiskScore = 0.12,
			Notes = "Long-standing client, no incidents.",
			ReferralCode = "REF-42",
			Beneficiaries = ["Marie Dupont"],
			SecurityQuestions = new() { ["mother_maiden_name"] = "Martin" },
			PreferredLanguage = "fr-FR",
			MarketingOptIn = false,
			ExternalRefs = [1001, 1002, 1003],
			AccountManagerId = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e"),
			CountryCode = "FR",
			TimeZone = "Europe/Paris",
			LastStatementDate = new DateTime(2026, 7, 31, 0, 0, 0, DateTimeKind.Utc),
		};

		private static AccountPropertyBag MakePropertyBag()
		{
			var bag = new AccountPropertyBag();
			bag.Add("origin", "acme-main");
			bag.Add("channel", "web");
			return bag;
		}

		/// <summary>Same type, every member null: the shape used to pin nil-guard parity between the two wires.</summary>
		private static ClientAccount MakeAllNullAccount() => new();

		#endregion

		#region The five outputs agree on the account graph...

		[Test]
		public void Test_All_Five_Outputs_Agree_On_The_Populated_Account()
		{
			var account = MakePopulatedAccount();
			AssertFiveOutputsAgree(account);
		}

		[Test]
		public void Test_All_Five_Outputs_Agree_On_The_All_Null_Account()
		{
			var account = MakeAllNullAccount();
			AssertFiveOutputsAgree(account);
		}

		private static void AssertFiveOutputsAgree(ClientAccount account)
		{
			string text = AcmeAccountSerializers.ClientAccount.ToXmlText(account);

			var slice = AcmeAccountSerializers.ClientAccount.ToXmlSlice(account);
			byte[] bytes = AcmeAccountSerializers.ClientAccount.ToXmlBytes(account);

			using (var ms = new MemoryStream())
			{
				AcmeAccountSerializers.ClientAccount.WriteXmlTo(ms, account);
				Assert.That(Encoding.UTF8.GetString(ms.ToArray()), Is.EqualTo(text), "WriteXmlTo(Stream), UTF-8 decoded");
			}

			using (var sw = new StringWriter())
			{
				AcmeAccountSerializers.ClientAccount.WriteXmlTo(sw, account);

				using (Assert.EnterMultipleScope())
				{
					Assert.That(slice.ToStringUtf8(), Is.EqualTo(text), "ToXmlSlice, UTF-8 decoded");
					Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(text), "ToXmlBytes, UTF-8 decoded");
					Assert.That(sw.ToString(), Is.EqualTo(text), "WriteXmlTo(TextWriter)");
				}
			}

			XDocument fromText = XDocument.Parse(text);

			XDocument doc = AcmeAccountSerializers.ClientAccount.ToXDocument(account);
			Assert.That(XNode.DeepEquals(doc, fromText), Is.True, "ToXDocument deep-equals the parsed text, as a tree");

			var sb = new StringBuilder();
			var xmlWriterSettings = new XmlWriterSettings { OmitXmlDeclaration = true };
			using (var xmlWriter = XmlWriter.Create(sb, xmlWriterSettings))
			{
				AcmeAccountSerializers.ClientAccount.WriteXmlTo(xmlWriter, account);
			}
			Assert.That(XNode.DeepEquals(XDocument.Parse(sb.ToString()), fromText), Is.True, "WriteXmlTo(XmlWriter) deep-equals the parsed text, as a tree");
		}

		#endregion

		#region The XSLT renders identical HTML from both wires...

		private static XslCompiledTransform LoadAccountExportXslt()
		{
			var assembly = typeof(AcmeRenderFacts).Assembly;
			const string resourceName = "SnowBank.Core.Tests.Xml.AcmeSimulation.account-export.xslt";
			using var stream = assembly.GetManifestResourceStream(resourceName)
				?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");
			using var reader = XmlReader.Create(stream);
			var xslt = new XslCompiledTransform();
			xslt.Load(reader);
			return xslt;
		}

		private static string Render(XslCompiledTransform xslt, string xml)
		{
			using var reader = XmlReader.Create(new StringReader(xml));
			var sb = new StringBuilder();
			using var writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true, ConformanceLevel = ConformanceLevel.Fragment });
			xslt.Transform(reader, writer);
			return sb.ToString();
		}

		[Test]
		public void Test_Xslt_Renders_Expected_Fragments_For_The_Populated_Account()
		{
			var xslt = LoadAccountExportXslt();
			string xml = AcmeAccountSerializers.ClientAccount.ToXmlText(MakePopulatedAccount());
			string html = Render(xslt, xml);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(html, Does.Contain("Jean Dupont"));
				Assert.That(html, Does.Contain("Loans: 3"));
				Assert.That(html, Does.Contain("Late: 1"));
				Assert.That(html, Does.Contain("OnTime: 2"));
				Assert.That(html, Does.Contain("TagGroups: 2"));
				// note: XPath 1.0 node-set/string "!=" is an existential comparison: an item with NO @type attribute
				// (the exact base Service) has an empty attribute node-set, and no node in an empty set can satisfy
				// "!= 'InsuranceService'", so it does NOT count here -- only items that carry a type attribute whose
				// value differs do. Measured identically on both wires by the parity tests below (both render "1").
				Assert.That(html, Does.Contain("NonInsuranceServices: 1"));
				// positive counterpart: exactly one Service item carries @type = 'InsuranceService' (the InsuranceService instance).
				Assert.That(html, Does.Contain("InsuranceServices: 1"));
			}
		}

		[Test]
		public void Test_Xslt_Renders_Expected_Fragments_For_The_All_Null_Account()
		{
			var xslt = LoadAccountExportXslt();
			string xml = AcmeAccountSerializers.ClientAccount.ToXmlText(MakeAllNullAccount());
			string html = Render(xslt, xml);

			using (Assert.EnterMultipleScope())
			{
				Assert.That(html, Does.Contain("(none)"));
				Assert.That(html, Does.Contain("Loans: none"));
				Assert.That(html, Does.Contain("TagGroups: none"));
				Assert.That(html, Does.Contain("NonInsuranceServices: none"));
				Assert.That(html, Does.Contain("InsuranceServices: none"));
			}
		}

		/// <summary>
		/// The nil-guard parity proof: transforms the SAME account through both the CrystalXml wire and the live DCS
		/// reference wire, and asserts the two HTML renderings are equal. Any divergence surfaced here is a genuine
		/// wire-shape mismatch between the two pipelines, not an artifact of this test's own expectations.
		/// </summary>
		[Test]
		public void Test_Xslt_Render_Parity_With_Live_Dcs_Populated_Account()
		{
			var xslt = LoadAccountExportXslt();
			var account = MakePopulatedAccount();

			string crystalXmlWire = AcmeAccountSerializers.ClientAccount.ToXmlText(account);
			string dcsWire = ReferenceDcsWire.Serialize(account, typeof(ClientAccount));

			// direct wire assertion: this DTO graph uses the compat profile only (no AccountPropertyBag member is
			// exercised through the ISerializable path in this specific fixture instance's populated members that
			// differ from the DCS-native shape), so the whole document should be byte-for-byte identical, not merely
			// HTML-equivalent after the lossy XSLT projection below.
			Assert.That(crystalXmlWire, Is.EqualTo(dcsWire),
				$"CrystalXml wire:\n{crystalXmlWire}\n\nDCS wire:\n{dcsWire}");

			string crystalXmlHtml = Render(xslt, crystalXmlWire);
			string dcsHtml = Render(xslt, dcsWire);

			Assert.That(crystalXmlHtml, Is.EqualTo(dcsHtml),
				$"CrystalXml wire:\n{crystalXmlWire}\n\nDCS wire:\n{dcsWire}\n\nCrystalXml HTML:\n{crystalXmlHtml}\n\nDCS HTML:\n{dcsHtml}");
		}

		[Test]
		public void Test_Xslt_Render_Parity_With_Live_Dcs_All_Null_Account()
		{
			var xslt = LoadAccountExportXslt();
			var account = MakeAllNullAccount();

			string crystalXmlWire = AcmeAccountSerializers.ClientAccount.ToXmlText(account);
			string dcsWire = ReferenceDcsWire.Serialize(account, typeof(ClientAccount));

			// direct wire assertion: see the populated-account counterpart above.
			Assert.That(crystalXmlWire, Is.EqualTo(dcsWire),
				$"CrystalXml wire:\n{crystalXmlWire}\n\nDCS wire:\n{dcsWire}");

			string crystalXmlHtml = Render(xslt, crystalXmlWire);
			string dcsHtml = Render(xslt, dcsWire);

			Assert.That(crystalXmlHtml, Is.EqualTo(dcsHtml),
				$"CrystalXml wire:\n{crystalXmlWire}\n\nDCS wire:\n{dcsWire}\n\nCrystalXml HTML:\n{crystalXmlHtml}\n\nDCS HTML:\n{dcsHtml}");
		}

		#endregion

	}

}

