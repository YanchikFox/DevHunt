using System.Text.Json;
using DevHunt.CoreApi.Services.Ai.Llm.Models;

namespace DevHunt.CoreApi.Services.Ai.Llm;

/// <summary>
/// Static catalog of JSON-schema tool definitions exposed to the LLM during project chat.
/// Filtered per skill by <see cref="IAiSkillResolver"/> before being sent to providers.
/// </summary>
public static class LlmToolCatalog
{
    /// <summary>
    /// Returns all project-scoped tools (task CRUD, project updates, memory, documents).
    /// Individual skills may expose only a subset via <see cref="AiSkillProfile.AllowedTools"/>.
    /// </summary>
    public static IReadOnlyList<LlmToolDefinition> GetProjectTools()
    {
        return new[]
        {
            Tool("create_task", "Create a new project task.", """
            {
              "type": "object",
              "properties": {
                "title": { "type": "string" },
                "description": { "type": "string" },
                "priority": { "type": "string", "enum": ["low", "medium", "high", "urgent"] },
                "columnId": { "type": "string" },
                "assigneeId": { "type": "string" },
                "tags": { "type": "array", "items": { "type": "string" } },
                "deadline": { "type": "string", "description": "ISO 8601 date, YYYY-MM-DD" },
                "estimatedHours": { "type": "number" }
              },
              "required": ["title"]
            }
            """),
            Tool("update_task", "Update an existing task by id.", """
            {
              "type": "object",
              "properties": {
                "taskId": { "type": "string" },
                "title": { "type": "string" },
                "description": { "type": "string" },
                "priority": { "type": "string", "enum": ["low", "medium", "high", "urgent"] },
                "assigneeId": { "type": "string" },
                "tags": { "type": "array", "items": { "type": "string" } },
                "deadline": { "type": "string" },
                "estimatedHours": { "type": "number" },
                "actualHours": { "type": "number" }
              },
              "required": ["taskId"]
            }
            """),
            Tool("delete_task", "Delete a task by id.", """
            {
              "type": "object",
              "properties": {
                "taskId": { "type": "string" }
              },
              "required": ["taskId"]
            }
            """),
            Tool("move_task", "Move a task to another column.", """
            {
              "type": "object",
              "properties": {
                "taskId": { "type": "string" },
                "columnId": { "type": "string" },
                "columnName": { "type": "string" },
                "position": { "type": "integer" }
              },
              "required": ["taskId"]
            }
            """),
            Tool("create_multiple_tasks", "Create multiple tasks at once.", """
            {
              "type": "object",
              "properties": {
                "tasks": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "title": { "type": "string" },
                      "description": { "type": "string" },
                      "priority": { "type": "string", "enum": ["low", "medium", "high", "urgent"] },
                      "tags": { "type": "array", "items": { "type": "string" } }
                    },
                    "required": ["title"]
                  }
                },
                "columnId": { "type": "string" }
              },
              "required": ["tasks"]
            }
            """),
            Tool("delete_multiple_tasks", "Bulk delete tasks. Use filter all or column when ids are not known.", """
            {
              "type": "object",
              "properties": {
                "taskIds": { "type": "array", "items": { "type": "string" } },
                "filter": { "type": "string", "enum": ["all", "column"] },
                "columnName": { "type": "string" }
              }
            }
            """),
            Tool("move_multiple_tasks", "Bulk move tasks to another column.", """
            {
              "type": "object",
              "properties": {
                "taskIds": { "type": "array", "items": { "type": "string" } },
                "filter": { "type": "string", "enum": ["all", "column"] },
                "sourceColumnName": { "type": "string" },
                "targetColumnName": { "type": "string" },
                "targetColumnId": { "type": "string" }
              },
              "required": ["targetColumnName"]
            }
            """),
            Tool("update_project", "Update project settings such as description, stack, status, visibility.", """
            {
              "type": "object",
              "properties": {
                "description": { "type": "string" },
                "shortDescription": { "type": "string" },
                "techStack": { "type": "array", "items": { "type": "string" } },
                "status": { "type": "string", "enum": ["draft", "recruiting", "in_progress", "completed", "on_hold", "cancelled"] },
                "visibility": { "type": "string", "enum": ["public", "private", "team_only"] },
                "difficultyLevel": { "type": "string", "enum": ["beginner", "intermediate", "advanced"] },
                "maxTeamSize": { "type": "integer" }
              }
            }
            """),
            Tool("generate_news_draft", "Signal intent to generate project news. Write the draft in your message.", """
            {
              "type": "object",
              "properties": {
                "type": { "type": "string", "enum": ["release", "update", "milestone", "announcement", "welcome", "recruitment"] },
                "topic": { "type": "string" },
                "tone": { "type": "string", "enum": ["professional", "casual", "excited", "formal"] },
                "includeCallToAction": { "type": "boolean" }
              },
              "required": ["type", "topic"]
            }
            """),
            Tool("record_decision", "Persist a meaningful decision or insight to the project's long-term memory. Call this when something worth remembering happened: an architectural choice, a process agreement, a non-obvious technical fact. One short sentence; pick a category.", """
            {
              "type": "object",
              "properties": {
                "summary": { "type": "string", "description": "One sentence (max ~280 chars). What and why." },
                "category": { "type": "string", "enum": ["decision", "action", "technical"], "description": "decision = product/architecture choice; action = AI-driven mutation; technical = non-obvious fact for future reference." }
              },
              "required": ["summary"]
            }
            """),
            Tool("read_document", "Fetch the full content of a project document. Pass documentId (UUID from the project context) when known; otherwise pass title (case-insensitive match against the document name). Use only when the title in the context isn't enough to answer.", """
            {
              "type": "object",
              "properties": {
                "documentId": { "type": "string", "description": "UUID of the document, exactly as listed under 'Available documents'. Preferred when known." },
                "title": { "type": "string", "description": "Document name; case-insensitive. Used as a fallback when documentId isn't known. Must uniquely identify a document in the project." }
              }
            }
            """),
        };
    }

    /// <summary>Parses embedded JSON schema text into a <see cref="LlmToolDefinition"/>.</summary>
    private static LlmToolDefinition Tool(string name, string description, string schemaJson)
    {
        using var doc = JsonDocument.Parse(schemaJson);
        return new LlmToolDefinition(name, description, doc.RootElement.Clone());
    }
}
