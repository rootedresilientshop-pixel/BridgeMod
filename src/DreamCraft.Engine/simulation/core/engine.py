"""Core pulse engine for DreamCraft: Legacies v2."""

from __future__ import annotations

import json
import logging
import os
import time
from pathlib import Path
from typing import Any

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.llm.client import LLMClient
from dreamcraft_v2.scribe_service import ScribeService
from dreamcraft_v2.simulation.characters.character import Character, deterministic_decision
from dreamcraft_v2.simulation.config import Settings
from dreamcraft_v2.simulation.events.event_manager import EventManager
from dreamcraft_v2.vault_manager import VaultManager

LOGGER = logging.getLogger(__name__)


class SimulationEngine:
    """Run staged world pulses with a systemic priority queue."""

    def __init__(
        self,
        database: DatabaseManager,
        llm_client: LLMClient,
        event_manager: EventManager,
        settings: Settings,
    ) -> None:
        self.database = database
        self.llm_client = llm_client
        self.event_manager = event_manager
        self.settings = settings
        self._tick = 0

    async def run_day(self, day_number: int) -> dict[str, Any]:
        """Backwards-compatible alias to the pulse pipeline."""
        return await self.run_pulse(day_number)

    async def run_pulse(self, day_number: int) -> dict[str, Any]:
        """Run a full staged pulse: entropy, threat impact, actions, and scribe."""
        self._tick = 0
        summary: dict[str, Any] = {
            "day": day_number,
            "stages": {},
            "actions": {"level_0": 0, "level_1": 0, "level_2": 0},
            "llm_calls": 0,
            "events": 0,
        }

        stage_a = await self._run_stage(day_number, "stage_a_entropy", self._stage_world_entropy)
        summary["stages"]["A"] = stage_a

        stage_b = await self._run_stage(day_number, "stage_b_threat_impact", self._stage_threat_impact)
        summary["stages"]["B"] = stage_b

        stage_c = await self._run_stage(day_number, "stage_c_action_resolution", self._stage_action_resolution)
        summary["stages"]["C"] = stage_c
        summary["actions"] = stage_c.get("actions", summary["actions"])
        summary["llm_calls"] = int(stage_c.get("llm_calls", 0))

        stage_d = await self._run_stage(
            day_number,
            "stage_d_scribe",
            lambda d: self._stage_scribe(d, summary),
        )
        summary["stages"]["D"] = stage_d

        events = await self.database.get_events_for_day(day_number)
        summary["events"] = len(events)
        await self.database.log_simulation(
            day_number,
            "pulse_summary",
            json.dumps(summary, default=str),
            duration_ms=None,
        )
        return summary

    async def _run_stage(
        self,
        day_number: int,
        phase: str,
        stage_callable: Any,
    ) -> dict[str, Any]:
        started = time.perf_counter()
        result = await stage_callable(day_number)
        duration_ms = int((time.perf_counter() - started) * 1000)
        await self.database.log_simulation(
            day_number,
            phase,
            f"{phase} complete",
            duration_ms=duration_ms,
        )
        payload = dict(result)
        payload["duration_ms"] = duration_ms
        return payload

    async def _stage_world_entropy(self, day_number: int) -> dict[str, Any]:
        """Stage A: increment character needs and decay region prosperity."""
        rows = await self.database.fetch_all(
            """
            SELECT id, health, hunger, energy, status, location_id
            FROM characters
            WHERE status IN ('alive', 'injured', 'unconscious')
            """
        )

        dead_now = 0
        for row in rows:
            health = int(row.get("health", 100))
            hunger = min(100, int(row.get("hunger", 0)) + 8)
            energy = max(0, int(row.get("energy", 100)) - 6)
            exhaustion = 100 - energy

            if hunger >= 80:
                health = max(0, health - 5)
            else:
                health = min(100, health + 1)

            if health <= 0:
                status = "dead"
                dead_now += 1
            elif exhaustion > 85:
                status = "unconscious"
            elif health < 35:
                status = "injured"
            else:
                status = "alive"

            await self.database.update_character_state(
                int(row["id"]),
                health=health,
                hunger=hunger,
                energy=energy,
                location_id=row.get("location_id"),
                status=status,
            )

        await self.database.execute(
            """
            UPDATE regions
            SET prosperity = MAX(0, prosperity - 1),
                danger_level = MIN(
                    10,
                    danger_level + CASE
                        WHEN EXISTS (
                            SELECT 1 FROM threats t
                            WHERE t.region_id = regions.id AND t.is_active = 1
                        ) THEN 1
                        ELSE 0
                    END
                )
            """
        )

        alive_count_row = await self.database.fetch_one(
            "SELECT COUNT(*) AS count FROM characters WHERE status != 'dead'"
        )
        dead_count_row = await self.database.fetch_one(
            "SELECT COUNT(*) AS count FROM characters WHERE status = 'dead'"
        )
        alive_count = int((alive_count_row or {}).get("count", 0))
        dead_count = int((dead_count_row or {}).get("count", 0))

        await self.database.execute(
            """
            INSERT INTO world_state (
                day, day_number, season, weather, time_of_day,
                global_events, resource_levels,
                population_total, population_alive, population_dead, updated_at
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, CURRENT_TIMESTAMP)
            ON CONFLICT(day) DO UPDATE SET
                day_number = excluded.day_number,
                season = excluded.season,
                weather = excluded.weather,
                time_of_day = excluded.time_of_day,
                global_events = excluded.global_events,
                resource_levels = excluded.resource_levels,
                population_total = excluded.population_total,
                population_alive = excluded.population_alive,
                population_dead = excluded.population_dead,
                updated_at = CURRENT_TIMESTAMP
            """,
            (
                day_number,
                day_number,
                self._season_for_day(day_number),
                self._weather_for_day(day_number),
                "morning",
                json.dumps({"pulse": day_number}),
                json.dumps({"entropy_step": 1}),
                alive_count + dead_count,
                alive_count,
                dead_count,
            ),
        )
        return {
            "characters_processed": len(rows),
            "new_deaths": dead_now,
            "population_alive": alive_count,
            "population_dead": dead_count,
        }

    async def _stage_threat_impact(self, day_number: int) -> dict[str, Any]:
        """Stage B: apply threat extortion/tax impacts to infrastructure NPCs."""
        threats = await self.database.fetch_all(
            """
            SELECT id, name, region_id
            FROM threats
            WHERE is_active = 1 AND region_id IS NOT NULL
            """
        )

        impacted_npcs = 0
        for threat in threats:
            threat_id = int(threat["id"])
            threat_name = str(threat["name"])
            region_id = int(threat["region_id"])

            npcs = await self.database.fetch_all(
                """
                SELECT id, role
                FROM infrastructure_npcs
                WHERE region_id = ? AND status = 'active'
                """,
                (region_id,),
            )

            for npc in npcs:
                npc_id = int(npc["id"])
                role = str(npc.get("role", "")).lower()
                if role in {"blacksmith", "merchant", "innkeeper"}:
                    impact_type = "extorted"
                    impact_value = 20
                    await self.database.execute(
                        """
                        UPDATE infrastructure_npcs
                        SET extorted_by_threat_id = ?, updated_at = CURRENT_TIMESTAMP,
                            notes = ?
                        WHERE id = ?
                        """,
                        (
                            threat_id,
                            f"{threat_name}: service_cost_modifier_pct=20",
                            npc_id,
                        ),
                    )
                else:
                    impact_type = "impacted"
                    impact_value = 10
                    await self.database.execute(
                        """
                        UPDATE infrastructure_npcs
                        SET impacted_by_threat_id = ?, updated_at = CURRENT_TIMESTAMP,
                            notes = ?
                        WHERE id = ?
                        """,
                        (
                            threat_id,
                            f"{threat_name}: service_cost_modifier_pct=10",
                            npc_id,
                        ),
                    )

                await self.database.execute(
                    """
                    INSERT INTO infrastructure_npc_threat_history(
                        infrastructure_npc_id, threat_id, impact_type,
                        impact_value, is_active, started_day, details
                    )
                    VALUES (?, ?, ?, ?, 1, ?, ?)
                    """,
                    (
                        npc_id,
                        threat_id,
                        impact_type,
                        impact_value,
                        day_number,
                        f"Applied by Stage B from threat '{threat_name}'",
                    ),
                )
                impacted_npcs += 1

        return {"active_threats": len(threats), "impacted_npcs": impacted_npcs}

    async def _stage_action_resolution(self, day_number: int) -> dict[str, Any]:
        """Stage C: resolve Level 0/1/2 character actions."""
        rows = await self.database.fetch_all(
            """
            SELECT
                c.*,
                l.region_id AS current_region_id,
                f.name AS faction_name
            FROM characters c
            LEFT JOIN locations l ON l.id = c.location_id
            LEFT JOIN factions f ON f.id = c.faction_id
            WHERE c.status = 'alive'
            ORDER BY c.id
            """
        )
        world_state = await self.database.get_world_state(day_number)
        llm_enabled = self.settings.simulation_burst_enabled and await self.llm_client.health_check()

        actions = {"level_0": 0, "level_1": 0, "level_2": 0}
        llm_calls = 0

        for row in rows:
            resolved = await self._resolve_character_action(
                day_number=day_number,
                character_row=row,
                world_state=world_state or {},
                llm_enabled=llm_enabled,
            )
            level_key = f"level_{resolved['level']}"
            if level_key in actions:
                actions[level_key] += 1
            llm_calls += int(resolved.get("llm_calls", 0))

        return {
            "characters_resolved": len(rows),
            "actions": actions,
            "llm_calls": llm_calls,
        }

    async def _resolve_character_action(
        self,
        day_number: int,
        character_row: dict[str, Any],
        world_state: dict[str, Any],
        llm_enabled: bool,
    ) -> dict[str, Any]:
        """Apply the systemic priority queue for one character."""
        character = Character.from_row(character_row)
        hunger = int(character_row.get("hunger", character.hunger))
        energy = int(character_row.get("energy", character.energy))
        exhaustion = 100 - energy

        if hunger > 85 or exhaustion > 85:
            if hunger > 85:
                action = "forage_for_food"
                new_hunger = max(0, hunger - 25)
                new_energy = max(0, energy - 5)
                description = f"{character.name} bypassed AI and foraged for food."
            else:
                action = "rest_at_inn"
                new_hunger = min(100, hunger + 5)
                new_energy = min(100, energy + 35)
                description = f"{character.name} bypassed AI and rested immediately."

            await self.database.update_character_state(
                character.id,
                health=character.health,
                hunger=new_hunger,
                energy=new_energy,
                location_id=character.location_id,
                status=character.status,
            )
            await self._insert_event(
                day_number,
                "survival",
                4,
                character.location_id,
                [character.id],
                {"action": action, "priority_level": 0},
                description,
            )
            return {"level": 0, "llm_calls": 0}

        threat = await self._active_threat_for_region(character_row.get("current_region_id"))
        if threat:
            health = int(character_row.get("health", character.health))
            level = int(character_row.get("level", 1))
            combat_ready = health >= 55 and energy >= 35
            forced_action = "combat" if combat_ready else "stealth"

            if forced_action == "combat":
                threat_severity = int(threat.get("severity", 5))
                score = health + energy + (level * 5)
                threshold = 65 + (threat_severity * 4)
                if score >= threshold:
                    bounty = int(threat.get("bounty", 0))
                    await self.database.execute(
                        """
                        UPDATE characters
                        SET gold = gold + ?, experience = experience + 20
                        WHERE id = ?
                        """,
                        (bounty, character.id),
                    )
                    if score - threshold >= 10:
                        await self.database.execute(
                            """
                            UPDATE threats
                            SET is_active = 0, resolved_day = ?, updated_at = CURRENT_TIMESTAMP
                            WHERE id = ?
                            """,
                            (day_number, int(threat["id"])),
                        )
                    description = f"{character.name} forced a combat resolution against {threat['name']}."
                    severity = 7
                else:
                    new_health = max(0, health - 15)
                    new_energy = max(0, energy - 15)
                    status = "dead" if new_health <= 0 else "injured"
                    await self.database.update_character_state(
                        character.id,
                        health=new_health,
                        hunger=min(100, hunger + 6),
                        energy=new_energy,
                        location_id=character.location_id,
                        status=status,
                    )
                    description = f"{character.name} failed a forced combat resolution against {threat['name']}."
                    severity = 8
            else:
                await self.database.update_character_state(
                    character.id,
                    health=character.health,
                    hunger=min(100, hunger + 6),
                    energy=max(0, energy - 10),
                    location_id=character.location_id,
                    status=character.status,
                )
                description = f"{character.name} forced a stealth resolution around {threat['name']}."
                severity = 5

            await self._insert_event(
                day_number,
                forced_action,
                severity,
                character.location_id,
                [character.id],
                {
                    "priority_level": 1,
                    "threat_id": int(threat["id"]),
                    "threat_name": threat["name"],
                    "region_id": threat.get("region_id"),
                },
                description,
            )
            return {"level": 1, "llm_calls": 0}

        if llm_enabled:
            prompt = self._build_llm_prompt(character_row, world_state)
            llm_text = await self.llm_client.make_completion(
                prompt=prompt,
                system_prompt="You are driving a persistent fantasy simulation. Return one concise action.",
            )
            decision = (llm_text or "").strip() or "pursue faction-safe objective"
            llm_calls = 1
        else:
            decision = str(
                deterministic_decision(
                    character,
                    {"location_options": [character.location_id or 0]},
                )
            )
            llm_calls = 0

        await self.database.execute(
            """
            UPDATE characters
            SET experience = experience + 3,
                hunger = MIN(100, hunger + 4),
                energy = MAX(0, energy - 5),
                updated_at = CURRENT_TIMESTAMP
            WHERE id = ?
            """,
            (character.id,),
        )
        await self._insert_event(
            day_number,
            "narrative_goal",
            3,
            character.location_id,
            [character.id],
            {"priority_level": 2, "decision": decision},
            f"{character.name} pursued an emergent objective.",
        )
        return {"level": 2, "llm_calls": llm_calls}

    async def _active_threat_for_region(self, region_id: Any) -> dict[str, Any] | None:
        """Return one active threat for the supplied region when available."""
        if region_id is None:
            return None
        return await self.database.fetch_one(
            """
            SELECT id, name, severity, bounty, region_id
            FROM threats
            WHERE region_id = ? AND is_active = 1
            ORDER BY severity DESC, id ASC
            LIMIT 1
            """,
            (int(region_id),),
        )

    def _build_llm_prompt(self, character_row: dict[str, Any], world_state: dict[str, Any]) -> str:
        """Build Level-2 narrative-goal prompt from faction, traits, and world state."""
        name = character_row.get("name", "Unknown")
        faction = character_row.get("faction_name") or "Unaffiliated"
        personality = character_row.get("personality", "{}")
        goals = character_row.get("goals", "[]")
        weather = world_state.get("weather", "clear")
        season = world_state.get("season", "unknown")
        day = world_state.get("day", "unknown")
        return (
            f"Character: {name}\n"
            f"Faction: {faction}\n"
            f"Traits: {personality}\n"
            f"Goals: {goals}\n"
            f"World State: day={day}, season={season}, weather={weather}\n"
            "Choose one short narrative goal for this pulse."
        )

    async def _stage_scribe(self, day_number: int, summary: dict[str, Any]) -> dict[str, Any]:
        """Stage D: update legacy ledger and write markdown pulse summary."""
        candidates = await self.database.fetch_all(
            """
            SELECT c.id, c.name, c.faction_id, l.region_id, c.level, c.legacy_score,
                   c.status, c.retired_at, c.died_at
            FROM characters c
            LEFT JOIN locations l ON l.id = c.location_id
            WHERE c.status IN ('retired', 'dead')
            """
        )

        inserted_ledger_rows = 0
        for row in candidates:
            existing = await self.database.fetch_one(
                "SELECT id FROM legacy_ledger WHERE character_id = ? LIMIT 1",
                (int(row["id"]),),
            )
            if existing:
                continue
            await self.database.execute(
                """
                INSERT INTO legacy_ledger(
                    character_id, character_name, final_faction_id, final_region_id,
                    final_level, legacy_score, retirement_reason, retirement_day, death_day,
                    epitaph, notable_deeds, world_impact
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    int(row["id"]),
                    str(row["name"]),
                    row.get("faction_id"),
                    row.get("region_id"),
                    int(row.get("level", 1)),
                    int(row.get("legacy_score", 0)),
                    f"status={row.get('status')}",
                    day_number if row.get("status") == "retired" else None,
                    day_number if row.get("status") == "dead" else None,
                    f"{row.get('name')} is remembered by the realm.",
                    "[]",
                    "{}",
                ),
            )
            inserted_ledger_rows += 1

        pulse_dir = Path("logs") / "pulses"
        pulse_dir.mkdir(parents=True, exist_ok=True)
        summary_path = pulse_dir / f"pulse_day_{day_number}.md"
        summary_path.write_text(self._render_markdown_summary(day_number, summary), encoding="utf-8")

        scribe = ScribeService(self.database, self.llm_client, output_root="artifacts")
        journal_output = await scribe.generate_daily_journals(day_number)
        snapshot_output = await scribe.generate_character_snapshots(day_number)

        export_root = os.getenv("SAGA_EXPORT_ROOT", "/app/exports")
        vault = VaultManager(export_root=export_root)
        manifest = vault.create_daily_export(
            journals_dir=journal_output["journals_dir"],
            snapshots_dir=snapshot_output["snapshots_dir"],
            db_path=self.settings.db_path,
            day_number=day_number,
        )

        return {
            "legacy_ledger_inserts": inserted_ledger_rows,
            "markdown_summary": str(summary_path),
            "journals_dir": journal_output["journals_dir"],
            "snapshots_cards_path": snapshot_output["cards_path"],
            "export_dir": manifest["export_dir"],
        }

    def _render_markdown_summary(self, day_number: int, summary: dict[str, Any]) -> str:
        """Render a pulse execution report for operators."""
        actions = summary.get("actions", {})
        stages = summary.get("stages", {})
        lines = [
            f"# Pulse Day {day_number}",
            "",
            "## Systemic Priority Queue",
            f"- Level 0 actions: {actions.get('level_0', 0)}",
            f"- Level 1 actions: {actions.get('level_1', 0)}",
            f"- Level 2 actions: {actions.get('level_2', 0)}",
            f"- LLM calls: {summary.get('llm_calls', 0)}",
            "",
            "## Stage Pipeline",
            f"- Stage A (World Entropy): `{json.dumps(stages.get('A', {}), default=str)}`",
            f"- Stage B (Threat Impact): `{json.dumps(stages.get('B', {}), default=str)}`",
            f"- Stage C (Action Resolution): `{json.dumps(stages.get('C', {}), default=str)}`",
            f"- Stage D (Scribe): `{json.dumps(stages.get('D', {}), default=str)}`",
            "",
            f"- Total events recorded for day: {summary.get('events', 0)}",
        ]
        return "\n".join(lines) + "\n"

    async def _insert_event(
        self,
        day: int,
        event_type: str,
        severity: int,
        location_id: int | None,
        participants: list[int],
        outcome: dict[str, Any],
        description: str,
    ) -> None:
        """Insert event rows directly for custom pulse event types."""
        await self.database.execute(
            """
            INSERT INTO events(
                day, tick, type, severity, is_major, location_id, description, participants, outcome
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                day,
                self._next_tick(),
                event_type,
                severity,
                1 if severity >= int(self.settings.major_event_threshold) else 0,
                location_id,
                description,
                json.dumps(participants),
                json.dumps(outcome),
            ),
        )

    def _next_tick(self) -> int:
        self._tick += 1
        return self._tick

    def _season_for_day(self, day_number: int) -> str:
        seasons = ["spring", "summer", "autumn", "winter"]
        return seasons[((day_number - 1) // 90) % len(seasons)]

    def _weather_for_day(self, day_number: int) -> str:
        options = ["clear", "rain", "fog", "storm", "windy"]
        return options[(day_number - 1) % len(options)]
