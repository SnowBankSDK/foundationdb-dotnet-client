# Travailler avec CrystalJson

Les gestes de tous les jours, une section par tâche. Cette page suppose que vous savez ce qu'est
CrystalJson et pourquoi il a un DOM (Document Object Model), des proxies et deux chemins de
sérialisation ; sinon, lisez [l'explication](index.fr.md) d'abord. Porter un parc
`DataContractJsonSerializer` ou Newtonsoft est un projet à part entière et a
[sa propre page](porting-legacy.md). Les tables complètes des attributs, des settings et des
diagnostics sont dans la [référence](reference.md).

Tous les exemples utilisent `using SnowBank.Data.Json;`.

> **Tip : passez-le en global using.** D'autres bibliothèques d'un projet typique déclarent
> aussi un type nommé `JsonObject` (`System.Text.Json.Nodes` en particulier). La première fois
> qu'un fichier mentionne `JsonObject` sans le bon `using`, l'autocomplétion de l'IDE propose
> d'en ajouter un, et choisir le mauvais namespace produit des erreurs déroutantes : les
> méthodes ont l'air identiques mais prennent d'autres arguments. Déclarer
> `global using SnowBank.Data.Json;` une fois dans le `GlobalUsings.cs` du projet supprime
> l'ambiguïté pour tous les fichiers d'un coup.

## Sérialiser et désérialiser un type

Aucun setup n'est requis. Le chemin par réflexion construit un contrat de sérialisation à
l'exécution, à partir du type lui-même :

```csharp
public sealed record Book
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int Year { get; init; }
}

string json = CrystalJson.Serialize(book);
// => {"Id":"B123","Title":"Dune","Year":1965}

// throw si le JSON est null ou vide
Book back = CrystalJson.Deserialize<Book>(json);

// null au lieu de throw
Book? maybe = CrystalJson.Deserialize<Book>(json, defaultValue: null);

// des octets UTF-8, prêts pour une valeur en base
Slice bytes = CrystalJson.ToSlice(book);
```

Pour un type qui vous appartient et que vous sérialisez souvent, préférez le **source
generator** : déclarez un container une fois, et le compilateur émet le convertisseur que le
chemin par réflexion reconstruirait sinon à l'exécution :

```csharp
[CrystalJsonConverter]
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }        // le code généré atterrit ici

string json = AcmeSerializers.Book.ToJsonText(book);
Book   back = AcmeSerializers.Book.Deserialize(json);
```

Le convertisseur généré est sans réflexion (il fonctionne sous AOT et trimming), plus rapide, et
apporte les proxies typés utilisés plus loin sur cette page. Le projet qui consomme le générateur
a besoin de `LangVersion` 9 ou plus et du générateur référencé comme analyzer ; les deux sont un
setup unique, détaillé dans la [référence](reference.md).

Les deux voies produisent les mêmes octets pour le même type, donc vous pouvez commencer en
ad hoc et adopter le générateur plus tard sans changer aucun document stocké. Le chemin par
réflexion reste le bon outil quand il n'y a pas de schéma typé à déclarer, ou quand un quick and
dirty suffit : un script, un test, un outil jetable.

## Parser et naviguer du JSON inconnu

Quand la forme est dynamique ou partiellement connue, parsez vers le DOM et naviguez. Le point
d'entrée du parsing énonce comment une mauvaise forme de premier niveau se traite. Quand un
payload non-objet serait un bug du producteur, parsez directement vers le type et laissez-le throw ;
quand c'est un cas ordinaire que votre code doit gérer, parsez vers `JsonValue` et inspectez :

```csharp
// un payload non-objet est extraordinaire ici : parser vers le type, ça throw sinon
JsonObject obj = JsonObject.Parse(json);

// un payload non-objet est un cas ordinaire ici : le gérer, puis continuer
JsonValue value = JsonValue.Parse(json);
if (value is not JsonObject o)
{
    // pas un objet : rejeter la requête, sauter l'entrée, ...
    return;
}
// à partir d'ici, o est l'objet typé
```

`JsonArray.Parse(...)` est le jumeau pour les tableaux, et le nommage est un pattern, pas une
coïncidence : chaque type du DOM imbrique une classe `ReadOnly` aux mêmes points d'entrée
(`JsonValue.ReadOnly.Parse`, `JsonObject.ReadOnly.Parse`, `JsonValue.ReadOnly.FromValue`), qui
rendent un document figé, sûr pour le cache, au lieu d'un mutable. La classe statique
`CrystalJson` sert la voie POCO (`Serialize`, `Deserialize`, `ToSlice`) ; le DOM se parse par
les types du DOM eux-mêmes.

Une fois parsé, la navigation a la **null propagation intégrée** : un indexeur ne throw jamais
sur un membre absent, il rend un objet null (`JsonNull.Missing`) que le hop suivant accepte,
donc une chaîne entière est sûre sans un seul null check manuel :

```csharp
JsonValue city = obj["user"]["address"]["city"];   // Missing si un hop manque, pas de NRE
bool present   = !city.IsNullOrMissing();

int    age  = obj.Get<int>("age", 0);              // défaut si absent
string name = obj.Get<string>("name");             // obligatoire : throw si absent
if (obj.TryGet<string>("email", out var email)) { /* ... */ }

JsonObject meta  = obj.GetObjectOrEmpty("meta");   // jamais null ; vide si absent
JsonArray  items = obj.GetArray("items");          // throw si ce n'est pas un tableau
foreach (var item in items.AsObjects()) { /* seulement les JsonObject */ }
```

Les proxies générés, plus loin sur cette page, propagent l'absence de la même façon : une chaîne
à travers un objet interne absent continue de naviguer (`proxy.Metadata.IsNullOrMissing()` vous
le dit), un membre optionnel se lit comme son défaut, et un membre `required` absent du document
throw une `JsonBindingException`. Jamais de `NullReferenceException`, et jamais de null check
manuel.

Une distinction compte quand vous testez le null : `JsonNull.Null` est un `null` explicite dans
le document, `JsonNull.Missing` est un membre qui n'était pas là, et `JsonNull.Error` est un
accès invalide (indexer un non-tableau). Les trois rapportent `IsNull == true` ; utilisez
`IsNullOrMissing()` ou `IsMissing()` quand la différence compte.

## Construire un document JSON

Construisez avec les factories `Create` ; les conversions implicites couvrent les valeurs
scalaires :

```csharp
var obj = JsonObject.Create([
    ("name", "Alice"),
    ("age", 30),
    ("tags", JsonArray.Create("admin", "user")),
    ("point", JsonObject.Create([ ("x", 1), ("y", 2) ])),
]);

var arr = JsonArray.Create(1, 2, 3);
```

Le nommage suit le même pattern que `Parse` : chaque factory a sa jumelle `ReadOnly`
(`JsonObject.ReadOnly.Create`, `JsonArray.ReadOnly.Create`) qui produit une valeur figée avec la
même forme d'appel. La forme collection initializer (`new JsonObject { ["name"] = "Alice" }`)
compile aussi ; ces pages utilisent les factories, qui se lisent à l'identique en mutable et en
figé.

Une valeur destinée au cache ou au partage entre threads doit être en lecture seule. Figez une
mutable, ou construisez en lecture seule directement :

```csharp
// copie profonde en lecture seule (elle-même si déjà figée) ; toute modification va throw
var frozen = obj.ToReadOnly();

// en lecture seule dès le départ, avec des tuples ("clé", valeur)
var ro = JsonObject.ReadOnly.Create([
    ("name", "Alice"),
    ("tags", JsonArray.ReadOnly.Create(["admin", "user"])),
]);
```

Muter un conteneur en lecture seule throw `InvalidOperationException`, et c'est la feature : un
document en cache ne peut pas être corrompu par un appelant. Pour aller d'une valeur CLR au DOM
sans texte intermédiaire, utilisez `JsonValue.FromValue(poco)` (ou
`JsonValue.ReadOnly.FromValue(poco)`).

## Modifier un document

Un DOM mutable se modifie en place, avec l'indexeur (à travers les conversions implicites vers
`JsonValue`) ou le `Set` fluent et générique, qui accepte toute valeur que le sérialiseur
connaît, un POCO entier compris :

```csharp
obj["status"] = "online";        // poser ou remplacer un champ
obj["point"]["x"] = 123;         // quand "point" existe et est un objet
obj.Remove("obsolete");
arr.Add(4);

obj                              // Set fluent, une édition par ligne
    .Set("count", 42)
    .Set("unit", "pages")
    .Set("author", author);      // Set<TValue> sérialise toute valeur, POCO compris
```

Pour construire une valeur DOM depuis une collection existante, les helpers `FromValues`
couvrent les spans, les tableaux, `IEnumerable<T>` (avec un sélecteur optionnel) et les
dictionnaires, sur les types mutables comme sur leurs jumelles `ReadOnly` :

```csharp
JsonArray  tags   = JsonArray.FromValues(book.Tags);
JsonArray  titles = JsonArray.FromValues(books, b => b.Title);
JsonObject scores = JsonObject.ReadOnly.FromValues(scoresByName);   // figé
```

Il n'y a pas d'auto-création sur le DOM brut : assigner à travers un intermédiaire absent
(`obj["missing"]["x"] = 1`) throw. Créez l'objet enfant d'abord.

Un document en lecture seule ne se modifie pas en place, par conception. Le « modifier », c'est
un cycle copy-on-write : prendre une copie mutable, la modifier, refiger. L'original figé reste
en place, donc toute référence en cache vers lui reste valide :

```csharp
var frozen = obj.ToReadOnly();

var draft = frozen.ToMutable();      // copie mutable ; l'original est intact
draft["status"] = "offline";
var updated = draft.ToReadOnly();    // un second document figé, qui porte l'édition
```

Une copie est évitable : quand une méthode construit un document et le retourne figé, `Freeze()`
marque l'instance elle-même en lecture seule au lieu de la copier, le pattern builder des
documents figés. Ne l'utilisez que sur une valeur que la méthode possède exclusivement, car
toute référence vers l'instance devient en lecture seule avec elle :

```csharp
static JsonObject BuildManifest()
{
    var m = JsonObject.Create();
    m["version"] = 3;
    // ...construire librement, puis figer en place : pas de copie défensive
    return m.Freeze();
}
```

## Lire et modifier via les proxies générés

Quand un document a quarante champs et que le code en consomme trois, ne le désérialisez pas.
Enveloppez le DOM parsé dans le proxy en lecture seule généré et lisez les champs voulus, typés :

```csharp
JsonObject doc = JsonObject.Parse(bytes);

// une vue typée sur le document parsé ; rien n'est copié ni matérialisé
AcmeSerializers.Book.ReadOnly book = AcmeSerializers.Book.ToReadOnly(doc);

// lecture typée, avec IntelliSense
string title = book.Title;

// matérialiser le POCO, seulement si nécessaire
Book poco = book.ToValue();
```

Les éditions sur un proxy en lecture seule passent par le copy-on-write : l'original reste figé,
vous obtenez un nouveau proxy figé (ou prenez un proxy mutable explicite) :

```csharp
// copy-on-write : le proxy d'origine reste figé
AcmeSerializers.Book.ReadOnly edited = book.With(m => { m.Year = 1966; });

// ou prendre un proxy mutable explicite
AcmeSerializers.Book.Writable w = book.ToMutable();
w.Year = 1966;
```

Comme le document en dessous garde tous les champs avec lesquels il est arrivé, un aller-retour
à travers un proxy ne perd jamais les champs que cette version du code ne connaît pas, le
problème de troncature silencieuse par lequel [l'explication](index.fr.md) s'ouvre.

## Durcir le parsing d'un input non fiable

Le parser est volontairement permissif par défaut (les commentaires JavaScript et les virgules
finales sont acceptés), ce qui est faux pour un input que vous ne contrôlez pas. Resserrez-le :

```csharp
var settings = CrystalJsonSettings.JsonStrict   // ni commentaires, ni virgules finales
    .ThrowOnDuplicateFields();                  // une clé répétée est une erreur

JsonValue value = JsonValue.Parse(payload, settings);
```

`JsonStrict` ne couvre pas les clés dupliquées à lui seul ; ajoutez `ThrowOnDuplicateFields()`
quand une clé répétée doit échouer. Le contenu après la valeur de premier niveau est rejeté par
défaut ; pour lire plusieurs documents consécutifs dans un même buffer, utilisez
`CrystalJson.ParseFragment`, pas `WithTrailingData()` (qui parse la première valeur et jette le
reste en silence).

## Changer la sérialisation d'un seul membre

La plupart des besoins par membre sont couverts par des attributs, sans code à écrire.
Commencez par eux :

```csharp
[JsonProperty("id")]                                  // renommage en sortie
public required string Id { get; init; }

[JsonProperty(DefaultValue = "draft")]                // le défaut déclaré du membre
public string Status { get; init; } = "draft";

[JsonProperty(EnumFormat = JsonEnumFormat.Number)]    // cet enum-là reste numérique
public BookGenre Genre { get; init; }

[JsonProperty(NumberFormat = JsonNumberFormat.String)]
public long AccountId { get; init; }                  // "12345678901234567" : sûr pour JS

[JsonBooleanLiterals("0", "1")]                       // formes legacy des booléens
public bool Enabled { get; set; }

[JsonBooleanLiterals(null, true)]                     // écrit true, ou omet le membre
public bool Flagged { get; set; }
```

Quand aucun attribut ne couvre le besoin, que le type du membre a sa propre forme compacte, ou
que la valeur doit traverser une forme legacy, écrivez un **member converter** et attachez-le
avec l'attribut propre à CrystalJson. Un cas réaliste : une struct de coordonnées stockée comme
un tableau compact `[lat, lon]` au lieu d'un objet à deux champs nommés :

```csharp
public readonly record struct GpsPosition(double Latitude, double Longitude);

public sealed class GpsPositionConverter : IJsonMemberConverter<GpsPosition>
{
    public JsonValue Pack(
        GpsPosition value,
        CrystalJsonSettings? settings = null,
        ICrystalJsonTypeResolver? resolver = null)
    {
        return JsonArray.Create(value.Latitude, value.Longitude);
    }

    public GpsPosition Unpack(JsonValue value, ICrystalJsonTypeResolver? resolver)
    {
        var arr = value.AsArray();
        return new GpsPosition(arr.Get<double>(0), arr.Get<double>(1));
    }
}

[JsonConvertWith(typeof(GpsPositionConverter))]
public GpsPosition Position { get; init; }
// => "position": [48.8584, 2.2945], sur les deux chemins et dans les deux sens
```

Un converter ne voit jamais null ni absent : le pipeline gère ces cas avant qu'il ne s'exécute.
Un converter qui n'implémente qu'une des deux facettes `IJsonPacker<T>` / `IJsonDeserializer<T>`
sert cette direction-là, et le traitement par défaut couvre l'autre. Nommer un type qui
n'implémente ni l'une ni l'autre est une erreur de build bruyante (`CJSON0010`), jamais un
fallback silencieux.

## Sérialiser un type entier soi-même

Quand c'est la forme elle-même qui est sur mesure (un id compact empaqueté comme un tableau de
ses parties), implémentez les interfaces directement sur le type :

```csharp
public interface IJsonPackable
{
    JsonValue JsonPack(CrystalJsonSettings settings, ICrystalJsonTypeResolver resolver);
}

public interface IJsonDeserializable<TSelf>
{
    static abstract TSelf JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver);
}
```

Un cas concret : un id de commande fait d'une région et d'un numéro de séquence, sérialisé comme
une seule string compacte (`"EU-000123"`) au lieu d'un objet à deux champs nommés :

```csharp
public readonly record struct OrderId(string Region, int Number)
    : IJsonPackable, IJsonDeserializable<OrderId>
{
    public JsonValue JsonPack(
        CrystalJsonSettings settings,
        ICrystalJsonTypeResolver resolver)
    {
        return JsonString.Return($"{this.Region}-{this.Number:D6}");
    }

    public static OrderId JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver = null)
    {
        // défensif : Required<string>() throw une JsonBindingException si null ou absent
        string literal = value.Required<string>();
        int dash = literal.IndexOf('-');
        return new OrderId(literal[..dash], int.Parse(literal[(dash + 1)..]));
    }
}

string json = CrystalJson.Serialize(new OrderId("EU", 123));
// => "EU-000123", partout où le type apparaît : seul, comme membre, dans une collection
```

Les mêmes interfaces produisent une forme **objet** sur mesure. Un intervalle de dates dont la
borne haute est optionnelle décide de ses propres membres : `"to"` n'existe que quand
l'intervalle est fermé, quoi que disent les settings sur les membres null :

```csharp
public sealed record DateRange(DateOnly From, DateOnly? To)
    : IJsonPackable, IJsonDeserializable<DateRange>
{
    public JsonValue JsonPack(
        CrystalJsonSettings settings,
        ICrystalJsonTypeResolver resolver)
    {
        var obj = JsonObject.Create("from", JsonString.Return(this.From));
        if (this.To is not null)
        {
            obj["to"] = JsonString.Return(this.To.Value);
        }
        return obj;
    }

    public static DateRange JsonDeserialize(
        JsonValue value,
        ICrystalJsonTypeResolver? resolver = null)
    {
        var obj = value.AsObject();
        return new DateRange(
            obj.Get<DateOnly>("from"),
            obj.Get<DateOnly?>("to", null));
    }
}

// => {"from":"2026-01-01"}                    borne ouverte
// => {"from":"2026-01-01","to":"2026-03-31"}  fermé
```

Déclarer le paramètre resolver avec une valeur par défaut est la convention (les appelants
peuvent l'omettre, et l'interface est quand même satisfaite). `JsonPack` et `JsonDeserialize`
doivent être inverses ; pinnez l'aller-retour dans un test. Construisez les valeurs avec les
factories (`JsonString.Return(...)`, `JsonNumber.Return(...)`, `JsonArray.ReadOnly.Create(...)`),
et gérez le null et l'absent défensivement, comme `Required<string>()` le fait ci-dessus. Un
converter au niveau du type, attaché avec `[JsonConvertWith]`, est l'alternative quand vous ne
pouvez pas modifier le type lui-même.
