#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="/opt/dreamcraft_v2"
SERVICE_USER="${SERVICE_USER:-ubuntu}"
PYTHON_BIN="${PYTHON_BIN:-python3.12}"

echo "[1/9] Installing system dependencies"
sudo apt-get update
sudo apt-get install -y software-properties-common curl sqlite3 ufw
sudo add-apt-repository -y ppa:deadsnakes/ppa
sudo apt-get update
sudo apt-get install -y python3.12 python3.12-venv python3.12-dev

echo "[2/9] Installing cloudflared"
curl -L https://github.com/cloudflare/cloudflared/releases/latest/download/cloudflared-linux-amd64.deb -o /tmp/cloudflared.deb
sudo dpkg -i /tmp/cloudflared.deb || sudo apt-get install -f -y

echo "[3/9] Creating project directory"
sudo mkdir -p "${PROJECT_DIR}"
sudo chown -R "${SERVICE_USER}:${SERVICE_USER}" "${PROJECT_DIR}"

echo "[4/9] Copying project files"
rsync -av --delete ./ "${PROJECT_DIR}/"

echo "[5/9] Creating virtual environment"
"${PYTHON_BIN}" -m venv "${PROJECT_DIR}/.venv"
source "${PROJECT_DIR}/.venv/bin/activate"

echo "[6/9] Installing Python dependencies"
pip install --upgrade pip
pip install -r "${PROJECT_DIR}/requirements.txt"
pip install pytest pytest-asyncio

echo "[7/9] Writing simulation service"
sudo tee /etc/systemd/system/dreamcraft-sim.service >/dev/null <<EOF
[Unit]
Description=DreamCraft v2 Simulation Scheduler
After=network.target

[Service]
Type=simple
User=${SERVICE_USER}
WorkingDirectory=${PROJECT_DIR}
Environment=PYTHONUNBUFFERED=1
ExecStart=${PROJECT_DIR}/.venv/bin/python -m dreamcraft_v2.main --simulate
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

echo "[8/9] Writing API service"
sudo tee /etc/systemd/system/dreamcraft-api.service >/dev/null <<EOF
[Unit]
Description=DreamCraft v2 API Server
After=network.target

[Service]
Type=simple
User=${SERVICE_USER}
WorkingDirectory=${PROJECT_DIR}
Environment=PYTHONUNBUFFERED=1
ExecStart=${PROJECT_DIR}/.venv/bin/python -m dreamcraft_v2.main --serve
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF

echo "[9/9] Configuring firewall and enabling services"
sudo ufw allow 22/tcp
sudo ufw allow 8000/tcp
sudo ufw --force enable
sudo systemctl daemon-reload
sudo systemctl enable dreamcraft-sim.service dreamcraft-api.service

echo "Setup complete. Start services with:"
echo "  sudo systemctl start dreamcraft-sim.service"
echo "  sudo systemctl start dreamcraft-api.service"
