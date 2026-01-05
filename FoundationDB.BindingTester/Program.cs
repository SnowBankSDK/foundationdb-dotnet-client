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

namespace FoundationDB.Client.Testing
{
	using System.IO;
	using FoundationDB.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;

	public static class Program
	{

		public static async Task Main(string[] args)
		{

			var a = AppContext.BaseDirectory;
			var b = AppContext.TargetFrameworkName;
			AppContext.GetData("hello");

			using var cts = new CancellationTokenSource();
			var ct = cts.Token;

			var builder = Host.CreateApplicationBuilder(args);
			builder.Services.AddFoundationDb(710);
			var host = builder.Build();

			var dbProvider = host.Services.GetRequiredService<IFdbDatabaseProvider>();

			var db = await dbProvider.GetDatabase(ct);

			{
				var test = TestSuiteBuilder
					.ParseTestDump(MapPathRelativeToProject("./Dumps/test_api.txt"))
					.Build();

				using (var tester = new StackMachine(db, test.Prefix, host.Services.GetRequiredService<ILogger<StackMachine>>()))
				{
					await tester.Run(test, ct);
				}
			}

			Console.WriteLine("Done!");
		}

		public static string MapPathRelativeToProject(string path, [CallerFilePath] string callerPath = "")
		{
			string folder = Path.GetDirectoryName(callerPath)!;
			return Path.GetFullPath(Path.Combine(folder, path));
		}

	}

}
