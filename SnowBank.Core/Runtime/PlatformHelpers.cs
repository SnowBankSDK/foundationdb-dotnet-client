#region Copyright (c) 2023-2026 SnowBank SAS, (c) 2005-2023 Doxense SAS
// All rights reserved.
// 
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
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL SNOWBANK SAS BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
#endregion

namespace SnowBank.Runtime
{
#if DEPRECATED
	using System.Reflection;
#endif

	[DebuggerNonUserCode]
	[PublicAPI]
	public static class PlatformHelpers
	{

#if DEPRECATED
		/// <summary>JIT a method, if possible</summary>
		private static void PreJit(MethodInfo? m)
		{
			if (m != null && !m.IsAbstract && !m.ContainsGenericParameters)
			{
				try
				{
					RuntimeHelpers.PrepareMethod(m.MethodHandle);
				}
				catch (PlatformNotSupportedException)
				{
#if FULL_DEBUG
					System.Diagnostics.Debug.WriteLine($"// Skipping JIT for unsupported method {m.DeclaringType?.Name}.{m}");
#endif
				}
			}
		}
#endif

		[Obsolete("This is not well supported on modern .NET. Use BenchmarkDotNet for proper benchmarks, or call dedicated 'warm up' method for each type")]
		public static void PreJit(params Type[] types)
		{
#if DEPRECATED
			foreach (var type in types)
			{
				PreJit(type);
			}
#endif
		}

		/// <summary>JIT all the methods of the specified type</summary>
		[Obsolete("This is not well supported on modern .NET. Use BenchmarkDotNet for proper benchmarks, or call dedicated 'warm up' method for this type")]
		public static void PreJit(Type type)
		{
#if DEPRECATED
			// JIT all the methods and properties
#if FULL_DEBUG
			int nm = 0;
#endif
			foreach (var m in type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
			{
				PreJit(m);
#if FULL_DEBUG
				++nm;
#endif
			}

#if FULL_DEBUG
			System.Diagnostics.Trace.WriteLine($"// PreJit({type.FullName}): {nm} method(s)");
#endif

			// make sure we JIT the nested classes as well
			foreach (var nested in type.GetNestedTypes())
			{
				PreJit(nested);
			}
#endif
		}

		[Pure]
		public static string GetThreadName(Thread thread)
		{
			return thread.Name ?? ("#" + thread.ManagedThreadId);
		}

		public static string CurrentThreadName
		{
			[Pure]
			get => GetThreadName(Thread.CurrentThread);
		}

	}

}
