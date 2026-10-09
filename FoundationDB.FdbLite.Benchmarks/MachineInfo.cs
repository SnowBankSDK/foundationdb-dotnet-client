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
	using System.Runtime.InteropServices;

	/// <summary>Best-effort physical-memory probe, so tier-2 (restart) numbers can be stated against machine RAM: a dataset larger than RAM cannot be served purely from the OS cache, and the "restart" tier silently degrades to device reads past that point.</summary>
	public static class MachineInfo
	{

		/// <summary>Total physical memory in bytes, or 0 when it cannot be determined on this platform.</summary>
		public static long PhysicalMemoryBytes()
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					var status = new MEMORYSTATUSEX { dwLength = (uint) Marshal.SizeOf<MEMORYSTATUSEX>() };
					return GlobalMemoryStatusEx(ref status) ? (long) status.ullTotalPhys : 0;
				}
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					foreach (var line in File.ReadLines("/proc/meminfo"))
					{
						if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
						{
							var parts = line.Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries);
							return long.Parse(parts[1]) * 1024; // kB
						}
					}
					return 0;
				}
				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					// sysctlbyname("hw.memsize") returns bytes as a 64-bit int
					long value = 0;
					var len = (nuint) sizeof(long);
					return sysctlbyname("hw.memsize", ref value, ref len, IntPtr.Zero, 0) == 0 ? value : 0;
				}
			}
			catch { /* best effort */ }
			return 0;
		}

		public static string Describe(long datasetBytes)
		{
			long ram = PhysicalMemoryBytes();
			string ds = $"{datasetBytes / (1 << 20):N0} MiB";
			if (ram <= 0)
			{
				return $"dataset {ds}, physical RAM unknown (compare manually: a dataset > RAM cannot stay in the OS cache)";
			}
			double ratio = (double) datasetBytes / ram;
			string verdict = ratio < 0.5 ? "fits cache comfortably" : ratio < 1.0 ? "near cache capacity - watch for a cliff" : "EXCEEDS RAM - restart tier will hit the device (mixed, not pure cache)";
			return $"dataset {ds} vs physical RAM {ram / (1 << 20):N0} MiB ({ratio:P0}): {verdict}";
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MEMORYSTATUSEX
		{
			public uint dwLength;
			public uint dwMemoryLoad;
			public ulong ullTotalPhys;
			public ulong ullAvailPhys;
			public ulong ullTotalPageFile;
			public ulong ullAvailPageFile;
			public ulong ullTotalVirtual;
			public ulong ullAvailVirtual;
			public ulong ullAvailExtendedVirtual;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

		[DllImport("libc", SetLastError = true)]
		private static extern int sysctlbyname(string name, ref long oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

	}

}
