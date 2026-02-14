"""Configuration loader for DreamCraft v2."""

from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


def _as_bool(value: str, default: bool) -> bool:
    """Convert environment strings to boolean values."""
    normalized = value.strip().lower()
    if normalized in {"1", "true", "yes", "on"}:
        return True
    if normalized in {"0", "false", "no", "off"}:
        return False
    return default


@dataclass(frozen=True, slots=True)
class Settings:
    """Runtime configuration for simulation, API, and LLM services."""

    database_path: str
    llm_endpoint: str
    llm_model: str
    llm_timeout: int
    simulation_burst_enabled: bool
    decision_window_start: str
    decision_window_end: str
    narrative_window_start: str
    narrative_window_end: str
    log_level: str
    api_port: int
    major_event_threshold: int

    @classmethod
    def from_env(cls) -> "Settings":
        """Build settings from environment variables with defaults."""
        default_db_path = Path(__file__).resolve().parents[1] / "data" / "dreamcraft_v2.db"
        burst_raw = os.getenv("SIMULATION_BURST_ENABLED", "true")
        return cls(
            database_path=os.getenv("DATABASE_PATH", str(default_db_path)),
            llm_endpoint=os.getenv("LLM_ENDPOINT", "http://llm.local:11434/v1"),
            llm_model=os.getenv("LLM_MODEL", "mistral"),
            llm_timeout=int(os.getenv("LLM_TIMEOUT", "120")),
            simulation_burst_enabled=_as_bool(burst_raw, True),
            decision_window_start=os.getenv("DECISION_WINDOW_START", "01:00"),
            decision_window_end=os.getenv("DECISION_WINDOW_END", "04:00"),
            narrative_window_start=os.getenv("NARRATIVE_WINDOW_START", "04:00"),
            narrative_window_end=os.getenv("NARRATIVE_WINDOW_END", "06:00"),
            log_level=os.getenv("LOG_LEVEL", "INFO"),
            api_port=int(os.getenv("API_PORT", "8000")),
            major_event_threshold=int(os.getenv("MAJOR_EVENT_THRESHOLD", "7")),
        )


settings = Settings.from_env()
