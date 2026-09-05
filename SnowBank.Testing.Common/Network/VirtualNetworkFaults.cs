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

#if NET8_0_OR_GREATER

namespace SnowBank.Networking
{

	/// <summary>Describes HOW a cut virtual link fails, as observed by the side attempting to use it</summary>
	/// <remarks>
	/// <para>Some faults are IMMEDIATE (a new connection attempt fails right away, with the corresponding socket shape),
	/// while <see cref="VirtualNetworkFaultKind.Blackhole"/> is DEFERRED: the caller first observes SILENCE (a connect
	/// or read/write that simply does not complete), and only later - when the configured budget elapses ON THE
	/// TOPOLOGY'S <see cref="VirtualNetworkTopology.Time"/> - an exception "pops up" in its own call stack. The fault
	/// injector never cancels anything to produce a timeout: the timeout budget belongs to the VICTIM, and the
	/// exception is manufactured lazily by a task parked on the (virtualizable) time source.</para>
	/// </remarks>
	public enum VirtualNetworkFaultKind
	{
		/// <summary>Hard cut: new connections are reset ("connection reset by peer"), and established connections initiated over this edge are aborted (like a severed TCP link).</summary>
		Severed = 0,

		/// <summary>The target actively refuses new connections (RST on SYN, "actively refused"). Established connections are NOT affected (a service that stops listening does not kill already-accepted sockets).</summary>
		Refused,

		/// <summary>The name no longer resolves (DNS failure). Connect-time only; established connections are NOT affected.</summary>
		NameResolution,

		/// <summary>Packets vanish (yanked cable, dead gateway): new connections PARK silently and fail with a connect timeout once <see cref="VirtualNetworkFault.ConnectTimeout"/> elapses on virtual time; the byte flow of established connections in this direction parks silently, failing with a read/write timeout after <see cref="VirtualNetworkFault.NoticeAfter"/> (if set).</summary>
		Blackhole,
	}

	/// <summary>Describes the failure mode applied to a cut virtual link (see <see cref="VirtualNetworkTopology.Cut(string, string, VirtualNetworkFault)"/>)</summary>
	public sealed record VirtualNetworkFault
	{

		private VirtualNetworkFault(VirtualNetworkFaultKind kind)
		{
			this.Kind = kind;
		}

		/// <summary>Kind of failure this fault produces</summary>
		public VirtualNetworkFaultKind Kind { get; }

		/// <summary>Budget granted to a NEW connection attempt over a blackholed link, before it fails with a simulated connect timeout. Measured on <see cref="VirtualNetworkTopology.Time"/>.</summary>
		public TimeSpan ConnectTimeout { get; private init; }

		/// <summary>Silent window granted to a parked read/write on an established connection over a blackholed link, before it fails with a simulated read/write timeout. <see langword="null"/> means "silent forever": only a <see cref="VirtualNetworkTopology.Restore"/> (or the caller's own cancellation) releases the parked operation. Measured on <see cref="VirtualNetworkTopology.Time"/>.</summary>
		public TimeSpan? NoticeAfter { get; private init; }

		/// <summary>Default connect budget of a blackholed link (~ the classic TCP SYN retransmission budget)</summary>
		public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(21);

		/// <summary>Hard cut: new connections are reset immediately ("connection reset by peer"), established connections initiated over this edge are aborted.</summary>
		public static VirtualNetworkFault Severed { get; } = new(VirtualNetworkFaultKind.Severed);

		/// <summary>The target actively refuses new connections ("actively refused", RST on SYN); established connections keep flowing.</summary>
		public static VirtualNetworkFault Refused { get; } = new(VirtualNetworkFaultKind.Refused);

		/// <summary>The target's name no longer resolves (DNS failure); established connections keep flowing.</summary>
		public static VirtualNetworkFault NameResolution { get; } = new(VirtualNetworkFaultKind.NameResolution);

		/// <summary>Packets vanish: silence first, then a timeout exception manufactured on the topology's (virtualizable) clock</summary>
		/// <param name="connectTimeout">Budget of a NEW connection attempt before it fails with a connect timeout (default: <see cref="DefaultConnectTimeout"/>)</param>
		/// <param name="noticeAfter">Silent window of a parked read/write on an established connection, before it fails with a read/write timeout; <see langword="null"/> parks forever (until restore or caller cancellation)</param>
		public static VirtualNetworkFault Blackhole(TimeSpan? connectTimeout = null, TimeSpan? noticeAfter = null) => new(VirtualNetworkFaultKind.Blackhole)
		{
			ConnectTimeout = connectTimeout ?? DefaultConnectTimeout,
			NoticeAfter = noticeAfter,
		};

		/// <inheritdoc />
		public override string ToString() => this.Kind switch
		{
			VirtualNetworkFaultKind.Blackhole => $"Blackhole(connect: {this.ConnectTimeout}, notice: {(this.NoticeAfter is null ? "never" : this.NoticeAfter.ToString())})",
			_ => this.Kind.ToString(),
		};

	}

	/// <summary>Live state of one DIRECTIONAL edge of the virtual network (all the traffic going FROM one host TO another)</summary>
	/// <remarks>
	/// <para>Edges are created lazily (on first use by the transport, or on the first <see cref="VirtualNetworkTopology.Cut(string, string, VirtualNetworkFault)"/>)
	/// and live for the rest of the test: the transport captures <see cref="CutToken"/> per connection, so an edge that is
	/// cut LATER can still abort the streams that were established while it was healthy - the same latch-then-renew pattern
	/// as <see cref="VirtualNetworkTopology.SimulatedHost.SetOffline"/>.</para>
	/// </remarks>
	public sealed class VirtualNetworkCutEdge
	{

		internal VirtualNetworkCutEdge(string from, string to)
		{
			this.From = from;
			this.To = to;
			this.SeverCts = new();
			this.RestorePulse = new(TaskCreationOptions.RunContinuationsAsynchronously);
		}

		/// <summary>Id of the host the traffic originates from</summary>
		public string From { get; }

		/// <summary>Id of the host the traffic goes to</summary>
		public string To { get; }

		private readonly object SyncRoot = new();

		/// <summary>Fault currently applied to this edge, or <see langword="null"/> when the edge is healthy (read per-request/per-operation by the transport)</summary>
		private volatile VirtualNetworkFault? CurrentFault;

		/// <summary>Source of <see cref="CutToken"/>; cancelled when a <see cref="VirtualNetworkFaultKind.Severed"/> cut is applied, and REPLACED on restore (latched: connections captured before the cut stay dead)</summary>
		private CancellationTokenSource SeverCts { get; set; }

		/// <summary>Completed (and replaced) every time the edge is restored; parked operations race against it</summary>
		private TaskCompletionSource RestorePulse { get; set; }

		/// <summary>Fault currently applied to this edge, or <see langword="null"/> when it is healthy</summary>
		public VirtualNetworkFault? ActiveFault => this.CurrentFault;

		/// <summary>Token that stays valid while this edge is not severed, and is cancelled when a <see cref="VirtualNetworkFaultKind.Severed"/> cut is applied. Captured per-connection by the transport (like the hosts' OnlineTokens), so cutting the edge aborts the established streams that were initiated over it.</summary>
		public CancellationToken CutToken
		{
			get
			{
				lock (this.SyncRoot)
				{
					return this.SeverCts.Token;
				}
			}
		}

		/// <summary>Task that completes the next time this edge is restored (used by parked operations to resume when the "cable is plugged back in")</summary>
		public Task WhenRestored
		{
			get
			{
				lock (this.SyncRoot)
				{
					return this.RestorePulse.Task;
				}
			}
		}

		internal void Apply(VirtualNetworkFault fault)
		{
			Contract.NotNull(fault);
			lock (this.SyncRoot)
			{
				this.CurrentFault = fault;
				if (fault.Kind == VirtualNetworkFaultKind.Severed)
				{ // abort every established connection that was initiated over this edge (severed link)
					this.SeverCts.Cancel();
				}
			}
		}

		internal void Restore()
		{
			lock (this.SyncRoot)
			{
				if (this.CurrentFault is null) return;
				if (this.CurrentFault.Kind == VirtualNetworkFaultKind.Severed)
				{ // renew the token: connections opened from now on live on a fresh token, while those cut above stay cut (latched)
					this.SeverCts = new CancellationTokenSource();
				}
				this.CurrentFault = null;
				var pulse = this.RestorePulse;
				this.RestorePulse = new(TaskCreationOptions.RunContinuationsAsynchronously);
				pulse.TrySetResult();
			}
		}

		/// <inheritdoc />
		public override string ToString() => $"CutEdge<{this.From} -> {this.To}>({this.CurrentFault?.ToString() ?? "healthy"})";

	}

}

#endif
