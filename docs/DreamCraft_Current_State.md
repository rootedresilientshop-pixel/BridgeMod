# DreamCraft: Legacies v2 — System State

_Last updated: 2026-02-16_

---

## Hardware Readiness

| Node | Status | Details |
| :--- | :--- | :--- |
| Pi 5 — intelpi | **PRODUCTION READY** | Docker v29.2.1 installed. 1TB SSD formatted EXT4, labelled `DREAM_VAULT`, mounted at `/mnt/ssd` (870 GB free). `passivepi` in docker group. 635 MB RAM used / 3.3 GB available. |
| Pi 4 — passivebrain | **OPERATIONAL (degraded)** | Cloudflare tunnel active. Apache/WordPress serving `dreamcraftstudio.org`. `passivebrain.service` crash-looping (exit-code 2). Runs on WiFi only (eth0 DOWN). |
| Oracle Cloud (ARM) | **NOT PROVISIONED** | Instance not yet created. Target: 4 vCPU / 24 GB RAM Ampere A1 (free tier). |

---

## Software Readiness

| Component | Node | Status | Version |
| :--- | :--- | :--- | :--- |
| Docker Engine | Pi 5 | **Installed** | 29.2.1 |
| Docker Engine | Pi 4 | Installed | daemon running, 0 containers |
| Ollama | Pi 5 | Running | tinyllama:latest, phi:latest |
| Ollama | Pi 4 | Running | llama3.2:3b-instruct-q4_K_M, tinyllama:latest |
| Ollama | Oracle | **Not installed** | — |
| DreamCraft v2 Container | Oracle | **Not deployed** | — |
| DreamCraft v2 Container | Pi 5 | Not deployed | — |
| Apache2 + WordPress | Pi 4 | Running | — |
| MariaDB | Pi 4 | Running | 11.8.3 |
| cloudflared | Pi 4 | Running | v2026.1.2 |
| cloudflared | Pi 5 | Not installed | — |
| AITrust service | Pi 5 | **Stopped & disabled** | Reclaimed ~565 MB RAM |
| Makefile | Dev machine | **Ready** | `up`, `down`, `logs`, `health`, `vault-sync`, `backup` |

---

## Network & Connectivity

| Link | Direction | Method | Status |
| :--- | :--- | :--- | :--- |
| Dev machine → Pi 5 | outbound | SSH `claude_pi5_key` | **Active** |
| Dev machine → Pi 4 | outbound | SSH `claude_pi4_key` via ProxyJump Pi5 | **Active** |
| Pi 5 → Pi 4 | outbound | SSH `id_ed25519` (passivepi@intelpi) | **Active — Trust Bridge established** |
| Pi 4 → Pi 5 | outbound | SSH `pi5_key` (passivepi@passivebrain) | **Active** |
| Pi 5 → Oracle | outbound | SSH (rsync/vault-sync) | **Not configured** |
| Pi 4 → Oracle | outbound | SSH | **Not configured** |
| cloudflared public tunnel | inbound | QUIC | **Active** (dreamcraftstudio.org) |
| dashboard.dreamcraftstudio.org | inbound → Pi 5 :5000 | Cloudflare Tunnel | **Routed** (Pi 5 :5000 must serve) |

---

## Blockers

| Priority | Blocker | Affected Component | Notes |
| :--- | :--- | :--- | :--- |
| **HIGH** | Oracle Cloud instance not provisioned | Simulation Engine, Primary LLM | Must create Ampere A1 free-tier instance, install Docker + Ollama |
| **HIGH** | `passivebrain.service` crash-looping (exit-code 2) | Pi 4 TTRPG Kit Generator | Python startup error — check `journalctl -u passivebrain -n 50` on Pi 4 |
| **MEDIUM** | DreamCraft v2 not deployed anywhere | Entire simulation stack | Pending Oracle provisioning |
| **MEDIUM** | Cloudflare tunnel `dashboard.dreamcraftstudio.org` → Pi 5 :5000 is unserved | Public dashboard | Unknown service on Pi 5 :5000; needs Dashboard API deployed |
| **MEDIUM** | Pi 4 eth0 DOWN (WiFi-only) | Pi 4 gateway reliability | Ethernet cable not connected |
| **LOW** | Nginx FAILED on Pi 4 (port conflict with Apache) | Pi 4 | Remove nginx or reassign ports |
| **LOW** | Pi 5 SSD is exFAT on the `sda1` partition | — | `sda1` (16 MB) unused; only `sda2` matters |
| **LOW** | Dev SSH config uses wrong user for Pi 4 | Local dev | Change `User claude` → `User passivepi` in `~/.ssh/config` `dreamcraft-pi4` block |

---

## Port Map

| Port | Service | Node | Public |
| :--- | :--- | :--- | :--- |
| :22 | SSH | Pi 4, Pi 5, Oracle | No |
| :80 | Apache/WordPress | Pi 4 | Via cloudflared |
| :443 | UFW allow (future HTTPS) | Pi 4 | — |
| :3306 | MariaDB | Pi 4 | No (localhost only) |
| :5000 | Dashboard API (target) | Pi 5 | Via cloudflared |
| :8001 | AITrust (disabled) | Pi 5 | No |
| :8002 | DreamCraft v2 API | Oracle (target) | No (internal) |
| :11434 | Ollama | Pi 4, Pi 5, Oracle | No (localhost only) |
| :20241 | Unknown (Pi 5) | Pi 5 | No |
