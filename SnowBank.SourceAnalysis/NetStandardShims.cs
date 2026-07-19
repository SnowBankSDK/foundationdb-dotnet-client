#if NETSTANDARD2_0

namespace System.Diagnostics.CodeAnalysis
{

	[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
	internal sealed class SetsRequiredMembersAttribute : Attribute
	{ }

	/// <summary>Specifies that when a method returns <see cref="ReturnValue"/>, the parameter may be null even if the corresponding type disallows it.</summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	internal sealed class MaybeNullWhenAttribute : Attribute
	{
		/// <summary>Initializes the attribute with the specified return value condition.</summary>
		/// <param name="returnValue">The return value condition. If the method returns this value, the associated parameter may be null.</param>
		public MaybeNullWhenAttribute(bool returnValue) => this.ReturnValue = returnValue;

		/// <summary>Gets the return value condition.</summary>
		public bool ReturnValue { get; }
	}

}

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	/// <summary>Reserved to be used by the compiler for tracking metadata.
	/// This class should not be used by developers in source code.</summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class IsExternalInit
	{
	}
	
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal sealed class RequiredMemberAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	internal sealed class CompilerFeatureRequiredAttribute : Attribute
	{
		public CompilerFeatureRequiredAttribute(string featureName) => this.FeatureName = featureName;
		public string FeatureName { get; }
		public bool IsOptional { get; init; }
		public const string RefStructs = nameof(RefStructs);
		public const string RequiredMembers = nameof(RequiredMembers);
	}

}
#endif
