"""
Diagram generation service — prompt building, Mermaid sanitization, generation.

Extracted from routers/ai.py to improve cohesion and reduce complexity.
"""

import asyncio
import logging
import re
import time
from dataclasses import dataclass

from fastapi import HTTPException

from clients.groq_client import GroqClient, GroqRateLimitError
from models.ai_models import DiagramRequest, DiagramResponse
from metrics import AI_MODEL_LATENCY
from services.ai_service import (
    DEFAULT_PROVIDER,
    require_groq_client,
    emit_token_metrics,
)

logger = logging.getLogger(__name__)

# ============================================================================
# Diagram Type Labels
# ============================================================================

DIAGRAM_TYPE_LABELS: dict[str, str] = {
    "architecture": "System Architecture",
    "sequence": "Sequence Diagram",
    "erd": "Entity-Relationship Diagram",
    "user_flow": "User Flow",
    "deployment": "Deployment Architecture",
    "state": "State Machine / Lifecycle",
}

# ============================================================================
# Per-type Mermaid instructions
# ============================================================================

_MERMAID_TYPE_PROMPTS: dict[str, str] = {
    "architecture": """Generate a Mermaid 'flowchart TD' system architecture diagram.
Rules:
- Use subgraphs to group by layer: "Client Layer", "API Gateway", "Backend Services", "Data Layer", "External Services".
- Shapes: ["label"] for UI/client, ("label") for services/APIs, [("label")] for queues, [["label"]] for databases.
- LABEL every arrow with protocol: |REST|, |WebSocket|, |SQL|, |Pub/Sub|, |gRPC|, |Redis|, etc.
- Show 8-15 meaningful nodes reflecting the ACTUAL tech stack. Break services into sub-components.
- Include authentication flow, caching, message broker, background workers if the stack implies them.
- Add classDef: classDef client fill:#4F46E5,stroke:#3730A3,color:#fff; classDef service fill:#0D9488,stroke:#0F766E,color:#fff; classDef db fill:#D97706,stroke:#B45309,color:#fff; classDef queue fill:#7C3AED,stroke:#6D28D9,color:#fff; classDef external fill:#6B7280,stroke:#4B5563,color:#fff;
- Apply classes using :::className.
- In 'class' assignments, ALWAYS separate node IDs with commas: class nodeA,nodeB,nodeC className; NEVER use spaces between node IDs.
SYNTAX WARNINGS:
- NEVER use parentheses () inside square brackets []. Wrong: node[CDN (Static)]. Correct: node["CDN - Static Assets"].
- NEVER use reserved words as class names: end, start, default. Use 'finish', 'begin', 'dflt' instead.
- Always quote labels containing special characters using double quotes: node["My Label (info)"].
- In subgraph titles, always use double quotes: subgraph "Edge / CDN".
Start with: flowchart TD""",

    "sequence": """Generate a Mermaid sequence diagram showing the main user interaction flow.
Rules:
- Use 'sequenceDiagram' syntax.
- Show at least 5-8 participants (User, Frontend, API Gateway, Backend Service, Database, Message Broker, External Service).
- Model a realistic flow (e.g., user creates a project, authentication happens, data is saved, events are published, notifications sent).
- Use activate/deactivate for service lifetimes.
- Use alt/opt/loop blocks for conditional and repeated flows.
- CRITICAL: 'else' keyword is ONLY valid inside 'alt' blocks. NEVER use 'else' inside 'opt' blocks. If you need two branches, use 'alt' instead of 'opt'.
- Add notes with 'Note over' or 'Note right of' for important details.
- Label messages with HTTP methods and paths where relevant: e.g., POST /api/projects.
- Show at least 15-25 messages in the sequence.
Start with: sequenceDiagram""",

    "erd": """Generate a Mermaid ER diagram showing the data model.
Rules:
- Use 'erDiagram' syntax.
- Show at least 8-12 entities with their key attributes.
- CRITICAL ATTRIBUTE SYNTAX: Each attribute line MUST follow format: type name constraint
  Correct: string email PK
  Correct: uuid id PK
  Correct: string name
  Correct: datetime created_at
  Correct: uuid user_id FK
  WRONG: uuid PK  (missing attribute name!)
  WRONG: PK uuid  (wrong order!)
- Valid types: string, int, uuid, datetime, boolean, text, jsonb, date, float, enum, bigint
- Valid constraints: PK, FK, UK (or omit for no constraint)
- Use proper cardinality: ||--o{, ||--|{, }o--o{, etc.
- Include entities like: Users, Projects, Tasks, TeamMembers, Skills, Notifications, Integrations, Reviews, etc.
- Each entity should have 5-10 attributes.
- Label relationships with verbs: "creates", "owns", "belongs to", "assigned to", "has", "sends", etc.
- Group related entities logically.
Start with: erDiagram""",

    "user_flow": """Generate a Mermaid flowchart showing a key user journey.
Rules:
- Use 'flowchart TD' syntax.
- Model the PRIMARY user flow (registration → project creation → team building → collaboration → showcase).
- Use diamond shapes for decisions {"label"}, rounded rectangles for actions ("label"), rectangles for states ["label"].
- Show 15-25 steps with meaningful decision branches.
- Include error/alternative paths (e.g., validation failed, access denied).
- Add notes as subgraphs to group flow stages: "Registration", "Project Setup", "Team Building", "Development", "Completion".
- Use descriptive labels on arrows showing conditions.
- Add classDef: classDef begin fill:#22C55E,stroke:#16A34A,color:#fff; classDef decision fill:#F59E0B,stroke:#D97706,color:#fff; classDef action fill:#3B82F6,stroke:#2563EB,color:#fff; classDef finish fill:#EF4444,stroke:#DC2626,color:#fff;
SYNTAX WARNINGS:
- NEVER use 'start' or 'end' as class names — they are reserved words in Mermaid. Use 'begin' and 'finish'.
- NEVER use :::end or :::start. Use :::finish and :::begin instead.
- Always quote labels containing special characters: ["My Label"].
Start with: flowchart TD""",

    "deployment": """Generate a Mermaid flowchart showing the deployment/infrastructure architecture.
Rules:
- Use 'flowchart LR' (left-to-right) syntax for deployment layout.
- Show infrastructure layers: Internet/CDN → Load Balancer → Application Cluster → Data Layer → Monitoring.
- Use subgraphs for: "Internet", "Edge / CDN", "Application Cluster", "Data Layer", "Monitoring & Observability".
- Show database replicas, caching layers, message brokers, object storage.
- Include CI/CD pipeline elements if relevant.
- Label connections with ports and protocols: |:3000|, |:5432 SQL|, |:6379 Redis|, |HTTPS :443|.
- Show at least 10-15 nodes including specific technologies.
- Add classDef: classDef internet fill:#6366F1,stroke:#4F46E5,color:#fff; classDef edge fill:#EC4899,stroke:#DB2777,color:#fff; classDef app fill:#0D9488,stroke:#0F766E,color:#fff; classDef data fill:#D97706,stroke:#B45309,color:#fff; classDef monitor fill:#8B5CF6,stroke:#7C3AED,color:#fff;
SYNTAX WARNINGS:
- NEVER use parentheses () inside square brackets []. Wrong: node[CDN (Static)]. Correct: node["CDN - Static Assets"].
- NEVER use reserved words as class names: end, start, default. 
- Always quote labels containing special characters: node["My Label"], subgraph "My Group".
- For node labels with parens, use double quotes: cdn["CDN - Static Assets"] NOT cdn[CDN (Static Assets)].
Start with: flowchart LR""",

    "state": """Generate a Mermaid state diagram showing the lifecycle of a core entity.
Rules:
- Use 'stateDiagram-v2' syntax.
- Model the project lifecycle: Draft → Recruiting → Active → Completing → Completed → Archived.
- Also consider: OnHold, Cancelled as states.
- Use composite states with nested states (e.g., Active contains Planning, Development, Review, Testing).
- Show transitions with meaningful labels: "publish", "team formed", "pause", "resume", "complete", "archive".
- Add notes using 'note right of' or 'note left of' to explain key states.
- Show at least 8-12 states with clear transitions between them.
- Include fork/choice states for conditional transitions.
Start with: stateDiagram-v2""",
}

# ============================================================================
# Per-type PlantUML instructions
# ============================================================================

_PLANTUML_TYPE_PROMPTS: dict[str, str] = {
    "architecture": """Generate a PlantUML component/deployment diagram.
Rules:
- Use packages for logical grouping: "Client Layer", "API Gateway", "Backend Services", "Data Layer".
- Use [ComponentName] for components, database "Name" for databases, queue "Name" for message brokers.
- Show 8-15 components with labeled arrows (REST, WebSocket, SQL, Pub/Sub, etc.).
- Add skinparam for clean styling: skinparam componentStyle rectangle, left to right direction.
- Include:  authentication, caching, background workers if the stack implies them.
- Use stereotypes: <<Frontend>>, <<API>>, <<Service>>, <<Database>>, <<Cache>>.
- Use color coding with skinparam: BackgroundColor<<Frontend>> LightBlue, BackgroundColor<<Service>> LightGreen, etc.
Start with: @startuml""",

    "sequence": """Generate a PlantUML sequence diagram.
Rules:
- Show 5-8 participants: actor "User", participant "Frontend", participant "API Gateway", participant "Core API", participant "Database", participant "Message Broker", participant "ML Service".
- Model a complete user interaction with 15-25 messages.
- Use 'activate'/'deactivate' for service lifetimes.
- Group phases with == Phase Name ==.
- Use alt/else for conditional flows, loop for repeated actions.
- Add notes with 'note right' and 'note over' for important details.
- Use 'skinparam sequenceMessageAlign center'.
- Label messages with HTTP methods: POST /api/projects, GET /api/users/{id}.
Start with: @startuml""",

    "erd": """Generate a PlantUML class diagram as an Entity-Relationship diagram.
Rules:
- Use 'entity' keyword for each entity, with proper PK/FK notation using * for required and -- for separator.
- Show 8-12 entities with 5-10 attributes each (id, timestamps, enums, relations).
- Use proper PlantUML cardinality: ||--o{, ||--|{, }o..o{.
- Add skinparam for styling: skinparam class BackgroundColor<<Entity>> LightGreen, linetype ortho.
- Include color coding by domain: <<User>> LightBlue, <<Project>> LightGreen, <<System>> LightGray.
- Label relationships with verbs on arrows.
Start with: @startuml""",

    "user_flow": """Generate a PlantUML activity diagram for a user journey.
Rules:
- Use activity diagram syntax with :Action;, if/else/endif, fork/fork again/end fork.
- Model the primary user flow with 15-25 steps, meaningful decisions, and parallel paths.
- Use 'partition "Section Name"' to group related activities.
- Add 'note right' blocks to provide context at key steps.
- Use skinparam for clean styling: skinparam activityBorderColor #2C3E50, skinparam activityDiamondBorderColor #E74C3C.
- Include error/alternative paths.
- Use start/stop keywords.
Start with: @startuml""",

    "deployment": """Generate a PlantUML deployment diagram.
Rules:
- Use 'cloud', 'node', 'database', 'folder', 'component', 'package' for infrastructure elements.
- Show layers: Internet → Edge/CDN → Load Balancer → Application Cluster → Data Layer → Monitoring.
- Show replicas for databases and app instances.
- Use 'left to right direction' for deployment layout.
- Label connections with ports and protocols.
- Add 'note right of' and 'note bottom of' for specs (ports, scaling, health checks).
- Include auto-scaling notes.
Start with: @startuml""",

    "state": """Generate a PlantUML state diagram.
Rules:
- Use 'state' keyword with nested states for composite states.
- Model entity lifecycle with 8-12 states and clear transitions.
- Use [*] for start/end states.
- Add 'state : description' for state details.
- Use nested 'state Parent { ... }' for composite states with internal substates and transitions.
- Label transitions with trigger actions.
- Use 'hide empty description' for cleaner output.
Start with: @startuml""",
}


# ============================================================================
# Mermaid Sanitization — decomposed into focused fix steps
# ============================================================================

_CLASSDEF_END_RE = re.compile(r'\bclassDef\s+end\b')
_CLASSDEF_START_RE = re.compile(r'\bclassDef\s+start\b')
_REF_END_RE = re.compile(r':::end\b')
_REF_START_RE = re.compile(r':::start\b')
_ER_ATTR_RE = re.compile(r'^(\s+)(\w+)\s+(PK|FK|UK)$')
_PAREN_IN_BRACKET_RE = re.compile(r'^(\s*\w+)\[([^\]"]*\([^\]]*\)[^\]"]*)\]')
# Matches: class nodeA nodeB nodeC className; (space-separated IDs instead of commas)
_CLASS_ASSIGN_RE = re.compile(r'^(\s*class\s+)([\w,][\w, ]*?)\s+([\w]+)\s*;?\s*$')


def _fix_reserved_classdef(line: str) -> str:
    """Replace reserved words 'end'/'start' in classDef and ::: references."""
    if "classDef" in line:
        line = _CLASSDEF_END_RE.sub('classDef finish', line)
        line = _CLASSDEF_START_RE.sub('classDef begin', line)
    line = _REF_END_RE.sub(':::finish', line)
    line = _REF_START_RE.sub(':::begin', line)
    return line


def _fix_er_attribute(line: str) -> str:
    """Fix ER diagram attributes missing name: 'uuid PK' → 'uuid id PK'."""
    match = _ER_ATTR_RE.match(line)
    if not match:
        return line
    indent, attr_type, constraint = match.groups()
    name = "id" if constraint == "PK" else f"{attr_type}_ref"
    return f"{indent}{attr_type} {name} {constraint}"


def _fix_paren_in_brackets(line: str) -> str:
    """Fix parentheses inside square brackets: node[Label (info)] → node["Label - info"]."""
    match = _PAREN_IN_BRACKET_RE.match(line)
    if not match:
        return line
    prefix = match.group(1)
    label = match.group(2)
    clean_label = label.replace("(", "- ").replace(")", "")
    return f'{prefix}["{clean_label.strip()}"]'


def _fix_class_assignment(line: str) -> str:
    """Fix 'class nodeA nodeB className;' → 'class nodeA,nodeB className;'.

    The LLM sometimes space-separates node IDs in class assignments instead of
    using commas, which causes Mermaid parse errors.
    """
    stripped = line.strip()
    if not stripped.startswith("class "):
        return line
    match = _CLASS_ASSIGN_RE.match(line)
    if not match:
        return line
    prefix = match.group(1)     # "    class "
    ids_part = match.group(2)   # "nodeA nodeB nodeC" or "nodeA,nodeB"
    class_name = match.group(3) # "className"
    # Split by commas and/or spaces, filter empty, rejoin with commas
    ids = [tok.strip() for tok in re.split(r'[,\s]+', ids_part) if tok.strip()]
    if len(ids) < 1:
        return line
    return f"{prefix}{','.join(ids)} {class_name};"


def _fix_opt_else_blocks(code: str) -> str:
    """Fix 'else' inside 'opt' blocks → convert 'opt' to 'alt'.

    Mermaid only allows 'else' inside 'alt' blocks. When the LLM generates
    'opt ... else ...', we convert the 'opt' to 'alt' so it's valid syntax.
    """
    lines = code.split("\n")
    block_stack: list[tuple[int, str]] = []  # (line_index, block_type)
    opt_to_alt_lines: set[int] = set()

    for i, raw in enumerate(lines):
        stripped = raw.strip()
        if stripped.startswith("alt ") or stripped == "alt":
            block_stack.append((i, "alt"))
        elif stripped.startswith("opt ") or stripped == "opt":
            block_stack.append((i, "opt"))
        elif stripped.startswith("else") and block_stack:
            parent_idx, parent_type = block_stack[-1]
            if parent_type == "opt":
                opt_to_alt_lines.add(parent_idx)
        elif stripped == "end" and block_stack:
            block_stack.pop()

    if not opt_to_alt_lines:
        return code

    for idx in opt_to_alt_lines:
        lines[idx] = lines[idx].replace("opt ", "alt ", 1).replace("opt\n", "alt\n", 1)

    return "\n".join(lines)


def _is_er_content_line(stripped: str) -> bool:
    """Return True if the stripped line is an ER attribute (not a block delimiter)."""
    return bool(stripped) and not stripped.endswith("{") and stripped != "}"


def _track_er_block(stripped: str, in_er_entity: bool) -> bool:
    """Update ER entity block tracking state."""
    if stripped.endswith("{") and not stripped.startswith("subgraph"):
        return True
    if stripped == "}":
        return False
    return in_er_entity


def sanitize_mermaid_code(code: str) -> str:
    """Auto-fix common Mermaid syntax issues that cause parse errors.

    Applies focused fix steps per line, tracking ER entity block context,
    then applies multi-line fixes (opt/else → alt/else).
    """
    lines = code.split("\n")
    fixed_lines: list[str] = []
    in_er_entity = False

    for line in lines:
        line = _fix_reserved_classdef(line)
        stripped = line.strip()
        in_er_entity = _track_er_block(stripped, in_er_entity)

        if in_er_entity and _is_er_content_line(stripped):
            line = _fix_er_attribute(line)

        line = _fix_paren_in_brackets(line)
        line = _fix_class_assignment(line)
        fixed_lines.append(line)

    result = "\n".join(fixed_lines)
    result = _fix_opt_else_blocks(result)
    return result


def strip_markdown_code_block(code: str) -> str:
    """Remove markdown code block wrappers from AI response."""
    code = code.strip()
    if not code.startswith("```"):
        return code
    lines = code.split("\n")
    if lines[0].startswith("```"):
        lines = lines[1:]
    if lines and lines[-1].strip() == "```":
        lines = lines[:-1]
    return "\n".join(lines).strip()


# ============================================================================
# Prompt Building
# ============================================================================

@dataclass(frozen=True)
class DiagramPromptParams:
    """Groups parameters for diagram prompt building (reduces arg count)."""
    tech_stack: str
    idea: str | None
    fmt: str
    diagram_type: str
    project_context: str | None


def build_diagram_prompt(params: DiagramPromptParams) -> str:
    """Build a detailed, type-specific prompt for diagram generation."""
    type_label = DIAGRAM_TYPE_LABELS.get(params.diagram_type, "Architecture")

    type_prompts = (
        _PLANTUML_TYPE_PROMPTS if params.fmt == "plantuml" else _MERMAID_TYPE_PROMPTS
    )
    format_instruction = type_prompts.get(
        params.diagram_type, type_prompts.get("architecture", "")
    )

    context_section = ""
    if params.project_context:
        ctx = params.project_context[:4000]
        context_section = (
            f"\n\nPROJECT CONTEXT (use this to make the diagram specific to THIS project, not generic):\n{ctx}"
        )

    idea_context = f"\nProject idea/description: {params.idea}" if params.idea else ""

    return f"""You are generating a DETAILED, production-quality {type_label} diagram.

SELECTED TECH STACK (use ONLY these technologies):
{params.tech_stack}
{idea_context}{context_section}

CRITICAL RULES:
1. Every component/entity MUST correspond to a technology or concept from the project. Do NOT invent unrelated items.
2. The diagram must be SPECIFIC to this project — not a generic template.
3. Include real entity names, real service names, real DB tables from the project.
4. The quality should match what a senior architect would produce for documentation.
5. Be DETAILED — more nodes/entities/steps is better than fewer.
6. Use proper syntax — the output must render without errors.

MERMAID SYNTAX SAFETY (applies to all diagram types):
- NEVER use parentheses () inside square brackets []. Use double-quoted labels: node["My Label - info"].
- NEVER use 'start' or 'end' as classDef names — they are RESERVED WORDS. Use 'begin'/'finish' instead.
- For ER diagrams: attribute format is ALWAYS 'type name constraint'. Example: string email PK, uuid id PK. NEVER write 'uuid PK' without a name.
- Always double-quote labels containing special chars: node["Label (with parens)"], subgraph "My Group".
- In flowcharts, never write :::end or :::start — use :::finish or :::begin.
- In 'class' statements, ALWAYS use commas between node IDs: 'class nodeA,nodeB className;'. NEVER space-separate: 'class nodeA nodeB className;' is INVALID.
- In sequence diagrams, 'else' is ONLY valid inside 'alt' blocks. NEVER use 'else' inside 'opt'. Use 'alt' if you need two branches.

{format_instruction}

Return ONLY the diagram code, no explanation, no markdown code blocks."""


# ============================================================================
# Diagram Generation
# ============================================================================


async def generate_diagram_from_request(request: DiagramRequest) -> DiagramResponse:
    """Generate architecture diagram in Mermaid or PlantUML format."""
    client = require_groq_client()
    params = DiagramPromptParams(
        tech_stack=request.techStack,
        idea=request.idea,
        fmt=request.format,
        diagram_type=request.diagramType,
        project_context=request.projectContext,
    )
    prompt = build_diagram_prompt(params)
    type_label = DIAGRAM_TYPE_LABELS.get(request.diagramType, "Architecture")

    max_retries = 3
    for attempt in range(max_retries):
        try:
            start = time.perf_counter()
            result, usage = await client.generate(
                prompt=prompt,
                model="smart",
                system_prompt=(
                    f"You are a senior software architect who creates detailed, production-quality "
                    f"{type_label} diagrams. Use ONLY the technologies from the user's tech stack. "
                    f"NEVER invent or add unlisted technologies. Create diagrams that are specific "
                    f"to the project described, not generic templates. "
                    f"Output ONLY valid {'PlantUML' if request.format == 'plantuml' else 'Mermaid'} code, nothing else."
                ),
                temperature=0.3,
                max_tokens=4000,
            )
            model_name = client.MODELS.get("smart", "smart")
            AI_MODEL_LATENCY.labels(model=model_name, endpoint="diagram").observe(
                time.perf_counter() - start
            )
            emit_token_metrics(usage, "diagram", model_name)

            raw_code = strip_markdown_code_block(result)
            if request.format == "mermaid":
                raw_code = sanitize_mermaid_code(raw_code)

            return DiagramResponse(
                code=raw_code,
                format=request.format,
                provider=DEFAULT_PROVIDER,
                model=model_name,
                promptTokens=usage.get("promptTokens"),
                completionTokens=usage.get("completionTokens"),
                totalTokens=usage.get("totalTokens"),
            )
        except GroqRateLimitError as e:
            if attempt < max_retries - 1:
                wait = (attempt + 1) * 10  # 10s, 20s
                logger.warning("Rate limit hit, retrying in %ds (attempt %d/%d)", wait, attempt + 1, max_retries)
                await asyncio.sleep(wait)
                continue
            logger.error("Rate limit exceeded after %d retries", max_retries)
            raise HTTPException(
                status_code=429, detail="AI service rate limit exceeded, please try again later"
            ) from e
        except Exception as e:
            logger.exception("Diagram generation failed")
            raise HTTPException(
                status_code=500, detail="Failed to generate diagram"
            ) from e
