global using System;
global using System.Diagnostics;
global using System.Diagnostics.CodeAnalysis;
global using System.Runtime.CompilerServices;
global using SnowBank.Diagnostics.Contracts;

// JetBrains Annotations
global using CollectionAccessAttribute = JetBrains.Annotations.CollectionAccessAttribute;
global using CollectionAccessType = JetBrains.Annotations.CollectionAccessType;
global using ContractAnnotationAttribute = JetBrains.Annotations.ContractAnnotationAttribute;
global using InstantHandleAttribute = JetBrains.Annotations.InstantHandleAttribute;
global using InvokerParameterNameAttribute = JetBrains.Annotations.InvokerParameterNameAttribute;
global using ImplicitUseTargetFlags = JetBrains.Annotations.ImplicitUseTargetFlags;
global using LinqTunnelAttribute = JetBrains.Annotations.LinqTunnelAttribute;
global using MustUseReturnValueAttribute = JetBrains.Annotations.MustUseReturnValueAttribute;
global using NoEnumerationAttribute = JetBrains.Annotations.NoEnumerationAttribute;
global using PositiveAttribute = JetBrains.Annotations.PositiveAttribute;
global using PowerOfTwoAttribute = JetBrains.Annotations.PowerOfTwoAttribute;
global using PublicAPIAttribute = JetBrains.Annotations.PublicAPIAttribute;
global using PureAttribute = System.Diagnostics.Contracts.PureAttribute;
global using StringFormatMethodAttribute = JetBrains.Annotations.StringFormatMethodAttribute;
global using UsedImplicitlyAttribute = JetBrains.Annotations.UsedImplicitlyAttribute;
global using MustDisposeResourceAttribute = JetBrains.Annotations.MustDisposeResourceAttribute;

// extension-method polyfills for netstandard2.0
global using SnowBank.Compat;
