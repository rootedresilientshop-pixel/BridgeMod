# DreamCraft: Legacies v2 — Architecture Decisions

_Decisions are immutable once recorded. Superseded decisions are marked, not deleted._

---

## ADR-001 — Inference Strategy: Oracle-Primary

**Date:** 2026-02-15
**Status:** Decided

### Context
The simulation engine issues multiple LLM calls per tick (one per character decision +
one Scribe narrative pass). These calls need to be low-latency and free. Two candidates
were evaluated:

- **Pi 5 Ollama (local):** Available now. Models: `tinyllama`, `phi`. Limited by Pi 5
  thermal throttling and 4 GB RAM shared with OS + dashboard.
- **Oracle Cloud Ollama (cloud-local):** Oracle Ampere A1 free tier provides 4 vCPU /
  24 GB RAM dedicated to the container. No thermal limits. No per-token cost.

### Decision
**Oracle-Primary inference.** The simulation container on Oracle targets
`http://localhost:11434/v1`. Primary models: `llama3.2:3b-instruct-q4_K_M` (decisions)
and `phi` or `phi:3.5` (narrative/scribe). Pi 5 Ollama (`tinyllama`/`phi`) is retained
as a **cold fallback** for dashboard read queries if Oracle is unreachable.

### Rationale
- 24 GB RAM on Oracle handles `llama3.2:3b` comfortably with no swapping.
- Inference and simulation co-locate on Oracle — zero network hop between engine and LLM.
- Pi 5 is freed for its primary role: SSD vault storage and dashboard serving.
- Pi 5 fallback is passive — it requires no configuration change; the dashboard API
  simply reads from the last vault snapshot.

### Consequences
- Oracle instance must be provisioned before simulation can run.
- `SAGA_LLM_ENDPOINT` must be set to `http://localhost:11434/v1` in the Oracle `.env`.
- Pi 5 Ollama models remain installed but idle during normal operation.

---

## ADR-002 — Storage Strategy: Pi 5 EXT4 SSD as Single Source of Truth

**Date:** 2026-02-15
**Status:** Decided

### Context
Narrative history, character arcs, and world-state snapshots need to be preserved
long-term without dependency on cloud storage costs or availability. The Oracle instance
is ephemeral (free tier can be reclaimed). SQLite WAL on Oracle is the live working
database, but is not a durable archive.

### Decision
**Pi 5's 1TB EXT4 SSD (`/mnt/ssd/dreamcraft_vault/`) is the Single Source of Truth**
for all long-term narrative history. Oracle exports daily snapshots to `/app/exports/`.
`pi5_pull_vault.py` rsync-pulls those exports to the SSD each night at 01:30 (30 minutes
after the simulation decision window closes at 01:00).

| Layer | Location | Role | Durability |
| :--- | :--- | :--- | :--- |
| Live working DB | Oracle `/app/data/saga.db` | Simulation read/write | Ephemeral |
| Daily exports | Oracle `/app/exports/YYYY-MM-DD/` | Snapshot staging | Ephemeral |
| Vault archive | Pi 5 `/mnt/ssd/dreamcraft_vault/` | Long-term narrative record | **Sovereign** |
| Backup | Pi 5 `/mnt/ssd/backups/saga-TIMESTAMP.db` | Point-in-time recovery | **Sovereign** |

### Rationale
- The SSD is physically owned. No monthly storage bill. No vendor lock-in.
- EXT4 with `noatime` is optimised for sequential rsync writes and SQLite WAL.
- rsync is incremental — only changed files transfer each night.
- If Oracle is lost, the vault contains the last complete snapshot. Rebuilding the
  simulation from the vault is a deliberate recovery path.

### Consequences
- Oracle → Pi 5 SSH trust must be configured before vault-sync cron can run.
- The SSD must remain mounted at `/mnt/ssd`. The fstab UUID entry ensures this on reboot.
- `pi5_pull_vault.py --install-cron` must be run once after Oracle is provisioned.

---

## ADR-003 — Public Access Strategy: Cloudflare Tunnel on Pi 4

**Date:** 2026-02-15
**Status:** Decided

### Context
Both `dreamcraftstudio.org` (WordPress) and `dashboard.dreamcraftstudio.org` (DreamCraft
API) must be publicly accessible without opening ports on the home router or managing
TLS certificates manually.

### Decision
**All public ingress routes through the Cloudflare QUIC tunnel running on Pi 4.**
Pi 4 is the dedicated gateway. Pi 5 never exposes a port to the public internet directly.

Current tunnel routing:

```yaml
ingress:
  - hostname: dreamcraftstudio.org
    service: http://localhost:80        # Pi 4 Apache/WordPress
  - hostname: www.dreamcraftstudio.org
    service: http://localhost:80
  - hostname: dashboard.dreamcraftstudio.org
    service: http://intelpi.local:5000  # Pi 5 Dashboard API
  - service: http_status:404
```

When the DreamCraft v2 API is deployed on Oracle, the dashboard route may be updated to
point directly at the Oracle instance or remain via Pi 5 depending on latency testing.

### Rationale
- Zero router configuration. No port-forwarding. No dynamic DNS.
- Cloudflare handles TLS termination and DDoS protection for free.
- Pi 4 is already provisioned for this role with fail2ban and UFW in place.

### Consequences
- Pi 4 must remain online and the cloudflared service must stay running for any public
  access to work.
- Pi 4 WiFi-only (eth0 DOWN) is a reliability risk for the gateway role.

---

## ADR-004 — Container Platform: Docker on Oracle ARM (linux/arm64)

**Date:** 2026-02-13
**Status:** Decided

### Context
The simulation needs to run reliably on an Oracle Ampere A1 (aarch64) instance and
optionally on Pi 5 (also aarch64). The build and runtime must be consistent across both.

### Decision
**Single `linux/arm64` Docker image** built from the project root. Resource limits of
`1.5 vCPU` and `5 GB RAM` are defined in `docker-compose.yml` to prevent runaway
consumption on the free-tier Oracle instance.

### Rationale
- Oracle Ampere A1 and Raspberry Pi 5 are both aarch64. One image serves both.
- Docker Compose healthcheck (SQLite SELECT + HTTP `/api/health`) ensures the container
  self-reports readiness and restarts on failure.
- `restart: unless-stopped` keeps the simulation running across reboots without
  a separate process supervisor.

### Consequences
- Image must be built on an aarch64 host or cross-compiled. `make up` handles this.
- The `./data` and `./exports` directories are bind-mounted so the DB and exports
  survive container rebuilds.
