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

namespace FoundationDB.Client
{

	/// <summary>Value that wraps a <see cref="JsonValue"/>, encoded into UTF-8 bytes</summary>
	public readonly struct FdbJsonValue : IFdbValue
		, IEquatable<FdbJsonValue>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbJsonValue(JsonValue data, CrystalJsonSettings? settings = null)
		{
			this.Data = data;
			this.JsonSettings = settings;
		}

		/// <summary>JSON Value</summary>
		public readonly JsonValue Data;

		/// <summary>JSON Serialization Settings</summary>
		public readonly CrystalJsonSettings? JsonSettings;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.Json;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Data.ToString("C"),
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.CreateInvariant($"{nameof(FdbJsonValue)}({this.Data})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Data.TryFormat(destination, out charsWritten, "C", null),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbJsonValue)}({this.Data})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
#if NET8_0_OR_GREATER
			if (this.JsonSettings is null || this.JsonSettings.Equals(CrystalJsonSettings.JsonCompact))
			{
				return this.Data.TryFormat(destination, out bytesWritten, "C", null);
			}
			if (this.JsonSettings.Equals(CrystalJsonSettings.Json))
			{
				return this.Data.TryFormat(destination, out bytesWritten, "D", null);
			}
			if (this.JsonSettings.Equals(CrystalJsonSettings.JsonIndented))
			{
				return this.Data.TryFormat(destination, out bytesWritten, "P", null);
			}
			if (this.JsonSettings.Equals(CrystalJsonSettings.JavaScript))
			{
				return this.Data.TryFormat(destination, out bytesWritten, "J", null);
			}
			return TryEncodeSlow(destination, out bytesWritten, this.Data, this.JsonSettings);
#else
			// JsonValue does not expose the UTF-8 TryFormat fast paths on this target: format to a string first, then encode it to UTF-8 (which allocates)
			string literal;
			if (this.JsonSettings is null || this.JsonSettings.Equals(CrystalJsonSettings.JsonCompact))
			{
				literal = this.Data.ToString("C");
			}
			else if (this.JsonSettings.Equals(CrystalJsonSettings.Json))
			{
				literal = this.Data.ToString("D");
			}
			else if (this.JsonSettings.Equals(CrystalJsonSettings.JsonIndented))
			{
				literal = this.Data.ToString("P");
			}
			else if (this.JsonSettings.Equals(CrystalJsonSettings.JavaScript))
			{
				literal = this.Data.ToString("J");
			}
			else
			{
				return TryEncodeSlow(destination, out bytesWritten, this.Data, this.JsonSettings);
			}

			int len = Encoding.UTF8.GetByteCount(literal);
			if (destination.Length < len)
			{
				bytesWritten = 0;
				return false;
			}
			bytesWritten = Encoding.UTF8.GetBytes(literal.AsSpan(), destination);
			return true;
#endif

			static bool TryEncodeSlow(Span<byte> destination, out int bytesWritten, JsonValue data, CrystalJsonSettings settings)
			{
				// we don't have a fast implementation for these settings, we will have to serialize into a pooled buffer
				using var bytes = data.ToJsonSlice(ArrayPool<byte>.Shared, settings);
				return bytes.TryCopyTo(destination, out bytesWritten);
			}
		}

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbJsonValue value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbJsonValue value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbJsonValue other) => this.Data.StrictEquals(other.Data);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbValueHelpers.AreEqual(in this, other);

		#endregion

	}

	/// <summary>Value that wraps a <see cref="JsonValue"/>, encoded into UTF-8 bytes</summary>
	public readonly struct FdbJsonValue<T> : IFdbValue
		, IEquatable<FdbJsonValue<T>>
		, IEquatable<FdbJsonValue>
	{

		[SkipLocalsInit, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbJsonValue(T? data, IJsonSerializer<T>? serializer = null, CrystalJsonSettings? settings = null)
		{
			this.Data = data;
			this.Serializer = serializer;
			this.JsonSettings = settings;
		}

		/// <summary>Serialized instance</summary>
		public readonly T? Data;

		/// <summary>Custom Serializer (optional)</summary>
		public readonly IJsonSerializer<T>? Serializer;

		/// <summary>JSON Serialization Settings</summary>
		public readonly CrystalJsonSettings? JsonSettings;

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public FdbValueTypeHint GetTypeHint() => FdbValueTypeHint.Json;

		#region Formatting...

		/// <inheritdoc />
		public override string ToString() => ToString(null);

		/// <inheritdoc />
		public string ToString(string? format, IFormatProvider? formatProvider = null) => (format ?? "") switch
		{
			"" or "D" or "d" or "V" or "v" => this.Data?.ToString() ?? "",
			"X" or "x" => this.ToSlice().ToString(format),
			"G" or "g" => string.CreateInvariant($"{nameof(FdbJsonValue)}({this.Data})"),
			_ => throw new FormatException(),
		};

		/// <inheritdoc />
		public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider) => format switch
		{
			"" or "D" or "d" or "V" or "v" => this.Data is ISpanFormattable sf ? sf.TryFormat(destination, out charsWritten, default, null) : (this.Data?.ToString() ?? "").TryCopyTo(destination, out charsWritten),
			"X" or "x" => this.ToSlice().TryFormat(destination, out charsWritten, format),
			"G" or "g" => destination.TryWrite(CultureInfo.InvariantCulture, $"{nameof(FdbJsonValue)}({this.Data})", out charsWritten),
			_ => throw new FormatException(),
		};

		#endregion

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSpan(out ReadOnlySpan<byte> span)
		{
			span = default;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryGetSizeHint(out int sizeHint)
		{
			sizeHint = 0;
			return false;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryEncode(scoped Span<byte> destination, out int bytesWritten)
		{
			//PERF: is there a better way?
			using var bytes = CrystalJson.ToSlice(this.Data, this.Serializer, ArrayPool<byte>.Shared, this.JsonSettings ?? CrystalJsonSettings.JsonCompact);
			return bytes.Span.TryCopyTo(destination, out bytesWritten);
		}

		#endregion

		#region Comparisons...

		/// <inheritdoc />
		public override int GetHashCode() => throw FdbValueHelpers.ErrorCannotComputeHashCodeMessage();

		/// <inheritdoc />
		public override bool Equals([NotNullWhen(true)] object? obj) => obj switch
		{
			Slice bytes => Equals(bytes.Span),
			FdbJsonValue<T> value => Equals(value),
			FdbJsonValue value => Equals(value),
			FdbRawValue value => Equals(value),
			IFdbValue value => FdbValueHelpers.AreEqual(in this, value),
			_ => false,
		};

		/// <inheritdoc />
		public bool Equals([NotNullWhen(true)] IFdbValue? other) => other switch
		{
			null => false,
			FdbJsonValue<T> value => Equals(value),
			FdbJsonValue value => Equals(value),
			FdbRawValue value => Equals(value),
			_ => FdbValueHelpers.AreEqual(in this, other),
		};

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbJsonValue<T> other)
			=> ReferenceEquals(this.Data, other.Data)
			|| this.Data is not null && (typeof(T).IsAssignableTo(typeof(IEquatable<T>))
				? ((IEquatable<T>) this.Data!).Equals(other.Data)
				: FdbValueHelpers.AreEqual(in this, in other));

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbJsonValue other) => FdbValueHelpers.AreEqual(in this, in other);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(FdbRawValue other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Slice other) => Equals(other.Span);

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(ReadOnlySpan<byte> other) => FdbValueHelpers.AreEqual(in this, other);

		#endregion

	}

}
