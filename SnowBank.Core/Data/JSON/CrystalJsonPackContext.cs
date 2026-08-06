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

	/// <summary>State of one packing walk: the settings and resolver in effect, and the recursion guards</summary>
	/// <remarks>
	/// <para>A context is opened once per root value (see the <c>Pack</c> extension in <see cref="JsonSerializerExtensions"/>)
	/// and passed <b>by ref</b> through every nested <see cref="IJsonPacker{T}.Pack"/> call, including the collection and
	/// dictionary helpers, so the depth and visited-object guards survive every seam of the walk.</para>
	/// <para>A packer's implementation brackets its body with <see cref="Enter(object?)"/> and <see cref="Leave(object?)"/>, same contract as
	/// <see cref="CrystalJsonWriter.MarkVisited"/> / <see cref="CrystalJsonWriter.Leave"/>: the pairing is not
	/// exception-safe by design, a walk that threw is not resumable.</para>
	/// </remarks>
	[PublicAPI]
	public struct CrystalJsonPackContext
	{

		/// <summary>Serialization settings in effect for the whole walk</summary>
		public readonly CrystalJsonSettings? Settings;

		/// <summary>Custom resolver in effect for the whole walk</summary>
		public readonly ICrystalJsonTypeResolver? Resolver;

		private int m_depth;

		private object[]? m_visitedObjects;

		private int m_visitedCursor;

		private readonly bool m_markVisited;

		private CrystalJsonPackContext(CrystalJsonSettings? settings, ICrystalJsonTypeResolver? resolver, int depth)
		{
			this.Settings = settings;
			this.Resolver = resolver;
			m_depth = depth;
			m_visitedObjects = null;
			m_visitedCursor = 0;
			m_markVisited = !(settings?.DoNotTrackVisitedObjects ?? false);
		}

		/// <summary>Opens a fresh packing walk</summary>
		/// <param name="settings">Serialization settings for the whole walk</param>
		/// <param name="resolver">Custom resolver for the whole walk</param>
		/// <param name="depth">Number of levels to consider already open, for a walk embedded under an outer structure; the visited stack starts empty either way</param>
		public static CrystalJsonPackContext Create(CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null, int depth = 0)
		{
			Contract.Positive(depth);
			return new(settings, resolver, depth);
		}

		/// <summary>Number of values already open above the one being packed</summary>
		public readonly int Depth => m_depth;

		/// <summary>Enters one value of the object graph, counting depth only</summary>
		/// <remarks>For value types, which cannot take part in a reference cycle: skips the visited stack, and avoids boxing the instance</remarks>
		/// <exception cref="JsonSerializationException">If the graph is nested deeper than <see cref="CrystalJsonWriter.MaxDepth"/></exception>
		public void Enter()
		{
			if (m_depth >= CrystalJsonWriter.MaxDepth)
			{
				throw CrystalJson.Errors.Serialization_FailTooDeep(m_depth, null);
			}
			++m_depth;
		}

		/// <summary>Leaves the value entered by the matching parameterless <see cref="Enter()"/></summary>
		public void Leave()
		{
			if (m_depth == 0) throw CrystalJson.Errors.Serialization_InternalDepthInconsistent();
			--m_depth;
		}

		/// <summary>Enters one value of the object graph: refuses past <see cref="CrystalJsonWriter.MaxDepth"/>, and refuses a reference cycle</summary>
		/// <param name="instance">Value about to be packed; only reference types join the visited stack</param>
		/// <exception cref="JsonSerializationException">If the graph is nested deeper than the cap, or <paramref name="instance"/> is one of its own ancestors</exception>
		public void Enter(object? instance)
		{
			if (m_depth >= CrystalJsonWriter.MaxDepth)
			{
				throw CrystalJson.Errors.Serialization_FailTooDeep(m_depth, instance);
			}
			if (instance is not null && m_markVisited && !instance.GetType().IsValueType)
			{
				if (m_visitedCursor > 0 && AlreadyVisited(m_visitedObjects.AsSpan(0, m_visitedCursor), instance))
				{
					if (!CrystalJsonWriter.TypeSafeForRecursion(instance.GetType()))
					{
						throw CrystalJson.Errors.Serialization_ObjectRecursionIsNotAllowed(m_visitedObjects!, instance, m_depth);
					}
				}
				var buffer = m_visitedObjects;
				if (buffer is null || m_visitedCursor >= buffer.Length)
				{
					Array.Resize(ref buffer, Math.Max((buffer?.Length ?? 0) * 2, 4));
					m_visitedObjects = buffer;
				}
				buffer[m_visitedCursor++] = instance;
			}
			++m_depth;

			static bool AlreadyVisited(ReadOnlySpan<object> stack, object value)
			{
				foreach (var item in stack)
				{
					if (ReferenceEquals(item, value))
					{
						return true;
					}
				}
				return false;
			}
		}

		/// <summary>Leaves the value entered by the matching <see cref="Enter(object?)"/></summary>
		/// <param name="instance">Same value that was passed to <see cref="Enter(object?)"/></param>
		public void Leave(object? instance)
		{
			if (m_depth == 0) throw CrystalJson.Errors.Serialization_InternalDepthInconsistent();
			if (instance is not null && m_markVisited && m_visitedCursor > 0 && !instance.GetType().IsValueType)
			{
				var previous = m_visitedObjects![--m_visitedCursor];
				m_visitedObjects[m_visitedCursor] = null!;
				if (!ReferenceEquals(previous, instance))
				{
					throw CrystalJson.Errors.Serialization_LeaveNotSameThanMark(m_depth, instance);
				}
			}
			--m_depth;
		}

	}

}
