# Installer un cluster

Avant que votre application puisse lire ou écrire quoi que ce soit, il lui faut un *cluster* à qui parler. Deux cas de figure :

- Vous avez déjà un *cluster* (un collègue en a installé un, ou il tourne dans votre infrastructure) : voir [Se connecter à un cluster existant](#connect-to-an-existing-cluster).
- Vous n'avez encore rien : voir [Lancer un cluster local avec Docker](#run-a-local-cluster-with-docker).

Les deux chemins mènent à [Prise en main](getting-started.md). Pour un *cluster* existant, trouvez sa version (`fdbcli --version`), faites-la correspondre au *package* `FoundationDB.Client.Native` et au niveau d'API, puis pointez votre application vers son fichier `fdb.cluster`. Si vous n'avez encore rien, lancez FoundationDB 7.4 en local dans Docker. Les deux voies sont couvertes ci-dessous.

## Se connecter à un *cluster* existant

### 1. Trouver la version du *cluster*

Le client natif que vous livrez doit correspondre au *cluster* (voir [Fonctionnement](foundationdb-101.md)), commencez donc par trouver la version :

```console
fdbcli --version
```

Vous devriez voir quelque chose comme :

```
FoundationDB CLI 7.3 (v7.3.76)
source version ...
protocol fdb00b073000000
```

C'est le `7.3` (ou `7.4`) qui compte. Si `fdbcli` n'est pas installé, demandez à la personne qui gère le *cluster*, ou lisez la version dans sa sortie `status`.

### 2. Choisir les *packages* correspondants

| Votre cluster | `FoundationDB.Client.Native` | Niveau d'API |
|---|---|---|
| `7.3.x` | une version `7.3.x` (p. ex. `7.3.76`) | `730` |
| `7.4.x` | une version `7.4.x` (p. ex. `7.4.6`) | `740` (ou `730` pour rester compatible avec `7.3`) |

Gardez `FoundationDB.Client` (le *package* managé) à la dernière version dans les deux cas. Seul le *package* natif suit la version du *cluster*.

> Vous prévoyez de passer à `7.4` bientôt ? Ciblez le niveau d'API `730` aujourd'hui sur votre *cluster* `7.3`, puis basculez le *package* natif vers `7.4.x` et montez le niveau d'API à `740` une fois le *cluster* mis à jour. Le *package* managé et votre code ne changent pas.

### 3. Y pointer votre application

Votre application a besoin des coordinateurs du *cluster*, fournis soit sous forme de **fichier *cluster*** (`fdb.cluster`, le même que celui qu'utilise `fdbcli`), soit sous forme de **_connection string_** avec le même contenu. Les deux tiennent sur une ligne, `description:id@host:port` (avec plus d'hôtes pour les *clusters* multi-coordinateurs) :

```
mycluster:abcdef1234567890@10.0.0.10:4500
```

Vous l'utiliserez dans [Prise en main](getting-started.md). Préférez le *package* natif NuGet à une installation du client à l'échelle de la machine, ainsi chaque projet épingle sa propre version (voir [pourquoi](foundationdb-101.md#why-pin-the-native-client-per-project)).

## Lancer un *cluster* local avec Docker

Pas de *cluster* ? Lancez un **FoundationDB 7.4** jetable, à un seul nœud, dans Docker. Cela fonctionne pareil sur Windows, Linux et macOS : `fdbserver` est un programme Linux, donc même sur Windows et macOS il tourne à l'intérieur du conteneur Linux, et votre application .NET lui parle par le réseau.

Côté application, prenez `FoundationDB.Client.Native` `7.4.x` et le niveau d'API `740` (ou `730`).

### 1. Démarrer le conteneur

```console
docker run --detach --name fdb \
  --publish 127.0.0.1:4500:4500 \
  --env FDB_NETWORKING_MODE=host \
  --env FDB_PORT=4500 \
  --env FDB_COORDINATOR_PORT=4500 \
  foundationdb/foundationdb:7.4.6
```

`FDB_NETWORKING_MODE=host` fait annoncer `127.0.0.1` par le serveur pour que votre application sur l'hôte puisse l'atteindre, et le `--publish 127.0.0.1:4500:4500` assorti garde le port identique à l'intérieur et à l'extérieur du conteneur. Les deux comptent : sans eux, le client se connecte une fois, reçoit une adresse qu'il ne peut pas atteindre, et chaque transaction tombe en *timeout*.

- `Cannot connect to the Docker daemon` : Docker n'est pas lancé (voir [Prérequis](prerequisites.md)).
- `The container name "/fdb" is already in use` : vous en avez déjà un. Réutilisez-le avec `docker start fdb`, ou supprimez-le avec `docker rm -f fdb` et relancez.
- Sur un Mac Apple Silicon, Docker peut exécuter l'image en émulation s'il n'existe pas de *build* natif arm64 (ça marche, c'est juste plus lent). S'il refuse de démarrer, ajoutez `--platform linux/amd64`.

### 2. Initialiser la base de données (une fois)

Un *cluster* tout neuf n'a pas encore de base de données. Créez-en une :

```console
docker exec fdb fdbcli --exec "configure new single ssd"
```

Vous devriez voir :

```
Database created
```

`single` signifie une seule copie des données (parfait pour du dev local) ; `ssd` est le moteur de stockage.

### 3. Vérifier qu'il tourne

```console
docker exec fdb fdbcli --exec "status minimal"
```

Vous devriez voir :

```
The database is available.
```

Juste après `configure`, `status minimal` peut brièvement signaler "The database is available, but has issues" pendant que le *cluster* termine son recrutement ; attendez quelques secondes et il se stabilise sur "The database is available". Si vous voyez "The database is unavailable" ou "Unable to locate a usable set of coordination servers", attendez quelques secondes et réessayez.

### 4. Se connecter depuis votre application

Utilisez cette *connection string* ; elle correspond au coordinateur du conteneur :

```
docker:docker@127.0.0.1:4500
```

C'est ce que vous passerez dans [Prise en main](getting-started.md).

### Nettoyer

```console
docker rm -f fdb
```

Supprime le conteneur. La commande ci-dessus ne monte aucun volume, donc cela jette aussi les données, ce qui est bien ce que vous voulez pour un *cluster* jetable.

### Ou laissez Aspire s'en charger

Pour du vrai développement, .NET Aspire peut démarrer ce *cluster* pour vous et injecter la connexion dans votre application automatiquement, sans `docker run` manuel. C'est la configuration recommandée une fois passé le « hello world » : voir [Aspire](aspire/index.md).

## La suite

- [Prise en main](getting-started.md) : votre première connexion, lecture et écriture.
