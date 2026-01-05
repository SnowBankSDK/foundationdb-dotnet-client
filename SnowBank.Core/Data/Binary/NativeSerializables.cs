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

namespace SnowBank.Data.Binary
{
	using SnowBank.Buffers;

	public readonly struct Utf8StringValue : ISliceSerializable
	{

		public readonly string? Value;

		public Utf8StringValue(string? value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteStringUtf8(this.Value);

		public static implicit operator Utf8StringValue(string? value) => new(value);

	}

	public readonly struct Int32Value : ISliceSerializable
	{

		public readonly int Value;

		public Int32Value(int value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteInt32(this.Value);

		public static implicit operator Int32Value(int value) => new(value);

	}

	public readonly struct UInt32Value : ISliceSerializable
	{

		public readonly uint Value;

		public UInt32Value(uint value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteUInt32(this.Value);

		public static implicit operator UInt32Value(uint value) => new(value);

	}

	public readonly struct Int64Value : ISliceSerializable
	{

		public readonly long Value;

		public Int64Value(long value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteInt64(this.Value);

		public static implicit operator Int64Value(long value) => new(value);

	}

	public readonly struct UInt64Value : ISliceSerializable
	{

		public readonly ulong Value;

		public UInt64Value(ulong value) => this.Value = value;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void WriteTo(ref SliceWriter writer) => writer.WriteUInt64(this.Value);

		public static implicit operator UInt64Value(ulong value) => new(value);

	}

}
