"""Seed script for DreamCraft v2 initial world data."""

from __future__ import annotations

import asyncio
import json
from itertools import cycle
from typing import Any

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.simulation.config import settings


async def seed_world(database: DatabaseManager) -> None:
    """Populate starter locations, factions, and characters if empty."""
    existing = await database.fetch_one("SELECT COUNT(*) AS count FROM characters")
    if existing and int(existing["count"]) > 0:
        return

    await _seed_locations(database)
    await _seed_factions(database)
    await _seed_characters(database)


async def _seed_locations(database: DatabaseManager) -> None:
    """Insert starter locations."""
    locations: list[dict[str, Any]] = [
        {"name": "Aurelian Keep", "type": "town", "danger": 2, "x": 10, "y": 12},
        {"name": "Whisperwood", "type": "wilderness", "danger": 5, "x": 18, "y": 25},
        {"name": "Moonwell Tavern", "type": "tavern", "danger": 1, "x": 11, "y": 14},
        {"name": "Ironroot Market", "type": "market", "danger": 2, "x": 13, "y": 11},
        {"name": "Sunspire Monastery", "type": "town", "danger": 3, "x": 7, "y": 9},
        {"name": "Blackglass Depths", "type": "dungeon", "danger": 9, "x": 28, "y": 33},
        {"name": "Mossgate Crossing", "type": "town", "danger": 3, "x": 15, "y": 20},
        {"name": "Ravencliff Pass", "type": "wilderness", "danger": 7, "x": 22, "y": 18},
        {"name": "Silvermere Docks", "type": "town", "danger": 2, "x": 4, "y": 22},
        {"name": "Emberfall Ruins", "type": "dungeon", "danger": 8, "x": 31, "y": 28},
        {"name": "Verdant Hollow", "type": "wilderness", "danger": 4, "x": 20, "y": 30},
        {"name": "Stormwatch Bastion", "type": "town", "danger": 5, "x": 26, "y": 8},
    ]
    for location in locations:
        await database.execute(
            """
            INSERT INTO locations(name, type, description, x, y, danger_level, capacity, resources)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                location["name"],
                location["type"],
                f"{location['name']} is a key destination in the realm.",
                location["x"],
                location["y"],
                location["danger"],
                75,
                json.dumps({"food": 60, "ore": 30, "herbs": 40}),
            ),
        )


async def _seed_factions(database: DatabaseManager) -> None:
    """Insert starter factions."""
    faction_rows = [
        ("The Dawn Accord", "good", 65),
        ("Iron Sigil Consortium", "neutral", 58),
        ("Nightweave Cabal", "chaotic", 72),
        ("Wardens of the Grove", "good", 61),
        ("Crimson Banner", "evil", 69),
        ("Free Cartographer Guild", "neutral", 53),
    ]
    names = [item[0] for item in faction_rows]
    for name, alignment, power in faction_rows:
        relations = {other: (10 if other == name else 0) for other in names}
        await database.execute(
            """
            INSERT INTO factions(name, description, alignment, power, gold, territory, relations)
            VALUES (?, ?, ?, ?, ?, ?, ?)
            """,
            (
                name,
                f"{name} influences the political landscape.",
                alignment,
                power,
                1500,
                json.dumps([]),
                json.dumps(relations),
            ),
        )


async def _seed_characters(database: DatabaseManager) -> None:
    """Insert starter characters with varied personalities and goals."""
    names = [
        "Arin Vale",
        "Mira Suncrest",
        "Thorne Blackbrook",
        "Lysa Emberglass",
        "Kael Thornwind",
        "Nora Duskwell",
        "Perrin Oakmantle",
        "Sable Vire",
        "Orrin Flintmark",
        "Talia Moonward",
        "Jarek Stormborn",
        "Iria Dawnleaf",
        "Corin Ashfell",
        "Vexa Hollow",
        "Bram Cindergale",
        "Elowen Reed",
        "Dain Ironhollow",
        "Selene Rook",
        "Garrick Valeheart",
        "Nyx Farsong",
    ]
    races = cycle(["human", "elf", "dwarf", "halfling", "orc", "tiefling"])
    classes = cycle(["warrior", "mage", "ranger", "rogue", "cleric", "bard"])

    locations = await database.fetch_all("SELECT id FROM locations ORDER BY id")
    factions = await database.fetch_all("SELECT id FROM factions ORDER BY id")
    location_ids = [int(row["id"]) for row in locations]
    faction_ids = [int(row["id"]) for row in factions]

    for index, name in enumerate(names, start=1):
        personality = {
            "courage": (index * 7) % 10 + 1,
            "curiosity": (index * 5) % 10 + 1,
            "discipline": (index * 3) % 10 + 1,
            "empathy": (index * 9) % 10 + 1,
        }
        goals = [
            f"Advance standing in faction {faction_ids[(index - 1) % len(faction_ids)]}",
            "Build trusted alliances",
            "Secure rare resources",
        ]
        await database.execute(
            """
            INSERT INTO characters(
                name, race, class_type, personality, goals, backstory, health, hunger, energy,
                gold, level, experience, location_id, faction_id, status
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                name,
                next(races),
                next(classes),
                json.dumps(personality),
                json.dumps(goals),
                f"{name} emerged from frontier struggles and seeks legacy.",
                100,
                index % 20,
                100 - (index % 15),
                100 + index * 3,
                1,
                0,
                location_ids[(index - 1) % len(location_ids)],
                faction_ids[(index - 1) % len(faction_ids)],
                "alive",
            ),
        )


async def main() -> None:
    """Run seed operation from CLI."""
    database = DatabaseManager(settings.database_path)
    await database.initialize_db()
    await seed_world(database)


if __name__ == "__main__":
    asyncio.run(main())
