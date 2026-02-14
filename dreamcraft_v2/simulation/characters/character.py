"""Character domain model and deterministic behavior helpers."""

from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any


@dataclass(slots=True)
class Character:
    """In-memory character model for simulation phases."""

    id: int
    name: str
    race: str
    class_type: str
    personality: dict[str, Any]
    goals: list[str]
    health: int
    hunger: int
    energy: int
    location_id: int | None
    faction_id: int | None
    status: str

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> "Character":
        """Build a Character from a database row dictionary."""
        personality = _safe_json_object(row.get("personality"), {})
        goals = _safe_json_object(row.get("goals"), [])
        goals_list = goals if isinstance(goals, list) else []
        return cls(
            id=int(row["id"]),
            name=str(row["name"]),
            race=str(row["race"]),
            class_type=str(row["class_type"]),
            personality=personality if isinstance(personality, dict) else {},
            goals=[str(goal) for goal in goals_list],
            health=int(row.get("health", 100)),
            hunger=int(row.get("hunger", 0)),
            energy=int(row.get("energy", 100)),
            location_id=row.get("location_id"),
            faction_id=row.get("faction_id"),
            status=str(row.get("status", "alive")),
        )


def process_needs(character: Character) -> Character:
    """Apply daily needs progression to a character."""
    if character.status == "dead":
        return character

    character.hunger = min(100, character.hunger + 8)
    character.energy = max(0, character.energy - 6)
    if character.hunger >= 80:
        character.health = max(0, character.health - 5)
    else:
        character.health = min(100, character.health + 2)

    if character.health <= 0:
        character.status = "dead"
    elif character.health < 35:
        character.status = "injured"
    elif character.energy < 10:
        character.status = "unconscious"
    else:
        character.status = "alive"
    return character


def decide_movement(character: Character, available_locations: list[int]) -> int | None:
    """Pick a destination for a character using deterministic weighted logic."""
    if not available_locations or character.status == "dead":
        return character.location_id

    if character.hunger > 70:
        return available_locations[0]
    if character.energy < 25:
        return available_locations[min(1, len(available_locations) - 1)]

    current = character.location_id if character.location_id is not None else 0
    next_index = (character.id + current) % len(available_locations)
    return available_locations[next_index]


def deterministic_decision(character: Character, context: dict[str, Any]) -> dict[str, Any]:
    """Return fallback decision payload when LLM is unavailable."""
    location_options = context.get("location_options", [])
    destination = decide_movement(character, [int(item) for item in location_options]) if location_options else character.location_id
    action = "rest" if character.energy < 20 else "socialize"
    if character.hunger > 75:
        action = "seek_food"
    if character.status in {"injured", "unconscious"}:
        action = "recover"
    return {"character_id": character.id, "action": action, "destination": destination}


def get_personality_prompt(character: Character) -> str:
    """Build a concise prompt grounding model behavior in personality and goals."""
    traits = ", ".join(f"{key}:{value}" for key, value in character.personality.items()) or "balanced"
    goals = ", ".join(character.goals) or "survive and adapt"
    return (
        f"Character: {character.name} ({character.race} {character.class_type}). "
        f"Traits: {traits}. Goals: {goals}. "
        "Choose one practical action for this simulation tick."
    )


def _safe_json_object(raw: Any, fallback: dict[str, Any] | list[Any]) -> Any:
    """Parse JSON values safely with a fallback."""
    if raw is None:
        return fallback
    if isinstance(raw, (dict, list)):
        return raw
    if isinstance(raw, str):
        try:
            return json.loads(raw)
        except json.JSONDecodeError:
            return fallback
    return fallback
