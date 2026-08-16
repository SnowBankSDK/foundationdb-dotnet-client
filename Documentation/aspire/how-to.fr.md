# Guides pratiques Aspire

Chaque section ici décrit une tâche avec l'intégration Aspire de FoundationDB. Elles supposent que
vous avez déjà un AppHost qui appelle `AddFoundationDb` et un service qui appelle `AddFoundationDb`,
comme dans [Getting started](getting-started.fr.md). Pour les tables des paramètres et des
modificateurs, voyez la [référence](reference.fr.md) ; pour comprendre pourquoi ces pièces
s'assemblent, voyez [l'explication](index.fr.md).

## Se connecter à un cluster existant

Pour le staging, la production, ou tout cluster qu'Aspire n'a pas démarré, utilisez
`AddFoundationDbCluster` avec un cluster file au lieu de `AddFoundationDb`. Il ne démarre aucun
container ; il passe le cluster file aux services qui le référencent :

```csharp
var fdb = builder.AddFoundationDbCluster("fdb",
    apiVersion: 730,
    root: "/Sandbox/Acme",
    clusterFile: "/etc/foundationdb/fdb.cluster");

builder.AddProject<Projects.Acme_Backend>("backend")
    .WithReference(fdb);
```

Un service lit la connexion de la même façon dans les deux cas (`AddFoundationDb("fdb")`), donc le
code du service ne change pas entre un container local et un cluster externe.

## Réutiliser le cluster et ses données d'une exécution à l'autre

Par défaut, `AddFoundationDb` crée un nouveau container à chaque exécution, donc les données ne
survivent pas à un redémarrage. Pour garder le container et son volume, marquez la ressource cluster
comme persistante :

```csharp
var fdb = builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6")
    .WithLifetime(ContainerLifetime.Persistent);
```

Une exécution ultérieure retrouve le même volume, donc la database est déjà configurée et l'étape de
provisioning est sautée.

## Épingler ou faire évoluer la version du cluster

`clusterVersion` sélectionne le tag de l'image Docker, et `rollForward` décide jusqu'où une image
plus récente peut être prise. La forme string de `clusterVersion` fixe un `rollForward` par défaut
que vous pouvez surcharger :

```csharp
// image exacte, jamais de roll forward
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6");

// dernier patch 7.4
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.*");

// dernière mineure 7.x
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.*");
```

Omettez `clusterVersion` et l'intégration dérive la version depuis `apiVersion` (le niveau 740 donne
7.4) et progresse jusqu'à la dernière version majeure compatible. La
[référence](reference.fr.md#fdbversionpolicy) liste chaque valeur de politique et le défaut que
chaque version string implique.

## Désactiver l'autoprovisioning

L'intégration configure une database neuve au premier démarrage. Pour gérer ça vous-même,
désactivez-le sur la ressource cluster :

```csharp
var fdb = builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6")
    .WithAutoProvisioning(false);
```

Un volume neuf n'a alors pas de database, et tout service qui l'ouvre attend sans erreur jusqu'à ce
que vous la configuriez à la main :

```console
docker exec <container> fdbcli --exec "configure new single ssd"
```

Laissez l'autoprovisioning activé sauf si vous avez une raison de lancer l'étape de configuration
vous-même ; désactivé, une première exécution contre un volume neuf se bloque jusqu'à ce que l'étape
manuelle s'exécute.

## Faire correspondre le client natif au cluster

Un service charge le client natif (`FoundationDB.Client.Native`), et sa version doit correspondre au
cluster en cours d'exécution à `major.minor` près. Épinglez-le dans le projet du service à la version
du cluster :

```console
dotnet add package FoundationDB.Client.Native --version "7.4.*"
```

Ou dans le fichier projet du service :

```xml
<ItemGroup>
  <!-- autres packages -->
  <PackageReference Include="FoundationDB.Client.Native" Version="7.4.*" />
</ItemGroup>
```

`7.4.*` se résout au dernier patch 7.4. Cette version suit le cluster, pas le SDK : le
`clusterVersion` de l'AppHost et le package natif du service doivent s'accorder sur `major.minor`,
tandis que le package `FoundationDB.Aspire` garde sa propre version de bibliothèque. Changez le
cluster et vous changez cet épinglage : un cluster 7.3 (`apiVersion: 730`, `clusterVersion: "7.3.x"`)
a besoin de `FoundationDB.Client.Native` en `7.3.*`. Quand les deux ne s'accordent pas, le service se
connecte puis time out à chaque opération.

## Choisir le port hôte du container

`AddFoundationDb` lie le container au port hôte `4550` par défaut. Passez `port` pour le changer :

```csharp
builder.AddFoundationDb("fdb", apiVersion: 740, root: "/Sandbox/Acme", clusterVersion: "7.4.6", port: 4560);
```

Le port hôte et le port du container sont toujours identiques, et le proxy Aspire est désactivé pour
cette ressource : le nœud FoundationDB annonce son propre port aux clients, donc un port hôte remappé
les enverrait vers une adresse qui n'existe pas. Choisissez un port libre ; n'attendez pas d'Aspire
qu'il le remappe.

## Lancer l'AppHost sans la CLI aspire

La CLI `aspire` provisionne les endpoints du dashboard et de la télémétrie. Un simple `dotnet run`
sur l'AppHost marche aussi si vous fournissez un `Properties/launchSettings.json` avec les endpoints
du dashboard et OTLP que la CLI injecterait sinon. Utilisez la CLI sauf si vous avez une raison de
gérer ces endpoints vous-même.

## Provisionner une database en dehors d'Aspire

L'AppHost et le test harness configurent tous deux une database neuve à travers une seule primitive,
`Fdb.Provisioning.EnsureDatabaseConfiguredAsync`. Appelez-la directement depuis un script ou un
harness personnalisé quand vous provisionnez un cluster vous-même. Elle prend un delegate qui lance
`fdbcli`, un timeout, et retourne une fois la database disponible :

```csharp
await Fdb.Provisioning.EnsureDatabaseConfiguredAsync(
    runFdbCli,                       // votre delegate : lance fdbcli, retourne (code de sortie, sortie)
    timeout: TimeSpan.FromSeconds(30),
    ct: cancellationToken);
```

Elle est idempotente : une database déjà configurée est laissée intacte. Si la database n'est pas
disponible avant le timeout, elle throw au lieu d'attendre indéfiniment. Le test harness l'appelle
sur chaque test container fraîchement créé, donc une suite de tests n'a besoin d'aucune étape
`fdbcli` manuelle.
