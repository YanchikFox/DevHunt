"""
Pydantic models for AI endpoints.

Extracted from routers/ai.py to keep files under 500 LoC.
"""

from typing import Dict, List

from pydantic import AliasChoices, BaseModel, ConfigDict, Field


# ============================================================================
# Base Models
# ============================================================================


class StrictBaseModel(BaseModel):
    model_config = ConfigDict(extra="forbid", strict=True)


class StrictAliasModel(StrictBaseModel):
    model_config = ConfigDict(extra="forbid", strict=True, populate_by_name=True)


# ============================================================================
# Tech Stack Models
# ============================================================================


class TechStackGoals(StrictBaseModel):
    performance: bool = False
    cost: bool = False
    developerSpeed: bool = Field(
        default=False,
        validation_alias=AliasChoices("developerSpeed", "developer_speed"),
    )
    scalability: bool = False
    security: bool = False
    maintainability: bool = False


class TechStackRequest(StrictBaseModel):
    idea: str
    goals: TechStackGoals | None = None


class TechStackBackend(BaseModel):
    """Backend technology details."""

    model_config = ConfigDict(extra="allow")
    language: str
    framework: str


class TechStackDatabase(BaseModel):
    """Database details with reasoning."""

    model_config = ConfigDict(extra="allow")
    primary: str
    why: str | None = None


class TechStackOption(BaseModel):
    """Single tech stack option with detailed breakdown."""

    model_config = ConfigDict(extra="allow")

    name: str
    description: str
    pros: List[str]
    cons: List[str]

    # Structured fields (optional for backwards compat)
    architecture: str | None = None
    backend: TechStackBackend | None = None
    frontend: Dict[str, str] | None = None
    database: TechStackDatabase | None = None
    components: List[str] | None = None
    why_this_fits: List[str] | None = Field(
        default=None,
        validation_alias=AliasChoices("why_this_fits", "whyThisFits"),
    )


class TechStackResponse(BaseModel):
    """Response with tech stack options."""

    model_config = ConfigDict(extra="allow")

    version: str
    promptVersion: str | None = None
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None
    options: List[TechStackOption]


# ============================================================================
# Plan Models
# ============================================================================


class PlanRequest(StrictAliasModel):
    idea: str
    techStack: str = Field(validation_alias=AliasChoices("techStack", "tech_stack"))
    customTags: List[str] | None = Field(
        default=None,
        validation_alias=AliasChoices("customTags", "custom_tags"),
    )
    customRoles: List[str] | None = Field(
        default=None,
        validation_alias=AliasChoices("customRoles", "custom_roles"),
    )
    goals: TechStackGoals | None = None


class PlanTask(StrictAliasModel):
    id: str
    title: str
    description: str = ""
    priority: str = "medium"
    tags: List[str] = Field(default_factory=list)
    dependsOn: List[str] = Field(
        default_factory=list,
        validation_alias=AliasChoices("dependsOn", "depends_on"),
    )


class PlanPhase(StrictBaseModel):
    id: str
    name: str
    description: str
    goals: List[str] = Field(default_factory=list)
    tasks: List[PlanTask] = Field(default_factory=list)


class PlanDraft(StrictBaseModel):
    version: str
    idea: str
    techStack: str
    promptVersion: str | None = None
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None
    phases: List[PlanPhase]


class RoadmapPhase(StrictBaseModel):
    phase: str
    description: str
    goals: List[str]


class TaskGenerationRequest(StrictAliasModel):
    idea: str
    phase: str
    techStack: str = Field(validation_alias=AliasChoices("techStack", "tech_stack"))


class TaskItem(StrictBaseModel):
    title: str
    description: str
    dependencies: List[str]


# ============================================================================
# Refinement Models
# ============================================================================


class RefinePlanRequest(StrictAliasModel):
    """Request to refine an existing plan based on user instructions."""

    idea: str
    techStack: str = Field(validation_alias=AliasChoices("techStack", "tech_stack"))
    currentPlan: dict = Field(
        validation_alias=AliasChoices("currentPlan", "current_plan"),
    )
    instructions: str
    customTags: List[str] | None = Field(
        default=None,
        validation_alias=AliasChoices("customTags", "custom_tags"),
    )
    customRoles: List[str] | None = Field(
        default=None,
        validation_alias=AliasChoices("customRoles", "custom_roles"),
    )
    goals: TechStackGoals | None = None


class RefineTechStackRequest(StrictBaseModel):
    """Request to refine existing tech stack options based on user instructions."""

    idea: str
    currentOptions: List[dict] = Field(
        validation_alias=AliasChoices("currentOptions", "current_options"),
    )
    instructions: str
    goals: TechStackGoals | None = None


# ============================================================================
# Diagram Models
# ============================================================================


class DiagramRequest(StrictAliasModel):
    """Request for generating architecture diagram."""

    techStack: str = Field(validation_alias=AliasChoices("techStack", "tech_stack"))
    idea: str | None = None
    format: str = "mermaid"
    diagramType: str = Field(
        default="architecture",
        validation_alias=AliasChoices("diagramType", "diagram_type"),
    )
    projectContext: str | None = Field(
        default=None,
        validation_alias=AliasChoices("projectContext", "project_context"),
    )


class DiagramResponse(StrictBaseModel):
    """Response with generated diagram code."""

    code: str
    format: str
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None


# ============================================================================
# Project Passport Models
# ============================================================================


class PassportPlanPhase(BaseModel):
    """Simplified plan phase for passport generation context."""

    model_config = ConfigDict(extra="allow")
    name: str
    description: str
    goals: List[str] = Field(default_factory=list)
    taskCount: int = 0


class PassportRequest(StrictAliasModel):
    """Request to generate a full project passport with all sections."""

    idea: str
    techStack: str = Field(validation_alias=AliasChoices("techStack", "tech_stack"))
    description: str | None = None
    phases: List[PassportPlanPhase] = Field(default_factory=list)
    language: str = "en"
    documentsContext: str | None = Field(
        default=None,
        validation_alias=AliasChoices("documentsContext", "documents_context"),
    )


class PassportSection(StrictBaseModel):
    """Single section of the generated passport."""

    type: str
    title: str
    content: str


class PassportResponse(StrictBaseModel):
    """Response containing all generated passport sections."""

    sections: List[PassportSection]
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None


# ============================================================================
# Chat Models
# ============================================================================


class ChatMessage(StrictBaseModel):
    """Single chat message."""

    role: str
    content: str


class ChatRequest(StrictAliasModel):
    """Request for AI chat."""

    message: str
    history: List[ChatMessage] = Field(default_factory=list)
    context: str | None = None
    projectId: str | None = Field(
        default=None,
        validation_alias=AliasChoices("projectId", "project_id"),
    )
    enableTools: bool = Field(
        default=False,
        validation_alias=AliasChoices("enableTools", "enable_tools"),
    )
    language: str = "en"


class ChatResponse(StrictBaseModel):
    """Response from AI chat."""

    message: str
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None


# ============================================================================
# Agent Chat Models
# ============================================================================


class ToolCallFunction(StrictBaseModel):
    """Function details of a tool call."""

    name: str
    arguments: str
    arguments_parsed: dict | None = None


class ToolCall(StrictBaseModel):
    """Single tool call from AI."""

    id: str
    type: str = "function"
    function: ToolCallFunction


class ToolDataPayload(StrictBaseModel):
    """Data payload for on-demand tool resolution.
    
    Contains full task details, ID mappings, and passport that are NOT
    included in the system prompt. Query tools resolve from this data.
    """

    tasks: List[dict] = Field(default_factory=list)
    taskIdMap: dict[str, str] = Field(default_factory=dict)
    teamMemberIdMap: dict[str, str] = Field(default_factory=dict)
    columns: List[dict] = Field(default_factory=list)
    passport: List[dict] = Field(default_factory=list)


class AgentChatRequest(StrictAliasModel):
    """Request for agentic AI chat."""

    message: str
    history: List[ChatMessage] = Field(default_factory=list)
    context: str | None = None
    toolData: ToolDataPayload | None = Field(
        default=None,
        validation_alias=AliasChoices("toolData", "tool_data"),
    )
    projectId: str | None = Field(
        default=None,
        validation_alias=AliasChoices("projectId", "project_id"),
    )
    enableTools: bool = Field(
        default=True,
        validation_alias=AliasChoices("enableTools", "enable_tools"),
    )
    language: str = "ru"


class AgentChatResponse(StrictBaseModel):
    """Response from agentic AI chat."""

    message: str | None = None
    toolCalls: List[ToolCall] | None = Field(
        default=None,
        validation_alias=AliasChoices("toolCalls", "tool_calls"),
    )
    hasToolCalls: bool = Field(
        default=False,
        validation_alias=AliasChoices("hasToolCalls", "has_tool_calls"),
    )
    provider: str | None = None
    model: str | None = None
    promptTokens: int | None = None
    completionTokens: int | None = None
    totalTokens: int | None = None
