# BridgeMod.sdk — Sample Walkthrough

> **Audience:** Game developers integrating BridgeMod.sdk into a Unity or
> custom C# game engine. Estimated reading time: 15 minutes.

---

## What This Sample Does

The `Legacies_Bridge_Test` folder contains two files that together demonstrate
the full BridgeMod security bridge:

| File | Language | Role |
|------|----------|------|
| `Program.cs` | C# | The **untrusted side**. Receives a mod payload from the PC toolchain and runs it through the firewall. |
| `pulse_test.py` | Python | The **trusted side**. Hosts the game simulation engine and the pytest validation suite. |

They implement identical logic in different languages. Running both and comparing
the output is the fastest way to understand how the bridge works.

---

## Prerequisites

| Requirement | Version | Install |
|-------------|---------|---------|
| .NET SDK | 6.0+ | https://dotnet.microsoft.com/download |
| Python | 3.10+ | https://www.python.org/downloads/ |
| pytest | 7.0+ | `pip install pytest` |

---

## Step 1: Understand the Security Model

Before running any code, read the architecture diagram in
[README.md](../README.md#the-solution-a-modding-firewall).

The key insight is the **trust boundary**:

```
UNTRUSTED                           TRUSTED
─────────────────────────────────── ──────────────────────
PC mod files, player-submitted JSON │ Console game runtime
ModBridge.Validate()                │ Python simulation engine
                                    │
                    ◀── PASS ───────│
                    ──── FAIL ────▶ │ (nothing reaches the engine)
```

The C# bridge sits on the **untrusted** side. It validates and sanitizes
the payload, then forwards only the clean result to the Python engine API.

---

## Step 2: Run the C# Sample

```bash
# From the repository root:
dotnet run --project Legacies_Bridge_Test
```

You should see output like:

```
============================================================
BridgeMod.sdk v0.2.2 — C# Bridge Sample Runner
============================================================

--- Test Case 1: Standard Payload ---
  IsValid       : True
  Health in result: 250
  Audit entries : 0
  RESULT: PASS

--- Test Case 2: Malicious Payload Rejection ---
  AUDIT | [2024-...] [PARSE_ERR_001] payload=test-002 :: Disallowed markup in field 'character_name': ...
  IsValid       : False
  ErrorCode     : PARSE_ERR_001
  HasCode in log: True
  RESULT: PASS

--- Test Case 3: Boundary Guard — Stat Overflow ---
  AUDIT | [2024-...] [BOUND_CLAMP_003] payload=test-003 :: Field 'health' clamped: 999999 -> 9999
  IsValid       : True
  Clamped health: 9999
  BOUND_CLAMP in log: True
  RESULT: PASS
```

**What to look for:**
- Test Case 1 produces **zero audit entries** — clean data passes silently.
- Test Case 2's audit line appears on `Console.Error` before the result — this
  is intentional. In production, it would go to a file, not the screen.
- Test Case 3 shows the original value (`999999`) and the clamped value (`9999`)
  in the audit entry, making it easy to reconstruct what the mod tried to do.

---

## Step 3: Run the Python Test Suite

```bash
# From the repository root:
pytest Legacies_Bridge_Test/pulse_test.py -v
```

Expected output:

```
======= test session starts =======
Legacies_Bridge_Test/pulse_test.py::TestBridgeValidator::test_bounds_overflow_is_clamped_and_logged PASSED
Legacies_Bridge_Test/pulse_test.py::TestBridgeValidator::test_malicious_script_tag_is_rejected PASSED
Legacies_Bridge_Test/pulse_test.py::TestBridgeValidator::test_standard_payload_is_accepted PASSED
======= 3 passed in 0.05s =======
```

---

## Step 4: Trace Through a Test Case Manually

Let's trace Test Case 3 (stat overflow) step by step.

### Input Payload

```json
{
  "character_name": "Overpowered Hero",
  "health": 999999,
  "strength": 50
}
```

### Gate 1: Deserialize & Parse

The string field `character_name` is checked against the disallowed-markup
regex. `"Overpowered Hero"` contains no `<tag>` patterns. **Gate 1: PASS.**

### Gate 2: Boundary Guards

```
health   = 999999  →  clamp(999999, 0, 9999)  →  9999   ← CLAMPED
strength = 50      →  clamp(50,     0, 9999)  →  50     ← unchanged
```

For each clamped field, an audit entry is written:

```
[BOUND_CLAMP_003] payload=test-003 :: Field 'health' clamped: 999999 -> 9999
[BOUND_CLAMP_003] payload=test-003 :: Field 'strength' clamped: 50 -> 50
```

Wait — `50` is not changed, so the second entry is NOT written. Only fields
whose value actually changes produce an audit entry.

### Gate 3: Audit Logger

The audit log now contains one entry (for `health`). The sanitized payload
is `{ "character_name": "Overpowered Hero", "health": 9999, "strength": 50 }`.

### Result

```
ValidationResult { IsValid: true, SanitizedPayload: { health: 9999, ... } }
```

---

## Step 5: Extend the Bridge

### Adding a New Stat Key

To add `"luck"` as a guarded stat field, update the `StatKeys` set in both
`Program.cs` and the `BridgeValidator._apply_boundary_guards()` method in
`pulse_test.py`:

**Program.cs** (around line 165):
```csharp
private static readonly HashSet<string> StatKeys =
    new(StringComparer.OrdinalIgnoreCase)
    {
        "health", "mana", "strength", "defense", "speed", "luck"  // ← add here
    };
```

**pulse_test.py** (around line 180):
```python
stat_keys = {"health", "mana", "strength", "defense", "speed", "luck"}  # ← add here
```

Then add a test case in `pulse_test.py` that verifies the new key is clamped.

### Adding a New Disallowed Pattern

The current filter blocks HTML/XML tags via `<[^>]+>`. To also block
SQL injection attempts (e.g., `'; DROP TABLE characters; --`), add a second
pattern:

**pulse_test.py:**
```python
_DISALLOWED_PATTERNS = [
    re.compile(r"<[^>]+>", re.IGNORECASE),                        # HTML/script tags
    re.compile(r"(--|;)\s*(drop|delete|insert|update)", re.IGNORECASE),  # SQL keywords
]
```

Update `_sanitize_strings` to iterate over `_DISALLOWED_PATTERNS` and use a
new error code (e.g., `PARSE_ERR_002`) for the SQL case so audit logs remain
granular.

---

## Step 6: Integrate with Unity

See the [Unity Integration Quick Reference](../README.md#unity-integration-quick-reference)
in the main README, and the full integration guide in
[console_modding_execution_plan.md](../console_modding_execution_plan.md).

---

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| `dotnet run` fails with `project not found` | You are in the wrong directory | Run from the **repository root**, not inside `Legacies_Bridge_Test/` |
| `pytest: command not found` | pytest is not installed | `pip install pytest` |
| Test Case 2 shows `FAIL` | The disallowed pattern regex was accidentally changed | Restore `<[^>]+>` as the pattern in `BridgeValidator._DISALLOWED_PATTERN` |
| Audit entries not appearing | `EnableAuditLog` is `False` in `BridgeConfig` | Set it to `True` |

---

## Next Steps

- Read [CHANGELOG.md](../CHANGELOG.md) to understand what changed in v0.2.2.
- Read [console_modding_execution_plan.md](../console_modding_execution_plan.md)
  for the roadmap toward full console platform support.
- Run the full pytest suite: `pytest tests/ -v`
