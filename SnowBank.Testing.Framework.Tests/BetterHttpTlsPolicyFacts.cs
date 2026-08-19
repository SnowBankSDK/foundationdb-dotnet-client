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
	using System.Net.Security;
	using System.Security.Cryptography;
	using System.Security.Cryptography.X509Certificates;
	using NUnit.Framework;
	using SnowBank.Networking.Http;

	/// <summary>Pins the ergonomic TLS trust ladder on <see cref="BetterHttpClientOptions"/>: self-signed certificates are a
	/// fact of life in dev/test/internal-LAN deployments (no public X509 for internal servers, local-CA distribution is its
	/// own pain), so the client options offer graded helpers instead of hand-written callbacks - trust EXTRA roots with full
	/// chain validation (the private-CA / pinned-self-signed story), or forgive chain-trust errors while still enforcing the
	/// host-name match. Validation is never silently disabled: accept-anything remains a separate, loudly-named method.</summary>
	[TestFixture]
	public class BetterHttpTlsPolicyFacts : SimpleTest
	{

		/// <summary>Creates a self-signed certificate (its own root)</summary>
		private static X509Certificate2 CreateSelfSigned(string cn = "CN=edge.lan.simulated")
		{
			using var key = RSA.Create(2048);
			var request = new CertificateRequest(cn, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
		}

		/// <summary>Creates a private CA and a leaf certificate signed by it</summary>
		private static (X509Certificate2 Authority, X509Certificate2 Leaf) CreatePrivateCaWithLeaf()
		{
			using var caKey = RSA.Create(2048);
			var caRequest = new CertificateRequest("CN=Acme Site CA", caKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
			var authority = caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddYears(10));

			using var leafKey = RSA.Create(2048);
			var leafRequest = new CertificateRequest("CN=api.acme.internal", leafKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
			var leaf = leafRequest.Create(authority, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), [ 1, 2, 3, 4 ]);
			return (authority, leaf);
		}

		/// <summary>Builds a chain the way SslStream would present it (against system trust; expected to fail for private roots, but populated)</summary>
		private static X509Chain BuildPresentedChain(X509Certificate2 leaf, X509Certificate2? intermediate = null)
		{
			var chain = new X509Chain();
			chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
			if (intermediate is not null) chain.ChainPolicy.ExtraStore.Add(intermediate);
			_ = chain.Build(leaf); // a private root does not build against system trust: we only need the elements populated
			return chain;
		}

		[Test]
		public void Test_TrustServerCertificates_Accepts_Chain_Rooted_In_The_Given_CA()
		{
			var (authority, leaf) = CreatePrivateCaWithLeaf();
			var options = new BetterHttpClientOptions().TrustServerCertificates(authority);
			var callback = options.ServerCertificateCustomValidationCallback!;

			using var presented = BuildPresentedChain(leaf, authority);
			Assert.That(callback(leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors), Is.True,
				"a leaf signed by the trusted private CA must be accepted despite the system-trust chain error");
		}

		[Test]
		public void Test_TrustServerCertificates_Accepts_The_Pinned_Self_Signed_Leaf()
		{
			var pinned = CreateSelfSigned();
			var options = new BetterHttpClientOptions().TrustServerCertificates(pinned);
			var callback = options.ServerCertificateCustomValidationCallback!;

			using var presented = BuildPresentedChain(pinned);
			Assert.That(callback(pinned, presented, SslPolicyErrors.RemoteCertificateChainErrors), Is.True,
				"a self-signed certificate pinned as its own trust root must be accepted");
		}

		[Test]
		public void Test_TrustServerCertificates_Rejects_Unrelated_Certificates()
		{
			var (authority, _) = CreatePrivateCaWithLeaf();
			var stranger = CreateSelfSigned("CN=stranger.lan.simulated");
			var options = new BetterHttpClientOptions().TrustServerCertificates(authority);
			var callback = options.ServerCertificateCustomValidationCallback!;

			using var presented = BuildPresentedChain(stranger);
			Assert.That(callback(stranger, presented, SslPolicyErrors.RemoteCertificateChainErrors), Is.False,
				"a certificate not rooted in the trusted CA must be rejected");
		}

		[Test]
		public void Test_TrustServerCertificates_Keeps_System_Trust()
		{
			// SslPolicyErrors.None means the platform already validated the chain against the system store: publicly-trusted
			// certificates - and the OS-trusted ASP.NET dev certificate that Aspire distributes - must keep working untouched.
			var (authority, _) = CreatePrivateCaWithLeaf();
			var someCert = CreateSelfSigned();
			var options = new BetterHttpClientOptions().TrustServerCertificates(authority);
			var callback = options.ServerCertificateCustomValidationCallback!;

			Assert.That(callback(someCert, null, SslPolicyErrors.None), Is.True,
				"a certificate the system already trusts must remain accepted");
		}

		[Test]
		public void Test_TrustServerCertificates_Never_Forgives_A_Name_Mismatch()
		{
			var (authority, leaf) = CreatePrivateCaWithLeaf();
			var options = new BetterHttpClientOptions().TrustServerCertificates(authority);
			var callback = options.ServerCertificateCustomValidationCallback!;

			using var presented = BuildPresentedChain(leaf, authority);
			Assert.That(callback(leaf, presented, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch), Is.False,
				"extra trust roots must not forgive a host-name mismatch");
			Assert.That(callback(null, null, SslPolicyErrors.RemoteCertificateNotAvailable), Is.False,
				"a missing certificate must never be accepted");
		}

		[Test]
		public void Test_AcceptSelfSignedServerCertificates_Forgives_Chain_Errors_Only()
		{
			var cert = CreateSelfSigned();
			var options = new BetterHttpClientOptions().AcceptSelfSignedServerCertificates();
			var callback = options.ServerCertificateCustomValidationCallback!;

			Assert.That(callback(cert, null, SslPolicyErrors.None), Is.True, "a fully valid certificate is accepted");
			Assert.That(callback(cert, null, SslPolicyErrors.RemoteCertificateChainErrors), Is.True,
				"chain-trust errors (self-signed, private root) are forgiven");
			Assert.That(callback(cert, null, SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch), Is.False,
				"a host-name mismatch is never forgiven (that is what accept-ANY would do, and it stays a separate, loud opt-in)");
			Assert.That(callback(null, null, SslPolicyErrors.RemoteCertificateNotAvailable), Is.False,
				"a missing certificate is never accepted");
		}

	}

}
