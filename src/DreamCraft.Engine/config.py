"""Application configuration for DreamCraft: Legacies v2."""

from __future__ import annotations

from pydantic import Field
from pydantic import field_validator
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Runtime settings loaded from .env using the SAGA_ prefix."""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_prefix="SAGA_",
        extra="ignore",
    )

    db_path: str = Field(default="data/saga.db")
    api_port: int = Field(default=8002, ge=1, le=65535)
    log_level: str = Field(default="INFO")

    llm_endpoint: str = Field(default="http://llm.local:11434/v1")
    llm_model: str = Field(default="mistral")
    llm_timeout: int = Field(default=120, ge=1)

    simulation_burst_enabled: bool = Field(default=True)
    decision_window_start: str = Field(default="01:00")
    decision_window_end: str = Field(default="04:00")
    narrative_window_start: str = Field(default="04:00")
    narrative_window_end: str = Field(default="06:00")

    major_event_threshold: int = Field(default=7, ge=1, le=10)
    sqlite_journal_mode: str = Field(default="WAL")
    sqlite_busy_timeout_ms: int = Field(default=5000, ge=0)

    @field_validator("sqlite_journal_mode")
    @classmethod
    def normalize_journal_mode(cls, value: str) -> str:
        """Normalize SQLite journal mode values to uppercase."""
        return value.strip().upper()

    @property
    def database_path(self) -> str:
        """Backwards-compatible alias used by existing modules."""
        return self.db_path


settings = Settings()
