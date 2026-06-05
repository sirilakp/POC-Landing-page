<#
.SYNOPSIS
  Grants the App Service Managed Identity the Microsoft Graph application
  permissions required by InvitationService.

.DESCRIPTION
  This is the one step in provision-azure-infra.ps1 that cannot be done with `az` alone
  — it needs Microsoft.Graph PowerShell because app-role-assignment for
  Managed Identities goes through Graph's /servicePrincipals API.

  Run once after provision-azure-infra.ps1 has created the Web App + MI.

  Idempotent: existing assignments are left alone.

.PARAMETER MIObjectId
  Object ID of the App Service Managed Identity (printed by provision-azure-infra.ps1).

.EXAMPLE
  ./Grant-GraphPermissions.ps1 -MIObjectId 00000000-0000-0000-0000-000000000000
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MIObjectId
)

$ErrorActionPreference = "Stop"

if (-not (Get-Module -ListAvailable -Name Microsoft.Graph.Applications)) {
    Write-Host "Installing Microsoft.Graph PowerShell module..." -ForegroundColor Yellow
    Install-Module Microsoft.Graph -Scope CurrentUser -Force
}

Import-Module Microsoft.Graph.Applications

Write-Host "==> Connecting to Microsoft Graph"
Connect-MgGraph -Scopes "AppRoleAssignment.ReadWrite.All","Application.Read.All" -NoWelcome

$graphSpId = (Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'").Id
$graphSp   = Get-MgServicePrincipal -ServicePrincipalId $graphSpId

$requiredRoles = @(
    "User.Invite.All",
    "User.Read.All",
    "AppRoleAssignment.ReadWrite.All",
    "Directory.Read.All"
)

$existing = Get-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $MIObjectId

foreach ($roleName in $requiredRoles) {
    $appRole = $graphSp.AppRoles | Where-Object { $_.Value -eq $roleName }
    if (-not $appRole) {
        Write-Warning "  Graph role '$roleName' not found — skipping"
        continue
    }
    $already = $existing | Where-Object { $_.AppRoleId -eq $appRole.Id -and $_.ResourceId -eq $graphSpId }
    if ($already) {
        Write-Host "  $roleName : already granted"
        continue
    }
    New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $MIObjectId -BodyParameter @{
        principalId = $MIObjectId
        resourceId  = $graphSpId
        appRoleId   = $appRole.Id
    } | Out-Null
    Write-Host "  $roleName : granted" -ForegroundColor Green
}

Write-Host ""
Write-Host "✓ Done." -ForegroundColor Green
