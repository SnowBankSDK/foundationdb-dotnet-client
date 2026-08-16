# Prise en main

Cette page vous mène d'un *cluster* en fonctionnement à vos premières lecture et écriture. Elle suppose que vous avez déjà :

- installé les [prérequis](prerequisites.md) (.NET 10, plus Docker si vous avez besoin d'un *cluster* local), et
- obtenu un *cluster* et sa chaîne de connexion depuis [Installer un cluster](cluster-setup.md).

Si vous avez sauté ces étapes, commencez par là. Les exemples ci-dessous utilisent le *cluster* Docker local de la page Installer un cluster (`docker:docker@127.0.0.1:4500`, FoundationDB 7.4). Vous utilisez plutôt votre propre *cluster* ? Remplacez la chaîne de connexion et alignez le niveau d'API (voir [Installer un cluster](cluster-setup.md)).

> Des versions exécutables des exemples de cette page se trouvent dans [`samples/getting-started/`](../samples/getting-started/).

## 1. Installer les *packages*

Installez le *binding* managé (utilisez toujours le plus récent) :

```console
dotnet add package FoundationDB.Client
```

Ensuite, installez le client natif, **épinglé à la version `major.minor` de votre *cluster***. C'est l'étape qui piège tout le monde : le *package* natif fixe le *wire protocol* et doit correspondre à votre *cluster*, sinon rien ne se connecte (voir [Fonctionnement](foundationdb-101.md)).

Pour un *cluster* **7.4** (y compris celui de Docker local, d'[Installer un cluster](cluster-setup.md)) :

```console
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

Pour un *cluster* **7.3** :

```console
dotnet add package FoundationDB.Client.Native --version "7.3.*"
```

`7.4.*` se résout vers la dernière version `7.4.x` publiée (et `7.3.*` vers la dernière `7.3.x`), donc vous obtenez le dernier correctif pour la version de votre *cluster* sans épingler un *build* précis. Si vous ne connaissez pas la version de votre *cluster*, [Installer un cluster](cluster-setup.md) montre comment la trouver. Installer le mauvais est la raison la plus fréquente pour laquelle le tutoriel ci-dessous « se connecte » mais expire ensuite à chaque opération.

> Le client natif de FoundationDB fonctionne **uniquement en 64 bits**, donc votre processus doit s'exécuter en 64 bits (le cas par défaut sur les *runtimes* modernes).

## 2. Ça marche : se connecter et lire la version

Commencez par une application console. C'est le plus petit programme qui prouve que votre installation fonctionne de bout en bout : il se connecte et demande au *cluster* sa *read version* actuelle.

Une application console a aussi besoin du *package* du conteneur d'injection de dépendances (une application web, plus tard, l'obtient via le SDK web) :

```console
dotnet add package Microsoft.Extensions.DependencyInjection
```

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Enregistre FoundationDB dans le conteneur DI. 740 est le niveau d'API : gardez-le au niveau
// de la version de votre cluster, ou en dessous (voir « Fonctionnement »).
services.AddFoundationDb(740, options =>
{
    // Les coordinateurs auxquels se connecter. C'est la chaîne de connexion provenant de « Installer un cluster ».
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

using var provider = services.BuildServiceProvider();

// Le seul service que vous utilisez pour parler à la base de données. Dans une vraie application, vous l'injectez au lieu de le résoudre à la main.
var db = provider.GetRequiredService<IFdbDatabaseProvider>();

// ReadAsync exécute le lambda dans une transaction de lecture et le réessaie en cas de conflit.
// GetReadVersionAsync est un aller-retour peu coûteux vers le cluster, donc une valeur ici signifie que la connexion fonctionne.
long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), CancellationToken.None);
Console.WriteLine($"Connected. Cluster read version = {readVersion}");
```

Vous devriez voir une ligne comme :

```
Connected. Cluster read version = 143602331405
```

C'est votre moment « ça marche » : la *read version* est une valeur en direct du *cluster*, donc une vraie connexion a eu lieu.

Si, au lieu de ça, **le programme reste bloqué quelques secondes puis expire**, votre application ne parle pas au *cluster*. La cause habituelle est une incompatibilité de version entre le *package* natif et le *cluster* : vérifiez [Fonctionnement](foundationdb-101.md), et confirmez la chaîne de connexion depuis [Installer un cluster](cluster-setup.md).

## 3. Vos premières lecture et écriture

Passez toujours par la ***retry loop*** (`ReadAsync` / `WriteAsync`) plutôt que de gérer les transactions à la main : elle gère pour vous le modèle de conflits et de *retry* de FoundationDB. On continue avec le même `db` :

```csharp
// Une location de répertoire : un chemin lisible que le Directory layer associe à un court
// préfixe de clé binaire. Rien n'est encore stocké ; vous la résolvez dans chaque transaction.
var location = db.Root["Examples"]["Hello"];

// WriteAsync exécute le lambda dans une transaction de lecture-écriture et la valide, en réessayant en cas de conflit.
await db.WriteAsync(async tr =>
{
    // Crée le répertoire la première fois, ou l'ouvre s'il existe déjà, à l'intérieur de la
    // transaction. (Resolve, utilisé pour la lecture ci-dessous, ouvre seulement un répertoire existant et
    // lève une exception s'il est absent.) Ne mettez pas le subspace en cache.
    var subspace = await location.CreateOrOpenAsync(tr);

    // subspace.Key(...) construit une clé encodée en tuple sous le préfixe, et FdbValue.ToTextUtf8
    // encode la valeur. Passez les deux directement à la transaction ; aucun travail d'octets à la main.
    tr.Set(subspace.Key("greeting"), FdbValue.ToTextUtf8("Hello, World!"));
}, CancellationToken.None);

string? greeting = await db.ReadAsync(async tr =>
{
    // résout cette location vers son subspace de clés
    var subspace = await location.Resolve(tr);

    // Lit une clé. ToStringUtf8() renvoie null si la clé est absente (un slice nil).
    var value = await tr.GetAsync(subspace.Key("greeting"));
    return value.ToStringUtf8();
}, CancellationToken.None);

Console.WriteLine($"Read back: {greeting}");
```

Trois choses à remarquer, toutes expliquées dans le [Guide](guide/keys-and-layers/index.md) :

- Vous construisez la clé avec **`subspace.Key("greeting")`** et passez cet objet directement à la transaction. Aucun encodage d'octets à la main.
- Vous **résolvez le *subspace* dans la transaction** (`location.Resolve(tr)`) et ne mettez jamais le préfixe en cache.
- Le lambda que vous passez à `ReadAsync` / `WriteAsync` **peut s'exécuter plusieurs fois**, donc il ne doit pas modifier d'état en dehors de la base de données.

Stocker les données sous `db.Root["Examples"]["Hello"]` plutôt qu'à une clé brute est un choix délibéré : le *Directory layer* garde votre *keyspace* propre et sans collision, au lieu d'éparpiller des clés isolées à la racine. C'est le premier pas vers le fait de « penser en *Layers* », abordé juste après.

> **Résistez à l'envie de construire les clés à la main.** Ne formatez pas les clés comme des `byte[]` bruts, des chaînes encodées en UTF-8, ou des `Slice` assemblés à la main. Composez-les toujours à partir de *subspaces* de répertoire, de tuples, et des *helpers* de clés typés (`subspace.Key(...)`), qui gèrent correctement pour vous l'ordre et l'échappement. Le guide [Clés, valeurs et *Layers*](guide/keys-and-layers/index.md) explique pourquoi c'est important et comment fonctionne l'encodage.

## 4. Une petite API HTTP que vous pouvez parcourir en cliquant

Comme la base de données est un *singleton* `IFdbDatabaseProvider` dans la DI, vous pouvez l'injecter dans des *endpoints*. Transformons le message d'accueil en une petite collection que vous pouvez créer, lister et supprimer via HTTP, avec une UI interactive pour l'essayer sans écrire de client.

Deux *packages* supplémentaires vous donnent cette UI : le générateur de document OpenAPI intégré, et [Scalar](https://github.com/scalar/scalar), qui le rend sous forme de tableau de bord cliquable.

```console
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
```

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFoundationDb(740, options =>
{
    options.ConnectionOptions.ConnectionString = "docker:docker@127.0.0.1:4500";
});

// décrit les endpoints au format OpenAPI
builder.Services.AddOpenApi();

var app = builder.Build();

// Crée le répertoire de démo une fois au démarrage, pour que chaque endpoint ci-dessous puisse simplement le résoudre.
{
    var startup = app.Services.GetRequiredService<IFdbDatabaseProvider>();
    await startup.WriteAsync(tr => startup.Root["Examples"]["Greetings"].CreateOrOpenAsync(tr), CancellationToken.None);
}

// sert le document OpenAPI à /openapi/v1.json
app.MapOpenApi();

// le rend sous forme d'UI cliquable à /scalar/v1
app.MapScalarApiReference();

// Liveness : prouve que l'application peut atteindre le cluster.
app.MapGet("/readversion", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
    return Results.Ok(new { readVersion });
});

app.MapPost("/greetings", async (NewGreeting input, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    // Génère l'id EN DEHORS de la retry loop, pour qu'un retry réutilise le même id.
    var id = Guid.NewGuid().ToString("N");
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        // résout le subspace où sont stockées les clés de la collection Greetings
        var subspace = await location.Resolve(tr);
        tr.Set(subspace.Key(id), FdbValue.ToTextUtf8(input.Text));
    }, ct);
    return Results.Created($"/greetings/{id}", new Greeting(id, input.Text));
});

app.MapGet("/greetings", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    var greetings = await db.ReadAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);

        // parcourt tout le subspace : redécode chaque clé en son id, lit le texte depuis la
        // valeur, et ToListAsync() rassemble les correspondances dans une List
        return await tr.GetRange(subspace.ToRange())
            .Select(kv => new Greeting(subspace.DecodeLast<string>(kv.Key)!, kv.Value.ToStringUtf8()!))
            .ToListAsync();
    }, ct);
    return Results.Ok(greetings);
});

app.MapDelete("/greetings/{id}", async (string id, IFdbDatabaseProvider db, CancellationToken ct) =>
{
    var location = db.Root["Examples"]["Greetings"];
    await db.WriteAsync(async tr =>
    {
        var subspace = await location.Resolve(tr);
        // supprime une seule clé
        tr.Clear(subspace.Key(id));
    }, ct);
    return Results.NoContent();
});

app.Run();

record NewGreeting(string Text);
record Greeting(string Id, string Text);
```

Lancez l'application et ouvrez **`/scalar/v1`** dans votre navigateur. Vous obtenez un tableau de bord qui liste chaque *endpoint* : faites un `POST` d'un message d'accueil ou deux, `GET /greetings` pour les voir revenir de FoundationDB, puis `DELETE` de l'un d'eux par son id. Pas de `curl`, pas de client séparé.

## Où aller ensuite

Vous avez maintenant une connexion qui fonctionne, une lecture, une écriture, et un *endpoint* HTTP. Deux directions à partir d'ici :

- **Pensez en *Layers*.** [Clés, valeurs et *Layers*](guide/keys-and-layers/index.md) est la chose la plus importante à apprendre ensuite : comment les clés sont encodées en tuple, comment les *subspaces* et le *Directory layer* organisent le *keyspace*, et comment empaqueter l'accès aux données dans un *Layer* réutilisable. Ensuite [Transactions](guide/transactions/index.md) pour la *retry loop*, les conflits, et les opérations atomiques.
- **Une configuration façon production.** Laissez .NET Aspire démarrer le *cluster* et injecter la connexion pour vous, aux côtés d'un vrai *backend* d'API. Voir [Aspire](aspire/index.md).
