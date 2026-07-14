using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register FoundationDB in the DI container. 740 is the API level: keep it at or
// below your cluster's version.
services.AddFoundationDb(740, options =>
{
    // The coordinators to connect to (the connection string from Cluster setup).
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

using var provider = services.BuildServiceProvider();
var db = provider.GetRequiredService<IFdbDatabaseProvider>();

// GetReadVersionAsync is a cheap round-trip: a value here means the connection works.
long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), CancellationToken.None);
Console.WriteLine($"Connected. Cluster read version = {readVersion}");

var location = db.Root["Examples"]["Hello"];

await db.WriteAsync(async tr =>
{
    // CreateOrOpen creates the directory the first time (Resolve only opens an existing one).
    var subspace = await location.CreateOrOpenAsync(tr);
    tr.Set(subspace.Key("greeting"), FdbValue.ToTextUtf8("Hello, World!"));
}, CancellationToken.None);

string? greeting = await db.ReadAsync(async tr =>
{
    // resolve this location to its key subspace
    var subspace = await location.Resolve(tr);
    var value = await tr.GetAsync(subspace.Key("greeting"));
    return value.ToStringUtf8();
}, CancellationToken.None);

Console.WriteLine($"Read back: {greeting}");
