using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DevHunt.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ParticipantOnlySeed : Migration
{
        /// <inheritdoc />
        /// <remarks>
        /// NOTE: This migration contains seed data with bcrypt-hashed passwords.
        /// The password hashes are SAFE to store in version control as they are:
        /// 1. Cryptographically hashed using bcrypt (not reversible)
        /// 2. Used only for development/testing environments
        /// 3. Should be changed in production via proper user registration
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Invitations",
                keyColumn: "Id",
                keyValue: new Guid("6f1645aa-0ad9-4d51-919e-8b332afbbb5f"));

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"));

            migrationBuilder.DeleteData(
                table: "Tasks",
                keyColumn: "Id",
                keyValue: new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"));

            migrationBuilder.DeleteData(
                table: "TeamMembers",
                keyColumn: "Id",
                keyValue: new Guid("02efec63-0182-4a60-9088-f13faf9d3651"));

            migrationBuilder.DeleteData(
                table: "TeamMembers",
                keyColumn: "Id",
                keyValue: new Guid("29d08d5d-f8c3-4df8-8638-524c2f46d9a6"));

            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"));

            migrationBuilder.DeleteData(
                table: "Projects",
                keyColumn: "Id",
                keyValue: new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"));

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "Bio", "CreatedAt", "Email", "Experience", "FullName", "Github", "IsActive", "IsVerified", "Language", "LastLogin", "Linkedin", "PasswordHash", "Rating", "Role", "Skills", "Timezone", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"), null, "Backend generalist focused on event-driven architectures and clean APIs.", new DateTime(2024, 12, 7, 9, 0, 0, 0, DateTimeKind.Utc), "timur.safin@devhunt.io", 7, "Timur Safin", "timursafin", true, true, "ru", new DateTime(2025, 1, 1, 9, 0, 0, 0, DateTimeKind.Utc), "timur-safin", "DEMO_PASSWORD_HASH", 4.8f, "participant", new[] { ".NET", "PostgreSQL", "RabbitMQ" }, "UTC+5", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), "https://timurs.dev" },
                    { new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"), null, "Mobile + web hybrid dev building onboarding flows with Expo/Next.", new DateTime(2024, 12, 20, 9, 0, 0, 0, DateTimeKind.Utc), "daria.shevchenko@devhunt.io", 5, "Daria Shevchenko", "dariashevchenko", true, true, "en", new DateTime(2024, 12, 31, 9, 0, 0, 0, DateTimeKind.Utc), "daria-shevchenko", "DEMO_PASSWORD_HASH", 4.2f, "participant", new[] { "React Native", "Next.js", "UX Writing" }, "UTC+1", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"), null, "AI engineer experimenting with lightweight recommendation pipelines.", new DateTime(2024, 12, 17, 9, 0, 0, 0, DateTimeKind.Utc), "arseniy.karpov@devhunt.io", 4, "Arseniy Karpov", "akarpoff", true, false, "ru", new DateTime(2025, 1, 1, 3, 0, 0, 0, DateTimeKind.Utc), "arseniy-karpov", "DEMO_PASSWORD_HASH", 4.3f, "participant", new[] { "Python", "ML.NET", "Vector Search" }, "UTC+4", new DateTime(2024, 12, 27, 9, 0, 0, 0, DateTimeKind.Utc), "https://karpov.ai" },
                    { new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"), null, "UX researcher pairing squads with qualitative insights from student pilots.", new DateTime(2024, 12, 22, 9, 0, 0, 0, DateTimeKind.Utc), "olga.smirnova@devhunt.io", 6, "Olga Smirnova", "olga-smirnova", true, false, "ru", new DateTime(2025, 1, 1, 7, 0, 0, 0, DateTimeKind.Utc), "olga-smirnova", "DEMO_PASSWORD_HASH", 4.4f, "participant", new[] { "UX Research", "Prototyping", "Storytelling" }, "UTC+3", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"), null, "Frontend engineer shipping polished UI kits for civic hackathons.", new DateTime(2024, 12, 14, 9, 0, 0, 0, DateTimeKind.Utc), "anastasia.petrenko@devhunt.io", 5, "Anastasia Petrenko", "anapetrenko", true, true, "ru", new DateTime(2024, 12, 31, 9, 0, 0, 0, DateTimeKind.Utc), "anastasia-petrenko", "DEMO_PASSWORD_HASH", 4.7f, "participant", new[] { "React", "Next.js", "Design Systems" }, "UTC+3", new DateTime(2024, 12, 28, 9, 0, 0, 0, DateTimeKind.Utc), "https://apetrenko.dev" },
                    { new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"), null, "Product-minded engineer who validates onboarding experiments end-to-end.", new DateTime(2024, 12, 2, 9, 0, 0, 0, DateTimeKind.Utc), "elena.koval@devhunt.io", 6, "Elena Koval", "elenakoval", true, false, "en", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), "elena-koval", "DEMO_PASSWORD_HASH", 4.5f, "participant", new[] { "Product Discovery", "TypeScript", "Analytics" }, "UTC+2", new DateTime(2024, 12, 26, 9, 0, 0, 0, DateTimeKind.Utc), null },
                    { new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"), null, "DevOps-minded engineer keeping demo clusters green during hackathons.", new DateTime(2024, 11, 22, 9, 0, 0, 0, DateTimeKind.Utc), "maxim.volkov@devhunt.io", 8, "Maxim Volkov", "mvolkov", true, true, "ru", new DateTime(2024, 12, 30, 9, 0, 0, 0, DateTimeKind.Utc), "maxim-volkov", "DEMO_PASSWORD_HASH", 4.9f, "participant", new[] { "Docker", "Kubernetes", "Grafana" }, "UTC+3", new DateTime(2024, 12, 25, 9, 0, 0, 0, DateTimeKind.Utc), "https://maxvolkov.dev" },
                    { new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"), null, "Data engineer prototyping ranking metrics for project recommendations.", new DateTime(2024, 12, 10, 9, 0, 0, 0, DateTimeKind.Utc), "nikita.lebedev@devhunt.io", 5, "Nikita Lebedev", "nikitalebedev", true, true, "en", new DateTime(2025, 1, 1, 8, 0, 0, 0, DateTimeKind.Utc), "nikita-lebedev", "DEMO_PASSWORD_HASH", 4.6f, "participant", new[] { "Python", "ETL", "dbt" }, "UTC+2", new DateTime(2024, 12, 29, 9, 0, 0, 0, DateTimeKind.Utc), "https://lebedev.dev" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("0d5a7ca7-35d8-4cf6-9729-a90f43769f8e"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("5d4d7082-ec8a-47c0-86b7-29c91ba524a1"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("6e2f9113-d6ef-4829-8714-0a3dc2a3e2f9"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("7fd40108-e6e6-4220-b66b-e5ef1f5c4843"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("a3a95f44-25b8-4a39-8c3e-4d6bb5be6db7"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("b9c00db5-4b63-4c2f-a0f5-0d11041a5e58"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("bc2dde64-00f2-4d6b-9ca8-9ff4ae3e8d12"));

            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("ee2f8a99-6d7a-47ad-83ac-5d2d0f1b9cd3"));

            migrationBuilder.InsertData(
                table: "Projects",
                columns: new[] { "Id", "CreatedAt", "Description", "DifficultyLevel", "EndDate", "ExpectedDurationDays", "Featured", "MaxTeamSize", "OwnerId", "Rating", "RequiredRoles", "ShortDescription", "ShowcasePublished", "StartDate", "Status", "TechStack", "Title", "UpdatedAt", "Visibility" },
                values: new object[,]
                {
                    { new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Platform that helps hackathon organizers manage teams and tasks in real time.", null, null, null, false, null, new Guid("00000000-0000-0000-0000-000000000000"), null, new string[0], null, false, null, "active", new[] { "ASP.NET Core", "React", "PostgreSQL" }, "Hackathon Companion", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "public" },
                    { new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), new DateTime(2024, 12, 22, 0, 0, 0, 0, DateTimeKind.Utc), "Showcase service for AI projects with ratings, feedback, and GitHub integration.", null, null, null, false, null, new Guid("00000000-0000-0000-0000-000000000000"), null, new string[0], null, false, null, "planning", new[] { "Next.js", "Node.js", "Redis" }, "AI Showcase Hub", new DateTime(2024, 12, 30, 0, 0, 0, 0, DateTimeKind.Utc), "public" }
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "AvatarUrl", "Bio", "CreatedAt", "Email", "Experience", "FullName", "Github", "IsActive", "IsVerified", "Language", "LastLogin", "Linkedin", "PasswordHash", "Rating", "Role", "Skills", "Timezone", "UpdatedAt", "Website" },
                values: new object[,]
                {
                    { new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), null, "Product manager with 6 years of experience.", new DateTime(2025, 11, 4, 2, 16, 31, 343, DateTimeKind.Utc).AddTicks(226), "product@devhunt.io", null, "Daria Nikolaeva", null, true, false, null, null, null, "DEMO_PASSWORD_HASH", null, "pm", new string[0], "UTC+1", null, null },
                    { new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"), null, "Full-stack developer working with React and .NET.", new DateTime(2025, 11, 4, 2, 16, 31, 343, DateTimeKind.Utc).AddTicks(214), "builder@devhunt.io", null, "Ivan Goncharov", null, true, false, null, null, null, "DEMO_PASSWORD_HASH", null, "participant", new string[0], "UTC+4", null, null },
                    { new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"), null, "Helps teams with solution architecture and code reviews.", new DateTime(2025, 11, 4, 2, 16, 31, 342, DateTimeKind.Utc).AddTicks(5640), "mentor@devhunt.io", null, "Maria Kotova", null, true, false, null, null, null, "DEMO_PASSWORD_HASH", null, "mentor", new string[0], "UTC+3", null, null }
                });

            migrationBuilder.InsertData(
                table: "Invitations",
                columns: new[] { "Id", "CreatedAt", "InviteeId", "InviterId", "Message", "ProjectId", "RespondedAt", "Role", "Status" },
                values: new object[] { new Guid("6f1645aa-0ad9-4d51-919e-8b332afbbb5f"), new DateTime(2024, 12, 31, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9"), new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), "Need help with architecture choices and feature vetting.", new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), null, "mentor", "pending" });

            migrationBuilder.InsertData(
                table: "Tasks",
                columns: new[] { "Id", "ActualHours", "AssignedToUserId", "CompletedAt", "CreatedAt", "CreatedByUserId", "Deadline", "Description", "EstimatedHours", "IsDeleted", "Priority", "ProjectId", "Status", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("f4f5025e-6ebf-4c0a-bf9f-6b9e1d6da98d"), null, new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2"), null, new DateTime(2025, 11, 4, 2, 16, 31, 349, DateTimeKind.Utc).AddTicks(2036), new Guid("00000000-0000-0000-0000-000000000000"), new DateTime(2025, 1, 3, 0, 0, 0, 0, DateTimeKind.Utc), null, null, false, "medium", new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "in_progress", "Set up CI/CD", null },
                    { new Guid("fd8f047c-d31e-469d-85ed-7f608d3f6306"), null, new Guid("39fa21f7-02ad-4941-9dff-64ec7b441d72"), null, new DateTime(2025, 11, 4, 2, 16, 31, 349, DateTimeKind.Utc).AddTicks(8415), new Guid("00000000-0000-0000-0000-000000000000"), null, null, null, false, "medium", new Guid("d7d17e84-7a92-4c2f-bc67-bc0612548723"), "todo", "Collect early adopter feedback", null }
                });

            migrationBuilder.InsertData(
                table: "TeamMembers",
                columns: new[] { "Id", "Contribution", "ContributionScore", "IsLeader", "JoinedAt", "LeftAt", "ProjectId", "Role", "Status", "UserId" },
                values: new object[,]
                {
                    { new Guid("02efec63-0182-4a60-9088-f13faf9d3651"), "Built the backend and frontend MVP in 72 hours.", null, false, new DateTime(2024, 12, 29, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "fullstack", "active", new Guid("56d8f0bd-16ce-4880-a94b-0c81273790d2") },
                    { new Guid("29d08d5d-f8c3-4df8-8638-524c2f46d9a6"), "Mentored the team and reviewed architecture decisions.", null, false, new DateTime(2024, 12, 30, 0, 0, 0, 0, DateTimeKind.Utc), null, new Guid("7f01de92-32e5-459d-bd48-3c968ccb67f1"), "mentor", "active", new Guid("f8f32d7a-0a82-4a81-90f5-6a55f4e5f8d9") }
                });
        }
    }

