# Plan: POC Landing Page — Migrate to Dynamic ASP.NET Core MVC + Entra ID (RBAC)

## Problem Statement

The current POC Landing Page is a static HTML file protected by a simple password. The goal is to:

1. Rebuild it as a **dynamic ASP.NET Core MVC application** (latest .NET) with a supporting **API**.
2. Replace the password-based access with **Microsoft Entra ID authentication** and **RBAC**.
3. Support a **cross-tenant scenario**: users live in the **Real (Production) Tenant**, but all applications (including this landing page) are hosted in the **Test Tenant**.

---

## Architecture Overview

```
Real Tenant (Production)          Test Tenant
┌──────────────────────┐          ┌──────────────────────────────────────┐
│  Inholland Users     │  login   │  App Registration: poc-landing-page  │
│  (Real AAD users)    │ ──────►  │  - Multi-tenant or B2B invite        │
│                      │          │  - App Roles defined here            │
└──────────────────────┘          │                                      │
                                  │  Azure Static Web App / App Service  │
                                  │  ASP.NET Core MVC + API              │
                                  └──────────────────────────────────────┘
```

### Cross-Tenant Strategy: B2B Guest Users

- Real Tenant users are **invited as B2B guests** into the Test Tenant.
- The app registration lives in the **Test Tenant** only (single-tenant registration is sufficient).
- App Roles are assigned to guest users (or groups) in the Test Tenant.
- This keeps the Test Tenant fully isolated from production whilst still using real identities.

> Alternative: Make the app registration multi-tenant (allow accounts from any org). Only recommended if B2B invites are not feasible.

---

## Roles (RBAC)

| Role Name | Permissions |
|---|---|
| `POC.Admin` | View **all** POCs · Add/edit/delete POC entries · Manage per-POC access (grant/revoke users) · Invite B2B guests · Change guest roles · Revoke guest access |
| `POC.Viewer` | Read-only access — sees **only the POCs the admin has granted them access to** |

### Per-POC Access Control

In addition to the two app roles, **each POC entry carries its own access list** (a list of user object IDs / emails). A `POC.Viewer` sees only the POCs whose access list contains their identity. A `POC.Admin` always sees every POC regardless of the list. This is enforced in `PocService.GetVisibleForUserAsync(...)` (see Phase 5) by filtering the JSON store against the signed-in user's `oid` claim.

Roles are defined as **App Roles** in the App Registration manifest in the Test Tenant. Guest users from the Real Tenant are assigned one of these roles in **Enterprise Applications → Users and Groups** in the Test Tenant.

---

## Technology Stack

| Layer | Technology |
|---|---|
| Web Framework | ASP.NET Core MVC (.NET 10 LTS) |
| Authentication | `Microsoft.Identity.Web` |
| Authorization | ASP.NET Core Policy-based RBAC (App Roles) |
| API | ASP.NET Core Web API (same project, `/api/` prefix) |
| Frontend | Razor Views + Bootstrap 5.3 (latest) |
| Hosting | Azure App Service (Test Tenant subscription) |
| Config / Secrets | Azure App Configuration + Key Vault (Test Tenant) |

---

## Project Structure

```
PocLandingPage/
├── PocLandingPage.sln
├── PocLandingPage.Web/
│   ├── Controllers/
│   │   ├── HomeController.cs              # Landing page (requires POC.Viewer) — filtered by per-POC access
│   │   ├── PocsAdminController.cs         # Manage POC entries + per-POC access (requires POC.Admin)
│   │   └── InvitationsController.cs       # B2B management page (requires POC.Admin)
│   ├── Api/
│   │   ├── PocsController.cs              # REST: GET/POST/PUT/DELETE /api/pocs + /api/pocs/{id}/access + /api/pocs/generate-description
│   │   └── InvitationsApiController.cs    # REST: GET /api/invitations/guests, POST, PUT, DELETE
│   ├── Models/
│   │   ├── PocEntry.cs                    # POC project model (incl. AllowedUserIds list)
│   │   ├── GuestUser.cs                   # ViewModel: guest user + role + invite status
│   │   ├── InviteUserRequest.cs           # Input model: Email + Role
│   │   └── GenerateDescriptionRequest.cs  # Input model: Name + optional URL/keywords
│   ├── Services/
│   │   ├── IPocService.cs / PocService.cs
│   │   ├── IInvitationService.cs
│   │   ├── InvitationService.cs           # Graph API wrapper
│   │   ├── IDescriptionGenerator.cs
│   │   └── DescriptionGenerator.cs        # LLM wrapper (Azure OpenAI)
│   ├── Views/
│   │   ├── Home/Index.cshtml              # Dynamic POC list (only POCs user has access to)
│   │   ├── PocsAdmin/Index.cshtml         # POC entry management + access list editor + "Generate description" button
│   │   ├── Invitations/Index.cshtml       # B2B guest management UI
│   │   └── Shared/_Layout.cshtml          # Layout with user info + logout + admin nav (Manage POCs / Manage Users)
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   └── Program.cs
└── PocLandingPage.Tests/
    └── ...
```

---

## Implementation Steps

> **Recommended implementation order:** Phase 3 → Phase 1 → Phase 2 → Phase 4 → Phase 5 → Phase 6 → Phase 7
> Create the project scaffold first, then configure the identity layer, then build features on top.

### Phase 1 — App Registration in Test Tenant

1. In the **Test Tenant** Azure Portal → Entra ID → App registrations → **New registration**.
2. Name: `poc-landing-page` (or similar).
3. Supported account types: **Accounts in this organizational directory only (Test Tenant)**.
4. Redirect URIs: `https://localhost:7xxx/signin-oidc` (dev) + production URL.
5. In the **Manifest**, define App Roles:
   ```json
   "appRoles": [
     {
       "allowedMemberTypes": ["User"],
       "description": "Can view all POC entries",
       "displayName": "POC Viewer",
       "id": "<new-guid>",
       "isEnabled": true,
       "value": "POC.Viewer"
     },
     {
       "allowedMemberTypes": ["User"],
       "description": "Can manage POC entries",
       "displayName": "POC Admin",
       "id": "<new-guid>",
       "isEnabled": true,
       "value": "POC.Admin"
     }
   ]
   ```
6. Under **Authentication**: enable ID tokens, set logout URL.
7. Create a **Client Secret** (store in Key Vault — never in code).

### Phase 2 — B2B User Management Admin Page

A dedicated admin section (restricted to `POC.Admin`) allows managing B2B guest users entirely from within the app — no manual Azure Portal work needed.

#### 2a — Graph API Permissions

Add the following **Application permissions** to the App Registration in the Test Tenant (requires admin consent):

| Permission | Purpose |
|---|---|
| `User.Invite.All` | Send B2B invitations to external users |
| `User.Read.All` | List existing guest users and their details |
| `AppRoleAssignment.ReadWrite.All` | Assign / revoke App Roles on invited users |
| `Directory.Read.All` | Read user profile info (display name, status) |

Grant admin consent in: **Azure Portal → App Registration → API Permissions → Grant admin consent**.

The App Service **Managed Identity** is used at runtime — no client secret needed for Graph calls.

> ⚠️ **`AzureAd:ServicePrincipalId`** is the **Enterprise Application Object ID** (not the App Registration Client ID) found in: Test Tenant → Enterprise Applications → `poc-landing-page` → Overview → Object ID. Getting this wrong is a common mistake — add a startup guard:
> ```csharp
> var spId = builder.Configuration["AzureAd:ServicePrincipalId"];
> if (string.IsNullOrWhiteSpace(spId))
>     throw new InvalidOperationException("AzureAd:ServicePrincipalId is not configured. Set it to the Enterprise Application Object ID in the Test Tenant.");
> ```

Grant the Managed Identity the same permissions via:
```powershell
# Grant Managed Identity the Graph app roles
$graphSpId  = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id
$miObjectId = "<MANAGED-IDENTITY-OBJECT-ID>"

foreach ($role in @("User.Invite.All","User.Read.All","AppRoleAssignment.ReadWrite.All","Directory.Read.All")) {
    $appRole = (Get-MgServicePrincipal -ServicePrincipalId $graphSpId).AppRoles | Where-Object { $_.Value -eq $role }
    New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId -BodyParameter @{
        principalId = $miObjectId
        resourceId  = $graphSpId
        appRoleId   = $appRole.Id
    }
}
```

#### 2b — Project Structure Additions

```
Controllers/
└── InvitationsController.cs       # MVC page controller (GET /admin/invitations)

Api/
└── InvitationsApiController.cs    # REST API (POST /api/invitations, DELETE /api/invitations/{userId}, GET /api/invitations/guests)

Models/
├── GuestUser.cs                   # ViewModel: Id, Email, DisplayName, Role, InviteStatus
├── InviteUserRequest.cs           # Input: Email (required), Role (required: "POC.Viewer" | "POC.Admin")
└── UpdateRoleRequest.cs           # Input: Role (required: "POC.Viewer" | "POC.Admin")

Services/
├── IInvitationService.cs
└── InvitationService.cs           # Wraps GraphServiceClient calls

Views/
└── Invitations/
    └── Index.cshtml               # Admin B2B management UI
```

#### 2c — InvitationService (Graph API Calls)

```csharp
public interface IInvitationService
{
    Task<IEnumerable<GuestUser>> GetGuestsAsync();
    Task InviteUserAsync(string email, string role);         // invite + assign role
    Task UpdateRoleAsync(string userId, string newRole);     // revoke old role, assign new
    Task RevokeAccessAsync(string userId);                   // remove all app role assignments
}
```

**Key Graph calls used:**

| Operation | Graph API |
|---|---|
| Send invitation | `POST /invitations` |
| List guest users | `GET /users?$filter=userType eq 'Guest'` |
| Get app role assignments | `GET /servicePrincipals/{id}/appRoleAssignedTo` |
| Assign app role | `POST /servicePrincipals/{id}/appRoleAssignedTo` |
| Revoke app role | `DELETE /servicePrincipals/{id}/appRoleAssignedTo/{assignmentId}` |

**`InvitationService.cs` sketch:**

> Role IDs are resolved at runtime by looking up the service principal's `AppRoles` by `Value` (e.g. `"POC.Viewer"`). No GUIDs need to be stored in configuration.

```csharp
public class InvitationService(GraphServiceClient graph, IConfiguration config) : IInvitationService
{
    private readonly string _spId = config["AzureAd:ServicePrincipalId"];

    // Resolve role GUID by name (e.g. "POC.Viewer") from the live service principal — no config GUIDs needed.
    private async Task<Guid> GetRoleIdAsync(string roleName)
    {
        var sp = await graph.ServicePrincipals[_spId].GetAsync();
        var role = sp.AppRoles.FirstOrDefault(r => r.Value == roleName)
            ?? throw new InvalidOperationException($"App role '{roleName}' not found on service principal.");
        return role.Id!.Value;
    }

    // GAP 1 FIX: implement GetGuestsAsync by joining guest users with their app role assignments.
    public async Task<IEnumerable<GuestUser>> GetGuestsAsync()
    {
        // Fetch all guest users
        var users = await graph.Users.GetAsync(req =>
        {
            req.QueryParameters.Filter = "userType eq 'Guest'";
            req.QueryParameters.Select = new[] { "id", "displayName", "mail", "userPrincipalName", "externalUserState" };
        });

        // Fetch all app role assignments for this service principal
        var assignments = await graph.ServicePrincipals[_spId].AppRoleAssignedTo.GetAsync();

        // Fetch app roles once to resolve IDs → names
        var sp = await graph.ServicePrincipals[_spId].GetAsync();
        var roleMap = sp.AppRoles.ToDictionary(r => r.Id!.Value, r => r.Value);

        // Join users with their assignments
        var assignmentLookup = assignments.Value
            .GroupBy(a => a.PrincipalId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return users.Value.Select(u =>
        {
            var hasAssignment = assignmentLookup.TryGetValue(Guid.Parse(u.Id), out var assignment);
            return new GuestUser
            {
                Id           = u.Id,
                DisplayName  = u.DisplayName,
                Email        = u.Mail ?? u.UserPrincipalName,
                Role         = hasAssignment && roleMap.TryGetValue(assignment.AppRoleId!.Value, out var roleName)
                                   ? roleName
                                   : null,
                InviteStatus = u.ExternalUserState  // "Accepted" or "PendingAcceptance"
            };
        });
    }

    public async Task InviteUserAsync(string email, string role)
    {
        var invitation = await graph.Invitations.PostAsync(new Invitation
        {
            InvitedUserEmailAddress = email,
            InviteRedirectUrl = config["App:BaseUrl"],
            SendInvitationMessage = true
        });

        await graph.ServicePrincipals[_spId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(invitation.InvitedUser.Id),
            ResourceId  = Guid.Parse(_spId),
            AppRoleId   = await GetRoleIdAsync(role)
        });
    }

    public async Task RevokeAccessAsync(string userId)
    {
        var assignments = await graph.ServicePrincipals[_spId].AppRoleAssignedTo
            .GetAsync(req => req.QueryParameters.Filter = $"principalId eq '{userId}'");

        foreach (var a in assignments.Value)
            await graph.ServicePrincipals[_spId].AppRoleAssignedTo[a.Id].DeleteAsync();
    }

    public async Task UpdateRoleAsync(string userId, string newRole)
    {
        // Revoke all current role assignments first, then assign the new role
        await RevokeAccessAsync(userId);

        await graph.ServicePrincipals[_spId].AppRoleAssignedTo.PostAsync(new AppRoleAssignment
        {
            PrincipalId = Guid.Parse(userId),
            ResourceId  = Guid.Parse(_spId),
            AppRoleId   = await GetRoleIdAsync(newRole)
        });
    }
}
```

#### 2d — API Endpoints

```csharp
[ApiController]
[Route("api/invitations")]
[Authorize(Policy = "AdminOnly")]
public class InvitationsApiController(IInvitationService svc) : ControllerBase
{
    // List all current B2B guests and their assigned role
    [HttpGet("guests")]
    public async Task<IActionResult> GetGuests() =>
        Ok(await svc.GetGuestsAsync());

    // Invite a new user and assign a role
    [HttpPost]
    public async Task<IActionResult> Invite(InviteUserRequest request)
    {
        await svc.InviteUserAsync(request.Email, request.Role);
        return Ok(new { message = $"Invitation sent to {request.Email}" });
    }

    // Change a user's role
    [HttpPut("{userId}/role")]
    public async Task<IActionResult> UpdateRole(string userId, [FromBody] UpdateRoleRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        await svc.UpdateRoleAsync(userId, request.Role);
        return Ok();
    }

    // Revoke all access (removes app role assignments; guest account stays in tenant)
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Revoke(string userId)
    {
        await svc.RevokeAccessAsync(userId);
        return Ok();
    }
}
```

#### 2e — Admin UI (`Views/Invitations/Index.cshtml`)

The page has two sections:

**1. Current Guests table**

| Column | Description |
|---|---|
| Display Name | User's name from Graph |
| Email | UPN / invited email |
| Role | Badge: `POC.Admin` (red) / `POC.Viewer` (blue) |
| Invite Status | `Accepted` / `Pending` |
| Actions | Change Role dropdown + Revoke button |

**2. Invite New User form**

```
┌──────────────────────────────────────────────┐
│  Invite New User                             │
│                                              │
│  Email:  [______________________________]    │
│  Role:   [POC.Viewer ▼]                      │
│                                              │
│                        [Send Invitation]     │
└──────────────────────────────────────────────┘
```

- Form submits to `POST /api/invitations` via JavaScript (fetch).
- On success: row is added to the guests table with status `Pending`.
- On error: inline error message shown (e.g. user already invited, invalid email).

**UX states to handle:**

| State | UI Behaviour |
|---|---|
| Invite sent | Green toast: "Invitation sent to user@domain.nl" |
| Already a guest | Warning: "User is already a guest in this tenant" |
| Role changed | Inline badge updates without page reload |
| Access revoked | Row removed from table with confirmation modal |
| Graph API error | Red alert with error message |

#### 2f — NuGet Packages Required

```bash
# Always install the latest stable — omit a version pin so NuGet resolves it.
dotnet add package Microsoft.Graph          # v5.x (Kiota-based SDK)
dotnet add package Azure.Identity           # v1.13+ — DefaultAzureCredential (Managed Identity)
```

Register in `Program.cs`:
```csharp
builder.Services.AddScoped<IInvitationService, InvitationService>();

// Local dev: use client credentials (clientId + clientSecret from user-secrets).
// Production: use Managed Identity (DefaultAzureCredential picks it up automatically).
TokenCredential graphCredential = builder.Environment.IsDevelopment()
    ? new ClientSecretCredential(
        builder.Configuration["AzureAd:TenantId"],
        builder.Configuration["AzureAd:ClientId"],
        builder.Configuration["AzureAd:ClientSecret"])
    : new DefaultAzureCredential();

builder.Services.AddSingleton<GraphServiceClient>(
    new GraphServiceClient(graphCredential));
```

#### 2g — POC Management Admin Page

A second admin section (also restricted to `POC.Admin`), sitting **next to** the user-management page in the admin nav. From here an admin can add, edit and delete POC entries and manage who can view each one.

**Routes**

| Page | URL | Auth |
|---|---|---|
| Manage POCs | `/admin/pocs` | `POC.Admin` |
| Manage Users | `/admin/invitations` | `POC.Admin` |

**Layout (`Views/PocsAdmin/Index.cshtml`)**

Two stacked sections:

1. **Existing POCs table** — Name · URL · Short description · # users with access · Actions (Edit · Manage access · Delete).
2. **Add / edit POC form** — opens inline or in a modal:

```
┌───────────────────────────────────────────────────────────┐
│  Add / edit POC                                           │
│                                                           │
│  Name:         [______________________________]           │
│  URL:          [______________________________]           │
│  Description:  [                              ]           │
│                [                              ]           │
│                [                              ]           │
│                [ ✨ Generate with AI ]   (optional)        │
│                                                           │
│  Allowed users (Viewers):                                 │
│   [+ Add user ▼]   (typeahead of B2B guests)              │
│   • alice@inholland.nl        [x]                         │
│   • bob@inholland.nl          [x]                         │
│                                                           │
│                       [Cancel]    [Save]                  │
└───────────────────────────────────────────────────────────┘
```

- The **Allowed users** typeahead is populated from `GET /api/invitations/guests` (same source as the user-management page) so admins can only grant access to users who already exist in the tenant.
- The **✨ Generate with AI** button calls `POST /api/pocs/generate-description` (see Phase 8) and fills the description textarea with the response. The admin can edit the result before saving.
- Admins always see every POC; the "Allowed users" list is irrelevant to them at view time but is what gates `POC.Viewer` access.

**API additions (`Api/PocsController.cs`)**

| Verb | Route | Body / Result |
|---|---|---|
| `GET`    | `/api/pocs`                       | All POCs (admin) **or** only those the caller has access to (viewer) |
| `POST`   | `/api/pocs`                       | Create POC — `{ name, url, description, allowedUserIds[] }` |
| `PUT`    | `/api/pocs/{id}`                  | Update POC fields |
| `DELETE` | `/api/pocs/{id}`                  | Delete POC |
| `GET`    | `/api/pocs/{id}/access`           | List of users with access to this POC |
| `PUT`    | `/api/pocs/{id}/access`           | Replace access list — `{ allowedUserIds[] }` |
| `POST`   | `/api/pocs/generate-description`  | LLM description — see Phase 8 |

**Model — `Models/PocEntry.cs`**

```csharp
public class PocEntry
{
    public Guid   Id          { get; set; } = Guid.NewGuid();
    public string Name        { get; set; } = "";
    public string Url         { get; set; } = "";
    public string Description { get; set; } = "";

    /// Entra ID object IDs (`oid` claim) of users who may view this POC.
    /// Empty list = no Viewers can see it (admins still see everything).
    public List<string> AllowedUserIds { get; set; } = new();
}
```

### Phase 3 — ASP.NET Core MVC Project Setup

```bash
# Use the .NET 10 SDK (verify with `dotnet --version` → 10.x).
dotnet new mvc -n PocLandingPage.Web --auth SingleOrg --framework net10.0
dotnet add package Microsoft.Identity.Web              # v3.x
dotnet add package Microsoft.Identity.Web.UI           # v3.x
dotnet add package Microsoft.ApplicationInsights.AspNetCore
dotnet add package Serilog.AspNetCore                  # v8.x
dotnet add package Serilog.Sinks.ApplicationInsights
```

> **Version policy:** install all packages without a pinned version so NuGet picks the latest stable. After scaffolding, run `dotnet list package --outdated` periodically and run `dotnet outdated -u` (or use Dependabot/Renovate) to keep dependencies current. CI should fail on known critical CVEs via `dotnet list package --vulnerable`.

#### Local Development Setup

Developers must configure secrets locally — **never commit credentials**:

```bash
dotnet user-secrets init
dotnet user-secrets set "AzureAd:ClientSecret" "<dev-client-secret>"
dotnet user-secrets set "AzureAd:ServicePrincipalId" "<enterprise-app-object-id>"
```

> Role IDs are resolved at runtime from the service principal — no GUIDs in config needed.

> Use a **separate dev App Registration** (or the same one with `https://localhost:7xxx/signin-oidc` redirect URI) so local testing never touches production config.

**`Program.cs`:**
```csharp
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ViewerOrAdmin", policy =>
        policy.RequireRole("POC.Viewer", "POC.Admin"));
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole("POC.Admin"));
});

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

// Logging: Serilog → Application Insights
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .WriteTo.ApplicationInsights(
           ctx.Configuration["ApplicationInsights:ConnectionString"],
           TelemetryConverter.Traces));

builder.Services.AddApplicationInsightsTelemetry();
```

**`appsettings.json`:**
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<TEST-TENANT-ID>",
    "ClientId": "<APP-CLIENT-ID>",
    "ClientSecret": "** loaded from Key Vault **",
    "CallbackPath": "/signin-oidc"
  }
}
```

### Phase 4 — Controllers & API

**`HomeController.cs`:**
```csharp
[Authorize(Policy = "ViewerOrAdmin")]
public class HomeController : Controller
{
    private readonly IPocService _pocService;
    public HomeController(IPocService pocService) => _pocService = pocService;

    public async Task<IActionResult> Index()
    {
        var oid     = User.FindFirst("oid")?.Value;
        var isAdmin = User.IsInRole("POC.Admin");
        // Admins see all; viewers only see POCs whose AllowedUserIds contains their oid.
        var pocs = isAdmin
            ? await _pocService.GetAllAsync()
            : await _pocService.GetVisibleForUserAsync(oid!);
        return View(pocs);
    }
}
```

**`Api/PocsController.cs`:**
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "ViewerOrAdmin")]
public class PocsController : ControllerBase
{
    // Admins → all entries. Viewers → only entries whose AllowedUserIds contains their oid.
    [HttpGet] public async Task<IActionResult> GetAll() { ... }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Create(PocEntry entry) { ... }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Update(Guid id, PocEntry entry) { ... }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(Guid id) { ... }

    [HttpPut("{id}/access")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> SetAccess(Guid id, [FromBody] List<string> allowedUserIds) { ... }

    [HttpPost("generate-description")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> GenerateDescription(
        GenerateDescriptionRequest req,
        [FromServices] IDescriptionGenerator gen) =>
        Ok(new { description = await gen.GenerateAsync(req.Name, req.Url, req.Keywords) });
}
```

### Phase 5 — Data Storage

The POC list is stored as a **single JSON file in Azure Blob Storage**.

- SDK: `Azure.Storage.Blobs`
- Container: `poc-data` (private, accessed via Managed Identity)
- File: `pocs.json` — array of `PocEntry` objects
- On write: read → modify → upload with ETag check to prevent race conditions
- Suitable for the expected dataset size (~10–30 entries)

**ETag concurrency implementation sketch (`PocService.cs`):**

```csharp
public async Task<List<PocEntry>> GetAllAsync()
{
    var client = _containerClient.GetBlobClient("pocs.json");
    // GAP 2 FIX: return empty list on first run before the blob exists.
    if (!await client.ExistsAsync())
        return new List<PocEntry>();

    var download = await client.DownloadContentAsync();
    return JsonSerializer.Deserialize<List<PocEntry>>(download.Value.Content) ?? new();
}

// Used by HomeController for non-admin viewers: only return POCs whose AllowedUserIds contains the caller's oid.
public async Task<List<PocEntry>> GetVisibleForUserAsync(string userOid)
{
    var all = await GetAllAsync();
    return all.Where(p => p.AllowedUserIds.Contains(userOid, StringComparer.OrdinalIgnoreCase)).ToList();
}

public async Task SaveAsync(List<PocEntry> entries)
{
    var client = _containerClient.GetBlobClient("pocs.json");

    var json    = JsonSerializer.Serialize(entries);
    var content = BinaryData.FromString(json);

    if (!await client.ExistsAsync())
    {
        // First-time write — no ETag yet, just upload.
        await client.UploadAsync(content);
        return;
    }

    // Read current ETag
    var download = await client.DownloadContentAsync();
    var etag     = download.Value.Details.ETag;

    // Upload only if ETag matches — throws RequestFailedException (412) on conflict.
    // Caller should catch 412, re-read, re-apply change, and retry.
    await client.UploadAsync(content, new BlobUploadOptions
    {
        Conditions = new BlobRequestConditions { IfMatch = etag }
    });
}
```

### Phase 6 — Layout / UI

- Replace the static HTML with Razor Views.
- `_Layout.cshtml` shows the signed-in user's name + role badge + logout button.
- `Home/Index.cshtml` renders POC cards dynamically from the API/service — viewers only see POCs they have been granted access to.
- `PocsAdmin/Index.cshtml` shows a management table (add/edit/delete + per-POC access list + "Generate description with AI"), visible only to `POC.Admin`.
- `Invitations/Index.cshtml` shows B2B guest management, visible only to `POC.Admin`.
- The layout's admin nav shows two sibling links: **Manage POCs** (`/admin/pocs`) and **Manage Users** (`/admin/invitations`).

### Phase 7 — Deploy to Azure App Service (Test Tenant)

1. Create an **Azure App Service** (Linux, .NET 10) in the Test Tenant subscription.
2. Assign a **Managed Identity** to the App Service.
3. Grant the Managed Identity access to Key Vault (for Client Secret retrieval).
4. Set `AZURE_CLIENT_ID` / Key Vault reference in App Service Configuration.
5. Update the App Registration redirect URIs with the production URL.
6. Update **GitHub Actions** pipeline to build and deploy.

### Phase 8 — Optional: AI-generated POC descriptions

When adding or editing a POC, the admin can click **✨ Generate with AI** to have an LLM draft the description. The result is editable before saving — the LLM is a writing assistant, never the source of truth.

**Backend — `IDescriptionGenerator`**

```csharp
public interface IDescriptionGenerator
{
    Task<string> GenerateAsync(string name, string? url = null, string? keywords = null);
}
```

**Provider: Azure OpenAI** (Test Tenant subscription, same Managed Identity).

- NuGet: `Azure.AI.OpenAI` (v2.x, GA — the rewritten SDK on top of the official `OpenAI` package) plus `Azure.Identity` (already added).
- Config in `appsettings.json`:
  ```json
  "AzureOpenAI": {
    "Endpoint":   "https://<resource>.openai.azure.com/",
    "Deployment": "gpt-4o-mini"
  }
  ```
- Auth: `DefaultAzureCredential` (Managed Identity in prod, dev identity locally) — no API key in code.

**Sketch:**

```csharp
public class DescriptionGenerator(AzureOpenAIClient client, IConfiguration cfg) : IDescriptionGenerator
{
    private readonly string _deployment = cfg["AzureOpenAI:Deployment"]!;

    public async Task<string> GenerateAsync(string name, string? url, string? keywords)
    {
        var chat = client.GetChatClient(_deployment);
        var prompt = $$"""
            Write a concise (2–3 sentence) description for a Proof of Concept project.
            Name: {{name}}
            URL:  {{url ?? "(none)"}}
            Keywords / notes: {{keywords ?? "(none)"}}
            Audience: internal Inholland staff browsing a POC landing page.
            Tone: factual, plain English, no marketing fluff.
            """;
        var response = await chat.CompleteChatAsync(
            new ChatMessage[] { new SystemChatMessage("You write short factual project blurbs."),
                                new UserChatMessage(prompt) });
        return response.Value.Content[0].Text.Trim();
    }
}
```

**Registration (`Program.cs`):**
```csharp
builder.Services.AddSingleton(_ => new AzureOpenAIClient(
    new Uri(builder.Configuration["AzureOpenAI:Endpoint"]!),
    new DefaultAzureCredential()));
builder.Services.AddScoped<IDescriptionGenerator, DescriptionGenerator>();
```

**UX in the admin POC form:**

| State | Behaviour |
|---|---|
| Button clicked, name empty | Button disabled; tooltip "Enter a name first" |
| Generating | Spinner on button; textarea shows placeholder "Generating…" |
| Success | Textarea filled with draft; small "AI draft — please review" hint above |
| Error | Inline red message: "Couldn't generate description, please write one manually" |

**Feature flag:** if `AzureOpenAI:Endpoint` is empty in config, the button is hidden — the rest of the app continues to work without the LLM dependency.

---

## Security Considerations

| Concern | Mitigation |
|---|---|
| Client Secret exposure | Store in Azure Key Vault, reference via App Service config |
| Unauthorized access | All routes require `[Authorize]`; API requires valid Bearer token |
| Real tenant data isolation | App registration is Test Tenant only; no write-back to Real Tenant |
| Token validation | `Microsoft.Identity.Web` validates issuer, audience, and signature automatically |
| Role escalation | Roles assigned manually in Test Tenant Enterprise App — no self-service |
| Per-POC access bypass | `HomeController` / `PocsController.GetAll` always filter by the caller's `oid` claim; admins are the only role that skips the filter |
| LLM prompt injection | `DescriptionGenerator` takes admin-entered name/url/keywords only and discards the response unless saved; output is never executed or rendered as HTML |

---

## Open Questions / Decisions Needed

- [x] ~~Should the POC list be editable via the UI (Admin role) or managed via code/JSON only?~~ → Editable via Admin UI
- [x] ~~Which storage backend for POC entries?~~ → **Blob JSON** (`Azure.Storage.Blobs`)
- [x] ~~Should the app be deployed to Azure Static Web Apps or Azure App Service?~~ → **Azure App Service (.NET 10, Linux)**
- [x] ~~Should the B2B invitation be automated or manual?~~ → **Automated via Microsoft Graph API** (PowerShell script + optional Admin API endpoint)
- [x] ⚠️ ~~**BLOCKER:** Are there specific Real Tenant users/groups that should map to `POC.Admin`?~~ → **Resolved.** Initial `POC.Admin`: **sirilak.pompan@inholland.nl** — assign manually in Test Tenant → Enterprise Applications → `poc-landing-page` → Users and Groups before go-live.

---

## References

- [Microsoft.Identity.Web documentation](https://learn.microsoft.com/en-us/azure/active-directory/develop/microsoft-identity-web)
- [Cross-tenant access overview (B2B)](https://learn.microsoft.com/en-us/azure/active-directory/external-identities/cross-tenant-access-overview)
- [App roles & RBAC in Entra ID](https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-add-app-roles-in-apps)
- [ASP.NET Core role-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles)
