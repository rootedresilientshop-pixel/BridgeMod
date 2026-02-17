"""Shared pytest fixtures for DreamCraft v2 tests."""

from __future__ import annotations

from pathlib import Path

import pytest

from dreamcraft_v2.data.database import DatabaseManager


@pytest.fixture
async def test_database(tmp_path: Path) -> DatabaseManager:
    """Provide initialized temporary database manager."""
    db_path = tmp_path / "test_sim.db"
    database = DatabaseManager(str(db_path))
    await database.initialize_db()
    return database
