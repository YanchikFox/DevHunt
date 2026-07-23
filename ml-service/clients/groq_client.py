"""
Groq API Client for AI generation tasks.

Groq provides fast LLM inference with generous free tier limits:
- llama-3.1-8b-instant: 14,400 requests/day, 6K tokens/min
- llama-3.3-70b-versatile: 1,000 requests/day, 12K tokens/min
- groq/compound-mini: 250 requests/day, 70K tokens/min, no daily token limit
"""

import json
import logging
from typing import Any, Callable, Optional

import httpx

logger = logging.getLogger(__name__)


class GroqClient:
    """Async client for Groq API."""

    BASE_URL = "https://api.groq.com/openai/v1"

    # Available models (free tier)
    MODELS = {
        # Best quality model (120B, production, 65K output tokens)
        "smart": "openai/gpt-oss-120b",
        # Fast model, high request limit (14.4K/day, 500K tokens/day)
        "fast": "llama-3.1-8b-instant",
        # Previous smart model (70B)
        "versatile": "llama-3.3-70b-versatile",
        # Compound model (70K tokens/min, no daily token limit)
        "compound": "groq/compound-mini",
        # Fallback models with HIGH token limits (500K tokens/day)
        "smart_fallback": "qwen/qwen3-32b",  # 500K tokens/day
        "scout": "meta-llama/llama-4-scout-17b-16e-instruct",  # 500K tokens/day
        "kimi": "moonshotai/kimi-k2-instruct",  # 300K tokens/day
    }
    
    # Fallback chain: try these models in order when rate limited
    FALLBACK_CHAIN = {
        "smart": ["versatile", "smart_fallback", "scout", "fast"],
        "versatile": ["smart_fallback", "scout", "fast"],
        "smart_fallback": ["scout", "fast"],
    }

    def __init__(
        self,
        api_key: str,
        on_rate_limit: Optional[Callable] = None,
        on_fallback: Optional[Callable] = None,
    ):
        """Initialize the Groq client.

        Args:
            api_key: Groq API key from https://console.groq.com/
            on_rate_limit: Optional callback(model_name) called on 429 responses.
            on_fallback: Optional callback(from_model, to_model, reason) on model fallback.
        """
        self.api_key = api_key
        self.client = httpx.AsyncClient(timeout=60.0)
        self.on_rate_limit = on_rate_limit
        self.on_fallback = on_fallback

    async def close(self):
        """Close the HTTP client."""
        await self.client.aclose()

    def _get_models_to_try(self, model: str) -> list[str]:
        """Build list of models to try including fallbacks."""
        models = [model]
        if model in self.FALLBACK_CHAIN:
            models.extend(self.FALLBACK_CHAIN[model])
        return models

    def _build_headers(self) -> dict[str, str]:
        """Build common request headers."""
        return {
            "Authorization": f"Bearer {self.api_key}",
            "Content-Type": "application/json",
        }

    @staticmethod
    def _extract_usage_info(usage: dict, model_name: str | None = None) -> dict[str, Any]:
        """Extract usage info from response."""
        info = {
            "promptTokens": usage.get("prompt_tokens"),
            "completionTokens": usage.get("completion_tokens"),
            "totalTokens": usage.get("total_tokens"),
        }
        if model_name:
            info["model"] = model_name
        return info

    async def _request_with_fallback(
        self,
        model: str,
        request_body_builder: callable,
        log_context: str = "",
    ) -> tuple[dict, str]:
        """Make request with automatic model fallback on rate limit.

        Args:
            model: Initial model alias
            request_body_builder: Function that takes model_name and returns request body
            log_context: Optional context for logging

        Returns:
            Tuple of (response data, model_name used)

        Raises:
            GroqRateLimitError: If all models are rate limited
        """
        models_to_try = self._get_models_to_try(model)
        last_error = None
        last_failed_model = None

        for try_model in models_to_try:
            model_name = self.MODELS.get(try_model, try_model)
            logger.info("Trying model: %s%s", model_name, f" ({log_context})" if log_context else "")

            try:
                response = await self.client.post(
                    f"{self.BASE_URL}/chat/completions",
                    headers=self._build_headers(),
                    json=request_body_builder(model_name),
                )

                if response.status_code == 429:
                    error_detail = response.json().get("error", {}).get("message", "Rate limit exceeded")
                    logger.warning("Rate limit for %s: %s", model_name, error_detail)
                    if self.on_rate_limit:
                        self.on_rate_limit(model_name)
                    last_error = GroqRateLimitError(error_detail)
                    last_failed_model = model_name
                    continue

                response.raise_for_status()
                # Track fallback if we had to skip a model
                if last_failed_model and self.on_fallback:
                    self.on_fallback(last_failed_model, model_name, "rate_limit")
                logger.info("Successfully used model: %s%s", model_name, f" ({log_context})" if log_context else "")
                return response.json(), model_name

            except httpx.HTTPStatusError as e:
                logger.warning("HTTP error for %s: %s", model_name, e)
                last_error = e
                last_failed_model = model_name
                continue

        if last_error:
            raise last_error
        raise GroqRateLimitError("All models in fallback chain are rate limited")

    async def generate(
        self,
        prompt: str,
        model: str = "smart",
        system_prompt: str | None = None,
        temperature: float = 0.7,
        max_tokens: int = 4000,
        response_format: dict | None = None,
    ) -> tuple[str, dict[str, Any]]:
        """Generate text completion.

        Args:
            prompt: User prompt
            model: Model alias ("fast", "smart", "compound") or full model name
            system_prompt: Optional system prompt
            temperature: Sampling temperature (0-2)
            max_tokens: Maximum tokens to generate
            response_format: Optional response format (e.g. {"type": "json_object"})

        Returns:
            Tuple of (generated text, usage metadata)
        """
        messages = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})

        model_name = self.MODELS.get(model, model)

        payload: dict[str, Any] = {
            "model": model_name,
            "messages": messages,
            "temperature": temperature,
            "max_tokens": max_tokens,
        }
        if response_format:
            payload["response_format"] = response_format

        response = await self.client.post(
            f"{self.BASE_URL}/chat/completions",
            headers={
                "Authorization": f"Bearer {self.api_key}",
                "Content-Type": "application/json",
            },
            json=payload,
        )

        if response.status_code == 429:
            error_detail = response.json().get("error", {}).get("message", "Rate limit exceeded")
            logger.warning("Groq rate limit: %s", error_detail)
            if self.on_rate_limit:
                self.on_rate_limit(model_name)
            raise GroqRateLimitError(error_detail)

        response.raise_for_status()
        data = response.json()

        content = data["choices"][0]["message"]["content"]
        usage = data.get("usage", {})

        usage_info = {
            "promptTokens": usage.get("prompt_tokens"),
            "completionTokens": usage.get("completion_tokens"),
            "totalTokens": usage.get("total_tokens"),
        }

        return content, usage_info

    @staticmethod
    def _clean_and_parse_json(text: str) -> dict:
        """Clean markdown wrappers and parse JSON."""
        # Clean up potential markdown wrappers
        cleaned = text.strip()
        if cleaned.startswith("```"):
            # Remove ```json or ``` wrapper
            lines = cleaned.split("\n")
            if lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip() == "```":
                lines = lines[:-1]
            cleaned = "\n".join(lines)

        # Try to extract JSON object/array
        cleaned = cleaned.strip()

        # Find JSON boundaries
        if cleaned.startswith("{"):
            end = cleaned.rfind("}")
            if end != -1:
                cleaned = cleaned[: end + 1]
        elif cleaned.startswith("["):
            end = cleaned.rfind("]")
            if end != -1:
                cleaned = cleaned[: end + 1]

        return json.loads(cleaned)

    async def generate_json(
        self,
        prompt: str,
        model: str = "smart",
        max_retries: int = 3,
        max_tokens: int = 4000,
    ) -> tuple[dict, dict[str, Any]]:
        """Generate JSON response with automatic parsing, retry, and model fallback.

        Args:
            prompt: User prompt (should request JSON output)
            model: Model alias or full name
            max_retries: Number of retries on parse failure per model
            max_tokens: Maximum tokens to generate

        Returns:
            Tuple of (parsed JSON dict, usage metadata)
        """
        system_prompt = (
            "You are a helpful assistant. Always respond with valid JSON only. "
            "No markdown code blocks, no explanation, no extra text. Just pure JSON."
        )

        models_to_try = self._get_models_to_try(model)
        last_error: Exception | None = None
        total_usage = {"promptTokens": 0, "completionTokens": 0, "totalTokens": 0}

        for current_model in models_to_try:
            for attempt in range(max_retries):
                try:
                    result, usage = await self.generate(
                        prompt=prompt,
                        model=current_model,
                        system_prompt=system_prompt,
                        temperature=0.4,
                        max_tokens=max_tokens,
                        response_format={"type": "json_object"},
                    )

                    # Accumulate usage
                    for key in total_usage:
                        if usage.get(key):
                            total_usage[key] += usage[key]

                    parsed = self._clean_and_parse_json(result)
                    return parsed, total_usage

                except GroqRateLimitError as e:
                    logger.warning("Rate limit for %s, trying next model", current_model)
                    if self.on_rate_limit:
                        cm_name = self.MODELS.get(current_model, current_model)
                        self.on_rate_limit(cm_name)
                    last_error = e
                    break  # Skip retries, move to next model

                except json.JSONDecodeError as e:
                    last_error = e
                    logger.warning(
                        "JSON parse failed (model=%s, attempt %d/%d): %s",
                        current_model,
                        attempt + 1,
                        max_retries,
                        str(e)[:100],
                    )
                    if attempt < max_retries - 1:
                        continue

        if isinstance(last_error, GroqRateLimitError):
            raise last_error
        raise GroqJSONParseError(f"Failed to parse JSON after all attempts: {last_error}")

    async def chat(
        self,
        messages: list[dict],
        context: str | None = None,
        model: str = "smart",
        language: str = "en",
    ) -> tuple[str, dict[str, Any]]:
        """Chat completion with message history.

        Args:
            messages: List of message dicts with "role" and "content"
            context: Optional context to inject into system prompt
            model: Model alias or full name
            language: Language code for responses ("en", "ru", etc.)

        Returns:
            Tuple of (assistant response, usage metadata)
        """
        lang_hints = {
            "ru": "Отвечай на русском языке.",
            "en": "Respond in English.",
        }
        lang_hint = lang_hints.get(language, f"Respond in {language}.")

        system_prompt = (
            "You are an AI architect assistant helping developers plan their projects. "
            f"Be concise, practical, and beginner-friendly. {lang_hint}"
        )

        if context:
            system_prompt += f"\n\nCurrent context: {context}"

        formatted_messages = [{"role": "system", "content": system_prompt}]
        formatted_messages.extend(messages)

        def build_request(model_name: str) -> dict:
            return {
                "model": model_name,
                "messages": formatted_messages,
                "temperature": 0.7,
                "max_tokens": 2000,
            }

        data, model_name = await self._request_with_fallback(model, build_request, "chat mode")

        content = data["choices"][0]["message"]["content"]
        usage_info = self._extract_usage_info(data.get("usage", {}), model_name)

        return content, usage_info

    # amazonq-ignore-next-line
    async def chat_with_tools(
        self,
        messages: list[dict],
        tools: list[dict],
        context: str | None = None,
        system_prompt: str | None = None,
        model: str = "smart",
        tool_choice: str = "auto",
    ) -> tuple[str | None, list[dict] | None, dict[str, Any]]:
        """Chat completion with function calling (tools) support.

        Args:
            messages: List of message dicts with "role" and "content"
            tools: List of tool definitions in OpenAI function calling format
            context: Optional context to inject into system prompt
            system_prompt: Optional custom system prompt (overrides default)
            model: Model alias or full name
            tool_choice: "auto" (let model decide), "none", or {"type": "function", "function": {"name": "..."}}

        Returns:
            Tuple of (text response or None, tool_calls list or None, usage metadata)
            - If model responds with text: (text, None, usage)
            - If model calls tools: (None or text, [tool_calls], usage)
        """
        default_system_prompt = (
            "You are an AI architect assistant that can help developers manage their projects. "
            "You have access to tools that can modify project data (tasks, settings, etc). "
            "When the user asks you to perform an action (create task, update project, etc), "
            "use the appropriate tool. For questions and advice, respond with text. "
            "Be concise and practical."
        )

        final_system_prompt = system_prompt or default_system_prompt
        if context:
            final_system_prompt += f"\n\nCurrent context:\n{context}"

        formatted_messages = [{"role": "system", "content": final_system_prompt}]
        formatted_messages.extend(messages)

        def build_request(model_name: str) -> dict:
            return {
                "model": model_name,
                "messages": formatted_messages,
                "temperature": 0.3,
                "max_tokens": 2000,
                "tools": tools,
                "tool_choice": tool_choice,
            }

        data, model_name = await self._request_with_fallback(model, build_request, "tools mode")

        message = data["choices"][0]["message"]
        usage_info = self._extract_usage_info(data.get("usage", {}), model_name)

        content = message.get("content")
        tool_calls = self._parse_tool_calls(message.get("tool_calls"))

        return content, tool_calls, usage_info

    @staticmethod
    def _parse_tool_calls(tool_calls_raw: list[dict] | None) -> list[dict] | None:
        """Parse raw tool calls from API response."""
        if not tool_calls_raw:
            return None

        tool_calls = []
        for tc in tool_calls_raw:
            tool_call = {
                "id": tc.get("id"),
                "type": tc.get("type", "function"),
                "function": {
                    "name": tc["function"]["name"],
                    "arguments": tc["function"]["arguments"],
                }
            }
            try:
                tool_call["function"]["arguments_parsed"] = json.loads(tc["function"]["arguments"])
            except json.JSONDecodeError:
                tool_call["function"]["arguments_parsed"] = None
                logger.warning("Failed to parse tool arguments: %s", tc["function"]["arguments"])

            tool_calls.append(tool_call)

        return tool_calls


class GroqRateLimitError(Exception):
    """Raised when Groq API rate limit is exceeded."""
    pass


class GroqJSONParseError(Exception):
    """Raised when JSON parsing fails after retries."""
    pass
