namespace DevHunt.Infrastructure.Constants;

/// <summary>
/// Document type keys used to categorize <see cref="DevHunt.Infrastructure.Models.ProjectDocument"/> records.
/// </summary>
public static class DocumentType
{
    // Manual document types
    /// <summary>Document type for project README content.</summary>
    public const string Readme = "readme";
    /// <summary>Document type for project wiki pages.</summary>
    public const string Wiki = "wiki";
    /// <summary>Document type for project guide content.</summary>
    public const string Guide = "guide";
    /// <summary>Document type for project changelog entries.</summary>
    public const string Changelog = "changelog";
    /// <summary>Document type for project API documentation.</summary>
    public const string ApiDocs = "api-docs";
    /// <summary>Fallback document type for uncategorized project documentation.</summary>
    public const string General = "general";

    // AI-generated document types (replacing Passport artifacts)
    /// <summary>Document type for AI-generated project overview content.</summary>
    public const string AiOverview = "ai-overview";
    /// <summary>Document type for AI-generated technology stack content.</summary>
    public const string AiTechStack = "ai-tech-stack";
    /// <summary>Document type for AI-generated architecture content.</summary>
    public const string AiArchitecture = "ai-architecture";
    /// <summary>Document type for AI-generated roadmap content.</summary>
    public const string AiRoadmap = "ai-roadmap";
    /// <summary>Document type for AI-generated decision log content.</summary>
    public const string AiDecisions = "ai-decisions";
    /// <summary>Document type for AI-generated architecture diagram content.</summary>
    public const string AiDiagram = "ai-diagram";

    /// <summary>
    /// Maps passport artifact type to document type.
    /// </summary>
    public static string FromArtifactType(string artifactType) => artifactType switch
    {
        "overview" => AiOverview,
        "tech_stack" => AiTechStack,
        "architecture" => AiArchitecture,
        "roadmap" => AiRoadmap,
        "decisions" => AiDecisions,
        "architecture_diagram" => AiDiagram,
        _ => General,
    };

    /// <summary>
    /// Indicates whether a document type is one of the AI-generated project document categories.
    /// </summary>
    public static bool IsAiGenerated(string? type) =>
        type is AiOverview or AiTechStack or AiArchitecture or AiRoadmap or AiDecisions or AiDiagram;
}
