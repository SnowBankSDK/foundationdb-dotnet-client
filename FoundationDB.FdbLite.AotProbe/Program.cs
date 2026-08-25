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

// Trim/AoT probe for the FdbLite persistent store. It roots the FdbLite public surface so the
// trimmer analyzes the reachable closure: open an in-memory store and a file-backed store, run a
// write transaction with tuple keys, read a key back, run a range read, clear a range. It also
// round-trips a composite tuple key, the tuple key encoding on the store path.
//
// This is cluster-free and native-client-free (FdbLite is pure managed), so it runs to completion
// under a self-contained or trimmed publish. It exists to surface trim warnings, not to benchmark.

using System.Buffers.Binary;
using FoundationDB.Client;
using FoundationDB.FdbLite;
using SnowBank.Data.Tuples;

// tuple key round-trip: STuple.Create(...).Append(...) builds a JoinedTuple, TuPack packs and
// unpacks it, Get<T> decodes each slot. This is the reflective tuple codec on the store path.
IVarTuple composite = STuple.Create("cat").Append("SKU-1", 42);
var packed = TuPack.Pack(composite);
var back = TuPack.Unpack(packed);
var tupleOk = back.Get<string>(0) == "cat" && back.Get<string>(1) == "SKU-1" && back.Get<int>(2) == 42;
Console.WriteLine($"[probe] tuple round-trip {(tupleOk ? "OK" : "FAILED")}: {composite} -> {packed.Count} bytes -> {back}");

// in-memory store: roots FdbLiteStore.CreateInMemory, the heap pager, the engine, the committed
// store and cursor, and the whole FoundationDB.Client transaction machinery over the seam.
using (var store = FdbLiteStore.CreateInMemory(FdbLiteGeometry.Default))
{
	await DriveAsync(store, "in-memory", CancellationToken.None);
}

// file-backed store: roots FdbLiteStore.OpenOrCreateFile and the memory-mapped pager path.
var filePath = Path.Combine(Path.GetTempPath(), $"fdblite-aotprobe-{Environment.ProcessId}.fdblite");
try
{
	using (var store = FdbLiteStore.OpenOrCreateFile(filePath, FdbLiteGeometry.Default))
	{
		await DriveAsync(store, "file-backed", CancellationToken.None);
	}
}
finally
{
	try { File.Delete(filePath); } catch { /* best effort cleanup */ }
}

return tupleOk ? 0 : 1;

static async Task DriveAsync(FdbLiteStore store, string label, CancellationToken ct)
{
	using var db = store.OpenDatabase(null, readOnly: false);

	var prefix = TuPack.EncodeKey("items");

	// write 16 tuple keys ("items", "SKU", i) -> little-endian int
	await db.WriteAsync(tr =>
	{
		for (int i = 0; i < 16; i++)
		{
			Span<byte> val = stackalloc byte[4];
			BinaryPrimitives.WriteInt32LittleEndian(val, i * 10);
			tr.Set(TuPack.EncodeKey("items", "SKU", i), Slice.FromBytes(val));
		}
	}, ct);

	// point read of one key
	var one = await db.ReadAsync(tr => tr.GetAsync(TuPack.EncodeKey("items", "SKU", 7)), ct);
	var readOne = one.Count == 4 ? BinaryPrimitives.ReadInt32LittleEndian(one.Span) : -1;

	// range read over the whole "items" prefix: roots FdbLiteCommittedStore.VisitRange / Scan and the cursor
	var range = await db.ReadAsync(tr => tr.GetRange(KeyRange.PrefixedBy(prefix)).ToListAsync(), ct);

	// clear the first half, then confirm the survivor count
	await db.WriteAsync(tr => tr.ClearRange(TuPack.EncodeKey("items", "SKU", 0), TuPack.EncodeKey("items", "SKU", 8)), ct);
	var after = await db.ReadAsync(tr => tr.GetRange(KeyRange.PrefixedBy(prefix)).ToListAsync(), ct);

	Console.WriteLine($"[probe] {label}: wrote 16, read SKU/7 = {readOne}, range = {range.Count}, after clear = {after.Count}");
}
