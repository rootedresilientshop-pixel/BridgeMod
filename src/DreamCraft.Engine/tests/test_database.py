"""Database schema and CRUD tests."""

from __future__ import annotations

import json

import pytest

from dreamcraft_v2.data.database import DatabaseManager


@pytest.mark.asyncio
async def test_schema_initialization(test_database: DatabaseManager) -> None:
    """Ensure expected core tables exist."""
    rows = await test_database.fetch_all(
        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
    )
    table_names = {row["name"] for row in rows}
    assert "characters" in table_names
    assert "events" in table_names
    assert "world_state" in table_names
    assert "schema_migrations" in table_names


@pytest.mark.asyncio
async def test_basic_character_crud(test_database: DatabaseManager) -> None:
    """Insert and retrieve a character."""
    await test_database.execute(
        """
        INSERT INTO locations(name, type, description, x, y, danger_level, capacity, resources)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """,
        ("Test Town", "town", "A safe test location", 0, 0, 1, 10, json.dumps({"food": 10})),
    )
    location = await test_database.fetch_one("SELECT id FROM locations WHERE name = ?", ("Test Town",))
    assert location is not None

    await test_database.execute(
        """
        INSERT INTO characters(name, race, class_type, personality, goals, location_id, status)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        """,
        (
            "Test Character",
            "human",
            "mage",
            json.dumps({"curiosity": 9}),
            json.dumps(["learn arcana"]),
            int(location["id"]),
            "alive",
        ),
    )
    character = await test_database.get_character_by_name("Test Character")
    assert character is not None
    assert character["name"] == "Test Character"

    at_location = await test_database.get_characters_at_location(int(location["id"]))
    assert len(at_location) == 1
