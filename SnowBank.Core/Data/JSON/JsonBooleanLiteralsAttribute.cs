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

	/// <summary>Specifies custom wire literals for a <see cref="bool"/> (or <see cref="Nullable{T}">bool?</see>) member, for compatibility with producers that do not emit real JSON booleans</summary>
	/// <remarks>
	/// <para>Example: <c>[JsonBooleanLiterals("0", "1")]</c> serializes <see langword="false"/> as <c>"0"</c> and <see langword="true"/> as <c>"1"</c>;
	/// the <c>int</c> flavor <c>[JsonBooleanLiterals(0, 1)]</c> emits JSON numbers instead of strings.</para>
	/// <para>Reading is tolerant by default: the configured literals are accepted (case-insensitively for strings) <b>and</b> genuine
	/// <see langword="true"/>/<see langword="false"/> as well, so the day the producer is modernized to emit real booleans, no redeploy
	/// is needed. Set <see cref="StrictLiterals"/> to <see langword="true"/> to reject genuine booleans instead, when catching a
	/// silently-changed producer matters more than tolerance.</para>
	/// <para>There is no System.Text.Json equivalent: STJ's answer for this shape is a custom converter. This attribute exists so a
	/// migrating application does not have to write one per member.</para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	[PublicAPI]
	public sealed class JsonBooleanLiteralsAttribute : Attribute
	{

		/// <summary>Booleans are serialized as JSON strings (ex: <c>"0"</c> / <c>"1"</c>)</summary>
		public JsonBooleanLiteralsAttribute(string falseLiteral, string trueLiteral)
		{
			Contract.NotNullOrEmpty(falseLiteral);
			Contract.NotNullOrEmpty(trueLiteral);
			this.FalseLiteral = JsonString.Return(falseLiteral);
			this.TrueLiteral = JsonString.Return(trueLiteral);
		}

		/// <summary>Booleans are serialized as JSON numbers (ex: <c>0</c> / <c>1</c>)</summary>
		public JsonBooleanLiteralsAttribute(int falseLiteral, int trueLiteral)
		{
			this.FalseLiteral = JsonNumber.Create(falseLiteral);
			this.TrueLiteral = JsonNumber.Create(trueLiteral);
		}

		/// <summary>Wire form of <see langword="false"/></summary>
		public JsonValue FalseLiteral { get; }

		/// <summary>Wire form of <see langword="true"/></summary>
		public JsonValue TrueLiteral { get; }

		/// <summary>When <see langword="true"/>, genuine JSON <see langword="true"/>/<see langword="false"/> are rejected on read instead of being accepted alongside the configured literals</summary>
		public bool StrictLiterals { get; set; }

	}

	/// <summary>Converter installed for members carrying <see cref="JsonBooleanLiteralsAttribute"/>; feeds the same per-member converter slot as <c>[JsonConverter(typeof(...))]</c></summary>
	internal sealed class JsonBooleanLiteralsConverter : IJsonMemberConverter<bool>
	{

		private JsonValue FalseLiteral { get; }

		private JsonValue TrueLiteral { get; }

		private bool StrictLiterals { get; }

		public JsonBooleanLiteralsConverter(JsonBooleanLiteralsAttribute attribute)
		{
			this.FalseLiteral = attribute.FalseLiteral;
			this.TrueLiteral = attribute.TrueLiteral;
			this.StrictLiterals = attribute.StrictLiterals;
		}

		public JsonValue Pack(bool instance, CrystalJsonSettings? settings = null, ICrystalJsonTypeResolver? resolver = null)
		{
			return instance ? this.TrueLiteral : this.FalseLiteral;
		}

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			switch (value)
			{
				case JsonBoolean b:
				{
					if (this.StrictLiterals)
					{ // the member opted into catching a producer that silently switched to real booleans
						throw new JsonBindingException($"Cannot bind a JSON boolean: this member only accepts the configured literals {this.FalseLiteral} and {this.TrueLiteral} (StrictLiterals)");
					}
					return b.ToBoolean();
				}
				case JsonString s:
				{
					if (this.TrueLiteral is JsonString ts && string.Equals(s.Value, ts.Value, StringComparison.OrdinalIgnoreCase)) return true;
					if (this.FalseLiteral is JsonString fs && string.Equals(s.Value, fs.Value, StringComparison.OrdinalIgnoreCase)) return false;
					break;
				}
				case JsonNumber n:
				{
					if (this.TrueLiteral is JsonNumber tn && n.Equals(tn)) return true;
					if (this.FalseLiteral is JsonNumber fn && n.Equals(fn)) return false;
					break;
				}
			}
			throw new JsonBindingException($"Cannot bind JSON {value.Type} '{value}' into a boolean: expected {this.FalseLiteral} or {this.TrueLiteral}");
		}

	}

}
