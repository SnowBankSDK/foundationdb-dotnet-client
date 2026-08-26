# L'intégration Aspire : ce que c'est, et pourquoi

[.NET Aspire](https://aspire.dev/) démarre pour vous un *cluster* FoundationDB local et remet sa
connexion à chaque service qui en a besoin. Cette page explique ce qu'est l'intégration et pourquoi
elle a cette forme ; pour le premier démarrage pas à pas, voir [Prise en main](getting-started.fr.md),
pour les recettes de tâches, voir [Guides pratiques](how-to.fr.md), et pour les tables des paramètres
et des modificateurs, voir la [référence](reference.fr.md).

Sans Aspire, vous démarrez un *container*, vous l'attendez, vous configurez la base de données et vous copiez
à la main un fichier de *cluster* dans chaque service (la page [Configuration du cluster](../cluster-setup.md)
parcourt ce chemin). L'intégration Aspire fait tout cela : l'AppHost décrit le *cluster* une fois, et
chaque service lit sa connexion depuis la configuration.

## La séparation *host* / client

L'intégration est livrée en deux *packages*, et chacun va dans un type de projet différent :

- **`FoundationDB.Aspire.Hosting`** va dans l'**AppHost**. Il définit la ressource *cluster* avec
  `AddFoundationDb` (un *container* qu'Aspire exécute pour vous) ou `AddFoundationDbCluster` (un *cluster*
  existant auquel vous vous connectez).
- **`FoundationDB.Aspire`** va dans **chaque service** qui parle au *cluster*. Il lit la connexion
  injectée et enregistre le *singleton* `IFdbDatabaseProvider` que le reste de votre code résout.

La séparation suit le modèle d'Aspire lui-même : l'AppHost est l'orchestrateur qui connaît chaque
ressource et la façon dont elles se connectent, et un service ne connaît que sa propre configuration.
Un service ne nomme jamais un *host*, un port ou un fichier de *cluster* ; il nomme la ressource (`"fdb"`),
et l'AppHost fournit le reste.

## Comment circule la connexion

Un seul nom relie les deux côtés. `AddFoundationDb("fdb", ...)` dans l'AppHost déclare une ressource
nommée `"fdb"`. `WithReference(fdb)` sur un projet injecte la *connection string* de cette ressource
dans le projet sous le même nom. `AddFoundationDb("fdb")` dans le service relit la *connection string*
par ce nom et enregistre le *provider* :

```
AppHost:   AddFoundationDb("fdb", ...)          définit le cluster
           project.WithReference(fdb)           injecte la connection string "fdb"
Service:   AddFoundationDb("fdb")               lit "fdb", enregistre IFdbDatabaseProvider
```

La *connection string* est la seule chose qui traverse la frontière, donc le même code de service se
connecte à un *container* local en développement et à un *cluster* de production en *staging*. Seul
l'AppHost change. `WaitFor(fdb)` retient un projet dépendant jusqu'à ce que le *cluster* se signale en
bonne santé, donc un service ne démarre pas contre une base de données qui ne peut pas encore répondre.

## Un *cluster* neuf se provisionne lui-même

Une base de données FoundationDB sur un volume de stockage tout neuf n'est pas utilisable tant qu'elle n'a
pas été configurée une fois. Lancez `status` dessus et elle signale « The base de données is unavailable » ;
un client qui l'ouvre attend, sans erreur, une étape de configuration qui ne vient jamais. Au premier
démarrage, cela ressemble à un blocage : l'AppHost reste à un CPU proche de zéro et rien ne démarre.

L'intégration supprime ce piège du premier démarrage. Quand le *container* du *cluster* démarre sur un
volume neuf, l'AppHost exécute `configure new single ssd` à l'intérieur, puis retient chaque ressource
qui attend le *cluster* jusqu'à ce que la base de données réponde. Une base de données déjà configurée est laissée
intacte, donc un redémarrage avec un volume persistant saute l'étape et journalise que la base de données est
déjà configurée. Le travail est idempotent et sûr quand deux initiateurs démarrent en même temps ;
deux AppHosts contre un même volume neuf convergent vers une seule base de données configurée, pas deux
bases de données en conflit.

Deux propriétés rendent ce comportement sûr à laisser activé par défaut. L'attente est **bornée**, pas
infinie : si la base de données ne devient pas disponible à temps, l'AppHost échoue et nomme la recette
manuelle `fdbcli --exec "configure new single ssd"`, plutôt que de rester bloqué en silence. Et le
chemin nominal **journalise** qu'il a configuré la base de données, donc un premier démarrage n'est jamais
silencieux. Désactivez le comportement avec `WithAutoProvisioning(false)` sur la ressource *cluster*
quand vous voulez gérer la configuration vous-même.

La même primitive de provisionnement soutient le harnais de test. Un *container* de test FoundationDB
fraîchement créé provisionne lui-même sa base de données au premier démarrage, donc une suite de tests n'a
besoin d'aucune étape `fdbcli` manuelle.

## Local ou externe

Deux méthodes de l'AppHost couvrent les deux cas, et un service ne peut pas les distinguer :

- `AddFoundationDb(...)` exécute un *container* FoundationDB à partir de l'image Docker
  `foundationdb/foundationdb`. À utiliser pour le développement local. Il a besoin de Docker sur la
  machine de développement.
- `AddFoundationDbCluster(...)` ne démarre aucun *container*. Il passe un fichier de *cluster* que vous
  fournissez aux projets qui le référencent. À utiliser pour le *staging*, la production, ou tout
  *cluster* qu'Aspire n'a pas démarré.

Les deux injectent une *connection string* sous le nom de la ressource, donc un service écrit pour l'un
fonctionne avec l'autre sans changement de code.

## Compatibilité des versions

FoundationDB couple le client natif au *cluster*, et cette version est distincte des *packages* du SDK. Le
client natif `fdb_c` qu'un service charge (`FoundationDB.Client.Native`) doit correspondre au *cluster*
en cours d'exécution sur la même version `major.minor`, et la version d'API qu'un service sélectionne
doit être inférieure ou égale à la version du *cluster*. L'AppHost choisit la version du *cluster*
(`clusterVersion`) et la version d'API (`apiVersion`) en un seul appel, donc les deux concordent ; le
*package* natif du service est un *pin* distinct que le développeur maintient aligné sur cette version du
*cluster*. Les *packages* `FoundationDB.Aspire` et `FoundationDB.Aspire.Hosting` portent la version propre
du SDK, sans rapport avec le *cluster*. La page [Comment il se connecte](../foundationdb-101.md) couvre
en détail le *versioning* du client et du *cluster*.

## Sa place

Le travail de l'intégration s'arrête là où commencent les autres guides : elle enregistre
l'`IFdbDatabaseProvider` que [Prise en main](../getting-started.md) et le [Guide](../guide/index.md)
supposent que vous avez déjà. À partir du *provider*, vous ouvrez la base de données et vous lisez et écrivez
exactement comme ces pages le décrivent.

Un détail transparaît en développement local : Aspire mappe le *cluster* sur son propre port hôte, pas
le `4500` qu'utilisent les procédures pas à pas en Docker simple, donc connectez-vous avec l'adresse
qu'Aspire affiche dans son *dashboard* plutôt qu'une adresse codée en dur.
