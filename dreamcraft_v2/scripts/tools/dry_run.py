"""Dry run: seed database, run 1 simulated day, report results."""

import asyncio
import sys
import os
import json

# Add project root to path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "..", ".."))

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.data.seed import seed_database
from dreamcraft_v2.llm.client import LLMClient
from dreamcraft_v2.simulation.config import Settings
from dreamcraft_v2.simulation.core.engine import SimulationEngine
from dreamcraft_v2.simulation.events.event_manager import EventManager


async def main():
    print("=" * 60)
    print("DreamCraft Legacies v2 — Dry Run (Fallback Mode)")
    print("=" * 60)

    # Use a temporary test database
    db_path = os.path.join(os.path.dirname(__file__), "..", "..", "test_dry_run.db")
    
    # Clean up any previous test database
    if os.path.exists(db_path):
        os.remove(db_path)
        print(f"[CLEANUP] Removed old test database")

    # Initialize database
    print(f"\n[DB] Initializing database at: {db_path}")
    db = DatabaseManager(db_path)
    await db.initialize_db()
    print("[DB] ✅ Schema created")

    # Seed the world
    print("\n[SEED] Populating world...")
    await seed_database(db)
    
    # Verify seed data
    locations = await db.get_locations()
    characters = await db.get_alive_characters()
    factions = await db.get_factions()
    print(f"[SEED] ✅ Locations: {len(locations)}")
    print(f"[SEED] ✅ Characters: {len(characters)}")
    print(f"[SEED] ✅ Factions: {len(factions)}")

    if not locations or not characters:
        print("[ERROR] ❌ Seed data missing! Check seed.py")
        return

    # Print some character details to verify personality data
    print("\n[CHARACTERS] Sample character data:")
    for char in characters[:3]:
        name = char.get("name", "Unknown")
        race = char.get("race", "Unknown")
        class_type = char.get("class_type", "Unknown")
        personality = char.get("personality", "{}")
        if isinstance(personality, str):
            personality = json.loads(personality)
        print(f"  - {name} ({race} {class_type})")
        print(f"    Personality: {personality}")
        print(f"    Health={char.get('health', '?')} Hunger={char.get('hunger', '?')} Energy={char.get('energy', '?')}")
        print(f"    Location: {char.get('location_id', '?')}, Faction: {char.get('faction_id', '?')}")

    # Create LLM client that will fail health check (no server running)
    # This forces fallback mode for all decisions
    llm = LLMClient(
        endpoint="http://localhost:99999",  # Intentionally unreachable
        model="test",
        timeout=2,
        max_retries=1,
    )

    settings = Settings()
    settings.major_event_threshold = 7

    event_manager = EventManager(db, settings.major_event_threshold)
    engine = SimulationEngine(
        database=db,
        llm_client=llm,
        event_manager=event_manager,
        settings=settings,
    )

    # Run Day 1
    print("\n" + "=" * 60)
    print("[SIM] Running Day 1...")
    print("=" * 60)

    try:
        summary = await engine.run_day(1)
        print(f"\n[SIM] ✅ Day 1 complete!")
        print(f"[SIM] Summary: {json.dumps(summary, indent=2)}")
    except Exception as e:
        print(f"\n[SIM] ❌ Day 1 FAILED: {type(e).__name__}: {e}")
        import traceback
        traceback.print_exc()
        # Don't exit — still try to report what happened
        summary = {"error": str(e)}

    # Report results
    print("\n" + "=" * 60)
    print("[REPORT] Post-simulation analysis")
    print("=" * 60)

    # World state
    world = await db.get_world_state()
    if world:
        print(f"\n[WORLD] Day: {world.get('day')}")
        print(f"[WORLD] Season: {world.get('season')}")
        print(f"[WORLD] Weather: {world.get('weather')}")
        print(f"[WORLD] Population alive: {world.get('population_alive')}")
        print(f"[WORLD] Population dead: {world.get('population_dead')}")
    else:
        print("[WORLD] ❌ No world state recorded")

    # Events
    events = await db.get_events_for_day(1)
    print(f"\n[EVENTS] Total events: {len(events)}")
    
    # Break down by type
    event_types = {}
    major_count = 0
    for event in events:
        etype = event.get("type", "unknown")
        event_types[etype] = event_types.get(etype, 0) + 1
        if event.get("is_major"):
            major_count += 1
    
    print(f"[EVENTS] By type:")
    for etype, count in sorted(event_types.items()):
        print(f"  - {etype}: {count}")
    print(f"[EVENTS] Major events (clip-worthy): {major_count}")

    # Show some sample events
    if events:
        print(f"\n[EVENTS] Sample events:")
        for event in events[:5]:
            print(f"  [{event.get('type')}] sev={event.get('severity')} "
                  f"major={event.get('is_major')} — {event.get('description')}")

    # Character state after simulation
    chars_after = await db.get_alive_characters()
    dead_chars = await db.fetch_all(
        "SELECT name, status FROM characters WHERE status != 'alive'"
    )
    print(f"\n[CHARS] Alive: {len(chars_after)}")
    print(f"[CHARS] Dead/injured/unconscious: {len(dead_chars)}")
    for dc in dead_chars:
        print(f"  - {dc['name']}: {dc['status']}")

    # Relationships created
    relationships = await db.fetch_all("SELECT * FROM relationships")
    print(f"\n[RELS] Relationships created: {len(relationships)}")
    for rel in relationships[:5]:
        print(f"  - {rel['character_id_a']} <-> {rel['character_id_b']}: "
              f"{rel['type']} (strength={rel['strength']})")

    # Simulation log
    log_entries = await db.fetch_all(
        "SELECT phase, message, duration_ms FROM simulation_log WHERE day = 1"
    )
    print(f"\n[PERF] Phase timings:")
    total_ms = 0
    for entry in log_entries:
        ms = entry.get("duration_ms", 0) or 0
        total_ms += ms
        print(f"  - {entry['phase']}: {ms}ms — {entry['message']}")
    print(f"  TOTAL: {total_ms}ms")

    # Final verdict
    print("\n" + "=" * 60)
    errors = []
    if not world:
        errors.append("No world state")
    if len(events) == 0:
        errors.append("No events generated")
    if len(chars_after) == 0:
        errors.append("All characters dead")
    if "error" in summary:
        errors.append(f"Simulation error: {summary['error']}")

    if errors:
        print("[RESULT] ❌ DRY RUN FAILED")
        for err in errors:
            print(f"  - {err}")
    else:
        print("[RESULT] ✅ DRY RUN PASSED")
        print(f"  - {len(events)} events generated")
        print(f"  - {major_count} major events flagged")
        print(f"  - {len(relationships)} relationships created")
        print(f"  - {len(chars_after)} characters alive")
        print(f"  - Total simulation time: {total_ms}ms")
    print("=" * 60)

    # Cleanup
    await llm.close()
    # Keep the test database for inspection
    print(f"\n[INFO] Test database preserved at: {db_path}")
    print("[INFO] You can inspect it with: sqlite3 {db_path}")


if __name__ == "__main__":
    asyncio.run(main())
