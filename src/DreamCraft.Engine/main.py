"""DreamCraft v2 entry point for API and simulation orchestration."""

from __future__ import annotations

import argparse
import asyncio
import logging
from datetime import datetime

import uvicorn
from apscheduler.schedulers.asyncio import AsyncIOScheduler

from dreamcraft_v2.api.main import create_app
from dreamcraft_v2.data.database import DatabaseManager
from dreamcraft_v2.data.seed import seed_world
from dreamcraft_v2.llm.client import LLMClient
from dreamcraft_v2.simulation.config import settings
from dreamcraft_v2.simulation.core.engine import SimulationEngine
from dreamcraft_v2.simulation.events.event_manager import EventManager


def parse_args() -> argparse.Namespace:
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="DreamCraft Legacies v2")
    parser.add_argument("--run-day", type=int, default=None, help="Run a specific simulation day")
    parser.add_argument("--serve", action="store_true", help="Run API server only")
    parser.add_argument("--simulate", action="store_true", help="Run simulation scheduler only")
    parser.add_argument(
        "--seed", action="store_true", help="Seed initial world data when database is empty"
    )
    return parser.parse_args()


async def build_engine() -> SimulationEngine:
    """Create initialized simulation engine dependencies."""
    database = DatabaseManager(settings.database_path)
    await database.initialize_db()
    llm_client = LLMClient(settings.llm_endpoint, settings.llm_model, settings.llm_timeout)
    event_manager = EventManager(database, settings.major_event_threshold)
    return SimulationEngine(
        database=database,
        llm_client=llm_client,
        event_manager=event_manager,
        settings=settings,
    )


async def run_single_day(day: int) -> None:
    """Run one simulation day."""
    engine = await build_engine()
    try:
        await engine.run_day(day)
    finally:
        await engine.llm_client.close()


async def run_scheduler() -> None:
    """Run simulation using APScheduler."""
    engine = await build_engine()
    scheduler = AsyncIOScheduler()

    async def _scheduled_run() -> None:
        state = await engine.database.get_world_state()
        next_day = 1 if not state else int(state["day"]) + 1
        await engine.run_day(next_day)

    scheduler.add_job(_scheduled_run, "cron", hour=1, minute=0, id="decision_window")
    scheduler.start()
    try:
        while True:
            await asyncio.sleep(1.0)
    finally:
        scheduler.shutdown(wait=False)
        await engine.llm_client.close()


async def run_api_server() -> None:
    """Run FastAPI API server."""
    app = create_app()
    config = uvicorn.Config(app=app, host="0.0.0.0", port=settings.api_port, log_level="info")
    server = uvicorn.Server(config)
    await server.serve()


async def main_async() -> None:
    """Coordinate CLI modes."""
    args = parse_args()
    logging.basicConfig(
        level=getattr(logging, settings.log_level.upper(), logging.INFO),
        format="%(asctime)s %(levelname)s %(name)s %(message)s",
    )

    database = DatabaseManager(settings.database_path)
    await database.initialize_db()
    if args.seed:
        await seed_world(database)

    if args.run_day is not None:
        await run_single_day(args.run_day)
        return

    if args.serve and not args.simulate:
        await run_api_server()
        return

    if args.simulate and not args.serve:
        await run_scheduler()
        return

    scheduler_task = asyncio.create_task(run_scheduler())
    api_task = asyncio.create_task(run_api_server())
    await asyncio.gather(scheduler_task, api_task)


def main() -> None:
    """Synchronous entrypoint wrapper."""
    logging.info("Starting DreamCraft v2 at %s", datetime.utcnow().isoformat())
    asyncio.run(main_async())


if __name__ == "__main__":
    main()
