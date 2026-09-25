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

namespace FoundationDB.Client.Tests
{
	using System.Runtime.CompilerServices;
	using FoundationDB.Client.Native;

	/// <summary>The native structs match the sizes of the C header (64-bit processes)</summary>
	[TestFixture]
	[Category("Fdb-Client-InProc")]
	public sealed class FdbNativeStructFacts
	{

		[Test]
		public void Native_Struct_Sizes_Match_The_C_Header()
		{
			Assume.That(IntPtr.Size, Is.EqualTo(8), "The native client is 64-bit only");

			// FDBKey and FDBKeyValue are declared under #pragma pack(4); the other structs use natural alignment
			Assert.That(Unsafe.SizeOf<FdbKeyNative>(), Is.EqualTo(12), "FDBKey");
			Assert.That(Unsafe.SizeOf<FdbKeyValue>(), Is.EqualTo(24), "FDBKeyValue");
			Assert.That(Unsafe.SizeOf<FdbKeySelectorNative>(), Is.EqualTo(20), "FDBKeySelector");
			Assert.That(Unsafe.SizeOf<FdbGetRangeReqAndResultNative>(), Is.EqualTo(56), "FDBGetRangeReqAndResult");
			Assert.That(Unsafe.SizeOf<FdbMappedKeyValueNative>(), Is.EqualTo(112), "FDBMappedKeyValue (the header says: Total 112 bytes)");
		}

	}

}
