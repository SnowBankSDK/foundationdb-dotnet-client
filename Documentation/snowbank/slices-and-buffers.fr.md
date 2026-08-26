# Données binaires : Slice et *buffers*

Presque tout dans cette *stack* finit par devenir des octets : les clés, les valeurs, les éléments encodés en tuple. Le type que vous utilisez pour contenir et manipuler ces octets est **`Slice`**, et ce guide explique comment l'utiliser correctement (avec ses compagnons `SliceReader`, `SliceWriter`, `SliceOwner`). C'est la fondation sous [Clés, valeurs et *Layers*](../fdb/guide/keys-and-layers/index.md).

## Ce qu'est `Slice`

`Slice` est un **`readonly struct`** (dans le *namespace* `System`) qui encapsule un segment de `byte[]` (trois champs : le tableau sous-jacent `Array`, un `Offset` et un `Count`). Il précède `Span<T>` et est l'équivalent logique de **`ReadOnlyMemory<byte>`**, mais porte une grande bibliothèque d'*helpers* pour convertir des octets vers et depuis de vrais types.

Deux propriétés déterminent tout le reste :

- **Un `Slice` est une vue, pas une copie.** En créer un depuis un `byte[]` partage le tableau ; muter le tableau est visible à travers le *slice* (et son `.Span`). Quand vous devez posséder les octets, copiez avec `.ToArray()` (ou `.ToSliceOwner()` pour une copie poolée).
- **`Slice.Nil` et `Slice.Empty` sont différents**, et la différence compte.

## Nil ou Empty

`Slice.Nil` n'a **aucun** tableau sous-jacent (comme un null) ; `Slice.Empty` a un tableau de **longueur zéro**.

| | `Slice.Nil` | `Slice.Empty` |
|---|---|---|
| `IsNull` | `true` | `false` |
| `IsEmpty` | `false` | `true` |
| `IsNullOrEmpty` | `true` | `true` |
| `IsPresent` | `false` | `true` |
| `GetBytes()` | `null` | tableau vide |
| `ToStringUtf8()` | `null` | `""` |
| `==` | distincts (`Nil != Empty`) | |
| `CompareTo` | égaux (les deux se trient en premier) | |

`tr.GetAsync(key)` renvoie **`Slice.Nil`** quand une clé n'existe pas, donc le test idiomatique « est-ce qu'elle existe ? » est `value.IsNull` :

```csharp
var v = await tr.GetAsync(key);
if (v.IsNull) { /* clé introuvable */ }
```

Utilisez `Nil` pour dire *absent* et `Empty` pour dire *présent mais vide*.

## Construire un Slice

```csharp
byte[] b = ...;
b.AsSlice();                 b.AsSlice(offset, count);     // vues sur un tableau
Slice.FromBytes("abc"u8);                                 // copie un ReadOnlySpan<byte>
Slice.FromStringUtf8("héllo");   Slice.FromString("x");   // UTF-8
Slice.FromStringAscii("ABC");                             // ASCII seulement (avec perte / throw si > 0x7F)
Slice.Empty;   Slice.Nil;   Slice.Zero(16);
Slice.FromGuid(g);   Slice.FromUuid128(u);   Slice.FromHexString("00ff");
```

### Trois encodages d'entiers : à choisir délibérément

Trois encodages sont faciles à confondre. Sur un `Slice` autonome :

| Factory | Encodage | taille int32 |
|---|---|---|
| `Slice.FromInt32(v)` | *little-endian* minimal (supprime les zéros de tête) | 1 à 4 octets |
| `Slice.FromFixed32(v)` | *little-endian* fixe | toujours 4 |
| `Slice.FromVarint32(v)` | *varint* LEB128 7 bits | 1 à 5 |

Chacun a un jumeau *big-endian* (`…BE`) ; **le *big-endian* fixe** est celui qui se trie correctement comme clé. Relisez avec `slice.ToInt32()` / `ToInt32BE()` etc.

> **Attention :** les `SliceWriter`/`SliceReader` en *streaming* les nomment différemment : là, la méthode à largeur fixe est simplement `WriteInt32`/`ReadInt32` (4 octets LE) et le *varint* est `WriteVarInt32`/`ReadVarInt32`. (`WriteFixed32`/`ReadFixed32` sont des alias obsolètes.)

## Lire les valeurs et découper

```csharp
slice.ToInt64();   slice.ToGuid();   slice.ToStringUtf8();   slice.ToArray();
ReadOnlySpan<byte> span = slice.Span;       // zéro-copie
ReadOnlyMemory<byte> mem = slice.Memory;
slice.Substring(7, 6);   slice[2..5];   slice[^1..];   // indexation négative / par Range
```

## Comparaison

`Slice` ordonne **lexicographiquement par octets bruts** (le même ordre dans lequel FoundationDB trie les clés) et est indépendant de l'*offset* (un contenu égal est égal quel que soit le tableau sous-jacent). Il supporte `==`, `<`, `>`, `CompareTo`, `StartsWith`, `EndsWith`, `IndexOf`, et `Slice.Comparer.Default` pour les dictionnaires et ensembles triés.

## Construire et parser : SliceWriter / SliceReader

`SliceWriter` est un *builder* extensible ; `SliceReader` est un curseur unidirectionnel. Associez chaque écriture à la lecture correspondante, et préférez des écritures **auto-délimitées** (largeur fixe, *varint*, ou chaîne préfixée par sa longueur) pour tout ce qui est relu séquentiellement :

```csharp
var w = new SliceWriter();
w.WriteInt32(order.Id);            // 4 octets fixes LE
w.WriteVarString(order.Customer);  // UTF-8 préfixé par sa longueur
w.WriteVarInt64(order.Total);
Slice packed = w.ToSlice();

var r = packed.ToSliceReader();
int id      = r.ReadInt32();
string cust = r.ReadVarString();
long total  = (long) r.ReadVarInt64();
```

`ToSlice()` renvoie une *vue sur le buffer du writer* ; copiez-la (`ToArray()`/`ToSliceOwner()`) si elle doit survivre au *writer*. Il n'y a pas de `ReadStringUtf8(n)`. Pour une chaîne brute (sans préfixe) de longueur connue, utilisez `r.ReadBytes(n).ToStringUtf8()`.

## *Pooling* : SliceOwner et ArrayPool

Pour rester sans allocation, louez des *buffers*. Un `SliceWriter` construit avec un `ArrayPool<byte>` doit être disposé ou cédé via `ToSliceOwner()`. `SliceOwner` est un `Slice` loué qui rend son *buffer* au *pool* sur `Dispose` ; **vous devez le disposer et ne devez pas utiliser ses données ensuite** :

```csharp
using (var owner = Slice.FromBytes(payload, ArrayPool<byte>.Shared))
{
    Use(owner.Data.Span);   // valide seulement à l'intérieur du using
}   // buffer rendu au pool ici
```

## *Interop* moderne et `ISpanEncodable`

`Slice` se convertit librement vers et depuis `ReadOnlySpan<byte>` (`.Span`), `ReadOnlyMemory<byte>` (`.Memory`), et `byte[]` (`.AsSlice()`). Les types du *hot path* (clés, valeurs, les *writers*) implémentent **`ISpanEncodable`** (`TryGetSpan` / `TryGetSizeHint` / `TryEncode`) pour pouvoir être écrits dans le *buffer* de l'appelant sans `Slice` intermédiaire. C'est ainsi que `subspace.Key(...)`/`FdbValue.*` se rendent dans des *buffers* poolés au dernier moment.

## Descendre plus bas niveau

Pour le code sensible aux performances, il y a plus :

- **`SpanReader` / `SpanWriter`** : des *readers*/*writers* `ref struct` qui travaillent directement sur un `Span<byte>` détenu par l'appelant (un `stackalloc` ou un *buffer* loué), avec zéro allocation. Utilisez-les quand vous détenez déjà un *buffer* de taille fixe et que le travail reste sur la *stack* ; utilisez ceux basés sur `Slice` quand vous devez agrandir ou céder le résultat.
- **`ISliceBufferWriter`** (`ArraySliceWriter` contigu, `SlabSliceWriter` basé sur des *slabs*, `PooledSliceWriter`) : des implémentations de `IBufferWriter<byte>` qui fournissent aussi des `Slice` ; se branchent sur des API comme `Utf8JsonWriter`.
- **`ISliceAllocator`** (`ArraySliceAllocator` / `PooledSliceAllocator`) : sous-allouent beaucoup de *slices* de courte durée depuis des *slabs* partagés (une arène par requête). (L'ancien `SlicePool` est obsolète.)
- **`ValueBuffer<T>` / `SegmentedValueBuffer<T>` / `PooledBuffer<T>`** : des accumulateurs de type valeur que vous pouvez initialiser avec de la mémoire de *stack*, pour collecter un nombre inconnu d'éléments sans allocation sur le *heap*.

Ils sont documentés en profondeur pour les agents dans les fichiers de référence du *skill* `snowbank-slices-and-buffers` ; n'y recourez que quand le profilage montre que ça en vaut la peine. Le code de tous les jours s'en sort très bien avec `Slice` + `SliceWriter`/`SliceReader` + `SliceOwner`.
