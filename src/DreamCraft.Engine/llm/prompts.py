"""Prompt templates for LLM-driven simulation decisions."""

# System prompts for each decision type
MOVEMENT_SYSTEM_PROMPT = (
    "You are a fantasy world simulation engine. You embody each character's "
    "unique personality when deciding their actions. A coward avoids danger. "
    "A curious explorer seeks the unknown. A loyal friend stays close to allies. "
    "Never make generic decisions — every choice must reflect WHO this character is. "
    "Respond with ONLY valid JSON, no other text."
)

INTERACTION_SYSTEM_PROMPT = (
    "You are a fantasy world narrative engine generating emergent story events. "
    "Characters interact based on their unique personalities, histories, and "
    "relationships. A tavern scene with a vengeful assassin and their target's "
    "best friend plays VERY differently than two friendly merchants. Generate "
    "surprising but believable interactions. Create drama, tension, humor, "
    "romance, betrayal — whatever the characters would naturally produce. "
    "Respond with ONLY valid JSON, no other text."
)

FACTION_POLITICS_SYSTEM_PROMPT = (
    "You are a fantasy world political simulation engine. Factions act based on "
    "their alignment, leadership personality, strategic interests, and relationships. "
    "An evil faction with a cunning leader schemes differently than a good faction "
    "with a naive leader. Consider power dynamics, alliances, grudges, and "
    "opportunities. Respond with ONLY valid JSON, no other text."
)

RELATIONSHIP_SYSTEM_PROMPT = (
    "You are evaluating how a specific event changes the relationship between "
    "two characters based on their unique personalities. A loyal character "
    "betrayed by an ally reacts very differently than a cynical one who expected it. "
    "Consider personality compatibility, shared history, and the emotional weight "
    "of the event. Respond with ONLY valid JSON, no other text."
)

DAILY_NARRATIVE_SYSTEM_PROMPT = (
    "You are writing brief, character-authentic journal entries for fantasy "
    "characters. Each character has a unique voice based on their personality. "
    "A gruff warrior writes differently than a scholarly mage. Keep entries "
    "short (2-3 sentences), personal, and true to the character's nature."
)


def _format_personality(personality: dict) -> str:
    """Format personality traits into readable text."""
    if not personality:
        return "Unknown temperament"
    traits = []
    for trait, value in sorted(personality.items()):
        if value >= 8:
            traits.append(f"extremely {trait}")
        elif value >= 6:
            traits.append(f"quite {trait}")
        elif value <= 2:
            traits.append(f"lacks {trait}")
        elif value <= 4:
            traits.append(f"somewhat low {trait}")
    return ", ".join(traits) if traits else "balanced temperament"


def movement_batch_prompt(
    location_name: str,
    location_type: str,
    characters: list[dict],
    nearby_locations: list[dict],
) -> tuple[str, str]:
    """Prompt for location-batched movement decisions."""
    char_list = "\n".join([
        f"- {c['name']} ({c['race']} {c['class_type']}, Level {c.get('level', 1)})\n"
        f"  Personality: {_format_personality(c.get('personality', {}))}\n"
        f"  Backstory: {c.get('backstory', 'Unknown')}\n"
        f"  State: Health={c['health']}/100, Hunger={c['hunger']}/100, "
        f"Energy={c['energy']}/100, Gold={c.get('gold', 0)}\n"
        f"  Goals: {', '.join(c.get('goals', ['none']))}"
        for c in characters
    ])

    loc_list = "\n".join([
        f"- {l['name']} ({l['type']}, danger={l['danger_level']})"
        for l in nearby_locations
    ])

    user_prompt = f"""You are simulating fantasy characters' movement decisions at {location_name} ({location_type}).

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

    return MOVEMENT_SYSTEM_PROMPT, user_prompt


def interaction_prompt(
    location_name: str,
    location_type: str,
    characters: list[dict],
    relationships: list[dict],
    recent_events: list[dict],
) -> tuple[str, str]:
    """Prompt for location interaction outcomes."""
    char_list = "\n".join([
        f"- [ID:{c['id']}] {c['name']} ({c['race']} {c['class_type']})\n"
        f"  Personality: {_format_personality(c.get('personality', {}))}\n"
        f"  Backstory: {c.get('backstory', 'Unknown')}\n"
        f"  State: Health={c['health']}/100, Hunger={c['hunger']}/100, Energy={c['energy']}/100\n"
        f"  Goals: {', '.join(c.get('goals', ['none']))}"
        for c in characters
    ])

    rel_list = "\n".join([
        f"- [ID:{r['char_a']}] <-> [ID:{r['char_b']}]: {r['type']} (strength={r['strength']})"
        for r in relationships
    ]) if relationships else "- None (first meetings)"

    event_list = "\n".join([f"- {e}" for e in recent_events[-3:]]) if recent_events else "- None"

    user_prompt = f"""You are simulating interactions between fantasy characters at {location_name} ({location_type}).

Characters present:
{char_list}

Existing relationships:
{rel_list}

Recent events here:
{event_list}

Generate emergent narrative interactions based on personalities and relationships.
Include relationship changes where relevant. Use character IDs (the numbers in [ID:X])
for the participants array.
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

    return INTERACTION_SYSTEM_PROMPT, user_prompt


def faction_politics_prompt(
    faction_name: str,
    faction_alignment: str,
    faction_power: int,
    relations: dict[str, int],
    member_count: int,
    recent_events: list[str],
    leader_name: str | None = None,
    leader_personality: dict | None = None,
) -> tuple[str, str]:
    """Prompt for faction political decisions."""
    rel_list = "\n".join([
        f"- {name}: {score:+d}"
        for name, score in relations.items()
    ])

    event_list = "\n".join([f"- {e}" for e in recent_events[-3:]]) if recent_events else "- None"

    leader_info = ""
    if leader_name:
        leader_traits = _format_personality(leader_personality or {})
        leader_info = f"\nFaction Leader: {leader_name}\nLeader Personality: {leader_traits}\n"

    user_prompt = f"""You are simulating political decisions for faction "{faction_name}" ({faction_alignment}).

Faction status:
- Power: {faction_power}/100
- Members: {member_count}{leader_info}

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

    return FACTION_POLITICS_SYSTEM_PROMPT, user_prompt


def relationship_evaluation_prompt(
    event_description: str,
    event_type: str,
    severity: int,
    char_a_name: str,
    char_a_personality: dict,
    char_a_backstory: str = "Unknown",
    char_b_name: str = "",
    char_b_personality: dict | None = None,
    char_b_backstory: str = "Unknown",
    current_relationship: dict | None = None,
) -> tuple[str, str]:
    """Prompt for evaluating relationship impact from event."""
    if char_b_personality is None:
        char_b_personality = {}

    current_rel = "None" if not current_relationship else (
        f"{current_relationship['type']} (strength={current_relationship['strength']})"
    )

    char_a_traits = _format_personality(char_a_personality)
    char_b_traits = _format_personality(char_b_personality)

    user_prompt = f"""You are evaluating how an event changes the relationship between two characters.

Event: {event_description}
Type: {event_type}, Severity: {severity}/10

Character A: {char_a_name}
Personality: {char_a_traits}
Backstory: {char_a_backstory}

Character B: {char_b_name}
Personality: {char_b_traits}
Backstory: {char_b_backstory}

Current relationship: {current_rel}

Based on this event and their personalities, how does their relationship change?
Respond ONLY with JSON (no other text):
{{
  "new_type": "friend|enemy|rival|ally|romantic|mentor|neutral|acquaintance",
  "new_strength": 0-100,
  "reason": "why"
}}"""

    return RELATIONSHIP_SYSTEM_PROMPT, user_prompt


def daily_narrative_prompt(
    character: dict,
    events_today: list[dict],
    relationships: list[dict],
    world_state: dict,
) -> tuple[str, str]:
    """Prompt for generating a character's daily narrative."""
    char_traits = _format_personality(character.get("personality", {}))
    event_list = "\n".join([
        f"- {e.get('description', 'An event occurred')} (severity: {e.get('severity', 3)})"
        for e in events_today
    ]) if events_today else "- Nothing significant happened"

    rel_text = ""
    if relationships:
        rel_text = "\n\nKey relationships:\n" + "\n".join([
            f"- {r.get('other_name', 'Someone')}: {r.get('type', 'acquaintance')} "
            f"(strength: {r.get('strength', 50)})"
            for r in relationships[:3]
        ])

    user_prompt = f"""Write a brief first-person journal entry for {character.get('name', 'A character')},
a {character.get('race', 'human')} {character.get('class_type', 'adventurer')}.

Personality: {char_traits}
Backstory: {character.get('backstory', 'Unknown')}

Today's events:
{event_list}{rel_text}

World: {world_state.get('season', 'unknown')} season, {world_state.get('weather', 'clear')} weather.

Write 2-3 sentences in their unique voice reflecting their personality and the day's events.
Do not use generic fantasy language. Make it sound like THIS specific person."""

    return DAILY_NARRATIVE_SYSTEM_PROMPT, user_prompt
