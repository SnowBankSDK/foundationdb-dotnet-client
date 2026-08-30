# CrystalXml : sortie XML générée pour CrystalJson

CrystalXml est une surcouche de sortie XML en écriture seule pour le *source generator* de
CrystalJson. Un *container* qui génère déjà des sérialiseurs JSON peut activer le XML avec un seul
attribut, et chaque type qu'il enregistre gagne une famille de sorties `ToXmlText` / `WriteXmlTo`
générées à la compilation : zéro réflexion à l'exécution, pas de `System.Xml.Serialization`, et une
sortie exacte à l'octet sur les *sinks* texte.

Il existe pour permettre à une application de remplacer la production XML basée sur
`DataContractSerializer` (le « format DCS ») par du code généré, tout en gardant la compatibilité à
l'octet avec les documents que ses consommateurs (par exemple une couche de rendu XSLT) analysent
déjà, et, indépendamment, pour donner aux *containers* modernes orientés JSON une projection XML
propre.

Il n'y a délibérément pas de `FromXml` : CrystalXml écrit du XML, il ne le lit jamais.

## Déclarer la sortie

Deux niveaux : le *container* dit quels formats il produit, les membres disent à quoi ils
ressemblent dans le format XML.

Un *container* est un marqueur neutre vis-à-vis du format, plus un attribut par format de sortie.
Les types qu'il sérialise sont enregistrés une seule fois, de façon neutre vis-à-vis du format : le
même enregistrement alimente chaque format que le *container* produit.

```csharp
// niveau container : le marqueur neutre, puis un attribut de sortie par format
[CrystalConverter]                                    // « cette classe héberge du code généré »
[CrystalJsonOutput(CrystalJsonSerializerDefaults.DataContractCompat)]
[CrystalXmlOutput]                                    // activation : chaque type du container reçoit une sortie XML
[CrystalSerializable(typeof(ClientAccount))]          // enregistrement neutre vis-à-vis du format
public static partial class LegacyRenderSerializers { }
```

| Attribut | Namespace | Rôle |
|---|---|---|
| `[CrystalConverter]` | `SnowBank.Data` | le marqueur de container ; ne dit rien sur les formats |
| `[CrystalSerializable(typeof(T))]` | `SnowBank.Data` | enregistre un type racine ; répétable ; alimente chaque format de sortie |
| `[CrystalJsonOutput(...)]` | `SnowBank.Data.Json` | demande le format JSON, et porte ses paramètres (profil, politique de nommage, insensibilité à la casse) |
| `[CrystalXmlOutput(...)]` | `SnowBank.Data.Xml` | demande le format XML, et porte ses paramètres (le préréglage de format, `DictionaryFormat`) |
| `[CrystalJsonConverter(...)]` | `SnowBank.Data.Json` | alias mono-format : `[CrystalConverter]` + `[CrystalJsonOutput]` avec les mêmes paramètres |
| `[CrystalXmlConverter(...)]` | `SnowBank.Data.Xml` | alias mono-format : `[CrystalConverter]` + `[CrystalXmlOutput]` avec les mêmes paramètres |

`[CrystalJsonSerializable(typeof(T))]` est l'ancienne orthographe de `[CrystalSerializable]`. Elle
fonctionne toujours (et génère du code identique à l'octet) mais elle est `[Obsolete]` :
l'enregistrement n'a jamais été spécifique à JSON.

### La table de vérité

| Attributs du container | Généré |
|---|---|
| `[CrystalConverter]` + `[CrystalJsonOutput]` | JSON uniquement |
| `[CrystalJsonConverter]` | JSON uniquement (alias de la ligne ci-dessus) |
| `[CrystalConverter]` + `[CrystalXmlOutput]` | **XML uniquement** : pas de `Serialize`/`Pack`/`Unpack`, pas de *proxies* JSON, pas de facette `IJsonConverter`, pas de `TypeMapper` |
| `[CrystalXmlConverter]` | XML uniquement (alias de la ligne ci-dessus) |
| `[CrystalConverter]` + les deux sorties | les deux formats, à partir d'un seul jeu de types enregistrés |
| `[CrystalJsonConverter]` + `[CrystalXmlOutput]` | **rejeté** (CRYS0002) : les alias mono-format ne se combinent pas |
| `[CrystalXmlConverter]` + `[CrystalJsonOutput]` | **rejeté** (CRYS0002), symétriquement |
| `[CrystalConverter]` seul | **rejeté** (CRYS0001) : un container qui ne nomme aucun format de sortie ne génère rien |
| plusieurs marqueurs de container sur une même classe | **rejeté** (CRYS0003) |

Un *container* XML uniquement n'a aucun profil JSON dont dériver, donc `[CrystalXmlOutput]` sans
paramètre se résout vers le format général, et ses noms d'éléments sont les noms de membres déclarés
(la politique de nommage est un paramètre de `[CrystalJsonOutput]`). Un *container* qui a besoin à la
fois d'une politique de nommage JSON et de son miroir XML déclare les deux sorties.

`[CrystalXmlOutput]` / `[CrystalXmlConverter]` choisissent le format par un préréglage du
constructeur : `[CrystalXmlOutput(CrystalXmlSerializerDefaults.General)]` ou
`[CrystalXmlOutput(CrystalXmlSerializerDefaults.DataContractCompat)]`. La forme sans paramètre (`Inherit`) dérive
le format du profil JSON du container (un profil JSON `DataContractCompat` donne le format DCS, tout
le reste donne le format général). Une combinaison incohérente (une politique de nommage à côté du
format DCS) est une erreur de build (CXML0001). Les options nommées :

| Option | Signification |
|---|---|
| `DictionaryFormat` | valeur par défaut du container pour la forme de dictionnaire (voir le profil général ci-dessous) |
| `OmitNamespaces` | format DCS uniquement : reproduit le fil dépouillé sans namespaces, octet pour octet. Sur le profil général l'option est inerte, et CXML0012 le dit |

```csharp
// niveau MEMBER : tout le XML vit dans [XmlProperty] (namespace SnowBank.Data.Xml)
[XmlProperty("@id")]                     // sucre syntaxique : normalisé au build en Name="id" + Attribute=true
[XmlProperty(ItemName = "tag")]          // forme de collection encapsulée, nommage des entrées pour les dictionnaires
```

Échelle de résolution par *setting* (jamais tout ou rien) :

1. les valeurs par défaut du profil du *container* (compat ou général) ;
2. `[JsonProperty]` / `[JsonPropertyName]` : fournissent le nom, pris tel quel (jamais remodelé
   par la politique de nommage) ;
3. `[XmlProperty]` : surcharge finale, option par option (un `ItemName` seul laisse le nom
   retomber sur l'étape 2, puis sur le nom de membre .NET via la politique de nommage).

`ItemName` est un concept purement XML : il ne rejoint jamais `[JsonProperty]`.

Règle absolue : aucune forme de sortie n'est jamais choisie par une heuristique sur les données.
Si la sortie varie, c'est qu'un attribut ou une option l'a demandé explicitement en amont. Tout
cas inexprimable est une erreur de build (la plage de diagnostics CXML) ou une exception typée à
l'exécution, jamais un *fallback* silencieux.

## *Pipeline* d'exécution

```
   code généré (un corps par type)
        |   WriteXml<TEmitter>(ref TEmitter emitter, T value)   where TEmitter : struct, ICrystalXmlEmitter
        v
  ICrystalXmlEmitter    -- jeu d'événements : StartElement / Attribute / Text / EndElement / RawAscii
        |
        +-- CrystalXmlWriter<TRune, TWriter>       TEXT : l'unique implémentation char + byte
        |     where TRune : unmanaged (char|byte)    formes exactes à l'octet, toujours passée par ref
        |     where TWriter : struct, IBufferWriter<TRune>
        |
        +-- CrystalXDocumentEmitter                        infoset : construit le DOM directement
        +-- CrystalXmlWriterEmitter                        infoset : délègue à System.Xml (interop)
```

Les noms d'éléments et d'attributs sont précalculés par le générateur en double représentation
(une *string* plus un littéral UTF-8 figé) dans des champs statiques `CrystalXmlName`, avec le
namespace de contrat incorporé au nom sur le format DCS, si bien que le chemin *byte* ne transcode
jamais un nom à l'exécution. Un nom ne porte jamais de préfixe : l'*emitter* attribue les préfixes
selon ce qui est en portée à sa profondeur. Les membres non publics passent par les mêmes *thunks*
`[UnsafeAccessor]` que du côté JSON. Le polymorphisme est un *switch* généré sur les types dérivés
connus du graphe ; un type d'exécution hors du graphe lève une exception typée.

Sorties publiques sur le *holder* généré (aucune ne passe par une autre) :

| Sortie | Chemin réel |
|---|---|
| `ToXmlText(value)` | cœur *char* sur `IBufferWriter<char>` |
| `WriteXmlTo(TextWriter, value)` | adaptateur vers le cœur *char* |
| `ToXmlSlice(value)` / `ToXmlBytes(value)` | cœur *byte* (UTF-8, sans *string* intermédiaire) |
| `WriteXmlTo(Stream / IBufferWriter<byte>, value)` | cœur *byte* |
| `ToXDocument(value)` / `WriteXmlTo(XmlWriter, value)` | *emitters* infoset : garanties au niveau *infoset* seulement, jamais exactes à l'octet |

Chaque sortie accepte un `rootName` optionnel et un `CrystalJsonSettings` optionnel (les valeurs
par défaut viennent du profil du *container* ; `ShowNullMembers`, formats de date/durée/enum).

Interfaces miroir du côté JSON : `ICrystalXmlSerializer<T>` (la facette implémentée par les
*holders* générés ; point d'extension pour les convertisseurs sur mesure par membre, vérifié au
moment de la génération), `ICrystalXmlElementSerializer<T>` (son extension de composition :
`WriteXmlElement` plus les deux noms avec lesquels un appelant compose, implémentée par chaque
convertisseur généré) et `ICrystalXmlSerializable` (*hook* d'instance : le type écrit son propre
XML).

### Racines collection et scalaire

L'enregistrement d'une collection ou d'un scalaire nu ne génère aucun convertisseur (le générateur
signale l'avertissement CJSON0019 : enregistrez le type d'élément, pas la collection). Ces documents
passent par des points d'entrée sur `CrystalXml`, qui reflètent
les huit sorties ci-dessus :

```csharp
// une séquence d'items de contrat, composée à partir de la facette du type d'item
string xml = CrystalXml.ToText(LegacySerializers.Shelf.Default, shelves);
// <ArrayOfShelf xmlns="..."><Shelf>...</Shelf><Shelf>...</Shelf></ArrayOfShelf>

// une racine scalaire nue, sur la classe imbriquée Scalar
string xml = CrystalXml.Scalar.ToText("hello");
// <string xmlns="http://schemas.microsoft.com/2003/10/Serialization/">hello</string>
```

Le nom de racine est résolu, jamais deviné : le `rootName` de l'appelant gagne ; le format DCS se
rabat sur sa convention `ArrayOfX`, dans le namespace du contrat de l'item ; le profil général n'a
pas de convention, donc une racine collection sans `rootName` lève `CrystalXmlRootNameException`.
Les éléments d'item gardent le nom d'élément du type d'item, et `itemName` le remplace. Les points
d'entrée scalaires écrivent le fil de référence des types lexicaux xsd (le nom lexical dans le
namespace Serialization, nil quand la valeur est null) ; un type hors de cet ensemble lève
`CrystalXmlUnknownTypeException`. Les scalaires vivent sur la classe imbriquée `CrystalXml.Scalar`
plutôt qu'en surcharges : une méthode générique prenant un `T?` nu capturerait tous les appels que
les surcharges à sérialiseur ne prennent pas, et un argument mal typé doit échouer à la
compilation plutôt qu'à l'écriture.

## Le profil de compatibilité : le format DCS

La spécification exécutable est une suite comparée à un oracle `DataContractSerializer` réel
(`SnowBank.Core.Tests/Xml/DcsOutputFidelityFacts.cs`, avec les règles de namespaces verrouillées
dans `DcsNamespaceReferenceFacts.cs` ; registre de couverture à côté, dans `COVERAGE.md`), sous
deux règles d'acceptation. La sortie par défaut est tenue au fil standard sur les noms étendus :
cette émission omet les déclarations qu'elle peut prouver inutilisées et écrit les autres sur le
premier élément qui en a besoin, donc ses octets diffèrent de ceux du sérialiseur de référence
alors que chaque élément et chaque attribut se résolvent vers la même paire (namespace, nom
local). La sortie `OmitNamespaces = true` est tenue au fil dépouillé octet pour octet. Points
saillants :

- Noms de racine et de contrat : `[DataContract(Name=)]` respecté, les génériques composent `XOfY`
  avec expansion `{0}`/`{#}` (empreinte de namespace volontairement omise), types imbriqués
  `Outer.Inner`, `XmlConvert.EncodeLocalName` appliqué.
- Ordre des membres : classe de base d'abord (récursivement), les membres sans `Order=` dans
  l'ordre ordinal-alphabétique du nom de sortie, puis les groupes `Order=` croissants avec départage
  alphabétique.
- Membres en lecture seule : une propriété get-only, ou une propriété avec un *setter* privé et
  sans activation explicite, n'atteint jamais la sortie, ce qui correspond à ce que le chemin par
  réflexion du sérialiseur de référence prend sur un POCO ordinaire. Sur un type `[DataContract]`,
  cette même forme portant `[DataMember]` est au contraire rejetée au moment de la génération
  (CXML0013) : la vérification « pas de méthode set » du sérialiseur de référence la rejette
  d'emblée (`InvalidDataContractException`, "No set method for property"), donc il n'y a de toute
  façon aucun format à reproduire. Un **champ** `[DataMember]` `readonly` est une forme différente
  (cette vérification ne concerne que les propriétés) et atteint bien la sortie, octet pour octet
  avec l'oracle réel.
- Membres null : `<X nil="true" />` par défaut ; `[DataMember(EmitDefaultValue = false)]` rend le
  membre absent quand il est à sa valeur CLR par défaut.
- Collections : l'élément d'*item* est nommé d'après le nom de contrat du type d'*item* (`<string>`,
  `<int>`, `<dateTime>`, `<Shelf>`, `<ArrayOfstring>` pour une liste imbriquée) ; une collection
  vide s'auto-ferme, une *string* vide garde une paire de balises ouvrante et fermante.
- Dictionnaires : `<KeyValueOfstringstring><Key>..</Key><Value>..</Value></KeyValueOfstringstring>`,
  et `<KeyValueOfstringShelf>` quand la valeur est un type de contrat.
- Namespaces : le namespace de contrat de l'élément racine (`[DataContract(Namespace = ...)]`,
  sinon `http://schemas.datacontract.org/2004/07/` plus le namespace CLR) est son namespace par
  défaut, et un élément membre vit dans le namespace du contrat qui le déclare. Cinq namespaces
  intégrés couvrent ce qu'aucun namespace CLR ne dérive : XMLSchema-instance (les attributs
  `i:nil` et `i:type`), XMLSchema (le QName du `i:type` d'un primitif *boxé*), Arrays (les
  collections et dictionnaires génériques non annotés), Serialization (les racines scalaires
  nues) et le contrat System (`DateTimeOffset`). Un nom porte le nom local et le namespace, jamais
  de préfixe : le *writer* attribue les préfixes, garde les déclarations en portée et déclare
  chaque namespace sur le premier élément qui en a besoin. Le namespace d'instance remonte à la
  racine quand deux sous-arbres ou plus peuvent porter un marqueur nil ou type.
- Polymorphisme : attribut `i:type="<QName du contrat>"` seulement quand le contrat d'exécution
  diffère du contrat déclaré ; le nom d'élément reste celui du type déclaré, dans le namespace du
  contrat déclarant. Un type dérivé dans le namespace du *slot* écrit un nom local nu ; un type
  d'un autre namespace écrit un QName préfixé et déclare le préfixe sur le même élément. Une
  instance d'une racine polymorphe concrète écrit son propre corps, sans annotation, ce que fait
  l'oracle, et c'est là que ce profil s'écarte volontairement du profil général (voir plus bas).
- Dialecte `ISerializable` : chaque entrée `SerializationInfo` devient un élément nommé d'après la
  clé (encodée), les valeurs déclarées `object` portent un discriminant `type=`.
- Scalaires : formes lexicales DCS (dates ISO tronquées selon `DateTimeKind`, durées ISO 8601,
  `char` comme son point de code, `decimal` gardant son échelle, doubles en *round-trip*, enums par
  `[EnumMember(Value=)]` ou par nom via un *switch* généré, `DateTimeOffset` comme la structure à
  deux éléments `{DateTime, OffsetMinutes}`, `byte[]` en base64).
- Texte : pas de déclaration XML, auto-fermeture `<X />` avec une espace, fins de ligne du texte
  en CRLF brut.
- `OmitNamespaces = true` : les namespaces, préfixes et déclarations disparaissent (`i:nil` arrive
  comme `nil`, `i:type` comme `type`, le discriminant ne garde que son nom local). C'est le fil
  dépouillé historique que certains consommateurs stockent et analysent, conservé comme option
  explicite, certifiée à l'octet.

Trois écarts délibérés par rapport au DCS brut, chacun verrouillé par un test dédié, sont des
exigences :

1. Les noms d'entrées de dictionnaire ne portent pas d'empreinte de hash de namespace
   (`KeyValueOfstringShelf`, pas `KeyValueOfstringShelfQU_P9Vt29`). Mesuré : zéro consommateur de
   l'empreinte.
2. Les caractères de contrôle sont assainis au niveau de la valeur (le DCS brut émet `&#x1;`, un
   document qu'un parser conforme rejette). Un mode de reproduction stricte existe pour les harnais
   de certification. **Sur les *sinks* texte uniquement** : ce filtre vit dans `CrystalXmlWriter`,
   c'est lui qui produit la sortie. Les *emitters* infoset (`CrystalXDocumentEmitter`,
   `CrystalXmlWriterEmitter`) n'en appliquent rien, le DOM voit les caractères tels quels, et
   `XmlWriter` en répond sous son propre `CheckCharacters`.
3. Les exceptions typées (`CrystalXmlCycleException`, `CrystalXmlUnknownTypeException`,
   `CrystalXmlRootNameException`, `NotSupportedException`, `XmlException`) remplacent
   `SerializationException`.

## Le profil général : le XML qu'un lecteur JSON prédirait

| JSON | XML général |
|---|---|
| `{"title": "x"}` | `<title>x</title>` : même échelle de nommage que le JSON |
| racine | le nom du type via la même échelle ; `rootName` optionnel par appel |
| membre null | absent (comme JSON par défaut) ; `WithNullMembers()` donne `<x nil="true" />` ; `[JsonIgnore(Condition = ...)]` par membre respecté |
| `"tags": ["a","b"]` | non encapsulé par défaut : `<tags>a</tags><tags>b</tags>` ; `[XmlProperty(ItemName = "tag")]` encapsule : `<tags><tag>a</tag>...</tags>` ; une collection imbriquée nue (`List<List<T>>`) est une erreur de build (CXML0006) : introduisez un type intermédiaire |
| dictionnaire | `CrystalXmlDictionaryFormat { Default, Direct, KeyAttribute, KeyValueAttributes, KeyValueElements }` ; la valeur par défaut générale est `Direct` (`<scores><math>12</math></scores>`, clé non-NCName = exception typée à l'exécution) |
| `"$type": "cat"` | attribut `type="cat"` : le discriminant est une annotation |
| `[XmlProperty("@id")]` | `<book id="42">` : donnée comme attribut, scalaires seulement ; interdit sur le profil de compatibilité (DCS n'a pas d'attributs utilisateur) |

Une instance d'une racine polymorphe concrète est rejetée ici avec
`CrystalXmlUnknownTypeException`, là où le profil de compatibilité écrit le corps propre de la
racine. Ce format correspond au côté JSON, qui ne porte pas non plus de discriminant pour cette
valeur : un lecteur ne pourrait pas la distinguer d'un sous-type dont l'annotation aurait disparu,
donc elle est rejetée plutôt qu'écrite sous une forme que personne ne peut interpréter.

Contrairement au profil de compatibilité, le format général ne porte aucune restriction sur les
membres en lecture seule : une propriété get-only, un champ `readonly` et un membre init-only sont
tous émis, à l'image du format JSON (qui ne filtre jamais non plus sur le caractère lecture seule,
seul le désérialiseur généré s'abstient d'en réassigner un).

## Exemple

```csharp
[CrystalConverter]
[CrystalJsonOutput(CrystalJsonSerializerDefaults.Web)]      // camelCase
[CrystalXmlOutput]                                          // format dérivé : général
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }

public sealed record Book
{
	[XmlProperty("@id")]
	public required int Id { get; init; }

	public required string Title { get; init; }

	[XmlProperty(ItemName = "tag")]
	public List<string> Tags { get; init; } = [];

	public Dictionary<string, int> Scores { get; init; } = [];

	public string? Subtitle { get; init; }
}

var book = new Book { Id = 42, Title = "Dune", Tags = ["sf", "space"], Scores = { ["math"] = 12 } };
string xml = AcmeSerializers.Book.ToXmlText(book);
// <book id="42"><title>Dune</title><tags><tag>sf</tag><tag>space</tag></tags><scores><math>12</math></scores></book>
```

Un type présent dans deux *containers* a deux sérialiseurs, chacun avec le format de son profil. Le
code générique prend la facette : `void Export<T>(ICrystalXmlSerializer<T> serializer, T value, IBufferWriter<byte> output)`.

## Diagnostics et gardes à l'exécution

Trois façons dont une construction est rejetée, et laquelle s'applique est une règle, pas un choix
au cas par cas :

| Mécanisme | Quand |
|---|---|
| **diagnostic CXML** | la construction est rejetée au moment de la génération, décidable à partir des seules DÉCLARATIONS (un attribut, un type, un nom de contrat). Il pointe la déclaration fautive et porte un remède. Un membre de la plage, CXML0012, est plutôt un Info : il ne rejette rien, il nomme un *setting* que le format résolu ne consulte jamais. |
| **`#error` dans la source émise** | une impossibilité structurelle découverte pendant l'émission, qu'aucune déclaration n'aurait pu prédire. Gardé aussi comme filet inaccessible sous un diagnostic qui couvre déjà le cas. |
| **exception typée** | la décision dépend des données : seule la valeur en cours d'écriture peut la prendre (un type d'exécution hors du graphe, une clé de dictionnaire non-NCName, une valeur d'enum non déclarée, un graphe plus profond que le plafond, une racine collection que ni l'appelant ni le profil ne nomment). |

Les règles sur le container dans son ensemble (quels formats de sortie il nomme, et si ses
marqueurs se combinent) ne concernent aucun des deux formats, donc elles portent plutôt un
identifiant neutre :

| Id | Rejette |
|---|---|
| CRYS0001 | `[CrystalConverter]` ne nommant aucun format de sortie : le container ne générerait rien |
| CRYS0002 | un alias mono-format (`[CrystalJsonConverter]`, `[CrystalXmlConverter]`) à côté d'un attribut de sortie : l'alias EST le choix du format |
| CRYS0003 | plusieurs marqueurs de container sur une même classe |

Les diagnostics au moment du build sur le format XML lui-même vivent dans la plage CXML :

| Id | Rejette |
|---|---|
| CXML0001 | incohérence profil/politique sur le container : une politique de nommage (camelCase et consorts) à côté du format XML DataContract, dont les noms d'éléments viennent du contrat de données. `PropertyNameCaseInsensitive` n'est PAS un déclencheur : il décide comment un nom entrant est apparié à la lecture du JSON, et cette surcouche ne lit jamais |
| CXML0002 | forme d'enregistrement : `[CrystalXmlOutput]` sur une classe qui n'héberge aucun sérialiseur généré |
| CXML0003 | projection en attribut d'un membre sans forme lexicale |
| CXML0004 | les attributs de nommage XML sur le profil de compatibilité |
| CXML0005 | deux membres se résolvant vers le même nom XML, discriminant compris |
| CXML0006 | une collection imbriquée nue sur le profil général |
| CXML0007 | tout nom qui n'est pas un NCName légal : un nom `[XmlProperty]` ou `ItemName` déclaré, un `@` seul, la contradiction `"@x"` + `Attribute = false`, un nom de membre DÉRIVÉ de son nom JSON, et un `[DataContract(Name = ...)]` qui nommerait l'élément racine. Profil général uniquement pour les cas dérivé et racine : le format de compatibilité encode chaque nom via `XmlConvert.EncodeLocalName` |
| CXML0008 | un convertisseur de membre sans la facette XML |
| CXML0009 | un membre projeté en attribut avec un convertisseur sur mesure |
| CXML0010 | `[CollectionDataContract]` sur le type d'un membre compat |
| CXML0011 | un dictionnaire dont la forme résolue porte la valeur comme texte (`KeyAttribute`, `KeyValueAttributes`) alors que le type de la valeur n'a pas de forme lexicale |
| CXML0012 | **Info, pas une erreur** : un *setting* qui a été écrit explicitement, résolu, puis jamais consulté : un `[XmlProperty(ItemName = ...)]` sur un membre sans *items*, sur un membre dont la forme de dictionnaire RÉSOLUE est `Direct` (dont les entrées sont nommées d'après leur propre clé), ou sur un membre dont le type écrit son propre contenu XML (`ICrystalXmlSerializable`, ce qui rend aussi inerte un `DictionaryFormat` au niveau du membre : seul le NOM d'élément vient encore du membre là) ; un `[JsonIgnore(Condition = Never)]` sur un membre projeté en attribut (un attribut n'a pas de forme nil, donc un attribut null est absent de toute façon) ; un `[CrystalXmlOutput(DictionaryFormat = ...)]` sur un container dont le profil résolu est celui de compatibilité (qui a une seule forme de dictionnaire) ; et un `[CrystalXmlOutput(OmitNamespaces = true)]` sur un container dont le profil résolu est le général (le fil dépouillé est une variante du format DCS) |
| CXML0013 | profil de compatibilité uniquement : une PROPRIÉTÉ en lecture seule (get-only, ou *setter* non public sans activation explicite) portant `[DataMember]` sur un type `[DataContract]` : le sérialiseur de référence rejette ce contrat d'emblée (`InvalidDataContractException`, "No set method for property"), donc il n'y a aucun format à reproduire. Ne se déclenche pas sur un CHAMP `[DataMember]` `readonly` (la vérification du DCS ne concerne que les propriétés) ni sur un membre init-only (un *flag* différent ; le DCS l'émet) |

À l'exécution, les graphes plus profonds que `CrystalXml.MaxDepth` (64 niveaux de récursion
générée, la valeur par défaut de System.Text.Json) lèvent
`CrystalXmlCycleException` : la garde ne peut pas distinguer un vrai cycle d'un graphe acyclique
légitimement plus profond, et son message le dit. Le compteur de profondeur ne peut pas traverser
un appel vers `ICrystalXmlSerializer<T>.WriteXml` ou `ICrystalXmlSerializable.WriteXml` : un cycle
qui passe entièrement par de tels *hooks* n'est pas couvert par la garde.

Les formats JSON générés partagent le même plafond, `CrystalJsonWriter.MaxDepth`
(`CrystalXml.MaxDepth` en est un alias). Sur le chemin `Pack` généré, les gardes voyagent dans un
`CrystalJsonPackContext` que `IJsonPacker<T>.Pack` prend par ref, si bien qu'elles survivent aux
*helpers* de collection/dictionnaire (`PackObject`/`PackArray`/`PackList`/`PackEnumerable` dans
`JsonSerializerExtensions`) comme aux convertisseurs de membre sur mesure : un cycle qui passe par
un membre `List<T>` ou `Dictionary<TKey, TValue>` lève l'erreur de récursion là aussi.

Les *callbacks* du cycle de vie de sérialisation (`[OnSerializing]` / `[OnSerialized]`) **sont**
invoqués sur le chemin XML, au même endroit et via le même appel généré que sur le chemin JSON :
les deux formats sont deux rendus d'une seule sérialisation, donc un *callback* qui prépare les
membres s'exécute pour les deux, une fois par écriture. `OnSerializing` s'exécute après l'ouverture
de l'élément mais avant que quoi que ce soit ne lise la valeur (membres projetés en attribut
compris), donc ses mutations sont ce que le document porte ; `OnSerialized` s'exécute juste avant
la fermeture de l'élément. Sur le dialecte `ISerializable` du profil de compatibilité, la paire
encadre l'appel à `GetObjectData`, là où le sérialiseur de référence les déclenche aussi. Il n'y a
pas de contrepartie `OnDeserializing` / `OnDeserialized`, puisque CrystalXml ne lit jamais.

## Prérequis côté consommateur

**Activer la sortie XML ne coûte à un *container* rien que la sortie JSON ne coûtait déjà.** Le
générateur dans son ensemble exige que le projet consommateur compile en **`LangVersion` 9 ou
plus** (en dessous, il rejette avec `SYSLIB1221`, le même diagnostic et le même plancher que le
générateur de System.Text.Json, et n'émet rien du tout, JSON compris). Le code XML émis reste dans
ce plancher : les noms d'éléments et d'attributs mis en cache sont écrits comme des littéraux de
tableau `byte[]` plutôt que comme des littéraux de *string* UTF-8 `"..."u8`, ce qui aurait relevé
la barre à C# 11 pour les *containers* XML uniquement. Un projet à l'ancienne (.NET Framework est en
C# 7.3 par défaut) a donc exactement une chose à faire, et c'est la même chose qu'un *container*
JSON uniquement lui demande : mettre `LangVersion` à 9 ou plus.

**Le chemin *lite* (`netstandard2.0` / `net472`) est pris en charge.** Le *runtime* de CrystalXml
se compile pour `netstandard2.0`, et le code XML généré compile et s'exécute sur le CLR
.NET Framework. Deux parties d'un *container* généré y sont conditionnelles, et aucune n'est du
XML :

- les **proxies** JSON `ReadOnly` / `Writable` ne sont pas émis, parce que leurs interfaces ont
  besoin de membres d'interface static abstract, que le CLR netfx ne peut pas prendre en charge
  (ils sont tout aussi absents en dessous de C# 11). Les convertisseurs, le `TypeMapper` et toute
  la surface XML sont émis normalement ;
- les annotations de *trimming* `[DynamicallyAccessedMembers]` sont supprimées quand l'attribut
  n'est pas visible pour le consommateur, ce qui ne compte que pour une publication *trimming*/AOT
  que le chemin *lite* ne fait pas.

La suite de certification XML s'exécute sur `net472` comme sur .NET moderne, y compris les
*fixtures* qui comparent la sortie de l'*emitter* à un **`DataContractSerializer` réel**. Ces
*fixtures* passent octet pour octet sur les deux, donc les formats DCS netfx et moderne s'accordent
sur chaque famille que la suite couvre.
