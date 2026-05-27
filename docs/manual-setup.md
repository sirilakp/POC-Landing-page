# Manual Setup — Human-in-the-loop Steps

These are the steps the remote Claude agent **cannot** do for you because they require Azure Portal access, admin consent, or tenant-level decisions. Work through them in order. Each step lists the **value to capture** — you'll paste those into `appsettings.json` / user-secrets / Key Vault later.

> Tenant context: everything happens in the **Test Tenant**. Real Tenant users come in as B2B guests.

---

## Captured values cheat-sheet

Fill these in as you go. The code agent reads them from config — never commit real values.

| Key | Where it comes from | Value |
|---|---|---|
| `AzureAd:TenantId`              | Test Tenant ID                                            | `__________________________________________` |
| `AzureAd:ClientId`              | App Registration → Overview → Application (client) ID    | `__________________________________________` |
| `AzureAd:ClientSecret`          | App Registration → Certificates & secrets                | `__________________________________________` |
| `AzureAd:ServicePrincipalId`    | Enterprise Applications → poc-landing-page → Object ID   | `__________________________________________` |
| `AzureOpenAI:Endpoint`          | Azure OpenAI resource → Keys and Endpoint → Endpoint     | `__________________________________________` |
| `AzureOpenAI:Deployment`        | Azure OpenAI → Deployments → name you chose              | `gpt-4o-mini` (or similar)                   |
| `Storage:ConnectionString` / URL| Storage account → Endpoints (use Managed Identity in prod)| `__________________________________________` |
| `App:BaseUrl` (prod)            | App Service → Overview → Default domain                  | `https://________________.azurewebsites.net` |
| Managed Identity Object ID      | App Service → Identity → Object (principal) ID           | `__________________________________________` |

---

## Phase 1 — App Registration (Test Tenant)

1. **Azure Portal → Entra ID → App registrations → New registration**
   - Name: `poc-landing-page`
   - Supported account types: **Accounts in this organizational directory only**
   - Redirect URI (Web): `https://localhost:7xxx/signin-oidc` (dev) — add the production URL later
   - → Capture **Application (client) ID** and **Directory (tenant) ID**

2. **Authentication blade**
   - Enable **ID tokens (used for implicit and hybrid flows)**
   - Front-channel logout URL: `https://<your-app>/signout-oidc`
   - Save

3. **Manifest → `appRoles`** — paste these two entries (generate fresh GUIDs):
   ```json
   "appRoles": [
     {
       "allowedMemberTypes": ["User"],
       "description": "Can view POCs they have been granted access to",
       "displayName": "POC Viewer",
       "id": "<new-guid-1>",
       "isEnabled": true,
       "value": "POC.Viewer"
     },
     {
       "allowedMemberTypes": ["User"],
       "description": "Can manage POC entries, access lists, and B2B users",
       "displayName": "POC Admin",
       "id": "<new-guid-2>",
       "isEnabled": true,
       "value": "POC.Admin"
     }
   ]
   ```

4. **Certificates & secrets → New client secret**
   - Description: `poc-landing-page-dev`
   - Expiry: 12 months (set a calendar reminder to rotate)
   - → **Copy the secret value immediately** (it disappears after navigating away). Store in Key Vault (Step 7.6 below) and locally via `dotnet user-secrets`.

5. **Enterprise Applications → poc-landing-page → Overview**
   - → Capture the **Object ID** (this is what goes into `AzureAd:ServicePrincipalId`, **NOT** the App Registration's Object ID — different value, common mistake).

---

## Phase 2a — Microsoft Graph permissions

1. **App Registration → API permissions → Add a permission → Microsoft Graph → Application permissions**, add:
   - `User.Invite.All`
   - `User.Read.All`
   - `AppRoleAssignment.ReadWrite.All`
   - `Directory.Read.All`

2. Click **Grant admin consent for <Test Tenant>** and confirm.

3. **Grant the App Service Managed Identity the same permissions** (only after Phase 7.3 creates the MI). Open Cloud Shell / a PowerShell with `Microsoft.Graph` module installed and run:
   ```powershell
   Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All","Application.Read.All"

   $graphSpId  = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id
   $miObjectId = "<MANAGED-IDENTITY-OBJECT-ID>"   # from Phase 7.3

   foreach ($role in @("User.Invite.All","User.Read.All","AppRoleAssignment.ReadWrite.All","Directory.Read.All")) {
       $appRole = (Get-MgServicePrincipal -ServicePrincipalId $graphSpId).AppRoles |
                  Where-Object { $_.Value -eq $role }
       New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $miObjectId -BodyParameter @{
           principalId = $miObjectId
           resourceId  = $graphSpId
           appRoleId   = $appRole.Id
       }
   }
   ```

---

## Phase 7 — Azure infrastructure (Test Tenant subscription)

Create resources in this order. Region: pick one close to NL (e.g. `westeurope`).

1. **Resource Group** — `rg-poc-landing-page`.

2. **Storage Account**
   - Kind: StorageV2, redundancy: LRS (fine for this).
   - Create container `poc-data` (private).

3. **App Service**
   - Plan: Linux, runtime stack **.NET 10**, SKU B1 or higher.
   - After create → **Identity → System assigned → On** → save.
   - → Capture **Object (principal) ID** of the Managed Identity.

4. **Grant Managed Identity → Storage Account: `Storage Blob Data Contributor`** (role assignment on the storage account scope).

5. **Application Insights** (workspace-based) — copy the **Connection String**, paste into App Service config as `ApplicationInsights:ConnectionString`.

6. **Key Vault**
   - Create the vault, RBAC permission model.
   - Grant Managed Identity role **Key Vault Secrets User** on the vault.
   - Add secret `AzureAd--ClientSecret` with the value from Phase 1.4.
   - Add secret `AzureAd--ServicePrincipalId` with the value from Phase 1.5.

7. **Azure OpenAI resource**
   - Create in Test Tenant subscription (region with `gpt-4o-mini` availability, e.g. `swedencentral`).
   - **Deployments → Create new deployment** → model `gpt-4o-mini` → name it `gpt-4o-mini`.
   - Grant Managed Identity role **Cognitive Services OpenAI User** on the OpenAI resource.
   - → Capture **Endpoint URL**.

8. **App Service → Configuration → Application settings**, add (use Key Vault references where noted):
   | Name | Value |
   |---|---|
   | `AzureAd__TenantId`              | `<tenant id>` |
   | `AzureAd__ClientId`              | `<client id>` |
   | `AzureAd__ClientSecret`          | `@Microsoft.KeyVault(SecretUri=<uri to AzureAd--ClientSecret>)` |
   | `AzureAd__ServicePrincipalId`    | `@Microsoft.KeyVault(SecretUri=<uri to AzureAd--ServicePrincipalId>)` |
   | `Storage__BlobEndpoint`          | `https://<account>.blob.core.windows.net` |
   | `Storage__Container`             | `poc-data` |
   | `AzureOpenAI__Endpoint`          | `<openai endpoint>` |
   | `AzureOpenAI__Deployment`        | `gpt-4o-mini` |
   | `App__BaseUrl`                   | `https://<app-service-default-domain>` |
   | `ApplicationInsights__ConnectionString` | `<conn string>` |

9. **App Registration → Authentication → Redirect URIs** — add the production URL:
   - `https://<app-service-default-domain>/signin-oidc`
   - Front-channel logout URL: `https://<app-service-default-domain>/signout-oidc`

---

## Initial role assignment (do **before go-live**)

**Enterprise Applications → poc-landing-page → Users and groups → Add user/group**
- User: `sirilak.pompan@inholland.nl`
- Role: `POC Admin`

Without this you cannot log in to the admin pages once the app is deployed.

---

## Local development setup (do once on your dev machine)

After the remote agent finishes scaffolding the project:

```bash
cd PocLandingPage/PocLandingPage.Web

dotnet user-secrets init
dotnet user-secrets set "AzureAd:TenantId"           "<tenant id>"
dotnet user-secrets set "AzureAd:ClientId"           "<client id>"
dotnet user-secrets set "AzureAd:ClientSecret"       "<client secret from Phase 1.4>"
dotnet user-secrets set "AzureAd:ServicePrincipalId" "<enterprise app object id from Phase 1.5>"
dotnet user-secrets set "AzureOpenAI:Endpoint"       "<openai endpoint>"
dotnet user-secrets set "AzureOpenAI:Deployment"     "gpt-4o-mini"
dotnet user-secrets set "Storage:BlobEndpoint"       "<storage endpoint>"
```

Then `dotnet run` — your browser should redirect to Microsoft login and back to the landing page.

---

## Checklist

Mark each as you complete it.

- [ ] Phase 1.1 — App registration created
- [ ] Phase 1.2 — Authentication blade configured
- [ ] Phase 1.3 — App roles added to manifest
- [ ] Phase 1.4 — Client secret created and stored
- [ ] Phase 1.5 — Enterprise App Object ID captured
- [ ] Phase 2a.1–2 — Graph permissions added + admin consent granted
- [ ] Phase 7.1 — Resource group
- [ ] Phase 7.2 — Storage account + `poc-data` container
- [ ] Phase 7.3 — App Service + Managed Identity
- [ ] Phase 7.4 — MI granted Blob Data Contributor on storage
- [ ] Phase 7.5 — Application Insights created
- [ ] Phase 7.6 — Key Vault + secrets + MI role
- [ ] Phase 7.7 — Azure OpenAI resource + deployment + MI role
- [ ] Phase 2a.3 — MI granted Graph application permissions (PowerShell)
- [ ] Phase 7.8 — App Service configuration set
- [ ] Phase 7.9 — Production redirect URIs added to app registration
- [ ] Initial `POC.Admin` role assigned to sirilak.pompan@inholland.nl
- [ ] Local dev user-secrets set
