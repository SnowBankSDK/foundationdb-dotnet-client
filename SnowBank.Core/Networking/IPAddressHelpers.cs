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

// ReSharper disable AccessToDisposedClosure
// ReSharper disable InconsistentNaming

namespace SnowBank.Networking
{
	using System.Buffers.Binary;
	using System.Globalization;
	using System.Net;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;
	using System.Text;
	using SnowBank.Buffers.Binary;
	using SnowBank.Runtime.Converters;

	/// <summary>Helpers for working with IP addresses (or MAC addresses)</summary>
	[PublicAPI]
	public static class IPAddressHelpers
	{

		/// <summary>Indicates whether an IP address (v4/v6) is syntactically valid</summary>
		/// <param name="ip">IPv4 address to check (e.g. "192.168.1.0")</param>
		/// <returns>True if the IP address is syntactically valid (4 numbers from 0 to 255)</returns>
		
		public static bool IsValidIP([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out _);
		}

#if !NETSTANDARD2_0

		/// <summary>Indicates whether an IP address (v4/v6) is syntactically valid</summary>
		/// <param name="ip">IPv4 address to check (e.g. "192.168.1.0")</param>
		/// <returns>True if the IP address is syntactically valid (4 numbers from 0 to 255)</returns>
		public static bool IsValidIP(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out _);
		}

#endif

		/// <summary>Determines whether this is a valid IPv4 address</summary>
		/// <param name="ip">String to check</param>
		/// <returns>true if it is a valid IPv4, false in all other cases</returns>
		public static bool IsValidIPv4([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetwork;
		}

#if !NETSTANDARD2_0

		/// <summary>Determines whether this is a valid IPv4 address</summary>
		/// <param name="ip">String to check</param>
		/// <returns>true if it is a valid IPv4, false in all other cases</returns>
		public static bool IsValidIPv4(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetwork;
		}

#endif

		/// <summary>Determines whether this is a valid IPv6 address</summary>
		/// <param name="ip">String to check</param>
		/// <returns>true if it is a valid IPv6, false in all other cases</returns>
		public static bool IsValidIPv6([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetworkV6;
		}

#if !NETSTANDARD2_0

		/// <summary>Determines whether this is a valid IPv6 address</summary>
		/// <param name="ip">String to check</param>
		/// <returns>true if it is a valid IPv6, false in all other cases</returns>
		public static bool IsValidIPv6(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetworkV6;
		}

#endif

		/// <summary>Determines whether this is an "any" IP address (0.0.0.0 or '::')</summary>
		public static bool IsAny(IPAddress? address)
		{
			return address != null && (IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address));
		}

		/// <summary>Converts an IP address into a lexicographically sortable version</summary>
		/// <param name="address"></param>
		/// <returns></returns>
		/// <example>ToSortableAddress("172.16.1.1") => "172.016.001.001"</example>
		[return: NotNullIfNotNull("address")]
		public static string? ToSortableAddress(IPAddress? address)
		{
			if (address == null) return null;

			switch (address.AddressFamily)
			{
				case AddressFamily.InterNetwork:
				{
					return ToSortableIPv4(address);
				}
				case AddressFamily.InterNetworkV6:
				{
					//TODO: how do we make an IPv6 sortable?
					return address.ToString();
				}
				default:
				{
					return address.ToString();
				}
			}
		}

		private static unsafe string ToSortableIPv4(IPAddress address)
		{
#pragma warning disable 618
			// Note: we are in IPv4 so we can use .Address without any problem
			long bytes = address.Address;
#pragma warning restore 618

			// result: "000.000.000.000" = 15 chars but we allocate 16 to make it a round number
			char* buffer = stackalloc char[16];
			int p = 14;

			int x = (int) ((bytes >> 24) & 0xFF);
			for (int i = 0; i < 3; i++)
			{
				buffer[p--] = (char) ((x % 10) + 48);
				x /= 10;
			}
			buffer[p--] = '.';

			x = (int) ((bytes >> 16) & 0xFF);
			for (int i = 0; i < 3; i++)
			{
				buffer[p--] = (char) ((x % 10) + 48);
				x /= 10;
			}
			buffer[p--] = '.';

			x = (int) ((bytes >> 8) & 0xFF);
			for (int i = 0; i < 3; i++)
			{
				buffer[p--] = (char) ((x % 10) + 48);
				x /= 10;
			}
			buffer[p--] = '.';

			x = (int) (bytes & 0xFF);
			for (int i = 0; i < 3; i++)
			{
				buffer[p--] = (char) ((x % 10) + 48);
				x /= 10;
			}

			Contract.Debug.Ensures(p == -1);

			return new string(buffer, 0, 15);
		}

		/// <summary>Test if the IP address is a Private Network (192.168., 10., ...) or not.</summary>
		public static bool IsPrivateRange(IPAddress address)
		{
			Contract.NotNull(address);

			switch (address.AddressFamily)
			{
				case AddressFamily.InterNetwork:
				{ // IPv4
					//note: Address is in "network order", so "AA.BB.CC.DD" => 0xDDCCBBAA !
#pragma warning disable CS0618
					var bits = address.Address;
#pragma warning restore CS0618

					if ((bits & 0x00FF) == 0x000A) return true; //    10.0.0.0/8  : 10.0.0.0     10.255.255.255
					if ((bits & 0xFFFF) == 0xA8C0) return true; // 196.168.0.0/16 : 192.168.0.0  192.168.255.255
					if ((bits & 0xF0FF) == 0x10AC) return true; //  172.16.0.0/20 : 172.16.0.0   172.31.255.255

					return false;
				}
				case AddressFamily.InterNetworkV6:
				{ // IPv6
					//REVIEW: are there any other cases?
					return address.IsIPv6SiteLocal;
				}
				default:
				{
					// note: IPAddress currently only returns one of the two enums above, but we guard against the future!
					return false;
				}
			}
		}

		/// <summary>Returns the first IPv4 in the list, or otherwise the first IPv6</summary>
		/// <param name="list">List of candidate IP addresses</param>
		/// <returns>First IPv4 address found, or null if none (or only IPv6)</returns>
		public static IPAddress? GetPreferredAddress(IPAddress[]? list)
		{
			if (list == null || list.Length == 0) return null;

			IPAddress? v6 = null;
			foreach (IPAddress address in list)
			{
				if (address.AddressFamily == AddressFamily.InterNetwork)
				{
					return address;
				}
				if (address.AddressFamily == AddressFamily.InterNetworkV6 && v6 == null)
				{
					v6 = address;
				}
			}
			return v6;
		}

		public static bool TryGetLocalAddressForRemoteAddress(IPAddress remoteAddress, [MaybeNullWhen(false)] out IPAddress localAddress)
		{
			Contract.NotNull(remoteAddress);
			
			if (IPAddress.IsLoopback(remoteAddress))
			{ // locally we return the same one
				localAddress = remoteAddress;
				return true;
			}

			// Life Pro Tip: to find the IP matching the right network adapter able to talk to a remote IP,
			// just do a fake "Connect" on a UDP socket and inspect the local endpoint
			// => the OS will do the lookup in the routing table for us, and return the right value!
			try
			{
				using (var sock = new Socket(remoteAddress.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
				{
					//HACKHACK: OfTheDead
					sock.Connect(remoteAddress, 161);
					localAddress = ((IPEndPoint?) sock.LocalEndPoint)?.Address;
					return localAddress != null;
				}
			}
			catch (Exception e)
			{
				Debug.WriteLine($"### Failed to get local address able to talk to remote address {remoteAddress}: {e}");
				localAddress = null;
				return false;
			}
		}
		
		/// <summary>Converts an IP address into a long (e.g. "255.255.255.0" -> 0xFFFFFF00)</summary>
		/// <param name="ip">IP address</param>
		/// <returns>Corresponding binary mask</returns>
		public static long IPToMask(string ip)
		{
			Contract.NotNull(ip);
			return IPToMask(IPAddress.Parse(ip));
		}

		/// <summary>Converts an IP address into a long (e.g. "255.255.255.0" -> 0xFFFFFF00)</summary>
		/// <param name="ip">IP address</param>
		/// <returns>Corresponding binary mask</returns>
		public static long IPToMask(IPAddress ip)
		{
			Contract.NotNull(ip);
			return BitConverter.ToUInt32(ip.GetAddressBytes(), 0);
		}

		public static IPAddress SubnetToCidr(IPAddress address, IPAddress subnet)
		{
			long v = IPToMask(address);
			long m = IPToMask(subnet);
			v &= m;
			return new IPAddress(v);
		}

		/// <summary>Determines the broadcast IP address from an IP address and a subnet mask</summary>
		/// <param name="ip">Host IP address (e.g. 192.168.1.156)</param>
		/// <param name="subnet">Subnet mask (e.g. 255.255.255.0)</param>
		/// <returns>Corresponding broadcast IP address (192.168.1.255)</returns>
		public static string IPToBroadcast(string ip, string subnet)
		{
			Contract.NotNull(ip);
			Contract.NotNull(subnet);

			long v = IPToMask(ip);
			long m = IPToMask(subnet);
			v &= m;
			m = (-1 ^ m) & 0xFFFFFFFF;
			v |= m;
			return new IPAddress(v).ToString();
		}

		private static int CountOccurrences(string s, char t)
		{
#if NET8_0_OR_GREATER
			return s.AsSpan().Count(t);
#else
			int n = 0;
			foreach (var c in s)
			{
				if (c == t) n++;
			}
			return n;
#endif
		}

		/// <summary>Tests whether an IP address is part of a range.
		/// several formats are accepted.
		/// e.g. for "between 192.168.1.0 and 192.168.1.255")
		///     "192.168.1.*"
		///     "192.168.1.0-255"
		///     "192.168.1.0/255.255.255.0"
		///     "192.168.1.0/24"
		/// </summary>
		/// <param name="ip">IP address to test</param>
		/// <param name="range">IP range</param>
		/// <returns>'true' if the IP is within the range (bounds included)</returns>
		public static bool IPMatchRange(string? ip, string? range)
		{
			if (string.IsNullOrEmpty(ip)) return false;
			if (string.IsNullOrEmpty(range)) return false;

			// "192.168.1.*"
			int p = range.IndexOf('*');
			if (p >= 0)
			{ 
				return ip.AsSpan(0, p).SequenceEqual(range.AsSpan(0, p));
			}

			// "192.168/16"
			p = range.IndexOf('/');
			if (p >= 0)
			{
				long mask;
				long lip = IPToMask(ip);
				string rm = range.Substring(0, p);
				int nc = CountOccurrences(rm, '.');
				if (nc < 3)
				{
					rm += nc switch
					{
						2 => ".0",
						1 => ".0.0",
						0 => ".0.0.0",
						_ => throw new ArgumentException($"Invalid range \'{rm}\'", nameof(range))
					};
				}
				long lval = IPToMask(rm);
				if (range.IndexOf('.', p) > 0)
				{ // format "192.168.1.0/255.255.255.0"
					mask = IPToMask(range[(p + 1)..]);
				}
				else
				{ // format "192.168.1.0/24"
					int offset = Convert.ToInt16(range[(p + 1)..]);
					mask = (1 << (offset)) - 1;
				}
				return ((lip & mask) == (lval & mask));
			}

			// "10.10.0.0-255"
			p = range.IndexOf('-');
			if (p >= 0)
			{ // TODO: format "192.168.1.0-255" for IPMatchRange

				string right = range[(p + 1)..].Trim();
				if (right.IndexOf('.') > 0)
				{ // "1.2.3.4-5.6.7.8"
					if (!IPAddress.TryParse(ip, out var ipAddr))
					{
						return false;
					}
					DecodeIPRange(range, out IPAddress first, out IPAddress last);
					return IPAddressComparer.Default.Compare(ipAddr, first) >= 0
					    && IPAddressComparer.Default.Compare(ipAddr, last) <= 0;
				}

				// 1.2.3.0-255
				int q = range.LastIndexOf('.');
				if (!ip.AsSpan(0, q).SequenceEqual(range.AsSpan(0, q)))
				{
					return false;
				}

				string submask = range.Substring(q + 1);
				if (submask == "0-255") return true;
				string[] tok = submask.Split('-');
				int n = Convert.ToInt16(ip.Substring(q + 1));
				if (n < Convert.ToInt16(tok[0])) return false;
				if (n > Convert.ToInt16(tok[1])) return false;
				return true;
			}
			// range made of a single ip?
			return (ip == range);
		}

		/// <summary>Returns the bounds of an IP address range</summary>
		/// <param name="range">IP range ("192.168.1.0/24", "192.168.1.0|255.255.255.0", "192.168.1.1-192.168.1.255", "192.168.1")</param>
		/// <param name="first">Receives the first IP address of the range</param>
		/// <param name="last">Receives the last IP address of the range</param>
		/// <param name="include0">If true, includes "192.168.0.0" as a valid address</param>
		/// <remarks>Throws an exception on error, in which case first and last are set to null</remarks>
		public static void DecodeIPRange(string range, out IPAddress first, out IPAddress last, bool include0 = false)
		{
			Contract.NotNull(range);
			if (range.Length < 2) throw new ArgumentException("Range cannot be empty", nameof(range));

			int p = range.IndexOf("/", StringComparison.Ordinal);
			if (p >= 0)
			{ // format "w.x.y.z/S" (ex: "192.168.1.0/24")
				int subnet = StringConverters.ToInt32(range.AsSpan(p + 1), -1);
				if (subnet == -1) throw new FormatException($"Invalid IP range '{range}' : subnet is invalid");
				if (subnet < 1 || subnet > 32) throw new FormatException($"Invalid IP rage '{range}' : subnet (/{subnet}) is out of range");

#if !NETSTANDARD2_0
				var tmp = range.AsSpan(0, p);
				if (!IPAddress.TryParse(tmp, out var addr))
				{
					throw new FormatException($"Invalid IP range '{range}' : network address ({tmp.ToString()}) is invalid");
				}
#else
				string tmp = range.Substring(0, p);
				if (!IPAddress.TryParse(tmp, out var addr))
				{
					throw new FormatException($"Invalid IP range '{range}' : network address ({tmp}) is invalid");
				}
#endif

				// we know the subnet (/8, /16, /24, ..) and the address
				// we need to derive the start address from it
				if (addr.AddressFamily == AddressFamily.InterNetworkV6) throw new NotSupportedException("IPv6 is not currently supported!");

				// the address is made of the first "subnet" bits
				long bytes = addr.GetAddressBytes().AsSpan().ToUInt32BE();

				// check that the IP is actually addressable if we are in /32
				if (subnet == 32 && ((bytes & 0xFF) == 0 || (bytes & 0xFF) == 255)) throw new FormatException($"Invalid IP range '{tmp.ToString()}': invalid /32 address! Should be .1 or .254");

				// mask that keeps the "subnet" high-order bits
				long submask = ((1 << (32 - subnet)) - 1);  // 32 => 0x00000000, 24 => 0x000000FF, 16 => 0x0000FFFF, ...
				long mask = 0xFFFFFFFF ^ submask;           // 32 => 0xFFFFFFFF, 24 => 0xFFFFFF00, 16 => 0xFFFF0000, ...

				long start = (bytes & mask);
				long end = (bytes & mask) + submask;

				// "round" the edges (.0 and .255) when they are not valid addresses
				if ((start & 0xFF) == 0 && !include0)
				{
					//ie: 192.168.1.0/24 covers 192.168.1.0 .. 192.168.1.255, bounds that are generally excluded
					// However, 192.168.0.0/16 covers 192.168.0.0 .. 192.168.255.255. But here, 192.168.1.0 is NOT a bound, so it is legal
					++start; // 0->1
				}
				else if ((start & 0xFF) == 255)
				{
					//note: however we forbid .255 in all cases, as a precaution...
					--start; // 255->254
				}
				if ((end & 0xFF) == 0 && !include0)
				{
					//see comment for 'start' above
					++end; // 0->1
				}
				else if ((end & 0xFF) == 255)
				{
					--end; // 255->254
				}

				start = BinaryPrimitives.ReverseEndianness((uint) start);
				end = BinaryPrimitives.ReverseEndianness((uint) end);

				first = new IPAddress(start);
				last = new IPAddress(end);
				return;
			}

			p = range.IndexOf('|');
			if (p >= 0)
			{ // format "iprange|ipmask" (ex: "192.168.1.0|255.255.255.0")
				string ipRange = range.Substring(0, p);
				string ipMask = range.Substring(p + 1);
				if (!IsValidIP(ipRange)) throw new FormatException($"Invalid IP range '{range}' : network address ({ipRange}) is invalid");
				if (!IsValidIP(ipMask)) throw new FormatException($"Invalid IP range '{range}' : network mask ({ipMask}) is invalid");

				IPAddress addr = IPAddress.Parse(ipRange);
				if (addr.AddressFamily == AddressFamily.InterNetworkV6) throw new NotSupportedException("IPv6 is not currently supported!");

				long bytes = addr.GetAddressBytes().AsSpan().ToUInt32BE();
				long mask = IPAddress.Parse(ipMask).GetAddressBytes().AsSpan().ToUInt32BE();
				long submask = 0xFFFFFFFF ^ mask;

				long start = (bytes & mask);
				long end = (bytes & mask) + submask;

				// "round" the edges (.0 and .255 are not valid)
				if ((start & 0xFF) == 0 && !include0) start += 1; // 0->1
				else if ((start & 0xFF) == 255) start -= 1; // 255->254
				if ((end & 0xFF) == 0 && !include0) end += 1; // 0->1
				else if ((end & 0xFF) == 255) end -= 1; // 255->254

				first = new IPAddress(UnsafeHelpers.ByteSwap32((uint) start));
				last = new IPAddress(UnsafeHelpers.ByteSwap32((uint) end));
				return;
			}

			p = range.IndexOf('-');
			if (p >= 0)
			{ // format "ipmin-ipmax" (ex; "192.168.1.1-192.168.1.254")

				string one = range.Substring(0, p);
				string two = range.Substring(p + 1);

				if (!IsValidIP(one)) throw new FormatException($"Invalid IP range '{range}' : first term ({one}) is not a valid IP address");
				if (!IsValidIP(two)) throw new FormatException($"Invalid IP range '{range}' : second term ({two}) is not a valid IP address");

				// "round" the edges (.0 and .255 are not valid)
				if (one.EndsWith(".0", StringComparison.Ordinal) && !include0) one = one.Substring(0, one.Length - 2) + ".1";
				if (two.EndsWith(".0", StringComparison.Ordinal) && !include0) two = two.Substring(0, two.Length - 2) + ".1";
				if (one.EndsWith(".255", StringComparison.Ordinal)) one = one.Substring(0, one.Length - 4) + ".254";
				if (two.EndsWith(".255", StringComparison.Ordinal)) two = two.Substring(0, two.Length - 4) + ".254";

				first = IPAddress.Parse(one);
				last = IPAddress.Parse(two);
				return;
			}

			p = CountOccurrences(range, '.');
			if (p < 1 || p > 4) throw new FormatException($"Invalid IP range '{range}' : format is not recognized");

			throw new NotSupportedException($"Range format '{range}' is not supported!");
		}

		/// <summary>Returns the number of addresses between (and including) two bounds</summary>
		/// <param name="from">Start address</param>
		/// <param name="to">Destination address</param>
		/// <param name="include0">true if we want to include .0 addresses</param>
		/// <param name="include255">true if we want to include .255 addresses</param>
		/// <returns>Count</returns>
		public static long GetHostCountBetween(IPAddress from, IPAddress to, bool include0 = false, bool include255 = false)
		{
			Contract.NotNull(from);
			Contract.NotNull(to);
			if (from.AddressFamily != to.AddressFamily) throw new ArgumentException("AddressFamily does not match", nameof(to));
			if (from.AddressFamily == AddressFamily.InterNetworkV6) throw new NotSupportedException("IPv6 not currently supported!!!");

			// simplest case
			if (from.Equals(to))
			{ // a single host in the range
				return 1;
			}

			// get the bytes for the comparisons
			byte[] fromBytes = from.GetAddressBytes();
			byte[] toBytes = to.GetAddressBytes();
			Contract.Debug.Assert(fromBytes.Length == toBytes.Length && fromBytes.Length == 4, "IP address size does not match!");

			// compare the start of the address (excluding the last byte)
			int fromSubnet = (fromBytes[0] << 16) + (fromBytes[1] << 8) + fromBytes[2];
			int toSubnet = (toBytes[0] << 16) + (toBytes[1] << 8) + toBytes[2];

			if (toSubnet == fromSubnet)
			{ // same /24 subnet, simplest case:
				long res = toBytes[3] - fromBytes[3] + 1;
				if (res <= 0) throw new ArgumentException("The 'to' address should be higher than or equal to the 'from' address!", nameof(to));
				return res;
			}
			else if (toSubnet < fromSubnet)
			{ // to is lower than from !?
				throw new ArgumentException("The 'to' address should be higher than or equal to the 'from' address!", nameof(to));
			}
			else
			{ // different subnets
			  // count the number of /24 ranges between the two, including the .0 addresses
				var adressesBySubnet = 254 + (include0 ? 1 : 0) + (include255 ? 1 : 0);
				long res = ((toSubnet - fromSubnet - 1) * adressesBySubnet) + (255 - fromBytes[3] + (include255 ? 1 : 0)) + toBytes[3] + (include0 ? 1 : 0);
				return res;
			}
		}

		/// <summary>Adds an offset to an IP address</summary>
		/// <param name="address">Base address (e.g. 192.168.1.23)</param>
		/// <param name="offset">Offset (e.g. 42)</param>
		/// <returns>New address (e.g. 192.168.1.65)</returns>
		/// <exception cref="ArgumentException">If <paramref name="address"/> is not a supported type (IPv4)</exception>
		public static IPAddress AddOffset(IPAddress address, int offset)
		{
			if (address.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException("Only IPv4 are currently supported!", nameof(address));
#pragma warning disable CS0618
			uint x = checked((uint) address.Address);
#pragma warning restore CS0618
			x = UnsafeHelpers.ByteSwap32(x);
			x = checked(x + (uint) offset);
			x = UnsafeHelpers.ByteSwap32(x);
			return new IPAddress(x);
		}

		/// <summary>Converts a binary MAC address into its string representation</summary>
		/// <param name="mac">Array of 6 bytes containing a MAC address</param>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(byte[] mac)
		{
			Contract.NotNull(mac);
			return MACAddressToString(mac, 0, mac.Length);
		}

		/// <summary>Converts a binary MAC address into its string representation</summary>
		/// <param name="mac">Buffer of 6 bytes containing a MAC address</param>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string MACAddressToString(Slice mac)
		{
			return MACAddressToString(mac.Span);
		}

		/// <summary>Converts a binary MAC address into its string representation</summary>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(byte[] mac, int offset, int count)
		{
			Contract.NotNull(mac);
			return MACAddressToString(mac.AsSpan(offset, count));
		}

		/// <summary>Converts a binary MAC address into its string representation</summary>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(ReadOnlySpan<byte> mac)
		{
			// note: normally this is 6 bytes long, but the MIB_IPNETROW structure sometimes returns an 8-byte buffer (last 2 set to zero)
			if (mac.Length < 6) throw new ArgumentException("MAC addresses are 6 bytes long", nameof(mac));
			return string.Format(CultureInfo.InvariantCulture, "{0:X2}-{1:X2}-{2:X2}-{3:X2}-{4:X2}-{5:X2}", mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
		}

		public static byte[] StringToMACAddress(string mac)
		{
			Contract.NotNullOrEmpty(mac);
			mac = mac.Replace("-", string.Empty).Replace(":", string.Empty);
			if (mac.Length != 12) throw new ArgumentException("mac address length invalid", nameof(mac));
			return Slice.FromHexString(mac).ToArray();
		}

		/// <summary>Send a ping request to the specified target</summary>
		/// <remarks>This implementation supports cancellation</remarks>
		public static async Task<PingReply> PingAsync(IPAddress addr, TimeSpan timeout, byte[] buffer, PingOptions options, CancellationToken ct)
		{
			if (timeout <= TimeSpan.Zero) throw new ArgumentException(null, nameof(timeout));
			ct.ThrowIfCancellationRequested();

			//note: Ping.SendPingAsync does NOT support any direct form of cancellation!
			// => no overload that takes a CancellationToken, and colling Dispose does not abort pending tasks!
			// Current workaround is to stop waiting for the task if the CT fires

			// round up to the nearest ms
			int ms = (int) Math.Ceiling(timeout.TotalMilliseconds);

			using (var ping = new Ping())
			{
				// start the ping
				var task = ping.SendPingAsync(addr, ms, buffer, options);

				// setup cancellation if required
				if (!task.IsCompleted && ct.CanBeCanceled)
				{
					//note: we have to wrap it in our own CTS, because if the original ct is never triggered,
					// we will leak a Task.Delay(...) task for each call!
					using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
					{
						var delay = Task.Delay(Timeout.Infinite, cts.Token);
						if (await Task.WhenAny(task, delay).ConfigureAwait(false) == delay)
						{
							ct.ThrowIfCancellationRequested(); // => throws!
						}
					}
				}
				return await task.ConfigureAwait(false);
			}
		}

		/// <summary>Perform a parallel traceroute from the current host to the specified target</summary>
		/// <param name="address">Target address</param>
		/// <param name="maxDistance">Maximum number of hops to scan (must be greater than 0)</param>
		/// <param name="timeout">Maximum delay when waiting for ICMP replies</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>List of hops needed to reach the target</returns>
		public static async Task<TracerouteReply> TracerouteAsync(IPAddress address, int maxDistance, TimeSpan timeout, CancellationToken ct)
		{
			Contract.NotNull(address);
			Contract.GreaterThan(maxDistance, 0);
			Contract.GreaterThan(timeout.Ticks, 0);
			ct.ThrowIfCancellationRequested();

			int timeoutMs = (int) Math.Ceiling(timeout.TotalMilliseconds);

			bool abortScan = false;
			var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var delay = Task.Delay(Timeout.Infinite, cts.Token);

			// we add a random token to more easily identify all the packets of the same traceroute run
			var token = Guid.NewGuid().GetHashCode().ToString("X08");

			async Task<TracerouteHop?> RunHop(int i)
			{
				var options = new PingOptions(i + 1, dontFragment: false);
				var buffer = Encoding.ASCII.GetBytes($"Doxense-Traceroute-{token}-TTL{i + 1:D03}");

				using (var ping = new Ping())
				{
					// send the ICMP packet...
					var sw = Stopwatch.StartNew();
					var task = ping.SendPingAsync(address, timeoutMs, buffer, options);

					if (await Task.WhenAny(task, delay).ConfigureAwait(false) == delay)
					{ // we were aborted!
						return null;
					}
					sw.Stop();
					var reply = await task.ConfigureAwait(false);

					if (reply.Status is IPStatus.Success or IPStatus.DestinationHostUnreachable)
					{ // no point going any further!
						lock (cts)
						{
							if (!abortScan)
							{
								abortScan = true;
								// cancel all remaining requests without waiting for the full timeout
								try { cts.CancelAfter(100); } catch { }
							}
						}
					}

					return new TracerouteHop
					{
						Status = reply.Status,
						Address = reply.Address,
						Rtt = sw.Elapsed,
						Distance = options.Ttl,
						Private = !IsAny(reply.Address) ? IsPrivateRange(reply.Address) : null,
					};
				}

			}

			var tasks = new List<Task<TracerouteHop?>>(maxDistance);
			for (int i = 0; i < maxDistance; i++)
			{
				if (i != 0) { await Task.Delay(i, ct).ConfigureAwait(false); }

				if (abortScan)
				{
					break;
				}

				tasks.Add(RunHop(i));
			}

			try
			{
				await Task.WhenAll(tasks).ConfigureAwait(false);
			}
			finally
			{
				lock (cts) { cts.Dispose(); }
			}

			ct.ThrowIfCancellationRequested();

			var hops = new List<TracerouteHop>();
			bool lastValid = false;
			IPStatus? status = null;
			foreach (var t in tasks)
			{
				var hop = await t.ConfigureAwait(false);

				if (hop == null) continue; // skip aborted task

				bool validNode = !IsAny(hop.Address);
				if (!validNode && !lastValid)
				{
					continue;
				}
				lastValid = validNode;

				hops.Add(hop);
				if (hop.Address.Equals(address))
				{
					status = hop.Status;
					break;
				}
				if (hop.Status == IPStatus.DestinationHostUnreachable)
				{
					status = IPStatus.DestinationHostUnreachable;
				}
			}

			return new TracerouteReply
			{
				Status = status ?? IPStatus.TimedOut,
				MaxTtl = maxDistance,
				Timeout = timeout,
				Hops = hops
			};
		}

	}

	[DebuggerDisplay("Status={Status}, MaxTtl={MaxTtl}, Hops={Hops.Count}")]
	public sealed record TracerouteReply
	{
		/// <summary>Result of the traceroute</summary>
		public required IPStatus Status { get; init; }

		/// <summary>Maximum TTL</summary>
		public required int MaxTtl { get; init; }

		/// <summary>Timeout</summary>
		public required TimeSpan Timeout { get; init; }

		/// <summary>List of hops that have replied</summary>
		public required List<TracerouteHop> Hops { get; init; }

	}

	[DebuggerDisplay("Distance={Distance}, Status={Status}, Address={Address}, Rtt={Rtt}, Private={Private}")]
	public sealed record TracerouteHop
	{
		public required int Distance { get; init; }

		public required IPStatus Status { get; init; }

		public required IPAddress Address { get; init; }

		public required TimeSpan Rtt { get; init; }

		public bool? Private { get; init; }

		public override string ToString()
		{
			return $"{this.Distance} {this.Rtt.TotalSeconds:N3} {this.Address} [{this.Status}]";
		}

	}

}
