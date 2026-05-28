# POC Landing Page — .NET 10 MVC

ASP.NET Core MVC + Entra ID + per-POC RBAC. Implements `docs/plan-integrate-with-entraid.md`.

## Layout

```
src/
├── PocLandingPage.slnx                 — solution
├── PocLandingPage.Web/                 — MVC app
│   ├── Api/                            — REST controllers (/api/pocs, /api/invitations)
│   ├── Controllers/                    — MVC controllers (Home, PocsAdmin, Invitations)
│   ├── Models/                         — PocEntry, GuestUser, DTOs
│   ├── Options/                        — Strongly-typed config (AzureAd, AzureOpenAI, Storage)
│   ├── Services/                       — PocService, InvitationService, UserDirectoryService, DescriptionGenerator
│   ├── Views/                          — Razor views (Bootstrap 5)
│   └── wwwroot/                        — static assets + admin page JS
└── PocLandingPage.Tests/               — xUnit + FluentAssertions + Moq
```

## Running locally

Prerequisites (one-time): work through **`../docs/manual-setup.md`** in the Azure Portal — app registration, Graph permissions, storage, etc. The app boot fails fast if `AzureAd:ServicePrincipalId` is missing.

```powershell
cd PocLandingPage.Web

dotnet user-secrets init
dotnet user-secrets set "AzureAd:TenantId"           "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId"           "<client id>"
dotnet user-secrets set "AzureAd:ClientSecret"       "<client secret>"
dotnet user-secrets set "AzureAd:ServicePrincipalId" "<enterprise app object id>"
dotnet user-secrets set "Storage:BlobEndpoint"       "https://<account>.blob.core.windows.net"
# Optional — enables ✨ Generate with AI in the POC admin page
dotnet user-secrets set "AzureOpenAI:Endpoint"       "https://<resource>.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:Deployment"     "gpt-4o-mini"

dotnet run
```

The app uses `DefaultAzureCredential`: in dev it picks up your Azure CLI / Visual Studio credentials; in production it uses the App Service Managed Identity.

## Running tests

```powershell
dotnet test src/PocLandingPage.slnx
```

The unit suite mocks Graph and Azure OpenAI via the `IGraphUserLookup`, `IInvitationService`, `IDescriptionGenerator` interfaces — no live calls. Blob storage is tested via an `InMemoryBlobStore` that simulates ETag concurrency.

## Roles

Defined as App Roles on the App Registration (see `docs/manual-setup.md` Phase 1.3):

- `POC.Viewer` — sees only POCs they've been granted access to (or POCs with `AllowAllViewers = true`).
- `POC.Admin` — sees all POCs, manages POC entries and per-POC access, manages B2B guests.

Per-POC access lists store Entra `oid`s; the API accepts emails and resolves them via Graph (`IUserDirectoryService`, cached for 5 min).

## What's not implemented here

Phase 1 (app registration), Phase 2a (Graph permission consent), Phase 7 (Azure infrastructure), the initial `POC.Admin` assignment, and the GitHub Actions deploy pipeline — all human-in-the-loop. See `docs/manual-setup.md`.
