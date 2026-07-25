# FoundationDB.FdbLite

Persistent single-process storage backend for the FoundationDB emulator (`FoundationDB.FakeDb`):
a memory-mapped, copy-on-write B+tree that implements the emulator's committed-store seam
(`ICommittedStore` / `ICommittedCursor`), giving the emulator a second, durable mode next to its
in-memory test mode.

- **Test mode** (`FakeDbStore`): in-memory, full version history, speed first.
- **Persistent mode** (this project): memory-mapped B+tree file, larger datasets, bounded version
  window, crash-safe commits.

The engine is layered:

- `Storage/` - the page-level engine: page format, pagers (heap and memory-mapped), free-space
  tracking, the copy-on-write B+tree, and the double-header commit protocol.
- the committed-store adapter and the database handler pair that plug the engine behind the
  emulator's transaction machinery.

Requires .NET 10 or later.
