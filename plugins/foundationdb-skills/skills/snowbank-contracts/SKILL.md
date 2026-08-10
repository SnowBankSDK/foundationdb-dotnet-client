---
name: snowbank-contracts
description: >-
  How to guard arguments and assert invariants with the Contract family in SnowBank.Core (namespace
  SnowBank.Diagnostics.Contracts): the argument guards (Contract.NotNull / NotNullOrEmpty /
  NotNullOrWhiteSpace / Positive / GreaterThan / GreaterOrEqual / LessThan / LessOrEqual / EqualTo /
  NotEqualTo / ValueNotNull) that validate a caller's arguments and throw ArgumentNullException /
  ArgumentException / ArgumentOutOfRangeException, versus the condition assertions (Contract.Requires /
  Assert / Ensures / Invariant / Fail) that check internal invariants and throw ContractException, plus
  the three compile levels (always-on Contract.X, Debug-only Contract.Debug.X, Paranoid.X under
  PARANOID_ANDROID), the CallerArgumentExpression auto-message, the nullable-flow and StackTraceHidden
  behavior, and the NUnit test integration. Use whenever code validates a method argument, replaces a
  hand-written `if (x == null) throw new ArgumentNullException(...)` or `Debug.Assert(...)` or
  `ArgumentNullException.ThrowIfNull(...)`, adds a precondition or state invariant, chooses between
  Contract, Contract.Debug and Paranoid, or hits a ContractException. Prefer these over raw throws and
  BCL asserts in SnowBank.Core / FoundationDB.Client code.
---

# Contract (SnowBank.Diagnostics.Contracts)

`Contract` is the guard and assertion family used across SnowBank.Core and FoundationDB.Client. It
replaces raw `throw new ArgumentNullException(...)`, `ArgumentNullException.ThrowIfNull(...)`,
`ArgumentOutOfRangeException.ThrowIf*`, and `System.Diagnostics.Debug.Assert(...)`. The namespace is
`SnowBank.Diagnostics.Contracts`; it is a `global using` in the SnowBank projects, add
`using SnowBank.Diagnostics.Contracts;` in a consumer.

There are **two families**, and they are not interchangeable:

1. **Argument guards** (`Contract.NotNull`, `Positive`, `GreaterThan`, ...) validate arguments that
   come from OUTSIDE the method. They are the logical equivalent of
   `if (cond) throw new Argument...Exception(...)`. A failure means the CALLER passed a bad argument.
   Always on (Debug and Release). Each throws the matching `Argument*` exception.
2. **Condition assertions** (`Contract.Requires`, `Assert`, `Ensures`, `Invariant`, `Fail`) check a
   condition that must NEVER be false, an invariant of your own code. A failure means the component
   itself has a bug. Each throws `ContractException`.

The rule for choosing: ask who can trigger it. A caller outside the type gets an argument guard. Your
own code breaking its own invariant gets `Contract.Debug.Requires` for a dev-time check, or an
explicit `if (...) throw` when the invariant must hold at run time (section 3).

---

## 1. Argument guards

Use these to validate public-API arguments. Each captures the argument name on its own (see section 5),
takes an optional `message` as the second argument, and throws the exception below.

| Guard | Replaces | Throws |
|---|---|---|
| `Contract.NotNull(x)` | `if (x == null) throw new ArgumentNullException(...)` | `ArgumentNullException` |
| `Contract.NotNullOrEmpty(s)` (string) | null + `s.Length == 0` | `ArgumentNullException` (null) / `ArgumentException` (empty) |
| `Contract.NotNullOrWhiteSpace(s)` | `string.IsNullOrWhiteSpace(s)` | `ArgumentNullException` (null) / `ArgumentException` (blank) |
| `Contract.NotNullOrEmpty(collection)` | null + empty | `ArgumentNullException` / `ArgumentException` |
| `Contract.NotEmpty(collection)` | `.Count == 0` | `ArgumentException` |
| `Contract.Positive(n)` | `if (n <= 0) throw ...` | `ArgumentException` |
| `Contract.PowerOfTwo(n)` | bit check | `ArgumentException` |
| `Contract.GreaterThan(n, t)` / `GreaterOrEqual` / `LessThan` / `LessOrEqual` | compare + throw | `ArgumentOutOfRangeException` |
| `Contract.EqualTo(x, v)` / `NotEqualTo(x, v)` | compare + throw | `ArgumentException` |
| `Contract.ValueNotNull(x)` | null check that returns `x` | `ArgumentNullException` |
| `Contract.PointerNotNull(p)` (unsafe) | `p == null` | `ArgumentNullException` |

```csharp
public void Load(Root root, int count)
{
    Contract.NotNull(root);
    Contract.Positive(count);
    // root is non-null from here (section 5)
}
```

`ValueNotNull` returns its argument, for a single-line setter:

```csharp
public string Name
{
    get => this.name;
    set => this.name = Contract.ValueNotNull(value, "Name cannot be null");
}
```

Numeric guards exist for `int`, `long`, `double`, `float` (and `uint` / `ulong` for `PowerOfTwo`).
`NotNullOrEmpty` / `NotEmpty` have overloads for arrays, collections, `Slice`, and `ArraySegment<T>`.
`NotNullAllowStructs<T>` is `[Obsolete]`; call `NotNull`.

**Non-trivial check:** when the check is heavy (for example a `SequenceCompareTo` bound check at a
public entry point), write a plain `if (...) throw` with a message, not a Contract call.

## 2. Condition assertions

Use these for a condition that must never be false. They throw `ContractException`.

| Method | Meaning |
|---|---|
| `Contract.Requires(cond)` | precondition at the start of a method |
| `Contract.Assert(cond)` | assertion inside a method body |
| `Contract.Ensures(cond)` | postcondition at the end |
| `Contract.Invariant(cond)` | an invariant that must always hold |
| `Contract.Fail(message, ex?)` | fail unconditionally (returns never) |

Do NOT use `Contract.Requires` to validate a public argument. It throws `ContractException`, which
signals an internal bug, not a caller error. Public arguments get the typed guards in section 1, which
throw the correct `Argument*` exception.

For an internal invariant, the common form is the Debug variant `Contract.Debug.Requires` (section 3),
not the always-on `Contract.Requires`. See section 3 for when to use an explicit `if (...) throw`
instead.

## 3. Three levels, by cost of the check

The same method set exists at three compile levels. Pick by how much you are willing to pay at run
time.

| Level | Compiled | Use for |
|---|---|---|
| `Contract.X(...)` | always (Debug and Release) | the argument guards (section 1) |
| `Contract.Debug.X(...)` | Debug only (`[Conditional("DEBUG")]`, removed from the Release binary) | state invariants inside private/internal methods |
| `Paranoid.X(...)` | only when the `PARANOID_ANDROID` symbol is defined | the hottest paths, where even a Debug check is too costly |

`Paranoid.IsParanoid` is a runtime flag for code that wants to skip expensive setup when Paranoid
checks are off.

**Default for a state invariant: `Contract.Debug.Requires`.** It is the equivalent of `Debug.Assert`:
it catches a regression during day-to-day development (Debug) and costs no CPU in Release, where the
public boundary already validated. A failure means a bug in the component itself, not a caller error.

```csharp
private void Apply(Node node, int position)
{
    Contract.Debug.Requires(node is not null && position >= 0);   // state invariant, Debug only
    // ...
}
```

**An invariant that must hold at run time gets an explicit `if (...) throw`, not `Contract.Requires`.**
A condition worth checking in Release is important enough to deserve an explicit check and a meaningful
exception. The always-on `Contract.Requires` / `Assert` exist, but are rarely the right tool: use
`Contract.Debug.Requires` for a dev-time check, or `if (...) throw` for a real runtime invariant.

## 4. The message is captured on its own

Every guard and assertion carries `[CallerArgumentExpression]`, so the compiler puts the source text
in the message. Do not write a message just to name the argument or restate the condition.

- `Contract.NotNull(root)` puts the name `root` in the `ArgumentNullException`.
- `Contract.Debug.Requires(node is not null && position >= 0)` puts the literal condition
  `"node is not null && position >= 0"` in the `ContractException`.

The optional `message` argument is for extra context only: `Contract.NotNull(root, "Root is required")`.

## 5. Nullable flow and stack traces

- **Nullable flow.** The guards carry `[NotNull]` and `[DoesNotReturnIf(false)]`, and the JetBrains
  `[AssertionMethod]` / `[AssertionCondition]` attributes. After `Contract.NotNull(x)`, both the C#
  compiler and ReSharper treat `x` as non-null for the rest of the method, which removes
  "possible null dereference" (CS8602) false positives.
- **Stack traces.** The guards are `[StackTraceHidden]`, so the guard frame does not appear in the
  stack trace. The trace points at the caller.
- The guards are `AggressiveInlining`; a value-type argument to `NotNull` is optimized away with no
  boxing.

## 6. Test integration

Under a unit-test runner, a `Contract` failure becomes a test assertion, and debugger breakpoints are
muted so an unattended CI run does not block. `Contract.IsUnitTesting` is set true when the runner is
detected.

Only NUnit is detected today (the failure maps to `NUnit.Framework.AssertionException`). xUnit,
MSTest, and TUnit each throw their own assertion type and are not yet mapped.

## 7. Why Contract, not if/throw or ThrowIfNull

- `ArgumentNullException.ThrowIfNull` does not exist on .NET Framework, and a static extension method
  cannot shim it, so it is not portable to the netstandard 2.0 / net472 consumers.
- `Contract` changes mode by environment: it breaks into the debugger when one is attached, and raises
  a formatted assertion under a test runner (section 6).
- `[CallerArgumentExpression]` removes the boilerplate message (section 4).
- The fixed-width `Contract.` prefix aligns a stack of parameter checks at the top of a method.

(Historical: the helpers once aided JIT inlining, because a `new FooException` in the body blocked it.
The modern JIT handles this, so do not cite inlining as a reason today.)

## 8. Golden rules and gotchas

- **Argument from a caller -> a typed guard** (`Contract.NotNull` / `Positive` / ...), which throws the
  matching `Argument*` exception. Never `Contract.Requires` for that: it throws `ContractException`, the
  wrong signal.
- **Own-code invariant, dev-time -> `Contract.Debug.Requires` / `Debug.Assert`** inside
  private/internal methods. Debug only, the equivalent of `Debug.Assert`: catches regressions in
  development, zero Release cost.
- **Own-code invariant that must hold at run time -> an explicit `if (...) throw`**, not the always-on
  `Contract.Requires`. A condition worth checking in Release deserves an explicit check and a
  meaningful exception.
- **Never `System.Diagnostics.Debug.Assert(...)`** in this codebase; use `Contract.Debug.Assert(...)`.
- **Never `ArgumentNullException.ThrowIfNull(...)` or `ArgumentOutOfRangeException.ThrowIf*`**; use the
  guards (portability, section 7).
- **Heavy check expression -> plain `if (...) throw`**, not a Contract call dressed over it.
- The exact exception depends on the guard: `Positive`, `EqualTo`, `NotEqualTo`, and the empty-string /
  empty-collection cases throw `ArgumentException`; `GreaterThan` / `GreaterOrEqual` / `LessThan` /
  `LessOrEqual` throw `ArgumentOutOfRangeException`; the null cases throw `ArgumentNullException`.
