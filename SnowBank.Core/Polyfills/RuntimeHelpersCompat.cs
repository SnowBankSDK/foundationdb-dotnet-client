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

// netstandard2.0 has no RuntimeHelpers.IsReferenceOrContainsReferences. The netstandard2.0 build redirects the
// RuntimeHelpers name to this shim via file-local aliases in the sources that need it.
// Internal: plumbing for the sources only (Compat-branded names must never appear in application code).

#if NETSTANDARD2_0

namespace SnowBank.Compat
{
	using System;
	using System.Reflection;

	internal static class RuntimeHelpersCompat
	{

		private static class PerType<T>
		{
			public static readonly bool Value = ComputeIsReferenceOrContainsReferences(typeof(T));
		}

		/// <summary>Mirrors <c>RuntimeHelpers.IsReferenceOrContainsReferences&lt;T&gt;()</c> (.NET Core 2.0+): computed once per type via reflection, then cached in a static field (so the per-call cost is the same as the real intrinsic).</summary>
		public static bool IsReferenceOrContainsReferences<T>() => PerType<T>.Value;

		private static bool ComputeIsReferenceOrContainsReferences(Type type)
		{
			if (!type.IsValueType) return true;
			if (type.IsPrimitive || type.IsPointer || type.IsEnum) return false;
			foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (field.FieldType != type && ComputeIsReferenceOrContainsReferences(field.FieldType))
				{
					return true;
				}
			}
			return false;
		}

	}

}

#endif
