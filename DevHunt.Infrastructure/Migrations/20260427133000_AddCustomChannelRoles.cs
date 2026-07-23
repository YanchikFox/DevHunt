using DevHunt.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevHunt.Infrastructure.Migrations
{
    // Hand-written migration: the [DbContext] / [Migration] attributes were
    // missing originally, so EF didn't pick the file up and the schema was
    // brought in by manual SQL on existing environments. The Up()/Down() SQL
    // is fully idempotent, so attaching the attributes here is safe — on a
    // database where the tables already exist it just inserts the row into
    // __EFMigrationsHistory; on a fresh database it creates the schema.
    [DbContext(typeof(DevHuntDbContext))]
    [Migration("20260427133000_AddCustomChannelRoles")]
    public partial class AddCustomChannelRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "Channel_Role_Definitions" (
                    "Id" uuid NOT NULL,
                    "ConversationId" uuid NOT NULL,
                    "Name" character varying(64) NOT NULL,
                    "CanPost" boolean NOT NULL DEFAULT true,
                    "CanManageMembers" boolean NOT NULL DEFAULT false,
                    "CanEditChannel" boolean NOT NULL DEFAULT false,
                    "CanDeleteChannel" boolean NOT NULL DEFAULT false,
                    "CanPinMessages" boolean NOT NULL DEFAULT false,
                    "CanDeleteMessages" boolean NOT NULL DEFAULT false,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    CONSTRAINT "PK_Channel_Role_Definitions" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_ChannelRoles_Conversations"
                        FOREIGN KEY ("ConversationId") REFERENCES "Conversations" ("Id") ON DELETE CASCADE
                );

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Channel_Role_Definitions_Conversation_Name"
                    ON "Channel_Role_Definitions" ("ConversationId", "Name");

                ALTER TABLE "Conversation_Participants"
                    ADD COLUMN IF NOT EXISTS "ChannelRoleDefinitionId" uuid NULL,
                    ADD COLUMN IF NOT EXISTS "CanPinMessages" boolean NULL,
                    ADD COLUMN IF NOT EXISTS "CanDeleteMessages" boolean NULL;

                CREATE INDEX IF NOT EXISTS "IX_Conversation_Participants_ChannelRoleDefinitionId"
                    ON "Conversation_Participants" ("ChannelRoleDefinitionId");

                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conname = 'FK_ConvPart_ChannelRoleDefinition'
                    ) THEN
                        ALTER TABLE "Conversation_Participants"
                            ADD CONSTRAINT "FK_ConvPart_ChannelRoleDefinition"
                            FOREIGN KEY ("ChannelRoleDefinitionId")
                            REFERENCES "Channel_Role_Definitions" ("Id")
                            ON DELETE SET NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Conversation_Participants"
                    DROP CONSTRAINT IF EXISTS "FK_ConvPart_ChannelRoleDefinition";

                DROP INDEX IF EXISTS "IX_Conversation_Participants_ChannelRoleDefinitionId";

                ALTER TABLE "Conversation_Participants"
                    DROP COLUMN IF EXISTS "CanDeleteMessages",
                    DROP COLUMN IF EXISTS "CanPinMessages",
                    DROP COLUMN IF EXISTS "ChannelRoleDefinitionId";

                DROP TABLE IF EXISTS "Channel_Role_Definitions";
                """);
        }
    }
}
