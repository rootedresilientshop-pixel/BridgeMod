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
        weather_options = ["clear", "rain", "fog", "storm", "windy"]
        weather = weather_options[day_number % len(weather_options)]
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
        """Move characters across locations using deterministic movement rules."""
        locations = await self.database.get_locations()
        location_ids = [int(location["id"]) for location in locations]
        if not location_ids:
            return
        rows = await self.database.get_alive_characters()
        for row in rows:
            character = Character.from_row(row)
            original_location = character.location_id
            destination = deterministic_decision(
                character, {"location_options": location_ids}
            ).get("destination")
            if destination != original_location:
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
                    outcome={"from": original_location, "to": character.location_id},
                    description=f"{character.name} moved from {original_location} to {character.location_id}",
                    tick=self._next_tick(),
                )

    async def process_interactions_by_location(self, day_number: int) -> None:
        """Generate interaction events batched by location."""
        rows = await self.database.fetch_all(
            "SELECT id, name, location_id FROM characters WHERE status = 'alive' AND location_id IS NOT NULL"
        )
        grouped: dict[int, list[dict[str, Any]]] = defaultdict(list)
        for row in rows:
            grouped[int(row["location_id"])].append(row)

        for location_id, characters in grouped.items():
            if len(characters) < 2:
                continue
            participant_ids = [int(character["id"]) for character in characters]
            interaction_type = EventType.SOCIAL if len(characters) < 6 else EventType.TRADE
            severity = 3 if interaction_type == EventType.SOCIAL else 4
            await self.event_manager.create_event(
                day=day_number,
                event_type=interaction_type,
                severity=severity,
                location=location_id,
                participants=participant_ids,
                outcome={"group_size": len(characters)},
                description=f"{len(characters)} characters interacted at location {location_id}",
                tick=self._next_tick(),
            )

    async def process_faction_politics(self, day_number: int) -> None:
        """Generate faction-political events in faction batches."""
        rows = await self.database.fetch_all(
            """
            SELECT faction_id, COUNT(*) AS member_count
            FROM characters
            WHERE status = 'alive' AND faction_id IS NOT NULL
            GROUP BY faction_id
            """
        )
        for row in rows:
            faction_id = int(row["faction_id"])
            members = int(row["member_count"])
            severity = min(8, 3 + (members // 10))
            await self.event_manager.create_event(
                day=day_number,
                event_type=EventType.POLITICAL,
                severity=severity,
                location=None,
                participants=[],
                outcome={"faction_id": faction_id, "member_count": members},
                description=f"Faction {faction_id} advanced political agenda",
                tick=self._next_tick(),
            )

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

    def _rng_for_day(self, day_number: int) -> random.Random:
        """Return deterministic RNG for a day."""
        return random.Random(day_number)
