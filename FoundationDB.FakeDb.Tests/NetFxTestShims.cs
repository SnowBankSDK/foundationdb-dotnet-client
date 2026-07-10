#if NETFRAMEWORK

// On the net472 validation target, both the netstandard2.0 build of SnowBank.Core and
// Docker.DotNet.Handler.Abstractions (pulled in by Testcontainers) export a public IsExternalInit,
// which the compiler rejects as ambiguous (CS8356): declaring our own local copy takes precedence.

namespace System.Runtime.CompilerServices
{
	using System.ComponentModel;

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static class IsExternalInit
	{ }

}

#endif
