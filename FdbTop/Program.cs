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

//#define DEBUG_LAYOUT

// ReSharper disable CompareOfFloatsByEqualityOperator
namespace FdbTop
{
	using SnowBank.Data.Json;

	public static class Program
	{
		private static readonly FdbConnectionOptions Options = new FdbConnectionOptions();

		private const int MIN_WIDTH = 146;
		private const int MIN_HEIGHT = 30;
		private const int DEFAULT_WIDTH = 160;
		private const int DEFAULT_HEIGHT = 60;

		public static void Main(string[] args)
		{
			//TODO: move this to the main, and add a command line argument to on/off ?

			//note: .NET Core may or may not be able to change the size of the console depending on the platform!
			try
			{
				if (OperatingSystem.IsWindows() && Console.LargestWindowWidth > 0 && Console.LargestWindowHeight > 0)
				{
					Console.WindowWidth = DEFAULT_WIDTH;
					Console.WindowHeight = DEFAULT_HEIGHT;
				}
			}
			catch
			{
				bool tooSmall;
				try
				{
					tooSmall = Console.WindowWidth < MIN_WIDTH || Console.WindowHeight < MIN_HEIGHT;
				}
				catch
				{
					tooSmall = true;
				}

				if (tooSmall)
				{
					Console.Error.WriteLine("This tool cannot run in a console smaller than 136x30 characters.");
					Console.Error.WriteLine("Either increase the screen resolution, or reduce the font size of your console.");
					Environment.ExitCode = -1;
					return;
				}
			}

			ScreenHeight = Console.WindowHeight;
			ScreenWidth = Console.WindowWidth;

			string title = GetTitle();
			try
			{
				// default settings
				Options.ClusterFile = null;
				Options.DefaultTimeout = TimeSpan.FromSeconds(10);

				if (args.Length > 0)
				{
					//TODO: proper arguments parsing!
					Options.ClusterFile = args[0];
				}

				SetTitle("fdbtop");

				Fdb.Start(620);
				using (var go = new CancellationTokenSource())
				{
					MainAsync(args, go.Token).GetAwaiter().GetResult();
				}
			}
			catch(Exception e)
			{
				Trace.WriteLine(e.ToString());
				Console.WriteLine("ClusterFile: " + Options.ClusterFile);
				Console.Error.WriteLine("CRASHED! " + e);
				Environment.ExitCode = -1;
			}
			finally
			{
				SetTitle(title);
				Console.CursorVisible = true;
				Fdb.Stop();
			}
		}

		public static int ScreenHeight;
		public static int ScreenWidth;

		public static FdbClusterConnectionString? CurrentCoordinators;

		private static async Task MainAsync(string[] args, CancellationToken cancel)
		{
			Console.CursorVisible = false;
			Console.TreatControlCAsInput = true;
			try
			{
				DateTime now;
				DateTime lap = DateTime.MinValue;
				DateTime next = DateTime.MinValue;
				bool repaint = true;
				bool exit = false;
				bool saveNext = false;
				bool updated = false;
				const double FAST = 1;
				const double SLOW = 5;
				double speed = FAST;

				DisplayMode mode = DisplayMode.Metrics;

				Task<FdbSystemStatus>? taskStatus = null;
				FdbSystemStatus? status = null;

				Program.Screen = new FrameBuffer(Console.WindowWidth, Console.WindowHeight);

				using (var db = await Fdb.OpenAsync(Options, cancel))
				{

					while (!exit && !cancel.IsCancellationRequested)
					{
						now = DateTime.UtcNow;

						if (CurrentCoordinators == null)
						{
							try
							{
								CurrentCoordinators = await Fdb.System.GetCoordinatorsAsync(db, cancel);
							}
							catch (Exception)
							{
								CurrentCoordinators = null;
								//TODO: error?
							}
						}

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
									else
									{ // [C]lear
										lap = now;
										next = now;
										History.Clear();
										updated = repaint = true;
									}
									break;
								}

								case ConsoleKey.F:
								{ // [F]ast (on/off)
									speed = speed == FAST ? SLOW : FAST;
									break;
								}

								case ConsoleKey.H:
								case ConsoleKey.F1:
								{
									mode = DisplayMode.Help;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.L:
								{ // [L]atency
									mode = DisplayMode.Latency;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.M:
								{ // [M]etrics
									mode = DisplayMode.Metrics;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.P:
								{ // [P]rocesses
									mode = DisplayMode.Processes;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.Q:
								{ // [Q]uit
									exit = true;
									break;
								}

								case ConsoleKey.R:
								{ // [R]oles
									mode = DisplayMode.Roles;
									updated = repaint = true;
									break;
								}

								case ConsoleKey.S:
								{ // [S]napshot
									saveNext = true;
									break;
								}

								case ConsoleKey.T:
								{ // [T]ransactions
									mode = DisplayMode.Transactions;
									updated = repaint = true;
									break;
								}

							}
						}

						taskStatus ??= Fdb.System.GetStatusAsync(db, cancel);

						if (status != null && saveNext)
						{
							using (var fs = System.IO.File.Create(@".\\status.json"))
							{
								await fs.WriteAsync(status.JsonData.ToJsonSlice().Memory, cancel);
								await fs.FlushAsync(cancel);
							}
							saveNext = false;
						}

						if (lap == DateTime.MinValue)
						{
							lap = now;
							next = now.AddSeconds(speed);
						}

						if (now >= next)
						{
							int ww = Console.WindowWidth;
							int hh = Console.WindowHeight;
							if (ww != Screen.Width || hh != Program.ScreenHeight)
							{
								Screen = new FrameBuffer(ww, hh);
								Program.Screen.Clear(ConsoleColor.Gray, ConsoleColor.Black);
							}

							if (taskStatus.IsCompleted)
							{
								try
								{
									var newStatus = await taskStatus.ConfigureAwait(false);
									status = newStatus;
								}
								catch (Exception)
								{
									//TODO: display error in top bar!
									FailToBar();
									repaint = true;
								}
								finally
								{
									taskStatus = null;
								}
							}

							if (status != null)
							{
								var metric = new HistoryMetric
								{
									Available = status.ReadVersion > 0,

									LocalTime = now - lap,
									Timestamp = status.Cluster.ClusterControllerTimestamp,
									ReadVersion = status.ReadVersion,

									ReadsPerSecond = status.Cluster.Workload.Operations.Reads.Hz,
									WritesPerSecond = status.Cluster.Workload.Operations.Writes.Hz,
									WrittenBytesPerSecond = status.Cluster.Workload.Bytes.Written.Hz,

									TransStarted = status.Cluster.Workload.Transactions.Started.Hz,
									TransCommitted = status.Cluster.Workload.Transactions.Committed.Hz,
									TransConflicted = status.Cluster.Workload.Transactions.Conflicted.Hz,

									LatencyCommit = status.Cluster.Latency.CommitSeconds,
									LatencyRead = status.Cluster.Latency.ReadSeconds,
									LatencyStart = status.Cluster.Latency.TransactionStartSeconds,
								};
								History.Enqueue(metric);
								updated = true;

								now = DateTime.UtcNow;
								while (next < now) next = next.AddSeconds(speed);
							}
						}

						if (status != null && updated)
						{
							var metric = History.LastOrDefault() ?? new ();
							switch (mode)
							{
								case DisplayMode.Metrics:
								{
									ShowMetricsScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Latency:
								{
									ShowLatencyScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Transactions:
								{
									ShowTransactionScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Processes:
								{
									ShowProcessesScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Roles:
								{
									ShowRolesScreen(status, metric, repaint);
									break;
								}
								case DisplayMode.Help:
								{
									ShowHelpScreen(repaint);
									break;
								}
							}
							repaint = false;
							updated = false;
							Screen.Paint();
						}

						await Task.Delay(100, cancel);
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

		private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		private const int MAX_HISTORY_CAPACITY = 100;
		private static readonly RingBuffer<HistoryMetric> History = new(MAX_HISTORY_CAPACITY);

		private const int MAX_RW_WIDTH = 40;
		private const int MAX_WS_WIDTH = 20;

		private class HistoryMetric
		{
			public bool Available { get; init; }

			public TimeSpan LocalTime { get; init; }
			public long ReadVersion { get; init; }

			public long Timestamp { get; init; }

			public double ReadsPerSecond { get; init; }
			public double WritesPerSecond { get; init; }
			public double WrittenBytesPerSecond { get; init; }

			public double TransStarted { get; init; }
			public double TransCommitted { get; init; }
			public double TransConflicted { get; init; }

			public double LatencyCommit { get; init; }
			public double LatencyRead { get; init; }
			public double LatencyStart { get; init; }
		}

		private static char GetBarChar(double scale) => scale switch
		{
			>= 1000000 => '@',
			>= 100000 => '#',
			>= 10000 => '|',
			_ => ':'
		};

		private static int Bar(double hz, double scale, int width)
		{
			if (hz == 0) return 0;

			var x = hz * width / scale;
			if (x < 0 || double.IsNaN(x)) x = 0;
			else if (x > width) x = width;
			return x == 0 ? 0 : x < 1 ? 1 : (int) Math.Round(x, MidpointRounding.AwayFromZero);
		}

		private static string BarGraph(double hz, double scale, int width, char full, string half, string nonZero)
		{
			var x = hz * (width * 2) / scale;
			if (x < 0 || double.IsNaN(x)) x = 0;
			else if (x > width * 2) x = width * 2;
			int n = (int) Math.Round(x, MidpointRounding.AwayFromZero);

			if (n == 0) return hz == 0 ? string.Empty : nonZero;
			if (n == 1) return half;
			if (n % 2 == 1) return new string(full, n / 2) + half;
			return new string(full, n / 2);
		}

		private const double KIBIBYTE = 1024.0;
		private const double MEBIBYTE = 1024.0 * 1024.0;
		private const double GIBIBYTE = 1024.0 * 1024.0 * 1024.0;
		private const double TEBIBYTE = 1024.0 * 1024.0 * 1024.0 * 1024.0;

		private static double KiloBytes(long x) => x / KIBIBYTE;

		private static double GigaBytes(long x) => x / GIBIBYTE;

		private static double MegaBytes(long x) => x / MEBIBYTE;

		private static double MegaBytes(double x) => x / MEBIBYTE;

		private static double TeraBytes(double x) => x / TEBIBYTE;

		private static string FriendlyBytes(long x)
		{
			if (x == 0) return "-";
			if (x < 900 * KIBIBYTE) return KiloBytes(x).ToString("N1", CultureInfo.InvariantCulture) + " KB";
			if (x < 900 * MEBIBYTE) return MegaBytes(x).ToString("N1", CultureInfo.InvariantCulture) + " MB";
			if (x < 900 * GIBIBYTE) return GigaBytes(x).ToString("N1", CultureInfo.InvariantCulture) + " GB";
			return TeraBytes(x).ToString("N1", CultureInfo.InvariantCulture) + " TB";
		}

		[StructLayout(LayoutKind.Sequential, Pack = 0)]
		private struct Texel
		{
			public int Value;

			public readonly char Text
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (char) (this.Value & 0xFFFF);
			}

			public readonly ConsoleColor ForegroundColor
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (ConsoleColor) ((this.Value >> 16) & 0xFF);
			}

			public readonly ConsoleColor BackgroundColor
			{
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => (ConsoleColor) ((this.Value >> 24) & 0xFF);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public void Set(char text, ConsoleColor foreground, ConsoleColor background)
			{
				this.Value = text | (((int) foreground) << 16) | (((int) background) << 24);
			}

			public readonly override string ToString()
			{
				return $"'{this.Text}' [{this.ForegroundColor}]";
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public static int GetRawValue(char text, ConsoleColor foreground, ConsoleColor background)
			{
				return text | (((int) foreground) << 16) | (((int) background) << 24);
			}
		}

		private sealed class FrameBuffer
		{
			public int Width { get; }

			public int Height { get; }

			public int Stride { get; }

			public Texel[] Visible { get; }

			public Texel[] Current { get; }

			public FrameBuffer(int width, int height)
			{
				this.Width = width;
				this.Height = height;
				int stride = width;
				this.Stride = stride;
				this.Current = new Texel[stride * height];
				this.Visible = new Texel[stride * height];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Contains(int x, int y)
			{
				return ((uint) x < this.Width) & ((uint) y < this.Height);
			}

			public void Set(int x, int y, char value, ConsoleColor foreground, ConsoleColor background)
			{
				if (Contains(x, y))
				{
					this.Current[y * this.Stride + x].Set(value, foreground, background);
				}
			}

			public bool Mask(ref int x, int y, ref int offset, ref int count)
			{
				if ((uint) y >= this.Height)
				{ // completely off screen
					offset = 0;
					return false;
				}

				if (x < 0)
				{ // truncate left
					offset = checked(offset - x);
					count = checked(count + x);
					x = 0;
				}

				if (x + count > this.Width)
				{ // truncate right
					count = checked(this.Width - x);
				}

				return count > 0;
			}

			public void Write(int x, int y, string text, ConsoleColor color, ConsoleColor background)
			{
				int count = text.Length;
				int index = 0;
				if (!Mask(ref x, y, ref index, ref count))
				{ // not visible
					return;
				}

				var buffer = this.Current;
				unsafe
				{
					int offset = (y * this.Stride) + x;
					//Debug.WriteLine($"x:{x}, y:{y}, off={offset}; index={index}; count={count}; text='{text}'");
					fixed (Texel* ptr = &buffer[offset])
					{
						for (int i = 0; i < count; i++)
						{
							ptr[i].Set(text[index + i], color, background);
						}
					}
				}
			}

			public void Clear(int x, int y, int count, ConsoleColor color, ConsoleColor background)
			{
				int _ = 0;
				if (!Mask(ref x, y, ref _, ref count))
				{ // not visible
					return;
				}

				var buffer = this.Current;
				int raw = Texel.GetRawValue(' ', color, background);
				unsafe
				{
					fixed (Texel* ptr = &buffer[y * this.Stride + x])
					{
						for (int i = 0; i < count; i++)
						{
							ptr[i].Value = raw;
						}
					}
				}

			}

			public void Clear(ConsoleColor color, ConsoleColor background)
			{
				var buffer = this.Current;
				int raw = Texel.GetRawValue(' ', color, background);
				for (int i = 0; i < buffer.Length; i++)
				{
					buffer[i].Value = raw;
				}
			}

			public void Paint()
			{
				var col = ConsoleColor.White;
				var bgnd = ConsoleColor.Black;
				Console.ForegroundColor = col;
				Console.BackgroundColor = bgnd;
				unsafe
				{
					fixed (Texel* visible = &this.Visible[0])
					fixed (Texel* current = &this.Current[0])
					{
						for (int y = 0; y < this.Height; y++)
						{
							Texel* rowVisible = visible + (y * this.Stride);
							Texel* rowCurrent = current + (y * this.Stride);

							bool prev = true;
							for (int x = 0; x < this.Width; x++)
							{
								bool same = rowVisible[x].Value == rowCurrent[x].Value;
								if (same)
								{ // character is same as before
									prev = true;
									continue;
								}

								if (prev)
								{ // start of new "different" run
									Console.SetCursorPosition(x, y);
									prev = false;
								}
								var newCol = rowCurrent[x].ForegroundColor;
								if (col != newCol)
								{ // new color
									Console.ForegroundColor = newCol;
									col = newCol;
								}

								newCol = rowCurrent[x].BackgroundColor;
								if (bgnd != newCol)
								{ // new background
									Console.BackgroundColor = newCol;
									bgnd = newCol;
								}

								Console.Write(rowCurrent[x].Text);
							}
						}
					}
				}

				// swap buffers
				Array.Copy(this.Current, 0, this.Visible, 0, this.Current.Length);
			}

		}

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		private static FrameBuffer Screen;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

		private static ConsoleColor CurrentColor = ConsoleColor.White;
		private static ConsoleColor CurrentBackground = ConsoleColor.Black;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void SetColor(ConsoleColor color)
		{
			CurrentColor = color;
		}

		private static void SetBackground(ConsoleColor color)
		{
			CurrentBackground = color;
		}

		private static void SetTitle(string title)
		{
			if (OperatingSystem.IsWindows())
			{
				Console.Title = title;
			}
		}

		private static string GetTitle()
		{
			if (OperatingSystem.IsWindows())
			{
				return Console.Title;
			}
			return string.Empty;
		}

		private static void WriteAt(int x, int y, string msg)
		{
			Screen.Write(x, y, msg, Program.CurrentColor, CurrentBackground);
		}

		private static void WriteAt(int x, int y, ref DefaultInterpolatedStringHandler message)
		{
			Screen.Write(x, y, string.Create(CultureInfo.InvariantCulture, ref message), CurrentColor, CurrentBackground);
		}

		private static double GetMax(RingBuffer<HistoryMetric> metrics, Func<HistoryMetric, double> selector)
		{
			double max = double.NaN;
			foreach (var item in metrics)
			{
				double x = selector(item);
				if (double.IsNaN(max) || x > max) max = x;
			}
			return max;
		}

		private static double GetMaxScale(double max)
		{
			return Math.Pow(10, Math.Ceiling(Math.Log10(max)));
		}

		private static ConsoleColor FrequencyColor(double hz)
		{
			return hz >= 100 ? ConsoleColor.White : hz >= 10 ? ConsoleColor.Gray : ConsoleColor.DarkCyan;
		}

		private static ConsoleColor DiskSpeedColor(double bps)
		{
			return bps >= 1048576 ? ConsoleColor.White : bps >= 1024 ? ConsoleColor.Gray : ConsoleColor.DarkGray;
		}

		private static ConsoleColor LatencyColor(double x)
		{
			return x >= 1 ? ConsoleColor.Red
				: x >= 0.1 ? ConsoleColor.Yellow
				: x >= 0.01 ? ConsoleColor.White
				: ConsoleColor.Gray;
		}

		private const int TOP_COL0 = 1;
		private const int TOP_COL1 = TOP_COL0 + 24;
		private const int TOP_COL2 = TOP_COL1 + 26;
		private const int TOP_COL3 = TOP_COL2 + 36;
		private const int TOP_COL4 = TOP_COL3 + 22;

		private const int TOP_ROW0 = 0;
		private const int TOP_ROW1 = 1;
		private const int TOP_ROW2 = 2;

		private static void RepaintTopBar(string screen)
		{
			SetColor(ConsoleColor.White);
			SetBackground(ConsoleColor.Black);
			Screen.Clear(Program.CurrentColor, Program.CurrentBackground);

			SetColor(ConsoleColor.DarkGray);
			WriteAt(TOP_COL0, TOP_ROW0, $"Reads  : {"",8} Hz");
			WriteAt(TOP_COL0, TOP_ROW1, $"Writes : {"",8} Hz");
			WriteAt(TOP_COL0, TOP_ROW2, $"Written: {"",8} MB/s");

			WriteAt(TOP_COL1, TOP_ROW0, $"Total K/V: {"",10} MB");
			WriteAt(TOP_COL1, TOP_ROW1, $"Disk Used: {"",10} MB");
			WriteAt(TOP_COL1, TOP_ROW2, string.Format("Shards: {0,5} x{0,6} MB", ""));

			WriteAt(TOP_COL2, TOP_ROW0, $"Server Time : {"",19}");
			WriteAt(TOP_COL2, TOP_ROW1, $"Client Time : {"",19}");
			WriteAt(TOP_COL2, TOP_ROW2, $"Read Version: {"",10}");

			WriteAt(TOP_COL3, TOP_ROW0, $"Coordinat.: {"",10}");
			WriteAt(TOP_COL3, TOP_ROW1, $"Storage   : {"",10}");
			WriteAt(TOP_COL3, TOP_ROW2, $"Redundancy: {"",10}");

			WriteAt(TOP_COL4, TOP_ROW0, $"State: {"",10}");
			WriteAt(TOP_COL4, TOP_ROW1, $"Data : {"",20}");
			WriteAt(TOP_COL4, TOP_ROW2, $"Perf.: {"",20}");

			// Bottom

			SetBackground(ConsoleColor.DarkCyan);
			WriteAt(0, ScreenHeight - 1, new string(' ', ScreenWidth - 1));
			SetColor(screen == "metrics" ? ConsoleColor.White : ConsoleColor.Black);
			WriteAt(0, ScreenHeight - 1, " [M]etrics ");
			SetColor(screen == "transactions" ? ConsoleColor.White : ConsoleColor.Black);
			WriteAt(11, ScreenHeight - 1, " [T]ransactions ");
			SetColor(screen == "latency" ? ConsoleColor.White : ConsoleColor.Black);
			WriteAt(27, ScreenHeight - 1, " [L]atency ");
			SetColor(screen == "processes" ? ConsoleColor.White : ConsoleColor.Black);
			WriteAt(38, ScreenHeight - 1, " [P]rocesses ");
			SetColor(screen == "roles" ? ConsoleColor.White : ConsoleColor.Black);
			WriteAt(51, ScreenHeight - 1, " [R]roles ");
			SetBackground(ConsoleColor.Black);
		}

		private static void UpdateTopBar(FdbSystemStatus status, HistoryMetric current)
		{
			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL0 + 9, TOP_ROW0, $"{current.ReadsPerSecond,8:N0}");
			WriteAt(TOP_COL0 + 9, TOP_ROW1, $"{current.WritesPerSecond,8:N0}");
			WriteAt(TOP_COL0 + 9, TOP_ROW2, $"{MegaBytes(current.WrittenBytesPerSecond),8:N2}");

			WriteAt(TOP_COL1 + 11, TOP_ROW0, $"{MegaBytes(status.Cluster.Data.TotalKVUsedBytes),10:N1}");
			WriteAt(TOP_COL1 + 11, TOP_ROW1, $"{MegaBytes(status.Cluster.Data.TotalDiskUsedBytes),10:N1}");
			WriteAt(TOP_COL1 + 8, TOP_ROW2, $"{status.Cluster.Data.PartitionsCount,5:N0}");
			WriteAt(TOP_COL1 + 15, TOP_ROW2, $"{MegaBytes(status.Cluster.Data.AveragePartitionSizeBytes),6:N1}");

			var serverTime = Epoch.AddSeconds(current.Timestamp);
			var clientTime = Epoch.AddSeconds(status.Client.Timestamp);
			WriteAt(TOP_COL2 + 14, TOP_ROW0, $"{serverTime.ToString("u"),19}");
			SetColor(Math.Abs((serverTime - clientTime).TotalSeconds) >= 20 ? ConsoleColor.Red : ConsoleColor.White);
			WriteAt(TOP_COL2 + 14, TOP_ROW1, $"{clientTime.ToString("u"),19}");
			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL2 + 14, TOP_ROW2, $"{current.ReadVersion:N0}");

			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL3 + 12, TOP_ROW0, $"{status.Cluster.Configuration.CoordinatorsCount,-10}");
			WriteAt(TOP_COL3 + 12, TOP_ROW1, $"{status.Cluster.Configuration.StorageEngine,-10}");
			WriteAt(TOP_COL3 + 12, TOP_ROW2, $"{status.Cluster.Configuration.RedundancyMode,-10}");

			if (!status.Client.DatabaseAvailable)
			{
				SetColor(ConsoleColor.Red);
				WriteAt(TOP_COL4 + 7, TOP_ROW0, "UNAVAILABLE");
			}
			else
			{
				SetColor(ConsoleColor.Green);
				WriteAt(TOP_COL4 + 7, TOP_ROW0, "Available  ");
			}
			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL4 + 7, TOP_ROW1, $"{status.Cluster.Data.StateName,-40}");
			WriteAt(TOP_COL4 + 7, TOP_ROW2, $"{status.Cluster.Qos.PerformanceLimitedBy.Name,-40}");

			//SetColor(ConsoleColor.Gray);
			//var msgs = status.Cluster.Messages.Concat(status.Client.Messages).ToArray();
			//for (int i = 0; i < 4; i++)
			//{
			//	string msg = msgs.Length > i ? msgs[i].Name : "";
			//	WriteAt(118, i + 1, "{0,-40}", msg.Length < 50 ? msg : msg.Substring(0, 40));
			//}

		}

		private static void FailToBar()
		{
			SetColor(ConsoleColor.DarkRed);
			WriteAt(TOP_COL0 + 9, TOP_ROW0, $"{"?",8:N0}");
			WriteAt(TOP_COL0 + 9, TOP_ROW1, $"{"?",8:N0}");
			WriteAt(TOP_COL0 + 9, TOP_ROW2, $"{"?",8:N2}");

			WriteAt(TOP_COL1 + 11, TOP_ROW0, $"{"?",10:N1}");
			WriteAt(TOP_COL1 + 11, TOP_ROW1, $"{"?",10:N1}");
			WriteAt(TOP_COL1 + 8, TOP_ROW2, $"{"?",5:N0}");
			WriteAt(TOP_COL1 + 15, TOP_ROW2, $"{"?",6:N1}");

			var clientTime = DateTime.Now;
			WriteAt(TOP_COL2 + 14, TOP_ROW0, $"{"?",19}");
			WriteAt(TOP_COL2 + 14, TOP_ROW1, $"{clientTime,19}");
			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL2 + 14, TOP_ROW2, $"{"?":N0}");

			SetColor(ConsoleColor.White);
			WriteAt(TOP_COL3 + 12, TOP_ROW0, $"{"?",-10}");
			WriteAt(TOP_COL3 + 12, TOP_ROW1, $"{"?",-10}");
			WriteAt(TOP_COL3 + 12, TOP_ROW2, $"{"?",-10}");

			WriteAt(TOP_COL4 + 7, TOP_ROW0, "UNAVAILABLE");

			WriteAt(TOP_COL4 + 7, TOP_ROW1, $"{"cluster_unreachable",-40}");
			WriteAt(TOP_COL4 + 7, TOP_ROW2, $"{"?",-40}");
		}

		private static void ShowMetricsScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Metrics");
				RepaintTopBar("metrics");

				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "   Reads (Hz)");
				WriteAt(COL2, 5, "  Writes (Hz)");
				WriteAt(COL3, 5, "Disk Speed (MB/s)");
			}

			UpdateTopBar(status, current);

			double maxRead = GetMax(History, (m) => m.ReadsPerSecond);
			double maxWrite = GetMax(History, (m) => m.WritesPerSecond);
			double maxSpeed = GetMax(History, (m) => m.WrittenBytesPerSecond);
			double scaleRead = GetMaxScale(maxRead);
			double scaleWrite = GetMaxScale(maxWrite);
			double scaleSpeed = GetMaxScale(maxSpeed);

			SetColor(ConsoleColor.DarkGreen);
			WriteAt(COL1 + 14, 5, $"{maxRead,35:N0}");
			WriteAt(COL2 + 14, 5, $"{maxWrite,35:N0}");
			WriteAt(COL3 + 18, 5, $"{MegaBytes(maxSpeed),13:N3}");

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				if (y < ScreenHeight)
				{
					SetColor(ConsoleColor.DarkGray);
					WriteAt(
						1,
						y,
						string.Format("{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |", TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
							"")
					);

					if (metric.Available)
					{
						bool isMaxRead = maxRead > 0 && metric.ReadsPerSecond == maxRead;
						bool isMaxWrite = maxWrite > 0 && metric.WritesPerSecond == maxWrite;
						bool isMaxSpeed = maxSpeed > 0 && metric.WrittenBytesPerSecond == maxSpeed;

						SetColor(isMaxRead ? ConsoleColor.Cyan : FrequencyColor(metric.ReadsPerSecond));
						WriteAt(COL1, y, $"{metric.ReadsPerSecond,8:N0}");
						SetColor(isMaxWrite ? ConsoleColor.Cyan : FrequencyColor(metric.WritesPerSecond));
						WriteAt(COL2, y, $"{metric.WritesPerSecond,8:N0}");
						SetColor(isMaxSpeed ? ConsoleColor.Cyan : DiskSpeedColor(metric.WrittenBytesPerSecond));
						WriteAt(COL3, y, $"{MegaBytes(metric.WrittenBytesPerSecond),10:N3}");

						SetColor(metric.ReadsPerSecond > 10 ? ConsoleColor.Green : ConsoleColor.DarkCyan);
						WriteAt(COL1 + 9, y, metric.ReadsPerSecond == 0 ? "-" : new string(GetBarChar(metric.ReadsPerSecond), Bar(metric.ReadsPerSecond, scaleRead, MAX_RW_WIDTH)));
						SetColor(metric.WritesPerSecond > 10 ? ConsoleColor.Green : ConsoleColor.DarkCyan);
						WriteAt(COL2 + 9, y, metric.WritesPerSecond == 0 ? "-" : new string(GetBarChar(metric.WritesPerSecond), Bar(metric.WritesPerSecond, scaleWrite, MAX_RW_WIDTH)));
						SetColor(metric.WrittenBytesPerSecond > 1000 ? ConsoleColor.Green : ConsoleColor.DarkCyan);
						WriteAt(COL3 + 11, y, metric.WrittenBytesPerSecond == 0 ? "-" : new string(GetBarChar(metric.WrittenBytesPerSecond / 1000), Bar(metric.WrittenBytesPerSecond, scaleSpeed, MAX_WS_WIDTH)));
					}
					else
					{
						SetColor(ConsoleColor.DarkRed);
						WriteAt(COL1, y, $"{"x",8}");
						WriteAt(COL2, y, $"{"x",8}");
						WriteAt(COL3, y, $"{"x",8}");
					}
				}
				--y;
			}

			SetColor(ConsoleColor.Gray);
		}

		private static void ShowLatencyScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Latency");
				RepaintTopBar("latency");

				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "  Commit (ms)");
				WriteAt(COL2, 5, "    Read (ms)");
				WriteAt(COL3, 5, "   Start (ms)");
			}

			UpdateTopBar(status, current);

			double maxCommit = GetMax(History, (m) => m.LatencyCommit);
			double maxRead = GetMax(History, (m) => m.LatencyRead);
			double maxStart = GetMax(History, (m) => m.LatencyStart);
			double scaleCommit = GetMaxScale(maxCommit);
			double scaleRead = GetMaxScale(maxRead);
			double scaleStart = GetMaxScale(maxStart);

			SetColor(ConsoleColor.DarkGreen);
			WriteAt(COL1 + 14, 5, $"{maxCommit * 1000,35:N3}");
			WriteAt(COL2 + 14, 5, $"{maxRead * 1000,35:N3}");
			WriteAt(COL3 + 14, 5, $"{maxStart * 1000,18:N3}");

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				if (y < ScreenHeight)
				{
					SetColor(ConsoleColor.DarkGray);
					WriteAt(
						1,
						y,
						string.Format("{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |", TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
							"")
					);

					if (metric.Available)
					{
						bool isMaxRead = maxCommit > 0 && metric.LatencyCommit == maxCommit;
						bool isMaxWrite = maxRead > 0 && metric.LatencyRead == maxRead;
						bool isMaxSpeed = maxStart > 0 && metric.LatencyStart == maxStart;

						SetColor(isMaxRead ? ConsoleColor.Cyan : LatencyColor(metric.LatencyCommit));
						WriteAt(COL1, y, $"{metric.LatencyCommit * 1000,8:N3}");
						SetColor(isMaxWrite ? ConsoleColor.Cyan : LatencyColor(metric.LatencyRead));
						WriteAt(COL2, y, $"{metric.LatencyRead * 1000,8:N3}");
						SetColor(isMaxSpeed ? ConsoleColor.Cyan : LatencyColor(metric.LatencyStart));
						WriteAt(COL3, y, $"{metric.LatencyStart * 1000,10:N3}");

						SetColor(ConsoleColor.Green);
						WriteAt(COL1 + 9, y, metric.LatencyCommit == 0 ? "-" : new string('|', Bar(metric.LatencyCommit, scaleCommit, MAX_RW_WIDTH)));
						WriteAt(COL2 + 9, y, metric.LatencyRead == 0 ? "-" : new string('|', Bar(metric.LatencyRead, scaleRead, MAX_RW_WIDTH)));
						WriteAt(COL3 + 11, y, metric.LatencyStart == 0 ? "-" : new string('|', Bar(metric.LatencyStart, scaleStart, MAX_WS_WIDTH)));
					}
					else
					{
						SetColor(ConsoleColor.DarkRed);
						WriteAt(COL1, y, $"{"x",8}");
						WriteAt(COL2, y, $"{"x",8}");
						WriteAt(COL3, y, $"{"x",8}");
					}
				}

				--y;
			}

			SetColor(ConsoleColor.Gray);
		}

		private static void ShowTransactionScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int COL0 = 1;
			const int COL1 = COL0 + 11;
			const int COL2 = COL1 + 12 + MAX_RW_WIDTH;
			const int COL3 = COL2 + 12 + MAX_RW_WIDTH;

			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Transactions");
				RepaintTopBar("transactions");

				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL0, 5, "Elapsed");
				WriteAt(COL1, 5, "Started (tps)");
				WriteAt(COL2, 5, "Committed (tps)");
				WriteAt(COL3, 5, "Conflicted (tps)");
			}

			UpdateTopBar(status, current);

			double maxStarted = GetMax(History, (m) => m.TransStarted);
			double maxCommitted = GetMax(History, (m) => m.TransCommitted);
			double maxConflicted = GetMax(History, (m) => m.TransConflicted);
			double scaleStarted = GetMaxScale(maxStarted);
			double scaleComitted = GetMaxScale(maxCommitted);
			double scaleConflicted = GetMaxScale(maxConflicted);

			SetColor(ConsoleColor.DarkGreen);
			WriteAt(COL1 + 14, 5, $"{maxStarted,35:N0}");
			WriteAt(COL2 + 16, 5, $"{maxCommitted,33:N0}");
			WriteAt(COL3 + 16, 5, $"{maxConflicted,15:N0}");

			int y = 7 + History.Count - 1;
			foreach (var metric in History)
			{
				if (y < ScreenHeight)
				{
					SetColor(ConsoleColor.DarkGray);
					WriteAt(
						1,
						y,
						string.Format("{0} | {1,8} {1,40} | {1,8} {1,40} | {1,10} {1,20} |", TimeSpan.FromSeconds(Math.Round(metric.LocalTime.TotalSeconds, MidpointRounding.AwayFromZero)),
							"")
					);

					if (metric.Available)
					{
						bool isMaxRead = maxStarted > 0 && metric.LatencyCommit == maxStarted;
						bool isMaxWrite = maxCommitted > 0 && metric.LatencyRead == maxCommitted;
						bool isMaxSpeed = maxConflicted > 0 && metric.LatencyStart == maxConflicted;

						SetColor(isMaxRead ? ConsoleColor.Cyan : FrequencyColor(metric.TransStarted));
						WriteAt(COL1, y, $"{metric.TransStarted,8:N0}");
						SetColor(isMaxWrite ? ConsoleColor.Cyan : FrequencyColor(metric.TransCommitted));
						WriteAt(COL2, y, $"{metric.TransCommitted,8:N0}");
						SetColor(isMaxSpeed ? ConsoleColor.Cyan : FrequencyColor(metric.TransConflicted));
						WriteAt(COL3, y, $"{metric.TransConflicted,8:N1}");

						SetColor(metric.TransStarted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen);
						WriteAt(COL1 + 9, y, metric.TransStarted == 0 ? "-" : new string('|', Bar(metric.TransStarted, scaleStarted, MAX_RW_WIDTH)));
						SetColor(metric.TransCommitted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen);
						WriteAt(COL2 + 9, y, metric.TransCommitted == 0 ? "-" : new string('|', Bar(metric.TransCommitted, scaleComitted, MAX_RW_WIDTH)));
						SetColor(metric.TransConflicted > 1000 ? ConsoleColor.Red : metric.TransConflicted > 10 ? ConsoleColor.Green : ConsoleColor.DarkGreen);
						WriteAt(COL3 + 9, y, metric.TransConflicted == 0 ? "-" : new string('|', Bar(metric.TransConflicted, scaleConflicted, MAX_WS_WIDTH)));
					}
					else
					{
						SetColor(ConsoleColor.DarkRed);
						WriteAt(COL1, y, $"{"x",8}");
						WriteAt(COL2, y, $"{"x",8}");
						WriteAt(COL3, y, $"{"x",8}");
					}
				}

				--y;
			}

			SetColor(ConsoleColor.Gray);
		}

		private struct RoleMap
		{
			public bool Master;
			public bool ClusterController;
			public bool Proxy;
			public bool Log;
			public bool Storage;
			public bool Resolver;
			public bool RateKeeper;
			public bool DataDistributor;
			public bool CommitProxy;
			public bool GrvProxy;
			public bool Other;

			public void Add(string role)
			{
				switch(role)
				{
					case "master": this.Master = true; break;
					case "cluster_controller": this.ClusterController = true; break;
					case "proxy": this.Proxy = true; break;
					case "commit_proxy": this.Proxy = true; this.CommitProxy = true; break;
					case "grv_proxy": this.Proxy = true; this.GrvProxy = true; break;
					case "log": this.Log = true; break;
					case "storage": this.Storage = true; break;
					case "resolver": this.Resolver = true; break;
					case "ratekeeper": this.Other = true; this.RateKeeper = true; break;
					case "data_distributor": this.Other = true; this.DataDistributor = true; break;
					default: this.Other = true; break;
				}
			}

			public void Reset()
			{
				this.Master = false;
				this.ClusterController = false;
				this.Proxy = false;
				this.CommitProxy = false;
				this.GrvProxy = false;
				this.Log = false;
				this.Storage = false;
				this.Resolver = false;
				this.Other = false;
				this.RateKeeper = false;
				this.DataDistributor = false;
			}

			public readonly override string ToString()
			{
				var sb = new StringBuilder(11);
				sb.Append(this.Master ? "M" : "-");
				sb.Append(this.ClusterController ? "C" : "-");
				sb.Append(this.Proxy ? "P" : "-");
				sb.Append(this.CommitProxy ? "c" : "-");
				sb.Append(this.GrvProxy? "g" : "-");
				sb.Append(this.Log ? "L" : "-");
				sb.Append(this.Storage ? "S" : "-");
				sb.Append(this.Resolver ? "R" : "-");
				sb.Append(this.Other ? "O" : "-");
				sb.Append(this.RateKeeper ? "r" : "-");
				sb.Append(this.DataDistributor ? "d" : "-");
				return sb.ToString();
			}
		}

		private static int LastProcessYMax;

		private static ConsoleColor MapDiskOpsToColor(double ops)
		{
			return ops < 500 * KIBIBYTE ? ConsoleColor.DarkGray
			     : ops < 5 * MEBIBYTE ? ConsoleColor.Gray
			     : ops < 25 * MEBIBYTE ? ConsoleColor.White
			     : ops > 100 * MEBIBYTE ? ConsoleColor.Red
			     : ConsoleColor.Cyan;
		}

		private static ConsoleColor MapQueueSizeToColor(double value)
		{
			return value < 10 * MEBIBYTE ? ConsoleColor.DarkGray
			     : value < 100 * MEBIBYTE ? ConsoleColor.Gray
			     : value < 1 * GIBIBYTE ? ConsoleColor.White
			     : value < 5 * GIBIBYTE ? ConsoleColor.Cyan
			     : value < 10 * GIBIBYTE ? ConsoleColor.DarkYellow
			     : ConsoleColor.DarkRed;
		}

		private static ConsoleColor MapMegabitsToColor(double megaBits)
		{
			megaBits /= 8;
			return megaBits < 0.1 ? ConsoleColor.DarkGray
			     : megaBits >= 1000 ? ConsoleColor.Red
			     : megaBits >= 100 ? ConsoleColor.DarkRed
			     : megaBits >= 50 ? ConsoleColor.DarkYellow
			     : megaBits >= 10 ? ConsoleColor.Cyan
			     : megaBits >= 1 ? ConsoleColor.White
			     : ConsoleColor.Gray;
		}

		private static ConsoleColor MapConnectionsToColor(long connections)
		{
			return connections < 10 ? ConsoleColor.DarkGray
			     : connections < 50 ? ConsoleColor.Gray
			     : connections < 100 ? ConsoleColor.White
			     : connections < 250 ? ConsoleColor.Cyan
			     : connections < 500 ? ConsoleColor.DarkYellow
			     : ConsoleColor.DarkRed;
		}

		private static ConsoleColor MapMemoryToColor(long value)
		{
			return value < GIBIBYTE ? ConsoleColor.DarkGray
			     : value <= 3 * GIBIBYTE ? ConsoleColor.Gray
			     : value <= 5 * GIBIBYTE ? ConsoleColor.White
			     : value <= 7 * GIBIBYTE ? ConsoleColor.DarkYellow
			     : ConsoleColor.DarkRed;
		}


		private static void ShowProcessesScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int CPU_BARSZ = 10;
			const int MEM_BARSZ = 5;
			const int HDD_BARSZ = 10;

			const int SP = 1;
			const int BAR = SP + 1 + SP;

			const int COL_HOST = 1;
			const int LEN_HOST = 16;
			const int COL_NET = COL_HOST + LEN_HOST + BAR;
			const int LEN_NET = 4 + SP + 8 + SP + 8;
			const int COL_CPU = COL_NET + LEN_NET + BAR;
			const int LEN_CPU = 5 + 1 + SP + CPU_BARSZ;
			const int COL_MEM_USED = COL_CPU + LEN_CPU + BAR;
			const int LEN_MEM_USED = 7 + SP;
			const int COL_MEM_TOTAL = COL_MEM_USED + LEN_MEM_USED;
			const int LEN_MEM_TOTAL = 9 + MEM_BARSZ;
			const int COL_DISK = COL_MEM_TOTAL + LEN_MEM_TOTAL; // HDD R/W
			const int LEN_DISK = 8 + SP + 7 + SP + 7;
			const int COL_HDD = COL_DISK + LEN_DISK + BAR;  // HDD Busy%
			const int LEN_HDD = 5 + 1 + SP + HDD_BARSZ;
			const int COL_ROLES = COL_HDD + LEN_HDD + BAR; // Roles
			const int LEN_ROLES = 11;
			const int COL9 = COL_ROLES + LEN_ROLES + BAR;

			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Processes");
				RepaintTopBar("processes");
				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL_HOST, 5, "Address (port)");
				WriteAt(COL_NET, 5, "Network (Mbps)");
				WriteAt(COL_NET, 6, " Cnx     Recv     Sent");
				WriteAt(COL_CPU, 5, "CPU Activity");
				WriteAt(COL_CPU, 6, " %core");
				WriteAt(COL_MEM_USED, 5, "Memory Activity (GB)");
				WriteAt(COL_MEM_USED, 6, " Used / Total");
				WriteAt(COL_DISK, 5, "Disk Activity (MB/s)");
				WriteAt(COL_DISK, 6, "   Queue Queried Mutated   HDD Busy");
				WriteAt(COL_ROLES, 5, "Roles");
				WriteAt(COL9, 5, "Uptime");

				LastProcessYMax = 0;

#if DEBUG_LAYOUT
				SetColor(ConsoleColor.DarkGray);
				WriteAt(COL_HOST, 4, "0 - - - - - -");
				WriteAt(COL_NET, 4, "1 - - - - - -");
				WriteAt(COL_CPU, 4, "3 - - - - - -");
				WriteAt(COL_MEM_USED, 4, "4 - - - - - -");
				WriteAt(COL_MEM_TOTAL, 4, "5 - - - - - -");
				WriteAt(COL_DISK, 4, "6 - - - - - -");
				WriteAt(COL_HDD, 4, "7 - - - - - -");
				WriteAt(COL_ROLES, 4, "8 - - - - - -");
#endif
			}

			UpdateTopBar(status, current);

			if (status.Cluster.Machines.Count == 0)
			{
				SetColor(ConsoleColor.Red);
				WriteAt(COL_HOST, 7, "No machines found!");
				//TODO display error message?
				return;
			}

			var maxVersion = status.Cluster.Processes.Values.Max(p => p.Version);

			string emptyLine = new string(' ', ScreenWidth);

			var map = new RoleMap();

			int y = 7;
			foreach(var machine in status.Cluster.Machines.Values.OrderBy(x => x.Address, StringComparer.Ordinal))
			{
				var procs = status.Cluster.Processes.Values
					.Where(p => p.MachineId == machine.Id)
					.OrderBy(p => p.Address, StringComparer.Ordinal)
					.ToList();

				long totalCnx = 0, totalQueueSize = 0;
				double totalDiskBusy = 0, totalMutationBytes = 0, totalQueriedBytes = 0;

				map.Reset();
				foreach(var proc in procs)
				{
					totalDiskBusy += proc.Disk.Busy;
					totalCnx += proc.Network.CurrentConnections;
					foreach (var role in proc.Roles)
					{
						map.Add(role.Role);
						switch (role)
						{
							case StorageRoleMetrics storage:
							{
								totalMutationBytes += storage.MutationBytes.Hz;
								totalQueriedBytes += storage.BytesQueried.Hz;
								totalQueueSize += storage.InputBytes.Counter - storage.DurableBytes.Counter;
								break;
							}
							case LogRoleMetrics:
							{
								break;
							}
						}
					}
				}
				totalDiskBusy /= procs.Count;

				SetColor(ConsoleColor.DarkGray);
				WriteAt(1, y,"                 | ____ ________ ________ | _____% __________ | _____ / _____ _____ | ________ _______ _______ | _____% __________ | ___________ | ".Replace('_', ' '));

				SetColor(ConsoleColor.White);
				//"{0,-15} | net {2,8:N3} in {3,8:N3} out | cpu {4,5:N1}% | mem {5,5:N1} / {7,5:N1} GB {8,-20} | hdd {9,5:N1}% {10,-20}",
				WriteAt(COL_HOST, y, machine.Address);
				SetColor(MapConnectionsToColor(totalCnx));
				WriteAt(COL_NET, y, $"{totalCnx,4:N0}");
				SetColor(MapMegabitsToColor(machine.Network.MegabitsReceived.Hz));
				WriteAt(COL_NET + 5, y, $"{machine.Network.MegabitsReceived.Hz,8:N2}");
				SetColor(MapMegabitsToColor(machine.Network.MegabitsSent.Hz));
				WriteAt(COL_NET + 14, y, $"{machine.Network.MegabitsSent.Hz,8:N2}");

				WriteAt(COL_CPU, y, $"{machine.Cpu.LogicalCoreUtilization * 100,5:N1}");

				WriteAt(COL_MEM_USED, y, $"{GigaBytes(machine.Memory.CommittedBytes),5:N1}");
				WriteAt(COL_MEM_TOTAL, y, $"{GigaBytes(machine.Memory.TotalBytes),5:N1}");

				SetColor(ConsoleColor.DarkGray);
				WriteAt(COL_ROLES, y, $"{map,11}");

				SetColor(machine.Cpu.LogicalCoreUtilization >= 0.9 ? ConsoleColor.Red : ConsoleColor.Green);
				WriteAt(COL_CPU + 7, y, $"{BarGraph(machine.Cpu.LogicalCoreUtilization, 1, CPU_BARSZ, '=', ":", "."),-10}"); // 1 = all the (logical) cores

				double memRatio = 1.0 * machine.Memory.CommittedBytes / machine.Memory.TotalBytes;
				SetColor(memRatio >= 0.95 ? ConsoleColor.Red : memRatio >= 0.79 ? ConsoleColor.DarkYellow : ConsoleColor.Green);
				WriteAt(COL_MEM_TOTAL + 6, y, $"{BarGraph(machine.Memory.CommittedBytes, machine.Memory.TotalBytes, MEM_BARSZ, '=', ":", "."),-5}");

				if (map.Log | map.Storage)
				{
					SetColor(MapQueueSizeToColor(totalQueueSize));
					WriteAt(COL_DISK, y, $"{FriendlyBytes(totalQueueSize),8}");
				}
				if (map.Storage)
				{
					SetColor(MapDiskOpsToColor(totalQueriedBytes));
					WriteAt(COL_DISK + 9, y, $"{MegaBytes(totalQueriedBytes),7:N1}");
					SetColor(MapDiskOpsToColor(totalMutationBytes));
					WriteAt(COL_DISK + 17, y, $"{MegaBytes(totalMutationBytes),7:N1}");
				}

				SetColor(ConsoleColor.Gray);
				WriteAt(COL_HDD, y, $"{totalDiskBusy * 100,5:N1}");
				SetColor(totalDiskBusy == 0.0 ? ConsoleColor.DarkGray : totalDiskBusy >= 0.95 ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen);
				WriteAt(COL_HDD + 7, y, $"{BarGraph(totalDiskBusy, 1, 10, '=', ":", "."),-10}");

				++y;

				//TODO: use a set to map procs ot machines? Where(..) will be slow if there are a lot of machines x processes
				foreach (var proc in procs)
				{
					int p = proc.Address.IndexOf(':');
					string port = p >= 0 ? proc.Address.Substring(p + 1) : proc.Address;

					map.Reset();
					double mutationBytes = 0, queriedBytes = 0;
					long queueSize = 0;
					foreach (var role in proc.Roles)
					{
						map.Add(role.Role);
						if (role is StorageRoleMetrics storage)
						{
							mutationBytes += storage.MutationBytes.Hz;
							queriedBytes += storage.BytesQueried.Hz;
							queueSize += storage.InputBytes.Counter - storage.DurableBytes.Counter;
						}
						else if (role is LogRoleMetrics log)
						{
							queueSize += log.InputBytes.Counter - log.DurableBytes.Counter;
						}
					}

					if (y < ScreenHeight)
					{
						SetColor(ConsoleColor.DarkGray);
						WriteAt(
							COL_HOST,
							y,
							string.Format("_______ | ______ | ____ ________ ________ | _____% __________ | _____ / _____ _____ | ________ _______ _______ | _____% __________ | ___________ |".Replace('_', ' '), "")
						);

						SetColor(proc.Version != maxVersion ? ConsoleColor.DarkRed : ConsoleColor.DarkGray);
						WriteAt(COL_HOST + 10, y, $"{proc.Version,6}");

						SetColor(proc.Excluded ? ConsoleColor.DarkRed : ConsoleColor.Gray);
						WriteAt(COL_HOST, y, $"{port,7}");

						SetColor(MapConnectionsToColor(proc.Network.CurrentConnections));
						WriteAt(COL_NET, y, $"{proc.Network.CurrentConnections,4:N0}");
						SetColor(MapMegabitsToColor(proc.Network.MegabitsReceived.Hz));
						WriteAt(COL_NET + 5, y, $"{Nice(proc.Network.MegabitsReceived.Hz, "-", 0.005, "~"),8:N2}");
						SetColor(MapMegabitsToColor(proc.Network.MegabitsSent.Hz));
						WriteAt(COL_NET + 14, y, $"{Nice(proc.Network.MegabitsSent.Hz, "-", 0.005, "~"),8:N2}");

						var cpuUsage = proc.Cpu.UsageCores;
						SetColor(cpuUsage >= 0.95 ? ConsoleColor.DarkRed : cpuUsage >= 0.75 ? ConsoleColor.DarkYellow : cpuUsage >= 0.2 ? ConsoleColor.Gray : ConsoleColor.DarkGray);
						WriteAt(COL_CPU, y, $"{cpuUsage * 100,5:N1}");
						SetColor(cpuUsage >= 0.95 ? ConsoleColor.DarkRed : cpuUsage >= 0.75 ? ConsoleColor.DarkYellow : ConsoleColor.DarkGreen);
						WriteAt(COL_CPU + 7, y, $"{BarGraph(proc.Cpu.UsageCores, 1, CPU_BARSZ, '|', ":", "."),-10}");


						long memoryUsed = proc.Memory.UsedBytes - proc.Memory.UnusedAllocatedMemory;
						long memoryAllocated = proc.Memory.UsedBytes;
						SetColor(MapMemoryToColor(memoryUsed));
						WriteAt(COL_MEM_USED, y, $"{GigaBytes(memoryUsed),5:N1}");
						SetColor(MapMemoryToColor(memoryAllocated));
						WriteAt(COL_MEM_TOTAL, y, $"{GigaBytes(memoryAllocated),5:N1}");
						SetColor(memoryUsed >= 0.9 * proc.Memory.LimitBytes ? ConsoleColor.DarkRed : memoryUsed >= 0.75 * proc.Memory.LimitBytes ? ConsoleColor.DarkYellow : ConsoleColor.DarkGreen);
						WriteAt(COL_MEM_TOTAL + 6, y, $"{BarGraph(memoryUsed, machine.Memory.CommittedBytes, MEM_BARSZ, '|', ":", "."),-5}");

						if (map.Log | map.Storage)
						{
							SetColor(MapQueueSizeToColor(queueSize));
							WriteAt(COL_DISK, y, $"{FriendlyBytes(queueSize),8}");
						}
						if (map.Storage)
						{
							SetColor(MapDiskOpsToColor(queriedBytes));
							WriteAt(COL_DISK + 9, y, $"{MegaBytes(queriedBytes),7:N1}");
							SetColor(MapDiskOpsToColor(mutationBytes));
							WriteAt(COL_DISK + 17, y, $"{MegaBytes(mutationBytes),7:N1}");
						}

						SetColor(ConsoleColor.Gray);
						WriteAt(COL_HDD, y, $"{proc.Disk.Busy * 100,5:N1}");
						SetColor(proc.Disk.Busy == 0.0 ? ConsoleColor.DarkGray : proc.Disk.Busy >= 0.95 ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen);
						WriteAt(COL_HDD + 7, y, $"{new string('|', Bar(proc.Disk.Busy, 1, 10)),-10}");

						SetColor(ConsoleColor.Gray);
						WriteAt(COL_ROLES, y, $"{map,11}");

						SetColor(ConsoleColor.DarkGray);
						WriteAt(COL9, y, $"{proc.Uptime.ToString(@"d\.hh\:mm\:ss"),11}");

					}
					++y;
				}

				WriteAt(COL_HOST, y, emptyLine);
				++y;
			}

			// clear the extra lines from the previous repaint
			y = Math.Min(y, ScreenHeight);
			for (; y < LastProcessYMax; y++)
			{
				WriteAt(COL_HOST, y, emptyLine);
			}
			LastProcessYMax = y;

		}

		private static object Nice(double value, object zero, double? epsilon = null, object? small = null)
		{
			if (value == 0) return zero;
			if (epsilon != null && value < epsilon.Value) return small ?? ".";
			return value;
		}

		private static string GetHostFromAddress(string address)
		{
			int p = address.IndexOf(':');
			if (p < 0) return address;
			return address.Substring(0, p);
		}

		private static string GetPortFromAddress(string address)
		{
			int p = address.IndexOf(':');
			if (p < 0) return string.Empty;
			return address.Substring(p + 1);
		}

		private static void ShowRolesScreen(FdbSystemStatus status, HistoryMetric current, bool repaint)
		{
			const int CPU_BARSZ = 10;
			const int HDD_BARSZ = 10;

			const int SP = 1;
			const int BAR = 3;

			const int COL0 = 1;
			const int COL_NET = COL0 + 27;
			const int COL2 = COL_NET + 8;
			const int COL_CPU = COL2 + 8 + 2;
			const int LEN_CPU = 10 + CPU_BARSZ;

			const int COL_MEMORY = COL_CPU + LEN_CPU;
			const int LEN_MEMORY = 8;

			const int COL_HDD = COL_MEMORY + LEN_MEMORY + BAR; // HDD Busy%
			const int LEN_HDD = 5 + 1 + SP + HDD_BARSZ;

			const int COL_STORAGE = COL_HDD + LEN_HDD + BAR; // HDD R/W
			const int LEN_STORAGE = 8 + SP + 7 + SP + 7 + SP + 8;

			const int COL_DATAVERSION = COL_STORAGE + LEN_STORAGE + BAR; // Roles
			const int LEN_DATAVERSION = 14;

			const int COL_KVSTORE = COL_DATAVERSION + LEN_DATAVERSION + BAR;

			int y = 5;

			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Roles");
				RepaintTopBar("roles");

				LastProcessYMax = 0;


#if DEBUG_LAYOUT
				SetColor(ConsoleColor.DarkGray);
				WriteAt(COL0, 4, "0 - - - - - -");
				WriteAt(COL_NET, 4, "1 - - - - - -");
				WriteAt(COL2, 4, "2 - - - - - -");
				WriteAt(COL_CPU, 4, "3 - - - - - -");
				WriteAt(COL_MEMORY, 4, "4 - - - - - -");
				WriteAt(COL_STORAGE, 4, "6 - - - - - -");
				WriteAt(COL_HDD, 4, "7 - - - - - -");
				WriteAt(COL_DATAVERSION, 4, "8 - - - - - -");
#endif
			}

			UpdateTopBar(status, current);

			if (status.Cluster.Machines.Count == 0)
			{
				SetColor(ConsoleColor.Red);
				WriteAt(COL0, y, "No machines found!");
				//TODO display error message?
				return;
			}

			string emptyLine = new string(' ', ScreenWidth);

			var byRoles = status.Cluster.Processes
				.SelectMany(kv => kv.Value.Roles.Select(r => (Process: kv.Value, Role: r, MachineId: kv.Value.MachineId)))
				.ToLookup(x => x.Role.Role);

			foreach (var roleId in new [] { "log", "storage", "proxy", "commit_proxy", "grv_proxy", "resolver", "master", "cluster_controller", "data_distributor", "ratekeeper" })
			{
				var kv = byRoles[roleId].ToList();

				bool hasDisk = roleId == "storage" || roleId == "log";

				SetColor(ConsoleColor.Cyan);
				WriteAt(0, y, emptyLine);
				WriteAt(COL0, y, roleId);
				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL_NET, y, "Network (Mbps)");
				WriteAt(COL_CPU, y, "Processor Activity");
				WriteAt(COL_MEMORY, y, "Memory");
				if (hasDisk)
				{
					WriteAt(COL_HDD, y, "Disk Activity");
					WriteAt(COL_STORAGE, y, "Storage Activity");
					WriteAt(COL_DATAVERSION, y, "Data Version");
					WriteAt(COL_KVSTORE, y, "KV Store");
				}
				++y;

				WriteAt(0, y, emptyLine);

				//WriteAt(COL1, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsReceived.Hz * 125000));
				//WriteAt(COL2, y, "{0,8:N3}", MegaBytes(machine.Network.MegabitsSent.Hz * 125000));
				//WriteAt(COL3, y, "{0,5:N1}", machine.Cpu.LogicalCoreUtilization * 100);
				//WriteAt(COL4, y, "{0,5:N1}", GigaBytes(machine.Memory.CommittedBytes));
				//WriteAt(COL5, y, "{0,5:N1}", GigaBytes(machine.Memory.TotalBytes));
				//SetColor(ConsoleColor.DarkGray);
				//WriteAt(COL8, y, "{0,11}", map);

				//SetColor(machine.Cpu.LogicalCoreUtilization >= 0.9 ? ConsoleColor.Red : ConsoleColor.Green);
				//WriteAt(COL3 + 7, y, "{0,-10}", BarGraph(machine.Cpu.LogicalCoreUtilization, 1, CPU_BARSZ, '|', ':')); // 1 = all the (logical) cores

				//double memRatio = 1.0 * machine.Memory.CommittedBytes / machine.Memory.TotalBytes;
				//SetColor(memRatio >= 0.95 ? ConsoleColor.Red : memRatio >= 0.79 ? ConsoleColor.DarkYellow : ConsoleColor.Green);
				//WriteAt(COL5 + 6, y, "{0,-10}", BarGraph(machine.Memory.CommittedBytes, machine.Memory.TotalBytes, MEM_BARSZ, '|', ':'));

				//SetColor(ConsoleColor.DarkGray);
				//WriteAt(COL6, y, "S: {0}; Q: {1}, D: {2}", FriendlyBytes(storageBytes), FriendlyBytes(queueDiskBytes), FriendlyBytes((long) blahBytes));

				long maxLogTransaction = 0;
				SetColor(ConsoleColor.DarkCyan);
				WriteAt(COL0, y, "         Address:Port");
				WriteAt(COL_NET, y, "   Recv    Sent");
				WriteAt(COL_MEMORY, y, " VM Size");
				WriteAt(COL_CPU, y, "% CPU Core");
				if (roleId == "storage")
				{
					WriteAt(COL_HDD, y, "% Busy");
					WriteAt(COL_STORAGE, y, "Queue Sz");
					WriteAt(COL_STORAGE + 9, y, "Queried");
					WriteAt(COL_STORAGE + 17, y, "Mutation");
					WriteAt(COL_STORAGE + 25, y, "  Stored");
					WriteAt(COL_DATAVERSION, y, "Data/Dura. Lag");
					WriteAt(COL_KVSTORE, y, "    Used");
				}
				else if (roleId == "log")
				{
					WriteAt(COL_HDD, y, "% Busy");
					WriteAt(COL_STORAGE, y, "Queue Sz");
					WriteAt(COL_STORAGE + 9, y, "  Input");
					WriteAt(COL_STORAGE + 17, y, "Durable");
					WriteAt(COL_STORAGE + 25, y, "    Used");
					maxLogTransaction = kv.Select(x => ((LogRoleMetrics) x.Role).DataVersion).Max();
					WriteAt(COL_DATAVERSION, y, "        Delta");
					WriteAt(COL_KVSTORE, y, "    Used");
				}

				++y;

				//TODO: use a set to map procs ot machines? Where(..) will be slow if there are a lot of machines x processes
				string? prevHost = null;
				foreach (var (proc, role, machineId) in kv.OrderBy(x => x.Process.Address))
				{
					if (y < ScreenHeight)
					{
						SetColor(ConsoleColor.DarkGray);
						WriteAt(COL0, y, " _______________:_____ | ________ ________ | _____% __________ | ________ | ".Replace('_', ' '));
						if (roleId == "storage")
						{
							WriteAt(COL_HDD, y, "_____% __________ | ________ _______ _______ ________ | _____s _____s | ________".Replace('_', ' '));
						}
						else if (roleId == "log")
						{
							WriteAt(COL_HDD, y, "_____% __________ | ________ _______ _______ ________ | _____________ | ________".Replace('_', ' '));
						}

						string host = GetHostFromAddress(proc.Address);
						if (host != prevHost)
						{
							SetColor(ConsoleColor.Gray);
							WriteAt(COL0 + 1, y, $"{host,15}");
						}
						SetColor(proc.Excluded ? ConsoleColor.DarkRed : ConsoleColor.Gray);
						WriteAt(COL0 + 1 + 16, y, GetPortFromAddress(proc.Address));
						prevHost = host;

						SetColor(MapMegabitsToColor(proc.Network.MegabitsReceived.Hz));
						WriteAt(COL_NET, y, $"{Nice(proc.Network.MegabitsReceived.Hz, "-", 0.05, "~"),7:N1}");
						SetColor(MapMegabitsToColor(proc.Network.MegabitsSent.Hz));
						WriteAt(COL2, y, $"{Nice(proc.Network.MegabitsSent.Hz, "-", 0.05, "~"),7:N1}");

						SetColor(ConsoleColor.Gray);
						WriteAt(COL_CPU, y, $"{proc.Cpu.UsageCores * 100,5:N1}");
						SetColor(proc.Cpu.UsageCores >= 0.95 ? ConsoleColor.DarkRed : proc.Cpu.UsageCores >= 0.75 ? ConsoleColor.DarkYellow : ConsoleColor.DarkGreen);
						WriteAt(COL_CPU + 7, y, $"{BarGraph(proc.Cpu.UsageCores, 1, CPU_BARSZ, '|', ":", "."),-10}");

						long memoryUsed = proc.Memory.UsedBytes;

						SetColor(memoryUsed >= 0.75 * proc.Memory.LimitBytes ? ConsoleColor.White : memoryUsed >= GIBIBYTE ? ConsoleColor.Gray : ConsoleColor.DarkGray);
						WriteAt(COL_MEMORY, y, $"{FriendlyBytes(memoryUsed),8}");

						if (role is StorageRoleMetrics storage)
						{
							// Queue Size
							SetColor(MapQueueSizeToColor(storage.InputBytes.Counter - storage.DurableBytes.Counter));
							WriteAt(COL_STORAGE, y, $"{FriendlyBytes(storage.InputBytes.Counter - storage.DurableBytes.Counter),8}");

							// Bytes Queried
							SetColor(MapDiskOpsToColor(storage.BytesQueried.Hz));
							WriteAt(COL_STORAGE + 9, y, $"{Nice(MegaBytes(storage.BytesQueried.Hz), "-"),7:N2}");

							// Mutation Bytes
							SetColor(MapDiskOpsToColor(storage.MutationBytes.Hz));
							WriteAt(COL_STORAGE + 17, y, $"{Nice(MegaBytes(storage.MutationBytes.Hz), "-"),7:N2}");

							SetColor(ConsoleColor.Gray);
							WriteAt(COL_STORAGE + 25, y, $"{FriendlyBytes(storage.StoredBytes),8}");

							var dataLag = storage.DataLag.Seconds;
							SetColor(dataLag < 0.5 ? ConsoleColor.DarkGray : dataLag < 1 ? ConsoleColor.Gray : dataLag < 2 ? ConsoleColor.White : dataLag < 6 ? ConsoleColor.Cyan : dataLag < 11 ? ConsoleColor.DarkYellow : ConsoleColor.DarkRed);
							WriteAt(COL_DATAVERSION, y, $"{Nice(storage.DataLag.Seconds, "-"),5:N1}");
							var durLag = storage.DurabilityLag.Seconds;
							//should usually be around 
							SetColor(durLag < 6 ? ConsoleColor.DarkGray : durLag < 8 ? ConsoleColor.Gray : durLag < 11 ? ConsoleColor.White : durLag < 16 ? ConsoleColor.Cyan : durLag < 26 ? ConsoleColor.DarkYellow : ConsoleColor.DarkRed);
							WriteAt(COL_DATAVERSION + 7, y, $"{durLag,5:N1}");

							WriteAt(COL_KVSTORE, y, $"{FriendlyBytes(storage.KVStoreUsedBytes),8}");

						}
						else if (role is LogRoleMetrics log)
						{
							// Queue Size
							SetColor(MapQueueSizeToColor(log.InputBytes.Counter - log.DurableBytes.Counter));
							WriteAt(COL_STORAGE, y, $"{FriendlyBytes(log.InputBytes.Counter - log.DurableBytes.Counter),8}");

							// Durable Bytes
							SetColor(MapDiskOpsToColor(log.InputBytes.Hz));
							WriteAt(COL_STORAGE + 9, y, $"{Nice(MegaBytes(log.InputBytes.Hz), "-"),7:N1}");

							SetColor(MapDiskOpsToColor(log.InputBytes.Hz));
							WriteAt(COL_STORAGE + 17, y, $"{Nice(MegaBytes(log.DurableBytes.Hz), "-"),7:N1}");

							SetColor(ConsoleColor.Gray);
							WriteAt(COL_STORAGE + 25, y, $"{FriendlyBytes(log.QueueDiskUsedBytes),8:N1}");

							long delta = log.DataVersion - maxLogTransaction;
							SetColor(delta >= -500_000 ? ConsoleColor.DarkGray : delta >= -1_000_000 ? ConsoleColor.Gray : delta >= -2_000_000 ? ConsoleColor.White : delta >= -5_000_000 ? ConsoleColor.Cyan : delta >= -10_000_000 ? ConsoleColor.DarkYellow : ConsoleColor.DarkRed);
							WriteAt(COL_DATAVERSION, y, $"{Nice(delta, "-"),13:N0}");

							WriteAt(COL_KVSTORE, y, $"{FriendlyBytes(log.KVStoreUsedBytes),8}");
						}

						//if (map.Log)
						//{
						//	SetColor(MapDiskOpsToColor(logBytes));
						//	WriteAt(COL6 + 16, y, "{0,7:N1}", MegaBytes(logBytes));
						//}
						if (hasDisk)
						{
							SetColor(ConsoleColor.Gray);
							WriteAt(COL_HDD, y, $"{proc.Disk.Busy * 100,5:N1}");
							SetColor(proc.Disk.Busy == 0.0 ? ConsoleColor.DarkGray : proc.Disk.Busy >= 0.95 ? ConsoleColor.DarkRed : ConsoleColor.DarkGreen);
							WriteAt(COL_HDD + 7, y, $"{BarGraph(proc.Disk.Busy, 1, HDD_BARSZ, '|', ":", "."),-5}");
						}

					}
					++y;
				}

				WriteAt(COL0, y, emptyLine);
				++y;
			}

			// clear the extra lines from the previous repaint
			y = Math.Min(y, ScreenHeight);
			for (; y < LastProcessYMax; y++)
			{
				WriteAt(COL0, y, emptyLine);
			}
			LastProcessYMax = y;

		}

		private static void ShowHelpScreen(bool repaint)
		{
			if (repaint)
			{
				SetTitle($"fdbtop - {CurrentCoordinators?.Description} - Help");
				RepaintTopBar("help");
			}

			int y = 5;
			WriteAt(TOP_COL0, y + 0, "[M] Metrics");
			WriteAt(TOP_COL0, y + 1, "[T] Transactions");
			WriteAt(TOP_COL0, y + 2, "[L] Latencies");
			WriteAt(TOP_COL0, y + 3, "[P] Processes");
			//TODO!
		}

		public enum DisplayMode
		{
			Help = 0,
			Processes,
			Metrics,
			Latency,
			Transactions,
			Roles,
		}

	}

}
