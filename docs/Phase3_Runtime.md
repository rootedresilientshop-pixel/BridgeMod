# Phase 3 — Deterministic Behavior Graph Runtime

**BridgeMod v0.4.0 — Full Architectural Specification**

---

## Overview

Phase 3 introduces a minimal, deterministic state machine executor for mod-provided behavior graphs. This enables game logic to be expressed as declarative node structures (state machines) without scripting, reflection, or dynamic code loading.

**Core Guarantee:** Same input → Same output. Every execution is reproducible.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Mod (JSON + Behavior Graph Definition)                         │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ BridgeMod Phase 1: Firewall (Type Check → Guards → Audit)      │
│ (Validates JSON structure)                                      │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ Phase 3: BehaviorGraphValidator                                │
│ - Check for duplicate state IDs                                │
│ - Verify initial state exists                                  │
│ - Validate all transition references                           │
│ - Ensure guard operators are well-formed                       │
│ - Prevent ambiguous transitions                                │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ Phase 3: BehaviorGraphExecutor                                 │
│ - Stateful executor (graph is immutable)                       │
│ - Dispatch(eventName, context) → deterministic transition      │
│ - Guard conditions evaluated against context                   │
│ - Current state always reflects executor state                 │
└─────────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────────┐
│ Game Runtime (consumes state transitions)                       │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Types

### BehaviorGraphDefinition

Immutable, sealed definition of a behavior graph.

```csharp
public sealed class BehaviorGraphDefinition
{
    // Unique identifier for this graph (e.g., "enemy_ai", "dialogue_tree")
    public string GraphId { get; }

    // Version string (e.g., "1.0", "2.0.1")
    public string Version { get; }

    // All states in the graph (read-only list)
    public IReadOnlyList<BehaviorState> States { get; }

    // ID of the initial state (must exist in States)
    public string InitialStateId { get; }

    // All transitions (read-only list)
    public IReadOnlyList<BehaviorTransition> Transitions { get; }
}
```

**Constructor Validation:**
- Ensures `GraphId` is not null or empty
- Ensures `Version` is not null or empty
- Ensures `States` list is not empty
- Ensures `InitialStateId` exists in the States list

### BehaviorState

Immutable, sealed representation of a single state.

```csharp
public sealed class BehaviorState
{
    // Unique identifier within the graph (e.g., "idle", "combat")
    public string StateId { get; }

    // Optional human-readable display name
    public string? DisplayName { get; }

    // Optional metadata (e.g., animation name, sound effect, numeric priorities)
    public Dictionary<string, object>? Metadata { get; }
}
```

**Constructor Validation:**
- Ensures `StateId` is not null or empty

### BehaviorTransition

Immutable, sealed representation of a state transition.

```csharp
public sealed class BehaviorTransition
{
    // Source state ID
    public string FromStateId { get; }

    // Target state ID
    public string ToStateId { get; }

    // Event that triggers this transition (e.g., "player_spotted", "damage_taken")
    public string EventName { get; }

    // Optional guard condition (if null, transition is unconditional)
    public TransitionGuard? Guard { get; }
}
```

**Constructor Validation:**
- Ensures all string fields are not null or empty

### TransitionGuard

Immutable, sealed guard condition for controlled transitions.

```csharp
public sealed class TransitionGuard
{
    // The context key to evaluate (e.g., "distance", "player_health")
    public string ContextKey { get; }

    // The comparison operator
    public GuardOperator Operator { get; }

    // The value to compare against (supports: int, float, bool, string)
    public object ComparisonValue { get; }
}
```

**Constructor Validation:**
- Ensures `ContextKey` is not null or empty
- Ensures `Operator` is a valid `GuardOperator` value
- Ensures `ComparisonValue` is a primitive type (int, float, bool, string)
- Rejects complex objects, collections, and null values

**Supported Guard Operators:**

| Operator | Numeric | Bool | String | Meaning |
|----------|---------|------|--------|---------|
| `Equals` | ✓ | ✓ | ✓ | context[key] == value |
| `NotEquals` | ✓ | ✓ | ✓ | context[key] != value |
| `GreaterThan` | ✓ | ✗ | ✗ | context[key] > value |
| `LessThan` | ✓ | ✗ | ✗ | context[key] < value |
| `GreaterThanOrEqual` | ✓ | ✗ | ✗ | context[key] >= value |
| `LessThanOrEqual` | ✓ | ✗ | ✗ | context[key] <= value |

**Important:** Only primitive types (int, float, bool, string) are supported. This is intentional—it ensures:
- Deterministic evaluation
- No reflection or dynamic loading
- No string interpolation or expression evaluation
- Portable across all game engines

### GuardOperator

```csharp
public enum GuardOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}
```

---

## Validation: BehaviorGraphValidator

Pre-execution validation catches errors before the graph runs.

```csharp
public static class BehaviorGraphValidator
{
    public static List<string> Validate(BehaviorGraphDefinition graph);
}
```

**Validation Rules:**

1. **Duplicate State IDs:** No two states can have the same ID
2. **Initial State Exists:** The `InitialStateId` must reference an actual state
3. **Transition References Valid:** All `FromStateId` and `ToStateId` must exist in the states list
4. **Guard Operators Valid:** All `GuardOperator` values must be in the enum
5. **No Ambiguous Transitions:** From any given state and event, at most one transition can succeed

**Example:**

```csharp
var errors = BehaviorGraphValidator.Validate(graph);
if (errors.Any())
{
    Console.WriteLine("Graph validation failed:");
    foreach (var error in errors)
        Console.WriteLine($"  - {error}");
}
```

---

## Execution: BehaviorGraphExecutor

Stateful executor that drives state transitions based on events and guard conditions.

```csharp
public sealed class BehaviorGraphExecutor
{
    // Constructor: takes an immutable graph definition
    public BehaviorGraphExecutor(BehaviorGraphDefinition graph);

    // Current state of the executor (read-only)
    public string CurrentStateId { get; }

    // Initialize executor to the initial state
    public void Initialize();

    // Dispatch an event with optional context
    public void Dispatch(string eventName, Dictionary<string, object>? context = null);

    // Reset executor to initial state
    public void Reset();
}
```

**Execution Rules:**

1. **Initialization:** `Initialize()` sets the executor to the `InitialStateId`
2. **Event Dispatch:** `Dispatch(eventName, context)` searches for transitions from the current state that match the event name
3. **Guard Evaluation:** If a matching transition has a guard, the guard is evaluated against the provided context
4. **No Match = No Transition:** If no transition matches (no matching event or guard fails), the executor remains in the current state
5. **State Update:** If a transition matches and its guard succeeds, the executor moves to the target state
6. **Deterministic:** The same graph, same sequence of events, same context values always produce the same state transitions

**Example:**

```csharp
var executor = new BehaviorGraphExecutor(graph);
executor.Initialize();  // CurrentStateId == "idle"

// Event with no guard—always transitions
executor.Dispatch("heard_sound");  // CurrentStateId == "alert"

// Event with guard—only transitions if guard passes
var context = new Dictionary<string, object>
{
    { "distance", 3.5f }
};
executor.Dispatch("player_spotted", context);
// Guard: distance < 50.0f
// 3.5f < 50.0f → true
// CurrentStateId == "chase"

// Same sequence with different context—deterministically produces same result
var context2 = new Dictionary<string, object>
{
    { "distance", 3.5f }
};
executor.Dispatch("player_spotted", context2);
// Same distance → Same guard evaluation → Same transition
```

---

## Determinism Guarantee

### What Is Deterministic

✅ **Same input always produces the same output:**
- Same graph definition
- Same sequence of events
- Same context values
- Same initial state
- = **Same state transitions, every time**

**This means:**
- Behavior graphs are safe for replays (record events, replay them, get identical results)
- Safe for testing (deterministic test outcomes)
- Safe for console certification (reproducible behavior)
- Safe for debugging (same conditions always produce same states)

### What Is NOT Deterministic (and Why)

❌ **No randomness.** Graphs do not contain probabilistic transitions or random decisions.

❌ **No reflection or dynamic loading.** Guards evaluate only primitive values, not computed properties.

❌ **No async or threading.** Transitions are synchronous, single-threaded, and immediate.

❌ **No I/O or external side effects.** Graphs don't read files, make network calls, or interact with the filesystem.

❌ **No scripting.** Graphs are declarative node structures, not executable code.

### Proof of Determinism

The test suite includes a determinism proof test that executes the same graph 1000 times with identical inputs and verifies identical outputs:

```csharp
[Fact]
public void BehaviorGraphExecutor_Determinism_1000Iterations()
{
    var results = new List<string>();

    for (int i = 0; i < 1000; i++)
    {
        var executor = new BehaviorGraphExecutor(graph);
        executor.Initialize();
        executor.Dispatch("event1", context);
        results.Add(executor.CurrentStateId);
    }

    // All 1000 results must be identical
    Assert.True(results.All(r => r == results[0]));
}
```

---

## Limitations (Intentional)

The following are **not supported** and will never be supported in the deterministic executor:

| Feature | Why Not | Alternative |
|---------|---------|-------------|
| Scripting (C#, Python, Lua) | Breaks determinism; requires reflection | Declare behavior as node graph + guards |
| Dynamic property access | Breaks determinism; requires reflection | Use context dict with primitive values |
| Probabilistic transitions | Not deterministic | Use external RNG if you need probabilistic behavior (outside the graph) |
| Async/threading | Introduces race conditions | Keep executor single-threaded and synchronous |
| I/O (files, network) | Not deterministic; not reproducible | Handle I/O outside the graph |
| Complex object comparisons | Breaks determinism; requires reflection | Flatten objects to primitive context values |
| Nested graphs | Deferred to Phase 4+ | Use sequential event dispatch for now |

---

## Integration Example: Enemy AI

```csharp
// 1. Define the graph (likely from a mod's JSON manifest)
var enemyGraph = new BehaviorGraphDefinition(
    graphId: "orc_ai",
    version: "1.0",
    states: new[]
    {
        new BehaviorState("idle"),
        new BehaviorState("alert"),
        new BehaviorState("chase"),
        new BehaviorState("attack")
    },
    initialStateId: "idle",
    transitions: new[]
    {
        new BehaviorTransition("idle", "alert", "heard_sound"),
        new BehaviorTransition("alert", "chase", "player_spotted",
            guard: new TransitionGuard("sight_range", GuardOperator.LessThan, 50.0f)),
        new BehaviorTransition("chase", "attack", "player_in_range",
            guard: new TransitionGuard("distance", GuardOperator.LessThan, 2.5f)),
        new BehaviorTransition("attack", "chase", "player_dodged",
            guard: new TransitionGuard("distance", GuardOperator.GreaterThan, 2.5f))
    }
);

// 2. Validate before using
var errors = BehaviorGraphValidator.Validate(enemyGraph);
if (errors.Any())
    throw new InvalidOperationException($"Graph invalid: {string.Join(", ", errors)}");

// 3. Create executor and initialize
var aiExecutor = new BehaviorGraphExecutor(enemyGraph);
aiExecutor.Initialize();

// 4. Game loop: dispatch events with context
while (enemy.IsAlive && gameRunning)
{
    var distanceToPlayer = Vector3.Distance(enemy.Position, player.Position);
    var context = new Dictionary<string, object>
    {
        { "sight_range", distanceToPlayer },
        { "distance", distanceToPlayer }
    };

    // Dispatch events based on game conditions
    if (enemy.HeardNoise)
        aiExecutor.Dispatch("heard_sound", context);

    if (enemy.CanSeePlayer)
        aiExecutor.Dispatch("player_spotted", context);

    if (distanceToPlayer < 2.5f && !enemy.IsAttacking)
        aiExecutor.Dispatch("player_in_range", context);

    if (distanceToPlayer > 2.5f && enemy.IsAttacking)
        aiExecutor.Dispatch("player_dodged", context);

    // 5. Apply the AI decision
    ApplyEnemyBehavior(enemy, aiExecutor.CurrentStateId);

    enemy.Update(deltaTime);
}

void ApplyEnemyBehavior(Enemy enemy, string state)
{
    switch (state)
    {
        case "idle":
            enemy.Idle();
            break;
        case "alert":
            enemy.LookAround();
            break;
        case "chase":
            enemy.ChasePlayer();
            break;
        case "attack":
            enemy.AttackPlayer();
            break;
    }
}
```

---

## JSON Representation

Modders create behavior graph definitions as JSON (validated by Phase 1 firewall):

```json
{
  "graphId": "orc_ai",
  "version": "1.0",
  "states": [
    { "stateId": "idle", "displayName": "Idle" },
    { "stateId": "alert", "displayName": "Alert" },
    { "stateId": "chase", "displayName": "Chasing Player" },
    { "stateId": "attack", "displayName": "Attacking" }
  ],
  "initialStateId": "idle",
  "transitions": [
    {
      "fromStateId": "idle",
      "toStateId": "alert",
      "eventName": "heard_sound"
    },
    {
      "fromStateId": "alert",
      "toStateId": "chase",
      "eventName": "player_spotted",
      "guard": {
        "contextKey": "sight_range",
        "operator": "LessThan",
        "comparisonValue": 50.0
      }
    },
    {
      "fromStateId": "chase",
      "toStateId": "attack",
      "eventName": "player_in_range",
      "guard": {
        "contextKey": "distance",
        "operator": "LessThan",
        "comparisonValue": 2.5
      }
    }
  ]
}
```

The game deserializes this into a `BehaviorGraphDefinition`, validates it, and executes it.

---

## Forward Compatibility

### What Will Never Change (Locked)

- Core type signatures (`BehaviorGraphDefinition`, `BehaviorState`, `BehaviorTransition`, `TransitionGuard`)
- Determinism guarantee
- Guard operator semantics
- Immutability of graph definitions
- Single-threaded, synchronous execution model

### What May Evolve (Additive)

- New optional metadata fields on states/transitions
- New guard operators (if deterministic)
- Optional state entry/exit callbacks (Phase 4+)
- Nested graph support (Phase 4+)
- Weighted transitions (Phase 4+)

### Version Lock

Games target a specific BridgeMod version. When upgrading BridgeMod, re-test your behavior graphs and update them if new features are available. The contract is stable, but you may choose to adopt new features.

---

## Testing

The test suite includes:

| Test Class | Coverage |
|-----------|----------|
| `BehaviorStateTests` | State creation, validation, metadata |
| `BehaviorTransitionTests` | Transition creation, validation |
| `TransitionGuardTests` | Guard operators, primitive type support, operator validation |
| `BehaviorGraphDefinitionTests` | Graph creation, state/transition validation |
| `BehaviorGraphValidatorTests` | Duplicate detection, reference validation, ambiguous transition detection |
| `BehaviorGraphExecutorTests` | Initialization, dispatch, guard evaluation, no-match behavior |
| `BehaviorGraphDeterminismTests` | Determinism proof (1000 iterations) |

**All tests pass:** 22 comprehensive test classes covering all determinism guarantees.

---

## Guarantees Summary

| Guarantee | What It Means | Why It Matters |
|-----------|--------------|----------------|
| **Deterministic** | Same input → Same output, always | Replays, testing, certification |
| **No Scripting** | Declarative graphs only, no code execution | Safe for all platforms; no malicious code |
| **No Reflection** | No dynamic type loading or introspection | Deterministic; efficient; portable |
| **Single-threaded** | Synchronous, immediate transitions | No race conditions; no deadlocks |
| **Immutable Graphs** | Graph definitions cannot change at runtime | Safe shared state; thread-safe if executor is single-threaded |
| **Deterministic Validation** | Validator produces same results every time | Reproducible errors; no false positives/negatives |

---

## What Phase 3 Enables

With Phase 3, game developers can:

1. **Declare behavior graph surfaces** via Phase 2 `ModSurfaceRegistry`
2. **Accept JSON-based behavior graphs** from modders
3. **Validate graphs** before execution (catch errors early)
4. **Execute graphs deterministically** (same input → same output)
5. **Drive game logic** (enemy AI, dialogue trees, quest systems, state machines)
6. **Port to console** without modification (determinism guarantee survives certification)

---

*Last updated: February 2026*
*BridgeMod v0.4.0 — Phase 3 Complete*
