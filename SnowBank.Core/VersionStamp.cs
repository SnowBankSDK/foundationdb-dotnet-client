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

namespace System
{
	using System.Buffers;
	using System.Buffers.Binary;
	using System.ComponentModel;
	using System.Runtime.InteropServices;
	using System.Text;
	using SnowBank.Data.Json;
	using Globalization;
	using SnowBank.Buffers;
	using SnowBank.Buffers.Binary;
	using SnowBank.Data.Binary;
	using SnowBank.Runtime;
	using SnowBank.Text;

	/// <summary>VersionStamp</summary>
	/// <remarks>A VersionStamp is unique, monotonically (but not sequentially) increasing value for each committed transaction.
	/// Its size can either be 10 bytes (80-bits) or 12-bytes (96-bits).
	/// The first 8 bytes are the committed version of the database. The next 2 bytes are monotonic in the serialization order for transactions.
	/// The optional last 2 bytes can contain a user-provider version number used to allow multiple stamps inside the same transaction.
	/// </remarks>
	[DebuggerDisplay("{ToString(),nq}")]
	[ImmutableObject(true), PublicAPI, Serializable]
	public readonly struct VersionStamp : IEquatable<VersionStamp>, IComparable<VersionStamp>, IComparable, IJsonSerializable, IJsonPackable, IJsonDeserializable<VersionStamp>, ISpanEncodable
		, IEquatable<Uuid80>, IComparable<Uuid80>
		, IEquatable<Uuid96>, IComparable<Uuid96>
#if NET8_0_OR_GREATER
		, ISpanFormattable
		, ISpanParsable<VersionStamp>
		, IUtf8SpanFormattable
		, IUtf8SpanParsable<VersionStamp>
#endif
	{
		//note: they are called "Versionstamp" in the doc, but "VersionStamp" seems more .NETy (like 'TimeSpan').

		private const ulong PLACEHOLDER_VERSION = ulong.MaxValue;
		private const ushort PLACEHOLDER_ORDER = ushort.MaxValue;
		private const ushort NO_USER_VERSION = 0;
		private const ulong HSB_VERSION = 0x8000000000000000UL;

		private const ushort FLAGS_NONE = 0x0;
		private const ushort FLAGS_HAS_VERSION = 0x1; // unset: 80-bits, set: 96-bits
		private const ushort FLAGS_IS_INCOMPLETE = 0x2; // unset: complete, set: incomplete

		/// <summary>The "empty" <see cref="VersionStamp"/></summary>
		/// <remarks>
		/// <para>This value will never be observed in the database, and can be used to represent the concept of <c>null</c>, <c>none</c> or <c>empty</c></para>
		/// <para>Please note that this is different from the <see cref="Incomplete()"/> stamp, which corresponds to a stamp whose value is not yet known, but will be replaced by concrete value in the near future (usually when the transaction commits).</para>
		/// </remarks>
		public static readonly VersionStamp None;

		/// <summary>Serialized bytes of the default incomplete stamp (composed of only 0xFF)</summary>
		internal static readonly Slice IncompleteToken = Slice.Repeat(0xFF, 10);

		/// <summary>Commit version of the transaction</summary>
		/// <remarks>This value is determined by the database at commit time.</remarks>
		
		public readonly ulong TransactionVersion; // Bytes 0..7

		/// <summary>Transaction Batch Order</summary>
		/// <remarks>This value is determined by the database at commit time.</remarks>
		public readonly ushort TransactionOrder; // Bytes 8..9

		/// <summary>User-provided version (between 0 and 65535)</summary>
		/// <remarks>For 80-bits VersionStamps, this value will be 0 and will not be part of the serialized key. You can use <see cref="HasUserVersion"/> to distinguish between both types of stamps.</remarks>
		public readonly ushort UserVersion; // Bytes 10..11 (if 'FLAGS_HAS_VERSION' is set)

		/// <summary>Internal flags (FLAGS_xxx constants)</summary>
		private readonly ushort Flags;
		//note: this flag is only present in memory, and is not serialized

		private VersionStamp(ulong version, ushort order, ushort user, ushort flags)
		{
			this.TransactionVersion = version;
			this.TransactionOrder = order;
			this.UserVersion = user;
			this.Flags = flags;
		}

		/// <summary>Converts an 80-bits UUID into an 80-bits VersionStamp</summary>
		public static VersionStamp FromUuid80(Uuid80 value)
		{
			Span<byte> buf = stackalloc byte[Uuid80.SizeOf]; // 10 required
			value.WriteToUnsafe(buf);
			ReadUnsafe(buf, out var vs);
			return vs;
		}

		/// <summary>Converts a 96-bits UUID into a 96-bits VersionStamp</summary>
		public static VersionStamp FromUuid96(Uuid96 value)
		{
			Span<byte> buf = stackalloc byte[Uuid96.SizeOf]; // 12 required
			value.WriteToUnsafe(buf);
			ReadUnsafe(buf, out var vs);
			return vs;
		}

		/// <summary>Creates an incomplete 80-bit <see cref="VersionStamp"/> with no user version.</summary>
		/// <returns>Placeholder that will be serialized as <code>FF FF FF FF FF FF FF FF FF FF</code> (10 bytes).</returns>
		/// <remarks>
		/// <para>This stamp contains a temporary marker that will be later filled by the database with the actual VersionStamp at transaction commit time.</para>
		/// <para>If you need to create multiple distinct stamps within the same transaction, please use <see cref="Incomplete(int)"/> instead.</para>
		/// </remarks>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete()
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, NO_USER_VERSION, FLAGS_IS_INCOMPLETE);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the VersionStamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <c>FF FF FF FF FF FF FF FF FF FF vv vv</c> (12 bytes) where <c>'vv vv'</c> is the user version encoded in little-endian.</returns>
		/// <remarks>
		/// <para>This stamp contains a temporary marker that will be later filled by the database with the actual VersionStamp at transaction commit time.</para>
		/// </remarks>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(int userVersion)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits.");
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, (ushort) userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		/// <summary>Creates an incomplete 96-bit <see cref="VersionStamp"/> with the given user version.</summary>
		/// <param name="userVersion">Value between 0 and 65535 that will be appended at the end of the VersionStamp, making it unique <i>within</i> the transaction.</param>
		/// <returns>Placeholder that will be serialized as <c>FF FF FF FF FF FF FF FF FF FF vv vv</c> (12 bytes) where <c>'vv vv'</c> is the user version encoded in little-endian.</returns>
		/// <exception cref="ArgumentException">If <paramref name="userVersion"/> is less than <c>0</c>, or greater than <c>65534</c> (0xFFFE).</exception>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Incomplete(ushort userVersion)
		{
			return new VersionStamp(PLACEHOLDER_VERSION, PLACEHOLDER_ORDER, userVersion, FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION);
		}

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Custom(ulong version, ushort order, bool incomplete)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
		}

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid80 uuid, bool incomplete)
		{
			// serialize the uuid
			Span<byte> ptr = stackalloc byte[Uuid80.SizeOf];
			uuid.WriteToUnsafe(ptr);

			// read the parts
			ulong version = BinaryPrimitives.ReadUInt64BigEndian(ptr);
			ushort order = BinaryPrimitives.ReadUInt16BigEndian(ptr[8..]);

			return new VersionStamp(version, order, NO_USER_VERSION, incomplete ? FLAGS_IS_INCOMPLETE : FLAGS_NONE);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Custom(ulong version, ushort order, int userVersion, bool incomplete)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits.");
			return new VersionStamp(version, order, (ushort) userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid80 uuid, ushort userVersion, bool incomplete)
		{
			// serialize the uuid
			Span<byte> ptr = stackalloc byte[Uuid80.SizeOf];
			uuid.WriteToUnsafe(ptr);

			// read the parts
			ulong version = BinaryPrimitives.ReadUInt64BigEndian(ptr);
			ushort order = BinaryPrimitives.ReadUInt16BigEndian(ptr[8..]);

			return new VersionStamp(version, order, userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		public static VersionStamp Custom(Uuid96 uuid, bool incomplete)
		{
			// serialize the uuid
			Span<byte> buffer = stackalloc byte[Uuid96.SizeOf];
			uuid.WriteToUnsafe(buffer);

			// read the parts
			ulong version = BinaryPrimitives.ReadUInt64BigEndian(buffer);
			ushort order = BinaryPrimitives.ReadUInt16BigEndian(buffer[8..]);
			ushort userVersion = BinaryPrimitives.ReadUInt16BigEndian(buffer[10..]);

			return new VersionStamp(version, order, userVersion, incomplete ? (ushort) (FLAGS_IS_INCOMPLETE | FLAGS_HAS_VERSION) : FLAGS_HAS_VERSION);
		}

		/// <summary>Creates an 80-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, without user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order)
		{
			return new VersionStamp(version, order, NO_USER_VERSION, FLAGS_NONE);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order, int userVersion)
		{
			Contract.Between(userVersion, 0, 0xFFFF, message: "Local version must fit in 16-bits, and cannot be 0xFFFF.");
			return new VersionStamp(version, order, (ushort) userVersion, FLAGS_HAS_VERSION);
		}

		/// <summary>Creates a 96-bit <see cref="VersionStamp"/>, obtained from the database.</summary>
		/// <returns>Complete stamp, with a user version.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static VersionStamp Complete(ulong version, ushort order, ushort userVersion)
		{
			return new VersionStamp(version, order, userVersion, FLAGS_HAS_VERSION);
		}

		/// <summary>Test if the stamp has a user version (96-bits) or not (80-bits)</summary>
		public bool HasUserVersion
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Flags & FLAGS_HAS_VERSION) != 0;
		}

		/// <summary>Test if the stamp is marked as <i>incomplete</i> (true), or has already been resolved by the database (false)</summary>
		public bool IsIncomplete
		{
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.Flags & FLAGS_IS_INCOMPLETE) != 0;
		}

		/// <summary>Return the length (in bytes) of the VersionStamp when serialized in binary format</summary>
		/// <returns>Returns 12 bytes for stamps with a user version, and 10 bytes without.</returns>
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetLength() => 10 + 2 * (this.Flags & FLAGS_HAS_VERSION);

		/// <inheritdoc />
		public override string ToString()
		{
			if (this.HasUserVersion)
			{
				return this.IsIncomplete
					? string.CreateInvariant($"@?#{this.UserVersion:x}")
					: string.CreateInvariant($"@{this.TransactionVersion:x}-{this.TransactionOrder:x}#{this.UserVersion:x}");
			}
			else
			{
				return this.IsIncomplete
					? "@?"
					: string.CreateInvariant($"@{this.TransactionVersion:x}-{this.TransactionOrder:x}");
			}
		}

		public string ToString(string? format, IFormatProvider? provider = null)
		{
			switch (format)
			{
				case null:
				case "D":
				case "d":
				{
					return ToString();
				}
				case "J":
				case "j":
				{ // "Java-like"
					return this.IsIncomplete ? ToJavaStringIncomplete(this.UserVersion) : ToJavaStringComplete(this.TransactionVersion, this.TransactionOrder, this.HasUserVersion ? this.UserVersion : default);
				}
				case "X":
				case "x":
				{
					return ToBase16String(in this, format == "x");
				}
				case "O":
				case "o":
				{
					return ToBase1024String(in this);
				}
				default:
				{
					throw new FormatException("Bad format specifier");
				}
			}

			static string ToBase16String(in VersionStamp stamp, bool lowerCase)
			{
				Span<char> buffer = stackalloc char[24];
				if (!stamp.TryFormatBase16(buffer, out int written, null, lowerCase))
				{
					throw new InvalidOperationException(); // not supported to happen?
				}
				return buffer[..written].ToString();
			}

			static string ToBase1024String(in VersionStamp stamp)
			{
				// will be either 8 or 10 characters
				Span<char> buffer = stackalloc char[10]; 
				// format to Base-1024
				stamp.TryFormatBase1024(buffer, out var charsWritten);
				// extract the string
				return buffer[..charsWritten].ToString();
			}

			static string ToJavaStringIncomplete(uint user)
			{
				return string.Create(CultureInfo.InvariantCulture, $"Versionstamp(<incomplete> {user})");
			}

			static string ToJavaStringComplete(ulong version, ushort order, ushort user)
			{
				// we need the byte representation of the stamp (excluding the user version)
				Span<byte> scratch = stackalloc byte[10];
				BinaryPrimitives.WriteUInt64BigEndian(scratch, version);
				BinaryPrimitives.WriteUInt16BigEndian(scratch[8..], order);

				var sb = new StringBuilder("Versionstamp(");
				// convert the bytes into "printable" characters, use the same encoding as found in ByteArrayUtil.printable(...) from the Java binding
				for (int i = 0; i < 10; i++)
				{
					byte b = scratch[i];
					if (b is >= 32 and < 127 && b != '\\')
					{
						sb.Append((char) b);
					}
					else if (b == '\\')
					{
						sb.Append(@"\\");
					}
					else
					{
						//use a lookup table here to avoid doing an expensive String.format() call
						sb.Append("\\x");
						int nib = (b & 0xF0) >> 4;
						sb.Append((char) (48 + nib + (((9 - nib) >> 31) & 39)));
						nib = b & 0x0F;
						sb.Append((char) (48 + nib + (((9 - nib) >> 31) & 39)));
					}
				}
				// append the user version (even if it is missing)
				sb.Append(' ');
				sb.Append(CultureInfo.InvariantCulture, $"{user}");
				sb.Append(')');
				return sb.ToString();
			}
		}

		/// <summary>Tries to format the value of the current instance into the provided span of characters.</summary>
		/// <param name="destination">The span in which to write this instance's value formatted as a span of characters.</param>
		/// <param name="charsWritten">When this method returns, contains the number of characters that were written in <paramref name="destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<char> destination,
			out int charsWritten,
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		)
		{
			return format switch
			{
				"" or "D" or "d" => TryFormatDefault(destination, out charsWritten, provider),
				"X" or "x" => TryFormatBase16(destination, out charsWritten, provider, format is "x"),
				"O" or "o" => TryFormatBase1024(destination, out charsWritten),
				_ => throw new FormatException("Bad format specified")
			};
		}

		private bool TryFormatDefault(Span<char> destination, out int bytesWritten, IFormatProvider? provider)
		{
			// must have _at least_ 2 characters with no user-version (to fit the incomplete stamp "@?"), and 4 characters with a user-version (to fit "@?#0")
			if (destination.Length < 2)
			{
				goto too_small;
			}

			destination[0] = '@';
			bytesWritten = 1;
			int len;

			if (this.IsIncomplete)
			{ // "@?" or "@?#123"
				destination[1] = '?';
				bytesWritten++;

				if (!this.HasUserVersion)
				{
					return true;
				}

				if (destination.Length < 4)
				{
					goto too_small;
				}
				destination[2] = '#';
				bytesWritten++;

				if (!this.UserVersion.TryFormat(destination[3..], out len, "x", provider))
				{
					goto too_small;
				}
				bytesWritten += len;
				return true;
			}

			// "@00000-0000" or "@00000-0000#0000"

			// transaction version
			destination = destination[1..];
			if (!this.TransactionVersion.TryFormat(destination, out len, "x", provider))
			{
				goto too_small;
			}
			bytesWritten += len;
			destination = destination[len..];

			// '-' separator
			if (destination.Length < 2)
			{
				goto too_small;
			}
			destination[0] = '-';
			bytesWritten += 1;
			destination = destination[1..];

			// transaction order
			if (TransactionOrder == 0)
			{
				destination[0] = '0';
				len = 1;
			}
			else if (!this.TransactionOrder.TryFormat(destination, out len, "x", provider))
			{
				goto too_small;
			}
			bytesWritten += len;

			if (this.HasUserVersion)
			{
				destination = destination[len..];

				// '#' separator
				if (destination.Length < 2)
				{
					goto too_small;
				}
				destination[0] = '#';
				bytesWritten += 1;
				destination = destination[1..];

				// transaction order
				if (!this.UserVersion.TryFormat(destination, out len, "x", provider))
				{
					goto too_small;
				}
				bytesWritten += len;
			}

			return true;

		too_small:
			bytesWritten = 0;
			return false;

		}

		private bool TryFormatBase16(Span<char> destination, out int charsWritten, IFormatProvider? provider, bool lowerCase)
		{
			if (!this.TransactionVersion.TryFormat(destination, out var len, lowerCase ? "x016" : "X016", provider))
			{
				goto too_small;
			}
			charsWritten = len;
			destination = destination[len..];

			if (!this.TransactionOrder.TryFormat(destination, out len, lowerCase ? "x04" : "X04", provider))
			{
				goto too_small;
			}
			charsWritten += len;

			if (this.HasUserVersion)
			{
				destination = destination[len..];
				if (!this.UserVersion.TryFormat(destination, out len, lowerCase ? "x04" : "X04", provider))
				{
					goto too_small;
				}
				charsWritten += len;
			}

			return true;

		too_small:
			charsWritten = 0;
			return false;
		}

		/// <summary>Tries to format the value of the current instance UTF-8 into the provided span of bytes.</summary>
		/// <param name="utf8Destination">The span in which to write this instance's value formatted as a span of bytes.</param>
		/// <param name="bytesWritten">When this method returns, contains the number of bytes that were written in <paramref name="utf8Destination" />.</param>
		/// <param name="format">A span containing the characters that represent a standard or custom format string that defines the acceptable format for <paramref name="utf8Destination" />.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns>
		/// <see langword="true" /> if the formatting was successful; otherwise, <see langword="false" />.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool TryFormat(
			Span<byte> utf8Destination,
			out int bytesWritten,
#if NET8_0_OR_GREATER
			[StringSyntax(StringSyntaxAttribute.GuidFormat)]
#endif
			ReadOnlySpan<char> format = default,
			IFormatProvider? provider = null
		)
		{
			return format switch
			{
				"" or "D" or "d" => TryFormatDefault(utf8Destination, out bytesWritten, provider),
				"O" or "o" => TryFormatBase1024(utf8Destination, out bytesWritten),
				"X" or "x" => throw new NotImplementedException("TODO: implement formatting VersionStamps to UTF-8 bytes in hexadecimal"),
				//TODO: support "J" has well?
				_ => throw new FormatException("Bad format specified"),
			};

		}

		private bool TryFormatDefault(Span<byte> utf8Destination, out int bytesWritten, IFormatProvider? provider)
		{
			// this is "easy mode" since all the characters are ASCII !
			// the maximum possible size for a VersionStamp is X characters:

			// must have _at least_ 2 characters with no user-version (to fit the incomplete stamp "@?"), and 4 characters with a user-version (to fit "@?#0")
			if (utf8Destination.Length < 2)
			{
				goto too_small;
			}

			utf8Destination[0] = (byte) '@';
			bytesWritten = 1;
			int len;

			if (this.IsIncomplete)
			{ // "@?" or "@?#123"
				utf8Destination[1] = (byte) '?';
				bytesWritten++;

				if (!this.HasUserVersion)
				{
					return true;
				}

				if (utf8Destination.Length < 4)
				{
					goto too_small;
				}
				utf8Destination[2] = (byte) '#';
				bytesWritten++;

				if (!this.UserVersion.TryFormat(utf8Destination[3..], out len, "x", provider))
				{
					goto too_small;
				}
				bytesWritten += len;
				return true;
			}

			// "@00000-0000" or "@00000-0000#0000"

			// transaction version
			utf8Destination = utf8Destination[1..];
			if (!this.TransactionVersion.TryFormat(utf8Destination, out len, "x", provider))
			{
				goto too_small;
			}
			bytesWritten += len;
			utf8Destination = utf8Destination[len..];

			// '-' separator
			if (utf8Destination.Length < 2)
			{
				goto too_small;
			}
			utf8Destination[0] = (byte) '-';
			bytesWritten += 1;
			utf8Destination = utf8Destination[1..];

			// transaction order
			if (TransactionOrder == 0)
			{
				utf8Destination[0] = (byte) '0';
				len = 1;
			}
			else if (!this.TransactionOrder.TryFormat(utf8Destination, out len, "x", provider))
			{
				goto too_small;
			}
			bytesWritten += len;

			if (this.HasUserVersion)
			{
				utf8Destination = utf8Destination[len..];

				// '#' separator
				if (utf8Destination.Length < 2)
				{
					goto too_small;
				}
				utf8Destination[0] = (byte) '#';
				bytesWritten += 1;
				utf8Destination = utf8Destination[1..];

				// transaction order
				if (!this.UserVersion.TryFormat(utf8Destination, out len, "x", provider))
				{
					goto too_small;
				}
				bytesWritten += len;
			}

			return true;

		too_small:
			bytesWritten = 0;
			return false;
		}

		/// <summary>Returns a newly allocated <see cref="Slice"/> that represents this VersionStamp</summary>
		/// <remarks>The slice with have a length of either 10 or 12 bytes.</remarks>
		public Slice ToSlice()
		{
			int len = GetLength(); // 10 or 12
			var tmp = new byte[len];
			WriteUnsafe(tmp.AsSpan(), in this);
			return new Slice(tmp);
		}

		/// <summary>Converts this 80-bits VersionStamp into an 80-bits UUID</summary>
		public Uuid80 ToUuid80()
		{
			if (this.HasUserVersion) throw new InvalidOperationException("Cannot convert 96-bit VersionStamp into a 80-bit UUID.");
			Span<byte> ptr = stackalloc byte[Uuid80.SizeOf];
			WriteUnsafe(ptr, in this);
			return Uuid80.ReadUnsafe(ptr);
		}

		/// <summary>Converts this 96-bits VersionStamp into a 96-bits UUID</summary>
		public Uuid96 ToUuid96()
		{
			if (!this.HasUserVersion) throw new InvalidOperationException("Cannot convert 80-bit VersionStamp into a 96-bit UUID.");
			Span<byte> ptr = stackalloc byte[Uuid96.SizeOf];
			WriteUnsafe(ptr, in this);
			return Uuid96.ReadUnsafe(ptr);
		}

		/// <summary>Writes this VersionStamp to the specified buffer, if it is large enough.</summary>
		/// <param name="buffer">Destination buffer, that must have a length of at least 10 or 12 bytes</param>
		/// <exception cref="ArgumentException"> if the buffer is not large enough.</exception>
		public int WriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) throw DestinationBufferTooSmall(len);
			WriteUnsafe(buffer[..len], in this);
			return len;

			[Pure, MethodImpl(MethodImplOptions.NoInlining)]
			static ArgumentException DestinationBufferTooSmall(int len) => new($"The target buffer must be at least {len} bytes long.");
		}

		/// <summary>Writes this VersionStamp to the specified buffer, if it is large enough.</summary>
		/// <param name="buffer">Destination buffer, that must have a length of at least 10 or 12 bytes</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> buffer)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len) return false;
			WriteUnsafe(buffer[..len], in this);
			return true;
		}

		/// <summary>Writes this VersionStamp to the specified buffer, if it is large enough.</summary>
		/// <param name="buffer">Destination buffer, that must have a length of at least 10 or 12 bytes</param>
		/// <param name="bytesWritten">Receives the number of bytes written to <paramref name="buffer"/> (either 10 or 12), if the operation is successful</param>
		/// <returns><c>true</c> if the buffer was large enough; otherwise, <c>false</c></returns>
		public bool TryWriteTo(Span<byte> buffer, out int bytesWritten)
		{
			int len = GetLength(); // 10 or 12
			if (buffer.Length < len)
			{
				bytesWritten = 0;
				return false;
			}
			WriteUnsafe(buffer[..len], in this);
			bytesWritten = len;
			return true;
		}

		/// <summary>Writes this VersionStamp to the specified destination</summary>
		public int WriteTo(ref SliceWriter writer)
		{
			int len = GetLength();
			WriteUnsafe(writer.AllocateSpan(len), in this);
			return len;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static void WriteUnsafe(Span<byte> buffer, in VersionStamp vs)
		{
			Contract.Debug.Assert(buffer.Length == 10 || buffer.Length == 12, "Buffer length must be 10 or 12");
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(buffer))
				{
					UnsafeHelpers.StoreUInt64BE(ptr, vs.TransactionVersion);
					UnsafeHelpers.StoreUInt16BE(ptr + 8, vs.TransactionOrder);
					if (buffer.Length >= 12)
					{
						UnsafeHelpers.StoreUInt16BE(ptr + 10, vs.UserVersion);
					}
				}
			}
		}

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static FormatException InvalidVersionStampSize() => new("A VersionStamp is either 10 or 12 bytes.");

		[Pure, MethodImpl(MethodImplOptions.NoInlining)]
		private static FormatException InvalidVersionStampFormat() => new("Unrecognized VersionStamp format.");

		/// <summary>Reads a <see cref="VersionStamp"/> from a sequence of bytes</summary>
		/// <exception cref="FormatException">If the buffer length is not exactly 10 or 12 bytes</exception>
		[Pure]
		public static VersionStamp ReadFrom(Slice data)
			=> TryReadFrom(data.Span, out var vs) ? vs : throw InvalidVersionStampSize();

		/// <summary>Try reading a <see cref="VersionStamp"/> from a sequence of bytes</summary>
		public static bool TryReadFrom(Slice data, out VersionStamp vs)
			=> TryReadFrom(data.Span, out vs);

		/// <summary>Reads a <see cref="VersionStamp"/> from a span of bytes</summary>
		/// <exception cref="FormatException">If the buffer length is not exactly 10 or 12 bytes</exception>
		[Pure]
		public static VersionStamp ReadFrom(ReadOnlySpan<byte> data)
			=> TryReadFrom(data, out var vs) ? vs : throw InvalidVersionStampSize();

		/// <summary>Attempts to read a <see cref="VersionStamp"/> from a span of bytes.</summary>
		/// <param name="data">The byte sequence to parse.</param>
		/// <param name="vs">When this method returns, contains the parsed <see cref="VersionStamp"/> if the parsing succeeded, or the default value if it failed.</param>
		/// <returns><see langword="true"/> if the parsing was successful; otherwise, <see langword="false"/>.</returns>
		/// <remarks>A valid <see cref="VersionStamp"/> is either 10 or 12 bytes long.</remarks>
		public static bool TryReadFrom(ReadOnlySpan<byte> data, out VersionStamp vs)
		{
			if (data.Length != 10 && data.Length != 12)
			{
				vs = default;
				return false;
			}
			ReadUnsafe(data, out vs);
			return true;
		}

		/// <summary>[DANGEROUS] Reads a VersionStamp from a source buffer, that must be large enough.</summary>
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static void ReadUnsafe(ReadOnlySpan<byte> buf, out VersionStamp vs)
		{
			// reads a complete 10 or 12 bytes VersionStamp
			Contract.Debug.Assert(buf.Length == 10 || buf.Length == 12);
			unsafe
			{
				fixed (byte* ptr = &MemoryMarshal.GetReference(buf))
				{
					ulong ver = UnsafeHelpers.LoadUInt64BE(ptr);
					ushort order = UnsafeHelpers.LoadUInt16BE(ptr + 8);
					ushort idx = buf.Length == 10 ? NO_USER_VERSION : UnsafeHelpers.LoadUInt16BE(ptr + 10);
					ushort flags = FLAGS_NONE;
					if (buf.Length == 12) flags |= FLAGS_HAS_VERSION;
					if ((ver & HSB_VERSION) != 0) flags |= FLAGS_IS_INCOMPLETE;
					vs = new VersionStamp(ver, order, idx, flags);
				}
			}
		}

		/// <summary>Parses the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns><see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>.</returns>
		/// <exception cref="FormatException">If the format is not valid.</exception>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static VersionStamp Parse(string s, IFormatProvider? provider)
		{
			Contract.NotNull(s);
			return Parse(s.AsSpan(), provider);
		}

		/// <summary>Attempts to parse the specified <see cref="ReadOnlySpan{T}"/> of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">When this method returns, contains the <see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>, if the conversion succeeded, or the default value if the conversion failed.</param>
		/// <returns><see langword="true"/> if the <paramref name="s"/> was successfully parsed; otherwise, <see langword="true"/>.</returns>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/></remarks>
		public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out VersionStamp result)
		{
			if (s == null || !TryParse(s.AsSpan(), provider, out result))
			{
				result = default;
				return false;
			}
			return true;
		}

		/// <summary>Parses a span of characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns><see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>.</returns>
		/// <exception cref="FormatException">If the format is not valid.</exception>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/> or <see cref="TryFormat(System.Span{char},out int,System.ReadOnlySpan{char},System.IFormatProvider?)"/></remarks>
		public static VersionStamp Parse(ReadOnlySpan<char> s, IFormatProvider? provider = null)
		{
			if (!TryParse(s, provider, out var result))
			{
				throw InvalidVersionStampFormat();
			}

			return result;
		}

		/// <summary>Attempts to parse the span characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">When this method returns, contains the <see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>, if the conversion succeeded, or the default value if the conversion failed.</param>
		/// <returns><see langword="true"/> if the <paramref name="s"/> was successfully parsed; otherwise, <see langword="true"/>.</returns>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/> or <see cref="TryFormat(System.Span{char},out int,System.ReadOnlySpan{char},System.IFormatProvider?)"/></remarks>
		public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out VersionStamp result)
		{
			if (s.Length < 2) goto invalid;
			if (s[0] != '@') goto invalid;

			s = s[1..];

			if (s[0] == '?')
			{ // incomplete: "@?" or "@?#USER"

				if (s.Length == 1)
				{
					result = Incomplete();
					return true;
				}
				
				if (s.Length < 3 || s[1] != '#') goto invalid;
				
				if (!ushort.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var x))
				{
					goto invalid;
				}

				result = Incomplete(x);
				return true;
			}

			// complete: "@VERSION-ORDER" or "@VERSION-ORDER#USER"
			int p = s.IndexOf('-');
			if (p <= 0 || p > 16) goto invalid;

			if (!ulong.TryParse(s[..p], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var version))
			{
				goto invalid;
			}

			s = s[(p + 1)..];

			p = s.IndexOf('#');
			if (p == 0 || p > 4) goto invalid;
			ushort user, flags;
			
			if (p > 0)
			{
				var tail = s[(p + 1)..];
				if (tail.Length == 0 || tail.Length > 4) goto invalid;
				if (!ushort.TryParse(tail, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out user))
				{
					goto invalid;
				}
				s = s[..p];
				flags = FLAGS_HAS_VERSION;
			}
			else
			{
				user = 0;
				flags = NO_USER_VERSION;
			}

			if (!ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var order))
			{
				goto invalid;
			}

			result = new(version, order, user, flags);
			return true;
			
		invalid:
			result = default;
			return false;
		}

		/// <summary>Parses a span of UTF-8 characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <returns><see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>.</returns>
		/// <exception cref="FormatException">If the format is not valid.</exception>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/> or <see cref="TryFormat(System.Span{byte},out int,System.ReadOnlySpan{char},System.IFormatProvider?)"/></remarks>
		public static VersionStamp Parse(ReadOnlySpan<byte> s, IFormatProvider? provider = null)
		{
			if (!TryParse(s, provider, out var result))
			{
				throw InvalidVersionStampFormat();
			}

			return result;
		}

		/// <summary>Attempts to parse a span of UTF-8 characters into a <see cref="VersionStamp"/>.</summary>
		/// <param name="s">The span of characters to parse.</param>
		/// <param name="provider">This parameter is ignored.</param>
		/// <param name="result">When this method returns, contains the <see cref="VersionStamp"/> value equivalent to the characters contained in <paramref name="s"/>, if the conversion succeeded, or the default value if the conversion failed.</param>
		/// <returns><see langword="true"/> if the <paramref name="s"/> was successfully parsed; otherwise, <see langword="true"/>.</returns>
		/// <remarks>This method can parse the result of calling <see cref="ToString()"/> or <see cref="TryFormat(System.Span{byte},out int,System.ReadOnlySpan{char},System.IFormatProvider?)"/></remarks>
		public static bool TryParse(ReadOnlySpan<byte> s, IFormatProvider? provider, out VersionStamp result)
		{
			if (s.Length < 2) goto invalid;
			if (s[0] != '@') goto invalid;

			s = s[1..];

			if (s[0] == '?')
			{ // incomplete: "@?" or "@?#USER"

				if (s.Length == 1)
				{
					result = Incomplete();
					return true;
				}
				
				if (s.Length < 3 || s[1] != '#') goto invalid;
				
				if (!ushort.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var x))
				{
					goto invalid;
				}

				result = Incomplete(x);
				return true;
			}

			// complete: "@VERSION-ORDER" or "@VERSION-ORDER#USER"
			int p = s.IndexOf((byte) '-');
			if (p <= 0 || p > 16) goto invalid;

			if (!ulong.TryParse(s[..p], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var version))
			{
				goto invalid;
			}

			s = s[(p + 1)..];

			p = s.IndexOf((byte) '#');
			if (p == 0 || p > 4) goto invalid;
			ushort user, flags;
			
			if (p > 0)
			{
				var tail = s[(p + 1)..];
				if (tail.Length == 0 || tail.Length > 4) goto invalid;
				if (!ushort.TryParse(tail, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out user))
				{
					goto invalid;
				}
				s = s[..p];
				flags = FLAGS_HAS_VERSION;
			}
			else
			{
				user = 0;
				flags = NO_USER_VERSION;
			}

			if (!ushort.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var order))
			{
				goto invalid;
			}

			result = new(version, order, user, flags);
			return true;
			
		invalid:
			result = default;
			return false;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator VersionStamp(Uuid80 value) => FromUuid80(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid80(VersionStamp value) => value.ToUuid80();

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator VersionStamp(Uuid96 value) => FromUuid96(value);

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Uuid96(VersionStamp value) => value.ToUuid96();

		#region Equality, Comparision, ...

		/// <inheritdoc />
		public override bool Equals(object? obj) => obj switch
		{
			VersionStamp vs => Equals(vs),
			Uuid80 u80 => Equals(u80),
			Uuid96 u96 => Equals(u96),
			_ => false
		};

		/// <inheritdoc />
		public override int GetHashCode()
		{
			return HashCode.Combine(this.TransactionVersion.GetHashCode(), (int) this.TransactionOrder, (int) this.UserVersion, (int) this.Flags);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(VersionStamp other)
		{
			//PERF: could we use Unsafe and compare the next sizeof(VersionStamp) bytes at once?
			return (this.TransactionVersion == other.TransactionVersion)
			   && (this.TransactionOrder == other.TransactionOrder)
			   && (this.UserVersion == other.UserVersion)
			   && (this.Flags == other.Flags);
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid80 other)
		{
			return !this.HasUserVersion && ToUuid80().Equals(other);
		}

		/// <inheritdoc />
		public int CompareTo(Uuid80 other)
		{
			if (!this.HasUserVersion)
			{
				return this.ToUuid80().CompareTo(other);
			}

			int cmp = ToUuid96().Upper80.CompareTo(other);
			if (cmp == 0)
			{
				cmp = this.UserVersion > 0 ? +1 : 0;
			}
			return cmp;
		}

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Uuid96 other)
		{
			return this.HasUserVersion && ToUuid96().Equals(other);
		}

		/// <inheritdoc />
		public int CompareTo(Uuid96 other)
		{
			if (this.HasUserVersion)
			{
				return ToUuid96().CompareTo(other);
			}

			int cmp = ToUuid80().CompareTo(other.Upper80);
			if (cmp == 0)
			{
				cmp = other.Lower16 > 0 ? -1 : 0;
			}
			return cmp;
		}

		/// <inheritdoc />
		public int CompareTo(object? other) => other switch
		{
			VersionStamp vs => this.CompareTo(vs),
			Uuid80 u80 => this.CompareTo(u80),
			Uuid96 u96 => this.CompareTo(u96),
			null => +1,
			_ => throw new ArgumentException($"Cannot compare a VersionStamp with an instance of {other.GetType().GetFriendlyName()}"),
		};

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator ==(VersionStamp left, VersionStamp right)
		{
			return left.Equals(right);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator !=(VersionStamp left, VersionStamp right)
		{
			return !left.Equals(right);
		}

		/// <summary>Returns the successor of this <see cref="VersionStamp"/></summary>
		/// <param name="left">Instance to increment</param>
		/// <returns>Smallest <see cref="VersionStamp"/> that is strictly greater than this value.</returns>
		/// <remarks>The operator will first increment the <see cref="UserVersion"/>, propagating the carry to the <see cref="TransactionOrder"/> and then the <see cref="TransactionVersion"/> in case of overlow</remarks>
		/// <exception cref="OverflowException">If <paramref name="left"/> is already the maximum possible value</exception>
		public static VersionStamp operator++(VersionStamp left)
		{
			ulong transactionVersion = left.TransactionVersion;
			int transactionOrder = left.TransactionOrder;
			int userVersion = left.HasUserVersion ? left.UserVersion : 0;

			// simply increment the UserVersion
			++userVersion;

			if (userVersion > ushort.MaxValue)
			{ // overflows into the transaction order
				transactionOrder++;
				userVersion = 0;

				if (transactionOrder > ushort.MaxValue)
				{ // overflows into the transaction version
					transactionVersion = checked(transactionVersion + 1);
					transactionOrder = 0;
				}
			}

			return new(transactionVersion, (ushort) transactionOrder, (ushort) userVersion, userVersion != 0 ? FLAGS_HAS_VERSION : FLAGS_NONE);
		}

		/// <inheritdoc />
		[Pure]
		public int CompareTo(VersionStamp other)
		{
			//ordering rules:
			// - incomplete stamps are stored AFTER resolved stamps (since if they commit they would have a value higher than any other stamp already in the database)
			// - ordered by transaction number then transaction batch order
			// - stamps with no user version are sorted before stamps with user version if they have the same first 10 bytes, so (XXXX) is before (XXXX, 0)

			if (this.IsIncomplete)
			{ // we ignore the transaction version/order!
				if (!other.IsIncomplete) return +1; // we are after
			}
			else
			{
				if (other.IsIncomplete) return -1; // we are before
				int cmp = this.TransactionVersion.CompareTo(other.TransactionVersion);
				if (cmp == 0) cmp = this.TransactionOrder.CompareTo(other.TransactionOrder);
				if (cmp != 0) return cmp;
			}

			// both have same version+order, or both are incomplete
			// => we need to decide on the (optional) user version
			return this.HasUserVersion 
				? (other.HasUserVersion ? this.UserVersion.CompareTo(other.UserVersion) : +1)
				: (other.HasUserVersion ? -1 : 0);
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) < 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator <=(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) <= 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) > 0;
		}

		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator >=(VersionStamp left, VersionStamp right)
		{
			return left.CompareTo(right) >= 0;
		}

		//REVIEW: does these make sense or not?
		// VersionStamp - VersionStamp == ???
		// VersionStamp + 123 == ???
		// VersionStamp * 2 == ???

		/// <summary>Compares <see cref="VersionStamp"/> instances for equality and ordering</summary>
		public sealed class Comparer : IEqualityComparer<VersionStamp>, IComparer<VersionStamp>
		{

			/// <summary>Default comparer for <see cref="VersionStamp"/>s</summary>
			public static Comparer Default { get; } = new();

			private Comparer()
			{ }

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public bool Equals(VersionStamp x, VersionStamp y)
			{
				return x.Equals(y);
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetHashCode(VersionStamp obj)
			{
				return obj.GetHashCode();
			}

			/// <inheritdoc />
			[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(VersionStamp x, VersionStamp y)
			{
				return x.CompareTo(y);
			}

		}

		#endregion

		#region JSON Serialization...

		/// <inheritdoc />
		void IJsonSerializable.JsonSerialize(CrystalJsonWriter writer) => writer.WriteValue(this.ToString());

		/// <inheritdoc />
		JsonValue IJsonPackable.JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver) => JsonString.Return(this.ToString());

		/// <inheritdoc />
		public static VersionStamp JsonDeserialize(JsonValue value, ICrystalJsonTypeResolver? resolver = null) => value switch
		{
			null or JsonNull => Incomplete(), //REVIEW: there is no real good candidate for this ... ?
			JsonString str => Parse(str.Value),
			_ => throw JsonBindingException.CannotBindJsonValueToThisType(value, typeof(VersionStamp)),
		};

		#endregion

		/// <summary>Try formatting this <see cref="VersionStamp"/> into Base-1024 string literal</summary>
		/// <param name="destination">Buffer that must have a size of at least 8 or 10 characters (depending on the value of <see cref="HasUserVersion"/>)</param>
		/// <param name="charsWritten">Number of bytes written to <paramref name="destination"/>, always equal to <c>10</c></param>
		/// <exception cref="ArgumentException"><paramref name="destination"/> is not large enough</exception>
		/// <remarks>
		/// <para>This will write 16 characters into <paramref name="destination"/> that each represent a 10-bit chunk of the combined prefix and version stamp.</para>
		/// <para>Each 10-bit chunk is offset by 48, so that 0b0000000000 is mapped to the '0' character.</para>
		/// <para>The resulting string should preserver the sort order of the 160-bit input.</para>
		/// </remarks>
		public bool TryFormatBase1024(Span<char> destination, out int charsWritten)
		{
			if (destination.Length < (this.HasUserVersion ? 10 : 8))
			{ // cannot be smaller than 8
				charsWritten = 0;
				return false;
			}

			// serialize the stamp to the stack, and then encode that buffer to Base-1024
			Span<byte> input = stackalloc byte[12];
			BinaryPrimitives.WriteUInt64BigEndian(input, this.TransactionVersion);
			BinaryPrimitives.WriteUInt16BigEndian(input[8..], this.TransactionOrder);
			if (!this.HasUserVersion)
			{
				return Base1024Encoding.TryEncodeTo(input[..10], destination, out charsWritten);
			}

			BinaryPrimitives.WriteUInt16BigEndian(input[10..], this.HasUserVersion ? this.UserVersion : default);
			return Base1024Encoding.TryEncodeTo(input, destination, out charsWritten);
		}

		public bool TryFormatBase1024(Span<byte> utf8Destination, out int bytesWritten)
		{
			// we will first format to a temp chars buffer, then convert this buffer to utf-8
			Span<char> chars = stackalloc char[10]; // will be either 8 or 10
			if (TryFormatBase1024(chars, out var charsWritten))
			{
				// this is not supposed to happen!

				switch (System.Text.Unicode.Utf8.FromUtf16(chars[..charsWritten], utf8Destination, out _, out bytesWritten))
				{
					case OperationStatus.Done:
					{
						return true;
					}
					case OperationStatus.DestinationTooSmall:
					{
						bytesWritten = 0;
						return false;
					}
				}
			}

			// this error does not depend on the destination buffer size
			// => we have to throw otherwise the caller will keep calling with a larger and larger buffer
#if DEBUG
			// this is not supposed to happen, unless there is a bug on our side in the formatting code
			if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
#endif
			throw new InvalidOperationException();
		}

		public static VersionStamp ParseBase1024(string source)
			=> ParseBase1024(source.AsSpan());

		public static VersionStamp ParseBase1024(ReadOnlySpan<char> source)
		{
			if (!TryParseBase1024(source, out var result))
			{
				throw new FormatException("Failed to parse VersionStamp from Base-1024 string literal");
			}
			return result;
		}

		public static bool TryParseBase1024(string source, out VersionStamp result)
			=> TryParseBase1024(source.AsSpan(), out result);

		public static bool TryParseBase1024(ReadOnlySpan<char> source, out VersionStamp result)
		{
			if (source.Length is not (8 or 10))
			{
				result = default;
				return false;
			}

			Span<byte> output = stackalloc byte[12];

			if (!Base1024Encoding.TryDecodeTo(source, output, out int bytesWritten) || bytesWritten is not (10 or 12))
			{
				result = default;
				return false;
			}

			result = ReadFrom(output[..bytesWritten]);
			return true;
		}

		#region ISpanEncodable...

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSpan(out ReadOnlySpan<byte> span) { span = default; return false; }

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryGetSizeHint(out int sizeHint) { sizeHint = (this.Flags & FLAGS_HAS_VERSION) != 0 ? 12 : 10; return true; }

		/// <inheritdoc />
		[Pure, MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool ISpanEncodable.TryEncode(scoped Span<byte> destination, out int bytesWritten)
			=> TryWriteTo(destination, out bytesWritten);

		#endregion

	}

}
