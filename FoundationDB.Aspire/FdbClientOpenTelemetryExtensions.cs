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

namespace OpenTelemetry.Trace
{
	using FoundationDB.Client;
	using OpenTelemetry.Metrics;

	/// <summary>Helper methods for configuring OpenTelemetry with FoundationDB</summary>
	public static class FdbClientOpenTelemetryExtensions
	{

		/// <summary>Adds the FoundationDB source to the list of subscribed sources.</summary>
		public static TracerProviderBuilder AddFoundationDbInstrumentation(this TracerProviderBuilder provider)
		{
			provider.AddSource(FdbClientInstrumentation.ActivityName);
			return provider;
		}

		/// <summary>Adds the FoundationDB source to the list of subscribed meters.</summary>
		public static MeterProviderBuilder AddFoundationDbInstrumentation(this MeterProviderBuilder provider)
		{
			provider.AddMeter(FdbClientInstrumentation.MeterName)
				.AddView(
					"db.fdb.client.transactions.duration",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 0.001, 0.0025, 0.005, 0.0075, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 ], })
				.AddView(
					"db.fdb.client.operations.duration",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 0.001, 0.0025, 0.005, 0.0075, 0.01, 0.025, 0.05, 0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10 ], })
				.AddView(
					"db.fdb.client.operations.size",
					new ExplicitBucketHistogramConfiguration { Boundaries = [ 0, 10, 20, 50, 100, 200, 500, 1_000, 2_000, 5_000, 10_000, 20_000, 50_000, 100_000, 200_000, 500_000, 1_000_000, 2_000_000, 5_000_000, 10_000_000 ] })
				;
			return provider;
		}

	}
}
