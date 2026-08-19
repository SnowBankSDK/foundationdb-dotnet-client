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

			// a dual clock/TimeProvider facade (e.g. a NodaTimeProvider wrapping a FakeTimeProvider) is the test's advanceable
			// time source: wire it as the topology's fault-injection scheduler automatically, so a fake-time test does not need
			// the manual "context.Topology.Time = fake" assignment. A test that assigns Topology.Time itself afterward still
			// wins (plain property, last write stands), and a test whose Clock is not also a TimeProvider leaves Topology.Time
			// at its default (the real system time), unchanged from today.
			if (this.Clock is TimeProvider timeProvider)
			{
				this.Topology.Time = timeProvider;
			}
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

		public Dictionary<string, TimelineEventRule> TimelineEventRules { get; } = new(StringComparer.Ordinal);

		/// <inheritdoc/>
		IReadOnlyDictionary<string, TimelineEventRule> IDistributedTestEnvironmentBuilder.TimelineEventRules => this.TimelineEventRules;

		/// <inheritdoc/>
		public void RegisterTimelineEvent(string eventName, string category, Func<string?, string>? formatLabel = null)
		{
			Contract.NotNullOrEmpty(eventName);
			Contract.NotNullOrEmpty(category);
			this.TimelineEventRules[eventName] = new() { Category = category, FormatLabel = formatLabel };
		}

		public List<DistributedTestCompletedHook> TestCompletedHooks { get; } = [ ];

		/// <inheritdoc/>
		IReadOnlyList<DistributedTestCompletedHook> IDistributedTestEnvironmentBuilder.TestCompletedHooks => this.TestCompletedHooks;

		/// <inheritdoc/>
		public void OnTestCompleted(DistributedTestCompletedHook hook)
		{
			Contract.NotNull(hook);
			this.TestCompletedHooks.Add(hook);
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
