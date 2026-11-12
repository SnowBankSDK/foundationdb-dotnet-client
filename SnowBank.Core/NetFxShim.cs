// add various attribute shims that are missing from .NET 6.0, but required for the latest c# language versions

#if !NET8_0_OR_GREATER

namespace System.Diagnostics.CodeAnalysis
{

	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
	internal sealed class SetsRequiredMembersAttribute : Attribute
	{ }

}

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal sealed class RequiredMemberAttribute : Attribute
	{ }

	// CompilerFeatureRequiredAttribute is public, in Polyfills/ClosedTypeAttributes.cs

}

#endif

#if !NET9_0_OR_GREATER
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.CompilerServices
{

	/// <summary>Specifies the priority of a member in overload resolution. When unspecified, the default priority is 0.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	internal sealed class OverloadResolutionPriorityAttribute : Attribute
	{

		/// <summary>Initializes a new instance of the <see cref="OverloadResolutionPriorityAttribute"/> class.</summary>
		/// <param name="priority">The priority of the attributed member. Higher numbers are prioritized, lower numbers are deprioritized. 0 is the default if no attribute is present.</param>
		public OverloadResolutionPriorityAttribute(int priority)
		{
			this.Priority = priority;
		}

		/// <summary> The priority of the member. </summary>
		public int Priority { get; }

	}

}
#endif


namespace System.Diagnostics.CodeAnalysis
{

	internal static class AotMessages
	{

		public const string RequiresDynamicCode = "The native code for this instantiation might not be available at runtime.";

		public const string TypeMightBeRemoved = "The type might be removed";

	}

#if !NET7_0_OR_GREATER

	/// <summary>Indicates that the specified method requires the ability to generate new code at runtime, for example through <see cref="Reflection"/>.</summary>
	/// <remarks>This allows tools to understand which methods are unsafe to call when compiling ahead of time.</remarks>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresDynamicCodeAttribute : Attribute
	{

		/// <summary>Initializes a new instance of the <see cref="RequiresDynamicCodeAttribute"/> class with the specified message.</summary>
		/// <param name="message">A message that contains information about the usage of dynamic code.</param>
		public RequiresDynamicCodeAttribute(string message)
		{
			Message = message;
		}

		/// <summary>Gets a message that contains information about the usage of dynamic code.</summary>
		public string Message { get; }

		/// <summary>Gets or sets an optional URL that contains more information about the method, why it requires dynamic code, and what options a consumer has to deal with it.</summary>
		public string? Url { get; set; }

	}

#endif // !NET7_0_OR_GREATER

}

