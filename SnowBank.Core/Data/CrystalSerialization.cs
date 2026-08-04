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

namespace SnowBank.Data
{

	/// <summary>Limits shared by every serialization wire in this library (JSON and XML, reflection-driven and source-generated alike)</summary>
	/// <remarks>This type is deliberately wire-neutral: it lives above <c>SnowBank.Data.Json</c> and <c>SnowBank.Data.Xml</c> so that
	/// neither has to reference the other to agree on a limit that only makes sense if all of them agree on it.</remarks>
	[PublicAPI]
	public static class CrystalSerialization
	{

		/// <summary>Maximum nesting depth any serialization wire may reach before it refuses to go deeper</summary>
		/// <remarks>
		/// <para>An object graph containing a reference cycle has no representation on any of these wires, so a serializer that
		/// walks it must stop instead of recursing forever. Every wire counts the levels it has entered and raises a typed,
		/// catchable exception once this cap is reached, rather than a <see cref="StackOverflowException"/>, which .NET cannot
		/// catch and which takes the whole process down.</para>
		/// <para>The guard cannot distinguish a genuine reference cycle from an acyclic graph that is simply nested deeper than
		/// this cap: either shape hits the same counter and raises the same exception. A caller that knows its data has no
		/// cycles but legitimately needs more than 256 levels should flatten the graph instead.</para>
		/// <para><b>Not every recursion is counted.</b> On the source-generated JSON <c>Pack</c> path, the counter resets to
		/// zero whenever a member is packed through the collection/dictionary helper seam (<c>PackObject</c>, <c>PackArray</c>,
		/// <c>PackList</c>, <c>PackEnumerable</c> in <c>JsonSerializerExtensions</c>): each helper holds only the three-argument
		/// <c>IJsonPacker&lt;T&gt;.Pack</c> member, with no way to thread the caller's depth through it. A reference cycle that
		/// runs through a <c>List&lt;T&gt;</c> or <c>Dictionary&lt;TKey,TValue&gt;</c> member is therefore not covered by this
		/// guard on that path and still overflows the native stack. This is the JSON analogue of the XML wire's own uncovered
		/// seam, documented on <see cref="SnowBank.Data.Xml.CrystalXml.MaxDepth"/>: a call into a custom member converter or a
		/// self-writing type also resets the XML counter to zero on the other side of the call.</para>
		/// <para><b>One value for every wire.</b> The exception TYPE differs per wire (<c>JsonSerializationException</c> on the
		/// JSON wires, <c>CrystalXmlCycleException</c> on the XML ones) but the boundary does not: a document that serializes on
		/// one wire must not be refused by another purely because they disagreed on where "too deep" starts.</para>
		/// <para><b>Why 256.</b> Measured overflow points of the recursions this cap protects, on a DEBUG build (nothing inlines)
		/// running on an ordinary 1 MB worker-thread stack: the source-generated <c>Pack</c> path dies past ~8000 levels (one
		/// frame per level), the source-generated <c>Serialize</c> path past ~6000 (two frames per level), the reflection-driven
		/// text path past ~2000, and the reflection-driven DOM path (<c>CrystalJsonDomWriter</c>, three frames per level) past
		/// ~1050, which is the worst of the four. 256 therefore leaves a four-fold margin on the worst wire and far more on the
		/// others, while sitting well past any document shape a real application produces. The XML wires pin this boundary
		/// exactly (a graph of exactly 256 levels serializes, 257 throws), so the value cannot be lowered without breaking
		/// them.</para>
		/// </remarks>
		public const int MaxDepth = 256;

	}

}
