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

// On modern targets, custom deserialization dispatches through the static abstract member
// IJsonDeserializable<TSelf>.JsonDeserialize. Static interface members need runtime support that
// netstandard2.0/netfx lacks, so on this target the interface is a marker and implementing types expose a
// plain `public static TSelf JsonDeserialize(JsonValue, ICrystalJsonTypeResolver?)` instead; this dispatcher
// locates that method by reflection ONCE per type and caches a compiled delegate — the per-call overhead
// after the first hit is a single delegate invocation.

#if NETSTANDARD2_0

namespace SnowBank.Data.Json
{
	using System;
	using System.Reflection;

	/// <summary>Reflection-based stand-in for the <c>TSelf.JsonDeserialize(...)</c> static-abstract dispatch (netstandard2.0 only).</summary>
	internal static class JsonDeserializableDispatcher<T>
	{

		private static readonly Func<JsonValue, ICrystalJsonTypeResolver?, T> Handler = CreateHandler();

		private static Func<JsonValue, ICrystalJsonTypeResolver?, T> CreateHandler()
		{
			var method = typeof(T).GetMethod(
				"JsonDeserialize",
				BindingFlags.Public | BindingFlags.Static,
				binder: null,
				[ typeof(JsonValue), typeof(ICrystalJsonTypeResolver) ],
				modifiers: null);

			if (method is null || method.ReturnType != typeof(T))
			{ // fail lazily (on first use) with an actionable message, instead of crashing the type initializer eagerly
				return (_, _) => throw new NotSupportedException($"Type '{typeof(T)}' does not expose a public static JsonDeserialize(JsonValue, ICrystalJsonTypeResolver?) method, which is required on this target in place of the IJsonDeserializable<T> static interface member.");
			}

			return (Func<JsonValue, ICrystalJsonTypeResolver?, T>) method.CreateDelegate(typeof(Func<JsonValue, ICrystalJsonTypeResolver?, T>));
		}

		/// <summary>Invokes the type's static <c>JsonDeserialize</c> method.</summary>
		public static T Invoke(JsonValue value, ICrystalJsonTypeResolver? resolver) => Handler(value, resolver);

	}

}

#endif
