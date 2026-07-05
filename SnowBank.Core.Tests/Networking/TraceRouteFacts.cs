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

namespace SnowBank.Networking.Tests
{
	using System.Net;
	using System.Net.NetworkInformation;
	using System.Net.Sockets;

	[TestFixture]
	[Category("Core-SDK")]
	[Parallelizable(ParallelScope.All)]
	public class TraceRouteFacts : SimpleTest
	{

		[Test]
		public async Task Test_Can_Traceroute_Internet()
		{
			// ce test a besoin de faire des tests avec des "vrai" IP, et donc le runner doit avoir accès au lan et a internet!
			var target = IPAddress.Parse("8.8.8.8"); // Google DNS

			Log($"# Traceroute: {target}");
			var (res, elapsed) = await Time(() => IPAddressHelpers.TracerouteAsync(target, 16, TimeSpan.FromSeconds(2), this.Cancellation));
			Log($"> [{elapsed.TotalSeconds:N3}s]");
			Dump(res);
			Assert.That(res.Status, Is.EqualTo(IPStatus.Success), $"Failed to traceroute {target}");
			Assert.That(res.Hops, Has.Count.GreaterThan(3), $"Should need at least 3 hops for {target}");
			Assert.That(res.Hops.Select(x => x.Address), Is.All.Not.Null, "Hops[*].Address");
			//note: on ne peut pas facilement tester "Distance" car parfois il y a des nodes qui ne répondent pas.
			// => on juste vérifier qu'ils sont positifs, et tous différents
			Assert.That(res.Hops.Select(x => x.Distance), Is.Unique.And.All.GreaterThan(0), "Hops[*].Distance");
			Assert.That(res.Hops.Select(x => x.Rtt), Is.All.GreaterThan(TimeSpan.Zero), "Hops[*].Rtt");
			//note: seulement les premiers hops sont "private", les derniers sont "public"!

			Assert.That(res.Hops[^1].Address, Is.EqualTo(target), "Last address should be the target");
			Assert.That(res.Hops.Take(res.Hops.Count - 1).Select(x => x.Address), Is.All.Not.EqualTo(target), "All except the last should be different than the target!");

			Assert.That(res.Hops[^1].Status, Is.EqualTo(IPStatus.Success), "Last status should be Success");
			Assert.That(res.Hops.Take(res.Hops.Count - 1).Select(x => x.Status), Is.All.Not.EqualTo(IPStatus.Success), "All except the last should be TtlExpired (or Timeout)!");
		}

		[Test]
		[Ignore("This test requires an actual LAN to run")]
		public async Task Test_Can_Traceroute_LAN_Host()
		{
			// ce test a besoin de faire des tests avec des "vrai" IP, et donc le runner doit avoir accès au lan et a internet!
			var target = IPAddress.Parse("10.10.0.1"); // DC01

			Log($"# Traceroute: {target}");
			var (res, elapsed) = await Time(() => IPAddressHelpers.TracerouteAsync(target, 16, TimeSpan.FromSeconds(2), this.Cancellation));
			Log($"> [{elapsed.TotalSeconds:N3}s]");
			Dump(res);
			Assert.That(res.Status, Is.EqualTo(IPStatus.Success), $"Failed to traceroute {target}");
			Assert.That(res.Hops, Has.Count.EqualTo(1), $"Should only need 1 hop for {target}");
			Assert.That(res.Hops[0].Address, Is.EqualTo(target), "Hops[0].Address");
			Assert.That(res.Hops[0].Distance, Is.EqualTo(1), "Hops[0].Distance");
			Assert.That(res.Hops[0].Private, Is.True, "Hops[0].Private");
			Assert.That(res.Hops[0].Rtt, Is.GreaterThan(TimeSpan.Zero), "Hops[0].Rtt");
		}

		[Test]
		public async Task Test_Can_Traceroute_Self()
		{
			// on va tracertoute l'ip public du host local
#if NET6_0_OR_GREATER
			var target = IPAddressHelpers.GetPreferredAddress(await Dns.GetHostAddressesAsync(Environment.MachineName, AddressFamily.InterNetwork, this.Cancellation));
#else
			// the (host, family, ct) overload is .NET 6+: resolve everything and filter here
			var target = IPAddressHelpers.GetPreferredAddress((await Dns.GetHostAddressesAsync(Environment.MachineName)).Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray());
#endif
			Assert.That(target, Is.Not.Null);

			Log($"# Traceroute: {target}");
			var (res, elapsed) = await Time(() => IPAddressHelpers.TracerouteAsync(target!, 16, TimeSpan.FromSeconds(2), this.Cancellation));
			Log($"> [{elapsed.TotalSeconds:N3}s]");
			Dump(res);
			Assert.That(res.Status, Is.EqualTo(IPStatus.Success), $"Traceroute to self should succeed: {target}");
			Assert.That(res.Hops, Has.Count.EqualTo(1), $"Only one hop {target}");
		}

		[Test]
		[Ignore("This test requires an actual LAN to run")]
		public async Task Test_Can_Traceroute_LAN_Host_Unreachable()
		{
			// ce test a besoin de faire des tests avec des "vrai" IP, et donc le runner doit avoir accès au lan et a internet!

			var target = IPAddress.Parse("10.10.254.253"); // y a interet a ce qu'elle existe pas!

			Log($"# Traceroute: {target}");
			var (res, elapsed) = await Time(() => IPAddressHelpers.TracerouteAsync(target, 16, TimeSpan.FromSeconds(2), this.Cancellation));
			Log($"> [{elapsed.TotalSeconds:N3}s]");
			Dump(res);
			//note: suivant le coefficient de marée, on a soit DestinationHostUnreachable, soit TimedOut.
			// => les conditions exactes pouir avoir DestinationHostUnreachable sont assez difficiles a reproduire exactement, surtout en CI!
			Assert.That(res.Status, Is.EqualTo(IPStatus.DestinationHostUnreachable).Or.EqualTo(IPStatus.TimedOut), $"Traceroute should have failed: {target}");
			Assert.That(res.Hops, Has.Count.EqualTo(1), $"Only one hop {target}");
		}

		[Test]
		[Ignore("This test requires an actual LAN to run")]
		public async Task Test_Can_Traceroute_LAN_Host_Timeout()
		{
			// ce test a besoin de faire des tests avec des "vrai" IP, et donc le runner doit avoir accès au lan et a internet!

			// IP qui n'est pas dans notre /16, mais qui est quand meme dans le coin!
			var target = IPAddress.Parse("10.254.253.252"); // y a interet a ce qu'elle existe pas!

			Log($"# Traceroute: {target}");
			var (res, elapsed) = await Time(() => IPAddressHelpers.TracerouteAsync(target, 16, TimeSpan.FromSeconds(2), this.Cancellation));
			Log($"> [{elapsed.TotalSeconds:N3}s]");
			Dump(res);
			Assert.That(res.Status, Is.EqualTo(IPStatus.TimedOut), $"Tracroute should have failed: {target}");
			Assert.That(res.Hops, Has.Count.GreaterThan(1), $"Should be at least two hops: {target}");
		}

	}

}
