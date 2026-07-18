global using System;
global using System.Collections.Generic;
global using System.Threading;
global using System.Threading.Tasks;
global using FoundationDB.Client;
global using FoundationDB.Client.Tests;
global using NUnit.Framework;
global using SnowBank.Data.Json;
global using SnowBank.Testing;

#if NETFRAMEWORK
// extension-method polyfills for span/collection BCL APIs missing from netstandard2.0 (see SnowBank.Core/Polyfills/)
global using SnowBank.Compat;
#endif
