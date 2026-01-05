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

namespace FdbShell
{

	public static class FdbCliCommands
	{

		public sealed record FdbCliResult
		{
			public required int ExitCode { get; init; }

			public required string StdOut { get; init; }

			public required string StdErr { get; init; }
		}

		public static Task<FdbCliResult> RunFdbCliCommand(string? command, string? options, string? clusterFile, IFdbShellTerminal terminal, CancellationToken ct)
		{
			string path = "fdbcli";
			string arguments = string.Format("-C \"{0}\" {2} --exec \"{1}\"", clusterFile, command, options);
			string workingDirectory = Environment.CurrentDirectory;

			var startInfo = new ProcessStartInfo
			{
				FileName = path,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				StandardOutputEncoding = Encoding.Default,
				CreateNoWindow = true
			};
			if (OperatingSystem.IsWindows())
			{
				startInfo.LoadUserProfile = false;
			}

			var proc = new Process()
			{
				StartInfo = startInfo,
			};

			var stdOut = new StringBuilder();
			var stdErr = new StringBuilder();

			// Capture StdOut
			proc.OutputDataReceived += (_, e) =>
			{
				if (e.Data != null)
				{
					stdOut.AppendLine(e.Data);
				}
			};
			startInfo.StandardOutputEncoding = Encoding.Default;
			// Capture StdErr
			proc.ErrorDataReceived += (_, e) =>
			{
				if (e.Data != null)
				{
					stdErr.AppendLine(e.Data);
				}
			};

			var cts = new TaskCompletionSource<FdbCliResult>(proc);

			// Bind task to process completion
			proc.EnableRaisingEvents = true;
			proc.Exited += (_, e) =>
			{
				var p = (cts.Task.AsyncState as Process)!;
				// We need to call WaitForExit() in order to be sure that StdOut and StdErr have been drained completely
				p.WaitForExit();
				cts.TrySetResult(new()
				{
					ExitCode = p.ExitCode,
					StdOut = stdOut.ToString(),
					StdErr = stdErr.ToString()
				});
			};

			proc.Start();
			proc.BeginOutputReadLine();
			proc.BeginErrorReadLine();
			return cts.Task;
		}

	}

}
