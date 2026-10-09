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

namespace FoundationDB.FdbLite.Benchmarks
{
	using System.Diagnostics;
	using System.Runtime.InteropServices;

	/// <summary>Purges the OS file-system cache so a "cold" read provably reaches the device, per platform.</summary>
	/// <remarks>
	/// <para>The cold benchmark series is only honest if the cache is actually purged. This helper returns a RESULT that says whether it worked; the harness MUST mark results not-cold (or refuse the cold series) when it did not, never report warm numbers under a cold label.</para>
	/// <para>Every purge path needs elevation (Windows standby-list empty needs SeProfileSingleProcessPrivilege or RAMMap; macOS <c>purge</c> may prompt for sudo; Linux drop_caches needs root). The runner cannot self-elevate: run the cold series from an elevated shell, per the recipe in README.md.</para>
	/// </remarks>
	public static class OsCachePurge
	{

		public readonly record struct Result(bool Purged, string Method, string Detail)
		{
			public override string ToString() => this.Purged ? $"PURGED via {this.Method}" : $"NOT PURGED ({this.Method}: {this.Detail}) - results are NOT COLD";
		}

		/// <summary>Environment override naming the exact purge tool to use on this machine (takes precedence over the built-in probes); e.g. a RAMMap path, or a custom script.</summary>
		public const string OverrideEnvVar = "FDBLITE_CACHE_PURGE_CMD";

		/// <summary>Attempts to purge the file cache. Never throws: failure is reported in the result.</summary>
		public static Result Purge()
		{
			var overrideCmd = Environment.GetEnvironmentVariable(OverrideEnvVar);
			if (!string.IsNullOrWhiteSpace(overrideCmd))
			{
				return RunShell(overrideCmd, "override");
			}

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				return PurgeWindows();
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				return RunProcess("purge", "", "macos purge");
			}
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// writing 3 needs root; the harness runs elevated for the cold series
				try
				{
					File.WriteAllText("/proc/sys/vm/drop_caches", "3");
					return new(true, "drop_caches", "/proc/sys/vm/drop_caches=3");
				}
				catch (Exception e)
				{
					return new(false, "drop_caches", e.GetType().Name + " (need root)");
				}
			}
			return new(false, "none", "unsupported platform");
		}

		private static Result PurgeWindows()
		{
			// preferred: Sysinternals RAMMap -Et (empties the standby list); requires RAMMap on PATH or named via the override var
			var rammap = ProbeOnPath("RAMMap64.exe") ?? ProbeOnPath("RAMMap.exe");
			if (rammap != null)
			{
				return RunProcess(rammap, "-Et", "RAMMap -Et");
			}
			// fallback: the native standby-list purge (needs SeProfileSingleProcessPrivilege = elevation)
			try
			{
				// an admin token CARRIES the privilege but it is DISABLED by default; it must be enabled
				// in the process token before NtSetSystemInformation accepts the purge (else 0xC0000061)
				if (!TryEnablePrivilege("SeProfileSingleProcessPrivilege", out var privDetail))
				{
					return new(false, "NtSetSystemInformation", $"cannot enable SeProfileSingleProcessPrivilege ({privDetail}) - run elevated");
				}
				int status = NtEmptyStandbyList();
				return status == 0
					? new(true, "NtSetSystemInformation", "MemoryPurgeStandbyList")
					: new(false, "NtSetSystemInformation", $"NTSTATUS 0x{status:X8} (need elevation)");
			}
			catch (Exception e)
			{
				return new(false, "NtSetSystemInformation", e.GetType().Name);
			}
		}

		private const int SE_PRIVILEGE_ENABLED = 0x00000002;
		private const int TOKEN_ADJUST_PRIVILEGES = 0x0020;
		private const int TOKEN_QUERY = 0x0008;
		private const int ERROR_NOT_ALL_ASSIGNED = 1300;

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private struct TokenPrivileges1
		{
			public int Count;
			public long Luid;
			public int Attributes;
		}

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern bool OpenProcessToken(nint processHandle, int desiredAccess, out nint tokenHandle);

		[DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		private static extern bool LookupPrivilegeValue(string? systemName, string name, out long luid);

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern bool AdjustTokenPrivileges(nint tokenHandle, bool disableAll, ref TokenPrivileges1 newState, int bufferLength, nint previousState, nint returnLength);

		[DllImport("kernel32.dll")]
		private static extern nint GetCurrentProcess();

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool CloseHandle(nint handle);

		private static bool TryEnablePrivilege(string privilege, out string detail)
		{
			nint token = 0;
			try
			{
				if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
				{
					detail = $"OpenProcessToken failed (win32 {Marshal.GetLastWin32Error()})";
					return false;
				}
				if (!LookupPrivilegeValue(null, privilege, out long luid))
				{
					detail = $"LookupPrivilegeValue failed (win32 {Marshal.GetLastWin32Error()})";
					return false;
				}
				var tp = new TokenPrivileges1 { Count = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
				if (!AdjustTokenPrivileges(token, false, ref tp, 0, 0, 0))
				{
					detail = $"AdjustTokenPrivileges failed (win32 {Marshal.GetLastWin32Error()})";
					return false;
				}
				// AdjustTokenPrivileges reports success even when nothing was assigned; the real answer is in the last error
				if (Marshal.GetLastWin32Error() == ERROR_NOT_ALL_ASSIGNED)
				{
					detail = "privilege not held by the token (process is not elevated)";
					return false;
				}
				detail = "enabled";
				return true;
			}
			finally
			{
				if (token != 0) { CloseHandle(token); }
			}
		}

		private const int SystemMemoryListInformation = 0x50;
		private const int MemoryPurgeStandbyList = 4;

		[DllImport("ntdll.dll")]
		private static extern int NtSetSystemInformation(int infoClass, ref int info, int length);

		private static int NtEmptyStandbyList()
		{
			int command = MemoryPurgeStandbyList;
			return NtSetSystemInformation(SystemMemoryListInformation, ref command, sizeof(int));
		}

		private static string? ProbeOnPath(string exe)
		{
			foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(dir)) { continue; }
				try
				{
					var candidate = Path.Combine(dir, exe);
					if (File.Exists(candidate)) { return candidate; }
				}
				catch { /* malformed PATH entry */ }
			}
			return null;
		}

		private static Result RunProcess(string fileName, string args, string method)
		{
			try
			{
				using var p = Process.Start(new ProcessStartInfo(fileName, args) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true });
				if (p == null) { return new(false, method, "process did not start"); }
				p.WaitForExit(30_000);
				return p.ExitCode == 0 ? new(true, method, "exit 0") : new(false, method, $"exit {p.ExitCode}: {p.StandardError.ReadToEnd().Trim()}");
			}
			catch (Exception e)
			{
				return new(false, method, e.GetType().Name + ": " + e.Message);
			}
		}

		private static Result RunShell(string command, string method)
		{
			bool win = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
			var psi = win
				? new ProcessStartInfo("cmd.exe", "/c " + command)
				: new ProcessStartInfo("/bin/sh", "-c \"" + command.Replace("\"", "\\\"") + "\"");
			psi.UseShellExecute = false;
			psi.RedirectStandardError = true;
			psi.RedirectStandardOutput = true;
			return RunProcess(psi.FileName, psi.Arguments, method);
		}

	}

}
