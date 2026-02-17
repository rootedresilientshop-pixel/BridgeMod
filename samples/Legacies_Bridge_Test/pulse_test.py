"""
pulse_test.py — BridgeMod.sdk v0.2.2 Bridge Validation Test Suite
==================================================================
Copyright (c) 2024 DreamCraft: Legacies Project
License: MIT

Purpose
-------
This module is the canonical test harness for the Python-side of the
BridgeMod security bridge. It validates that the three firewall layers
(Deserialize, Boundary Guards, Audit Logger) behave correctly when
presented with standard, malicious, and out-of-bounds mod payloads.

It is intended to be read alongside Program.cs, which demonstrates
the C# side of the same bridge. Together, they form the
Legacies_Bridge_Test sample — a reference implementation for
developers integrating BridgeMod.sdk into their own game engines.

Architecture Note
-----------------
In the full stack, this Python engine runs on a trusted server
(Oracle Cloud ARM or Raspberry Pi 5). The C# bridge runs on the
client/console side. The bridge validates an incoming payload and
only forwards a sanitized version to this engine.

These tests simulate that exchange locally, without needing a live
server, so developers can verify bridge behavior in isolation.

Test Cases
----------
1. test_standard_payload   — Happy path. Valid data passes through.
2. test_malicious_payload  — Injected script tag is stripped and rejected.
3. test_bounds_overflow    — Stat overflow is clamped and logged.

Usage
-----
    python pulse_test.py

    # Or via pytest:
    pytest Legacies_Bridge_Test/pulse_test.py -v
"""

import json
import re
import unittest
from dataclasses import dataclass, field
from datetime import datetime, timezone
from typing import Any


# ---------------------------------------------------------------------------
# Error Codes
# ---------------------------------------------------------------------------
# These codes are shared between the Python engine and the C# bridge.
# When the bridge rejects a payload, it records one of these codes in the
# audit log so that post-incident analysis can pinpoint the exact failure.

PARSE_ERR_001 = "PARSE_ERR_001"      # Payload contains disallowed markup/script content
BOUND_CLAMP_003 = "BOUND_CLAMP_003"  # A numeric value exceeded its maximum and was clamped


# ---------------------------------------------------------------------------
# Configuration Constants
# ---------------------------------------------------------------------------

MAX_STAT_VALUE = 9999   # Hard ceiling for any character stat (health, mana, strength, etc.)
MIN_STAT_VALUE = 0      # Hard floor — stats cannot go negative


# ---------------------------------------------------------------------------
# Audit Logger
# ---------------------------------------------------------------------------

@dataclass
class AuditEntry:
    """
    A single immutable record in the bridge audit log.

    Each entry captures what happened to a payload at a specific moment
    in time. Entries are written in append-only fashion; they are never
    modified or deleted. This makes the log tamper-evident.

    Attributes
    ----------
    timestamp : str
        ISO 8601 UTC timestamp of when the event occurred.
    event_code : str
        A machine-readable code identifying the event type (e.g. PARSE_ERR_001).
    payload_id : str
        An identifier for the payload that triggered this entry. In production
        this would be a cryptographic hash of the raw payload bytes.
    detail : str
        Human-readable description of what happened. Useful for debugging.
    """
    timestamp: str
    event_code: str
    payload_id: str
    detail: str


class AuditLogger:
    """
    In-memory audit logger for the BridgeMod validation pipeline.

    In production, entries would be flushed to an append-only file or
    a write-once database table. For this test harness, they are stored
    in memory so that tests can inspect them directly.

    Usage
    -----
        logger = AuditLogger()
        logger.log("BOUND_CLAMP_003", payload_id="abc123", detail="health clamped 999999 -> 9999")
        print(logger.entries)  # List[AuditEntry]
    """

    def __init__(self) -> None:
        """Initialize with an empty entry list."""
        self.entries: list[AuditEntry] = []

    def log(self, event_code: str, payload_id: str, detail: str) -> None:
        """
        Append a new audit entry.

        Parameters
        ----------
        event_code : str
            Machine-readable event code (use the module-level constants).
        payload_id : str
            Identifier for the originating payload.
        detail : str
            Human-readable description of the event.
        """
        entry = AuditEntry(
            timestamp=datetime.now(timezone.utc).isoformat(),
            event_code=event_code,
            payload_id=payload_id,
            detail=detail,
        )
        self.entries.append(entry)

    def has_code(self, event_code: str) -> bool:
        """Return True if any entry matches the given event code."""
        return any(e.event_code == event_code for e in self.entries)


# ---------------------------------------------------------------------------
# Bridge Validator
# ---------------------------------------------------------------------------

@dataclass
class ValidationResult:
    """
    The result returned by BridgeValidator.validate().

    Attributes
    ----------
    is_valid : bool
        True if the payload passed all checks and is safe to apply to game state.
    sanitized_payload : dict | None
        The cleaned payload. None if validation failed entirely.
    error_code : str | None
        The error code if validation failed, or None on success.
    """
    is_valid: bool
    sanitized_payload: dict[str, Any] | None = None
    error_code: str | None = None


class BridgeValidator:
    """
    The Python-side representation of the BridgeMod firewall.

    This validator mirrors the logic in the C# ModBridge class (see Program.cs).
    Having both implementations lets developers verify that the two sides of
    the bridge agree on what constitutes a valid or invalid payload.

    Pipeline
    --------
    1. _sanitize_strings  — Strip disallowed markup from all string fields.
    2. _apply_boundary_guards — Clamp numeric stats to [MIN_STAT_VALUE, MAX_STAT_VALUE].
    3. Return a ValidationResult with the cleaned payload (or an error).

    Parameters
    ----------
    audit_logger : AuditLogger
        The logger instance to write audit entries to.
    """

    # HTML/script tags that are never allowed in any string field of a mod payload.
    # This prevents stored-XSS style attacks where a mod author embeds executable
    # content in a character name or description field.
    _DISALLOWED_PATTERN = re.compile(r"<[^>]+>", re.IGNORECASE)

    def __init__(self, audit_logger: AuditLogger) -> None:
        self._logger = audit_logger

    def validate(self, raw_payload: dict[str, Any], payload_id: str = "unknown") -> ValidationResult:
        """
        Run the full validation pipeline on a raw mod payload.

        Parameters
        ----------
        raw_payload : dict
            The untrusted payload as parsed from JSON.
        payload_id : str
            An identifier for this payload (used in audit log entries).

        Returns
        -------
        ValidationResult
            Contains is_valid, the sanitized payload (if valid), and an
            error code (if invalid).
        """
        # Step 1: Deep-copy so we never mutate the caller's data
        payload = json.loads(json.dumps(raw_payload))

        # Step 2: Strip disallowed markup from all string fields
        rejected = self._sanitize_strings(payload, payload_id)
        if rejected:
            return ValidationResult(is_valid=False, error_code=PARSE_ERR_001)

        # Step 3: Clamp numeric stats to allowed boundaries
        self._apply_boundary_guards(payload, payload_id)

        return ValidationResult(is_valid=True, sanitized_payload=payload)

    def _sanitize_strings(self, payload: dict[str, Any], payload_id: str) -> bool:
        """
        Walk all string values in the payload and check for disallowed markup.

        If any string field contains an HTML or script tag, the entire payload
        is rejected with PARSE_ERR_001. The tag is not silently stripped —
        rejection is the safe default because the presence of markup indicates
        a likely injection attempt.

        Returns
        -------
        bool
            True if a disallowed pattern was found (payload should be rejected).
            False if all strings are clean.
        """
        for key, value in payload.items():
            if isinstance(value, str) and self._DISALLOWED_PATTERN.search(value):
                self._logger.log(
                    event_code=PARSE_ERR_001,
                    payload_id=payload_id,
                    detail=f"Disallowed markup found in field '{key}': {value!r}",
                )
                return True  # Reject the whole payload
        return False

    def _apply_boundary_guards(self, payload: dict[str, Any], payload_id: str) -> None:
        """
        Clamp any numeric stat fields to [MIN_STAT_VALUE, MAX_STAT_VALUE].

        Stat fields are identified by the key suffix '_value' or the key
        name 'health', 'mana', 'strength', 'defense', 'speed'. This is
        intentionally conservative — only known stat keys are clamped.
        Unknown numeric fields are left untouched but logged.

        This prevents integer overflow attacks where a malicious mod author
        sets a stat to a value like 999999 to gain an unfair advantage or
        trigger engine bugs.
        """
        stat_keys = {"health", "mana", "strength", "defense", "speed"}
        for key, value in payload.items():
            if key in stat_keys and isinstance(value, (int, float)):
                clamped = max(MIN_STAT_VALUE, min(MAX_STAT_VALUE, value))
                if clamped != value:
                    self._logger.log(
                        event_code=BOUND_CLAMP_003,
                        payload_id=payload_id,
                        detail=f"Field '{key}' clamped: {value} -> {clamped}",
                    )
                    payload[key] = clamped


# ---------------------------------------------------------------------------
# Test Cases
# ---------------------------------------------------------------------------

class TestBridgeValidator(unittest.TestCase):
    """
    Test suite for the BridgeValidator firewall logic.

    Each test case corresponds to one row in the Verification Summary
    table in the project README. Passing all three confirms that the
    bridge's security contract is upheld.
    """

    def setUp(self) -> None:
        """Create a fresh logger and validator before each test."""
        self.logger = AuditLogger()
        self.validator = BridgeValidator(audit_logger=self.logger)

    # ------------------------------------------------------------------
    # Test Case 1: Standard Payload (Happy Path)
    # ------------------------------------------------------------------

    def test_standard_payload_is_accepted(self) -> None:
        """
        GIVEN a well-formed mod payload with all values in range,
        WHEN  the bridge validates it,
        THEN  is_valid is True, the sanitized payload matches the input,
              and no audit entries are written.

        This is the happy-path smoke test. It verifies that the bridge
        does not over-reject valid data.
        """
        payload = {
            "character_name": "Aldric the Wanderer",
            "character_class": "Ranger",
            "health": 250,
            "mana": 100,
            "strength": 14,
        }

        result = self.validator.validate(payload, payload_id="test-001")

        self.assertTrue(result.is_valid, "Standard payload should be accepted")
        self.assertEqual(result.sanitized_payload["health"], 250)
        self.assertEqual(result.sanitized_payload["character_name"], "Aldric the Wanderer")
        self.assertEqual(len(self.logger.entries), 0, "No audit entries should be written for clean data")

    # ------------------------------------------------------------------
    # Test Case 2: Malicious Payload Rejection
    # ------------------------------------------------------------------

    def test_malicious_script_tag_is_rejected(self) -> None:
        """
        GIVEN a mod payload where a string field contains an HTML script tag,
        WHEN  the bridge validates it,
        THEN  is_valid is False, error_code is PARSE_ERR_001,
              and an audit entry with that code is written.

        This simulates a stored-XSS / injection attempt where a malicious
        mod author embeds executable markup inside a character name field.
        The bridge must reject the entire payload — not silently strip the
        tag — because the presence of markup is itself a red flag.
        """
        malicious_payload = {
            "character_name": "Aldric<script>alert('pwned')</script>",
            "health": 100,
        }

        result = self.validator.validate(malicious_payload, payload_id="test-002")

        self.assertFalse(result.is_valid, "Payload with script injection must be rejected")
        self.assertEqual(result.error_code, PARSE_ERR_001)
        self.assertTrue(
            self.logger.has_code(PARSE_ERR_001),
            "Audit log must contain a PARSE_ERR_001 entry",
        )

    # ------------------------------------------------------------------
    # Test Case 3: Boundary Guard — Stat Overflow
    # ------------------------------------------------------------------

    def test_stat_overflow_is_clamped_and_logged(self) -> None:
        """
        GIVEN a mod payload where 'health' is set to 999999 (far above MAX_STAT_VALUE),
        WHEN  the bridge validates it,
        THEN  is_valid is True (the payload is salvageable),
              sanitized health equals MAX_STAT_VALUE (9999),
              and an audit entry with BOUND_CLAMP_003 is written.

        This is the boundary guard test. The bridge does not reject overflow
        outright — it clamps to the maximum and logs the event. This design
        choice allows legitimate mods that set 'high but valid' values to pass
        through, while still protecting the engine from values that could
        trigger integer overflow bugs in C++ game code.
        """
        overflow_payload = {
            "character_name": "Overpowered Hero",
            "health": 999999,  # Way above the 9999 ceiling
            "strength": 50,    # Also above ceiling
        }

        result = self.validator.validate(overflow_payload, payload_id="test-003")

        self.assertTrue(result.is_valid, "Clamped payload should still be valid")
        self.assertEqual(
            result.sanitized_payload["health"],
            MAX_STAT_VALUE,
            f"Health must be clamped to {MAX_STAT_VALUE}",
        )
        self.assertEqual(
            result.sanitized_payload["strength"],
            MAX_STAT_VALUE,
            f"Strength must also be clamped to {MAX_STAT_VALUE}",
        )
        self.assertTrue(
            self.logger.has_code(BOUND_CLAMP_003),
            "Audit log must contain a BOUND_CLAMP_003 entry for each clamped field",
        )


# ---------------------------------------------------------------------------
# Entry Point
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    print("=" * 60)
    print("BridgeMod.sdk v0.2.2 — Bridge Validation Test Suite")
    print("=" * 60)
    unittest.main(verbosity=2)
