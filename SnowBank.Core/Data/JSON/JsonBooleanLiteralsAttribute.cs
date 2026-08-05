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

		/// <summary>Specifies the wire literals for this member</summary>
		/// <param name="whenFalse">
		/// Wire form of <see langword="false"/>: a <see cref="string"/>, a <see cref="bool"/>, or a numeric value (ex: <c>"0"</c>, <c>0</c>).
		/// <para>Pass <see langword="null"/> to NOT EMIT the member at all when the value is <see langword="false"/>, for a consumer that expects either the member absent or the true literal.</para>
		/// </param>
		/// <param name="whenTrue">Wire form of <see langword="true"/>: a <see cref="string"/>, a <see cref="bool"/>, or a numeric value (ex: <c>"1"</c>, <c>1</c>). It may not be <see langword="null"/>: emitting nothing for <see langword="true"/> has no meaning.</param>
		/// <exception cref="ArgumentException">If either argument is of a type that has no JSON wire form.</exception>
		/// <remarks>The two arguments do not have to share a type: <c>[JsonBooleanLiterals("0", 1)]</c> is legal, because legacy wires are not always consistent.</remarks>
		public JsonBooleanLiteralsAttribute(object? whenFalse, object whenTrue)
		{
			this.FalseLiteral = ToLiteral(whenFalse, nameof(whenFalse));
			this.TrueLiteral = ToLiteral(whenTrue, nameof(whenTrue)) ?? throw new ArgumentNullException(nameof(whenTrue), "A true literal is required: emitting nothing for true has no meaning.");
		}

		/// <summary>Converts an attribute argument to its wire form, refusing types that have none</summary>
		internal static JsonValue? ToLiteral(object? value, string parameterName)
			=> value switch
			{
				null => null,
				string s => JsonString.Return(s),
				bool b => JsonBoolean.Return(b),
				sbyte or byte or short or ushort or int or uint or long or ulong or float or double => JsonValue.FromValue(value),
				_ => throw new ArgumentException(string.Format(System.Globalization.CultureInfo.InvariantCulture, CrystalJson.Errors.BooleanLiteralTypeNotSupported, parameterName, value.GetType().Name), parameterName),
			};

		/// <summary>Wire form of <see langword="false"/>, or <see langword="null"/> when the member is not emitted at all for <see langword="false"/></summary>
		public JsonValue? FalseLiteral { get; }

		/// <summary>Wire form of <see langword="true"/></summary>
		public JsonValue TrueLiteral { get; }

		/// <summary>When <see langword="true"/>, genuine JSON <see langword="true"/>/<see langword="false"/> are rejected on read instead of being accepted alongside the configured literals</summary>
		public bool StrictLiterals { get; set; }

	}

	/// <summary>Converter installed for members carrying <see cref="JsonBooleanLiteralsAttribute"/>; feeds the same per-member converter slot as <c>[JsonConverter(typeof(...))]</c></summary>
	/// <remarks>Public because generated converters instantiate it directly; application code normally uses the attribute instead.</remarks>
	[PublicAPI]
	public sealed class JsonBooleanLiteralsConverter : IJsonMemberConverter<bool>
	{

		private JsonValue? FalseLiteral { get; }

		private JsonValue TrueLiteral { get; }

		private bool StrictLiterals { get; }

		internal JsonBooleanLiteralsConverter(JsonBooleanLiteralsAttribute attribute)
		{
			this.FalseLiteral = attribute.FalseLiteral;
			this.TrueLiteral = attribute.TrueLiteral;
			this.StrictLiterals = attribute.StrictLiterals;
		}

		/// <inheritdoc cref="JsonBooleanLiteralsAttribute(object?,object)"/>
		public JsonBooleanLiteralsConverter(object? whenFalse, object whenTrue, bool strictLiterals = false)
		{
			this.FalseLiteral = JsonBooleanLiteralsAttribute.ToLiteral(whenFalse, nameof(whenFalse));
			this.TrueLiteral = JsonBooleanLiteralsAttribute.ToLiteral(whenTrue, nameof(whenTrue)) ?? throw new ArgumentNullException(nameof(whenTrue), "A true literal is required: emitting nothing for true has no meaning.");
			this.StrictLiterals = strictLiterals;
		}

		public JsonValue Pack(ref CrystalJsonPackContext context, bool instance)
		{
			// a null false-literal means "do not emit": the writers decide that from the member's flags before asking
			// this converter what to write, so reaching here with false means something forced the member to be emitted
			return instance ? this.TrueLiteral : (this.FalseLiteral ?? JsonNull.Null);
		}

		public bool Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			switch (value)
			{
				case JsonBoolean b:
				{
					// a configured literal that IS a genuine boolean matches on its own terms, before strictness is
					// considered: [JsonBooleanLiterals(null, true)] must not reject the very literal it declares
					if (this.TrueLiteral is JsonBoolean tb && tb.ToBoolean() == b.ToBoolean()) return true;
					if (this.FalseLiteral is JsonBoolean fb && fb.ToBoolean() == b.ToBoolean()) return false;

					if (this.FalseLiteral is null && !b.ToBoolean())
					{ // there is no false literal because ABSENCE means false; an explicit false is the same state
						// spelled out, so refusing it would reject a value the shape already considers legal
						return false;
					}

					if (this.StrictLiterals)
					{ // the member opted into catching a producer that silently switched to real booleans
						throw new JsonBindingException($"Cannot bind a JSON boolean: this member only accepts the configured literals {this.FalseLiteral?.ToString() ?? "(none: false is not emitted)"} and {this.TrueLiteral} (StrictLiterals)");
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
