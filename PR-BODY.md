# Rebuild POC Landing Page as .NET 10 MVC with Entra ID + per-POC RBAC

Implements [docs/plan-integrate-with-entraid.md](docs/plan-integrate-with-entraid.md). The previous static HTML + shared password is replaced with an authenticated ASP.NET Core MVC app where access to each POC is controlled by the admin.

## Summary

- ASP.NET Core MVC on **.NET 10**, all code under `src/`.
- **Microsoft.Identity.Web** for OIDC sign-in against the Test Tenant; two authorization policies (`ViewerOrAdmin`, `AdminOnly`).
- **Per-POC access control** — each `PocEntry` carries `AllowAllViewers` plus an explicit `AllowedUserIds` list. `HomeController` and `GET /api/pocs` filter by the caller's `oid` claim; admins always see everything.
- **Admin pages** at `/admin/pocs` and `/admin/invitations`, with a checkbox-list UX and a master "All viewers" toggle.
- **Email-only input** — admins pick emails in the UI; the backend resolves to `oid` via Graph (`IUserDirectoryService` with 5-min `IMemoryCache`). Unresolved emails surface back to the UI.
- **Azure Blob storage** for the POC list (`pocs.json` in container `poc-data`) with **ETag concurrency** and automatic retry on conflict.
- **Microsoft Graph** wraps invitations: list guests, send B2B invite + role assignment, change role, revoke access (`InvitationService`).
- **Azure OpenAI** powers the optional **✨ Generate with AI** button; feature-flagged off when `AzureOpenAI:Endpoint` is empty (so the app boots fine without it).
- **DefaultAzureCredential** everywhere — Managed Identity in prod, Az CLI / VS creds in dev. No secrets in code.
- Validate-on-start options pattern; the app refuses to boot if `AzureAd:ServicePrincipalId` is missing (common config mistake).

## What's out of scope

These are human-in-the-loop and live in [docs/manual-setup.md](docs/manual-setup.md):

- Phase 1 — App Registration in Test Tenant
- Phase 2a — Graph permissions + admin consent
- Phase 7 — Azure infrastructure (App Service, Storage, Key Vault, App Insights, Azure OpenAI)
- Initial `POC.Admin` assignment in Enterprise Applications
- GitHub Actions deploy pipeline

## Test plan

Automated (`dotnet test src/PocLandingPage.slnx`):
- [x] `PocEntry.IsVisibleTo` — `AllowAllViewers`, OID membership, case-insensitive match, not-in-list rejection.
- [x] `PocService` — empty blob, add/get/update/delete, oid filtering, retry-on-concurrent-conflict.
- [x] `UserDirectoryService` — first-call resolution, cache reuse, null on not-found, split resolved/unresolved, cached-vs-Graph fetch paths.
- [x] `NullDescriptionGenerator` — disabled flag + clear error message.

Manual verification still to do (requires the manual-setup steps to be complete):
- [ ] Sign in as `POC.Admin` — both admin pages render, sibling nav links shown.
- [ ] Sign in as `POC.Viewer` — only POCs in their access list (or `AllowAllViewers`) shown; admin nav hidden.
- [ ] Add POC: emails resolve, unresolved emails surface a warning.
- [ ] Toggle "All viewers": per-user checkboxes greyed out.
- [ ] Edit POC: existing access populates checkboxes from `/api/pocs/{id}/access`.
- [ ] Generate description with AI when `AzureOpenAI:Endpoint` is configured.
- [ ] Invite a B2B user, change their role, revoke access — confirm against Enterprise Applications → Users and groups.

## Notable judgment calls

- **No Serilog**: the plan named it, but Serilog.Sinks.ApplicationInsights pins to App Insights 2.x while the rest of the SDK is on 3.x. Dropped Serilog and rely on the built-in `ILogger` + Application Insights provider — same observability outcome, no version conflicts.
- **FluentAssertions 7.x**: pinned to the last MIT-licensed version to avoid the commercial 8.x license.
- **`IGraphUserLookup` abstraction**: thin interface in front of `GraphServiceClient` so `UserDirectoryService` cache logic is fully unit-testable without an integration-test environment.
- **`IBlobStore` abstraction**: lets `PocService` ETag retry logic be tested against an `InMemoryBlobStore` that simulates conflicts.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
