"""Simulation engine tests."""

from __future__ import annotations

import pytest

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.data.seed import seed_world
from dreamcraft_v2.simulation.config import Settings
from dreamcraft_v2.simulation.core.engine import SimulationEngine
from dreamcraft_v2.simulation.events.event_manager import EventManager


class DummyLLMClient:
    """Minimal async LLM client stub for tests."""

    async def health_check(self) -> bool:
        """Always report unavailable to trigger fallback."""
        return False

    async def make_batch_completion(self, prompts: list[str]) -> list[str | None]:
        """Return empty model responses."""
        return [None for _ in prompts]

    async def close(self) -> None:
        """No-op close method."""
        return None


@pytest.mark.asyncio
async def test_run_day_creates_events_and_world_state(test_database: DatabaseManager) -> None:
    """run_day should produce events and world state output."""
    await seed_world(test_database)
    event_manager = EventManager(test_database, major_event_threshold=7)
    settings = Settings(
        db_path=test_database.database_path,
        llm_endpoint="http://llm.local:11434/v1",
        llm_model="mistral",
        llm_timeout=5,
        simulation_burst_enabled=True,
        decision_window_start="01:00",
        decision_window_end="04:00",
        narrative_window_start="04:00",
        narrative_window_end="06:00",
        log_level="INFO",
        api_port=8002,
        major_event_threshold=7,
    )
    engine = SimulationEngine(
        database=test_database,
        llm_client=DummyLLMClient(),  # type: ignore[arg-type]
        event_manager=event_manager,
        settings=settings,
    )

    summary = await engine.run_day(1)
    state = await test_database.get_world_state(1)
    events = await test_database.get_events_for_day(1)

    assert summary["day"] == 1
    assert summary["events"] >= 1
    assert state is not None
    assert len(events) >= 1
