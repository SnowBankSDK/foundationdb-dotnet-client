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

	[TestFixture]
	public class DatabaseInjectionFacts
	{

		private static DiagnosticResult Expected(int location) => Verify.Diagnostic(FdbDescriptors.DatabaseInjection).WithLocation(location);

		private static Task Analyze(string source, params DiagnosticResult[] expected)
			=> Verify.Analyzer<DatabaseInjectionAnalyzer>(source, TestReferences.WithAspNetCore, OutputKind.ConsoleApplication, expected);

		private const string Usings = """
			using System.Threading;
			using System.Threading.Tasks;
			using FoundationDB.Client;
			using FoundationDB.DependencyInjection;
			using Microsoft.AspNetCore.Builder;
			using Microsoft.AspNetCore.Mvc;
			using Microsoft.Extensions.DependencyInjection;
			using Microsoft.Extensions.Hosting;

			""";

		[Test]
		public Task Reports_A_Minimal_Api_Parameter() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapGet("/version", async ({|#0:IFdbDatabase db|}, CancellationToken ct) => await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct));
			app.Run();
			""", Expected(0));

		[Test]
		public Task Reports_GetRequiredService() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			var db = {|#0:app.Services.GetRequiredService<IFdbDatabase>()|};
			app.Run();
			""", Expected(0));

		[Test]
		public Task Reports_A_FromServices_Parameter() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapPost("/items", ({|#0:[FromServices] IFdbDatabase db|}) => "ok");
			app.Run();
			""", Expected(0));

		[Test]
		public Task Reports_A_Constructor_Parameter_Of_A_Registered_Service() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			builder.Services.AddHostedService<Worker>();
			builder.Build().Run();

			sealed class Worker({|#0:IFdbDatabase db|}) : BackgroundService
			{
				protected override Task ExecuteAsync(CancellationToken stoppingToken) => db.ReadAsync(tr => tr.GetReadVersionAsync(), stoppingToken);
			}
			""", Expected(0));

		[Test]
		public Task Ignores_A_Compilation_Without_AddFoundationDb() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			var app = builder.Build();
			app.MapGet("/version", async (IFdbDatabase db, CancellationToken ct) => await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct));
			app.Run();
			""");

		[Test]
		public Task Ignores_A_Compilation_That_Registers_IFdbDatabase() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			builder.Services.AddSingleton<IFdbDatabase>(sp => null!);
			var app = builder.Build();
			app.MapGet("/version", async (IFdbDatabase db, CancellationToken ct) => await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct));
			app.Run();
			""");

		[Test]
		public Task Ignores_A_Class_That_Is_Not_Registered() => Analyze(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			builder.Build().Run();

			sealed class Store(IFdbDatabase db)
			{
				public Task<long> VersionAsync(CancellationToken ct) => db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
			}
			""");

		[Test]
		public Task Fixes_A_Parameter_Used_Only_For_Retry_Loops() => Verify.CodeFixAtCompilationEnd<DatabaseInjectionAnalyzer, DatabaseInjectionCodeFix>(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapGet("/version", async ({|#0:IFdbDatabase db|}, CancellationToken ct) => await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct));
			app.Run();
			""", Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapGet("/version", async (IFdbDatabaseProvider db, CancellationToken ct) => await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct));
			app.Run();
			""", TestReferences.WithAspNetCore, OutputKind.ConsoleApplication, Expected(0));

		[Test]
		public Task Offers_No_Fix_When_The_Parameter_Uses_Other_Members() => Verify.CodeFixAtCompilationEnd<DatabaseInjectionAnalyzer, DatabaseInjectionCodeFix>(Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapGet("/name", ({|#0:IFdbDatabase db|}) => db.Name);
			app.Run();
			""", Usings + """
			var builder = WebApplication.CreateBuilder(args);
			builder.Services.AddFoundationDb(740);
			var app = builder.Build();
			app.MapGet("/name", ({|#0:IFdbDatabase db|}) => db.Name);
			app.Run();
			""", TestReferences.WithAspNetCore, OutputKind.ConsoleApplication, Expected(0));

	}
}
