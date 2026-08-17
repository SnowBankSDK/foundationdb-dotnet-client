# Writing style for this documentation

Rules for every page under `Documentation/`, and for the top-level `README.md`. They derive
from the principles of Simplified Technical English (ASD-STE100), adapted for software
documentation. The goal is text that a non-native reader parses once, correctly.

## Sentences

- One idea per sentence. Prefer short sentences.
- Active voice, with a named actor: "the generator reports CJSON0011", not "an error is reported".
- Present tense for descriptions; imperative for instructions ("Install the package", not
  "You will want to install the package").
- Positive form. No double negatives.
- Every "this", "it" and "that" has one clear referent.
- Noun stacks stop at three words; break longer ones with prepositions.

## Words

- One meaning per word, and the same word for the same thing on every page. Do not rotate
  synonyms for style.
- Concrete words and numbers over adjectives: "cuts the run from 5m19s to 4s", never
  "dramatically faster".
- No jokes, asides, idioms or drama. State the fact; the reader judges its importance.
- No em dashes. Use commas, parentheses or colons.
- No filler words: delve, leverage, robust, seamless, utilize, powerful, blazing, and their
  relatives. If a sentence survives without a word, remove the word.
- American English spelling.

## Structure

- Lead with the conclusion: the first sentence of a section tells the reader whether the
  section concerns them.
- A change entry states, in order: what changed, old and new behavior, who is affected, what
  to do.
- A warning states the consequence, not only the rule: "caching the prefix risks corruption",
  not "do not cache the prefix".
- Every API name, number and code sample must exist in the source tree. Verify before writing.
- Example applications and tenants are named **Acme**.

## Code samples

- A call that does not fit comfortably on one line puts one argument per line, and the closing
  `)` (with any `;`) on its own line. This keeps samples legible on a width-constrained page.
- A lambda argument opens on the calling line: `db.ReadWriteAsync(async tr =>`, then the body,
  then `}, ct);`. Do not push the lambda onto its own line under the call.
- A multi-line raw-string literal (`"""`) aligns its closing delimiter with the left margin of
  the content; that shared indentation is stripped from the string.
- Short calls stay on one line. The rule is for long argument lists or an argument that is itself
  multi-line.

## Technical names

One term per concept. The left column is the term these pages use; do not substitute.

| Term | Meaning | Do not write |
|---|---|---|
| the binding | `FoundationDB.Client`, the managed library | wrapper, driver, client library |
| the native client | `fdb_c`, shipped by `FoundationDB.Client.Native` | the C library, the native driver |
| the cluster | the FoundationDB server processes | the server, the database machines |
| the retry loop | `ReadAsync` / `WriteAsync` / `ReadWriteAsync` | retry helper, transaction runner |
| a Layer | a component implementing `IFdbLayer<TState>` | plugin, module |
| a subspace | a resolved key prefix (`IKeySubspace`) | namespace, folder (except as analogy, once) |
| a location | an unresolved path (`ISubspaceLocation`) | address |
| the Directory layer | the path-to-prefix mapping layer | directory service |
| the reflection path | CrystalJson serialization driven by runtime contracts | the runtime path, dynamic mode |
| generated converters | output of the CrystalJson source generator | codegen output, compiled serializers |
| a container | a class hosting generated serializers | registry, hub |
| an enrolled type | a type named by `[CrystalSerializable(typeof(T))]` | registered type |
| the DCS format | XML byte-compatible with `DataContractSerializer` | legacy XML, compat XML |
| the emulator | `FoundationDB.FakeDb`, the in-memory backend | the fake, the mock |

A term specific to one page (for example `SliceOwner`) does not need a row here; use the type
name itself, consistently.
