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

namespace FoundationDB.Filters.Logging
{
	using System.Reflection;
	using System.Threading.Channels;
	using FoundationDB.Client;
	using SnowBank.Collections.CacheOblivious;

	public sealed class FdbLoggingOptions
	{

		/// <summary>Fields that will be included in the logged transactions</summary>
		public FdbLoggedFields Fields { get; set; }

		/// <summary>Session Identifier attached to all logged transactions</summary>
		public string? SessionId { get; set; }

		public string? Origin { get; set; }

	}

	[Flags]
	[PublicAPI]
	public enum FdbLoggedFields
	{
		/// <summary>Default logging options</summary>
		Default = 0,

		/// <summary>Capture the stacktrace of the caller method that created the transaction</summary>
		RecordCreationStackTrace = 0x100,

		/// <summary>Capture the stacktrace of the caller method for each operation</summary>
		RecordOperationStackTrace = 0x200,

		/// <summary>Capture all the stack traces.</summary>
		/// <remarks>This is a shortcut for <see cref="RecordCreationStackTrace"/> | <see cref="RecordOperationStackTrace"/></remarks>
		WithStackTraces = RecordCreationStackTrace | RecordOperationStackTrace,
	}

	/// <summary>Container that logs all operations performed by a transaction</summary>
	public sealed partial class FdbTransactionLog
	{

		/// <summary>Sequence ID of the logged transaction</summary>
		/// <remarks>
		/// <para>This is a sequential counter of the transactions inside the current process</para>
		/// <para>Use <see cref="Uuid"/> if you require a globally unique identifier</para>
		/// </remarks>
		public required int Id { get; init; }

		/// <summary>Unique ID for this transaction</summary>
		public required Guid Uuid { get; init; }

		/// <summary>Session ID of the logged transaction</summary>
		/// <remarks>If non-null, this is used to merge transaction logs produced by different "nodes" in the same "run", after the fact, in order to recreate a global timeline.</remarks>
		public string? SessionId { get; init; }

		/// <summary>Unique ID of the source of this transaction</summary>
		/// <remarks>This will be <c>null</c> for locally generated transaction.</remarks>
		public string? Origin { get; init; }

		/// <summary>Logging options for this log</summary>
		public required FdbLoggedFields Fields { get; init; }

		/// <summary>True if the transaction is Read Only</summary>
		public bool IsReadOnly { get; init; }

		/// <summary>StackTrace of the method that created this transaction</summary>
		/// <remarks>Only if the <see cref="FdbLoggedFields.RecordCreationStackTrace"/> option is set</remarks>
		public StackTrace? CallSite { get; init; }

		#region Interning...

		/// <summary>Buffer used to store interned keys and values captured by this log</summary>
		private SliceBuffer m_buffer = new(4096);

		/// <summary>Capture slices larger than this size will be allocated on the heap; otherwise, they will be interned in the local buffer</summary>
		private const int MAX_INTERNING_SIZE = 1024;

		[Pure]
		internal Slice Grab(Slice bytes)
		{
			switch (bytes.Count)
			{
				case 0 or > MAX_INTERNING_SIZE:
				{
					return bytes.Copy();
				}
				default:
				{
					lock (this.Lock)
					{
						return m_buffer.Intern(bytes);
					}
				}
			}
		}

		[Pure]
		internal Slice Grab(ReadOnlySpan<byte> bytes)
		{
			switch (bytes.Length)
			{
				case 0:
				{
					return Slice.Empty;
				}
				case > MAX_INTERNING_SIZE:
				{
					return Slice.FromBytes(bytes);
				}
				default:
				{
					lock (this.Lock)
					{
						return m_buffer.Intern(bytes);
					}
				}
			}
		}

		[Pure]
		internal Slice[] Grab(ReadOnlySpan<Slice> slices)
		{
			if (slices.Length == 0)
			{
				return [ ];
			}

			var res = new Slice[slices.Length];

			// first pass to deal with empty/large values
			bool useBuffer = false;
			for (int i = 0; i < slices.Length; i++)
			{
				if (slices[i].Count is 0 or > MAX_INTERNING_SIZE)
				{
					res[i] = slices[i].Copy();
				}
				else
				{
					useBuffer = true;
				}
			}

			if (useBuffer)
			{ // we have values that must be interned
				lock (this.Lock)
				{
					for (int i = 0; i < slices.Length; i++)
					{
						if (slices[i].Count is > 0 and <= MAX_INTERNING_SIZE)
						{
							res[i] = m_buffer.Intern(slices[i]);
						}
					}
				}
			}

			return res;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal KeySelector Grab(KeySelector selector) => new(Grab(selector.Key), selector.OrEqual, selector.Offset);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal KeySelector Grab(KeySpanSelector selector) => new(Grab(selector.Key), selector.OrEqual, selector.Offset);

		internal KeySelector[] Grab(ReadOnlySpan<KeySelector> selectors)
		{
			if (selectors.Length == 0) return [ ];

			var res = new KeySelector[selectors.Length];

			bool useBuffer = false;
			for (int i = 0; i < selectors.Length; i++)
			{
				ref readonly KeySelector selector = ref selectors[i];
				if (selector.Key.Count is 0 or > MAX_INTERNING_SIZE)
				{
					res[i] = new(selector.Key.Copy(), selector.OrEqual, selector.Offset);
				}
				else
				{
					useBuffer = true;
				}
			}

			if (useBuffer)
			{
				lock (this.Lock)
				{
					for (int i = 0; i < selectors.Length; i++)
					{
						ref readonly KeySelector selector = ref selectors[i];
						if (selector.Key.Count is > 0 and <= MAX_INTERNING_SIZE)
						{
							res[i] = new(Grab(selector.Key), selector.OrEqual, selector.Offset);
						}
					}
				}
			}

			return res;
		}

		#endregion

		internal void Execute<TState, TCommand>(TState state, TCommand cmd, Action<TState, TCommand> action)
			where TCommand : FdbTransactionLog.Command
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				action(state, cmd);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				EndOperation(cmd, error);
			}
		}

		internal TResult Execute<TState, TCommand, TResult>(TState state, TCommand cmd, Func<TState, TCommand, TResult> handler)
			where TCommand : FdbTransactionLog.Command
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				return handler(state, cmd);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				EndOperation(cmd, error);
			}
		}

		internal async Task ExecuteAsync<TState, TCommand>(TState state, TCommand cmd, Func<TState, TCommand, Task> handle, Action<TState, TCommand, FdbTransactionLog>? onSuccess = null)
			where TCommand : FdbTransactionLog.Command
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				await handle(state, cmd).ConfigureAwait(false);
			}
			catch (Exception e)
			{
				error = e;
				throw;
			}
			finally
			{
				EndOperation(cmd, error);
				if (error is null)
				{
					onSuccess?.Invoke(state, cmd, this);
				}
			}
		}

		internal async Task<TResult> ExecuteAsync<TState, TCommand, TResult>(TState state, TCommand cmd, Func<TState, TCommand, Task<TResult>> lambda)
			where TCommand : FdbTransactionLog.Command<TResult>
		{
			Exception? error = null;
			BeginOperation(cmd);
			try
			{
				TResult result = await lambda(state, cmd).ConfigureAwait(false);
				cmd.Result = Maybe.Return<TResult>(result);
				return result;
			}
			catch (Exception e)
			{
				error = e;
				cmd.Result = Maybe.Error<TResult>(System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e));
				throw;
			}
			finally
			{
				EndOperation(cmd, error);
			}
		}

		internal static StackTrace CaptureStackTrace(int numStackFramesToSkip)
		{
#if DEBUG
			const bool NEED_FILE_INFO = true;
#else
			const bool NEED_FILE_INFO = false;
#endif
			return new(1 + numStackFramesToSkip, NEED_FILE_INFO);
		}

		/// <summary>Lock used to update the internal state</summary>
#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock Lock = new();
#else
		private readonly object Lock = new();
#endif

		/// <summary>Number of operations performed by the transaction</summary>
		public int Operations { get; private set; }

		/// <summary>List of all commands processed by the transaction</summary>
		public required List<Command> Commands { get; init; }

		/// <summary>List of the start of each segment in the log (attempt or retry).</summary>
		/// <remarks>
		/// <para>Each entry in the list corresponds to the index of the first command for the corresponding segment</para>
		/// <para>If the list is null (or empty), then the log contains only a single segment</para>
		/// <para>For example, in a log with 20 commands and segments <c>[0, 7, 15]</c>, then commands 0 to 6 belong to the first attempt, commands 7 to 14 belong to the second attempt, and commands 15 to 19 belong to the third and last attempt.</para>
		/// </remarks>
		public List<int>? Segments { get; private set; }

		/// <summary>Timestamp of the start of transaction</summary>
		public long StartTimestamp { get; private set; }

		/// <summary>Timestamp of the end of transaction</summary>
		public long? StopTimestamp { get; private set; }

		/// <summary>Timestamp (UTC) of the start of transaction</summary>
		public DateTimeOffset StartedUtc { get; internal set; }

		/// <summary>Timestamp (UTC) of the end of the transaction</summary>
		public DateTimeOffset? StoppedUtc { get; internal set; }

		/// <summary>Timestamp (UTC) of the last successful commit of the transaction</summary>
		public DateTimeOffset? CommittedUtc { get; internal set; }

		/// <summary>Committed version of the transaction (if a commit was successful)</summary>
		public long? CommittedVersion { get; internal set; }

		/// <summary>Internal step counter of the transaction</summary>
		/// <remarks>This counter is used to detect sequential vs parallel commands</remarks>
		public int Step { get; private set; }

		/// <summary>Read size of the last commit attempt</summary>
		/// <remarks>This value only account for read commands in the last attempt</remarks>
		public long ReadSize { get; private set; }

		/// <summary>Write size of the last commit attempt</summary>
		/// <remarks>This value only account for write commands in the last attempt</remarks>
		public long WriteSize { get; private set; }

		/// <summary>Commit size of the last commit attempt</summary>
		/// <remarks>
		/// <para>This value only account for write commands in the last attempt</para>
		/// <para>It will be <c>null</c> for transactions that are read-only, or did not attempt to commit</para>
		/// </remarks>
		public long? CommitSize { get; internal set; }

		/// <summary>Total of the commit size of all attempts performed by this transaction</summary>
		/// <remarks>
		/// <para>This value include the size of all previous retry attempts</para>
		/// <para>It will be <c>null</c> for transactions that are read-only, or did not attempt to commit</para>
		/// </remarks>
		public long? TotalCommitSize { get; internal set; }

		/// <summary>If true, the transaction has completed (either Commit() completed successfully or Dispose was called)</summary>
		public bool Completed { get; private set; }

		/// <summary>Total number of attempts to commit this transaction</summary>
		/// <remarks>This value is increment on each call to Commit()</remarks>
		public int Attempts { get; internal set; }

		/// <summary>Sets to <c>true</c> if at least one command failed to execute</summary>
		public bool HasError { get; internal set; }

		/// <summary>Sets to <c>true</c> if at least one attempt of the transaction fails to commit with <see cref="FdbError.NotCommitted"/></summary>
		public bool HasConflict { get; internal set; }

		/// <summary>Receives the actual value of the VersionStamps generated by this transaction</summary>
		/// <remarks>
		/// <para>This value will be non-null only if the last attempt used <see cref="FdbMutationType.VersionStampedKey"/> or <see cref="FdbMutationType.VersionStampedValue"/> operations and committed successfully.</para>
		/// </remarks>
		public VersionStamp? VersionStamp { get; internal set; }

		/// <summary>If set to <c>true</c>, the <see cref="VersionStamp"/> of this transaction will be captured when it commits successfully</summary>
		internal bool RequiresVersionStamp { get; set; }

		/// <summary>Total duration of the transaction (including all attempts)</summary>
		/// <remarks>If the transaction is still ongoing, returns the time elapsed since the start.</remarks>
		public TimeSpan TotalDuration => GetElapsedTime(this.StartTimestamp, this.StopTimestamp ?? Stopwatch.GetTimestamp());

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal TimeSpan GetTimeOffset() => GetElapsedTime(this.StartTimestamp, Stopwatch.GetTimestamp());

		/// <summary>Return the duration between two timestamps</summary>
		/// <remarks>The duration is rounded "away from zero", which is a different behavior than <see cref="Stopwatch.GetElapsedTime(long,long)"/> which truncates the ticks ("towards zero")</remarks>
		[Pure]
		internal static TimeSpan GetElapsedTime(long startingTimestamp, long endingTimestamp)
			=> new((long) Math.Round((endingTimestamp - startingTimestamp) * s_tickFrequency, MidpointRounding.AwayFromZero));

		private static readonly double s_tickFrequency = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency; // Cached ratio between timestamp values and BCL ticks

		/// <summary>Marks the start of the transaction</summary>
		internal void Start(IFdbTransaction trans, DateTimeOffset start)
		{
			Contract.Debug.Requires(trans != null);

			this.StartedUtc = start; //TODO: use a configurable clock?
			this.StartTimestamp = Stopwatch.GetTimestamp();
		}

		/// <summary>Marks the end of the transaction</summary>
		internal bool Stop(IFdbTransaction trans)
		{
			Contract.Debug.Requires(trans != null);

			//TODO: verify that the trans is the same one that was passed to Start(..)?
			if (this.Completed)
			{
				return false;
			}

			this.StopTimestamp = Stopwatch.GetTimestamp();
			this.StoppedUtc = DateTimeOffset.UtcNow; //TODO: use a configurable clock?
			this.Completed = true;
			return true;
		}

		/// <summary>Adds a new already completed command to the log</summary>
		public void AddOperation(Command cmd, bool countAsOperation = true)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();

			cmd.StartOffset = ts;
			cmd.EndOffset = cmd.StartOffset;
			cmd.ThreadId = Environment.CurrentManagedThreadId;
			if ((this.Fields & FdbLoggedFields.RecordOperationStackTrace) != 0)
			{
				cmd.CallSite = CaptureStackTrace(2);
			}

			lock (this.Lock)
			{
				cmd.Step = this.Step;
				if (countAsOperation) ++this.Operations;
				this.Commands.Add(cmd);
			}
		}

		/// <summary>Starts tracking the execution of a new command</summary>
		public void BeginOperation(Command cmd)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();

			cmd.StartOffset = ts;
			cmd.ThreadId = Environment.CurrentManagedThreadId;
			if ((this.Fields & FdbLoggedFields.RecordOperationStackTrace) != 0)
			{
				cmd.CallSite = CaptureStackTrace(4);
			}

			lock (this.Lock)
			{
				cmd.Step = this.Step;
				if (cmd.ArgumentBytes.HasValue) this.WriteSize += cmd.ArgumentBytes.Value;
				if (cmd.Op == Operation.Reset)
				{ // start of a new segment
					if (this.Segments is null)
					{
						this.Segments = [ 0, this.Commands.Count ];
					}
					else
					{
						this.Segments.Add(this.Commands.Count);
					}
				}
				this.Commands.Add(cmd);
				++this.Operations;
			}
		}

		/// <summary>Marks the end of the execution of a command</summary>
		public void EndOperation(Command cmd, Exception? error = null)
		{
			Contract.Debug.Requires(cmd != null);

			var ts = GetTimeOffset();

			cmd.EndOffset = ts;
			cmd.Error = error;

			lock (this.Lock)
			{
				cmd.EndStep = ++this.Step;
				if (cmd.ResultBytes.HasValue)
				{
					this.ReadSize += cmd.ResultBytes.Value;
				}

				if (error is FdbException fdbEx)
				{
					this.HasError = true;
					switch (fdbEx.Code)
					{
						case FdbError.NotCommitted:
						{
							this.HasConflict = true;
							//TODO: detect cache validation errors vs "regular" conflicts?
							break;
						}
					}
				}
			}
		}

		/// <summary>Adds a debug annotation to the log</summary>
		public void Annotate(string text)
		{
			AddOperation(new LogCommand(text), countAsOperation: false);
		}

		#region JSON Serialization...

		public JsonObject ToJson(bool readOnly = true)
		{
			var obj = new JsonObject();
			obj["id"] = this.Id;
			obj["uuid"] = this.Uuid;
			obj["startedAt"] = this.StartedUtc;
			obj.AddIfNotNull("stoppedAt", this.StoppedUtc);
			obj["totalDuration"] = this.TotalDuration;
			obj.AddIfNonZero("attempts", this.Attempts);
			obj.AddIfNonZero("operations", this.Operations);
			obj.AddIfTrue("readOnly", this.IsReadOnly);
			obj.AddIfNonZero("readSize", this.ReadSize);
			obj.AddIfNonZero("writeSize", this.WriteSize);
			obj.AddIfNotNull("commitVersion", this.CommittedVersion);
			obj.AddIfNotNull("committedAt", this.CommittedUtc);
			obj.AddIfNonZero("commitSize", this.CommitSize);
			obj.AddIfNonZero("totalCommitSize", this.TotalCommitSize);
			obj.AddIfTrue("hasError", this.HasError);
			obj.AddIfTrue("hasConflict", this.HasConflict);
			lock (this.Commands)
			{
				var commands = new JsonArray(this.Commands.Count);
				foreach (var command in this.Commands)
				{
					commands.Add(command.ToJson(readOnly));
				}
				if (readOnly)
				{
					CrystalJsonMarshall.FreezeTopLevel(commands);
				}
				obj["commands"] = commands;
			}
			obj.AddIfNotNull("sid", this.SessionId);
			obj.AddIfNotNull("origin", this.Origin);
			if (readOnly)
			{
				CrystalJsonMarshall.FreezeTopLevel(obj);
			}
			return obj;
		}

		public static FdbTransactionLog FromJson(JsonObject obj)
		{
			Contract.NotNull(obj);
			var id = obj.Get<int>("id");
			var uuid = obj.Get<Guid>("uuid");
			var commands = Command.FromJson(obj.GetArray("commands"));
			var log = new FdbTransactionLog()
			{
				Id = id,
				Uuid = uuid,
				SessionId = obj.Get<string?>("sid", null),
				Origin = obj.Get<string?>("origin", null),
				Fields = FdbLoggedFields.Default, //BUGBUG: should we serialize the fields as well?
				StartedUtc = obj.Get<DateTimeOffset>("startedAt"),
				StoppedUtc = obj.Get<DateTimeOffset?>("stoppedAt", null),
				Attempts = obj.Get<int>("attempts", 0),
				Operations = obj.Get<int>("operations", 0),
				IsReadOnly = obj.Get<bool>("readOnly", false),
				ReadSize = obj.Get<long>("readSize", 0),
				WriteSize = obj.Get<long>("writeSize", 0),
				CommittedVersion = obj.Get<long?>("commitVersion", null),
				CommittedUtc = obj.Get<DateTimeOffset?>("committedAt", null),
				CommitSize = obj.Get<long?>("commitSize", null),
				TotalCommitSize = obj.Get<long?>("totalCommitSize", null),
				HasError = obj.Get<bool>("hasError", false),
				HasConflict = obj.Get<bool>("hasConflict", false),
				Commands = commands,
			};

			return log;
		}

		#endregion

		#region Reports Generation...

		/// <summary>Generate an ASCII report with all the commands that were executed by the transaction</summary>
		public string GetCommandsReport(bool detailed = false, KeyResolver? keyResolver = null)
		{
			keyResolver ??= KeyResolver.Default;

			var sb = new StringBuilder(1024);

			var commands = this.Commands;

			sb.Append(CultureInfo.InvariantCulture, $"Transaction #{this.Id} ({(this.IsReadOnly ? "read-only" : "read/write")}, {commands.Count:N0} operations, started {this.StartedUtc.TimeOfDay}Z");
			if (this.StoppedUtc.HasValue)
			{
				sb.Append(CultureInfo.InvariantCulture, $", ended {this.StoppedUtc.Value.TimeOfDay}Z)");
			}
			else
			{
				sb.Append(", did not finish)");
			}

			sb.AppendLine();

			int reads = 0, writes = 0;
			foreach (var cmd in commands)
			{
				if (detailed)
				{
					sb.Append(CultureInfo.InvariantCulture, $"{cmd.Step,3} - T+{cmd.StartOffset.TotalMilliseconds,7:##0.000} ({cmd.Duration.Ticks / 10.0,7:##,##0} µs) : {cmd.ToString(keyResolver)}");
				}
				else
				{
					sb.Append(CultureInfo.InvariantCulture, $"{cmd.Step,3} : {(cmd.Error != null ? "[FAILED] " : "")}{cmd.ToString(keyResolver)}");
				}
				sb.AppendLine();
				switch (cmd.Mode)
				{
					case Mode.Read: ++reads; break;
					case Mode.Write: ++writes; break;
				}
			}
			if (this.Completed)
			{
				sb.AppendLine(CultureInfo.InvariantCulture, $"Stats: {this.Operations:N0} operations, {reads:N0} reads ({this.ReadSize:N0} bytes), {writes:N0} writes ({this.CommitSize:N0} bytes), {this.TotalDuration.TotalMilliseconds:N2} ms");
			}
			sb.AppendLine();
			return sb.ToString();
		}

		/// <summary>Generate a full ASCII report with the detailed timeline of all the commands that were executed by the transaction</summary>
		public string GetTimingsReport(bool showCommands = false, KeyResolver? keyResolver = null)
		{
			keyResolver ??= KeyResolver.Default;

			var sb = new StringBuilder(1024);

			TimeSpan duration = this.TotalDuration;
			// ideal range is between 10 and 80 chars
			double scale = 0.00001d;
			int width = (int) (duration.TotalSeconds / scale);
			{
				// should scale to: 1, 2.5, 5, 10, 25, 50, 100, ...
				int order = 0;
				int maxWidth = showCommands ? 50 : 100;
				while (width > maxWidth)
				{
					scale *= order == 0 ? 2.5d : 2;
					order = (order + 1) % 3;
					width = (int) (duration.TotalSeconds / scale);
				}
			}

			var commands = this.Commands.ToArray();

			// Header
			sb.Append(CultureInfo.InvariantCulture, $"Transaction #{this.Id} ({(this.IsReadOnly ? "read-only" : "read/write")}, {commands.Length} operations, '#' = {(scale < 1E-3 ? $"{scale * 1E6} µs" : $"{scale * 1E3} ms")}, started {this.StartedUtc.TimeOfDay}Z [{this.StartedUtc.ToUnixTimeMilliseconds() / 1000.0:F3}]");
			if (this.StoppedUtc.HasValue)
			{
				sb.Append(CultureInfo.InvariantCulture, $", ended {this.StoppedUtc.Value.TimeOfDay}Z [{this.StoppedUtc.Value.ToUnixTimeMilliseconds() / 1000.0:F3}])");
			}
			else
			{
				sb.Append(", did not finish");
			}
			sb.AppendLine();

			if (commands.Length > 0)
			{
				var bar = new string('─', width + 2);
				sb.AppendLine(CultureInfo.InvariantCulture, $"┌  oper. ┬{bar}┬──── start ──── end ── duration ──┬─ sent  recv ┐");

				// look for the timestamps of the first and last commands
				var first = TimeSpan.Zero;
				foreach (var cmd in commands)
				{
					if (cmd.Op == Operation.Log) continue;
					first = cmd.StartOffset;
					break;
				}
				for(int i = commands.Length - 1; i >= 0; i--)
				{
					if (commands[i].Op == Operation.Log) continue;
					var endOffset = commands[i].EndOffset;
					if (endOffset.HasValue) duration = endOffset.Value;
					break;
				}
				duration -= first;

				int step = -1;
				bool previousWasOnError = false;
				int attempts = 1;
				int charsToSkip = 0;
				foreach (var cmd in commands)
				{
					if (previousWasOnError)
					{
						sb.AppendLine(CultureInfo.InvariantCulture, $"├────────┼{bar}┼──────────────────────────────────┼─────────────┤ == Attempt #{(++attempts):N0} == {(this.StartedUtc + cmd.StartOffset).TimeOfDay}Z ({(this.StartedUtc + cmd.StartOffset).ToUnixTimeMilliseconds() / 1000.0:F3})");
					}

					long ticks = cmd.Duration.Ticks;
					string w = GetFancyGraph(width, (cmd.StartOffset - first).Ticks, ticks, duration.Ticks, charsToSkip);

					if (ticks > 0)
					{
						sb.Append(CultureInfo.InvariantCulture, $"│{(cmd.Step == step ? ":" : " ")}{cmd.Step,-3:##0}{(cmd.Error != null ? "!" : " ")}{cmd.ShortName,2}{(ticks >= TimeSpan.TicksPerMillisecond * 10 ? '*' : ticks >= TimeSpan.TicksPerMillisecond ? '°' : ' ')}│ {w} │ T+{cmd.StartOffset.TotalMilliseconds,7:##0.000} ~ {(cmd.EndOffset ?? TimeSpan.Zero).TotalMilliseconds,7:##0.000} ({ticks / 10.0,7:##,##0} µs) │ {cmd.ArgumentBytes,5} {cmd.ResultBytes,5} │ {(showCommands ? cmd.ToString(keyResolver) : string.Empty)}");
					}
					else
					{ // annotation
						sb.Append(CultureInfo.InvariantCulture, $"│{(cmd.Step == step ? ":" : " ")}{cmd.Step,-3:##0}{(cmd.Error != null ? "!" : " ")}{cmd.ShortName,2} │ {w} │ T+{cmd.StartOffset.TotalMilliseconds,7:##0.000}                        │     -     - │ {(showCommands ? cmd.ToString(keyResolver) : string.Empty)}");
					}

					if (showCommands && cmd.CallSite != null)
					{
						var f = GetFirstInterestingStackFrame(cmd.CallSite);
						if (f != null)
						{
							var m = f.GetMethod()!;
							string name = GetUserFriendlyMethodName(m);
							sb.Append(" // ").Append(name);
							var fn = f.GetFileName();
							if (fn != null) sb.Append(CultureInfo.InvariantCulture, $" at {fn}:{f.GetFileLineNumber()}");
						}
					}

					sb.AppendLine();

					previousWasOnError = cmd.Op == Operation.OnError;
					if (previousWasOnError)
					{
						charsToSkip = (int)Math.Floor(1.0d * width * (cmd.EndOffset ?? TimeSpan.Zero).Ticks / duration.Ticks);
					}

					step = cmd.Step;
				}

				sb.AppendLine(CultureInfo.InvariantCulture, $"└────────┴{bar}┴──────────────────────────────────┴─────────────┘");

				// Footer
				if (this.Completed)
				{
					sb.Append("> ");
					var flag = false;
					if (this.ReadSize > 0)
					{
						sb.Append(CultureInfo.InvariantCulture, $"Read {this.ReadSize:N0} bytes");
						flag = true;
					}
					if (this.CommitSize > 0)
					{
						if (flag) sb.Append(" and ");
						sb.Append(CultureInfo.InvariantCulture, $"Committed {this.CommitSize:N0} bytes");
						if (this.VersionStamp != null) sb.Append(", used VersionStamp ").Append(this.VersionStamp.Value.ToString());
						flag = true;
					}
					if (!flag) sb.Append("Completed");
					sb.AppendLine(CultureInfo.InvariantCulture, $" in {this.TotalDuration.TotalMilliseconds:N3} ms and {attempts:N0} attempt(s)");
				}
			}
			else
			{ // empty transaction
				sb.AppendLine(CultureInfo.InvariantCulture, $"> Completed after {this.TotalDuration.TotalMilliseconds:N3} ms without performing any operation");
			}
			return sb.ToString();
		}

		private static StackFrame? GetFirstInterestingStackFrame(StackTrace? st)
		{
			if (st == null) return null;
			var self = typeof (Fdb).Module;
			for (int k = 0; k < st.FrameCount; k++)
			{
				var f = st.GetFrame(k);
				var m = f?.GetMethod();
				if (m == null) continue;

				var t = m.DeclaringType;
				if (t == null) continue;

				// discard any method in this assembly
				if (t.Module == self) continue;
				// discard any NETFX method (async state machines, threadpool, ...)
				if (t.Namespace!.StartsWith("System.", StringComparison.Ordinal)) continue;
				// discard any compiler generated state machine
				return f;
			}
			return null;
		}

		private static string GetUserFriendlyMethodName(MethodBase m)
		{
			Contract.Debug.Requires(m != null);
			var t = m.DeclaringType;
			Contract.Debug.Assert(t != null);

			if (m.Name == "MoveNext")
			{ // compiler generated state machine?

				// look for "OriginalType.<MethodName>d__123.MoveNext()", and replace it with "OriginalType.MethodName()"
				int p;
				if (t.Name.StartsWith("<", StringComparison.Ordinal)
				    && (p = t.Name.IndexOf('>')) > 0
					&& t.DeclaringType != null)
				{
					return t.DeclaringType.Name + "." + t.Name.Substring(1, p - 1) + "()";
				}
			}

			return t.Name + "." + m.Name + "()";
		}

		private static char GetFancyChar(int pos, int count, double start, double end, bool skip)
		{
			const string PALETTE_START = "`^:x{(&%"; // start of a segment that continues after us
			const string PALETTE_STOP  = ",.:x})&%"; // end of a segment started before us
			const string PALETTE_DOT   = "·◦•~*&$%"; // segment that starts and ends in the same character


			double cb = 1.0 * pos / count;
			double ce = 1.0 * (pos + 1) / count;

			if (cb >= end)
			{ // this is completely after the end of the segment
				return ' ';
			}

			if (ce < start)
			{ // this is completely before the start of the segment
				return skip ? '!' : ' ';
			}

			if (cb >= start && ce <= end)
			{ // this character is completely covered
				return '#';
			}

			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (start == end) return '|';

			var palette =
				start > cb && end < ce ? PALETTE_DOT
				: end > ce ? PALETTE_START
				: PALETTE_STOP;

			double x = count * (Math.Min(ce, end) - Math.Max(cb, start));
			x = Math.Min(Math.Max(x, 0), 1);

			int p = (int) Math.Round(x * (palette.Length - 1), MidpointRounding.AwayFromZero);

			return palette[p];
		}

		private static string GetFancyGraph(int width, long offset, long duration, long total, int skip)
		{
			double begin = 1.0d * offset / total;
			double end = 1.0d * (offset + duration) / total;

			Span<char> tmp = stackalloc char[width];
			for (int i = 0; i < tmp.Length; i++)
			{
				tmp[i] = GetFancyChar(i, tmp.Length, begin, end, i < skip);
			}
#if NETCOREAPP2_1_OR_GREATER
			return new string(tmp);
#else
			// the string(ReadOnlySpan<char>) constructor is not available, but Span<char>.ToString() produces the same result
			return tmp.ToString();
#endif
		}

		#endregion

		/// <summary>List of all operation types supported by a transaction</summary>
		public enum Operation
		{
			Invalid = 0,

			Set,
			Clear,
			ClearRange,
			Atomic,
			AddConflictRange,
			Get,
			GetKey,
			GetValues,
			GetKeys,
			GetRange,
			CheckValue,
			Watch,

			GetReadVersion,
			Commit,
			Cancel,
			Reset,
			OnError,
			SetOption,
			GetVersionStamp,
			GetAddressesForKey,
			GetRangeSplitPoints,
			GetEstimatedRangeSizeBytes,
			GetApproximateSize,

			Log,
		}

		/// <summary>Categories of operations supported by a transaction</summary>
		public enum Mode
		{
			/// <summary>Invalid mode</summary>
			Invalid = 0,
			/// <summary>Operation that reads keys and/or values from the database</summary>
			Read,
			/// <summary>Operation that writes or clears keys from the database</summary>
			Write,
			/// <summary>Operation that changes the state or behavior of the transaction</summary>
			Meta,
			/// <summary>Operation that watch changes performed in the database, outside the transaction</summary>
			Watch,
			/// <summary>Comments, annotations, debug output attached to the transaction</summary>
			Annotation
		}

	}

	public enum FdbTransactionFileFormat
	{
		Invalid = 0,

		/// <summary>Output each log as a JSON object on a single line, using '\r\n' as separator.</summary>
		/// <remarks><see href="https://jsonlines.org/"/></remarks>
		JsonLines,

		/// <summary>Output each transaction as a textual timing report.</summary>
		Text,
	}

	public sealed class FdbTransactionFileLogger : IDisposable
	{

		public const int DefaultBatchSize = 100;

		public static readonly TimeSpan DefaultThrottlingDelay = TimeSpan.FromMilliseconds(250);

		public string FilePath { get; init; }

		public FdbTransactionFileFormat Format { get; init; }

		public int BatchSize { get; init; }

		public TimeSpan ThrottlingDelay { get; init; }

		private Channel<FdbTransactionLog> Logs { get; } = Channel.CreateUnbounded<FdbTransactionLog>(new() { SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false });

		private CancellationTokenSource? Lifecycle { get; set; }

		public Exception? LastError { get; private set; }

		public FdbTransactionFileLogger(string filePath, FdbTransactionFileFormat format, int batchSize = DefaultBatchSize, TimeSpan throttlingDelay = default)
		{
			Contract.NotNullOrEmpty(filePath);
			Contract.GreaterOrEqual(batchSize, 1);
			Contract.GreaterOrEqual(throttlingDelay, TimeSpan.Zero);
			if (format is not (FdbTransactionFileFormat.JsonLines or FdbTransactionFileFormat.Text)) throw new ArgumentException("Transaction file format not supported", nameof(format));

			if (throttlingDelay == TimeSpan.Zero) throttlingDelay = DefaultThrottlingDelay;

			this.FilePath = filePath;
			this.Format = format;
			this.BatchSize = batchSize;
			this.ThrottlingDelay = throttlingDelay;
		}

		public void Publish(FdbTransactionLog log)
		{
			this.Logs.Writer.TryWrite(log);
		}

		public async Task Run(CancellationToken stoppingToken)
		{
			var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
			this.Lifecycle = cts;
			var ct = cts.Token;

			var reader = this.Logs.Reader;

			var batchSize = this.BatchSize;
			var batch = new List<string>(batchSize);

			var path = Path.GetFullPath(this.FilePath);

			if (!File.Exists(path))
			{
				// TODO: handle case when we cannot create the path of file (access rights?, IO error?)
				if (!Directory.Exists(Path.GetDirectoryName(path)))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				}
			}

			bool complete = false;

			while (!ct.IsCancellationRequested)
			{
				if (!complete)
				{
					await Task.Delay(this.ThrottlingDelay, ct).ConfigureAwait(false);
				}

				await reader.WaitToReadAsync(ct).ConfigureAwait(false);

				complete = false;
				while (reader.TryRead(out var log))
				{
					switch (this.Format)
					{
						case FdbTransactionFileFormat.JsonLines:
						{
							batch.Add(log.ToJson().ToJsonText());
							break;
						}
						case FdbTransactionFileFormat.Text:
						{
							batch.Add(log.GetTimingsReport(true));
							break;
						}
					}
					if (batch.Count >= batchSize)
					{
						complete = true;
						break;
					}
				}

				try
				{
#if NETCOREAPP2_0_OR_GREATER
					await File.AppendAllLinesAsync(path, batch, ct).ConfigureAwait(false);
#else
					// File.AppendAllLinesAsync is not available: offload the synchronous write to the thread pool instead
					await Task.Run(() => File.AppendAllLines(path, batch), ct).ConfigureAwait(false);
#endif
					this.LastError = null;
				}
				catch (Exception e)
				{
#if DEBUG
					System.Diagnostics.Debug.WriteLine($"ERROR: failed to append to transaction log {path}: {e}");
#endif
					this.LastError = e;
					//TODO: log error
					//TODO: buffer for a bit?
					complete = false;
				}

				batch.Clear();
			}
		}

		public void Dispose()
		{
			this.Lifecycle?.Cancel();
		}

		public static FdbTransactionHistory LoadFrom(IEnumerable<string> paths)
		{
			var history = new FdbTransactionHistory();
			foreach (var path in paths)
			{
				var lines = File.ReadAllLines(path);
				foreach (var line in lines)
				{
					if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
					{
						continue;
					}

					var obj = JsonObject.Parse(line);
					var log = FdbTransactionLog.FromJson(obj);
					history.Add(log);
				}
			}

			return history;
		}

	}

	public sealed class FdbTransactionHistory
	{

		[DebuggerDisplay("Count={Concurrent.Count}")]
		public sealed class TimeSlice
		{

			public List<FdbTransactionLog> Concurrent { get; init; }

			public TimeSlice(FdbTransactionLog log)
			{
				this.Concurrent = [ log ];
			}

			/// <inheritdoc />
			public override string ToString() => $"[{this.Concurrent.Count}] {{ {string.Join(", ", this.Concurrent.Select(l => l.Id))} }}";

			public bool HasConflict()
			{
				foreach (var log in this.Concurrent)
				{
					if (log.HasConflict) return true;
				}
				return false;
			}

		}

		public List<FdbTransactionLog> Transactions { get; } = new();

		public ColaRangeDictionary<long, TimeSlice> Timeline = new();

		public void Add(FdbTransactionLog log)
		{
			this.Transactions.Add(log);
			this.Timeline.Merge(
				log.StartedUtc.UtcTicks,
				log.StoppedUtc.GetValueOrDefault().UtcTicks,
				log,
				static (prev, log) =>
				{
					if (prev is null) return new(log);
					prev.Concurrent.Add(log);
					return prev;
				}
			);
		}

		/// <summary>Returns a sequence of all transactions in the timeline that failed to commit due to a conflict</summary>
		public IEnumerable<FdbTransactionLog> GetConflictedTransactions()
		{
			return this.Transactions
				.Where(log => log.HasConflict)
				.OrderBy(log => log.StartedUtc);
		}

		public FdbTransactionLog[] FindIntersecting(FdbTransactionLog log, bool writeOnly = false)
		{
			var set = new Dictionary<Guid, FdbTransactionLog>();
			foreach (var entry in this.Timeline.Scan(log.StartedUtc.UtcTicks, log.StoppedUtc!.Value.UtcTicks))
			{
				foreach (var l in entry.Value.Concurrent)
				{
					if (l == log) continue;
					if (writeOnly && l.IsReadOnly) continue;
					set.TryAdd(l.Uuid, l);
				}
			}

			return set.Values.OrderBy(l => l.StartedUtc).ToArray();
		}

	}

}
