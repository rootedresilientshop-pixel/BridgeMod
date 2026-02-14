"""Event manager tests."""

from __future__ import annotations

import pytest

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.simulation.events.event_manager import EventManager, EventType


@pytest.mark.asyncio
async def test_event_creation_and_major_flagging(test_database: DatabaseManager) -> None:
    """Create events and verify major event flags."""
    manager = EventManager(test_database, major_event_threshold=7)
    await manager.create_event(
        day=1,
        event_type=EventType.COMBAT,
        severity=8,
        location=None,
        participants=[],
        outcome={"winner": "none"},
        description="Severe battle",
    )
    await manager.create_event(
        day=1,
        event_type=EventType.SOCIAL,
        severity=3,
        location=None,
        participants=[],
        outcome={"result": "chat"},
        description="Minor social event",
    )
    await manager.flag_major_events(day=1)

    major = await manager.get_major_events(day=1)
    assert len(major) == 1
    assert major[0]["severity"] == 8
