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

#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.

namespace SnowBank.Data.Tuples.Binary
{

	/// <summary>Helper class for serializing and deserializing values of type <typeparamref name="T"/> using the tuple binary format</summary>
	/// <typeparam name="T">Type of values to be serialized</typeparam>
	public static class TuplePacker<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
	{

		internal static readonly (TuplePackers.Encoder<T> Direct, TuplePackers.SpanEncoder<T> Span) Encoders = TuplePackers.GetSerializer<T>();

		internal static readonly TuplePackers.Decoder<T?> Decoder = TuplePackers.GetDeserializer<T>(required: true);

		/// <summary>Serializes a boxed value to the tuple format using a <see cref="TupleWriter"/>.</summary>
		/// <param name="writer">The <see cref="TupleWriter"/> into which the value will be serialized.</param>
		/// <param name="value">The boxed value to be serialized. The value must be assignable to type <typeparamref name="T"/>.</param>
		/// <remarks>
		/// <para>The buffer does not need to be pre-allocated.</para>
		/// <para>This method is useful for scenarios where the value is boxed and needs to be serialized without knowing its type at compile time. The buffer does not need to be pre-allocated.</para>
		/// </remarks>
		public static void SerializeBoxedTo(TupleWriter writer, object? value)
		{
			Encoders.Direct(writer, (T) value!);
		}

		/// <summary>Serializes a boxed value to the tuple format using a <see cref="TupleWriter"/>.</summary>
		/// <param name="writer">The <see cref="TupleWriter"/> into which the value will be serialized.</param>
		/// <param name="value">The boxed value to be serialized. The value must be assignable to type <typeparamref name="T"/>.</param>
		/// <remarks>
		/// <para>The buffer does not need to be pre-allocated.</para>
		/// <para>This method is useful for scenarios where the value is boxed and needs to be serialized without knowing its type at compile time. The buffer does not need to be pre-allocated.</para>
		/// </remarks>
		public static bool TrySerializeBoxedTo(ref TupleSpanWriter writer, in object? value)
		{
			return Encoders.Span(ref writer, (T) value!);
		}

		/// <summary>Deserializes a tuple segment into a value of type <typeparamref name="T"/>.</summary>
		/// <param name="slice">Slice that contains the binary representation of a tuple item.</param>
		/// <returns>Decoded value, or an exception if the item type is not compatible.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize(Slice slice) => Decoder(slice.Span);

		/// <summary>Deserializes a tuple segment into a value of type <typeparamref name="T"/>.</summary>
		/// <param name="span">Span that contains the binary representation of a tuple item</param>
		/// <returns>Decoded value, or an exception if the item type is not compatible</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static T? Deserialize(ReadOnlySpan<byte> span)
		{
			//<JIT_HACK>
			// - In Release builds, this will be cleaned up and inlined by the JIT as a direct invocation of the correct WriteXYZ method
			// - In Debug builds, we have to disable this, because it would be too slow
#if !DEBUG
			// non-nullable
			if (typeof(T) == typeof(int)) return (T?) (object) TuplePackers.DeserializeInt32(span);
			if (typeof(T) == typeof(long)) return (T?) (object) TuplePackers.DeserializeInt64(span);
			if (typeof(T) == typeof(bool)) return (T?) (object) TuplePackers.DeserializeBoolean(span);
			if (typeof(T) == typeof(float)) return (T?) (object) TuplePackers.DeserializeSingle(span);
			if (typeof(T) == typeof(double)) return (T?) (object) TuplePackers.DeserializeDouble(span);
			if (typeof(T) == typeof(VersionStamp)) return (T?) (object) TuplePackers.DeserializeVersionStamp(span);
			if (typeof(T) == typeof(Guid)) return (T?) (object) TuplePackers.DeserializeGuid(span);
			if (typeof(T) == typeof(Uuid128)) return (T?) (object) TuplePackers.DeserializeUuid128(span);
			if (typeof(T) == typeof(Uuid96)) return (T?) (object) TuplePackers.DeserializeUuid96(span);
			if (typeof(T) == typeof(Uuid80)) return (T?) (object) TuplePackers.DeserializeUuid80(span);
			if (typeof(T) == typeof(Uuid64)) return (T?) (object) TuplePackers.DeserializeUuid64(span);
			if (typeof(T) == typeof(Uuid48)) return (T?) (object) TuplePackers.DeserializeUuid48(span);
			if (typeof(T) == typeof(TimeSpan)) return (T?) (object) TuplePackers.DeserializeTimeSpan(span);
			if (typeof(T) == typeof(Slice)) return (T?) (object) TuplePackers.DeserializeSlice(span);
			// nullable
			if (typeof(T) == typeof(int?)) return (T?) (object?) TuplePackers.DeserializeInt32Nullable(span);
			if (typeof(T) == typeof(long?)) return (T?) (object?) TuplePackers.DeserializeInt64Nullable(span);
			if (typeof(T) == typeof(bool?)) return (T?) (object?) TuplePackers.DeserializeBooleanNullable(span);
			if (typeof(T) == typeof(float?)) return (T?) (object?) TuplePackers.DeserializeSingleNullable(span);
			if (typeof(T) == typeof(double?)) return (T?) (object?) TuplePackers.DeserializeDoubleNullable(span);
			if (typeof(T) == typeof(VersionStamp?)) return (T?) (object?) TuplePackers.DeserializeVersionStampNullable(span);
			if (typeof(T) == typeof(Guid?)) return (T?) (object?) TuplePackers.DeserializeGuidNullable(span);
			if (typeof(T) == typeof(Uuid128?)) return (T?) (object?) TuplePackers.DeserializeUuid128Nullable(span);
			if (typeof(T) == typeof(Uuid96?)) return (T?) (object?) TuplePackers.DeserializeUuid96Nullable(span);
			if (typeof(T) == typeof(Uuid80?)) return (T?) (object?) TuplePackers.DeserializeUuid80Nullable(span);
			if (typeof(T) == typeof(Uuid64?)) return (T?) (object?) TuplePackers.DeserializeUuid64Nullable(span);
			if (typeof(T) == typeof(Uuid48?)) return (T?) (object?) TuplePackers.DeserializeUuid48Nullable(span);
			if (typeof(T) == typeof(TimeSpan?)) return (T?) (object?) TuplePackers.DeserializeTimeSpanNullable(span);
#endif
			//</JIT_HACK>

			return Decoder(span);
		}

	}

}
