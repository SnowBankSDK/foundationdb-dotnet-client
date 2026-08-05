SnowBank SDK - JSON Source Code Generator
=========================================

> Generates code for custom JSON converters and proxies

This package contains a source code generator that will output optimized code for application types.

- __Containers__: application can define a _container type_ and a set of top-level _application types_ (records, structs, ...) that are exchanged with other processes as JSON.
- __Converters__: generates custom implements of `IJsonConverter<...>` for all the types in the dependency graph. Each converter can _serialize_, _pack_ and _unpack_ instances of these types, without relying on reflection at runtime.
- __Proxies__: generates a set of ReadOnly and Writable _proxies_, that expose an underlying JSON Object as a type-safe object with the same properties and types as the parent type, without having to deserialize all the properties.

## How to reference

Via NuGet, simply add a `PackageReference ` to any project that contains your application types.

Via a git sub-module, add a `ProjectReference` and flag it as a source analyzer.

```xml
<ProjectReference
	Include="..\_PATH_TO_MODULE_\SnowBank.Serialization.Json.CodeGen\SnowBank.Serialization.Json.CodeGen.csproj"
	OutputItemType="Analyzer"
	ReferenceOutputAssembly="false" />
```

This package will only be used at build time, and should not be redistributed with the rest of tha application.

## How to use

### Containers

In the shared assembly that contains most of your "data" types, simply create a new static class that will act as a _container_, decorate it with `[CrystalJsonConverter]`, and then add as many `[CrystalSerializable(typeof(...)]` as you have top-level data types.

The converter will walk the type dependency graph from all these types, and generate custom converters and proxies for each one. You don't need to mention any nested type or types references by properties, as they should automatically be included.

```
	[CrystalJsonConverter]
	[CrystalSerializable(typeof(User))]
	[CrystalSerializable(typeof(Order))]
	[CrystalSerializable(typeof(Product))]
	[CrystalSerializable(typeof(Role))]
	// and any other main types
	public static partial class MyAppConverters
	{

	}
```

This will generate a nested type for each of these types `T`, that will generate multiple types with the following suffix:
- `(TypeName)Converter`: implement a `IJsonConverter<T>` and provides the `Serialize(...)`, `Pack(...)` and `Unpack(...)` methods
- `(TypeName)ReadOnly`: read-only struct that implements `IJsonReadOnlyProxy<...>`
- `(TypeName)ReadOnly`: mutable class that implements `IJsonWritableProxy<...>`

### Examples

Example: how to serialize a type into a JSON literal (text, bytes, ...):
```c#
// generate the instance that you need to serialize into JSON...
User result = MethodThatReturnsUser();

// serialize into a string:
string jsonText = MyAppConverters.User.ToJson(result);

// serialize into a Slice:
Slice jsonBytes = MyAppConverters.User.ToJsonSlice(result);

// serialize into a byte[] array:
Slice jsonBytes = MyAppConverters.User.ToJsonBytes(result);
```

Example: how to pack a type into a JSON Object (DOM):
```c#
// generate the instance that you need to serialize into JSON...
User result = MethodThatReturnsUser();

JsonValue json = MyAppConverters.User.Pack(result);
Console.WriteLine($"- Id: {json["id"]}");
Console.WriteLine($"- FirstName: {json["firstName"]}");
Console.WriteLine($"- LastName: {json["lastName"]}");
```

Example: how to unpack a JSON Object back into an instance of the original type:
```c#
JsonObject obj = new();
obj["id"] = "ag007"";
obj["firstName"] = "James";
obj["lastName"] = "Bond";
// ...

Person person = MyAppConverters.User.Unpack(obj);
Console.WriteLine($"- Id: {person.Id}");
Console.WriteLine($"- FirstName: {person.FirstName}");
Console.WriteLine($"- LastName: {person.LastName}");
```

Example: how to work with a read-only proxy for a type:
```c#
JsonObject obj = new();
obj["id"] = "ag007";
obj["firstName"] = "James";
obj["lastName"] = "Bond";
// ...

var person = MyAppConverters.User.ToReadOnly(obj);
Console.WriteLine($"- Id: {person.Id}");
Console.WriteLine($"- FirstName: {person.FirstName}");
Console.WriteLine($"- LastName: {person.LastName}");
```

_Observe that the read-only proxy looks identical to the original type, and note that it will only deserialize the value of a field only when it is accessed_

Example: how to work with a writable proxy for a type:
```c#
JsonObject obj = new();
// ...

var person = MyAppConverters.User.ToWritable(obj);
person.Id = "ag007";
person.FirstName = "James";
person.LastName = "Bond";

Console.WriteLine($"- Id: {obj["id"]}");
Console.WriteLine($"- FirstName: {obj["firstName"]}");
Console.WriteLine($"- LastName: {obj["lastName"]}");
```

_Observe that the writable proxy looks identical to the original type, and note that it will only touch the properties that are updated, without changing any other fields in the existing JSON object_

Example: how to get a `ICrystalJsonTypeResolver` that will use the generated converters for all the known types in the container.

```c#
public static T Decode<T>(JsonObject obj)
{

	// this returns a resolver that will map all the application types in the container to the generated converters
	var resolver = MyAppConverters.GetResolver();

	// get a converter for a known type:
	if (!resolver.TryGetConverterFor<Person>(out var personConverter))
	{
		throw new InvalidOperationException("Type not supported");
	}

	// use the converter to deserialize a T instance
	return personConverter.Unpack(obj);
}

var obj = new JsonObject() { ["id"] = "ag007", ["firstName"] = "James", ["lastName"] = "Bond" };
var person = Decode<Person>(obj);
Console.WriteLine($"- Id: {person.Id}");
Console.WriteLine($"- FirstName: {person.FirstName}");
Console.WriteLine($"- LastName: {person.LastName}");
```
