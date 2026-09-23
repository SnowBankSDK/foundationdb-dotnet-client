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

namespace SnowBank.Analyzers.Tests
{
	using System.Reflection;
	using System.Text.RegularExpressions;

	[TestFixture]
	public class DescriptorFacts
	{

		private static IEnumerable<DiagnosticDescriptor> AllDescriptors() => SbkDescriptors.All.Concat(FdbDescriptors.All);

		[Test]
		public void Ids_Are_Unique()
		{
			var ids = AllDescriptors().Select(d => d.Id).ToList();
			Assert.That(ids, Is.Unique);
		}

		[Test]
		public void Each_Descriptor_Follows_The_Tier_Contract()
		{
			foreach (var d in AllDescriptors())
			{
				var match = Regex.Match(d.Id, "^(SBK|FDB)([0-2])[0-9]{3}$");
				Assert.That(match.Success, Is.True, $"{d.Id}: id format");
				string prefix = match.Groups[1].Value;
				char tier = match.Groups[2].Value[0];

				Assert.That(d.DefaultSeverity, Is.EqualTo(tier == '0' ? DiagnosticSeverity.Error : DiagnosticSeverity.Warning), $"{d.Id}: severity");
				// literal names: the shared category constants are internal to each analyzer assembly
				string expectedCategory = (prefix, tier) switch
				{
					("SBK", '2') => "SnowBankPerformance",
					("SBK", _) => "SnowBankCorrectness",
					("FDB", '2') => "FdbPerformance",
					_ => "FdbCorrectness",
				};
				Assert.That(d.Category, Is.EqualTo(expectedCategory), $"{d.Id}: category");
				Assert.That(d.IsEnabledByDefault, Is.True, $"{d.Id}: enabled by default");
				Assert.That(d.HelpLinkUri, Does.Match($"^https://github\\.com/SnowBankSDK/foundationdb-dotnet-client/blob/[^/+]+/Documentation/analyzers/{d.Id}\\.md$"), $"{d.Id}: help link");
				Assert.That(d.MessageFormat.ToString(), Does.Not.Contain("http"), $"{d.Id}: no url in the message");
				Assert.That(d.MessageFormat.ToString(), Does.Not.Contain("—"), $"{d.Id}: no em dash");
			}
		}

		[Test]
		public void Each_Descriptor_Has_A_Documentation_Page()
		{
			foreach (var d in AllDescriptors())
			{
				string page = Path.Combine(TestReferences.RepositoryRoot, "Documentation", "analyzers", d.Id + ".md");
				Assert.That(File.Exists(page), Is.True, $"{d.Id}: missing {page}");
			}
		}

		[Test]
		public void Help_Link_Uses_The_Package_Version()
		{
			// VersionInfo.props sets the same version on every project of the repository
			string version = typeof(DescriptorFacts).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
			foreach (var d in AllDescriptors())
			{
				Assert.That(d.HelpLinkUri, Does.Contain($"/blob/{version}/"), $"{d.Id}: help link version");
			}
		}

	}
}
