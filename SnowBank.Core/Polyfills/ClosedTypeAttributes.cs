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

// Attributes that the C# 15 compiler needs to compile a 'closed' class.
// The compiler matches both attributes by full name, so a copy declared in this assembly works on every target.
// - IsClosedTypeAttribute ships in the net11.0 runtime only: net8.0 and net10.0 get an internal copy, netstandard2.0 a public one.
// - CompilerFeatureRequiredAttribute ships in the net7.0 runtime and later: only netstandard2.0 needs a copy.
// The netstandard2.0 copies are public, so that a .NET Framework code base that references this build can declare its own closed classes.

#if !NET11_0_OR_GREATER

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	/// <summary>Lists the derived types of a C# 15 <c>closed</c> class.</summary>
	/// <remarks>
	/// <para>The C# compiler applies this attribute to every <c>closed</c> class, and fills <see cref="DerivedTypes"/> with the classes that derive from it directly.</para>
	/// <para>The type ships in the .NET 11 runtime. This copy has the same shape, for the target frameworks that do not have it.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
#if NETSTANDARD2_0
	public
#else
	internal
#endif
	sealed class IsClosedTypeAttribute : Attribute
	{

		/// <summary>Classes that derive directly from the closed class.</summary>
		public Type[] DerivedTypes { get; set; } = [];

	}

}

#endif

#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices
{

	/// <summary>Indicates that compiler support for a particular feature is required for the location where this attribute is applied.</summary>
	/// <remarks>The type ships in the .NET 7 runtime and later. This copy has the same shape, for the netstandard2.0 build.</remarks>
	[AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
	public sealed class CompilerFeatureRequiredAttribute : Attribute
	{

		/// <summary>Initializes a new instance of the <see cref="CompilerFeatureRequiredAttribute"/> type.</summary>
		/// <param name="featureName">The name of the feature that the compiler must support.</param>
		public CompilerFeatureRequiredAttribute(string featureName) => this.FeatureName = featureName;

		/// <summary>The name of the compiler feature.</summary>
		public string FeatureName { get; }

		/// <summary>If <see langword="true" />, a compiler that does not know the feature can ignore the attribute.</summary>
		public bool IsOptional { get; init; }

		/// <summary>The <see cref="FeatureName"/> used for the ref structs C# feature.</summary>
		public const string RefStructs = nameof(RefStructs);

		/// <summary>The <see cref="FeatureName"/> used for the required members C# feature.</summary>
		public const string RequiredMembers = nameof(RequiredMembers);

	}

}

#endif
