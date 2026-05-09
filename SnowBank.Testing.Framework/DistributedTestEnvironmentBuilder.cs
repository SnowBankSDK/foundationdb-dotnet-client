#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{
	/// <summary>Represents the top level builder for a complete test environment</summary>
	[DebuggerDisplay("Name={Name}")]
	internal class DistributedTestEnvironmentBuilder : IDistributedTestEnvironmentBuilder
	{

		public DistributedTestEnvironmentBuilder(DistributedTest subject, string name, TextWriter logOutput, TextWriter errorOutput, CancellationToken lifetime)
		{
			Contract.NotNull(subject);
			Contract.NotNull(name);
			Contract.NotNull(logOutput);
			Contract.NotNull(errorOutput);

			this.TestSubject = subject;
			this.Name = name;
			this.Lifetime = lifetime;
			this.Clock = subject.Clock;
			this.LogOutput = logOutput;
			this.LogOutputError = errorOutput;
		}

		public DistributedTest TestSubject { get; }

		public List<IDistributedTestComponent> Components { get; } = [ ];

		public VirtualNetworkTopology Topology { get; set; } = new();
		
		/// <inheritdoc/>
		IVirtualNetworkTopology IDistributedTestEnvironmentBuilder.Topology => this.Topology;

		public TextWriter LogOutput { get; set; }

		public TextWriter LogOutputError { get; set; }

		/// <inheritdoc/>
		public IClock Clock { get; set; }

		/// <inheritdoc/>
		public CancellationToken Lifetime { get; }

		public string Name { get; }

		public IDistributedTestComponent RegisterComponent(IDistributedTestComponent component)
		{
			Contract.NotNull(component);
			this.Components.Add(component);
			return component;
		}

		/// <inheritdoc/>
		public IVirtualNetworkLocation AddLocation(string id, string name, VirtualNetworkType type, Action<IDistributedTestNetworkBuilder> configureHosts, Action<VirtualNetworkLocationOptions>? configureNetwork = null)
		{
			string dnsSuffix = "." + id.ToLowerInvariant() + ".simulated";
			var options = new VirtualNetworkLocationOptions()
			{
				DnsSuffix = dnsSuffix,
			};
			configureNetwork?.Invoke(options);
			var location = this.Topology.RegisterLocation(id, name, type, options);
			var builder = new DistributedTestNetworkBuilder(this, location);
			configureHosts(builder);
			return location;
		}

		public Dictionary<Type, object> Features { get; } = new();

		/// <inheritdoc/>
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

		/// <inheritdoc/>
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

		/// <inheritdoc/>
		public bool HasFeature<TFeature>() => this.Features.ContainsKey(typeof(TFeature));

	}

}
