# Tuples

> This page explains the tuple model in depth. In this library, tuples are how you encode **keys** (and sometimes values): the tuple binary encoding produces bytes whose sort order matches the logical order of the elements: exactly what FoundationDB's ordered keyspace needs. For how tuples become database keys via subspaces, see the [Keys, Values & Layers guide](../fdb/guide/keys-and-layers/index.md).

_"A tuple is an ordered list of elements."_ - [Wikipedia](https://en.wikipedia.org/wiki/Tuple)

<pre>
         0       1                      2
    +---------+-----+--------------------------------------+
t = | "Hello" | 123 | 773166b7-de74-4fcc-845c-84080cc89533 |
    +---------+-----+--------------------------------------+
</pre>

This tuple has size 3: three elements in a fixed order, at positions 0, 1 and 2.

The difference with a regular struct, is that the elements do not have names, only positions: `t[0]`, `t[1]`, ..., `t[i]` with `0 <= i < N`, like an array.

The difference with an array, is that all the elements can have a different types.

There are various ways to represent a tuple in plain text, and one of them is as a vector:

<pre>("Hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})</pre>

That text form is for humans. On disk the tuple is a compact binary string, and its bytes sort in the same order as the elements. Each item opens with a one-byte **type marker**, then its value:

```fdb-bytes
tuple: ("Hello", 123, {guid})
str   .02 'Hello' .00      # string "Hello"
int   .15 7B               # integer 123
uuid  .30 <uuid:16>        # 128-bit UUID
```

The markers are what make the ordering work: they sort the types into a fixed order, and the value bytes order values within a type. A long or opaque item like a UUID is shown collapsed, rather than as sixteen raw bytes.

There is a special case, for the tuple of size 1, where we usually add an extra `,` at the end, to distinguish it from an expression:

<pre>("Hello", )</pre>

The empty tuple has size 0:

<pre>()</pre>

### Why not `object[]` or `Tuple<...>`

The minimum implementation of a tuple is an `object[]` array. It is neither efficient nor safe for keys built from elements of different types: every value type (int, Guid, bool, ...) gets boxed, and reading an element back is a blind cast. Was the 3rd element an `int` or a `long`? A wrong guess is an `InvalidCastException` at runtime.

```csharp
// in application A that encoded a key...
var items = new object[] { "Hello", 123, Guid.NewGuid() };
// one allocation for the object[] array, and two allocations to box the int and the guid!
var key = SomeLibrary.Encode(items);

// in a different application B that decodes the same key
var items = SomeLibrary.Decode(key);
var a = (string)items[0];
var b = (long)items[1]; // FAIL: it's actually an int !
var c = (Guid)items[2];
var d = (int)items[3]; // FAIL: there is no 4th item !
```

The BCL `Tuple<...>` classes state the types and the number of elements, which restores type safety and IntelliSense.

```csharp
// in application A that encoded a key...
Tuple<string, int, Guid> items = Tuple.Create("Hello", 123, Guid.NewGuid());
// a single allocation for the Tuple instance
var key = SomeLibrary.Encode(items);

// in a different application B that decodes the same key
Tuple<string, int, Guid> items = SomeLibrary.Decode<string, int, Guid>(key);
string a = items.Item1;
int b = items.Item2;
Guid c = items.Item3;
```

The BCL classes stop there: you cannot combine them or split them, and you still have to know that the 2nd element was an `int`, not a `long` or a `uint`. Key encoding needs a richer tuple API, which is what this library provides.

## IVarTuple

The `IVarTuple` interface, defined in `SnowBank.Data.Tuples`, is the base of all the different tuple implementations, each targeting a specific use case.

This interface has the bare-minimum API that every variant must implement, and is in turn used by a set of extension methods that add more generic behavior without needing to be reimplemented in each variant.

There is also a static class, `STuple`, which holds methods to create and manipulate all the variants.

_note: the interface is called `IVarTuple` (not `ITuple`) because the BCL already defines an `ITuple`, and we couldn't name our static helper `Tuple` without colliding with the BCL's `Tuple` class. `IVarTuple` does implement the BCL `System.Runtime.CompilerServices.ITuple` for interop._

### Types of tuples

Tuples adapt to different use cases: some have a fixed size and types (like the BCL tuples), some are variable-length (like a vector). Some should be structs (to avoid allocations in tight loops), others reference types. And some are thin wrappers around an encoded binary blob that defer decoding until the elements are accessed.

That's why there are several variants, all implementing `IVarTuple`:

- `STuple<T1>` … `STuple<T1, …, T8>` are the equivalent of the BCL's `Tuple<…>`, but implemented as **structs** (up to 8 elements). They're efficient as a temporary step when building larger tuples, and ideal when you want type safety and good IntelliSense, since the element types are known at compile time.
- `ListTuple` wraps an `object[]` and exposes a subset of it; taking a sub-range is cheap because it doesn't copy the items.
- `JoinedTuple` glues two tuples together (of any type); `LinkedTuple` is the special case of appending a single value to an existing tuple.
- Plus internal variants for parsed and cached representations: for example, ones that lazily decode only the elements you actually access, or that cache the binary encoding of a frequently-reused prefix.

### Creating a tuple

The simplest way to create a tuple is from its elements:

```csharp
var t = STuple.Create("Hello", 123, Guid.NewGuid());
```

The actual type of the tuple is `STuple<string, int, Guid>` which is a struct. Since we are using the `var` keyword, then as long as `t` stays inside the method, it is not boxed.

We can also create a tuple by adding something to an existing tuple, even starting with the Empty tuple:

```csharp
var t = STuple.Empty.Append("Hello").Append(123).Append(Guid.NewGuid());
```

Here _t_ is still a struct of type `STuple<string, int, Guid>`, and nothing allocated: the Empty tuple is a singleton, and the intermediate `Append()` calls returned structs of type `STuple<string>` and `STuple<string, int>`. Past 8 elements, the chain switches to an array-based variant.

If we have a variable-size list of items, we can also create a tuple from it:

```csharp
IEnumerable<MyFoo> xs = ....;
// xs is a sequence of MyFoo objects, with an Id property (of type Guid)
var t = STuple.FromEnumerable(xs.Select(x => x.Id));
```

When all the elements of a tuple are of the same type, you can use specialized versions:
```csharp
var xs = new [] { "Bonjour", "le", "Monde!" };
var t = STuple.FromArray<string>(xs);
```

If you were already using the BCL's Tuple, you can easily convert from one to the other, via a set of implicit and explicit cast operators:

```csharp
var bcl = Tuple.Create("Hello", 123, Guid.NewGuid());
STuple<string, int, Guid> t = bcl; // implicit cast

var t = STuple.Create("Hello", 123, Guid.NewGuid());
Tuple<string, int, Guid> bcl = (Tuple<string, int, Guid>) t; // explicit cast
```

You can also create a tuple by copying the elements of an `object[]` array:

```csharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.FromObjects(xs); // => ("hello", 123, guid)
var t2 = STuple.FromObjects(xs, 1, 2); // => (123, guid)
xs[1] = 456; // won't change the content of the tuples
// t[1] => 123
```

`STuple.Wrap` skips the copy by wrapping the array itself. This breaks the immutability contract of the tuple API: a later write to the array changes the tuple. Use it only when you control the array for its whole lifetime.

```csharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.Wrap(xs); // no copy!
var t2 = STuple.Wrap(xs, 1, 2); // no copy!
xs[1] = 456; // will change the content of the tuples!!
// t[1] => 456
```

### Using a tuple

The first thing to check on a tuple is its size. Every tuple exposes a `Count` property with the number of elements (0 to N), and a set of helper extension methods verifies the size before you access the elements:

- `t.IsNullOrEmpty()` returns `true` if either `t == null` or `t.Count == 0`
- `t.OfSize(3)` checks that `t` is not null, and that `t.Count` is equal to 3, and then returns the tuple itself, so you can write: `t.OfSize(3).DoSomethingWhichExpectsThreeElements()`
- `t.OfSizeAtLeast(3)` works the same, except it checks that `t.Count >= 3`

With an `STuple<T1, ...>` struct you can skip this step, since the size is known at compile time.

To read the content of a tuple, call `t.Get<T>(index)`, where `index` is the offset _in the tuple_ of the element, and `T` is the type into which the value converts.

```csharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(0); // => "hello"
var y = t.Get<int>(1); // => 123
var z = t.Get<Guid>(2); // => guid
```

If `index` is negative, then it is relative to the end of the tuple, where -1 is the last element, -2 is the next-to-last element, and -N is the first element.

```csharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(-3); // => "hello"
var y = t.Get<int>(-2); // => 123
var z = t.Get<Guid>(-1); // => guid
```

### Text output

Every tuple overrides `ToString()` and renders its content in one standardized format:

```csharp
var t1 = STuple.Create("hello", 123, Guid.NewGuid());
Console.WriteLine("t1 = {0}", t1);
// => t1 = ("hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create("hello");
Console.WriteLine("t2 = {0}", t2);
// => t2 = ("hello",)
var t3 = STuple.Empty;
Console.WriteLine("t3 = {0}", t3);
// => t3 = ()
```

A tuple of size 1 renders with a trailing comma (`(123,)` instead of `(123)`), which distinguishes it from an expression in parentheses.

### Nested tuples

A tuple is a vector of elements, so a tuple can contain another tuple:

```csharp
var t1 = STuple.Create("hello", STuple.Create(123, 456), Guid.NewGuid());
// t1 = ("hello", (123, 456), {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create(STuple.Create("a", "b"));
// t2 = ((a, b),)
var t3 = STuple.Create("hello", STuple.Empty, "world");
// t3 = ("hello", (), "world");
```

_note: The easy mistake is to call `t1.Append(t2)` instead of `t1.Concat(t2)`, which adds t2 as a single element at the end of t1, instead of adding t2's elements at the end of t1._

This can be useful when you want to model a fixed-size key: `(product_id, location_id, order_id)` where location_id is a hierarchical key with a variable size, but still keep a fixed size of 3:

```csharp
var productId = "B00CS8QSSK";
var locationId = new [] { "Europe", "France", "Lille" };
var orderId = Guid.NewGuid();

var t = STuple.Create(productId, STuple.FromArray(locationId), orderId);
// t.Count => 3
// t[0] => "B00CS8QSSK"
// t[1] => ("Europe", "France", "Lille")
// t[2] => {773166b7-de74-4fcc-845c-84080cc89533}
```

Code that parses the key can always read `t[2]` to get the order_id, whatever the size of the location_id.

### Combining tuples

Tuples are immutable: no method modifies an element in place. Instead, `Substring`, `Append` and `Concat` return a new tuple, with or without copying the items (depending on the variant).

The most common case adds one value to a tuple with `t.Append<T>(T value)`: for example, a cached base tuple plus a document id.

```csharp
var location = STuple.Create("Acme", "Documents");

var documentId = Guid.NewGuid();
var t = location.Append(documentId);
// t => ("Acme", "Documents", {773166b7-de74-4fcc-845c-84080cc89533});
```

Remember that `Append` with a tuple argument adds it as one nested element. To merge the elements of two tuples, use `t1.Concat(t2)`, which returns a new tuple with the elements of both:

```csharp
var location = STuple.Create("Acme", "OrdersByProduct");

var productId = "B00CS8QSSK";
var orderId = Guid.NewGuid();
var t1 = STuple.Create(productId, orderId)
// t1 => ("B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533})

var t2 = location.Concat(t1);
// t2 => ("Acme", "OrdersByProduct", "B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533});
```

### Splitting tuples

A subset of a tuple comes from one of the `t.Substring(...)` methods, or from the `t[from, to]` indexer.

`Substring()` works the same way as on a string:

```csharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Substring(0, 3); // => (1, 2, 3)
var v = t.Substring(5, 2); // => (6, 7)
var w = t.Substring(7); // => (8, 9, 10)

// also works with negative indexing!
var w = v.Substring(-3); // => (8, 9, 10)
```

The `t[from, to]` indexer returns the elements at positions `from <= p < to`: the `to` bound is excluded.

```csharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t[0, 3]; // => (1, 2, 3)
var v = t[5, 7]; // => (6, 7)
// remember that 'to' is excluded!
var w = t[7, -1]; // => (8, 9)
// to fix that, you can use 'null' ("up to the end")
var w = t[7, null]; // => (8, 9, 10)

// also works with negative indexing!
var w = v[-3, null]; // => (8, 9, 10)
```

`t.Truncate(3)` is shorthand for `t.Substring(0, 3)`:

```csharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Truncate(3);
// u => (1, 2, 3);
var v = t.Truncate(-3);
// v => (8, 9, 10);
```

### Decoding into model types

Code that decodes keys often extracts a fixed number of elements into local variables, then constructs an instance of an application model class:

```csharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var x = tuple.Get<string>(0);
    var y = tuple.Get<int>(1);
    var z = tuple.Get<Guid>(2);
    return new MyFooBar(x, y, z);
}
```

This method has problems:

- no null check on `tuple`;
- no check that `tuple.Count` is exactly 3;
- a copy/pasted `tuple.Get<...>(0)` line whose index was never changed to 1 or 2 compiles fine and reads the wrong element.

The `t.As<T1, ..., TN>()` helpers convert an `IVarTuple` into an `STuple<T1, ..., TN>`, which restores the size check, type safety and IntelliSense:

```csharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var t = tuple.As<string, int, Guid>();
    // this throws if tuple is null, or not of size 3
    return new MyFooBar(t.Item1, t.Item2, t.Item3);
}
```

Two elements of the same type can still be swapped by mistake. The `t.With<T1, ..., TN>(Action<T1, ..., TN>)` and `t.With<T1, ..., TN, TResult>(Func<T1, ..., TN, TResult>)` overloads give the elements names:

```csharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    return tuple.With((Guid productId, Guid categoryId, Guid orderId) => new MyFooBar(productId, categoryId, orderId));
    // all three elements are GUID, but adding names helps catch argument inversions
}
```
