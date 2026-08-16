# Démarrer avec Aspire

Cette page démarre un cluster FoundationDB local avec .NET Aspire et y connecte un service. À la fin,
le service lit une valeur en direct depuis le cluster au démarrage, et le premier lancement configure
la database pour vous. Dix minutes, un seul chemin. Pour la conception derrière les deux packages et
le flux de connexion, lisez [l'explication](index.fr.md) ; pour des tâches ponctuelles comme se
connecter à un cluster existant ou épingler une version, voyez les [guides pratiques](how-to.fr.md) ;
pour les tables de paramètres complètes, voyez la [référence](reference.fr.md).

Cette page suppose que vous avez déjà une solution Aspire avec un projet AppHost (`Acme.AppHost`) et un
projet de service (`Acme.Backend`). Sinon, créez-en une d'abord avec les
[templates Aspire](https://aspire.dev/), puis revenez. Vous avez aussi besoin de Docker en cours
d'exécution, car l'AppHost démarre le cluster dans un container.

## 1. Installer les packages du SDK

Le package host va dans l'AppHost, le package client dans le service. Les deux portent la version de la
bibliothèque :

```console
# dans Acme.AppHost
dotnet add package FoundationDB.Aspire.Hosting

# dans Acme.Backend
dotnet add package FoundationDB.Aspire
```

Ou ajoutez les références directement dans vos fichiers projet :

```xml
<!-- Acme.AppHost.csproj -->
<ItemGroup>
  <!-- autres packages -->
  <PackageReference Include="FoundationDB.Aspire.Hosting" Version="7.4.2" />
</ItemGroup>

<!-- Acme.Backend.csproj -->
<ItemGroup>
  <!-- autres packages -->
  <PackageReference Include="FoundationDB.Aspire" Version="7.4.2" />
</ItemGroup>
```

Le client natif (`FoundationDB.Client.Native`) arrive à l'étape 3, une fois la version du cluster
choisie, car sa version suit le cluster et non ces packages.

## 2. Déclarer le cluster dans l'AppHost

Dans `Acme.AppHost/Program.cs`, ajoutez le cluster et donnez au backend une référence vers celui-ci :

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Démarre un cluster FoundationDB local à un seul nœud dans Docker.
// apiVersion est le niveau d'API que les services demandent ; clusterVersion est le tag de l'image Docker ;
// root est le chemin directory-layer sous lequel les services résolvent leurs clés.
var fdb = builder.AddFoundationDb("fdb",
    apiVersion: 740,
    root: "/Sandbox/Acme",
    clusterVersion: "7.4.6",
    rollForward: FdbVersionPolicy.Exact);

// Donne au backend une référence vers le cluster, et ne le démarre qu'une fois le cluster sain.
builder.AddProject<Projects.Acme_Backend>("backend")
    .WithReference(fdb)
    .WaitFor(fdb);

builder.Build().Run();
```

`AddFoundationDb` exécute le cluster comme un container Docker. `WithReference(fdb)` passe la connection
string au backend sous le nom de ressource (`"fdb"`), et `WaitFor(fdb)` retient le backend jusqu'à ce
que le cluster se signale sain.

## 3. Installer le client natif pour correspondre au cluster

Le cluster que vous avez déclaré est un 7.4 (`clusterVersion: "7.4.6"`, `apiVersion: 740`). Installez le
client natif au même `major.minor` dans le service :

```console
# dans Acme.Backend
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

Ou ajoutez la référence dans le fichier projet du service :

```xml
<!-- Acme.Backend.csproj -->
<ItemGroup>
  <!-- autres packages -->
  <PackageReference Include="FoundationDB.Client.Native" Version="7.4.*" />
</ItemGroup>
```

Le client natif définit le protocole réseau, donc sa version suit le cluster, pas les packages du SDK.
`FoundationDB.Aspire` reste à la version de la bibliothèque (`7.4.2`), et `FoundationDB.Client.Native`
suit `clusterVersion`. Si vous visez plus tard un cluster 7.3, réglez `apiVersion` sur `730` et
`clusterVersion` sur un `7.3.x` à l'étape 2, et changez cet épinglage en `7.3.*` : les deux évoluent
toujours ensemble. Une incompatibilité est la raison habituelle pour laquelle un service se connecte
puis tombe en timeout à chaque opération ; voyez [Comment la connexion s'établit](../foundationdb-101.md).

## 4. Lire la connexion dans le service

Dans `Acme.Backend/Program.cs`, enregistrez FoundationDB à partir de la connexion injectée, puis
ajoutez un endpoint qui lit depuis celle-ci :

```csharp
using FoundationDB.Client;
using FoundationDB.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// câblage Aspire standard (télémétrie, health checks)
builder.AddServiceDefaults();

// "fdb" correspond au nom utilisé dans AddFoundationDb(...) dans l'AppHost.
// Ceci enregistre le singleton IFdbDatabaseProvider.
builder.AddFoundationDb("fdb");

var app = builder.Build();

// Prouve la connexion : GetReadVersionAsync est un aller-retour peu coûteux, donc une valeur ici signifie que ça marche.
app.MapGet("/readversion", async (IFdbDatabaseProvider db, CancellationToken ct) =>
{
    long readVersion = await db.ReadAsync(tr => tr.GetReadVersionAsync(), ct);
    return Results.Ok(new { readVersion });
});

app.Run();
```

`AddFoundationDb("fdb")` lit la connection string injectée et enregistre le singleton
`IFdbDatabaseProvider`, déjà pointé vers le cluster que l'AppHost a démarré. À partir d'ici, l'API de
lecture et d'écriture est la même que dans [Démarrer](../getting-started.md) ; l'endpoint ci-dessus lit
seulement la read version courante du cluster pour prouver la connexion.

## 5. Le lancer

Démarrez l'AppHost avec le CLI `aspire`, qui provisionne le dashboard et les ports pour vous :

```console
dotnet tool install --global aspire.cli    # une seule fois
aspire run --apphost Acme.AppHost/Acme.AppHost.csproj
```

Le dashboard s'ouvre dans votre navigateur. À ce premier lancement, la ressource `fdb` démarre sur un
volume neuf, donc l'AppHost configure la database une fois et journalise qu'il l'a créée ; la ressource
passe alors à l'état sain, et le backend démarre (il a attendu le cluster). Tout le premier démarrage
se fait sans intervention : aucune étape `fdbcli` manuelle.

Ouvrez l'endpoint `/readversion` du backend depuis le dashboard. Vous obtenez un nombre en direct :

```json
{ "readVersion": 143602331405 }
```

C'est ça, le succès : la read version est une valeur venant du cluster, donc une vraie connexion a eu
lieu.

Arrêtez l'AppHost et relancez-le. Le second lancement ne réutilise rien par défaut (un container neuf
et un volume neuf), donc il provisionne à nouveau. Pour conserver le container et ses données d'un
lancement à l'autre, ajoutez `.WithLifetime(ContainerLifetime.Persistent)` au cluster ; un lancement
ultérieur trouve alors la database déjà configurée et saute l'étape. Les
[guides pratiques](how-to.fr.md) couvrent cela et les autres tâches courantes.

## Pour aller plus loin

Vous avez un cluster qu'Aspire démarre pour vous et un service qui s'y connecte via
`IFdbDatabaseProvider`.

- [Guides pratiques](how-to.fr.md) : se connecter à un cluster existant, réutiliser le container d'un
  lancement à l'autre, épingler ou faire évoluer la version du cluster, désactiver le provisionnement
  automatique.
- [Démarrer](../getting-started.md) : l'API de lecture et d'écriture que vous utilisez une fois le
  provider enregistré.
- [Ce que c'est, et pourquoi](index.fr.md) : la séparation entre host et client, le flux de connexion,
  et comment un cluster neuf se provisionne lui-même.
