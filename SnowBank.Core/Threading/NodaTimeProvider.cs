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

namespace SnowBank.Threading
{
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.DependencyInjection.Extensions;
	using NodaTime;

	/// <summary>A single source of time exposed through BOTH the BCL <see cref="TimeProvider"/> facade (timestamps, timers, delays, timeouts) and the NodaTime <see cref="IClock"/> facade (<see cref="Instant"/>s)</summary>
	/// <remarks>
	/// <para>The two facades always read the SAME underlying provider, so code stamping Instants via <see cref="IClock"/>
	/// and code scheduling via <see cref="TimeProvider"/> can never diverge - the classic bug where a test advances a fake
	/// provider (timers fire at virtual T+30s) while documents keep being stamped with the real wall clock.</para>
	/// <para>Production registers <see cref="System"/> (wrapping <see cref="TimeProvider.System"/>); a timing-sensitive
	/// test wraps a <c>FakeTimeProvider</c> and advances it: timers, timeouts AND Instants move together.</para>
	/// </remarks>
	[PublicAPI]
	public sealed class NodaTimeProvider : TimeProvider, IClock
	{

		/// <summary>The real system clock, exposed through both facades</summary>
		public static readonly NodaTimeProvider System = new(TimeProvider.System);

		/// <summary>Wraps an existing provider (the system provider in production, a fake advanceable provider in tests)</summary>
		public NodaTimeProvider(TimeProvider inner)
		{
			Contract.NotNull(inner);
			this.Inner = inner;
		}

		/// <summary>The single underlying source of time that both facades read</summary>
		public TimeProvider Inner { get; }

		#region IClock (NodaTime)...

		/// <inheritdoc />
		public Instant GetCurrentInstant() => Instant.FromDateTimeOffset(this.Inner.GetUtcNow());

		#endregion

		#region TimeProvider (BCL)...

		// every member delegates to Inner: timers and timestamps included, so Task.Delay(..., this),
		// new CancellationTokenSource(timeout, this) and PeriodicTimer(period, this) all follow the inner clock

		/// <inheritdoc />
		public override DateTimeOffset GetUtcNow() => this.Inner.GetUtcNow();

		/// <inheritdoc />
		public override TimeZoneInfo LocalTimeZone => this.Inner.LocalTimeZone;

		/// <inheritdoc />
		public override long GetTimestamp() => this.Inner.GetTimestamp();

		/// <inheritdoc />
		public override long TimestampFrequency => this.Inner.TimestampFrequency;

		/// <inheritdoc />
		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => this.Inner.CreateTimer(callback, state, dueTime, period);

		#endregion

		/// <inheritdoc />
		public override string ToString() => $"NodaTimeProvider({(ReferenceEquals(this.Inner, TimeProvider.System) ? "System" : this.Inner.ToString())})";

	}

	/// <summary>Extensions for consuming NodaTime instants from a plain <see cref="TimeProvider"/></summary>
	[PublicAPI]
	public static class NodaTimeProviderExtensions
	{

		/// <summary>Reads the current instant from this provider (the NodaTime equivalent of <see cref="TimeProvider.GetUtcNow"/>)</summary>
		/// <remarks>Lets scheduling code inject ONLY a <see cref="TimeProvider"/> and still produce <see cref="Instant"/>s, instead of injecting both a provider and an <see cref="IClock"/>.</remarks>
		public static Instant GetCurrentInstant(this TimeProvider provider) => provider is IClock clock ? clock.GetCurrentInstant() : Instant.FromDateTimeOffset(provider.GetUtcNow());

		/// <summary>Registers the system clock as a <see cref="NodaTimeProvider"/> singleton, aliased as both <see cref="TimeProvider"/> and <see cref="IClock"/></summary>
		/// <param name="services">Service collection to register into</param>
		/// <param name="source">Underlying source of time; <c>null</c> uses the real system clock (a test passes its fake advanceable provider)</param>
		/// <remarks>Uses TryAdd semantics: a test harness that registered its own (fake-backed) clock FIRST wins, and this call becomes a no-op - so libraries and startup code can call it unconditionally.</remarks>
		public static IServiceCollection AddSystemClock(this IServiceCollection services, TimeProvider? source = null)
		{
			Contract.NotNull(services);
			services.TryAddSingleton<NodaTimeProvider>(source is null ? NodaTimeProvider.System : new NodaTimeProvider(source));
			services.TryAddSingleton<TimeProvider>(sp => sp.GetRequiredService<NodaTimeProvider>());
			services.TryAddSingleton<IClock>(sp => sp.GetRequiredService<NodaTimeProvider>());
			return services;
		}

	}

}
