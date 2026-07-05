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

namespace SnowBank.Testing
{
	using Microsoft.Extensions.Logging;

	internal class NUnitLogger : ILogger
	{

		private static readonly string NewLineWithMessagePadding = Environment.NewLine + "# > ";

		public string Name { get; }

		private string LoggedName { get; }

		private string? LoggedActor { get; }

		private NUnitLoggerOptions Options { get; }

		public IExternalScopeProvider? ScopeProvider { get; set; }

		public NUnitLogger(string name, NUnitLoggerOptions options, IExternalScopeProvider? provider)
		{
			this.Name = name;
			this.Options = options;
			this.ScopeProvider = provider;
			if (options.UseShortName)
			{
				int p = name.LastIndexOf('.');
				this.LoggedName = "[" + (p < 0 ? name : name[(p + 1)..]) + "]";
			}
			else
			{
				this.LoggedName = "[" + name + "]";
			}

			if (options.ActorId != null)
			{
				this.LoggedActor = "@" + options.ActorId;
			}
			else
			{
				this.LoggedActor = null;
			}
		}

		private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
		{
			LogLevel.Trace => "  ~~~~~",
			LogLevel.Debug => "  =====",
			LogLevel.Information => "  INFO ",
			LogLevel.Warning => "! WARN ",
			LogLevel.Error => "* ERROR",
			LogLevel.Critical => "**FATAL",
			_ => throw new ArgumentOutOfRangeException(nameof(logLevel))
		};

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string?> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}
			Contract.NotNull(formatter);

			var now = DateTime.Now;
			var message = formatter(state, exception);

			if (!string.IsNullOrEmpty(message) || exception != null)
			{
				WriteMessage(now, logLevel, this.LoggedActor, this.LoggedName, eventId.Id, eventId.Name, message, exception);
			}
		}

		[ThreadStatic]
		private static StringBuilder? CachedBuilderInstance;

		public void WriteMessage(DateTime now, LogLevel logLevel, string? actorId, string logName, int eventId, string? eventName, string? message, Exception? exception)
		{
			var logBuilder = CachedBuilderInstance;
			CachedBuilderInstance = null;

			logBuilder ??= new();

			CreateDefaultLogMessage(logBuilder, now, logLevel, actorId, logName, eventId, message, exception);

			var output = (logLevel >= LogLevel.Error ? this.Options.OutputError : null) ?? this.Options.Output ?? TestContext.Progress;
			string text = logBuilder.ToString();
			output.WriteLine(text);

			System.Diagnostics.Debug.WriteLine(text);

			this.Options.MessageHandler?.Invoke((logLevel, logName, eventId, eventName, message, exception));

			logBuilder.Clear();
			if (logBuilder.Capacity > 1024)
			{
				logBuilder.Capacity = 1024;
			}
			CachedBuilderInstance = logBuilder;
		}

		private void CreateDefaultLogMessage(StringBuilder logBuilder, DateTime now, LogLevel logLevel, string? actorId, string logName, int eventId, string? message, Exception? exception)
		{
			logBuilder.Append("# ");

			// timestamp
			if (this.Options.TraceTimestamp)
			{
				var origin = this.Options.DateOrigin;
				if (origin != null)
				{
					logBuilder.Append((now - origin.Value).TotalSeconds.ToString("N3", CultureInfo.InvariantCulture)).Append(' ');
				}
				else
				{
					logBuilder.Append(now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(' ');
				}
			}

			// log level
			logBuilder.Append(GetLogLevelString(logLevel));

			// logger id
			if (actorId != null)
			{
				logBuilder.Append(' ').Append(actorId);
				if (actorId.Length < 10) logBuilder.Append(' ', 10 - actorId.Length);
			}

			// category
			logBuilder.Append(' ').Append(logName);
			if (logName.Length < 32) logBuilder.Append(' ', 32 - logName.Length);

			// event id
			if (this.Options.IncludeEventId && eventId != 0)
			{
#if NET6_0_OR_GREATER
				logBuilder.Append(CultureInfo.InvariantCulture, $" (ID {eventId:X})");
#else
				// the interpolated-handler Append overload is not available: format to a transient string (extra allocation)
				logBuilder.Append(string.Format(CultureInfo.InvariantCulture, " (ID {0:X})", eventId));
#endif
			}

			// scope information
			if (this.Options.IncludeScopes)
			{
				GetScopeInformation(logBuilder, multiLine: true);
			}

			// message
			if (!string.IsNullOrEmpty(message))
			{
				logBuilder.Append(" \"").Append(message).Append('"');
			}

			// Example:
			// System.InvalidOperationException
			//    at Namespace.Class.Function() in File:line X
			if (exception != null)
			{
				// exception message
				logBuilder.AppendLine().Append("Error: ").Append(exception.ToString());
			}

			logBuilder.Replace(Environment.NewLine, NewLineWithMessagePadding);
		}

		private void GetScopeInformation(StringBuilder stringBuilder, bool multiLine)
		{
			var scopeProvider = this.ScopeProvider;
			if (scopeProvider != null)
			{
				var initialLength = stringBuilder.Length;

				scopeProvider.ForEachScope((scope, state) =>
				{
					var (builder, paddAt) = state;
					var padd = paddAt == builder.Length;
					if (padd)
					{
						builder.AppendLine().Append("        => ");
					}
					else
					{
						builder.AppendLine().Append(" => ");
					}
					builder.Append(scope);
				}, (stringBuilder, multiLine ? initialLength : -1));

				if (stringBuilder.Length > initialLength && multiLine)
				{
					stringBuilder.AppendLine();
				}
			}
		}

		public bool IsEnabled(LogLevel logLevel)
		{
			return logLevel >= this.Options.LogLevel && logLevel < LogLevel.None;
		}

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => this.ScopeProvider?.Push(state) ?? NullScope.Instance;

	}

}
