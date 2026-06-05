# Deployment scripts

Two scripts, run in order:

1. **`provision-azure-infra.ps1`** — creates every Azure resource the app needs
   and writes its config (App Service settings + `dotnet user-secrets`). Run once.
2. **`deploy-app-code.ps1`** — builds the .NET app and pushes the compiled code
   to the App Service. Run on every code change you want to ship.

The scripts are **idempotent**: each step uses "create if not exists"
semantics, so you can re-run after fixing a parameter or a transient
failure.

## Prerequisites

1. **Azure CLI** ≥ 2.55 — https://aka.ms/installazurecli
2. **`az login`** to the Test Tenant; **`az account set --subscription <id>`** if you have several
3. **PowerShell 7+** (the scripts run on Windows, Mac, and Linux via `pwsh`)
4. **App Registration already created** in the Test Tenant. See
   [`docs/manual-setup.md` Phase 1](../../docs/manual-setup.md#phase-1--app-registration-test-tenant).
   You need:
   - **TenantId**
   - **ClientId**
   - **Enterprise Application Object ID** (the `ServicePrincipalId` — *not* the App Registration's Object ID)
   - **ClientSecret** (optional — script can store it in Key Vault if provided)

5. **For `Grant-GraphPermissions.ps1`**: PowerShell module `Microsoft.Graph` (auto-installed on first run).

## Quick start

```powershell
# From repo root
cd src/deployment

# Provision Azure resources + write user-secrets for local dev
./provision-azure-infra.ps1 `
    -ResourceGroup rg-poc-landing-page `
    -Location westeurope `
    -NamePrefix pocland `
    -TenantId  <tenant-id> `
    -ClientId  <client-id> `
    -ServicePrincipalId <enterprise-app-object-id> `
    -ClientSecret <client-secret>

# One-time: grant the Managed Identity its Graph permissions.
# The MIObjectId is printed by provision-azure-infra.ps1 ("MI principalId: ...").
./Grant-GraphPermissions.ps1 -MIObjectId <mi-object-id>

# Build and deploy the application code to the App Service.
# Re-run this whenever you want to ship new code.
./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland
```

## What `provision-azure-infra.ps1` creates

| # | Resource                  | Why |
|---|---------------------------|-----|
| 1 | Resource Group            | Container |
| 2 | Storage Account + `poc-data` container | Backing store for `pocs.json` |
| 3 | Application Insights      | Logs / telemetry |
| 4 | Key Vault                 | Holds `ClientSecret` + `ServicePrincipalId` |
| 5 | App Service Plan (Linux B1) + Web App (.NET 10) | Hosting |
| 6 | System-assigned Managed Identity on the Web App | Auth without secrets |
| 7 | Azure OpenAI account + `gpt-4o-mini` deployment | Powers ✨ Generate with AI (skip with `-SkipOpenAI`) |
| 8 | RBAC: MI → Blob Data Contributor, Key Vault Secrets User, Cognitive Services OpenAI User | Lets the app talk to its own resources |
| 9 | App Service application settings (Key Vault references) | Wires config into the running app |

It also runs `dotnet user-secrets set ...` so the project runs locally with the same identities.

## Parameters

| Parameter             | Required | Notes |
|-----------------------|----------|-------|
| `-ResourceGroup`      | yes      | Created if it doesn't exist |
| `-Location`           | no       | Default `westeurope` |
| `-NamePrefix`         | yes      | 3–12 lowercase chars; used as the base name for every resource |
| `-TenantId`           | yes      | Test Tenant directory id |
| `-ClientId`           | yes      | App Registration (client) id |
| `-ServicePrincipalId` | yes      | Enterprise App Object ID (**not** the App Reg's Object ID) |
| `-ClientSecret`       | no       | Stored in Key Vault if provided |
| `-SkipOpenAI`         | no       | Skip the Azure OpenAI step if your subscription lacks access |
| `-Mode`               | no       | `azure` / `dev` / `both` (default `both`) |
| `-WebProjectPath`     | no       | Override path to the Web `.csproj` |

## What you still have to do by hand

`provision-azure-infra.ps1` cannot do these — they need a human in the Portal:

1. **Add the prod redirect URI** to the App Registration. The script prints the exact URL and an `az ad app update` command you can run instead of clicking.
2. **`Grant-GraphPermissions.ps1`** — see above.
3. **Assign `POC.Admin`** to your initial admin user in
   *Enterprise Applications → poc-landing-page → Users and groups*.

After those three, browse to `https://<your-app>.azurewebsites.net` and sign in.

## Deploying the app code with `deploy-app-code.ps1`

`provision-azure-infra.ps1` creates the empty App Service; `deploy-app-code.ps1`
builds the .NET app and pushes the compiled output to it. Run it after the infra
exists, and again on every code change you want to ship.

```powershell
cd src/deployment
./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland
```

What it does:

| # | Step | Detail |
|---|------|--------|
| 1 | Preflight | Verifies `az`/`dotnet`, `az login`, and that the Web App exists |
| 2 | Publish | `dotnet publish -c Release` into a clean `publish-out/` folder |
| 3 | Package | Zips the output with forward-slash paths (required by Linux Kudu) |
| 4 | Deploy | `az webapp deploy --type zip` to the Web App |
| 5 | Verify | Optional HTTP smoke check of the app's hostname |

### Deploy parameters

| Parameter         | Required | Notes |
|-------------------|----------|-------|
| `-ResourceGroup`  | yes      | Resource group containing the Web App |
| `-NamePrefix`     | yes\*    | Web App name is derived as `<NamePrefix>-app` |
| `-AppName`        | yes\*    | Explicit Web App name — use instead of `-NamePrefix` |
| `-Configuration`  | no       | Build configuration. Default `Release` |
| `-WebProjectPath` | no       | Override path to the Web `.csproj` |
| `-OutputPath`     | no       | Publish output folder. Default `publish-out/`. Cleaned each run |
| `-SkipBuild`      | no       | Reuse the existing publish output (re-deploy the same build) |
| `-NoVerify`       | no       | Skip the post-deploy smoke check |

\* Pass **either** `-NamePrefix` **or** `-AppName`, not both.

Examples:

```powershell
# Standard build + deploy
./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland

# Re-deploy the last build without rebuilding
./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -AppName pocland-app -SkipBuild
```

## Re-running safely

The scripts are designed to be re-run repeatedly. If something fails part-way through, fix the underlying cause and run again — already-created resources are detected and skipped. The only side effect of a re-run is that role assignments are re-attempted (Azure handles "assignment already exists" gracefully).

## Tearing it all down

```powershell
az group delete --name rg-poc-landing-page --yes
```

Note: this does **not** remove the App Registration in Entra ID — delete it from the Portal if you want a clean slate.
