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

	/// <summary>Helpers permettant de travailler sur des adresses IP (ou MAC address)</summary>
	[PublicAPI]
	public static class IPAddressHelpers
	{

		/// <summary>Indique si une adresse IP(v4/v6) est valide syntaxiquement</summary>
		/// <param name="ip">Adresse IPv4 à vérifier (ex: "192.168.1.0")</param>
		/// <returns>True si l'adresse IP est valide syntaxiquement (4 nombres de 0 à 255)</returns>
		
		public static bool IsValidIP([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out _);
		}

		/// <summary>Indique si une adresse IP(v4/v6) est valide syntaxiquement</summary>
		/// <param name="ip">Adresse IPv4 à vérifier (ex: "192.168.1.0")</param>
		/// <returns>True si l'adresse IP est valide syntaxiquement (4 nombres de 0 à 255)</returns>
		public static bool IsValidIP(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out _);
		}

		/// <summary>Détermine s'il s'agit d'une adresse IPv4 valide</summary>
		/// <param name="ip">Chaîne à vérifier</param>
		/// <returns>true si c'est une IPv4 valide, false dans tout les autres cas</returns>
		public static bool IsValidIPv4([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetwork;
		}

		/// <summary>Détermine s'il s'agit d'une adresse IPv4 valide</summary>
		/// <param name="ip">Chaîne à vérifier</param>
		/// <returns>true si c'est une IPv4 valide, false dans tout les autres cas</returns>
		public static bool IsValidIPv4(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetwork;
		}

		/// <summary>Détermine s'il s'agit d'une adresse IPv6 valide</summary>
		/// <param name="ip">Chaîne à vérifier</param>
		/// <returns>true si c'est une IPv6 valide, false dans tout les autres cas</returns>
		public static bool IsValidIPv6([NotNullWhen(true)] string? ip)
		{
			return !string.IsNullOrEmpty(ip) && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetworkV6;
		}

		/// <summary>Détermine s'il s'agit d'une adresse IPv6 valide</summary>
		/// <param name="ip">Chaîne à vérifier</param>
		/// <returns>true si c'est une IPv6 valide, false dans tout les autres cas</returns>
		public static bool IsValidIPv6(ReadOnlySpan<char> ip)
		{
			return ip.Length != 0 && IPAddress.TryParse(ip, out var value) && value.AddressFamily == AddressFamily.InterNetworkV6;
		}

		/// <summary>Détermine s'il s'agit d'une adresse IP "any" (0.0.0.0 ou '::')</summary>
		public static bool IsAny(IPAddress? address)
		{
			return address != null && (IPAddress.Any.Equals(address) || IPAddress.IPv6Any.Equals(address));
		}

		/// <summary>Convertit une adresse IP en version triable lexicographiquement</summary>
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
					//TODO: comment rendre une IPv6 sortable ?
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
			// Note: on est en IPv4 donc on peut utiliser .Address sans problèmes
			long bytes = address.Address;
#pragma warning restore 618

			// résultat: "000.000.000.000" = 15 chars mais on on alloue 16 pour que ce soit un nombre rond
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
					//note: Address est en "network order", donc "AA.BB.CC.DD" => 0xDDCCBBAA !
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
					//REVIEW: y-a-t-il d'autres cas?
					return address.IsIPv6SiteLocal;
				}
				default:
				{
					// note: IPAddress actuellement ne retourne que l'un des deux enum ci-dessus, mais on se protégère contre le futur!
					return false;
				}
			}
		}

		/// <summary>Retourne la première IPv4 dans la liste, ou sinon la première IPv6</summary>
		/// <param name="list">Liste d'adresse IP candidates</param>
		/// <returns>Premiere adresse IPv4 trouvée ou null si aucune (ou que du IPv6)</returns>
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
			{ // en local on retourne la meme
				localAddress = remoteAddress;
				return true;
			}

			// Life Pro Tip: pour connaitre l'IP correspondant au bon network adapter capable de parler a une IP distance,
			// il suffit de faire un fake "Connect" sur un socket UDP et de consulter le endpoint local
			// => l'OS va faire le lookup dans la table de routage pour nous, et nous retourner la bonne valeur!
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
		
		/// <summary>Convertit une adresse IP en long (ex: "255.255.255.0" -> 0xFFFFFF00</summary>
		/// <param name="ip">Adresse IP</param>
		/// <returns>Masque binaire correspondant</returns>
		public static long IPToMask(string ip)
		{
			Contract.NotNull(ip);
			return IPToMask(IPAddress.Parse(ip));
		}

		/// <summary>Convertit une adresse IP en long (ex: "255.255.255.0" -> 0xFFFFFF00</summary>
		/// <param name="ip">Adresse IP</param>
		/// <returns>Masque binaire correspondant</returns>
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

		/// <summary>Détermine l'adresse IP de broadcast à partir d'une adresse IP et d'un masque de sous-réseau</summary>
		/// <param name="ip">Adresse IP du host (ex: 192.168.1.156)</param>
		/// <param name="subnet">Masque de sous réseau (ex: 255.255.255.0)</param>
		/// <returns>Adresse IP de broadcast correspondante (192.168.1.255)</returns>
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

		/// <summary>Teste si une adresse IP fait partie d'une plage.
		/// il y a plusieurs formats acceptés.
		/// ex: pour "entre 192.168.1.0 et 192.168.1.255")
		///     "192.168.1.*"
		///     "192.168.1.0-255"
		///     "192.168.1.0/255.255.255.0"
		///     "192.168.1.0/24"
		/// </summary>
		/// <param name="ip">Adresse IP à tester</param>
		/// <param name="range">Plage d'IP</param>
		/// <returns>'true' si l'IP est dans la plage (bornes incluses)</returns>
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
			{ // TODO: format "192.168.1.0-255" pour IPMatchRange

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
			// plage constitué d'une seule ip ?
			return (ip == range);
		}

		/// <summary>Retourne les bornes d'une plage d'adresse IP</summary>
		/// <param name="range">Plage IP ("192.168.1.0/24", "192.168.1.0|255.255.255.0", "192.168.1.1-192.168.1.255", "192.168.1")</param>
		/// <param name="first">Récupère la première adresse IP de la plage</param>
		/// <param name="last">Récupère la dernière adresse IP de la plage</param>
		/// <param name="include0">Si true, inclue "192.168.0.0" comme adresse valide</param>
		/// <remarks>Retourne une exception en cas d'erreur, dans lequel cas first et last sont fixés à null</remarks>
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

				var tmp = range.AsSpan(0, p);
				if (!IPAddress.TryParse(tmp, out var addr))
				{
					throw new FormatException($"Invalid IP range '{range}' : network address ({tmp.ToString()}) is invalid");
				}

				// on connait le subnet (/8, /16, /24, ..) et l'adresse
				// il faut qu'on en déduise l'adresse de début
				if (addr.AddressFamily == AddressFamily.InterNetworkV6) throw new NotSupportedException("IPv6 is not currently supported!");

				// l'adresse sera constitué des "subnet" premiers bits
				long bytes = addr.GetAddressBytes().AsSpan().ToUInt32BE();

				// check que l'IP est bien adressable si on est en /32
				if (subnet == 32 && ((bytes & 0xFF) == 0 || (bytes & 0xFF) == 255)) throw new FormatException($"Invalid IP range '{tmp.ToString()}': invalid /32 address! Should be .1 or .254");

				// masque qui va garder les "subnet" bits de poids fort
				long submask = ((1 << (32 - subnet)) - 1);  // 32 => 0x00000000, 24 => 0x000000FF, 16 => 0x0000FFFF, ...
				long mask = 0xFFFFFFFF ^ submask;           // 32 => 0xFFFFFFFF, 24 => 0xFFFFFF00, 16 => 0xFFFF0000, ...

				long start = (bytes & mask);
				long end = (bytes & mask) + submask;

				// "arrondi" les bords (.0 et .255) quand il ne sont pas des adresses valides)
				if ((start & 0xFF) == 0 && !include0)
				{
					//ie: 192.168.1.0/24, couvre 192.168.1.0 .. 192.168.1.255, bornes qui sont en général écartées
					// Par contre, 192.168.0.0/16, couvre 192.168.0.0 .. 192.168.255.255. Mais ici, 192.168.1.0 n'est PAS une borne, donc il est légal
					++start; // 0->1
				}
				else if ((start & 0xFF) == 255)
				{
					//note: on va par contre on va interdire .255 dans tous les cas, par précaution...
					--start; // 255->254
				}
				if ((end & 0xFF) == 0 && !include0)
				{
					//voire commentaire pour 'start' plus haut
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

				// "arrondi" les bords (.0 et .255 ne sont pas valides)
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

				// "arrondi" les bords (.0 et .255 ne sont pas valides)
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

		/// <summary>Retourne le nombre d'adresses entre (et incluant) deux bornes</summary>
		/// <param name="from">Adresse de départ</param>
		/// <param name="to">Adresse de destination</param>
		/// <param name="include0">true si on souhaite inclure les adresses en .0</param>
		/// <param name="include255">true si on souhaite inclure les adresses en .255</param>
		/// <returns>Nombre </returns>
		public static long GetHostCountBetween(IPAddress from, IPAddress to, bool include0 = false, bool include255 = false)
		{
			Contract.NotNull(from);
			Contract.NotNull(to);
			if (from.AddressFamily != to.AddressFamily) throw new ArgumentException("AddressFamily does not match", nameof(to));
			if (from.AddressFamily == AddressFamily.InterNetworkV6) throw new NotSupportedException("IPv6 not currently supported!!!");

			// cas le plus simple
			if (from.Equals(to))
			{ // un seul host dans la plage
				return 1;
			}

			// récupère les octets pour les comparaisons
			byte[] fromBytes = from.GetAddressBytes();
			byte[] toBytes = to.GetAddressBytes();
			Contract.Debug.Assert(fromBytes.Length == toBytes.Length && fromBytes.Length == 4, "IP address size does not match!");

			// compare le début de l'adresse (en excluant le dernier octet)
			int fromSubnet = (fromBytes[0] << 16) + (fromBytes[1] << 8) + fromBytes[2];
			int toSubnet = (toBytes[0] << 16) + (toBytes[1] << 8) + toBytes[2];

			if (toSubnet == fromSubnet)
			{ // même subnet /24, cas le plus simple:
				long res = toBytes[3] - fromBytes[3] + 1;
				if (res <= 0) throw new ArgumentException("The 'to' address should be higher than or equal to the 'from' address!", nameof(to));
				return res;
			}
			else if (toSubnet < fromSubnet)
			{ // to est inférieur à from !?
				throw new ArgumentException("The 'to' address should be higher than or equal to the 'from' address!", nameof(to));
			}
			else
			{ // subnets différents
			  // compte le nombre de plages /24 qu'il y a entre les deux en intégrant les adresses en 0
				var adressesBySubnet = 254 + (include0 ? 1 : 0) + (include255 ? 1 : 0);
				long res = ((toSubnet - fromSubnet - 1) * adressesBySubnet) + (255 - fromBytes[3] + (include255 ? 1 : 0)) + toBytes[3] + (include0 ? 1 : 0);
				return res;
			}
		}

		/// <summary>Ajoute un offset à une adresse IP</summary>
		/// <param name="address">Adresse de base (ex: 192.168.1.23)</param>
		/// <param name="offset">Offset (ex: 42)</param>
		/// <returns>Nouvelle adresse (ex: 192.168.1.65)</returns>
		/// <exception cref="ArgumentException">Si <paramref name="address"/> n'est pas d'un type supporté (IPv4)</exception>
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

		/// <summary>Convertit une adresse MAC binaire en représentation string</summary>
		/// <param name="mac">Tableau de 6 octets contenant une adresse MAC</param>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(byte[] mac)
		{
			Contract.NotNull(mac);
			return MACAddressToString(mac, 0, mac.Length);
		}

		/// <summary>Convertit une adresse MAC binaire en représentation string</summary>
		/// <param name="mac">Buffer de 6 octets contenant une adresse MAC</param>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static string MACAddressToString(Slice mac)
		{
			return MACAddressToString(mac.Span);
		}

		/// <summary>Convertit une adresse MAC binaire en représentation string</summary>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(byte[] mac, int offset, int count)
		{
			Contract.NotNull(mac);
			return MACAddressToString(mac.AsSpan(offset, count));
		}

		/// <summary>Convertit une adresse MAC binaire en représentation string</summary>
		/// <returns>"00-11-22-33-44-55"</returns>
		/// <version>1.1.0.7</version>
		public static string MACAddressToString(ReadOnlySpan<byte> mac)
		{
			// note: normalement ca fait 6 bytes de long, mais la structure MIB_IPNETROW retourne parfois un buffer de 8 bytes (2 derniers a zero)
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
					{ // ca ne sert a rien de continuer plus!
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
