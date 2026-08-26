# Guides pratiques des transactions

Chaque section ici traite une tâche sur une transaction FoundationDB. Elles supposent que vous avez déjà un `IFdbDatabase` ou un `IFdbDatabaseProvider`, tel que [Prise en main](../../getting-started.md) le met en place. Pour l'API de transaction bas niveau, voir la [référence](reference.md) ; pour comprendre pourquoi la *retry loop* réessaie et pourquoi un *handler* doit être idempotent, voir [l'explication](index.md).

## Exécuter la *retry loop*

N'appelez pas `BeginTransaction` et `CommitAsync` à la main dans le code applicatif. Utilisez les méthodes réessayables de `IFdbDatabase` (ou `IFdbDatabaseProvider`), et choisissez la plus étroite pour la tâche :

| Méthode | Transaction | Utilisation |
|---|---|---|
| `db.ReadAsync(handler, ct)` | `IFdbReadOnlyTransaction` | lectures seules ; retourne un résultat |
| `db.WriteAsync(handler, ct)` | `IFdbTransaction` | mutations qui ne retournent **rien** (le *handler* peut quand même lire) |
| `db.ReadWriteAsync(handler, ct)` | `IFdbTransaction` | mutations qui doivent **retourner une valeur** |

```csharp
// LECTURE
Book? book = await db.ReadAsync(async tr =>
{
    var bytes = await tr.GetAsync(subspace.Key("D", id));
    return CrystalJson.Deserialize<Book>(bytes);   // vide/absent -> null
}, ct);

// ÉCRITURE (rien à retourner)
await db.WriteAsync(tr => tr.Set(subspace.Key("D", book.Id), FdbValue.ToJson(book)), ct);

// READ-MODIFY-WRITE (résultat nécessaire)
long balance = await db.ReadWriteAsync(async tr =>
{
    long current = (await tr.GetAsync(accountKey)).ToInt64();
    long updated = current + amount;
    tr.Set(accountKey, FdbValue.ToFixed64LittleEndian(updated));
    return updated;
}, ct);
```

La *retry loop* fait le *commit* pour vous, donc n'appelez jamais `CommitAsync` dans le *handler*. `WriteAsync` et `ReadWriteAsync` vous donnent toutes deux une transaction complète en lecture/écriture ; la différence porte seulement sur le fait de retourner une valeur, pas sur le fait de lire. Gardez tout effet de bord externe hors du *handler* : il peut s'exécuter plus d'une fois, et les écritures de la tentative précédente sont jetées lors d'un *retry*, donc ne touchez aux caches, aux compteurs et aux logs qu'après le retour de la *retry loop* (voir [pourquoi le handler doit être idempotent](index.md#your-handler-must-be-idempotent)). N'attrapez pas dans le *handler* les codes `FdbException` réessayables comme un conflit ; la *retry loop* s'en charge. Levez votre propre exception hors du *handler* pour une vraie erreur applicative : elle annule la transaction et se propage, sans *commit* et sans *retry*.

## Incrémenter un compteur avec des mutations atomiques

Une mutation atomique change une valeur sur le *cluster* sans la lire d'abord, donc elle n'ajoute rien au *read set* et n'entre jamais en conflit avec une autre mutation atomique, même sous forte contention. Préférez-en une à un *read-modify-write* dès que la nouvelle valeur est fonction de l'ancienne :

```csharp
tr.AtomicAdd64(counterKey, +1);            // valeur stockée en little-endian 64 bits fixe
tr.AtomicIncrement64(counterKey);
tr.AtomicDecrement64(counterKey, clearIfZero: true);
tr.AtomicMax(key, v); tr.AtomicMin(key, v); tr.AtomicAnd/Or/Xor(key, mask);
```

Une écriture atomique évite le conflit de lecture, mais une clé écrite fréquemment sérialise quand même ses écritures au niveau du *resolver* ; la [recette de *sharding*](#spread-a-write-hot-key-across-shards) répartit cette charge.

## Lire des données périmées avec une lecture *snapshot*

Une lecture *snapshot* retourne la valeur d'une clé sans l'ajouter au *read set* de la transaction, donc une écriture concurrente sur cette clé ne met pas cette transaction en conflit. Utilisez `tr.Snapshot.GetAsync` ou `tr.Snapshot.GetRange` quand une valeur légèrement périmée est acceptable, par exemple pour compter des *shards* ou rassembler des statistiques.

## Répartir une clé *write-hot* sur plusieurs *shards*

Une même clé écrite par de nombreuses transactions les sérialise toutes au niveau du *resolver*. Répartissez les écritures sur N sous-clés et additionnez-les à la lecture. `FdbHighContentionCounter` implémente ce *pattern*.

## Surveiller une clé pour détecter ses changements

`tr.Watch(key, ct)` retourne un `FdbWatch` qui se termine quand la valeur de la clé change après le *commit* de la transaction. Créez-le dans une transaction et faites-en le `await` **à l'extérieur** :

```csharp
FdbWatch watch = await db.ReadWriteAsync(async tr => tr.Watch(signalKey, ct), ct);
await watch;   // se résout quand signalKey change
```

- Passez un `CancellationToken` **applicatif/externe** à `Watch`, **pas** `tr.Cancellation` : le *watch* survit à la transaction.
- Un *watch* **notifie** seulement que la clé a changé ; il ne livre pas la nouvelle valeur. Quand il se déclenche, relisez.
- Les *watches* sont limités par base de données, donc utilisez-les pour des signaux à basse fréquence, pas pour du *streaming* à haut débit.

L'usage canonique est un **fan-out sur une clé de signal** : un producteur incrémente une unique clé surveillée avec `AtomicIncrement` dans la même transaction que son écriture de données ; les consommateurs surveillent cette clé et relisent quand elle se déclenche. C'est l'ossature du *pub/sub* et des *change feeds* (voir [*Layers* avancés](../advanced-layers/index.md)).

## Paginer une grande plage sur plusieurs transactions

Un *range scan* qui risque d'être volumineux ne doit pas s'exécuter en une seule longue lecture : il atteindra la limite des cinq secondes et échouera avec `transaction_too_old`. Paginez-le sur plusieurs transactions, en reprenant chacune à partir du `Successor()` de la dernière clé. Pour un import ou export en masse, utilisez les *helpers* `Fdb.Bulk.*`, qui gèrent le *batching* et la fenêtre de temps pour vous.

## Composer plusieurs *Layers* dans une seule transaction

Un *Layer* résout son `State` propre à la transaction dans le *handler* et l'y utilise. Résoudre plusieurs *Layers* dans le même *handler* les fait committer ensemble, ou pas du tout :

```csharp
await db.WriteAsync(async tr =>
{
    var books   = await bookStore.Resolve(tr);
    var counter = await statsCounter.Resolve(tr);
    books.Insert(tr, book);
    counter.Add(tr, 1);          // commit atomique des deux, ou d'aucun
}, ct);
```

Quand `Resolve` a besoin d'un argument, le plus souvent un *tenant*, implémentez `IFdbLayer<TState, TOptions>`, dont `Resolve(tr, options)` et les *helpers* de *retry loop* prennent cet argument. Pour savoir comment écrire un *Layer*, voir [Clés et *Layers*](../keys-and-layers/index.md).
