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

//#define USE_LOG_FILE

// ReSharper disable MethodHasAsyncOverload

namespace FdbShell
{
	using System.CommandLine;
	using System.CommandLine.Invocation;
	using FoundationDB.DependencyInjection;
	using Spectre.Console;
	using Microsoft.Extensions.DependencyInjection;
	using Mono.Terminal;

	public static class Program
	{

		public static async Task<int> Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			// Initialize FDB

			if (args.Contains("--spawn"))
			{
				// compute a hash of the arguments, to detect if the child process is already started
				var hash = SnowBank.IO.Hashing.Fnv1aHash64.FromString(string.Join("¤", args), ignoreCase: false);


				// respawn this process in a new terminal window, with the same arguments (minus the --spawn)
				// -> this is a workaround to an issue in Aspire that, when FdbShell is started in the AppHost, it will not have a valid console (stdin/stdout)
				var process = Process.GetCurrentProcess();
				var psi = new ProcessStartInfo()
				{
					WorkingDirectory = Environment.CurrentDirectory,
					FileName = process.ProcessName,
					CreateNoWindow = false,
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Normal,
				};

				// we may be started via "dotnet FdbShell.dll --args", so we have to use the actual process args, and not the one passed to Main(...)
				var commandLineArgs = Environment.GetCommandLineArgs();

				foreach (var arg in commandLineArgs)
				{
					if (arg == "--spawn") continue;
					psi.ArgumentList.Add(arg);
				}
				// add the hashcode
				psi.ArgumentList.Add("--child=" + hash.ToString("x08", CultureInfo.InvariantCulture));
				psi.ArgumentList.Add("--parent=" + process.Id.ToString(CultureInfo.InvariantCulture));

				try
				{
					var child = Process.Start(psi)!;
					child.WaitForExit();
					Environment.ExitCode = child.ExitCode;
				}
				catch (Exception e)
				{
					Console.Error.WriteLine($"CRASHED: {e}");
					Environment.ExitCode = -1;
				}
				return 0;
			}

			try
			{
				using var go = new CancellationTokenSource();

				#region Options Parsing...

				var cmd = new FdbShellCommand(go.Token);
				var result = cmd.Parse(args);
				if (result.Errors.Count != 0)
				{
					foreach (var parseError in result.Errors)
					{
						Console.Error.WriteLine(parseError.Message);
					}
					return 1;
				}

				#endregion

				await result.InvokeAsync(cancellationToken: go.Token);

				return 0;
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("CRASH: " + e);
				return 1;
			}
			finally
			{
				Fdb.Stop();
			}
		}

		public class FdbShellCommand : RootCommand
		{

			public FdbShellCommand(CancellationToken cancel)
				: base("Hello, there!")
			{
				this.Options.Add(ClusterFileOption);
				this.Options.Add(ConnectionStringOption);
				this.Options.Add(ApiVersionOption);
				this.Options.Add(PartitionOption);
				this.Options.Add(TimeoutOption);
				this.Options.Add(RetriesOption);
				this.Options.Add(AspireOption);
				this.Options.Add(DockerOption);
				this.Options.Add(ChildHashOption);
				this.Options.Add(ParentProcessOption);
				this.Options.Add(ExecOption);

				this.SetAction((result) => RunShell(result, cancel));

				this.Cancellation = cancel;
			}

			public CancellationToken Cancellation { get; }

			private static readonly System.CommandLine.Option<string?> ClusterFileOption = new("--connfile", ["-c", "-C"])
			{
				Description = "The path of a file containing the connection string for the FoundationDB cluster.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<string?> ConnectionStringOption = new ("--connStr")
			{
				Description = "The connection string for the FoundationDB cluster.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<int?> ApiVersionOption = new("--api")
			{
				Description = "The API version level that should be used.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<string?> PartitionOption = new("--partition", [ "-p" ])
			{
				Description = "The name of the database partition to open.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<int?> TimeoutOption = new("--timeout", [ "-t" ])
			{
				DefaultValueFactory = (_) => 30,
				Description = "Default timeout (in seconds) for failed transactions.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<int?> RetriesOption = new("--retries", [ "-r" ])
			{
				DefaultValueFactory = (_) => 10,
				Description = "Default max retry count for failed transactions.",
				Recursive = true,
			};

			private static readonly System.CommandLine.Option<string?> ExecOption = new("--exec")
			{
				Description = "Execute this command, and exits immediately.",
				//*****
			};

			private static readonly Option<bool> AspireOption = new("--aspire")
			{
				Description = "Connect to a local docker instance managed by .NET Aspire",
				Recursive = true,
			};

			private static readonly Option<int?> DockerOption = new("--docker")
			{
				Description = "Connect to a local docker instance running on the given port",
				Recursive = true,
			};

			private static readonly Option<string> ChildHashOption = new("--child")
			{
				Description = "Hash of the arguments of the parent process that spawned this instance",
				Recursive = true,
			};

			private static readonly Option<int?> ParentProcessOption = new("--parent")
			{
				Description = "PID of the parent process that spawned this instance",
				Recursive = true,
			};

			private async Task RunShell(ParseResult result, CancellationToken cancel)
			{
				var clusterFile = result.GetValue(ClusterFileOption);
				var connectionString = result.GetValue(ConnectionStringOption);
				var apiVersion = result.GetValue(ApiVersionOption);
				var partition = result.GetValue(PartitionOption);
				var timeout = result.GetValue(TimeoutOption) ?? 30;
				var maxRetries = result.GetValue(RetriesOption) ?? 10;
				var execCommand = result.GetValue(ExecOption);
				var aspire = result.GetValue(AspireOption);
				var docker = result.GetValue(DockerOption);
				var childHash = result.GetValue(ChildHashOption);
				var parentProcess = result.GetValue(ParentProcessOption);

				if (aspire || docker != null)
				{
					clusterFile = null;
					var port = docker ?? 4550;
					connectionString = "docker:docker@127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
				}

				if (apiVersion == null)
				{
					apiVersion = (aspire || docker != null) ? 730 : !string.IsNullOrEmpty(connectionString) ? 720 : 620;
				}

				var rootPath = string.IsNullOrEmpty(partition) ? FdbPath.Root : FdbPath.Parse(partition);

				string? startCommand = null;
				if (!string.IsNullOrEmpty(execCommand))
				{
					startCommand = execCommand;
				}
				//else if (extra.Count > 0)
				//{ // the remainder of the command line will be the first command to execute
				//	startCommand = string.Join(" ", extra);
				//}

				if (parentProcess != null)
				{
					Process? parent;
					try
					{
						parent = Process.GetProcessById(parentProcess.Value);
					}
					catch (ArgumentException)
					{
						Console.Error.WriteLine($"Parent process {parentProcess.Value} not found, or has already terminated.");
						Environment.Exit(-1);
						return;
					}

					_ = parent.WaitForExitAsync(this.Cancellation).ContinueWith(_ =>
					{
						Console.WriteLine($"Parent process {parent.Id} has exited!");
						Environment.Exit(-1);
					}, this.Cancellation);
				}

				var builder = new ServiceCollection();

				builder.AddFoundationDb(apiVersion.Value, options =>
				{
					options.ConnectionOptions.ClusterFile = clusterFile;
					options.ConnectionOptions.ConnectionString = connectionString;
					options.ConnectionOptions.Root = rootPath;

					options.ConnectionOptions.DefaultTimeout = TimeSpan.FromSeconds(Math.Max(0, timeout));
					options.ConnectionOptions.DefaultRetryLimit = Math.Max(0, maxRetries);

					options.UseNativeClient(allowSystemFallback: false);
				});

				builder.AddSingleton<FdbShellRunner>();
				builder.AddSingleton<IFdbShellTerminal, FdbShellConsoleTerminal>();

				var services = builder.BuildServiceProvider();

				var terminal = services.GetRequiredService<IFdbShellTerminal>();

				IFdbDatabaseProvider dbProvider;
				try
				{
					dbProvider = services.GetRequiredService<IFdbDatabaseProvider>();
				}
				catch (FileNotFoundException e)
				{ // the most probably reason is that the native library could not be found
					
					terminal.StdErr("FoundationDB Client failed to initialize!", ConsoleColor.Red);
					terminal.StdErr($"> {e.Message}");
					Environment.ExitCode = -2;
					return;
				}
				catch (Exception e)
				{
					terminal.StdErr("FoundationDB Client failed to start!", ConsoleColor.Red);
					terminal.StdErr($"> {e}");
					Environment.ExitCode = -1;
					return;
				}

				var runner = services.GetRequiredService<FdbShellRunner>();

				//if (terminal.IsInteractive)
				//{
				//	terminal.SetWindowSize(160, 60);
				//}

				var shellArgs = new FdbShellRunnerArguments()
				{
					StartCommand = startCommand,
					RunSingleCommand = execCommand != null,
				};

				// pre-start FDB client
				dbProvider.Start();

				if (parentProcess != null)
				{
					terminal.StdOut($"Attaching to parent process {parentProcess.Value} and token {childHash}.");
				}

				await runner.RunAsync(shellArgs, this.Cancellation);
			}
		}
	}

	public interface IFdbShellTerminal
	{

		bool IsInteractive { get; }

		void StdOut(string? msg = null, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true);

		void StdOut(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true);

		string Escape(string? msg);

		void Markup(string? msg = null, bool newLine = true);

		void Markup(ref DefaultInterpolatedStringHandler msg, bool newLine = true);

		void StdErr(string msg, ConsoleColor color = ConsoleColor.DarkRed);

		void StdErr(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkRed);

		void Beep();

		bool SetWindowSize(int? width, int? height);

	}

	public class FdbShellConsoleTerminal : IFdbShellTerminal
	{

		public bool IsInteractive => true;

		public bool DisableColors { get; set; }

		public bool IsSilent { get; set; }

		public void Beep()
		{
			if (!this.IsSilent)
			{
				Console.Beep();
			}
		}

		public void StdOut(string? msg = null, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true)
		{
			if (this.DisableColors)
			{
				if (newLine)
				{
					Console.WriteLine(msg);
				}
				else
				{
					Console.Write(msg);
				}
			}
			else
			{
				var prev = Console.ForegroundColor;
				if (prev != color) Console.ForegroundColor = color;

				if (newLine)
				{
					Console.WriteLine(msg);
				}
				else
				{
					Console.Write(msg);
				}

				if (prev != color) Console.ForegroundColor = prev;
			}
		}

		public void StdOut(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true)
			=> StdOut(string.CreateInvariant(ref msg), color, newLine);

		public string Escape(string? msg)
		{
			msg ??= "";
			return msg.Replace("[", "[[").Replace("]", "]]");
		}

		public void Markup(string? msg = null, bool newLine = true)
		{
			msg ??= "";
			AnsiConsole.Markup(newLine ? (msg + Environment.NewLine) : msg);
		}

		public void Markup(ref DefaultInterpolatedStringHandler msg, bool newLine = true)
			=> Markup(string.Create(CultureInfo.InvariantCulture, ref msg), newLine);

		public void StdErr(string msg, ConsoleColor color = ConsoleColor.DarkRed)
		{
			if (this.DisableColors)
			{
				Console.Error.WriteLine(msg);
			}
			else
			{
				var prev = Console.ForegroundColor;
				if (prev != color)
				{
					Console.ForegroundColor = color;
				}

				Console.Error.WriteLine(msg);

				if (prev != color)
				{
					Console.ForegroundColor = prev;
				}
			}
		}

		public void StdErr(ref DefaultInterpolatedStringHandler msg, ConsoleColor color = ConsoleColor.DarkRed)
			=> StdErr(string.Create(CultureInfo.InvariantCulture, ref msg), color);

		public bool SetWindowSize(int? width, int? height)
		{
			if (width <= 0) return false;

			if (OperatingSystem.IsWindows())
			{
				try
				{
					if (width > 0)
					{
						Console.WindowWidth = width.Value;
					}

					if (height > 0)
					{
						Console.WindowHeight = height.Value;
					}
				}
				catch (Exception e)
				{
					StdErr($"Failed to change console width: {e.Message}");
					return false;
				}
			}

			return false;
		}

	}

	public static class FdbShellTerminalExtensions
	{

		public static void Comment(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg);

		public static void Comment(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg);

		public static void Error(this IFdbShellTerminal terminal, string msg) => terminal.StdErr(msg, ConsoleColor.Red);

		public static void Error(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdErr(ref msg, ConsoleColor.Red);

		public static void Success(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg, ConsoleColor.Green);

		public static void Success(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg, ConsoleColor.Green);

		public static void Progress(this IFdbShellTerminal terminal, string msg) => terminal.StdOut(msg, newLine: false);

		public static void Progress(this IFdbShellTerminal terminal, ref DefaultInterpolatedStringHandler msg) => terminal.StdOut(ref msg, newLine: false);

	}

	public sealed record FdbShellRunnerArguments
	{

		public FdbPath InitialPath { get; set; } = FdbPath.Root;

		public string? StartCommand { get; set; }

		public bool RunSingleCommand { get; set; }

	}

	public class FdbShellRunner
	{

		private IFdbDatabaseProvider Db { get; }

		public IFdbShellTerminal Terminal { get; }

		public FdbPath CurrentDirectoryPath { get; private set; }

		public string Description { get; set; } = "?";

		public FdbShellRunner(IFdbDatabaseProvider db, IFdbShellTerminal terminal)
		{
			this.Db = db;
			this.Terminal = terminal;
		}

		private void Markup(string? log, bool newLine = true) => this.Terminal.Markup(log, newLine);

		private void Markup(ref DefaultInterpolatedStringHandler log, bool newLine = true) => this.Terminal.Markup(ref log, newLine);

		private void StdOut(string? log = null, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true) => this.Terminal.StdOut(log, color, newLine);

		private void StdOut(ref DefaultInterpolatedStringHandler log, ConsoleColor color = ConsoleColor.DarkGray, bool newLine = true) => this.Terminal.StdOut(ref log, color, newLine);

		private void StdErr(string log, ConsoleColor color = ConsoleColor.DarkRed) => this.Terminal.StdErr(log, color);

		private void StdErr(ref DefaultInterpolatedStringHandler log, ConsoleColor color = ConsoleColor.DarkRed) => this.Terminal.StdErr(ref log, color);

		private static readonly Dictionary<string, string> KnownCommands = new (StringComparer.OrdinalIgnoreCase)
		{
			["quit"] = "Stop the shell",
			["cd"] = "Change the current directory",
			["show"] = "List the keys and values in a directory",
			["map"] = "Map the content of a directory and display a report",
			["version"] = "Display the version of the client and cluster",
			["find"] = "Find a directory",
			["clear"] = "Clear a key in the current directory",
			["clearrange"] = "Clear a range of keys in the current directory",
			["get"] = "Read the value of a key in the current directory",
			["coordinators"] = "Display the list of current coordinators",
			["count"] = "Count the number of keys in the current directory",
			["dir"] = "Display the list of sub-directories",
			["dump"] = "Dump the content of the current directory",
			["help"] = "Display this list",
			["layer"] = "Display of change the layer id of the current directory",
			["mkdir"] = "Create a new sub-directory",
			["mv"] = "Move a directory to another location",
			["pwd"] = "Prints the current path",
			["ren"] = "Rename a directory",
			["rmdir"] = "Remove a directory",
			["sampling"] = "Perform a random sampling of the keys in a directory",
			["shards"] = "Display the shards that intersect a directory",
			["status"] = "Display the status of cluster",
			["topology"] = "Display the topology of the cluster",
			["tree"] = "Show all the directories under the current directory",
			["wide"] = "Switch to wide screen (only on Windows)",
		};

		private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;

			Console.Error.WriteLine("Aborted via CTRL-C");

			this.Lifecycle.Cancel();

			//Environment.Exit(-1);
		}

		private CancellationTokenSource Lifecycle { get; set; } = new();

		public async Task RunAsync(FdbShellRunnerArguments args, CancellationToken stoppingToken)
		{
			this.Lifecycle = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
			var cancel = this.Lifecycle.Token;

			this.CurrentDirectoryPath = args.InitialPath;

			// handle CTRL-C gracefully
			Console.CancelKeyPress += OnCancelKeyPress;

			// enable UTF-8 in order to use custom fonts and emojis (requires NF-compatible font)
			try { Console.OutputEncoding = Encoding.UTF8; }
			catch { }

			bool stop = false;
			try
			{
				Markup($"[bold white]FdbShell v{this.GetType().Assembly.GetName().Version}[/] - Copyright (c) SnowBank SAS 2023-2026");
				Markup($"Using API [cyan]{Fdb.ApiVersion}[/] and client v[cyan]{Fdb.GetClientVersion().Version}[/]");

				string desc = !string.IsNullOrEmpty(this.Db.ProviderOptions.ConnectionOptions.ConnectionString)
					? $"Connecting to cluster at {(this.Db.ProviderOptions.ConnectionOptions.ConnectionString)}"
					: $"Connecting to cluster using file {(this.Db.ProviderOptions.ConnectionOptions.ClusterFile ?? "<default>")}";

				try
				{
					await AnsiConsole
						.Status()
						.Spinner(Spinner.Known.Dots)
						.SpinnerStyle(Style.Parse("green"))
						.StartAsync(
							desc,
							async (ctx) =>
							{
								var sw = Stopwatch.StartNew();

								int attempt = 0;
								int dots = 0;

								while (true)
								{
									var taskConnect = Fdb.System.GetCoordinatorsAsync(this.Db, cancel);

									while (!cancel.IsCancellationRequested)
									{
										dots = (dots + 1) % 4;
										if (attempt == 0 && sw.Elapsed.TotalSeconds > 3)
										{
											ctx.SpinnerStyle(Style.Parse("yellow"));
										}
										ctx.Status($"{desc}{(attempt == 0 ? "" : $", attempt #{attempt} ")}{new string('.', dots)}");

										if (taskConnect == (await Task.WhenAny(taskConnect, Task.Delay(500, cancel)).ConfigureAwait(false)))
										{
											break;
										}


										if (Console.KeyAvailable)
										{
											switch (Console.ReadKey().Key)
											{
												case ConsoleKey.Escape:
												{
													StdOut(" Abort!");
													StdErr("Could not connect to cluster.", ConsoleColor.Red);
													throw new OperationCanceledException();
												}
											}
										}

										//StdOut(".", newLine: false);
									}

									cancel.ThrowIfCancellationRequested();

									if (taskConnect.IsCompletedSuccessfully)
									{
										var cf = await taskConnect;
										StdOut();
										Markup($"Connected to: [cyan]{cf.Description}[/]");
										Markup($"Coordinators: [cyan]{string.Join("[/], [cyan]", cf.Coordinators.Select(x => x.ToString()))}[/]");

										if (cf.Coordinators.Length == 1)
										{
											this.Description = cf.Coordinators[0].Address + ":" + cf.Coordinators[0].Port.ToString(CultureInfo.InvariantCulture);
										}
										else
										{
											this.Description = cf.Description;
										}
										return;
									}

									++attempt;
									ctx.SpinnerStyle(Style.Parse("red"));

									try
									{
										await taskConnect;
									}
									catch (FdbException)
									{
										// retry!
									}
								}
							});
				}
				catch (Exception e)
				{
					if (e is not (OperationCanceledException or TaskCanceledException))
					{
						StdOut("");
						StdErr($"Failed to get coordinators state from cluster: {e.Message}");
					}
					Environment.ExitCode = -1;
					return;
				}
				
				StdOut();
				StdOut("FoundationDB Shell menu:");
				foreach (var cmd in (string[]) ["cd", "dir", "show", "tree", "help", "quit"])
				{
					Markup($"  [white]{cmd,-8}[/] {FdbShellRunner.KnownCommands[cmd]}");
				}
				StdOut("");

				var le = new LineEditor("FDBShell", cancel);

				le.AutoCompleteEvent = (txt, _) =>
				{
					string[] res;
					int p = txt.IndexOf(' ');
					if (p > 0)
					{
						string cmd = txt[..p];
						string arg = txt[(p + 1)..].Trim();

						if (cmd is "cd" or "rmdir")
						{ // handle completion for directories

							// txt: "cd foo" => prefix = "foo"
							// txt: "cd foobar/b" => prefix = "b"

							bool hasLeadingSlash = arg.EndsWith('/');
							var path = FdbPath.Parse(hasLeadingSlash ? (arg + "!") : arg);
							var parent = path.Count > 1 ? path.GetParent() : path.IsAbsolute ? FdbPath.Root : FdbPath.Empty;
							string search = hasLeadingSlash ? "" : path.Name;

							var children = RunAsyncCommand((db, _, ct) => AutoCompleteDirectories(CombinePath(this.CurrentDirectoryPath, parent.ToString()), db, ct), cancel).GetAwaiter().GetResult();

							if (children.GetValueOrDefault() == null)
							{
								return new(txt, null);
							}

							res = children.Value!
								.Where(s => s.StartsWith(search, StringComparison.Ordinal))
								.Select(s => (cmd + " " + parent[s])[txt.Length..])
								.ToArray();

							if (res is [ "" ])
							{ // someone was at "cd /Foo/Bar", pressed TAB again, and there is no other match
								// => we interpret it as "want to go in the sub-folder

								res = [ "/" ]; // add a "slash"
							}

							return new(txt, res);
						}

						// unknown command
						return new(txt, null);
					}

					// list of commands
					res = KnownCommands
					      .Where(cmd => cmd.Key.StartsWith(txt, StringComparison.OrdinalIgnoreCase))
					      .Select(cmd => cmd.Key[txt.Length..])
					      .ToArray();
					return new(txt, res);
				};
				le.TabAtStartCompletes = true;

				string? statusPrompt;
				string? prompt;

				//const string COLOR_FDB_BLUE_LIGHT = "#9BCFFF";
				const string COLOR_FDB_BLUE_MEDIUM = "#379CF6";
				const string COLOR_FDB_BLUE_DARK = "#0073E6";

				const string ICON_DATABASE = "\ue64d";
				const string ICON_TRIANGLE = "\ue0b0";
				const string ICON_CHEVRON = "\ue0b1";

				const string ICON_PARTITION = "\udb82\udf9c";
				const string ICON_TENANT = "\udb83\uddab";
				const string ICON_TABLE = "\udb81\udcf1";
				const string ICON_TEST = "\udb80\udc96";
				const string ICON_SPECIAL = "\udb84\udc80";

				static string Decorate(FdbPathSegment seg) => seg.LayerId switch
				{
					null or "" => seg.Name,
					"partition" => ICON_PARTITION + " " + seg.Name,
					"tenant" => ICON_TENANT + " " + seg.Name,
					"table" => ICON_TABLE + " " + seg.Name,
					"test" => ICON_TEST + " " + seg.Name,
					_ => ICON_SPECIAL + " " + seg.Name
				};

				void UpdatePrompt(FdbPath path)
				{
					//[{colorFdb1} on black][/]
					statusPrompt = $"[white on {COLOR_FDB_BLUE_MEDIUM}] {ICON_DATABASE} [/][{COLOR_FDB_BLUE_MEDIUM} on white]{ICON_TRIANGLE}[/][black on white] {this.Description} [/][white on {COLOR_FDB_BLUE_MEDIUM}]{ICON_TRIANGLE}[/][white on {COLOR_FDB_BLUE_MEDIUM}] ";

					// compute the estimated size
					int size = 0;
					int lastPart = 0;
					int lastSpe = 0;
					for(int i = 0; i < path.Count; i++)
					{
						var seg = path[i];
						size += seg.Name.Length + 2;
						if (seg.LayerId == FdbDirectoryPartition.LayerId)
						{
							lastPart = i;
						}
						else if (seg.LayerId.Length != 0)
						{
							lastSpe = i;
						}
					}

					bool compact = size > 60;

					if (path.Count == 0)
					{ // we are in the root folder
						statusPrompt += "/";
					}
					else if (lastPart > 0)
					{ // we are in a deep partition
						statusPrompt += "\uea7c";
						path = path[lastPart..];
						lastSpe -= lastPart;
					}
					else
					{
						statusPrompt += Decorate(path[0]);
						path = path[1..];
					}

					bool skipped = false;
					bool first = true;
					if (path.Count > 0)
					{
						for (int i = 0; i < path.Count; i++)
						{
							var seg = path[i];
							if (compact && (i < path.Count - 2) && seg.LayerId.Length == 0)
							{
								if (!skipped)
								{
									if (first)
									{
										statusPrompt += $" [/][{COLOR_FDB_BLUE_MEDIUM} on {COLOR_FDB_BLUE_DARK}]{ICON_TRIANGLE}[/][white on {COLOR_FDB_BLUE_DARK}] \uea7c";
									}
									else
									{
										statusPrompt += $" [/][black on {COLOR_FDB_BLUE_DARK}]{ICON_CHEVRON}[/][white on {COLOR_FDB_BLUE_DARK}] \uea7c";
									}
								}
								skipped = true;
								continue;
							}
							skipped = false;

							if (first && i == 0)
							{
								statusPrompt += $" [/][{COLOR_FDB_BLUE_MEDIUM} on {COLOR_FDB_BLUE_DARK}]{ICON_TRIANGLE}[/][white on {COLOR_FDB_BLUE_DARK}] " + Decorate(path[0]);
							}
							else
							{
								statusPrompt += $" [/][black on {(i == 0 ? COLOR_FDB_BLUE_MEDIUM : COLOR_FDB_BLUE_DARK)}]{ICON_CHEVRON}[/][white on {COLOR_FDB_BLUE_DARK}] " + Decorate(seg);
							}
							first = false;
						}
					}

					if (first)
					{
						statusPrompt += $" [/][{COLOR_FDB_BLUE_MEDIUM} on black]{ICON_TRIANGLE} [/]";
					}
					else
					{
						statusPrompt += $" [/][{COLOR_FDB_BLUE_DARK} on black]{ICON_TRIANGLE} [/]";
					}

					//statusPrompt = $"[fdb:{this.Description} {path}]";
					prompt = "\u279c ";
				}
				le.PromptColor = ConsoleColor.Cyan;
				UpdatePrompt(this.CurrentDirectoryPath);

				string? nextCommand = args.StartCommand;

				if (nextCommand == null)
				{
					await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(this.CurrentDirectoryPath, STuple.Empty, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
				}

				while (!stop && !this.Lifecycle.IsCancellationRequested)
				{
					string s;
					if (nextCommand != null)
					{
						s = nextCommand;
						nextCommand = null;
					}
					else
					{
						StdOut("");
						UpdatePrompt(this.CurrentDirectoryPath);
						AnsiConsole.Markup(statusPrompt + Environment.NewLine);
						s = le.Edit(prompt, "");
					}

					if (s == null) break;

					StdOut();

					//TODO: we need a tokenizer that recognizes binary keys, tuples, escaped paths, etc...
					var tokens = Tokenize(s);
					string cmd = (tokens.Count > 0 ? tokens.Get<string>(0) : null) ?? string.Empty;
					var extras = tokens.Count > 1 ? tokens.Substring(1) : STuple.Empty;

					var trimmedCommand = cmd.Trim().ToLowerInvariant();
					try
					{
						switch (trimmedCommand)
						{
							case "":
							{
								continue;
							}

							case "help":
							{
								ShowCommandHelp(this.Terminal);
								break;
							}

							case "version":
							{
								//TODO: get the version from the client or status json
								throw new NotImplementedException();
								//if (clusterFile != null)
								//{
								//	await VersionCommand(extras, clusterFile, Console.Out, cancel);
								//}
								//break;
							}

							case "tree":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Tree(path, extras, db, log, ct), cancel);
								break;
							}
							case "map":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Map(path, extras, db, log, ct), cancel);
								break;
							}

							case "dir":
							case "ls":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
								break;
							}
							case "ll":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(path, extras, BasicCommands.DirectoryBrowseOptions.ShowCount, db, log, ct), cancel);
								break;
							}

							case "count":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Count(path, extras, db, log, ct), cancel);
								break;
							}

							case "show":
							case "top":
							{
								await RunAsyncCommand((db, log, ct) => BasicCommands.Show(this.CurrentDirectoryPath, extras, false, db, log, ct), cancel);
								break;
							}
							case "last":
							{
								await RunAsyncCommand((db, log, ct) => BasicCommands.Show(this.CurrentDirectoryPath, extras, true, db, log, ct), cancel);
								break;
							}

							case "dump":
							{
								string? output = PopParam(ref extras);
								if (string.IsNullOrEmpty(output))
								{
									StdErr("You must specify a target file path.", ConsoleColor.Red);
									break;
								}
								await RunAsyncCommand((db, log, ct) => BasicCommands.Dump(this.CurrentDirectoryPath, output, extras, db, log, ct), cancel);
								break;
							}

							case "cd":
							case "pwd":
							{
								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var newPath = CombinePath(this.CurrentDirectoryPath, prm);
									var res = await RunAsyncCommand(
										(db, _, ct) => db.ReadAsync(tr => BasicCommands.TryOpenCurrentDirectoryAsync(tr, newPath), ct),
										cancel
									);
									if (res.Failed)
									{
										StdErr($"# Failed to open Directory {newPath}: {res.Error!.Message}", ConsoleColor.Red);
										this.Terminal.Beep();
									}
									else if (!res.HasValue || res.Value.Directory == null)
									{
										StdOut($"# Directory {newPath} does not exist!", ConsoleColor.Red);
										this.Terminal.Beep();
									}
									else
									{
										this.CurrentDirectoryPath = res.Value.Directory.Path;

										// auto "dir" !
										await RunAsyncCommand((db, log, ct) => BasicCommands.Dir(this.CurrentDirectoryPath, extras, BasicCommands.DirectoryBrowseOptions.Default, db, log, ct), cancel);
									}
								}
								else
								{
									var res = await RunAsyncCommand(
										(db, _, ct) => db.ReadAsync(tr => BasicCommands.TryOpenCurrentDirectoryAsync(tr, this.CurrentDirectoryPath), ct),
										cancel
									);
									if (res.Failed)
									{
										StdErr($"# Failed to query Directory {this.CurrentDirectoryPath}: {res.Error!.Message}", ConsoleColor.Red);
									}
									else if (!res.HasValue || res.Value.Directory == null)
									{
										StdOut($"# Directory {this.CurrentDirectoryPath} does not exist anymore");
									}
									else
									{
										StdOut("Current path: ", newLine: false);
										StdOut(res.Value.Directory.Path.ToString(), ConsoleColor.Cyan);
										StdOut("");
									}
								}

								break;
							}
							case "mkdir":
							case "md":
							{ // "mkdir DIRECTORYNAME"

								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(this.CurrentDirectoryPath, prm);
									await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, extras, db, log, ct), cancel);
								}

								break;
							}
							case "rmdir":
							{ // "rmdir DIRECTORYNAME"
								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(this.CurrentDirectoryPath, prm);
									await RunAsyncCommand((db, log, ct) => BasicCommands.RemoveDirectory(path, extras, db, log, ct), cancel);
								}

								break;
							}

							case "mv":
							case "ren":
							{ // "mv SOURCE DESTINATION"

								string? prm = PopParam(ref extras);
								var srcPath = CombinePath(this.CurrentDirectoryPath, prm);
								var dstPath = CombinePath(this.CurrentDirectoryPath, extras.Get<string>(0));
								await RunAsyncCommand((db, log, ct) => BasicCommands.MoveDirectory(srcPath, dstPath, extras.Substring(1), db, log, ct), cancel);

								break;
							}

							case "get":
							{ // "get KEY"

								if (extras.Count == 0)
								{
									StdErr("You must specify a key to read.", ConsoleColor.Red);
									break;
								}

								await RunAsyncCommand((db, log, ct) => BasicCommands.Get(this.CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "clear":
							{ // "clear KEY"

								if (extras.Count == 0)
								{
									StdErr("You must specify a key to clear.", ConsoleColor.Red);
									break;
								}

								await RunAsyncCommand((db, log, ct) => BasicCommands.Clear(this.CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "clearrange":
							{ // "clear *" or "clear FROM TO"

								if (extras.Count == 0)
								{
									StdErr("You must specify either '*', a prefix, or a key range.", ConsoleColor.Red);
									break;
								}

								await RunAsyncCommand((db, log, ct) => BasicCommands.ClearRange(this.CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "layer":
							{
								string? prm = PopParam(ref extras);
								if (string.IsNullOrEmpty(prm))
								{ // displays the layer id of the current folder
									await RunAsyncCommand((db, log, ct) => BasicCommands.ShowDirectoryLayer(this.CurrentDirectoryPath, extras, db, log, ct), cancel);

								}
								else
								{ // change the layer id of the current folder
									prm = prm.Trim();
									// double or single quotes can be used to escape the value
									if (prm.Length >= 2 && (prm.StartsWith("'") && prm.EndsWith("'")) || (prm.StartsWith("\"") && prm.EndsWith("\"")))
									{
										prm = prm.Substring(1, prm.Length - 2);
									}

									await RunAsyncCommand((db, log, ct) => BasicCommands.ChangeDirectoryLayer(this.CurrentDirectoryPath, prm, extras, db, log, ct), cancel);
								}

								break;
							}

							case "mkpart":
							{ // "mkpart PARTITIONNAME"

								string? prm = PopParam(ref extras);
								if (!string.IsNullOrEmpty(prm))
								{
									var path = CombinePath(this.CurrentDirectoryPath, prm);
									await RunAsyncCommand((db, log, ct) => BasicCommands.CreateDirectory(path, STuple.Create(FdbDirectoryPartition.LayerId).Concat(extras), db, log, ct), cancel);
								}

								break;
							}

							case "topology":
							{
								await RunAsyncCommand((db, log, ct) => BasicCommands.Topology(null, extras, db, log, ct), cancel);
								break;
							}

							case "shards":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Shards(path, extras, db, log, ct), cancel);
								break;
							}

							case "sampling":
							{
								string? prm = PopParam(ref extras);
								var path = CombinePath(this.CurrentDirectoryPath, prm);
								await RunAsyncCommand((db, log, ct) => BasicCommands.Sampling(path, extras, db, log, ct), cancel);
								break;
							}

							case "coordinators":
							{
								await RunAsyncCommand((db, log, ct) => CoordinatorsCommand(db, log, ct), cancel);
								break;
							}

							case "partition":
							{
								//TODO: how to re-implement this method?
								throw new NotSupportedException();

								//string? prm = PopParam(ref extras);
								//if (string.IsNullOrEmpty(prm))
								//{
								//	StdOut($"# Current partition is {partition}");
								//	//TODO: browse existing partitions ?
								//	break;
								//}

								//var newPartition = FdbPath.Parse(prm.Trim());
								//IFdbDatabase? newDb = null;
								//try
								//{
								//	var options = new FdbConnectionOptions
								//	{
								//		ClusterFile = clusterFile,
								//		Root = newPartition
								//	};
								//	newDb = await ChangeDatabase(options, cancel);
								//}
								//catch (Exception)
								//{
								//	newDb?.Dispose();
								//	newDb = null;
								//	throw;
								//}
								//finally
								//{
								//	if (newDb != null)
								//	{
								//		if (Db != null)
								//		{
								//			Db.Dispose();
								//			Db = null;
								//		}

								//		Db = newDb;
								//		partition = newPartition;
								//		StdOut($"# Changed partition to {partition}");
								//	}
								//}

								//break;
							}

							case "q":
							case "x":
							case "quit":
							case "exit":
							case "bye":
							{
								stop = true;
								break;
							}

							case "gc":
							{
								long before = GC.GetTotalMemory(false);
								this.Terminal.Progress("Collecting garbage...");
								GC.Collect();
								GC.WaitForPendingFinalizers();
								GC.Collect();
								StdOut(" Done");
								long after = GC.GetTotalMemory(false);
								StdOut($"- before = {before:N0}");
								StdOut($"- after  = {after:N0}");
								StdOut($"- delta  = {(before - after):N0}");
								break;
							}

							case "mem":
							{
								//TODO: implement this in a more cross-platform way!
								throw new NotSupportedException();
								//StdOut("Memory usage:");
								//StdOut("- Managed Mem  : " + GC.GetTotalMemory(false).ToString("N0"));
								//if (OperatingSystem.IsWindows())
								//{
								//	StdOut("- Working Set  : " + PerfCounters.WorkingSet!.NextValue().ToString("N0") + " (peak " + PerfCounters.WorkingSetPeak!.NextValue().ToString("N0") + ")");
								//	StdOut("- Virtual Bytes: " + PerfCounters.VirtualBytes!.NextValue().ToString("N0") + " (peak " + PerfCounters.VirtualBytesPeak!.NextValue().ToString("N0") + ")");
								//	StdOut("- Private Bytes: " + PerfCounters.PrivateBytes!.NextValue().ToString("N0"));
								//	StdOut("- BytesInAlHeap: " + PerfCounters.ClrBytesInAllHeaps!.NextValue().ToString("N0"));
								//}
								//break;
							}

							case "wide":
							{
								this.Terminal.SetWindowSize(160, null);

								break;
							}

							case "status":
							case "wtf":
							{
								var clusterFile = this.Db.ProviderOptions.ConnectionOptions.ClusterFile;

								if (string.IsNullOrEmpty(this.Db.ProviderOptions.ConnectionOptions.ConnectionString))
								{
									//TODO: write the string to a temp file?
									throw new NotSupportedException("Needs a cluster file");
								}

								var result = await RunAsyncCommand((_, log, ct) => FdbCliCommands.RunFdbCliCommand("status details", null, clusterFile, log, ct), cancel);
								if (result.Failed) break;
								if (result.GetValueOrDefault()?.ExitCode != 0)
								{
									StdErr($"# fdbcli exited with code {result.Value?.ExitCode}");
									StdOut("> StdErr:");
									StdOut(result.Value?.StdErr ?? "");
									StdOut("> StdOut:");
								}

								StdOut(result.Value?.StdOut ?? "");
								break;
							}

							case "find":
							{
								if (extras.Count == 0)
								{
									StdErr("You must specify a query.", ConsoleColor.Red);
									break;
								}
								await RunAsyncCommand((db, log, ct) => BasicCommands.Find(this.CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							case "r":
							case "read":
							{
								if (extras.Count == 0)
								{
									StdErr("You must specify a query.", ConsoleColor.Red);
									break;
								}
								await RunAsyncCommand((db, log, ct) => BasicCommands.Query(this.CurrentDirectoryPath, extras, db, log, ct), cancel);
								break;
							}

							default:
							{
								StdErr($"Unknown command : '{trimmedCommand}'", ConsoleColor.Red);
								break;
							}
						}
					}
					catch (Exception e)
					{
						StdErr($"Failed to execute command '{trimmedCommand}': {e.Message}", ConsoleColor.Red);
#if DEBUG
						StdErr(e.ToString());
#endif
					}

					if (args.RunSingleCommand)
					{ // only run one command, and then exit
						break;
					}

				}
			}
			finally
			{
				StdOut("Bye");
				this.Db.Dispose();
			}
		}

		private void ShowCommandHelp(IFdbShellTerminal terminal)
		{
			terminal.StdOut("List of supported commands:");
			foreach (var (k, v) in FdbShellRunner.KnownCommands.OrderBy(kv => kv.Key))
			{
				terminal.StdOut($"  {k,-14} {v}");
			}
		}

		public async Task RunAsyncCommand(Func<IFdbDatabase, IFdbShellTerminal, CancellationToken, Task> command, CancellationToken cancel)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			try
			{
				var db = await this.Db.GetDatabase(cts.Token);

				await command(db, this.Terminal, cts.Token).ConfigureAwait(false);
			}
			finally
			{
				await cts.CancelAsync();
			}
		}

		public async Task<Maybe<T>> RunAsyncCommand<T>(Func<IFdbDatabase, IFdbShellTerminal, CancellationToken, Task<T>> command, CancellationToken cancel)
		{
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancel);
			try
			{
				var db = await this.Db.GetDatabase(cts.Token);

				return Maybe.Return<T>(await command(db, this.Terminal, cts.Token).ConfigureAwait(false));
			}
			catch (Exception e)
			{
				return Maybe.Error<T>(e);
			}
			finally
			{
				await cts.CancelAsync();
			}
		}

		private static string? PopParam(ref IVarTuple extras)
		{
			if (extras.Count == 0)
			{
				return null;
			}

			string? prm = extras.Get<string>(0);
			extras = extras.Substring(1);
			return prm;
		}

		#region Directories...

		private static bool HasIndirection(FdbPath path)
		{
			foreach (var seg in path)
			{
				if (seg.Name == "." || seg.Name == "..") return true;
			}
			return false;
		}

		private static FdbPath RemoveIndirection(FdbPath path)
		{
			Debug.Assert(path.IsAbsolute, "Path must be absolute");

			var segments = new List<FdbPathSegment>(path.Count);
			foreach (var seg in path.Segments.Span)
			{
				if (seg.Name == ".") continue;
				if (seg.Name == "..")
				{
					if (segments.Count == 0)
					{
						throw new InvalidOperationException("The specified path is invalid.");
					}
					segments.RemoveAt(segments.Count - 1);
					continue;
				}
				segments.Add(seg);
			}
			return FdbPath.Absolute(segments);
		}

		private static FdbPath CombinePath(FdbPath parent, string? children)
		{
			if (string.IsNullOrEmpty(children) || children == ".") return parent;

			if (children == ".." && parent.IsAbsolute && parent.Count > 0)
			{
				return parent.GetParent();
			}

			var p = FdbPath.Parse(children);
			if (!p.IsAbsolute) p = parent[p];
			if (HasIndirection(p)) p = RemoveIndirection(p);
			return p;
		}

		private async Task<string[]?> AutoCompleteDirectories(FdbPath path, IFdbDatabase db, CancellationToken ct)
		{
			try
			{
				return await db.ReadAsync(async tr =>
				{
					var (parent, _, _) = await BasicCommands.TryOpenCurrentDirectoryAsync(tr, path);
					if (parent == null) return null;
					var paths = await parent.ListAsync(tr);
					return paths.Select(p => p.Name).ToArray();
				}, ct);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine("error: " + e.Message);
				return null;
			}
		}

		#endregion

		private async Task CoordinatorsCommand(IFdbDatabase db, IFdbShellTerminal terminal, CancellationToken ct)
		{
			var cf = await Fdb.System.GetCoordinatorsAsync(db, ct);
			this.Description = cf.Description;
			terminal.StdOut($"Connected to: {cf.Description} ({cf.Id})", ConsoleColor.Gray);
			terminal.StdOut($"Found {cf.Coordinators.Length} coordinator(s):");
			foreach (var coordinator in cf.Coordinators)
			{
				terminal.StdOut($"  {coordinator.Address}:{coordinator.Port}", ConsoleColor.White);
			}
		}

		private static IVarTuple Tokenize(string s)
		{

			bool hasToken = false;
			var tokens = STuple.Empty;
			int p = 0;

			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];
				switch (c)
				{
					case ' ':
					case '\t':
					{ // end of previous token, start of new token
						if (hasToken)
						{
							tokens = tokens.Append(s.Substring(p, i - p));
							hasToken = false;
						}
						break;
					}
					case '(':
					{ 
						if (!hasToken)
						{ // start of tuple
							string exp = s.Substring(i);
							STuple.Deformatter.ParseNext(exp, out var tok, out var tail);
							tokens = tokens.Append(tok);
							if (string.IsNullOrEmpty(tail))
							{
								i = s.Length;
							}
							else
							{
								i += exp.Length - tail.Length - 1;
							}
						}
						break;
					}
					case '\'':
					case '"':
					{
						if (!hasToken)
						{
							tokens = tokens.Append(ParseQuotedString(s, c, i, out int j));
							i = j;
						}
						break;
					}
					default:
					{
						if (!hasToken)
						{
							p = i;
							hasToken = true;
						}
						break;
					}
				}
			}
			if (hasToken)
			{
				tokens = tokens.Append(s.Substring(p));
			}

			return tokens;
		}

		private static string ParseQuotedString(string s, char quote, int start, out int next)
		{
			//TODO: read '.....'
			var sb = new StringBuilder();
			bool slash = false;
			for (int i = start + 1; i < s.Length; i++)
			{
				char c = s[i];
				switch (c)
				{
					case '\'':
					{
						if (slash)
						{
							slash = false;
							sb.Append('\'');
							break;
						}
						if (quote == '\'')
						{
							next = i + 1;
							return sb.ToString();
						}

						sb.Append('\'');
						break;
					}
					case '"':
					{
						if (slash)
						{
							slash = false;
							sb.Append('"');
							break;
						}
						if (quote == '"')
						{
							next = i + 1;
							return sb.ToString();
						}
						sb.Append('"');
						break;
					}
					case '\\':
					{
						if (slash)
						{
							slash = false;
							sb.Append('\\');
							break;
						}
						slash = true;
						break;
					}
					default:
					{
						if (slash)
						{
							slash = false;
							switch (c)
							{
								case 't': sb.Append('\t'); break;
								case 'r': sb.Append('\r'); break;
								case 'n': sb.Append('\n'); break;
								default: throw new FormatException("Invalid escape string literal");
							}
							break;
						}
						sb.Append(c);
						break;
					}
				}
			}
			throw new FormatException("Missing final quote at end of string literal");
		}

	}

}
