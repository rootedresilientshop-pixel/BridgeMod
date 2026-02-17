"""Tests for retired-character migration service."""

from __future__ import annotations

import json
from pathlib import Path

import pytest

from dreamcraft_v2.character_loader import CharacterLoader, RetiredCharacterInput
from dreamcraft_v2.data.database import DatabaseManager


@pytest.mark.asyncio
async def test_ability_and_trait_mapping_shapes(test_database: DatabaseManager) -> None:
    """5e stats and Ideal/Bond/Flaw should map into saga-oriented weights."""
    loader = CharacterLoader(test_database)
    mapped = loader._map_abilities_to_saga(
        {
            "strength": 18,
            "dexterity": 12,
            "constitution": 16,
            "intelligence": 10,
            "wisdom": 14,
            "charisma": 17,
        }
    )
    assert mapped["physical_labor_efficiency"] > mapped["arcane_efficiency"]
    assert mapped["faction_relationship_gain"] >= 70

    traits = loader._parse_personality_traits(
        ideal="Protect the weak and uphold justice.",
        bond="My crew depends on me.",
        flaw="I can be reckless in battle.",
        backstory_markdown="Former harbor guard turned adventurer.",
    )
    assert traits["altruism"] > 50
    assert traits["loyalty"] > 50
    assert traits["aggression"] >= 50
    assert traits["caution"] <= 50


@pytest.mark.asyncio
async def test_loader_skips_duplicates_from_same_source_file(
    test_database: DatabaseManager,
    tmp_path: Path,
) -> None:
    """Loading the same source file twice should skip duplicate inserts."""
    source_file = tmp_path / "retired_party.json"
    source_file.write_text(
        json.dumps(
            [
                {
                    "name": "Captain Mara",
                    "race": "human",
                    "class_type": "fighter",
                    "level": 7,
                    "abilities": {
                        "strength": 16,
                        "dexterity": 13,
                        "constitution": 15,
                        "intelligence": 11,
                        "wisdom": 12,
                        "charisma": 14,
                    },
                    "ideal": "Duty before comfort",
                    "bond": "My sailors are family",
                    "flaw": "Pride",
                    "backstory": "A seasoned sailor who defended the coast for years.",
                    "accomplishments": ["Defeated the reef tyrant", "Saved three villages"],
                }
            ]
        ),
        encoding="utf-8",
    )

    loader = CharacterLoader(test_database)
    first = await loader.load_from_file(source_file)
    second = await loader.load_from_file(source_file)

    assert first["loaded"] == 1
    assert first["skipped_duplicates"] == 0
    assert second["loaded"] == 0
    assert second["skipped_duplicates"] == 1

    rows = await test_database.fetch_all("SELECT name FROM characters WHERE name = 'Captain Mara'")
    assert len(rows) == 1


def test_record_dataclass_construction() -> None:
    """RetiredCharacterInput should hold canonical import fields."""
    record = RetiredCharacterInput(
        name="Riven",
        race="elf",
        class_type="ranger",
        level=5,
        abilities={
            "strength": 12,
            "dexterity": 17,
            "constitution": 13,
            "intelligence": 10,
            "wisdom": 15,
            "charisma": 9,
        },
        ideal="Explore forgotten places",
        bond="My mentor's map",
        flaw="Too curious",
        backstory="Raised in deep forests.",
        accomplishments=["Mapped the shattered vale"],
    )
    assert record.name == "Riven"
    assert record.level == 5
