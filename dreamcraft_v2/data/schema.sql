PRAGMA foreign_keys = ON;

-- Authoritative DreamCraft: Legacies v2 schema
-- This consolidates prior fragmented Phase 4A/4B concepts into a single model.

CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS regions (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    biome TEXT,
    danger_level INTEGER DEFAULT 0 CHECK (danger_level BETWEEN 0 AND 10),
    prosperity INTEGER DEFAULT 50 CHECK (prosperity BETWEEN 0 AND 100),
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS locations (
    id INTEGER PRIMARY KEY,
    region_id INTEGER REFERENCES regions(id) ON DELETE SET NULL,
    name TEXT NOT NULL UNIQUE,
    type TEXT NOT NULL,
    description TEXT,
    x INTEGER,
    y INTEGER,
    danger_level INTEGER DEFAULT 0 CHECK (danger_level BETWEEN 0 AND 10),
    capacity INTEGER DEFAULT 50 CHECK (capacity > 0),
    resources TEXT
);

-- Merged v1/v2 factions model (power politics + JSON relationships)
CREATE TABLE IF NOT EXISTS factions (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    type TEXT,
    description TEXT,
    alignment TEXT,
    power INTEGER DEFAULT 50 CHECK (power BETWEEN 0 AND 100),
    power_level INTEGER DEFAULT 5 CHECK (power_level BETWEEN 1 AND 10),
    gold INTEGER DEFAULT 1000,
    wealth INTEGER DEFAULT 1000,
    goals TEXT,
    territory TEXT,
    relations TEXT,
    active_plots TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Merged v1/v2 characters model (combat/progression + simulation needs)
CREATE TABLE IF NOT EXISTS characters (
    id INTEGER PRIMARY KEY,
    player_id INTEGER,
    name TEXT NOT NULL UNIQUE,
    race TEXT NOT NULL,
    class_type TEXT NOT NULL,
    personality TEXT NOT NULL,
    personality_traits TEXT,
    goals TEXT,
    primary_goal TEXT,
    risk_tolerance TEXT,
    backstory TEXT,

    health INTEGER DEFAULT 100,
    hunger INTEGER DEFAULT 0,
    energy INTEGER DEFAULT 100,
    gold INTEGER DEFAULT 0,
    level INTEGER DEFAULT 1,
    experience INTEGER DEFAULT 0,

    hp_current INTEGER,
    hp_max INTEGER,
    armor_class INTEGER,
    attack_bonus INTEGER,
    damage_die TEXT,

    strength INTEGER,
    dexterity INTEGER,
    constitution INTEGER,
    intelligence INTEGER,
    wisdom INTEGER,
    charisma INTEGER,

    inventory TEXT DEFAULT '[]',
    accomplishments TEXT DEFAULT '[]',

    location_id INTEGER REFERENCES locations(id) ON DELETE SET NULL,
    faction_id INTEGER REFERENCES factions(id) ON DELETE SET NULL,
    status TEXT DEFAULT 'alive'
        CHECK (status IN ('alive', 'injured', 'unconscious', 'retired', 'dead')),

    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    died_at TIMESTAMP,
    retired_at TIMESTAMP,
    legacy_score INTEGER DEFAULT 0
);

CREATE TABLE IF NOT EXISTS relationships (
    id INTEGER PRIMARY KEY,
    character_id_a INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    character_id_b INTEGER NOT NULL REFERENCES characters(id) ON DELETE CASCADE,
    type TEXT NOT NULL,
    strength INTEGER DEFAULT 50 CHECK (strength BETWEEN 0 AND 100),
    history TEXT,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(character_id_a, character_id_b),
    CHECK (character_id_a != character_id_b)
);

CREATE TABLE IF NOT EXISTS events (
    id INTEGER PRIMARY KEY,
    day INTEGER NOT NULL,
    tick INTEGER,
    type TEXT NOT NULL,
    severity INTEGER DEFAULT 1 CHECK (severity BETWEEN 1 AND 10),
    is_major BOOLEAN DEFAULT FALSE,
    location_id INTEGER REFERENCES locations(id) ON DELETE SET NULL,
    description TEXT NOT NULL,
    participants TEXT,
    outcome TEXT,
    narrative TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Merged v1/v2 world-state model (time + resources + population)
CREATE TABLE IF NOT EXISTS world_state (
    id INTEGER PRIMARY KEY,
    day INTEGER NOT NULL UNIQUE,
    day_number INTEGER UNIQUE,
    season TEXT DEFAULT 'spring',
    weather TEXT DEFAULT 'clear',
    time_of_day TEXT DEFAULT 'morning',
    global_events TEXT,
    resource_levels TEXT,
    food_available INTEGER DEFAULT 0,
    wood_available INTEGER DEFAULT 0,
    stone_available INTEGER DEFAULT 0,
    shelter_structures INTEGER DEFAULT 0,
    population_total INTEGER,
    population_alive INTEGER,
    population_dead INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS narratives (
    id INTEGER PRIMARY KEY,
    character_id INTEGER REFERENCES characters(id) ON DELETE CASCADE,
    day INTEGER NOT NULL,
    type TEXT NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS simulation_log (
    id INTEGER PRIMARY KEY,
    day INTEGER NOT NULL,
    phase TEXT NOT NULL,
    message TEXT NOT NULL,
    duration_ms INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Boss Economy (Phase 4A + 4B unified)
CREATE TABLE IF NOT EXISTS threats (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    threat_type TEXT NOT NULL,
    severity INTEGER DEFAULT 5 CHECK (severity BETWEEN 1 AND 10),
    summon_cost INTEGER NOT NULL DEFAULT 0 CHECK (summon_cost >= 0),
    bounty INTEGER NOT NULL DEFAULT 0 CHECK (bounty >= 0),
    region_influence TEXT NOT NULL DEFAULT '{}',
    region_id INTEGER REFERENCES regions(id) ON DELETE SET NULL,
    source_faction_id INTEGER REFERENCES factions(id) ON DELETE SET NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    spawned_day INTEGER,
    resolved_day INTEGER,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS infrastructure_npcs (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL,
    role TEXT NOT NULL,
    region_id INTEGER NOT NULL REFERENCES regions(id) ON DELETE RESTRICT,
    location_id INTEGER REFERENCES locations(id) ON DELETE SET NULL,
    importance_level INTEGER DEFAULT 5 CHECK (importance_level BETWEEN 1 AND 10),
    status TEXT NOT NULL DEFAULT 'active'
        CHECK (status IN ('active', 'inactive', 'missing', 'dead')),
    extorted_by_threat_id INTEGER REFERENCES threats(id) ON DELETE SET NULL,
    impacted_by_threat_id INTEGER REFERENCES threats(id) ON DELETE SET NULL,
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS infrastructure_npc_threat_history (
    id INTEGER PRIMARY KEY,
    infrastructure_npc_id INTEGER NOT NULL REFERENCES infrastructure_npcs(id) ON DELETE CASCADE,
    threat_id INTEGER NOT NULL REFERENCES threats(id) ON DELETE CASCADE,
    impact_type TEXT NOT NULL CHECK (impact_type IN ('extorted', 'impacted')),
    impact_value INTEGER DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    started_day INTEGER,
    ended_day INTEGER,
    details TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Long-term history of retired/dead characters
CREATE TABLE IF NOT EXISTS legacy_ledger (
    id INTEGER PRIMARY KEY,
    character_id INTEGER REFERENCES characters(id) ON DELETE SET NULL,
    character_name TEXT NOT NULL,
    final_faction_id INTEGER REFERENCES factions(id) ON DELETE SET NULL,
    final_region_id INTEGER REFERENCES regions(id) ON DELETE SET NULL,
    final_level INTEGER,
    legacy_score INTEGER NOT NULL DEFAULT 0,
    retirement_reason TEXT,
    retirement_day INTEGER,
    death_day INTEGER,
    epitaph TEXT,
    notable_deeds TEXT,
    world_impact TEXT,
    archived_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_locations_region_id ON locations(region_id);

CREATE INDEX IF NOT EXISTS idx_factions_power ON factions(power);

CREATE INDEX IF NOT EXISTS idx_characters_location_id ON characters(location_id);
CREATE INDEX IF NOT EXISTS idx_characters_faction_id ON characters(faction_id);
CREATE INDEX IF NOT EXISTS idx_characters_status ON characters(status);
CREATE INDEX IF NOT EXISTS idx_characters_legacy_score ON characters(legacy_score);

CREATE INDEX IF NOT EXISTS idx_events_day ON events(day);
CREATE INDEX IF NOT EXISTS idx_events_is_major ON events(is_major);
CREATE INDEX IF NOT EXISTS idx_events_location_id ON events(location_id);
CREATE INDEX IF NOT EXISTS idx_events_type ON events(type);
CREATE INDEX IF NOT EXISTS idx_events_day_major ON events(day, is_major);

CREATE INDEX IF NOT EXISTS idx_world_state_day ON world_state(day);

CREATE INDEX IF NOT EXISTS idx_narratives_character_id ON narratives(character_id);
CREATE INDEX IF NOT EXISTS idx_narratives_day ON narratives(day);

CREATE INDEX IF NOT EXISTS idx_relationships_character_a ON relationships(character_id_a);
CREATE INDEX IF NOT EXISTS idx_relationships_character_b ON relationships(character_id_b);
CREATE INDEX IF NOT EXISTS idx_relationships_character_b_a ON relationships(character_id_b, character_id_a);

CREATE INDEX IF NOT EXISTS idx_simulation_log_day ON simulation_log(day);

CREATE INDEX IF NOT EXISTS idx_threats_is_active ON threats(is_active);
CREATE INDEX IF NOT EXISTS idx_threats_region_id ON threats(region_id);
CREATE INDEX IF NOT EXISTS idx_threats_severity ON threats(severity);

CREATE INDEX IF NOT EXISTS idx_infrastructure_npcs_region_id ON infrastructure_npcs(region_id);
CREATE INDEX IF NOT EXISTS idx_infrastructure_npcs_extorted_by ON infrastructure_npcs(extorted_by_threat_id);
CREATE INDEX IF NOT EXISTS idx_infrastructure_npcs_impacted_by ON infrastructure_npcs(impacted_by_threat_id);

CREATE INDEX IF NOT EXISTS idx_infra_history_npc_id ON infrastructure_npc_threat_history(infrastructure_npc_id);
CREATE INDEX IF NOT EXISTS idx_infra_history_threat_id ON infrastructure_npc_threat_history(threat_id);
CREATE INDEX IF NOT EXISTS idx_infra_history_is_active ON infrastructure_npc_threat_history(is_active);

CREATE INDEX IF NOT EXISTS idx_legacy_ledger_character_id ON legacy_ledger(character_id);
CREATE INDEX IF NOT EXISTS idx_legacy_ledger_legacy_score ON legacy_ledger(legacy_score);
