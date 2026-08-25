# Introduction

Cette bibliothèque est un *binding* C#/.NET pour [FoundationDB](https://www.foundationdb.org/) : elle encapsule le client natif `fdb_c` et expose une API idiomatique, économe en allocations, en `async`/`await`.

## Ce qu'est FoundationDB

FoundationDB est un *key/value store* distribué et **ordonné**, avec des **transactions ACID sérialisables** sur tout le *keyspace*. Il est volontairement minimal : il stocke des clés binaires associées à des valeurs binaires, garde les clés triées, et permet d'en lire et d'en écrire plusieurs de façon atomique. Tout ce qui est de plus haut niveau (tables, index, files d'attente, collections de documents, *pub/sub*) se construit par-dessus cette primitive, sous forme de *Layer*.

Ce minimalisme vous laisse deux responsabilités : l'encodage des clés et le modèle de transactions. Faites-les bien et le *store* est fiable ; faites-les mal et le résultat est une corruption de données silencieuse. Cette documentation couvre les deux.

## Ce que ce *binding* vous apporte

- **Des clés typées et *lazy*** : `subspace.Key("user", 123)` construit un petit `struct` qui s'encode en tuple seulement au moment où il est passé à une transaction. Vous n'assemblez jamais les octets d'une clé à la main.
- **Une *retry loop*** : `db.ReadAsync` / `WriteAsync` / `ReadWriteAsync` gèrent pour vous le modèle de conflits et de *retry* de FoundationDB.
- **Le *Directory layer*** : associe des chemins lisibles à des préfixes de clés courts et denses.
- **Les *Layers*** : un petit contrat (`IFdbLayer<TState>`) pour empaqueter l'accès aux données dans des composants réutilisables et composables.
- **L'attention aux allocations** : `Slice`, les *buffers* poolés et les clés/valeurs en `struct` gardent le *hot path* libre d'allocations `byte[]` inutiles.

## Où aller ensuite

- **Nouveau sur FoundationDB ?** Commencez par [Prérequis](prerequisites.md), puis [Fonctionnement](foundationdb-101.md) et [Installer un cluster](cluster-setup.md).
- [Prise en main](getting-started.md) : installez les *packages* et exécutez vos premières lecture et écriture.
- [Guide → Clés et *Layers*](guide/keys-and-layers/index.md) : la chose la plus importante à apprendre en premier.
- [Aspire](aspire/index.md) : lancez un *cluster* local et câblez-le automatiquement dans vos services. Le [README](../../README.md) contient les détails de déploiement et de *build*.

> **Une note sur le périmètre :** FoundationDB a des limites bien connues à prendre en compte dès le premier jour : une transaction dure au plus ~5 secondes, une valeur est plafonnée à 100 Ko, une clé à 10 Ko, et une même transaction écrit au plus 10 Mo. Le guide [Transactions](guide/transactions/index.md) explique pourquoi elles existent et comment vivre avec.
