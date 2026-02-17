"""Targeted tests for the staged pulse engine and systemic priority queue."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.simulation.config import Settings
from dreamcraft_v2.simulation.core.engine import SimulationEngine
from dreamcraft_v2.simulation.events.event_manager import EventManager


class TrackingLLMClient:
    """Minimal async LLM stub that tracks prompt usage."""

    def __init__(self, *, healthy: bool, response: str = "Secure resources for the faction.") -> None:
        self.healthy = healthy
        self.response = response
        self.calls = 0

    async def health_check(self) -> bool:
        return self.healthy

    async def make_completion(self, prompt: str, system_prompt: str = "", **_: object) -> str | None:
        _ = prompt
        _ = system_prompt
        self.calls += 1
        return self.response

    async def close(self) -> None:
        return None


async def _seed_priority_world(database: DatabaseManager) -> None:
    """Insert minimal entities required for staged pulse testing."""
    await database.execute(
        """
        INSERT INTO regions (id, name, biome, danger_level, prosperity, description)
        VALUES
            (1, 'Red Wastes', 'desert', 7, 60, 'Boss-heavy region'),
            (2, 'Green Vale', 'plains', 2, 75, 'Stable region')
        """
    )

    await database.execute(
        """
        INSERT INTO locations (id, region_id, name, type, description, x, y, danger_level, capacity, resources)
        VALUES
            (1, 1, 'Ash Market', 'market', 'Trade outpost', 5, 7, 6, 80, ?),
            (2, 2, 'Silver Inn', 'town', 'Safe rest point', 1, 1, 1, 100, ?)
        """,
        (
            json.dumps({"food": 50, "ore": 20}),
            json.dumps({"food": 80, "wood": 50}),
        ),
    )

    await database.execute(
        """
        INSERT INTO factions (id, name, type, description, alignment, power, relations)
        VALUES (1, 'Dawn Accord', 'guild', 'Order faction', 'good', 65, ?)
        """,
        (json.dumps({"Dawn Accord": 10}),),
    )

    await database.execute(
        """
        INSERT INTO threats (
            id, name, threat_type, severity, summon_cost, bounty,
            region_influence, region_id, source_faction_id, is_active, spawned_day, description
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            1,
            "Wyrm of Ash",
            "boss",
            5,
            300,
            150,
            json.dumps({"1": {"tax_pct": 20}}),
            1,
            1,
            1,
            1,
            "Extorting frontier trade routes",
        ),
    )

    await database.execute(
        """
        INSERT INTO infrastructure_npcs (id, name, role, region_id, location_id, status)
        VALUES (1, 'Torin', 'blacksmith', 1, 1, 'active')
        """
    )

    # Character A -> Level 0 (hunger already critical)
    await database.execute(
        """
        INSERT INTO characters (
            id, name, race, class_type, personality, goals, health, hunger, energy,
            level, experience, location_id, faction_id, status
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            1,
            "Asha",
            "human",
            "ranger",
            json.dumps({"discipline": 5}),
            json.dumps(["survive"]),
            90,
            90,
            70,
            2,
            0,
            2,
            1,
            "alive",
        ),
    )

    # Character B -> Level 1 (active threat in region 1)
    await database.execute(
        """
        INSERT INTO characters (
            id, name, race, class_type, personality, goals, health, hunger, energy,
            level, experience, location_id, faction_id, status
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            2,
            "Brom",
            "dwarf",
            "warrior",
            json.dumps({"courage": 9}),
            json.dumps(["defend region"]),
            100,
            10,
            100,
            1,
            0,
            1,
            1,
            "alive",
        ),
    )

    # Character C -> Level 2 (healthy, safe region, LLM path)
    await database.execute(
        """
        INSERT INTO characters (
            id, name, race, class_type, personality, goals, health, hunger, energy,
            level, experience, location_id, faction_id, status
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            3,
            "Cira",
            "elf",
            "mage",
            json.dumps({"curiosity": 8}),
            json.dumps(["research artifacts"]),
            100,
            15,
            90,
            1,
            0,
            2,
            1,
            "alive",
        ),
    )

    # Stage D ledger candidate
    await database.execute(
        """
        INSERT INTO characters (
            id, name, race, class_type, personality, goals, health, hunger, energy,
            level, experience, location_id, faction_id, status, legacy_score
        )
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (
            4,
            "Dane",
            "human",
            "cleric",
            json.dumps({"empathy": 7}),
            json.dumps(["mentor novices"]),
            100,
            10,
            80,
            4,
            100,
            2,
            1,
            "retired",
            42,
        ),
    )


@pytest.mark.asyncio
async def test_run_pulse_resolves_levels_and_stages(
    test_database: DatabaseManager,
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    """Validate Stage A/B/C/D outputs and Level 0/1/2 action routing."""
    monkeypatch.chdir(tmp_path)
    await _seed_priority_world(test_database)

    llm_client = TrackingLLMClient(healthy=True, response="Fortify alliance with local artisans.")
    engine = SimulationEngine(
        database=test_database,
        llm_client=llm_client,  # type: ignore[arg-type]
        event_manager=EventManager(test_database, major_event_threshold=7),
        settings=Settings(db_path=test_database.database_path, simulation_burst_enabled=True),
    )

    summary = await engine.run_pulse(1)

    assert summary["actions"]["level_0"] == 1
    assert summary["actions"]["level_1"] == 1
    assert summary["actions"]["level_2"] == 1
    assert summary["llm_calls"] == 1
    assert llm_client.calls == 1

    assert summary["stages"]["A"]["characters_processed"] >= 3
    assert summary["stages"]["B"]["active_threats"] == 1
    assert summary["stages"]["B"]["impacted_npcs"] >= 1
    assert summary["stages"]["D"]["legacy_ledger_inserts"] >= 1

    npc = await test_database.fetch_one("SELECT * FROM infrastructure_npcs WHERE id = 1")
    assert npc is not None
    assert int(npc["extorted_by_threat_id"]) == 1
    assert "service_cost_modifier_pct=20" in str(npc.get("notes", ""))

    ledger = await test_database.fetch_all("SELECT * FROM legacy_ledger WHERE character_id = 4")
    assert len(ledger) == 1

    summary_path = Path(summary["stages"]["D"]["markdown_summary"])
    assert summary_path.exists()
    markdown = summary_path.read_text(encoding="utf-8")
    assert "Systemic Priority Queue" in markdown
    assert "Stage A (World Entropy)" in markdown

    events = await test_database.get_events_for_day(1)
    priority_levels = set()
    for event in events:
        outcome = event.get("outcome", "{}")
        if isinstance(outcome, str):
            parsed = json.loads(outcome)
        else:
            parsed = outcome
        level = parsed.get("priority_level")
        if level is not None:
            priority_levels.add(int(level))
    assert priority_levels.issuperset({0, 1, 2})


@pytest.mark.asyncio
async def test_level0_bypasses_threat_and_llm(
    test_database: DatabaseManager,
    monkeypatch: pytest.MonkeyPatch,
    tmp_path: Path,
) -> None:
    """Ensure survival gating takes precedence over threats and LLM calls."""
    monkeypatch.chdir(tmp_path)

    await test_database.execute(
        """
        INSERT INTO regions (id, name, biome, danger_level, prosperity, description)
        VALUES (1, 'Howling Reach', 'tundra', 8, 55, 'Frozen frontier')
        """
    )
    await test_database.execute(
        """
        INSERT INTO locations (id, region_id, name, type, description, x, y, danger_level, capacity, resources)
        VALUES (1, 1, 'Frostgate', 'town', 'Fortified gate', 0, 0, 5, 50, ?)
        """,
        (json.dumps({"food": 20}),),
    )
    await test_database.execute(
        """
        INSERT INTO threats (
            id, name, threat_type, severity, summon_cost, bounty,
            region_influence, region_id, is_active, spawned_day
        )
        VALUES (1, 'Ice Tyrant', 'boss', 7, 500, 200, ?, 1, 1, 1)
        """,
        (json.dumps({"1": {"tax_pct": 20}}),),
    )
    await test_database.execute(
        """
        INSERT INTO characters (
            id, name, race, class_type, personality, goals, health, hunger, energy,
            level, location_id, status
        )
        VALUES (1, 'Edda', 'human', 'rogue', ?, ?, 80, 95, 90, 2, 1, 'alive')
        """,
        (json.dumps({"caution": 7}), json.dumps(["stay alive"])),
    )

    llm_client = TrackingLLMClient(healthy=True)
    engine = SimulationEngine(
        database=test_database,
        llm_client=llm_client,  # type: ignore[arg-type]
        event_manager=EventManager(test_database, major_event_threshold=7),
        settings=Settings(db_path=test_database.database_path, simulation_burst_enabled=True),
    )

    summary = await engine.run_pulse(1)
    assert summary["actions"]["level_0"] == 1
    assert summary["actions"]["level_1"] == 0
    assert summary["actions"]["level_2"] == 0
    assert summary["llm_calls"] == 0
    assert llm_client.calls == 0

    events = await test_database.get_events_for_day(1)
    assert any(event["type"] == "survival" for event in events)
    assert all(event["type"] != "combat" for event in events)
    assert all(event["type"] != "stealth" for event in events)
    assert all(event["type"] != "narrative_goal" for event in events)
