PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS schema_migrations (
    version INTEGER PRIMARY KEY,
    applied_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS locations (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    type TEXT NOT NULL,
    description TEXT,
    x INTEGER,
    y INTEGER,
    danger_level INTEGER DEFAULT 0 CHECK (danger_level BETWEEN 0 AND 10),
    capacity INTEGER DEFAULT 50 CHECK (capacity > 0),
    resources TEXT
);

CREATE TABLE IF NOT EXISTS factions (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    description TEXT,
    alignment TEXT,
    power INTEGER DEFAULT 50 CHECK (power BETWEEN 0 AND 100),
    gold INTEGER DEFAULT 1000,
    territory TEXT,
    relations TEXT
);

CREATE TABLE IF NOT EXISTS characters (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    race TEXT NOT NULL,
    class_type TEXT NOT NULL,
    personality TEXT NOT NULL,
    goals TEXT,
    backstory TEXT,
    health INTEGER DEFAULT 100,
    hunger INTEGER DEFAULT 0,
    energy INTEGER DEFAULT 100,
    gold INTEGER DEFAULT 0,
    level INTEGER DEFAULT 1,
    experience INTEGER DEFAULT 0,
    location_id INTEGER REFERENCES locations(id),
    faction_id INTEGER REFERENCES factions(id),
    status TEXT DEFAULT 'alive' CHECK (status IN ('alive', 'dead', 'injured', 'unconscious')),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
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
    location_id INTEGER REFERENCES locations(id),
    description TEXT NOT NULL,
    participants TEXT,
    outcome TEXT,
    narrative TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS world_state (
    id INTEGER PRIMARY KEY,
    day INTEGER NOT NULL UNIQUE,
    season TEXT DEFAULT 'spring',
    weather TEXT DEFAULT 'clear',
    time_of_day TEXT DEFAULT 'morning',
    global_events TEXT,
    resource_levels TEXT,
    population_alive INTEGER,
    population_dead INTEGER,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
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

CREATE INDEX IF NOT EXISTS idx_events_day ON events(day);
CREATE INDEX IF NOT EXISTS idx_events_is_major ON events(is_major);
CREATE INDEX IF NOT EXISTS idx_events_location_id ON events(location_id);

CREATE INDEX IF NOT EXISTS idx_characters_location_id ON characters(location_id);
CREATE INDEX IF NOT EXISTS idx_characters_faction_id ON characters(faction_id);
CREATE INDEX IF NOT EXISTS idx_characters_status ON characters(status);

CREATE INDEX IF NOT EXISTS idx_narratives_character_id ON narratives(character_id);
CREATE INDEX IF NOT EXISTS idx_narratives_day ON narratives(day);

CREATE INDEX IF NOT EXISTS idx_relationships_character_a ON relationships(character_id_a);
CREATE INDEX IF NOT EXISTS idx_relationships_character_b ON relationships(character_id_b);

-- Composite index for clip system queries
CREATE INDEX IF NOT EXISTS idx_events_day_major ON events(day, is_major);

-- Event type filtering for dashboard
CREATE INDEX IF NOT EXISTS idx_events_type ON events(type);

-- Simulation log lookup by day
CREATE INDEX IF NOT EXISTS idx_simulation_log_day ON simulation_log(day);

-- Bidirectional relationship queries
CREATE INDEX IF NOT EXISTS idx_relationships_character_b_a ON relationships(character_id_b, character_id_a);
