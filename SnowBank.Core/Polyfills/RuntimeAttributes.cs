#region Copyright (c) 2023-2026 SnowBank SAS
// All rights reserved.
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
#endregion

// Polyfills for BCL attributes that PolySharp does not generate, and that are missing from netstandard2.0.
// These only exist in the netstandard2.0 build; modern targets use the real BCL types.

#if NETSTANDARD2_0

namespace System.Diagnostics
{

	/// <summary>Types and members attributed with <see cref="StackTraceHiddenAttribute"/> will be omitted from the stack trace text shown to the user.</summary>
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Struct, Inherited = false)]
	internal sealed class StackTraceHiddenAttribute : Attribute
	{
		public StackTraceHiddenAttribute() { }
	}

}

namespace System.Diagnostics.CodeAnalysis
{

	/// <summary>Specifies the types of members that are dynamically accessed. This is a compat no-op backport (trimming/AOT are not a concern on .NET Framework hosts).</summary>
	[Flags]
	internal enum DynamicallyAccessedMemberTypes
	{
		None = 0,
		PublicParameterlessConstructor = 0x0001,
		PublicConstructors = 0x0002 | PublicParameterlessConstructor,
		NonPublicConstructors = 0x0004,
		PublicMethods = 0x0008,
		NonPublicMethods = 0x0010,
		PublicFields = 0x0020,
		NonPublicFields = 0x0040,
		PublicNestedTypes = 0x0080,
		NonPublicNestedTypes = 0x0100,
		PublicProperties = 0x0200,
		NonPublicProperties = 0x0400,
		PublicEvents = 0x0800,
		NonPublicEvents = 0x1000,
		Interfaces = 0x2000,
		All = ~None,
	}

	/// <summary>Indicates that certain members on a specified <see cref="System.Type"/> are accessed dynamically. Compat no-op backport.</summary>
	[AttributeUsage(
		AttributeTargets.Field | AttributeTargets.ReturnValue | AttributeTargets.GenericParameter |
		AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Class,
		Inherited = false)]
	internal sealed class DynamicallyAccessedMembersAttribute : Attribute
	{
		public DynamicallyAccessedMembersAttribute(DynamicallyAccessedMemberTypes memberTypes)
		{
			this.MemberTypes = memberTypes;
		}

		public DynamicallyAccessedMemberTypes MemberTypes { get; }
	}

	/// <summary>Indicates that the specified method requires dynamic access to code that is not referenced statically. Compat no-op backport.</summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class, Inherited = false)]
	internal sealed class RequiresUnreferencedCodeAttribute : Attribute
	{
		public RequiresUnreferencedCodeAttribute(string message) => this.Message = message;
		public string Message { get; }
		public string? Url { get; set; }
	}

	/// <summary>Suppresses reporting of a specific trimming or AOT rule violation. Compat no-op backport.</summary>
	[AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	internal sealed class UnconditionalSuppressMessageAttribute : Attribute
	{
		public UnconditionalSuppressMessageAttribute(string category, string checkId)
		{
			this.Category = category;
			this.CheckId = checkId;
		}
		public string Category { get; }
		public string CheckId { get; }
		public string? Scope { get; set; }
		public string? Target { get; set; }
		public string? MessageId { get; set; }
		public string? Justification { get; set; }
	}

}

#endif
