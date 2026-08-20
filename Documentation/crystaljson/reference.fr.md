# Référence CrystalJson

Les tables de référence du travail quotidien : le *setup* du générateur de source, les attributs que
vous posez sur un type, les *settings* que vous passez à un appel, et les diagnostics de *build* que vous
pouvez rencontrer. Pour les guides pratiques, voir [Travailler avec CrystalJson](serializing.fr.md) ;
pour la conception, voir [Ce que c'est, et pourquoi](index.fr.md). Quand un comportement a changé
entre deux versions, le [guide de migration 7.4.2 vers 7.4.3](../releases/7.4.3.md) porte
l'histoire complète, et cette page y renvoie plutôt que de la répéter.

Tous les exemples utilisent `using SnowBank.Data.Json;`.

## *Setup*

Les types d'exécution de CrystalJson (`JsonValue`, `CrystalJson`, `CrystalJsonSettings`) vivent dans
**`SnowBank.Core`**. Un projet qui référence `SnowBank.Core` sérialise, parse et utilise le DOM via le
chemin par réflexion sans autre *setup*.

Les convertisseurs générés et les *proxies* typés demandent en plus le générateur de source. Il est
distribué comme un *package* séparé que le compilateur exécute comme un *analyzer* Roslyn. C'est un outil
de *build*, pas une partie de votre application livrée ; `SnowBank.Core` est la seule dépendance
d'exécution :

```xml
<!-- exécution : le DOM JsonValue et l'API CrystalJson -->
<PackageReference Include="SnowBank.Core" />
<!-- build seulement : le générateur de source, un analyzer Roslyn, non redistribué avec votre app -->
<PackageReference Include="SnowBank.Serialization.Json.CodeGen" />
```

Donnez aux deux la même version que vos autres *packages* SnowBank, ou omettez la version en gestion
centralisée des *packages*. Le générateur demande **C# 9 ou plus** : en dessous, il rapporte `SYSLIB1221`
et n'émet rien, JSON compris, donc mettez `<LangVersion>` à 9 ou plus (le déclencheur classique est un
projet porté qui pinne encore un `<LangVersion>7.3</LangVersion>` d'époque .NET Framework).

Un *container* est une classe `partial` qui déclare les types qu'elle sérialise. Les membres générés
atterrissent dedans :

```csharp
[CrystalJsonConverter]
[CrystalSerializable(typeof(Book))]
public static partial class AcmeSerializers { }

string json = AcmeSerializers.Book.ToJsonText(book);
Book back = AcmeSerializers.Book.Deserialize(json);
```

## Attributs de *container*

Le *container* déclare trois choses indépendantes : quelle classe héberge le code, quels formats elle
produit, et quels types elle enrôle.

| Attribut | *Namespace* | Rôle |
|---|---|---|
| `[CrystalConverter]` | `SnowBank.Data` | le marqueur de *container* ; ne nomme aucun format à lui seul |
| `[CrystalSerializable(typeof(T))]` | `SnowBank.Data` | enrôle un type ; répétable ; alimente chaque format que le *container* produit |
| `[CrystalJsonOutput(...)]` | `SnowBank.Data.Json` | demande le format JSON et porte ses paramètres (profil, politique de nommage) |
| `[CrystalJsonConverter(...)]` | `SnowBank.Data.Json` | alias : `[CrystalConverter]` + `[CrystalJsonOutput]` avec les mêmes paramètres, pour un *container* JSON seul |
| `[CrystalJsonSelfSerializable]` | `SnowBank.Data.Json` | méta-attribut pour les types auto-sérialisables (un type sert de son propre *container*) ; voir le [guide de migration](../releases/7.4.3.md#new-apis) |

Un profil passé à `[CrystalJsonOutput(...)]` ou `[CrystalJsonConverter(...)]` fixe la forme de sortie
par défaut du *container*, `CrystalJsonSerializerDefaults.Web` pour le *camelCase*,
`.DataContractCompat` pour le format historique de `DataContractJsonSerializer`. Des *settings* passés
au site d'appel remplacent le profil pour cet appel.

`[CrystalSerializable]` remplace l'ancien `[CrystalJsonSerializable]` ; l'enrôlement est désormais
neutre vis-à-vis du format. La sortie XML a ses propres attributs et sa propre page,
[CrystalXml](../CrystalXml.md).

## Attributs de membre

Posez-les sur une propriété ou un champ pour changer la sérialisation de ce seul membre. Tous sont
honorés sur le chemin par réflexion comme sur le chemin généré.

| Attribut | Effet |
|---|---|
| `[JsonProperty("name")]` | renomme le membre en sortie |
| `[JsonProperty(DefaultValue = ...)]` | déclare le défaut du membre, utilisé par la condition d'ignore `WhenWritingDefault` |
| `[JsonProperty(EnumFormat = JsonEnumFormat.Number)]` | écrit cet *enum* comme son nombre plutôt que son nom |
| `[JsonProperty(NumberFormat = JsonNumberFormat.String)]` | écrit ce nombre comme une *string* (`"12345678901234567"`), ce qui protège les valeurs 64 bits de la perte de précision JavaScript |
| `[JsonBooleanLiterals(whenFalse, whenTrue)]` | littéraux sur mesure pour un booléen ; un littéral false `null` omet le membre quand il est false. Les arguments sont une *string*, un *bool*, ou un nombre |
| `[JsonIgnore]` | exclut le membre (inconditionnel) |
| `[JsonIgnore(Condition = ...)]` | exclusion conditionnelle ; voir la table ci-dessous |
| `[JsonConvertWith(typeof(X))]` | sérialise le membre via le convertisseur `X` (implémente `IJsonPacker<T>` et/ou `IJsonDeserializer<T>`) |
| `[JsonInclude]` | inclut un membre non-public sur un type sans `[DataContract]` |
| `[IgnoreDataMember]` | exclut le membre sur un type sans `[DataContract]` |

`[JsonIgnore(Condition = ...)]` lit `JsonIgnoreCondition`, en suivant le sens de System.Text.Json.
Attention au piège de nommage : `Never` veut dire « ne jamais ignorer ».

| Condition | Effet |
|---|---|
| `Always` (le défaut) | membre exclu |
| `Never` | membre toujours émis, passant outre les suppressions de null et de défaut au niveau des *settings* |
| `WhenWritingNull` | omis seulement quand la valeur est null |
| `WhenWritingDefault` | omis seulement quand la valeur égale le défaut du membre |

Pour les types `[DataContract]`, `[DataMember(Name = ...)]` renomme et
`[DataMember(IsRequired = true)]` fait *throw* à la lecture quand le membre est absent. Les *containers*
générés appliquent le modèle d'appartenance DataContract depuis la 7.4.3 ; le
[guide de migration](../releases/7.4.3.md#breaking-changes) en donne le détail.

### Attributs d'autres sérialiseurs

CrystalJson lit les attributs qu'un *DTO* existant porte déjà de System.Text.Json et de
Newtonsoft.Json (JSON.NET), donc un type porté se sérialise sans réannotation, à l'identique sur les
deux chemins :

| Attribut étranger | Interprété par CrystalJson comme |
|---|---|
| System.Text.Json `[JsonPropertyName("x")]` | un renommage, comme `[JsonProperty("x")]` |
| Newtonsoft `[JsonProperty("x")]` | un renommage |
| `[JsonIgnore]`, l'une ou l'autre orthographe | exclut le membre |
| System.Text.Json `[JsonIgnore(Condition = ...)]` | exclusion conditionnelle, les conditions ci-dessus |
| System.Text.Json `[JsonInclude]` | inclut un membre non-public |
| `[JsonConverter(typeof(X))]`, l'une ou l'autre orthographe | exécute `X`, quand il implémente `IJsonPacker<T>` et/ou `IJsonDeserializer<T>` |
| `[DataContract]` / `[DataMember]` | le modèle d'appartenance DataContract |

Quand plusieurs attributs de nommage s'accordent, le nom effectif vient du plus prioritaire :
CrystalJson `[JsonProperty]`, puis `[JsonPropertyName]`, puis Newtonsoft `[JsonProperty]`. Deux
attributs de nommage qui divergent sont une erreur de *build* (`CJSON0011`) : un type ne peut pas servir
deux contrats de sortie. Un `[JsonConverter]` étranger qui nomme un type n'implémentant pas le contrat
de convertisseur CrystalJson est ignoré, pas une erreur, donc un *DTO* à moitié porté reste
sérialisable. Le [guide de migration](../releases/7.4.3.md) donne les règles d'*interop*
complètes.

## *Settings*

Passez un `CrystalJsonSettings` à un appel `Serialize`, `Parse`, ou `Deserialize`. Partez d'un *preset*
et ajoutez des modificateurs *fluents* ; chaque modificateur rend une nouvelle instance en cache.

*Presets* :

| *Preset* | Sortie |
|---|---|
| `CrystalJsonSettings.Json` | le défaut : du JSON lisible |
| `CrystalJsonSettings.JsonCompact` | sans espaces |
| `CrystalJsonSettings.JsonIndented` | multi-lignes, indenté |
| `CrystalJsonSettings.JsonStrict` | rejette les commentaires et les virgules finales à la lecture |
| `CrystalJsonSettings.JsonReadOnly` | parse vers des valeurs figées |
| `CrystalJsonSettings.DataContractCompat` | reproduit la sortie de `DataContractJsonSerializer` |

Modificateurs courants :

| Modificateur | Effet |
|---|---|
| `.ThrowOnDuplicateFields()` | une clé répétée est une erreur à la lecture, pas un *last-wins* |
| `.WithoutComments()` | rejette les commentaires JavaScript à la lecture |
| `.WithoutTrailingCommas()` | rejette une virgule finale à la lecture |
| `.WithEnumAsNumbers()` / `.WithEnumAsStrings()` | écrit les *enums* comme leur nombre, ou leur nom (le défaut) |
| `.WithNullMembers()` / `.WithoutNullMembers()` | émet ou omet les membres dont la valeur est null |
| `.WithoutDefaultValues()` | omet les membres qui égalent leur défaut |
| `.WithMicrosoftDates()` / `.WithIso8601Dates()` | format des dates en sortie |
| `.WithIso8601Durations()` / `.WithNumericDurations()` | `TimeSpan` en `"P1DT2H3M4S"`, ou en nombre de secondes (le défaut) |
| `.WithDictionariesAsPairArrays()` / `.WithDictionariesAsMaps()` | un dictionnaire comme un tableau de `{"Key":..,"Value":..}`, ou comme un objet JSON (le défaut) |

`JsonStrict` ne couvre pas les clés dupliquées ; ajoutez `.ThrowOnDuplicateFields()` quand une clé
répétée doit échouer. Pour durcir le *parser* face à un *input* non fiable, voir
[Durcir le *parsing* d'un *input* non fiable](serializing.fr.md#durcir-le-parsing-dun-input-non-fiable).

Pour lire plusieurs documents consécutifs dans un même *buffer*, utilisez `CrystalJson.ParseFragment`,
pas `WithTrailingData()` (qui parse la première valeur et jette le reste).

## Défauts

- **Les *enums* se sérialisent par leur nom**, pas leur nombre. La lecture accepte les noms
  (insensible à la casse), les nombres et les nombres en *string*, quels que soient les *settings*.
- **Le *parser* est permissif par défaut** : les commentaires JavaScript et les virgules finales sont
  acceptés. C'est faux pour un *input* que vous ne contrôlez pas ; resserrez-le avec `JsonStrict`.
- **Les nombres gardent leur littéral d'origine** sur la route DOM tant que vous ne les lisez pas
  comme une valeur typée.

## Diagnostics

Les codes `CJSON####` ci-dessous sont ceux qu'un auteur normal rencontre en écrivant des *DTO*. Chacun
est rapporté au même endroit par les deux chemins : le générateur émet le diagnostic, et le chemin
par réflexion *throw* le même message quand il construit le contrat du type. Le
[guide de migration](../releases/7.4.3.md) donne le traitement complet de chacun.

| Id | Sévérité | Refuse | Remède |
|---|---|---|---|
| `CJSON0008` | Error | un `[JsonIgnore]` inconditionnel à côté d'un signal d'inclusion (`[DataMember]`, `[JsonInclude]`, un attribut de nommage) | scinder en un *DTO* par format, ou retirer un des deux attributs |
| `CJSON0010` | Error | `[JsonConvertWith]` nomme un type qui n'implémente ni `IJsonPacker<T>` ni `IJsonDeserializer<T>` | implémenter une facette de convertisseur, ou corriger le type nommé |
| `CJSON0011` | Error | un membre déclare deux noms différents pour deux sérialiseurs | un *DTO* par format, chacun avec un seul jeu cohérent d'attributs |
| `CJSON0012` | Warning | un membre `internal` sans signal d'inclusion ni d'exclusion, sérialisé par le générateur mais invisible au chemin par réflexion | ajouter `[JsonInclude]` ou `[JsonIgnore]` pour fixer l'intention |
| `CJSON0013` | Error | le profil `DataContractCompat` combiné à une politique de nommage | retirer la politique de nommage ; le profil fixe les noms |
| `CJSON0015` | Error | un *callback* de sérialisation qui prend un `StreamingContext` | retirer le paramètre, ou le remplacer par `JsonValue`, `JsonObject`, ou `JsonArray` |
| `CJSON0016` | Error | `[OnDeserializing]` sur un type avec un membre `required` ou `init`-only | retirer `[OnDeserializing]`, ou rendre le membre assignable |
| `CJSON0017` | Error | un argument de `[JsonBooleanLiterals]` qui n'est ni *string*, ni *bool*, ni nombre | utiliser un littéral valide |
| `CJSON0018` | Warning | `StrictLiterals = true` avec un littéral false `null` (rien à imposer du côté false) | retirer `StrictLiterals`, ou donner au membre un vrai littéral false |
| `CJSON0019` | Warning | une inscription `[CrystalSerializable]` d'un type que CrystalJson sérialise déjà nativement | retirer l'inscription |
| `CJSON0022` | Error | `[DataMember]` sur une implémentation explicite d'interface : le membre appartient au contrat, et le code généré ne peut pas déclarer d'accesseur pour un nom de membre qualifié | le promouvoir en membre normal, ou déplacer le contrat sur un *DTO* dédié |

Les diagnostics des types auto-sérialisables (`CJSON0004` à `CJSON0007`, `CJSON0020`, `CJSON0021`) et
les codes du générateur XML (`CRYS####`, `CXML####`) sont couverts dans le
[guide de migration](../releases/7.4.3.md) et [CrystalXml](../CrystalXml.md).
