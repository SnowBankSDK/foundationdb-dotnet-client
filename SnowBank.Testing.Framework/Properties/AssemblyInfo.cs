#region Copyright (c) 2023-2026 SnowBank SAS
//
// All rights are reserved. Reproduction or transmission in whole or in part, in
// any form or by any means, electronic, mechanical or otherwise, is prohibited
// without the prior written consent of the copyright owner.
//
#endregion

using System.Runtime.InteropServices;

[assembly: Guid("8deff3d0-6b8f-4c6a-8b17-dfd5ba5c284b")]

// we exclude this assembly from test coverage by default when it is being consumed as a NuGet package
#if RELEASE
[assembly: System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
#endif
