#region Copyright (c) 2023-2025 SnowBank SAS, (c) 2005-2023 Doxense SAS
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
	using System.Buffers;
	using System.ComponentModel;
	using SnowBank.Runtime;

	/// <summary>Extension methods for <see cref="JsonValue"/> and other derived types</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class JsonValueExtensions
	{

		/// <summary>Tests if a JSON value is <see langword="null"/>, or null-like</summary>
		/// <param name="value">JSON Value</param>
		/// <returns><see langword="true"/> if <paramref name="value"/> is <see langword="null"/>, or any instance of type <see cref="JsonNull"/></returns>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrMissing([NotNullWhen(false)] this JsonValue? value)
		{
			return value is (null or JsonNull);
		}

		/// <summary>Tests if a JSON value is <see cref="JsonNull.Missing"/></summary>
		/// <param name="value">JSON value</param>
		/// <returns><see langword="true"/> if <paramref name="value"/> is equal to the <see cref="JsonNull.Missing"/> singleton.</returns>
		/// <remarks><see cref="JsonNull.Null"/> n'est pas considéré comme manquant (c'est un null explicite)</remarks>
		[Pure, ContractAnnotation("null=>true"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsMissing([NotNullWhen(false)] this JsonValue? value)
		{
			return ReferenceEquals(value, JsonNull.Missing);
		}


		/// <summary>Tests if a JSON value is <see cref="JsonNull.Error"/></summary>
		/// <param name="value">JSON value</param>
		/// <returns><see langword="true"/> if <paramref name="value"/> is equal to the <see cref="JsonNull.Error"/> singleton.</returns>
		[Pure, ContractAnnotation("null=>true")]
		public static bool IsError([NotNullWhen(false)] this JsonValue? value)
		{
			return ReferenceEquals(value, JsonNull.Error);
		}

		/// <summary>Ensures that a JSON value is not <see langword="null"/>> or missing</summary>
		/// <param name="value">JSON value</param>
		/// <returns>The same instance if the value is non-null. Throws an exception if it is null or missing</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null or missing</exception>
		[ ContractAnnotation("null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Required(this JsonValue? value) => value is not (null or JsonNull) ? value : FailValueIsNullOrMissing();

		/// <summary>Ensures that a JSON value is not <see langword="null"/>> or missing</summary>
		/// <param name="value">JSON value that must not be null, or missing</param>
		/// <param name="index">Array index</param>
		/// <param name="message">Error message</param>
		/// <returns>The same instance if the value is non-null. Throws an exception if it is null or missing</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null or missing</exception>
		[Pure, ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredIndex(this JsonValue? value, int index, string? message = null) => value is not (null or JsonNull) ? value : FailIndexIsNullOrMissing(index, value, message);

		/// <summary>Ensures that a JSON value is not <see langword="null"/>> or missing</summary>
		/// <param name="value">JSON value that must not be null, or missing</param>
		/// <param name="index">Array index</param>
		/// <param name="message">Error message</param>
		/// <returns>The same instance if the value is non-null. Throws an exception if it is null or missing</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null or missing</exception>
		[Pure, ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredIndex(this JsonValue? value, Index index, string? message = null) => value is not (null or JsonNull) ? value : FailIndexIsNullOrMissing(index, value, message);

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value of the <paramref name="field"/> in the parent object.</param>
		/// <param name="field">Name of the field in the parent object.</param>
		/// <param name="message">Message of the exception thrown if the value is null or missing</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredField(this JsonValue? value, string field, string? message = null) => value is not (null or JsonNull) ? value : FailFieldIsNullOrMissing(value, field, message);

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value of the <paramref name="field"/> in the parent object.</param>
		/// <param name="field">Name of the field in the parent object.</param>
		/// <param name="message">Message of the exception thrown if the value is null or missing</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredField(this JsonValue? value, ReadOnlySpan<char> field, string? message = null) => value is not (null or JsonNull) ? value : FailFieldIsNullOrMissing(value, field, message);

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value of the <paramref name="field"/> in the parent object.</param>
		/// <param name="field">Name of the field in the parent object.</param>
		/// <param name="message">Message of the exception thrown if the value is null or missing</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredField(this JsonValue? value, ReadOnlyMemory<char> field, string? message = null) => value is not (null or JsonNull) ? value : FailFieldIsNullOrMissing(value, field, message);

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value at the specified <paramref name="path"/> in the parent object.</param>
		/// <param name="path">Path to a field</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredPath(this JsonValue? value, string path) => value is not (null or JsonNull) ? value : FailPathIsNullOrMissing(value, JsonPath.Create(path));

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value at the specified <paramref name="path"/> in the parent object.</param>
		/// <param name="path">Path to a field</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredPath(this JsonValue? value, ReadOnlyMemory<char> path) => value is not (null or JsonNull) ? value : FailPathIsNullOrMissing(value, JsonPath.Create(path));

		/// <summary>Ensures that the value of a field in a JSON Object is not null or missing</summary>
		/// <param name="value">Value at the specified <paramref name="path"/> in the parent object.</param>
		/// <param name="path">Path to a field</param>
		/// <returns>The same value, if it is not null or missing; otherwise, an exception is thrown</returns>
		/// <exception cref="JsonBindingException">If the value is null or missing</exception>
		[ContractAnnotation("value:null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue RequiredPath(this JsonValue? value, JsonPath path) => value is not (null or JsonNull) ? value : FailPathIsNullOrMissing(value, path);

		/// <summary>Ensures that a JSON Array is not null</summary>
		/// <param name="value">JSON Array</param>
		/// <returns>The same instance if not null. Throws an exception if null</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null</exception>
		[ContractAnnotation("null => halt")]
		public static JsonArray Required(this JsonArray? value) => value ?? FailArrayIsNullOrMissing();

		/// <summary>Ensures that a JSON Object is not null</summary>
		/// <param name="value">JSON Object</param>
		/// <returns>The same instance if not null. Throws an exception if null</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null</exception>
		[ContractAnnotation("null => halt")]
		public static JsonObject Required(this JsonObject? value) => value ?? FailObjectIsNullOrMissing(value);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailValueIsNullOrMissing()
			=> throw CrystalJson.Errors.Parsing_ValueIsNullOrMissing();

		[Pure]
		internal static IndexOutOfRangeException ErrorValueIsOutOfBounds()
			=> new("Index is outside the bounds of the array.");

		[DoesNotReturn, StackTraceHidden]
		internal static JsonArray FailArrayIsOutOfBounds()
			=> throw ErrorValueIsOutOfBounds();

		[DoesNotReturn, StackTraceHidden]
		internal static JsonObject FailObjectIsOutOfBounds()
			=> throw ErrorValueIsOutOfBounds();

		[DoesNotReturn, StackTraceHidden]
		internal static JsonArray FailArrayIsNullOrMissing()
			=> throw new JsonBindingException("Required JSON array was null or missing.");

		[DoesNotReturn, StackTraceHidden]
		internal static JsonArray FailValueIsNotAnArray(JsonValue value)
			=> throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailIndexIsNullOrMissing(int index, JsonValue? value, string? message = null)
			=> throw new JsonBindingException(message ?? (ReferenceEquals(value, JsonNull.Error) ? $"Index {index} is outside the bounds of the JSON Array." : $"Required JSON field at index {index} was null or missing."), JsonPath.Create(index), null, null);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailIndexIsNullOrMissing(Index index, JsonValue? value, string? message = null)
			=> throw new JsonBindingException(message ?? (ReferenceEquals(value, JsonNull.Error) ? $"Index {index} is outside the bounds of the JSON Array." : $"Required JSON field at index {index} was null or missing."), JsonPath.Create(index), null, null);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailFieldIsNullOrMissing(JsonValue? parent, string field, string? message = null) => throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, field, message);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailFieldIsNullOrMissing(JsonValue? parent, ReadOnlySpan<char> field, string? message = null) => throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, field.ToString(), message);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailFieldIsNullOrMissing(JsonValue? parent, ReadOnlyMemory<char> field, string? message = null) => throw CrystalJson.Errors.Parsing_FieldIsNullOrMissing(parent, field.GetStringOrCopy(), message);

		[Pure]
		internal static JsonBindingException ErrorPathIsNullOrMissing(JsonValue? parent, JsonPath path) => new($"Required JSON path '{path}' was null or missing.", path, parent, null);

		[DoesNotReturn, StackTraceHidden]
		internal static JsonValue FailPathIsNullOrMissing(JsonValue? parent, JsonPath path) => throw ErrorPathIsNullOrMissing(parent, path);

		#region ToStuff(...)

		/// <summary>Serializes a <see cref="JsonValue"/> into a text literal possible</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
		[Pure]
		[OverloadResolutionPriority(1)]
		public static string ToJsonText(this JsonValue? value) => value?.ToJsonText(CrystalJsonSettings.Json) ?? JsonTokens.Null;

		/// <summary>Serializes a <see cref="JsonValue"/> into the most compact text literal possible</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
		[Pure]
		[OverloadResolutionPriority(1)]
		[Obsolete("Use ToJsonTextIndented() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static string ToJsonCompact(this JsonValue? value) => value?.ToJsonText(CrystalJsonSettings.JsonCompact) ?? JsonTokens.Null;

		/// <summary>Serializes a <see cref="JsonValue"/> into the most compact text literal possible</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <remarks>Note: if the JSON has to be sent over HTTP, or stored on disk, prefer <see cref="ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> or <see cref="ToJsonBytes(JsonValue)"/> that will return the same result but already utf-8 encoded</remarks>
		[Pure]
		[OverloadResolutionPriority(1)]
		public static string ToJsonTextCompact(this JsonValue? value) => value?.ToJsonText(CrystalJsonSettings.JsonCompact) ?? JsonTokens.Null;

		/// <summary>Serializes a <see cref="JsonValue"/> into a human-friendly indented text representation (for logging, console output, etc...)</summary>
		/// <param name="value">JSON value to serialize</param>
		[Pure]
		[OverloadResolutionPriority(1)]
		[Obsolete("Use ToJsonTextIndented() instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static string ToJsonIndented(this JsonValue? value) => value?.ToJsonText(CrystalJsonSettings.JsonIndented) ?? JsonTokens.Null;

		/// <summary>Serializes a <see cref="JsonValue"/> into a human-friendly indented text representation (for logging, console output, etc...)</summary>
		/// <param name="value">JSON value to serialize</param>
		[Pure]
		[OverloadResolutionPriority(1)]
		public static string ToJsonTextIndented(this JsonValue? value) => value?.ToJsonText(CrystalJsonSettings.JsonIndented) ?? JsonTokens.Null;

		/// <summary>Serializes a <see cref="JsonValue"/> into a byte array, using the default settings</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <returns>Array of the utf-8 encoded text representation of the JSON value</returns>
		/// <remarks>Only call this when interacting with legacy API that only accept byte[] arrays. Prefer <see cref="ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> that will reduce the number of needed memory copies and allocations</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static byte[] ToJsonBytes(this JsonValue? value) => CrystalJson.ToBytes(value);

		/// <summary>Serializes a <see cref="JsonValue"/> into a byte array, using custom settings</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <param name="settings">Custom serialization settings</param>
		/// <returns>Array of the utf-8 encoded text representation of the JSON value</returns>
		/// <remarks>Only call this when interacting with legacy API that only accept byte[] arrays. Prefer <see cref="ToJsonSlice(JsonValue?,CrystalJsonSettings?)"/> that will reduce the number of needed memory copies and allocations</remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static byte[] ToJsonBytes(this JsonValue? value, CrystalJsonSettings? settings) => CrystalJson.ToBytes(value, settings);

		/// <summary>Serializes a <see cref="JsonValue"/> into a <see cref="Slice"/>, using custom settings</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <param name="settings">Custom serialization settings</param>
		/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static Slice ToJsonSlice(this JsonValue? value, CrystalJsonSettings? settings = null) => CrystalJson.ToSlice(value, settings);

		/// <summary>Serializes a <see cref="JsonValue"/> into a <see cref="Slice"/>, using custom settings</summary>
		/// <param name="value">JSON value to serialize</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Custom serialization settings</param>
		/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
		[Pure, MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		[OverloadResolutionPriority(1)]
		public static SliceOwner ToJsonSlice(this JsonValue? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null) => CrystalJson.ToSlice(value, pool, settings);

		/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a <see cref="Slice"/>, using custom settings</summary>
		/// <param name="value">Instance to serialize</param>
		/// <param name="settings">Custom serialization settings</param>
		/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Slice ToJsonSlice<TJsonSerializable>(this TJsonSerializable? value, CrystalJsonSettings? settings = null)
			where TJsonSerializable : IJsonSerializable
			=> CrystalJson.ToSlice(value, settings);

		/// <summary>Serializes a <see cref="IJsonSerializable"/> value into a <see cref="Slice"/>, using custom settings</summary>
		/// <param name="value">Instance to serialize</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Custom serialization settings</param>
		/// <returns><see cref="Slice"/> that contains the utf-8 encoded text representation of the JSON value</returns>
		[Pure, MustDisposeResource, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static SliceOwner ToJsonSlice<TJsonSerializable>(this TJsonSerializable? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null)
			where TJsonSerializable : IJsonSerializable
			=> CrystalJson.ToSlice(value, pool, settings);

		#endregion

		#region As<T>...

		[DoesNotReturn, StackTraceHidden]
		internal static T FailRequiredValueIsNullOrMissing<T>() => throw new JsonBindingException($"Required JSON value of type {typeof(T).GetFriendlyName()} was null or missing");

		/// <summary>Converts this required <see cref="JsonValue"/> into an instance of the specified type.</summary>
		/// <typeparam name="TValue">Target managed type</typeparam>
		/// <param name="value">JSON value to be converted</param>
		/// <param name="resolver">Optional type resolver used to bind the value into a managed CLR type (<see cref="CrystalJson.DefaultResolver"/> is omitted)</param>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is <see langword="null"/>, <see cref="JsonNull">null-like</see>, or cannot be bound to the specified type.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TValue Required<TValue>(this JsonValue? value, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			if (value is null or JsonNull)
			{
				FailValueIsNullOrMissing();
			}

			#region <JIT_HACK>
			// pattern recognized and optimized by the JIT, only in Release build
			if (default(TValue) is not null)
			{
				// value types are safe because they can never be null
				if (typeof(TValue) == typeof(bool)) return (TValue) (object) value.ToBoolean();
				if (typeof(TValue) == typeof(char)) return (TValue) (object) value.ToChar();
				if (typeof(TValue) == typeof(byte)) return (TValue) (object) value.ToByte();
				if (typeof(TValue) == typeof(sbyte)) return (TValue) (object) value.ToSByte();
				if (typeof(TValue) == typeof(short)) return (TValue) (object) value.ToInt16();
				if (typeof(TValue) == typeof(ushort)) return (TValue) (object) value.ToUInt16();
				if (typeof(TValue) == typeof(int)) return (TValue) (object) value.ToInt32();
				if (typeof(TValue) == typeof(uint)) return (TValue) (object) value.ToUInt32();
				if (typeof(TValue) == typeof(long)) return (TValue) (object) value.ToInt64();
				if (typeof(TValue) == typeof(ulong)) return (TValue) (object) value.ToUInt64();
				if (typeof(TValue) == typeof(float)) return (TValue) (object) value.ToSingle();
				if (typeof(TValue) == typeof(double)) return (TValue) (object) value.ToDouble();
				if (typeof(TValue) == typeof(Half)) return (TValue) (object) value.ToHalf();
				if (typeof(TValue) == typeof(decimal)) return (TValue) (object) value.ToDecimal();
				if (typeof(TValue) == typeof(Guid)) return (TValue) (object) value.ToGuid();
				if (typeof(TValue) == typeof(Uuid128)) return (TValue) (object) value.ToUuid128();
				if (typeof(TValue) == typeof(Uuid96)) return (TValue) (object) value.ToUuid96();
				if (typeof(TValue) == typeof(Uuid80)) return (TValue) (object) value.ToUuid80();
				if (typeof(TValue) == typeof(Uuid64)) return (TValue) (object) value.ToUuid64();
				if (typeof(TValue) == typeof(TimeSpan)) return (TValue) (object) value.ToTimeSpan();
				if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) value.ToDateTime();
				if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) value.ToDateTimeOffset();
				if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) value.ToDateOnly();
				if (typeof(TValue) == typeof(TimeOnly)) return (TValue) (object) value.ToTimeOnly();
				if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) value.ToInstant();
				if (typeof(TValue) == typeof(NodaTime.Duration)) return (TValue) (object) value.ToDuration();
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128)) return (TValue) (object) value.ToInt128();
				if (typeof(TValue) == typeof(UInt128)) return (TValue) (object) value.ToUInt128();
#endif

				return value.Bind<TValue>(default!, resolver);
			}
			else
			{
				// nullable value types are safe because we already checked for null above
				if (typeof(TValue) == typeof(bool?)) return (TValue) (object) value.ToBoolean();
				if (typeof(TValue) == typeof(char?)) return (TValue) (object) value.ToChar();
				if (typeof(TValue) == typeof(byte?)) return (TValue) (object) value.ToByte();
				if (typeof(TValue) == typeof(sbyte?)) return (TValue) (object) value.ToSByte();
				if (typeof(TValue) == typeof(short?)) return (TValue) (object) value.ToInt16();
				if (typeof(TValue) == typeof(ushort?)) return (TValue) (object) value.ToUInt16();
				if (typeof(TValue) == typeof(int?)) return (TValue) (object) value.ToInt32();
				if (typeof(TValue) == typeof(uint?)) return (TValue) (object) value.ToUInt32();
				if (typeof(TValue) == typeof(long?)) return (TValue) (object) value.ToInt64();
				if (typeof(TValue) == typeof(ulong?)) return (TValue) (object) value.ToUInt64();
				if (typeof(TValue) == typeof(float?)) return (TValue) (object) value.ToSingle();
				if (typeof(TValue) == typeof(double?)) return (TValue) (object) value.ToDouble();
				if (typeof(TValue) == typeof(Half?)) return (TValue) (object) value.ToHalf();
				if (typeof(TValue) == typeof(decimal?)) return (TValue) (object) value.ToDecimal();
				if (typeof(TValue) == typeof(Guid?)) return (TValue) (object) value.ToGuid();
				if (typeof(TValue) == typeof(Uuid128?)) return (TValue) (object) value.ToUuid128();
				if (typeof(TValue) == typeof(Uuid96?)) return (TValue) (object) value.ToUuid96();
				if (typeof(TValue) == typeof(Uuid80?)) return (TValue) (object) value.ToUuid80();
				if (typeof(TValue) == typeof(Uuid64?)) return (TValue) (object) value.ToUuid64();
				if (typeof(TValue) == typeof(TimeSpan?)) return (TValue) (object) value.ToTimeSpan();
				if (typeof(TValue) == typeof(DateTime?)) return (TValue) (object) value.ToDateTime();
				if (typeof(TValue) == typeof(DateTimeOffset?)) return (TValue) (object) value.ToDateTimeOffset();
				if (typeof(TValue) == typeof(DateOnly?)) return (TValue) (object) value.ToDateOnly();
				if (typeof(TValue) == typeof(TimeOnly?)) return (TValue) (object) value.ToTimeOnly();
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return (TValue) (object) value.ToInstant();
				if (typeof(TValue) == typeof(NodaTime.Duration?)) return (TValue) (object) value.ToDuration();
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128?)) return (TValue) (object) value.ToInt128();
				if (typeof(TValue) == typeof(UInt128?)) return (TValue) (object) value.ToUInt128();
#endif

				return value.Bind<TValue>(default, resolver) ?? throw CrystalJson.Errors.Parsing_ValueIsNullOrMissing();
			}
			#endregion </JIT_HACK>
		}

		/// <summary>Converts this value into the specified CLR type, with a fallback value if it is null or missing.</summary>
		/// <typeparam name="TValue">Target CLR type</typeparam>
		/// <exception cref="JsonBindingException">If the value cannot be bound to the specified type.</exception>
		/// <remarks>If the value is <see langword="null"/> or "null-like", this will return the <paramref name="defaultValue"/>.</remarks>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? As<TValue>(this JsonValue? value, TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null)
		{
			value ??= JsonNull.Null;

			#region <JIT_HACK>
			if (default(TValue) is not null)
			{
				if (typeof(TValue) == typeof(bool)) return (TValue) (object) value.ToBoolean((bool) (object) defaultValue!);
				if (typeof(TValue) == typeof(char)) return (TValue) (object) value.ToChar((char) (object) defaultValue!);
				if (typeof(TValue) == typeof(byte)) return (TValue) (object) value.ToByte((byte) (object) defaultValue!);
				if (typeof(TValue) == typeof(sbyte)) return (TValue) (object) value.ToSByte((sbyte) (object) defaultValue!);
				if (typeof(TValue) == typeof(short)) return (TValue) (object) value.ToInt16((short) (object) defaultValue!);
				if (typeof(TValue) == typeof(ushort)) return (TValue) (object) value.ToUInt16((ushort) (object) defaultValue!);
				if (typeof(TValue) == typeof(int)) return (TValue) (object) value.ToInt32((int) (object) defaultValue!);
				if (typeof(TValue) == typeof(uint)) return (TValue) (object) value.ToUInt32((uint) (object) defaultValue!);
				if (typeof(TValue) == typeof(long)) return (TValue) (object) value.ToInt64((long) (object) defaultValue!);
				if (typeof(TValue) == typeof(ulong)) return (TValue) (object) value.ToUInt64((ulong) (object) defaultValue!);
				if (typeof(TValue) == typeof(float)) return (TValue) (object) value.ToSingle((float) (object) defaultValue!);
				if (typeof(TValue) == typeof(double)) return (TValue) (object) value.ToDouble((double) (object) defaultValue!);
				if (typeof(TValue) == typeof(Half)) return (TValue) (object) value.ToHalf((Half) (object) defaultValue!);
				if (typeof(TValue) == typeof(decimal)) return (TValue) (object) value.ToDecimal((decimal) (object) defaultValue!);
				if (typeof(TValue) == typeof(Guid)) return (TValue) (object) value.ToGuid((Guid) (object) defaultValue!);
				if (typeof(TValue) == typeof(Uuid128)) return (TValue) (object) value.ToUuid128((Uuid128) (object) defaultValue!);
				if (typeof(TValue) == typeof(Uuid96)) return (TValue) (object) value.ToUuid96((Uuid96) (object) defaultValue!);
				if (typeof(TValue) == typeof(Uuid80)) return (TValue) (object) value.ToUuid80((Uuid80) (object) defaultValue!);
				if (typeof(TValue) == typeof(Uuid64)) return (TValue) (object) value.ToUuid64((Uuid64) (object) defaultValue!);
				if (typeof(TValue) == typeof(TimeSpan)) return (TValue) (object) value.ToTimeSpan((TimeSpan) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateTime)) return (TValue) (object) value.ToDateTime((DateTime) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateTimeOffset)) return (TValue) (object) value.ToDateTimeOffset((DateTimeOffset) (object) defaultValue!);
				if (typeof(TValue) == typeof(DateOnly)) return (TValue) (object) value.ToDateOnly((DateOnly) (object) defaultValue!);
				if (typeof(TValue) == typeof(TimeOnly)) return (TValue) (object) value.ToTimeOnly((TimeOnly) (object) defaultValue!);
				if (typeof(TValue) == typeof(NodaTime.Instant)) return (TValue) (object) value.ToInstant((NodaTime.Instant) (object) defaultValue!);
				if (typeof(TValue) == typeof(NodaTime.Duration)) return (TValue) (object) value.ToDuration((NodaTime.Duration) (object) defaultValue!);
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128)) return (TValue) (object) value.ToInt128OrDefault((Int128) (object) defaultValue!);
				if (typeof(TValue) == typeof(UInt128)) return (TValue) (object) value.ToUInt128OrDefault((UInt128) (object) defaultValue!);
#endif

				// cannot return null, so defaultValue will never be used
				return value.Bind<TValue>(defaultValue, resolver);
			}
			else
			{
				// nullable value types are safe because we already checked for null above, and JsonNull will do "the right thing"
				if (typeof(TValue) == typeof(bool?)) return (TValue?) (object?) value.ToBooleanOrDefault((bool?) (object?) defaultValue);
				if (typeof(TValue) == typeof(char?)) return (TValue?) (object?) value.ToCharOrDefault((char?) (object?) defaultValue);
				if (typeof(TValue) == typeof(byte?)) return (TValue?) (object?) value.ToByteOrDefault((byte?) (object?) defaultValue);
				if (typeof(TValue) == typeof(sbyte?)) return (TValue?) (object?) value.ToSByteOrDefault((sbyte?) (object?) defaultValue);
				if (typeof(TValue) == typeof(short?)) return (TValue?) (object?) value.ToInt16OrDefault((short?) (object?) defaultValue);
				if (typeof(TValue) == typeof(ushort?)) return (TValue?) (object?) value.ToUInt16OrDefault((ushort?) (object?) defaultValue);
				if (typeof(TValue) == typeof(int?)) return (TValue?) (object?) value.ToInt32OrDefault((int?) (object?) defaultValue);
				if (typeof(TValue) == typeof(uint?)) return (TValue?) (object?) value.ToUInt32OrDefault((uint?) (object?) defaultValue);
				if (typeof(TValue) == typeof(long?)) return (TValue?) (object?) value.ToInt64OrDefault((long?) (object?) defaultValue);
				if (typeof(TValue) == typeof(ulong?)) return (TValue?) (object?) value.ToUInt64OrDefault((ulong?) (object?) defaultValue);
				if (typeof(TValue) == typeof(float?)) return (TValue?) (object?) value.ToSingleOrDefault((float?) (object?) defaultValue);
				if (typeof(TValue) == typeof(double?)) return (TValue?) (object?) value.ToDoubleOrDefault((double?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Half?)) return (TValue?) (object?) value.ToHalfOrDefault((Half?) (object?) defaultValue);
				if (typeof(TValue) == typeof(decimal?)) return (TValue?) (object?) value.ToDecimalOrDefault((decimal?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Guid?)) return (TValue?) (object?) value.ToGuidOrDefault((Guid?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Uuid128?)) return (TValue?) (object?) value.ToUuid128OrDefault((Uuid128?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Uuid96?)) return (TValue?) (object?) value.ToUuid96OrDefault((Uuid96?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Uuid80?)) return (TValue?) (object?) value.ToUuid80OrDefault((Uuid80?) (object?) defaultValue);
				if (typeof(TValue) == typeof(Uuid64?)) return (TValue?) (object?) value.ToUuid64OrDefault((Uuid64?) (object?) defaultValue);
				if (typeof(TValue) == typeof(TimeSpan?)) return (TValue?) (object?) value.ToTimeSpanOrDefault((TimeSpan?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateTime?)) return (TValue?) (object?) value.ToDateTimeOrDefault((DateTime?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateTimeOffset?)) return (TValue?) (object?) value.ToDateTimeOffsetOrDefault((DateTimeOffset?) (object?) defaultValue);
				if (typeof(TValue) == typeof(DateOnly?)) return (TValue?) (object?) value.ToDateOnlyOrDefault((DateOnly?) (object?) defaultValue);
				if (typeof(TValue) == typeof(TimeOnly?)) return (TValue?) (object?) value.ToTimeOnlyOrDefault((TimeOnly?) (object?) defaultValue);
				if (typeof(TValue) == typeof(NodaTime.Instant?)) return (TValue?) (object?) value.ToInstantOrDefault((NodaTime.Instant?) (object?) defaultValue);
				if (typeof(TValue) == typeof(NodaTime.Duration?)) return (TValue?) (object?) value.ToDurationOrDefault((NodaTime.Duration?) (object?) defaultValue);
#if NET8_0_OR_GREATER
				if (typeof(TValue) == typeof(Int128?)) return (TValue?) (object?) value.ToInt128OrDefault((Int128?) (object?) defaultValue);
				if (typeof(TValue) == typeof(UInt128?)) return (TValue?) (object?) value.ToUInt128OrDefault((UInt128?) (object?) defaultValue);
#endif

				return value.Bind<TValue>(defaultValue, resolver);
			}
			#endregion </JIT_HACK>
		}

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static JsonValue As(this JsonValue? value, JsonValue? missingValue) => (value is JsonNull ? null : value) ?? missingValue ?? JsonNull.Missing;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static string As(this JsonValue? value, string missingValue) => value?.ToStringOrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static bool As(this JsonValue? value, bool missingValue) => value?.ToBooleanOrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static int As(this JsonValue? value, int missingValue) => value?.ToInt32OrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static long As(this JsonValue? value, long missingValue) => value?.ToInt64OrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static double As(this JsonValue? value, double missingValue) => value?.ToDoubleOrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static float As(this JsonValue? value, float missingValue) => value?.ToSingleOrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Half As(this JsonValue? value, Half missingValue) => value?.ToHalfOrDefault() ?? missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Guid As(this JsonValue? value, Guid missingValue) => value is not (null or JsonNull) ? value.ToGuid() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Uuid128 As(this JsonValue? value, Uuid128 missingValue) => value is not (null or JsonNull) ? value.ToUuid128() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static Uuid64 As(this JsonValue? value, Uuid64 missingValue) => value is not (null or JsonNull) ? value.ToUuid64() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static TimeSpan As(this JsonValue? value, TimeSpan missingValue) => value is not (null or JsonNull) ? value.ToTimeSpan() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static DateTime As(this JsonValue? value, DateTime missingValue) => value is not (null or JsonNull) ? value.ToDateTime() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static DateTimeOffset As(this JsonValue? value, DateTimeOffset missingValue) => value is not (null or JsonNull) ? value.ToDateTimeOffset() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static NodaTime.Instant As(this JsonValue? value, NodaTime.Instant missingValue) => value is not (null or JsonNull) ? value.ToInstant() : missingValue;

		/// <summary>Returns the converted value, or a fallback value if it is missing</summary>
		/// <param name="value">JSON value to convert</param>
		/// <param name="missingValue">Fallback value</param>
		/// <returns>The converted value, or <paramref name="missingValue"/> if it is <see langword="null"/> or missing</returns>
		[Pure]
		public static NodaTime.Duration As(this JsonValue? value, NodaTime.Duration missingValue) => value is not (null or JsonNull) ? value.ToDuration() : missingValue;

		#endregion

		#region Object Helpers...

		/// <summary>Returns this value as a <b>required</b> JSON Object.</summary>
		/// <param name="value">Value that must be a JSON Object.</param>
		/// <returns>The same instance cast as <see cref="JsonObject"/>, Throws an exception if the value is null, missing, or any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null, missing, or not a JSON Object.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject AsObject(this JsonValue? value) => value switch
		{
			JsonObject obj => obj,
			null or JsonNull => (ReferenceEquals(value, JsonNull.Error) ? FailObjectIsOutOfBounds() : FailObjectIsNullOrMissing(value)),
			_ => FailValueIsNotAnObject(value)
		};

		/// <summary>Returns this value as JSON Object, or <see langword="null"/> if it is null or missing.</summary>
		/// <param name="value">Value that can either be a JSON Object or null or missing.</param>
		/// <returns>The same instance cast as <see cref="JsonObject"/>, or <see langword="null"/> if it was null or missing. Throws an exception if the value is any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a JSON Object and not null or missing.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject? AsObjectOrDefault(this JsonValue? value) => value switch
		{
			JsonObject obj => obj,
			null or JsonNull => (ReferenceEquals(value, JsonNull.Error) ? FailObjectIsOutOfBounds() : null),
			_ => FailValueIsNotAnObject(value)
		};

		/// <summary>Returns this value as a JSON Object, or an empty (read-only) object it is null or missing.</summary>
		/// <param name="value">Value that can either be a JSON Object or null or missing.</param>
		/// <returns>The same instance cast as <see cref="JsonObject"/>, or the <see cref="JsonObject.ReadOnly.Empty"/> singleton if it was null or missing. Throws an exception if the value is any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a JSON Object.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonObject AsObjectOrEmpty(this JsonValue? value) => value switch
		{
			JsonObject obj => obj,
			null or JsonNull => (ReferenceEquals(value, JsonNull.Error) ? FailObjectIsOutOfBounds() : JsonObject.ReadOnly.Empty),
			_ => FailValueIsNotAnObject(value)
		};

		/// <exception cref="JsonBindingException"/>
		[DoesNotReturn, StackTraceHidden]
		internal static JsonObject FailObjectIsNullOrMissing(JsonValue? value) => throw new JsonBindingException("Required JSON object was null or missing.", value);

		/// <exception cref="JsonBindingException"/>
		[DoesNotReturn, StackTraceHidden]
		internal static JsonObject FailValueIsNotAnObject(JsonValue? value) => throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value);

		[Pure]
		public static JsonObject ToJsonObject([InstantHandle] this IEnumerable<KeyValuePair<string, JsonValue>> items, IEqualityComparer<string>? comparer = null)
		{
			return JsonObject.Create(comparer ?? StringComparer.Ordinal, items);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, IEqualityComparer<string>? comparer = null)
		{
			return JsonObject.FromValues(comparer ?? JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal, items);
		}

		[Pure]
		public static JsonObject ToJsonObject<TValue>([InstantHandle] this IEnumerable<KeyValuePair<string, TValue>> items, [InstantHandle] Func<TValue, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			Contract.NotNull(items);
			Contract.NotNull(valueSelector);

			//TODO: move this as JsonObject.FromValues(...)

			comparer ??= JsonObject.ExtractKeyComparer(items) ?? StringComparer.Ordinal;

			var map = new Dictionary<string, JsonValue>(items.TryGetNonEnumeratedCount(out var count) ? count : 0, comparer);
			foreach (var item in items)
			{
				Contract.Debug.Assert(item.Key is not null, "Item cannot have a null key");
				map.Add(item.Key, valueSelector(item.Value) ?? JsonNull.Null);
			}
			return new JsonObject(map, readOnly: false);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, JsonValue?> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			return JsonObject.FromValues(comparer ?? StringComparer.Ordinal, source, keySelector, valueSelector);
		}

		[Pure]
		public static JsonObject ToJsonObject<TElement, TValue>([InstantHandle] this IEnumerable<TElement> source, [InstantHandle] Func<TElement, string> keySelector, [InstantHandle] Func<TElement, TValue> valueSelector, IEqualityComparer<string>? comparer = null)
		{
			return JsonObject.FromValues(comparer ?? StringComparer.Ordinal, source, keySelector, valueSelector);
		}

		/// <summary>Returns either the object itself, or an empty read-only object if it was missing</summary>
		[Pure]
		public static JsonObject OrEmpty(this JsonObject? self) => self ?? JsonObject.ReadOnly.Empty;

		#endregion

		#region Array Helpers...

		/// <summary>Returns this value as a required JSON Array.</summary>
		/// <param name="value">Value that must be a JSON Array.</param>
		/// <returns>The same instance cast as <see cref="JsonArray"/>, Throws an exception if the value is null, missing, or any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is null, missing, or not a JSON Array.</exception>
		[Pure, ContractAnnotation("null => halt"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray AsArray(this JsonValue? value) => value is null or JsonNull ? (ReferenceEquals(value, JsonNull.Error) ? FailArrayIsOutOfBounds() : FailArrayIsNullOrMissing()) : value as JsonArray ?? FailValueIsNotAnArray(value);

		/// <summary>Returns this value as a JSON Array, or <see langword="null"/> if it is null or missing.</summary>
		/// <param name="value">Value that can either be a JSON Array or null or missing.</param>
		/// <returns>The same instance cast as <see cref="JsonArray"/>, or <see langword="null"/> if it was null or missing. Throws an exception if the value is any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a JSON Array and not null or missing.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray? AsArrayOrDefault(this JsonValue? value) => value.IsNullOrMissing() ? (ReferenceEquals(value, JsonNull.Error) ? FailArrayIsOutOfBounds() : null) : value as JsonArray ?? FailValueIsNotAnArray(value);

		/// <summary>Returns this value as a JSON Array, or an empty (read-only) object it is null or missing.</summary>
		/// <param name="value">Value that can either be a JSON Array or null or missing.</param>
		/// <returns>The same instance cast as <see cref="JsonArray"/>, or the <see cref="JsonArray.ReadOnly.Empty"/> singleton if it was null or missing. Throws an exception if the value is any other type.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a JSON Array.</exception>
		[Pure, ContractAnnotation("null => null"), MethodImpl(MethodImplOptions.AggressiveInlining)]
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static JsonArray AsArrayOrEmpty(this JsonValue? value) => value.IsNullOrMissing() ? (ReferenceEquals(value, JsonNull.Error) ? FailArrayIsOutOfBounds() : JsonArray.ReadOnly.Empty) : value as JsonArray ?? FailValueIsNotAnArray(value);

		/// <summary>Returns either the array itself, or an empty read-only array if it was missing</summary>
		[Pure]
		public static JsonArray OrEmpty(this JsonArray? self) => self ?? JsonArray.ReadOnly.Empty;

		#endregion

		#region AsNumber...

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonNumber"/>.</summary>
		/// <param name="value">JSON value expected to be a string</param>
		/// <returns>Value cast into a <see cref="JsonNumber"/> if it was a number literal, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a number literal.</exception>
		[Pure, ContractAnnotation("null => halt")]
		public static JsonNumber AsNumber(this JsonValue? value)
			=> value as JsonNumber ?? throw ErrorValueIsNotANumber(value);

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonNumber"/>.</summary>
		/// <param name="value">JSON value expected to be a string</param>
		/// <returns>Value cast into a <see cref="JsonNumber"/> if it was a number literal, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a number literal.</exception>
		[Pure]
		public static JsonNumber? AsNumberOrDefault(this JsonValue? value)
			=> value.IsNullOrMissing() ? null : value as JsonNumber ?? throw ErrorValueIsNotANumber(value);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static JsonBindingException ErrorValueIsNotANumber(JsonValue? value)
			=> value.IsNullOrMissing()
				? new("Expected JSON Number was either null or missing.")
				: CrystalJson.Errors.Parsing_CannotCastToJsonNumber(value);

		#endregion

		#region AsString...

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonString"/>.</summary>
		/// <param name="value">JSON value expected to be a string</param>
		/// <returns>Value cast into a <see cref="JsonString"/> if it was a string literal, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a string literal.</exception>
		[Pure, ContractAnnotation("null => halt")]
		public static JsonString AsString(this JsonValue? value)
			=> value as JsonString ?? throw ErrorValueIsNotAString(value);

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonString"/>.</summary>
		/// <param name="value">JSON value expected to be either a string literal, or null/missing</param>
		/// <returns>Value cast into a <see cref="JsonString"/> it it was a string literal, <see langword="null"/> if it was null or missing, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is neither null nor a string literal.</exception>
		[Pure]
		public static JsonString? AsStringOrDefault(this JsonValue? value)
			=> value.IsNullOrMissing() ? null : value as JsonString ?? throw ErrorValueIsNotAString(value);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static JsonBindingException ErrorValueIsNotAString(JsonValue? value)
			=> value.IsNullOrMissing()
				? new("Expected JSON String was either null or missing.")
				: CrystalJson.Errors.Parsing_CannotCastToJsonString(value);

		#endregion

		#region AsBoolean...

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonString"/>.</summary>
		/// <param name="value">JSON value expected to be a string</param>
		/// <returns>Value cast into a <see cref="JsonString"/> if it was a string literal, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is not a string literal.</exception>
		[Pure, ContractAnnotation("null => halt")]
		public static JsonBoolean AsBoolean(this JsonValue? value)
			=> value as JsonBoolean ?? throw ErrorValueIsNotABoolean(value);

		/// <summary>Ensures that a <see cref="JsonValue"/> is is non-null <see cref="JsonString"/>.</summary>
		/// <param name="value">JSON value expected to be either a string literal, or null/missing</param>
		/// <returns>Value cast into a <see cref="JsonString"/> it it was a string literal, <see langword="null"/> if it was null or missing, or an exception if it is something else.</returns>
		/// <exception cref="JsonBindingException">If <paramref name="value"/> is neither null nor a string literal.</exception>
		[Pure]
		public static JsonBoolean? AsBooleanOrDefault(this JsonValue? value)
			=> value.IsNullOrMissing() ? null : value as JsonBoolean ?? throw ErrorValueIsNotABoolean(value);

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static JsonBindingException ErrorValueIsNotABoolean(JsonValue? value)
			=> value.IsNullOrMissing()
				? new("Expected JSON Boolean was either null or missing.")
				: CrystalJson.Errors.Parsing_CannotCastToJsonBoolean(value);

		#endregion

		#region Getters...

		/// <summary>Returns the value of the <b>required</b> field with the specified name, converted into an array with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue"></param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the required array is null or missing</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/></returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[Obsolete("Use Get<TValue[]>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TValue[] GetArray<TValue>(this JsonValue self, string key, TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null, string? message = null)
		{
			var value = self.GetValueOrDefault(key).RequiredField(key, message);
			if (value is not JsonArray arr)
			{
				throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
			}
			return arr.ToArray<TValue>(defaultValue, resolver)!;
		}

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into an array with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Values returned if the field is null or missing.</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing, or cannot be bound to the specified type.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		[Obsolete("Use Get<TValue[]>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TValue[]? GetArray<TValue>(this JsonValue? self, string key, TValue[]? defaultValue) => GetArray(self, key, defaultValue, null);

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into an array with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Values returned if the field is null or missing.</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		[Obsolete("Use Get<TValue[]>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TValue[]? GetArray<TValue>(this JsonValue? self, string key, TValue[]? defaultValue, ICrystalJsonTypeResolver? resolver)
		{
			var value = self?.GetValueOrDefault(key);
			switch (value)
			{
				case null or JsonNull:
				{
					return defaultValue;
				}
				case JsonArray arr:
				{
					return arr.ToArray<TValue>()!;
				}
				default:
				{
					throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
				}
			}
		}

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into an array with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Values returned if the field is null or missing.</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[Obsolete("Use Get<TValue[]>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TValue[] GetArray<TValue>(this JsonValue? self, string key, ReadOnlySpan<TValue> defaultValue) => GetArray(self, key, defaultValue, null);

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into an array with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Values returned if the field is null or missing.</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[Obsolete("Use Get<TValue[]>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static TValue[] GetArray<TValue>(this JsonValue? self, string key, ReadOnlySpan<TValue> defaultValue, ICrystalJsonTypeResolver? resolver)
		{
			var value = self?.GetValueOrDefault(key);
			switch (value)
			{
				case null or JsonNull:
				{
					return defaultValue.ToArray();
				}
				case JsonArray arr:
				{
					return arr.ToArray<TValue>()!;
				}
				default:
				{
					throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
				}
			}
		}

		/// <summary>Return the value of the <b>required</b> field with the specified name, converted into a list with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue"></param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the required array is null or missing</param>
		/// <returns>List of values converted into instances of type <typeparamref name="TValue"/></returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[Obsolete("Use Get<List<TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static List<TValue> GetList<TValue>(this JsonValue self, string key, TValue? defaultValue = default, ICrystalJsonTypeResolver? resolver = null, string? message = null)
		{
			Contract.NotNull(self);
			var value = self.GetValueOrDefault(key).RequiredField(key, message);
			if (value is not JsonArray arr) throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
			return arr.ToList<TValue>(defaultValue, resolver)!;
		}

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into a list with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">List returned if the field is null or missing.</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		[Obsolete("Use Get<List<TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static List<TValue>? GetList<TValue>(this JsonValue? self, string key, List<TValue>? defaultValue) => GetList(self, key, defaultValue, null);

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into a list with elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TValue">Type of the elements of the array</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">List returned if the field is null or missing.</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Array of values converted into instances of type <typeparamref name="TValue"/>, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		[Obsolete("Use Get<List<TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static List<TValue>? GetList<TValue>(this JsonValue? self, string key, List<TValue>? defaultValue, ICrystalJsonTypeResolver? resolver)
		{
			var value = self?.GetValueOrDefault(key);
			switch (value)
			{
				case null or JsonNull:
				{
					return defaultValue;
				}
				case JsonArray arr:
				{
					return arr.ToList<TValue>()!;
				}
				default:
				{
					throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
				}
			}
		}

		/// <summary>Return the value of the <b>required</b> field with the specified name, converted into a dictionary with keys of type <typeparamref name="TKey"/> and elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TKey">Type of the keys of the dictionary</typeparam>
		/// <typeparam name="TValue">Type of the elements of the dictionary</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <param name="message">Optional error message if the required array is null or missing</param>
		/// <returns>Dictionary of keys and values converted into instances of type <typeparamref name="TKey"/> and <typeparamref name="TValue"/> respectively.</returns>
		/// <exception cref="JsonBindingException">The field is null or missing, or cannot be bound to the specified type.</exception>
		[Obsolete("Use Get<Dictionary<TKey, TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		public static Dictionary<TKey, TValue> GetDictionary<TKey, TValue>(this JsonValue self, string key, ICrystalJsonTypeResolver? resolver = null, string? message = null) where TKey : notnull
		{
			Contract.NotNull(self);
			var value = self.GetValue(key);
			if (value is not JsonObject obj) throw CrystalJson.Errors.Parsing_CannotCastToJsonObject(value);
			return obj.ToDictionary<TKey, TValue>(resolver);
		}

		/// <summary>Return the value of the <i>optional</i> field with the specified name, converted into a dictionary with keys of type <typeparamref name="TKey"/> and elements of type <typeparamref name="TValue"/></summary>
		/// <typeparam name="TKey">Type of the keys of the dictionary</typeparam>
		/// <typeparam name="TValue">Type of the elements of the dictionary</typeparam>
		/// <param name="self">Parent object</param>
		/// <param name="key">Name of the field</param>
		/// <param name="defaultValue">Dictionary returned if the field is null or missing.</param>
		/// <param name="resolver">Optional custom type resolver</param>
		/// <returns>Dictionary of keys and values converted into instances of type <typeparamref name="TKey"/> and <typeparamref name="TValue"/> respectively, or <paramref name="defaultValue"/> if the field was null or missing.</returns>
		/// <exception cref="JsonBindingException">The field is null, missing or cannot be bound to the specified type.</exception>
		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		[Obsolete("Use Get<Dictionary<TKey, TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static Dictionary<TKey, TValue>? GetDictionary<TKey, TValue>(this JsonValue? self, string key, Dictionary<TKey, TValue>? defaultValue, ICrystalJsonTypeResolver? resolver) where TKey : notnull
		{
			var value = self?.GetValueOrDefault(key);
			switch (value)
			{
				case null or JsonNull:
				{
					return defaultValue;
				}
				case JsonObject obj:
				{
					return obj.ToDictionary<TKey, TValue>(resolver);
				}
				default:
				{
					throw CrystalJson.Errors.Parsing_CannotCastToJsonArray(value);
				}
			}
		}

		[RequiresUnreferencedCode(AotMessages.TypeMightBeRemoved)]
		[Obsolete("Use Get<Dictionary<TKey, TValue>>() instead.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static Dictionary<TKey, TValue>? GetDictionary<TKey, TValue>(this JsonValue? self, string key, Dictionary<TKey, TValue>? defaultValue) where TKey : notnull
			=> GetDictionary(self, key, defaultValue, null);

		#endregion

	}

	/// <summary>Advanced methods only intended to be used by generated code</summary>
	/// <remarks><b>CAUTION</b>: misuse of this type MAY cause runtime corruption!</remarks>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class CrystalJsonMarshall
	{

		/// <summary>Marks a JSON Object as read-only, without recursively freezing its children.</summary>
		/// <remarks>
		/// <para>This is only intended to be used when the caller is <b>ABSOLUTELY SURE</b> that all the children in the object are already read-only.</para>
		/// <para>If any children is still mutable, this will break the invariant contract of read-only objects.</para>
		/// </remarks>
		public static JsonObject FreezeTopLevel(JsonObject obj)
		{
			Contract.Debug.Requires(obj != null);
			return obj.FreezeUnsafe();
		}

		/// <summary>Marks a JSON Array as read-only, without recursively freezing its items.</summary>
		/// <remarks>
		/// <para>This is only intended to be used when the caller is <b>ABSOLUTELY SURE</b> that all the items in the array are already read-only.</para>
		/// <para>If any item is still mutable, this will break the invariant contract of read-only objects.</para>
		/// </remarks>
		public static JsonArray FreezeTopLevel(JsonArray array)
		{
			Contract.Debug.Requires(array != null);
			return array.FreezeUnsafe();
		}

		/// <summary>Returns a JSON Array that wraps an existing array of values, without copying any content.</summary>
		/// <param name="items">Buffer that contains a set of JSON values, which should be used by the new JSON Array.</param>
		/// <param name="size">Number of items from the start of <paramref name="items"/> that will make up the array</param>
		/// <param name="readOnly">If <c>true</c>, mark the array as read-only, in which case all elements in <paramref name="items"/> <b>MUST</b> also be read-only!</param>
		/// <returns>JSON Array that reuse the existing buffer</returns>
		/// <remarks>
		/// <para>This is only intended to be used when the caller is <b>ABSOLUTELY SURE</b> that <b>no</b> item in the buffer is <c>null</c></para>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, but some items are mutable, this would violate the invariants of <see cref="JsonArray"/> and break your application!</para>
		/// </remarks>
		public static JsonArray CreateArrayFromValues(JsonValue[]? items, int size, bool readOnly)
		{
			if (size == 0) return readOnly ? JsonArray.ReadOnly.Empty : new();

			ArgumentNullException.ThrowIfNull(items);
			ArgumentOutOfRangeException.ThrowIfGreaterThan((uint) size, (uint) items.Length);
			items.AsSpan(0, size);
			return new(items, size, readOnly);
		}

		/// <summary>Returns a JSON Object that wraps an existing dictionary of key/value pairs, </summary>
		/// <param name="items">Dictionary that contains a set of key/value pairs</param>
		/// <param name="readOnly">If <c>true</c>, mark the object as read-only, in which case all elements in <paramref name="items"/> <b>MUST</b> also be read-only!</param>
		/// <returns>JSON Array that reuse the existing buffer</returns>
		/// <remarks>
		/// <para>This is only intended to be used when the caller is <b>ABSOLUTELY SURE</b> that <b>NO</b> item in the dictionary is <c>null</c>.</para>
		/// <para>If <paramref name="readOnly"/> is <c>true</c>, but some items are mutable, this would violate the invariants of <see cref="JsonObject"/> and break your application!</para>
		/// </remarks>
		public static JsonObject CreateObjectFromDictionary(Dictionary<string, JsonValue>? items, bool readOnly)
		{
			if (items is null || items.Count == 0) return readOnly ? JsonObject.ReadOnly.Empty : new();

			return new(items, readOnly);
		}

	}

}
