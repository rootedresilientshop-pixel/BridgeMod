"""Event management and major event flagging for simulation output."""

from __future__ import annotations

import json
from enum import StrEnum
from typing import Any

from dreamcraft_v2.data.database import DatabaseManager


class EventType(StrEnum):
    """Supported event categories."""

    COMBAT = "combat"
    TRADE = "trade"
    SOCIAL = "social"
    POLITICAL = "political"
    DISCOVERY = "discovery"
    BOSS = "boss"
    DEATH = "death"
    MOVEMENT = "movement"
    NEEDS = "needs"


class EventManager:
    """Create, flag, and fetch events."""

    def __init__(self, database: DatabaseManager, major_event_threshold: int = 7) -> None:
        """Create an event manager."""
        self.database = database
        self.major_event_threshold = major_event_threshold

    async def create_event(
        self,
        day: int,
        event_type: EventType,
        severity: int,
        location: int | None,
        participants: list[int] | None,
        outcome: dict[str, Any] | None,
        description: str,
        tick: int | None = None,
    ) -> None:
        """Insert an event and pre-compute its major flag."""
        is_major = int(severity >= self.major_event_threshold)
        await self.database.execute(
            """
            INSERT INTO events(
                day, tick, type, severity, is_major, location_id, description, participants, outcome
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                day,
                tick,
                event_type.value,
                severity,
                is_major,
                location,
                description,
                json.dumps(participants or []),
                json.dumps(outcome or {}),
            ),
        )

    async def flag_major_events(self, day: int) -> None:
        """Set major flag for all events at or above threshold on a day."""
        await self.database.execute(
            """
            UPDATE events
            SET is_major = CASE WHEN severity >= ? THEN 1 ELSE 0 END
            WHERE day = ?
            """,
            (self.major_event_threshold, day),
        )

    async def get_events_for_dashboard(self, since_day: int) -> list[dict[str, Any]]:
        """Return recent events for dashboard use."""
        return await self.database.get_recent_events(since_day)

    async def get_major_events(self, day: int | None = None) -> list[dict[str, Any]]:
        """Return events flagged as major."""
        return await self.database.get_major_events(day)
