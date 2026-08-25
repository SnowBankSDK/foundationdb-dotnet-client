# Les bases des transactions

Une transaction est le type qui vous permet d'interagir avec la base de données, pour lire la valeur des clés et/ou les modifier. Cette page est la référence bas niveau de l'API de transaction ; pour les règles pratiques d'un bon usage des transactions (idempotence, la limite de 5 secondes, les conflits, les opérations atomiques, les *watches*), lisez le [guide Transactions](index.md) et ses [recettes how-to](how-to.md).

Il existe deux sortes de transactions :

- Les transactions **en lecture seule** ne peuvent que lire et n'ont jamais besoin d'un *commit*. Elles obtiennent une *read version* du *cluster* et effectuent toutes leurs lectures à cet instant précis.
- Les transactions **en lecture/écriture** peuvent aussi modifier la base de données au moment du *commit*. Si une clé qu'elles ont lue a été modifiée entre-temps par une autre transaction, leur *commit* échoue sur un conflit et elles doivent être rejouées.

Elles sont exposées via `IFdbReadOnlyTransaction` et `IFdbTransaction` (qui l'étend), de sorte que le compilateur vous aide à rester dans ce que chacune autorise.

## Les transactions manuelles

Vous *pouvez* créer et gérer les transactions à la main. Notez que `BeginTransaction`/`BeginReadOnlyTransaction` sont synchrones, et que `Set`/`Clear` préparent les mutations localement ; rien n'est durable tant que `CommitAsync()` n'a pas réussi :

```csharp
CancellationToken ct = /* ... */;

// transaction en lecture seule
using (IFdbReadOnlyTransaction tr = db.BeginReadOnlyTransaction(ct))
{
    Slice value1 = await tr.GetAsync(key1);
    Slice value2 = await tr.GetAsync(key2);
    var values = await tr.GetRange(beginInclusive, endExclusive).ToListAsync();
}

// transaction en lecture/écriture
using (IFdbTransaction tr = db.BeginTransaction(ct))
{
    Slice value1 = await tr.GetAsync(key1);   // on peut lire
    tr.Set(key2, value2);                     // prépare une écriture
    tr.Clear(key3);                           // prépare une suppression
    tr.ClearRange(beginInclusive, endExclusive); // ou supprime une plage

    await tr.CommitAsync();                   // rien ne change dans la base de données tant que ceci n'a pas réussi
}
```

**N'écrivez pas votre code applicatif de cette façon.** Gérer vous-même la durée de vie de la transaction, et décider quelles erreurs sont rejouables et pendant combien de temps, est source d'erreurs :

- Les conflits entre transactions sont un résultat normal et attendu pour beaucoup d'algorithmes ; quand ils surviennent, la transaction doit être rejouée.
- Beaucoup d'erreurs transitoires empêchent temporairement un *commit* mais réussiraient à la tentative suivante.

## Les *retry loops* (à utiliser)

Chaque `IFdbDatabase` fournit des helpers de *retry loop* qui gèrent tout ce qui précède à votre place : `ReadAsync`, `WriteAsync` et `ReadWriteAsync`.

- `ReadAsync` : transactions en lecture seule ; toute tentative d'écriture *throw*.
- `WriteAsync` : transactions en lecture/écriture qui ne renvoient pas de résultat (une opération de type `void`).
- `ReadWriteAsync` : transactions en lecture/écriture qui renvoient un résultat.

Le *handler* que vous passez est exécuté **au moins une fois** ; seul le résultat de la dernière itération (celle qui réussit) est renvoyé. Sur une erreur rejouable, la transaction est réinitialisée et le *handler* s'exécute à nouveau ; sur une erreur non rejouable, la boucle s'interrompt et relance l'exception. Chaque boucle prend un `CancellationToken` fourni par l'appelant, souvent le seul moyen d'abandonner une transaction bloquée sur une panne.

```csharp
CancellationToken ct = /* ... */;

// lit la valeur d'une clé
Slice result1 = await db.ReadAsync(tr => tr.GetAsync(key1), ct);

// modifie une clé (pas de résultat)
await db.WriteAsync(tr => tr.Set(key1, value1), ct);

// lit une clé et en modifie une autre, en renvoyant une valeur
Slice result2 = await db.ReadWriteAsync(tr =>
{
    tr.Set(key2, value2);
    return tr.GetAsync(key1);
}, ct);
```

> Le *handler* peut s'exécuter plusieurs fois, donc il **ne doit pas modifier d'état en dehors de la base de données** (pas de caches, de compteurs, de *logging* ni de *messaging* à l'intérieur de la lambda). Faites ce travail après le retour de la boucle. Ce point et le reste des règles sont couverts dans le [guide Transactions](index.md).

**Pourquoi à la fois `WriteAsync` et `ReadWriteAsync` ?** C'est une limite de la résolution de types en C# : les *handlers* « en écriture seule » renvoient `Task` et les *handlers* « en lecture/écriture » renvoient `Task<T>` (transtypable en `Task`), ce qui crée une ambiguïté de surcharge. Des noms distincts l'évitent. Par convention, tout ce qui est nommé `Read…` renvoie une valeur.
