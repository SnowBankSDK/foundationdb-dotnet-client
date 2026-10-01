#### Using Aspire

It is possible to add a FoundationDB cluster resource to your Aspire application model, and pass a reference to this cluster to the projects that need it.

For local development, a local FoundationDB node will be started using the `foundationdb/foundationdb` Docker image, and all projects that use the cluster reference will have a temporary Cluster file pointing to the local instance.

Note: you will need to install Docker on your development machine, as explained in https://learn.microsoft.com/en-us/dotnet/aspire/get-started/add-aspire-existing-app#prerequisites

In the Program.cs of you AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define a locally hosted FoundationDB cluster
    var fdb = builder
        .AddFoundationDb("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterVersion: "7.2.5", rollForward: FdbVersionPolicy.Exact);

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

For testing/staging/production, or "non local" development, it is also possible to configure a FoundationDB connection resource that will pass the specified Cluster file to the projects that reference the cluster resource.

In the Program.cs of your AppHost project:
```c#
private static void Main(string[] args)
{
    var builder = DistributedApplication.CreateBuilder(args);

    // Define an external FoundationDB cluster connection
    var fdb = builder
        .AddFoundationDbCluster("fdb", apiVersion: 720, root: "/Sandbox/MySuperApp", clusterFile: "/SOME/PATH/TO/testing.cluster")		;

    // Project that needs a reference to this cluster
    var backend = builder
        .AddProject<Projects.AwesomeWebApiBackend>("backend")
        //...
        .WithReference(fdb); // register the fdb cluster connection

    // ...
}
```

#### Interactive tools in the dashboard

The local cluster resource can add commands to the Aspire dashboard that open an interactive tool in the terminal dock:

```c#
var fdb = builder
    .AddFoundationDb("fdb", apiVersion: 730, root: "/Sandbox/MySuperApp")
    .WithFdbCli()   // "FdbCli" command: fdbcli, inside the cluster container
    .WithFdbShell() // "FdbShell" command: FdbShell on the host, started in the root folder of the applications
    ;
```

- `WithFdbCli()` runs the `fdbcli` of the container image, so it always matches the version of the cluster.
- `WithFdbShell()` runs the [FdbShell](https://www.nuget.org/packages/FdbShell) .NET tool with `dotnet tool exec`, so the AppHost needs no reference to it. The first run downloads the package. The command runs the FdbShell released with this package, of the same version, so the tool and the AppHost always agree on how FdbShell finds the cluster.
  FdbShell loads the native client of the cluster's branch, the same one as the applications: the AppHost must reference the `FoundationDB.Client.Native` package of that branch (`<PackageReference Include="FoundationDB.Client.Native" />`, with the version that central package management gives to every project). The AppHost stops at startup, with this fix in the message, when the package is missing or belongs to another major.minor version than the cluster.
- In a solution that holds the source of FdbShell, `WithFdbShell<Projects.FdbShell>()` runs the project instead. It needs a `ProjectReference` from the AppHost to `FdbShell.csproj`, pinned to the framework of the AppHost (`SetTargetFramework="TargetFramework=net10.0"`). It loads the native client of the AppHost's `FoundationDB.Client.Native` reference when there is one, and its own otherwise.

Each click on a command opens a new tab in the terminal dock. Type `exit` in `fdbcli` (or `quit` in FdbShell) before closing the tab.

Then, in the Program.cs, or where you are declaring your services with the DI, use the following extension method to add support for FoundationDB:

```c#
var builder = WebApplication.CreateBuilder(args);

// setup Aspire services...
builder.AddServiceDefaults();
//...

// hookup the FoundationDB component
builder.AddFoundationDb("fdb"); // "fdb" is the same name we used in AddFoundationDb(...) or AddFoundationDbCLuster(...) in the AppHost above.

// ...rest of the startup logic....
```

This will automatically register an instance of the `IFdbDatabaseProvider` service, automatically configured to connect the FDB local or external cluster defined in the AppHost.
