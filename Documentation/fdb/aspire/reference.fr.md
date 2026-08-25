# Référence Aspire

Les types, méthodes, paramètres et défauts de l'intégration Aspire de FoundationDB. Pour un premier
lancement, voir [Getting started](getting-started.fr.md) ; pour des tâches ponctuelles, voir les
[guides pratiques](how-to.fr.md) ; pour la conception, voir [l'explication](index.fr.md).

## *Packages*

| Package | Projet | Fournit |
|---|---|---|
| `FoundationDB.Aspire.Hosting` | AppHost | `AddFoundationDb`, `AddFoundationDbCluster`, et les modificateurs de ressource cluster |
| `FoundationDB.Aspire` | chaque service | `AddFoundationDb(connectionName)`, qui enregistre `IFdbDatabaseProvider` |
| `FoundationDB.Client.Native` | chaque service | le client natif `fdb_c`, épinglé sur le `major.minor` du cluster |

## AddFoundationDb

Définit un *cluster* FoundationDB qu'Aspire fait tourner comme un *container* Docker, et rend un
`IResourceBuilder<FdbClusterResource>`.

```csharp
IResourceBuilder<FdbClusterResource> AddFoundationDb(
    this IDistributedApplicationBuilder builder,
    string name,
    int apiVersion,
    string root,
    int? port = null,
    string? clusterVersion = null,
    FdbVersionPolicy? rollForward = null)
```

| Paramètre | Sens | Défaut |
|---|---|---|
| `name` | le nom de la ressource, réutilisé par le `AddFoundationDb` du service | requis |
| `apiVersion` | le niveau d'API que l'application demande, égal ou inférieur à la version du cluster | requis |
| `root` | la racine du directory-layer contre laquelle l'application résout ses chemins | requis |
| `port` | le port hôte auquel le container se lie | `4550` |
| `clusterVersion` | la version d'image cible (`"7.4.6"`, `"7.4.*"`, `"7.*"`) | dérivée de `apiVersion` |
| `rollForward` | jusqu'où une image plus récente peut être sélectionnée | dérivée de `clusterVersion` |

Une surcharge prend `root` comme un `FdbPath` et ajoute `imageRegistry` (défaut `"docker.io"`).

## AddFoundationDbCluster

Définit une connexion à un *cluster* existant à partir d'un *cluster file*. Elle ne démarre aucun
*container*, et rend un `IResourceBuilder<FdbConnectionResource>`.

```csharp
IResourceBuilder<FdbConnectionResource> AddFoundationDbCluster(
    this IDistributedApplicationBuilder builder,
    string name,
    int apiVersion,
    string root,
    string? clusterFile = null,
    string? clusterVersion = null)
```

| Paramètre | Sens | Défaut |
|---|---|---|
| `name` | le nom de la ressource, réutilisé par le `AddFoundationDb` du service | requis |
| `apiVersion` | le niveau d'API que l'application demande | requis |
| `root` | la racine du directory-layer contre laquelle l'application résout ses chemins | requis |
| `clusterFile` | le chemin du cluster file passé aux services qui le référencent | `null` |
| `clusterVersion` | la version de la bibliothèque cliente à utiliser | `null` |

Une surcharge prend `root` comme un `FdbPath`.

## Modificateurs de ressource cluster

S'appliquent au *builder* que `AddFoundationDb` ou `AddFoundationDbCluster` rend.

| Modificateur | S'applique à | Effet |
|---|---|---|
| `WithAutoProvisioning(bool enabled = true)` | `FdbClusterResource` | active ou désactive le provisioning au premier démarrage (actif par défaut) |
| `WithClusterVersion(string version)` | `FdbConnectionResource` | définit la version de la bibliothèque cliente |
| `WithLifetime(ContainerLifetime.Persistent)` | ressources container (Aspire) | réutilise le container et son volume d'une exécution à l'autre |
| `WithReference(fdb)` | un projet (Aspire) | injecte la connection string du cluster sous son nom |
| `WaitFor(fdb)` | un projet (Aspire) | retient le projet jusqu'à ce que le cluster se déclare en bonne santé |

## FdbVersionPolicy

La politique de *roll-forward* pour l'image Docker, dans le *namespace* `Aspire.Hosting.ApplicationModel`.

| Valeur | Sélectionne |
|---|---|
| `Exact` | la version exacte demandée |
| `Latest` | la dernière version du registry, compatibilité non garantie |
| `LatestMajor` | la dernière version compatible au niveau de la demande ou au-dessus, tous majors confondus |
| `LatestMinor` | le dernier minor compatible au niveau de la demande ou au-dessus, dans le major |
| `LatestPatch` | le dernier patch dans le minor demandé |

## Formes de clusterVersion

La forme *string* de `clusterVersion` fixe la version et un `rollForward` par défaut.

| Forme | Exemple | Version | rollForward par défaut |
|---|---|---|---|
| exacte | `"7.4.6"` | cette version | `Exact` |
| wildcard de patch | `"7.4.*"` | `7.4` | `LatestPatch` |
| wildcard de minor | `"7.*"` | `7` | `LatestMinor` |
| omise ou `"*"` | (aucun) | dérivée de `apiVersion` | `LatestMajor` |

Un `apiVersion` de niveau `740` correspond à la version `7.4` (le dernier chiffre du niveau est
normalement `0`).

## AddFoundationDb (service)

Lit la connexion injectée nommée `connectionName` et enregistre le *singleton* `IFdbDatabaseProvider`.
Dans le *package* `FoundationDB.Aspire`.

```csharp
IHostApplicationBuilder AddFoundationDb(
    this IHostApplicationBuilder builder,
    string connectionName,
    Action<FdbClientSettings>? configureSettings = null,
    Action<FdbDatabaseProviderOptions>? configureProvider = null)
```

`configureSettings` ajuste les *settings* du client lus depuis la configuration ; `configureProvider`
ajuste les options du *provider*. Les deux sont optionnels.

## Fdb.Provisioning.EnsureDatabaseConfiguredAsync

Configure la base de données d'un *cluster* neuf et rend la main une fois qu'elle est disponible. Dans
`FoundationDB.Client` ; l'AppHost et le *test harness* l'appellent tous les deux.

```csharp
Task EnsureDatabaseConfiguredAsync(
    FdbCliRunner runFdbCli,
    TimeSpan timeout,
    string configuration = "single ssd",
    TimeSpan? probeInterval = null,
    Action<string>? log = null,
    CancellationToken ct = default)
```

| Paramètre | Sens | Défaut |
|---|---|---|
| `runFdbCli` | un delegate qui exécute `fdbcli` avec les arguments donnés et rend son code de sortie et sa sortie | requis |
| `timeout` | la borne de l'attente ; la méthode throw si la base de données n'est pas disponible à temps | requis |
| `configuration` | l'argument de `configure new` | `"single ssd"` |
| `probeInterval` | le délai entre deux vérifications de disponibilité | un défaut interne |
| `log` | un sink pour les lignes de progression | `null` |
| `ct` | le cancellation token | `default` |

L'appel est idempotent : une base de données déjà configurée est laissée intacte.

## Défauts du *container*

| Setting | Valeur |
|---|---|
| port hôte et port du container | `4550` (les deux égaux, proxy Aspire désactivé) |
| image | `foundationdb/foundationdb` |
| registry d'image | `docker.io` |
| volume de données | `fdb_data` monté sur `/var/fdb/data` |

## Télémétrie

`FoundationDB.Aspire` ajoute `AddFoundationDbInstrumentation` à la fois à `TracerProviderBuilder` et
`MeterProviderBuilder`, pour que le *pipeline* OpenTelemetry d'un service puisse collecter les traces et
les métriques du *binding*.
