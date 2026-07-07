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

namespace SnowBank.Networking.PacketCapture
{
	using System.Globalization;
	using System.Runtime.CompilerServices;

	/// <summary>Unique Identifier for a <see cref="CapturedPacket"/></summary>
	[DebuggerDisplay("{ToString(),nq}")]
	public readonly struct CapturedPacketId : IEquatable<CapturedPacketId>, IJsonSerializable, IJsonPackable, IJsonDeserializable<CapturedPacketId>, IParsable<CapturedPacketId>
	{

		private const char SEPARATOR = ':';

		public static readonly CapturedPacketId Zero = new CapturedPacketId();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public CapturedPacketId(string? generation, ulong value)
		{
			this.Generation = generation;
			this.Value = value;
		}

		/// <summary>Generation identifier</summary>
		/// <remarks>This ID is generated randomly at the start of a new capture run (when the packet counter is initialized at 0)</remarks>
		public readonly string? Generation;

		/// <summary>Packet counter in this generation</summary>
		/// <remarks>This is a strictly monotonic counter that is used to stream packet logs</remarks>
		public readonly ulong Value;

		public bool IsEmpty
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => this.Value == 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(CapturedPacketId other) => this.Value == other.Value && this.Generation == other.Generation;

		public override bool Equals(object? obj) => obj is CapturedPacketId other && Equals(other);

		public override int GetHashCode() => HashCode.Combine(this.Value, this.Generation);

		public override string ToString()
		{
			Span<char> buffer = stackalloc char[(this.Generation?.Length ?? 0) + 1 + 20];
			if (!TryFormat(this.Generation, this.Value, buffer, out int written))
			{
#if DEBUG
				if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
				throw new InvalidOperationException("Buffer is too small!");
			}
			return written == 0 ? string.Empty : buffer[..written].ToString();
		}

		private static bool TryFormat(string? generation, ulong value, Span<char> buffer, out int written)
		{
			written = 0;
			if (generation == null)
			{ // empty buffer!
				return true;
			}

			int n = generation.Length;
			if (n + 2 > buffer.Length)
			{ // not enough for GEN + ':' + at least one digit!
				return false;
			}

			generation.AsSpan().CopyTo(buffer);
			buffer[n] = SEPARATOR;
			if (!value.TryFormat(buffer[(n + 1)..], out int numSize, default, CultureInfo.InvariantCulture))
			{
				return false;
			}

			written = n + 1 + numSize;
			return true;
		}

		private static string? CachedGeneration;

		static CapturedPacketId IParsable<CapturedPacketId>.Parse(string literal, IFormatProvider? provider) => Parse(literal);

		public static CapturedPacketId Parse(string literal)
		{
			Contract.NotNull(literal);
			return TryParse(literal, out var id) ? id : throw new FormatException("Malformed captured packet id.");
		}

		static bool IParsable<CapturedPacketId>.TryParse(string? literal, IFormatProvider? provider, out CapturedPacketId id) => TryParse(literal, out id);

		public static bool TryParse(string? literal, out CapturedPacketId id)
		{
			if (string.IsNullOrEmpty(literal))
			{
				id = Zero;
				return true;
			}

			int p = literal.IndexOf(':');
			if (p < 0)
			{
				id = default;
				return false;
			}

			if (!ulong.TryParse(literal.AsSpan(p + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong value))
			{
				id = default;
				return false;
			}

			// reuses the cached generation if possible!
			var generation = CachedGeneration;
			if (generation == null || !literal.AsSpan(0, p).SequenceEqual(generation))
			{
				generation = literal[..p];
				CachedGeneration = generation;
			}

			id = new CapturedPacketId(generation, value);
			return true;
		}

		/// <summary>Test if both ids belong to the same generation</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool SameGeneration(CapturedPacketId other) => this.Generation == other.Generation;

		/// <summary>Test if this id is the next packet (in the same generation) as the specified id</summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool IsSuccessorOf(CapturedPacketId predecessor)
		{
			return this.Generation == predecessor.Generation && (this.Value - 1) == predecessor.Value;
		}

		public static CapturedPacketId operator +(CapturedPacketId x, int y)
		{
			return y == 0 ? x
				: y > 0 ? new CapturedPacketId(x.Generation, checked(x.Value + (ulong) y))
				: new CapturedPacketId(x.Generation, checked(x.Value - (ulong) (-y)));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static CapturedPacketId operator ++(CapturedPacketId x)
		{
			return new CapturedPacketId(x.Generation, checked(x.Value + 1UL)); //BUGBUG: REVIEW: rollover?
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Value == right.Value && left.Generation == right.Generation;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Value != right.Value || left.Generation != right.Generation;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Generation == right.Generation ? left.Value < right.Value : string.CompareOrdinal(left.Generation, right.Generation) < 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Generation == right.Generation ? left.Value <= right.Value : string.CompareOrdinal(left.Generation, right.Generation) <= 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Generation == right.Generation ? left.Value > right.Value : string.CompareOrdinal(left.Generation, right.Generation) > 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(CapturedPacketId left, CapturedPacketId right)
		{
			return left.Generation == right.Generation ? left.Value >= right.Value : string.CompareOrdinal(left.Generation, right.Generation) >= 0;
		}

		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer)
		{
			writer.WriteValue(this.ToString());
		}

		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver)
		{
			return JsonString.Return(this.ToString());
		}

		static CapturedPacketId IJsonDeserializable<CapturedPacketId>.JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver)
		{
			return value is JsonString str && TryParse(str.Value, out var id) ? id : throw new JsonBindingException("Malformed captured packet id");
		}

	}

}
