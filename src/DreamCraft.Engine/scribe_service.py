"""Narrative and card snapshot generation for DreamCraft: Legacies v2."""

from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.llm.client import LLMClient


class ScribeService:
    """Generate daily markdown journals and dashboard card snapshots."""

    def __init__(
        self,
        database: DatabaseManager,
        llm_client: LLMClient,
        *,
        output_root: str | Path = "artifacts",
    ) -> None:
        self.database = database
        self.llm_client = llm_client
        self.output_root = Path(output_root)

    async def generate_daily_journals(self, day_number: int) -> dict[str, Any]:
        """Write one markdown journal entry per active character."""
        journals_dir = self.output_root / "journals" / f"day_{day_number}"
        journals_dir.mkdir(parents=True, exist_ok=True)

        characters = await self.database.fetch_all(
            """
            SELECT c.*, l.name AS location_name, r.name AS region_name, f.name AS faction_name
            FROM characters c
            LEFT JOIN locations l ON l.id = c.location_id
            LEFT JOIN regions r ON r.id = l.region_id
            LEFT JOIN factions f ON f.id = c.faction_id
            WHERE c.status IN ('alive', 'injured', 'unconscious')
            ORDER BY c.id
            """
        )
        events = await self.database.get_events_for_day(day_number)
        llm_enabled = await self.llm_client.health_check()

        paths: list[str] = []
        for character in characters:
            char_id = int(character["id"])
            char_events = _events_for_character(events, char_id)
            markdown = await self._build_journal_markdown(
                day_number=day_number,
                character=character,
                events=char_events,
                llm_enabled=llm_enabled,
            )
            filename = f"{char_id:04d}_{_slugify(str(character['name']))}.md"
            out_path = journals_dir / filename
            out_path.write_text(markdown, encoding="utf-8")
            paths.append(str(out_path))

        return {"journals_dir": str(journals_dir), "journal_paths": paths}

    async def generate_character_snapshots(self, day_number: int) -> dict[str, Any]:
        """Write JSON snapshots for active character cards."""
        base_dir = self.output_root / "snapshots" / f"day_{day_number}"
        cards_dir = base_dir / "cards"
        cards_dir.mkdir(parents=True, exist_ok=True)

        characters = await self.database.fetch_all(
            """
            SELECT c.*, l.name AS location_name, r.name AS region_name, f.name AS faction_name
            FROM characters c
            LEFT JOIN locations l ON l.id = c.location_id
            LEFT JOIN regions r ON r.id = l.region_id
            LEFT JOIN factions f ON f.id = c.faction_id
            WHERE c.status IN ('alive', 'injured', 'unconscious')
            ORDER BY c.id
            """
        )

        cards: list[dict[str, Any]] = []
        per_card_paths: list[str] = []
        for row in characters:
            card = await self._character_card_payload(row)
            cards.append(card)
            card_path = cards_dir / f"{int(row['id']):04d}_{_slugify(str(row['name']))}.json"
            card_path.write_text(json.dumps(card, indent=2), encoding="utf-8")
            per_card_paths.append(str(card_path))

        source_of_truth = base_dir / "character_cards.json"
        source_of_truth.write_text(json.dumps(cards, indent=2), encoding="utf-8")
        manifest = base_dir / "manifest.json"
        manifest.write_text(
            json.dumps(
                {
                    "day": day_number,
                    "cards_count": len(cards),
                    "cards_file": str(source_of_truth),
                    "card_files": per_card_paths,
                },
                indent=2,
            ),
            encoding="utf-8",
        )

        return {
            "snapshots_dir": str(base_dir),
            "cards_path": str(source_of_truth),
            "card_paths": per_card_paths,
        }

    async def _build_journal_markdown(
        self,
        *,
        day_number: int,
        character: dict[str, Any],
        events: list[dict[str, Any]],
        llm_enabled: bool,
    ) -> str:
        event_lines = [f"- {event.get('description', 'An event occurred.')}" for event in events[:8]]
        raw_event_text = "\n".join(event_lines) if event_lines else "- Quiet day with minimal incidents."
        legacy_entries = await self.database.fetch_all(
            """
            SELECT legacy_score, retirement_reason, notable_deeds, archived_at
            FROM legacy_ledger
            WHERE character_id = ? OR character_name = ?
            ORDER BY archived_at DESC
            LIMIT 3
            """,
            (int(character["id"]), str(character["name"])),
        )
        legacy_lines = [
            f"- Legacy {int(item.get('legacy_score', 0))}: {item.get('retirement_reason', 'no note')}"
            for item in legacy_entries
        ]
        legacy_text = "\n".join(legacy_lines) if legacy_lines else "- No prior legacy ledger entries."

        if llm_enabled:
            prompt = (
                f"Write a concise daily journal entry in markdown for Day {day_number}.\n"
                f"Character: {character['name']} ({character.get('race')} {character.get('class_type')})\n"
                f"Faction: {character.get('faction_name', 'Unaffiliated')}\n"
                f"Region: {character.get('region_name', 'Unknown')}\n"
                f"Location: {character.get('location_name', 'Unknown')}\n"
                "Raw Events:\n"
                f"{raw_event_text}\n"
                "Recent Legacy Context:\n"
                f"{legacy_text}\n"
                "Return markdown with sections: Summary, Key Actions, Legacy Thread."
            )
            response = await self.llm_client.make_completion(
                prompt=prompt,
                system_prompt=(
                    "You are the world scribe for a persistent fantasy simulation. "
                    "Convert raw simulation telemetry into clean, lore-friendly markdown."
                ),
                max_tokens=420,
            )
            body = response.strip() if response else self._fallback_journal_body(raw_event_text, legacy_text)
        else:
            body = self._fallback_journal_body(raw_event_text, legacy_text)

        header = [
            f"# Daily Journal: {character['name']} (Day {day_number})",
            "",
            f"- Region: {character.get('region_name', 'Unknown')}",
            f"- Location: {character.get('location_name', 'Unknown')}",
            f"- Faction: {character.get('faction_name', 'Unaffiliated')}",
            "",
        ]
        return "\n".join(header) + body + "\n"

    async def _character_card_payload(self, row: dict[str, Any]) -> dict[str, Any]:
        legacy_rows = await self.database.fetch_all(
            """
            SELECT character_name, legacy_score, retirement_reason, notable_deeds, archived_at
            FROM legacy_ledger
            WHERE character_id = ? OR character_name = ?
            ORDER BY archived_at DESC
            LIMIT 3
            """,
            (int(row["id"]), str(row["name"])),
        )
        achievements = [
            {
                "legacy_score": int(item.get("legacy_score", 0)),
                "retirement_reason": item.get("retirement_reason"),
                "notable_deeds": item.get("notable_deeds"),
                "archived_at": item.get("archived_at"),
            }
            for item in legacy_rows
        ]
        return {
            "character_id": int(row["id"]),
            "name": row["name"],
            "race": row.get("race"),
            "class_type": row.get("class_type"),
            "status": row.get("status"),
            "stats": {
                "health": int(row.get("health", 100)),
                "hunger": int(row.get("hunger", 0)),
                "energy": int(row.get("energy", 100)),
                "gold": int(row.get("gold", 0)),
                "level": int(row.get("level", 1)),
                "experience": int(row.get("experience", 0)),
                "legacy_score": int(row.get("legacy_score", 0)),
            },
            "location": {
                "location_id": row.get("location_id"),
                "location_name": row.get("location_name"),
                "region_name": row.get("region_name"),
            },
            "faction": {
                "faction_id": row.get("faction_id"),
                "faction_name": row.get("faction_name"),
            },
            "recent_legacy_achievements": achievements,
        }

    def _fallback_journal_body(self, event_text: str, legacy_text: str) -> str:
        return (
            "## Summary\n"
            "A steady pulse of decisions shaped the day.\n\n"
            "## Key Actions\n"
            f"{event_text}\n\n"
            "## Legacy Thread\n"
            f"{legacy_text}\n"
        )


def _events_for_character(events: list[dict[str, Any]], character_id: int) -> list[dict[str, Any]]:
    """Filter event rows where participants include the given character."""
    relevant: list[dict[str, Any]] = []
    for event in events:
        participants = event.get("participants", "[]")
        if isinstance(participants, str):
            try:
                participants_data = json.loads(participants)
            except json.JSONDecodeError:
                participants_data = []
        elif isinstance(participants, list):
            participants_data = participants
        else:
            participants_data = []
        normalized = {int(item) for item in participants_data if str(item).isdigit()}
        if character_id in normalized:
            relevant.append(event)
    return relevant


def _slugify(value: str) -> str:
    text = re.sub(r"[^a-zA-Z0-9]+", "-", value.strip().lower())
    return text.strip("-") or "character"
