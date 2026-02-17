"""Export packager for Oracle-side DreamCraft: Legacies vault sync."""

from __future__ import annotations

import gzip
import json
import shutil
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from dreamcraft_v2.config import settings


class VaultManager:
    """Bundle daily markdown, snapshots, and compressed DB backup for sync."""

    def __init__(self, *, export_root: str | Path = "/app/exports") -> None:
        self.export_root = Path(export_root)

    def create_daily_export(
        self,
        *,
        journals_dir: str | Path,
        snapshots_dir: str | Path,
        db_path: str | Path | None = None,
        day_number: int | None = None,
    ) -> dict[str, Any]:
        export_date = datetime.now(timezone.utc).strftime("%Y-%m-%d")
        export_dir = self.export_root / export_date
        journals_target = export_dir / "journals"
        snapshots_target = export_dir / "snapshots"
        journals_target.mkdir(parents=True, exist_ok=True)
        snapshots_target.mkdir(parents=True, exist_ok=True)

        source_journals = Path(journals_dir)
        source_snapshots = Path(snapshots_dir)
        self._copy_tree_contents(source_journals, journals_target)
        self._copy_tree_contents(source_snapshots, snapshots_target)

        source_db = Path(db_path or settings.db_path)
        backup_path = export_dir / "saga.db.gz"
        self._gzip_copy(source_db, backup_path)

        manifest = {
            "day_number": day_number,
            "export_date_utc": export_date,
            "export_dir": str(export_dir),
            "journals_dir": str(journals_target),
            "snapshots_dir": str(snapshots_target),
            "database_backup": str(backup_path),
        }
        (export_dir / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
        return manifest

    def _copy_tree_contents(self, source: Path, destination: Path) -> None:
        if not source.exists():
            return
        for item in source.iterdir():
            target = destination / item.name
            if item.is_dir():
                shutil.copytree(item, target, dirs_exist_ok=True)
            else:
                shutil.copy2(item, target)

    def _gzip_copy(self, source_file: Path, destination_gzip: Path) -> None:
        if not source_file.exists():
            raise FileNotFoundError(f"Database file not found for export: {source_file}")
        with source_file.open("rb") as src, gzip.open(destination_gzip, "wb") as dst:
            shutil.copyfileobj(src, dst)
