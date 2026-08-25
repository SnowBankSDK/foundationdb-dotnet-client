# CrystalJson : ce que c'est, et pourquoi

CrystalJson est la *stack* JSON de `SnowBank.Core` (*namespace* `SnowBank.Data.Json`). Toutes les
couches de ce SDK qui stockent ou transmettent des documents (valeurs dans FoundationDB,
collections de documents, enregistrements de changements) sérialisent à travers elle. Cette page
explique ce qu'est CrystalJson et pourquoi il a cette forme ; pour les guides pratiques, voir
[Travailler avec CrystalJson](serializing.fr.md), et pour les tables complètes des attributs, des
*settings* et des diagnostics, voir la [référence](reference.fr.md).

Ce n'est **pas** `System.Text.Json`, ni Newtonsoft. Les noms de types semblent familiers
(`JsonObject`, `JsonArray`) mais l'API est différente, et ces différences sont le cœur du sujet.

## Pourquoi : le problème de l'aller-retour *POCO*

Une application distribuée sur FoundationDB manipule beaucoup de données représentées en JSON. La
méthode classique, désérialiser vers un *POCO* (*Plain Old CLR Object*, un concept qu'on rencontre
aussi sous les noms *DTO*, *Data Transfer Object*, ou *view model*), utiliser l'objet, le resérialiser,
a deux défauts à cette échelle :

- **Elle peut être inefficace.** Matérialiser un document entier coûte des allocations et du CPU,
  même quand le code ne consomme que trois de ses quarante champs, ou n'en modifie qu'un seul.
- **Elle tronque silencieusement quand le schéma évolue.** Un *POCO* ne conserve que les champs
  qu'il déclare. Quand plusieurs versions d'un composant coexistent, un composant plus ancien qui
  lit, modifie et réécrit un document à travers son *POCO* d'ancienne génération supprime tous les
  champs qu'il ne connaît pas :

```csharp
// le document stocké a été écrit par un composant plus récent :
// {"id":"B123","title":"Dune","rating":4.5}

// le modèle de ce composant ne connaît pas "rating"
record Book(string Id, string Title);

var book = CrystalJson.Deserialize<Book>(json);
// => book ne porte pas "rating" ; on le modifie, on le réécrit :
//    le document stocké a maintenant PERDU "rating"
```

CrystalJson répond par une échelle de trois représentations, choisies au cas par cas, jamais une
fois pour toute l'application :

1. **Le DOM** (*Document Object Model* : `JsonObject`, `JsonArray`, ...) est l'extrémité sûre : le
   *parsing* ne paie aucun coût de projection, et aucun champ n'est jamais perdu, puisque rien
   n'est projeté. Le prix est
   le typage : du code DOM ressemble plus à du JavaScript qu'à du C#.
2. **Les *proxies* générés** sont l'intermédiaire : le générateur de source émet des vues en lecture
   seule et en écriture qui exposent une forme fortement typée *au-dessus* du DOM. Le code lit
   `proxy.Title` avec IntelliSense et vérification à la compilation, pendant que le document
   en dessous garde tous les champs avec lesquels il est arrivé. Rien n'est matérialisé tant que
   personne ne demande le *POCO* complet.
3. **Les *POCO*** restent disponibles pour les cas qui leur conviennent : un type que le composant
   courant possède entièrement, ou une frontière où la vie du document s'arrête de toute façon.

Deux propriétés du DOM soutiennent cette échelle. Un `JsonObject` ou un `JsonArray` est soit
***mutable*, soit en lecture seule** : une valeur en lecture seule est profondément immuable, donc
les documents fréquemment demandés peuvent être mis en cache en mémoire et partagés entre *threads*
sans risque de corruption, et le ***copy-on-write*** est le *pattern* pour « muter » un document
figé (on modifie une copie, l'original figé reste intact). Le DOM a aussi
des ***wrappers* observables** qui enregistrent quels champs ont été lus ou écrits, ce que les
couches réactives construites sur cette *stack* utilisent pour les abonnements et la génération de
patchs ; ces *wrappers* appartiennent aux couches qui les distribuent, et leur documentation vit
avec elles.

Deux engagements plus modestes complètent la conception. Les valeurs se parsent depuis et se
sérialisent vers des `Slice` / *spans* UTF-8 sans `string` intermédiaire (les voisins de cette
*stack* parlent en octets). Et la navigation a la ***null propagation* intégrée** : un champ absent
se lit comme `JsonNull.Missing` au lieu de *throw*, chaque lecture énonce sa propre politique (une
valeur par défaut, ou une lecture obligatoire qui *throw*), et les *proxies* générés propagent
l'absence de la même façon. D'entrée de jeu, cela supprime toute une classe de
`NullReferenceException` en production, et le *boilerplate* de *null checks* qui s'en protège.

## Le modèle à deux étages

CrystalJson est deux étages utilisés ensemble. La classe statique `CrystalJson` est le point
d'entrée de la voie *POCO* (`Serialize`, `Deserialize`) ; les types du DOM se parsent et se
construisent eux-mêmes (`JsonValue.Parse`, `JsonObject.Parse`, `JsonValue.FromValue`) :

- **Le DOM** (`JsonValue` et ses sous-types) : un arbre que l'on parse, parcourt, construit et
  modifie. À utiliser pour le JSON dynamique ou sans schéma : configuration, documents
  arbitraires, enregistrements de changements.
- **Le *source generator*** (`SnowBank.Serialization.Json.CodeGen`) : pour vos propres types
  métier. Une classe *container* (ou le type lui-même, en mode auto-sérialisable) déclare les types
  qu'elle sérialise, et le générateur émet à la compilation des convertisseurs sans réflexion,
  plus les *proxies* typés en lecture seule et en écriture de l'échelle ci-dessus.

Un type sans convertisseur généré se sérialise quand même : le **chemin par réflexion** construit
un contrat à l'exécution à partir des mêmes attributs. Les deux chemins sont tenus au même
résultat, octet pour octet, et quand une combinaison d'attributs leur donnerait deux réponses
différentes, la politique est de la refuser bruyamment plutôt que de laisser le résultat dépendre
du chemin qui a sérialisé la valeur.

## Un type, un seul format de sortie

Cette politique de refus a un nom parce qu'elle vise un *pattern legacy* bien précis : le ***DTO* à
double sortie**. Certains parcs applicatifs ont accumulé des types annotés pour deux sérialiseurs
à la fois, si bien que la même classe produisait deux documents différents selon la bibliothèque
qui la sérialisait :

```csharp
public class Order
{
    [DataMember(Name = "order_id")]     // le nom qu'émettait DataContractJsonSerializer
    [JsonProperty("orderId")]           // le nom qu'émettait Newtonsoft
    public string? Id { get; set; }

    [DataMember]                        // présent sur la sortie DCJS...
    [JsonIgnore]                        // ...caché de la sortie Newtonsoft
    public string? InternalCode { get; set; }
}
```

Cela a toujours été un *hack*, pas une technique prise en charge : ça ne tient que tant que chaque
site d'appel choisit soigneusement le bon sérialiseur, et un seul mauvais choix envoie à un
consommateur le document de l'autre. CrystalJson ne peut pas l'honorer, même en principe, parce
qu'il a lui-même deux chemins de sérialisation (réflexion et généré), et « quel document
vais-je obtenir » ne doit jamais dépendre du chemin. Les deux membres ci-dessus sont donc des
**erreurs de compilation**, pas des choix : le double nom est refusé (`CJSON0011`), et la paire
inclusion-plus-ignore-inconditionnel est refusée (`CJSON0008`). Le remède est toujours la
scission : un *DTO* par contrat de format, chacun portant un seul jeu cohérent d'attributs. La
même politique rejette la signature de *callback* de l'ère `DataContractJsonSerializer` plutôt que
de l'approximer. Le [guide de migration](../../releases/7.4.3.md) documente chaque refus
avec son identifiant de diagnostic et son remède.

Notez que le *DTO* à double sortie est un besoin différent de servir des consommateurs *legacy* et
modernes depuis les mêmes types, ce qui est pris en charge et fait l'objet de la section
suivante : là, les *types* sont partagés et ce sont les *containers* qui diffèrent, donc chaque
sortie reste un contrat complet et cohérent.

## Le pont de migration

Les parcs applicatifs anciens (`DataContractJsonSerializer`, Newtonsoft) ne peuvent généralement
pas changer leur format JSON le jour où ils se modernisent : des consommateurs figés analysent
encore les anciens octets. CrystalJson traite cette situation comme un chemin de migration pris
en charge, pas comme un obstacle :

- **La lecture est tolérante, toujours.** Énumérations numériques ou textuelles, les deux formes
  de dictionnaires, les deux formes de durées et le format de dates Microsoft sont acceptés en
  lecture quels que soient les *settings*. Producteurs et consommateurs évoluent indépendamment.
- **Le profil de compatibilité reproduit le format historique.**
  `CrystalJsonSettings.DataContractCompat` émet ce que `DataContractJsonSerializer` émettait,
  octet pour octet, avec une courte liste documentée de différences. Un composant adopte
  CrystalJson d'abord, et ses consommateurs voient les mêmes octets.
- **Le format moderne vient ensuite, à votre rythme.** Un montage à double *container* sert les deux
  formats depuis les mêmes types, donc la bascule peut être globale, composant par composant, ou
  par requête (choisie par un en-tête, un *user agent*, tout ce qui distingue un consommateur
  ancien d'un moderne). Supprimez le *container* de compatibilité quand le dernier consommateur
  ancien a disparu.

## Sa place dans la *stack*

`FdbValue.ToJson(obj)` encode une valeur FoundationDB à travers CrystalJson ; une couche de
collection de documents construite sur ce SDK stocke ses documents comme des valeurs sérialisées
par CrystalJson avec des convertisseurs générés ; [CrystalXml](../CrystalXml.md) réutilise les
mêmes *containers*, le même mécanisme d'*enrollment* et les mêmes *settings* pour émettre du XML depuis les mêmes
types. Une application construite sur ce SDK peut utiliser une autre bibliothèque JSON à sa
frontière HTTP, mais la couche de données parle CrystalJson de bout en bout.
