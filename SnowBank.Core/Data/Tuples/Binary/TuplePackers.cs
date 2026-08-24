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

namespace SnowBank.Data.Tuples.Binary
{
	using System;
	using System.Collections.Frozen;
	using System.Globalization;
	using System.Linq.Expressions;
	using System.Numerics;
	using System.Reflection;
	using SnowBank.Buffers.Text;
	using SnowBank.Data.Tuples;
	using SnowBank.Runtime;
	using SnowBank.Runtime.Converters;

	/// <summary>Helper methods used during serialization of values to the tuple binary format</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class TuplePackers
	{

		#region Serializers...

		/// <summary>Delegate that writes a value of type <typeparamref name="T"/> into a <see cref="TupleWriter"/></summary>
		public delegate void Encoder<in T>(TupleWriter writer, T? value);

		/// <summary>Delegate that writes a value of type <typeparamref name="T"/> into a <see cref="TupleWriter"/></summary>
		public delegate bool SpanEncoder<T>(ref TupleSpanWriter writer, in T? value);

		private static readonly FrozenDictionary<Type, (Delegate Direct, Delegate? Span)> WellKnownSerializers = InitializeDefaultSerializers();

		private static FrozenDictionary<Type, (Delegate Direct, Delegate? Span)> InitializeDefaultSerializers()
		{
			var map = new Dictionary<Type, (Delegate Direct, Delegate? Span)>
			{
				// ref types
				[typeof(byte[])]           = (new Encoder<byte[]?>(TupleParser.WriteBytes), new SpanEncoder<byte[]?>(TupleParser.TryWriteBytes)),
				[typeof(string)]           = (new Encoder<string?>(TupleParser.WriteString), new SpanEncoder<string?>(TupleParser.TryWriteString)),
				[typeof(System.Net.IPAddress)] = (new Encoder<System.Net.IPAddress?>(TupleParser.WriteIpAddress), new SpanEncoder<System.Net.IPAddress?>(TupleParser.TryWriteIpAddress)),
				[typeof(IVarTuple)]        = (new Encoder<IVarTuple?>(SerializeVarTupleTo), new SpanEncoder<IVarTuple?>(TrySerializeVarTupleTo)),
				[typeof(TuPackUserType)]   = (new Encoder<TuPackUserType?>(TupleParser.WriteUserType), new SpanEncoder<TuPackUserType?>(TupleParser.TryWriteUserType)),

				// structs
				[typeof(Slice)]            = (new Encoder<Slice>(TupleParser.WriteBytes), new SpanEncoder<Slice>(TupleParser.TryWriteBytes)),
				[typeof(bool)]             = (new Encoder<bool>(TupleParser.WriteBool), new SpanEncoder<bool>(TupleParser.TryWriteBool)),
				[typeof(char)]             = (new Encoder<char>(TupleParser.WriteChar), new SpanEncoder<char>(TupleParser.TryWriteChar)),
				[typeof(byte)]             = (new Encoder<byte>(TupleParser.WriteByte), new SpanEncoder<byte>(TupleParser.TryWriteByte)),
				[typeof(sbyte)]            = (new Encoder<sbyte>(TupleParser.WriteSByte), new SpanEncoder<sbyte>(TupleParser.TryWriteSByte)),
				[typeof(short)]            = (new Encoder<short>(TupleParser.WriteInt16), new SpanEncoder<short>(TupleParser.TryWriteInt16)),
				[typeof(ushort)]           = (new Encoder<ushort>(TupleParser.WriteUInt16), new SpanEncoder<ushort>(TupleParser.TryWriteUInt16)),
				[typeof(int)]              = (new Encoder<int>(TupleParser.WriteInt32), new SpanEncoder<int>(TupleParser.TryWriteInt32)),
				[typeof(uint)]             = (new Encoder<uint>(TupleParser.WriteUInt32), new SpanEncoder<uint>(TupleParser.TryWriteUInt32)),
				[typeof(long)]             = (new Encoder<long>(TupleParser.WriteInt64), new SpanEncoder<long>(TupleParser.TryWriteInt64)),
				[typeof(ulong)]            = (new Encoder<ulong>(TupleParser.WriteUInt64), new SpanEncoder<ulong>(TupleParser.TryWriteUInt64)),
				[typeof(float)]            = (new Encoder<float>(TupleParser.WriteSingle), new SpanEncoder<float>(TupleParser.TryWriteSingle)),
				[typeof(double)]           = (new Encoder<double>(TupleParser.WriteDouble), new SpanEncoder<double>(TupleParser.TryWriteDouble)),
				[typeof(decimal)]          = (new Encoder<decimal>(TupleParser.WriteQuadruple), new SpanEncoder<decimal>(TupleParser.TryWriteQuadruple)),
				[typeof(VersionStamp)]     = (new Encoder<VersionStamp>(TupleParser.WriteVersionStamp), new SpanEncoder<VersionStamp>(TupleParser.TryWriteVersionStamp)),
				[typeof(Guid)]             = (new Encoder<Guid>(TupleParser.WriteGuid), new SpanEncoder<Guid>(TupleParser.TryWriteGuid)),
				[typeof(Uuid128)]          = (new Encoder<Uuid128>(TupleParser.WriteUuid128), new SpanEncoder<Uuid128>(TupleParser.TryWriteUuid128)),
				[typeof(Uuid96)]           = (new Encoder<Uuid96>(TupleParser.WriteUuid96), new SpanEncoder<Uuid96>(TupleParser.TryWriteUuid96)),
				[typeof(Uuid80)]           = (new Encoder<Uuid80>(TupleParser.WriteUuid80), new SpanEncoder<Uuid80>(TupleParser.TryWriteUuid80)),
				[typeof(Uuid64)]           = (new Encoder<Uuid64>(TupleParser.WriteUuid64), new SpanEncoder<Uuid64>(TupleParser.TryWriteUuid64)),
				[typeof(Uuid48)]           = (new Encoder<Uuid48>(TupleParser.WriteUuid48), new SpanEncoder<Uuid48>(TupleParser.TryWriteUuid48)),
				[typeof(TimeSpan)]         = (new Encoder<TimeSpan>(TupleParser.WriteTimeSpan), new SpanEncoder<TimeSpan>(TupleParser.TryWriteTimeSpan)),
				[typeof(DateTime)]         = (new Encoder<DateTime>(TupleParser.WriteDateTime), new SpanEncoder<DateTime>(TupleParser.TryWriteDateTime)),
				[typeof(DateTimeOffset)]   = (new Encoder<DateTimeOffset>(TupleParser.WriteDateTimeOffset), new SpanEncoder<DateTimeOffset>(TupleParser.TryWriteDateTimeOffset)),
				[typeof(NodaTime.Instant)] = (new Encoder<NodaTime.Instant>(TupleParser.WriteInstant), new SpanEncoder<NodaTime.Instant>(TupleParser.TryWriteInstant)),
				[typeof(BigInteger)]       = (new Encoder<BigInteger>(TupleParser.WriteBigInteger), new SpanEncoder<BigInteger>(TupleParser.TryWriteBigInteger)),
#if NET8_0_OR_GREATER
				[typeof(Int128)]           = (new Encoder<Int128>(TupleParser.WriteInt128), null), //TODO!
				[typeof(UInt128)]          = (new Encoder<UInt128>(TupleParser.WriteUInt128), null), //TODO!
#endif
				[typeof(ArraySegment<byte>)] = (new Encoder<ArraySegment<byte>>(TupleParser.WriteBytes), new SpanEncoder<ArraySegment<byte>>(TupleParser.TryWriteBytes)),

				// Nullable<T>

				[typeof(Slice?)]            = (new Encoder<Slice?>(TupleParser.WriteBytes), new SpanEncoder<Slice?>(TupleParser.TryWriteBytes)),
				[typeof(bool?)]             = (new Encoder<bool?>(TupleParser.WriteBool), new SpanEncoder<bool?>(TupleParser.TryWriteBool)),
				[typeof(char?)]             = (new Encoder<char?>(TupleParser.WriteChar), new SpanEncoder<char?>(TupleParser.TryWriteChar)),
				[typeof(byte?)]             = (new Encoder<byte?>(TupleParser.WriteByte), new SpanEncoder<byte?>(TupleParser.TryWriteByte)),
				[typeof(sbyte?)]            = (new Encoder<sbyte?>(TupleParser.WriteSByte), new SpanEncoder<sbyte?>(TupleParser.TryWriteSByte)),
				[typeof(short?)]            = (new Encoder<short?>(TupleParser.WriteInt16), new SpanEncoder<short?>(TupleParser.TryWriteInt16)),
				[typeof(ushort?)]           = (new Encoder<ushort?>(TupleParser.WriteUInt16), new SpanEncoder<ushort?>(TupleParser.TryWriteUInt16)),
				[typeof(int?)]              = (new Encoder<int?>(TupleParser.WriteInt32), new SpanEncoder<int?>(TupleParser.TryWriteInt32)),
				[typeof(uint?)]             = (new Encoder<uint?>(TupleParser.WriteUInt32), new SpanEncoder<uint?>(TupleParser.TryWriteUInt32)),
				[typeof(long?)]             = (new Encoder<long?>(TupleParser.WriteInt64), new SpanEncoder<long?>(TupleParser.TryWriteInt64)),
				[typeof(ulong?)]            = (new Encoder<ulong?>(TupleParser.WriteUInt64), new SpanEncoder<ulong?>(TupleParser.TryWriteUInt64)),
				[typeof(float?)]            = (new Encoder<float?>(TupleParser.WriteSingle), new SpanEncoder<float?>(TupleParser.TryWriteSingle)),
				[typeof(double?)]           = (new Encoder<double?>(TupleParser.WriteDouble), new SpanEncoder<double?>(TupleParser.TryWriteDouble)),
				[typeof(decimal?)]          = (new Encoder<decimal?>(TupleParser.WriteQuadruple), new SpanEncoder<decimal?>(TupleParser.TryWriteQuadruple)), 
				[typeof(VersionStamp?)]     = (new Encoder<VersionStamp?>(TupleParser.WriteVersionStamp), new SpanEncoder<VersionStamp?>(TupleParser.TryWriteVersionStamp)),
				[typeof(Guid?)]             = (new Encoder<Guid?>(TupleParser.WriteGuid), new SpanEncoder<Guid?>(TupleParser.TryWriteGuid)),
				[typeof(Uuid128?)]          = (new Encoder<Uuid128?>(TupleParser.WriteUuid128), new SpanEncoder<Uuid128?>(TupleParser.TryWriteUuid128)),
				[typeof(Uuid96?)]           = (new Encoder<Uuid96?>(TupleParser.WriteUuid96), new SpanEncoder<Uuid96?>(TupleParser.TryWriteUuid96)),
				[typeof(Uuid80?)]           = (new Encoder<Uuid80?>(TupleParser.WriteUuid80), new SpanEncoder<Uuid80?>(TupleParser.TryWriteUuid80)),
				[typeof(Uuid64?)]           = (new Encoder<Uuid64?>(TupleParser.WriteUuid64), new SpanEncoder<Uuid64?>(TupleParser.TryWriteUuid64)),
				[typeof(Uuid48?)]           = (new Encoder<Uuid48?>(TupleParser.WriteUuid48), new SpanEncoder<Uuid48?>(TupleParser.TryWriteUuid48)),
				[typeof(TimeSpan?)]         = (new Encoder<TimeSpan?>(TupleParser.WriteTimeSpan), new SpanEncoder<TimeSpan?>(TupleParser.TryWriteTimeSpan)),
				[typeof(DateTime?)]         = (new Encoder<DateTime?>(TupleParser.WriteDateTime), new SpanEncoder<DateTime?>(TupleParser.TryWriteDateTime)),
				[typeof(DateTimeOffset?)]   = (new Encoder<DateTimeOffset?>(TupleParser.WriteDateTimeOffset), new SpanEncoder<DateTimeOffset?>(TupleParser.TryWriteDateTimeOffset)),
				[typeof(NodaTime.Instant?)] = (new Encoder<NodaTime.Instant?>(TupleParser.WriteInstant), new SpanEncoder<NodaTime.Instant?>(TupleParser.TryWriteInstant)),
				[typeof(BigInteger?)]       = (new Encoder<BigInteger?>(TupleParser.WriteBigInteger), new SpanEncoder<BigInteger?>(TupleParser.TryWriteBigInteger)),
#if NET8_0_OR_GREATER
				[typeof(Int128?)]           = (new Encoder<Int128?>(TupleParser.WriteInt128), null), //TODO
				[typeof(UInt128?)]          = (new Encoder<UInt128?>(TupleParser.WriteUInt128), null), //TODO
#endif

				// Span-like
				[typeof(ReadOnlyMemory<byte>)] = (new Encoder<ReadOnlyMemory<byte>>(TupleParser.WriteBytes), new SpanEncoder<ReadOnlyMemory<byte>>(TupleParser.TryWriteBytes)),
				[typeof(Memory<byte>)]      = (new Encoder<Memory<byte>>(TupleParser.WriteBytes), new SpanEncoder<Memory<byte>>(TupleParser.TryWriteBytes)),
				[typeof(ReadOnlyMemory<char>)] = (new Encoder<ReadOnlyMemory<char>>(TupleParser.WriteString), new SpanEncoder<ReadOnlyMemory<char>>(TupleParser.TryWriteString)),
				[typeof(Memory<char>)] = (new Encoder<Memory<char>>(TupleParser.WriteString), new SpanEncoder<Memory<char>>(TupleParser.TryWriteString)),
				[typeof(ArraySegment<byte>?)] = (new Encoder<ArraySegment<byte>?>(TupleParser.WriteBytes), new SpanEncoder<ArraySegment<byte>?>(TupleParser.TryWriteBytes)),
			};

			return map.ToFrozenDictionary();
		}


		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or that throws an exception if the type is not supported</returns>
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		internal static (Encoder<T>, SpanEncoder<T>) GetSerializer<T>()
		{
			//note: this method is only called once per initializing of TuplePackers<T> to create the cached delegate.

			var (direct, span) = GetSerializerFor(typeof(T));

			return (
				((Encoder<T>?) direct) ?? MakeNotSupportedSerializer<T>(),
				((SpanEncoder<T>?) span) ?? MakeNotSupportedSpanSerializer<T>()
			);
		}

		[Pure]
		private static Encoder<T> MakeNotSupportedSerializer<T>()
		{
			return (_, _) => throw new InvalidOperationException($"Does not know how to serialize values of type '{typeof(T).Name}' into keys");
		}

		[Pure]
		private static SpanEncoder<T> MakeNotSupportedSpanSerializer<T>()
		{
			return (ref _, in _) => throw new InvalidOperationException($"Does not know how to serialize values of type '{typeof(T).Name}' into keys");
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static (Delegate? Direct, Delegate? Span) GetSerializerFor(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
			Type type
		)
		{
			Contract.NotNull(type);

			if (type == typeof(object))
			{ // return a generic serializer that will inspect the runtime type of the object
				return (
					new Encoder<object>(SerializeObjectTo),
					new SpanEncoder<object>(TrySerializeObjectTo)
				);
			}

			// look for well-known types that have their own (non-generic) TuplePackers.SerializeTo(...) method

			if (WellKnownSerializers.TryGetValue(type, out var methods))
			{
				return methods;
			}

			// maybe it is a nullable type ?
			var nullableType = Nullable.GetUnderlyingType(type);
			if (nullableType != null)
			{ // nullable types can reuse the underlying type serializer
				var directMethod = GetTuplePackersType().GetMethod(nameof(SerializeNullableTo), BindingFlags.Static | BindingFlags.Public);
				var spanMethod = GetTuplePackersType().GetMethod(nameof(TrySerializeNullableTo), BindingFlags.Static | BindingFlags.Public);
				if (directMethod != null)
				{
					Contract.Debug.Assert(spanMethod != null);
					try
					{
						return (
							directMethod.MakeGenericMethod(nullableType).CreateDelegate(typeof(Encoder<>).MakeGenericType(type)),
							spanMethod!.MakeGenericMethod(nullableType).CreateDelegate(typeof(SpanEncoder<>).MakeGenericType(type))
						);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer for type 'Nullable<{nullableType.Name}>'.", e);
					}
				}
			}

			// maybe it is an IVarTuple ?
			if (type.IsAssignableTo(typeof(IVarTuple)))
			{
				if (type.IsAssignableTo(typeof(ITuplePackable)))
				{ // most tuples implement ITupleFormattable directly!

					var directMethod = GetTuplePackersType().GetMethod(nameof(SerializePackableTupleTo), BindingFlags.Static | BindingFlags.Public);
					var spanMethod = GetTuplePackersType().GetMethod(nameof(TrySerializePackableTupleTo), BindingFlags.Static | BindingFlags.Public);
					if (directMethod != null)
					{
						Contract.Debug.Assert(spanMethod != null);
						try
						{
							return (
								directMethod.MakeGenericMethod(type).CreateDelegate(typeof(Encoder<>).MakeGenericType(type)),
								spanMethod!.MakeGenericMethod(type).CreateDelegate(typeof(SpanEncoder<>).MakeGenericType(type))
							);
						}
						catch (Exception e)
						{
							throw new InvalidOperationException($"Failed to compile fast tuple serializer for type '<{type.GetFriendlyName()}>'.", e);
						}
					}
				}
				else
				{
					// will use the default IVarTuple packing implementation (which may use boxing)
					var directMethod = GetTuplePackersType().GetMethod(nameof(SerializeVarTupleTo), BindingFlags.Static | BindingFlags.Public);
					var spanMethod = GetTuplePackersType().GetMethod(nameof(TrySerializeVarTupleTo), BindingFlags.Static | BindingFlags.Public);
					if (directMethod != null)
					{
						Contract.Debug.Assert(spanMethod != null);
						try
						{
							return (
								directMethod.MakeGenericMethod(type).CreateDelegate(typeof(Encoder<>).MakeGenericType(type)),
								spanMethod!.MakeGenericMethod(type).CreateDelegate(typeof(SpanEncoder<>).MakeGenericType(type))
							);
						}
						catch (Exception e)
						{
							throw new InvalidOperationException($"Failed to compile fast tuple serializer for Tuple type '{type.Name}'.", e);
						}
					}
				}
			}

			// is it a custom type that can pack its items?
			if (type.IsAssignableTo(typeof(ITuplePackable)))
			{
				// this is NOT a tuple, but a custom type that can also "pack itself"

				var directMethod = GetTuplePackersType().GetMethod(nameof(SerializePackableItemsTo), BindingFlags.Static | BindingFlags.Public);
				var spanMethod = GetTuplePackersType().GetMethod(nameof(TrySerializePackableItemsTo), BindingFlags.Static | BindingFlags.Public);
				if (directMethod != null)
				{
					Contract.Debug.Assert(spanMethod != null);
					try
					{
						return (
							directMethod.MakeGenericMethod(type).CreateDelegate(typeof(Encoder<>).MakeGenericType(type)),
							spanMethod!.MakeGenericMethod(type).CreateDelegate(typeof(SpanEncoder<>).MakeGenericType(type))
						);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer for type '<{type.GetFriendlyName()}>'.", e);
					}
				}
			}

			// ValueTuple<T..>
			if (type == typeof(ValueTuple) || (type.Name.StartsWith(nameof(System.ValueTuple) + "`", StringComparison.Ordinal) && type.Namespace == "System"))
			{
				var typeArgs = type.GetGenericArguments();
				var directMethod = FindValueTupleSerializerMethod(typeArgs);
				var spanMethod = FindValueTupleSpanSerializerMethod(typeArgs);
				if (directMethod != null || spanMethod != null)
				{
					try
					{
						return (
							directMethod?.MakeGenericMethod(typeArgs).CreateDelegate(typeof(Encoder<>).MakeGenericType(type)),
							spanMethod?.MakeGenericMethod(typeArgs).CreateDelegate(typeof(SpanEncoder<>).MakeGenericType(type))
						);
					}
					catch (Exception e)
					{
						throw new InvalidOperationException($"Failed to compile fast tuple serializer for Tuple type '{type.Name}'.", e);
					}
				}
			}

			// no luck...
			return (null, null);
		}

		private static MethodInfo? FindValueTupleSerializerMethod(Type[] args)
		{
			//note: we want to find the correct SerializeValueTuple<...>(TupleWriter, (...,), but this cannot be done with Type.GetMethod(...) directly
			// => we have to scan for all methods with the correct name, and the same number of Type Arguments than the ValueTuple.
			return GetTuplePackersType()
				.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.SingleOrDefault(m => m.Name == nameof(SerializeValueTupleTo) && m.GetGenericArguments().Length == args.Length);
		}

		private static MethodInfo? FindValueTupleSpanSerializerMethod(Type[] args)
		{
			//note: we want to find the correct TrySerializeValueTuple<...>(ref TupleSpanWriter, (...,), but this cannot be done with Type.GetMethod(...) directly
			// => we have to scan for all methods with the correct name, and the same number of Type Arguments than the ValueTuple.
			return GetTuplePackersType()
			       .GetMethods(BindingFlags.Static | BindingFlags.Public)
			       .SingleOrDefault(m => m.Name == nameof(TrySerializeValueTupleTo) && m.GetGenericArguments().Length == args.Length);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void SerializeTo<T>(TupleWriter writer, T value)
		{
#if !DEBUG
			//<JIT_HACK>
			// - In Release builds, this will be cleaned up and inlined by the JIT as a direct invocation of the correct WriteXYZ method
			// - In Debug builds, we have to disable this, because it would be too slow
			//IMPORTANT: only ValueTypes and they must have a corresponding Write$TYPE$(TupleWriter, $TYPE) in TupleParser!
			if (default(T) is not null)
			{
				if (typeof(T) == typeof(bool)) { writer.WriteBool((bool) (object) value!); return; }
				if (typeof(T) == typeof(int)) { writer.WriteInt32((int) (object) value!); return; }
				if (typeof(T) == typeof(long)) { writer.WriteInt64((long) (object) value!); return; }
				if (typeof(T) == typeof(uint)) { writer.WriteUInt32((uint) (object) value!); return; }
				if (typeof(T) == typeof(ulong)) { writer.WriteUInt64((ulong) (object) value!); return; }
				if (typeof(T) == typeof(short)) { writer.WriteInt32((short) (object) value!); return; }
				if (typeof(T) == typeof(ushort)) { writer.WriteUInt32((ushort) (object) value!); return; }
				if (typeof(T) == typeof(sbyte)) { writer.WriteInt32((sbyte) (object) value!); return; }
				if (typeof(T) == typeof(byte)) { writer.WriteUInt32((byte) (object) value!); return; }
				if (typeof(T) == typeof(float)) { writer.WriteSingle((float) (object) value!); return; }
				if (typeof(T) == typeof(double)) { writer.WriteDouble((double) (object) value!); return; }
				if (typeof(T) == typeof(decimal)) { writer.WriteQuadruple((decimal) (object) value!); return; }
				if (typeof(T) == typeof(char)) { writer.WriteChar((char) (object) value!); return; }
				if (typeof(T) == typeof(TimeSpan)) { writer.WriteTimeSpan((TimeSpan) (object) value!); return; }
				if (typeof(T) == typeof(DateTime)) { writer.WriteDateTime((DateTime) (object) value!); return; }
				if (typeof(T) == typeof(DateTimeOffset)) { writer.WriteDateTimeOffset((DateTimeOffset) (object) value!); return; }
				if (typeof(T) == typeof(NodaTime.Instant)) { writer.WriteInstant((NodaTime.Instant) (object) value!); return; }
				if (typeof(T) == typeof(Guid)) { writer.WriteGuid((Guid) (object) value!); return; }
				if (typeof(T) == typeof(Uuid128)) { writer.WriteUuid128((Uuid128) (object) value!); return; }
				if (typeof(T) == typeof(Uuid96)) { writer.WriteUuid96((Uuid96) (object) value!); return; }
				if (typeof(T) == typeof(Uuid80)) { writer.WriteUuid80((Uuid80) (object) value!); return; }
				if (typeof(T) == typeof(Uuid64)) { writer.WriteUuid64((Uuid64) (object) value!); return; }
				if (typeof(T) == typeof(Uuid48)) { writer.WriteUuid48((Uuid48) (object) value!); return; }
				if (typeof(T) == typeof(VersionStamp)) { writer.WriteVersionStamp((VersionStamp) (object) value!); return; }
				if (typeof(T) == typeof(Slice)) { writer.WriteBytes((Slice) (object) value!); return; }
				if (typeof(T) == typeof(ArraySegment<byte>)) { writer.WriteBytes((ArraySegment<byte>) (object) value!); return; }
			}
			else
			{
				if (typeof(T) == typeof(bool?)) { writer.WriteBool((bool?) (object) value!); return; }
				if (typeof(T) == typeof(int?)) { writer.WriteInt32((int?) (object) value!); return; }
				if (typeof(T) == typeof(long?)) { writer.WriteInt64((long?) (object) value!); return; }
				if (typeof(T) == typeof(uint?)) { writer.WriteUInt32((uint?) (object) value!); return; }
				if (typeof(T) == typeof(ulong?)) { writer.WriteUInt64((ulong?) (object) value!); return; }
				if (typeof(T) == typeof(short?)) { writer.WriteInt32((short?) (object) value!); return; }
				if (typeof(T) == typeof(ushort?)) { writer.WriteUInt32((ushort?) (object) value!); return; }
				if (typeof(T) == typeof(sbyte?)) { writer.WriteInt32((sbyte?) (object) value!); return; }
				if (typeof(T) == typeof(byte?)) { writer.WriteUInt32((byte?) (object) value!); return; }
				if (typeof(T) == typeof(float?)) { writer.WriteSingle((float?) (object) value!); return; }
				if (typeof(T) == typeof(double?)) { writer.WriteDouble((double?) (object) value!); return; }
				if (typeof(T) == typeof(decimal?)) { writer.WriteQuadruple((decimal?) (object) value!); return; }
				if (typeof(T) == typeof(char?)) { writer.WriteChar((char?) (object) value!); return; }
				if (typeof(T) == typeof(TimeSpan?)) { writer.WriteTimeSpan((TimeSpan?) (object) value!); return; }
				if (typeof(T) == typeof(DateTime?)) { writer.WriteDateTime((DateTime?) (object) value!); return; }
				if (typeof(T) == typeof(DateTimeOffset?)) { writer.WriteDateTimeOffset((DateTimeOffset?) (object) value!); return; }
				if (typeof(T) == typeof(Guid?)) { writer.WriteGuid((Guid?) (object) value!); return; }
				if (typeof(T) == typeof(Uuid128?)) { writer.WriteUuid128((Uuid128?) (object) value!); return; }
				if (typeof(T) == typeof(Uuid96?)) { writer.WriteUuid96((Uuid96?) (object) value!); return; }
				if (typeof(T) == typeof(Uuid80?)) { writer.WriteUuid80((Uuid80?) (object) value!); return; }
				if (typeof(T) == typeof(Uuid64?)) { writer.WriteUuid64((Uuid64?) (object) value!); return; }
				if (typeof(T) == typeof(Uuid48?)) { writer.WriteUuid48((Uuid48?) (object) value!); return; }
				if (typeof(T) == typeof(VersionStamp?)) { writer.WriteVersionStamp((VersionStamp?) (object) value!); return; }

				if (typeof(T) == typeof(string)) { writer.WriteString((string?) (object?) value); return ; }
			}
			//</JIT_HACK>
#endif

			// tuples and packable types write themselves through the instance interface, no reflection
			if (typeof(T).IsAssignableTo(typeof(ITuplePackable)))
			{
				if (value is null)
				{
					writer.WriteNil();
					return;
				}
				var packable = (ITuplePackable) value;
				if (typeof(T).IsAssignableTo(typeof(IVarTuple)))
				{ // a nested tuple item is written as an embedded tuple
					var tw = writer.BeginTuple();
					packable.PackTo(tw);
					tw.EndTuple();
				}
				else
				{ // a custom packable writes its own items into the current tuple
					packable.PackTo(writer);
				}
				return;
			}

			// invoke the encoder directly (reflection fallback for the remaining types)
			TuplePacker<T>.Encoders.Direct(writer, value);
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializeNullableTo<T>(TupleWriter writer, T? value)
			where T : struct
		{
			if (value is not null)
			{
				SerializeTo(writer, value.Value);
			}
			else
			{
				writer.WriteNil();
			}
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TrySerializeNullableTo<T>(ref TupleSpanWriter writer, in T? value)
			where T : struct
		{
			return value is null ? writer.TryWriteNil() : TrySerializeTo(ref writer, value.Value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool TrySerializeTo<T>(ref TupleSpanWriter writer, in T value)
		{
#if !DEBUG
			//<JIT_HACK>
			if (default(T) is not null)
			{
				if (typeof(T) == typeof(bool)) return TupleParser.TryWriteBool(ref writer, (bool) (object) value!);
				if (typeof(T) == typeof(int)) return TupleParser.TryWriteInt32(ref writer, (int) (object) value!);
				if (typeof(T) == typeof(long)) return TupleParser.TryWriteInt64(ref writer, (long) (object) value!);
				if (typeof(T) == typeof(uint)) return TupleParser.TryWriteUInt32(ref writer, (uint) (object) value!);
				if (typeof(T) == typeof(ulong)) return TupleParser.TryWriteUInt64(ref writer, (ulong) (object) value!);
				if (typeof(T) == typeof(short)) return TupleParser.TryWriteInt32(ref writer, (short) (object) value!);
				if (typeof(T) == typeof(ushort)) return TupleParser.TryWriteUInt32(ref writer, (ushort) (object) value!);
				if (typeof(T) == typeof(sbyte)) return TupleParser.TryWriteInt32(ref writer, (sbyte) (object) value!);
				if (typeof(T) == typeof(byte)) return TupleParser.TryWriteUInt32(ref writer, (byte) (object) value!);
				if (typeof(T) == typeof(float)) return TupleParser.TryWriteSingle(ref writer, (float) (object) value!);
				if (typeof(T) == typeof(double)) return TupleParser.TryWriteDouble(ref writer, (double) (object) value!);
				if (typeof(T) == typeof(decimal)) return TupleParser.TryWriteQuadruple(ref writer, (decimal) (object) value!);
				if (typeof(T) == typeof(char)) return TupleParser.TryWriteChar(ref writer, (char) (object) value!);
				if (typeof(T) == typeof(TimeSpan)) return TupleParser.TryWriteTimeSpan(ref writer, (TimeSpan) (object) value!);
				if (typeof(T) == typeof(DateTime)) return TupleParser.TryWriteDateTime(ref writer, (DateTime) (object) value!);
				if (typeof(T) == typeof(DateTimeOffset)) return TupleParser.TryWriteDateTimeOffset(ref writer, (DateTimeOffset) (object) value!);
				if (typeof(T) == typeof(Guid)) return TupleParser.TryWriteGuid(ref writer, (Guid) (object) value!);
				if (typeof(T) == typeof(Uuid128)) return TupleParser.TryWriteUuid128(ref writer, (Uuid128) (object) value!);
				if (typeof(T) == typeof(Uuid96)) return TupleParser.TryWriteUuid96(ref writer, (Uuid96) (object) value!);
				if (typeof(T) == typeof(Uuid80)) return TupleParser.TryWriteUuid80(ref writer, (Uuid80) (object) value!);
				if (typeof(T) == typeof(Uuid64)) return TupleParser.TryWriteUuid64(ref writer, (Uuid64) (object) value!);
				if (typeof(T) == typeof(Uuid48)) return TupleParser.TryWriteUuid48(ref writer, (Uuid48) (object) value!);
				if (typeof(T) == typeof(VersionStamp)) return TupleParser.TryWriteVersionStamp(ref writer, (VersionStamp) (object) value!);
				if (typeof(T) == typeof(Slice)) return TupleParser.TryWriteBytes(ref writer, (Slice) (object) value!);
				if (typeof(T) == typeof(ArraySegment<byte>)) return TupleParser.TryWriteBytes(ref writer, (ArraySegment<byte>) (object) value!);
			}
			else
			{
				if (typeof(T) == typeof(bool?)) return TupleParser.TryWriteBool(ref writer, (bool?) (object) value!);
				if (typeof(T) == typeof(int?)) return TupleParser.TryWriteInt32(ref writer, (int?) (object) value!);
				if (typeof(T) == typeof(long?)) return TupleParser.TryWriteInt64(ref writer, (long?) (object) value!);
				if (typeof(T) == typeof(uint?)) return TupleParser.TryWriteUInt32(ref writer, (uint?) (object) value!);
				if (typeof(T) == typeof(ulong?)) return TupleParser.TryWriteUInt64(ref writer, (ulong?) (object) value!);
				if (typeof(T) == typeof(short?)) return TupleParser.TryWriteInt32(ref writer, (short?) (object) value!);
				if (typeof(T) == typeof(ushort?)) return TupleParser.TryWriteUInt32(ref writer, (ushort?) (object) value!);
				if (typeof(T) == typeof(sbyte?)) return TupleParser.TryWriteInt32(ref writer, (sbyte?) (object) value!);
				if (typeof(T) == typeof(byte?)) return TupleParser.TryWriteUInt32(ref writer, (byte?) (object) value!);
				if (typeof(T) == typeof(float?)) return TupleParser.TryWriteSingle(ref writer, (float?) (object) value!);
				if (typeof(T) == typeof(double?)) return TupleParser.TryWriteDouble(ref writer, (double?) (object) value!);
				if (typeof(T) == typeof(decimal?)) return TupleParser.TryWriteQuadruple(ref writer, (decimal?) (object) value!);
				if (typeof(T) == typeof(char?)) return TupleParser.TryWriteChar(ref writer, (char?) (object) value!);
				if (typeof(T) == typeof(TimeSpan?)) return TupleParser.TryWriteTimeSpan(ref writer, (TimeSpan?) (object) value!);
				if (typeof(T) == typeof(DateTime?)) return TupleParser.TryWriteDateTime(ref writer, (DateTime?) (object) value!);
				if (typeof(T) == typeof(DateTimeOffset?)) return TupleParser.TryWriteDateTimeOffset(ref writer, (DateTimeOffset?) (object) value!);
				if (typeof(T) == typeof(Guid?)) return TupleParser.TryWriteGuid(ref writer, (Guid?) (object) value!);
				if (typeof(T) == typeof(Uuid128?)) return TupleParser.TryWriteUuid128(ref writer, (Uuid128?) (object) value!);
				if (typeof(T) == typeof(Uuid96?)) return TupleParser.TryWriteUuid96(ref writer, (Uuid96?) (object) value!);
				if (typeof(T) == typeof(Uuid80?)) return TupleParser.TryWriteUuid80(ref writer, (Uuid80?) (object) value!);
				if (typeof(T) == typeof(Uuid64?)) return TupleParser.TryWriteUuid64(ref writer, (Uuid64?) (object) value!);
				if (typeof(T) == typeof(Uuid48?)) return TupleParser.TryWriteUuid48(ref writer, (Uuid48?) (object) value!);
				if (typeof(T) == typeof(VersionStamp?)) return TupleParser.TryWriteVersionStamp(ref writer, (VersionStamp?) (object) value!);

				if (typeof(T) == typeof(string)) return TupleParser.TryWriteString(ref writer, (string?) (object?) value);
			}
			//</JIT_HACK>
#endif

			// tuples and packable types write themselves through the instance interface, no reflection
			if (typeof(T).IsAssignableTo(typeof(ITupleSpanPackable)))
			{
				if (value is null)
				{
					return writer.TryWriteNil();
				}
				var packable = (ITupleSpanPackable) value;
				return typeof(T).IsAssignableTo(typeof(IVarTuple))
					? TupleParser.TryBeginTuple(ref writer) && packable.TryPackTo(ref writer) && TupleParser.TryEndTuple(ref writer) // embedded nested tuple
					: packable.TryPackTo(ref writer); // custom packable writes its own items
			}

			// invoke the encoder directly (reflection fallback for the remaining types)
			return TuplePacker<T>.Encoders.Span(ref writer, value);
		}

		/// <summary>Serialize a packable tuple</summary>
		/// <typeparam name="TTuple">Type of the tuple that must implement both <see cref="IVarTuple"/> and <see cref="ITuplePackable"/></typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="tuple">Tuple to pack</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializePackableTupleTo<TTuple>(TupleWriter writer, TTuple? tuple)
			where TTuple : ITuplePackable
		{
			if (tuple is null)
			{
				writer.WriteNil();
			}
			else
			{
				// we are serializing an item of a parent tuple, so we have to start a new embedded tuple!
				var tw = writer.BeginTuple();
				tuple.PackTo(tw);
				tw.EndTuple();
			}
		}

		/// <summary>Serialize a packable tuple</summary>
		/// <typeparam name="TTuple">Type of the tuple that must implement both <see cref="IVarTuple"/> and <see cref="ITuplePackable"/></typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="tuple">Tuple to pack</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TrySerializePackableTupleTo<TTuple>(ref TupleSpanWriter writer, in TTuple? tuple)
			where TTuple : ITupleSpanPackable
		{
			if (tuple is null)
			{
				return writer.TryWriteNil();
			}

			// we are serializing an item of a parent tuple, so we have to start a new embedded tuple!
			return TupleParser.TryBeginTuple(ref writer)
			    && tuple.TryPackTo(ref writer)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void SerializePackableItemsTo<T>(TupleWriter writer, T? value)
			where T : ITuplePackable
		{
			if (value is null)
			{
				writer.WriteNil();
			}
			else
			{
				// we are serializing an item of a tuple, so we have to start a new embedded tuple
				value.PackTo(writer);
			}
		}

		/// <summary>Serialize a nullable value, by checking for null at runtime</summary>
		/// <typeparam name="T">Underling type of the nullable type</typeparam>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Nullable value to serialize</param>
		/// <remarks>Uses the underlying type's serializer if the value is not null</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TrySerializePackableItemsTo<T>(ref TupleSpanWriter writer, in T? value)
			where T : ITupleSpanPackable
		{
			if (value is null)
			{
				return writer.TryWriteNil();
			}

			// we are serializing an item of a tuple, so we have to start a new embedded tuple

			return value.TryPackTo(ref writer);
		}

		/// <summary>Serialize an untyped object, by checking its type at runtime [VERY SLOW]</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>
		/// May throw at runtime if the type is not supported.
		/// This method will be very slow! Please consider using typed tuples instead!
		/// </remarks>
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		public static void SerializeObjectTo(TupleWriter writer, object? value)
		{
			if (value == null)
			{ // null value
				// includes all null references to ref types, as nullables where HasValue == false
				writer.WriteNil();
				return;
			}
			GetBoxedEncoder(value.GetType())(writer, value);
		}

		/// <summary>Serialize an untyped object, by checking its type at runtime [VERY SLOW]</summary>
		/// <param name="writer">Target buffer</param>
		/// <param name="value">Untyped value whose type will be inspected at runtime</param>
		/// <remarks>
		/// May throw at runtime if the type is not supported.
		/// This method will be very slow! Please consider using typed tuples instead!
		/// </remarks>
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		public static bool TrySerializeObjectTo(ref TupleSpanWriter writer, in object? value)
		{
			if (value is null)
			{ // null value
				// includes all null references to ref types, as nullables where HasValue == false
				return writer.TryWriteNil();
			}

			return GetBoxedSpanEncoder(value.GetType())(ref writer, value);
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static Encoder<object> GetBoxedEncoder(Type type)
		{
			var encoders = TuplePackers.BoxedEncoders;
			if (!encoders.TryGetValue(type, out var encoder))
			{
				return GetBoxedEncoderSlow(type);
			}
			return encoder;

			[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
			[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
			static Encoder<object> GetBoxedEncoderSlow(Type type)
			{
				var encoder = CreateBoxedEncoder(type);
				while (true)
				{
					var encoders = TuplePackers.BoxedEncoders;
					var updated = new Dictionary<Type, Encoder<object>>(encoders) { [type] = encoder };

					if (Interlocked.CompareExchange(ref TuplePackers.BoxedEncoders, updated, encoders) == encoders)
					{
						break;
					}
				}
				return encoder;
			}
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static SpanEncoder<object> GetBoxedSpanEncoder(Type type)
		{
			var encoders = TuplePackers.BoxedSpanEncoders;
			if (!encoders.TryGetValue(type, out var encoder))
			{
				return GetBoxedSpanEncoderSlow(type);
			}
			return encoder;

			[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
			[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
			static SpanEncoder<object> GetBoxedSpanEncoderSlow(Type type)
			{
				var encoder = CreateBoxedSpanEncoder(type);
				while (true)
				{
					var encoders = TuplePackers.BoxedSpanEncoders;
					var updated = new Dictionary<Type, SpanEncoder<object>>(encoders) { [type] = encoder };

					if (Interlocked.CompareExchange(ref TuplePackers.BoxedSpanEncoders, updated, encoders) == encoders)
					{
						break;
					}
				}
				return encoder;
			}
		}

		private static Dictionary<Type, Encoder<object>> BoxedEncoders = GetDefaultBoxedEncoders();

		private static Dictionary<Type, SpanEncoder<object>> BoxedSpanEncoders = GetDefaultBoxedSpanEncoders();

		private static Dictionary<Type, Encoder<object>> GetDefaultBoxedEncoders()
		{
			var encoders = new Dictionary<Type, Encoder<object>>(TypeEqualityComparer.Default)
			{
				[typeof(bool)] = static (writer, value) => writer.WriteBool((bool) value!),
				[typeof(bool?)] = static (writer, value) => writer.WriteBool((bool?) value),
				[typeof(char)] = static (writer, value) => writer.WriteChar((char) value!),
				[typeof(char?)] = static (writer, value) => writer.WriteChar((char?) value),
				[typeof(string)] = static (writer, value) => writer.WriteString((string?) value),
				[typeof(sbyte)] = static (writer, value) => writer.WriteInt32((sbyte) value!),
				[typeof(sbyte?)] = static (writer, value) => writer.WriteInt32((sbyte?) value),
				[typeof(short)] = static (writer, value) => writer.WriteInt32((short) value!),
				[typeof(short?)] = static (writer, value) => writer.WriteInt32((short?) value),
				[typeof(int)] = static (writer, value) => writer.WriteInt32((int) value!),
				[typeof(int?)] = static (writer, value) => writer.WriteInt32((int?) value),
				[typeof(long)] = static (writer, value) => writer.WriteInt64((long) value!),
				[typeof(long?)] = static (writer, value) => writer.WriteInt64((long?) value),
				[typeof(byte)] = static (writer, value) => writer.WriteByte((byte) value!),
				[typeof(byte?)] = static (writer, value) => writer.WriteByte((byte?) value),
				[typeof(ushort)] = static (writer, value) => writer.WriteUInt32((ushort) value!),
				[typeof(ushort?)] = static (writer, value) => writer.WriteUInt32((ushort?) value),
				[typeof(uint)] = static (writer, value) => writer.WriteUInt32((uint) value!),
				[typeof(uint?)] = static (writer, value) => writer.WriteUInt32((uint?) value),
				[typeof(ulong)] = static (writer, value) => writer.WriteUInt64((ulong) value!),
				[typeof(ulong?)] = static (writer, value) => writer.WriteUInt64((ulong?) value),
				[typeof(float)] = static (writer, value) => writer.WriteSingle((float) value!),
				[typeof(float?)] = static (writer, value) => writer.WriteSingle((float?) value),
				[typeof(double)] = static (writer, value) => writer.WriteDouble((double) value!),
				[typeof(double?)] = static (writer, value) => writer.WriteDouble((double?) value),
				[typeof(decimal)] = static (writer, value) => writer.WriteQuadruple((decimal) value!),
				[typeof(decimal?)] = static (writer, value) => writer.WriteQuadruple((decimal?) value),
				[typeof(Slice)] = static (writer, value) => writer.WriteBytes((Slice) value!),
				[typeof(byte[])] = static (writer, value) => writer.WriteBytes((byte[]?) value),
				[typeof(Guid)] = static (writer, value) => writer.WriteGuid((Guid) value!),
				[typeof(Guid?)] = static (writer, value) => writer.WriteGuid((Guid?) value),
				[typeof(Uuid128)] = static (writer, value) => writer.WriteUuid128((Uuid128) value!),
				[typeof(Uuid128?)] = static (writer, value) => writer.WriteUuid128((Uuid128?) value),
				[typeof(Uuid96)] = static (writer, value) => writer.WriteUuid96((Uuid96) value!),
				[typeof(Uuid96?)] = static (writer, value) => writer.WriteUuid96((Uuid96?) value),
				[typeof(Uuid80)] = static (writer, value) => writer.WriteUuid80((Uuid80) value!),
				[typeof(Uuid80?)] = static (writer, value) => writer.WriteUuid80((Uuid80?) value),
				[typeof(Uuid64)] = static (writer, value) => writer.WriteUuid64((Uuid64) value!),
				[typeof(Uuid64?)] = static (writer, value) => writer.WriteUuid64((Uuid64?) value),
				[typeof(VersionStamp)] = static (writer, value) => writer.WriteVersionStamp((VersionStamp) value!),
				[typeof(VersionStamp?)] = static (writer, value) => writer.WriteVersionStamp((VersionStamp?) value),
				[typeof(TimeSpan)] = static (writer, value) => writer.WriteTimeSpan((TimeSpan) value!),
				[typeof(TimeSpan?)] = static (writer, value) => writer.WriteTimeSpan((TimeSpan?) value),
				[typeof(DateTime)] = static (writer, value) => writer.WriteDateTime((DateTime) value!),
				[typeof(DateTime?)] = static (writer, value) => writer.WriteDateTime((DateTime?) value),
				[typeof(DateTimeOffset)] = static (writer, value) => writer.WriteDateTimeOffset((DateTimeOffset) value!),
				[typeof(DateTimeOffset?)] = static (writer, value) => writer.WriteDateTimeOffset((DateTimeOffset?) value),
				[typeof(IVarTuple)] = static (writer, value) => SerializeVarTupleTo(writer, (IVarTuple?) value),
				[typeof(DBNull)] = static (writer, _) => writer.WriteNil(),
			};

			return encoders;
		}

		private static Dictionary<Type, SpanEncoder<object>> GetDefaultBoxedSpanEncoders()
		{
			var encoders = new Dictionary<Type, SpanEncoder<object>>(TypeEqualityComparer.Default)
			{
				[typeof(bool)] = static (ref writer, in value) => TupleParser.TryWriteBool(ref writer, (bool) value!),
				[typeof(bool?)] = static (ref writer, in value) => TupleParser.TryWriteBool(ref writer, (bool?) value),
				[typeof(char)] = static (ref writer, in value) => TupleParser.TryWriteChar(ref writer, (char) value!),
				[typeof(char?)] = static (ref writer, in value) => TupleParser.TryWriteChar(ref writer, (char?) value),
				[typeof(string)] = static (ref writer, in value) => TupleParser.TryWriteString(ref writer, (string?) value),
				[typeof(sbyte)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (sbyte) value!),
				[typeof(sbyte?)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (sbyte?) value),
				[typeof(short)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (short) value!),
				[typeof(short?)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (short?) value),
				[typeof(int)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (int) value!),
				[typeof(int?)] = static (ref writer, in value) => TupleParser.TryWriteInt32(ref writer, (int?) value),
				[typeof(long)] = static (ref writer, in value) => TupleParser.TryWriteInt64(ref writer, (long) value!),
				[typeof(long?)] = static (ref writer, in value) => TupleParser.TryWriteInt64(ref writer, (long?) value),
				[typeof(byte)] = static (ref writer, in value) => TupleParser.TryWriteByte(ref writer, (byte) value!),
				[typeof(byte?)] = static (ref writer, in value) => TupleParser.TryWriteByte(ref writer, (byte?) value),
				[typeof(ushort)] = static (ref writer, in value) => TupleParser.TryWriteUInt32(ref writer, (ushort) value!),
				[typeof(ushort?)] = static (ref writer, in value) => TupleParser.TryWriteUInt32(ref writer, (ushort?) value),
				[typeof(uint)] = static (ref writer, in value) => TupleParser.TryWriteUInt32(ref writer, (uint) value!),
				[typeof(uint?)] = static (ref writer, in value) => TupleParser.TryWriteUInt32(ref writer, (uint?) value),
				[typeof(ulong)] = static (ref writer, in value) => TupleParser.TryWriteUInt64(ref writer, (ulong) value!),
				[typeof(ulong?)] = static (ref writer, in value) => TupleParser.TryWriteUInt64(ref writer, (ulong?) value),
				[typeof(float)] = static (ref writer, in value) => TupleParser.TryWriteSingle(ref writer, (float) value!),
				[typeof(float?)] = static (ref writer, in value) => TupleParser.TryWriteSingle(ref writer, (float?) value),
				[typeof(double)] = static (ref writer, in value) => TupleParser.TryWriteDouble(ref writer, (double) value!),
				[typeof(double?)] = static (ref writer, in value) => TupleParser.TryWriteDouble(ref writer, (double?) value),
				[typeof(decimal)] = static (ref writer, in value) => TupleParser.TryWriteQuadruple(ref writer, (decimal) value!),
				[typeof(decimal?)] = static (ref writer, in value) => TupleParser.TryWriteQuadruple(ref writer, (decimal?) value),
				[typeof(Slice)] = static (ref writer, in value) => TupleParser.TryWriteBytes(ref writer, (Slice) value!),
				[typeof(byte[])] = static (ref writer, in value) => TupleParser.TryWriteBytes(ref writer, (byte[]?) value),
				[typeof(Guid)] = static (ref writer, in value) => TupleParser.TryWriteGuid(ref writer, (Guid) value!),
				[typeof(Guid?)] = static (ref writer, in value) => TupleParser.TryWriteGuid(ref writer, (Guid?) value),
				[typeof(Uuid128)] = static (ref writer, in value) => TupleParser.TryWriteUuid128(ref writer, (Uuid128) value!),
				[typeof(Uuid128?)] = static (ref writer, in value) => TupleParser.TryWriteUuid128(ref writer, (Uuid128?) value),
				[typeof(Uuid96)] = static (ref writer, in value) => TupleParser.TryWriteUuid96(ref writer, (Uuid96) value!),
				[typeof(Uuid96?)] = static (ref writer, in value) => TupleParser.TryWriteUuid96(ref writer, (Uuid96?) value),
				[typeof(Uuid80)] = static (ref writer, in value) => TupleParser.TryWriteUuid80(ref writer, (Uuid80) value!),
				[typeof(Uuid80?)] = static (ref writer, in value) => TupleParser.TryWriteUuid80(ref writer, (Uuid80?) value),
				[typeof(Uuid64)] = static (ref writer, in value) => TupleParser.TryWriteUuid64(ref writer, (Uuid64) value!),
				[typeof(Uuid64?)] = static (ref writer, in value) => TupleParser.TryWriteUuid64(ref writer, (Uuid64?) value),
				[typeof(VersionStamp)] = static (ref writer, in value) => TupleParser.TryWriteVersionStamp(ref writer, (VersionStamp) value!),
				[typeof(VersionStamp?)] = static (ref writer, in value) => TupleParser.TryWriteVersionStamp(ref writer, (VersionStamp?) value),
				[typeof(TimeSpan)] = static (ref writer, in value) => TupleParser.TryWriteTimeSpan(ref writer, (TimeSpan) value!),
				[typeof(TimeSpan?)] = static (ref writer, in value) => TupleParser.TryWriteTimeSpan(ref writer, (TimeSpan?) value),
				[typeof(DateTime)] = static (ref writer, in value) => TupleParser.TryWriteDateTime(ref writer, (DateTime) value!),
				[typeof(DateTime?)] = static (ref writer, in value) => TupleParser.TryWriteDateTime(ref writer, (DateTime?) value),
				[typeof(DateTimeOffset)] = static (ref writer, in value) => TupleParser.TryWriteDateTimeOffset(ref writer, (DateTimeOffset) value!),
				[typeof(DateTimeOffset?)] = static (ref writer, in value) => TupleParser.TryWriteDateTimeOffset(ref writer, (DateTimeOffset?) value),
				[typeof(IVarTuple)] = static (ref writer, in value) => TrySerializeVarTupleTo(ref writer, (IVarTuple?) value),
				[typeof(DBNull)] = static (ref writer, in _) => writer.TryWriteNil(),
			};

			return encoders;
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static Encoder<object> CreateBoxedEncoder(Type type)
		{
			var m = typeof(TuplePacker<>).MakeGenericType(type).GetMethod(nameof(TuplePacker<>.SerializeBoxedTo));
			Contract.Debug.Assert(m != null);

			var writer = Expression.Parameter(typeof(TupleWriter), "writer");
			var value = Expression.Parameter(typeof(object), "value");

			var body = Expression.Call(m, writer, value);
			return Expression.Lambda<Encoder<object>>(body, writer, value).Compile();
		}

		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		[RequiresUnreferencedCode("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
		private static SpanEncoder<object> CreateBoxedSpanEncoder(Type type)
		{
			var m = typeof(TuplePacker<>).MakeGenericType(type).GetMethod(nameof(TuplePacker<>.TrySerializeBoxedTo));
			Contract.Debug.Assert(m != null);

			var writer = Expression.Parameter(typeof(TupleSpanWriter).MakeByRefType(), "writer");
			var value = Expression.Parameter(typeof(object).MakeByRefType(), "value");

			var body = Expression.Call(m, writer, value);
			return Expression.Lambda<SpanEncoder<object>>(body, writer, value).Compile();
		}

		/// <summary>Serializes an embedded tuples</summary>
		public static void SerializeVarTupleTo<TTuple>(TupleWriter writer, TTuple? tuple)
			where TTuple : IVarTuple
		{
			if (tuple is null)
			{
				writer.WriteNil();
			}
			else
			{
				var tw = writer.BeginTuple();
				TupleEncoder.WriteTo(tw, tuple);
				tw.EndTuple();
			}
		}

		/// <summary>Serializes an embedded tuples</summary>
		public static bool TrySerializeVarTupleTo<TTuple>(ref TupleSpanWriter writer, in TTuple? tuple)
			where TTuple : IVarTuple
		{
			if (tuple is null)
			{
				return writer.TryWriteNil();
			}
			else
			{
				return TupleParser.TryBeginTuple(ref writer)
				    && TupleEncoder.TryWriteTo(ref writer, tuple)
				    && TupleParser.TryEndTuple(ref writer);
			}
		}

		/// <summary>Serializes a tuple with a single element</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(TupleWriter writer, ValueTuple<T1> tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with a single element</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(ref TupleSpanWriter writer, in ValueTuple<T1> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 2 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(TupleWriter writer, (T1, T2) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 2 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 3 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(TupleWriter writer, (T1, T2, T3) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 3 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2, T3> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 4 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(TupleWriter writer, (T1, T2, T3, T4) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			SerializeTo(tw, tuple.Item4);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 4 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2, T3, T4> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TrySerializeTo(ref writer, tuple.Item4)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 5 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(TupleWriter writer, (T1, T2, T3, T4, T5) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			SerializeTo(tw, tuple.Item4);
			SerializeTo(tw, tuple.Item5);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 5 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2, T3, T4, T5> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TrySerializeTo(ref writer, tuple.Item4)
			    && TrySerializeTo(ref writer, tuple.Item5)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 6 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(TupleWriter writer, (T1, T2, T3, T4, T5, T6) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			SerializeTo(tw, tuple.Item4);
			SerializeTo(tw, tuple.Item5);
			SerializeTo(tw, tuple.Item6);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 6 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2, T3, T4, T5, T6> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TrySerializeTo(ref writer, tuple.Item4)
			    && TrySerializeTo(ref writer, tuple.Item5)
			    && TrySerializeTo(ref writer, tuple.Item6)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 7 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(TupleWriter writer, (T1, T2, T3, T4, T5, T6, T7) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			SerializeTo(tw, tuple.Item4);
			SerializeTo(tw, tuple.Item5);
			SerializeTo(tw, tuple.Item6);
			SerializeTo(tw, tuple.Item7);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 7 elements</summary>
		public static bool TrySerializeValueTupleTo<
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
				[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(ref TupleSpanWriter writer, in ValueTuple<T1, T2, T3, T4, T5, T6, T7> tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TrySerializeTo(ref writer, tuple.Item4)
			    && TrySerializeTo(ref writer, tuple.Item5)
			    && TrySerializeTo(ref writer, tuple.Item6)
			    && TrySerializeTo(ref writer, tuple.Item7)
			    && TupleParser.TryEndTuple(ref writer);
		}

		/// <summary>Serializes a tuple with 8 elements</summary>
		public static void SerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(TupleWriter writer, (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			var tw = writer.BeginTuple();
			SerializeTo(tw, tuple.Item1);
			SerializeTo(tw, tuple.Item2);
			SerializeTo(tw, tuple.Item3);
			SerializeTo(tw, tuple.Item4);
			SerializeTo(tw, tuple.Item5);
			SerializeTo(tw, tuple.Item6);
			SerializeTo(tw, tuple.Item7);
			SerializeTo(tw, tuple.Item8);
			tw.EndTuple();
		}

		/// <summary>Serializes a tuple with 8 elements</summary>
		public static bool TrySerializeValueTupleTo<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(ref TupleSpanWriter writer, in (T1, T2, T3, T4, T5, T6, T7, T8) tuple)
		{
			return TupleParser.TryBeginTuple(ref writer)
			    && TrySerializeTo(ref writer, tuple.Item1)
			    && TrySerializeTo(ref writer, tuple.Item2)
			    && TrySerializeTo(ref writer, tuple.Item3)
			    && TrySerializeTo(ref writer, tuple.Item4)
			    && TrySerializeTo(ref writer, tuple.Item5)
			    && TrySerializeTo(ref writer, tuple.Item6)
			    && TrySerializeTo(ref writer, tuple.Item7)
			    && TrySerializeTo(ref writer, tuple.Item8)
			    && TupleParser.TryEndTuple(ref writer);
		}

		#endregion

		#region Deserializers...

		/// <summary>Delegate that decodes a value of type <typeparamref name="T"/> from encoded bytes</summary>
		public delegate T Decoder<out T>(ReadOnlySpan<byte> packed);

		private static readonly FrozenDictionary<Type, Delegate> WellKnownUnpackers = InitializeDefaultUnpackers();

		private static FrozenDictionary<Type, Delegate> InitializeDefaultUnpackers()
		{
			var map = new Dictionary<Type, Delegate>
			{
				[typeof(Slice)] = new Decoder<Slice>(DeserializeSlice),
				[typeof(byte[])] = new Decoder<byte[]?>(DeserializeBytes),
				[typeof(bool)] = new Decoder<bool>(DeserializeBoolean),
				[typeof(string)] = new Decoder<string?>(DeserializeString),
				[typeof(char)] = new Decoder<char>(DeserializeChar),
				[typeof(sbyte)] = new Decoder<sbyte>(DeserializeSByte),
				[typeof(short)] = new Decoder<short>(DeserializeInt16),
				[typeof(int)] = new Decoder<int>(DeserializeInt32),
				[typeof(long)] = new Decoder<long>(DeserializeInt64),
				[typeof(byte)] = new Decoder<byte>(DeserializeByte),
				[typeof(ushort)] = new Decoder<ushort>(DeserializeUInt16),
				[typeof(uint)] = new Decoder<uint>(DeserializeUInt32),
				[typeof(ulong)] = new Decoder<ulong>(DeserializeUInt64),
				[typeof(float)] = new Decoder<float>(DeserializeSingle),
				[typeof(double)] = new Decoder<double>(DeserializeDouble),
				//[typeof(decimal)] = new Decoder<decimal>(TuplePackers.DeserializeDecimal), //TODO: not implemented
				[typeof(Guid)] = new Decoder<Guid>(DeserializeGuid),
				[typeof(Uuid128)] = new Decoder<Uuid128>(DeserializeUuid128),
				[typeof(Uuid96)] = new Decoder<Uuid96>(DeserializeUuid96),
				[typeof(Uuid80)] = new Decoder<Uuid80>(DeserializeUuid80),
				[typeof(Uuid64)] = new Decoder<Uuid64>(DeserializeUuid64),
				[typeof(Uuid48)] = new Decoder<Uuid48>(DeserializeUuid48),
				[typeof(TimeSpan)] = new Decoder<TimeSpan>(DeserializeTimeSpan),
				[typeof(DateTime)] = new Decoder<DateTime>(DeserializeDateTime),
				[typeof(DateTimeOffset)] = new Decoder<DateTimeOffset>(DeserializeDateTimeOffset),
				[typeof(System.Net.IPAddress)] = new Decoder<System.Net.IPAddress?>(DeserializeIpAddress),
				[typeof(VersionStamp)] = new Decoder<VersionStamp>(DeserializeVersionStamp),
				[typeof(IVarTuple)] = new Decoder<IVarTuple?>(DeserializeTuple),
				[typeof(TuPackUserType)] = new Decoder<TuPackUserType?>(DeserializeUserType),
				[typeof(BigInteger)] = new Decoder<BigInteger>(DeserializeBigInteger),
#if NET8_0_OR_GREATER
				[typeof(Int128)] = new Decoder<Int128>(DeserializeInt128),
				[typeof(UInt128)] = new Decoder<UInt128>(DeserializeUInt128),
#endif

				// nullables

				[typeof(bool?)] = new Decoder<bool?>(DeserializeBooleanNullable),
				[typeof(char?)] = new Decoder<char?>(DeserializeCharNullable),
				[typeof(sbyte?)] = new Decoder<sbyte?>(DeserializeSByteNullable),
				[typeof(short?)] = new Decoder<short?>(DeserializeInt16Nullable),
				[typeof(int?)] = new Decoder<int?>(DeserializeInt32Nullable),
				[typeof(long?)] = new Decoder<long?>(DeserializeInt64Nullable),
				[typeof(byte?)] = new Decoder<byte?>(DeserializeByteNullable),
				[typeof(ushort?)] = new Decoder<ushort?>(DeserializeUInt16Nullable),
				[typeof(uint?)] = new Decoder<uint?>(DeserializeUInt32Nullable),
				[typeof(ulong?)] = new Decoder<ulong?>(DeserializeUInt64Nullable),
				[typeof(float?)] = new Decoder<float?>(DeserializeSingleNullable),
				[typeof(double?)] = new Decoder<double?>(DeserializeDoubleNullable),
				//[typeof(decimal?)] = new Decoder<decimal?>(TuplePackers.DeserializeDecimalNullable), //TODO: not implemented
				[typeof(Guid?)] = new Decoder<Guid?>(DeserializeGuidNullable),
				[typeof(Uuid128?)] = new Decoder<Uuid128?>(DeserializeUuid128Nullable),
				[typeof(Uuid96?)] = new Decoder<Uuid96?>(DeserializeUuid96Nullable),
				[typeof(Uuid80?)] = new Decoder<Uuid80?>(DeserializeUuid80Nullable),
				[typeof(Uuid64?)] = new Decoder<Uuid64?>(DeserializeUuid64Nullable),
				[typeof(Uuid48?)] = new Decoder<Uuid48?>(DeserializeUuid48Nullable),
				[typeof(TimeSpan?)] = new Decoder<TimeSpan?>(DeserializeTimeSpanNullable),
				[typeof(DateTime?)] = new Decoder<DateTime?>(DeserializeDateTimeNullable),
				[typeof(DateTimeOffset?)] = new Decoder<DateTimeOffset?>(DeserializeDateTimeOffsetNullable),
				[typeof(VersionStamp?)] = new Decoder<VersionStamp?>(DeserializeVersionStampNullable),
				[typeof(BigInteger?)] = new Decoder<BigInteger?>(DeserializeBigIntegerNullable),
#if NET8_0_OR_GREATER
				[typeof(Int128?)] = new Decoder<Int128?>(DeserializeInt128Nullable),
				[typeof(UInt128?)] = new Decoder<UInt128?>(DeserializeUInt128Nullable),
#endif

			};

			return map.ToFrozenDictionary();
		}

		/// <summary>Returns a lambda that will be able to serialize values of type <typeparamref name="T"/></summary>
		/// <typeparam name="T">Type of values to serialize</typeparam>
		/// <returns>Reusable action that knows how to serialize values of type <typeparamref name="T"/> into binary buffers, or an exception if the type is not supported</returns>
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		internal static Decoder<T> GetDeserializer<T>(bool required)
		{
			Type type = typeof(T);

			if (WellKnownUnpackers.TryGetValue(type, out var decoder))
			{ // We already know how to decode this type
				return (Decoder<T>) decoder;
			}

			// Nullable<T>
			var underlyingType = Nullable.GetUnderlyingType(typeof(T));
			if (underlyingType != null && WellKnownUnpackers.TryGetValue(underlyingType, out decoder))
			{ 
				return (Decoder<T>) MakeNullableDeserializer(type, underlyingType, decoder);
			}

			// STuple<...>
			if (typeof(IVarTuple).IsAssignableFrom(type))
			{
				if (type.IsValueType && type.IsGenericType && type.Name.StartsWith(nameof(STuple) + "`", StringComparison.Ordinal))
				{
					return (Decoder<T>) MakeSTupleDeserializer(type);
				}
			}

			if ((type.Name == nameof(ValueTuple) || type.Name.StartsWith(nameof(ValueTuple) + "`", StringComparison.Ordinal)) && type.Namespace == "System")
			{
				return (Decoder<T>) MakeValueTupleDeserializer(type);
			}

			if (required)
			{ // will throw at runtime
				return MakeNotSupportedDeserializer<T>();
			}
			// when all else fails...
			return MakeConvertBoxedDeserializer<T>();
		}

		[Pure]
		private static Decoder<T> MakeNotSupportedDeserializer<T>()
		{
			return (_) => throw new InvalidOperationException($"Does not know how to deserialize keys into values of type {typeof(T).GetFriendlyName()}");
		}

		[Pure]
		private static Decoder<T> MakeConvertBoxedDeserializer<T>()
		{
			return (value) => TypeConverters.ConvertBoxed<T>(DeserializeBoxed(value))!;
		}

		/// <summary>Checks if a tuple segment is the equivalent of 'Nil'</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsNilSegment(ReadOnlySpan<byte> slice)
		{
			return slice.Length == 0 || slice[0] == TupleTypes.Nil;
		}

#pragma warning disable IL2026
		
		// this only exists so that we can add this attribute for AoT
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
		private static Type GetTuplePackersType() => typeof(TuplePackers);
		
#pragma warning restore IL2026

		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static Delegate MakeNullableDeserializer(Type nullableType, Type type, Delegate decoder)
		{
			Contract.Debug.Requires(nullableType != null && type != null && decoder != null);
			// We have a Decoder of T, but we have to transform it into a Decoder for Nullable<T>, which returns null if the slice is "nil", or falls back to the underlying decoder if the slice contains something

			var prmSlice = Expression.Parameter(typeof(ReadOnlySpan<byte>), "slice");
			var body = Expression.Condition(
				// IsNilSegment(slice) ?
				Expression.Call(GetTuplePackersType().GetMethod(nameof(IsNilSegment), BindingFlags.Static | BindingFlags.NonPublic)!, prmSlice),
				// True => default(Nullable<T>)
				Expression.Default(nullableType),
				// False => decoder(slice)
				Expression.Convert(Expression.Invoke(Expression.Constant(decoder), prmSlice), nullableType)
			);

			return Expression.Lambda(body, prmSlice).Compile();
		}

		private static Dictionary<int, MethodInfo> STupleCandidateMethods = ComputeSTupleCandidateMethods();

		private static Dictionary<int, MethodInfo> ComputeSTupleCandidateMethods() => GetTuplePackersType()
			.GetMethods()
			.Where(m => m.Name == nameof(DeserializeTuple) && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
			.ToDictionary(m => m.GetGenericArguments().Length);

		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static Delegate MakeSTupleDeserializer(Type type)
		{
			Contract.Debug.Requires(type != null);

			// (slice) => TuPack.DeserializeTuple<T...>(slice)

			var typeArgs = type.GetGenericArguments();
			if (!STupleCandidateMethods.TryGetValue(typeArgs.Length, out var method))
			{
				throw new InvalidOperationException($"There is no method able to deserialize a tuple with {typeArgs.Length} arguments!");
			}
			method = method.MakeGenericMethod(typeArgs);

			var prmSlice = Expression.Parameter(typeof(ReadOnlySpan<byte>), "slice");
			var body = Expression.Call(method, prmSlice);

			var fnType = typeof(Decoder<>).MakeGenericType(type);

			return Expression.Lambda(fnType, body, prmSlice).Compile();
		}

		private static Dictionary<int, MethodInfo> ValueTupleCandidateMethods = ComputeValueTupleCandidateMethods();

		private static Dictionary<int, MethodInfo> ComputeValueTupleCandidateMethods() => GetTuplePackersType()
			.GetMethods()
			.Where(m => m.Name == nameof(DeserializeValueTuple) && m.IsGenericMethod && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(ReadOnlySpan<byte>))
			.ToDictionary(m => m.GetGenericArguments().Length);

		[Pure]
		[RequiresDynamicCode(AotMessages.RequiresDynamicCode)]
		private static Delegate MakeValueTupleDeserializer(Type type)
		{
			Contract.Debug.Requires(type != null);

			// (slice) => TuPack.DeserializeValueTuple<T...>(slice)

			var typeArgs = type.GetGenericArguments();
			if (!ValueTupleCandidateMethods.TryGetValue(typeArgs.Length, out var method))
			{
				throw new InvalidOperationException($"There is no method able to deserialize a tuple with {typeArgs.Length} arguments!");
			}
			method = method.MakeGenericMethod(typeArgs);

			var prmSlice = Expression.Parameter(typeof(ReadOnlySpan<byte>), "slice");
			var body = Expression.Call(method, prmSlice);

			var fnType = typeof(Decoder<>).MakeGenericType(type);

			return Expression.Lambda(fnType, body, prmSlice).Compile();
		}

		/// <summary>Deserializes a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large integers as Int64 or even UInt64.</remarks>
		public static object? DeserializeBoxed(Slice slice) => DeserializeBoxed(slice.Span);

		// to reduce boxing, we pre-allocate some well known singletons
		private static readonly object FalseSingleton = false;
		private static readonly object TrueSingleton = true;
		private static readonly object ZeroSingleton = 0;
		private static readonly object OneSingleton = 1;
		private static readonly object TwoSingleton = 2;
		private static readonly object ThreeSingleton = 3;
		private static readonly object FourSingleton = 4;

		/// <summary>Deserializes a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large integers as Int64 or even UInt64.</remarks>
		public static object? DeserializeBoxed(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8)
				{
					long l = TupleParser.ParseInt64(type, slice);
					return l switch
					{
						0 => ZeroSingleton,
						1 => OneSingleton,
						2 => TwoSingleton,
						3 => ThreeSingleton,
						4 => FourSingleton,
						_ => l
					};
				}

				switch (type)
				{
					case TupleTypes.Nil: return null;
					case TupleTypes.Bytes: return TupleParser.ParseBytes(slice);
					case TupleTypes.Utf8: return TupleParser.ParseUtf8(slice);
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple: return TupleParser.ParseEmbeddedTuple(slice).ToTuple();
					case TupleTypes.NegativeBigInteger: return TupleParser.ParseNegativeBigInteger(slice);
				}
			}
			else
			{
				switch (type)
				{
					case TupleTypes.PositiveBigInteger: return TupleParser.ParsePositiveBigInteger(slice);
					case TupleTypes.Single: return TupleParser.ParseSingle(slice);
					case TupleTypes.Double: return TupleParser.ParseDouble(slice);
					//TODO: Triple
					case TupleTypes.Quadruple: return TupleParser.ParseQuadruple(slice);
					case TupleTypes.False: return FalseSingleton;
					case TupleTypes.True: return TrueSingleton;
					case TupleTypes.Uuid128: return TupleParser.ParseGuid(slice);
					case TupleTypes.Uuid64: return TupleParser.ParseUuid64(slice);
					case TupleTypes.Uuid80: return TupleParser.ParseVersionStamp(slice);
					case TupleTypes.Uuid96: return TupleParser.ParseVersionStamp(slice);

					case TupleTypes.Directory:
					{
						// should only be one byte (followed by other tuple segments)
						if (slice.Length == 1) return TuPackUserType.Directory;
						break;
					}
					case TupleTypes.Escape:
					{
						return slice.Length == 1
							? TuPackUserType.System
							: TuPackUserType.SystemKey(slice[1..].ToSlice());
					}
				}
			}

			throw new FormatException($"Cannot convert tuple segment with unknown type code 0x{type:X}");
		}

		/// <summary>Deserializes a packed element into an object by choosing the most appropriate type at runtime</summary>
		/// <param name="sb">Destination buffer</param>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>Decoded element, in the type that is the best fit.</returns>
		/// <remarks>You should avoid working with untyped values as much as possible! Blindly casting the returned object may be problematic because this method may need to return very large integers as Int64 or even UInt64.</remarks>
		public static void StringifyBoxedTo(ref FastStringBuilder sb, ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0)
			{
				sb.Append("null"); // or is it?
				return;
			}

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8)
				{
					STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseInt64(type, slice));
					return;
				}

				switch (type)
				{
					case TupleTypes.Nil:
					{
						sb.Append("null"); // or is it?
						return;
					}
					case TupleTypes.Bytes:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseBytes(slice));
						return;
					}
					case TupleTypes.Utf8:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseUtf8(slice)); //TODO: use a stackalloc'ed buffer?
						return;
					}
					case TupleTypes.LegacyTupleStart:
					{
						throw TupleParser.FailLegacyTupleNotSupported();
					}
					case TupleTypes.EmbeddedTuple:
					{
						var t = TupleParser.ParseEmbeddedTuple(slice); //.ToTuple();
						if (t.Count == 0)
						{
							sb.Append("()");
						}
						else
						{
							sb.Append('(');
							if (t.AppendItemsTo(ref sb) == 1)
							{
								sb.Append(",)");
							}
							else
							{
								sb.Append(')');
							}
						}
						return;
					}
					case TupleTypes.NegativeBigInteger:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseNegativeBigInteger(slice));
						return;
					}
				}
			}
			else
			{
				switch (type)
				{
					case TupleTypes.PositiveBigInteger:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParsePositiveBigInteger(slice));
						return;
					}
					case TupleTypes.Single:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseSingle(slice));
						return;
					}
					case TupleTypes.Double:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseDouble(slice));
						return;
					}
					//TODO: Triple
					case TupleTypes.Quadruple:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseQuadruple(slice));
						return;
					}
					case TupleTypes.False:
					{
						sb.Append("false");
						return;
					}
					case TupleTypes.True:
					{
						sb.Append("true");
						return;
					}
					case TupleTypes.Uuid128:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseGuid(slice));
						return;
					}
					case TupleTypes.Uuid64:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseUuid64(slice));
						return;
					}
					case TupleTypes.Uuid80:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseVersionStamp(slice));
						return;
					}
					case TupleTypes.Uuid96:
					{
						STuple.Formatter.StringifyTo(ref sb, TupleParser.ParseVersionStamp(slice));
						return;
					}

					case TupleTypes.Directory:
					{
						if (slice.Length == 1)
						{
							sb.Append("|Directory|");
							return;
						}
						break;
					}
					case TupleTypes.Escape:
					{
						if (slice.Length == 1)
						{
							sb.Append("|System|");
						}
						else
						{
							sb.Append("|System:");
							sb.Append(Slice.Dump(slice[1..]));
							sb.Append('|');
						}
						return;
					}
				}
			}

			throw new FormatException($"Cannot convert tuple segment with unknown type code 0x{type:X}");
		}

		/// <summary>Deserializes a tuple segment into a Slice</summary>
		public static Slice DeserializeSlice(Slice slice)
		{
			// Convert the tuple value into a sensible Slice representation.
			// The behavior should be equivalent to calling the corresponding Slice.From{TYPE}(TYPE value)

			if (slice.IsNullOrEmpty) return Slice.Nil; //TODO: fail ?

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil: return Slice.Nil;
				case TupleTypes.Bytes: return TupleParser.ParseBytes(slice);
				case TupleTypes.Utf8: return Slice.FromString(TupleParser.ParseUtf8(slice.Span));

				case TupleTypes.Single: return Slice.FromSingle(TupleParser.ParseSingle(slice.Span));
				case TupleTypes.Double: return Slice.FromDouble(TupleParser.ParseDouble(slice.Span));
				//TODO: triple
				case TupleTypes.Quadruple: return Slice.FromDecimal(TupleParser.ParseQuadruple(slice.Span));

				case TupleTypes.Uuid128: return Slice.FromGuid(TupleParser.ParseGuid(slice.Span));
				case TupleTypes.Uuid64: return Slice.FromUuid64(TupleParser.ParseUuid64(slice.Span));
					
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8: return type >= TupleTypes.IntZero
					? Slice.FromInt64(DeserializeInt64(slice.Span))
					: Slice.FromUInt64(DeserializeUInt64(slice.Span));

				default: throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Slice");
			}
		}

		/// <summary>Deserializes a tuple segment into a Slice</summary>
		public static bool TryDeserializeSlice(ReadOnlySpan<byte> slice, out Slice value)
		{
			// Convert the tuple value into a sensible Slice representation.
			// The behavior should be equivalent to calling the corresponding Slice.From{TYPE}(TYPE value)

			if (slice.Length == 0)
			{
				value = default; //TODO: fail ?
				return true;
			}

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					value = Slice.Nil;
					return true;
				}
				case TupleTypes.Bytes:
				{
					return TupleParser.TryParseBytes(slice, out value);
				}
				case TupleTypes.Utf8:
				{
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
						//PERF: if the string is "clean" we could simply copy the UTF-8 bytes as-is ?
						value = Slice.FromString(str);
						return true;
					}
					break;
				}
				case TupleTypes.Single:
				{
					if (TupleParser.TryParseSingle(slice, out var f))
					{
						value = Slice.FromSingle(f);
						return true;
					}
					break;
				}
				case TupleTypes.Double:
				{
					if (TupleParser.TryParseDouble(slice, out var d))
					{
						value = Slice.FromDouble(d);
						return true;
					}
					break;
				}
				//TODO: triple
				case TupleTypes.Quadruple:
				{
					if (TupleParser.TryParseQuadruple(slice, out var d))
					{
						value = Slice.FromDecimal(d);
						return true;
					}
					break;
				}
				case TupleTypes.Uuid128:
				{
					if (TupleParser.TryParseGuid(slice, out var g))
					{
						value = Slice.FromGuid(g);
						return true;
					}
					break;
				}
				case TupleTypes.Uuid64:
				{
					if (TupleParser.TryParseUuid64(slice, out var g))
					{
						value = Slice.FromUuid64(g);
						return true;
					}
					break;
				}
				case >= TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{
					if (TryDeserializeUInt64(slice, out var ul))
					{
						value = Slice.FromUInt64(ul);
					}
					break;
				}
				case >= TupleTypes.IntNeg8:
				{
					if (TryDeserializeInt64(slice, out var l))
					{
						value = Slice.FromInt64(l);
					}
					break;
				}
			}

			value = default;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a Slice</summary>
		public static Slice DeserializeSlice(ReadOnlySpan<byte> slice)
		{
			// Convert the tuple value into a sensible Slice representation.
			// The behavior should be equivalent to calling the corresponding Slice.From{TYPE}(TYPE value)

			if (slice.Length == 0) return Slice.Nil; //TODO: fail ?

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil: return Slice.Nil;
				case TupleTypes.Bytes: return TupleParser.ParseBytes(slice);
				case TupleTypes.Utf8: return Slice.FromString(TupleParser.ParseUtf8(slice));

				case TupleTypes.Single: return Slice.FromSingle(TupleParser.ParseSingle(slice));
				case TupleTypes.Double: return Slice.FromDouble(TupleParser.ParseDouble(slice));
				//TODO: triple
				case TupleTypes.Quadruple: return Slice.FromDecimal(TupleParser.ParseQuadruple(slice));

				case TupleTypes.Uuid128: return Slice.FromGuid(TupleParser.ParseGuid(slice));
				case TupleTypes.Uuid64: return Slice.FromUuid64(TupleParser.ParseUuid64(slice));
				
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					return type < TupleTypes.IntZero
						? Slice.FromInt64(DeserializeInt64(slice))
						: Slice.FromUInt64(DeserializeUInt64(slice));
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Slice");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a byte array</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)] //REVIEW: because of Slice.GetBytes()
		public static byte[] DeserializeBytes(ReadOnlySpan<byte> slice)
		{
			//note: DeserializeSlice(RoS<byte>) already creates a copy, hopefully with the correct size, so we can expose it safely
			var decoded = DeserializeSlice(slice);
			return SliceMarshal.GetBytesOrCopy(decoded);
		}

		/// <summary>Deserializes a tuple segment into a custom tuple type</summary>
		public static TuPackUserType? DeserializeUserType(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null; //TODO: fail ?

			int type = slice[0];
			if (slice.Length == 1)
			{
				return type switch
				{
					TupleTypes.Nil => null,
					TupleTypes.Directory => TuPackUserType.Directory,
					TupleTypes.Escape => TuPackUserType.System,
					_ => new(type)
				};
			}

			return new(type, slice[1..].ToSlice());
		}

		/// <summary>Deserializes a tuple segment into a tuple</summary>
		public static IVarTuple? DeserializeTuple(Slice slice)
		{
			if (slice.IsNullOrEmpty) return null;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
				case TupleTypes.Bytes:
				{
					return TuPack.Unpack(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
				case TupleTypes.EmbeddedTuple:
				{
					return TupleParser.ParseEmbeddedTuple(slice);
				}
				default:
				{
					throw new FormatException("Cannot convert tuple segment into a Tuple");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a tuple</summary>
		public static bool TryDeserializeTuple(ReadOnlySpan<byte> slice, out SpanTuple value)
		{
			if (slice.Length == 0)
			{
				value = SpanTuple.Empty;
				return true;
			}

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					value = SpanTuple.Empty;
					return true;
				}
				case TupleTypes.Bytes:
				{
					if (TupleParser.TryParseBytes(slice, out var bytes))
					{
						return TuPack.TryUnpack(bytes.Span, out value);
					}
					break;
				}
				case TupleTypes.LegacyTupleStart:
				{
					value = default; // not supported anymore
					return false;
				}
				case TupleTypes.EmbeddedTuple:
				{
					return TupleParser.TryParseEmbeddedTuple(slice, out value);
				}
			}

			value = default;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a tuple</summary>
		public static IVarTuple? DeserializeTuple(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			byte type = slice[0];
			return type switch
			{
				TupleTypes.Nil => null,
				TupleTypes.Bytes => TuPack.Unpack(TupleParser.ParseBytes(slice)),
				TupleTypes.LegacyTupleStart => throw TupleParser.FailLegacyTupleNotSupported(),
				TupleTypes.EmbeddedTuple => TupleParser.ParseEmbeddedTuple(slice).ToTuple(),
				_ => throw new FormatException("Cannot convert tuple segment into a Tuple")
			};
		}

		/// <summary>Deserializes a slice containing a tuple composed of a single element</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 2 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 3 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2, T3>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 4 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2, T3, T4>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 5 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2, T3, T4, T5>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 6 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2, T3, T4, T5, T6>(slice);

		/// <summary>Deserializes a slice containing a tuple composed of 7 elements</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static STuple<T1?, T2?, T3?, T4?, T5?, T6?, T7?> DeserializeTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(ReadOnlySpan<byte> slice)
			=> DeserializeValueTuple<T1, T2, T3, T4, T5, T6, T7>(slice);

		//note: there is no STuple<...> with 8 generic arguments !

		/// <summary>Deserializes a slice containing a tuple composed of a single element</summary>
		[Pure]
		public static ValueTuple<T1?> DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1>
			(ReadOnlySpan<byte> slice)
		{
			ValueTuple<T1?> res = default;
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a slice containing a tuple composed of 2 elements</summary>
		[Pure]
		public static ValueTuple<T1?, T2?> DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2>
			(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?>);
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a slice containing a tuple composed of 3 elements</summary>
		[Pure]
		public static ValueTuple<T1?, T2?, T3?> DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3>
			(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?>);
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;

		}

		/// <summary>Deserializes a slice containing a tuple composed of 4 elements</summary>
		[Pure]
		public static ValueTuple<T1?, T2?, T3?, T4?> DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4>
			(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?, T4?>);
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;

		}

		/// <summary>Deserializes a slice containing a tuple composed of 5 elements</summary>
		[Pure]
		public static ValueTuple<T1?, T2?, T3?, T4?, T5?> DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5>
			(ReadOnlySpan<byte> slice)
		{
			var res = default(ValueTuple<T1?, T2?, T3?, T4?, T5?>);
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a slice containing a tuple composed of 6 elements</summary>
		[Pure]
		public static (T1?, T2?, T3?, T4?, T5?, T6?) DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6>
			(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?));
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a slice containing a tuple composed of 7 elements</summary>
		[Pure]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?) DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7>
			(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?, T7?));
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a slice containing a tuple composed of 8 elements</summary>
		[Pure]
		public static (T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?) DeserializeValueTuple<
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T1,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T2,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T3,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T4,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T5,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T6,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T7,
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T8>
			(ReadOnlySpan<byte> slice)
		{
			var res = default((T1?, T2?, T3?, T4?, T5?, T6?, T7?, T8?));
			if (slice.Length != 0)
			{
				switch (slice[0])
				{
					case TupleTypes.Nil:
					{
						break;
					}
					case TupleTypes.Bytes:
					{
						var bytes = TupleParser.ParseBytes(slice);
						var reader = new TupleReader(bytes.Span);
						//note: depth is 0 since the embedded tuple was encoded as top-level
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					case TupleTypes.LegacyTupleStart: throw TupleParser.FailLegacyTupleNotSupported();
					case TupleTypes.EmbeddedTuple:
					{
						// extract the embedded tuple, and resume parsing
						var reader = TupleReader.UnpackEmbeddedTuple(slice);
						TupleEncoder.DecodeKey(ref reader, out res);
						break;
					}
					default:
					{
						throw new FormatException($"Cannot convert tuple segment into a {res.GetType().Name}");
					}
				}
			}
			return res;
		}

		/// <summary>Deserializes a tuple segment into a <see cref="Boolean"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static bool DeserializeBoolean(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return false; //TODO: fail ?

			byte type = slice[0];

			// Booleans are usually encoded as integers, with 0 for False (<14>) and 1 for True (<15><01>)
			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				//note: DeserializeInt64 handles most cases
				return 0 != DeserializeInt64(slice);
			}

			switch (type)
			{
				case TupleTypes.Nil:
				{ // null is false
					return false;
				}
				case TupleTypes.Bytes:
				{ // empty is false, all other is true
					return slice.Length != 2; // <01><00>
				}
				case TupleTypes.Utf8:
				{// empty is false, all other is true
					return slice.Length != 2; // <02><00>
				}
				case TupleTypes.Single:
				{
					//TODO: should NaN considered to be false ?
					//=> it is the "null" of the floats, so if we do, 'null' should also be considered false
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					return 0f != TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					//TODO: should NaN considered to be false ?
					//=> it is the "null" of the floats, so if we do, 'null' should also be considered false
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					return 0d != TupleParser.ParseDouble(slice);
				}
				//TODO: triple
				case TupleTypes.Quadruple:
				{
					return 0m != TupleParser.ParseQuadruple(slice);
				}
				case TupleTypes.False:
				{
					return false;
				}
				case TupleTypes.True:
				{
					return true;
				}
			}

			//TODO: should we handle weird cases like strings "True" and "False"?

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a boolean");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Boolean"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool? DeserializeBooleanNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeBoolean(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="SByte"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static sbyte DeserializeSByte(ReadOnlySpan<byte> slice) => checked((sbyte) DeserializeInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="SByte"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static sbyte? DeserializeSByteNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeSByte(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="Int16"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static short DeserializeInt16(ReadOnlySpan<byte> slice) => checked((short) DeserializeInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Int16"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static short? DeserializeInt16Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt16(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="Int32"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static int DeserializeInt32(ReadOnlySpan<byte> slice) => checked((int) DeserializeInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Int32"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int? DeserializeInt32Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt32(slice) : null;

		/// <summary>Tests if the slice corresponding to the encoding of a negative integer</summary>
		public static bool IsNegativeInteger(ReadOnlySpan<byte> slice) => slice.Length != 0 && (slice[0] is >= TupleTypes.IntNeg8 and <= TupleTypes.IntNeg1);

		/// <summary>Tests if the slice corresponding to the encoding of a positive integer (including 0)</summary>
		public static bool IsPositiveInteger(ReadOnlySpan<byte> slice) => slice.Length != 0 && (slice[0] is >= TupleTypes.IntZero and <= TupleTypes.IntPos8);

		/// <summary>Deserializes a tuple segment into an Int64, if possible</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeInt64(ReadOnlySpan<byte> slice, out long value)
		{
			value = 0;

			if (slice.Length == 0)
			{
				return true; //TODO: fail ?
			}

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8)
				{
					return TupleParser.TryParseInt64(type, slice, out value);
				}

				switch (type)
				{
					case TupleTypes.Nil:
					{
						return true;
					}
					case TupleTypes.Bytes:
					{
#if NET7_0_OR_GREATER
						return TupleParser.TryParseAscii(slice, out var str) && long.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return TupleParser.TryParseAscii(slice, out var str) && long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
					}
					case TupleTypes.Utf8:
					{
#if NET7_0_OR_GREATER
						return TupleParser.TryParseUtf8(slice, out var str) && long.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return TupleParser.TryParseUtf8(slice, out var str) && long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
					}
				}
			}

			return false;
		}

		/// <summary>Deserializes a tuple segment into an Int64</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static long DeserializeInt64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0L; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntNeg8) return TupleParser.ParseInt64(type, slice);

				switch (type)
				{
					case TupleTypes.Nil:
					{
						return 0;
					}
					case TupleTypes.Bytes:
					{
#if NET8_0_OR_GREATER
						if (!long.TryParse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture, out var result))
						{
							throw new FormatException("Cannot convert tuple segment of type Bytes (0x01) into a signed integer");
						}
						return result;
#else
						return long.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
#endif
					}
					case TupleTypes.Utf8:
					{
#if NET8_0_OR_GREATER
						if (!long.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
						{
							throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a signed integer");
						}
						return result;
#else
						return long.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
#endif
					}
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a signed integer");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Int64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long? DeserializeInt64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt64(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="Byte"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static byte DeserializeByte(ReadOnlySpan<byte> slice) => checked((byte) DeserializeUInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Byte"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static byte? DeserializeByteNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeByte(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="UInt16"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ushort DeserializeUInt16(ReadOnlySpan<byte> slice) => checked((ushort) DeserializeUInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="UInt16"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ushort? DeserializeUInt16Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt16(slice) : null;

		/// <summary>Deserializes a slice into an <see cref="UInt32"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static uint DeserializeUInt32(ReadOnlySpan<byte> slice) => checked((uint) DeserializeUInt64(slice));

		/// <summary>Deserializes a tuple segment into a nullable <see cref="UInt32"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static uint? DeserializeUInt32Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt32(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="UInt64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeUInt64(ReadOnlySpan<byte> slice, out ulong value)
		{
			value = 0;

			if (slice.Length == 0)
			{
				return true; //TODO: fail ?
			}

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntZero)
				{
					return TupleParser.TryParseUInt64(type, slice, out value);
				}
				if (type >= TupleTypes.IntNeg8)
				{
					return false; // overflow!
				}

				switch (type)
				{
					case TupleTypes.Nil:
					{
						return true;
					}
					case TupleTypes.Bytes:
					{
#if NET7_0_OR_GREATER
						return TupleParser.TryParseAscii(slice, out var str) && ulong.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return TupleParser.TryParseAscii(slice, out var str) && ulong.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
					}
					case TupleTypes.Utf8:
					{
#if NET7_0_OR_GREATER
						return TupleParser.TryParseUtf8(slice, out var str) && ulong.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return TupleParser.TryParseUtf8(slice, out var str) && ulong.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
					}
				}
			}

			return false;
		}

		/// <summary>Deserializes a tuple segment into an <see cref="UInt64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static ulong DeserializeUInt64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0UL; //TODO: fail ?

			int type = slice[0];
			if (type <= TupleTypes.IntPos8)
			{
				if (type >= TupleTypes.IntZero) return checked((ulong) TupleParser.ParseInt64(type, slice));
				if (type >= TupleTypes.IntNeg8) throw new OverflowException(); // negative values

				switch (type)
				{
					case TupleTypes.Nil: return 0;
					case TupleTypes.Bytes: return ulong.Parse(TupleParser.ParseAscii(slice), CultureInfo.InvariantCulture);
					case TupleTypes.Utf8: return ulong.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
				}
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an unsigned integer");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="UInt64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ulong? DeserializeUInt64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt64(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="Single"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeSingle(ReadOnlySpan<byte> slice, out float value)
		{
			if (slice.Length == 0)
			{
				value = 0;
				return true;
			}

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					value = 0;
					return true;
				}
				case TupleTypes.Utf8:
				{
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
#if NET7_0_OR_GREATER
						return float.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return float.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
#endif

					}
					break;
				}
				case TupleTypes.Single:
				{
					return TupleParser.TryParseSingle(slice, out value);
				}
				case TupleTypes.Double:
				{
					if (TupleParser.TryParseDouble(slice, out var d))
					{
						value = (float) d;
						return true;
					}

					break;
				}
				case TupleTypes.Quadruple:
				{
					if (TupleParser.TryParseQuadruple(slice, out var d))
					{
						value = (float) d;
						return true;
					}
					break;
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					if (TryDeserializeInt64(slice, out var l))
					{
						value = l;
						return true;
					}

					break;
				}
			}

			value = float.NaN;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a <see cref="Single"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static float DeserializeSingle(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{
#if NET8_0_OR_GREATER
					if (!float.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
					{
						throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a floating point number");
					}
					return result;
#else
					return float.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
#endif
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return (float) TupleParser.ParseDouble(slice);
				}
				case TupleTypes.Quadruple:
				{
					return (float) TupleParser.ParseQuadruple(slice);
				}
			}

			if (type <= TupleTypes.IntPos8 && type >= TupleTypes.IntNeg8)
			{
				return DeserializeInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Single");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Single"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static float? DeserializeSingleNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeSingle(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="Double"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeDouble(ReadOnlySpan<byte> slice, out double value)
		{
			if (slice.Length == 0)
			{
				value = 0;
				return true;
			}

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					value = 0;
					return true;
				}
				case TupleTypes.Utf8:
				{
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
#if NET7_0_OR_GREATER
						return double.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return double.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
#endif
					}
					break;
				}
				case TupleTypes.Single:
				{
					if (TupleParser.TryParseSingle(slice, out var s))
					{
						value = s;
						return true;
					}
					break;
				}
				case TupleTypes.Double:
				{
					return TupleParser.TryParseDouble(slice, out value);
				}
				case TupleTypes.Quadruple:
				{
					if (TupleParser.TryParseQuadruple(slice, out var d))
					{
						value = (double) d;
						return true;
					}
					break;
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					value = DeserializeInt64(slice);
					return true;
				}
			}

			value = double.NaN;
			return false;
		}

		/// <summary>Deserialize a tuple segment into a <see cref="Double"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static double DeserializeDouble(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{
#if NET8_0_OR_GREATER
					if (!double.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
					{
						throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a floating point number");
					}
					return result;
#else
					return double.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
#endif
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice);
				}
				case TupleTypes.Double:
				{
					return TupleParser.ParseDouble(slice);
				}
				case TupleTypes.Quadruple:
				{
					return (double) TupleParser.ParseQuadruple(slice);
				}
			}

			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				return DeserializeInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a floating point number");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Double"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double? DeserializeDoubleNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDouble(slice) : null;

#if NET8_0_OR_GREATER

		/// <summary>Deserialize a tuple segment into a <see cref="Int128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Int128 DeserializeInt128(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{ // encoded as a base-10 number?
					if (!Int128.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
					{
						throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a big integer");
					}
					return result;
				}
				case TupleTypes.NegativeBigInteger:
				{
					return TupleParser.ParseNegativeInt128(slice);
				}
				case TupleTypes.PositiveBigInteger:
				{
					return checked((Int128) TupleParser.ParsePositiveUInt128(slice));
				}
			}

			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				return DeserializeInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a big integer");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Int128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Int128? DeserializeInt128Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeInt128(slice) : null;

		/// <summary>Deserialize a tuple segment into a <see cref="UInt128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static UInt128 DeserializeUInt128(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{ // encoded as a base-10 number?
					if (!UInt128.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
					{
						throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a big integer");
					}
					return result;
				}
				case TupleTypes.NegativeBigInteger:
				{
					// unsigned cannot have a negative value
					throw new OverflowException();
				}
				case TupleTypes.PositiveBigInteger:
				{
					return TupleParser.ParsePositiveUInt128(slice);
				}
			}

			if (type is <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8)
			{
				return DeserializeUInt64(slice);
			}

			throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a big integer");
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Int128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static UInt128? DeserializeUInt128Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUInt128(slice) : null;

#endif

		/// <summary>Deserialize a tuple segment into a <see cref="BigInteger"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeBigInteger(ReadOnlySpan<byte> slice, out BigInteger value)
		{
			if (slice.Length == 0)
			{
				value = 0;
				return true;
			}

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					value = 0;
					return true;
				}
				case TupleTypes.Utf8:
				{ // encoded as a base-10 number?
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
#if NET7_0_OR_GREATER
						return BigInteger.TryParse(str, CultureInfo.InvariantCulture, out value);
#else
						// TryParse(string, IFormatProvider, out ...) not available
						return BigInteger.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
#endif
					}
					break;
				}
				case TupleTypes.NegativeBigInteger:
				{
					return TupleParser.TryParseNegativeBigInteger(slice, out value);
				}
				case TupleTypes.PositiveBigInteger:
				{
					return TupleParser.TryParsePositiveBigInteger(slice, out value);
				}
				case >= TupleTypes.IntNeg8 and < TupleTypes.IntZero:
				{
					if (TryDeserializeInt64(slice, out var l))
					{
						value = l;
						return true;
					}
					break;
				}
				case TupleTypes.IntZero:
				{
					value = 0;
					return true;
				}
				case > TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{
					if (TryDeserializeUInt64(slice, out var ul))
					{
						value = ul;
						return true;
					}
					break;
				}
			}

			value = default;
			return false;
		}

		/// <summary>Deserialize a tuple segment into a <see cref="BigInteger"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static BigInteger DeserializeBigInteger(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return 0;

			byte type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					//REVIEW: or should we return NaN?
					return 0;
				}
				case TupleTypes.Utf8:
				{ // encoded as a base-10 number?
#if NET8_0_OR_GREATER
					if (!BigInteger.TryParse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture, out var result))
					{
						throw new FormatException("Cannot convert tuple segment of type Utf8 (0x02) into a big integer");
					}
					return result;
#else
					return BigInteger.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
#endif
				}
				case TupleTypes.NegativeBigInteger:
				{
					return TupleParser.ParseNegativeBigInteger(slice);
				}
				case TupleTypes.PositiveBigInteger:
				{
					return TupleParser.ParsePositiveBigInteger(slice);
				}
				case >= TupleTypes.IntNeg8 and < TupleTypes.IntZero:
				{
					return DeserializeInt64(slice);
				}
				case TupleTypes.IntZero:
				{
					return 0;
				}
				case > TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{
					return DeserializeUInt64(slice);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a big integer");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="BigInteger"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static BigInteger? DeserializeBigIntegerNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeBigInteger(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="DateTime"/> (UTC)</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTime will be in UTC, because the original TimeZone details are lost.</remarks>
		public static DateTime DeserializeDateTime(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return DateTime.MinValue; //TODO: fail ?

			byte type = slice[0];

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return DateTime.MinValue;
				}

				case TupleTypes.Utf8:
				{ // we only support ISO 8601 dates. For ex: YYYY-MM-DDTHH:MM:SS.fffff"
					string str = TupleParser.ParseUtf8(slice);
					return DateTime.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				}

				case TupleTypes.Double:
				{ // Number of days since Epoch
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't use TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long) (TupleParser.ParseDouble(slice) * TimeSpan.TicksPerDay);
					return new DateTime(ticks, DateTimeKind.Utc);
				}

				case TupleTypes.Quadruple:
				{
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't use TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long) (TupleParser.ParseQuadruple(slice) * TimeSpan.TicksPerDay);
					return new DateTime(ticks, DateTimeKind.Utc);
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{ // If we have an integer, we consider it to be a number of Ticks (Windows Only)
					return new DateTime(DeserializeInt64(slice), DateTimeKind.Utc);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a DateTime");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="DateTime"/> (UTC)</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTime will be in UTC, because the original TimeZone details are lost.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTime? DeserializeDateTimeNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDateTime(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="DateTimeOffset"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTimeOffset will be in UTC if converted value did not specify any offset.</remarks>
		public static DateTimeOffset DeserializeDateTimeOffset(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return DateTime.MinValue; //TODO: fail ?

			byte type = slice[0];

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return DateTimeOffset.MinValue;
				}

				case TupleTypes.Utf8:
				{ // we only support ISO 8601 dates. For ex: YYYY-MM-DDTHH:MM:SS.fffff+xxxx"
					string str = TupleParser.ParseUtf8(slice);
					return DateTimeOffset.Parse(str, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
				}

				case TupleTypes.Double:
				{ // Number of days since Epoch
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't use TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseDouble(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}

				case TupleTypes.Quadruple:
				{
					const long UNIX_EPOCH_TICKS = 621355968000000000L;
					//note: we can't use TimeSpan.FromDays(...) because it rounds to the nearest millisecond!
					long ticks = UNIX_EPOCH_TICKS + (long)(TupleParser.ParseQuadruple(slice) * TimeSpan.TicksPerDay);
					return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{ // If we have an integer, we consider it to be a number of Ticks (Windows Only)
					return new DateTimeOffset(new DateTime(DeserializeInt64(slice), DateTimeKind.Utc));
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a DateTimeOffset");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="DateTimeOffset"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <returns>DateTime in UTC</returns>
		/// <remarks>The returned DateTimeOffset will be in UTC if converted value did not specify any offset.</remarks>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static DateTimeOffset? DeserializeDateTimeOffsetNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeDateTimeOffset(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="TimeSpan"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static TimeSpan DeserializeTimeSpan(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return TimeSpan.Zero; //TODO: fail ?

			byte type = slice[0];

			// We serialize TimeSpans as number of seconds in a 64-bit float.

			switch(type)
			{
				case TupleTypes.Nil:
				{
					return TimeSpan.Zero;
				}
				case TupleTypes.Utf8:
				{ // "HH:MM:SS.fffff"
					return TimeSpan.Parse(TupleParser.ParseUtf8(slice), CultureInfo.InvariantCulture);
				}
				case TupleTypes.Single:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseSingle(slice) * TimeSpan.TicksPerSecond));
				}
				case TupleTypes.Double:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseDouble(slice) * TimeSpan.TicksPerSecond));
				}
				case TupleTypes.Quadruple:
				{ // Number of seconds
					//note: We can't use TimeSpan.FromSeconds(...) because it rounds to the nearest millisecond!
					return new TimeSpan((long) (TupleParser.ParseQuadruple(slice) * TimeSpan.TicksPerSecond));
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{ // If we have an integer, we consider it to be a number of Ticks (Windows Only)
					return new TimeSpan(DeserializeInt64(slice));
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a TimeSpan");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="TimeSpan"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TimeSpan? DeserializeTimeSpanNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeTimeSpan(slice) : null;

		/// <summary>Deserializes a tuple segment into a Unicode character</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static char DeserializeChar(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return '\0';

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return '\0';
				}
				case TupleTypes.Bytes:
				{
					var s = TupleParser.ParseBytes(slice);
					if (s.Count == 0) return '\0';
					if (s.Count == 1) return (char) s[0];
					throw new FormatException($"Cannot convert bytes of length {s.Count} into a Char");
				}
				case TupleTypes.Utf8:
				{
					var s = TupleParser.ParseUtf8(slice);
					if (s.Length == 0) return '\0';
					if (s.Length == 1) return s[0];
					throw new FormatException($"Cannot convert string of size {s.Length} into a Char");
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					return (char) TupleParser.ParseInt64(type, slice);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a Char");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable Unicode character</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static char? DeserializeCharNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeChar(slice) : null;

		/// <summary>Deserializes a tuple segment into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeString(ReadOnlySpan<byte> slice, out string? value)
		{
			if (slice.Length == 0)
			{
				value = null;
				return true;
			}

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					value = null;
					return true;
				}
				case TupleTypes.Bytes:
				{
					return TupleParser.TryParseAscii(slice, out value);
				}
				case TupleTypes.Utf8:
				{
					return TupleParser.TryParseUtf8(slice, out value);
				}
				case TupleTypes.Single:
				{
					if (TupleParser.TryParseSingle(slice, out var f))
					{
						value = f.ToString("R", CultureInfo.InvariantCulture);
						return true;
					}
					break;
				}
				case TupleTypes.Double:
				{
					if (TupleParser.TryParseDouble(slice, out var d))
					{
						value = d.ToString("R", CultureInfo.InvariantCulture);
						return true;
					}
					break;
				}
				case TupleTypes.Quadruple:
				{
					if (TupleParser.TryParseQuadruple(slice, out var d))
					{
						value = d.ToString("R", CultureInfo.InvariantCulture);
						return true;
					}
					break;
				}
				case TupleTypes.Uuid128:
				{
					if (TupleParser.TryParseGuid(slice, out var uuid))
					{
						value = uuid.ToString();
						return true;
					}
					break;
				}
				case TupleTypes.Uuid64:
				{
					if (TupleParser.TryParseUuid64(slice, out var uuid))
					{
						value = uuid.ToString();
						return true;
					}
					break;
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					if (TupleParser.TryParseInt64(type, slice, out var l))
					{
						value = l.ToString(null, CultureInfo.InvariantCulture);
						return true;
					}
					break;
				}
			}

			value = null;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a Unicode string</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static string? DeserializeString(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			byte type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
				case TupleTypes.Bytes:
				{
					return TupleParser.ParseAscii(slice);
				}
				case TupleTypes.Utf8:
				{
					return TupleParser.ParseUtf8(slice);
				}
				case TupleTypes.Single:
				{
					return TupleParser.ParseSingle(slice).ToString(CultureInfo.InvariantCulture);
				}
				case TupleTypes.Double:
				{
					return TupleParser.ParseDouble(slice).ToString(CultureInfo.InvariantCulture);
				}
				case TupleTypes.Quadruple:
				{
					return TupleParser.ParseQuadruple(slice).ToString(CultureInfo.InvariantCulture);
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.ParseGuid(slice).ToString();
				}
				case TupleTypes.Uuid64:
				{
					return TupleParser.ParseUuid64(slice).ToString();
				}
				case <= TupleTypes.IntPos8 and >= TupleTypes.IntNeg8:
				{
					return TupleParser.ParseInt64(type, slice).ToString(CultureInfo.InvariantCulture);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a String");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a <see cref="Guid"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeGuid(ReadOnlySpan<byte> slice, out Guid value)
		{
			if (slice.Length == 0)
			{
				value = Guid.Empty;
				return true;
			}

			switch(slice[0])
			{
				case TupleTypes.Nil:
				{
					value = Guid.Empty;
					return true;
				}
				case TupleTypes.Bytes:
				{
					if (TupleParser.TryParseAscii(slice, out var str))
					{
						return Guid.TryParse(str, out value);
					}
					break;
				}
				case TupleTypes.Utf8:
				{
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
						return Guid.TryParse(str, out value);
					}
					break;
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.TryParseGuid(slice, out value);
				}
				//REVIEW: should we allow converting an Uuid64 into a Guid? This looks more like a bug than an expected behavior...
			}

			value = Guid.Empty;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a <see cref="Guid"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Guid DeserializeGuid(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Guid.Empty;

			return slice[0] switch
			{
				TupleTypes.Nil => Guid.Empty,
				TupleTypes.Bytes => Guid.Parse(TupleParser.ParseAscii(slice)),
				TupleTypes.Utf8 => Guid.Parse(TupleParser.ParseUtf8(slice)),
				TupleTypes.Uuid128 => TupleParser.ParseGuid(slice),
				//REVIEW: should we allow converting an Uuid64 into a Guid? This looks more like a bug than an expected behavior...
				_ => throw new FormatException($"Cannot convert tuple segment of type 0x{slice[0]:X02} into a System.Guid")
			};
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Guid"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Guid? DeserializeGuidNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeGuid(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="Uuid128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid128 DeserializeUuid128(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid128.Empty;

			int type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid128.Empty;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as a 16-byte array
					return Uuid128.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid128.Parse(TupleParser.ParseUtf8(slice));
				}
				case TupleTypes.Uuid128:
				{
					return TupleParser.ParseUuid128(slice);
				}
				//REVIEW: should we allow converting an Uuid64 into an Uuid128? This looks more like a bug than an expected behavior...
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid128");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Uuid128"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid128? DeserializeUuid128Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid128(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="Uuid96"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid96 DeserializeUuid96(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid96.Empty;

			int type = slice[0];
			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid96.Empty;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as a 12-byte array
					return Uuid96.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid96.Parse(TupleParser.ParseUtf8(slice));
				}
				case TupleTypes.Uuid96:
				{
					return TupleParser.ParseUuid96(slice);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid96");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Uuid96"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid96? DeserializeUuid96Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid96(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="Uuid80"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid80 DeserializeUuid80(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid80.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid80.Empty;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as a 10-byte array
					return Uuid80.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid80.Parse(TupleParser.ParseUtf8(slice));
				}
				case TupleTypes.Uuid80:
				{
					return TupleParser.ParseUuid80(slice);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid80");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Uuid80"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid80? DeserializeUuid80Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid80(slice) : null;

		/// <summary>Deserializes a tuple segment into 64-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool TryDeserializeUuid64(ReadOnlySpan<byte> slice, out Uuid64 value)
		{
			if (slice.Length == 0)
			{
				value = Uuid64.Empty;
				return true;
			}

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					value = Uuid64.Empty;
					return true;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as an 8-byte array
					if (TupleParser.TryParseBytes(slice, out var bytes))
					{
						return Uuid64.TryRead(bytes, out value);
					}
					break;
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					if (TupleParser.TryParseUtf8(slice, out var str))
					{
						return Uuid64.TryParse(str, out value);
					}
					break;
				}
				case TupleTypes.Uuid64:
				{
					return TupleParser.TryParseUuid64(slice, out value);
				}
				case >= TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{ // expect 64-bit number
					if (TupleParser.TryParseUInt64(type, slice, out var l))
					{
						value = Uuid64.FromUInt64(l);
						return true;
					}
					break;
				}
			}

			// we don't support negative numbers!
			value = default;
			return false;
		}

		/// <summary>Deserializes a tuple segment into 64-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid64 DeserializeUuid64(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid64.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid64.Empty;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as an 8-byte array
					return Uuid64.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid64.Parse(TupleParser.ParseUtf8(slice));
				}
				case TupleTypes.Uuid64:
				{
					return TupleParser.ParseUuid64(slice);
				}
				case >= TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{ // expect 64-bit number
					return Uuid64.FromUInt64(TupleParser.ParseUInt64(type, slice));
				}
				default:
				{
					// we don't support negative numbers!
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid64");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Uuid64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid64? DeserializeUuid64Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid64(slice) : null;

		/// <summary>Deserializes a tuple segment into 64-bit UUID</summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static Uuid48 DeserializeUuid48(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return Uuid48.Empty;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return Uuid48.Empty;
				}
				case TupleTypes.Bytes:
				{ // expect binary representation as a 6-byte array
					return Uuid48.Read(TupleParser.ParseBytes(slice));
				}
				case TupleTypes.Utf8:
				{ // expect text representation
					return Uuid48.Parse(TupleParser.ParseUtf8(slice));
				}
				case >= TupleTypes.IntZero and <= TupleTypes.IntPos8:
				{ // expect 48-bit number
					return Uuid48.FromUInt64(TupleParser.ParseUInt64(type, slice));
				}
				default:
				{
					// we don't support negative numbers!
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into an Uuid48");
				}
			}
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="Uuid64"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Uuid48? DeserializeUuid48Nullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeUuid48(slice) : null;

		/// <summary>Deserializes a tuple segment into a <see cref="VersionStamp"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		/// <param name="value">Corresponding value</param>
		public static bool DeserializeVersionStamp(ReadOnlySpan<byte> slice, out VersionStamp value)
		{
			if (slice.Length == 0)
			{
				value = default;
				return true;
			}

			int type = slice[0];
			switch(type)
			{
				case TupleTypes.Nil:
				{
					value = default;
					return true;
				}
				case TupleTypes.Uuid80 or TupleTypes.Uuid96:
				{
					return VersionStamp.TryReadFrom(slice[1..], out value);
				}
			}

			value = default;
			return false;
		}

		/// <summary>Deserializes a tuple segment into a <see cref="VersionStamp"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static VersionStamp DeserializeVersionStamp(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return default;

			int type = slice[0];
			return type switch
			{
				TupleTypes.Nil => default,
				TupleTypes.Uuid80 or TupleTypes.Uuid96 => VersionStamp.TryReadFrom(slice.Slice(1), out var stamp) ? stamp : throw new FormatException("Cannot convert malformed tuple segment into a VersionStamp"),
				_ => throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into a VersionStamp")
			};
		}

		/// <summary>Deserializes a tuple segment into a nullable <see cref="VersionStamp"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp? DeserializeVersionStampNullable(ReadOnlySpan<byte> slice) => !IsNilSegment(slice) ? DeserializeVersionStamp(slice) : null;

		/// <summary>Deserializes a tuple segment into an <see cref="System.Net.IPAddress"/></summary>
		/// <param name="slice">Slice that contains a single packed element</param>
		public static System.Net.IPAddress? DeserializeIpAddress(ReadOnlySpan<byte> slice)
		{
			if (slice.Length == 0) return null;

			int type = slice[0];

			switch (type)
			{
				case TupleTypes.Nil:
				{
					return null;
				}
				case TupleTypes.Bytes:
				{
					return new System.Net.IPAddress(TupleParser.ParseBytes(slice).ToArray());
				}
				case TupleTypes.Utf8:
				{
					return System.Net.IPAddress.Parse(TupleParser.ParseUtf8(slice));
				}
				case TupleTypes.Uuid128:
				{ // could be an IPv6 encoded as a 128-bits UUID
					// we have a RoS<byte> but IPAddress.Parse wants a RoS<char>
					// => we assume that the slice contains an ASCII-encoded address, so we will simply convert it into span "as is"
					return new System.Net.IPAddress(slice.Slice(1).ToArray());
				}
				case >= TupleTypes.IntPos1 and <= TupleTypes.IntPos4:
				{ // could be an IPv4 encoded as a 32-bit unsigned integer
					var value = TupleParser.ParseInt64(type, slice);
					Contract.Debug.Assert(value >= 0 && value <= uint.MaxValue);
					return new System.Net.IPAddress(value);
				}
				default:
				{
					throw new FormatException($"Cannot convert tuple segment of type 0x{type:X} into System.Net.IPAddress");
				}
			}
		}

		/// <summary>Unpacks a tuple from a buffer</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with zero or more elements</param>
		/// <param name="embedded"></param>
		/// <returns>Decoded tuple</returns>
		internal static SlicedTuple Unpack(Slice buffer, bool embedded)
		{
			var reader = new TupleReader(buffer.Span, embedded ? 1 : 0);
			if (!TryUnpack(ref reader, out var tuple, out var error))
			{
				if (error != null) throw error;
				return SlicedTuple.Empty;
			}
			return tuple.ToTuple(buffer);
		}

		/// <summary>Unpacks a tuple from a buffer</summary>
		/// <param name="reader">Reader positioned on the start of the packed representation of a tuple with zero or more elements</param>
		/// <returns>Decoded tuple</returns>
		internal static SpanTuple Unpack(scoped ref TupleReader reader)
		{
			// most tuples will probably fit within (prefix, sub-prefix, id, key) so pre-allocating with 4 should be ok...
			var items = new Range[4];

			int p = 0;
			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var item, out var error))
				{
					if (error != null) throw error;
					break;
				}

				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuples will never but very large...
					Array.Resize(ref items, p + 4);
				}
				items[p++] = item;
			}

			if (reader.HasMore)
			{
				throw new FormatException("Parsing of tuple failed failed before reaching the end of the key");
			}

			return new SpanTuple(reader.Input, p == 0 ? [] : items.AsSpan(0, p));
		}

		internal static bool TryUnpack(scoped ref TupleReader reader, out SpanTuple tuple, out Exception? error)
		{
			// (PartitionPrefix, SubspacePrefix, TENANT_PREFIX, LAYER_SUBSPACE, A, B, C, D)
			var items = new Range[8];

			int p = 0;
			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var token, out error))
				{
					if (error != null)
					{
						goto too_small;
					}
					break;
				}

				if (p >= items.Length)
				{
					// note: do not grow exponentially, because tuple keys don't really have that many elements
					Array.Resize(ref items, p + 4);
				}
				items[p++] = token;
			}

			if (reader.HasMore)
			{
				goto too_small;
			}

			tuple = new SpanTuple(reader.Input, p == 0 ? [ ] : items.AsSpan(0, p));
			error = null;
			return true;

		too_small:
			tuple = default;
			return false;

		}

		/// <summary>Ensure that a slice is a packed tuple that contains a single and valid element</summary>
		/// <param name="buffer">Slice that should contain the packed representation of a singleton tuple</param>
		/// <param name="token">Position of the decoded slice in the buffer</param>
		/// <returns></returns>
		public static bool TryUnpackSingle(ReadOnlySpan<byte> buffer, out Range token)
		{
			var reader = new TupleReader(buffer);
			if (!TupleParser.TryParseNext(ref reader, out token, out _))
			{ // failed to parse
				return false;
			}
			if (reader.HasMore)
			{ // there are more than one item in the tuple
				return false;
			}

			return true;
		}

		/// <summary>Only returns the first N items of a packed tuple, without deserializing them.</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with at least 3 elements</param>
		/// <param name="tokens">Raw slice corresponding to the third element from the end of the tuple</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="error">Receives an exception if the parsing failed</param>
		/// <returns><see langword="true"/> if the buffer was successfully parsed and has the expected size</returns>
		/// <remarks>If <paramref name="expectedSize"/> is <see langword="null"/>, this will not attempt to decode the rest of the tuple and will not observe any invalid or corrupted data.</remarks>
		public static bool TryUnpackFirst(ReadOnlySpan<byte> buffer, Span<Range> tokens, int? expectedSize, out Exception? error)
		{
			var reader = new TupleReader(buffer);

			for (int i = 0; i < tokens.Length; i++)
			{
				if (!TupleParser.TryParseNext(ref reader, out tokens[i], out error))
				{
					error ??= new InvalidOperationException("Tuple has less elements than expected.");
					return false;
				}
			}

			if (expectedSize != null)
			{ // we have to continue parsing, to compute the actual size!

				for (int i = tokens.Length; i < expectedSize.Value; i++)
				{
					if (!TupleParser.TryParseNext(ref reader, out _, out error))
					{
						error ??= new InvalidOperationException("Tuple has less elements than expected.");
						return false;
					}
				}
				// should not have anymore !
				if (reader.HasMore)
				{
					error = new InvalidOperationException("Tuple has more elements than expected.");
					return false;
				}
			}

			error = null;
			return true; 
		}

		/// <summary>Only returns the last N items of a packed tuple, without decoding them.</summary>
		/// <param name="buffer">Slice that contains the packed representation of a tuple with at least 3 elements</param>
		/// <param name="tokens">Array that will receive the last N raw slice corresponding to each of the last N elements</param>
		/// <param name="expectedSize">If not <see langword="null"/>, verifies that the tuple has the expected size</param>
		/// <param name="error">Receive an exception if the parsing failed</param>
		/// <returns><see langword="true"/> if the buffer was successfully parsed and has the expected size</returns>
		/// <exception cref="InvalidOperationException">If the decoded tuple does not have the expected size</exception>
		public static bool TryUnpackLast(ReadOnlySpan<byte> buffer, Span<Range> tokens, int? expectedSize, out Exception? error)
		{
			error = null;
			var reader = new TupleReader(buffer);

			int n = 0;
			var tail = tokens.Slice(1);

			while (true)
			{
				if (!TupleParser.TryParseNext(ref reader, out var token, out error))
				{
					if (error != null)
					{ // malformed token
						tokens.Clear();
						return false;
					}
					// no more tokens
					break;
				}

				if (n < tokens.Length)
				{
					tokens[n] = token;
				}
				else
				{
					// slide to the left
					tail.CopyTo(tokens);
					// append last
					tokens[^1] = token;
				}
				++n;
			}

			if (n < tokens.Length || reader.HasMore)
			{ // tuple has fewer elements than expected or has extra bytes
				error = new InvalidOperationException("Tuple has less elements than expected.");
				tokens.Clear();
				return false;
			}

			if (expectedSize != null && n != expectedSize.Value)
			{
				error = new InvalidOperationException($"Expected a tuple of size {expectedSize.Value}, but decoded only {n} elements");
				tokens.Clear();
				return false;
			}

			return true;
		}

		#endregion

	}

}
