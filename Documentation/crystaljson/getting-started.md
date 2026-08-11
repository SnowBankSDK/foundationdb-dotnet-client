# Getting started with CrystalJson

This page follows one book through CrystalJson end to end: serialize it, read it back, parse it as a
document, navigate it safely, edit a copy, and freeze it. Ten minutes, one example. When you are
done, [Working with CrystalJson](serializing.md) has the task-by-task guides and the
[reference](reference.md) has the attribute and settings tables. For the design behind the DOM
(Document Object Model), the proxies and the two serialization paths, read
[the explanation](index.md).

Every example uses one using:

```csharp
using SnowBank.Data.Json;
```

## The type

Plain records, no attributes, nothing to set up:

```csharp
public sealed record Book
{
    public required string Id { get; init; }
    public required string Isbn { get; init; }
    public required string Title { get; init; }
    public required string[] Authors { get; init; }
    public int Year { get; init; }
    public Publisher? Publisher { get; init; }   // optional: a document may omit it
}

public sealed record Publisher
{
    public required string Name { get; init; }
    public string? City { get; init; }
}

var book = new Book
{
    Id = "B123",
    Isbn = "978-0441013593",
    Title = "Dune",
    Authors = ["Frank Herbert"],
    Year = 1965,
    Publisher = new Publisher { Name = "Chilton Books", City = "Philadelphia" },
};
```

## Serialize it

`CrystalJson.Serialize` turns the value into a JSON string. No converter and no registration are
needed; the reflection path reads the type at run time. The default output is a single readable
line, with a space after each colon and comma:

```csharp
string json = CrystalJson.Serialize(book);
// => { "Id": "B123", "Isbn": "978-0441013593", "Title": "Dune", "Authors": [ "Frank Herbert" ], "Year": 1965, "Publisher": { "Name": "Chilton Books", "City": "Philadelphia" } }
```

Pass a `CrystalJsonSettings` as the second argument to change the output. Two presets you reach for
early are `JsonCompact`, which drops every space for storage or transport, and `JsonIndented`, which
breaks the document across lines for a human to read:

```csharp
string compact = CrystalJson.Serialize(book, CrystalJsonSettings.JsonCompact);
// => {"Id":"B123","Isbn":"978-0441013593","Title":"Dune","Authors":["Frank Herbert"],"Year":1965,"Publisher":{"Name":"Chilton Books","City":"Philadelphia"}}

string indented = CrystalJson.Serialize(book, CrystalJsonSettings.JsonIndented);
```

The indented form reads:

```json
{
	"Id": "B123",
	"Isbn": "978-0441013593",
	"Title": "Dune",
	"Authors": [
		"Frank Herbert"
	],
	"Year": 1965,
	"Publisher": {
		"Name": "Chilton Books",
		"City": "Philadelphia"
	}
}
```

`CrystalJsonSettings` carries every output and parsing option, and the modifiers compose (for example
`CrystalJsonSettings.JsonIndented.WithEnumAsNumbers()`). The [reference](reference.md#settings) lists
the presets and the common modifiers.

The same value can be written to outputs other than a string. Pick the one the caller needs:

```csharp
using System.Buffers;

byte[] bytes = CrystalJson.ToBytes(book);           // a fresh byte[]
CrystalJson.SerializeTo(stream, book);              // straight into a Stream, no array in between

Slice slice = CrystalJson.ToSlice(book);            // UTF-8 bytes as a Slice
using SliceOwner owner = CrystalJson.ToSlice(book, ArrayPool<byte>.Shared);   // pooled, returned on Dispose
```

`Slice` is this SDK's view over a byte range, and it is what the database layers store and transmit,
so a value serialized straight to a `Slice` skips the `byte[]` copy those layers would otherwise make.
For a hot path, `ToSlice` with an `ArrayPool<byte>` returns a `SliceOwner` that rents its buffer and
gives it back when you dispose it, which allocates the least. `Slice`, `SliceOwner` and the pooled
buffers have their own guide, [Binary Data (Slice and Buffers)](../guide/slices-and-buffers.md).

## Read it back

`CrystalJson.Deserialize` binds the JSON back to a `Book`. The plain form throws when the input is
null or empty; pass a default to get a value instead of an exception:

```csharp
Book back = CrystalJson.Deserialize<Book>(json);            // throws on null or empty input
Book? maybe = CrystalJson.Deserialize<Book>(json, defaultValue: null);   // null instead
```

## Parse it as a document

When you want to read a field without binding the whole type, parse into the DOM and navigate it.
`JsonObject.Parse` gives you a tree; `Get<T>` reads one member:

```csharp
JsonObject doc = JsonObject.Parse(json);

string title = doc.Get<string>("Title");    // "Dune", required: throws if the field is absent
```

For a field that may be absent, read it as a nullable type. `Get<int?>` returns null when the member
is missing, which is clearer than inventing a sentinel like `0` or `-1` that a real year could take:

```csharp
int? year = doc.Get<int?>("Year", null);     // 1965, or null if the field is absent
```

## Navigate safely

Navigation never throws on a missing member. A path through an absent field returns a null value that
the next read accepts, so you check once at the end instead of at every step:

```csharp
JsonValue nowhere = doc["does"]["not"]["exist"];   // no exception; returns a Missing value
bool present = !nowhere.IsNullOrMissing();          // false
```

`Missing` is one of three special `JsonNull` values: an absent member, an explicit `null` in the
document, or an invalid access. They all read as null; [Working with CrystalJson](serializing.md)
shows when the difference matters.

This is the DOM's safety net, and it hides three mistakes that each read as "the field is just
absent." Know them before they cost you an afternoon.

**Field names are case-sensitive by default.** `"title"` is not `"Title"`, so a typo reads as Missing
rather than as an error:

```csharp
JsonValue oops = doc["title"];               // Missing, even though "Title" is right there
```

You can opt into case-insensitive matching with settings, but the default is an exact match.

**An optional object that the document omits stays safe to walk through.** `Publisher` is optional, so
a document without it does not throw when you reach past it:

```csharp
JsonObject legacy = JsonObject.Parse("{\"Id\":\"B124\",\"Title\":\"Nova\"}");
JsonValue city = legacy["Publisher"]["City"];   // Missing, no exception, because Publisher is absent
```

**A renamed or removed field reads as its default, silently.** If a newer schema renamed `Year` to
`PublishedYear`, this read returns the default and says nothing:

```csharp
int? y = doc.Get<int?>("PublishedYear", null);   // null: the field moved, and the default hides it
```

When a field must be present, read it as required (`doc.Get<int>("PublishedYear")`), which throws and
surfaces the drift instead of hiding it. The opposite direction is safe by design: when newer code
adds fields and writes the document back through the DOM, the fields an older reader does not know are
kept, not dropped. That is the silent-truncation problem [the explanation](index.md) opens with.

## Edit a copy

A parsed document is mutable, so you can change it in place. The indexer takes any value with an
implicit conversion to `JsonValue`, which covers the scalars (string, numbers, bool):

```csharp
doc["Year"] = 1966;              // implicit conversion: string, numbers, bool
```

For any other type, the indexer will not convert it for you. Use `Set<TValue>`, which serializes any
value, a record or a whole POCO included, or convert first with `JsonValue.FromValue(x)`:

```csharp
doc.Set("Publisher", new Publisher { Name = "Ace", City = "New York" });   // Set<TValue> serializes it
doc["Authors"] = JsonArray.FromValues(new[] { "Frank Herbert" });          // or convert, then assign
```

## Freeze it

A document you cache or share across threads should be read-only, so no caller can change it under
you. Freeze a copy; the frozen value rejects every edit:

```csharp
JsonObject frozen = doc.ToReadOnly();   // deep read-only copy
frozen["Year"] = 1967;                  // throws InvalidOperationException
```

To change a frozen document, take a mutable copy, edit it, and freeze again. The original stays valid,
so every cached reference to it still holds:

```csharp
JsonObject draft = frozen.ToMutable();    // mutable copy; the frozen original is untouched
draft["Year"] = 1967;
JsonObject updated = draft.ToReadOnly();   // a second frozen document, carrying the edit
```

The generated proxies fold that copy-edit-freeze cycle into one call, `book.With(m => m.Year = 1967)`,
which returns a new frozen proxy. [Working with CrystalJson](serializing.md) shows the proxies.

## Where to go next

You have used all three representations: the POCO (Plain Old CLR Object) through
`Serialize` / `Deserialize`, the DOM through `Parse` and the indexer, and the read-only and mutable
forms of a document.

- [Working with CrystalJson](serializing.md) covers the everyday tasks: building documents,
  the generated proxies for large documents, hardening the parser for untrusted input, and
  per-member serialization.
- [Reference](reference.md) has the attribute, settings and diagnostics tables.
- [What it is and why](index.md) explains the design: why the DOM never drops a field, and why
  the two serialization paths are held to the same output.
