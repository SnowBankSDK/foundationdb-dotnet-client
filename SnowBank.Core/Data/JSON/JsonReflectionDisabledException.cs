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

namespace SnowBank.Data.Json
{
	using SnowBank.Runtime;

	/// <summary>Thrown when <see cref="CrystalJson.DisableReflection"/> is set and a value would be
	/// (de)serialized through the runtime reflection path instead of a source-generated converter or the JSON DOM.</summary>
	/// <remarks>Diagnostic only. The message names the offending type, so a hidden reflection fallback becomes a
	/// located failure while validating that a code path stays reflection-free for Native AoT.</remarks>
	public sealed class JsonReflectionDisabledException : InvalidOperationException
	{
		/// <summary>The type that would have been handled by runtime reflection.</summary>
		public Type Type { get; }

		/// <summary>Creates the exception for the type that tripped the disabled reflection path.</summary>
		public JsonReflectionDisabledException(Type type)
			: base($"CrystalJson reflection is disabled (CrystalJson.DisableReflection is set): the type '{type.GetFriendlyName()}' has no source-generated converter and would be handled by runtime reflection. Register it in a [CrystalJsonConverter]/[CrystalSerializable] container, serialize through the JSON DOM, or annotate the call path for AoT if reflection is unavoidable.")
		{
			this.Type = type;
		}
	}
}
