# Phase 3 — Behavior Graph Runtime

**BridgeMod.SDK v0.4.0 (Planned)**

---

## Overview

Phase 3 introduces deterministic behavior graph execution. Game developers can author state machines and event-condition-action (ECA) rule graphs as declarative node structures, and mods can implement behavior graphs targeting these declared surfaces.

**Key principles:**
- No scripting. Nodes are pre-defined types; graphs are data structures.
- Deterministic execution. Same graph + same inputs = same outputs, always.
- Bounded execution. Graphs have maximum depth, node count, and step limits.
- Host-declared surfaces. Behavior graphs are surfaces (like data surfaces in Phase 2).
- Adapter-compatible. Any engine can execute behavior graphs by consuming validated node definitions.

---

## Design Constraints (Non-Negotiable)

| Constraint | Rationale |
|-----------|-----------|
| **No scripting** | Scripts are arbitrary code. Determinism cannot be guaranteed. |
| **No reflection** | Reflection prevents deterministic analysis of code structure. |
| **No dynamic loading** | Cannot guarantee safety of dynamically loaded assemblies. |
| **Deterministic node types** | Every node type must produce the same output for the same input. |
| **Bounded depth** | Graphs with arbitrary nesting depth can cause stack overflow. |
| **Bounded nodes** | Very large graphs can exceed memory limits. |
| **Bounded steps** | Infinite loops must be prevented. |
| **No shared mutable state** | Each mod's graph execution is isolated. |

---

## Phase 3 Deliverables (Design Only, No Implementation)

### 1. Node Type System

Nodes are immutable, typed definitions:

```csharp
public abstract class GraphNode
{
    public string NodeId { get; }
    public string NodeType { get; }  // "decision", "state", "action", etc.
    public IList<string> Inputs { get; }
    public IList<string> Outputs { get; }
}
```

**Built-in node types:**
- **Decision** — Evaluates a boolean condition; routes to true/false edges
- **State** — Represents a state in a state machine; can have entry/exit actions
- **Action** — Calls a host-provided action handler (e.g., "damage_character")
- **Constant** — Returns a fixed value
- **Arithmetic** — Basic math operations (add, subtract, multiply, divide)
- **Comparison** — Numeric/string comparisons (equal, greater, less)

**Engines can register custom node types** during initialization, but all custom types must be deterministic.

### 2. Graph Structure

```csharp
public sealed class BehaviorGraph
{
    public string GraphId { get; }
    public IReadOnlyList<GraphNode> Nodes { get; }
    public IReadOnlyList<GraphEdge> Edges { get; }
    public string EntryNodeId { get; }
    public ExecutionConstraints? Constraints { get; }
}

public sealed class GraphEdge
{
    public string SourceNodeId { get; }
    public string TargetNodeId { get; }
    public string? Condition { get; }  // Optional condition label (for decision nodes)
}
```

### 3. Deterministic Executor

```csharp
public sealed class GraphExecutor
{
    public GraphExecutionResult Execute(
        BehaviorGraph graph,
        Dictionary<string, object?> inputs,
        ExecutionContext context);
}

public sealed class GraphExecutionResult
{
    public bool Success { get; }
    public Dictionary<string, object?> Outputs { get; }
    public int StepsExecuted { get; }
    public long ExecutionTimeMs { get; }
    public IList<ExecutionEvent> Events { get; }
}
```

**Execution model:** Step-based (not tick-based). Each `Execute()` call runs until:
- The graph reaches an exit node, OR
- The max step limit is reached, OR
- An error occurs

### 4. Host Integration

Hosts declare behavior graph surfaces:

```csharp
registry.Register(new ModSurfaceDeclaration(
    name: "EnemyAI",
    category: ModSurfaceCategory.BehaviorGraphs,
    status: ModSurfaceStatus.Enabled,
    description: "Enemy decision-making behavior graphs."));
```

Mods declare which actions they support:

```json
{
  "version": "1.0",
  "name": "Smart Enemies",
  "targetSurfaces": ["EnemyAI"],
  "graphs": [
    {
      "graphId": "archer_flee_behavior",
      "type": "behavior_graph",
      "nodes": [...],
      "edges": [...]
    }
  ]
}
```

### 5. Safety Guarantees

**Determinism:**
- Same input → Same output (guaranteed)
- No randomness in node execution
- No time-dependent behavior

**Isolation:**
- Each mod's graph runs in its own context
- No shared state between mods
- Exceptions in one graph don't affect others

**Termination:**
- Max step limit prevents infinite loops
- Max depth prevents stack overflow
- Max nodes prevent memory exhaustion

---

## What Phase 3 Does NOT Introduce

- ❌ No scripting
- ❌ No reflection
- ❌ No dynamic loading
- ❌ No mutable global state
- ❌ No time-dependent behavior
- ❌ No non-deterministic operations

---

## Forward Compatibility

**Locked (Never Changing):**
- Node type definitions
- Graph structure format
- Executor contract
- Determinism guarantee

**Additive (Can Evolve):**
- New node types (always deterministic)
- New surface categories (in Phase 3+)
- New execution constraints (more restrictive only)

---

## Engine Adapter Examples

### DreamCraft.Engine Adapter

```csharp
public class DreamCraftGraphAdapter
{
    private readonly GraphExecutor _executor;

    public void ExecuteEnemyBehavior(Enemy enemy, BehaviorGraph graph)
    {
        var inputs = new Dictionary<string, object?>
        {
            { "enemy_health", enemy.Health },
            { "player_distance", Vector3.Distance(enemy.Position, player.Position) }
        };

        var result = _executor.Execute(graph, inputs, context);

        if (result.Success)
        {
            if (result.Outputs.TryGetValue("action", out var action))
                enemy.PerformAction((string)action);
        }
    }
}
```

### Custom Engine Adapter

Any engine can implement this pattern:
1. Define action handlers (what the graph can call)
2. Pass mod-authored graph to executor
3. Consume the deterministic result
4. Apply result to game state

---

## Testing Strategy

- Unit tests for each node type
- Integration tests for graph execution
- Determinism tests (run same graph 1000x, verify identical results)
- Performance tests (ensure max-step enforcement works)
- Safety tests (malformed graphs are rejected safely)
- Isolation tests (one failing mod doesn't crash others)

---

## Timeline & Dependencies

**Prerequisites:**
- Phase 2 complete (surfaces declared)
- Canonical mod contract stable
- ExecutionConstraints defined

**Phase 3 Work:**
- [ ] Node type definitions + documentation
- [ ] Graph structure + serialization format
- [ ] Deterministic executor implementation
- [ ] Host integration tests
- [ ] Engine adapter example (DreamCraft.Engine)
- [ ] Performance benchmarking
- [ ] v0.4.0 release

**No timeline estimate given.** Implementation will proceed based on community demand and architectural readiness.

---

## Success Criteria

- ✅ Graph executor passes 100+ determinism tests
- ✅ Host can declare BehaviorGraphs surfaces
- ✅ Mods can declare graph-based behavior
- ✅ Zero scripting or dynamic code execution
- ✅ All Phase 1 + Phase 2 tests still pass
- ✅ 0 build warnings, 0 compiler errors
- ✅ Full documentation and examples

---

*Last updated: February 20, 2026*
*Phase 3 Design Specification (Implementation Pending)*
