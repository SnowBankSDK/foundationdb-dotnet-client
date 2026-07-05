global using System.Buffers;
global using System.Buffers.Binary;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.IO;
global using System.Runtime.CompilerServices;
global using System.Text;
global using SnowBank.Diagnostics.Contracts;
global using SnowBank.Data.Json;
global using Microsoft.Extensions.DependencyInjection;
global using NodaTime;
global using SnowBank.Buffers;
global using SnowBank.Data.Tuples;
global using SnowBank.Networking;
global using SnowBank.Runtime;
global using SnowBank.Threading;
// JetBrains Annotations
global using PublicAPIAttribute = JetBrains.Annotations.PublicAPIAttribute;
global using PureAttribute = System.Diagnostics.Contracts.PureAttribute;

#if NETSTANDARD2_0
// extension-method polyfills for span/collection BCL APIs missing from netstandard2.0 (see SnowBank.Core/Polyfills/)
global using SnowBank.Compat;
#endif
