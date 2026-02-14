"""Core simulation engine for daily world progression."""

from __future__ import annotations

import json
import logging
import random
import time
from collections import defaultdict
from typing import Any

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.llm.client import LLMClient
from dreamcraft_v2.simulation.characters.character import (
    Character,
    deterministic_decision,
    get_personality_prompt,
    process_needs,
)
from dreamcraft_v2.simulation.config import Settings
from dreamcraft_v2.simulation.events.event_manager import EventManager, EventType

LOGGER = logging.getLogger(__name__)


class SimulationEngine:
    """Orchestrates all phases of a simulation day."""

    def __init__(
        self,
        database: DatabaseManager,
        llm_client: LLMClient,
        event_manager: EventManager,
        settings: Settings,
    ) -> None:
        """Create a simulation engine."""
        self.database = database
        self.llm_client = llm_client
        self.event_manager = event_manager
        self.settings = settings
        self._tick = 0

    async def run_day(self, day_number: int) -> dict[str, Any]:
        """Run all simulation phases for a day and return summary stats."""
        self._tick = 0
        self._rng = self._rng_for_day(day_number)  # For fallback methods
        summary: dict[str, Any] = {"day": day_number, "events": 0, "llm_calls": 0}

        await self._timed_phase(
            day_number,
            "world_state",
            lambda: self.update_world_state(day_number),
        )
        await self._timed_phase(
            day_number,
            "needs",
            lambda: self.process_character_needs(day_number),
        )
        await self._timed_phase(
            day_number,
            "movement",
            lambda: self.process_character_movement(day_number),
        )
        await self._timed_phase(
            day_number,
            "interactions",
            lambda: self.process_interactions_by_location(day_number),
        )
        await self._timed_phase(
            day_number,
            "relationship_updates",
            lambda: self.update_relationships_from_events(day_number),
        )
        await self._timed_phase(
            day_number,
            "politics",
            lambda: self.process_faction_politics(day_number),
        )
        await self._timed_phase(
            day_number,
            "combat",
            lambda: self.process_combat_encounters(day_number),
        )
        await self._timed_phase(
            day_number,
            "event_flagging",
            lambda: self.generate_events_with_flagging(day_number),
        )
        decision_queue = await self._timed_phase(
            day_number,
            "decision_queue",
            lambda: self.queue_llm_decision_requests(day_number),
        )
        applied = await self._timed_phase(
            day_number,
            "decision_apply",
            lambda: self.apply_llm_decisions(day_number, decision_queue),
        )
        await self._timed_phase(
            day_number,
            "summary_log",
            lambda: self.log_simulation_results(day_number, applied),
        )

        events = await self.database.get_events_for_day(day_number)
        summary["events"] = len(events)
        summary["llm_calls"] = int(applied.get("llm_calls", 0))
        return summary

    async def update_world_state(self, day_number: int) -> None:
        """Set season/weather/time metadata for the current day."""
        season = self._season_for_day(day_number)
        weather = self._weather_for_day(day_number)
        alive = await self.database.fetch_one(
            "SELECT COUNT(*) AS count FROM characters WHERE status != 'dead'"
        )
        dead = await self.database.fetch_one(
            "SELECT COUNT(*) AS count FROM characters WHERE status = 'dead'"
        )
        await self.database.insert_world_state(
            day=day_number,
            season=season,
            weather=weather,
            time_of_day="morning",
            global_events={"note": f"Day {day_number} initialized"},
            resource_levels={"food": max(0, 100 - (day_number % 15))},
            population_alive=int((alive or {}).get("count", 0)),
            population_dead=int((dead or {}).get("count", 0)),
        )

    async def process_character_needs(self, day_number: int) -> None:
        """Advance hunger/energy/health for all living characters."""
        rows = await self.database.get_alive_characters()
        for row in rows:
            character = process_needs(Character.from_row(row))
            await self.database.update_character_state(
                character.id,
                health=character.health,
                hunger=character.hunger,
                energy=character.energy,
                location_id=character.location_id,
                status=character.status,
            )
            if character.status in {"injured", "dead", "unconscious"}:
                severity = 9 if character.status == "dead" else 6
                await self.event_manager.create_event(
                    day=day_number,
                    event_type=EventType.NEEDS if character.status != "dead" else EventType.DEATH,
                    severity=severity,
                    location=character.location_id,
                    participants=[character.id],
                    outcome={"status": character.status},
                    description=f"{character.name} status changed to {character.status}",
                    tick=self._next_tick(),
                )

    async def process_character_movement(self, day_number: int) -> None:
        """Move characters via LLM decisions, batched by current location."""
        from dreamcraft_v2.llm.parser import parse_json_response, validate_movement_response
        from dreamcraft_v2.llm.prompts import movement_batch_prompt

        locations_data = await self.database.get_locations()
        if not locations_data:
            return

        # Build location lookup
        location_map = {int(loc["id"]): loc for loc in locations_data}

        # Group characters by current location
        rows = await self.database.get_alive_characters()
        grouped: dict[int | None, list[dict]] = defaultdict(list)
        for row in rows:
            grouped[row.get("location_id")].append(row)

        # Process each location batch
        for current_location_id, char_rows in grouped.items():
            if not char_rows:
                continue

            # Get nearby locations (all except current)
            nearby_locations = [
                loc for loc in locations_data
                if int(loc["id"]) != current_location_id
            ]
            if not nearby_locations:
                continue

            # Get current location info
            current_location = location_map.get(current_location_id)
            location_name = current_location["name"] if current_location else "Unknown"
            location_type = current_location["type"] if current_location else "unknown"

            # Build character data for prompt
            char_data = [
                {
                    "name": char["name"],
                    "race": char["race"],
                    "class_type": char["class_type"],
                    "health": char.get("health", 100),
                    "hunger": char.get("hunger", 0),
                    "energy": char.get("energy", 100),
                    "goals": (
                        json.loads(char["goals"])
                        if isinstance(char.get("goals"), str)
                        else (char.get("goals") or [])
                    ),
                }
                for char in char_rows
            ]

            # Get LLM movement decisions
            prompt = movement_batch_prompt(location_name, location_type, char_data, nearby_locations)
            llm_response = await self.llm_client.make_completion(
                prompt=prompt,
                system_prompt="You are simulating fantasy character movement.",
            )

            # Parse and validate response
            response_data = parse_json_response(llm_response) if llm_response else None
            if not validate_movement_response(response_data):
                # Fallback to deterministic movement
                await self._process_movement_fallback(
                    char_rows, current_location_id, day_number, locations_data
                )
                continue

            # Apply LLM decisions
            decisions_map = {d["character_name"]: d["destination"] for d in response_data["decisions"]}

            for char_row in char_rows:
                character = Character.from_row(char_row)
                original_location = character.location_id

                # Find destination location by name
                destination_location = next(
                    (loc for loc in locations_data if loc["name"] == decisions_map.get(character.name)),
                    None
                )

                if not destination_location:
                    continue

                destination_id = int(destination_location["id"])

                if destination_id != original_location:
                    character.location_id = destination_id
                    await self.database.update_character_state(
                        character.id,
                        health=character.health,
                        hunger=character.hunger,
                        energy=character.energy,
                        location_id=character.location_id,
                        status=character.status,
                    )
                    await self.event_manager.create_event(
                        day=day_number,
                        event_type=EventType.MOVEMENT,
                        severity=2,
                        location=destination_id,
                        participants=[character.id],
                        outcome={"from": original_location, "to": destination_id},
                        description=f"{character.name} moved to {destination_location['name']}",
                        tick=self._next_tick(),
                    )

    async def _process_movement_fallback(
        self,
        char_rows: list[dict],
        current_location_id: int | None,
        day_number: int,
        locations: list[dict],
    ) -> None:
        """Fallback movement when LLM unavailable."""
        for row in char_rows:
            character = Character.from_row(row)
            location_ids = [int(loc["id"]) for loc in locations]

            # Use deterministic fallback: needs-based movement
            destination = deterministic_decision(
                character, {"location_options": location_ids}
            ).get("destination")

            if destination != character.location_id:
                character.location_id = int(destination) if destination is not None else None
                await self.database.update_character_state(
                    character.id,
                    health=character.health,
                    hunger=character.hunger,
                    energy=character.energy,
                    location_id=character.location_id,
                    status=character.status,
                )
                await self.event_manager.create_event(
                    day=day_number,
                    event_type=EventType.MOVEMENT,
                    severity=2,
                    location=character.location_id,
                    participants=[character.id],
                    outcome={"from": current_location_id, "to": character.location_id},
                    description=f"{character.name} moved (fallback)",
                    tick=self._next_tick(),
                )

    async def process_interactions_by_location(self, day_number: int) -> None:
        """Generate location interactions via LLM, with relationship context."""
        from dreamcraft_v2.llm.parser import parse_json_response, validate_interaction_response
        from dreamcraft_v2.llm.prompts import interaction_prompt

        rows = await self.database.fetch_all(
            "SELECT id, name, location_id, race, class_type, status FROM characters WHERE status = 'alive' AND location_id IS NOT NULL"
        )

        grouped: dict[int, list[dict]] = defaultdict(list)
        for row in rows:
            grouped[int(row["location_id"])].append(row)

        locations_data = await self.database.get_locations()
        location_map = {int(loc["id"]): loc for loc in locations_data}

        for location_id, characters in grouped.items():
            if len(characters) < 2:
                continue

            location = location_map.get(location_id)
            location_name = location["name"] if location else "Unknown"
            location_type = location["type"] if location else "unknown"

            # Get full character data
            char_data = []
            char_ids = []
            for char in characters:
                full_data = await self.database.get_character(int(char["id"]))
                if full_data:
                    char_data.append(full_data)
                    char_ids.append(int(full_data["id"]))

            # Get relationships between characters at this location
            relationships = []
            for i, char_a in enumerate(char_ids):
                for char_b in char_ids[i + 1 :]:
                    rel = await self.database.get_relationship_between(char_a, char_b)
                    if rel:
                        relationships.append({
                            "char_a": char_a,
                            "char_b": char_b,
                            "type": rel["type"],
                            "strength": rel["strength"],
                        })

            # Get recent events at this location
            recent = await self.database.fetch_all(
                "SELECT description FROM events WHERE location_id = ? AND day >= ? ORDER BY day DESC LIMIT 5",
                (location_id, day_number - 3),
            )
            recent_events = [e["description"] for e in recent]

            # Build char data for prompt
            char_list = [
                {
                    "id": int(c["id"]),
                    "name": c["name"],
                    "race": c["race"],
                    "class_type": c["class_type"],
                    "status": c["status"],
                    "personality": (
                        json.loads(c["personality"])
                        if isinstance(c.get("personality"), str)
                        else (c.get("personality") or {})
                    ),
                }
                for c in char_data
            ]

            # Get LLM interaction outcomes
            prompt = interaction_prompt(
                location_name, location_type, char_list, relationships, recent_events
            )

            llm_response = await self.llm_client.make_completion(
                prompt=prompt,
                system_prompt="You are simulating fantasy character interactions.",
            )

            response_data = parse_json_response(llm_response) if llm_response else None
            if not validate_interaction_response(response_data):
                # Fallback: generate simple location-based events
                await self._generate_fallback_interaction(location_type, char_ids, location_id, day_number)
                continue

            # Create events from LLM response
            for event in response_data["events"]:
                participant_ids = [int(pid) for pid in event.get("participants", [])]
                if not participant_ids:
                    continue

                await self.event_manager.create_event(
                    day=day_number,
                    event_type=event.get("type", "SOCIAL").upper(),
                    severity=event.get("severity", 3),
                    location=location_id,
                    participants=participant_ids,
                    outcome={
                        "description": event.get("description"),
                        "relationship_changes": event.get("relationship_changes", []),
                    },
                    description=event.get("description", "Interaction occurred"),
                    tick=self._next_tick(),
                )

    async def _generate_fallback_interaction(
        self, location_type: str, char_ids: list[int], location_id: int, day_number: int
    ) -> None:
        """Fallback interaction when LLM unavailable."""
        if location_type == "tavern":
            event_type = EventType.SOCIAL
            severity = 3
        elif location_type == "market":
            event_type = EventType.TRADE
            severity = 4
        elif location_type in ("dungeon", "wilderness"):
            event_type = EventType.COMBAT
            severity = 6
        else:
            event_type = EventType.SOCIAL
            severity = 3

        await self.event_manager.create_event(
            day=day_number,
            event_type=event_type,
            severity=severity,
            location=location_id,
            participants=char_ids,
            outcome={"context": f"{location_type}_fallback"},
            description=f"{len(char_ids)} characters interacted (fallback)",
            tick=self._next_tick(),
        )

    async def process_faction_politics(self, day_number: int) -> None:
        """Generate faction political events via LLM."""
        from dreamcraft_v2.llm.parser import parse_json_response, validate_political_response
        from dreamcraft_v2.llm.prompts import faction_politics_prompt

        # Get factions with member counts
        rows = await self.database.fetch_all(
            """
            SELECT f.*, COUNT(c.id) as member_count
            FROM factions f
            LEFT JOIN characters c ON c.faction_id = f.id AND c.status = 'alive'
            GROUP BY f.id
            """
        )

        for faction_row in rows:
            faction_id = int(faction_row["id"])
            faction_name = faction_row["name"]
            alignment = faction_row.get("alignment", "neutral")
            power = int(faction_row.get("power", 50))
            member_count = int(faction_row.get("member_count", 0))

            # Parse relations JSON
            relations_json = faction_row.get("relations", "{}")
            relations = (
                json.loads(relations_json)
                if isinstance(relations_json, str)
                else (relations_json or {})
            )

            # Get recent political events
            recent = await self.database.fetch_all(
                "SELECT description FROM events WHERE type = 'POLITICAL' AND day >= ? ORDER BY day DESC LIMIT 5",
                (day_number - 7,),
            )
            recent_events = [e["description"] for e in recent]

            # Get LLM political decision
            prompt = faction_politics_prompt(
                faction_name, alignment, power, relations, member_count, recent_events
            )

            llm_response = await self.llm_client.make_completion(
                prompt=prompt,
                system_prompt="You are simulating a fantasy faction's political decisions.",
            )

            response_data = parse_json_response(llm_response) if llm_response else None
            if not validate_political_response(response_data):
                # Fallback: simple consolidation event
                severity = min(8, 3 + (member_count // 10) + (power // 25))
                await self.event_manager.create_event(
                    day=day_number,
                    event_type=EventType.POLITICAL,
                    severity=severity,
                    location=None,
                    participants=[],
                    outcome={"faction_id": faction_id, "member_count": member_count},
                    description=f"{faction_name} consolidated power (fallback)",
                    tick=self._next_tick(),
                )
                continue

            # Create political event
            severity = response_data.get("severity", 5)
            action_desc = response_data.get("action", "Political action")

            await self.event_manager.create_event(
                day=day_number,
                event_type=EventType.POLITICAL,
                severity=severity,
                location=None,
                participants=[],
                outcome={
                    "faction": faction_name,
                    "action_type": response_data.get("type"),
                    "target": response_data.get("target_faction"),
                },
                description=action_desc,
                tick=self._next_tick(),
            )

            # Apply power and relation changes
            power_change = response_data.get("power_change", 0)
            if power_change != 0:
                await self.database.update_faction_power(faction_id, power_change)

            relation_changes = response_data.get("relation_changes", [])
            for change in relation_changes:
                target_name = change.get("faction")
                delta = change.get("change", 0)
                if target_name and delta != 0:
                    await self.database.update_faction_relation(faction_id, target_name, delta)

    async def process_combat_encounters(self, day_number: int) -> None:
        """Process dangerous-location encounters and create combat events."""
        locations = await self.database.fetch_all(
            "SELECT id, danger_level FROM locations WHERE danger_level >= 6"
        )
        for location in locations:
            location_id = int(location["id"])
            danger_level = int(location["danger_level"])
            characters = await self.database.get_characters_at_location(location_id)
            if len(characters) < 2:
                continue
            combatants = [int(char["id"]) for char in characters[: min(6, len(characters))]]
            severity = min(10, max(5, danger_level))
            event_type = EventType.BOSS if severity >= 8 else EventType.COMBAT
            await self.event_manager.create_event(
                day=day_number,
                event_type=event_type,
                severity=severity,
                location=location_id,
                participants=combatants,
                outcome={"danger_level": danger_level},
                description=f"Encounter at location {location_id} with danger {danger_level}",
                tick=self._next_tick(),
            )

    async def update_relationships_from_events(self, day_number: int) -> None:
        """Update relationships based on major events via LLM evaluation."""
        from dreamcraft_v2.llm.parser import parse_json_response, validate_relationship_response
        from dreamcraft_v2.llm.prompts import relationship_evaluation_prompt

        # Get major events from today
        events = await self.database.fetch_all(
            "SELECT * FROM events WHERE day = ? AND severity >= 5 ORDER BY tick",
            (day_number,)
        )

        for event in events:
            event_type = event.get("type", "")
            severity = int(event.get("severity", 5))
            participants_json = event.get("participants", "[]")

            participants = (
                json.loads(participants_json)
                if isinstance(participants_json, str)
                else (participants_json or [])
            )

            if len(participants) < 2:
                continue

            # Evaluate each pair of participants
            for i, char_a_id in enumerate(participants):
                for char_b_id in participants[i + 1 :]:
                    char_a = await self.database.get_character(int(char_a_id))
                    char_b = await self.database.get_character(int(char_b_id))

                    if not char_a or not char_b:
                        continue

                    current_rel = await self.database.get_relationship_between(
                        int(char_a_id), int(char_b_id)
                    )

                    # Get LLM evaluation
                    prompt = relationship_evaluation_prompt(
                        event.get("description", ""),
                        event_type,
                        severity,
                        char_a["name"],
                        char_a.get("personality", {}),
                        char_b["name"],
                        char_b.get("personality", {}),
                        current_rel,
                    )

                    llm_response = await self.llm_client.make_completion(
                        prompt=prompt,
                        system_prompt="You are evaluating relationship changes.",
                    )

                    response_data = parse_json_response(llm_response) if llm_response else None
                    if not validate_relationship_response(response_data):
                        continue

                    new_type = response_data.get("new_type", "neutral")
                    new_strength = int(response_data.get("new_strength", 50))

                    if current_rel:
                        # Update existing
                        await self.database.update_relationship(
                            int(char_a_id),
                            int(char_b_id),
                            new_type,
                            new_strength,
                            f"Day {day_number}: {event_type}",
                        )
                    else:
                        # Create new
                        await self.database.create_relationship(
                            int(char_a_id),
                            int(char_b_id),
                            new_type,
                            new_strength,
                            f"Day {day_number}: First meeting via {event_type}",
                        )

    async def generate_events_with_flagging(self, day_number: int) -> None:
        """Recompute major-event flags for the day based on severity threshold."""
        await self.event_manager.flag_major_events(day_number)

    async def queue_llm_decision_requests(self, day_number: int) -> list[dict[str, Any]]:
        """Build queued decision prompts for alive characters."""
        rows = await self.database.get_alive_characters()
        queue: list[dict[str, Any]] = []
        for row in rows:
            character = Character.from_row(row)
            context = {
                "day": day_number,
                "location_id": character.location_id,
                "status": character.status,
            }
            queue.append(
                {
                    "character": character,
                    "context": context,
                    "prompt": get_personality_prompt(character)
                    + f" Current context: {json.dumps(context)}",
                }
            )
        return queue

    async def apply_llm_decisions(
        self, day_number: int, queue: list[dict[str, Any]]
    ) -> dict[str, Any]:
        """Apply model decisions when available, else deterministic fallback."""
        if not queue:
            return {"llm_calls": 0, "fallback_calls": 0}

        llm_calls = 0
        fallback_calls = 0
        llm_enabled = self.settings.simulation_burst_enabled and await self.llm_client.health_check()
        prompts = [item["prompt"] for item in queue]
        llm_outputs: list[str | None] = []

        if llm_enabled:
            llm_outputs = await self.llm_client.make_batch_completion(prompts=prompts)
            llm_calls = len(prompts)
        else:
            llm_outputs = [None] * len(queue)

        for item, llm_text in zip(queue, llm_outputs, strict=True):
            character: Character = item["character"]
            context: dict[str, Any] = item["context"]
            if llm_text:
                outcome = {"decision": llm_text.strip()}
            else:
                fallback = deterministic_decision(
                    character, {"location_options": [character.location_id or 0]}
                )
                outcome = {"decision": fallback}
                fallback_calls += 1

            await self.event_manager.create_event(
                day=day_number,
                event_type=EventType.SOCIAL,
                severity=3,
                location=character.location_id,
                participants=[character.id],
                outcome=outcome,
                description=f"Decision resolved for {character.name}",
                tick=self._next_tick(),
            )
            _ = context

        return {"llm_calls": llm_calls, "fallback_calls": fallback_calls}

    async def log_simulation_results(self, day_number: int, applied: dict[str, Any]) -> None:
        """Write end-of-day summary to simulation_log."""
        events = await self.database.get_events_for_day(day_number)
        message = (
            f"Day {day_number} complete: events={len(events)} "
            f"llm_calls={applied.get('llm_calls', 0)} "
            f"fallback_calls={applied.get('fallback_calls', 0)}"
        )
        await self.database.log_simulation(day_number, "summary", message, duration_ms=None)
        LOGGER.info(message)

    async def _timed_phase(self, day_number: int, phase: str, phase_callable: Any) -> Any:
        """Run a phase, logging its duration to simulation_log."""
        started = time.perf_counter()
        result = await phase_callable()
        elapsed_ms = int((time.perf_counter() - started) * 1000)
        await self.database.log_simulation(
            day=day_number,
            phase=phase,
            message=f"{phase} phase complete",
            duration_ms=elapsed_ms,
        )
        return result

    def _next_tick(self) -> int:
        """Increment and return an event tick number."""
        self._tick += 1
        return self._tick

    def _season_for_day(self, day_number: int) -> str:
        """Return season name based on day-of-year style cycle."""
        seasons = ["spring", "summer", "autumn", "winter"]
        return seasons[((day_number - 1) // 90) % len(seasons)]

    def _weather_for_day(self, day_number: int) -> str:
        """Return season-influenced weather (deterministic)."""
        rng = self._rng_for_day(day_number)
        season = self._season_for_day(day_number)
        weights = {
            "spring": {"clear": 3, "rain": 4, "fog": 2, "storm": 1, "windy": 2},
            "summer": {"clear": 5, "rain": 2, "fog": 1, "storm": 2, "windy": 1},
            "autumn": {"clear": 2, "rain": 3, "fog": 3, "storm": 2, "windy": 3},
            "winter": {"clear": 2, "rain": 1, "fog": 2, "storm": 3, "windy": 2},
        }
        options = list(weights[season].keys())
        w = list(weights[season].values())
        return rng.choices(options, weights=w, k=1)[0]

    def _rng_for_day(self, day_number: int) -> random.Random:
        """Return deterministic RNG for a day."""
        return random.Random(day_number)
