"""Service for importing retired 5e characters into DreamCraft: Legacies v2."""

from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from dreamcraft_v2.config import settings
from dreamcraft_v2.data.database import DatabaseManager


@dataclass(slots=True)
class RetiredCharacterInput:
    """Canonical source record for one imported retired character."""

    name: str
    race: str
    class_type: str
    level: int
    abilities: dict[str, int]
    ideal: str
    bond: str
    flaw: str
    backstory: str
    accomplishments: list[str]
    faction_hint: str | None = None


class CharacterLoader:
    """Load source character files and insert mapped records into saga.db."""

    def __init__(self, database: DatabaseManager) -> None:
        self.database = database

    async def load_from_file(self, source_file: str | Path) -> dict[str, Any]:
        """Parse, validate, map, and insert characters from one source file."""
        source_path = Path(source_file)
        if not source_path.exists():
            raise FileNotFoundError(f"Source file not found: {source_path}")

        raw = source_path.read_text(encoding="utf-8")
        source_hash = hashlib.sha256(raw.encode("utf-8")).hexdigest()
        records = self._parse_source_records(source_path, raw)
        self._validate_unique_names_in_source(records, source_path)

        loaded = 0
        skipped = 0
        imported_names: list[str] = []
        for record in records:
            if await self._already_loaded_from_source(record.name, source_hash):
                skipped += 1
                continue

            region_id = await self._resolve_region_id(record.backstory)
            location_id = await self._ensure_import_location(region_id)
            faction_id = await self._resolve_faction_id(record.faction_hint)

            mapped = self._map_abilities_to_saga(record.abilities)
            traits = self._parse_personality_traits(
                ideal=record.ideal,
                bond=record.bond,
                flaw=record.flaw,
                backstory_markdown=record.backstory,
            )
            personality_blob = {
                "level2_weights": traits,
                "derived_5e": mapped,
            }

            goals = self._build_goals(record)
            legacy_score = self._compute_starting_legacy_score(record.level, record.accomplishments)
            source_marker = f"source_file_hash:{source_hash}"
            source_path_marker = f"source_file_path:{source_path.as_posix()}"
            source_name_marker = f"source_character_name:{record.name}"

            accomplishments = list(record.accomplishments)
            accomplishments.extend([source_marker, source_path_marker, source_name_marker])

            await self.database.execute(
                """
                INSERT INTO characters(
                    name, race, class_type, personality, personality_traits,
                    goals, primary_goal, backstory,
                    health, hunger, energy, gold, level, experience,
                    strength, dexterity, constitution, intelligence, wisdom, charisma,
                    location_id, faction_id, status, legacy_score, accomplishments
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    record.name,
                    record.race,
                    record.class_type,
                    json.dumps(personality_blob),
                    json.dumps(traits),
                    json.dumps(goals),
                    goals[0] if goals else "Establish a new legacy in the region.",
                    record.backstory,
                    self._health_from_constitution(record.abilities.get("constitution", 10)),
                    10,
                    90,
                    100 + (record.level * 5),
                    max(1, int(record.level)),
                    0,
                    record.abilities.get("strength", 10),
                    record.abilities.get("dexterity", 10),
                    record.abilities.get("constitution", 10),
                    record.abilities.get("intelligence", 10),
                    record.abilities.get("wisdom", 10),
                    record.abilities.get("charisma", 10),
                    location_id,
                    faction_id,
                    "alive",
                    legacy_score,
                    json.dumps(accomplishments),
                ),
            )
            imported_names.append(record.name)
            loaded += 1

        return {
            "source_file": str(source_path),
            "source_hash": source_hash,
            "records_read": len(records),
            "loaded": loaded,
            "skipped_duplicates": skipped,
            "imported_names": imported_names,
        }

    def _parse_source_records(self, source_path: Path, raw: str) -> list[RetiredCharacterInput]:
        """Parse source files in JSON or markdown forms into canonical records."""
        suffix = source_path.suffix.lower()
        if suffix == ".json":
            return self._parse_json_records(raw)
        if suffix in {".md", ".markdown"}:
            return self._parse_markdown_records(raw)
        raise ValueError(f"Unsupported source file format: {suffix}")

    def _parse_json_records(self, raw: str) -> list[RetiredCharacterInput]:
        """Parse JSON source format."""
        parsed = json.loads(raw)
        if isinstance(parsed, dict):
            entries = parsed.get("characters", [parsed])
        elif isinstance(parsed, list):
            entries = parsed
        else:
            raise ValueError("Invalid JSON source: expected object or list.")

        records: list[RetiredCharacterInput] = []
        for entry in entries:
            if not isinstance(entry, dict):
                continue
            abilities = self._normalize_abilities(entry)
            records.append(
                RetiredCharacterInput(
                    name=self._pick(entry, "name", "character_name"),
                    race=self._pick(entry, "race", default="human"),
                    class_type=self._pick(entry, "class_type", "class", default="adventurer"),
                    level=int(self._pick(entry, "level", default=1)),
                    abilities=abilities,
                    ideal=self._pick(entry, "ideal", default=""),
                    bond=self._pick(entry, "bond", default=""),
                    flaw=self._pick(entry, "flaw", default=""),
                    backstory=self._pick(entry, "backstory", "bio", default=""),
                    accomplishments=self._normalize_accomplishments(entry.get("accomplishments")),
                    faction_hint=self._pick(entry, "faction_hint", "faction", default=None),
                )
            )
        return records

    def _parse_markdown_records(self, raw: str) -> list[RetiredCharacterInput]:
        """Parse markdown character sheets separated by --- lines."""
        blocks = [block.strip() for block in re.split(r"\n-{3,}\n", raw) if block.strip()]
        records: list[RetiredCharacterInput] = []
        for block in blocks:
            field_map: dict[str, str] = {}
            for line in block.splitlines():
                match = re.match(r"^\s*([A-Za-z_ ]+)\s*:\s*(.+?)\s*$", line)
                if not match:
                    continue
                key = match.group(1).strip().lower().replace(" ", "_")
                value = match.group(2).strip()
                field_map[key] = value

            if "name" not in field_map:
                continue

            abilities = {
                "strength": _int_or_default(field_map.get("strength"), 10),
                "dexterity": _int_or_default(field_map.get("dexterity"), 10),
                "constitution": _int_or_default(field_map.get("constitution"), 10),
                "intelligence": _int_or_default(field_map.get("intelligence"), 10),
                "wisdom": _int_or_default(field_map.get("wisdom"), 10),
                "charisma": _int_or_default(field_map.get("charisma"), 10),
            }
            accomplishments = re.findall(r"^\s*-\s+(.+)$", block, flags=re.MULTILINE)

            records.append(
                RetiredCharacterInput(
                    name=field_map["name"],
                    race=field_map.get("race", "human"),
                    class_type=field_map.get("class", field_map.get("class_type", "adventurer")),
                    level=_int_or_default(field_map.get("level"), 1),
                    abilities=abilities,
                    ideal=field_map.get("ideal", ""),
                    bond=field_map.get("bond", ""),
                    flaw=field_map.get("flaw", ""),
                    backstory=field_map.get("backstory", block),
                    accomplishments=accomplishments,
                    faction_hint=field_map.get("faction"),
                )
            )
        return records

    async def _already_loaded_from_source(self, name: str, source_hash: str) -> bool:
        """Ensure duplicates from the same source file are not inserted twice."""
        marker = f"source_file_hash:{source_hash}"
        row = await self.database.fetch_one(
            """
            SELECT id
            FROM characters
            WHERE name = ? AND accomplishments LIKE ?
            LIMIT 1
            """,
            (name, f"%{marker}%"),
        )
        return row is not None

    async def _resolve_region_id(self, backstory: str) -> int:
        """Assign a starting region based on backstory context keywords."""
        text = backstory.lower()
        region_name = "Frontier Heartlands"
        biome = "plains"
        description = "Core trade routes and neutral settlements."

        keyword_map = [
            ({"coast", "sea", "ship", "sailor", "harbor", "port"}, "Azure Coast", "coastal"),
            ({"mountain", "peak", "mine", "stone", "dwarf", "cliff"}, "Ironpeak Range", "mountain"),
            ({"forest", "grove", "wild", "wood", "druid"}, "Verdant Wilds", "forest"),
            ({"desert", "dune", "oasis", "sun-scorched"}, "Red Wastes", "desert"),
            ({"swamp", "marsh", "bog", "fen"}, "Mirelands", "swamp"),
        ]
        for keywords, candidate_name, candidate_biome in keyword_map:
            if any(word in text for word in keywords):
                region_name = candidate_name
                biome = candidate_biome
                description = f"Auto-assigned from source backstory ({candidate_biome})."
                break

        existing = await self.database.fetch_one(
            "SELECT id FROM regions WHERE name = ? LIMIT 1",
            (region_name,),
        )
        if existing:
            return int(existing["id"])

        await self.database.execute(
            """
            INSERT INTO regions(name, biome, danger_level, prosperity, description)
            VALUES (?, ?, ?, ?, ?)
            """,
            (region_name, biome, 3, 60, description),
        )
        created = await self.database.fetch_one(
            "SELECT id FROM regions WHERE name = ? LIMIT 1",
            (region_name,),
        )
        if not created:
            raise RuntimeError("Failed to create or resolve region.")
        return int(created["id"])

    async def _ensure_import_location(self, region_id: int) -> int:
        """Ensure a stable import waypoint location exists for the region."""
        region = await self.database.fetch_one("SELECT name FROM regions WHERE id = ?", (region_id,))
        region_name = (region or {}).get("name", f"Region {region_id}")
        location_name = f"Legacy Gate - {region_name}"
        existing = await self.database.fetch_one(
            "SELECT id FROM locations WHERE name = ? LIMIT 1",
            (location_name,),
        )
        if existing:
            return int(existing["id"])

        await self.database.execute(
            """
            INSERT INTO locations(region_id, name, type, description, x, y, danger_level, capacity, resources)
            VALUES (?, ?, 'town', ?, 0, 0, 2, 120, ?)
            """,
            (
                region_id,
                location_name,
                "Auto-created staging location for imported retired characters.",
                json.dumps({"food": 80, "wood": 40, "ore": 20}),
            ),
        )
        created = await self.database.fetch_one(
            "SELECT id FROM locations WHERE name = ? LIMIT 1",
            (location_name,),
        )
        if not created:
            raise RuntimeError("Failed to create import location.")
        return int(created["id"])

    async def _resolve_faction_id(self, faction_hint: str | None) -> int | None:
        """Resolve optional faction hint to faction_id."""
        if not faction_hint:
            return None
        row = await self.database.fetch_one(
            "SELECT id FROM factions WHERE LOWER(name) = LOWER(?) LIMIT 1",
            (faction_hint.strip(),),
        )
        if row:
            return int(row["id"])
        return None

    def _map_abilities_to_saga(self, abilities: dict[str, int]) -> dict[str, int]:
        """Map 5e abilities into saga-relevant efficiency values (0-100)."""
        strength = abilities.get("strength", 10)
        dexterity = abilities.get("dexterity", 10)
        constitution = abilities.get("constitution", 10)
        intelligence = abilities.get("intelligence", 10)
        wisdom = abilities.get("wisdom", 10)
        charisma = abilities.get("charisma", 10)

        def scale(value: float) -> int:
            return int(max(0, min(100, round((value / 20) * 100))))

        return {
            "physical_labor_efficiency": scale((strength * 0.65) + (constitution * 0.35)),
            "combat_efficiency": scale((strength * 0.40) + (dexterity * 0.35) + (constitution * 0.25)),
            "scouting_efficiency": scale((dexterity * 0.55) + (wisdom * 0.45)),
            "arcane_efficiency": scale((intelligence * 0.70) + (wisdom * 0.30)),
            "faction_relationship_gain": scale((charisma * 0.60) + (wisdom * 0.40)),
            "economic_negotiation": scale((charisma * 0.55) + (intelligence * 0.45)),
        }

    def _parse_personality_traits(
        self,
        *,
        ideal: str,
        bond: str,
        flaw: str,
        backstory_markdown: str,
    ) -> dict[str, int]:
        """Translate Ideal/Bond/Flaw/backstory text into Level-2 trait weights."""
        traits = {
            "altruism": 50,
            "ambition": 50,
            "discipline": 50,
            "curiosity": 50,
            "loyalty": 50,
            "caution": 50,
            "aggression": 50,
            "honor": 50,
            "pragmatism": 50,
            "compassion": 50,
        }
        corpus = " ".join([ideal, bond, flaw, backstory_markdown]).lower()

        signals: dict[str, tuple[set[str], int]] = {
            "altruism": ({"protect", "serve", "aid", "charity", "selfless"}, 12),
            "ambition": ({"glory", "power", "ascend", "fame", "prove", "legacy"}, 10),
            "discipline": ({"oath", "duty", "order", "code", "regiment"}, 10),
            "curiosity": ({"learn", "discover", "secret", "mystery", "research"}, 9),
            "loyalty": ({"family", "tribe", "crew", "brotherhood", "bond"}, 11),
            "caution": ({"careful", "patient", "stealth", "avoid", "survive"}, 9),
            "aggression": ({"revenge", "wrath", "slay", "conquer", "dominate"}, 12),
            "honor": ({"honor", "truth", "justice", "noble", "integrity"}, 11),
            "pragmatism": ({"practical", "necessary", "cost", "efficient", "realist"}, 8),
            "compassion": ({"mercy", "kind", "forgive", "heal", "empathy"}, 10),
        }
        for trait, (keywords, delta) in signals.items():
            if any(k in corpus for k in keywords):
                traits[trait] += delta

        flaw_text = flaw.lower()
        if "greed" in flaw_text or "selfish" in flaw_text:
            traits["altruism"] -= 12
            traits["pragmatism"] += 6
        if "coward" in flaw_text or "fear" in flaw_text:
            traits["caution"] += 10
            traits["aggression"] -= 8
        if "pride" in flaw_text or "arrog" in flaw_text:
            traits["ambition"] += 8
            traits["compassion"] -= 6
        if "reckless" in flaw_text:
            traits["aggression"] += 9
            traits["caution"] -= 10

        return {key: int(max(0, min(100, value))) for key, value in traits.items()}

    def _compute_starting_legacy_score(self, level: int, accomplishments: list[str]) -> int:
        """Map prior level and accomplishments to initial legacy score."""
        base = max(0, level) * 8
        accomplishment_bonus = len(accomplishments) * 5
        major_bonus = 0
        for item in accomplishments:
            text = item.lower()
            if any(token in text for token in ("saved", "defeated", "slain", "champion", "legendary")):
                major_bonus += 6
            if any(token in text for token in ("founded", "rebuilt", "restored", "brokered peace")):
                major_bonus += 8
        return max(0, min(1000, base + accomplishment_bonus + major_bonus))

    def _build_goals(self, record: RetiredCharacterInput) -> list[str]:
        """Generate initial simulation goals from source personality fields."""
        goals = []
        if record.ideal:
            goals.append(f"Live by ideal: {record.ideal}")
        if record.bond:
            goals.append(f"Honor bond: {record.bond}")
        if record.flaw:
            goals.append(f"Manage flaw: {record.flaw}")
        if not goals:
            goals.append("Establish a new chapter in Legacies.")
        return goals[:5]

    def _normalize_abilities(self, entry: dict[str, Any]) -> dict[str, int]:
        """Normalize common 5e ability key variants into one dictionary."""
        ability_source = entry.get("abilities")
        if isinstance(ability_source, dict):
            raw = ability_source
        else:
            raw = entry
        return {
            "strength": _int_or_default(self._pick(raw, "strength", "str", default=10), 10),
            "dexterity": _int_or_default(self._pick(raw, "dexterity", "dex", default=10), 10),
            "constitution": _int_or_default(self._pick(raw, "constitution", "con", default=10), 10),
            "intelligence": _int_or_default(self._pick(raw, "intelligence", "int", default=10), 10),
            "wisdom": _int_or_default(self._pick(raw, "wisdom", "wis", default=10), 10),
            "charisma": _int_or_default(self._pick(raw, "charisma", "cha", default=10), 10),
        }

    def _normalize_accomplishments(self, value: Any) -> list[str]:
        """Normalize accomplishments payload into list[str]."""
        if value is None:
            return []
        if isinstance(value, list):
            return [str(item) for item in value]
        if isinstance(value, str):
            stripped = value.strip()
            if not stripped:
                return []
            if stripped.startswith("["):
                try:
                    parsed = json.loads(stripped)
                    if isinstance(parsed, list):
                        return [str(item) for item in parsed]
                except json.JSONDecodeError:
                    pass
            return [item.strip() for item in stripped.split(";") if item.strip()]
        return [str(value)]

    def _validate_unique_names_in_source(
        self,
        records: list[RetiredCharacterInput],
        source_path: Path,
    ) -> None:
        """Fail fast when source contains duplicate character names."""
        seen: set[str] = set()
        for record in records:
            key = record.name.strip().lower()
            if key in seen:
                raise ValueError(
                    f"Duplicate character '{record.name}' in source file {source_path}."
                )
            seen.add(key)

    def _pick(self, source: dict[str, Any], *keys: str, default: Any = "") -> Any:
        for key in keys:
            if key in source and source[key] is not None:
                return source[key]
        return default

    def _health_from_constitution(self, constitution: int) -> int:
        """Set starting health from constitution for imported characters."""
        return max(60, min(140, 80 + ((constitution - 10) * 5)))


def _int_or_default(value: Any, default: int) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Import retired 5e characters into Legacies v2.")
    parser.add_argument(
        "--source",
        required=True,
        help="Path to source character file (.json or .md).",
    )
    parser.add_argument(
        "--db-path",
        default=None,
        help="Optional override for SQLite DB path (defaults to SAGA_DB_PATH).",
    )
    return parser.parse_args()


async def main_async() -> None:
    args = parse_args()
    database = DatabaseManager(args.db_path or settings.db_path)
    await database.initialize_db()
    loader = CharacterLoader(database)
    result = await loader.load_from_file(args.source)
    print(json.dumps(result, indent=2))


def main() -> None:
    asyncio.run(main_async())


if __name__ == "__main__":
    main()
