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

## Plain language

The recurring failure is text that sounds technical but tells the reader nothing. Guard against it:

- **No coined metaphors or invented verbs.** Describe what the code does, in ordinary words. Write
  "raises the timeout to a 15-second minimum", not "floors the timeout"; "a wait that expects a
  result", not "a positive wait"; "the wall clock is a separate service", not "the wall clock rides its
  own circuit". If a phrase would not appear in the .NET documentation, it is probably invented.
- **No informal or idiomatic terms for real concepts, and no puffery.** They read as chatty, not
  precise. Replace each with its plain word: "knobs" to options or settings; "bake" to store or
  generate; "handle" or "drive" to the concrete action ("control the timeout", "run the request");
  "gives up" to fails; "bump" to upgrade; "seed a chain of builders" to "the starting point for the
  builder methods"; "refuses" or "a refusal" to the concrete outcome (warns, rejects, throws, fails,
  "is a compile error"); "loud" or "loudly" to the mechanism ("throws NotSupportedException", "fails
  the build"); "vocabulary" and "jargon" to "wording", "terms", or the actual words being discussed.
  Drop empty intensifiers: "a full 15 ms" is "15 ms".
- **No possession or agency verbs on abstract entities.** An interface, an instance, or a subsystem
  does not "own", "keep", "carry", or "hand out" anything; those verbs apply to physical entities and
  say nothing about code. State the technical relationship instead: a member, a reference, an
  attached instance, a call. Write "`IFdbDatabase` has a `Time` property; the watch reads it", not
  "the database owns a `TimeProvider`"; "the hook must write the discriminator", not "the hooked type
  owns its discriminator" (owner ruling 2026-08-28).
- **Do not lift internal code names into prose.** A private field or an internal nickname
  (`PositiveWaitFloor`, `SettlePerStep`) is not a reader-facing term: state the behavior instead. If the
  code name itself does not say what it does, that is a naming defect. Fix the name, and the prose
  follows.
- **Direct subject, verb, object, with a concrete actor.** Write "use the DataContract preset to write
  the standard format", not "the DataContract profile writes the standard format" (a concept is not an
  actor); "CrystalJson decodes the value", not "the value binds from CrystalJson"; "give a name to the
  client", not "give the client a name". A reversed or ditransitive phrasing reads backward.
- **State the number or the name, not a vague noun.** Write "a 15-second minimum timeout", not "a
  patience floor". Name the method for an action ("a bug in `VisitRangeAsync`", not "a range-visit
  bug"), and define an unfamiliar term on first use ("`i:nil`, the XML attribute that marks a null
  element"). A number states what it measures: "15 ms of real time", not "15 ms"; "192 fewer bytes per
  operation", not "192 fewer".
- **A changes document carries only what changed.** Do not repeat unchanged facts, such as a
  framework-support list that did not move, to fill a section.

## Structure

- Lead with the conclusion: the first sentence of a section tells the reader whether the
  section concerns them.
- Release notes: the introduction is ONE sentence giving the theme of the release. The Highlights
  list is the under-a-minute summary; the introduction does not repeat it. Each section then opens
  with the symptom, prior workaround, or wrong expectation that tells a reader "this section solves
  a problem you have", before the mechanism. State the motivation when the change matters in one
  context only (a test-only improvement says so, and says what it makes possible).
- Every section describing an API addition or change carries a code sample. A sample is often the
  reader's first contact with an API they did not know; a small snippet grounds the feature. Every
  sample compiles and runs against the built assemblies before the notes ship.
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
