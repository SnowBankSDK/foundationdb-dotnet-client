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

namespace SnowBank.Testing.Framework
{
	using System.Security.Cryptography;
	using System.Security.Cryptography.X509Certificates;
	using SnowBank.Networking.PacketCapture;

	/// <summary>Base class for all tests that simulate a distributed environment</summary>
	public abstract class DistributedTest : SimpleTest
	{

		private DistributedTestContext? CurrentContext { get; set; }

		/// <summary>Configures a new test environment for the current test method</summary>
		[DebuggerNonUserCode]
		public async Task<DistributedTestContext> MakeItSo(Action<IDistributedTestEnvironmentBuilder> configure)
		{
			var ct = this.Cancellation;
			ct.ThrowIfCancellationRequested();

			var test = TestContext.CurrentContext.Test;
			var logStdOut = MustOutputLogsOnConsole ? TestContext.Out : TestContext.Progress;
			var logStdErr = MustOutputLogsOnConsole ? TestContext.Error : TestContext.Progress;

			var builder = new DistributedTestEnvironmentBuilder(this, $"{test.FullName}({test.ID})", logStdOut, logStdErr, ct);
			configure(builder);

			// let a derived test base register library-specific instrumentation (e.g. timeline event rules) on the environment,
			// so individual tests do not have to - and the generic framework stays free of any knowledge of those libraries.
			OnConfigureEnvironment(builder);

			// before take-off checks
			Assume.That(builder.TestSubject, Is.Not.Null, "Test environment subject is missing");
			Assume.That(builder.Clock, Is.Not.Null, "Test environment clock is missing");
			Assume.That(builder.Components, Is.Not.Null, "Test environment components list is missing");
			Assume.That(builder.LogOutput, Is.Not.Null, "Test environment log output is missing");
			Assume.That(builder.LogOutputError, Is.Not.Null, "Test environment error log output is missing");

			var context = new DistributedTestContext(builder);
			this.CurrentContext = context;

			using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
			{
				await context.Setup(cts.Token);
			}

			return context;
		}

		/// <summary>Hook for a derived test base class to register library-specific instrumentation on the test environment
		/// (e.g. <see cref="IDistributedTestEnvironmentBuilder.RegisterTimelineEvent"/> mappings), once for all of its tests.</summary>
		/// <remarks>Called by <see cref="MakeItSo"/> after the test's own <c>configure</c> callback, before the environment starts.</remarks>
		protected virtual void OnConfigureEnvironment(IDistributedTestEnvironmentBuilder builder)
		{
			// nothing by default
		}

		/// <summary>Runs part of the test under a root <see cref="Activity"/></summary>
		/// <typeparam name="T">Type of the result of the test handler</typeparam>
		/// <param name="operationName">Name of the operation (used as the name of the Activity)</param>
		/// <param name="handler">Handler that will run under a dedicated Activity.</param>
		/// <returns>Result of the handler</returns>
		protected async Task<T> RunWithActivity<T>(string operationName, Func<Task<T>> handler)
		{
			using var activity = new Activity(operationName);
			activity.IsAllDataRequested = true;
			activity.Start();

			return await handler();
		}

		/// <summary>Dumps all received network packets received so far to the console</summary>
		protected void LogNetworkPackets(Func<CapturedPacket, bool>? filter = null)
		{
			if (this.CurrentContext == null) throw new InvalidOperationException("Test has already stopped running");
			Log(DumpNetworkPackets(this.CurrentContext, filter));
		}

		private string? DumpNetworkPackets(DistributedTestContext context, Func<CapturedPacket, bool>? filter = null)
		{
			var packets = context.GetNetworkPackets();
			if (packets.Count == 0) return null;
			var sb = new StringBuilder();
			sb.AppendLine("# ======================================================================================================================");
			sb.AppendLineInvariant($"# Dumping network packets: {packets.Count}");
			int i = 1;
			foreach (var packet in packets)
			{
				sb.AppendLineInvariant($"# --- {i:N0} / {packets.Count} --- T+{ElapsedSinceTestStart(packet.Metadata.StartedAt).TotalSeconds:N3}: {packet.Id} [{packet.Metadata.ActorId} => {packet.Metadata.Connection.RemoteHost}:{packet.Metadata.Connection.RemotePort}] <{packet.Metadata.TraceId}>");
				sb.AppendLineInvariant($"# {packet}");
				sb.AppendLine(packet.GetBasicDump(includeBody: true));
				++i;
			}
			sb.AppendLine("# ======================================================================================================================");
			return sb.ToString();
		}

		protected sealed override void OnAfterEachTest() //REVIEW: do we need an async version ?
		{
			var testContext = TestContext.CurrentContext; // context NUnit
			var context = this.CurrentContext; // context local

			// the main cancellation token for the test is already canceled, but we will allow up to 30 sec for the teardown!

			if (context != null)
			{
				if (testContext.Result.FailCount > 0)
				{
					context.Timeline.Record(new ()
					{
						Source = "TEST",
						Start = context.RealClock.GetCurrentInstant(),
						Category = "TEST",
						Label = $"FAILED with {testContext.Result.FailCount} failure(s) ({testContext.AssertCount} assertions)",
						Failed = true,
						//TODO: details!
					});
				}
				else
				{
					context.Timeline.Record(new ()
					{
						Source = "TEST",
						Start = context.RealClock.GetCurrentInstant(),
						Category = "TEST",
						Label = $"PASS ({testContext.AssertCount} assertions)",
						//TODO: details!
					});
				}

				// invoke the library-registered completion hooks while the hosts are still up (a typical hook dumps
				// library-specific state on failure); a throwing hook is reported but can never mask the test outcome
				if (context.TestCompletedHooks.Count > 0)
				{
					var outcome = new DistributedTestOutcome(testContext.Result.FailCount > 0, testContext.Result.FailCount, testContext.AssertCount);
					using var hookCts = new CancellationTokenSource(5_000);
					foreach (var hook in context.TestCompletedHooks)
					{
						try
						{
							hook(context, outcome, hookCts.Token).GetAwaiter().GetResult(); //BUGBUG: await! (same constraint as TearDown below)
						}
						catch (Exception e)
						{
							context.LogOutputError.Write($"# test-completed hook failed: [{e.GetType().Name}] {e.Message}");
						}
					}
				}

				this.CurrentContext = null;
				using (var cts = new CancellationTokenSource(5_000))
				{
					try
					{
						context.TearDown(cts.Token).GetAwaiter().GetResult(); //BUGBUG: await!
					}
					catch (Exception e)
					{
						throw new AssertionException("Failed to teardown test environment", e);
					}
				}

				// Dump the timeline of events. In stream mode a passing test already showed every event live, so the
				// consolidated journal is redundant and skipped - but a FAILING test always gets it (the post-mortem is
				// exactly what is needed when something breaks, in every mode).
				bool failed = TestContext.CurrentContext.Result.FailCount > 0;
				if (LogVerbosity != TestLogVerbosity.Stream || failed)
				{
					var sb = new StringBuilder();
					context.Timeline.DumpReport(sb, context.Name, context.StartedAt, context.CompletedAt);
					context.LogOutput.Write(sb.ToString());
				}

				// if the test fails, we also dump any information that may be useful for troubleshooting!
				if (failed)
				{
					var packetsDump = DumpNetworkPackets(context);
					if (packetsDump != null)
					{
						context.LogOutputError.Write(packetsDump);
					}
				}

			}

			base.OnAfterEachTest();
		}

		protected sealed override Task OnWaitOperationCompleted(string operation, string conditionExpression, bool success, Exception? error, Instant startedAt, Instant endedAt)
		{
			var timeline = this.CurrentContext?.Timeline;
			if (timeline != null)
			{
				int off = conditionExpression.StartsWith("() => ", StringComparison.Ordinal) ? 6 : 0;

				timeline.Record(new Timeline.Datum()
				{
					Start = startedAt,
					End = endedAt,
					Source = "TEST",
					Category = "TEST",
					Label = $"{operation}{(success ? "" : " FAILED")}: {conditionExpression[off..]}{(error != null ? $" => [{error.GetType().Name}] {error.Message}" : null)}",
					Failed = !success,
					//TODO: details?
				});
			}
			return Task.CompletedTask;
		}

		/// <summary>Logs a test event to the test timeline</summary>
		/// <param name="message">Message attached to the event</param>
		protected void LogEvent(string message)
		{
			Log(message);
			var timeline = this.CurrentContext?.Timeline;
			if (timeline != null)
			{
				var now = this.Clock.GetCurrentInstant();
				timeline.Record(new()
				{
					Start = now,
					End = now,
					Source = "TEST",
					Category = "TEST",
					Label = "### " + message,
					Failed = false,
					//TODO: details?
				});
			}
		}

		#region Cryptography...

		/// <summary>Generates a new RSA key, and returns both the public and private version</summary>
		/// <param name="keySizeInBits">Size of the RSA key in bits (defaults to 2048)</param>
		protected (RSA Public, RSA Private) CreateRsaPublicPrivateKeyPair(int keySizeInBits = 2048)
		{
			var rsaPrivate = RSA.Create(keySizeInBits);

			var rsaPublic = RSA.Create();
			rsaPublic.ImportParameters(rsaPrivate.ExportParameters(includePrivateParameters: false));

			return (
				rsaPublic,
				rsaPrivate
			);
		}

		/// <summary>Creates a new X.509 certificate that can be used for Digital Signature</summary>
		/// <param name="subjectName">Subject name of the certificate (ex: CN=client.test.local)</param>
		/// <param name="issuerCertificate">Issue certificate, or <c>null</c> for self-signed</param>
		protected X509Certificate2 CreateDigitalSignatureCertificate(string subjectName, X509Certificate2? issuerCertificate = null)
		{
			Contract.NotNullOrEmpty(subjectName);

			using var rsa = RSA.Create(2048);

			var dn = new X500DistinguishedName(subjectName);
			var request = new CertificateRequest(dn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
			request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([ new("1.3.6.1.5.5.7.3.2") ], false)); // a.k.a "clientAuth"

			X509Certificate2 certificate;
			if (issuerCertificate == null)
			{ // self-signed
				certificate = request.CreateSelfSigned(
					new(DateTime.UtcNow.AddDays(-1)),
					new(DateTime.UtcNow.AddDays(3650))
				);
			}
			else
			{ // issued by a CA
				var serialNumber = new byte[8];
				RandomNumberGenerator.Fill(serialNumber);
				serialNumber[0] &= 0x7F; // remove sign bit

				certificate = request.Create(
					issuerCertificate,
					new(DateTime.UtcNow.AddDays(-1)),
					new(issuerCertificate.NotAfter),
					serialNumber
				);
			}

			if (!certificate.HasPrivateKey)
			{
				certificate = certificate.CopyWithPrivateKey(rsa);
			}

			return certificate;
		}

		#endregion

	}

}
