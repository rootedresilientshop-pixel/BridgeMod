"""Initialize the DreamCraft: Legacies v2 SQLite database."""

from __future__ import annotations

import argparse
import asyncio
from pathlib import Path

import aiosqlite

from dreamcraft_v2.config import settings


async def setup_database(db_path: str | None = None) -> None:
    """Create the authoritative v2 schema with WAL and busy-timeout settings."""
    target_path = Path(db_path or settings.db_path)
    target_path.parent.mkdir(parents=True, exist_ok=True)

    schema_path = Path(__file__).with_name("schema.sql")
    schema_sql = schema_path.read_text(encoding="utf-8")

    async with aiosqlite.connect(target_path) as connection:
        await connection.execute("PRAGMA foreign_keys = ON")
        await connection.execute("PRAGMA journal_mode = WAL")
        await connection.execute("PRAGMA busy_timeout = 5000")
        await connection.execute("PRAGMA synchronous = NORMAL")
        await connection.executescript(schema_sql)
        await connection.commit()

    print(f"Initialized Legacies v2 database at {target_path}")


def parse_args() -> argparse.Namespace:
    """Parse CLI arguments for optional database-path override."""
    parser = argparse.ArgumentParser(description="Setup DreamCraft: Legacies v2 database")
    parser.add_argument(
        "--db-path",
        default=None,
        help="Optional SQLite path override (defaults to SAGA_DB_PATH from .env).",
    )
    return parser.parse_args()


def main() -> None:
    """Run async database setup."""
    args = parse_args()
    asyncio.run(setup_database(args.db_path))


if __name__ == "__main__":
    main()
