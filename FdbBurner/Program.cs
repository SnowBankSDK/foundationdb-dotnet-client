
namespace FdbBurner
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FoundationDB.Client;

	public class Program
	{
		static string ClusterPath = null;

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			if (OperatingSystem.IsWindows() && Console.LargestWindowHeight > 0 && Console.LargestWindowWidth > 0)
			{
				Console.WindowWidth = 80;
				Console.WindowHeight = 40;
			}

			string title = OperatingSystem.IsWindows() ? Console.Title : string.Empty;

			try
			{

				if (args.Length > 0)
				{
					ClusterPath = args[0];
				}

				Fdb.Start(Fdb.GetDefaultApiVersion());
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			catch (Exception e)
			{
				Trace.WriteLine(e.ToString());
				Console.Error.WriteLine("TOO HOT! " + e);
				Environment.ExitCode = -1;
			}
			finally
			{
				if (OperatingSystem.IsWindows())
				{
					Console.Title = title;
				}
				Console.CursorVisible = true;
				Fdb.Stop();
			}
		}


		const int N = 1000;

		private static int CurrentSize = 3;
		private static readonly int[] VALUE_SIZES = [ 0, 4, 32, 100, 1000, 4000 ];

		private static readonly Random Rnd = new();
		private static readonly string Suffix = Guid.NewGuid().ToString();
		private static Slice Value = Slice.Random(Rnd, VALUE_SIZES[CurrentSize]);
		private static bool Randomized;

		private static long Keys;
		private static long Transactions;
		private static long Bytes;

		private static async Task BurnerThread(IFdbDatabase db, CancellationToken ct)
		{

			var location = db.Root["Benchmarks"]["Burner"]["Sequential"];
			await db.WriteAsync(async tr =>
			{
				var x = await location.CreateOrOpenAsync(tr);
				tr.ClearRange(x);
			}, ct);

			long pos = 0;

			Random rnd;
			lock (Rnd)
			{
				rnd = new Random(Rnd.Next());
			}

			using (var tr = db.BeginTransaction(ct))
			{
				while (!ct.IsCancellationRequested)
				{
					FdbException error = null;
					try
					{
						tr.Reset();
						// resolved in each transaction: the directory can move between two of them
						var folder = await location.Resolve(tr);

						for(int i = 0; i < N; i++)
						{
							long x = Randomized
								? rnd.Next()
								: pos + i;

							tr.Set(folder.Key(x, Suffix), Value);
							Interlocked.Increment(ref Keys);
						}
						pos += N;

						await tr.CommitAsync();
						Interlocked.Increment(ref Transactions);
						Interlocked.Add(ref Bytes, tr.Size);
					}
					catch (FdbException e)
					{
						error = e;
					}

					if (error != null && !ct.IsCancellationRequested)
					{
						await tr.OnErrorAsync(error.Code);
					}
				}
			}

		}

		public class Datum
		{
			public DateTime Date { get; set; }
			public long Keys { get; set; }
			public long Commits { get; set; }
			public long Bytes { get; set; }
			public double DiskWriteBps { get; set; }
			public double DiskReadBps { get; set; }
			public double DiskWriteIops { get; set; }
			public double DiskReadIops { get; set; }
		}

		private static void WriteAt(int x, int y, string fmt, params object[] args)
		{
			Console.SetCursorPosition(x, y);
			Console.Write(String.Format(CultureInfo.InvariantCulture, fmt, args));
		}

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			Console.CursorVisible = false;
			Console.TreatControlCAsInput = true;
			try
			{
				CancellationTokenSource cts = null;
				Task burner = null;

				const int CAPACITY = 4 * 2;
				var history = new Queue<Datum>(CAPACITY);

				var options = new FdbConnectionOptions
				{
					ClusterFile = Program.ClusterPath,
					DefaultTimeout = TimeSpan.FromSeconds(10)
				};
				//TODO: proper parsing of command line arguments!

				using (var db = await Fdb.OpenAsync(options, cancel))
				{
					bool exit = false;
					bool hot = false;
					bool repaint = true;

					var processName = Process.GetCurrentProcess().ProcessName;
					var perfDiskReads = OperatingSystem.IsWindows() ? new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "0 C:") : null;
					var perfDiskWrites = OperatingSystem.IsWindows() ? new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "0 C:") : null;
					var perfDiskWriteIops = OperatingSystem.IsWindows() ? new PerformanceCounter("PhysicalDisk", "Disk Writes/sec", "0 C:") : null;
					var perfDiskReadIops = OperatingSystem.IsWindows() ? new PerformanceCounter("PhysicalDisk", "Disk Reads/sec", "0 C:") : null;

					const int COL0 = 1;
					const int COL1 = COL0 + 15;
					const int COL2 = COL1 + 15;
					const int COL3 = COL2 + 15;
					const int COL4 = COL3 + 15;

					while (!exit && !cancel.IsCancellationRequested)
					{
						if (Console.KeyAvailable)
						{
							var k = Console.ReadKey();
							switch (k.Key)
							{
								case ConsoleKey.Escape:
								{ // [ESC]
									exit = true;
									break;
								}

								case ConsoleKey.C:
								{
									if (k.Modifiers.HasFlag(ConsoleModifiers.Control))
									{ // CTRL-C
										exit = true;
									}
									break;
								}

								case ConsoleKey.R:
								{
									Randomized = !Randomized;
									repaint = true;
									break;
								}

								case ConsoleKey.V:
								{ // Change Value Size
									CurrentSize = (CurrentSize + 1) % VALUE_SIZES.Length;
									Value = Slice.Random(Rnd, VALUE_SIZES[CurrentSize]);
									repaint = true;
									break;
								}

								case ConsoleKey.Spacebar:
								{
									hot = !hot;
									repaint = true;
									if (hot)
									{
										cts = new CancellationTokenSource();
										burner = Task.Run(() => BurnerThread(db, cts.Token), cts.Token);
									}
									else
									{
										cts.Cancel();
										try { await burner; }
										catch (TaskCanceledException) { }
										cts.Dispose();
									}
									break;
								}

							}
						}

						if (!repaint)
						{
							await Task.Delay(250, cts.Token);
						}

						long curKeys =  Volatile.Read(ref Keys);
						long curTrans = Volatile.Read(ref Transactions);
						long curBytes = Volatile.Read(ref Bytes);
						double? curDiskWrites = null;
						double? curDiskReads = null;
						double? curDiskWriteIo = null;
						double? curDiskReadIo = null;
						if (OperatingSystem.IsWindows())
						{
							curDiskWrites = perfDiskWrites?.NextValue();
							curDiskReads = perfDiskReads?.NextValue();
							curDiskWriteIo = perfDiskWriteIops?.NextValue();
							curDiskReadIo = perfDiskReadIops?.NextValue();
						}

						while (history.Count >= CAPACITY) history.Dequeue();

						var now = DateTime.UtcNow;
						history.Enqueue(new Datum
						{
							Date = now,
							Keys = curKeys,
							Commits = curTrans,
							Bytes = curBytes,
							DiskWriteBps = curDiskWrites ?? double.NaN,
							DiskReadBps = curDiskReads ?? double.NaN,
							DiskWriteIops = curDiskWriteIo ?? double.NaN,
							DiskReadIops = curDiskReadIo ?? double.NaN,
						});

						if (repaint)
						{
							Console.Title = "FdbBurner - " + (!hot ? "ICY COLD" : Randomized ? "HOT HOT HOT" : "HOT HOT");

							Console.BackgroundColor = !hot ? ConsoleColor.DarkCyan : Randomized ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen;
							Console.Clear();
							Console.ForegroundColor = ConsoleColor.Gray;
							WriteAt(COL0, 1, "Pattern   : {0,10}", "");
							WriteAt(COL2, 1, "Value Size: {0,6} bytes", "");
							WriteAt(COL0, 3, "{0,-12}", "Transactions");
							WriteAt(COL1, 3, "{0,-12}", "Keys");
							WriteAt(COL2, 3, "{0,-10}", "Written Bytes");
							WriteAt(COL3, 3, "{0,-10}", "Disk Writes");
							WriteAt(COL4, 3, "{0,-10}", "Disk Reads");
							WriteAt(COL3, 7, "{0,-10}", "Write IOPS");
							WriteAt(COL4, 7, "{0,-10}", "Read IOPS");

							repaint = false;
						}

						Console.ForegroundColor = ConsoleColor.White;
						WriteAt(COL0 + 12, 1, "{0,-10}", Randomized ? "Random" : "Sequential");
						WriteAt(COL2 + 12, 1, "{0,6:N0}", Value.Count);

						WriteAt(COL0, 4, "{0,12:N0}", curTrans);
						WriteAt(COL1, 4, "{0,12:N0}", curKeys);
						WriteAt(COL2, 4, "{0,10:N1} MB", curBytes / 1048576.0);
						WriteAt(COL3, 4, "{0,10:N1} MB/s", curDiskWrites / 1048576.0);
						WriteAt(COL4, 4, "{0,10:N1} MB/s", curDiskReads / 1048576.0);

						if (history.Count > 1)
						{
							var old = history.Peek();
							var dur = (now - old.Date).TotalSeconds;
							double speed;

							Console.ForegroundColor = ConsoleColor.White;

							speed = (curTrans - old.Commits) / dur;
							WriteAt(COL0, 5, "{0,12:N0}", speed);

							speed = (curKeys - old.Keys) / dur;
							WriteAt(COL1, 5, "{0,12:N0}", speed);

							speed = (curBytes - old.Bytes) / dur;
							WriteAt(COL2, 5, "{0,10:N1} MB/s", speed / 1048576.0);

							var writeSpeed = history.Average(d => d.DiskWriteBps);
							var readSpeed = history.Average(d => d.DiskReadBps);
							WriteAt(COL3, 5, "{0,10:N1} MB/s", writeSpeed / 1048576.0);
							WriteAt(COL4, 5, "{0,10:N1} MB/s", readSpeed / 1048576.0);

							var writeIops = history.Average(d => d.DiskWriteIops);
							var readIops = history.Average(d => d.DiskReadIops);
							WriteAt(COL3, 8, "{0,10:N0} iops", writeIops);
							WriteAt(COL4, 8, "{0,10:N0} iops", readIops);

							var factor = speed > 0 ? writeSpeed / speed : 0;
							WriteLarge(0, 16, "{0,8:F3}", speed / 1048576.0);
							WriteLarge(0, 24, "{0,8:F3}", writeSpeed / 1048576.0);
							WriteLarge(0, 32, "X{0,5:F1}", factor);
						}


						Console.SetCursorPosition(0, 0);
					}

				}
			}
			finally
			{
				Console.CursorVisible = true;
				Console.ResetColor();
				Console.Clear();
			}
		}

		#region ASCII Arts

		// note: taken from "Banner3" font
		private static readonly string[] Font =
		[
		//   ==========----------==========----------==========----------==========----------==========----------==========----------==========----------
			"  #####       ##     #######   #######  ##        ########   #######  ########   #######   #######                                ##     ## ",
			" ##   ##    ####    ##     ## ##     ## ##    ##  ##        ##     ## ##    ##  ##     ## ##     ##                                ##   ##  ",
			"##     ##     ##           ##        ## ##    ##  ##        ##            ##    ##     ## ##     ##                                 ## ##   ",
			"##     ##     ##     #######   #######  ##    ##  #######   ########     ##      #######   ########                        ####      ###    ",
			"##     ##     ##    ##               ## #########       ##  ##     ##   ##      ##     ##        ##                        ####     ## ##   ",
			" ##   ##      ##    ##        ##     ##       ##  ##    ##  ##     ##   ##      ##     ## ##     ##              ###        ##     ##   ##  ",
			"  #####     ######  #########  #######        ##   ######    #######    ##       #######   #######               ###       ##     ##     ## "
		];

		const int CHAR_SPACE = 10;
		const int CHAR_DOT   = 11;
		const int CHAR_COMMA = 12;
		const int CHAR_X     = 13;

		public static string GetScanLine(int digit, int line)
		{
			return Font[line].Substring(digit * 10, 10);
		}

		public static void WriteLarge(int x, int y, string fmt, double value)
		{
			string s = String.Format(CultureInfo.InvariantCulture, fmt, value);
			var sb = new StringBuilder();
			for (int i = 0; i < Font.Length; i++)
			{
				sb.Clear();
				foreach (var c in s)
				{
					switch (c)
					{
						case ' ':
							sb.Append(GetScanLine(CHAR_SPACE, i));
							break;
						case '0':
						case '1':
						case '2':
						case '3':
						case '4':
						case '5':
						case '6':
						case '7':
						case '8':
						case '9':
							sb.Append(GetScanLine(c - 48, i));
							break;
						case '.':
							sb.Append(GetScanLine(CHAR_DOT, i));
							break;
						case ',':
							sb.Append(GetScanLine(CHAR_COMMA, i));
							break;
						case 'x':
						case 'X':
							sb.Append(GetScanLine(CHAR_X, i));
							break;
					}
				}
				Console.SetCursorPosition(x, y);
				Console.Write(sb.ToString());
				++y;
			}
		}

		#endregion

	}
}
