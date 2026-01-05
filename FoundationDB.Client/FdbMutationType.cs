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

	/// <summary>Defines a type of mutation applied to a key</summary>
	[PublicAPI]
	public enum FdbMutationType
	{
		/// <summary>Invalid</summary>
		Invalid = 0,

		//note: there is no entry for 1

		/// <summary>Performs an addition of little-endian integers.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// The integers to be added must be stored in a little-endian representation.
		/// They can be signed in two's complement representation or unsigned.
		/// You can add to an integer at a known offset in the value by prepending the appropriate number of zero bytes to <c>param</c> and padding with zero bytes to match the length of the value.
		/// However, this offset technique requires that you know the addition will not cause the integer field within the value to overflow.
		/// </summary>
		Add = 2,

		/// <summary>Performs a bitwise <c>and</c> operation.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// </summary>
		BitAnd = 6,

		/// <summary>Performs a bitwise <c>or</c> operation.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// </summary>
		BitOr = 7,

		/// <summary>Performs a bitwise <c>xor</c> operation.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// </summary>
		BitXor = 8,

		/// <summary>Appends <c>param</c> to the end of the existing value already in the database at the given key (or creates the key and sets the value to <c>param</c> if the key is empty).
		/// This will only append the value if the final concatenated value size is less than or equal to the maximum value size (i.e., if it fits).
		/// WARNING: No error is surfaced back to the user if the final value is too large because the mutation will not be applied until after the transaction has been committed.
		/// Therefore, it is only safe to use this mutation type if one can guarantee that one will keep the total value size under the maximum size.
		/// </summary>
		AppendIfFits = 9,

		/// <summary>Performs a little-endian comparison of byte strings.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// The larger of the two values is then stored in the database.
		/// </summary>
		Max = 12,

		/// <summary>Performs a little-endian comparison of byte strings.
		/// If the existing value in the database is not present or shorter than <c>param</c>, it is first extended to the length of <c>param</c> with zero bytes.
		/// If <c>param</c> is shorter than the existing value in the database, the existing value is truncated to match the length of <c>param</c>.
		/// The smaller of the two values is then stored in the database.
		/// </summary>
		Min = 13,

		/// <summary>Transforms <c>key</c> using a VersionStamp for the transaction.</summary>
		/// <remarks>
		/// <para>Sets the transformed key in the database to <c>param</c>.</para>
		/// <para>The key is transformed by removing the final four bytes from the key and reading those as a little-Endian 32-bit integer to get a position <c>pos</c>.</para>
		/// <para>The 10 bytes of the key from <c>pos</c> to <c>pos + 10</c> are replaced with the VersionStamp of the transaction used.</para>
		/// <para>The first byte of the key is position 0.</para>
		/// <para>A VersionStamp is a 10 byte, unique, monotonically (but not sequentially) increasing value for each committed transaction.</para>
		/// <para>The first 8 bytes are the committed version of the database (serialized in big-Endian order).</para>
		/// <para>The last 2 bytes are monotonic in the serialization order for transactions.</para>
		/// <para>WARNING: prior to API version 520, the offset was computed from only the final two bytes rather than the final four bytes.</para>
		/// </remarks>
		VersionStampedKey = 14,

		/// <summary>Transforms <c>param</c> using a VersionStamp for the transaction.</summary>
		/// <remarks>
		/// <para>Sets the <c>key</c> given to the transformed <c>param</c>.</para>
		/// <para>The parameter is transformed by removing the final four bytes from <c>param</c> and reading those as a little-Endian 32-bit integer to get a position <c>pos</c>.</para>
		/// <para>The 10 bytes of the parameter from <c>pos</c> to <c>pos + 10</c> are replaced with the VersionStamp of the transaction used.</para>
		/// <para>The first byte of the parameter is position 0.</para>
		/// <para>A VersionStamp is a 10 byte, unique, monotonically (but not sequentially) increasing value for each committed transaction.</para>
		/// <para>The first 8 bytes are the committed version of the database (serialized in big-Endian order).</para>
		/// <para>The last 2 bytes are monotonic in the serialization order for transactions.</para>
		/// <para>WARNING: prior to API version 520, the VersionStamp was always placed at the beginning of the parameter rather than computing an offset.</para>
		/// </remarks>
		VersionStampedValue = 15,

		/// <summary>Performs lexicographic comparison of byte strings. If the existing value in the database is not present, then the parameter is stored. Otherwise, the smaller of the two values is then stored in the database.</summary>
		ByteMin = 16,

		/// <summary>Performs lexicographic comparison of byte strings. If the existing value in the database is not present, then the parameter is stored. Otherwise, the larger of the two values is then stored in the database.</summary>
		ByteMax = 17,

		// note: 18 and 19 are not defined anymore, used to be called Min "v2" and Max "v2" ?

		/// <summary>Performs an atomic compare and clear operation. If the existing value in the database is equal to the given value, then given key is cleared.</summary>
		CompareAndClear = 20,

	}

}
