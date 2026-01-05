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
	using System.IO;

	/// <summary>Writes JSON document fragments into a destination stream</summary>
	[PublicAPI]
	[DebuggerNonUserCode]
	public sealed class CrystalJsonStreamWriter : IDisposable, IAsyncDisposable
	{
		// We use a regular CrystalJsonWriter to write into a TextWriter that then outputs into a MemoryStream that acts as an in-memory buffer.
		// The TextWriter has its own small buffer, and must be periodically flushed into the MemoryStream. All of this is non-async since the target is in memory.
		// Periodically, or when the buffer is large enough, we can flush into the destination stream asynchronously.
		//
		//     CrystalJsonWriter => TextWriter(Buffer=[]) => MemoryStream(Buffer=[]) => Stream
		//
		// The destination stream is expected to be either a FileStream, or a NetworkStream.

		/// <summary>Size after which the buffer will automatically be flushed to the output stream</summary>
		private const int AUTO_FLUSH_THRESHOLD = 256 * 1024;
		private const int SCRATCH_INITIAL_SIZE = 64 * 1024;

		/// <summary>Destination stream</summary>
		private readonly Stream m_stream;
		/// <summary>Memory buffer</summary>
		private readonly MemoryStream m_scratch;
		/// <summary>JSON writer that writes to the buffer</summary>
		private readonly CrystalJsonWriter m_writer;
		/// <summary>If <c>true</c>, the destination stream will be disposed when this instance is disposed</summary>
		private readonly bool m_ownStream;
		/// <summary>If <c>true</c>, this instance has already been disposed</summary>
		private bool m_disposed;

		/// <summary>Last visited type</summary>
		private Type? m_lastType;
		/// <summary>Cached visitor for the last visited type</summary>
		private CrystalJsonTypeVisitor? m_visitor;

		private string m_newLine = "\r\n";

		public CrystalJsonStreamWriter(Stream output, CrystalJsonSettings? settings, CrystalJsonTypeResolver? resolver = null, bool ownStream = false)
		{
			Contract.NotNull(output);

			m_stream = output;
			m_ownStream = ownStream;
			settings ??= CrystalJsonSettings.JsonCompact;
			resolver ??= CrystalJson.DefaultResolver;

			m_scratch = new(SCRATCH_INITIAL_SIZE);
			m_writer = new(m_scratch, 0, settings, resolver);
		}

		public CrystalJsonWriter.NodeType CurrentNode => m_writer.CurrentState.Node;

		public string NewLine => m_newLine;

		/// <summary>Returns the estimated position in the source stream</summary>
		/// <remarks>CAUTION: this value is only accurate right after a Flush!</remarks>
		public long? PositionHint
		{
			get
			{
				if (m_disposed) ThrowDisposed();
				//note: if there has not been any recent flush, there can still be some characters inside the writer (but not yet flushed in the scratch buffer)
				return m_stream.Position + m_scratch.Length;
			}
		}

		/// <summary>Writes a document fragment, and flushes the stream</summary>
		public void WriteFragment<T>(T item, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var visitor = GetVisitor(typeof(T));
			visitor(item, typeof(T), item is not null ? item.GetType() : null, m_writer);
			m_writer.Buffer.Write(m_newLine);

			FlushInternal(true);
		}

		/// <summary>Writes a document fragment, and flushes the stream</summary>
		public Task WriteFragmentAsync<T>(T item, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var visitor = GetVisitor(typeof(T));
			visitor(item, typeof(T), item is not null ? item.GetType() : null, m_writer);
			m_writer.Buffer.Write(m_newLine);

			return FlushInternalAsync(true, cancellationToken);
		}

		#region Objects...

		/// <summary>Writes a top-level object, and flushes the stream</summary>
		/// <param name="handler">Handler that is responsible for writing the object content into the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task that completes once the object has been completely written, and the buffer has been flushed into the stream</returns>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginObjectFragment"/>.</para>
		/// </remarks>
		public void WriteObjectFragment([InstantHandle] Action<ObjectStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var obj = BeginObjectFragment())
			{
				handler(obj);
			}
			FlushInternal(true);
		}

		/// <summary>Writes a top-level object, and flushes the stream</summary>
		/// <param name="handler">Handler that is responsible for writing the object content into the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task that completes once the object has been completely written, and the buffer has been flushed into the stream</returns>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginObjectFragment"/>.</para>
		/// </remarks>
		public async Task WriteObjectFragmentAsync([InstantHandle] Func<ObjectStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			using (var obj = BeginObjectFragment())
			{
				await handler(obj).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		[PublicAPI]
		public sealed class ObjectStream : IDisposable
		{
			private readonly CrystalJsonStreamWriter m_parent;
			private readonly CancellationToken m_ct;
			private readonly CrystalJsonWriter.State m_state;

			internal ObjectStream(CrystalJsonStreamWriter parent, CrystalJsonWriter.State state, CancellationToken cancellationToken)
			{
				m_parent = parent;
				m_ct = cancellationToken;
				m_state = state;
			}

			public CrystalJsonWriter Writer => m_parent.m_writer;

			public CancellationToken Cancellation => m_ct;

			private void VisitProperty(string name, CrystalJsonTypeVisitor? visitor, Type type, object? item)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name, knownSafe: true);

				if (item is null || visitor is null)
				{
					writer.WriteNull();
				}
				else
				{
					visitor(item, type, item.GetType(), writer);
				}
			}

			public void WriteFieldNull(string name)
			{
				m_parent.EnsureInObjectMode();
				VisitProperty(name, null, typeof(object), null);
			}

			public void WriteField<T>(string name, T value)
			{
				m_parent.EnsureInObjectMode();
				VisitProperty(name, CrystalJsonVisitor.GetVisitorForType(typeof(T), atRuntime: false), typeof(T), value);
			}

			public void WriteField<T>(string name, T? value)
				where T : struct
			{
				m_parent.EnsureInObjectMode();
				if (value.HasValue)
				{
					VisitProperty(name, CrystalJsonVisitor.GetVisitorForType(typeof(T), atRuntime: false), typeof(T), value.Value);
				}
				else
				{
					VisitProperty(name, null, typeof(object), null);
				}
			}

			[Pure]
			public ArrayStream BeginArrayStream(string name)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInObjectMode();
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name, knownSafe: false);

				return m_parent.BeginArrayFragment(m_ct);
			}

			[Pure]
			public ObjectStream BeginObjectStream(string name)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInObjectMode();
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				writer.WritePropertyName(name, knownSafe: false);

				return m_parent.BeginObjectFragment(m_ct);
			}

			public void Dispose()
			{
				var writer = m_parent.m_writer;
				var state = writer.CurrentState;
				if (state.Node != CrystalJsonWriter.NodeType.Object) throw new InvalidOperationException("Invalid writer state: object mode expected");

				if (state.Tail)
				{
					writer.Buffer.Write(m_parent.NewLine);
				}
				writer.Buffer.Write('}', m_parent.NewLine);
				writer.PopState(m_state);
			}

		}

		/// <summary>Starts manual serialization of an object</summary>
		/// <returns>Sub-stream that can be used to write the object fragment, and which should Disposed once the object is complete.</returns>
		[Pure]
		public ObjectStream BeginObjectFragment(CancellationToken cancellationToken = default)
		{
			var state = m_writer.PushState(CrystalJsonWriter.NodeType.Object);
			m_writer.Buffer.Write('{', m_newLine);
			return new ObjectStream(this, state, cancellationToken);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void EnsureInObjectMode()
		{
			if (m_disposed)
			{
				ThrowDisposed();
			}
			if (m_writer.CurrentState.Node != CrystalJsonWriter.NodeType.Object)
			{
				ThrowNotInObjectMode();
			}

			[DoesNotReturn, StackTraceHidden]
			static void ThrowNotInObjectMode()
			{
				throw new InvalidOperationException("Invalid writer state: can only write fields in object mode. Did you forget to call BeginObject() first?");
			}
		}

		#endregion

		#region Arrays...

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="handler">Handler that is responsible for writing the array content into the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public void WriteArrayFragment([InstantHandle] Action<ArrayStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				handler(array);
			}
			FlushInternal(true);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <typeparam name="TState">Type of the state</typeparam>
		/// <param name="state">Value that will be passed as the first parameter to <paramref name="handler"/></param>
		/// <param name="handler">Handler that is responsible for writing the array content into the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public void WriteArrayFragment<TState>(TState state, [InstantHandle] Action<TState, ArrayStream> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				handler(state, array);
			}
			FlushInternal(true);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="handler">Handler that is responsible for writing the array content into the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task that completes once the array has been completely written, and the buffer has been flushed into the stream</returns>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public async Task WriteArrayFragmentAsync([InstantHandle] Func<ArrayStream, Task> handler, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await handler(array).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="items">Sequence that contains the items that will be written to the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public void WriteArrayFragment<T>(IEnumerable<T> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				array.WriteBatch(items);
			}
			FlushInternal(true);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="items">Sequence that contains the items that will be written to the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task that completes once the array has been completely written, and the buffer has been flushed into the stream</returns>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public async Task WriteArrayFragmentAsync<T>(IEnumerable<T> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await array.WriteBatchAsync(items).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="items">Sequence that contains the items that will be written to the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public void WriteArrayFragment(IEnumerable<JsonValue> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				array.WriteBatch(items);
			}
			FlushInternal(true);
		}

		/// <summary>Writes a top-level array, and flushes the stream</summary>
		/// <param name="items">Sequence that contains the items that will be written to the stream</param>
		/// <param name="cancellationToken">Token used to cancel the operation</param>
		/// <returns>Task that completes once the array has been completely written, and the buffer has been flushed into the stream</returns>
		/// <remarks>
		/// <para>The stream will be flushed after this call</para>
		/// <para>This method should only be called for top-level object. For sub-objects, please use <see cref="BeginArrayFragment"/>.</para>
		/// </remarks>
		public async Task WriteArrayFragmentAsync(IEnumerable<JsonValue> items, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			using (var array = BeginArrayFragment(cancellationToken))
			{
				await array.WriteBatchAsync(items).ConfigureAwait(false);
			}
			await FlushInternalAsync(true, cancellationToken).ConfigureAwait(false);
		}

		[PublicAPI]
		public sealed class ArrayStream : IDisposable
		{
			private readonly CrystalJsonStreamWriter m_parent;
			private readonly CancellationToken m_ct;
			private readonly CrystalJsonWriter.State m_state;

			internal ArrayStream(CrystalJsonStreamWriter parent, CrystalJsonWriter.State state, CancellationToken cancellationToken)
			{
				m_parent = parent;
				m_ct = cancellationToken;
				m_state = state;
			}

			public CrystalJsonWriter Writer => m_parent.m_writer;

			public CancellationToken Cancellation => m_ct;

			private void VisitValue(JsonValue value)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				value.JsonSerialize(writer);
			}

			private void VisitArrayItem(CrystalJsonTypeVisitor visitor, Type type, object? item)
			{
				var writer = m_parent.m_writer;
				if (writer.MarkNext())
				{
					writer.Buffer.Write(",\r\n ");
				}
				else
				{
					writer.Buffer.Write(' ');
				}
				visitor(item, type, item is not null ? item.GetType() : typeof(object), writer);
			}

			/// <summary>Ajoute un JsonValue dans un tableau ouvert</summary>
			public void WriteItem(JsonValue item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitValue(item);

				if (m_parent.ShouldFlush)
				{
					m_parent.FlushInternal(false);
				}
			}

			/// <summary>Ajoute un JsonValue dans un tableau ouvert</summary>
			public async Task WriteItemAsync(JsonValue item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitValue(item);

				if (m_parent.ShouldFlush)
				{
					await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
				}
			}

			/// <summary>Ajoute un élément dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'élément</typeparam>
			/// <param name="item">Element ajouté</param>
			/// <returns></returns>
			public void WriteItem<T>(T item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitArrayItem(m_parent.GetVisitor(typeof(T)), typeof(T), item);

				if (m_parent.ShouldFlush)
				{
					m_parent.FlushInternal(false);
				}
			}

			/// <summary>Ajoute un élément dans un tableau ouvert</summary>
			/// <typeparam name="T">Type de l'élément</typeparam>
			/// <param name="item">Element ajouté</param>
			/// <returns></returns>
			public async Task WriteItemAsync<T>(T item)
			{
				m_ct.ThrowIfCancellationRequested();
				m_parent.EnsureInArrayMode();

				VisitArrayItem(m_parent.GetVisitor(typeof(T)), typeof(T), item);

				if (m_parent.ShouldFlush)
				{
					await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
				}
			}

			/// <summary>Ajoute une liste de JsonValue dans un tableau ouvert</summary>
			public void WriteBatch(IEnumerable<JsonValue> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				foreach (var item in items)
				{
					VisitValue(item);

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute une liste de JsonValue dans un tableau ouvert</summary>
			public async Task WriteBatchAsync(IEnumerable<JsonValue> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				foreach (var item in items)
				{
					VisitValue(item);

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert</summary>
			public void WriteBatch<T>(IEnumerable<T> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(T);
				var visitor = m_parent.GetVisitor(type);
				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, item);

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert</summary>
			public async Task WriteBatchAsync<T>(IEnumerable<T> items)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(T);
				var visitor = m_parent.GetVisitor(type);
				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, item);

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert, en appliquant une transformation sur chaque élément</summary>
			/// <typeparam name="TInput">Type des éléments de la séquence source</typeparam>
			/// <typeparam name="TOutput">Type des éléments transformés</typeparam>
			/// <param name="items">Séquence des éléments constituants le batch</param>
			/// <param name="selector">Transformation appliquée à chaque éléments de <paramref name="items"/></param>
			/// <returns></returns>
			public void WriteBatch<TInput, TOutput>(IEnumerable<TInput> items, [InstantHandle] Func<TInput, TOutput> selector)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(TOutput);
				var visitor = m_parent.GetVisitor(type);

				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, selector(item));

					if (m_parent.ShouldFlush)
					{
						m_parent.FlushInternal(false);
					}
				}
			}

			/// <summary>Ajoute plusieurs éléments dans un tableau ouvert, en appliquant une transformation sur chaque élément</summary>
			/// <typeparam name="TInput">Type des éléments de la séquence source</typeparam>
			/// <typeparam name="TOutput">Type des éléments transformés</typeparam>
			/// <param name="items">Séquence des éléments constituants le batch</param>
			/// <param name="selector">Transformation appliquée à chaque éléments de <paramref name="items"/></param>
			/// <returns></returns>
			public async Task WriteBatchAsync<TInput, TOutput>(IEnumerable<TInput> items, [InstantHandle] Func<TInput, TOutput> selector)
			{
				Contract.NotNull(items);
				m_parent.EnsureInArrayMode();

				var type = typeof(TOutput);
				var visitor = m_parent.GetVisitor(type);

				foreach (var item in items)
				{
					m_ct.ThrowIfCancellationRequested();
					VisitArrayItem(visitor, type, selector(item));

					if (m_parent.ShouldFlush)
					{
						await m_parent.FlushInternalAsync(false, m_ct).ConfigureAwait(false);
					}
				}
			}

			public void Dispose()
			{
				var writer = m_parent.m_writer;
				var state = writer.CurrentState;
				if (state.Node != CrystalJsonWriter.NodeType.Array) throw new InvalidOperationException("Invalid writer state: array mode expected");

				if (state.Tail)
				{
					writer.Buffer.Write(m_parent.NewLine);
				}
				writer.Buffer.Write(']', m_parent.NewLine);
				writer.PopState(m_state);
			}

		}

		/// <summary>Starts manual serialization of an array</summary>
		/// <returns>Sub-stream that can be used to write the array fragment, and which should Disposed once the array is complete.</returns>
		[Pure]
		public ArrayStream BeginArrayFragment(CancellationToken cancellationToken = default)
		{
			var state = m_writer.PushState(CrystalJsonWriter.NodeType.Array);
			m_writer.Buffer.Write('[', m_newLine);
			return new ArrayStream(this, state, cancellationToken);
		}

		private void EnsureInArrayMode()
		{
			if (m_disposed)
			{
				ThrowDisposed();
			}

			if (m_writer.CurrentState.Node != CrystalJsonWriter.NodeType.Array)
			{
				ThrowNotInArrayMode();
			}

			[DoesNotReturn, StackTraceHidden]
			static void ThrowNotInArrayMode()
			{
				throw new InvalidOperationException("Invalid writer state: can only write batched entries in Array mode. Did you forget to call BeginArray() first?");
			}
		}

		#endregion

		#region Buffer management...

		public Task FlushAsync(CancellationToken ct)
		{
			if (m_disposed) ThrowDisposed();
			return FlushInternalAsync(true, ct);
		}

		public void Flush()
		{
			if (m_disposed) ThrowDisposed();
			FlushInternal(true);
		}

		private CrystalJsonTypeVisitor GetVisitor(Type declaredType)
		{
			Contract.Debug.Requires(declaredType is not null);

			if (declaredType != m_lastType)
			{
				m_lastType = declaredType;
				m_visitor = CrystalJsonVisitor.GetVisitorForType(declaredType);
			}
			Contract.Debug.Ensures(m_visitor is not null && m_lastType is not null);
			return m_visitor;
		}

		/// <summary>Tests if the internal buffer is large enough to be flushed</summary>
		private bool ShouldFlush => !m_disposed && m_scratch.Length >= AUTO_FLUSH_THRESHOLD;

		/// <summary>Ensures that all data written so far is flushed into the destination stream</summary>
		private async Task FlushInternalAsync(bool flushStream, CancellationToken cancellationToken)
		{
			// Flush the TextWriter so that we are certain all characters written so far are in the scratch buffer!
			await m_writer.FlushAsync(cancellationToken).ConfigureAwait(false);

			// Flush the scratch buffer into the destination stream
			if (m_scratch.Length > 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyToAsync() ignores the value of bufferSize
					await m_scratch.CopyToAsync(m_stream, 4096, cancellationToken).ConfigureAwait(false);
				}
				finally
				{
					m_scratch.SetLength(0);
				}
			}

			if (flushStream)
			{
				await m_stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
		}

		/// <summary>Ensures that all data written so far is flushed into the destination stream</summary>
		private void FlushInternal(bool flushStream)
		{
			// Flush the TextWriter so that we are certain all characters written so far are in the scratch buffer!
			m_writer.Flush();

			// Flush the scratch buffer into the destination stream
			if (m_scratch.Length != 0)
			{
				try
				{
					m_scratch.Seek(0, SeekOrigin.Begin);
					//note: MemoryStream.CopyTo() ignores the value of bufferSize
					m_scratch.CopyTo(m_stream, 4096);
				}
				finally
				{
					// if the copy fails, Dispose() will be called immediately after, and we should not be retried a second time!
					// => we force the buffer size to 0, so that Dispose() does not attempt to flush
					m_scratch.SetLength(0);
				}
			}

			if (flushStream)
			{
				m_stream.Flush();
			}
		}

		#endregion

		#region IDisposable...

		public void Dispose()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//note: Dispose() can also be called again after an I/O error on the stream itself
				try
				{
					FlushInternal(true);
				}
				finally
				{
					if (m_ownStream)
					{
						m_stream.Dispose();
					}
				}
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (!m_disposed)
			{
				m_disposed = true;
				//note: Dispose() can also be called again after an I/O error on the stream itself
				try
				{
					await FlushInternalAsync(true, CancellationToken.None).ConfigureAwait(true);
				}
				finally
				{
					if (m_ownStream)
					{
						await m_stream.DisposeAsync().ConfigureAwait(false);
					}
				}
			}
		}

		[DoesNotReturn, StackTraceHidden]
		private void ThrowDisposed()
		{
			throw new ObjectDisposedException(this.GetType().Name);
		}

		#endregion

		#region Static Helpers...

		[Pure]
		public static CrystalJsonStreamWriter Create(string path, CrystalJsonSettings? settings = null, CrystalJsonTypeResolver? resolver = null)
		{
			Contract.NotNullOrEmpty(path);

			Stream? stream = null;
			CrystalJsonStreamWriter? sw = null;
			bool failed = true;
			try
			{
				stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 0x400, FileOptions.SequentialScan);
				sw = new CrystalJsonStreamWriter(stream, settings, resolver, true);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
					stream?.Dispose();
				}
			}
		}

		[Pure]
		public static CrystalJsonStreamWriter Create(Stream stream, CrystalJsonSettings? settings = null, CrystalJsonTypeResolver? resolver = null, bool ownStream = false)
		{
			Contract.NotNull(stream);

			CrystalJsonStreamWriter? sw = null;
			bool failed = true;
			try
			{
				sw = new(stream, settings, resolver, ownStream);
				failed = false;
				return sw;
			}
			finally
			{
				if (failed)
				{
					sw?.Dispose();
				}
			}
		}

		#endregion

	}

}
