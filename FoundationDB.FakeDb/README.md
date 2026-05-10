FoundationDB.FakeDb
===================

Library for emulating an "in-process" FoundationDB cluster, suitable for testing or quick prototyping.

# Concept

This library contains a very managed implementation of the FoundationDB Client API, using an in-memory store.

The `FakeDbStore` is used to store all the data, and has the low-level implementation of the various API methods.

The `FakeDbProvider` will expose a standard `IFdbDatabase` implementation that will use this store, instead of calling the native C client.

The store supports concurrent accesses, meaning that any number of threads can interact with the same store, in the same way as multiple processes would with an actual cluster.

You can create as many stores as you want in the same process, allowing parallel execution of test methods, each with their own independent "cluster".

# How to use

**Disclaimer:** this emulator _cannot_ replicate 1 to 1 the behavior of a live cluster, and should only be used to test application logic.
You should always test against an actual cluster before concluding anything!

When using dependency injection, simply call `AddFakeDb(...)` instead of `AddFoundationDb(...)` on your application builder. This should automatically inject the handler that will call the emulator, instead of the native C client.

```c#
var builder = WebApplication.CreateBuilder(args);

// Register the FoundationDB emulator
builder.Services.AddFakeDb(730);

var app = builder.Build();
```

When emulating different processes, each with its own `IServiceProvider`, they need to share the same underlying store, otherwise they would each use a different cluster.

The easiest way is to manually create a `FakeDbStore` singleton, and inject the same instance into each provider.

```c#
public async Task Test_Multiple_Processes()
{
  // create the shared store instance
  var store = new FakeDbStore(730);

  // configure the first process
  var builder1 = WebApplication.CreateBuilder(args);
  builder1.Services.AddFakeDb(730, store);
  // add more services...

  // configure the second process
  var builder2 = WebApplication.CreateBuilder(args);
  builder2.Services.AddFakeDb(730, store);
  // add more services...

  // both builders will share the same store and will be able to interact with each other.
  // you can also inspect the content of the cluster from the test method using the 'store' variable.
  // ...

}
```

You can also introspect the content of the `FakeDbStore` by using the `FakeDbDebugger` helper type.

For example, here are two helper methods that format the content of a store into a textual representation:
```c#
public static void DumpStore(FakeDbStore store, string label)
{
	DumpStore(store.CurrentSnapshotUnsafe, label);
}

public static string DumpStore(FakeDbStore.Snapshot snapshot, string label)
{
	var sb = new StringBuilder();
	sb.AppendLine($"### {label}");
	sb.AppendLine($"* Version: {snapshot.Version:X}");

	var data = FakeDbDebugger.GetSnapshotData(snapshot);
	sb.AppendLine($"* Keys: {data.Count:N0}");
	foreach (var x in data)
	{
		sb.AppendLine($"| - {x.Key:K} = {x.Value:P}");
	}

	var conflicts = FakeDbDebugger.GetSnapshotConflictRanges(snapshot);
	sb.AppendLine($"* Ranges: {conflicts.Count:N0}");
	foreach (var x in conflicts)
	{
		sb.AppendLine($"| - {x.Begin:K}..{x.End:K}: {x.Value:N0}");
	}
	return sb.ToString();
}
```

It is also possible to capture immutable "snapshots" of the store at any time, compare them, or even dump them to disk.

```c#
// get the initial snapshot
FakeDbStore.Snapshot before = store.CurrentSnapshotUnsafe;
Assert.That(before.ContainsKey(/*...*/), Is.False);
Assert.That(before.ContainsKey(/*...*/), Is.False);

// Perform some operation that will mutate the database...
await DoSomething(/*...*/);

// get the updated snapshot
FakeDbStore.Snapshot after = store.CurrentSnapshotUnsafe;

// you can inspect the list of changes between two snapshots
foreach(var delta in after.Diff(before))
{
	Log($"- {delta.Key:K} : {delta.Before:V} => {delta.After:V}");
}

// you can also  check specific key/value pairs, or use a "diff" method between two snapshots
Assert.That(after.ContainsKey(/*...*/), Is.True);
Assert.That(after.Read(/*...*/), Is.EqualTo(/*...*/));
```
