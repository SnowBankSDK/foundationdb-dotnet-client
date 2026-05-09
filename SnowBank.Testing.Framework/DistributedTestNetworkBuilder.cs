#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

namespace SnowBank.Testing.Framework
{

	[DebuggerDisplay("Name={Location.Name}")]
	internal class DistributedTestNetworkBuilder : IDistributedTestNetworkBuilder
	{

		public DistributedTestNetworkBuilder(DistributedTestEnvironmentBuilder parent, IVirtualNetworkLocation location)
		{
			this.Parent = parent;
			this.Location = location;
		}

		public DistributedTestEnvironmentBuilder Parent { get; }

		/// <inheritdoc/>
		IDistributedTestEnvironmentBuilder IDistributedTestNetworkBuilder.Top => this.Parent;

		/// <inheritdoc/>
		public IVirtualNetworkLocation Location { get; }

		/// <inheritdoc/>
		public bool TryGetFeature<TFeature>([MaybeNullWhen(false)] out TFeature feature) => this.Parent.TryGetFeature<TFeature>(out feature);

		/// <inheritdoc/>
		public void SetFeature<TFeature>(TFeature feature) => this.Parent.SetFeature<TFeature>(feature);

		/// <inheritdoc/>
		public bool HasFeature<TFeature>() => this.Parent.HasFeature<TFeature>();

		/// <inheritdoc/>
		public IDistributedTestComponent RegisterComponent(IDistributedTestComponent component) => this.Parent.RegisterComponent(component);

	}

}
