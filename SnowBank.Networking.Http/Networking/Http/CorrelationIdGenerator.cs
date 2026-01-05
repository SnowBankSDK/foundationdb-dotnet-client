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

namespace SnowBank.Networking.Http
{

	internal static class CorrelationIdGenerator
	{

		// Base32 encoding - in ascii sort order for easy text based sorting
		private static readonly char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

		// Seed the _lastConnectionId for this application instance with
		// the number of 100-nanosecond intervals that have elapsed since 12:00:00 midnight, January 1, 0001
		// for a roughly increasing _lastId over restarts
		private static long LastId = DateTime.UtcNow.Ticks;

		public static string GetNextId() => GenerateId(Interlocked.Increment(ref CorrelationIdGenerator.LastId));

		private static string GenerateId(long id)
		{
			return string.Create(14, id, (buffer, value) =>
			{
				char[] encode32Chars = s_encode32Chars;

				buffer[13] = 'C'; //note: add a 'C' suffix - for 'Client' - in order to prevent collisions with TraceIdentifier from server requests (that use the same format!)
				buffer[12] = encode32Chars[value & 31];
				buffer[11] = encode32Chars[(value >> 5) & 31];
				buffer[10] = encode32Chars[(value >> 10) & 31];
				buffer[9] = encode32Chars[(value >> 15) & 31];
				buffer[8] = encode32Chars[(value >> 20) & 31];
				buffer[7] = encode32Chars[(value >> 25) & 31];
				buffer[6] = encode32Chars[(value >> 30) & 31];
				buffer[5] = encode32Chars[(value >> 35) & 31];
				buffer[4] = encode32Chars[(value >> 40) & 31];
				buffer[3] = encode32Chars[(value >> 45) & 31];
				buffer[2] = encode32Chars[(value >> 50) & 31];
				buffer[1] = encode32Chars[(value >> 55) & 31];
				buffer[0] = encode32Chars[(value >> 60) & 31];
			});
		}

	}

}
