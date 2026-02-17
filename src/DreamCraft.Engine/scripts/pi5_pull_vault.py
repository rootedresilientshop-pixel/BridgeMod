"""Pi 5 pull script for DreamCraft vault exports.

Pulls the latest Oracle export directory into:
  /mnt/ssd/dreamcraft_vault/

Usage:
  python3 pi5_pull_vault.py --oracle-user ubuntu --oracle-host 1.2.3.4

Optional cron installation (default simulation finish at 01:00 -> pull at 01:30):
  python3 pi5_pull_vault.py --oracle-user ubuntu --oracle-host 1.2.3.4 --install-cron
"""

from __future__ import annotations

import argparse
import re
import shutil
import subprocess
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Pull latest DreamCraft export from Oracle.")
    parser.add_argument("--oracle-user", required=True, help="SSH user for Oracle host.")
    parser.add_argument("--oracle-host", required=True, help="Oracle host/IP.")
    parser.add_argument("--oracle-export-root", default="/app/exports", help="Export root on Oracle.")
    parser.add_argument(
        "--local-vault-root",
        default="/mnt/ssd/dreamcraft_vault",
        help="Local SSD destination on Pi 5.",
    )
    parser.add_argument("--ssh-port", type=int, default=22)
    parser.add_argument(
        "--simulation-finish",
        default="01:00",
        help="Simulation finish time HH:MM; pull will be scheduled +30 min.",
    )
    parser.add_argument(
        "--install-cron",
        action="store_true",
        help="Install/update a daily cron entry for automatic pull.",
    )
    return parser.parse_args()


def latest_export_folder(user: str, host: str, root: str, ssh_port: int) -> str:
    cmd = [
        "ssh",
        "-p",
        str(ssh_port),
        f"{user}@{host}",
        f"ls -1 {root} | sort | tail -n 1",
    ]
    result = subprocess.run(cmd, capture_output=True, text=True, check=True)
    folder = result.stdout.strip()
    if not folder:
        raise RuntimeError(f"No export folders found at {root} on {host}.")
    return folder


def pull_export(
    user: str,
    host: str,
    remote_root: str,
    export_folder: str,
    local_root: str,
    ssh_port: int,
) -> str:
    local_target = Path(local_root)
    local_target.mkdir(parents=True, exist_ok=True)

    remote_path = f"{user}@{host}:{remote_root}/{export_folder}/"
    destination = str(local_target / export_folder)
    Path(destination).mkdir(parents=True, exist_ok=True)

    if shutil.which("rsync"):
        cmd = ["rsync", "-az", "-e", f"ssh -p {ssh_port}", remote_path, destination]
    else:
        cmd = ["scp", "-P", str(ssh_port), "-r", remote_path.rstrip("/"), destination]

    subprocess.run(cmd, check=True)
    return destination


def _add_30_minutes(hhmm: str) -> tuple[int, int]:
    match = re.fullmatch(r"([01]?\d|2[0-3]):([0-5]\d)", hhmm.strip())
    if not match:
        raise ValueError(f"Invalid time format '{hhmm}', expected HH:MM.")
    hour = int(match.group(1))
    minute = int(match.group(2))
    total = (hour * 60) + minute + 30
    return (total // 60) % 24, total % 60


def install_daily_cron(script_path: str, args: argparse.Namespace) -> str:
    hour, minute = _add_30_minutes(args.simulation_finish)
    cron_cmd = (
        f"{minute} {hour} * * * "
        f"python3 {script_path} "
        f"--oracle-user {args.oracle_user} "
        f"--oracle-host {args.oracle_host} "
        f"--oracle-export-root {args.oracle_export_root} "
        f"--local-vault-root {args.local_vault_root} "
        f"--ssh-port {args.ssh_port}"
    )

    existing = subprocess.run(["crontab", "-l"], capture_output=True, text=True)
    lines = []
    if existing.returncode == 0:
        for line in existing.stdout.splitlines():
            if "pi5_pull_vault.py" in line:
                continue
            lines.append(line)
    lines.append(cron_cmd)
    payload = "\n".join(line for line in lines if line.strip()) + "\n"
    subprocess.run(["crontab", "-"], input=payload, text=True, check=True)
    return cron_cmd


def main() -> None:
    args = parse_args()
    script_path = str(Path(__file__).resolve())

    if args.install_cron:
        entry = install_daily_cron(script_path, args)
        print(f"Installed daily cron entry: {entry}")
        return

    latest = latest_export_folder(
        user=args.oracle_user,
        host=args.oracle_host,
        root=args.oracle_export_root,
        ssh_port=args.ssh_port,
    )
    destination = pull_export(
        user=args.oracle_user,
        host=args.oracle_host,
        remote_root=args.oracle_export_root,
        export_folder=latest,
        local_root=args.local_vault_root,
        ssh_port=args.ssh_port,
    )
    print(f"Pulled export '{latest}' to: {destination}")


if __name__ == "__main__":
    main()
