#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{
	using SnowBank.Networking.PacketCapture;

	/// <summary>Execution context for a distributed test environment</summary>
	[DebuggerDisplay("Name={Name}")]
	[PublicAPI]
	public class DistributedTestContext : IDistributedTestContext
	{

		internal DistributedTestContext(DistributedTestEnvironmentBuilder builder)
		{
			Contract.Debug.Requires(builder != null);
			this.Builder = builder;
			this.Features = new(builder.Features);
			this.Clock = builder.Clock;
			this.RealClock = SystemClock.Instance;
			this.TestSubject = builder.TestSubject;
			this.Name = builder.Name;
			this.LogOutput = builder.LogOutput;
			this.LogOutputError = builder.LogOutputError;
			this.CreatedAt = this.RealClock.GetCurrentInstant();
			this.Timeline = new Timeline(new TimelineOptions()
			{
				MaxChunkSize = 128,
				MaxChunks = null,
			});
		}

		internal DistributedTestEnvironmentBuilder Builder { get; }

		public TextWriter LogOutput { get; }

		public TextWriter LogOutputError { get; }

		public CancellationTokenSource Lifetime { get; } = new CancellationTokenSource();

		public VirtualNetworkTopology Topology => this.Builder.Topology;

		IVirtualNetworkTopology IDistributedTestContext.Topology => this.Builder.Topology;

		public DistributedTest TestSubject { get; }

		public string Name { get; }

		public IClock Clock { get; }

		public IClock RealClock { get; }

		public Timeline Timeline { get; }

		public Instant CreatedAt { get; }

		public Instant StartedAt { get; private set; }

		public Instant CompletedAt { get; private set; }

		public TComponent GetComponent<TComponent>(string id)
			where TComponent : IDistributedTestComponent
		{
			var compo = this.Builder.Components.FirstOrDefault(c => c.Id == id);
			if (compo == null) throw new InvalidOperationException($"There is not component '{id}' defined in this test environment.");
			if (compo is not TComponent casted) throw new InvalidOperationException($"Component '{id}' is of type {compo.GetType().GetFriendlyName()} instead of expected {typeof(TComponent).GetFriendlyName()}.");
			return casted;
		}

		public IEnumerable<TComponent> FindComponents<TComponent>(Func<TComponent, bool>? predicate = null) where TComponent : IDistributedTestComponent
		{
			foreach (var component in this.Builder.Components)
			{
				if (component is TComponent candidate && (predicate?.Invoke(candidate) ?? true))
				{
					yield return candidate;
				}
			}
		}

		public void Log(string? msg)
		{
			if (msg != null)
			{
				this.LogOutput.WriteLine(msg);
			}
			else
			{
				this.LogOutput.WriteLine();
			}
		}

		public void Log(ref DefaultInterpolatedStringHandler handler) => this.LogOutput.WriteLine(handler.ToStringAndClear());

		#region IFeatureCollection...

		public Dictionary<Type, object> Features { get; }

		public bool TryGetFeature<TFeature>([MaybeNullWhen(false)] out TFeature feature)
		{
			if (!this.Features.TryGetValue(typeof(TFeature), out var obj))
			{
				feature = default;
				return false;
			}

			feature = (TFeature) obj;
			return true;
		}

		public void SetFeature<TFeature>(TFeature? feature)
		{
			if (feature is null)
			{
				this.Features.Remove(typeof(TFeature));
			}
			else
			{
				this.Features[typeof(TFeature)] = feature;
			}
		}

		public bool HasFeature<TFeature>() => this.Features.ContainsKey(typeof(TFeature));

		#endregion

		#region Packet Capture...

		private List<CapturedPacket> Packets { get; } = [ ];

		void IDistributedTestContext.EmitNetworkPackets(ReadOnlyMemory<CapturedPacket> packets)
		{
			lock (this.Packets)
			{
				foreach (var packet in packets.Span)
				{
					this.Packets.Add(packet);
				}
			}
		}

		public List<CapturedPacket> GetNetworkPackets(Func<CapturedPacket, bool>? filter = null)
		{
			lock (this.Packets)
			{
				if (filter == null)
				{
					return [ ..this.Packets ];
				}

				var res = new List<CapturedPacket>(this.Packets.Count);
				foreach (var packet in this.Packets)
				{
					if (filter?.Invoke(packet) ?? true)
					{
						res.Add(packet);
					}
				}
				return res;
			}
		}

		#endregion

		internal async Task Setup(CancellationToken ct)
		{
			try
			{
				// step 1: inter-connect entre components
				foreach (var component in this.Builder.Components)
				{
					try
					{
						await component.Prepare(this, ct);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Test startup failed because test component {component.Id} ({component.GetType().Name}) could not be prepared: [{e.GetType().Name}] {e.Message}", e);
					}
				}

				ct.ThrowIfCancellationRequested();

				// step2: "init"
				foreach (var component in this.Builder.Components)
				{
					try
					{
						await component.Init(ct);
					}
					catch (Exception e)
					{
						if (e is InvalidOperationException invex && invex.Message.Contains("No constructor for type '") && component is DistributedTestComponent wc)
						{ // c'est problablement un crash du DI! on va dumper le contenu de tous les services définis

							var sb = new StringBuilder();
							foreach (var desc in wc.GetRequiredService<IServiceCollection>())
							{
								sb.AppendLine($"{desc.ServiceType.Namespace}.{desc.ServiceType.GetFriendlyName()} / {desc.ImplementationType?.GetFriendlyName()} [{desc.Lifetime}]");
							}

							this.LogOutputError.WriteLine(sb.ToString());
						}

						throw new InvalidOperationException($"Test startup failed because test component {component.Id} ({component.GetType().Name}) could not be initialized: [{e.GetType().Name}] {e.Message}", e);
					}
				}

				ct.ThrowIfCancellationRequested();

				// step3: "start up"
				foreach (var component in this.Builder.Components)
				{
					try
					{
						await component.Start(ct);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Test startup failed because test component {component.Id} ({component.GetType().Name}) could not be started: [{e.GetType().Name}] {e.Message}", e);
					}
				}
			}
			finally
			{
				this.StartedAt = this.RealClock.GetCurrentInstant();
			}
		}

		internal async Task TearDown(CancellationToken ct)
		{
			this.CompletedAt = this.RealClock.GetCurrentInstant();
			await this.Lifetime.CancelAsync();

			// stop in reverse order!
			var stopOrder = new List<IDistributedTestComponent>(this.Builder.Components);
			stopOrder.Reverse();

			// Phase 1: Stop
			foreach (var compo in stopOrder)
			{
				try
				{
					await compo.Stop(ct);
				}
				catch (Exception e)
				{
					//??
					SimpleTest.LogError($"# Failed to stop test component {compo.Id} ({compo.GetType().Name}) ", e);
				}
			}

			// Phase 2: Dispose
			foreach (var compo in stopOrder)
			{
				try
				{
					await compo.DisposeAsync();
				}
				catch (Exception e)
				{
					SimpleTest.LogError($"# Failed to dispose test component {compo.Id} (({compo.GetType().Name}) )", e);
				}
			}

			//REVIEW: should we dispose any feature singleton that implements IDisposable?
			this.Features.Clear();
		}

	}

}
