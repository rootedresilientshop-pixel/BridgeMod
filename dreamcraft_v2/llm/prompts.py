"""Prompt templates for LLM-driven simulation decisions."""


def movement_batch_prompt(
    location_name: str,
    location_type: str,
    characters: list[dict],
    nearby_locations: list[dict],
) -> str:
    """Prompt for location-batched movement decisions."""
    char_list = "\n".join([
        f"- {c['name']} ({c['race']} {c['class_type']}): "
        f"Health={c['health']}, Hunger={c['hunger']}, Energy={c['energy']}, "
        f"Goals: {', '.join(c.get('goals', [])[:2])}"
        for c in characters
    ])

    loc_list = "\n".join([
        f"- {l['name']} ({l['type']}, danger={l['danger_level']})"
        for l in nearby_locations
    ])

    return f"""You are simulating fantasy characters' movement decisions at {location_name} ({location_type}).

Characters present:
{char_list}

Available nearby locations:
{loc_list}

For each character, decide where they go based on their personality, needs, and goals.
Respond ONLY with JSON (no other text):
{{
  "decisions": [
    {{"character_name": "Name", "destination": "location_name", "reason": "brief reason"}}
  ]
}}"""


def interaction_prompt(
    location_name: str,
    location_type: str,
    characters: list[dict],
    relationships: list[dict],
    recent_events: list[dict],
) -> str:
    """Prompt for location interaction outcomes."""
    char_list = "\n".join([
        f"- {c['name']} ({c['race']} {c['class_type']}): "
        f"Status={c['status']}"
        for c in characters
    ])

    rel_list = "\n".join([
        f"- {r['char_a']}<->{r['char_b']}: {r['type']} (strength={r['strength']})"
        for r in relationships
    ]) if relationships else "- None (first meetings)"

    event_list = "\n".join([f"- {e}" for e in recent_events[-3:]]) if recent_events else "- None"

    return f"""You are simulating interactions between fantasy characters at {location_name} ({location_type}).

Characters present:
{char_list}

Existing relationships:
{rel_list}

Recent events here:
{event_list}

Generate emergent narrative interactions based on personalities and relationships.
Include relationship changes where relevant.
Respond ONLY with JSON (no other text):
{{
  "events": [
    {{
      "type": "social|combat|trade|romance|betrayal|discovery|tension",
      "severity": 1-10,
      "participants": [character_ids],
      "description": "what happened",
      "relationship_changes": [
        {{"char_a": id, "char_b": id, "change": "improved|worsened|new_rivalry|new_friendship|romantic_spark"}}
      ]
    }}
  ]
}}"""


def faction_politics_prompt(
    faction_name: str,
    faction_alignment: str,
    faction_power: int,
    relations: dict[str, int],
    member_count: int,
    recent_events: list[str],
) -> str:
    """Prompt for faction political decisions."""
    rel_list = "\n".join([
        f"- {name}: {score:+d}"
        for name, score in relations.items()
    ])

    event_list = "\n".join([f"- {e}" for e in recent_events[-3:]]) if recent_events else "- None"

    return f"""You are simulating political decisions for faction "{faction_name}" ({faction_alignment}).

Faction status:
- Power: {faction_power}/100
- Members: {member_count}

Relations with other factions:
{rel_list}

Recent political events:
{event_list}

Decide their political action based on faction nature and strategic interests.
Respond ONLY with JSON (no other text):
{{
  "action": "description of action",
  "type": "expansion|diplomacy|aggression|defense|internal",
  "severity": 1-10,
  "target_faction": "name or null",
  "power_change": -5 to +5,
  "relation_changes": [{{"faction": "name", "change": -10 to +10}}]
}}"""


def relationship_evaluation_prompt(
    event_description: str,
    event_type: str,
    severity: int,
    char_a_name: str,
    char_a_personality: dict,
    char_b_name: str,
    char_b_personality: dict,
    current_relationship: dict | None,
) -> str:
    """Prompt for evaluating relationship impact from event."""
    current_rel = "None" if not current_relationship else (
        f"{current_relationship['type']} (strength={current_relationship['strength']})"
    )

    return f"""You are evaluating how an event affects a relationship.

Event: {event_description}
Type: {event_type}, Severity: {severity}/10

Character A: {char_a_name}
Character B: {char_b_name}
Current relationship: {current_rel}

Based on this event and their personalities, how does their relationship change?
Respond ONLY with JSON (no other text):
{{
  "new_type": "friend|enemy|rival|ally|romantic|mentor|neutral|acquaintance",
  "new_strength": 0-100,
  "reason": "why"
}}"""
