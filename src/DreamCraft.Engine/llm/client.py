"""Async LLM HTTP client for remote Ollama inference."""

from __future__ import annotations

import asyncio
import logging
from typing import Any

import httpx

LOGGER = logging.getLogger(__name__)


class LLMClient:
    """Async client wrapper with retries and rate-limited batch support."""

    def __init__(
        self,
        endpoint: str,
        model: str,
        timeout: int = 120,
        max_retries: int = 3,
        batch_delay_seconds: float = 0.2,
    ) -> None:
        """Create a configured LLM client."""
        self.endpoint = endpoint.rstrip("/")
        self.model = model
        self.timeout = timeout
        self.max_retries = max_retries
        self.batch_delay_seconds = batch_delay_seconds
        self._client = httpx.AsyncClient(timeout=httpx.Timeout(timeout))

    async def close(self) -> None:
        """Close the underlying HTTP client."""
        await self._client.aclose()

    async def __aenter__(self) -> LLMClient:
        """Async context manager entry."""
        return self

    async def __aexit__(self, *args: Any) -> None:
        """Async context manager exit."""
        await self.close()

    async def health_check(self) -> bool:
        """Return True when the LLM endpoint is reachable."""
        # Try Ollama's /api/tags first, then fall back to OpenAI-compatible /v1/models
        urls = [
            f"{self.endpoint}/api/tags",
            f"{self.endpoint}/v1/models",
        ]
        for url in urls:
            try:
                response = await self._client.get(url)
                healthy = response.status_code == 200
                LOGGER.info("LLM health check status=%s healthy=%s url=%s", response.status_code, healthy, url)
                if healthy:
                    return True
            except httpx.HTTPError as exc:
                LOGGER.debug("LLM health check failed for url=%s error=%s", url, exc)
        LOGGER.warning("LLM health check failed: no endpoints reachable")
        return False

    async def make_completion(
        self,
        prompt: str,
        system_prompt: str = "You are a fantasy simulation assistant.",
        temperature: float = 0.7,
        max_tokens: int = 256,
    ) -> str | None:
        """Request a single completion and return text, or None on failure."""
        payload = {
            "model": self.model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": prompt},
            ],
            "temperature": temperature,
            "max_tokens": max_tokens,
        }
        url = f"{self.endpoint}/chat/completions"

        for attempt in range(1, self.max_retries + 1):
            try:
                LOGGER.debug("LLM request attempt=%s model=%s", attempt, self.model)
                response = await self._client.post(url, json=payload)
                response.raise_for_status()
                body: dict[str, Any] = response.json()
                content = (
                    body.get("choices", [{}])[0]
                    .get("message", {})
                    .get("content")
                )
                LOGGER.debug("LLM response received attempt=%s has_content=%s", attempt, bool(content))
                return content if isinstance(content, str) else None
            except (httpx.TimeoutException, httpx.HTTPError, KeyError, IndexError) as exc:
                LOGGER.warning("LLM request failed attempt=%s error=%s", attempt, exc)
                if attempt >= self.max_retries:
                    break
                backoff_seconds = 2 ** (attempt - 1)
                await asyncio.sleep(backoff_seconds)
        return None

    async def make_batch_completion(
        self,
        prompts: list[str],
        system_prompt: str = "You are a fantasy simulation assistant.",
        temperature: float = 0.7,
        max_tokens: int = 256,
        max_concurrent: int = 3,
    ) -> list[str | None]:
        """Run concurrent completions with a semaphore to limit concurrency."""
        semaphore = asyncio.Semaphore(max_concurrent)

        async def limited_completion(index: int, prompt: str) -> str | None:
            async with semaphore:
                LOGGER.info("LLM batch request %s/%s", index, len(prompts))
                result = await self.make_completion(
                    prompt=prompt,
                    system_prompt=system_prompt,
                    temperature=temperature,
                    max_tokens=max_tokens,
                )
                await asyncio.sleep(self.batch_delay_seconds)
                return result

        tasks = [limited_completion(i, p) for i, p in enumerate(prompts, start=1)]
        return await asyncio.gather(*tasks)
