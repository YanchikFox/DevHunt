using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;

namespace DevHunt.Infrastructure;

/// <summary>
    /// Entity Framework Core DbContext for DevHunt
    /// 
    /// Database: PostgreSQL (according to SRS v1.0, section 8.2)
    /// 
    /// Features:
    /// - Support for PostgreSQL-specific types (text[], JSONB via EF)
    /// - Automatic migrations on startup
    /// - Seed data for development
    /// 
    /// Entities:
    /// - User: Platform users
    /// - Project: Projects (corresponds to UC-2, UC-5)
    /// - TeamMember: Team members (UC-4)
    /// - TaskItem: Kanban tasks (UC-4)
    /// - Invitation: Invitations and applications (UC-3)
    /// - Review: Reviews and ratings
    /// - Notification: Notifications
    /// - ModerationReport: Moderation complaints
    /// - Conversation: Dialogs and chats
    /// - ConversationParticipant: Chat participants
    /// - Message: Messages
    /// </summary>
    public class DevHuntDbContext : DbContext
    {
        public DevHuntDbContext(DbContextOptions<DevHuntDbContext> options) : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectBoost> ProjectBoosts { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<TaskItem> Tasks { get; set; }
        public DbSet<Invitation> Invitations { get; set; }
        public DbSet<ModerationReport> ModerationReports { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
        public DbSet<ChannelRoleDefinition> ChannelRoleDefinitions { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<AiMessageDetails> AiMessageDetails { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillAlias> SkillAliases { get; set; }
        public DbSet<UserSkill> UserSkills { get; set; }
        public DbSet<UserSkillEntry> UserSkillEntries { get; set; }
        public DbSet<ProjectTechStack> ProjectTechStacks { get; set; }
        public DbSet<ProjectRole> ProjectRoles { get; set; }
        public DbSet<ShowcaseProject> ShowcaseProjects { get; set; }
        public DbSet<ShowcaseLike> ShowcaseLikes { get; set; }
        public DbSet<ShowcaseComment> ShowcaseComments { get; set; }
        public DbSet<ProjectFile> ProjectFiles { get; set; }
        public DbSet<ProjectDocument> ProjectDocuments { get; set; }
        public DbSet<ActivityRecord> ActivityRecords { get; set; }
        public DbSet<ProjectNewsPost> ProjectNewsPosts { get; set; }
        public DbSet<ProjectSubscription> ProjectSubscriptions { get; set; }
        public DbSet<UserFollow> UserFollows { get; set; }
        public DbSet<Recommendation> Recommendations { get; set; }
        public DbSet<Integration> Integrations { get; set; }
        public DbSet<Achievement> Achievements { get; set; }
        public DbSet<UserAchievement> UserAchievements { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        
        // REL-002: Outbox Pattern for reliable event delivery
        public DbSet<OutboxEvent> OutboxEvents { get; set; }
        
        // Support System
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<TicketMessage> TicketMessages { get; set; }
        public DbSet<TicketHistory> TicketHistories { get; set; }
        
        // Community/Feedback System
        public DbSet<FeedbackItem> FeedbackItems { get; set; }
        public DbSet<FeedbackVote> FeedbackVotes { get; set; }
        public DbSet<FeedbackComment> FeedbackComments { get; set; }
        
        // Admin - Project Issues
        public DbSet<ProjectIssue> ProjectIssues { get; set; }
        
        // Privacy Settings
        public DbSet<UserPrivacySettings> UserPrivacySettings { get; set; }

        // Task Board Enhancement
        public DbSet<TaskColumn> TaskColumns { get; set; }
        public DbSet<TaskLink> TaskLinks { get; set; }
        public DbSet<TaskAttachment> TaskAttachments { get; set; }
        public DbSet<TaskBoardSettings> TaskBoardSettings { get; set; }
        
        // AI Planning
        public DbSet<AiPlan> AiPlans { get; set; }
        public DbSet<AiOperationLog> AiOperationLogs { get; set; }
        public DbSet<ProjectArtifact> ProjectArtifacts { get; set; }
        
        // Audit Logs
        public DbSet<AuditLog> AuditLogs { get; set; }

        // News Post interactions
        public DbSet<NewsPostLike> NewsPostLikes { get; set; }
        public DbSet<NewsPostComment> NewsPostComments { get; set; }

        // Admin Extensions
        public DbSet<AdminNote> AdminNotes { get; set; }
        public DbSet<PlatformSetting> PlatformSettings { get; set; }
        public DbSet<FeatureFlag> FeatureFlags { get; set; }
        
        // Code Analysis
        public DbSet<CodeAnalysisResult> CodeAnalysisResults { get; set; }
        public DbSet<CodeAnalysisEmbedding> CodeAnalysisEmbeddings { get; set; }
        public DbSet<CodeAnalysisEmbeddingJob> CodeAnalysisEmbeddingJobs { get; set; }

        // BYOK LLM (Bring Your Own Key) + model registry
        public DbSet<UserApiKey> UserApiKeys { get; set; }
        public DbSet<LlmModel> LlmModels { get; set; }
        public DbSet<UserLlmModel> UserLlmModels { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevHuntDbContext).Assembly);

            modelBuilder.Entity<UserLlmModel>(b =>
            {
                b.HasOne(m => m.User)
                    .WithMany()
                    .HasForeignKey(m => m.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One row per (user, provider, modelId) — re-sync is an upsert.
                b.HasIndex(m => new { m.UserId, m.Provider, m.ModelId })
                    .HasDatabaseName("IX_UserLlmModels_UserId_Provider_ModelId")
                    .IsUnique();

                b.HasIndex(m => m.UserId)
                    .HasDatabaseName("IX_UserLlmModels_UserId");
            });

            modelBuilder.Entity<ModerationReport>()
                .HasIndex(x => new { x.TargetType, x.TargetId, x.Status })
                .HasDatabaseName("IX_ModerationReports_TargetType_TargetId_Status");

            modelBuilder.Entity<CodeAnalysisResult>()
                .HasIndex(x => new { x.ProjectId, x.CreatedAt })
                .HasDatabaseName("IX_CodeAnalysisResults_ProjectId_CreatedAt")
                .IsDescending(false, true);

            modelBuilder.Entity<CodeAnalysisResult>()
                .HasIndex(x => new { x.IntegrationId, x.Branch })
                .HasDatabaseName("IX_CodeAnalysisResults_IntegrationId_Branch");

            modelBuilder.Entity<CodeAnalysisEmbeddingJob>()
                .HasIndex(j => j.AnalysisResultId)
                .IsUnique()
                .HasDatabaseName("IX_CodeAnalysisEmbeddingJobs_AnalysisResultId");

            modelBuilder.Entity<CodeAnalysisEmbeddingJob>()
                .HasIndex(j => new { j.Status, j.NextAttemptAt })
                .HasDatabaseName("IX_CodeAnalysisEmbeddingJobs_Status_NextAttemptAt");

            // Partial unique index: Slug must be unique when set (NULL slugs allowed unlimited).
            modelBuilder.Entity<Project>()
                .HasIndex(p => p.Slug)
                .HasDatabaseName("IX_Projects_Slug")
                .IsUnique()
                .HasFilter("\"Slug\" IS NOT NULL");

            modelBuilder.Entity<ProjectBoost>(b =>
            {
                b.ToTable("ProjectBoosts");
                b.HasKey(x => new { x.ProjectId, x.UserId });
                b.HasOne(x => x.Project)
                    .WithMany(p => p.Boosts)
                    .HasForeignKey(x => x.ProjectId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(x => x.UserId)
                    .HasDatabaseName("IX_ProjectBoosts_UserId");
            });

            // DEV-114: per-user showcase likes. Composite key enforces one like per user/showcase.
            modelBuilder.Entity<ShowcaseLike>(b =>
            {
                b.ToTable("ShowcaseLikes");
                b.HasKey(x => new { x.ShowcaseId, x.UserId });
                b.HasOne(x => x.Showcase)
                    .WithMany()
                    .HasForeignKey(x => x.ShowcaseId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                b.HasIndex(x => x.UserId)
                    .HasDatabaseName("IX_ShowcaseLikes_UserId");
            });

            // Conversations — Discord/Slack-style channels live alongside
            // Direct/Group chats. ProjectId is optional (Direct/Group don't
            // have one), Slug is enforced unique per project only for
            // ProjectChannel rows via a filtered index.
            modelBuilder.Entity<Conversation>(b =>
            {
                b.HasOne(c => c.Project)
                    .WithMany()
                    .HasForeignKey(c => c.ProjectId)
                    // Keep history on project delete — channels get soft-unlinked
                    // and can be archived later; prevents accidental cascade wipes.
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(c => c.CreatedBy)
                    .WithMany()
                    .HasForeignKey(c => c.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasIndex(c => c.ProjectId)
                    .HasDatabaseName("IX_Conversations_ProjectId");

                // Slug unique within a project and only for channels.
                b.HasIndex(c => new { c.ProjectId, c.Slug })
                    .HasDatabaseName("IX_Conversations_ProjectId_Slug")
                    .IsUnique()
                    .HasFilter("\"Type\" = 2 AND \"Slug\" IS NOT NULL AND \"ProjectId\" IS NOT NULL");
            });

            modelBuilder.Entity<ConversationParticipant>(b =>
            {
                // Banner is nullable: unban / legacy / not banned.
                b.HasOne(p => p.BannedBy)
                    .WithMany()
                    .HasForeignKey(p => p.BannedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(p => p.RoleDefinition)
                    .WithMany()
                    .HasForeignKey(p => p.ChannelRoleDefinitionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // One participation per (conversation, user). Without this the
                // backfill for public channels would silently create duplicates
                // on races, and re-joining after leave would also dupe.
                b.HasIndex(p => new { p.ConversationId, p.UserId })
                    .HasDatabaseName("IX_Conversation_Participants_ConvUser")
                    .IsUnique();
            });

            modelBuilder.Entity<ChannelRoleDefinition>(b =>
            {
                b.ToTable("Channel_Role_Definitions");

                b.HasOne(r => r.Conversation)
                    .WithMany()
                    .HasForeignKey(r => r.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(r => new { r.ConversationId, r.Name })
                    .HasDatabaseName("IX_Channel_Role_Definitions_Conversation_Name")
                    .IsUnique();
            });

            modelBuilder.Entity<Message>(b =>
            {
                b.Property(m => m.AiMetadataJson)
                    .HasColumnType("jsonb");

                b.HasOne(m => m.PinnedByUser)
                    .WithMany()
                    .HasForeignKey(m => m.PinnedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(m => m.AiDetails)
                    .WithOne(d => d.Message)
                    .HasForeignKey<AiMessageDetails>(d => d.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasIndex(m => new { m.ConversationId, m.IsPinned, m.CreatedAt })
                    .HasDatabaseName("IX_Messages_ConversationId_IsPinned_CreatedAt");
            });

            modelBuilder.Entity<AiMessageDetails>(b =>
            {
                b.HasKey(d => d.MessageId);
                b.Property(d => d.MessageId).ValueGeneratedNever();
                b.Property(d => d.FullPayloadJson).HasColumnType("jsonb");
                b.HasIndex(d => d.CreatedAt).HasDatabaseName("IX_AiMessageDetails_CreatedAt");
            });

            modelBuilder.Entity<MessageReaction>(b =>
            {
                b.HasIndex(r => new { r.MessageId, r.UserId, r.Emoji })
                    .HasDatabaseName("IX_MessageReactions_Message_User_Emoji")
                    .IsUnique();

                b.HasOne(r => r.Message)
                    .WithMany(m => m.Reactions)
                    .HasForeignKey(r => r.MessageId)
                    .OnDelete(DeleteBehavior.Cascade);

                b.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // DEV-97: one active membership per (project, user). Partial filter lets a user who
            // left a project rejoin without colliding with their old 'left' row. Mirrors the
            // 20260614120000_RestoreTeamMemberUniqueIndex migration.
            modelBuilder.Entity<TeamMember>()
                .HasIndex(m => new { m.ProjectId, m.UserId })
                .HasDatabaseName("IX_TeamMembers_ProjectId_UserId_Active")
                .HasFilter("\"Status\" = 'active'")
                .IsUnique();
        }
    }
