# Action Required — Grant Graph Permissions to App Service Managed Identity

**Project:** POC Landing Page  
**Requested by:** Sirilak Pompan  
**Date:** 2026-05-30

---

## Background

We have deployed a new ASP.NET Core web application to the Test Tenant (`inhazuretest.onmicrosoft.com`). The app runs on Azure App Service with a **system-assigned Managed Identity**. It uses **Microsoft Graph** to invite external users (B2B guests) and manage their application role assignments.

For this to work, the Managed Identity needs 4 **application permissions** (app roles) on Microsoft Graph. These can only be granted by someone with the **Application Administrator**, **Privileged Role Administrator**, or **Global Administrator** directory role in `inhazuretest.onmicrosoft.com`.

Our deployment account (`adm_sirilakp@inhazuretest.onmicrosoft.com`) does not have any of these roles and cannot perform this step.

---

## What we need you to do

Grant the following 4 Microsoft Graph permissions to the Managed Identity listed below.

### Managed Identity to grant permissions to

| Property | Value |
|---|---|
| Display name | `poclanding-app` (App Service) |
| Object ID | `0555224d-508c-43d9-a60a-06b265a0a85a` |
| Tenant | `inhazuretest.onmicrosoft.com` |

### Permissions to grant

| Permission name | Type | App Role ID |
|---|---|---|
| `User.Invite.All` | Application | `09850681-111b-4a89-9bed-3f2cae46d706` |
| `User.Read.All` | Application | `df021288-bdef-4463-88db-98f22de89214` |
| `Directory.Read.All` | Application | `7ab1d382-f21e-4acd-a863-ba3e13f7da61` |

---

## Option A — Run our script (easiest, ~1 minute)

We have a ready-made script. You just need to be logged into `az` CLI in the test tenant, then run:

```powershell
# Step 1 — log in to the test tenant
az login --tenant inhazuretest.onmicrosoft.com

# Step 2 — run the script
cd "C:\Inholland\POCs\POC Landing page\src\deployment"
pwsh -NoProfile -ExecutionPolicy Bypass -File ".\Grant-GraphPermissions-AzCli.ps1" -MIObjectId "0555224d-508c-43d9-a60a-06b265a0a85a"
```

The script is idempotent — safe to run multiple times.

---

## Option B — Graph Explorer (no script, browser only)

1. Open https://developer.microsoft.com/en-us/graph/graph-explorer
2. Sign in with a Global Admin account of `inhazuretest.onmicrosoft.com`
3. Make sure the following consent scope is granted to Graph Explorer: `AppRoleAssignment.ReadWrite.All`
4. Run the following **4 POST requests** one by one (change only the `appRoleId` value each time):

**Request details (same for all 4):**
- Method: `POST`
- URL: `https://graph.microsoft.com/v1.0/servicePrincipals/0555224d-508c-43d9-a60a-06b265a0a85a/appRoleAssignments`
- Content-Type: `application/json`

**Request 1 — User.Invite.All**
```json
{
  "principalId": "0555224d-508c-43d9-a60a-06b265a0a85a",
  "resourceId": "c4dc7131-081c-49a7-b2b6-d5206fdad05b",
  "appRoleId": "09850681-111b-4a89-9bed-3f2cae46d706"
}
```

**Request 2 — User.Read.All**
```json
{
  "principalId": "0555224d-508c-43d9-a60a-06b265a0a85a",
  "resourceId": "c4dc7131-081c-49a7-b2b6-d5206fdad05b",
  "appRoleId": "df021288-bdef-4463-88db-98f22de89214"
}
```

**Request 3 — AppRoleAssignment.ReadWrite.All**
```json
{
  "principalId": "0555224d-508c-43d9-a60a-06b265a0a85a",
  "resourceId": "c4dc7131-081c-49a7-b2b6-d5206fdad05b",
  "appRoleId": "06b708a9-e830-4db3-a914-8e69da51d44f"
}
```

**Request 4 — Directory.Read.All**
```json
{
  "principalId": "0555224d-508c-43d9-a60a-06b265a0a85a",
  "resourceId": "c4dc7131-081c-49a7-b2b6-d5206fdad05b",
  "appRoleId": "7ab1d382-f21e-4acd-a863-ba3e13f7da61"
}
```

Each successful POST returns HTTP `201 Created`. If you get `403 Forbidden`, ensure the consent scope for Graph Explorer includes `AppRoleAssignment.ReadWrite.All`.

---

## Option C — Azure Portal (verify after granting)

After granting via either option above, you can verify the result:

1. Azure Portal → `inhazuretest.onmicrosoft.com`
2. **Enterprise Applications** → search for `poclanding-app`
3. **Permissions** → should show 4 granted application permissions for Microsoft Graph

---

## Additional request — Application Administrator role for our account

To avoid needing to escalate for this type of task in the future, we would also like to request that the **Application Administrator** directory role be assigned to:

**`adm_sirilakp@inhazuretest.onmicrosoft.com`** **or one of our DevOps team members**

This role allows managing app registrations and service principal permissions without being a Global Administrator.

---

## Contact

Questions? Reach out to Sirilak Pompan.

---

## Why each permission is needed — risk explanation for the admin

These are **Microsoft Graph API permissions**. They are entirely separate from Azure RBAC (Owner/Contributor/Reader roles on subscriptions and resource groups). Granting these permissions does **not** give the app any access to Azure infrastructure — it cannot touch other App Services, storage accounts, databases, or anything in the Azure portal resource tree. The managed identity's Azure RBAC role was granted separately during deployment.

### 1. `User.Invite.All` — Medium sensitivity, low blast radius

**What it does:** Lets the app send B2B guest invitations to external email addresses, creating a guest account in the tenant.

**Why we need it:** The core feature of this app is inviting external students/partners as guest users. Without this, no invitation can be sent.

**Risk:** The app can create guest accounts in the tenant. It cannot invite itself to other tenants, cannot escalate those guests to admin roles, and cannot modify existing users. Guest accounts have no permissions by default until explicitly assigned.

---

### 2. `User.Read.All` — Low sensitivity, read-only

**What it does:** Lets the app look up any user's profile (name, email, object ID) in the directory.

**Why we need it:** After inviting a guest, the app needs to find that user's object ID to assign them an app role. It also checks whether a guest already exists before re-inviting.

**Risk:** Read-only. The app can read user profiles but cannot change, delete, or impersonate any user. This is one of the safest Graph permissions.

---

### 3. `AppRoleAssignment.ReadWrite.All` — High sensitivity, to be scrutinized

**What it does:** Lets the app grant or revoke app role assignments on service principals in the tenant.

**Why we need it:** After inviting a guest, the app assigns them a role (e.g., "Roosterplanner user") on the app registration. This is how the app controls what the guest is allowed to do inside the application.

**Risk:** This is the most powerful permission in this list. In theory the app could assign roles on *other* apps in the tenant — not just itself. Microsoft's permission model does not allow narrowing this to a single app. Our application code only ever calls this for its own app registration, but the permission itself is tenant-wide. Trust is placed in our application code to not misuse it.

> If the app were ever compromised, this permission could allow an attacker to assign users to roles on other apps in the tenant. This is the one to scrutinize.

---

### 4. `Directory.Read.All` — Low-medium sensitivity, read-only

**What it does:** Lets the app read the full directory — users, groups, service principals, app registrations.

**Why we need it:** To look up the service principal ID of our own app registration at runtime (so we know which resource to assign roles against), and to verify role assignments were applied correctly.

**Risk:** Read-only. Cannot modify anything. The app can enumerate directory objects, which is sensitive from a data-exposure standpoint, but it cannot change or delete anything.

---

### Summary

| Permission | Sensitivity | Can it modify users? | Can it delete anything? | Strictly necessary? |
|---|---|---|---|---|
| `User.Invite.All` | Medium | Creates guests only | No | Yes |
| `User.Read.All` | Low | No | No | Yes |
| `AppRoleAssignment.ReadWrite.All` | **High** | Assigns roles | No | Yes |
| `Directory.Read.All` | Low-Medium | No | No | Yes |

### These permissions do NOT affect Azure RBAC

| | Azure RBAC | Graph App Permissions |
|---|---|---|
| Controls access to | Azure resources (VMs, App Service, Key Vault, etc.) | Azure AD / Entra ID data (users, groups, app roles) |
| Assigned on | Subscriptions / resource groups / resources | Service principals via Enterprise Apps |
| Used by | Humans and managed identities accessing Azure infra | Apps calling the Microsoft Graph API |
| Admin role needed | Owner or User Access Administrator | Application Admin or Global Admin |

Granting these 4 Graph permissions does not change what the app can do in the Azure portal resource tree. The two systems are independent.
