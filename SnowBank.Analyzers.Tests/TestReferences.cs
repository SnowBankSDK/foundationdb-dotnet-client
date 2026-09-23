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

	/// <summary>Reference sets for the test compilations.</summary>
	internal static class TestReferences
	{

		/// <summary>.NET 10 reference assemblies (the library builds referenced below target net10.0).</summary>
		public static ReferenceAssemblies Net100 { get; } = ReferenceAssemblies.Net.Net100;

		/// <summary>.NET 10 plus the ASP.NET Core shared framework (minimal APIs, dependency injection).</summary>
		public static ReferenceAssemblies WithAspNetCore { get; } = ReferenceAssemblies.Net.Net100
			.AddPackages([ new PackageIdentity("Microsoft.AspNetCore.App.Ref", "10.0.12") ]);

		/// <summary>SnowBank.Core, FoundationDB.Client, and their non-framework dependencies found next to the test assembly.</summary>
		public static ImmutableArray<MetadataReference> Libraries { get; } = LoadLibraries();

		private static ImmutableArray<MetadataReference> LoadLibraries()
		{
			Assembly[] roots = [ typeof(Slice).Assembly, typeof(FoundationDB.Client.IFdbDatabase).Assembly ];
			var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var root in roots)
			{
				paths.Add(root.Location);
				foreach (var name in root.GetReferencedAssemblies())
				{
					var candidate = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
					if (File.Exists(candidate)) paths.Add(candidate);
				}
			}
			return [ ..paths.Order().Select(p => (MetadataReference) MetadataReference.CreateFromFile(p)) ];
		}

		/// <summary>Root folder of the FoundationDB repository, written by MSBuild at build time.</summary>
		public static string RepositoryRoot { get; } = Path.GetFullPath(typeof(TestReferences).Assembly
			.GetCustomAttributes<AssemblyMetadataAttribute>()
			.Single(a => a.Key == "RepositoryRoot").Value!);

	}
}
