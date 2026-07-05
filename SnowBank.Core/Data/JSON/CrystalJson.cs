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

//#define DEBUG_JSON_PARSER

// ReSharper disable ArrangeStaticMemberQualifier

namespace SnowBank.Data.Json
{
	using System.Buffers;
	using System.ComponentModel;
	using System.IO;
	using System.Reflection;
	using System.Text;
	using SnowBank.Collections.Caching;
	using SnowBank.Runtime;

	/// <summary>Helper class to serialize, parse or deserialize JSON documents</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public static class CrystalJson
	{

		/// <summary>Default type resolver</summary>
		public static readonly CrystalJsonTypeResolver DefaultResolver = new();

		/// <summary>Options when save JSON to disk</summary>
		[Flags]
		public enum SaveOptions
		{
			/// <summary>Use the default options</summary>
			None = 0,
			/// <summary>If the file already exists, save first into a temporary file, and swap it with the previous one in a single step</summary>
			AtomicSave = 1,
			/// <summary>If the file already exists, a backup copy will be created (with the ".bak" extension)</summary>
			KeepBackup = 2,
			/// <summary>Append to the end of the file (create it if necessary), instead of overwriting it. Should only be used to JSON fragments, or JSON logs</summary>
			Append = 4,
		}

		/// <summary>Options when load JSON from disk</summary>
		[Flags]
		public enum LoadOptions
		{
			/// <summary>Use the default options</summary>
			None = 0,
			/// <summary>If the file does not exist, return the default value of the type (null, 0, false, ...)</summary>
			ReturnNullIfMissing = 1,
			/// <summary>If the source is using streaming (socket, ...), do not wait to reach the end of the file, and stop once a complete top-level value as been consumed.</summary>
			Streaming = 2
		}

		#region Serialization...

		/// <summary>Serializes a boxed value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToString(value, typeof(object), settings, resolver);
		}

		/// <summary>Serializes a boxed value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="declaredType">Type of the field or property, as declared in the parent type.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize(object? value, Type declaredType, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToString(value, declaredType, settings, resolver);
		}

		/// <summary>Serializes a value (of any type)</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		[OverloadResolutionPriority(1)]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use value.ToJsonText(...) instead")]
		public static string SerializeJson(JsonValue? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return value?.ToJsonText(settings, resolver) ?? JsonTokens.Null;
		}

		/// <summary>Serializes a value that implements <see cref="IJsonSerializable"/></summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		[EditorBrowsable(EditorBrowsableState.Never)]
		[Obsolete("Use value.ToJsonText(...) instead")]
		public static string SerializeJson(IJsonSerializable? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return value?.ToJsonText(settings, resolver) ?? JsonTokens.Null;
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a string literal</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value == null)
			{ // special case for null instances
				return JsonTokens.Null;
			}

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitValue<T>(value, writer);

				return writer.GetString();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a string literal, using a customer serializer</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="serializer">Custom serializer for this type</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static string Serialize<T>(T? value, IJsonSerializer<T>? serializer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value == null)
			{ // special case for null instances
				return JsonTokens.Null;
			}

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				if (serializer != null)
				{
					serializer.Serialize(writer, value);
				}
				else
				{
					CrystalJsonVisitor.VisitValue<T>(value, writer);
				}

				return writer.GetString();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Performs a custom seriazation operation, using a pooled <see cref="CrystalJsonWriter"/>.</summary>
		/// <typeparam name="TState">Type of the state that is forwarded to <paramref name="handler"/></typeparam>
		/// <param name="state">State that is forwarded to <paramref name="handler"/></param>
		/// <param name="handler">Handler that receives a pooled writer, and forwarded <paramref name="state"/>, and will use the writer to produce some JSON and consume the result.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <remarks>
		/// <para>The handler is expected to call any of the <see cref="CrystalJsonWriter.GetString"/>, <see cref="CrystalJsonWriter.GetUtf8Slice(ArrayPool{byte})"/> or similar methods, before returning.</para>
		/// <para>The handler *MUST NOT* expose the pooled writer to the outside! Doing this would break the application</para>
		/// </remarks>
		public static void Convert<TState>(TState state, Action<CrystalJsonWriter, TState> handler, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(handler);

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				handler(writer, state);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Performs a custom serialization operation, using a pooled <see cref="CrystalJsonWriter"/>.</summary>
		/// <typeparam name="TState">Type of the state that is forwarded to <paramref name="handler"/></typeparam>
		/// <typeparam name="TResult">Type of the result returned by the handler</typeparam>
		/// <param name="state">State that is forwarded to <paramref name="handler"/></param>
		/// <param name="handler">Handler that receives a pooled writer, and forwarded <paramref name="state"/>, and will use the writer to produce some JSON and consume the result.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The result of the custom serialization (usually a <see cref="string"/>, <see cref="Slice"/> or any other value)</returns>
		/// <remarks>
		/// <para>The handler is expected to call any of the <see cref="CrystalJsonWriter.GetString()"/>, <see cref="CrystalJsonWriter.GetUtf8Slice(ArrayPool{byte})"/> or similar methods, to extract the content of the writer.</para>
		/// <para>The handler *MUST NOT* expose the pooled writer to the outside! Doing this would break the application</para>
		/// </remarks>
		public static TResult Convert<TState, TResult>(TState state, Func<CrystalJsonWriter, TState, TResult> handler, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(handler);

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				return handler(writer, state);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Performs a custom seriazation operation, using a pooled <see cref="CrystalJsonWriter"/>.</summary>
		/// <typeparam name="TState">Type of the state that is forwarded to <paramref name="handler"/></typeparam>
		/// <param name="state">State that is forwarded to <paramref name="handler"/></param>
		/// <param name="handler">Handler that receives a pooled writer, and forwarded <paramref name="state"/>, and will use the writer to produce some JSON and consume the result.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <remarks>
		/// <para>The handler is expected to call any of the <see cref="CrystalJsonWriter.GetString"/>, <see cref="CrystalJsonWriter.GetUtf8Slice(ArrayPool{byte})"/> or similar methods, before returning.</para>
		/// <para>The handler *MUST NOT* expose the pooled writer to the outside! Doing this would break the application</para>
		/// </remarks>
		public static async Task ConvertAsync<TState>(TState state, Func<CrystalJsonWriter, TState, Task> handler, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(handler);

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				await handler(writer, state).ConfigureAwait(false);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Performs a custom serialization operation, using a pooled <see cref="CrystalJsonWriter"/>.</summary>
		/// <typeparam name="TState">Type of the state that is forwarded to <paramref name="handler"/></typeparam>
		/// <typeparam name="TResult">Type of the result returned by the handler</typeparam>
		/// <param name="state">State that is forwarded to <paramref name="handler"/></param>
		/// <param name="handler">Handler that receives a pooled writer, and forwarded <paramref name="state"/>, and will use the writer to produce some JSON and consume the result.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The result of the custom serialization (usually a <see cref="string"/>, <see cref="Slice"/> or any other value)</returns>
		/// <remarks>
		/// <para>The handler is expected to call any of the <see cref="CrystalJsonWriter.GetString"/>, <see cref="CrystalJsonWriter.GetUtf8Slice(ArrayPool{byte})"/> or similar methods, to extract the content of the writer.</para>
		/// <para>The handler *MUST NOT* expose the pooled writer to the outside! Doing this would break the application</para>
		/// </remarks>
		public static async Task<TResult> ConvertAsync<TState, TResult>(TState state, Func<CrystalJsonWriter, TState, Task<TResult>> handler, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(handler);

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				return await handler(writer, state).ConfigureAwait(false);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a boxed value (of any type) into the specified buffer</summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="buffer">Destination buffer (created automatically if null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The value of <paramref name="buffer"/>, for call chaining</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		[Pure]
		public static StringBuilder Serialize(object? value, StringBuilder? buffer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			if (value == null)
			{ // special case for null instances
				return buffer?.Append(JsonTokens.Null) ?? new StringBuilder(JsonTokens.Null);
			}

			// grab a new buffer if needed
			buffer ??= CreateBufferFromSettings(settings);

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitValue(value, typeof(object), writer);

				writer.CopyTo(buffer);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}

			return buffer;
		}

		/// <summary>Creates a new empty buffer with the appropriate size</summary>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Empty StringBuilder, with an initial capacity that depends on the settings</returns>
		[Pure]
		private static StringBuilder CreateBufferFromSettings(CrystalJsonSettings? settings)
		{
			int capacity = settings?.OptimizeForLargeData == true ? 4096 : 512;
			return new StringBuilder(capacity);
		}

		/// <summary>Serializes a boxed value (of any type) into the specified buffer</summary>
		/// <param name="value">Class, struct, Enumerable, Nullable&lt;T&gt;, ...</param>
		/// <param name="declaredType"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		[Pure]
		private static string SerializeToString(object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			if (value == null)
			{ // special case for null instances
				return JsonTokens.Null;
			}

			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitValue(value, declaredType, writer);

				return writer.GetString();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a boxed value (of any type) into the specified output</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The <paramref name="output"/> instance, for call chaining</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static TextWriter SerializeTo(TextWriter output, object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToTextWriter(output, value, typeof(object), settings, resolver);
		}

		/// <summary>Serializes a value (of any type) into the specified output</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>The <paramref name="output"/> instance, for call chaining</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static TextWriter SerializeTo<T>(TextWriter output, T value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return SerializeToTextWriter(output, value, typeof(T), settings, resolver);
		}

		/// <summary>Serializes a value (of any type) into the specified stream</summary>
		/// <param name="output">Output for the JSON document</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SerializeTo<T>(Stream output, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNull(output);

			SerializeToStream(output, value, typeof(T), settings, resolver);
		}

		/// <summary>Serializes a boxed <paramref name="value"/> (of any type) into the file at the specified <paramref name="path"/></summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <param name="options">Save settings</param>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SaveTo(string path, object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(object), settings, resolver, options);
		}

		/// <summary>Serializes a <paramref name="value"/> (of any type) into the file at the specified <paramref name="path"/></summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <param name="options">Save settings</param>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static void SaveTo<T>(string path, T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, SaveOptions options = SaveOptions.None)
		{
			SerializeAndSaveInternal(path, value, typeof(T), settings, resolver, options);
		}

		/// <summary>Serializes a boxed value into an indented JSON string, suitable for humans (logging, troubleshooting, ...)</summary>
		[Pure]
		public static string Dump(object? value)
		{
			return Serialize(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>Serializes a value into an indented JSON string, suitable for humans (logging, troubleshooting, ...)</summary>
		[Pure]
		public static string Dump<TValue>(TValue? value)
		{
			return Serialize(value, CrystalJsonSettings.JsonIndented);
		}

		/// <summary>Serializes a boxed value into an in-memory buffer</summary>
		/// <returns>Byte array that contains the resulting JSON document</returns>
		[Pure]
		public static byte[] ToBytes(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return ToSlice(value, settings, resolver).ToArray();
		}

		/// <summary>Serializes a value into an in-memory buffer</summary>
		/// <returns>Byte array that contains the resulting JSON document</returns>
		[Pure]
		public static byte[] ToBytes<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return ToSlice<T>(value, settings, resolver).ToArray();
		}

		/// <summary>Serializes a value into an in-memory buffer</summary>
		/// <returns>Byte array that contains the resulting JSON document</returns>
		[Pure]
		public static byte[] ToBytes<T>(T? value, IJsonSerializer<T> serializer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return ToSlice<T>(value, serializer, settings, resolver).ToArray();
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="JsonSerializationException">if the serialization fails</exception>
		[Pure]
		public static Slice ToSlice(object? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
			=> ToSlice(value, typeof(object), settings, resolver);

		internal static ObjectPool<CrystalJsonWriter> WriterPool = new(() => new CrystalJsonWriter());

		/// <summary>Serializes a value into an UTF-8 encoded Slice</summary>
		/// <typeparam name="T">Advertised type of the instance.</typeparam>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		[Pure]
		public static Slice ToSlice<T>(T? value, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitValue(value, writer);

				return writer.GetUtf8Slice();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a JSON value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		[Pure]
		public static Slice ToJsonSlice(JsonValue? value, CrystalJsonSettings? settings = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, null);

				(value ?? JsonNull.Null).JsonSerialize(writer);

				return writer.GetUtf8Slice();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a JSON value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <see langword="null"/>)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		[Pure]
		public static SliceOwner ToJsonSlice(JsonValue? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, null);

				(value ?? JsonNull.Null).JsonSerialize(writer);

				return writer.GetUtf8Slice(pool);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="Slice"/>, using a customer serializer</summary>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		public static Slice ToSlice<T>(T? value, IJsonSerializer<T>? serializer, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				if (serializer != null)
				{
					serializer.Serialize(writer, value);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, writer);
				}

				return writer.GetUtf8Slice();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		[Pure, MustDisposeResource]
		public static SliceOwner ToSlice<T>(T? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitValue(value, writer);
				
				return writer.GetUtf8Slice(pool);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a span of values of type <typeparamref name="T"/> as a JSON Array into a <see cref="SliceOwner"/> using the specified <see cref="ArrayPool{T}">pool</see></summary>
		/// <param name="values">Span of the instances to serialize (can be null)</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`[ 123, 456, 789, ... ]`</c>, <c>`[ true, true, false, ... ]`</c>, <c>`[ "ABC", "DEF", "GHI", ... ]`</c>, <c>`[ { "foo":..., "bar": ... }, { "foo":..., "bar": ... }, ... ]`</c>, <c>`[ [ ... ], [ ... ], ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If any value fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		public static SliceOwner ToSlice<T>(ReadOnlySpan<T?> values, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				CrystalJsonVisitor.VisitSpan(values, writer);
				return writer.GetUtf8Slice(pool);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a value of type <typeparamref name="T"/> into a <see cref="SliceOwner"/>, using a customer serializer, and the specified <see cref="ArrayPool{T}">pool</see></summary>
		/// <param name="value">Instance to serialize (can be null)</param>
		/// <param name="serializer">Custom serializer</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns><c>`123`</c>, <c>`true`</c>, <c>`"ABC"`</c>, <c>`{ "foo":..., "bar": ... }`</c>, <c>`[ ... ]`</c>, ...</returns>
		/// <exception cref="JsonSerializationException">If the object fails to serialize properly (non-serializable type, loop in the object graph, ...)</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		public static SliceOwner ToSlice<T>(T? value, IJsonSerializer<T>? serializer, ArrayPool<byte>? pool, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);
				if (serializer != null)
				{
					serializer.Serialize(writer, value);
				}
				else if (value is JsonValue j)
				{
					j.JsonSerialize(writer);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, writer);
				}

				return writer.GetUtf8Slice(pool);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <see langword="null"/>)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if <see langword="null"/>)</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// </remarks>
		[Pure]
		public static SliceOwner ToSlice(object? value, ArrayPool<byte>? pool, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			return ToSlice(value, typeof(object), pool, settings, resolver);
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice</summary>
		/// <param name="value">Instance to serialize (of any type)</param>
		/// <param name="type">The advertised type of the instance, if known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <see langword="null"/>)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if <see langword="null"/>)</param>
		/// <param name="pool">Pool used to allocate the content of the slice (use <see cref="ArrayPool{T}.Shared"/> if <see langword="null"/>)</param>
		/// <returns>Slice of memory that contains the utf-8 encoded JSON document</returns>
		/// <exception cref="JsonSerializationException">if the serialization fails</exception>
		/// <remarks>
		/// <para>The <see cref="SliceOwner"/> returned <b>MUST</b> be disposed; otherwise, the rented buffer will not be returned to the <paramref name="pool"/>.</para>
		/// <para>If <paramref name="type"/> is an interface or abstract class, or if <paramref name="value"/> is a derived type of <paramref name="type"/>, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static SliceOwner ToSlice(object? value, Type? type, ArrayPool<byte>? pool, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				if (value is JsonValue j)
				{
					j.JsonSerialize(writer);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, writer);
				}
				return writer.GetUtf8Slice(pool);
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
			
		}

		/// <summary>Serializes a boxed value into an UTF-8 encoded Slice.</summary>
		/// <param name="value">Instance to serialize (of any type).</param>
		/// <param name="type">The advertised type of the instance, if known.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if <see langword="null"/>).</param>
		/// <param name="resolver">Custom type resolver (use default behavior if <see langword="null"/>).</param>
		/// <returns>Slice of memory that contains the UTF-8 encoded JSON document.</returns>
		/// <exception cref="JsonSerializationException">Thrown if the serialization fails.</exception>
		/// <remarks>
		/// <para>If the type of the value is an interface or abstract class, or if the value is a derived type, the serialized document may include an additional attribute with the original type name, which may not be recognized by other libraries or platforms.</para>
		/// </remarks>
		[Pure]
		public static Slice ToSlice(object? value, Type? type, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			var writer = WriterPool.Allocate();
			try
			{
				writer.Initialize(0, settings, resolver);

				if (value is JsonValue j)
				{
					j.JsonSerialize(writer);
				}
				else
				{
					CrystalJsonVisitor.VisitValue(value, type ?? typeof(object), writer);
				}
				return writer.GetUtf8Slice();
			}
			finally
			{
				writer.Dispose();
				WriterPool.Free(writer);
			}
		}

		/// <summary>Serializes a boxed value to a <see cref="TextWriter"/>.</summary>
		/// <param name="output">The destination where the serialized JSON document will be written.</param>
		/// <param name="value">The instance to serialize, which can be <see langword="null"/>.</param>
		/// <param name="declaredType">The advertised type of the instance. Use <c>typeof(object)</c> if unknown.</param>
		/// <param name="settings">The serialization settings. If <see langword="null"/>, default JSON settings will be used.</param>
		/// <param name="resolver">A custom type resolver. If <see langword="null"/>, the default behavior will be used.</param>
		/// <returns>The same instance as <paramref name="output"/>.</returns>
		private static TextWriter SerializeToTextWriter(TextWriter output, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			Contract.NotNull(output);

			if (value == null)
			{ // cas spécial pour null
				output.Write(JsonTokens.Null);
			}
			else
			{
				var writer = WriterPool.Allocate();
				try
				{
					//REVIEW: what value for auto-flush ?
					writer.Initialize(output, 65536, settings, resolver);

					CrystalJsonVisitor.VisitValue(value, declaredType, writer);
					
					writer.Flush(last: true);
				}
				finally
				{
					writer.Dispose();
					WriterPool.Free(writer);
				}
			}
			return output;
		}

		/// <summary>Serializes a boxed value to a <see cref="Stream"/>.</summary>
		/// <param name="output">The destination stream where the serialized JSON document will be written.</param>
		/// <param name="value">The instance to serialize, which can be <see langword="null"/>.</param>
		/// <param name="declaredType">The advertised type of the instance. Use <c>typeof(object)</c> if unknown.</param>
		/// <param name="settings">The serialization settings. If <see langword="null"/>, default JSON settings will be used.</param>
		/// <param name="resolver">A custom type resolver. If <see langword="null"/>, the default behavior will be used.</param>
		/// <returns>The same instance as <paramref name="output"/>.</returns>
		private static Stream SerializeToStream(Stream output, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver)
		{
			Contract.NotNull(output);

			if (ReferenceEquals(output, Stream.Null))
			{
				return output;
			}

			if (value == null)
			{ // cas spécial pour null
				output.Write("null"u8);
			}
			else
			{
				var writer = WriterPool.Allocate();
				try
				{
					//REVIEW: what value for auto-flush ?
					writer.Initialize(output, 65536, settings, resolver);

					CrystalJsonVisitor.VisitValue(value, declaredType, writer);
					
					writer.Flush(last: true);
				}
				finally
				{
					writer.Dispose();
					WriterPool.Free(writer);
				}
			}
			return output;
		}

		/// <summary>Creates a new <see cref="StreamWriter"/> that can be used to serialize JSON into a file, with the specified settings</summary>
		/// <param name="path">Path to the output file</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Writer that will output bytes into the specified file</returns>
		/// <remarks>The stream will be tuned according to the serialization settings used, especially if planning to write a large amount of data.</remarks>
		private static Stream OpenJsonStream(string path, CrystalJsonSettings? settings)
		{
			var bufferSize = 0x400;
			if (settings != null && settings.OptimizeForLargeData)
			{
				bufferSize = 0x14000; // 80K
			}

			return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize << 2, FileOptions.SequentialScan | FileOptions.WriteThrough);
		}

		private static void SerializeAndSaveInternal(string path, object? value, Type declaredType, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, SaveOptions options)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			string savePath = path;
			string? bakPath = null;

			if ((options & SaveOptions.Append) == SaveOptions.Append)
			{
				throw new NotSupportedException("Append save is not supported");
			}

			#region Settings...

			bool doAtomicUpdate = false;
			if (File.Exists(path))
			{ // A file with the same name already exists, we will need to swap or backup
				if ((options & SaveOptions.AtomicSave) == SaveOptions.AtomicSave)
				{
					doAtomicUpdate = true;
					savePath += ".new";
				}
				if ((options & SaveOptions.KeepBackup) == SaveOptions.KeepBackup)
				{
					bakPath = savePath + ".bak";
				}
			}
			else
			{ // No file with this name was found

				// this could be due to the fact that the folder does not exist
				string parent = Path.GetDirectoryName(path)!;
				Contract.Debug.Assert(parent != null);
				if (!Directory.Exists(parent))
				{
					// create the path!
					//REVIEW: TODO: should we have an option for this ? AutoCreatePath = true|false ?
					Directory.CreateDirectory(parent);
				}
			}

			#endregion

			if (doAtomicUpdate)
			{ // Replace the file using "atomic" semantics
			  // - Save to a temporary ".new" file (in the same folder)
			  // - If serialization fails, the ".new" file is deleted
			  // - Replace the previous file with the ".new" file
			  // - The previous file maybe be renamed into a ".bak" if the KeepBackup options is enabled.

				try
				{
					using (var output = OpenJsonStream(savePath, settings))
					{
						SerializeToStream(output, value, declaredType, settings, resolver);

						// some Stream implementations don't property Flush after the last byte, so we do an explicit flush here
						output.Flush();
					}
				}
				catch (Exception)
				{
					if (File.Exists(savePath)) File.Delete(savePath);
					throw;
				}

				// Swap the old and new files
				File.Replace(savePath, path, bakPath);
			}
			else if (bakPath != null)
			{ // Overwrite the previous file, but keep a backup of the previous file
				// - Rename the previous file into ".bak" (deleted any previous ".bak" file)
				// - Save the new data into the destination file
				// - In case of a serialization error, we try to rename the ".bak" into the original name (which _could_ fail!)

				bool swapped = false;
				try
				{
					File.Replace(savePath, bakPath, null);
					swapped = true;

					using (var output = OpenJsonStream(savePath, settings))
					{
						SerializeToStream(output, value, declaredType, settings, resolver);

						output.Flush();
					}
				}
				catch (Exception)
				{
					if (swapped)
					{ // Revert using the backup file!
						File.Replace(bakPath, savePath, null);
					}
					throw;
				}

			}
			else
			{ // The file does not exist on disk
				// - Save directly to disk
				// - In case of serialization error, delete the (incomplete) file

				try
				{
					using (var output = OpenJsonStream(savePath, settings))
					{
						SerializeToStream(output, value, declaredType, settings, resolver);
						output.Flush();
					}
				}
				catch (Exception)
				{
					if (File.Exists(savePath)) File.Delete(savePath);
					throw;
				}
			}
		}

		#endregion

		#region Parsing...

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string? jsonText,
			CrystalJsonSettings? settings = null
		)
		{
#if DEBUG_JSON_PARSER
			Debug.WriteLine("CrystalJson.Parse('" + jsonText + "', ...)");
#endif
			return ParseFromReader(new JsonStringReader(jsonText), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(byte[]? jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.AsSlice(), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FormatException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(Slice jsonBytes, CrystalJsonSettings? settings = null)
		{
			if (jsonBytes.IsNullOrEmpty) return JsonNull.Missing;
			return ParseFromReader(new JsonSliceReader(jsonBytes), settings);
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			if (jsonBytes.Length == 0) return JsonNull.Missing;
			unsafe
			{
				fixed (byte* first = jsonBytes)
				{
					return ParseFromReader(new JsonUnmanagedReader(first, jsonBytes.Length), settings);
				}
			}
		}

		/// <summary>Parses a JSON buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonBytes.Span, settings);
		}

		/// <summary>Parses a JSON sequence of buffer, and returns the corresponding JSON value</summary>
		/// <param name="jsonBytes">Sequence of buffers containing the UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonBytes"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ref ReadOnlySequence<byte> jsonBytes, CrystalJsonSettings? settings = null)
		{
			if (jsonBytes.IsSingleSegment)
			{
				return Parse(jsonBytes.First.Span, settings);
			}

			long len = jsonBytes.Length;
			if (len > int.MaxValue) throw new NotSupportedException("Cannot parse sequence of bytes larger than 2 GiB.");
			using (var scratch = MemoryPool<byte>.Shared.Rent((int) len))
			{
				var mem = scratch.Memory.Span;
				int p = 0;
				foreach (var chunk in jsonBytes)
				{
					chunk.Span.CopyTo(mem.Slice(p, chunk.Length));
					p += chunk.Length;
				}
				Contract.Debug.Assert(p == mem.Length);
				return Parse(mem, settings);
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ReadOnlySpan<char> jsonText, CrystalJsonSettings? settings = null)
		{
			if (jsonText.Length == 0) return JsonNull.Null;
			unsafe
			{
				fixed (char* first = jsonText)
				{
					return ParseFromReader(new JsonCharReader(first, jsonText.Length), settings);
				}
			}
		}

		/// <summary>Parses a JSON text literal, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue Parse(ReadOnlyMemory<char> jsonText, CrystalJsonSettings? settings = null)
		{
			return Parse(jsonText.Span, settings);
		}

		/// <summary>Parses a JSON text literal sequence, and returns the corresponding JSON value</summary>
		/// <param name="jsonText">Sequence of buffers containing the JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="jsonText"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue Parse(ref ReadOnlySequence<char> jsonText, CrystalJsonSettings? settings = null)
		{
			if (jsonText.IsSingleSegment)
			{
				return Parse(jsonText.First.Span, settings);
			}

			long len = jsonText.Length;
			if (len > int.MaxValue) throw new NotSupportedException("Cannot parse sequence of chars larger than 4 GiB.");
			using (var scratch = MemoryPool<char>.Shared.Rent((int) len))
			{
				var mem = scratch.Memory.Span;
				int p = 0;
				foreach (var chunk in jsonText)
				{
					chunk.Span.CopyTo(mem.Slice(p, chunk.Length));
					p += chunk.Length;
				}
				Contract.Debug.Assert(p == mem.Length);
				return Parse(mem, settings);
			}
		}

		/// <summary>Reads the content of a reader, and returns the corresponding JSON value</summary>
		/// <param name="reader">Instance from which to read the JSON text document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="reader"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue ParseFrom(TextReader reader, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(reader);
			return ParseFromReader(new JsonTextReader(reader), settings);
		}

		/// <summary>Reads the content of a reader, and returns the corresponding JSON value</summary>
		/// <param name="source">Instance from which to read the JSON text document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="source"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		internal static JsonValue ParseFromReader<TReader>(TReader source, CrystalJsonSettings? settings = null)
			where TReader : struct, IJsonReader
		{
			Contract.NotNull(source);

			var tokenizer = default(CrystalJsonTokenizer<TReader>);
			try
			{
				tokenizer = new CrystalJsonTokenizer<TReader>(source, settings ?? CrystalJsonSettings.Json);
				return CrystalJsonParser<TReader>.ParseJsonValue(ref tokenizer) ?? JsonNull.Missing;
			}
			finally
			{
				tokenizer.Dispose();
			}
		}

		/// <summary>Parses the next JSON fragment from a text literal</summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="charsRead">Receives the number of characters read from <paramref name="jsonText"/>, or <c>0</c> if no fragment was found.</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value, or <see cref="JsonNull.Missing"/> if <paramref name="jsonText"/> is empty or composed only of white-space characters</returns>
		/// <remarks>
		/// <para>When called in a loop to consume a multi-fragment buffer, the caller should stop when <paramref name="charsRead"/> is <c>0</c>.</para>
		/// <para>This methods will skip any initial whitespaces (which will be included in <paramref name="charsRead"/>), but will not skip any trailing whitespaces.</para>
		/// </remarks>
		public static JsonValue ParseFragment(ReadOnlySpan<char> jsonText, out int charsRead, CrystalJsonSettings? settings = null)
		{
			if (jsonText.Trim().Length == 0)
			{
				charsRead = 0;
				return JsonNull.Missing;
			}

			var tokenizer = default(CrystalJsonTokenizer<JsonCharReader>);
			try
			{
				unsafe
				{
					fixed (char* buffer = jsonText)
					{
						tokenizer = new(new JsonCharReader(buffer, jsonText.Length), settings ?? CrystalJsonSettings.Json);

						var result = CrystalJsonParser<JsonCharReader>.ParseJsonValue(ref tokenizer) ?? JsonNull.Missing;
						charsRead = tokenizer.Source.Consumed;
						if (tokenizer.HasReadNextToken())
						{
							--charsRead;
						}
						return result;
					}
				}
			}
			finally
			{
				tokenizer.Dispose();
			}
		}

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="path">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="options">Options used for streaming operations.</param>
		/// <returns>Corresponding JSON value. If the file is missing or empty and the <see cref="LoadOptions.ReturnNullIfMissing"/> flag is set in <paramref name="options"/>, <see cref="JsonNull.Missing"/> will be returned instead.</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FileNotFoundException">If the file does not exist, and flag <see cref="LoadOptions.ReturnNullIfMissing"/> is not set in <paramref name="options"/></exception>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static JsonValue ParseFrom(string path, CrystalJsonSettings? settings = null, LoadOptions options = LoadOptions.None)
		{
			return LoadAndParseInternal(path, settings, options);
		}

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="path">Instance from which to read the JSON document</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Corresponding JSON value.</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please use one of the overloads that accepts a <see cref="CrystalJsonSettings"/> parameter.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FileNotFoundException">If the file does not exist.</exception>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		public static Task<JsonValue> ParseFromAsync(string path, CancellationToken ct)
			=> LoadAndParseInternalAsync(path, null, LoadOptions.None, ct);

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="path">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Corresponding JSON value.</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FileNotFoundException">If the file does not exist.</exception>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		public static Task<JsonValue> ParseFromAsync(string path, CrystalJsonSettings? settings, CancellationToken ct)
			=> LoadAndParseInternalAsync(path, settings, LoadOptions.None, ct);

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="path">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="options">Options used for streaming operations.</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Corresponding JSON value. If the file is missing or empty and the <see cref="LoadOptions.ReturnNullIfMissing"/> flag is set in <paramref name="options"/>, <see cref="JsonNull.Missing"/> will be returned instead.</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="FileNotFoundException">If the file does not exist, and flag <see cref="LoadOptions.ReturnNullIfMissing"/> is not set in <paramref name="options"/></exception>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Task<JsonValue> ParseFromAsync(string path, CrystalJsonSettings? settings, LoadOptions options, CancellationToken ct)
			=> LoadAndParseInternalAsync(path, settings, options, ct);

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="source">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <returns>Corresponding JSON value. If <paramref name="source"/> is empty, will return <see cref="JsonNull.Missing"/></returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static JsonValue ParseFrom(Stream source, CrystalJsonSettings? settings = null)
		{
			Contract.NotNull(source);

			if (!source.CanRead) throw new InvalidOperationException("Cannot read from source stream");

			if (ReferenceEquals(source, Stream.Null))
			{ // not content
				return JsonNull.Missing;
			}

			if (source is MemoryStream ms && ms.TryGetBuffer(out var buffer))
			{ // read from the buffer directly
				return Parse(buffer.AsSlice(), settings);
			}

			return ParseFromStream(source, settings);

			static JsonValue ParseFromStream(Stream source, CrystalJsonSettings? settings)
			{
				using var reader = new StreamReader(source, Encoding.UTF8, true);

				return ParseFromReader(new JsonTextReader(reader), settings);
			}
		}

		/// <summary>Reads the content of a file, and returns the corresponding JSON value</summary>
		/// <param name="source">Instance from which to read the JSON document</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="ct">Token used to cancel the operation</param>
		/// <returns>Corresponding JSON value.</returns>
		/// <remarks>
		/// <para>The value may be mutable (for objects and arrays) and can be modified. If you require an immutable thread-safe value, please configure the <paramref name="settings"/> accordingly.</para>
		/// <para>If the result is always expected to be an Array or an Object, please call <see cref="JsonValueExtensions.AsArray"/> or <see cref="JsonValueExtensions.AsObject"/> on the result.</para>
		/// </remarks>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static Task<JsonValue> ParseFromAsync(Stream source, CrystalJsonSettings? settings, CancellationToken ct)
		{
			Contract.NotNull(source);
			if (ct.IsCancellationRequested) return Task.FromCanceled<JsonValue>(ct);

			if (!source.CanRead) throw new InvalidOperationException("Cannot read from source stream");

			if (ReferenceEquals(source, Stream.Null))
			{ // no content
				return Task.FromResult(JsonNull.Missing);
			}

			switch (source)
			{
				case MemoryStream ms:
				{ // read from memory (non-blocking)
					return ParseFromMemoryStream(ms, settings, ct);
				}
				default:
				{ // read from another thread (blocking)
					return ParseFromDeferred(source, settings, ct);
				}
			}


			static Task<JsonValue> ParseFromMemoryStream(MemoryStream ms, CrystalJsonSettings? settings, CancellationToken ct)
			{
				try
				{
					JsonValue value;
					if (ms.TryGetBuffer(out var buffer))
					{
						value = Parse(buffer.AsSlice(), settings);
					}
					else
					{
						using var reader = new StreamReader(ms, Encoding.UTF8, true);

						value = ParseFromReader(new JsonTextReader(reader), settings);
					}
					return Task.FromResult(value);
				}
				catch (Exception e)
				{
					return Task.FromException<JsonValue>(e);
				}
			}

			static Task<JsonValue> ParseFromDeferred(Stream source, CrystalJsonSettings? settings, CancellationToken ct)
			{
				//TODO: find a better solution!
				// => for now, we defer to the thread pool
				return Task.Run(() =>
				{
					ct.ThrowIfCancellationRequested();
					//TODO: use a stream wrapper that checks the cancellation token?
					using var reader = new StreamReader(source, Encoding.UTF8, true);
					return ParseFromReader(new JsonTextReader(reader), settings);
				}, ct);
			}
		}

		/// <summary>Checks if the buffer is likely to contain a JSON Object or Array</summary>
		/// <param name="jsonBytes">Buffer contains UTF-8 encoded text to inspect</param>
		/// <returns><see langword="true"/> if the buffer <b>MAY</b> contain either a JSON Object (<c>{...}</c>) or JSON Array (<c>[...]</c>)</returns>
		/// <remarks>Note: this uses a heuristic that does not perform a complete analysis, and may return false positives!</remarks>
		[Pure]
		public static bool MaybeJsonDocument([NotNullWhen(true)] byte[]? jsonBytes)
		{
			return jsonBytes != null && MaybeJsonDocument(jsonBytes.AsSlice());
		}
		/// <summary>Checks if the buffer is likely to contain a JSON Object or Array</summary>
		/// <param name="jsonBytes">Buffer contains UTF-8 encoded text to inspect</param>
		/// <returns><see langword="true"/> if the buffer <b>MAY</b> contain either a JSON Object (<c>{...}</c>) or JSON Array (<c>[...]</c>)</returns>
		/// <remarks>Note: this uses a heuristic that does not perform a complete analysis, and may return false positives!</remarks>
		[Pure]
		public static bool MaybeJsonDocument(Slice jsonBytes)
		{
			return jsonBytes.HasValue && MaybeJsonDocument(jsonBytes.Span);
		}
		
		/// <summary>Checks if the buffer is likely to contain a JSON Object or Array</summary>
		/// <param name="jsonBytes">Buffer contains UTF-8 encoded text to inspect</param>
		/// <returns><see langword="true"/> if the buffer <b>MAY</b> contain either a JSON Object (<c>{...}</c>) or JSON Array (<c>[...]</c>)</returns>
		/// <remarks>Note: this uses a heuristic that does not perform a complete analysis, and may return false positives!</remarks>
		[Pure]
		public static bool MaybeJsonDocument(ReadOnlySpan<byte> jsonBytes)
		{
			// trim any whitespaces
			jsonBytes = jsonBytes.Trim("\0\r\n\t "u8);
			
			// is it "null" ?
			if (jsonBytes.SequenceEqual("null"u8))
			{
				return true;
			}

			if (jsonBytes.Length < 2) return false;

			// does it start/end with {} or [] ?
			var first = jsonBytes[0];
			var last = jsonBytes[^1];
			
			return (first == '{' && last == '}') || (first == '[' && last == ']');
		}

		/// <summary>Creates a <see cref="StreamReader"/> that will load the content of a JSON file on disk</summary>
		/// <param name="path">Path to the file to load</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		[Pure]
		private static StreamReader OpenJsonStreamReader(string path, CrystalJsonSettings? settings)
		{
			var bufferSize = 0x1000; // x4 = 16 KiB
			if (settings is { OptimizeForLargeData: true })
			{
				bufferSize = 0x8000; // x4 = 128 KiB
			}

			FileStream? fileStream = null;
			try
			{
				fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize << 2, FileOptions.SequentialScan);

				return new StreamReader(fileStream, Encoding.UTF8, true, bufferSize);
			}
			catch (Exception)
			{
				fileStream?.Dispose();
				throw;
			}
		}

		[Pure]
		internal static JsonValue LoadAndParseInternal(string path, CrystalJsonSettings? settings, LoadOptions options = LoadOptions.None)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			var fi = new FileInfo(path);
			if (!fi.Exists)
			{ // The file does not exist

				if ((options & LoadOptions.ReturnNullIfMissing) == LoadOptions.ReturnNullIfMissing)
				{ // Treat a FileNotFound as if it was present with a "null" value
					return JsonNull.Missing;
				}
				// 404'ed !
				throw new FileNotFoundException("Specified JSON file could not be found", path);
			}

			//TODO: maybe buffer everything in memory if this is a small file?

			using var reader = OpenJsonStreamReader(path, settings);
			return ParseFromReader(new JsonTextReader(reader), settings);
		}

		[Pure]
		internal static async Task<JsonValue> LoadAndParseInternalAsync(string path, CrystalJsonSettings? settings, LoadOptions options, CancellationToken ct)
		{
			Contract.NotNullOrEmpty(path);
			path = Path.GetFullPath(path);

			var fi = new FileInfo(path);
			if (!fi.Exists)
			{ // The file does not exist

				if ((options & LoadOptions.ReturnNullIfMissing) == LoadOptions.ReturnNullIfMissing)
				{ // Treat a FileNotFound as if it was present with a "null" value
					return JsonNull.Missing;
				}
				// 404'ed !
				throw new FileNotFoundException("Specified JSON file could not be found", path);
			}

			//TODO: find a better solution than this!
			var value = await Task.Run(() =>
			{
				ct.ThrowIfCancellationRequested();
				using var reader = OpenJsonStreamReader(path, settings);
				//TODO: use a stream wrapper that checks the cancellation token?
				return ParseFromReader(new JsonTextReader(reader), settings);
			}, ct).ConfigureAwait(false);

			return value;
		}

		#endregion

		#region Deserialization...

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string jsonText
		) where TValue : notnull
		{
			return Parse(jsonText).Required<TValue>(resolver: null);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string jsonText,
			CrystalJsonSettings? settings,
			ICrystalJsonTypeResolver? resolver = null
		) where TValue : notnull
		{
			return Parse(jsonText, settings).Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="serializer"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string jsonText,
			IJsonDeserializer<TValue>? serializer,
			CrystalJsonSettings? settings = null,
			ICrystalJsonTypeResolver? resolver = null
		) where TValue : notnull
		{
			resolver ??= CrystalJson.DefaultResolver;
			var parsed = Parse(jsonText, settings);
			return serializer != null
				? serializer.Unpack(parsed, resolver)
				: parsed.Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="defaultValue"></param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string jsonText,
			TValue defaultValue)
		{
			return Parse(jsonText).As(defaultValue);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonText">JSON text document to parse</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.Json)]
#endif
			string jsonText,
			TValue defaultValue,
			CrystalJsonSettings? settings,
			ICrystalJsonTypeResolver? resolver = null
		)
		{
			return Parse(jsonText, settings).As(defaultValue, resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(byte[] jsonBytes) where TValue : notnull
		{
			return Parse(jsonBytes).Required<TValue>();
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(byte[] jsonBytes, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(byte[] jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(byte[] jsonBytes, TValue defaultValue, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static TValue Deserialize<TValue>(Slice jsonBytes) where TValue : notnull
		{
			return Parse(jsonBytes).Required<TValue>(resolver: null);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static TValue Deserialize<TValue>(Slice jsonBytes, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(Slice jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(Slice jsonBytes, TValue defaultValue, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		public static TValue Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlySpan<byte> jsonBytes, TValue defaultValue, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return Parse(jsonBytes, settings).Required<TValue>(resolver);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, TValue defaultValue)
		{
			return Parse(jsonBytes).As(defaultValue);
		}

		/// <summary>De-serializes a JSON text literal into a value of type <typeparamref name="TValue"/></summary>
		/// <param name="jsonBytes">UTF-8 encoded JSON document to parse</param>
		/// <param name="defaultValue"></param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		[Pure]
		[return: NotNullIfNotNull(nameof(defaultValue))]
		public static TValue? Deserialize<TValue>(ReadOnlyMemory<byte> jsonBytes, TValue defaultValue, CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver = null)
		{
			return Parse(jsonBytes, settings).As(defaultValue, resolver);
		}

		/// <summary>Deserializes the content of a source of JSON text into an instance of type <typeparamref name="TValue"/></summary>
		/// <param name="source">Source of text to parse</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(TextReader source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			return ParseFrom(source, settings).As<TValue?>(default, resolver);
		}

		/// <summary>Deserializes the content of a stream into an instance of type <typeparamref name="TValue"/></summary>
		/// <param name="source">File to read</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(Stream source, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null) where TValue : notnull
		{
			Contract.NotNull(source);

			using var sr = new StreamReader(source, Encoding.UTF8, true);

			return ParseFromReader(new JsonTextReader(sr), settings).As<TValue?>(default, resolver);
		}

		/// <summary>Deserializes the content of a file on disk into an instance of type <typeparamref name="TValue"/></summary>
		/// <param name="path">Path to the file to read</param>
		/// <param name="settings">Serialization settings (use default JSON settings if null)</param>
		/// <param name="resolver">Custom type resolver (use default behavior if null)</param>
		/// <param name="options">Read options</param>
		/// <returns>Deserialized instance</returns>
		/// <exception cref="JsonSyntaxException">If the JSON document is not syntactically correct.</exception>
		/// <exception cref="InvalidOperationException">If the JSON document is <c>"null"</c></exception>
		[Pure]
		public static TValue? LoadFrom<TValue>(string path, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, LoadOptions options = LoadOptions.None) where TValue : notnull
		{
			return LoadAndParseInternal(path, settings ?? CrystalJsonSettings.Json, options).As<TValue?>(default, resolver);
		}

		#endregion

		#region Helpers...

		internal const long Ticks1970Jan1 = 621355968000000000L; // = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;

		internal static long DateToJavaScriptTicks(DateTime date)
		{
			long ticks = ((date != DateTime.MinValue && date != DateTime.MaxValue && date.Kind != DateTimeKind.Utc) ? date.ToUniversalTime() : date).Ticks;
			return (ticks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;
		}

		internal static long DateToJavaScriptTicks(DateTimeOffset date)
			=> (date.UtcTicks - Ticks1970Jan1) / TimeSpan.TicksPerMillisecond;

		internal static DateTime JavaScriptTicksToDate(long ticks)
			=> new((ticks * TimeSpan.TicksPerMillisecond) + Ticks1970Jan1, DateTimeKind.Utc);

		/// <summary>Tests if the resolver is the default resolver (<see langword="true"/>) or a customized resolver (<see langword="false"/>)</summary>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static bool IsDefaultResolver([NotNullWhen(false)] ICrystalJsonTypeResolver? resolver)
			=> resolver == null || ReferenceEquals(resolver, DefaultResolver);

		#endregion

		#region Error Handling...

		[PublicAPI]
		public static class Errors
		{
			// note: this type is used by source-generated converters

			#region Serialization Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_FailTooDeep(int depth, object? current) => new($"Reached maximum depth of {depth} while serializing child object of type '{current?.GetType().GetFriendlyName() ?? "<null>"}'. Top object is too complex to be serialized this way!");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_ObjectRecursionIsNotAllowed(IEnumerable<object?> visited, object? value, int depth) => new($"Object of type '{value?.GetType().FullName}' at depth {depth} already serialized before! Recursive object graphs not supported. Visited path: {string.Join(" <- ", visited.Select(v => v?.GetType().FullName ?? "<null>"))}");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_InternalDepthInconsistent() => new("public depth is inconsistent.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_LeaveNotSameThanMark(int depth, object? current) => new($"De-synchronization of the visited object stack: Leave() was called with a different value of type '{current?.GetType().GetFriendlyName() ?? "<null>"}' than MarkVisited() at depth {depth}.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_DoesNotKnowHowToSerializeType(Type type) => new($"Doesn't know how to serialize values of type '{type.GetFriendlyName()}'.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_DoesNotKnowHowToSerializeNullableType(Type type) => new($"Doesn't know how to serialize Nullable type '{type.GetFriendlyName()}'.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_CouldNotResolveTypeDefinition(Type type) => new($"Could not get the members list for type '{type.GetFriendlyName()}'.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidSignature(Type type, MethodInfo method) => new($"Static serialization method '{type.GetFriendlyName()}.{method.Name}' must take two parameters.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidFirstParam(Type type, MethodInfo method, Type prmType)
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				return new JsonSerializationException($"First parameter of static method '{type.GetFriendlyName()}.{method.Name}' must be assignable to type '{type.GetFriendlyName()}' (it was '{prmType.GetFriendlyName()}').");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_StaticJsonSerializeMethodInvalidSecondParam(Type type, MethodInfo method, Type prmType)
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				return new JsonSerializationException($"Second parameter of static method '{type.GetFriendlyName()}.{method.Name}' must be a {nameof(CrystalJsonWriter)} object (it was '{prmType.GetFriendlyName()}').");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_InstanceJsonPackMethodInvalidSignature(Type type, MethodInfo method)
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				return new JsonSerializationException($"Static serialization method '{type.GetFriendlyName()}.{method.Name}' must take two parameters.");
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonSerializationException Serialization_CouldNotGetDefaultValueForMember(Type type, MemberInfo info, Exception? error)
			{
				var memberType = info is PropertyInfo pi ? pi.PropertyType : info is FieldInfo fi ? fi.FieldType : typeof(object);
#if NET6_0_OR_GREATER
				return memberType.IsByRefLike
					? new JsonSerializationException($"Cannot serialize {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}: ref-like types are NOT supported.", error)
					: new JsonSerializationException($"Cannot generate default value for {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}.", error);
#else
				// Type.IsByRefLike does not exist on netstandard2.0 (by-ref-like members cannot occur on this target)
				return new JsonSerializationException($"Cannot generate default value for {(info is PropertyInfo ? "property" : "field")} {type.GetFriendlyName()}.{info.Name} with type {memberType.GetFriendlyName()}.", error);
#endif
			}


			#endregion

			#region Parsing Errors...

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_ValueIsNullOrMissing() => new("Required JSON value was null or missing.");

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_FieldIsNullOrMissing(JsonValue? parent, string field, string? message) => new(message ?? $"Required JSON field '{field}' was null or missing.", JsonPath.Create(field), parent, null);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_FieldIsNullOrMissing(JsonValue? parent, ReadOnlyMemory<char> field, string? message) => new(message ?? $"Required JSON field '{field}' was null or missing.", JsonPath.Create(field), parent, null);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_ItemIsNullOrMissing(JsonValue? parent, Index index, string? message) => new(message ?? $"Required JSON item at index {index} was null or missing.", JsonPath.Create(index), parent, null);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_ChildIsNullOrMissing(JsonValue? parent, JsonPathSegment segment, string? message)
				=> segment.TryGetName(out var name) ? Parsing_FieldIsNullOrMissing(parent, name, message)
					: segment.TryGetIndex(out var index) ? Parsing_ItemIsNullOrMissing(parent, index, message)
					: Parsing_ValueIsNullOrMissing();

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_ChildIsNullOrMissing(IJsonProxyNode node, in JsonPathSegment child, string? message)
			{
				var path = node.GetPath(child);
				return child.IsName() ? new(message ?? $"Required JSON field '{path}' was null or missing.", path, null, null)
					: child.IsIndex() ? new(message ?? $"Required JSON item '{path}' was null or missing.", path, null, null)
					: path.IsEmpty() ? new(message ?? $"Required JSON top-level value was null or missing.", path, null, null)
					: new(message ?? $"Required JSON value '{path}' was null or missing.", path, null, null);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_DescendantIsNullOrMissing(IJsonProxyNode node, JsonPath path, string? message)
			{
				return path.TryGetLastKey(out _) ? new(message ?? $"Required JSON field '{path}' was null or missing.", path, null, null)
					: path.TryGetLastIndex(out _) ? new(message ?? $"Required JSON item '{path}' was null or missing.", path, null, null)
					: path.IsEmpty() ? new(message ?? "Required JSON top-level value was null or missing.", path, null, null)
					: new(message ?? $"Required JSON value '{path}' was null or missing.", path, null, null);
			}

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastToJsonObject(JsonValue? value) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as an Object.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastToJsonArray(JsonValue? value) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as an Array.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastToJsonNumber(JsonValue? value) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as a Number.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastToJsonString(JsonValue? value) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as a String.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastToJsonBoolean(JsonValue? value) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} as a Boolean.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastFieldToJsonObject(JsonValue? value, string fieldName) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} in field '{fieldName}' as an Object.", value);

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			public static JsonBindingException Parsing_CannotCastFieldToJsonArray(JsonValue? value, string fieldName) => new($"Cannot parse JSON {(value ?? JsonNull.Missing).Type} in field '{fieldName}' as an Array.", value);

			#endregion

		}

		#endregion

		#region Test Warmup...

		/// <summary>Runs a warmup sequence that will force the JIT visit the most commonly used types and methods at least once</summary>
		/// <remarks>You should only call this from a custom unit test or benchmark framework.</remarks>
		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static void Warmup()
		{
			try
			{
				var arr = JsonArray.Create(
				[
					JsonBoolean.True,
					JsonNumber.One,
					JsonString.Return("hello"),
					JsonString.Return(Guid.NewGuid()),
					JsonDateTime.Return(DateTimeOffset.Now),
					JsonArray.Create([ 1, 2, 3 ]),
					JsonObject.Create(("foo", "bar")),
				]);
				_ = arr[0].As<bool>();
				_ = arr[1].As<int>();
				_ = arr[2].As<string>();
				_ = arr[3].As<Guid>();
				_ = arr[4].As<DateTimeOffset>();
				_ = arr[5].As<int[]>();
				_ = arr[5].Get<string>("foo", "baz");
				_ = CrystalJson.Parse(CrystalJson.Serialize(arr), CrystalJsonSettings.JsonReadOnly);
				_ = arr.StrictEquals(arr);
			}
			// ReSharper disable once EmptyGeneralCatchClause
			catch { }
		}

		#endregion

	}

}
