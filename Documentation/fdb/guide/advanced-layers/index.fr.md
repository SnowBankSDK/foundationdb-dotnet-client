# Comment le *cluster* traite une transaction

La conception d'un *Layer* sophistiqué découle de la compréhension de la façon dont le *cluster* traite réellement une transaction. Cette page explique ce modèle : les rôles, le déroulement, et pourquoi les règles avec lesquelles vous composez (la limite de 5 secondes, les conflits, l'horloge globale) en sont des conséquences directes. Les [*Layers* avancés](how-to.md) appliquent le modèle à la performance et aux *patterns* distribués ; lisez d'abord cette page. Elle part du principe que vous avez lu [Clés et *Layers*](../keys-and-layers/index.md) et [Transactions](../transactions/index.md).

## Comment une transaction est traitée

FoundationDB répartit les responsabilités entre plusieurs rôles (c'est l'architecture publiée de FoundationDB ; les contraintes avec lesquelles vous composez en sont des conséquences directes) :

| Rôle | Responsabilité |
|---|---|
| **Coordinators** | Petit groupe Paxos ; élit le Cluster Controller et détient le fichier de *cluster*. Les clients s'amorcent ici. |
| **Cluster Controller** | Recrute et surveille tous les autres rôles ; pilote la *recovery*. |
| **Master / Sequencer** | Distribue des **versions monotones croissantes** : les *read versions* et les *commit versions*. C'est l'horloge logique globale. |
| **GRV proxies** | Fournissent le *get-read-version* : demandent au master la dernière version committée et confirment que les transaction logs sont toujours actifs (ainsi une *read version* n'est jamais périmée après une *recovery*). Débit régulé par le Ratekeeper. |
| **Commit proxies** | Pilotent les commits : obtiennent une *commit version* du master, envoient les *conflict ranges* aux resolvers, rendent les mutations durables sur les transaction logs. |
| **Resolvers** | Gardent en mémoire les **~5 dernières secondes d'écritures committées** et y comparent les *read-conflict ranges* d'une transaction en cours de commit. C'est ici que les conflits (`not_committed`, 1020) sont décidés. |
| **Transaction logs (tlogs)** | *Write-ahead log* durable et répliqué ; reçoivent les mutations dans l'ordre des versions et n'acquittent qu'une fois le **fsync** effectué sur un quorum. |
| **Storage servers** | Détiennent les données réparties en *shards* et répliquées ; gardent ~5 secondes de mutations en mémoire plus une copie sur disque « telle qu'elle était il y a 5 secondes » ; servent les lectures via MVCC. |
| **Ratekeeper** / **Data Distributor** | Régulent le débit de démarrage des transactions à l'approche de la saturation / maintiennent l'équilibre des *shards* entre les storage servers. |

Une **transaction de lecture-écriture** se déroule ainsi :

1. **Get read version (GRV).** La première lecture récupère une *read version* auprès d'un GRV proxy (une version committée récente, confirmée par quorum).
2. **Les lectures** vont *directement aux storage servers* à cette version. Le client met en cache la table *shard*→serveur et peut émettre des lectures en parallèle. Les *read-conflict ranges* s'accumulent côté client, sauf si vous utilisez des *snapshot reads*.
3. **Les écritures** sont mises en *buffer* dans le client ; rien n'atteint encore le *cluster*.
4. **Commit.** Le client envoie les mutations et les *conflict ranges* à un commit proxy → celui-ci obtient une *commit version* du master → les resolvers vérifient les conflits → si tout est propre, les mutations sont rendues durables sur les tlogs → le proxy acquitte avec la *commit version* (c'est elle qui remplit vos `VersionStamp`).
5. Les storage servers récupèrent et appliquent de façon asynchrone les mutations committées depuis les tlogs.

### Pourquoi ces règles existent

- **_Read version_ = l'horloge du sequencer.** C'est la seule notion de « maintenant » sur laquelle tous les nœuds s'accordent, ce qui en fait précisément la bonne base pour la coordination entre nœuds (et pourquoi les horloges système locales ne le sont pas ; voir *L'horloge globale* plus bas).
- **`VersionStamp` = la _commit version_.** Globalement ordonnée et monotone, idéale pour les *logs* et les *feeds*.
- **Conflits = verdicts des resolvers** sur les *read-conflict ranges*. Les *snapshot reads* (aucun *read-conflict* ajouté) et les opérations atomiques (aucune lecture du tout) les évitent.
- **La limite de 5 secondes = la fenêtre MVCC** que les resolvers et les storage servers conservent. Une *read version* plus ancienne que cela produit `transaction_too_old`. C'est aussi pourquoi une *recovery* « avance » le temps et abandonne les transactions en cours. Gardez les transactions courtes ; paginez les longs *scans* sur plusieurs d'entre elles.
- **Les lectures passent à l'échelle horizontalement** sur les storage servers ; **les commits sont canalisés** à travers proxies → resolvers → tlogs. Les charges de travail à dominante lecture passent donc facilement à l'échelle, alors que le débit de commit est ce qu'il faut économiser : gardez les *write sets* petits et regroupez les écritures.

## L'horloge globale

Le sequencer est la seule source de « maintenant » sur laquelle tous les nœuds s'accordent. Utilisez-la ; n'utilisez jamais les horloges système locales d'un nœud pour des décisions entre nœuds.

- `tr.GetReadVersionAsync()` donne la *read version* (une horloge logique monotone à l'échelle du *cluster*). Utilisez-la pour les *leases*, l'ordonnancement et le raisonnement « as-of ».
- `tr.CreateVersionStamp()` + `SetVersionStampedKey/Value` donnent la *commit version*, pour les *logs* et les *feeds* ordonnés.

Deux pièges, bien réels :

1. **Les horloges système locales n'ont pas de « maintenant » partagé.** Comparer un *timestamp* émis sur un nœud au `DateTime.UtcNow` d'un autre nœud n'a aucun sens : le *skew*, la dérive, les sauts NTP et les pauses de VM font diverger les deux horloges d'un écart inconnu. Un nœud dont l'horloge est rapide évince des pairs vivants ; un nœud dont l'horloge est lente n'évince jamais les pairs morts.
2. **Le rythme d'incrémentation des versions n'est pas constant** (~1 000 000/s, mais il dérive et ralentit quand le *cluster* est inactif). Ne convertissez donc **pas** un delta de versions en durée. À la place, stockez un *token* issu de la base de données et testez son **changement** (égalité), et mesurez le temps écoulé uniquement comme l'écart entre deux lectures locales consécutives *propres* à l'observateur.

Une horloge partagée élimine le *skew*, mais pas l'**impossibilité fondamentale du détecteur de défaillances** : vous ne pouvez jamais savoir avec certitude si un pair est lent ou mort. La *liveness* est donc toujours une politique (un seuil) adossée à un **evict-and-resync**, et non une preuve.
