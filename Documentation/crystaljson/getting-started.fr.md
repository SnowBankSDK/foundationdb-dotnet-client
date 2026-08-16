# Démarrer avec CrystalJson

Cette page suit un livre de bout en bout à travers CrystalJson : le sérialiser, le relire, le parser
comme un document, y naviguer sans risque, en modifier une copie, et le figer. Dix minutes, un seul
exemple. Ensuite, [Travailler avec CrystalJson](serializing.fr.md) donne les guides tâche par tâche
et la [référence](reference.fr.md) donne les tables des attributs et des *settings*. Pour la conception
derrière le DOM (Document Object Model), les *proxies* et les deux chemins de sérialisation, lisez
[l'explication](index.fr.md).

Tous les exemples utilisent un seul *using* :

```csharp
using SnowBank.Data.Json;
```

## Le type

De simples *records*, sans attribut, sans rien à configurer :

```csharp
public sealed record Book
{
    public required string Id { get; init; }
    public required string Isbn { get; init; }
    public required string Title { get; init; }
    public required string[] Authors { get; init; }
    public int Year { get; init; }
    public Publisher? Publisher { get; init; }   // optionnel : un document peut l'omettre
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

## Le sérialiser

`CrystalJson.Serialize` transforme la valeur en une *string* JSON. Aucun convertisseur ni aucune
inscription ne sont nécessaires ; le chemin par réflexion lit le type à l'exécution. La sortie par
défaut tient sur une seule ligne lisible, avec une espace après chaque deux-points et chaque virgule :

```csharp
string json = CrystalJson.Serialize(book);
// => { "Id": "B123", "Isbn": "978-0441013593", "Title": "Dune", "Authors": [ "Frank Herbert" ], "Year": 1965, "Publisher": { "Name": "Chilton Books", "City": "Philadelphia" } }
```

Passez un `CrystalJsonSettings` en deuxième argument pour changer la sortie. Deux *presets* que vous
utiliserez tôt sont `JsonCompact`, qui retire toutes les espaces pour le stockage ou le transport, et
`JsonIndented`, qui répartit le document sur plusieurs lignes pour une lecture humaine :

```csharp
string compact = CrystalJson.Serialize(book, CrystalJsonSettings.JsonCompact);
// => {"Id":"B123","Isbn":"978-0441013593","Title":"Dune","Authors":["Frank Herbert"],"Year":1965,"Publisher":{"Name":"Chilton Books","City":"Philadelphia"}}

string indented = CrystalJson.Serialize(book, CrystalJsonSettings.JsonIndented);
```

La forme indentée se lit :

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

`CrystalJsonSettings` porte toutes les options de sortie et de *parsing*, et les modificateurs se
composent (par exemple `CrystalJsonSettings.JsonIndented.WithEnumAsNumbers()`). La
[référence](reference.fr.md#settings) liste les *presets* et les modificateurs courants.

La même valeur peut être écrite vers d'autres sorties qu'une *string*. Choisissez celle dont l'appelant
a besoin :

```csharp
using System.Buffers;

byte[] bytes = CrystalJson.ToBytes(book);           // un byte[] neuf
CrystalJson.SerializeTo(stream, book);              // directement dans un Stream, sans tableau intermédiaire

Slice slice = CrystalJson.ToSlice(book);            // les octets UTF-8 comme une Slice
using SliceOwner owner = CrystalJson.ToSlice(book, ArrayPool<byte>.Shared);   // depuis un pool, rendu au Dispose
```

`Slice` est la vue de ce SDK sur une plage d'octets, et c'est ce que les couches de base de données
stockent et transmettent, donc une valeur sérialisée directement vers une `Slice` évite la copie
`byte[]` que ces couches feraient sinon. Pour un chemin chaud, `ToSlice` avec un `ArrayPool<byte>`
rend un `SliceOwner` qui loue son *buffer* et le rend quand vous le disposez, ce qui alloue le moins.
`Slice`, `SliceOwner` et les *buffers* en *pool* ont leur propre guide,
[Données binaires (Slice et Buffers)](../guide/slices-and-buffers.md).

## Le relire

`CrystalJson.Deserialize` réassocie le JSON à un `Book`. La forme simple *throw* quand l'entrée est
null ou vide ; passez une valeur par défaut pour obtenir une valeur au lieu d'une exception :

```csharp
Book back = CrystalJson.Deserialize<Book>(json);            // throw si l'entrée est null ou vide
Book? maybe = CrystalJson.Deserialize<Book>(json, defaultValue: null);   // null à la place
```

## Le parser comme un document

Quand vous voulez lire un champ sans matérialiser tout le type, parsez vers le DOM et naviguez.
`JsonObject.Parse` donne un arbre ; `Get<T>` lit un membre :

```csharp
JsonObject doc = JsonObject.Parse(json);

string title = doc.Get<string>("Title");    // "Dune", obligatoire : throw si le champ est absent
```

Pour un champ qui peut être absent, lisez-le comme un type *nullable*. `Get<int?>` rend null quand le
membre est absent, ce qui est plus clair qu'inventer une sentinelle comme `0` ou `-1` qu'une vraie
année pourrait prendre :

```csharp
int? year = doc.Get<int?>("Year", null);     // 1965, ou null si le champ est absent
```

## Naviguer sans risque

La navigation ne *throw* jamais sur un membre absent. Un chemin à travers un champ absent rend une
valeur null que la lecture suivante accepte, donc vous vérifiez une fois à la fin plutôt qu'à chaque
étape :

```csharp
JsonValue nowhere = doc["does"]["not"]["exist"];   // pas d'exception ; rend une valeur Missing
bool present = !nowhere.IsNullOrMissing();          // false
```

`Missing` est l'une des trois valeurs `JsonNull` particulières : un membre absent, un `null` explicite
dans le document, ou un accès invalide. Toutes se lisent comme null ;
[Travailler avec CrystalJson](serializing.fr.md) montre quand la différence compte.

C'est le filet de sécurité du DOM, et il masque trois erreurs qui se lisent chacune comme « le champ
est simplement absent ». Connaissez-les avant qu'elles ne vous coûtent un après-midi.

**Les noms de champs sont sensibles à la casse par défaut.** `"title"` n'est pas `"Title"`, donc une
faute de frappe se lit comme Missing plutôt que comme une erreur :

```csharp
JsonValue oops = doc["title"];               // Missing, alors que le champ "Title" existe bel et bien
```

Vous pouvez activer une correspondance insensible à la casse via les *settings*, mais le défaut est une
correspondance exacte.

**Un objet optionnel que le document omet reste sûr à parcourir.** `Publisher` est optionnel, donc un
document sans lui ne *throw* pas quand vous passez au travers :

```csharp
JsonObject legacy = JsonObject.Parse("{\"Id\":\"B124\",\"Title\":\"Nova\"}");
JsonValue city = legacy["Publisher"]["City"];   // Missing, pas d'exception, car Publisher est absent
```

**Un champ renommé ou supprimé se lit comme son défaut, en silence.** Si un schéma plus récent a
renommé `Year` en `PublishedYear`, cette lecture rend le défaut sans rien dire :

```csharp
int? y = doc.Get<int?>("PublishedYear", null);   // null : le champ a bougé, et le défaut le masque
```

Quand un champ doit être présent, lisez-le comme obligatoire (`doc.Get<int>("PublishedYear")`), ce qui
*throw* et fait remonter la dérive au lieu de la masquer. La direction inverse est sûre par conception :
quand du code plus récent ajoute des champs et réécrit le document via le DOM, les champs qu'un
lecteur plus ancien ne connaît pas sont gardés, pas supprimés. C'est le problème de troncature
silencieuse par lequel [l'explication](index.fr.md) s'ouvre.

## Modifier une copie

Un document parsé est *mutable*, vous pouvez donc le modifier en place. L'indexeur prend toute valeur
avec une conversion implicite vers `JsonValue`, ce qui couvre les scalaires (*string*, nombres, *bool*) :

```csharp
doc["Year"] = 1966;              // conversion implicite : string, nombres, bool
```

Pour tout autre type, l'indexeur ne le convertira pas pour vous. Utilisez `Set<TValue>`, qui sérialise
n'importe quelle valeur, un record ou un POCO entier compris, ou convertissez d'abord avec
`JsonValue.FromValue(x)` :

```csharp
doc.Set("Publisher", new Publisher { Name = "Ace", City = "New York" });   // Set<TValue> le sérialise
doc["Authors"] = JsonArray.FromValues(new[] { "Frank Herbert" });          // ou convertir, puis assigner
```

## Le figer

Un document que vous mettez en cache ou partagez entre *threads* doit être en lecture seule, pour
qu'aucun appelant ne le change sous vos pieds. Figez une copie ; la valeur figée rejette toute
modification :

```csharp
JsonObject frozen = doc.ToReadOnly();   // copie profonde en lecture seule
frozen["Year"] = 1967;                  // throw InvalidOperationException
```

Pour changer un document figé, prenez une copie *mutable*, modifiez-la, puis refigez. L'original reste
valide, donc toute référence en cache vers lui tient toujours :

```csharp
JsonObject draft = frozen.ToMutable();    // copie mutable ; l'original figé est intact
draft["Year"] = 1967;
JsonObject updated = draft.ToReadOnly();   // un second document figé, qui porte l'édition
```

Les *proxies* générés replient ce cycle copier-modifier-figer en un seul appel,
`book.With(m => m.Year = 1967)`, qui rend un nouveau *proxy* figé.
[Travailler avec CrystalJson](serializing.fr.md) montre les *proxies*.

## Pour aller plus loin

Vous avez utilisé les trois représentations : le *POCO* (Plain Old CLR Object) via
`Serialize` / `Deserialize`, le DOM via `Parse` et l'indexeur, et les formes en lecture seule et
*mutable* d'un document.

- [Travailler avec CrystalJson](serializing.fr.md) couvre les tâches de tous les jours : construire
  des documents, les *proxies* générés pour les gros documents, durcir le *parser* pour un *input* non
  fiable, et la sérialisation par membre.
- [Référence](reference.fr.md) contient les tables des attributs, des *settings* et des diagnostics.
- [Ce que c'est, et pourquoi](index.fr.md) explique la conception : pourquoi le DOM ne perd jamais un
  champ, et pourquoi les deux chemins de sérialisation sont tenus au même résultat.
