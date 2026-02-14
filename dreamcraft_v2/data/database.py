"""Async SQLite database manager for DreamCraft v2."""

from __future__ import annotations

import json
import logging
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Any, AsyncIterator

import aiosqlite

LOGGER = logging.getLogger(__name__)
SCHEMA_VERSION = 1


class DatabaseManager:
    """Manage database connections, schema initialization, and common queries."""

    def __init__(self, database_path: str | Path) -> None:
        """Create a new database manager."""
        self.database_path = str(database_path)
        self.schema_path = Path(__file__).resolve().parent / "schema.sql"

    @asynccontextmanager
    async def get_connection(self) -> AsyncIterator[aiosqlite.Connection]:
        """Yield a configured SQLite connection."""
        connection = await aiosqlite.connect(self.database_path)
        connection.row_factory = aiosqlite.Row
        await connection.execute("PRAGMA foreign_keys = ON")
        try:
            yield connection
        finally:
            await connection.close()

    async def initialize_db(self) -> None:
        """Initialize schema and apply migrations when needed."""
        schema_sql = self.schema_path.read_text(encoding="utf-8")
        async with self.get_connection() as connection:
            await connection.executescript(schema_sql)
            await connection.commit()
        await self.apply_migrations()

    async def get_schema_version(self) -> int:
        """Return the latest applied schema version."""
        async with self.get_connection() as connection:
            cursor = await connection.execute(
                "SELECT COALESCE(MAX(version), 0) AS version FROM schema_migrations"
            )
            row = await cursor.fetchone()
            return int(row["version"]) if row else 0

    async def apply_migrations(self) -> None:
        """Track schema version for future migrations."""
        current_version = await self.get_schema_version()
        if current_version >= SCHEMA_VERSION:
            return

        async with self.get_connection() as connection:
            for version in range(current_version + 1, SCHEMA_VERSION + 1):
                await connection.execute(
                    "INSERT OR IGNORE INTO schema_migrations(version) VALUES (?)",
                    (version,),
                )
                LOGGER.info("Applied schema version %s", version)
            await connection.commit()

    async def execute(
        self, query: str, params: tuple[Any, ...] | None = None
    ) -> None:
        """Execute a write query."""
        async with self.get_connection() as connection:
            await connection.execute(query, params or ())
            await connection.commit()

    async def fetch_one(
        self, query: str, params: tuple[Any, ...] | None = None
    ) -> dict[str, Any] | None:
        """Fetch one row and return it as a dictionary."""
        async with self.get_connection() as connection:
            cursor = await connection.execute(query, params or ())
            row = await cursor.fetchone()
            return dict(row) if row else None

    async def fetch_all(
        self, query: str, params: tuple[Any, ...] | None = None
    ) -> list[dict[str, Any]]:
        """Fetch multiple rows and return dictionaries."""
        async with self.get_connection() as connection:
            cursor = await connection.execute(query, params or ())
            rows = await cursor.fetchall()
            return [dict(row) for row in rows]

    async def insert_world_state(
        self,
        day: int,
        season: str,
        weather: str,
        time_of_day: str,
        global_events: dict[str, Any] | None,
        resource_levels: dict[str, Any] | None,
        population_alive: int,
        population_dead: int,
    ) -> None:
        """Insert or update world state for a specific day."""
        await self.execute(
            """
            INSERT INTO world_state (
                day, season, weather, time_of_day, global_events, resource_levels,
                population_alive, population_dead
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(day) DO UPDATE SET
                season = excluded.season,
                weather = excluded.weather,
                time_of_day = excluded.time_of_day,
                global_events = excluded.global_events,
                resource_levels = excluded.resource_levels,
                population_alive = excluded.population_alive,
                population_dead = excluded.population_dead
            """,
            (
                day,
                season,
                weather,
                time_of_day,
                json.dumps(global_events or {}),
                json.dumps(resource_levels or {}),
                population_alive,
                population_dead,
            ),
        )

    async def log_simulation(
        self, day: int, phase: str, message: str, duration_ms: int | None = None
    ) -> None:
        """Insert a simulation log message."""
        await self.execute(
            """
            INSERT INTO simulation_log(day, phase, message, duration_ms)
            VALUES (?, ?, ?, ?)
            """,
            (day, phase, message, duration_ms),
        )

    async def get_character(self, character_id: int) -> dict[str, Any] | None:
        """Return a single character by ID."""
        return await self.fetch_one(
            "SELECT * FROM characters WHERE id = ?", (character_id,)
        )

    async def get_character_by_name(self, name: str) -> dict[str, Any] | None:
        """Return a single character by name."""
        return await self.fetch_one("SELECT * FROM characters WHERE name = ?", (name,))

    async def get_characters(
        self, limit: int = 100, offset: int = 0
    ) -> list[dict[str, Any]]:
        """Return paginated characters."""
        return await self.fetch_all(
            "SELECT * FROM characters ORDER BY id LIMIT ? OFFSET ?", (limit, offset)
        )

    async def get_characters_at_location(self, location_id: int) -> list[dict[str, Any]]:
        """Return all characters at a location."""
        return await self.fetch_all(
            "SELECT * FROM characters WHERE location_id = ?", (location_id,)
        )

    async def get_alive_characters(self) -> list[dict[str, Any]]:
        """Return all alive characters."""
        return await self.fetch_all("SELECT * FROM characters WHERE status = 'alive'")

    async def update_character_state(
        self,
        character_id: int,
        *,
        health: int,
        hunger: int,
        energy: int,
        location_id: int | None,
        status: str,
    ) -> None:
        """Update mutable state for a character."""
        await self.execute(
            """
            UPDATE characters
            SET health = ?, hunger = ?, energy = ?, location_id = ?, status = ?,
                updated_at = CURRENT_TIMESTAMP
            WHERE id = ?
            """,
            (health, hunger, energy, location_id, status, character_id),
        )

    async def get_events_for_day(self, day: int) -> list[dict[str, Any]]:
        """Return all events for a day."""
        return await self.fetch_all(
            "SELECT * FROM events WHERE day = ? ORDER BY tick, id", (day,)
        )

    async def get_recent_events(self, since_day: int) -> list[dict[str, Any]]:
        """Return events at or after a day."""
        return await self.fetch_all(
            "SELECT * FROM events WHERE day >= ? ORDER BY day DESC, tick DESC, id DESC",
            (since_day,),
        )

    async def get_major_events(self, day: int | None = None) -> list[dict[str, Any]]:
        """Return major events, optionally filtered by day."""
        if day is None:
            return await self.fetch_all(
                "SELECT * FROM events WHERE is_major = 1 ORDER BY day DESC, tick DESC, id DESC"
            )
        return await self.fetch_all(
            """
            SELECT * FROM events
            WHERE is_major = 1 AND day = ?
            ORDER BY tick DESC, id DESC
            """,
            (day,),
        )

    async def get_world_state(self, day: int | None = None) -> dict[str, Any] | None:
        """Return world state for day, or latest state when day is None."""
        if day is not None:
            return await self.fetch_one("SELECT * FROM world_state WHERE day = ?", (day,))
        return await self.fetch_one("SELECT * FROM world_state ORDER BY day DESC LIMIT 1")

    async def get_factions(self) -> list[dict[str, Any]]:
        """Return all factions."""
        return await self.fetch_all("SELECT * FROM factions ORDER BY id")

    async def get_locations(self) -> list[dict[str, Any]]:
        """Return all locations."""
        return await self.fetch_all("SELECT * FROM locations ORDER BY id")

    async def get_locations_with_counts(self) -> list[dict[str, Any]]:
        """Return locations with current character counts."""
        return await self.fetch_all(
            """
            SELECT l.*, COUNT(c.id) AS character_count
            FROM locations l
            LEFT JOIN characters c ON c.location_id = l.id
            GROUP BY l.id
            ORDER BY l.id
            """
        )

    async def get_narratives_for_character(
        self, character_id: int, day: int | None = None
    ) -> list[dict[str, Any]]:
        """Return narratives for a character."""
        if day is None:
            return await self.fetch_all(
                """
                SELECT * FROM narratives
                WHERE character_id = ?
                ORDER BY day DESC, id DESC
                """,
                (character_id,),
            )
        return await self.fetch_all(
            """
            SELECT * FROM narratives
            WHERE character_id = ? AND day = ?
            ORDER BY id DESC
            """,
            (character_id, day),
        )
