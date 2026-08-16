# Tuples

> Cette page explique en détail le modèle de tuples. Dans cette bibliothèque, les tuples sont la façon d'encoder les **clés** (et parfois les valeurs) : l'encodage binaire des tuples produit des octets dont l'ordre de tri correspond à l'ordre logique des éléments, exactement ce qu'il faut au *keyspace* ordonné de FoundationDB. Pour savoir comment les tuples deviennent des clés de base de données via les *subspaces*, voir le [guide Clés, valeurs et *Layers*](index.md).

_« Un tuple est une liste ordonnée d'éléments. »_ - [Wikipedia](http://en.wikipedia.org/wiki/Tuple)

<pre>
         0       1                      2
    +---------+-----+--------------------------------------+
t = | "Hello" | 123 | 773166b7-de74-4fcc-845c-84080cc89533 |
    +---------+-----+--------------------------------------+
</pre>

Ce tuple a une taille de 3 : trois éléments dans un ordre fixe, aux positions 0, 1 et 2.

La différence avec un `struct` classique, c'est que les éléments n'ont pas de noms, seulement des positions : `t[0]`, `t[1]`, ..., `t[i]` avec `0 <= i < N`, comme un tableau.

La différence avec un tableau, c'est que tous les éléments peuvent avoir un type différent.

Il y a plusieurs façons de représenter un tuple en texte brut, et l'une d'elles est sous forme de vecteur :

<pre>("Hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})</pre>

Cette forme textuelle est pour les humains. Sur le disque, le tuple est une chaîne binaire compacte, et ses octets se trient dans le même ordre que les éléments. Chaque élément commence par un **marqueur de type** d'un octet, puis sa valeur :

```fdb-bytes
tuple: ("Hello", 123, {guid})
str   .02 'Hello' .00      # chaîne "Hello"
int   .15 7B               # entier 123
uuid  .30 <uuid:16>        # UUID 128 bits
```

Ce sont les marqueurs qui font fonctionner l'ordonnancement : ils trient les types dans un ordre fixe, et les octets de valeur ordonnent les valeurs au sein d'un type. Un élément long ou opaque comme un UUID est montré replié, plutôt que sous forme de seize octets bruts.

Il y a un cas particulier, pour le tuple de taille 1, où l'on ajoute d'habitude une `,` supplémentaire à la fin, pour le distinguer d'une expression :

<pre>("Hello", )</pre>

Le tuple vide a une taille de 0 :

<pre>()</pre>

### Pourquoi pas `object[]` ou `Tuple<...>`

L'implémentation minimale d'un tuple est un tableau `object[]`. Elle n'est ni efficace ni sûre pour des clés construites à partir d'éléments de types différents : chaque type valeur (int, Guid, bool, ...) est *boxé*, et relire un élément est un *cast* aveugle. Le 3e élément était-il un `int` ou un `long` ? Une mauvaise supposition, c'est une `InvalidCastException` à l'exécution.

```CSharp
// dans l'application A qui a encodé une clé...
var items = new object[] { "Hello", 123, Guid.NewGuid() };
// une allocation pour le tableau object[], et deux allocations pour boxer l'int et le guid !
var key = SomeLibrary.Encode(items);

// dans une autre application B qui décode la même clé
var items = SomeLibrary.Decode(key);
var a = (string)items[0];
var b = (long)items[1]; // ÉCHEC : c'est en fait un int !
var c = (Guid)items[2];
var d = (int)items[3]; // ÉCHEC : il n'y a pas de 4e élément !
```

Les classes `Tuple<...>` de la BCL indiquent les types et le nombre d'éléments, ce qui rétablit la sûreté de typage et IntelliSense.

```CSharp
// dans l'application A qui a encodé une clé...
Tuple<string, int, Guid> items = Tuple.Create("Hello", 123, Guid.NewGuid());
// une seule allocation pour l'instance de Tuple
var key = SomeLibrary.Encode(items);

// dans une autre application B qui décode la même clé
Tuple<string, int, Guid> items = SomeLibrary.Decode<string, int, Guid>(key);
string a = items.Item1;
int b = items.Item2;
Guid c = items.Item3;
```

Les classes de la BCL s'arrêtent là : vous ne pouvez ni les combiner ni les découper, et vous devez toujours savoir que le 2e élément était un `int`, pas un `long` ni un `uint`. L'encodage des clés a besoin d'une API de tuples plus riche, et c'est ce que fournit cette bibliothèque.

## IVarTuple

L'interface `IVarTuple`, définie dans `SnowBank.Data.Tuples`, est la base de toutes les différentes implémentations de tuples, chacune ciblant un cas d'usage précis.

Cette interface expose l'API minimale que chaque variante doit implémenter, et sert à son tour à un ensemble de méthodes d'extension qui ajoutent un comportement plus générique sans avoir à être réimplémentées dans chaque variante.

Il y a aussi une classe statique, `STuple`, qui contient des méthodes pour créer et manipuler toutes les variantes.

_note : l'interface s'appelle `IVarTuple` (et non `ITuple`) parce que la BCL définit déjà un `ITuple`, et nous ne pouvions pas nommer notre helper statique `Tuple` sans entrer en collision avec la classe `Tuple` de la BCL. `IVarTuple` implémente bien le `System.Runtime.CompilerServices.ITuple` de la BCL pour l'interop._

### Types de tuples

Les tuples s'adaptent à différents cas d'usage : certains ont une taille et des types fixes (comme les tuples de la BCL), d'autres sont de longueur variable (comme un vecteur). Certains devraient être des `struct` (pour éviter les allocations dans les boucles serrées), d'autres des types référence. Et certains sont de simples *wrappers* autour d'un *blob* binaire encodé qui diffèrent le décodage jusqu'à ce que les éléments soient accédés.

C'est pourquoi il existe plusieurs variantes, toutes implémentant `IVarTuple` :

- `STuple<T1>` … `STuple<T1, …, T8>` sont l'équivalent des `Tuple<…>` de la BCL, mais implémentés en **structs** (jusqu'à 8 éléments). Ils sont efficaces comme étape temporaire lors de la construction de tuples plus grands, et idéaux quand vous voulez la sûreté de typage et un bon IntelliSense, puisque les types des éléments sont connus à la compilation.
- `ListTuple` encapsule un `object[]` et en expose un sous-ensemble ; prendre un sous-intervalle est peu coûteux parce qu'il ne copie pas les éléments.
- `JoinedTuple` colle deux tuples ensemble (de n'importe quel type) ; `LinkedTuple` est le cas particulier de l'ajout d'une seule valeur à un tuple existant.
- Plus des variantes internes pour les représentations *parsées* et mises en cache : par exemple, certaines qui décodent de façon *lazy* seulement les éléments que vous accédez réellement, ou qui mettent en cache l'encodage binaire d'un préfixe fréquemment réutilisé.

### Créer un tuple

La façon la plus simple de créer un tuple est à partir de ses éléments :

```CSharp
var t = STuple.Create("Hello", 123, Guid.NewGuid());
```

Le type réel du tuple sera `STuple<string, int, Guid>`, qui est un `struct`. Comme nous utilisons le mot-clé `var`, tant que `t` reste à l'intérieur de la méthode, il ne sera pas *boxé*.

On peut aussi créer un tuple en ajoutant quelque chose à un tuple existant, même en partant du tuple Empty :

```CSharp
var t = STuple.Empty.Append("Hello").Append(123).Append(Guid.NewGuid());
```

Ici _t_ est toujours un `struct` de type `STuple<string, int, Guid>`, et rien n'est alloué : le tuple Empty est un singleton, et les appels intermédiaires à `Append()` ont renvoyé des `struct` de type `STuple<string>` et `STuple<string, int>`. Au-delà de 8 éléments, la chaîne bascule vers une variante à base de tableau.

Si nous avons une liste d'éléments de taille variable, nous pouvons aussi en créer un tuple :

```CSharp
IEnumerable<MyFoo> xs = ....;
// xs est une séquence d'objets MyFoo, avec une propriété Id (de type Guid)
var t = STuple.FromEnumerable(xs.Select(x => x.Id));
```

Quand tous les éléments d'un tuple sont du même type, vous pouvez utiliser des versions spécialisées :
```CSharp
var xs = new [] { "Bonjour", "le", "Monde!" };
var t = STuple.FromArray<string>(xs);
```

Si vous utilisiez déjà le Tuple de la BCL, vous pouvez facilement convertir de l'un vers l'autre, via un ensemble d'opérateurs de *cast* implicites et explicites :

```CSharp
var bcl = Tuple.Create("Hello", 123, Guid.NewGuid());
STuple<string, int, Guid> t = bcl; // cast implicite

var t = STuple.Create("Hello", 123, Guid.NewGuid());
Tuple<string, int, Guid> bcl = (Tuple<string, int, Guid>) t; // cast explicite
```

Vous pouvez aussi créer un tuple en copiant les éléments d'un tableau `object[]` :

```CSharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.FromObjects(xs); // => ("hello", 123, guid)
var t2 = STuple.FromObjects(xs, 1, 2); // => (123, guid)
xs[1] = 456; // ne changera pas le contenu des tuples
// t[1] => 123
```

`STuple.Wrap` évite la copie en encapsulant le tableau lui-même. Cela casse le contrat d'immuabilité de l'API de tuples : une écriture ultérieure dans le tableau modifie le tuple. Ne l'utilisez que lorsque vous contrôlez le tableau pendant toute sa durée de vie.

```CSharp
var xs = new object[] { "Hello", 123, Guid.NewGuid() };
var t1 = STuple.Wrap(xs); // pas de copie !
var t2 = STuple.Wrap(xs, 1, 2); // pas de copie !
xs[1] = 456; // va changer le contenu des tuples !!
// t[1] => 456
```

### Utiliser un tuple

La première chose à vérifier sur un tuple est sa taille. Chaque tuple expose une propriété `Count` avec le nombre d'éléments (0 à N), et un ensemble de méthodes d'extension utilitaires vérifie la taille avant que vous n'accédiez aux éléments :

- `t.IsNullOrEmpty()` renvoie `true` si `t == null` ou `t.Count == 0`
- `t.OfSize(3)` vérifie que `t` n'est pas null, et que `t.Count` est égal à 3, puis renvoie le tuple lui-même, ce qui vous permet d'écrire : `t.OfSize(3).DoSomethingWhichExpectsThreeElements()`
- `t.OfSizeAtLeast(3)` fonctionne de la même façon, sauf qu'elle vérifie que `t.Count >= 3`

Avec un `struct` `STuple<T1, ...>`, vous pouvez sauter cette étape, puisque la taille est connue à la compilation.

Pour lire le contenu d'un tuple, appelez `t.Get<T>(index)`, où `index` est la position _dans le tuple_ de l'élément, et `T` le type vers lequel la valeur est convertie.

```CSharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(0); // => "hello"
var y = t.Get<int>(1); // => 123
var z = t.Get<Guid>(2); // => guid
```

Si `index` est négatif, alors il est relatif à la fin du tuple, où -1 est le dernier élément, -2 l'avant-dernier, et -N le premier élément.

```CSharp
var t = STuple.Create("hello", 123, Guid.NewGuid());
var x = t.Get<string>(-3); // => "hello"
var y = t.Get<int>(-2); // => 123
var z = t.Get<Guid>(-1); // => guid
```

### Sortie texte

Chaque tuple redéfinit `ToString()` et rend son contenu dans un format standardisé unique :

```CSharp
var t1 = STuple.Create("hello", 123, Guid.NewGuid());
Console.WriteLine("t1 = {0}", t1);
// => t1 = ("hello", 123, {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create("hello");
Console.WriteLine("t1 = {0}", t2);
// => t2 = ("hello",)
var t3 = STuple.Empty;
Console.WriteLine("t3 = {0}", t3);
// => t3 = ()
```

Un tuple de taille 1 se rend avec une virgule finale (`(123,)` au lieu de `(123)`), ce qui le distingue d'une expression entre parenthèses.

### Tuples imbriqués

Un tuple est un vecteur d'éléments, donc un tuple peut contenir un autre tuple :

```CSharp
var t1 = STuple.Create("hello", STuple.Create(123, 456), Guid.NewGuid());
// t1 = ("hello", (123, 456), {773166b7-de74-4fcc-845c-84080cc89533})
var t2 = STuple.Create(STuple.Create("a", "b"));
// t2 = ((a, b),)
var t3 = STuple.Create("hello", STuple.Empty, "world");
// t3 = ("hello", (), "world");
```

_note : L'erreur facile est d'appeler `t1.Append(t2)` au lieu de `t1.Concat(t2)`, ce qui ajoutera t2 comme un seul élément à la fin de t1, au lieu d'ajouter les éléments de t2 à la fin de t1._

Cela peut être utile quand vous voulez modéliser une clé de taille fixe : `(product_id, location_id, order_id)` où location_id est une clé hiérarchique de taille variable, tout en gardant une taille fixe de 3 :

```CSharp
var productId = "B00CS8QSSK";
var locationId = new [] { "Europe", "France", "Lille" };
var orderId = Guid.NewGuid();

var t = STuple.Create(productId, STuple.FromArray(locationId), orderId);
// t.Count => 3
// t[0] => "B00CS8QSSK"
// t[1] => ("Europe", "France", "Lille")
// t[2] => {773166b7-de74-4fcc-845c-84080cc89533}
```

Le code qui *parse* la clé peut toujours lire `t[2]` pour obtenir l'order_id, quelle que soit la taille du location_id.

### Combiner des tuples

Les tuples sont immuables : aucune méthode ne modifie un élément en place. À la place, `Substring`, `Append` et `Concat` renvoient un nouveau tuple, avec ou sans copie des éléments (selon la variante).

Le cas le plus courant ajoute une valeur à un tuple avec `t.Append<T>(T value)` : par exemple, un tuple de base mis en cache plus un identifiant de document.

```CSharp
var location = STuple.Create("Acme", "Documents");

var documentId = Guid.NewGuid();
var t = location.Append(documentId);
// t => ("Acme", "Documents", {773166b7-de74-4fcc-845c-84080cc89533});
```

Rappelez-vous qu'`Append` avec un tuple en argument l'ajoute comme un seul élément imbriqué. Pour fusionner les éléments de deux tuples, utilisez `t1.Concat(t2)`, qui renvoie un nouveau tuple avec les éléments des deux :

```CSharp
var location = STuple.Create("Acme", "OrdersByProduct");

var productId = "B00CS8QSSK";
var orderId = Guid.NewGuid();
var t1 = STuple.Create(productId, orderId)
// t1 => ("B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533})

var t2 = location.Concat(t1);
// t2 => ("Acme", "OrdersByProduct", "B00CS8QSSK", {773166b7-de74-4fcc-845c-84080cc89533});
```

### Découper des tuples

Un sous-ensemble d'un tuple s'obtient via l'une des méthodes `t.Substring(...)`, ou via l'indexeur `t[from, to]`.

`Substring()` fonctionne de la même façon que sur une chaîne :

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Substring(0, 3); // => (1, 2, 3)
var v = t.Substring(5, 2); // => (6, 7)
var w = t.Substring(7); // => (8, 9, 10)

// fonctionne aussi avec l'indexation négative !
var w = v.Substring(-3); // => (8, 9, 10)
```

L'indexeur `t[from, to]` renvoie les éléments aux positions `from <= p < to` : la borne `to` est exclue.

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t[0, 3]; // => (1, 2, 3)
var v = t[5, 7]; // => (6, 7)
// rappelez-vous que 'to' est exclu !
var w = t[7, -1]; // => (8, 9)
// pour corriger ça, vous pouvez utiliser 'null' ("jusqu'à la fin")
var w = t[7, null]; // => (8, 9, 10)

// fonctionne aussi avec l'indexation négative !
var w = v[-3, null]; // => (8, 9, 10)
```

`t.Truncate(3)` est un raccourci pour `t.Substring(0, 3)` :

```CSharp
var t = STuple.Create(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
var u = t.Truncate(3);
// u => (1, 2, 3);
var v = t.Truncate(-3);
// v => (8, 9, 10);
```

### Décoder vers des types modèles

Le code qui décode des clés extrait souvent un nombre fixe d'éléments dans des variables locales, puis construit une instance d'une classe modèle de l'application :

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var x = tuple.Get<string>(0);
    var y = tuple.Get<int>(1);
    var z = tuple.Get<Guid>(2);
    return new MyFooBar(x, y, z);
}
```

Cette méthode a des problèmes :

- pas de vérification null sur `tuple` ;
- pas de vérification que `tuple.Count` vaut exactement 3 ;
- une ligne `tuple.Get<...>(0)` copiée-collée dont l'index n'a jamais été changé en 1 ou 2 compile sans problème et lit le mauvais élément.

Les *helpers* `t.As<T1, ..., TN>()` convertissent un `IVarTuple` en `STuple<T1, ..., TN>`, ce qui rétablit la vérification de taille, la sûreté de typage et IntelliSense :

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    var t = tuple.As<string, int, Guid>();
    // lève une exception si tuple est null, ou n'est pas de taille 3
    return new MyFooBar(t.Item1, t.Item2, t.Item3);
}
```

Deux éléments du même type peuvent encore être intervertis par erreur. Les surcharges `t.With<T1, ..., TN>(Action<T1, ..., TN>)` et `t.With<T1, ..., TN, TResult>(Func<T1, ..., TN, TResult>)` donnent des noms aux éléments :

```CSharp
public MyFooBar DecodeFoobar(IVarTuple tuple)
{
    return tuple.With((Guid productId, Guid categoryId, Guid orderId) => new MyFooBar(productId, categoryId, orderId));
    // les trois éléments sont des GUID, mais donner un nom aide à repérer les erreurs d'inversion d'arguments
}
```
