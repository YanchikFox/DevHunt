---
title: File Storage — project files, documents, task attachments, avatars
type: domain
status: verified
sources:
  - DevHunt.CoreApi/Controllers/ProjectFilesController.cs
  - DevHunt.CoreApi/Controllers/ProjectDocumentsController.cs
  - DevHunt.CoreApi/Controllers/TaskAttachmentsController.cs
  - DevHunt.CoreApi/Controllers/ProfileController.cs
  - DevHunt.CoreApi/Services/ObjectStorageService.cs
  - DevHunt.CoreApi/Program.cs
verified_at: 2026-05-04
verified_against_commit: cc43ab9
last_user_review: null
---

## What it is

Anything user-uploaded that isn't a chat message: images, generic
project files, long-form project documents (with AI-generation
flavors for "passport" and "diagram"), kanban task attachments,
and user avatars. All blobs land in **SeaweedFS** (S3-compatible)
through one shared service.

## Surface

### HTTP — Core API

[ProjectFilesController.cs](DevHunt.CoreApi/Controllers/ProjectFilesController.cs)
(`/api/projects/{projectId}/files`):

- `GET /` — list
- `GET /gallery` — image-only view at line 250
- `POST /` — upload
- `GET /{fileId}` — fetch metadata / signed URL
- `DELETE /{fileId}`

[ProjectDocumentsController.cs](DevHunt.CoreApi/Controllers/ProjectDocumentsController.cs)
(`/api/projects/{projectId}/docs`):

- `GET /`, `GET /{docId}`, `POST /`, `PUT /{docId}`,
  `DELETE /{docId}` — document CRUD
- `POST /generate-passport` (line 353) — calls
  `MLServiceClient.GeneratePassportAsync`
- `POST /generate-diagram` (line 470) — calls
  `MLServiceClient.GenerateDiagramAsync`

[TaskAttachmentsController.cs](DevHunt.CoreApi/Controllers/TaskAttachmentsController.cs)
(`/api/projects/{projectId}/tasks/{taskId}/attachments`):

- `GET /`, `POST /` (upload blob), `POST /link` (point at
  existing `ProjectFile`), `GET /{attachmentId}`,
  `DELETE /{attachmentId}`

Avatar endpoints from
[ProfileController.cs](DevHunt.CoreApi/Controllers/ProfileController.cs):

- `POST /api/profile/avatar`, `DELETE /api/profile/avatar` —
  cross-references the `auth-and-identity` domain at the API
  level but the storage path is owned here.

### UI

No top-level "files" route. UI is embedded inside the project
workspace
([frontend/src/app/[locale]/dashboard/projects/[id]/_components/](frontend/src/app/[locale]/dashboard/projects/[id]/_components/)).
Avatar UI lives in
[frontend/src/app/[locale]/dashboard/profile/edit/](frontend/src/app/[locale]/dashboard/profile/edit/).

### SignalR

None.

## Entities involved

- `ProjectFile` — generic project upload, FK to `Project` and
  `User` (UploadedBy).
- `ProjectDocument` — long-form document, FK to `Project` and
  `User` (Author).
- `TaskAttachment` — FK to `TaskItem` (Task), `User`
  (AttachedBy), and **optionally** `ProjectFile` (link mode).

Avatars are stored on the `User` row directly (column path on
the user — exact field name not enumerated; consult
`ProfileController.cs` if needed).

Authoritative: [data/entity-catalog.md](../data/entity-catalog.md).

## Crosses these systems

- [systems/core-api.md](../systems/core-api.md) — controllers
  + `S3ObjectStorageService` (registered scoped at
  [Program.cs:240](DevHunt.CoreApi/Program.cs#L240)) +
  `FileUploadValidationMiddleware` in the request pipeline.
- [systems/ml-service.md](../systems/ml-service.md) — invoked by
  `MLServiceClient` for the passport / diagram document flavors.
- [systems/infrastructure.md](../systems/infrastructure.md) —
  the three entities.
- [systems/frontend.md](../systems/frontend.md) — UI inside
  project workspace and profile edit page.
- **External:** the SeaweedFS deployable
  (`object-storage` compose service); not represented as
  `systems/*.md` because the source code isn't in this repo.

## Known traps

- **Object-storage initialization is non-fatal at boot.** Per
  [Program.cs:466-481](DevHunt.CoreApi/Program.cs#L466-L481),
  `IObjectStorageService.InitializeBucketsAsync` runs after
  Build and its failure logs a warning rather than crashing —
  the API starts in degraded mode and uploads will fail. Don't
  read "API is up" as "uploads work."
- **`FileUploadValidationMiddleware` runs early in the
  pipeline** (per [Program.cs:374](DevHunt.CoreApi/Program.cs#L374)).
  Body-size limits are 10 MB total / 4 MB per field
  (FormOptions, lines 263-274). Bigger uploads fail before
  hitting any controller.

## What I should NOT assume

- **`ProjectFile` and `TaskAttachment` are not the same row**
  even when a `link` attachment points at a `ProjectFile`.
  Deleting a `ProjectFile` that has linked attachments will
  cascade per the FK rules in
  [data/entity-catalog.md](../data/entity-catalog.md);
  deleting a `link`-mode `TaskAttachment` does **not** delete
  the underlying `ProjectFile`. Different lifetimes.
- **Generate-passport and generate-diagram write
  `ProjectDocument` rows.** They are a *generation* surface
  using ml-service compute, not a separate document type. The
  output is a regular `ProjectDocument`; the difference from
  `POST /docs` is only the body-fill mechanism.
- **Avatars and project files share the bucket** but have
  different access patterns. Avatars are public; project files
  may be visibility-gated by the controller. Don't expose
  signed URLs without checking what surface generated them.
- **The S3 client is `AWSSDK.S3`** (per
  [Directory.Packages.props](Directory.Packages.props))
  pointed at SeaweedFS. Behavior diverges from real AWS S3 in
  edge cases — multi-part upload thresholds, list-objects
  pagination semantics. Test against the actual SeaweedFS
  before claiming feature parity.
- **Avatar deletion does not free the blob** automatically —
  not verified in this pass; depends on whether
  `ProfileController.DELETE /avatar` calls
  `IObjectStorageService.DeleteObjectAsync`. Open the
  controller before asserting.
