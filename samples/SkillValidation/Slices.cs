// Compile-validation for the snowbank-slices-and-buffers skill (SKILL.md + references).
// Exercises the everyday Slice/SliceReader/SliceWriter/SliceOwner patterns plus the Span-first
// equivalents and the pooled accumulators/buffers. Nothing connects to a database.

using System.Buffers;
using System.Buffers.Binary;
using SnowBank.Buffers;        // SliceWriter, SliceReader, SpanReader, SpanWriter, ValueBuffer, ArraySliceWriter, SlicePool
using SnowBank.Data.Binary;    // ISpanEncodable

namespace SkillValidation;

public static class Slices
{
	public static void Core()
	{
		// construction, a Slice is a VIEW over the array (no copy)
		byte[] b = [1, 2, 3, 4, 5];
		Slice view = b.AsSlice();
		Slice sub = b.AsSlice(1, 3);
		Slice fromSpan = Slice.FromBytes("abc"u8);
		Slice text = Slice.FromStringUtf8("héllo");
		Slice ascii = Slice.FromStringAscii("ABC");
		Slice zero = Slice.Zero(16);
		Slice hex = Slice.FromHexString("00ff1234");
		Slice g = Slice.FromGuid(Guid.NewGuid());

		// the three distinct integer encodings
		Slice min = Slice.FromInt32(258);       // minimal little-endian (1-4 bytes)
		Slice fix = Slice.FromFixed32(258);      // always 4 bytes LE
		Slice vint = Slice.FromVarint32(258);    // LEB128 varint
		Slice be = Slice.FromFixed32BE(258);     // 4 bytes BE (sortable)

		// Nil vs Empty
		bool nil = Slice.Nil.IsNull;             // true
		bool emp = Slice.Empty.IsEmpty;          // true
		bool noe = Slice.Nil.IsNullOrEmpty;      // true
		bool pres = Slice.Empty.IsPresent;       // true
		byte[]? nilBytes = Slice.Nil.GetBytes(); // null
		string? nilStr = Slice.Nil.ToStringUtf8(); // null

		// reading
		long n64 = Slice.FromFixed64(123).ToInt64();
		int beVal = be.ToInt32BE();
		string s = text.ToStringUtf8()!;
		byte[] copy = view.ToArray();            // defensive copy
		ReadOnlySpan<byte> span = view.Span;
		ReadOnlyMemory<byte> mem = view.Memory;
		Slice rangeSlice = view[1..4];
		Slice tail = view[^2..];
		Slice substr = view.Substring(1, 3);

		// comparison (lexicographic by bytes)
		bool lt = Slice.FromStringAscii("a").CompareTo(Slice.FromStringAscii("b")) < 0;
		bool starts = view.StartsWith(b.AsSlice(0, 2));
		var set = new SortedSet<Slice>(Slice.Comparer.Default) { view, sub, fromSpan };

		_ = (sub, fromSpan, ascii, zero, hex, g, min, fix, vint, nil, emp, noe, pres,
		     nilBytes, nilStr, n64, beVal, s, copy, span.Length, mem.Length, rangeSlice, tail, substr, lt, starts, set.Count);
	}

	public static void WriteAndRead()
	{
		// build with SliceWriter (self-delimiting writes for sequential parsing)
		var w = new SliceWriter();
		w.WriteInt32(42);          // fixed 4 bytes LE (self-delimiting)
		w.WriteVarInt32(1000);
		w.WriteVarString("hello");
		w.WriteStringUtf8("raw");
		w.WriteBytes("xyz"u8);
		Slice packed = w.ToSlice();
		int pos = w.Position;

		// parse with SliceReader (each read pairs with the matching write)
		var r = packed.ToSliceReader();
		int f = r.ReadInt32();
		uint v = r.ReadVarInt32();
		string vs = r.ReadVarString();
		string raw = r.ReadBytes(3).ToStringUtf8()!;   // raw string: read the known length
		bool more = r.HasMore;
		int rem = r.Remaining;
		Slice restHead = r.Tail;
		Slice rest = r.ReadToEnd();

		// ISpanEncodable: render into a caller buffer with no intermediate Slice
		var enc = (ISpanEncodable) w;
		if (enc.TryGetSizeHint(out int size))
		{
			var dst = new byte[size];
			enc.TryEncode(dst, out int written);
			_ = written;
		}

		_ = (pos, f, v, vs, raw, more, rem, restHead, rest);
	}

	public static void Pooling()
	{
		// SliceWriter renting from a pool -> hand the buffer off as a SliceOwner
		var w = new SliceWriter(ArrayPool<byte>.Shared);
		w.WriteStringUtf8("Hello, pool!");
		using (SliceOwner owner = w.ToSliceOwner())
		{
			_ = (owner.IsValid, owner.Count, owner.Data.Count, owner.Span.Length, owner.Pool);
		}   // buffer returned to the pool here

		// rent a copy of some bytes directly
		using (var owner2 = Slice.FromBytes("payload"u8, ArrayPool<byte>.Shared))
		{
			_ = owner2.Data.ToArray();
		}
	}

	public static void SpanFirst()
	{
		// SpanWriter over stack memory (fixed buffer, zero allocation, won't grow)
		Span<byte> scratch = stackalloc byte[64];
		var sw = new SpanWriter(scratch);
		sw.WriteInt32(42);
		sw.WriteInt64BE(0xDEADBEEF);
		sw.WriteByte(0xFF);
		sw.WriteBytes("ok"u8);
		Span<byte> room = sw.Allocate(4);
		room.Clear();
		ReadOnlySpan<byte> written = sw.ToSpan();

		// SpanReader over the written span
		var sr = new SpanReader(written);
		int n = sr.ReadInt32();
		long u = sr.ReadInt64BE();
		int peek = sr.PeekByte();
		_ = (n, u, peek, sr.Remaining);
	}

	public static void Accumulators()
	{
		// ValueBuffer<T>: stack-seeded growable accumulator; result is a single contiguous span
		using var buf = new ValueBuffer<int>(stackalloc int[8]);
		buf.Add(1);
		buf.AddRange([2, 3, 4]);
		Span<int> items = buf.GetSpan();
		int[] arr = buf.ToArray();

		// ArraySliceWriter: an IBufferWriter<byte> producing a contiguous result
		var asw = new ArraySliceWriter();
		Span<byte> dst = asw.GetSpan(8);
		BinaryPrimitives.WriteInt64LittleEndian(dst, 123);
		asw.Advance(8);
		ReadOnlySpan<byte> result = asw.WrittenSpan;

		// ISliceAllocator: many short-lived slices sub-allocated from shared slabs, released together
		using var alloc = new ArraySliceAllocator();
		ArraySegment<byte> seg1 = alloc.Allocate(4);
		ArraySegment<byte> seg2 = alloc.Allocate(4);
		seg1.AsSpan().Fill(1);
		seg2.AsSpan().Fill(2);

		_ = (items.Length, arr.Length, result.Length, seg1.Count, seg2.Count);
	}
}
