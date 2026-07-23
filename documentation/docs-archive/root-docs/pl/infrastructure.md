# Infrastructure — Dokumentacja Techniczna

## Spis treści

1. [Przegląd systemu](#1-przegląd-systemu)
2. [Architektura warstwy infrastrukturalnej](#2-architektura-warstwy-infrastrukturalnej)
3. [Stos technologiczny](#3-stos-technologiczny)
4. [Struktura projektu](#4-struktura-projektu)
5. [DevHuntDbContext — kontekst bazy danych](#5-devhuntdbcontext--kontekst-bazy-danych)
6. [Encje domenowe — pełny katalog](#6-encje-domenowe--pełny-katalog)
7. [Encja User — użytkownik](#7-encja-user--użytkownik)
8. [Encja Project — projekt](#8-encja-project--projekt)
9. [Encja TeamMember — członek zespołu](#9-encja-teammember--członek-zespołu)
10. [Encja TaskItem — zadanie Kanban](#10-encja-taskitem--zadanie-kanban)
11. [System zadań rozszerzony — TaskBoard](#11-system-zadań-rozszerzony--taskboard)
12. [System zaproszeń — Invitation](#12-system-zaproszeń--invitation)
13. [System czatu — Conversation / Message](#13-system-czatu--conversation--message)
14. [System umiejętności — Skill / UserSkill / UserSkillEntry](#14-system-umiejętności--skill--userskill--userskillentry)
15. [System osiągnięć — Achievement / UserAchievement](#15-system-osiągnięć--achievement--userachievement)
16. [System showcase — ShowcaseProject](#16-system-showcase--showcaseproject)
17. [System aktywności i feedu — ActivityRecord / UserFollow / ProjectSubscription](#17-system-aktywności-i-feedu--activityrecord--userfollow--projectsubscription)
18. [System news — ProjectNewsPost / NewsPostLike / NewsPostComment](#18-system-news--projectnewspost--newspostlike--newspostcomment)
19. [Integracje zewnętrzne — Integration](#19-integracje-zewnętrzne--integration)
20. [System rekomendacji AI — Recommendation / AiPlan / AiOperationLog / ProjectArtifact](#20-system-rekomendacji-ai--recommendation--aiplan--aioperationlog--projectartifact)
21. [System wsparcia — SupportTicket / TicketMessage / TicketHistory](#21-system-wsparcia--supportticket--ticketmessage--tickethistory)
22. [System feedbacku społeczności — FeedbackItem / FeedbackVote / FeedbackComment](#22-system-feedbacku-społeczności--feedbackitem--feedbackvote--feedbackcomment)
23. [Pozostałe encje — Notification / ModerationReport / Review / RefreshToken / OutboxEvent / AuditLog / UserPrivacySettings](#23-pozostałe-encje)
24. [Pliki i dokumenty — ProjectFile / ProjectDocument](#24-pliki-i-dokumenty--projectfile--projectdocument)
25. [Konfiguracja EF Core — EntityConfigurations](#25-konfiguracja-ef-core--entityconfigurations)
26. [Stałe domenowe — Constants](#26-stałe-domenowe--constants)
27. [EnvLoader — ładowanie zmiennych środowiskowych](#27-envloader--ładowanie-zmiennych-środowiskowych)
28. [DesignTimeDbContextFactory — fabryka design-time](#28-designtimedbcontextfactory--fabryka-design-time)
29. [ReadWriteDbContextFactory — repliki odczytu](#29-readwritedbcontextfactory--repliki-odczytu)
30. [DatabaseMigrator — runner migracji](#30-databasemigrator--runner-migracji)
31. [DatabaseSeeder — generator danych testowych](#31-databaseseeder--generator-danych-testowych)
32. [System migracji — historia](#32-system-migracji--historia)
33. [Centralne zarządzanie pakietami — Directory.Packages.props](#33-centralne-zarządzanie-pakietami--directorypackagesprops)
34. [Shared package — packages/throttle](#34-shared-package--packagesthrottle)
35. [Docker — DatabaseMigrator i DatabaseSeeder](#35-docker--databasemigrator-i-databaseseeder)
36. [Diagram ERD — pełna mapa relacji](#36-diagram-erd--pełna-mapa-relacji)
37. [Zależności NuGet](#37-zależności-nuget)

---

## 1. Przegląd systemu

**DevHunt.Infrastructure** to wspólna warstwa danych całej platformy DevHunt. Jest referencjonowana przez wszystkie serwisy .NET (CoreApi, AuthService, DatabaseMigrator, DatabaseSeeder) i zawiera:

| Element | Opis |
|---------|------|
| **DevHuntDbContext** | DbContext Entity Framework Core — 40+ DbSet |
| **Encje (modele)** | 40+ klas C# definiujących schemat PostgreSQL |
| **EntityConfigurations** | Konfiguracje Fluent API (relacje, indeksy, typy JSONB) |
| **Migracje** | 25+ migracji EF Core od InitialMigration do AddProjectArtifacts |
| **EnvLoader** | Ładowanie zmiennych z pliku `.env` |
| **ReadWriteDbContextFactory** | Wsparcie dla replik odczytu (Primary/Replica) |
| **DesignTimeDbContextFactory** | Fabryka do `dotnet ef` (generowanie migracji) |
| **Constants** | Stałe domenowe (statusy projektów, widoczność, itp.) |

Dodatkowo w ekosystemie infrastruktury znajdują się:
- **DevHunt.DatabaseMigrator** — standalone job (Kubernetes/Docker) do bezpiecznego stosowania migracji
- **DevHunt.DatabaseSeeder** — generator realistycznych danych testowych (skills, achievements, projects, users)
- **packages/throttle** — współdzielony pakiet Node.js (backpressure middleware)

---

## 2. Architektura warstwy infrastrukturalnej

```
┌─────────────────────────────────────────────────────────────────────┐
│                   DevHunt.Infrastructure                            │
│              (Shared .NET Library — net10.0)                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────┐  ┌────────────────────┐  ┌────────────────┐  │
│  │  DevHuntDbContext │  │  Entity Models     │  │ Configurations │  │
│  │  (40+ DbSet)     │  │  (40+ klas C#)     │  │ (Fluent API)   │  │
│  └────────┬─────────┘  └────────┬───────────┘  └────────┬───────┘  │
│           │                     │                        │          │
│  ┌────────▼─────────────────────▼────────────────────────▼───────┐  │
│  │             PostgreSQL 16 via Npgsql EF Core 9               │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌────────────────┐  ┌──────────────────┐  ┌────────────────────┐  │
│  │   EnvLoader    │  │ DesignTimeFactory│  │ ReadWriteFactory   │  │
│  │   (.env file)  │  │ (dotnet ef)      │  │ (Primary/Replica)  │  │
│  └────────────────┘  └──────────────────┘  └────────────────────┘  │
│                                                                     │
│  ┌────────────────┐  ┌──────────────────┐                          │
│  │   Constants    │  │    Migrations    │                          │
│  │   (Statusy)   │  │   (25+ plików)   │                          │
│  └────────────────┘  └──────────────────┘                          │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│  Referencjonowane przez:                                            │
│  • DevHunt.CoreApi        (business logic)                         │
│  • DevHunt.AuthService    (authentication)                         │
│  • DevHunt.DatabaseMigrator (migration runner)                     │
│  • DevHunt.DatabaseSeeder  (test data generator)                   │
│  • DevHunt.CoreApi.Tests   (xUnit tests)                           │
│  • DevHunt.AuthService.Tests (xUnit tests)                         │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 3. Stos technologiczny

| Kategoria | Technologia | Wersja |
|-----------|-------------|--------|
| Framework | .NET | 10.0 |
| ORM | Entity Framework Core | 9.0.1 |
| Baza danych | PostgreSQL (Npgsql) | 16 (provider 9.0.1) |
| Pakiety .NET | Centralne — `Directory.Packages.props` | — |
| Nullable | Enabled | — |
| Implicit Usings | Enabled | — |
| Design-time | `Microsoft.EntityFrameworkCore.Design` 9.0.1 | — |

---

## 4. Struktura projektu

```
DevHunt.Infrastructure/
├── DevHunt.Infrastructure.csproj       # Projekt .NET — Npgsql + EF Core Design
├── DevHuntDbContext.cs                  # Kontekst EF Core — 40+ DbSet
├── DesignTimeDbContextFactory.cs        # Fabryka dla dotnet ef (migracje)
├── ReadWriteDbContextFactory.cs         # Fabryka Primary/Replica
├── Configuration/
│   ├── EnvLoader.cs                    # Ładowanie .env (196 linii)
│   └── EntityConfigurations/
│       ├── ActivityRecordConfiguration.cs
│       ├── AdminConfiguration.cs       # ProjectIssue + UserPrivacySettings
│       ├── AiConfiguration.cs          # AiPlan + AiOperationLog + ProjectArtifact
│       ├── FeedbackConfiguration.cs
│       ├── InvitationConfiguration.cs
│       ├── NewsPostConfiguration.cs
│       ├── ProjectSubscriptionConfiguration.cs
│       ├── SupportConfiguration.cs
│       ├── TaskBoardConfiguration.cs   # TaskLink + TaskAttachment
│       └── UserFollowConfiguration.cs
├── Constants/
│   └── ProjectDomainConstants.cs       # ProjectStatus, TeamMemberStatus, ProjectVisibility
├── Migrations/                          # 25+ migracji EF Core
│   ├── 20251030235524_InitialMigration.cs
│   ├── ... (chronologicznie)
│   └── 20260210182708_AddProjectArtifacts.cs
├── Models/                              # Encje w podfolderze Models/
│   ├── ActivityRecord.cs
│   ├── AiOperationLog.cs
│   ├── AiPlan.cs
│   ├── Conversation.cs
│   ├── ConversationParticipant.cs
│   ├── FeedbackComment.cs
│   ├── FeedbackItem.cs
│   ├── FeedbackVote.cs
│   ├── Message.cs
│   ├── NewsPostComment.cs
│   ├── NewsPostLike.cs
│   ├── ProjectDocument.cs
│   ├── ProjectFile.cs
│   ├── ProjectIssue.cs
│   ├── ProjectNewsPost.cs
│   ├── ProjectSubscription.cs
│   ├── ShowcaseComment.cs
│   ├── SupportTicket.cs
│   ├── TaskAttachment.cs
│   ├── TaskBoardSettings.cs
│   ├── TaskColumn.cs
│   ├── TaskLink.cs
│   ├── TicketHistory.cs
│   ├── TicketMessage.cs
│   ├── UserFollow.cs
│   └── UserPrivacySettings.cs
└── (Encje root-level — starsze modele)
    ├── User.cs
    ├── Project.cs
    ├── TeamMember.cs
    ├── TaskItem.cs
    ├── Invitation.cs
    ├── Notification.cs
    ├── ModerationReport.cs
    ├── Review.cs
    ├── Skill.cs, SkillAlias.cs, UserSkill.cs, UserSkillEntry.cs
    ├── ProjectTechStack.cs, ProjectRole.cs
    ├── ShowcaseProject.cs
    ├── Achievement.cs, UserAchievement.cs
    ├── Integration.cs
    ├── Recommendation.cs
    ├── RefreshToken.cs
    ├── OutboxEvent.cs
    ├── AuditLog.cs
    └── ProjectArtifact.cs
```

---

## 5. DevHuntDbContext — kontekst bazy danych

### 5.1 DbSet — pełna lista

```csharp
public class DevHuntDbContext : DbContext
{
    // ── Core ──────────────────────────────
    public DbSet<User> Users { get; set; }
    public DbSet<Project> Projects { get; set; }
    public DbSet<TeamMember> TeamMembers { get; set; }
    public DbSet<TaskItem> Tasks { get; set; }
    public DbSet<Invitation> Invitations { get; set; }

    // ── Content ──────────────────────────
    public DbSet<ModerationReport> ModerationReports { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    // ── Chat ─────────────────────────────
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationParticipant> ConversationParticipants { get; set; }
    public DbSet<Message> Messages { get; set; }

    // ── Skills ───────────────────────────
    public DbSet<Skill> Skills { get; set; }
    public DbSet<SkillAlias> SkillAliases { get; set; }
    public DbSet<UserSkill> UserSkills { get; set; }
    public DbSet<UserSkillEntry> UserSkillEntries { get; set; }
    public DbSet<ProjectTechStack> ProjectTechStacks { get; set; }
    public DbSet<ProjectRole> ProjectRoles { get; set; }

    // ── Showcase ─────────────────────────
    public DbSet<ShowcaseProject> ShowcaseProjects { get; set; }
    public DbSet<ShowcaseComment> ShowcaseComments { get; set; }

    // ── Files & Docs ─────────────────────
    public DbSet<ProjectFile> ProjectFiles { get; set; }
    public DbSet<ProjectDocument> ProjectDocuments { get; set; }

    // ── Activity & Social ────────────────
    public DbSet<ActivityRecord> ActivityRecords { get; set; }
    public DbSet<ProjectNewsPost> ProjectNewsPosts { get; set; }
    public DbSet<ProjectSubscription> ProjectSubscriptions { get; set; }
    public DbSet<UserFollow> UserFollows { get; set; }
    public DbSet<NewsPostLike> NewsPostLikes { get; set; }
    public DbSet<NewsPostComment> NewsPostComments { get; set; }

    // ── AI / ML ──────────────────────────
    public DbSet<Recommendation> Recommendations { get; set; }
    public DbSet<Integration> Integrations { get; set; }
    public DbSet<Achievement> Achievements { get; set; }
    public DbSet<UserAchievement> UserAchievements { get; set; }

    // ── Auth ─────────────────────────────
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // ── Outbox ───────────────────────────
    public DbSet<OutboxEvent> OutboxEvents { get; set; }

    // ── Support ──────────────────────────
    public DbSet<SupportTicket> SupportTickets { get; set; }
    public DbSet<TicketMessage> TicketMessages { get; set; }
    public DbSet<TicketHistory> TicketHistories { get; set; }

    // ── Feedback ─────────────────────────
    public DbSet<FeedbackItem> FeedbackItems { get; set; }
    public DbSet<FeedbackVote> FeedbackVotes { get; set; }
    public DbSet<FeedbackComment> FeedbackComments { get; set; }

    // ── Admin ────────────────────────────
    public DbSet<ProjectIssue> ProjectIssues { get; set; }
    public DbSet<UserPrivacySettings> UserPrivacySettings { get; set; }

    // ── Task Board ───────────────────────
    public DbSet<TaskColumn> TaskColumns { get; set; }
    public DbSet<TaskLink> TaskLinks { get; set; }
    public DbSet<TaskAttachment> TaskAttachments { get; set; }
    public DbSet<TaskBoardSettings> TaskBoardSettings { get; set; }

    // ── AI Planning ──────────────────────
    public DbSet<AiPlan> AiPlans { get; set; }
    public DbSet<AiOperationLog> AiOperationLogs { get; set; }
    public DbSet<ProjectArtifact> ProjectArtifacts { get; set; }

    // ── Audit ────────────────────────────
    public DbSet<AuditLog> AuditLogs { get; set; }
}
```

### 5.2 Konfiguracja modelu

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(DevHuntDbContext).Assembly);
}
```

Automatycznie wczytuje **wszystkie klasy** implementujące `IEntityTypeConfiguration<T>` z assembly.

### 5.3 Ostrzeżenia konfiguracyjne

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.ConfigureWarnings(warnings =>
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
}
```

Wycisza ostrzeżenie o oczekujących zmianach modelu.

---

## 6. Encje domenowe — pełny katalog

### Grupy encji

| Grupa | Encje | Ilość |
|-------|-------|-------|
| **Core** | User, Project, TeamMember, TaskItem, Invitation | 5 |
| **Task Board** | TaskColumn, TaskLink, TaskAttachment, TaskBoardSettings | 4 |
| **Chat** | Conversation, ConversationParticipant, Message | 3 |
| **Skills** | Skill, SkillAlias, UserSkill, UserSkillEntry, ProjectTechStack, ProjectRole | 6 |
| **Showcase** | ShowcaseProject, ShowcaseComment | 2 |
| **Activity/Social** | ActivityRecord, UserFollow, ProjectSubscription, ProjectNewsPost, NewsPostLike, NewsPostComment | 6 |
| **AI/ML** | Recommendation, AiPlan, AiOperationLog, ProjectArtifact | 4 |
| **Support** | SupportTicket, TicketMessage, TicketHistory | 3 |
| **Feedback** | FeedbackItem, FeedbackVote, FeedbackComment | 3 |
| **Content** | Review, Notification, ModerationReport | 3 |
| **Files** | ProjectFile, ProjectDocument | 2 |
| **Auth/Security** | RefreshToken, AuditLog, UserPrivacySettings | 3 |
| **Integration** | Integration, OutboxEvent, Achievement, UserAchievement | 4 |
| **Łącznie** | — | **48** |

---

## 7. Encja User — użytkownik

### 7.1 Pola

| Pole | Typ | Walidacja | Opis |
|------|-----|-----------|------|
| `Id` | `Guid` | PK | UUID użytkownika |
| `Email` | `string` | Required, EmailAddress | Email (lowercase) |
| `PasswordHash` | `string?` | — | BCrypt hash; null dla OAuth-only |
| `Role` | `string` | — | participant / company / curator / admin / superadmin |
| `FullName` | `string?` | — | Wyświetlana nazwa |
| `Bio` | `string?` | — | Biografia |
| `Timezone` | `string?` | — | np. "Europe/Moscow" |
| `Skills` | `List<string>` | — | Legacy — PostgreSQL text[] |
| `Experience` | `int?` | — | Lata doświadczenia |
| `Rating` | `float?` | — | Średnia ocen (1-5) |
| `AvatarUrl` | `string?` | — | URL awatara |
| `IsVerified` | `bool` | def: false | Zweryfikowany przez kuratora |
| `IsActive` | `bool` | def: true | Aktywne konto (soft-delete) |
| `CreatedAt` | `DateTime` | def: UtcNow | Data rejestracji |
| `LastLogin` | `DateTime?` | — | Ostatnie logowanie |
| `Language` | `string?` | — | Preferowany język (ru, en) |
| `Github` / `Linkedin` / `Website` | `string?` | — | Linki społecznościowe |
| `GithubId` / `GoogleId` | `string?` | — | OAuth identifiers |
| `GithubUsername` | `string?` | — | GitHub login (do API) |
| `IsEmailVerified` | `bool` | def: false | Zweryfikowany email |
| `VerificationToken` | `string?` | — | 6-cyfrowy kod weryfikacyjny |
| `VerificationTokenExpiresAt` | `DateTime?` | — | Wygaśnięcie kodu (24h) |
| `PasswordResetToken` | `string?` | — | Token resetu hasła |
| `PasswordResetTokenExpiresAt` | `DateTime?` | — | Wygaśnięcie (1h) |

### 7.2 Kolekcje nawigacyjne

| Kolekcja | Typ | Relacja |
|----------|-----|---------|
| `TeamMemberships` | `ICollection<TeamMember>` | User → wiele TeamMember |
| `AssignedTasks` | `ICollection<TaskItem>` | User → wiele TaskItem |
| `SentInvitations` | `ICollection<Invitation>` | User (Inviter) → wiele |
| `ReceivedInvitations` | `ICollection<Invitation>` | User (Invitee) → wiele |
| `Reports` | `ICollection<ModerationReport>` | User (Reporter) → wiele |
| `UserSkills` | `ICollection<UserSkill>` | User → wiele (legacy) |
| `UserAchievements` | `ICollection<UserAchievement>` | User → wiele |
| `UserSkillEntries` | `ICollection<UserSkillEntry>` | User → wiele (nowy system) |

### 7.3 Role

| Rola | Uprawnienia |
|------|-------------|
| `participant` | Zwykły użytkownik — dołącza do projektów |
| `company` | Konto firmowe — staże/oferty |
| `curator` | Mentor — nadzór nad projektami |
| `admin` | Administrator systemu |
| `superadmin` | Super administrator |

### 7.4 Autentykacja

| Metoda | Pole |
|--------|------|
| Email/hasło | `PasswordHash` (BCrypt) |
| GitHub OAuth | `GithubId` |
| Google OAuth | `GoogleId` |

---

## 8. Encja Project — projekt

### 8.1 Pola

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | PK |
| `Title` | `string` [200] | Tytuł projektu |
| `Description` | `string` [4000] | Opis projektu |
| `TechStack` | `List<string>` | PostgreSQL text[] |
| `Status` | `string` [50] | draft / recruiting / active / completed / archived |
| `Visibility` | `string` [50] | public / private / unlisted |
| `OwnerId` | `Guid` | Właściciel (FK: User) |
| `ShortDescription` | `string?` [500] | Krótki opis |
| `DifficultyLevel` | `string?` [24] | beginner / intermediate / advanced |
| `ExpectedDurationDays` | `int?` | Szacowany czas (dni) |
| `StartDate` / `EndDate` | `DateTime?` | Harmonogram |
| `ShowcasePublished` | `bool` | Czy showcase opublikowany |
| `Featured` | `bool` | Wyróżniony |
| `Rating` | `float?` | Średnia ocena |
| `MaxTeamSize` | `int?` | Max rozmiar zespołu |
| `RequiredRoles` | `List<string>` | PostgreSQL text[] |
| `DefaultNewsVisibility` | `string` [50] | Domyślna widoczność newsów |
| `DefaultFilesVisibility` | `string` [50] | Domyślna widoczność plików |

### 8.2 Cykl życia projektu (State Machine)

```
draft → recruiting → active → completed → archived
```

### 8.3 Widoczność

| Wartość | Opis |
|---------|------|
| `public` | Widoczny dla wszystkich w katalogu |
| `private` | Tylko dla członków zespołu i właściciela |
| `unlisted` | Dostępny via bezpośredni link, nie w katalogu |

---

## 9. Encja TeamMember — członek zespołu

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | PK |
| `UserId` | `Guid` | FK: User |
| `ProjectId` | `Guid` | FK: Project |
| `Role` | `string` [100] | Rola projektowa (developer, designer, devops, qa, ...) |
| `Contribution` | `string?` [2000] | Opis wkładu |
| `Status` | `string` [24] | active / inactive / left |
| `IsLeader` | `bool` | Lider zespołu |
| `ContributionScore` | `int?` | Punkty kontrybucji |
| `LeftAt` | `DateTime?` | Data opuszczenia |
| `CanPublishNews` | `bool` | Uprawnienie: news |
| `CanManageTasks` | `bool` | Uprawnienie: zadania |
| `CanManageFiles` | `bool` | Uprawnienie: pliki |
| `CanManageGallery` | `bool` | Uprawnienie: galeria |
| `JoinedAt` | `DateTime` | Data dołączenia |

### Granularny system uprawnień

Każdy member ma 4 granularne uprawnienia (`bool`), niezależne od roli:
- **CanPublishNews** — publikacja postów w newsach projektu
- **CanManageTasks** — CRUD zadań na tablicy Kanban
- **CanManageFiles** — upload/delete plików projektu
- **CanManageGallery** — zarządzanie galerią projektu

Lider (`IsLeader = true`) ma automatycznie podwyższone uprawnienia.

---

## 10. Encja TaskItem — zadanie Kanban

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | PK |
| `ProjectId` | `Guid` | FK: Project |
| `Title` | `string` [200] | Tytuł zadania |
| `Status` | `string` [50] | todo / doing / review / done / archived / cancelled |
| `AssignedToUserId` | `Guid?` | FK: User (przypisany) |
| `Deadline` | `DateTime?` | Deadline |
| `Description` | `string?` [1000] | Opis (markdown) |
| `Priority` | `string?` [32] | low / medium / high / urgent |
| `CreatedByUserId` | `Guid` | Twórca zadania |
| `EstimatedHours` / `ActualHours` | `float?` | Śledzenie czasu |
| `IsDeleted` | `bool` | Soft delete |
| `ColumnId` | `Guid?` | FK: TaskColumn (custom kolumna) |
| `PositionInColumn` | `int` | Pozycja w kolumnie (0-based) |
| `CanvasX` / `CanvasY` | `float?` | Pozycja w trybie Canvas |
| `Tags` | `string?` [500] | Tagi (CSV: "Frontend,Design") |
| `GitHubIssueId` | `long?` | GitHub Issue ID (sync) |
| `GitHubIssueNumber` | `int?` | GitHub Issue #numer |
| `GitHubIssueUrl` | `string?` [500] | URL do GitHub Issue |

### Workflow statusów

```
todo → doing → review → done
                          │
                    archived / cancelled (terminalne)
```

---

## 11. System zadań rozszerzony — TaskBoard

### 11.1 TaskColumn — kolumna Kanban

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | PK |
| `ProjectId` | `Guid` | FK: Project |
| `Name` | `string` [100] | Nazwa kolumny |
| `Position` | `int` | Kolejność (0-based) |
| `CanvasX/Y/Width/Height` | `float?` | Pozycja w trybie Canvas (Miro-style) |
| `Color` | `string?` [7] | Hex kolor (#3B82F6) |
| `IsDefault` | `bool` | Czy domyślna (nie do usunięcia) |
| `IsCompleted` | `bool` | Czy przeniesienie = ukończone |
| `WipLimit` | `int?` | Limit WIP (null = brak) |

### 11.2 TaskLink — zależności między zadaniami

| Pole | Typ | Opis |
|------|-----|------|
| `SourceTaskId` | `Guid` | FK: TaskItem (źródło) |
| `TargetTaskId` | `Guid` | FK: TaskItem (cel) |
| `LinkType` | `string` [20] | Typ zależności |
| `CreatedByUserId` | `Guid` | Twórca powiązania |

**Typy powiązań:**

| Typ | Opis | Odwrotny |
|-----|------|----------|
| `blocks` | Źródło blokuje cel | `blocked_by` |
| `blocked_by` | Źródło jest blokowane przez cel | `blocks` |
| `depends_on` | Zależność od ukończenia | — |
| `related_to` | Informacyjne powiązanie | — |
| `duplicate_of` | Duplikat | — |
| `parent_of` | Epic/story → subtask | `child_of` |
| `child_of` | Subtask → epic | `parent_of` |

Unikalny indeks: `(SourceTaskId, TargetTaskId, LinkType)`.

### 11.3 TaskAttachment — załączniki do zadań

| Pole | Typ | Opis |
|------|-----|------|
| `TaskId` | `Guid` | FK: TaskItem |
| `ProjectFileId` | `Guid?` | FK: ProjectFile (referencja bez duplikacji) |
| `FileName` | `string?` [255] | Nazwa pliku (inline upload) |
| `ContentType` | `string?` [100] | MIME type |
| `FileSize` | `long?` | Rozmiar (bajty) |
| `StorageKey` | `string?` [500] | Klucz w object storage |
| `AttachedByUserId` | `Guid` | Kto załączył |

Dwa tryby: **referencja na istniejący ProjectFile** lub **inline upload** (nowy plik).

### 11.4 TaskBoardSettings — ustawienia tablicy

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project (1:1) |
| `ViewMode` | `string` [10] | `board` (Trello) lub `canvas` (Miro) |
| `CanvasZoom` | `float` | Zoom (1.0 = 100%) |
| `CanvasPanX/Y` | `float` | Przesunięcie widoku |
| `ShowCompletedTasks` | `bool` | Pokaż ukończone |
| `DefaultColumnId` | `Guid?` | Domyślna kolumna nowych zadań |

---

## 12. System zaproszeń — Invitation

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `InviterId` | `Guid` | FK: User (kto zaprasza) |
| `InviteeId` | `Guid` | FK: User (kto jest zapraszany) |
| `Status` | `string` [32] | pending / accepted / declined / cancelled |
| `Type` | `string` [50] | `invite` (owner zaprasza) lub `request` (user prosi) |
| `Role` | `string` [100] | Proponowana rola |
| `Message` | `string?` [2000] | Wiadomość |

### Dwa flow zaproszeń

| Flow | Opis |
|------|------|
| `invite` | Owner/Leader zaprasza użytkownika do zespołu |
| `request` | Użytkownik prosi o dołączenie do projektu |

### Lifecycle

```
pending → accepted → TeamMember tworzony automatycznie
         declined
         cancelled
```

### Indeksy

- `(ProjectId, InviteeId, Status)` — sprawdzanie czy zaproszenie istnieje
- `(InviteeId, Status)` — lista zaproszeń użytkownika

---

## 13. System czatu — Conversation / Message

### 13.1 Conversation

| Pole | Typ | Opis |
|------|-----|------|
| `Id` | `Guid` | PK |
| `Type` | `ConversationType` | `Direct` (0) / `Group` (1) |
| `Title` | `string?` | Tytuł (dla grupowych) |
| `LastMessageAt` | `DateTime?` | Czas ostatniej wiadomości |

### 13.2 ConversationParticipant

| Pole | Typ | Opis |
|------|-----|------|
| `ConversationId` | `Guid` | FK: Conversation |
| `UserId` | `Guid` | FK: User |
| `JoinedAt` | `DateTime` | Data dołączenia |
| `LastReadAt` | `DateTime?` | Ostatnia odczytana wiadomość |
| `IsMuted` | `bool` | Wyciszone powiadomienia |

### 13.3 Message

| Pole | Typ | Opis |
|------|-----|------|
| `SenderId` | `Guid` | FK: User |
| `Content` | `string` [10000] | Treść wiadomości |
| `MessageType` | `MessageType` | Direct / Group / Project |
| `ConversationId` | `Guid?` | FK: Conversation |
| `ProjectId` | `Guid?` | FK: Project (wiadomości projektowe) |
| `ReplyToId` | `Guid?` | FK: Message (odpowiedź) |
| `IsEdited` | `bool` | Edytowana |
| `IsDeleted` | `bool` | Soft delete |
| `IsAiGenerated` | `bool` | Wygenerowana przez AI |

---

## 14. System umiejętności — Skill / UserSkill / UserSkillEntry

### 14.1 Skill

| Pole | Typ | Opis |
|------|-----|------|
| `Name` | `string` [100] | Unikalna nazwa (React, Python, ...) |
| `Category` | `string` [50] | Kategoria (Languages, Web, Backend, ...) |
| `Description` | `string?` | Opis |
| `IconUrl` | `string?` | URL ikony |

### 14.2 SkillAlias (synonimy)

| Pole | Typ | Opis |
|------|-----|------|
| `SkillId` | `Guid` | FK: Skill |
| `Alias` | `string` [100] | Oryginalny alias ("csharp") |
| `AliasNormalized` | `string` [120] | Znormalizowany (lowercase, stripped) — unikalny |

Poprawia wyszukiwanie: "csharp" → C#, ".net" → .NET.

### 14.3 UserSkill (legacy join table)

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | FK: User |
| `SkillId` | `Guid` | FK: Skill |
| `ProficiencyLevel` | `string` [50] | beginner / intermediate / advanced / expert |
| `YearsOfExperience` | `int?` | Lata doświadczenia |
| `Verified` | `bool` | Zweryfikowane |

### 14.4 UserSkillEntry (nowy system)

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | FK: User |
| `SkillId` | `Guid?` | FK: Skill (opcjonalne — może być null jeśli nie zmapowane) |
| `Raw` | `string` [100] | Oryginalny tekst wprowadzony przez użytkownika |
| `RawNormalized` | `string` [100] | Znormalizowany (do wyszukiwania) |

Zachowuje surowy tekst użytkownika, jednocześnie linkując do kanonicznych Skills.

### 14.5 ProjectTechStack

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `SkillId` | `Guid` | FK: Skill |
| `IsRequired` | `bool` | Czy wymagana |
| `ProficiencyRequired` | `string?` [50] | Wymagany poziom |

---

## 15. System osiągnięć — Achievement / UserAchievement

### 15.1 Achievement

| Pole | Typ | Opis |
|------|-----|------|
| `Code` | `string` [50] | Unikalny kod (first_project, team_leader) |
| `Title` | `string` [255] | Tytuł |
| `Description` | `string?` | Opis |
| `IconUrl` | `string?` | URL ikony |
| `Category` | `string?` [50] | projects / teamwork / social / profile / activity / special / moderation |
| `Points` | `int` | Punkty za osiągnięcie |

### 15.2 UserAchievement

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | FK: User |
| `AchievementId` | `Guid` | FK: Achievement |
| `EarnedAt` | `DateTime` | Data zdobycia |
| `Progress` | `int?` | Postęp (0-100) |

### 15.3 Seed — katalog osiągnięć

Seeder zawiera **55 osiągnięć** w 7 kategoriach:

| Kategoria | Przykłady | Ilość |
|-----------|-----------|-------|
| projects | First Project, Mission Complete, Finisher | 9 |
| teamwork | Team Player, Collaborator, Recruiter | 8 |
| social | First Follower, Rising Star, Influencer | 8 |
| profile | Profile Complete, Early Adopter, Skill Collector | 7 |
| activity | First Task, Productive, Week Warrior | 8 |
| special | Bug Hunter, Mentor, Top Rated | 7 |
| moderation | Rookie Moderator, Guardian, Support Hero | 8 |

---

## 16. System showcase — ShowcaseProject

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `Summary` | `string` | Opis showcase |
| `DemoUrl` | `string?` | URL demo |
| `DemoVideoUrl` | `string?` | URL wideo |
| `ScreenshotsJson` | `string?` (JSONB) | Array URL screenshotów |
| `RepositoryUrl` | `string?` | URL repozytorium |
| `MetricsJson` | `string?` (JSONB) | Metryki projektu |
| `Featured` | `bool` | Wyróżniony |
| `ViewsCount` | `int` | Wyświetlenia |
| `LikesCount` | `int` | Polubienia |

**ShowcaseComment** — drzewo komentarzy z `ParentCommentId` (zagnieżdżone odpowiedzi).

---

## 17. System aktywności i feedu — ActivityRecord / UserFollow / ProjectSubscription

### 17.1 ActivityRecord

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid?` | FK: Project (opcjonalne) |
| `ActorId` | `Guid` | FK: User (kto wykonał) |
| `TargetUserId` | `Guid?` | FK: User (kogo dotyczy) |
| `EventType` | `string` [100] | Typ zdarzenia |
| `Summary` | `string` [1000] | Opis |
| `PayloadJson` | `string?` (JSONB) | Dodatkowe dane |
| `Visibility` | `string` [50] | public / subscribers / members / private |
| `EventGroup` | `string` [50] | Grupowanie (project, social, ...) |

### 17.2 UserFollow

| Pole | Typ | Opis |
|------|-----|------|
| `FollowerId` | `Guid` | FK: User (kto obserwuje) |
| `FollowedId` | `Guid` | FK: User (kogo obserwuje) |

Klucz główny: `(FollowerId, FollowedId)` — composite key.

### 17.3 ProjectSubscription

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `UserId` | `Guid` | FK: User |

Subskrypcja aktualizacji projektu.

---

## 18. System news — ProjectNewsPost / NewsPostLike / NewsPostComment

### 18.1 ProjectNewsPost

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `AuthorId` | `Guid` | FK: User |
| `Title` | `string` [255] | Tytuł |
| `Content` | `string` | Treść |
| `Visibility` | `string` [50] | public / subscribers / members |
| `IsPinned` | `bool` | Przypięty |
| `AttachmentsJson` | `string?` | Metadane załączników |
| `LikesCount` / `CommentsCount` | `int` | Zdenormalizowane liczniki |

### 18.2 NewsPostLike

| Pole | Typ | Opis |
|------|-----|------|
| `NewsPostId` | `Guid` | FK: ProjectNewsPost |
| `UserId` | `Guid` | FK: User |

Jeden użytkownik = jeden like na post.

### 18.3 NewsPostComment

| Pole | Typ | Opis |
|------|-----|------|
| `NewsPostId` | `Guid` | FK: ProjectNewsPost |
| `AuthorId` | `Guid` | FK: User |
| `Content` | `string` [2000] | Treść |
| `DeletedAt` | `DateTime?` | Soft delete |

Płaska struktura (bez zagnieżdżenia komentarzy).

---

## 19. Integracje zewnętrzne — Integration

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `ServiceType` | `string` [50] | github / gitlab / jira |
| `ConfigJson` | `string?` (JSONB) | Konfiguracja integracji |
| `AccessTokenEncrypted` | `string?` | Zaszyfrowany token dostępu |
| `IsActive` | `bool` | Czy aktywna |
| `LastSyncAt` | `DateTime?` | Ostatnia synchronizacja |

**Helper property:** `Config` (`[NotMapped]`) — deserializacja `ConfigJson` do `Dictionary<string, object>`.

---

## 20. System rekomendacji AI — Recommendation / AiPlan / AiOperationLog / ProjectArtifact

### 20.1 Recommendation

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `UserId` | `Guid` | FK: User |
| `MatchScore` | `decimal(5,4)` | Wynik dopasowania (0.0 – 1.0) |
| `ReasoningJson` | `string?` (JSONB) | Uzasadnienie AI |
| `Viewed` | `bool` | Czy obejrzana |
| `Actioned` | `bool` | Czy podjęto akcję |

### 20.2 AiPlan — plan AI

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `CreatedByUserId` | `Guid` | FK: User |
| `Idea` | `string` [2000] | Pomysł na projekt |
| `TechStack` | `string` [500] | Stack technologiczny |
| `Status` | `string` [32] | draft / applied / rejected |
| `PlanVersion` | `string` [32] | v1, v2, ... |
| `PlanJson` | `string` (JSONB) | Pełny plan w JSON |
| `AppliedAt` | `DateTime?` | Data zastosowania |

### 20.3 AiOperationLog — audit AI

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `PlanId` | `Guid?` | FK: AiPlan |
| `OperationType` | `string` [32] | generate / apply |
| `Capability` | `string` [32] | plan / recommend / ... |
| `Provider` | `string?` [32] | groq / gemini / openai |
| `Model` | `string?` [64] | Nazwa modelu |
| `Status` | `string` [32] | started / completed / failed |
| `Success` | `bool` | Sukces/porażka |
| `DurationMs` | `int?` | Czas trwania (ms) |
| `PromptTokens` / `CompletionTokens` / `TotalTokens` | `int?` | Zużycie tokenów |
| `MetadataJson` | `string?` (JSONB) | Dodatkowe metadane |

### 20.4 ProjectArtifact — „Paszport Projektu"

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `Type` | `string` [50] | overview / tech_stack / architecture / roadmap / decisions |
| `Title` | `string?` [200] | Tytuł sekcji |
| `Content` | `string` | Treść (markdown, generowana przez AI) |
| `Version` | `int` | Monotoniczny numer wersji |
| `GeneratedByUserId` | `Guid?` | Kto wymusił generację |

Unikalny indeks: `(ProjectId, Type)` — jeden artifact per typ per projekt.

---

## 21. System wsparcia — SupportTicket / TicketMessage / TicketHistory

### 21.1 SupportTicket

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | Autor |
| `Category` | `string` [50] | question / bug / feature / billing / other |
| `Subject` | `string` [200] | Temat |
| `Description` | `string` [5000] | Opis problemu |
| `Status` | `string` [50] | open / in_progress / waiting_user / resolved / closed |
| `Priority` | `string` [50] | low / medium / high / urgent |
| `AssignedToUserId` | `Guid?` | Przypisany admin |
| `RelatedProjectId` | `Guid?` | Powiązany projekt |

### 21.2 TicketMessage

| `TicketId` | `Guid` | FK: SupportTicket |
| `AuthorId` | `Guid` | Autor wiadomości |
| `Content` | `string` [5000] | Treść |
| `IsInternal` | `bool` | Widoczne tylko dla admins (notatki wewnętrzne) |

### 21.3 TicketHistory

| `TicketId` | `Guid` | FK: SupportTicket |
| `ChangedByUserId` | `Guid` | Kto dokonał zmiany |
| `ChangeType` | `string` [50] | status / priority / assignment / category / reopen / escalate |
| `OldValue` / `NewValue` | `string?` [100] | Wartości przed/po |
| `Reason` | `string?` [500] | Powód zmiany |

---

## 22. System feedbacku społeczności — FeedbackItem / FeedbackVote / FeedbackComment

### 22.1 FeedbackItem

| Pole | Typ | Opis |
|------|-----|------|
| `AuthorId` | `Guid` | Twórca feedbacku |
| `Type` | `string` [50] | bug / suggestion / feature / question |
| `Title` | `string` [200] | Tytuł |
| `Status` | `string` [50] | open / under_review / planned / in_progress / completed / rejected / duplicate |
| `VoteCount` / `CommentCount` | `int` | Zdenormalizowane liczniki |

### 22.2 FeedbackVote

| `FeedbackId` | `Guid` | FK: FeedbackItem |
| `IsUpvote` | `bool` | true = upvote, false = downvote |

### 22.3 FeedbackComment

| `FeedbackId` | `Guid` | FK: FeedbackItem |
| `Content` | `string` [2000] | Treść |
| `DeletedAt` | `DateTime?` | Soft delete |

---

## 23. Pozostałe encje

### 23.1 Notification

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | Odbiorca |
| `Type` | `string` [50] | Typ (general, project, team, ...) |
| `Title` | `string` [255] | Tytuł |
| `Content` | `string?` [2000] | Treść |
| `RelatedEntityType` | `string?` [50] | Typ powiązanej encji |
| `RelatedEntityId` | `Guid?` | ID powiązanej encji |
| `Priority` | `string?` [16] | low / medium / high |
| `IsRead` | `bool` | Przeczytane |
| `ReadAt` | `DateTime?` | Data odczytania |

### 23.2 ModerationReport

| Pole | Typ | Opis |
|------|-----|------|
| `ReporterId` | `Guid` | FK: User (zgłaszający) |
| `TargetType` | `string` [32] | Project / Task / Message / User |
| `TargetId` | `Guid` | ID zgłaszanego obiektu |
| `Reason` | `string` [4000] | Powód zgłoszenia |
| `Status` | `string` [24] | pending / reviewed / resolved |
| `ActionTaken` | `string?` [200] | Podjęta akcja |

### 23.3 Review

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `ReviewerId` | `Guid` | FK: User (recenzent) |
| `ReviewedUserId` | `Guid?` | FK: User (recenzowany) |
| `Rating` | `int` [1-5] | Ocena |
| `ReviewText` | `string?` [2000] | Tekst recenzji |

### 23.4 RefreshToken

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | FK: User |
| `TokenHash` | `string` [64] | HMAC-SHA256 hash (nigdy plain text!) |
| `ExpiresAt` | `DateTime` | Wygaśnięcie |
| `LastUsedAt` | `DateTime?` | Ostatnie użycie |
| `UsageCount` | `int` | Licznik użyć (monitoring anomalii) |
| `IsRevoked` | `bool` | Unieważniony |
| `RevocationReason` | `string?` [100] | reuse_detected / user_logout / expired |

**Bezpieczeństwo (R4):** Token przechowywany jako hash SHA256, nie w plain text.

### 23.5 OutboxEvent — Transactional Outbox Pattern (REL-002)

| Pole | Typ | Opis |
|------|-----|------|
| `EventType` | `string` | Typ zdarzenia |
| `Payload` | `string` | Serializowany JSON |
| `Status` | `OutboxEventStatus` | Pending / Processing / Completed / Failed |
| `RetryCount` | `int` | Aktualna liczba prób |
| `MaxRetries` | `int` | Max prób (domyślnie 3) |
| `ErrorMessage` | `string?` | Komunikat ostatniego błędu |

**Przepływ:**
1. Zdarzenie zapisywane do bazy (w transakcji z operacją biznesową)
2. Background worker odczytuje Pending → ustawia Processing → publikuje do RabbitMQ
3. Po sukcesie: Completed. Po porażce × MaxRetries: Failed

### 23.6 AuditLog

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid?` | Wykonawca akcji |
| `UserRole` | `string?` [50] | Rola w momencie akcji |
| `Action` | `string` [100] | np. "user.hard_deleted", "role.changed" |
| `EntityType` | `string` [50] | Typ encji (User, Project, ...) |
| `EntityId` | `Guid?` | ID encji |
| `Details` | `string?` | Szczegóły (human-readable) |
| `IpAddress` | `string?` [45] | Adres IP |
| `Severity` | `string` [20] | info / warning / critical |

### 23.7 UserPrivacySettings

| Pole | Typ | Opis |
|------|-----|------|
| `UserId` | `Guid` | FK: User (1:1, unikalny indeks) |
| `ProfileVisibility` | `string` [50] | public / private / friends_only |
| `ShowEmail` | `bool` | def: false — Pokaż email publicznie |
| `ShowSkills` | `bool` | def: true |
| `ShowExperience` | `bool` | def: true |
| `ShowRating` | `bool` | def: true |
| `ShowProjects` | `bool` | def: true |
| `ShowSocialLinks` | `bool` | def: true |
| `ShowAchievements` | `bool` | def: true |
| `AllowEmailSearch` | `bool` | def: false |
| `NotifyOnMessages` | `bool` | def: true |
| `NotifyOnInvitations` | `bool` | def: true |
| `ActivityVisibility` | `string` [50] | public / followers / private |

---

## 24. Pliki i dokumenty — ProjectFile / ProjectDocument

### 24.1 ProjectFile

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `UploadedById` | `Guid` | FK: User (kto uploadował) |
| `FileName` | `string` [255] | Oryginalna nazwa |
| `ContentType` | `string?` [100] | MIME type |
| `FileSize` | `long` | Rozmiar (bajty) |
| `StorageKey` | `string` [500] | Klucz w object storage (S3/SeaweedFS) |
| `Category` | `string?` [50] | documentation / design / code / other |

### 24.2 ProjectDocument

| Pole | Typ | Opis |
|------|-----|------|
| `ProjectId` | `Guid` | FK: Project |
| `AuthorId` | `Guid` | FK: User |
| `Title` | `string` [200] | Tytuł |
| `Content` | `string` | Treść (Markdown / HTML) |
| `DocumentType` | `string?` [50] | readme / wiki / guide / changelog / api-docs |
| `ContentFormat` | `string` [20] | markdown / html / plain |
| `Path` | `string?` [500] | Ścieżka hierarchii (docs/getting-started.md) |
| `SortOrder` | `int` | Kolejność wyświetlania |
| `IsPublic` | `bool` | Widoczność publiczna |

---

## 25. Konfiguracja EF Core — EntityConfigurations

Plik konfiguracyjny `ApplyConfigurationsFromAssembly` automatycznie wczytuje **10 klas konfiguracyjnych**:

| Klasa | Encje | Kluczowe reguły |
|-------|-------|-----------------|
| `ActivityRecordConfiguration` | ActivityRecord | PayloadJson → JSONB |
| `AdminConfiguration` (ProjectIssue + UserPrivacySettings) | ProjectIssue, UserPrivacySettings | Cascade/Restrict FK, indeksy (ProjectId, Status, CreatedAt) |
| `AiConfiguration` (AiPlan + AiOperationLog + ProjectArtifact) | AiPlan, AiOperationLog, ProjectArtifact | PlanJson/MetadataJson → JSONB, unikalny (ProjectId,Type) |
| `FeedbackConfiguration` | FeedbackItem, FeedbackVote, FeedbackComment | — |
| `InvitationConfiguration` | Invitation | Cascade Project, Restrict Inviter/Invitee, indeksy |
| `NewsPostConfiguration` | ProjectNewsPost, NewsPostLike, NewsPostComment | — |
| `ProjectSubscriptionConfiguration` | ProjectSubscription | — |
| `SupportConfiguration` | SupportTicket, TicketMessage, TicketHistory | — |
| `TaskBoardConfiguration` (TaskLink + TaskAttachment) | TaskLink, TaskAttachment | Unique (Source,Target,LinkType), Cascade task, SetNull file |
| `UserFollowConfiguration` | UserFollow | Composite PK (FollowerId, FollowedId) |

### Wzorce konfiguracji

**Strategia DeleteBehavior:**

| Strategia | Kiedy |
|-----------|-------|
| `Cascade` | Usunięcie rodzica = usunięcie dzieci (Project → Tasks, Invitations) |
| `Restrict` | Zapobiega usunięciu (User → Invitations — nie kasuj usera z aktywnymi zaproszeniami) |
| `SetNull` | Ustawia FK na null (TaskAttachment → ProjectFile — plik usunięty, attachment zachowany) |

**Kolumny JSONB w PostgreSQL:**

| Encja | Pole | Typ PostgreSQL |
|-------|------|----------------|
| ActivityRecord | PayloadJson | jsonb |
| AiPlan | PlanJson | jsonb |
| AiOperationLog | MetadataJson | jsonb |
| Integration | ConfigJson | jsonb |
| Recommendation | ReasoningJson | jsonb |
| ShowcaseProject | ScreenshotsJson, MetricsJson | jsonb |
| ProjectRole | RequiredSkillsJson | jsonb |

---

## 26. Stałe domenowe — Constants

```csharp
public static class ProjectStatus
{
    public const string Draft = "draft";
    public const string Recruiting = "recruiting";
    public const string Active = "active";
    public const string Completed = "completed";
    public const string Archived = "archived";
}

public static class TeamMemberStatus
{
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Left = "left";
}

public static class ProjectVisibility
{
    public const string Public = "public";
    public const string Private = "private";
    public const string Unlisted = "unlisted";
}
```

---

## 27. EnvLoader — ładowanie zmiennych środowiskowych

Plik `Configuration/EnvLoader.cs` (192 linie) — lekki loader `.env` bez zewnętrznych zależności.

### 27.1 Funcje

| Metoda | Opis |
|--------|------|
| `Load(customPath?)` | Ładuje .env (thread-safe, singleton) |
| `BuildConnectionStringFromPostgresEnv(hostOverride?)` | Buduje connection string z POSTGRES_* env vars |

### 27.2 Algorytm ładowania

1. **Szukanie pliku** — `FindEnvFile()` rekursywnie od `AppContext.BaseDirectory` w górę
2. **Parsowanie** — linia po linii, ignoruje `#` komentarze
3. **Strip cudzysłowów** — `StripQuotes()` (single i double)
4. **Rozwijanie zmiennych** — `ExpandVariables()` — wsparcie `${VAR}` w wartościach
5. **Local overrides** — `ApplyLocalDatabaseOverrides()` — automatycznie podmienia hostname bazy dla lokalnego dev

### 27.3 Connection string z env

```csharp
// Priorytet:
// 1. DEVHUNT_DB_CONNECTION (explicit)
// 2. CONNECTIONSTRINGS__DEFAULTCONNECTION (ASP.NET Core convention)
// 3. Build from POSTGRES_HOST/PORT/DB/USER/PASSWORD
// 4. Fallback: "Host=localhost;Port=5432;Database=devhunt_db;Username=postgres;Password=postgres"
```

---

## 28. DesignTimeDbContextFactory — fabryka design-time

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevHuntDbContext>
{
    public DevHuntDbContext CreateDbContext(string[] args)
    {
        EnvLoader.Load();

        var connectionString =
            Environment.GetEnvironmentVariable("DEVHUNT_DB_CONNECTION") ??
            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULTCONNECTION") ??
            EnvLoader.BuildConnectionStringFromPostgresEnv() ??
            "Host=localhost;Port=5432;Database=devhunt_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<DevHuntDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new DevHuntDbContext(optionsBuilder.Options);
    }
}
```

Używana przez `dotnet ef` do generowania migracji bez uruchamiania pełnego serwisu.

### Komendy migracji

```bash
# Tworzenie nowej migracji
dotnet ef migrations add <Nazwa> \
  --project DevHunt.Infrastructure \
  --startup-project DevHunt.CoreApi

# Stosowanie migracji
dotnet ef database update \
  --project DevHunt.Infrastructure \
  --startup-project DevHunt.CoreApi
```

---

## 29. ReadWriteDbContextFactory — repliki odczytu

```csharp
public class ReadWriteDbContextFactory
{
    public DevHuntDbContext CreateWriteContext()  // → Primary (DefaultConnection)
    public DevHuntDbContext CreateReadContext()   // → Replica (ReadOnlyConnection)
}
```

### Cechy

| Kontekst | Connection String | QueryTrackingBehavior |
|----------|-------------------|----------------------|
| Write | `DefaultConnection` | Default (Tracking) |
| Read | `ReadOnlyConnection` (fallback → Primary) | **NoTracking** |

`NoTracking` eliminuje overhead śledzenia zmian dla zapytań readonly — optymalizacja wydajności.

---

## 30. DatabaseMigrator — runner migracji

### 30.1 Cel

Standalone job aplikujący migracje EF Core **przed** uruchomieniem serwisów. Zapobiega race conditions (multiple instances migrujących jednocześnie).

### 30.2 Algorytm

```
[1] EnvLoader.Load()
[2] Build IConfiguration (appsettings.json + env vars + CLI args)
[3] Get ConnectionString("DefaultConnection")
[4] Pętla retry (max 10 prób, exponential backoff):
    [4.1] new DevHuntDbContext(options)
    [4.2] await dbContext.Database.MigrateAsync()
    [4.3] Sukces → exit code 0
    [4.4] Błąd (NpgsqlException/Timeout/DbUpdateException) → retry po 2^n sekund
    [4.5] Inny błąd → exit code 1
```

### 30.3 Retry logic

| Próba | Opóźnienie |
|-------|------------|
| 1 | 2 s |
| 2 | 4 s |
| 3 | 8 s |
| 4 | 16 s |
| ... | 2^n s |
| 10 | 1024 s (~17 min) |

### 30.4 Exit codes

| Kod | Znaczenie |
|-----|-----------|
| 0 | Sukces — migracje zastosowane |
| 1 | Porażka — po wyczerpaniu prób lub niespodziewany błąd |

---

## 31. DatabaseSeeder — generator danych testowych

### 31.1 Cel

Populowanie bazy realistycznymi danymi seed dla development i testowania.

### 31.2 Dane seed

| Kategoria | Ilość elementów | Opis |
|-----------|-----------------|------|
| **Skills** | ~220 | 13 kategorii (Languages, Web, Backend, Databases, DevOps, Cloud, Security, Mobile, Data, AI/ML, Testing, Design, Product) |
| **Skill Aliases** | wiele | Synonimy (csharp → C#, .net → .NET) |
| **Achievements** | 55 | 7 kategorii (projects, teamwork, social, profile, activity, special, moderation) |
| **E2E User** | 1 | Deterministyczny konto testowe (e2e@devhunt.local) |
| **Projects** | zmienna | Realistyczne projekty z TechStack |
| **Team Members** | zmienna | Przypisania do projektów |
| **Invitations** | zmienna | Zaproszenia/requestsy |
| **Tasks** | zmienna | Zadania Kanban |

### 31.3 Przepływ seedowania

```
[1] EnsureE2EUserAsync() — konto e2e@devhunt.local z BCrypt hasłem
[2] SeedSkillsAsync() — 220+ skills (idempotentnie — update jeśli istnieje)
[3] SeedSkillAliasesAsync() — synonimy
[4] SeedUserSkillsAsync() — powiązania user ↔ skill
[5] SeedAchievementsAsync() — 55 achievements
[6] (Opcjonalnie) SeedProjectsAsync() → SeedTeamMembersAsync() → SeedInvitationsAsync() → SeedTasksAsync()
```

**Env var `DEVHUNT_SEEDER_SKIP_PROJECTS=true`** — pomija seeding projektów (przydatne dla szybkich testów).

### 31.4 E2E User

```csharp
var email = Environment.GetEnvironmentVariable("DEVHUNT_E2E_USER_EMAIL") ?? "e2e@devhunt.local";
var password = Environment.GetEnvironmentVariable("DEVHUNT_E2E_USER_PASSWORD") ?? "TestPassword123!";
```

Konto deterministyczne do automatycznych testów E2E. Email zweryfikowany, rola `participant`.

### 31.5 Retry logic

Identyczny jak w DatabaseMigrator — 10 prób z exponential backoff. Obsługuje `PostgresException` (SqlState `42703` — brak kolumny) przez graceful skip.

---

## 32. System migracji — historia

### 32.1 Chronologiczna lista migracji

| Data | Nazwa | Opis |
|------|-------|------|
| 2025-10-30 | `InitialMigration` | Początkowy schemat (Users, Projects, TeamMembers, Tasks) |
| 2025-10-31 | `FullFeatureImplementation` | Notifications, Reviews, ModerationReports |
| 2025-11-02 | `ChatModuleImplementation` | Conversations, Messages, Participants |
| 2025-11-02 | `ChatEnhancements` | Ulepszenia czatu |
| 2025-11-02 | `SkillsSystemImplementation` | Skills, UserSkills, ProjectTechStack |
| 2025-11-02 | `ProjectRolesImplementation` | ProjectRoles |
| 2025-11-02 | `ShowcaseProjectsImplementation` | ShowcaseProjects, ShowcaseComments |
| 2025-11-02 | `RecommendationsImplementation` | Recommendations |
| 2025-11-02 | `IntegrationsImplementation` | Integrations |
| 2025-11-02 | `AchievementsSystemImplementation` | Achievements, UserAchievements |
| 2025-11-03 | `AddRefreshTokens` | RefreshTokens |
| 2025-11-03 | `AddSupportCommunityIssuesFilesPrivacy` | SupportTickets, FeedbackItems, ProjectFiles, UserPrivacySettings |
| 2025-11-03 | `AddTicketHistory` | TicketHistory |
| 2025-11-04 | `AddShowcaseCommentsProjectFilesDocs` | ShowcaseComments extended, ProjectDocuments |
| 2025-11-08 | `ParticipantOnlySeed` | Seed data |
| 2025-11-09 | `AddInvitationType` | Invitation.Type field |
| 2025-11-12 | `RefreshToken_AddHashing` | TokenHash zamiast plain Token |
| 2025-11-12 | `OutboxPattern_AddOutboxEvents` | OutboxEvents |
| 2025-11-24 | `AddActivityProjectNews` | ActivityRecords, ProjectNewsPosts |
| 2025-11-24 | `AddUserFollowAndActivityExtensions` | UserFollows, ActivityRecord extensions |
| 2025-11-24 | `MakeActivityProjectOptional` | ActivityRecord.ProjectId nullable |
| 2025-11-25 | `AddPrivacyAndPermissions` | Rozszerzone privacy/permissions |
| 2025-11-25 | `OptimizeFeedIndexes` | Indeksy performance |
| 2025-12-10 | `AddSocialAuthAndVerification` | OAuth (GitHub, Google), weryfikacja email |
| 2025-12-22 | `AddSkillAliases` | SkillAliases |
| 2025-12-22 | `AddUserSkillEntries` | UserSkillEntries |
| 2025-12-23 | `AddProjectDefaultVisibilities` | DefaultNewsVisibility, DefaultFilesVisibility |
| 2025-12-29 | `AddPasswordResetFields` | PasswordResetToken/ExpiresAt |
| 2026-01-05 | `AddTaskBoardEnhancements` | TaskColumns, TaskLinks, TaskAttachments, TaskBoardSettings |
| 2026-01-10 | `AddAiPlanning` | AiPlans |
| 2026-01-10 | `AddAiOperationLogMetadata` | AiOperationLog extended |
| 2026-01-12 | `Projects_AddPerformanceIndexes` | Indeksy wydajności na Projects |
| 2026-01-21 | `AddGitHubSyncFields` | GitHubIssueId/Number/Url na TaskItem |
| 2026-02-09 | `AddNewsLikesAndComments` | NewsPostLikes, NewsPostComments |
| 2026-02-10 | `AddProjectArtifacts` | ProjectArtifacts |

**Łącznie: 35 plików migracji** (migracja + .Designer.cs + snapshot).

---

## 33. Centralne zarządzanie pakietami — Directory.Packages.props

### Idea

`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` — **wszystkie projekty .NET** w solucji używają tych samych wersji pakietów, zdefiniowanych w jednym miejscu.

### Kluczowe pakiety

| Kategoria | Pakiet | Wersja |
|-----------|--------|--------|
| **ORM** | Microsoft.EntityFrameworkCore | 9.0.1 |
| **DB** | Npgsql.EntityFrameworkCore.PostgreSQL | 9.0.1 |
| **Auth** | Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 |
| **OAuth** | AspNet.Security.OAuth.GitHub | 7.0.1 |
| **OAuth** | Microsoft.AspNetCore.Authentication.Google | 10.0.0 |
| **JWT** | Microsoft.IdentityModel.Tokens | 8.0.1 |
| **JWT** | System.IdentityModel.Tokens.Jwt | 8.0.1 |
| **Cache** | Microsoft.Extensions.Caching.StackExchangeRedis | 9.0.0 |
| **SignalR** | Microsoft.AspNetCore.SignalR.StackExchangeRedis | 10.0.0 |
| **Health** | Microsoft.Extensions.Diagnostics.HealthChecks | 9.0.0 |
| **Logging** | Serilog.AspNetCore | 9.0.0 |
| **Security** | BCrypt.Net-Next | 4.0.3 |
| **API Docs** | Swashbuckle.AspNetCore | 7.2.0 |
| **Rate Limit** | AspNetCoreRateLimit | 5.0.0 |
| **Resilience** | Polly | 8.4.2 |
| **S3** | AWSSDK.S3 | 3.7.401 |
| **RabbitMQ** | RabbitMQ.Client | 6.8.1 |
| **Tracing** | OpenTelemetry (all) | 1.11.1 |
| **Email** | MailKit | 4.9.0 |
| **Metrics** | prometheus-net.AspNetCore | 8.2.1 |
| **Testing** | xUnit + Moq + FluentAssertions + coverlet | — |
| **Architecture Tests** | NetArchTest.Rules | 1.3.2 |

---

## 34. Shared package — packages/throttle

Współdzielony pakiet Node.js używany przez **Integration Gateway** i **Notification Service**.

### 34.1 Lokalizacja

```
packages/throttle/index.js (247 linii)
```

### 34.2 API

```javascript
export function createThrottle(options) → { throttle, getThrottleMetrics, shutdownThrottle }
```

### 34.3 Parametry

| Parametr | Domyślna | Opis |
|----------|----------|------|
| `logger` | wymagany | Instancja loggera |
| `maxConcurrent` | 100 | Max aktywnych żądań |
| `maxQueueSize` | 200 | Max rozmiar kolejki |
| `cpuThreshold` | 0.8 | CPU limit (80%) |
| `memoryThreshold` | 0.9 | RAM limit (90%) |
| `metricsRefreshMs` | 2000 | Interwał odświeżania metryk systemu |

### 34.4 Algorytm

```
Żądanie → isSystemOverloaded()?
              │ TAK → 503 (retryAfter: 5)
              │ NIE
              ▼
         activeRequests < maxConcurrent?
              │ TAK → Przetwarzaj natychmiast, activeRequests++
              │ NIE
              ▼
         requestQueue.length < maxQueueSize?
              │ TAK → Dodaj do kolejki (timeout 30s)
              │ NIE → 503 (retryAfter: 10)
```

### 34.5 Monitorowanie systemu

```javascript
function getCpuLoad() → oblicza CPU usage z os.cpus()
function getMemoryUsage() → (totalMemory - freeMemory) / totalMemory
```

Odświeżane co `metricsRefreshMs` (timer z `.unref()` — nie blokuje event loop shutdown).

### 34.6 Użycie w serwisach

| Serwis | maxConcurrent | maxQueueSize |
|--------|---------------|-------------|
| Integration Gateway | 100 | 200 |
| Notification Service | 50 | 100 |

---

## 35. Docker — DatabaseMigrator i DatabaseSeeder

### 35.1 DatabaseMigrator Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build     # Build stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final   # Runtime stage

# Multi-stage build:
# 1. Restore → Build → Publish (SDK image)
# 2. Copy published output (ASP.NET runtime image)
# Non-root user: appuser
```

### 35.2 DatabaseSeeder Dockerfile

Identyczna struktura jak Migrator. Dodatkowa zależność: `BCrypt.Net-Next` (do hashowania hasła E2E user).

### 35.3 Uruchamianie w Docker Compose

```yaml
# DatabaseMigrator uruchamiany PRZED serwisami
db-migrator:
  build:
    context: .
    dockerfile: DevHunt.DatabaseMigrator/Dockerfile
  depends_on:
    db:
      condition: service_healthy
  # Exit po zakończeniu migracji (exit code 0)

# DatabaseSeeder uruchamiany PO migracjach
db-seeder:
  build:
    context: .
    dockerfile: DevHunt.DatabaseSeeder/Dockerfile
  depends_on:
    db-migrator:
      condition: service_completed_successfully
```

---

## 36. Diagram ERD — pełna mapa relacji

```
┌────────────────────────────────────────────────────────────────────┐
│                          User                                      │
│  (Id, Email, PasswordHash, Role, FullName, Bio, ...)              │
└──────┬───────────┬───────────┬───────────┬───────────┬────────────┘
       │           │           │           │           │
       │ 1:N       │ 1:N       │ 1:N       │ 1:N       │ 1:1
       ▼           ▼           ▼           ▼           ▼
TeamMember   Invitation   TaskItem   UserSkillEntry  UserPrivacy
 (UserId)    (Inviter/    (Assigned)  (UserId)       Settings
 (ProjectId)  Invitee)                                (UserId)
       │           │
       │           │
  ┌────▼────┐   ┌──▼────┐
  │ Project │◄──┤       │
  │ (Id)    │   │       │
  └────┬────┘   └───────┘
       │
       │ 1:N (wiele relacji)
       ├──→ TaskItem ──→ TaskColumn, TaskLink, TaskAttachment
       ├──→ ProjectTechStack ──→ Skill
       ├──→ ProjectRole
       ├──→ ShowcaseProject ──→ ShowcaseComment
       ├──→ ProjectFile
       ├──→ ProjectDocument
       ├──→ ProjectNewsPost ──→ NewsPostLike, NewsPostComment
       ├──→ ProjectSubscription
       ├──→ ActivityRecord
       ├──→ Integration
       ├──→ AiPlan ──→ AiOperationLog
       ├──→ ProjectArtifact
       └──→ Recommendation

  ┌────────────────┐       ┌─────────────────┐
  │ Conversation   │ 1:N   │ Message         │
  │ (Direct/Group) │──────→│ (Content, Reply)│
  └──────┬─────────┘       └─────────────────┘
         │ M:N
         ▼
  ConversationParticipant (UserId, ConversationId)

  ┌──────────────┐      ┌──────────────┐      ┌─────────────────┐
  │ Skill        │ 1:N  │ SkillAlias   │      │ UserSkill       │
  │ (Name, Cat.) │─────→│ (Alias)      │      │ (legacy M:N)    │
  └──────────────┘      └──────────────┘      └─────────────────┘

  ┌──────────────┐      ┌──────────────┐
  │ Achievement  │ M:N  │ UserAchieve. │
  │ (Code, Pts)  │─────→│ (EarnedAt)   │
  └──────────────┘      └──────────────┘

  ┌──────────────┐      ┌──────────────┐      ┌─────────────────┐
  │ SupportTicket│ 1:N  │TicketMessage │      │ TicketHistory   │
  └──────────────┘─────→└──────────────┘      └─────────────────┘

  ┌──────────────┐      ┌──────────────┐      ┌─────────────────┐
  │ FeedbackItem │ 1:N  │FeedbackVote  │      │ FeedbackComment │
  └──────────────┘─────→└──────────────┘      └─────────────────┘

  ┌──────────────────────────────────────┐
  │ Standalone entities:                  │
  │ • Notification (UserId)              │
  │ • ModerationReport (ReporterId)      │
  │ • Review (ProjectId, ReviewerId)     │
  │ • RefreshToken (UserId, TokenHash)   │
  │ • OutboxEvent (EventType, Payload)   │
  │ • AuditLog (Action, Severity)        │
  │ • UserFollow (FollowerId, FollowedId)│
  └──────────────────────────────────────┘
```

---

## 37. Zależności NuGet

### DevHunt.Infrastructure

| Pakiet | Zastosowanie |
|--------|-------------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.1 | Provider PostgreSQL dla EF Core |
| `Microsoft.EntityFrameworkCore.Design` 9.0.1 | Narzędzia design-time (migracje) |

### DevHunt.DatabaseMigrator

| Pakiet | Zastosowanie |
|--------|-------------|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Provider DB |
| `Microsoft.EntityFrameworkCore.Design` | Design-time |
| `Microsoft.Extensions.Logging.Console` | Logowanie |
| `Microsoft.Extensions.Configuration.*` | Konfiguracja (JSON, env, CLI) |

### DevHunt.DatabaseSeeder

| Pakiet | Zastosowanie |
|--------|-------------|
| Pakiety jak Migrator + | — |
| `BCrypt.Net-Next` | Hashowanie hasła E2E user |

### Directory.Build.props (globalne)

| Ustawienie | Wartość |
|------------|---------|
| TargetFramework | net10.0 |
| Nullable | enable |
| ImplicitUsings | enable |
| TreatWarningsAsErrors | false |
| NoWarn | CS1591 (brak XML comments) |

---

*Dokument wygenerowany na podstawie analizy kodu źródłowego DevHunt.Infrastructure, DevHunt.DatabaseMigrator, DevHunt.DatabaseSeeder i packages/throttle.*
*Wersja: 1.0 | Data: 2025*
