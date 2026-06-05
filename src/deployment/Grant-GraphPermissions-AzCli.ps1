#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Grants the App Service Managed Identity the Microsoft Graph application
  permissions required by InvitationService — using az rest (no PS module needed).

.PARAMETER MIObjectId
  Object ID of the App Service Managed Identity (printed by deploy.ps1).

.EXAMPLE
  ./Grant-GraphPermissions-AzCli.ps1 -MIObjectId 00000000-0000-0000-0000-000000000000
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$MIObjectId
)

$ErrorActionPreference = "Stop"

Write-Host "==> Getting access token for Microsoft Graph..." -ForegroundColor Cyan
$tokenJson = & az account get-access-token --resource "https://graph.microsoft.com" --output json
if ($LASTEXITCODE -ne 0) { throw "Failed to get Graph access token" }
$graphToken = ($tokenJson | ConvertFrom-Json).accessToken
$headers = @{
    "Authorization" = "Bearer $graphToken"
    "Content-Type"  = "application/json"
}

Write-Host "==> Looking up the Microsoft Graph service principal..." -ForegroundColor Cyan
$resp = Invoke-RestMethod -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=appId eq '00000003-0000-0000-c000-000000000000'&`$select=id,appRoles" `
    -Headers $headers
$graphSp   = $resp.value[0]
$graphSpId = $graphSp.id
Write-Host "  Graph SP id: $graphSpId"

$requiredRoles = @(
    "User.Invite.All",
    "User.Read.All",
    "AppRoleAssignment.ReadWrite.All",
    "Directory.Read.All"
)

Write-Host ""
Write-Host "==> Getting existing app role assignments for MI $MIObjectId..." -ForegroundColor Cyan
$existing = (Invoke-RestMethod -Method GET `
    -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$MIObjectId/appRoleAssignments" `
    -Headers $headers).value

foreach ($roleName in $requiredRoles) {
    $appRole = $graphSp.appRoles | Where-Object { $_.value -eq $roleName }
    if (-not $appRole) {
        Write-Warning "  Graph role '$roleName' not found — skipping"
        continue
    }

    $already = $existing | Where-Object { $_.appRoleId -eq $appRole.id -and $_.resourceId -eq $graphSpId }
    if ($already) {
        Write-Host "  $roleName : already granted" -ForegroundColor Yellow
        continue
    }

    $body = @{
        principalId = $MIObjectId
        resourceId  = $graphSpId
        appRoleId   = $appRole.id
    } | ConvertTo-Json -Compress

    Invoke-RestMethod -Method POST `
        -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$MIObjectId/appRoleAssignments" `
        -Headers $headers `
        -Body $body | Out-Null
    Write-Host "  $roleName : granted" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done." -ForegroundColor Green
