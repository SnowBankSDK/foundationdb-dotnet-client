# Guides pratiques : clés, valeurs et *Layers*

Chaque section ici décrit une tâche avec l'API *key/value*. Elles supposent que vous avez déjà une
base de données ouverte (un `IFdbDatabaseProvider`), telle que configurée dans
[Prise en main](../../getting-started.md). Pour comprendre pourquoi les clés sont des tuples, ce
qu'est un *subspace*, et pourquoi un *Layer* ne détient aucun état par transaction, voyez
[l'explication](index.md) ; pour l'encodage des tuples en détail, voyez la [référence](../../../snowbank/tuples.md).

## Construire une clé

Construisez les clés avec `subspace.Key(...)`, qui encode ses arguments en tuple derrière le préfixe
du *subspace*. Construisez les valeurs avec les *factories* `FdbValue.*`. Passez l'objet clé ou valeur
directement à la transaction ; ne le pré-sérialisez pas avec `.ToSlice()`.

```csharp
// construit les clés (typées, lazy)
var k = subspace.Key("user", 123);          // préfixe + ("user", 123)
Slice value = await tr.GetAsync(k);          // rendue en octets ici, dans des buffers poolés
tr.Set(subspace.Key("user", 123), FdbValue.FromTuple(("Alice", 30)));
tr.Clear(subspace.Key("user", 123));
```

`.ToSlice()` existe, mais seulement quand vous avez besoin des *octets en tant que données*
(*logging*, tests, ou stockage d'une clé à l'intérieur d'une valeur). Pour comprendre pourquoi la
concaténation manuelle casse l'ordre et l'échappement, voyez [l'explication](index.md).

## Construire une clé dont la fin est dynamique

Pour un index générique dont la valeur indexée a un type arbitraire, chaînez un tuple construit à
l'exécution sur un préfixe typé :

```csharp
IVarTuple value = /* construit à l'exécution */;
var indexKey = subspace.Key(INDEXES, indexId).Tuple(value);   // préfixe typé (1, idx) + suffixe dynamique
```

C'est le remplacement moderne de l'ancien style dynamique `subspace.Pack(...)`.

## Créer des ids ordonnés et sans collision

Pour les files d'attente, les journaux d'événements et les *change feeds* (tout ce qui a besoin d'ids
ordonnés globalement et sans collision), laissez la base de données assigner un **VersionStamp** au moment du
commit :

```csharp
var stamp = tr.CreateVersionStamp(userVersion);              // un stamp incomplet, complété au commit
tr.SetVersionStampedKey(log.Key(stamp), payload);            // FDB écrit le vrai stamp monotone au commit
```

Un simple *range scan* renvoie alors les entrées dans l'ordre du commit, sans compteur partagé source
de contention. Voyez [*Layers* avancés](../advanced-layers/index.md) pour le *pattern* complet de
*change feed*.

## Lire un *range*

La plupart des *layers* lisent des ***ranges***, pas des clés individuelles. Construisez les *ranges*
à partir de clés et de *subspaces* ; n'incrémentez jamais les octets à la main.

```csharp
tr.GetRange(subspace.ToRange());                  // tout ce qui est sous le subspace
tr.GetRange(subspace.Key("user", 123).ToRange()); // tout ce qui est sous un préfixe
FdbKeyRange.Between(subspace.Key(100), subspace.Key(200));  // [100, 200)
```

Dérivations utiles (méthodes d'extension sur n'importe quelle clé) : `key.Successor()` (la clé
suivante, une borne inférieure exclusive), `key.NextSibling()` (la première clé qui n'a pas `key`
comme préfixe, une borne supérieure exclusive sur ses enfants), `subspace.First()` /
`subspace.Last()`, et les `KeySelector` `FirstGreaterOrEqual()` / `LastLessOrEqual()`.

## Décoder des clés issues d'un *range*

Lisez un *range*, récupérez les octets bruts des clés, et décodez-les avec le **même *subspace*** qui
les a produits :

```csharp
foreach (var kv in chunk)
{
    var (name, id) = subspace.Decode<string, int>(kv.Key);  // STuple<string?, int?>
    int idOnly     = subspace.DecodeLast<int>(kv.Key);
    IVarTuple all  = subspace.Unpack(kv.Key);
}
```

Utilisez `Decode`/`DecodeLast`/`Unpack` ; ne découpez jamais les octets à la main.

## Résoudre un *subspace* via le *Directory layer*

Vous ne codez jamais un préfixe en dur. Déclarez un **chemin** logique, et résolvez-le en *subspace*
via le *Directory layer* à l'intérieur de la transaction :

```csharp
ISubspaceLocation location = db.Root["Tenants"]["ACME"]["Documents"]["Books"];

await db.WriteAsync(async tr =>
{
    IKeySubspace subspace = await location.Resolve(tr);   // interroge le Directory layer
    tr.Set(subspace.Key("BOOK_123"), FdbValue.FromTuple(("Title", "ISBN")));
}, ct);
```

Trois règles, et [l'explication](index.md) couvre pourquoi le préfixe est dynamique en premier lieu :

- **Résolvez à chaque transaction.** Le préfixe est stable en pratique mais pas garanti pour
  toujours ; le mettre en cache vous-même contourne le *Directory layer* et risque une corruption.
- **Resolve ouvre ; il ne crée pas.** `Resolve` *throw* si le *directory* n'existe pas encore.
  Créez-le la première fois avec `location.CreateOrOpenAsync(tr)` dans une transaction en
  lecture-écriture, ce que fait un *layer* à l'initialisation.
- **L'indexeur `db.Root[...]` descend d'un *segment* à la fois.** `db.Root["a", "b"]` n'est *pas*
  deux segments : la surcharge à deux arguments est `(name, layerId)`. Chaînez l'indexeur
  (`db.Root["a"]["b"]`) ou passez un `FdbPath`.

## Choisir un encodage de valeur

Les valeurs sont produites par les *factories* `FdbValue.*`. Choisissez la *factory* qui correspond
au *pattern* d'accès :

| Besoin | Utiliser |
|---|---|
| Octets bruts / *blob* | `FdbValue.ToBytes(slice)` |
| Valeur vide (entrées d'index) | `FdbValue.Empty` |
| Texte | `FdbValue.ToTextUtf8(s)` / `ToTextUtf16(s)` |
| Un compteur que vous muterez atomiquement | `FdbValue.ToFixed64LittleEndian(n)` (le little-endian de taille fixe est requis pour `AtomicAdd64`) |
| Un tuple | `FdbValue.FromTuple(("a", 1))` |
| Document JSON | `FdbValue.ToJson(obj)`, voir [CrystalJson](../../../snowbank/crystaljson/index.md) |

Pour relire : `slice.ToInt64()`, `slice.ToStringUtf8()`, `CrystalJson.Deserialize<T>(slice)` (qui
associe une clé manquante ou vide à `null`), etc.

Pour une valeur JSON, `FdbValue.ToJson(obj)` sérialise un objet via CrystalJson, la *stack* JSON du
SDK, et `CrystalJson.Deserialize<T>(slice)` la relit :

```csharp
tr.Set(subspace.Key("D", book.Id), FdbValue.ToJson(book));
Book? loaded = CrystalJson.Deserialize<Book>(await tr.GetAsync(subspace.Key("D", book.Id)));
```

CrystalJson est une *stack* JSON généraliste avec son propre guide : le DOM, le *source generator* et
les *settings* sont dans [CrystalJson](../../../snowbank/crystaljson/index.md).

## Écrire un *Layer*

Encapsulez l'accès à la base de données dans un *Layer* : un fin *wrapper* au-dessus d'un
`ISubspaceLocation` qui ne détient aucun état par transaction. Pour comprendre pourquoi le *pattern* a
cette forme, voyez [l'explication](index.md). Chaque *layer* suit la même forme :

1. La classe du *layer* est un **fin *wrapper* réutilisable** au-dessus d'un `ISubspaceLocation`
   (plus des codecs/options). Elle ne détient **aucun état par transaction**.
2. Elle implémente `IFdbLayer<TState>`. `Resolve(tr)` résout la *location* et renvoie un **`State`**
   qui contient l'`IKeySubspace` résolu. Mémoïsez-le dans `tr.Context` pour que les appels répétés à
   `Resolve(tr)` dans une transaction soient peu coûteux.
3. Tout le travail réel se fait dans des méthodes qui prennent une transaction et utilisent le
   *subspace* du `State` pour construire les clés.
4. **Le `State` ne doit jamais échapper à la transaction** : ne le stockez pas dans un champ et ne le
   réutilisez pas entre les *retries*. (les données locales de `tr.Context` sont par transaction,
   donc y mémoïser est sûr ; un champ de *layer* ne l'est pas.)

### Un *store* de documents avec un index secondaire

```csharp
public sealed partial class BookStore : IFdbLayer<BookStore.State>
{
    // Distingue les sous-parties du subspace avec de petites constantes entières, pas des strings :
    // 0 s'encode en 1 octet (0x14), 1 en 2 octets (0x15 0x01), alors que "D" fait 3 octets (0x02 'D' 0x00) sur chaque clé.
    private const int SUBSPACE_DOCUMENTS = 0;      // (0, <id>)            -> document json
    private const int SUBSPACE_INDEX_AUTHOR = 1;   // (1, <author>, <id>) -> vide (entrée d'index)

    public BookStore(ISubspaceLocation location) => this.Location = location;
    public ISubspaceLocation Location { get; }
    public string Name => nameof(BookStore);

    private const string LocalDataKey = nameof(BookStore);
    public ValueTask<State> Resolve(IFdbReadOnlyTransaction tr)
    {
        if (tr.Context.TryGetLocalData(LocalDataKey, out State? s)) return new(s);
        return ResolveSlow(this, tr);
        static async ValueTask<State> ResolveSlow(BookStore self, IFdbReadOnlyTransaction tr)
        {
            var subspace = await self.Location.Resolve(tr);
            return tr.Context.GetOrCreateLocalData(LocalDataKey, new State(self, subspace));
        }
    }

    public sealed partial class State
    {
        public IKeySubspace Subspace { get; }
        internal State(BookStore layer, IKeySubspace subspace) => this.Subspace = subspace;

        public void Insert(IFdbTransaction tr, Book book)
        {
            tr.Set(this.Subspace.Key(SUBSPACE_DOCUMENTS, book.Id), FdbValue.ToJson(book));
            tr.Set(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, book.Author, book.Id), FdbValue.Empty);
        }

        public async Task<Book?> GetAsync(IFdbReadOnlyTransaction tr, string id)
            => CrystalJson.Deserialize<Book>(await tr.GetAsync(this.Subspace.Key(SUBSPACE_DOCUMENTS, id)));

        public IAsyncQuery<string> FindIdsByAuthor(IFdbReadOnlyTransaction tr, string author)
            => tr.GetRange(this.Subspace.Key(SUBSPACE_INDEX_AUTHOR, author).ToRange())
                 .Select(kv => this.Subspace.DecodeLast<string>(kv.Key)!);
    }
}
```

Utilisé via les *helpers* de *retry loop*, qui résolvent le *state* pour vous :

```csharp
var store = new BookStore(db.Root["Documents"]["Books"]);
await store.WriteAsync(db, (tr, st) => st.Insert(tr, book), ct);
Book? b = await store.ReadAsync(db, (tr, st) => st.GetAsync(tr, "B1"), ct);
```

## Composer plusieurs *layers* dans une transaction

Comme les méthodes d'un *layer* prennent une transaction au lieu d'ouvrir la leur, une seule
*retry loop* peut piloter plusieurs *layers* de façon atomique. Insérez un document, mettez en file
d'attente un *job* d'arrière-plan, et publiez un événement dans le même `WriteAsync`, et soit ils
committent tous, soit aucun :

```csharp
await db.WriteAsync(async tr =>
{
    await books.InsertAsync(tr, book);
    await workers.QueueAsync(tr, new GenerateThumbnails(book.Id));
    await feed.PublishAsync(tr, new BookCreated(book.Id));
}, ct);
```

Si la transaction échoue à committer, c'est comme si la requête n'avait jamais eu lieu : pas de
document, pas de *job*, pas d'événement.

## Maintenir un index secondaire

Les entrées d'index sont des **données dérivées** : c'est votre code, pas la base de données, qui les garde
synchronisées. C'est là que les *layers* se trompent le plus souvent :

- **Pour changer l'index, vous devez connaître l'*ancienne* valeur indexée**, et vous ne pouvez
  l'apprendre que depuis le **document stocké**, jamais depuis un objet que l'appelant vous passe (il
  peut être périmé, laissant une entrée d'index orpheline). `Update`/`Patch`/`Delete` lisent donc le
  document actuel et en dérivent l'ancienne clé d'index à partir de *celui-là*.
- **Mutez l'index dans la même transaction que le document**, pour qu'il ne puisse jamais se
  désynchroniser lors d'un échec partiel.
- **Ne réécrivez l'index que lorsque la valeur indexée a réellement changé.** Pour des documents mis
  à jour fréquemment dont le champ indexé est stable, cela évite des écritures inutiles (et les
  conflits qu'elles provoquent).

Concrètement, changer l'auteur d'un livre réécrit le document **sur place** et **déplace** son entrée
d'index, les deux dans une seule transaction. Le document garde sa clé, donc seule sa valeur change ;
la clé d'index est réellement différente, donc l'ancienne entrée est supprimée et une nouvelle est
insérée :

```fdb-diff
title: changer l'auteur d'un livre  ·  Tolkien vers J.R.R. Tolkien
~ (..., D:0, "hobbit") = { "title": "The Hobbit", "author": -"Tolkien" +"J.R.R. Tolkien" }
- (..., I:1, "Tolkien", "hobbit") = ''
+ (..., I:1, "J.R.R. Tolkien", "hobbit") = ''
```

L'exemple propose trois variantes de mise à jour, qui échangent une lecture contre des obligations
pour l'appelant :

| Méthode | Lit l'ancien doc ? | À utiliser quand |
|---|---|---|
| `UpdateAsync(tr, book)` | oui | l'appelant a construit un `Book` neuf et ne détient pas l'original |
| `UpdateAsync(tr, updated, original)` | non | l'appelant a déjà lu `original` **dans la même transaction** (sa lecture fournit le conflit qui garde l'index cohérent ; passer un `original` périmé corrompt l'index) |
| `PatchAsync(tr, id, patch)` | oui | mises à jour de champs peu coûteuses ; un patch sans effet (`updated == current`, par égalité de valeur du *record*) n'écrit rien |

## Rendre les clés d'un *layer* lisibles

Les clés brutes sont des octets opaques. Les outils (le *shell* FQL, `FdbShell`, les *dumps*, le
*logger* de transactions) peuvent les afficher comme des tuples lisibles si le *layer* publie un
schéma. Implémentez `IFdbLayerSchemaMapper` (souvent comme classe imbriquée) et renvoyez une
`FqlTemplateExpression` par famille de clés :

```csharp
public sealed class SchemaMapper : IFdbLayerSchemaMapper
{
    public string LayerId => "docstore.Books";
    public IEnumerable<FqlTemplateExpression> GetRules()
    {
        yield return new("document",
            FqlTupleExpression.Create().Integer(SUBSPACE_DOCUMENTS, "D").VarString("id"),
            FdbValueTypeHint.Json);
        yield return new("index.author",
            FqlTupleExpression.Create().Integer(SUBSPACE_INDEX_AUTHOR, "I").VarString("author").VarString("id"),
            FdbValueTypeHint.None);
    }
}
```

Une fois ce schéma publié, une clé brute cesse d'être des octets opaques et se lit comme un tuple
lisible. Les deux familles s'affichent ainsi (`D`/`I` sont les noms d'affichage des *subspaces*
`0`/`1`, et `...` représente le préfixe Directory résolu) :

```fdb-fql
// un document livre
(..., D:0, <id:string>) = <json>

// entrée d'index par auteur (valeur vide)
(..., I:1, <author:string>, <id:string>) = ''
```

Le *hint* de valeur peut aussi être une **fonction de la clé décodée**
(`(SpanTuple t) => t.Get<string>(0) switch { … }`) quand le type de la valeur dépend de la clé.

## Migrer depuis l'ancienne API de clés dynamiques

L'ancien code utilisait une API de *subspace* dynamique (`IDynamicKeySubspace`,
`subspace.Encode(...)` / `.Pack(...)`), remplacée par la famille typée `subspace.Key(...)`. Traduisez
mécaniquement :

| Ancien (dynamique) | Nouveau (typé) |
|---|---|
| `subspace.Encode(a, b, c)` | `subspace.Key(a, b, c)` |
| `subspace.Pack(STuple.Create(a, b).Concat(value))` | `subspace.Key(a, b).Tuple(value)` |
| `subspace.EncodeRange(a, b)` | `subspace.Key(a, b).ToRange()` |
| `global.Partition.ByKey(p)` | `global.Key(p).ToSubspace()` |
| type de champ/retour `IDynamicKeySubspace` | `IKeySubspace` |

## Des *layers* de référence à imiter

En cas de doute, lisez les vraies implémentations dans `FoundationDB.Layers.Common/` : `FdbMap`
(clé→valeur), `FdbIndex` (clés composites `(value, id)`), `FdbVector` (clés d'index entières),
`FdbHighContentionCounter` (évitement de la contention en écriture), `FdbBlob` (découpage des grandes
valeurs), `FdbStringIntern` (*maps* bidirectionnelles).

Ensuite : **[Transactions](../transactions/index.md)** pour la sémantique de *retry loop* dans
laquelle ces *layers* s'exécutent.
