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

#if NETSTANDARD2_0

namespace SnowBank.Data.Json
{
	using System.Reflection;

	/// <summary>Runtime probe for <c>System.Runtime.CompilerServices.ITuple</c></summary>
	/// <remarks>The interface is not visible to the netstandard2.0 compiler, but both the .NET Framework (4.7.1+)
	/// and .NET Core runtimes carry it, so tuple detection keeps working at runtime. Item access goes through
	/// reflection: correct but much slower than the modern direct interface calls (only the non-generic fallback
	/// path pays this; the typed ValueTuple&lt;...&gt; fast paths do not use it).</remarks>
	internal static class RuntimeITuple
	{

		/// <summary>Handle to <c>System.Runtime.CompilerServices.ITuple</c>, or <c>null</c> if this runtime does not have it</summary>
		public static readonly Type? Type = System.Type.GetType("System.Runtime.CompilerServices.ITuple");

		private static readonly PropertyInfo? LengthProperty = Type?.GetProperty("Length");

		private static readonly PropertyInfo? ItemIndexer = Type?.GetProperty("Item");

		/// <summary>Tests if the type implements <c>ITuple</c> (ValueTuple, Tuple, ...)</summary>
		public static bool IsTuple(Type? type) => type is not null && Type?.IsAssignableFrom(type) == true;

		/// <summary>Tests if the instance implements <c>ITuple</c> (ValueTuple, Tuple, ...)</summary>
		public static bool IsInstance(object? value) => value is not null && Type?.IsInstanceOfType(value) == true;

		/// <summary>Reads <c>ITuple.Length</c> on a boxed tuple</summary>
		public static int GetLength(object tuple) => (int) LengthProperty!.GetValue(tuple)!;

		/// <summary>Reads <c>ITuple[index]</c> on a boxed tuple</summary>
		public static object? GetItem(object tuple, int index) => ItemIndexer!.GetValue(tuple, [ index ]);

	}

}

#endif
