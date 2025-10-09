# FoundationDB.Client.Native

This package will redistribute the native client libraries required to connect to a FoundationDB cluster from a .NET application.

## Why is it needed

The .NET FoundationDB Binding (`FoundationDB.Client`) is a managed .NET assembly that contains the APIs and other facilities to query a FoundationDB cluster from your .NET application.

But like all other bindings, it requires a native library called `FDB Client Library` (usually `lifdb_c.so` or `fdb_c.dll`) to communicate with a compatible FoundationDB cluster.

Contrary to other database systems, a native client library compiled for 7.4.x can only connect to 7.4.x servers
and will not be able to connect to older (<= 7.3.x) or newer (>= 7.5.x, 8.x, ...) live cluster.

This gives you two choices:
- Build your application to target a specific minor version of FoundationDB, such as `7.4`
  - You will need to also deploy a `7.4.x` cluster, and your binaries will not be compatible with any other versions.
  - You will have to rebuild your application if you decide to upgrade or download to a different minor or major version.
- Build your application to target a specific API level such as `730`.
  - You will be able to connect to a live cluster with version _at least_ `7.3.x`.
  - You will need to redistribute the by a side channel, either by including them manually in a DockerFile, or installing them at the last minute during deployment.

The `FoundationDB.Client.Native` package helps solve the first case, by allowing you to reference a specific version of the native libraries in your project,
and redistributes the native client library as part of the your binaries.

## What it does

Your application must have access to a build of the FoundationDB Client library ("fdb_c") at runtime, in order to connect to your FoundationDB cluster.

This package includes the `fdb_c` client library, built for the following supported platforms:
- `win-x64`
- `linux-x64`
- `linux-arm64` (aka `aarch64`)
- `osx-arm64`

The assembly redistributed in the package contains a mini loader that will locate and use the correct library at runtime.

## How to use it

This package can be used in two ways:

- Via a `PackageReference` to the `FoundationDB.Client.Native` NuGet package, if you are also referencing the `FoundationDB.Client` NuGet package.
- Via a `ProjectReference` with custom `.targets` to a local copy of `FoundationDB.Client.Native.csproj`, if you are compiling the .NET Binding as part of your application.

### Referencing in your project file

#### Via NuGet

If you are not compiling the source of FoundationDB.Client, but instead are referencing it via the NuGet package, then you should instead add a reference to the FoundationDB.Client.Native package:

```msbuild_build_script
<ItemGroup>
	<PackageReference Include"FoundationDB.Client" Version="x.y.z" /> <!-- use version compatible with the API level you need -->
	<PackageReference Include"FoundationDB.Client.Native" Version="7.4.XXX" /> <!-- use a version compatible with your production cluster -->
</ItemGroup>
```

The package will automatically copy the native libraries during build, pack or publish.

#### Via Source compilation

Let's assume that you have created a git submodule called "FoundationDB" at the root of your solution folder.

To include this package in your project, simply add the following at the end of your `.csproj` file:

```msbuild_build_script
<ItemGroup>
	<ProjectReference Include="..\Path\To\FoundationDB.Client\FoundationDB.Client.csproj" />
	<ProjectReference Include="..\Path\To\FoundationDB.Client.Native\FoundationDB.Client.Native.csproj" />
</ItemGroup>

<Import Project="..\Path\To\FoundationDB.Client.Native\build\FoundationDB.Client.Native.targets" />
```

The imported target will automatically copy the native libraries to your project's output folder.

To check that this is working properly, navigate to your `bin/debug/net##.0/` folder,
and check that it contains the expected folder structure.

For example, when building for .NET 10.0 in Debug configuration:
```
bin\debug\net10.0\
- YourApp.exe
  ...
- FoundationDB.Client.dll
- FoundationDB.Client.Native.dll
  ...
- runtimes\
  - win-x64\
    - native\
      - fdb_c.dll
  - linux-x64\
    - native\
      - libfdb_c.so
  - linux-arm64\
    - native\
      - libfdb_c.so
  - osx-arm64\
    - native\
      - libfdb_c.dylib
```

### Loading the native libraries at runtime

The packge contains a small loader that will locate the native library and set the `FdbDatabaseProviderOptions.NativeLibraryPath` property automatically.

In order for the loader to find the libraries at runtime, locate in your application startup logic the call to `AddFoundationDB(...)`,
and inject a call to `UseNativeClient()` like so:

```csharp
builder.AddFoundationDb(740, options =>
{
	// load the native library that was distributed by the 'FoundationDB.Client.Native' package
	options.UseNativeClient();

	// set any other options you require...
});
```

By default, this will probe for the location of the native libraries on disk.

This operation may fail if no compatible library is found, which could happen if the application is running on a non-supported platform,
or if the files were not properly bundled with the application (invalid name or location, not readable by the process, ...).
	
Specifying `UseNativeClient(allowSystemFallback: true)` will allow the loader to fall back
to the native resolution mechanism of your operating system, if must deploy them to a different location.

_Note: The call to UseNativeClient() MUST NOT be trimmed during publishing,
otherwise the linker may consider that the reference `FoundationDB.Client.Native` is unused,
and may not copy the native libraries to the build output folder!_

## Publishing / Packing

The package supports both Framework-Dependent vs Self-Contained, and Portable vs Platform-specific modes.

When publishing for a specific runtime, for example `linux-x64` or `win-x64`, only the files for this platform will copied to the same folder as your executable.

When publishing a portable application that could run on any platform, all native libraries for all supported platforms will be copied under the relevant `runtimes/{rid}/native` sub-folders,
which could cause a significant increase in the size of your redistributable package!

The native client loader will look - at runtime - for a file with the expected name for the current platform (`fdb_c.dll` on Windows, `libfdb_c.so` on Linux, `libfdb_c.dylib` on macOS)
by looking first in the working directory of your application, and then under the `runtimes/{rid}/native` subfolder that matches the runtime platform.

If you are building a Docker image, you should target `linux-x64` or `linux-arm64`, in order to only include the library for this specific platform, and reduce the overall size of your image.

## Alternatives

You can also redistribute the native libraries via a separate mechanism, and either let the operating system find the library, or manually specify the path via the `NativeLibraryPath` option.
