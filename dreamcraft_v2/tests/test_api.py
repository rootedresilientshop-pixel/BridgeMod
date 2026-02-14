"""API endpoint tests."""

from __future__ import annotations

import json
from pathlib import Path

import httpx
import pytest

from dreamcraft_v2.api.main import create_app
from dreamcraft_v2.data.database import DatabaseManager


@pytest.mark.asyncio
async def test_api_endpoints(tmp_path: Path) -> None:
    """Verify core API endpoints return expected payloads."""
    db_path = tmp_path / "api_test.db"
    database = DatabaseManager(str(db_path))
    await database.initialize_db()

    await database.execute(
        """
        INSERT INTO world_state(day, season, weather, time_of_day, global_events, resource_levels, population_alive, population_dead)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """,
        (1, "spring", "clear", "morning", json.dumps({}), json.dumps({}), 1, 0),
    )
    await database.execute(
        """
        INSERT INTO locations(name, type, description, x, y, danger_level, capacity, resources)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """,
        ("API Town", "town", "Test location", 1, 1, 1, 50, json.dumps({"food": 20})),
    )
    location = await database.fetch_one("SELECT id FROM locations WHERE name = ?", ("API Town",))
    assert location is not None

    await database.execute(
        """
        INSERT INTO characters(name, race, class_type, personality, goals, location_id, status)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        """,
        (
            "API Hero",
            "human",
            "warrior",
            json.dumps({"courage": 8}),
            json.dumps(["protect town"]),
            int(location["id"]),
            "alive",
        ),
    )

    app = create_app(str(db_path))
    transport = httpx.ASGITransport(app=app)
    async with httpx.AsyncClient(transport=transport, base_url="http://testserver") as client:
        health = await client.get("/api/health")
        world = await client.get("/api/world/state")
        characters = await client.get("/api/characters")
        locations = await client.get("/api/locations")

    assert health.status_code == 200
    assert world.status_code == 200
    assert characters.status_code == 200
    assert locations.status_code == 200
    assert len(characters.json()) == 1
