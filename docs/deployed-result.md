# Deployment Result — POC Landing Page

**Date:** 2026-05-30  
**Deployed by:** adm_sirilakp@inhazuretest.onmicrosoft.com  
**Target tenant:** inhazuretest.onmicrosoft.com (Test Tenant)  
**Subscription:** Inholland IT Services Test (`d7af05bb-256a-40b1-a113-c12d0f5a61ad`)  
**Branch deployed from:** feat/entra-id-rebuild

---

## Status overview

| Step | Status | Notes |
|---|---|---|
| App Registration created | ✅ Done | |
| App Roles defined | ✅ Done | |
| Client secret created | ✅ Done | Stored in Key Vault |
| Azure resources provisioned | ✅ Done | All resources created |
| Key Vault secrets written | ✅ Done | Both secrets verified |
| App Service settings configured | ✅ Done | KV references applied |
| Redirect URI added to App Registration | ✅ Done | Both localhost + prod |
| Grant Graph permissions to Managed Identity | ❌ Blocked | See blocker below |
| Assign POC.Admin to Sirilak.Pompan@INHOLLAND.nl | ⏳ Pending | Do after Graph permissions |
| Deploy app code to App Service | ⏳ Pending | Do last |

---

## Azure resources created

| Resource | Name | Details |
|---|---|---|
| Resource Group | `rg-poc-landing-page` | westeurope |
| Storage Account | `poclandingst` | Container: `poc-data` |
| Application Insights | `poclanding-ai` | westeurope |
| Key Vault | `poclanding-kv` | RBAC mode |
| App Service Plan | `poclanding-plan` | Linux B1 |
| Web App | `poclanding-app` | .NET 10 Linux |
| Azure OpenAI | `poclanding-aoai` | gpt-4o-mini, GlobalStandard 10K TPM |

**App URL:** https://poclanding-app.azurewebsites.net

---

## App Registration

| Property | Value |
|---|---|
| Name | `poc-landing-page` |
| Client ID (Application ID) | `fdfed2e8-a925-4519-b796-acc434c926a3` |
| Object ID | `57012d96-8ecf-4f0d-bd7b-b5aa95e7a70c` |
| Service Principal Object ID | `a970d4c4-2598-4e89-a074-aff9d4f0033b` |
| Tenant | `inhazuretest.onmicrosoft.com` (`5a559fff-9b46-4b3b-bd5f-9d01bab492b0`) |
| Redirect URIs | `https://localhost:7000/signin-oidc`, `https://poclanding-app.azurewebsites.net/signin-oidc` |
| ID tokens | Enabled |

### App Roles

| Role | Display name | GUID |
|---|---|---|
| `POC.Admin` | POC Admin | `09c3e415-9140-43b5-97ae-263200bd7e16` |
| `POC.Viewer` | POC Viewer | `2255ee13-4dbd-4be3-b059-25190756fc3d` |

### Client Secrets

| Secret value (hint) | Status | Notes |
|---|---|---|
| `[REDACTED-ORPHAN]` | Orphan — delete this | Created by accident during session; not stored anywhere |
| `[REDACTED]` | ✅ Active — keep this | Stored in Key Vault as `AzureAd--ClientSecret` |

**Action needed:** Delete the orphan secret shown in Azure Portal via App Registration → Certificates & secrets.

---

## Managed Identity

| Property | Value |
|---|---|
| Type | System-assigned |
| Principal ID (Object ID) | `0555224d-508c-43d9-a60a-06b265a0a85a` |
| Assigned to | Web App `poclanding-app` |

### RBAC assignments granted to MI

| Role | Scope |
|---|---|
| Storage Blob Data Contributor | `poclandingst` |
| Key Vault Secrets User | `poclanding-kv` |
| Cognitive Services OpenAI User | `poclanding-aoai` |

---

## ❌ Blocker — Cannot grant Graph permissions (no Entra directory role)

### What needs to happen

The App Service Managed Identity (`0555224d-…`) needs 4 Microsoft Graph **application permissions** so the app can invite B2B guests and manage role assignments:

| Permission | App Role ID |
|---|---|
| `User.Invite.All` | `09850681-111b-4a89-9bed-3f2cae46d706` |
| `User.Read.All` | `df021288-bdef-4463-88db-98f22de89214` |
| `AppRoleAssignment.ReadWrite.All` | `06b708a9-e830-4db3-a914-8e69da51d44f` |
| `Directory.Read.All` | `7ab1d382-f21e-4acd-a863-ba3e13f7da61` |

### Why it is blocked

`adm_sirilakp@inhazuretest.onmicrosoft.com` has **no Entra directory role** in the test tenant — only a few group memberships (developers group, subscription owners group). Granting app-role assignments to a service principal requires at minimum the **Application Administrator** or **Privileged Role Administrator** directory role.

This needs to be discussed with the team / whoever manages `inhazuretest.onmicrosoft.com`.

### Resolution options

**Option A — Assign a directory role to `adm_sirilakp` (recommended)**  
Have a Global Administrator assign the **Application Administrator** role to `adm_sirilakp@inhazuretest.onmicrosoft.com`. After that, simply run:
```powershell
cd "C:\Inholland\POCs\POC Landing page\src\deployment"
pwsh -NoProfile -ExecutionPolicy Bypass -File ".\Grant-GraphPermissions-AzCli.ps1" -MIObjectId "0555224d-508c-43d9-a60a-06b265a0a85a"
```

**Option B — Global Admin runs the script directly**  
The script `src/deployment/Grant-GraphPermissions-AzCli.ps1` can be run by anyone with Application Administrator or higher. They just need to be logged in via `az login` first.

**Option C — Manual via Graph Explorer (one-time, no script)**  
Sign in to https://developer.microsoft.com/en-us/graph/graph-explorer as a Global Admin of `inhazuretest.onmicrosoft.com`, then run 4 POST requests:

- URL: `https://graph.microsoft.com/v1.0/servicePrincipals/0555224d-508c-43d9-a60a-06b265a0a85a/appRoleAssignments`
- Method: `POST`
- Content-Type: `application/json`

Body template (repeat for each row in the table above, substituting `appRoleId`):
```json
{
  "principalId": "0555224d-508c-43d9-a60a-06b265a0a85a",
  "resourceId": "c4dc7131-081c-49a7-b2b6-d5206fdad05b",
  "appRoleId": "<app-role-id-from-table>"
}
```

---

## ⏳ Remaining steps (to do after blocker is resolved)

### Step 1 — Grant Graph permissions
See blocker section above. Run `Grant-GraphPermissions-AzCli.ps1` or use Graph Explorer.

### Step 2 — Assign POC.Admin to the initial admin user
The app uses Entra App Roles for access control. The first admin user needs the `POC.Admin` role assigned in the Enterprise Application.

> **Portal path:**  
> Azure Portal → `inhazuretest.onmicrosoft.com` → **Enterprise Applications** → `poc-landing-page` → **Users and groups** → **Add user/group** → Select `Sirilak.Pompan@INHOLLAND.nl` → Role: **POC Admin** → Assign

Note: `Sirilak.Pompan@INHOLLAND.nl` is from the production tenant (`inholland.nl`). They log in as a B2B guest. If they haven't been invited yet, either invite them through the app's Manage Users page once the app is running, or invite manually via Portal → External Identities first.

### Step 3 — Deploy the app code
Once Graph permissions are in place, deploy the application:
```powershell
cd "C:\Inholland\POCs\POC Landing page\src"
dotnet publish PocLandingPage.Web/PocLandingPage.Web.csproj -c Release -o ./publish
az webapp deploy --name "poclanding-app" --resource-group "rg-poc-landing-page" --src-path ./publish --type zip
```

### Step 4 — Clean up orphan client secret
Azure Portal → App Registrations → `poc-landing-page` → Certificates & secrets → delete the orphan secret.

### Step 5 — Local development setup (optional)
To run the app locally, re-run `deploy.ps1` with `-Mode dev` to write all secrets to `dotnet user-secrets`:
```powershell
cd "C:\Inholland\POCs\POC Landing page\src\deployment"
pwsh -NoProfile -ExecutionPolicy Bypass -File ".\deploy.ps1" `
    -ResourceGroup "rg-poc-landing-page" `
    -Location "westeurope" `
    -NamePrefix "poclanding" `
    -TenantId "5a559fff-9b46-4b3b-bd5f-9d01bab492b0" `
    -ClientId "fdfed2e8-a925-4519-b796-acc434c926a3" `
    -ServicePrincipalId "a970d4c4-2598-4e89-a074-aff9d4f0033b" `
  -ClientSecret "<enter-secret-from-portal-or-key-vault>" `
    -Mode "dev"
```

Or set it directly in Visual Studio / dotnet user-secrets (recommended for local only):
```powershell
cd "C:\Inholland\POCs\POC Landing page\src"
dotnet user-secrets set "AzureAd:ClientSecret" "<enter-secret-value>" --project .\PocLandingPage.Web\PocLandingPage.Web.csproj
```

---

## Key Vault secrets

| Secret name | Value / Reference |
|---|---|
| `AzureAd--ClientSecret` | `[REDACTED - stored in Key Vault]` |
| `AzureAd--ServicePrincipalId` | `a970d4c4-2598-4e89-a074-aff9d4f0033b` |

KV URI base: `https://poclanding-kv.vault.azure.net/secrets/`

---

## App Service application settings (configured)

| Setting | Value |
|---|---|
| `AzureAd__Instance` | `https://login.microsoftonline.com/` |
| `AzureAd__TenantId` | `5a559fff-9b46-4b3b-bd5f-9d01bab492b0` |
| `AzureAd__ClientId` | `fdfed2e8-a925-4519-b796-acc434c926a3` |
| `AzureAd__CallbackPath` | `/signin-oidc` |
| `AzureAd__ClientSecret` | `@Microsoft.KeyVault(SecretUri=https://poclanding-kv.vault.azure.net/secrets/AzureAd--ClientSecret)` |
| `AzureAd__ServicePrincipalId` | `@Microsoft.KeyVault(SecretUri=https://poclanding-kv.vault.azure.net/secrets/AzureAd--ServicePrincipalId)` |
| `Storage__BlobEndpoint` | `https://poclandingst.blob.core.windows.net/` |
| `Storage__Container` | `poc-data` |
| `Storage__BlobName` | `pocs.json` |
| `App__BaseUrl` | `https://poclanding-app.azurewebsites.net` |
| `ApplicationInsights__ConnectionString` | `InstrumentationKey=e88138c9-e803-4d56-82ed-1e751b1a7323;...` |
| `AzureOpenAI__Endpoint` | `https://poclanding-aoai-73d97.openai.azure.com/` |
| `AzureOpenAI__Deployment` | `gpt-4o-mini` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
