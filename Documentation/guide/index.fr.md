# Construire sur FoundationDB : le guide du développeur

FoundationDB vous donne un unique *key/value store* ordonné et transactionnel, et vous demande de construire tout le reste par-dessus. Cette liberté est tout l'intérêt, et la principale source d'erreurs. Ce guide est une présentation pratique et assumée de la façon d'utiliser ce *binding* .NET (`FoundationDB.Client` / `SnowBank`) *comme il faut* : comment encoder les clés, comment les transactions se comportent réellement, et comment construire des *Layers* distribués et sophistiqués sans tomber dans les pièges classiques.

Il est organisé en quatre parties :

1. **[Clés, valeurs et *Layers*](keys-and-layers/index.md)** : comment les données sont encodées, et comment empaqueter l'accès aux données dans un *Layer* réutilisable. Commencez ici.
2. **[Transactions](transactions/index.md)** : la *retry loop*, l'idempotence, les conflits, les opérations atomiques et les *watches*.
3. **[Layers avancés](advanced-layers/index.md)** : comment le *cluster* traite une transaction, comment rendre les *Layers* rapides, et les *patterns* difficiles des systèmes distribués (*change feeds*, *leases*, rétention, *fencing*).
4. **[Données binaires (Slice et Buffers)](slices-and-buffers.md)** : la boîte à outils au niveau des octets sous tout le reste, `Slice` et `SliceReader`/`SliceWriter`, les *buffers* poolés, et les encodages d'entiers. Utilisez-la quand vous écrivez des codecs de valeurs personnalisés.

> Ces guides sont le pendant, destiné aux humains, des *skills* orientés agents sous [`.claude/skills/`](../../.claude/skills/), et chaque exemple de code reflète les *samples* vérifiés à la compilation dans [`samples/SkillValidation/`](../../samples/SkillValidation/).

## Le modèle mental en un écran

- La base de données est **une unique *map* plate et triée d'octets → octets.** Les clés sont triées lexicographiquement par leurs octets bruts, et cet ordre est la *seule* structure dont vous disposez. Chaque table, index, file d'attente et collection de documents est une illusion que vous construisez en choisissant soigneusement les octets des clés.
- **Les tuples, c'est ainsi que vous choisissez ces octets.** L'encodage en tuple transforme des valeurs typées (chaînes, entiers, GUID, `VersionStamp`) en octets dont l'ordre correspond à l'ordre logique des valeurs. `(42, "a")` se trie toujours avant `(42, "b")`, lui-même avant `(43, …)`. C'est pourquoi les tuples sont l'encodage de clés par défaut.
- Un ***subspace*** est un préfixe de clé que vous obtenez en résolvant une *location* logique (généralement via le *Directory layer*). Toutes vos clés y résident.
- Une **transaction** est sérialisable et ACID, mais peut devoir être rejouée, et est limitée à **5 secondes** et **10 Mo** d'écritures.
- Un ***Layer*** est un petit composant réutilisable qui transforme l'API *key/value* brute en une abstraction utile (une *map*, un index, un *document store*, un *change feed*).

## Les grandes leçons (apprises à la dure)

Elles reviennent tout au long du guide, et il vaut mieux les intégrer dès le départ.

- **Ne touchez jamais aux octets bruts.** Construisez les clés avec `subspace.Key(...)` et les valeurs avec `FdbValue.*`, et passez ces objets directement à la transaction. La concaténation manuelle de chaînes ou d'octets casse l'ordre et l'échappement.
- **Les clés sont *lazy*.** `subspace.Key("a", 1)` est un petit `struct` qui retient ses composants, et il n'est rendu en octets qu'au moment où la transaction en a besoin. N'appelez pas `.ToSlice()` de façon anticipée.
- **Votre *handler* de transaction s'exécute plus d'une fois.** Il doit être une fonction pure de l'état de la base de données : pas d'effets de bord externes (caches, compteurs, *logging*) à l'intérieur.
- **Utilisez les opérations atomiques en cas de contention.** Un unique compteur *hot* sérialise tous les *writers* au niveau du *resolver*, alors que `AtomicAdd64` et le *sharding* ne le font pas.
- **Il n'y a pas d'horloge murale globale.** Les horloges de nœuds différents ne sont pas comparables. Quand vous avez besoin d'une notion partagée de temps ou d'ordre, utilisez la **read version** de la base de données (une horloge monotone fournie par le *sequencer* du *cluster*), jamais `DateTime.UtcNow` d'un nœud à l'autre.
- **La latence, ce sont des allers-retours.** Le client fait du pipelining, alors regroupez les lectures indépendantes (`GetValuesAsync`, `Task.WhenAll`) et évitez les enchaînements « lire, décider, lire encore ».
- **Les *logs* non bornés doivent être élagués, et les consommateurs doivent pouvoir détecter qu'ils ont pris du retard.** Un *change feed* n'est pas terminé tant qu'il n'a pas à la fois une rétention *et* un moyen de dire à un abonné bloqué de se resynchroniser.

Si un morceau de code que vous écrivez ou relisez touche aux clés, aux transactions ou à la coordination multi-nœuds, le guide correspondant ci-dessous contient le *pattern* idiomatique, et le raisonnement qui le sous-tend.
