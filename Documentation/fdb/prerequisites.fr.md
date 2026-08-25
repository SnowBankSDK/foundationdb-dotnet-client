# Prérequis

Cette page liste ce dont vous avez besoin avant de vous connecter à FoundationDB depuis .NET, et comment vérifier que chaque élément fonctionne. Si vous avez déjà un projet .NET 10 et accès à un *cluster*, passez directement à [Prise en main](getting-started.md).

Il vous faut trois choses :

1. **Le SDK .NET 10 (ou plus récent).** [Installez-le pour votre plateforme](https://dotnet.microsoft.com/download).
2. **Docker**, si vous voulez faire tourner un *cluster* FoundationDB en local. [Installez-le pour votre plateforme](https://docs.docker.com/get-docker/). Si vous avez déjà un *cluster* auquel vous connecter, vous pouvez sauter cette étape.
3. **La bibliothèque cliente native `fdb_c`.** Vous ne l'installez *pas* vous-même : le *package* NuGet [`FoundationDB.Client.Native`](https://www.nuget.org/packages/FoundationDB.Client.Native) la fournit pour vous, sur Windows, Linux et macOS. Le choix de la bonne version est traité dans [Installer un cluster](cluster-setup.md).

> Le client natif de FoundationDB est **64 bits uniquement**. Votre processus .NET doit s'exécuter en 64 bits (le comportement par défaut sur les *runtimes* modernes).

## Vérifier votre installation

Exécutez ces trois commandes. Pour chacune, voici à quoi ressemble un succès et ce que signifient les échecs courants.

**Vérifier le SDK .NET :**

```console
dotnet --info
```

Vous devriez voir une liste des SDK installés avec au moins une entrée `10.x` :

```
.NET SDKs installed:
  10.0.100 [/usr/share/dotnet/sdk]
```

- `command not found` (ou `'dotnet' is not recognized` sous Windows) : le SDK n'est pas installé, ou n'est pas dans votre `PATH`. Installez-le, puis ouvrez un nouveau terminal.
- Seules des versions plus anciennes sont listées (par exemple `8.0.x`) : installez le SDK .NET 10.

**Vérifier que Docker est installé et démarré :**

```console
docker version
```

Vous devriez voir à la fois une section `Client` et une section `Server`, chacune avec une version :

```
Client:
 Version:    27.x.x
Server: Docker Desktop
 Engine:
  Version:   27.x.x
```

- `command not found` (ou `'docker' is not recognized`) : Docker n'est pas installé, ou n'est pas dans votre `PATH`.
- `Cannot connect to the Docker daemon` (ou vous ne voyez que la section `Client`) : Docker est installé mais n'est pas démarré. Lancez Docker Desktop (Windows/macOS) ou exécutez `sudo systemctl start docker` (Linux), attendez quelques secondes, puis réessayez.

**Vérifier que Docker peut télécharger et exécuter une image :**

```console
docker run --rm hello-world
```

Vous devriez voir un court message se terminant par :

```
Hello from Docker!
This message shows that your installation appears to be working correctly.
```

- La commande reste bloquée et vous devez appuyer sur `Ctrl-C` : le *pull* n'a pas pu joindre Docker Hub. Vérifiez votre réseau ou votre *proxy*, puis réessayez.
- `permission denied while trying to connect to the Docker daemon socket` (Linux) : votre utilisateur n'est pas dans le groupe `docker`. Ajoutez-le avec `sudo usermod -aG docker $USER`, puis déconnectez-vous et reconnectez-vous, ou préfixez les commandes par `sudo`.

## Ensuite

- [Fonctionnement](foundationdb-101.md) : un modèle mental en deux minutes du client, de la bibliothèque native et du *cluster*. À lire avant d'écrire du code : cette page explique le piège qui attrape presque tous les nouveaux venus.
