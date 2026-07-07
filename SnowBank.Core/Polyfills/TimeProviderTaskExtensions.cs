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

// "Reverse polyfill": on net8+ the TimeProvider type is in-box, but the Microsoft.Bcl.TimeProvider package that carries
// the provider.Delay(...) extension is NOT referenced there (only on netstandard2.0). This file restores that ONE extension
// shape on net8+ by delegating to the in-box Task.Delay(delay, provider, ct) overload, so call sites read the same on every
// target with no #if. On netstandard2.0 this file is excluded and the extension comes from the package instead.
//
// Kept INTERNAL on purpose: Microsoft.Bcl.TimeProvider's net8 asset also declares a public
// System.Threading.Tasks.TimeProviderTaskExtensions.Delay, so a public copy of this would be an ambiguous-call (CS0121)
// in any project that has the package transitively (e.g. anything pulling Microsoft.AspNetCore.SignalR.Client).

#if NET8_0_OR_GREATER

namespace SnowBank.Compat
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Reverse-polyfill of the <c>Microsoft.Bcl.TimeProvider</c> <see cref="TimeProvider"/> extensions on net8+ (delegating to the in-box BCL overloads).</summary>
	internal static class TimeProviderTaskExtensions
	{
		/// <summary>Creates a task that completes after the specified <paramref name="delay"/>, measured against the given <paramref name="timeProvider"/>.</summary>
		/// <remarks>Mirrors the <c>Microsoft.Bcl.TimeProvider</c> extension used on netstandard2.0; on net8+ it forwards to the in-box <see cref="Task.Delay(TimeSpan, TimeProvider, CancellationToken)"/> overload.</remarks>
		public static Task Delay(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken = default)
			=> Task.Delay(delay, timeProvider, cancellationToken);
	}
}

#endif
