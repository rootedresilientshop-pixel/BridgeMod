"""LLM response parsing and validation."""

import json
import re
from typing import Any


def parse_json_response(text: str) -> dict[str, Any] | None:
    """Extract JSON from LLM response (handles markdown, extra text, arrays)."""
    if not text:
        return None

    # Extract from markdown code block
    code_block_match = re.search(r'```(?:json)?\s*(\{.*?\})\s*```', text, re.DOTALL)
    if code_block_match:
        text = code_block_match.group(1)

    # Find JSON object in text
    json_match = re.search(r'\{.*\}', text, re.DOTALL)
    if json_match:
        text = json_match.group(0)
        try:
            return json.loads(text)
        except json.JSONDecodeError:
            pass

    # Try matching JSON arrays
    array_match = re.search(r'\[.*\]', text, re.DOTALL)
    if array_match:
        try:
            arr = json.loads(array_match.group(0))
            if isinstance(arr, list):
                return {"events": arr}  # Wrap in expected structure
        except json.JSONDecodeError:
            pass

    return None


def validate_movement_response(data: dict[str, Any] | None) -> bool:
    """Validate movement decision response."""
    if not isinstance(data, dict) or "decisions" not in data:
        return False

    decisions = data["decisions"]
    if not isinstance(decisions, list):
        return False

    for decision in decisions:
        if not isinstance(decision, dict):
            return False
        required = {"character_name", "destination", "reason"}
        if not all(k in decision for k in required):
            return False

    return True


def validate_interaction_response(data: dict[str, Any] | None) -> bool:
    """Validate interaction outcome response."""
    if not isinstance(data, dict) or "events" not in data:
        return False

    events = data["events"]
    if not isinstance(events, list):
        return False

    for event in events:
        if not isinstance(event, dict):
            return False
        required = {"type", "severity", "participants", "description"}
        if not all(k in event for k in required):
            return False
        if not 1 <= event["severity"] <= 10:
            return False

    return True


def validate_political_response(data: dict[str, Any] | None) -> bool:
    """Validate faction politics response."""
    if not isinstance(data, dict):
        return False

    required = {"action", "type", "severity"}
    if not all(k in data for k in required):
        return False

    if not 1 <= data["severity"] <= 10:
        return False

    if "power_change" in data and not -5 <= data["power_change"] <= 5:
        return False

    return True


def validate_relationship_response(data: dict[str, Any] | None) -> bool:
    """Validate relationship evaluation response."""
    if not isinstance(data, dict):
        return False

    required = {"new_type", "new_strength", "reason"}
    if not all(k in data for k in required):
        return False

    if not 0 <= data["new_strength"] <= 100:
        return False

    valid_types = {"friend", "enemy", "rival", "ally", "romantic", "mentor", "neutral", "acquaintance"}
    if data["new_type"] not in valid_types:
        return False

    return True
