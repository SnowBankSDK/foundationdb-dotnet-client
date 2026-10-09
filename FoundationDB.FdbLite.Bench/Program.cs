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

namespace FoundationDB.FdbLite.Bench
{
	using System.Globalization;

	/// <summary>Command line of the harness.</summary>
	public sealed class BenchOptions
	{

		public static readonly string[] KnownEngines = [ "fdblite", "fdblite-db", "fdblite-mem", "fakedb" ];

		public string Mode { get; set; } = "verify";

		public Suite.Scale Scale { get; set; } = Suite.Scale.Smoke;

		/// <summary>Substring filter on the workload name, empty for the whole matrix.</summary>
		public string Filter { get; set; } = "";

		public string[] Engines { get; set; } = [ "fdblite", "fdblite-db", "fakedb" ];

		/// <summary>Page size as a power of two, 12 to 16. 15 is the shipped 32 KiB default.</summary>
		public int PageLog2 { get; set; } = 15;

		/// <summary>Timed passes per workload and engine.</summary>
		public int Repeats { get; set; } = 3;

		/// <summary>Ceiling on warm-up passes per workload and engine.</summary>
		public int MaxWarmup { get; set; } = 3;

		/// <summary>Folder of the store files.</summary>
		public string Dir { get; set; } = Path.Combine(Path.GetTempPath(), "fdblite-bench");

		/// <summary>JSON file written by the measure mode.</summary>
		public string? Out { get; set; }

		/// <summary>JSON file of an earlier run to compare against.</summary>
		public string? Compare { get; set; }

		/// <summary>Per-operation latency histograms (costly: two timestamps per operation).</summary>
		public bool Latency { get; set; }

		/// <summary>Files named by the positional arguments of the <c>compare</c> mode.</summary>
		public List<string> Positional { get; } = [];

		public FdbLiteGeometry Geometry => FdbLiteGeometry.Uniform(this.PageLog2);

		/// <summary>Parses the arguments. Returns null, with the reason, when they are invalid.</summary>
		public static BenchOptions? Parse(string[] args, out string error)
		{
			var o = new BenchOptions();
			error = "";
			int positional = 0;
			for (int i = 0; i < args.Length; i++)
			{
				string a = args[i];
				string Next()
				{
					if (i + 1 >= args.Length) throw new ArgumentException($"{a} needs a value");
					return args[++i];
				}
				try
				{
					switch (a)
					{
						case "--engines": o.Engines = Next().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries); break;
						case "--page-log2": o.PageLog2 = int.Parse(Next(), CultureInfo.InvariantCulture); break;
						case "--repeats": o.Repeats = int.Parse(Next(), CultureInfo.InvariantCulture); break;
						case "--warmup": o.MaxWarmup = int.Parse(Next(), CultureInfo.InvariantCulture); break;
						case "--dir": o.Dir = Next(); break;
						case "--out": o.Out = Next(); break;
						case "--compare": o.Compare = Next(); break;
						case "--latency": o.Latency = true; break;
						default:
							if (a.StartsWith("--", StringComparison.Ordinal)) { error = $"unknown option {a}"; return null; }
							if (o.Mode == "compare") { o.Positional.Add(a); break; }
							switch (positional++)
							{
								case 0: o.Mode = a; break;
								case 1:
									if (!Enum.TryParse<Suite.Scale>(a, true, out var scale)) { error = $"unknown scale '{a}' (smoke, standard, large)"; return null; }
									o.Scale = scale;
									break;
								case 2: o.Filter = a; break;
								default: error = $"unexpected argument {a}"; return null;
							}
							break;
					}
				}
				catch (Exception e) when (e is ArgumentException or FormatException)
				{
					error = e.Message;
					return null;
				}
			}
			if (o.Mode is not ("verify" or "measure" or "profile" or "list" or "compare")) { error = $"unknown mode '{o.Mode}'"; return null; }
			if (o.PageLog2 is < 12 or > 16) { error = "--page-log2 must be between 12 and 16"; return null; }
			if (o.Repeats < 1) { error = "--repeats must be at least 1"; return null; }
			if (o.MaxWarmup < 0) { error = "--warmup must be 0 or more"; return null; }
			if (o.Engines.Length == 0) { error = "--engines needs at least one engine"; return null; }
			foreach (var e in o.Engines)
			{
				if (Array.IndexOf(KnownEngines, e) < 0) { error = $"unknown engine '{e}' ({string.Join(", ", KnownEngines)})"; return null; }
			}
			if (o.Mode == "compare" && o.Positional.Count != 2) { error = "compare needs two JSON files: compare old.json new.json"; return null; }
			return o;
		}

		/// <summary>Workloads of the matrix at the selected scale, after the name filter.</summary>
		public List<Workload> Loads()
		{
			var loads = Suite.Build(this.Scale);
			return string.IsNullOrEmpty(this.Filter)
				? loads.ToList()
				: loads.Where(w => w.Name.Contains(this.Filter, StringComparison.OrdinalIgnoreCase)).ToList();
		}

		/// <summary>Opens a fresh store of the given engine under the scratch folder.</summary>
		public IKvDriver Open(string engine, string tag)
		{
			Directory.CreateDirectory(this.Dir);
			string path = Path.Combine(this.Dir, $"{engine}-{tag}.fdblite");
			return engine switch
			{
				"fdblite" => new FdbLiteEngineDriver(path, this.Geometry),
				"fdblite-db" => BindingDriver.ForFdbLite(path, this.Geometry),
				"fdblite-mem" => BindingDriver.ForFdbLiteMem(this.Geometry),
				"fakedb" => BindingDriver.ForFakeDb(),
				_ => throw new ArgumentException($"unknown engine '{engine}'"),
			};
		}

		/// <summary>The lines every mode prints first, so a result file or a console capture states its conditions.</summary>
		public void PrintHeader(int loadCount)
		{
			Console.WriteLine($"# fdblite bench {typeof(Program).Assembly.GetName().Version}: {this.Mode}, scale {this.Scale}, {loadCount} workloads, engines {string.Join(",", this.Engines)}");
			Console.WriteLine($"# page size {(1 << this.PageLog2) / 1024} KiB, pre-commit consolidation pinned Off (file stores default to Adaptive), store files under {this.Dir}");
			Console.WriteLine($"# {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, {System.Runtime.InteropServices.RuntimeInformation.OSDescription}, {Environment.ProcessorCount} cores, {(Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") == "0" ? "tiered compilation OFF (not production code)" : "tiered compilation on")}");
		}

	}

	public static class Program
	{

		public static int Main(string[] args)
		{
			var options = BenchOptions.Parse(args, out var error);
			if (options is null)
			{
				Console.Error.WriteLine($"error: {error}");
				return Usage();
			}
			try
			{
				return options.Mode switch
				{
					"verify" => VerifyMode.Run(options),
					"measure" => MeasureMode.Run(options),
					"profile" => ProfileMode.Run(options),
					"list" => List(options),
					"compare" => BenchReport.Compare(options.Positional[0], options.Positional[1]),
					_ => Usage(),
				};
			}
			catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
			{
				Console.Error.WriteLine($"error: {e.Message}");
				return 2;
			}
		}

		private static int Usage()
		{
			Console.WriteLine("usage: FoundationDB.FdbLite.Bench [verify|measure|profile|list] [smoke|standard|large] [name-filter] [options]");
			Console.WriteLine("       FoundationDB.FdbLite.Bench compare old.json new.json");
			Console.WriteLine("options:");
			Console.WriteLine("  --engines a,b,c    engines to run: fdblite, fdblite-db, fdblite-mem, fakedb (default fdblite,fdblite-db,fakedb)");
			Console.WriteLine("  --page-log2 N      page size as a power of two, 12 to 16 (default 15 = 32 KiB)");
			Console.WriteLine("  --repeats N        timed passes per workload and engine (default 3)");
			Console.WriteLine("  --warmup N         ceiling on warm-up passes (default 3)");
			Console.WriteLine("  --dir path         folder of the store files (default <temp>/fdblite-bench)");
			Console.WriteLine("  --out file.json    write the measure results to this file");
			Console.WriteLine("  --compare old.json print the delta against an earlier measure file");
			Console.WriteLine("  --latency          per-operation latency histograms (slower; the shape of the distribution)");
			Console.WriteLine("exit codes: 0 ok, 1 a verify check failed or an engine threw, 2 usage or I/O error");
			return 2;
		}

		private static int List(BenchOptions options)
		{
			foreach (var w in options.Loads())
			{
				Console.WriteLine($"{w.Family,-8} {w.Name,-30} {w.Ops,10:N0} ops   {w.Shape}");
			}
			return 0;
		}

	}

}
