-- Dev-only helper: apply schema changes for migrations that EF Core cannot
-- discover on its own.
--
-- Four migrations lack both a [Migration] attribute and a .Designer.cs file, so
-- `dotnet ef database update` / DevHunt.DatabaseMigrator never apply them. In
-- production these columns/tables were added out-of-band; for local dev we apply
-- them here. Without this, login fails with 500 ("column TokenFamilyId ... does
-- not exist"). This script is idempotent and safe to re-run.
--
-- Usage (against the local Docker Postgres):
--   docker cp scripts/database/dev-fix-missing-migrations.sql \
--     devhunt-database-postgres:/tmp/fix.sql
--   docker exec -e PGPASSWORD=$POSTGRES_PASSWORD devhunt-database-postgres \
--     psql -U postgres -d devhunt_db -v ON_ERROR_STOP=1 -f /tmp/fix.sql

-- 20260529000000_AddUserLlmModels
CREATE TABLE IF NOT EXISTS "UserLlmModels" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Provider" character varying(32) NOT NULL,
    "ModelId" character varying(128) NOT NULL,
    "DisplayName" character varying(128) NOT NULL,
    "Description" character varying(512),
    "Tier" character varying(16) NOT NULL,
    "ContextWindow" integer NOT NULL,
    "MaxOutputTokens" integer,
    "SupportsTools" boolean NOT NULL,
    "SupportsStreaming" boolean NOT NULL,
    "SupportsVision" boolean NOT NULL,
    "InputPricePer1M" numeric(12,4),
    "OutputPricePer1M" numeric(12,4),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_UserLlmModels" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_UserLlmModels_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_UserLlmModels_UserId" ON "UserLlmModels" ("UserId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserLlmModels_UserId_Provider_ModelId" ON "UserLlmModels" ("UserId", "Provider", "ModelId");

-- 20260604180000_AddRefreshTokenFamilyId (required for login)
ALTER TABLE "RefreshTokens" ADD COLUMN IF NOT EXISTS "TokenFamilyId" uuid NOT NULL DEFAULT gen_random_uuid();
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_TokenFamilyId" ON "RefreshTokens" ("TokenFamilyId");

-- 20260614120000_RestoreTeamMemberUniqueIndex
UPDATE "TeamMembers" t
SET "Status" = 'left',
    "LeftAt" = COALESCE("LeftAt", NOW() AT TIME ZONE 'UTC')
FROM (
    SELECT "Id",
           ROW_NUMBER() OVER (
               PARTITION BY "ProjectId", "UserId"
               ORDER BY "JoinedAt" ASC, "Id" ASC
           ) AS rn
    FROM "TeamMembers"
    WHERE "Status" = 'active'
) dup
WHERE t."Id" = dup."Id" AND dup.rn > 1;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TeamMembers_ProjectId_UserId_Active"
    ON "TeamMembers" ("ProjectId", "UserId") WHERE "Status" = 'active';

-- 20260614130000_AddShowcaseLikes
CREATE TABLE IF NOT EXISTS "ShowcaseLikes" (
    "ShowcaseId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ShowcaseLikes" PRIMARY KEY ("ShowcaseId", "UserId"),
    CONSTRAINT "FK_ShowcaseLikes_Showcase_Projects_ShowcaseId" FOREIGN KEY ("ShowcaseId") REFERENCES "Showcase_Projects" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ShowcaseLikes_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS "IX_ShowcaseLikes_UserId" ON "ShowcaseLikes" ("UserId");

-- Columns present in the EF model snapshot but never added by a migration.
ALTER TABLE "Projects" ADD COLUMN IF NOT EXISTS "OpenRoles" jsonb;
ALTER TABLE "Tasks" ADD COLUMN IF NOT EXISTS "Tags" character varying(500);

-- Record the four discoverable-migration IDs so EF treats them as applied.
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES
    ('20260529000000_AddUserLlmModels', '9.0.1'),
    ('20260604180000_AddRefreshTokenFamilyId', '9.0.1'),
    ('20260614120000_RestoreTeamMemberUniqueIndex', '9.0.1'),
    ('20260614130000_AddShowcaseLikes', '9.0.1')
ON CONFLICT ("MigrationId") DO NOTHING;
