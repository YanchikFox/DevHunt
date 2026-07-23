using DevHunt.Infrastructure;
using DevHunt.Infrastructure.Configuration;
using DevHunt.Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace DevHunt.DatabaseSeeder;

class Program
{
    #region Seed Data Catalog

    private static readonly (string Name, string Category, string? Description)[] SkillsSeedData =
    [
        // Languages
        ("JavaScript", "Languages", "Programming language"),
        ("TypeScript", "Languages", "Typed superset of JavaScript"),
        ("Python", "Languages", "Programming language"),
        ("C#", "Languages", "Programming language"),
        ("F#", "Languages", "Programming language"),
        ("Visual Basic .NET", "Languages", "Programming language"),
        ("Java", "Languages", "Programming language"),
        ("Go", "Languages", "Programming language"),
        ("Rust", "Languages", "Programming language"),
        ("Kotlin", "Languages", "Programming language"),
        ("Swift", "Languages", "Programming language"),
        ("Dart", "Languages", "Programming language"),
        ("Ruby", "Languages", "Programming language"),
        ("PHP", "Languages", "Programming language"),
        ("C", "Languages", "Programming language"),
        ("C++", "Languages", "Programming language"),
        ("Objective-C", "Languages", "Programming language"),
        ("Scala", "Languages", "Programming language"),
        ("Elixir", "Languages", "Programming language"),
        ("Erlang", "Languages", "Programming language"),
        ("Haskell", "Languages", "Programming language"),
        ("Lua", "Languages", "Programming language"),
        ("R", "Languages", "Programming language"),
        ("SQL", "Languages", "Query language"),
        ("Bash", "Languages", "Shell scripting"),
        ("PowerShell", "Languages", "Shell scripting"),
        ("HTML", "Web", "Markup language"),
        ("CSS", "Web", "Style sheet language"),

        // Web
        ("React", "Web", "UI library"),
        ("Next.js", "Web", "React framework"),
        ("Remix", "Web", "Full stack web framework"),
        ("Gatsby", "Web", "Static site generator"),
        ("Vue.js", "Web", "JavaScript framework"),
        ("Nuxt", "Web", "Vue framework"),
        ("Angular", "Web", "Web framework"),
        ("Svelte", "Web", "Web framework"),
        ("SvelteKit", "Web", "Svelte application framework"),
        ("SolidJS", "Web", "UI library"),
        ("Qwik", "Web", "Resumable web framework"),
        ("Lit", "Web", "Web components library"),
        ("Astro", "Web", "Content-focused web framework"),
        ("Node.js", "Web", "JavaScript runtime"),
        ("Bun", "Web", "JavaScript runtime"),
        ("Deno", "Web", "JavaScript/TypeScript runtime"),
        ("Express", "Web", "Node.js web framework"),
        ("Fastify", "Web", "Node.js web framework"),
        ("NestJS", "Web", "Node.js framework"),
        ("Koa", "Web", "Node.js web framework"),
        ("Hono", "Web", "Web framework"),
        ("tRPC", "Web", "Typesafe API"),
        ("React Query", "Web", "Server state management"),
        ("Redux", "Web", "State management"),
        ("Zustand", "Web", "State management"),
        ("MobX", "Web", "State management"),
        ("Vite", "Web", "Build tool"),
        ("Webpack", "Web", "Module bundler"),
        ("Rollup", "Web", "Module bundler"),
        ("Parcel", "Web", "Bundler"),
        ("ESLint", "Web", "Linting tool"),
        ("Prettier", "Web", "Code formatter"),
        ("Storybook", "Web", "UI component workshop"),
        ("Tailwind CSS", "Web", "Utility-first CSS"),
        ("Bootstrap", "Web", "CSS framework"),
        ("Sass", "Web", "CSS preprocessor"),
        ("PostCSS", "Web", "CSS tooling"),
        ("shadcn/ui", "Web", "UI component patterns"),
        ("Radix UI", "Web", "Accessible UI primitives"),
        ("Material UI", "Web", "React UI library"),
        ("Chakra UI", "Web", "React UI library"),
        ("Ant Design", "Web", "UI library"),
        ("REST", "Web", "API style"),
        ("GraphQL", "Web", "API query language"),
        ("gRPC", "Web", "High-performance RPC"),
        ("OpenAPI", "Web", "API specification"),
        ("Swagger", "Web", "OpenAPI tooling"),
        ("OAuth 2.0", "Web", "Authorization framework"),
        ("OpenID Connect", "Web", "Identity layer"),
        ("JWT", "Web", "JSON Web Tokens"),
        ("WebSockets", "Web", "Realtime communication"),
        ("SignalR", "Web", "Realtime communication"),
        ("PWA", "Web", "Progressive Web Apps"),
        ("SSR", "Web", "Server-side rendering"),
        ("CSR", "Web", "Client-side rendering"),
        ("Accessibility", "Web", "A11y best practices"),
        ("i18n", "Web", "Internationalization"),
        ("SEO", "Web", "Search engine optimization"),

        // Backend
        (".NET", "Backend", "Microsoft development platform"),
        ("ASP.NET Core", "Backend", "Web framework"),
        ("Entity Framework Core", "Backend", "ORM"),
        ("Dapper", "Backend", "Micro-ORM"),
        ("MediatR", "Backend", "Mediator pattern"),
        ("MassTransit", "Backend", "Distributed application framework"),
        ("Hangfire", "Backend", "Background jobs"),
        ("Quartz.NET", "Backend", "Job scheduling"),
        ("Serilog", "Backend", "Structured logging"),
        ("NLog", "Backend", "Logging"),
        ("FluentValidation", "Backend", "Validation"),
        ("AutoMapper", "Backend", "Object mapping"),
        ("Spring Boot", "Backend", "Java framework"),
        ("Quarkus", "Backend", "Java framework"),
        ("Micronaut", "Backend", "JVM framework"),
        ("Ktor", "Backend", "Kotlin framework"),
        ("Django", "Backend", "Python framework"),
        ("FastAPI", "Backend", "Python API framework"),
        ("Flask", "Backend", "Python web framework"),
        ("Ruby on Rails", "Backend", "Ruby framework"),
        ("Laravel", "Backend", "PHP framework"),
        ("Symfony", "Backend", "PHP framework"),
        ("Gin", "Backend", "Go web framework"),
        ("Fiber", "Backend", "Go web framework"),
        ("Actix Web", "Backend", "Rust web framework"),
        ("Rocket", "Backend", "Rust web framework"),
        ("Tauri", "Desktop", "Desktop app framework"),
        ("Electron", "Desktop", "Desktop app framework"),
        ("WPF", "Desktop", "Desktop UI framework"),
        ("WinUI", "Desktop", "Windows UI"),
        ("MAUI", "Mobile", "Cross-platform .NET UI"),

        // Databases / Storage
        ("PostgreSQL", "Databases", "Relational database"),
        ("MySQL", "Databases", "Relational database"),
        ("MariaDB", "Databases", "Relational database"),
        ("SQLite", "Databases", "Embedded database"),
        ("SQL Server", "Databases", "Relational database"),
        ("CockroachDB", "Databases", "Distributed SQL database"),
        ("MongoDB", "Databases", "Document database"),
        ("DynamoDB", "Databases", "NoSQL database"),
        ("Cassandra", "Databases", "Wide-column store"),
        ("ScyllaDB", "Databases", "Wide-column store"),
        ("Neo4j", "Databases", "Graph database"),
        ("Redis", "Databases", "In-memory data store"),
        ("Memcached", "Databases", "In-memory cache"),
        ("Elasticsearch", "Databases", "Search engine"),
        ("OpenSearch", "Databases", "Search and analytics"),
        ("Meilisearch", "Databases", "Search engine"),
        ("ClickHouse", "Databases", "Analytical database"),
        ("BigQuery", "Databases", "Data warehouse"),
        ("Snowflake", "Databases", "Data warehouse"),
        ("S3", "Storage", "Object storage"),
        ("MinIO", "Storage", "S3-compatible storage"),
        ("SeaweedFS", "Storage", "Distributed object/file store"),

        // Messaging
        ("RabbitMQ", "Messaging", "Message broker"),
        ("Kafka", "Messaging", "Event streaming"),
        ("NATS", "Messaging", "Messaging system"),
        ("Redis Streams", "Messaging", "Streams"),

        // DevOps
        ("Docker", "DevOps", "Containerization"),
        ("Docker Compose", "DevOps", "Local orchestration"),
        ("Kubernetes", "DevOps", "Container orchestration"),
        ("Helm", "DevOps", "Kubernetes package manager"),
        ("Kustomize", "DevOps", "Kubernetes configuration"),
        ("Terraform", "DevOps", "Infrastructure as Code"),
        ("Pulumi", "DevOps", "Infrastructure as Code"),
        ("Ansible", "DevOps", "Automation"),
        ("Packer", "DevOps", "Image building"),
        ("Git", "DevOps", "Version control"),
        ("GitHub Actions", "DevOps", "CI/CD"),
        ("GitLab CI", "DevOps", "CI/CD"),
        ("Jenkins", "DevOps", "CI/CD"),
        ("Argo CD", "DevOps", "GitOps"),
        ("Flux", "DevOps", "GitOps"),
        ("Prometheus", "DevOps", "Monitoring"),
        ("Grafana", "DevOps", "Dashboards"),
        ("Loki", "DevOps", "Log aggregation"),
        ("Tempo", "DevOps", "Tracing backend"),
        ("OpenTelemetry", "DevOps", "Observability"),
        ("Jaeger", "DevOps", "Tracing"),
        ("Zipkin", "DevOps", "Tracing"),
        ("Sentry", "DevOps", "Error tracking"),
        ("Nginx", "DevOps", "Reverse proxy"),
        ("Traefik", "DevOps", "Reverse proxy"),
        ("Caddy", "DevOps", "Web server"),
        ("Linux", "DevOps", "Operating system"),
        ("Windows", "DevOps", "Operating system"),

        // Cloud
        ("AWS", "Cloud", "Cloud platform"),
        ("Azure", "Cloud", "Cloud platform"),
        ("Google Cloud", "Cloud", "Cloud platform"),
        ("Serverless", "Cloud", "Serverless architecture"),
        ("CDN", "Cloud", "Content delivery network"),

        // Security
        ("OWASP", "Security", "App security best practices"),
        ("Threat Modeling", "Security", "Security analysis"),
        ("Penetration Testing", "Security", "Security testing"),
        ("TLS", "Security", "Transport security"),
        ("CORS", "Security", "Cross-origin resource sharing"),
        ("CSP", "Security", "Content Security Policy"),
        ("SSO", "Security", "Single sign-on"),

        // Mobile
        ("Android", "Mobile", "Mobile platform"),
        ("Jetpack Compose", "Mobile", "Android UI toolkit"),
        ("Room", "Mobile", "Android persistence"),
        ("Retrofit", "Mobile", "HTTP client"),
        ("OkHttp", "Mobile", "HTTP client"),
        ("Hilt", "Mobile", "Dependency injection"),
        ("Koin", "Mobile", "Dependency injection"),
        ("SwiftUI", "Mobile", "iOS UI framework"),
        ("UIKit", "Mobile", "iOS UI framework"),
        ("Combine", "Mobile", "Reactive framework"),
        ("React Native", "Mobile", "Cross-platform mobile"),
        ("Flutter", "Mobile", "Cross-platform UI"),
        ("Xcode", "Mobile", "Apple IDE"),
        ("Android Studio", "Mobile", "Android IDE"),

        // Data
        ("Pandas", "Data", "Data analysis"),
        ("NumPy", "Data", "Numerical computing"),
        ("Apache Spark", "Data", "Distributed processing"),
        ("Apache Flink", "Data", "Stream processing"),
        ("Airflow", "Data", "Workflow orchestration"),
        ("dbt", "Data", "Data transformation"),
        ("Kafka Connect", "Data", "Streaming integration"),

        // AI/ML
        ("ML.NET", "AI/ML", "Machine learning for .NET"),
        ("PyTorch", "AI/ML", "Deep learning"),
        ("TensorFlow", "AI/ML", "Machine learning"),
        ("scikit-learn", "AI/ML", "Machine learning"),
        ("OpenAI API", "AI/ML", "LLM API"),
        ("LangChain", "AI/ML", "LLM orchestration"),
        ("LlamaIndex", "AI/ML", "RAG framework"),
        ("Vector Search", "AI/ML", "Semantic retrieval"),
        ("RAG", "AI/ML", "Retrieval-Augmented Generation"),

        // Testing
        ("Playwright", "Testing", "End-to-end testing"),
        ("Cypress", "Testing", "End-to-end testing"),
        ("Selenium", "Testing", "Browser automation"),
        ("Jest", "Testing", "JavaScript testing"),
        ("Vitest", "Testing", "JavaScript testing"),
        ("Testing Library", "Testing", "UI testing"),
        ("xUnit", "Testing", ".NET unit testing"),
        ("NUnit", "Testing", ".NET unit testing"),
        ("MSTest", "Testing", ".NET unit testing"),
        ("Postman", "Testing", "API testing"),

        // Design / Product
        ("Figma", "Design", "Design and prototyping"),
        ("UI Design", "Design", "Interface design"),
        ("UX Research", "Design", "User research"),
        ("Design Systems", "Design", "Component systems"),
        ("User Testing", "Design", "Usability testing"),
        ("Product Management", "Product", "Product strategy"),
        ("Agile", "Product", "Delivery methodology"),
        ("Scrum", "Product", "Delivery framework"),
        ("Kanban", "Product", "Flow-based delivery"),
    ];

    private static readonly (string Code, string Title, string Description, string Category, int Points)[] AchievementsSeedData =
    [
        ("first_project",      "First Project",         "Created your first project on DevHunt.",                    "projects",  10),
        ("three_projects",     "Getting Started",       "Created 3 projects on DevHunt.",                            "projects",  15),
        ("five_projects",      "Prolific Creator",      "Created 5 projects on DevHunt.",                            "projects",  25),
        ("ten_projects",       "Project Machine",       "Created 10 projects — you're unstoppable!",                 "projects",  50),
        ("project_completed",  "Mission Complete",      "Completed your first project successfully.",                 "projects",  20),
        ("three_completed",    "Hat Trick",             "Completed 3 projects.",                                     "projects",  40),
        ("five_completed",     "Finisher",              "Completed 5 projects.",                                     "projects",  60),
        ("featured_project",   "Featured",              "One of your projects was featured by the community.",        "projects",  30),
        ("showcase_published", "Show & Tell",           "Published your first project showcase.",                     "projects",  15),
        ("project_published",  "Going Live",            "Published a project from draft to recruiting.",              "projects",  10),
        ("first_team",         "Team Player",           "Joined your first team.",                                   "teamwork",  10),
        ("three_teams",        "Networking",            "Participated in 3 different teams.",                         "teamwork",  20),
        ("five_teams",         "Collaborator",          "Participated in 5 different teams.",                         "teamwork",  35),
        ("ten_teams",          "Team Hopper",           "Participated in 10 different teams.",                        "teamwork",  60),
        ("team_leader",        "Team Leader",           "Became a leader of a project team.",                         "teamwork",  25),
        ("project_owner",      "Captain",               "Own 3 or more projects.",                                   "teamwork",  30),
        ("big_team",           "Recruiter",             "Built a team of 5+ active members on one project.",          "teamwork",  25),
        ("multi_role",         "Jack of All Trades",    "Held 3 different roles across teams.",                       "teamwork",  20),
        ("first_follower",     "First Follower",        "Got your first follower on DevHunt.",                       "social",    10),
        ("ten_followers",      "Growing Audience",      "Reached 10 followers.",                                     "social",    20),
        ("popular",            "Rising Star",           "Reached 50 followers.",                                     "social",    40),
        ("influencer",         "Influencer",            "Reached 100 followers.",                                    "social",    70),
        ("first_review",       "First Review",          "Wrote your first project review.",                          "social",    10),
        ("helpful",            "Helpful Hand",          "Wrote 10 reviews.",                                         "social",    20),
        ("first_comment",      "Commentator",           "Left your first showcase comment.",                         "social",    10),
        ("social_butterfly",   "Social Butterfly",      "Following 20 or more users.",                               "social",    15),
        ("profile_complete",   "Profile Complete",      "Filled in all profile fields — bio, skills, and links.",    "profile",   15),
        ("verified",           "Verified",              "Verified your email and identity.",                          "profile",   10),
        ("early_adopter",      "Early Adopter",         "Joined DevHunt during the early stages.",                   "profile",   50),
        ("avatar_set",         "Picture Perfect",       "Uploaded a profile avatar.",                                 "profile",    5),
        ("skilled",            "Skill Collector",       "Added 5 or more skills to your profile.",                   "profile",   10),
        ("github_connected",   "Open Door",             "Connected your GitHub account.",                             "profile",   10),
        ("social_presence",    "Well Connected",        "Filled in all 3 social links (GitHub, LinkedIn, Website).", "profile",   10),
        ("first_task",         "First Task",            "Completed your first task in a project.",                   "activity",  10),
        ("ten_tasks",          "Productive",            "Completed 10 tasks across all projects.",                   "activity",  20),
        ("task_master",        "Task Master",           "Completed 50 tasks across all projects.",                   "activity",  40),
        ("hundred_tasks",      "Centurion",             "Completed 100 tasks.",                                      "activity",  75),
        ("first_message",      "Ice Breaker",           "Sent your first chat message.",                             "activity",   5),
        ("chatterbox",         "Chatterbox",            "Sent 100 chat messages.",                                   "activity",  15),
        ("streak_7",           "Week Warrior",          "Maintained a 7-day activity streak.",                       "activity",  20),
        ("streak_30",          "Unstoppable",           "Maintained a 30-day activity streak!",                      "activity",  60),
        ("bug_hunter",         "Bug Hunter",            "Reported 5 project issues.",                                "special",   25),
        ("mentor",             "Mentor",                "Received a curator or admin role.",                          "special",   35),
        ("open_source",        "Open Source Champion",   "Connected an open-source repository to DevHunt.",          "special",   20),
        ("first_contribution", "First Contribution",    "Joined someone else's project as a team member.",           "special",   15),
        ("code_reviewer",      "Code Reviewer",         "Wrote peer reviews for 10 different users.",                "special",   30),
        ("polyglot",           "Polyglot",              "Used 5+ different technologies across your projects.",      "special",   20),
        ("top_rated",          "Top Rated",             "Achieved a rating of 4.5+ with at least 3 reviews.",       "special",   40),
        ("first_moderation",   "Rookie Moderator",      "Processed your first moderation report.",                  "moderation", 15),
        ("mod_veteran",        "Mod Veteran",           "Processed 50 moderation reports.",                         "moderation", 50),
        ("guardian",           "Guardian",              "Blocked 5 rule-violating users.",                          "moderation", 30),
        ("verifier",           "Verifier",              "Verified 10 user accounts.",                               "moderation", 25),
        ("issue_resolver",     "Issue Resolver",        "Resolved 10 project issues.",                              "moderation", 35),
        ("support_hero",       "Support Hero",          "Resolved 25 support tickets.",                             "moderation", 45),
        ("curator_star",       "Curator Star",          "Featured 10 projects or showcases.",                       "moderation", 40),
        ("watchdog",           "Watchdog",              "Escalated 5 critical issues.",                             "moderation", 20),
    ];

    #endregion

    private record SeederContext(DevHuntDbContext Db, DateTime Baseline, ILogger Logger);

    static async Task<int> Main(string[] args)
    {
        EnvLoader.Load();

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<Program>();

        logger.LogInformation("=== DevHunt Database Seeder Starting ===");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            logger.LogError("Connection string 'DefaultConnection' is not configured");
            return 1;
        }

        logger.LogInformation("Connecting to database...");

        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseLoggerFactory(loggerFactory);

        var maxAttempts = 10;
        var attempt = 0;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                logger.LogInformation("Attempt {Attempt}/{MaxAttempts} to connect", attempt, maxAttempts);

                using var dbContext = new DevHuntDbContext(optionsBuilder.Options);

                var canConnect = await dbContext.Database.CanConnectAsync();
                if (!canConnect)
                {
                    logger.LogError("Cannot connect to database. Please ensure database exists and migrations are applied.");
                    return 1;
                }

                logger.LogInformation("Seeding database...");
                await SeedDatabaseAsync(dbContext, logger);

                logger.LogInformation("=== Seeding completed successfully ===");
                return 0;
            }
            catch (Exception ex) when (ex is NpgsqlException or TimeoutException or DbUpdateException)
            {
                if (attempt == maxAttempts)
                {
                    logger.LogError(ex, "Failed to seed database after {Attempts} attempts", attempt);
                    return 1;
                }

                var delaySeconds = Math.Pow(2, attempt);
                logger.LogWarning(ex,
                    "Attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds}s...",
                    attempt, maxAttempts, delaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during seeding: {Message}", ex.Message);
                return 1;
            }
        }

        return 1;
    }

    private static async Task SeedDatabaseAsync(DevHuntDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var baseline = new DateTime(2025, 1, 15, 9, 0, 0, DateTimeKind.Utc);

        // E2E user (deterministic login for tests)
        await EnsureE2EUserAsync(db, logger);

        // Seed rich user accounts
        logger.LogInformation("Seeding users...");
        await SeedUsersAsync(db, logger, baseline, ct);
        await db.SaveChangesAsync(ct);

        var users = await db.Users.ToListAsync(ct);
        if (users.Count == 0)
        {
            logger.LogWarning("No users found; aborting.");
            return;
        }
        logger.LogInformation("Found {Count} users", users.Count);

        // Skills
        logger.LogInformation("Seeding skills...");
        var skills = await SeedSkillsAsync(db, logger, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding skill aliases...");
        await SeedSkillAliasesAsync(db, skills, logger, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding user skills...");
        await SeedUserSkillsAsync(db, users, skills, logger, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding achievements...");
        await SeedAchievementsAsync(db, logger, ct);
        await db.SaveChangesAsync(ct);

        var ctx = new SeederContext(db, baseline, logger);

        // Projects + kanban + tasks
        logger.LogInformation("Seeding projects...");
        var projects = await SeedProjectsAsync(ctx, users, skills, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding task columns...");
        var columnMap = await SeedTaskColumnsAsync(ctx, projects, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding team members...");
        await SeedTeamMembersAsync(ctx, projects, users, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding invitations...");
        await SeedInvitationsAsync(ctx, projects, users, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding tasks...");
        await SeedTasksAsync(ctx, projects, users, columnMap, ct);
        await db.SaveChangesAsync(ct);

        // Rich content
        logger.LogInformation("Seeding project news posts...");
        await SeedNewsPostsAsync(ctx, projects, users, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding reviews...");
        await SeedReviewsAsync(ctx, projects, users, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding support tickets...");
        await SeedSupportTicketsAsync(ctx, users, projects, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding feedback items...");
        await SeedFeedbackAsync(ctx, users, projects, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding moderation reports...");
        await SeedModerationReportsAsync(ctx, users, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Database seeding completed!");
    }

    // ── Users ────────────────────────────────────────────────────────────────

    private static async Task SeedUsersAsync(DevHuntDbContext db, ILogger logger, DateTime baseline, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(u => u.Role != "participant" || true, ct))
        {
            var count = await db.Users.CountAsync(ct);
            // Only skip if we already have substantial data (more than just the E2E user)
            if (count > 2)
            {
                logger.LogInformation("Users already seeded ({Count}), skipping.", count);
                return;
            }
        }

        const string defaultPassword = "DevHunt123!";

        var usersData = new[]
        {
            (Email: "alex.chen@devhunt.dev",    Username: "alexchen",   FullName: "Alex Chen",         Role: "admin",
             Bio: "Platform admin and full-stack engineer. Love building developer tools and open-source communities.",
             Github: "https://github.com/alexchen",   Linkedin: "https://linkedin.com/in/alexchen",   Website: "https://alexchen.dev",
             GithubUsername: "alexchen", Experience: 8, Skills: new[]{"C#",".NET","React","TypeScript","PostgreSQL","Docker","Kubernetes"},
             DaysAgo: 120, Rating: 4.9f),

            (Email: "maria.santos@devhunt.dev", Username: "mariasantos", FullName: "Maria Santos",      Role: "curator",
             Bio: "Community curator and UX advocate. I help projects find their best teammates and shine.",
             Github: "https://github.com/mariasantos", Linkedin: "https://linkedin.com/in/maria-santos-dev", Website: "https://mariasantos.io",
             GithubUsername: "mariasantos", Experience: 6, Skills: new[]{"Figma","UI Design","UX Research","React","TypeScript","Product Management"},
             DaysAgo: 110, Rating: 4.8f),

            (Email: "alice.johnson@devhunt.dev", Username: "alicej",     FullName: "Alice Johnson",     Role: "participant",
             Bio: "Frontend engineer obsessed with performance and accessibility. React + TypeScript all day.",
             Github: "https://github.com/alicej",    Linkedin: "https://linkedin.com/in/alicej",    Website: "https://alicej.dev",
             GithubUsername: "alicej", Experience: 5, Skills: new[]{"React","TypeScript","Next.js","Tailwind CSS","Vite","Playwright"},
             DaysAgo: 95, Rating: 4.7f),

            (Email: "bob.williams@devhunt.dev",  Username: "bobw",        FullName: "Bob Williams",      Role: "participant",
             Bio: "Backend developer specializing in .NET and distributed systems. Always chasing the perfect API design.",
             Github: "https://github.com/bobw",     Linkedin: "https://linkedin.com/in/bobwilliams", Website: null,
             GithubUsername: "bobw", Experience: 7, Skills: new[]{"C#",".NET","ASP.NET Core","PostgreSQL","Redis","RabbitMQ","Docker"},
             DaysAgo: 88, Rating: 4.6f),

            (Email: "carol.martinez@devhunt.dev", Username: "carolm",     FullName: "Carol Martinez",    Role: "participant",
             Bio: "Full-stack developer and open-source contributor. I enjoy building products that solve real problems.",
             Github: "https://github.com/carolm",   Linkedin: "https://linkedin.com/in/carolmartinez", Website: "https://carolm.codes",
             GithubUsername: "carolm", Experience: 4, Skills: new[]{"TypeScript","Node.js","React","PostgreSQL","GraphQL","Docker"},
             DaysAgo: 75, Rating: 4.5f),

            (Email: "dave.kim@devhunt.dev",      Username: "davekim",    FullName: "Dave Kim",          Role: "participant",
             Bio: "Mobile engineer focused on React Native and Flutter. I build apps people actually enjoy using.",
             Github: "https://github.com/davekim",  Linkedin: "https://linkedin.com/in/davekim",   Website: null,
             GithubUsername: "davekim", Experience: 3, Skills: new[]{"React Native","Flutter","TypeScript","Kotlin","Swift","Firebase"},
             DaysAgo: 62, Rating: 4.3f),

            (Email: "emma.davis@devhunt.dev",    Username: "emmad",      FullName: "Emma Davis",        Role: "participant",
             Bio: "Product designer turned frontend dev. I care deeply about the intersection of design and engineering.",
             Github: "https://github.com/emmad",    Linkedin: "https://linkedin.com/in/emmadesigns", Website: "https://emmadavis.design",
             GithubUsername: "emmad", Experience: 4, Skills: new[]{"Figma","React","TypeScript","Tailwind CSS","Design Systems","Storybook"},
             DaysAgo: 55, Rating: 4.6f),

            (Email: "frank.novak@devhunt.dev",   Username: "frankn",     FullName: "Frank Novak",       Role: "participant",
             Bio: "DevOps engineer and platform reliability enthusiast. Kubernetes, Terraform, and a good cup of coffee.",
             Github: "https://github.com/frankn",   Linkedin: "https://linkedin.com/in/franknovak",  Website: null,
             GithubUsername: "frankn", Experience: 6, Skills: new[]{"Docker","Kubernetes","Terraform","GitHub Actions","Prometheus","Grafana","Linux"},
             DaysAgo: 48, Rating: 4.4f),

            (Email: "grace.lee@devhunt.dev",     Username: "gracelee",   FullName: "Grace Lee",         Role: "participant",
             Bio: "ML engineer with a background in NLP and recommendation systems. Python and PyTorch evangelist.",
             Github: "https://github.com/gracelee", Linkedin: "https://linkedin.com/in/grace-lee-ml", Website: "https://gracelee.ai",
             GithubUsername: "gracelee", Experience: 5, Skills: new[]{"Python","PyTorch","TensorFlow","scikit-learn","FastAPI","PostgreSQL","RAG"},
             DaysAgo: 40, Rating: 4.8f),

            (Email: "henry.brown@devhunt.dev",   Username: "henryb",     FullName: "Henry Brown",       Role: "participant",
             Bio: "Data engineer passionate about building reliable data pipelines and analytics platforms.",
             Github: "https://github.com/henryb",   Linkedin: "https://linkedin.com/in/henrybrown",  Website: null,
             GithubUsername: "henryb", Experience: 5, Skills: new[]{"Python","Apache Spark","Kafka","PostgreSQL","dbt","Airflow","ClickHouse"},
             DaysAgo: 35, Rating: 4.3f),

            (Email: "ivan.petrov@devhunt.dev",   Username: "ivanp",      FullName: "Ivan Petrov",       Role: "participant",
             Bio: "Systems programmer in Rust and Go. I build things that are fast, safe, and correct — in that order.",
             Github: "https://github.com/ivanp",    Linkedin: "https://linkedin.com/in/ivanpetrov",  Website: "https://ivanpetrov.dev",
             GithubUsername: "ivanp", Experience: 6, Skills: new[]{"Rust","Go","C++","PostgreSQL","Redis","Kafka","Linux"},
             DaysAgo: 28, Rating: 4.7f),

            (Email: "julia.white@devhunt.dev",   Username: "juliaw",     FullName: "Julia White",       Role: "participant",
             Bio: "iOS developer with SwiftUI expertise. Making apps beautiful, accessible, and fun to use.",
             Github: "https://github.com/juliaw",   Linkedin: "https://linkedin.com/in/juliawhite",  Website: null,
             GithubUsername: "juliaw", Experience: 3, Skills: new[]{"Swift","SwiftUI","UIKit","Xcode","Combine","Kotlin"},
             DaysAgo: 20, Rating: 4.2f),
        };

        var now = DateTime.UtcNow;
        foreach (var u in usersData)
        {
            if (await db.Users.AnyAsync(x => x.Email == u.Email, ct)) continue;

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = u.Email,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role,
                Bio = u.Bio,
                Github = u.Github,
                Linkedin = u.Linkedin,
                Website = u.Website,
                GithubUsername = u.GithubUsername,
                Experience = u.Experience,
                Rating = u.Rating,
                Skills = u.Skills.ToList(),
                IsVerified = true,
                IsActive = true,
                IsEmailVerified = true,
                Language = "en",
                Timezone = "UTC",
                CreatedAt = baseline.AddDays(-u.DaysAgo),
                UpdatedAt = baseline.AddDays(-u.DaysAgo / 2),
                LastLogin = now.AddHours(-new Random(u.Email.GetHashCode()).Next(1, 72)),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
            });
        }

        logger.LogInformation("Users seeded ({Count} accounts, password: {Pwd})", usersData.Length, defaultPassword);
    }

    private static async Task EnsureE2EUserAsync(DevHuntDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var email = (Environment.GetEnvironmentVariable("DEVHUNT_E2E_USER_EMAIL") ?? "e2e@devhunt.local")
            .Trim().ToLowerInvariant();
        var password = Environment.GetEnvironmentVariable("DEVHUNT_E2E_USER_PASSWORD") ?? "TestPassword123!";

        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = "E2E User",
                Role = "participant",
                IsActive = true,
                IsVerified = true,
                IsEmailVerified = true,
                Language = "en",
                Timezone = "UTC",
                CreatedAt = DateTime.UtcNow,
                Skills = [],
            };
            db.Users.Add(user);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        user.IsEmailVerified = true;
        user.IsActive = true;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("E2E user ensured: {Email}", email);
    }

    // ── Skills ───────────────────────────────────────────────────────────────

    private static async Task<List<Skill>> SeedSkillsAsync(DevHuntDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var existing = await db.Skills.ToListAsync(ct);
        var byLowerName = existing
            .GroupBy(s => s.Name.Trim().ToLowerInvariant())
            .Select(g => g.First())
            .ToDictionary(s => s.Name.Trim().ToLowerInvariant(), s => s);

        var added = 0;
        var updated = 0;

        foreach (var (name, category, description) in SkillsSeedData)
        {
            var key = name.Trim().ToLowerInvariant();
            if (byLowerName.TryGetValue(key, out var existingSkill))
            {
                var changed = false;
                if (!string.Equals(existingSkill.Category, category, StringComparison.Ordinal))
                { existingSkill.Category = category; changed = true; }
                if (!string.Equals(existingSkill.Description, description, StringComparison.Ordinal))
                { existingSkill.Description = description; changed = true; }
                if (changed) updated++;
                continue;
            }

            var skill = new Skill
            {
                Id = Guid.NewGuid(),
                Name = name,
                Category = category,
                Description = description,
                CreatedAt = now
            };
            db.Skills.Add(skill);
            byLowerName[key] = skill;
            added++;
        }

        logger.LogInformation("Skills: {Existing} existing, {Added} added, {Updated} updated", existing.Count, added, updated);
        return byLowerName.Values.OrderBy(s => s.Category).ThenBy(s => s.Name).ToList();
    }

    private static async Task SeedAchievementsAsync(DevHuntDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var existing = await db.Achievements.ToListAsync(ct);
        var byCode = existing.ToDictionary(a => a.Code, a => a, StringComparer.OrdinalIgnoreCase);
        var added = 0; var updated = 0;

        foreach (var (code, title, description, category, points) in AchievementsSeedData)
        {
            if (byCode.TryGetValue(code, out var ea))
            {
                var changed = false;
                if (!string.Equals(ea.Title, title)) { ea.Title = title; changed = true; }
                if (!string.Equals(ea.Description, description)) { ea.Description = description; changed = true; }
                if (!string.Equals(ea.Category, category)) { ea.Category = category; changed = true; }
                if (ea.Points != points) { ea.Points = points; changed = true; }
                if (changed) updated++;
                continue;
            }
            db.Achievements.Add(new Achievement
            {
                Id = Guid.NewGuid(), Code = code, Title = title,
                Description = description, Category = category, Points = points,
            });
            byCode[code] = null!;
            added++;
        }
        logger.LogInformation("Achievements: {Existing} existing, {Added} added, {Updated} updated", existing.Count, added, updated);
    }

    private static string NormalizeAlias(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Trim().ToLowerInvariant();
        s = s.Replace("c#", "csharp").Replace("c++", "cplusplus").Replace("c plus plus", "cplusplus")
             .Replace(".net", "dotnet").Replace("asp.net", "aspnet")
             .Replace("node.js", "nodejs").Replace("next.js", "nextjs")
             .Replace("react native", "reactnative");
        s = System.Text.RegularExpressions.Regex.Replace(s, "[^a-z0-9]+", "");
        return s;
    }

    private static async Task SeedSkillAliasesAsync(DevHuntDbContext db, List<Skill> skills, ILogger logger, CancellationToken ct = default)
    {
        var existingSet = (await db.SkillAliases.AsNoTracking().Select(a => a.AliasNormalized).ToListAsync(ct))
            .Where(a => !string.IsNullOrWhiteSpace(a)).ToHashSet(StringComparer.Ordinal);

        var byName = skills.GroupBy(s => s.Name).Select(g => g.First()).ToDictionary(s => s.Name, s => s.Id);

        Guid IdOf(string n) => byName.TryGetValue(n, out var id) ? id : throw new InvalidOperationException($"Missing skill: {n}");

        var aliasPairs = new List<(string Alias, string SkillName)>
        {
            ("js", "JavaScript"), ("javascript", "JavaScript"), ("ts", "TypeScript"), ("typescript", "TypeScript"),
            ("py", "Python"), ("python3", "Python"), ("golang", "Go"), ("csharp", "C#"), ("fsharp", "F#"),
            ("vb", "Visual Basic .NET"), ("vbnet", "Visual Basic .NET"), ("cplusplus", "C++"), ("c++", "C++"),
            ("objectivec", "Objective-C"), ("objc", "Objective-C"), ("nextjs", "Next.js"), ("next.js", "Next.js"),
            ("rn", "React Native"), ("reactnative", "React Native"), ("reactjs", "React"), ("react.js", "React"),
            ("vue", "Vue.js"), ("vuejs", "Vue.js"), ("nuxtjs", "Nuxt"), ("tailwind", "Tailwind CSS"),
            ("tailwindcss", "Tailwind CSS"), ("dotnet", ".NET"), (".net", ".NET"), ("aspnet", ".NET"),
            ("asp.net", "ASP.NET Core"), ("aspnetcore", "ASP.NET Core"), ("c#", "C#"),
            ("k8s", "Kubernetes"), ("android dev", "Android"), ("compose", "Jetpack Compose"),
            ("jetpackcompose", "Jetpack Compose"), ("postgres", "PostgreSQL"), ("psql", "PostgreSQL"),
            ("postgresql", "PostgreSQL"), ("mongo", "MongoDB"), ("mongodb", "MongoDB"),
            ("mssql", "SQL Server"), ("sqlserver", "SQL Server"), ("redis", "Redis"),
            ("gh actions", "GitHub Actions"), ("githubactions", "GitHub Actions"),
            ("tf", "Terraform"), ("kafka", "Kafka"), ("grpc", "gRPC"), ("graphql", "GraphQL"),
        };

        var now = DateTime.UtcNow;
        var toAdd = new List<SkillAlias>();
        foreach (var (alias, skillName) in aliasPairs)
        {
            var normalized = NormalizeAlias(alias);
            if (string.IsNullOrWhiteSpace(normalized) || !existingSet.Add(normalized)) continue;
            toAdd.Add(new SkillAlias { Id = Guid.NewGuid(), SkillId = IdOf(skillName), Alias = alias, AliasNormalized = normalized, CreatedAt = now });
        }

        if (toAdd.Count > 0) db.SkillAliases.AddRange(toAdd);
        logger.LogInformation("Skill aliases: {Added} added", toAdd.Count);
    }

    private static async Task SeedUserSkillsAsync(DevHuntDbContext db, List<User> users, List<Skill> skills, ILogger logger, CancellationToken ct = default)
    {
        if (await db.UserSkills.AnyAsync(ct) && await db.UserSkillEntries.AnyAsync(ct))
        {
            logger.LogInformation("User skills already exist, skipping.");
            return;
        }

        var skillMap = skills.ToDictionary(s => s.Name, s => s.Id);
        var aliasMap = await db.SkillAliases.AsNoTracking()
            .GroupBy(a => a.AliasNormalized).Select(g => g.First())
            .ToDictionaryAsync(a => a.AliasNormalized, a => a.SkillId, ct);

        var existingEntrySet = (await db.UserSkillEntries.AsNoTracking()
            .Select(x => new { x.UserId, x.RawNormalized }).ToListAsync(ct))
            .Where(x => !string.IsNullOrWhiteSpace(x.RawNormalized))
            .Select(x => (x.UserId, x.RawNormalized))
            .ToHashSet();

        var hasUserSkills = await db.UserSkills.AnyAsync(ct);
        var userSkills = new List<UserSkill>();
        var userSkillEntries = new List<UserSkillEntry>();

        foreach (var user in users.Where(u => u.Skills is { Count: > 0 }))
        {
            foreach (var raw in user.Skills!)
            {
                var rawNorm = NormalizeAlias(raw);
                if (string.IsNullOrEmpty(rawNorm)) continue;

                var key = (user.Id, rawNorm);
                if (!existingEntrySet.Add(key)) continue;

                Guid? resolvedId = skillMap.TryGetValue(raw, out var sid) ? sid
                    : aliasMap.TryGetValue(rawNorm, out var aid) ? aid : null;

                userSkillEntries.Add(new UserSkillEntry
                {
                    Id = Guid.NewGuid(), UserId = user.Id, SkillId = resolvedId,
                    Raw = raw, RawNormalized = rawNorm, CreatedAt = DateTime.UtcNow
                });

                if (!hasUserSkills && skillMap.TryGetValue(raw, out var skillId))
                {
                    var exp = user.Experience ?? 0;
                    userSkills.Add(new UserSkill
                    {
                        Id = Guid.NewGuid(), UserId = user.Id, SkillId = skillId,
                        ProficiencyLevel = exp < 2 ? "beginner" : exp < 5 ? "intermediate" : exp < 8 ? "advanced" : "expert",
                        YearsOfExperience = user.Experience, Verified = user.IsVerified, CreatedAt = DateTime.UtcNow
                    });
                }
            }
        }

        if (!hasUserSkills && userSkills.Count > 0) db.UserSkills.AddRange(userSkills);
        if (userSkillEntries.Count > 0) db.UserSkillEntries.AddRange(userSkillEntries);
        logger.LogInformation("User skills: {Skills} entries, {Entries} raw entries", userSkills.Count, userSkillEntries.Count);
    }

    // ── Projects ─────────────────────────────────────────────────────────────

    private static async Task<List<Project>> SeedProjectsAsync(SeederContext ctx, List<User> users, List<Skill> skills, CancellationToken ct = default)
    {
        var existing = await ctx.Db.Projects.ToListAsync(ct);
        if (existing.Count > 0)
        {
            ctx.Logger.LogInformation("Projects already exist ({Count}), skipping.", existing.Count);
            return existing;
        }

        var skillMap = skills.ToDictionary(s => s.Name, s => s.Id);

        // Pick named users for predictable ownership
        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var alice   = Get("alicej");
        var bob     = Get("bobw");
        var carol   = Get("carolm");
        var dave    = Get("davekim");
        var grace   = Get("gracelee");
        var ivan    = Get("ivanp");

        var projects = new List<Project>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "EcoTracker — Environmental Monitoring Platform",
                Slug = "ecotracker",
                ShortDescription = "Real-time environmental data tracking and open visualization",
                Description = "<p>A comprehensive platform for tracking environmental metrics including air quality, water quality, and biodiversity indexes. Features real-time sensor data collection, interactive maps, alerting, and predictive analytics powered by ML models.</p><p>We are looking for engineers passionate about climate tech and data visualization.</p>",
                Status = "recruiting",
                Visibility = "public",
                OwnerId = alice.Id,
                DifficultyLevel = "intermediate",
                ExpectedDurationDays = 90,
                MaxTeamSize = 6,
                RequiredRoles = ["Frontend Developer", "Backend Developer", "Data Scientist", "UI/UX Designer"],
                TechStack = ["React", "Next.js", ".NET", "PostgreSQL", "Python"],
                Featured = true,
                Rating = 4.7f,
                BoostsCount = 34,
                CreatedAt = ctx.Baseline.AddDays(-60),
                UpdatedAt = ctx.Baseline.AddDays(-2),
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "DevHunt Mobile App",
                Slug = "devhunt-mobile",
                ShortDescription = "Native mobile companion for the DevHunt platform",
                Description = "<p>Cross-platform mobile application for iOS and Android that lets users browse projects, manage their teams, and get real-time notifications on the go. Built with React Native and backed by the existing DevHunt API.</p>",
                Status = "active",
                Visibility = "public",
                OwnerId = dave.Id,
                DifficultyLevel = "advanced",
                ExpectedDurationDays = 120,
                MaxTeamSize = 5,
                RequiredRoles = ["Mobile Developer", "Backend Developer", "UI/UX Designer"],
                TechStack = ["React Native", "TypeScript", ".NET", "PostgreSQL"],
                Featured = false,
                Rating = 4.4f,
                BoostsCount = 18,
                StartDate = ctx.Baseline.AddDays(-50),
                CreatedAt = ctx.Baseline.AddDays(-55),
                UpdatedAt = ctx.Baseline.AddDays(-1),
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "AI Code Review Assistant",
                Slug = "ai-code-review",
                ShortDescription = "LLM-powered automated code review for GitHub PRs",
                Description = "<p>An AI assistant that integrates with GitHub to automatically review pull requests, detect code smells, suggest refactors, and explain diffs in plain English. Uses OpenAI and custom fine-tuned models.</p><p>Currently in active development — we ship every two weeks.</p>",
                Status = "active",
                Visibility = "public",
                OwnerId = grace.Id,
                DifficultyLevel = "advanced",
                ExpectedDurationDays = 80,
                MaxTeamSize = 4,
                RequiredRoles = ["ML Engineer", "Backend Developer", "DevOps Engineer"],
                TechStack = ["Python", "FastAPI", "OpenAI API", "PostgreSQL", "Docker"],
                Featured = true,
                Rating = 4.9f,
                BoostsCount = 67,
                StartDate = ctx.Baseline.AddDays(-45),
                CreatedAt = ctx.Baseline.AddDays(-50),
                UpdatedAt = ctx.Baseline.AddDays(-1),
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "TaskFlow — Smart Kanban for Developers",
                Slug = "taskflow",
                ShortDescription = "Opinionated Kanban board built for dev teams",
                Description = "<p>A minimalist yet powerful Kanban board with Git integration, time tracking, and smart automation. Unlike Jira — it stays out of your way. Built by developers, for developers.</p><p>Fully completed and open-source. Looking for contributors to help with v2.</p>",
                Status = "completed",
                Visibility = "public",
                OwnerId = bob.Id,
                DifficultyLevel = "intermediate",
                ExpectedDurationDays = 60,
                MaxTeamSize = 4,
                RequiredRoles = ["Frontend Developer", "Backend Developer"],
                TechStack = ["React", "TypeScript", "Node.js", "PostgreSQL"],
                Featured = false,
                Rating = 4.6f,
                BoostsCount = 29,
                StartDate = ctx.Baseline.AddDays(-90),
                EndDate = ctx.Baseline.AddDays(-10),
                CreatedAt = ctx.Baseline.AddDays(-95),
                UpdatedAt = ctx.Baseline.AddDays(-10),
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "OpenDocs — Collaborative API Documentation",
                Slug = "opendocs",
                ShortDescription = "Real-time collaborative editor for API documentation",
                Description = "<p>A tool for teams to write, review, and publish API documentation together in real-time. Think Notion meets Swagger. Supports OpenAPI, custom markdown, and auto-generated docs from code annotations.</p>",
                Status = "draft",
                Visibility = "public",
                OwnerId = carol.Id,
                DifficultyLevel = "intermediate",
                ExpectedDurationDays = 75,
                MaxTeamSize = 4,
                RequiredRoles = ["Frontend Developer", "Backend Developer", "Technical Writer"],
                TechStack = ["TypeScript", "React", "Node.js", "PostgreSQL", "WebSockets"],
                Featured = false,
                Rating = null,
                BoostsCount = 5,
                CreatedAt = ctx.Baseline.AddDays(-15),
                UpdatedAt = ctx.Baseline.AddDays(-3),
            },
            new()
            {
                Id = Guid.NewGuid(),
                Title = "RustCache — High-Performance Distributed Cache",
                Slug = "rustcache",
                ShortDescription = "Redis-compatible distributed cache written in Rust",
                Description = "<p>A high-performance, Redis-protocol-compatible distributed cache built entirely in Rust. Targets sub-millisecond latencies and multi-GB datasets. Currently implements SET/GET/EXPIRE and cluster sharding.</p><p>Ideal for engineers interested in systems programming and distributed systems.</p>",
                Status = "recruiting",
                Visibility = "public",
                OwnerId = ivan.Id,
                DifficultyLevel = "expert",
                ExpectedDurationDays = 150,
                MaxTeamSize = 3,
                RequiredRoles = ["Systems Engineer", "Rust Developer"],
                TechStack = ["Rust", "Linux", "Docker", "Kubernetes"],
                Featured = false,
                Rating = 4.5f,
                BoostsCount = 41,
                CreatedAt = ctx.Baseline.AddDays(-30),
                UpdatedAt = ctx.Baseline.AddDays(-5),
            },
        };

        ctx.Db.Projects.AddRange(projects);
        await ctx.Db.SaveChangesAsync(ct);

        // ProjectTechStack join rows
        var pts = new List<ProjectTechStack>();
        foreach (var project in projects)
        {
            foreach (var tech in project.TechStack)
            {
                if (skillMap.TryGetValue(tech, out var sid))
                    pts.Add(new ProjectTechStack { Id = Guid.NewGuid(), ProjectId = project.Id, SkillId = sid, IsRequired = true, ProficiencyRequired = "intermediate" });
            }
        }
        ctx.Db.ProjectTechStacks.AddRange(pts);

        ctx.Logger.LogInformation("Projects seeded: {Count}", projects.Count);
        return projects;
    }

    // ── Task Columns ─────────────────────────────────────────────────────────

    private static async Task<Dictionary<Guid, List<TaskColumn>>> SeedTaskColumnsAsync(SeederContext ctx, List<Project> projects, CancellationToken ct = default)
    {
        if (await ctx.Db.TaskColumns.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Task columns already exist, skipping.");
            return await ctx.Db.TaskColumns.GroupBy(c => c.ProjectId)
                .ToDictionaryAsync(g => g.Key, g => g.ToList(), ct);
        }

        var now = DateTime.UtcNow;
        var result = new Dictionary<Guid, List<TaskColumn>>();
        var allColumns = new List<TaskColumn>();

        foreach (var project in projects)
        {
            var columns = new List<TaskColumn>
            {
                new() { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Backlog",     Position = 0, IsDefault = true,  IsCompleted = false, Color = "#6B7280", CreatedAt = now },
                new() { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "In Progress", Position = 1, IsDefault = false, IsCompleted = false, Color = "#3B82F6", CreatedAt = now },
                new() { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Review",      Position = 2, IsDefault = false, IsCompleted = false, Color = "#F59E0B", CreatedAt = now },
                new() { Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Done",        Position = 3, IsDefault = false, IsCompleted = true,  Color = "#10B981", CreatedAt = now },
            };
            result[project.Id] = columns;
            allColumns.AddRange(columns);
        }

        ctx.Db.TaskColumns.AddRange(allColumns);
        return result;
    }

    // ── Team Members ─────────────────────────────────────────────────────────

    private static async Task SeedTeamMembersAsync(SeederContext ctx, List<Project> projects, List<User> users, CancellationToken ct = default)
    {
        if (await ctx.Db.TeamMembers.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Team members already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var alice  = Get("alicej");
        var bob    = Get("bobw");
        var carol  = Get("carolm");
        var dave   = Get("davekim");
        var emma   = Get("emmad");
        var frank  = Get("frankn");
        var grace  = Get("gracelee");
        var henry  = Get("henryb");
        var ivan   = Get("ivanp");
        var julia  = Get("juliaw");

        var members = new List<TeamMember>();

        void AddOwner(Project p, DateTime? joinedOverride = null) => members.Add(new TeamMember
        {
            Id = Guid.NewGuid(), ProjectId = p.Id, UserId = p.OwnerId,
            Role = "Owner", Status = "active", IsLeader = true,
            CanManageTasks = true, CanPublishNews = true, CanManageFiles = true, CanManageGallery = true,
            JoinedAt = joinedOverride ?? p.CreatedAt,
        });

        void AddMember(Project p, User u, string role, string status = "active", bool leader = false,
            int daysAfterStart = 5, int? contribution = null) => members.Add(new TeamMember
        {
            Id = Guid.NewGuid(), ProjectId = p.Id, UserId = u.Id,
            Role = role, Status = status, IsLeader = leader,
            CanManageTasks = leader, CanPublishNews = leader,
            ContributionScore = contribution,
            JoinedAt = p.CreatedAt.AddDays(daysAfterStart),
        });

        var ecotracker = projects.First(p => p.Slug == "ecotracker");
        AddOwner(ecotracker);
        AddMember(ecotracker, bob,   "Backend Developer", daysAfterStart: 3, contribution: 85);
        AddMember(ecotracker, grace, "Data Scientist",    daysAfterStart: 7, contribution: 72);
        AddMember(ecotracker, emma,  "UI/UX Designer",    daysAfterStart: 5, contribution: 60);
        AddMember(ecotracker, frank, "DevOps Engineer",   daysAfterStart: 10, contribution: 45);

        var mobile = projects.First(p => p.Slug == "devhunt-mobile");
        AddOwner(mobile);
        AddMember(mobile, julia, "iOS Developer",     daysAfterStart: 4,  contribution: 68);
        AddMember(mobile, carol, "Backend Developer", daysAfterStart: 6,  contribution: 55);
        AddMember(mobile, emma,  "UI/UX Designer",    daysAfterStart: 8,  contribution: 40);

        var aiReview = projects.First(p => p.Slug == "ai-code-review");
        AddOwner(aiReview);
        AddMember(aiReview, henry, "Data Engineer",    daysAfterStart: 3, contribution: 78);
        AddMember(aiReview, bob,   "Backend Developer",daysAfterStart: 5, contribution: 62);
        AddMember(aiReview, frank, "DevOps Engineer",  daysAfterStart: 7, contribution: 50);

        var taskflow = projects.First(p => p.Slug == "taskflow");
        AddOwner(taskflow);
        AddMember(taskflow, alice, "Frontend Developer", daysAfterStart: 3, contribution: 90, leader: false);
        AddMember(taskflow, carol, "Full-stack Developer", daysAfterStart: 5, contribution: 75);

        var opendocs = projects.First(p => p.Slug == "opendocs");
        AddOwner(opendocs);

        var rustcache = projects.First(p => p.Slug == "rustcache");
        AddOwner(rustcache);
        AddMember(rustcache, frank, "Systems Engineer", daysAfterStart: 4, contribution: 55);

        ctx.Db.TeamMembers.AddRange(members);
        ctx.Logger.LogInformation("Team members seeded: {Count}", members.Count);
    }

    // ── Invitations ──────────────────────────────────────────────────────────

    private static async Task SeedInvitationsAsync(SeederContext ctx, List<Project> projects, List<User> users, CancellationToken ct = default)
    {
        if (await ctx.Db.Invitations.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Invitations already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var carol  = Get("carolm");
        var ivan   = Get("ivanp");
        var julia  = Get("juliaw");
        var henry  = Get("henryb");

        var invitations = new List<Invitation>();

        var opendocs  = projects.First(p => p.Slug == "opendocs");
        var rustcache = projects.First(p => p.Slug == "rustcache");
        var ecotracker = projects.First(p => p.Slug == "ecotracker");
        var mobile    = projects.First(p => p.Slug == "devhunt-mobile");

        invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), ProjectId = opendocs.Id, InviterId = opendocs.OwnerId, InviteeId = ivan.Id,
            Type = "invite", Role = "Backend Developer", Status = "pending",
            Message = "Hey Ivan! Your Rust background would be super valuable for our API layer. Interested?",
            CreatedAt = ctx.Baseline.AddDays(-5),
        });

        invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), ProjectId = opendocs.Id, InviterId = opendocs.OwnerId, InviteeId = henry.Id,
            Type = "invite", Role = "Technical Writer", Status = "pending",
            Message = "We need someone who can write clear, developer-friendly docs. Your profile is perfect!",
            CreatedAt = ctx.Baseline.AddDays(-4),
        });

        invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), ProjectId = rustcache.Id, InviterId = rustcache.OwnerId, InviteeId = carol.Id,
            Type = "invite", Role = "Systems Engineer", Status = "declined",
            Message = "Would love to have a full-stack perspective on the tooling side.",
            CreatedAt = ctx.Baseline.AddDays(-12), RespondedAt = ctx.Baseline.AddDays(-10),
        });

        invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), ProjectId = ecotracker.Id, InviterId = ecotracker.OwnerId, InviteeId = julia.Id,
            Type = "application", Role = "Frontend Developer", Status = "pending",
            Message = "I love climate tech! I'd be excited to contribute to the visualization layer.",
            CreatedAt = ctx.Baseline.AddDays(-3),
        });

        invitations.Add(new Invitation
        {
            Id = Guid.NewGuid(), ProjectId = mobile.Id, InviterId = mobile.OwnerId, InviteeId = ivan.Id,
            Type = "invite", Role = "Backend Developer", Status = "accepted",
            Message = "We shipped the first milestone — looking to scale the team. Your systems expertise is exactly what we need.",
            CreatedAt = ctx.Baseline.AddDays(-20), RespondedAt = ctx.Baseline.AddDays(-19),
        });

        ctx.Db.Invitations.AddRange(invitations);
        ctx.Logger.LogInformation("Invitations seeded: {Count}", invitations.Count);
    }

    // ── Tasks ────────────────────────────────────────────────────────────────

    private static async Task SeedTasksAsync(SeederContext ctx, List<Project> projects, List<User> users,
        Dictionary<Guid, List<TaskColumn>> columnMap, CancellationToken ct = default)
    {
        if (await ctx.Db.Tasks.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Tasks already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var alice  = Get("alicej");
        var bob    = Get("bobw");
        var carol  = Get("carolm");
        var dave   = Get("davekim");
        var grace  = Get("gracelee");
        var frank  = Get("frankn");
        var henry  = Get("henryb");
        var ivan   = Get("ivanp");

        var tasks = new List<TaskItem>();

        TaskColumn Col(Guid projectId, string name) =>
            columnMap.TryGetValue(projectId, out var cols)
                ? cols.First(c => c.Name == name)
                : throw new Exception($"Column '{name}' not found for project {projectId}");

        // EcoTracker
        var eco = projects.First(p => p.Slug == "ecotracker");
        tasks.AddRange(new[]
        {
            MakeTask(eco.Id, "Set up monorepo and CI/CD pipeline", "Configure GitHub Actions, Docker Compose for local dev, and staging env deployment.", "done",   "high",   bob.Id,   alice.Id,  ctx.Baseline.AddDays(-58), Col(eco.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-55), est: 8,  actual: 9),
            MakeTask(eco.Id, "Design sensor data ingestion API",   "REST + WebSocket endpoints to receive telemetry from edge devices.",                  "done",   "high",   bob.Id,   bob.Id,    ctx.Baseline.AddDays(-55), Col(eco.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-50), est: 12, actual: 11),
            MakeTask(eco.Id, "Build interactive map component",    "Leaflet.js map showing sensor locations with real-time AQI overlays.",                 "done",   "high",   alice.Id, alice.Id,  ctx.Baseline.AddDays(-50), Col(eco.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-40), est: 16, actual: 18),
            MakeTask(eco.Id, "Implement anomaly detection model",  "Python ML pipeline: detect pollution spikes using LSTM on time-series data.",          "review", "high",   grace.Id, grace.Id,  ctx.Baseline.AddDays(-20), Col(eco.Id,"Review"),      deadline: ctx.Baseline.AddDays(5),      est: 24),
            MakeTask(eco.Id, "Add alerting & notification system", "Email + push alerts when threshold values are exceeded.",                              "in-progress","medium",alice.Id,bob.Id, ctx.Baseline.AddDays(-10), Col(eco.Id,"In Progress"), deadline: ctx.Baseline.AddDays(8),      est: 12),
            MakeTask(eco.Id, "Write API documentation",            "OpenAPI spec + developer guide for third-party sensor integrations.",                  "todo",   "low",    alice.Id, null,      ctx.Baseline.AddDays(-5),  Col(eco.Id,"Backlog"),     deadline: ctx.Baseline.AddDays(14),     est: 8),
        });

        // DevHunt Mobile
        var mob = projects.First(p => p.Slug == "devhunt-mobile");
        tasks.AddRange(new[]
        {
            MakeTask(mob.Id, "Project scaffolding with Expo",      "Set up Expo managed workflow, navigation library, theming.",                           "done",   "high",   dave.Id,  dave.Id,   ctx.Baseline.AddDays(-53), Col(mob.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-50), est: 6,  actual: 7),
            MakeTask(mob.Id, "Auth screens (login/signup)",        "Build login, signup, forgot-password screens with API integration.",                   "done",   "high",   carol.Id, carol.Id,  ctx.Baseline.AddDays(-48), Col(mob.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-42), est: 16, actual: 14),
            MakeTask(mob.Id, "Project discovery feed",             "Infinite scroll feed with filter chips (tech stack, status, difficulty).",             "review", "high",   dave.Id,  dave.Id,   ctx.Baseline.AddDays(-15), Col(mob.Id,"Review"),      deadline: ctx.Baseline.AddDays(3),      est: 20),
            MakeTask(mob.Id, "Push notification integration",      "Firebase Cloud Messaging integration for project invites and messages.",               "in-progress","medium",carol.Id,carol.Id,ctx.Baseline.AddDays(-8), Col(mob.Id,"In Progress"), deadline: ctx.Baseline.AddDays(7),      est: 10),
            MakeTask(mob.Id, "iOS App Store submission prep",      "Screenshots, metadata, privacy policy, app review compliance checklist.",              "todo",   "medium", dave.Id,  null,      ctx.Baseline.AddDays(-2),  Col(mob.Id,"Backlog"),     deadline: ctx.Baseline.AddDays(20),     est: 6),
        });

        // AI Code Review
        var ai = projects.First(p => p.Slug == "ai-code-review");
        tasks.AddRange(new[]
        {
            MakeTask(ai.Id, "GitHub App OAuth integration",       "Implement GitHub App installation flow and webhook event handler.",                     "done",   "high",   bob.Id,   bob.Id,    ctx.Baseline.AddDays(-48), Col(ai.Id,"Done"),         completedAt: ctx.Baseline.AddDays(-43), est: 14, actual: 16),
            MakeTask(ai.Id, "LLM prompt engineering for reviews", "Design, test, and iterate on review prompts; evaluate diff chunk strategies.",         "done",   "high",   grace.Id, grace.Id,  ctx.Baseline.AddDays(-42), Col(ai.Id,"Done"),         completedAt: ctx.Baseline.AddDays(-35), est: 20, actual: 22),
            MakeTask(ai.Id, "Review result storage & history API","Persist review runs; expose history endpoint with pagination.",                         "done",   "medium", henry.Id, henry.Id,  ctx.Baseline.AddDays(-35), Col(ai.Id,"Done"),         completedAt: ctx.Baseline.AddDays(-28), est: 10, actual: 10),
            MakeTask(ai.Id, "Fine-tune model on project codebase","Collect labeled examples; fine-tune GPT-4o-mini for domain-specific review quality.",  "in-progress","high",grace.Id, grace.Id, ctx.Baseline.AddDays(-12), Col(ai.Id,"In Progress"), deadline: ctx.Baseline.AddDays(10),     est: 40),
            MakeTask(ai.Id, "Multi-language support (Go, Rust)",  "Extend tokenization and prompt templates to handle Go and Rust codebases.",             "todo",   "medium", grace.Id, null,      ctx.Baseline.AddDays(-4),  Col(ai.Id,"Backlog"),     deadline: ctx.Baseline.AddDays(18),     est: 16),
            MakeTask(ai.Id, "Rate limiting and cost controls",    "Per-user token budget, throttling, and billing-safe fallback to local models.",         "todo",   "medium", frank.Id, frank.Id,  ctx.Baseline.AddDays(-3),  Col(ai.Id,"Backlog"),     deadline: ctx.Baseline.AddDays(15),     est: 8),
        });

        // TaskFlow (completed project — all tasks done)
        var tf = projects.First(p => p.Slug == "taskflow");
        tasks.AddRange(new[]
        {
            MakeTask(tf.Id, "Core board drag-and-drop",           "Implement DnD with @dnd-kit; persist column/card order optimistically.",               "done",   "high",   bob.Id,   alice.Id,  ctx.Baseline.AddDays(-92), Col(tf.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-80), est: 20, actual: 22),
            MakeTask(tf.Id, "GitHub PR/issue sync",               "Webhook listener to auto-create tasks from GitHub issues and link PRs.",               "done",   "high",   bob.Id,   bob.Id,    ctx.Baseline.AddDays(-80), Col(tf.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-60), est: 24, actual: 28),
            MakeTask(tf.Id, "Time tracking widget",               "Inline timer, manual entry, and CSV export of time logs.",                             "done",   "medium", carol.Id, carol.Id,  ctx.Baseline.AddDays(-60), Col(tf.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-40), est: 16, actual: 14),
            MakeTask(tf.Id, "Burndown and velocity charts",       "Sprint burndown chart and rolling 2-week velocity graph.",                             "done",   "medium", bob.Id,   carol.Id,  ctx.Baseline.AddDays(-40), Col(tf.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-20), est: 12, actual: 13),
            MakeTask(tf.Id, "v1.0 release and documentation",     "Tag release, publish changelog, write README + getting-started guide.",                "done",   "high",   bob.Id,   bob.Id,    ctx.Baseline.AddDays(-20), Col(tf.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-10), est: 8,  actual: 7),
        });

        // RustCache
        var rc = projects.First(p => p.Slug == "rustcache");
        tasks.AddRange(new[]
        {
            MakeTask(rc.Id, "Async TCP listener with Tokio",      "Implement non-blocking TCP server accepting RESP3 protocol frames.",                   "done",   "high",   ivan.Id,  ivan.Id,   ctx.Baseline.AddDays(-28), Col(rc.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-22), est: 16, actual: 18),
            MakeTask(rc.Id, "SET / GET / DEL / EXPIRE commands",  "Core command handlers with TTL eviction using background timer tasks.",               "done",   "high",   ivan.Id,  ivan.Id,   ctx.Baseline.AddDays(-22), Col(rc.Id,"Done"),        completedAt: ctx.Baseline.AddDays(-15), est: 20, actual: 19),
            MakeTask(rc.Id, "Consistent hashing for cluster mode","Implement ring hash for key routing across N nodes.",                                  "in-progress","high",frank.Id, frank.Id, ctx.Baseline.AddDays(-10), Col(rc.Id,"In Progress"), deadline: ctx.Baseline.AddDays(12),     est: 30),
            MakeTask(rc.Id, "Benchmarking suite vs Redis",        "Criterion-based benchmarks: throughput and p99 latency at 1M ops/sec.",               "todo",   "medium", ivan.Id,  null,      ctx.Baseline.AddDays(-5),  Col(rc.Id,"Backlog"),     deadline: ctx.Baseline.AddDays(20),     est: 12),
        });

        ctx.Db.Tasks.AddRange(tasks);
        ctx.Logger.LogInformation("Tasks seeded: {Count}", tasks.Count);
    }

    private static TaskItem MakeTask(
        Guid projectId, string title, string description, string status, string priority,
        Guid createdBy, Guid? assignedTo, DateTime createdAt, TaskColumn column,
        DateTime? completedAt = null, DateTime? deadline = null, float? est = null, float? actual = null)
        => new()
        {
            Id = Guid.NewGuid(), ProjectId = projectId, Title = title, Description = description,
            Status = status, Priority = priority, CreatedByUserId = createdBy, AssignedToUserId = assignedTo,
            CreatedAt = createdAt, UpdatedAt = createdAt.AddDays(1), CompletedAt = completedAt,
            Deadline = deadline, EstimatedHours = est, ActualHours = actual, IsDeleted = false,
            ColumnId = column.Id, PositionInColumn = 0,
        };

    // ── News Posts ────────────────────────────────────────────────────────────

    private static async Task SeedNewsPostsAsync(SeederContext ctx, List<Project> projects, List<User> users, CancellationToken ct = default)
    {
        if (await ctx.Db.ProjectNewsPosts.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("News posts already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];

        var posts = new List<ProjectNewsPost>();
        var likes = new List<NewsPostLike>();

        void AddPost(Project p, User author, string title, string content, bool pinned, int daysAgo, int likesCount, string visibility = "public")
        {
            var post = new ProjectNewsPost
            {
                Id = Guid.NewGuid(), ProjectId = p.Id, AuthorId = author.Id,
                Title = title, Content = content, Visibility = visibility,
                IsPinned = pinned, CreatedAt = ctx.Baseline.AddDays(-daysAgo),
                UpdatedAt = ctx.Baseline.AddDays(-daysAgo), LikesCount = likesCount,
            };
            posts.Add(post);

            // Add actual like rows for a few users
            var likerPool = users.Where(u => u.Id != author.Id).Take(likesCount).ToList();
            foreach (var liker in likerPool)
                likes.Add(new NewsPostLike { NewsPostId = post.Id, UserId = liker.Id, CreatedAt = ctx.Baseline.AddDays(-daysAgo + 1) });
        }

        var eco  = projects.First(p => p.Slug == "ecotracker");
        var mob  = projects.First(p => p.Slug == "devhunt-mobile");
        var ai   = projects.First(p => p.Slug == "ai-code-review");
        var tf   = projects.First(p => p.Slug == "taskflow");

        AddPost(eco, Get("alicej"),
            "🌱 EcoTracker v0.3 — real-time map is live!",
            "<p>Huge milestone this week: our interactive sensor map is finally live on staging. You can now see AQI readings update in real-time across 12 test locations. Huge thanks to <strong>Bob</strong> for the ingestion API and <strong>Grace</strong> for the anomaly model integration.</p><p>Next up: alerting system and public beta invite. Stay tuned!</p>",
            true, 5, 8);

        AddPost(eco, Get("bobw"),
            "Sensor API documentation released",
            "<p>We've published the full REST + WebSocket API spec for third-party sensor manufacturers. Check the /docs endpoint on staging. Early adopters welcome — DM me for an API key.</p>",
            false, 18, 4);

        AddPost(eco, Get("alicej"),
            "Team update — welcoming Emma and Frank",
            "<p>Excited to introduce two new team members: <strong>Emma</strong> (UI/UX) and <strong>Frank</strong> (DevOps). Our Figma prototype is taking shape and the staging cluster is now on Kubernetes. We're on track for the public beta next month!</p>",
            false, 35, 6);

        AddPost(mob, Get("davekim"),
            "📱 First TestFlight build is out!",
            "<p>We just pushed the first internal TestFlight build. The feed, auth, and profile screens are working end-to-end. If you're on iOS and want early access, drop your Apple ID in the thread below.</p><p>Android beta on Firebase App Distribution to follow within the week.</p>",
            true, 3, 12);

        AddPost(mob, Get("carolm"),
            "Push notifications now working on both platforms",
            "<p>After a week of Firebase integration pain, push notifications are finally working on both iOS and Android. Project invites and chat messages will now ping you instantly. 🎉</p>",
            false, 11, 7);

        AddPost(ai, Get("gracelee"),
            "🚀 AI Review Assistant — 500 reviews milestone",
            "<p>We just crossed <strong>500 automated code reviews</strong> processed since our soft launch. Average review time: 8.2 seconds. False positive rate: 4.1%. We're now starting to collect labeled feedback to fine-tune the model further.</p><p>If you've used the tool, please fill out the 3-minute survey — your feedback shapes the model.</p>",
            true, 2, 19);

        AddPost(ai, Get("gracelee"),
            "New: multi-file context window support",
            "<p>The reviewer now passes up to 5 related files as context when reviewing a PR, dramatically improving suggestions for cross-file refactors. The tradeoff is slightly higher latency (~12s) but the quality improvement is significant in our tests.</p>",
            false, 14, 11);

        AddPost(tf, Get("bobw"),
            "TaskFlow v1.0 is shipped! 🎉",
            "<p>After 3 months of work, <strong>TaskFlow v1.0</strong> is live and open-source. 1,200+ stars on GitHub in the first 48 hours. Thank you to every contributor — this wouldn't exist without you.</p><p>What's next: v2 planning starts in two weeks. Issues welcome!</p>",
            true, 10, 24);

        ctx.Db.ProjectNewsPosts.AddRange(posts);
        ctx.Db.NewsPostLikes.AddRange(likes);
        ctx.Logger.LogInformation("News posts seeded: {Count} posts, {Likes} likes", posts.Count, likes.Count);
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    private static async Task SeedReviewsAsync(SeederContext ctx, List<Project> projects, List<User> users, CancellationToken ct = default)
    {
        if (await ctx.Db.Reviews.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Reviews already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];

        var reviews = new List<Review>();

        void AddReview(Project p, User reviewer, User? reviewed, int rating, string text, int daysAgo) =>
            reviews.Add(new Review
            {
                Id = Guid.NewGuid(), ProjectId = p.Id, ReviewerId = reviewer.Id,
                ReviewedUserId = reviewed?.Id, Rating = rating, ReviewText = text,
                CreatedAt = ctx.Baseline.AddDays(-daysAgo),
            });

        var eco = projects.First(p => p.Slug == "ecotracker");
        var ai  = projects.First(p => p.Slug == "ai-code-review");
        var tf  = projects.First(p => p.Slug == "taskflow");
        var rc  = projects.First(p => p.Slug == "rustcache");

        AddReview(eco, Get("bobw"),   Get("alicej"),  5, "Alice runs one of the most organized projects I've contributed to. Clear scope, great communication, and she gives constructive feedback on every PR.", 20);
        AddReview(eco, Get("gracelee"), Get("alicej"), 5, "Really well-scoped project with a genuine real-world impact. The codebase is clean and the weekly syncs are efficient.", 18);
        AddReview(eco, Get("emmad"),  Get("alicej"),  4, "Great collaboration and a well-thought-out design system. Could improve the onboarding docs for new contributors.", 12);

        AddReview(ai,  Get("henryb"), Get("gracelee"), 5, "Grace is an exceptional ML engineer. Her prompt engineering work cut false positives by 40% in one sprint.", 8);
        AddReview(ai,  Get("bobw"),   Get("gracelee"), 5, "Working with Grace was a highlight. She explains complex ML concepts clearly and always considers engineering constraints.", 6);

        AddReview(tf,  Get("alicej"), Get("bobw"),    5, "Bob shipped an incredibly polished product with a tiny team. The GitHub integration alone saves me an hour a day.", 10);
        AddReview(tf,  Get("carolm"), Get("bobw"),    5, "TaskFlow genuinely changed how I manage my projects. The drag-and-drop is buttery smooth and the burndown charts are actually useful.", 9);
        AddReview(tf,  Get("davekim"), Get("bobw"),   4, "Very solid tool. Would love a mobile app — hint hint. Minor UX issues in the time tracker but nothing blocking.", 7);

        AddReview(rc,  Get("frankn"), Get("ivanp"),   5, "Ivan's systems intuition is next-level. The TCP implementation is clean, well-documented, and genuinely fast.", 5);

        ctx.Db.Reviews.AddRange(reviews);
        ctx.Logger.LogInformation("Reviews seeded: {Count}", reviews.Count);
    }

    // ── Support Tickets ───────────────────────────────────────────────────────

    private static async Task SeedSupportTicketsAsync(SeederContext ctx, List<User> users, List<Project> projects, CancellationToken ct = default)
    {
        if (await ctx.Db.SupportTickets.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Support tickets already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var admin   = Get("alexchen");
        var carol   = Get("carolm");
        var dave    = Get("davekim");
        var henry   = Get("henryb");
        var julia   = Get("juliaw");
        var ivan    = Get("ivanp");
        var frank   = Get("frankn");

        var tickets   = new List<SupportTicket>();
        var messages  = new List<TicketMessage>();
        var histories = new List<TicketHistory>();

        void AddTicket(User user, string category, string subject, string description, string status,
            string priority, int daysAgo, Project? relatedProject = null,
            (string text, bool isStaff, int hoursAfter)[]? replies = null)
        {
            var ticketId = Guid.NewGuid();
            var createdAt = ctx.Baseline.AddDays(-daysAgo);

            tickets.Add(new SupportTicket
            {
                Id = ticketId, UserId = user.Id, Category = category,
                Subject = subject, Description = description,
                Status = status, Priority = priority,
                AssignedToUserId = status is "in_progress" or "resolved" ? admin.Id : null,
                RelatedProjectId = relatedProject?.Id,
                CreatedAt = createdAt,
                UpdatedAt = status is "resolved" or "closed" ? createdAt.AddDays(2) : createdAt.AddHours(2),
                ResolvedAt = status is "resolved" or "closed" ? createdAt.AddDays(2) : null,
                ClosedAt = status == "closed" ? createdAt.AddDays(3) : null,
            });

            messages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(), TicketId = ticketId, AuthorId = user.Id,
                Content = description, IsInternal = false, CreatedAt = createdAt,
            });

            if (replies != null)
            {
                foreach (var (text, isStaff, hoursAfter) in replies)
                {
                    messages.Add(new TicketMessage
                    {
                        Id = Guid.NewGuid(), TicketId = ticketId,
                        AuthorId = isStaff ? admin.Id : user.Id,
                        Content = text, IsInternal = false,
                        CreatedAt = createdAt.AddHours(hoursAfter),
                    });
                }
            }

            histories.Add(new TicketHistory
            {
                Id = Guid.NewGuid(), TicketId = ticketId, ChangedByUserId = admin.Id,
                ChangeType = "status", OldValue = "open", NewValue = status,
                CreatedAt = createdAt.AddMinutes(30), Reason = "Ticket created",
            });
        }

        var eco = projects.First(p => p.Slug == "ecotracker");
        var ai  = projects.First(p => p.Slug == "ai-code-review");

        AddTicket(carol, "technical", "GitHub OAuth login fails with 'redirect_uri_mismatch'",
            "I'm trying to log in with my GitHub account but keep getting 'redirect_uri_mismatch'. I've tried three different browsers. My account email is carol.martinez@devhunt.dev.",
            "resolved", "high", 14,
            replies: new[]
            {
                ("Hi Carol! This is a known issue with GitHub's OAuth app when the callback URL contains a trailing slash. I've fixed the config on our end — can you try again?", true, 4),
                ("That fixed it! Thank you so much, logged in fine now.", false, 5),
                ("Great! Closing the ticket. Let us know if anything else comes up.", true, 6),
            });

        AddTicket(dave, "bug", "Profile avatar upload stuck at 0% on mobile",
            "Uploading a profile picture from the mobile app always gets stuck at 0%. The same image uploads fine from desktop. I'm on iOS 17.4, iPhone 15 Pro. The file is a 2MB JPEG.",
            "in_progress", "medium", 7, relatedProject: null,
            replies: new[]
            {
                ("Thanks Dave, I can reproduce this on iOS. It looks like the multipart upload fails silently when the connection is on cellular. Investigating a fix.", true, 8),
            });

        AddTicket(henry, "account", "Can't change email address — form says 'email already taken'",
            "I'm trying to update my email from the old one to henry.brown@devhunt.dev but it says the email is already taken. I never registered with this email — it might be a duplicate from a previous OAuth sign-in?",
            "resolved", "medium", 20,
            replies: new[]
            {
                ("Hi Henry! I found a ghost account tied to that email from an incomplete Google OAuth flow. I've merged it and freed up the address — please try again.", true, 2),
                ("Worked perfectly, thank you!", false, 3),
            });

        AddTicket(julia, "feature_request", "Request: dark mode for the mobile app",
            "The mobile app doesn't have a dark mode option. As someone who codes late at night, this is pretty painful. The web version has it — any plans to bring it to mobile?",
            "open", "low", 5,
            replies: new[]
            {
                ("Thanks Julia! This is on our roadmap for the next mobile release. I'll link this ticket to the issue tracker.", true, 12),
            });

        AddTicket(ivan, "bug", "Project slug conflicts not handled gracefully",
            "When I tried to create a project with the slug 'rustcache', I got a generic 500 error instead of a helpful validation message. The slug was already taken by my own project. The error should say 'slug already in use'.",
            "open", "medium", 3, relatedProject: eco);

        AddTicket(frank, "technical", "Webhook deliveries timing out for large PRs",
            "The AI Code Review Assistant webhook handler times out (~30s) on PRs with more than 50 changed files. The review never completes and there's no error notification. This affects roughly 10% of our PRs.",
            "in_progress", "high", 2, relatedProject: ai,
            replies: new[]
            {
                ("Hi Frank, this is a real issue — our webhook processor is synchronous and blocks on large payloads. I'm implementing an async queue to fix this. ETA: 48 hours.", true, 6),
            });

        AddTicket(carol, "account", "Two-factor auth recovery codes not working",
            "I set up TOTP two weeks ago and saved my recovery codes. Today I tried one because I lost my phone and it says 'Invalid code'. I triple-checked the code — no extra spaces or dashes.",
            "resolved", "urgent", 25,
            replies: new[]
            {
                ("Hi Carol! I can see from the logs that the codes were generated with an older hashing format. I've reset your 2FA — please set it up again. Sorry for the inconvenience!", true, 1),
                ("Done, works now. Please add an option to download codes as a PDF next time.", false, 2),
                ("Great suggestion — filed a feature request. Thanks!", true, 3),
            });

        ctx.Db.SupportTickets.AddRange(tickets);
        ctx.Db.TicketMessages.AddRange(messages);
        ctx.Db.TicketHistories.AddRange(histories);
        ctx.Logger.LogInformation("Support tickets seeded: {Count} tickets, {Msgs} messages", tickets.Count, messages.Count);
    }

    // ── Feedback ──────────────────────────────────────────────────────────────

    private static async Task SeedFeedbackAsync(SeederContext ctx, List<User> users, List<Project> projects, CancellationToken ct = default)
    {
        if (await ctx.Db.FeedbackItems.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Feedback items already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var admin = Get("alexchen");

        var items    = new List<FeedbackItem>();
        var voteRows = new List<FeedbackVote>();
        var comments = new List<FeedbackComment>();

        void AddFeedback(User author, string type, string title, string description,
            string status, string priority, int voteCount, int daysAgo, Project? project = null,
            (User commenter, string text, int hoursAfter)[]? replies = null)
        {
            var id = Guid.NewGuid();
            var createdAt = ctx.Baseline.AddDays(-daysAgo);
            items.Add(new FeedbackItem
            {
                Id = id, AuthorId = author.Id, Type = type, Title = title,
                Description = description, Status = status, Priority = priority,
                VoteCount = voteCount, CommentCount = replies?.Length ?? 0,
                RelatedProjectId = project?.Id,
                CreatedAt = createdAt, UpdatedAt = createdAt,
                CompletedAt = status == "completed" ? createdAt.AddDays(10) : null,
            });

            var voterPool = users.Where(u => u.Id != author.Id).Take(voteCount).ToList();
            foreach (var voter in voterPool)
                voteRows.Add(new FeedbackVote { Id = Guid.NewGuid(), FeedbackId = id, UserId = voter.Id, CreatedAt = createdAt.AddHours(1) });

            if (replies != null)
            {
                foreach (var (commenter, text, hoursAfter) in replies)
                    comments.Add(new FeedbackComment
                    {
                        Id = Guid.NewGuid(), FeedbackId = id, AuthorId = commenter.Id,
                        Content = text, CreatedAt = createdAt.AddHours(hoursAfter),
                    });
            }
        }

        var eco = projects.First(p => p.Slug == "ecotracker");
        var ai  = projects.First(p => p.Slug == "ai-code-review");

        AddFeedback(Get("alicej"), "feature", "Add project tags / categories for better discovery",
            "Right now the only filter on the discover page is tech stack. It would be great to also tag projects by domain (climate, developer tools, fintech, etc.) so users can find projects relevant to their interests.",
            "planned", "high", 47, 30,
            replies: new[]
            {
                (Get("mariasantos"), "Fully agree — I hear this from new users every week. We're planning a taxonomy sprint next quarter.", 4),
                (Get("carolm"),      "Would love to see this! Domain tags would make it 10x easier to find projects I actually care about.", 6),
            });

        AddFeedback(Get("davekim"), "bug", "Notification badge count doesn't reset after reading",
            "The notification dot in the nav stays red even after I've opened and read all notifications. Have to refresh the page to clear it. Happens consistently in Chrome 124 on macOS.",
            "in_progress", "medium", 23, 18,
            replies: new[]
            {
                (Get("alexchen"), "Confirmed bug — it's a race condition in the optimistic update. Fix is in progress.", 2),
            });

        AddFeedback(Get("henryb"), "feature", "CSV export for project tasks and time logs",
            "When a project wraps up it would be incredibly useful to export all tasks and time logs as CSV for reporting to stakeholders or retrospectives. Even a basic export would save a lot of manual work.",
            "open", "medium", 18, 22,
            replies: new[]
            {
                (Get("bobw"), "Yes please! We had to screenshot the burndown charts for our last retrospective. Export would be a huge quality-of-life improvement.", 8),
            });

        AddFeedback(Get("frankn"), "feature", "Slack integration for project notifications",
            "It would be great to pipe DevHunt notifications (new tasks, team member joined, milestone reached) into a Slack channel. We already use Slack for team communication and having to check two places is friction.",
            "open", "low", 31, 12,
            replies: new[]
            {
                (Get("gracelee"), "Upvoted! Webhook support generally (not just Slack) would be even better — then each team can wire it up however they like.", 3),
                (Get("mariasantos"), "Webhook support is actually planned for the integration gateway. Adding this feedback to the spec.", 5),
            });

        AddFeedback(Get("ivanp"), "bug", "Code block syntax highlighting broken in project description",
            "Fenced code blocks in project descriptions don't get syntax highlighted — they render as plain text in a monospace font. The preview in the editor shows highlighting correctly but the published version doesn't.",
            "open", "medium", 9, 8);

        ctx.Db.FeedbackItems.AddRange(items);
        ctx.Db.FeedbackVotes.AddRange(voteRows);
        ctx.Db.FeedbackComments.AddRange(comments);
        ctx.Logger.LogInformation("Feedback seeded: {Count} items, {Votes} votes, {Comments} comments", items.Count, voteRows.Count, comments.Count);
    }

    // ── Moderation Reports ────────────────────────────────────────────────────

    private static async Task SeedModerationReportsAsync(SeederContext ctx, List<User> users, CancellationToken ct = default)
    {
        if (await ctx.Db.ModerationReports.AnyAsync(ct))
        {
            ctx.Logger.LogInformation("Moderation reports already exist, skipping.");
            return;
        }

        User Get(string username) => users.FirstOrDefault(u => u.Username == username) ?? users[0];
        var admin   = Get("alexchen");
        var curator = Get("mariasantos");

        var reports = new List<ModerationReport>();

        reports.Add(new ModerationReport
        {
            Id = Guid.NewGuid(),
            ReporterId = Get("carolm").Id,
            TargetType = "project",
            TargetId = Guid.NewGuid(),
            Reason = "This project is posted multiple times under different names with no real progress or updates. Looks like it's just farming boosts.",
            Status = "resolved",
            ActionTaken = "Project marked as duplicate and removed from featured listing. Owner notified.",
            CreatedAt = ctx.Baseline.AddDays(-22),
            ProcessedAt = ctx.Baseline.AddDays(-20),
            ProcessedByUserId = curator.Id,
        });

        reports.Add(new ModerationReport
        {
            Id = Guid.NewGuid(),
            ReporterId = Get("frankn").Id,
            TargetType = "user",
            TargetId = Guid.NewGuid(),
            Reason = "This user is spamming project invitations to everyone — I've received 5 unsolicited invitations in 2 days with no context or relevance to my skills.",
            Status = "pending",
            CreatedAt = ctx.Baseline.AddDays(-4),
        });

        reports.Add(new ModerationReport
        {
            Id = Guid.NewGuid(),
            ReporterId = Get("davekim").Id,
            TargetType = "message",
            TargetId = Guid.NewGuid(),
            Reason = "User is posting promotional links for an unrelated SaaS product in multiple project chat channels.",
            Status = "resolved",
            ActionTaken = "Message deleted. User received a warning. Repeat violation will result in suspension.",
            CreatedAt = ctx.Baseline.AddDays(-10),
            ProcessedAt = ctx.Baseline.AddDays(-9),
            ProcessedByUserId = admin.Id,
        });

        ctx.Db.ModerationReports.AddRange(reports);
        ctx.Logger.LogInformation("Moderation reports seeded: {Count}", reports.Count);
    }
}
