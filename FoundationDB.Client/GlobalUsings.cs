global using System;
global using System.Buffers;
global using System.Collections.Generic;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Globalization;
global using System.Linq;
global using System.Runtime.CompilerServices;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using SnowBank.Data.Tuples;
global using SnowBank.Diagnostics.Contracts;
global using SnowBank.Data.Json;
global using SnowBank.Data.Binary;
global using SnowBank.Buffers;
global using SnowBank.Data.Tuples.Binary;
global using SnowBank.Linq;
global using SnowBank.Runtime;
global using SnowBank.Threading;

// JetBrains Annotations
global using ContractAnnotationAttribute = JetBrains.Annotations.ContractAnnotationAttribute;
global using LinqTunnelAttribute = JetBrains.Annotations.LinqTunnelAttribute;
global using InstantHandleAttribute = JetBrains.Annotations.InstantHandleAttribute;
global using MustDisposeResourceAttribute = JetBrains.Annotations.MustDisposeResourceAttribute;
global using MustUseReturnValueAttribute = JetBrains.Annotations.MustUseReturnValueAttribute;
global using PositiveAttribute = JetBrains.Annotations.PositiveAttribute;
global using PublicAPIAttribute = JetBrains.Annotations.PublicAPIAttribute;
global using PureAttribute = System.Diagnostics.Contracts.PureAttribute;
global using StringFormatMethodAttribute = JetBrains.Annotations.StringFormatMethodAttribute;

#if NETSTANDARD2_0
// extension-method polyfills for span/collection BCL APIs missing from netstandard2.0 (see SnowBank.Core/Polyfills/)
global using SnowBank.Compat;
#endif
