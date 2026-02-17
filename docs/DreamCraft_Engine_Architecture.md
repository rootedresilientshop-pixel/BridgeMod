# DreamCraft: Legacies v2 — Project Definition

## Vision

**Hybrid Cloud-Sovereign** simulation platform: an autonomous medieval world that runs
continuously on cloud compute while keeping all long-term narrative history under local
physical control. No data lock-in, no per-token billing for inference, no single point
of failure.

---

## Architecture — Hybrid Cloud-Sovereign

```
┌─────────────────────────────────────────────────────────────────┐
│                     CLOUD LAYER (Oracle ARM)                    │
│                       "The Engine Room"                         │
│                                                                 │
│  ┌──────────────────────┐   ┌─────────────────────────────┐    │
│  │  Simulation Engine   │   │   Ollama (Primary Inference) │    │
│  │  (Docker Container)  │──▶│   llama3.2:3b / phi:3.5     │    │
│  │  port :8002          │   │   port :11434 (localhost)   │    │
│  │  SQLite WAL          │   └─────────────────────────────┘    │
│  │  /app/exports/       │                                       │
│  └──────────┬───────────┘                                       │
└─────────────┼───────────────────────────────────────────────────┘
              │ rsync / SSH  (daily @ 01:30 via cron)
              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  LOCAL LAYER — Pi 5 (intelpi)                   │
│                    "The Vault & Dashboard"                      │
│                                                                 │
│  ┌──────────────────────┐   ┌─────────────────────────────┐    │
│  │  1TB EXT4 SSD        │   │   Dashboard API             │    │
│  │  /mnt/ssd/           │   │   port :5000                │    │
│  │  dreamcraft_vault/   │   │   (served via cloudflared)  │    │
│  │  (Scribe archives)   │   └─────────────────────────────┘    │
│  └──────────────────────┘                                       │
│  Ollama fallback: tinyllama / phi  (:11434 localhost)           │
│  Docker Engine v29.2.1  |  4 GB RAM  |  eth0 192.168.0.197     │
└─────────────────────────────┬───────────────────────────────────┘
                              │ cloudflared tunnel (QUIC)
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                  LOCAL LAYER — Pi 4 (passivebrain)              │
│                        "The Gateway"                            │
│                                                                 │
│  ┌────────────────────────────────────────────────────────┐    │
│  │  Cloudflare Tunnel  (tunnel ID: 3eecc475-...)          │    │
│  │  dreamcraftstudio.org          → Apache/WordPress :80  │    │
│  │  www.dreamcraftstudio.org      → Apache/WordPress :80  │    │
│  │  dashboard.dreamcraftstudio.org → Pi 5 :5000           │    │
│  └────────────────────────────────────────────────────────┘    │
│  Apache2 + WordPress  |  MariaDB 11.8  |  wlan0 192.168.0.108  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Infrastructure Map

| Node | Role | Key Services | IP |
| :--- | :--- | :--- | :--- |
| Oracle Cloud (ARM) | Engine Room | Docker (Simulation), Ollama (primary LLM) | TBD |
| Pi 5 — intelpi | Vault & Dashboard | DREAM_VAULT SSD, Dashboard API, Ollama (fallback) | 192.168.0.197 |
| Pi 4 — passivebrain | Gateway | Cloudflare Tunnel, Apache/WordPress | 192.168.0.108 |

---

## Data Flow

1. **Simulation tick** (Oracle, 01:00–04:00): Engine runs character decisions via Ollama.
2. **Narrative pass** (Oracle, 04:00–06:00): Scribe generates narrative for major events.
3. **Vault sync** (Pi 5, 01:30 cron): `pi5_pull_vault.py` rsync-pulls `/app/exports/` from
   Oracle into `/mnt/ssd/dreamcraft_vault/`.
4. **Dashboard read** (Pi 5, :5000): REST + WebSocket API serves world state, characters,
   events, factions, and locations to any connected browser via `dashboard.dreamcraftstudio.org`.

---

## Key Configuration (SAGA_ env prefix)

| Variable | Default | Purpose |
| :--- | :--- | :--- |
| `SAGA_DB_PATH` | `data/saga.db` | SQLite database path |
| `SAGA_API_PORT` | `8002` | API server port |
| `SAGA_LLM_ENDPOINT` | `http://llm.local:11434/v1` | Ollama base URL |
| `SAGA_LLM_MODEL` | `mistral` | Active inference model |
| `SAGA_DECISION_WINDOW_START` | `01:00` | Simulation burst start |
| `SAGA_DECISION_WINDOW_END` | `04:00` | Simulation burst end |
| `SAGA_NARRATIVE_WINDOW_START` | `04:00` | Scribe pass start |
| `SAGA_NARRATIVE_WINDOW_END` | `06:00` | Scribe pass end |
| `SAGA_MAJOR_EVENT_THRESHOLD` | `7` | Severity score for major events |
| `SAGA_SQLITE_JOURNAL_MODE` | `WAL` | SQLite journal mode |

---

## Sovereignty Principles

- **Inference is local-first.** Ollama runs on Oracle ARM bare metal; no API key required.
- **Storage is Pi-sovereign.** Long-term narrative archives live on the physical SSD at
  `/mnt/ssd/dreamcraft_vault/` — not in cloud object storage.
- **Public access is zero-trust.** All external traffic routes through Cloudflare Tunnel
  (QUIC). No ports are opened on the home router.
- **Failure degrades gracefully.** If Oracle is unreachable, Pi 5's fallback Ollama
  (tinyllama/phi) keeps the dashboard queryable off the last vault snapshot.

---

## Quick Operations (Makefile)

All day-to-day operations are handled via `make` from the project root:

| Command | Action |
| :--- | :--- |
| `make up` | Build and start the simulation container (`docker compose up --build -d`) |
| `make down` | Stop the container |
| `make logs` | Tail container logs live |
| `make health` | Check `/api/health`, SQLite integrity, and row counts |
| `make vault-sync` | Pull latest Oracle export to Pi 5 `/mnt/ssd/dreamcraft_vault/` via rsync |
| `make backup` | Copy `data/saga.db` → `backups/saga-YYYYMMDD-HHMMSS.db` |

`vault-sync` reads `ORACLE_HOST` from the environment or from `SAGA_ORACLE_HOST` in `.env`.
Override inline: `make vault-sync ORACLE_HOST=<ip> ORACLE_USER=ubuntu`
