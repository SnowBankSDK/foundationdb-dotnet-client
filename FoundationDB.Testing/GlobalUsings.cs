global using System;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Globalization;
global using System.Runtime.CompilerServices;
global using System.Threading;
global using System.Threading.Tasks;
global using SnowBank.Data.Tuples;
global using NodaTime;
global using NUnit.Framework;
global using SnowBank.Diagnostics.Contracts;
global using SnowBank.Linq;
global using SnowBank.Runtime;
global using SnowBank.Testing;

#if NETFRAMEWORK
// extension-method polyfills for span/collection BCL APIs missing from netstandard2.0 (see SnowBank.Core/Polyfills/)
global using SnowBank.Compat;
#endif
