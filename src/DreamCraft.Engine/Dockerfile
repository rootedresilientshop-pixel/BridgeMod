FROM --platform=linux/arm64 python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1
ENV PYTHONPATH=/app

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends sqlite3 rsync \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt /app/dreamcraft_v2/requirements.txt
RUN pip install --no-cache-dir -r /app/dreamcraft_v2/requirements.txt

COPY . /app/dreamcraft_v2
COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

EXPOSE 8002

HEALTHCHECK --interval=60s --timeout=10s --start-period=20s --retries=3 \
  CMD python -c "import os,sqlite3,urllib.request; db=os.getenv('SAGA_DB_PATH','/app/data/saga.db'); sqlite3.connect(db).execute('SELECT 1').fetchone(); r=urllib.request.urlopen('http://127.0.0.1:8002/api/health', timeout=5); assert r.status==200"

ENTRYPOINT ["/app/entrypoint.sh"]
