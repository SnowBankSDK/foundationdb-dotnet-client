global using NUnit.Framework;
global using SnowBank.Data.Json;
global using SnowBank.Testing;

#if NETFRAMEWORK
// extension-method polyfills for span/collection BCL APIs missing from netstandard2.0 (see SnowBank.Core/Polyfills/)
global using SnowBank.Compat;
#endif
