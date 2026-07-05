global using System;
global using System.Buffers.Binary;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Globalization;
global using System.IO;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using NUnit.Framework;
global using SnowBank.Testing;
global using SnowBank.Text;

// JetBrains Annotations
global using MustDisposeResourceAttribute = JetBrains.Annotations.MustDisposeResourceAttribute;

#if NETFRAMEWORK
// the netstandard2.0 build of SnowBank.Core publicly ships the BCL extension polyfills (span overloads, TryFormat, ...)
global using SnowBank.Compat;
#endif
