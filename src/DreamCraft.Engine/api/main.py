"""FastAPI server for DreamCraft v2 simulation APIs."""

from __future__ import annotations

import asyncio
import json
from typing import Any

from fastapi import FastAPI, HTTPException, Query, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field

from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.simulation.config import settings
from dreamcraft_v2.simulation.events.event_manager import EventManager


class HealthResponse(BaseModel):
    """Health check payload."""

    status: str


class CharacterResponse(BaseModel):
    """Character response model."""

    id: int
    name: str
    race: str
    class_type: str
    personality: dict[str, Any] = Field(default_factory=dict)
    goals: list[str] = Field(default_factory=list)
    backstory: str | None = None
    health: int
    hunger: int
    energy: int
    gold: int
    level: int
    experience: int
    location_id: int | None = None
    faction_id: int | None = None
    status: str


class WorldStateResponse(BaseModel):
    """World state response model."""

    day: int
    season: str
    weather: str
    time_of_day: str
    global_events: dict[str, Any] = Field(default_factory=dict)
    resource_levels: dict[str, Any] = Field(default_factory=dict)
    population_alive: int | None = None
    population_dead: int | None = None


class EventResponse(BaseModel):
    """Event response model."""

    id: int
    day: int
    tick: int | None = None
    type: str
    severity: int
    is_major: bool
    location_id: int | None = None
    description: str
    participants: list[int] = Field(default_factory=list)
    outcome: dict[str, Any] = Field(default_factory=dict)
    narrative: str | None = None


def create_app(database_path: str | None = None) -> FastAPI:
    """Create and configure the FastAPI application."""
    app = FastAPI(title="DreamCraft Legacies v2 API")
    app.add_middleware(
        CORSMiddleware,
        allow_origins=["*"],
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    database = DatabaseManager(database_path or settings.database_path)
    event_manager = EventManager(database, settings.major_event_threshold)
    app.state.database = database
    app.state.event_manager = event_manager

    @app.on_event("startup")
    async def startup() -> None:
        """Initialize database schema on startup."""
        await database.initialize_db()

    @app.get("/api/health", response_model=HealthResponse)
    async def health() -> HealthResponse:
        """Return API health."""
        return HealthResponse(status="ok")

    @app.get("/api/world/state", response_model=WorldStateResponse)
    async def get_current_world_state() -> WorldStateResponse:
        """Return latest world state."""
        state = await database.get_world_state()
        if not state:
            raise HTTPException(status_code=404, detail="World state not found")
        return _world_state_response(state)

    @app.get("/api/world/state/{day}", response_model=WorldStateResponse)
    async def get_world_state_for_day(day: int) -> WorldStateResponse:
        """Return world state for a specific day."""
        state = await database.get_world_state(day=day)
        if not state:
            raise HTTPException(status_code=404, detail="World state not found")
        return _world_state_response(state)

    @app.get("/api/characters", response_model=list[CharacterResponse])
    async def get_characters(
        limit: int = Query(default=100, ge=1, le=500),
        offset: int = Query(default=0, ge=0),
    ) -> list[CharacterResponse]:
        """Return paginated characters."""
        rows = await database.get_characters(limit=limit, offset=offset)
        return [_character_response(row) for row in rows]

    @app.get("/api/characters/{character_id}", response_model=CharacterResponse)
    async def get_character(character_id: int) -> CharacterResponse:
        """Return one character by ID."""
        row = await database.get_character(character_id)
        if not row:
            raise HTTPException(status_code=404, detail="Character not found")
        return _character_response(row)

    @app.get("/api/characters/{character_id}/narrative")
    async def get_character_narrative(character_id: int) -> list[dict[str, Any]]:
        """Return all narratives for a character."""
        return await database.get_narratives_for_character(character_id)

    @app.get("/api/events/recent", response_model=list[EventResponse])
    async def get_recent_events(since_day: int = Query(default=1, ge=1)) -> list[EventResponse]:
        """Return recent events for dashboard."""
        rows = await event_manager.get_events_for_dashboard(since_day)
        return [_event_response(row) for row in rows]

    @app.get("/api/events/major", response_model=list[EventResponse])
    async def get_major_events(day: int | None = Query(default=None, ge=1)) -> list[EventResponse]:
        """Return major events for clips."""
        rows = await event_manager.get_major_events(day=day)
        return [_event_response(row) for row in rows]

    @app.get("/api/events/day/{day}", response_model=list[EventResponse])
    async def get_events_for_day(day: int) -> list[EventResponse]:
        """Return all events for a day."""
        rows = await database.get_events_for_day(day)
        return [_event_response(row) for row in rows]

    @app.get("/api/factions")
    async def get_factions() -> list[dict[str, Any]]:
        """Return all factions."""
        rows = await database.get_factions()
        return [_decode_json_fields(row, {"territory", "relations"}) for row in rows]

    @app.get("/api/locations")
    async def get_locations() -> list[dict[str, Any]]:
        """Return locations with character counts."""
        rows = await database.get_locations_with_counts()
        return [_decode_json_fields(row, {"resources"}) for row in rows]

    @app.websocket("/ws/events")
    async def events_websocket(websocket: WebSocket) -> None:
        """Push recent events to connected dashboard clients."""
        await websocket.accept()
        try:
            last_day = 1
            while True:
                rows = await event_manager.get_events_for_dashboard(last_day)
                if rows:
                    last_day = max(int(row["day"]) for row in rows)
                    payload = [_event_response(row).model_dump() for row in rows[:50]]
                    await websocket.send_json({"events": payload})
                await asyncio.sleep(2.0)
        except WebSocketDisconnect:
            return

    return app


def _character_response(row: dict[str, Any]) -> CharacterResponse:
    """Convert DB row into CharacterResponse."""
    row = _decode_json_fields(row, {"personality", "goals"})
    goals = row["goals"] if isinstance(row["goals"], list) else []
    return CharacterResponse(
        id=int(row["id"]),
        name=str(row["name"]),
        race=str(row["race"]),
        class_type=str(row["class_type"]),
        personality=row["personality"] if isinstance(row["personality"], dict) else {},
        goals=[str(item) for item in goals],
        backstory=row.get("backstory"),
        health=int(row.get("health", 100)),
        hunger=int(row.get("hunger", 0)),
        energy=int(row.get("energy", 100)),
        gold=int(row.get("gold", 0)),
        level=int(row.get("level", 1)),
        experience=int(row.get("experience", 0)),
        location_id=row.get("location_id"),
        faction_id=row.get("faction_id"),
        status=str(row.get("status", "alive")),
    )


def _world_state_response(row: dict[str, Any]) -> WorldStateResponse:
    """Convert DB row into WorldStateResponse."""
    row = _decode_json_fields(row, {"global_events", "resource_levels"})
    return WorldStateResponse(
        day=int(row["day"]),
        season=str(row["season"]),
        weather=str(row["weather"]),
        time_of_day=str(row["time_of_day"]),
        global_events=row.get("global_events", {}) if isinstance(row.get("global_events"), dict) else {},
        resource_levels=row.get("resource_levels", {}) if isinstance(row.get("resource_levels"), dict) else {},
        population_alive=row.get("population_alive"),
        population_dead=row.get("population_dead"),
    )


def _event_response(row: dict[str, Any]) -> EventResponse:
    """Convert DB row into EventResponse."""
    row = _decode_json_fields(row, {"participants", "outcome"})
    participants = row["participants"] if isinstance(row["participants"], list) else []
    outcome = row["outcome"] if isinstance(row["outcome"], dict) else {}
    return EventResponse(
        id=int(row["id"]),
        day=int(row["day"]),
        tick=row.get("tick"),
        type=str(row["type"]),
        severity=int(row["severity"]),
        is_major=bool(row["is_major"]),
        location_id=row.get("location_id"),
        description=str(row["description"]),
        participants=[int(item) for item in participants],
        outcome=outcome,
        narrative=row.get("narrative"),
    )


def _decode_json_fields(row: dict[str, Any], fields: set[str]) -> dict[str, Any]:
    """Decode JSON text fields into Python objects."""
    parsed = dict(row)
    for field in fields:
        value = parsed.get(field)
        if isinstance(value, str):
            try:
                parsed[field] = json.loads(value)
            except json.JSONDecodeError:
                parsed[field] = {} if field != "goals" and field != "participants" else []
    return parsed


app = create_app()
