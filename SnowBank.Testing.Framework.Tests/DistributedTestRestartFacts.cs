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

namespace SnowBank.Testing.Framework.Tests
{
	using Microsoft.Extensions.DependencyInjection;
	using NUnit.Framework;

	/// <summary>Self-tests for the distributed test framework's node-restart capability:
	/// a restart yields a brand new DI container (fresh singletons),
	/// correctly disposes the previous incarnation's <see cref="IDisposable"/> AND <see cref="IAsyncDisposable"/> services,
	/// preserves the <c>Data</c> handoff bag and the host identity, and reports the right <see cref="HostStatus"/>.</summary>
	[TestFixture]
	public class DistributedTestRestartFacts : DistributedTest
	{

		/// <summary>Fake singleton that proves each incarnation gets a fresh DI container (unique <see cref="Id"/>)
		/// and that the container disposes it (<see cref="Disposed"/>).</summary>
		private sealed class FakeSyncService : IDisposable
		{
			public Guid Id { get; } = Guid.NewGuid();
			public bool Disposed { get; private set; }
			public void Dispose() => this.Disposed = true;
		}

		/// <summary>Same as <see cref="FakeSyncService"/> but exercises the async-disposal path.</summary>
		private sealed class FakeAsyncService : IAsyncDisposable
		{
			public Guid Id { get; } = Guid.NewGuid();
			public bool Disposed { get; private set; }
			public ValueTask DisposeAsync() { this.Disposed = true; return default; }
		}

		[Test]
		public async Task Test_Restart_Gives_A_Fresh_DI_And_Disposes_The_Previous_Incarnation()
		{
			// Factory-created singletons are owned (and disposed) by the DI container; we capture every created instance so we
			// can prove (a) each incarnation builds a NEW container (different Guids), and (b) the previous container disposed
			// both the IDisposable and the IAsyncDisposable singleton when the host stopped/restarted.
			var syncInstances = new List<FakeSyncService>();
			var asyncInstances = new List<FakeAsyncService>();

			var context = await MakeItSo(env => env.AddSimpleLan(lan =>
			{
				lan.WithMinimalWebHost("NODE", host =>
				{
					host.ConfigureServices(builder =>
					{
						builder.Services.AddSingleton<FakeSyncService>(_ => { var s = new FakeSyncService(); syncInstances.Add(s); return s; });
						builder.Services.AddSingleton<FakeAsyncService>(_ => { var s = new FakeAsyncService(); asyncInstances.Add(s); return s; });
					});
					host.ConfigureApplication(app => { /* NOP */ });
				});
			}));

			var node = context.GetWebHost("NODE");

			// (1) first incarnation: resolve the singletons (forcing their creation) and capture their identities
			var sync0 = node.GetRequiredService<FakeSyncService>();
			var async0 = node.GetRequiredService<FakeAsyncService>();
			Assert.That(sync0.Disposed, Is.False);
			Assert.That(async0.Disposed, Is.False);
			Assert.That(context.GetHostStatus("NODE"), Is.EqualTo(HostStatus.Started));
			Assert.That(node.RestartCount, Is.EqualTo(0), "the initial start is RestartCount 0");
			Assert.That(node.IsFirstStart, Is.True);

			// the Data handoff bag must survive the restart
			node.Data["token"] = "abc-123";

			// (2) restart -> the previous incarnation's container is disposed (both IDisposable and IAsyncDisposable singletons)
			await context.RestartHost("NODE", this.Cancellation);
			Assert.That(sync0.Disposed, Is.True, "the previous incarnation's IDisposable singleton must be disposed on restart");
			Assert.That(async0.Disposed, Is.True, "the previous incarnation's IAsyncDisposable singleton must be disposed on restart");

			// (3) fresh incarnation: a brand new DI container -> new singleton instances with different identities
			var sync1 = node.GetRequiredService<FakeSyncService>();
			var async1 = node.GetRequiredService<FakeAsyncService>();
			Assert.That(sync1.Id, Is.Not.EqualTo(sync0.Id), "a restart must build a brand new DI container (no stray service from the previous incarnation)");
			Assert.That(async1.Id, Is.Not.EqualTo(async0.Id));
			Assert.That(sync1.Disposed, Is.False);
			Assert.That(async1.Disposed, Is.False);
			Assert.That(context.GetHostStatus("NODE"), Is.EqualTo(HostStatus.Started));
			Assert.That(node.RestartCount, Is.EqualTo(1), "RestartCount is bumped on restart");
			Assert.That(node.IsRestart, Is.True);

			// the host kept its identity and the Data bag across the restart
			Assert.That(node.Data["token"], Is.EqualTo("abc-123"), "the Data handoff bag must persist across a restart");

			// (4) stop (without restarting) -> the CURRENT incarnation is disposed immediately; nothing is left to shut down at teardown
			await context.StopHost("NODE", this.Cancellation);
			Assert.That(context.GetHostStatus("NODE"), Is.EqualTo(HostStatus.Stopped));
			Assert.That(sync1.Disposed, Is.True, "StopHost must dispose the current incarnation's IDisposable singleton");
			Assert.That(async1.Disposed, Is.True, "StopHost must dispose the current incarnation's IAsyncDisposable singleton");

			// at teardown, the framework will call Stop() again on this already-stopped host: it must be a no-op (no double-dispose).
			// The test simply passing (no teardown error) proves that contract.
		}

	}

}
