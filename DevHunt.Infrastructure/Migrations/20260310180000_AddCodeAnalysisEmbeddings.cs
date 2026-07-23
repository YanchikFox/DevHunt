using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations;

/// <summary>
/// Adds CodeAnalysisEmbeddings table with pgvector support for semantic RAG search.
/// Requires: CREATE EXTENSION IF NOT EXISTS vector (done in db-init/01-extensions.sql).
/// </summary>
public partial class AddCodeAnalysisEmbeddings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Ensure pgvector extension exists (idempotent)
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "CodeAnalysisEmbeddings" (
                "Id"                uuid NOT NULL DEFAULT gen_random_uuid(),
                "AnalysisResultId"  uuid NOT NULL,
                "ProjectId"         uuid NOT NULL,
                "RuleId"            varchar(200) NOT NULL,
                "RuleName"          varchar(300),
                "Severity"          varchar(20) NOT NULL,
                "Category"          varchar(50) NOT NULL,
                "IssueCount"        integer NOT NULL DEFAULT 0,
                "EmbeddingText"     text NOT NULL,
                "SampleMessage"     text,
                "TopFilesJson"      jsonb,
                "CweId"             varchar(20),
                "SampleSuggestion"  text,
                "Embedding"         vector(384),
                "CreatedAt"         timestamptz NOT NULL DEFAULT now(),

                CONSTRAINT "PK_CodeAnalysisEmbeddings" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CodeAnalysisEmbeddings_CodeAnalysisResults"
                    FOREIGN KEY ("AnalysisResultId") REFERENCES "CodeAnalysisResults"("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CodeAnalysisEmbeddings_Projects"
                    FOREIGN KEY ("ProjectId") REFERENCES "Projects"("Id") ON DELETE CASCADE
            );
        """);

        // Index for fetching embeddings by project (latest analysis)
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_CodeAnalysisEmbeddings_ProjectId"
                ON "CodeAnalysisEmbeddings" ("ProjectId");
        """);

        // Index for fetching embeddings by analysis result
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_CodeAnalysisEmbeddings_AnalysisResultId"
                ON "CodeAnalysisEmbeddings" ("AnalysisResultId");
        """);

        // IVFFlat index for fast vector similarity search
        // Uses cosine distance (<=>); needs ~100+ rows to be effective
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_CodeAnalysisEmbeddings_Embedding"
                ON "CodeAnalysisEmbeddings"
                USING ivfflat ("Embedding" vector_cosine_ops)
                WITH (lists = 10);
        """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "CodeAnalysisEmbeddings";""");
    }
}
