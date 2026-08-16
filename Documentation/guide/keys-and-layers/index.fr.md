# Clés, valeurs et *Layers* : ce que c'est et pourquoi

C'est le premier sujet à bien maîtriser, et la source la plus fréquente d'usage incorrect. La base de données est une seule *map* plate d'octets, et chaque table, index et collection de documents est un *pattern* que vous construisez par-dessus. Cette page explique pourquoi les clés sont encodées en tuples, ce que sont un *subspace* et le *Directory layer*, et ce qu'est un *Layer* ; pour les recettes concrètes (construire des clés, résoudre des *subspaces*, écrire un *Layer*, choisir un encodage de valeur) voir les [guides pratiques](how-to.md), et pour l'encodage en tuple en détail voir la [référence](reference.md).

## Une seule *map* plate et triée d'octets

FoundationDB vous donne un seul *key/value store* ordonné et transactionnel, et vous demande de construire tout le reste par-dessus. La base de données est une seule *map* plate et triée, d'octets vers octets. Les clés se trient lexicographiquement selon leurs octets bruts, et cet ordre est la seule structure que vous obtenez. Chaque table, index, file d'attente et collection de documents est un *pattern* que vous construisez en choisissant soigneusement les octets de la clé. Choisissez bien les octets de la clé et un *range scan* renvoie exactement les lignes voulues, dans l'ordre voulu ; choisissez-les mal et le même *scan* renvoie trop, trop peu, ou dans le mauvais ordre.

La base de données stocke des octets et ne les inspecte jamais. Vous ne manipulez presque jamais ces octets vous-même. Vous construisez un objet clé, vous le passez à la transaction, et le *binding* le rend en octets au dernier moment. La raison de procéder ainsi est la correction : concaténer manuellement des chaînes ou des octets casse l'ordre des octets, ou l'échappement dont dépend le tri. [La page des guides pratiques](how-to.md) donne les recettes de construction ; cette page explique pourquoi ce sont les seules recettes sûres.

## Pourquoi les clés sont des tuples

Les tuples, c'est ainsi que vous choisissez les octets de la clé. L'encodage en tuple transforme des valeurs typées (chaînes, entiers, GUID, `VersionStamp`) en octets dont l'ordre correspond à l'ordre logique des valeurs. `(42, "a")` se trie toujours avant `(42, "b")`, qui se trie avant `(43, ...)`. Cette propriété est la raison pour laquelle les tuples sont l'encodage de clé par défaut : le tri que la base de données vous donne gratuitement est le tri que votre application veut, tant que les octets proviennent de l'encodeur de tuples.

Chaque élément commence par un marqueur de type d'un octet, puis ses octets de valeur. Les marqueurs trient les types dans un ordre fixe, et les octets de valeur ordonnent les valeurs au sein d'un type. C'est tout le mécanisme derrière les clés ordonnées ; la [référence](reference.md) couvre en détail l'encodage, les variantes de tuples et les *helpers* de décodage.

Les clés sont aussi *lazy*. `subspace.Key("user", 123)` est un petit `struct` qui retient ses parties et ne les rend en octets qu'au moment où la transaction en a besoin. Vous passez l'objet clé directement à `tr.GetAsync`, `tr.Set` ou `tr.Clear` ; vous ne le pré-sérialisez pas avec `.ToSlice()` pour ensuite faire circuler des octets. Le `struct` *lazy* laisse le *binding* rendre dans des *buffers* poolés au point d'utilisation, et il garde les parties typées disponibles le plus longtemps possible.

## Les *subspaces* et le *Directory layer*

Un *subspace* est un préfixe de clé. Toutes les clés d'un composant vivent dans un seul *subspace*, si bien que ses clés n'entrent jamais en collision avec celles d'un autre composant. Vous n'inventez ni ne codez en dur ce préfixe. À la place, vous déclarez un chemin logique et laissez le *Directory layer* l'associer à un préfixe binaire court et dense.

Voyez une *location* comme un dossier dans un système de fichiers, et le *Directory layer* comme la table qui associe un chemin de dossier à un numéro d'*i-node*. Votre code raisonne en chemins lisibles ; la base de données stocke un préfixe entier court. Si le préfixe `42` est attribué à `/Tenant/ACME/MyApp/v1/Documents/Books`, une clé qui s'y trouve est stockée sous la forme `(42, "BOOK_123")` au lieu du tuple de chemin complet, ce qui économise des dizaines d'octets sur chaque clé.

```fdb-bytes
tuple: (42, "BOOK_123")
int  .15 2A                # préfixe dir · 42
str  .02 'BOOK_123' .00    # chaîne "BOOK_123"
```

Le préfixe est alloué dynamiquement et n'est pas connu tant que le *directory* n'a pas été créé la première fois, donc dans toute cette documentation le préfixe se replie en un `...` de tête, et la clé propre d'un *layer* se lit `(..., "BOOK_123")`. C'est la même idée qu'un chemin relatif `./BOOK_123` au lieu du chemin absolu `/Tenant/ACME/MyApp/v1/Documents/Books/BOOK_123` : les octets du préfixe sont toujours là, la documentation ne détaille simplement pas une valeur qui change à chaque déploiement.

```fdb-bytes
tuple: (..., "BOOK_123")
dir  ...                   # préfixe dir
str  .02 'BOOK_123' .00    # chaîne "BOOK_123"
```

Seule une page portant spécifiquement sur la clé complète, ou sur le *Directory layer* lui-même, détaille le préfixe. Deux conséquences en découlent, et [la page des guides pratiques](how-to.md) les transforme en recettes : vous résolvez la *location* une fois par transaction plutôt que de mettre le préfixe en cache (le mettre en cache vous-même risque une corruption), et résoudre ouvre un *directory* existant au lieu d'en créer un.

## Ce qu'est un *Layer*, et pourquoi

Un *Layer* est l'équivalent FoundationDB d'un petit composant d'accès aux données : une *map*, un index, une collection de documents. Plutôt que d'éparpiller l'accès à la base de données dans des contrôleurs et des pages, vous l'encapsulez dans un *Layer*, et chaque *layer* de `FoundationDB.Layers.Common` et des *layers* SnowBank plus vastes suit la même forme.

Un *Layer* est un *wrapper* fin et réutilisable au-dessus d'un `ISubspaceLocation`. Il ne détient aucun état par transaction. Il implémente `IFdbLayer<TState>`, et `Resolve(tr)` résout la *location* et renvoie un `State` qui contient le `IKeySubspace` résolu. Tout le vrai travail se fait dans des méthodes qui prennent une transaction et utilisent le *subspace* du `State` pour construire des clés.

Une règle rend le *pattern* sûr : le `State` ne doit jamais s'échapper de la transaction. Le *subspace* résolu n'est valide qu'à l'intérieur de la transaction qui l'a produit, donc un *layer* ne stocke jamais le `State` dans un champ et ne le réutilise pas d'un *retry* à l'autre. Mémoïser le `State` dans `tr.Context` est sûr, parce que ces données locales sont par transaction ; un champ de *layer* ne l'est pas. Comme les méthodes d'un *layer* prennent une transaction au lieu d'en ouvrir une, une seule *retry loop* peut piloter plusieurs *layers* de façon atomique : insérez un document, mettez un *job* en file, et publiez un événement dans la même transaction, et soit tous sont validés, soit aucun.

La forme d'un *layer* repose sur la *retry loop* de transaction, qui exécute un *handler* plus d'une fois et qui fait l'objet de [Transactions](../transactions/index.md). [La page des guides pratiques](how-to.md) montre un *layer* complet avec un index secondaire, comment composer des *layers*, et comment garder un index cohérent.
