# Tuples

> This page explains the tuple model in depth. In this library, tuples are how you encode **keys** (and sometimes values): the tuple binary encoding produces bytes whose sort order matches the logical order of the elements: exactly what FoundationDB's ordered keyspace needs. For how tuples become database keys via subspaces, see the [Keys, Values & Layers guide](guide/keys-and-layers.md).

_"A tuple is an ordered list of elements."_ - [Wikipedia](http://en.wikipedia.org/wiki/Tuple)

<pre>
         0       1                      2
    +---------+-----+--------------------------------------+
t = | "Hello" | 123 | 773166b7-de74-4fcc-845c-84080cc89533 |
    +---------+-----+--------------------------------------+
</pre>

This is a tuple of size 3, which contains 3 elements in a specific order: the first element, the second element and then - you guessed it - the third element.

The difference with a regular struct, is that the elements do not have names, only positions: `t[0]`, `t[1]`, ..., `t[i]` with `0 <= i < N`, like an array.

The difference with an array, is that all the elements can have a different types.

There are various ways to represent a tuple in plain text, and one of them is as a vector:

<pre>("Hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})</pre>

There is a special case, for the tuple of size 1, where we usually add an extra `,` at the end, to distinguish it from an expression:

<pre>("Hello", )</pre>

And of course you can have an empty tuple, of size 0:

<pre>()</pre>

### The Dark Ages

The absolute minimum implementation of a tuple is an `object[]` array. But this would not be very efficient nor user friendly, especially when you need to encode and decode keys composed of multiple elements with different types. You will probably use a lot of value types (int, Guid, bool, ...) that would need to be boxed, and you will also need to blindly casts items back into their expected type. Now was the 3rd element of this tuple an `int` or a `long`? If you guessed wrong, you will get an `InvalidCastException` at runtime. Uhoh :(

```CSharp
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

The .NET Framework comes with a set of `Tuple<...>` classes, which gives you the ability to specify the types as well as the number of elements. You get type safety and a better intellisense experience.

```CSharp
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

This is much better, but unfortunately, the BCL's Tuple classes are relatively barebone and don't offer much in term of feature, if at all. You can't really combine them or split them. They still require you to know that the 2nd element was an `int`, and not a `long` or `uint`.

And quite frankly, if you have used other languages where tuples are first-class citizens (Python, for example), they seem rather bleak.
    
That's why we need a better API, in order to help us be more productive.

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

The most simple way to create a tuple, is from its elements:

```CSharp
var t = STuple.Create("Hello", 123, Guid.NewGuid());
```

The actual type of the tuple will be `STuple<string, int, Guid>` which is a struct. Since we are using the `var` keyword, then as long as `t` stays inside the method, it will not be boxed.

We can also create a tuple by adding something to an existing tuples, even starting with the Empty tuple:

```CSharp
var t = STuple.Empty.Append("Hello").Append(123).Append(Guid.NewGuid());
```

The good news here is that _t_ is still a struct of type `STuple<string, int, Guid>` and we did not produce any allocations: the Empty tuple is a singleton, and all the intermediate Append() returned structs of type `STuple<string>` and `STuple<string, int>`. There is of course a limit to the number of elements that can be added, before we have to switch to an array-based tuple variant.

If we have a variable-size list of items, we can also create a tuple from it:

```CSharp
IEnumerable<MyFoo> xs = ....;
// xs is a sequence of MyFoo objects, with an Id property (of type Guid)
var t = STuple.FromEnumerable(xs.Select(x => x.Id));
```

When all the elements or a tuple are of the same type, you can use specialized versions:
```CSharp
var xs = new [] { "Bonjour", "le", "Monde!" };
var t = STuple.FromArray<string>(xs);
```

If you were already using the BCL's Tuple, you can easily convert from one to the other, via a set of implicit and explicit cast operators:

```CSharp
var bcl = Tuple.Create("Hello", 123, Guid.NewGuid());
STuple<string, int, Guid> t = bcl; // implicit cast

var t = STuple.Create("Hello", 123, Guid.NewGuid());
Tuple<string, int, Guid> bcl = (Tuple<string, int, Guid>) t; // explicit cast
```

And for the more adventurous, you can of course create a tuple by copying the elements of an object[] array.

```CSharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.FromObjects(xs); // => ("hello", 123, guid)
var t2 = STuple.FromObjects(xs, 1, 2); // => (123, guid)
xs[1] = 456; // won't change the content of the tuples
// t[1] => 123
```

If you really want to push it, you can skip copying the items by wrapping an existing array, but then you will break the immutability contract of the Tuples API. Don't try this at home!

```CSharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.Wrap(xs); // no copy!
var t2 = STuple.Wrap(xs, 1, 2); // no copy!
xs[1] = 456; // will change the content of the tuples!!
// t[1] => 456
```

### Using a tuple

Now that you have a tuple, the first thing you would wan't to know, is its size and if it is empty or not.

All tuples expose a `Count` property which returns the number of elements in the tuple (0 to N).
    
To help you verify that a tuple has the correct size before accessing its elements, there is a set of help extension methods just for that:

- `t.IsNullOrEmpty()` returns `true` if either `t == null` or `t.Count == 0`
- `t.OfSize(3)` checks that `t` is not null, and that `t.Count` is equal to 3, and then returns the tuple itself, so you can write: `t.OfSize(3).DoSomethingWichExceptsThreeElements()`
- `t.OfSizeAtLeast(3)` works the same, except it checks that `t.Count >= 3`

Of course, if you have one of the `STuple<T1, ...>` struct, you can skip this step, since the size if known at compile time.

To read the content of a tuple, you can simply call `t.Get<T>(index)`, where `index` is the offset _in the tuple_ of the element, and `T` is the type into which the value will be converted.

```CSharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(0); // => "hello"
var y = t.Get<int>(1); // => 123
var z = t.Get<Guid>(2); // => guid
```

If `index` is negative, then it is relative to the end of the tuple, where -1 is the last element, -2 is the next-to-last element, and -N is the first element.

```CSharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(-3); // => "hello"
var y = t.Get<int>(-2); // => 123
var z = t.Get<Guid>(-1); // => guid
```

### Pretty Printing

Code that manipulate tuples can get complex pretty fast, so you need a way to display the content of a tuple is a nice and understable way.

For that, every tuple overrides `ToString()` to return a nicely formatted string with a standardized format.

```CSharp
var t1 = STuple.Create("hello", 123, Guid.NewGuid());
Console.WriteLine("t1 = {0}", t1);
// => t1 = ("hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create("hello");
Console.WriteLine("t1 = {0}", t2);
// => t2 = ("hello",)
var t3 = STuple.Empty;
Console.WriteLine("t3 = {0}", t3);
// => t3 = ()
```

There is a special case for tuples of size 1, which have a trailing comma - `(123,)` instead of `(123)` - so that they can be distinguished from a normal expression in parenthesis.

### Tuples all the way down

Since a tuple is just a vector of elements, you can of course put a tuple inside another tuple.

This works:

```CSharp
var t1 = STuple.Create("hello", STuple.Create(123, 456), Guid.NewGuid());
// t1 = ("hello", (123, 456), {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create(STuple.Create("a", "b"));
// t2 = ((a, b),)
var t3 = STuple.Create("hello", STuple.Empty, "world");
// t3 = ("hello", (), "world");
```

_note: The easy mistake is to call `t1.Append(t2)` instead of `t1.Concat(t2)`, which will add t2 as a single element at the end of t1, instead of adding t2's elements ad the end of t1._

This can be usefull when you want to model a fixed-size key: `(product_id, location_id, order_id)` where location_id is a hierarchical key with a variable size, but still keep a fixed size of 3:

```CSharp
var productId = "B00CS8QSSK";
var locationId = new [] { "Europe", "France", "Lille" };
var orderId = Guid.NewGuid();

var t = STuple.Create(productId, STuple.FromArray(locationId), orderId);
// t.Count => 3
// t[0] => "B00CS8QSSK"
// t[1] => ("Europe", "France", "Lille")
// t[2] => {773166b7-de74-4fcc-845c-84080cc89533}
```

You code that want to parse the key can always read `t[2]` to get the order_id, without caring about the actuel size of the location_id.

### Combining tuples

Since tuples are immutable, there are no methods to modify the value of an element. You'd do that by creating a new tuple, with a combination of Substring, Append or Concat.

You can, though, modify tuples by returning a new tuple, with or without copying the items (depending on the tuple variant being used).

The most common case is to simply add a value to a tuple via the `t.Append<T>(T value)` method. For example you have a base tuple (cached value), and you want to add a document ID.

```CSharp
var location = STuple.Create("MyAwesomeApp", "Documents");

var documentId = Guid.NewGuid();
var t = location.Append(document);
// t => ("MyAwesomeApp", "Documents", {773166b7-de74-4fcc-845c-84080cc89533});
```

Don't forget that if you Append a tuple, it will be added as a nested tuple!

If you actually want to merge the elements of two tuples, when you can use the `t1.Concat(t2)` method, which return a new tuple with the elements of both t1 and t2.

```CSharp
var location = STuple.Create("MyAwesomeApp", "OrdersByProduct");

var productId = "B00CS8QSSK";
var orderId = Guid.NewGuid();
var t1 = STuple.Create(productId, orderId)
// t1 => ("B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533})

var t2 = location.Concat(t1);
// t2 => ("MyAwesomeApp", "OrdersByProduct", "B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533});
```

### Splitting tuples

You can also split tuples into smaller chunks.

First, you can return a subset of a tuple via on of the `t.Substring(...)` methods, or the `t[from, to]` indexer.
    
The `Substring()` method works exactly the same way as for regulard strings.

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Substring(0, 3); // => (1, 2, 3)
var v = t.Substring(5, 2); // => (6, 7)
var w = t.Substring(7); // => (8, 9, 10)

// also works with negative indexing!
var w = v.Substring(-3); // => (8, 9, 10)
```

The `t[from, to]` indexer gets some getting used to. If actual returns all the elements in the tuple with position `from <= p < to`, which means that the `to` is excluded.

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t[0, 3]; // => (1, 2, 3)
var v = t[5, 7]; // => (6, 7)
// rember that 'to' is excluded!
var w = t[7, -1]; // => (8, 9)
// to fix that, you can use 'null' ("up to the end")
var w = t[7, null]; // => (8, 9, 10)

// also works with negative indexing!
var w = v[-3, null]; // => (8, 9, 10)
```

If you are tired of writing `t.Substring(0, 3)` all the time, you can also use `t.Truncate(3)` which does the same thing.

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Truncate(3);
// u => (1, 2, 3);
var v = t.Truncate(-3);
// v => (8, 9, 10);
```

### More advanced stuff

When decoding keys using tuple, you wil often find yourself extracting a fixed number of arguments into local variables, and then constructing an instance of a Model class from your application.

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var x = tuple.Get<string>(0);
    var y = tuple.Get<int>(1);
    var z = tuple.Get<Guid>(2);
    return new MyFooBar(x, y, z);
}
```

The keen eye will see the problems with this method:

- no null check on tuple.
- what if tuple.Count is 5 ?
- what if tuple.Count is only 2 ?
- you probably copy/pasted `var x = tuple.Get<...>(0)` two more times, and forgot to change the index to 1 and 2! _(even Notch does it!)_

One solution is to use the set of `t.As<T1, ..., TN>()` helper methods to convert a tuple of type `IVarTuple` into a more friendly `STuple<T1, ..., TN>`, introducing type safety and IntelliSense.

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var t = tuple.As<string, int, Guid>();
    // this throws if tuple is null, or not of size 3
    return new MyFooBar(t.Item1, t.Item2, t.Item3);
}
```

That's better, but you can still swap two arguments by mistake, if they have the same type.

To combat this, you can use on of the `t.With<T1, ..., TN>(Action<T1, ..., TN>)` or `t.With<T1, ..., TN, TResult>(Func<T1, ..., TN, TResult>)` which can give names to the elements.

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    return tuple.With((Guid productId, Guid categoryId, Guid orderId) => new MyFooBar(productId, categoriyId, orderId));
    // all three elements are GUID, but adding name help you catch argument inversion errors
}
```

