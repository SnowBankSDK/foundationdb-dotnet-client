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

namespace FoundationDB.Client
{
	using System.Runtime.Serialization;
	using System.Security;
	using FoundationDB.Client.Native;

	/// <summary>Represents an error that occurred while calling the FoundationDB Client API</summary>
	[Serializable]
	public sealed class FdbException : Exception
	{

		/// <summary>Creates a new exception with a given code</summary>
		/// <remarks>The message comes from the managed <see cref="FdbErrorMessages"/> table, never from the native client:
		/// this constructor works identically on every backend, including a process without <c>fdb_c</c> deployed.</remarks>
		public FdbException(FdbError errorCode)
			: this(errorCode, FdbErrorMessages.TryGetMessage(errorCode) ?? $"{errorCode} ({(int) errorCode})", null)
		{ }

		/// <summary>Creates a new exception with a given code, message</summary>
		public FdbException(FdbError errorCode, string message)
			: this(errorCode, message, null)
		{ }

		/// <summary>Creates a new exception with a given code, message, and inner exception</summary>
		public FdbException(FdbError errorCode, string message, Exception? innerException)
			: base(message, innerException)
		{
			this.Code = errorCode;
		}

#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
#endif
		private FdbException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
			this.Code = (FdbError)info.GetInt32("Code");
		}

		[SecurityCritical]
#if NET8_0_OR_GREATER
		[Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.")]
#endif
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue("Code", (int)this.Code);
		}

		/// <summary>Gets the <see cref="FdbError">error code</see> returned by the Native API.</summary>
		public FdbError Code { get; }

		//REVIEW: do we need to add more properties? TransactionId ? DBName? 

	}

}
