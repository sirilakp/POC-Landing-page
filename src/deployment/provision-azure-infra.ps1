<#
.SYNOPSIS
  Idempotent deployment script for POC Landing Page on Azure.

.DESCRIPTION
  Creates (or updates) every Azure resource the app needs, wires up
  Managed Identity RBAC, and writes settings into:
   - Azure: App Service application settings (Key Vault references)
   - Local: dotnet user-secrets for the Web project

  Uses the currently-logged-in `az` context (DefaultAzureCredential). Run
  `az login` and `az account set --subscription <id>` first if you have
  more than one subscription.

  The script is safe to re-run: every step uses "create if not exists"
  semantics so partial failures can be resumed.

.PARAMETER ResourceGroup
  Resource Group name. Created if missing.

.PARAMETER Location
  Azure region. Default: westeurope.

.PARAMETER NamePrefix
  Used to derive resource names (must be globally-unique-safe — lowercase,
  3-12 chars). Example: "pocland" → storage `poclandst`, app `pocland-app`.

.PARAMETER TenantId
  Test Tenant ID. Required.

.PARAMETER ClientId
  App Registration (client) ID. Required. Create the app registration
  first per docs/manual-setup.md Phase 1, then pass its client ID here.

.PARAMETER ServicePrincipalId
  Enterprise Application Object ID for the same app registration
  (Test Tenant → Enterprise Applications → <app> → Overview → Object ID).
  Required.

.PARAMETER ClientSecret
  Optional — if provided, stored in Key Vault. If omitted you must put
  it in Key Vault yourself (see docs/manual-setup.md Phase 1.4).

.PARAMETER SkipOpenAI
  Skip Azure OpenAI resource creation. Use when your subscription
  doesn't have OpenAI access yet — the app remains functional, the
  ✨ Generate with AI button is hidden.

.PARAMETER Mode
  azure  — provision Azure resources + app settings only
  dev    — only write dotnet user-secrets locally
  both   — both (default)

.PARAMETER WebProjectPath
  Path to the Web .csproj (for `dotnet user-secrets`). Default: ../PocLandingPage.Web/PocLandingPage.Web.csproj

.EXAMPLE
  ./provision-azure-infra.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland `
               -TenantId <tid> -ClientId <cid> -ServicePrincipalId <spid>

.EXAMPLE
  ./provision-azure-infra.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland `
               -TenantId <tid> -ClientId <cid> -ServicePrincipalId <spid> `
               -ClientSecret <secret> -SkipOpenAI
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ResourceGroup,
    [string]$Location = "westeurope",
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z][a-z0-9]{2,11}$')][string]$NamePrefix,
    [Parameter(Mandatory=$true)][string]$TenantId,
    [Parameter(Mandatory=$true)][string]$ClientId,
    [Parameter(Mandatory=$true)][string]$ServicePrincipalId,
    [string]$ClientSecret,
    [switch]$SkipOpenAI,
    [ValidateSet("azure","dev","both")][string]$Mode = "both",
    [string]$WebProjectPath = (Join-Path $PSScriptRoot "../PocLandingPage.Web/PocLandingPage.Web.csproj")
)

$ErrorActionPreference = "Stop"

# ---------- helpers ----------
function Write-Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
    & az @Args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($Args -join ' ') failed with exit $LASTEXITCODE"
    }
}

function Get-AzJson {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
    $output = & az @Args --output json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "az $($Args -join ' ') failed: $output" }
    if (-not $output) { return $null }
    return $output | ConvertFrom-Json
}

function Test-AzExists {
    param([Parameter(ValueFromRemainingArguments=$true)][string[]]$Args)
    & az @Args --output none 2>$null
    return ($LASTEXITCODE -eq 0)
}

# ---------- preflight ----------
Write-Step "Preflight"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) is not installed. Install from https://aka.ms/installazurecli"
}

$account = Get-AzJson account show
if (-not $account) {
    throw "Not logged in. Run 'az login' first."
}
Write-Host "  Subscription : $($account.name) ($($account.id))"
Write-Host "  Signed in as : $($account.user.name)"

if ($Mode -ne "dev" -and -not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "az CLI required for Azure mode"
}

if ($Mode -ne "azure") {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "dotnet CLI required for dev mode"
    }
    if (-not (Test-Path $WebProjectPath)) {
        throw "Web project not found at $WebProjectPath — pass -WebProjectPath."
    }
}

# Derived names
$storageName    = ($NamePrefix + "st").ToLower()
$containerName  = "poc-data"
$planName       = "$NamePrefix-plan"
$appName        = "$NamePrefix-app"
$keyVaultName   = "$NamePrefix-kv"
$appInsightsName = "$NamePrefix-ai"
$openAiName     = "$NamePrefix-aoai"
$openAiDeployment = "gpt-4o-mini"

Write-Host "  Resource names:"
Write-Host "    RG          : $ResourceGroup"
Write-Host "    Storage     : $storageName / $containerName"
Write-Host "    App Service : $appName (plan: $planName)"
Write-Host "    Key Vault   : $keyVaultName"
Write-Host "    App Insights: $appInsightsName"
if (-not $SkipOpenAI) {
    Write-Host "    Azure OpenAI: $openAiName (deployment: $openAiDeployment)"
}

if ($Mode -eq "dev") {
    Write-Step "Skipping Azure provisioning (Mode=dev)"
} else {

# ---------- Resource Group ----------
Write-Step "Resource group: $ResourceGroup"
if (Test-AzExists group show --name $ResourceGroup) {
    Write-Host "  already exists"
} else {
    Invoke-Az group create --name $ResourceGroup --location $Location | Out-Null
    Write-Host "  created"
}

# ---------- Storage Account ----------
Write-Step "Storage account: $storageName"
if (-not (Test-AzExists storage account show --name $storageName --resource-group $ResourceGroup)) {
    Invoke-Az storage account create `
        --name $storageName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku Standard_LRS `
        --kind StorageV2 `
        --allow-blob-public-access false | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}
$storageEndpoint = (Get-AzJson storage account show --name $storageName --resource-group $ResourceGroup).primaryEndpoints.blob

Write-Step "Blob container: $containerName"
$containerExists = Test-AzExists storage container show --name $containerName --account-name $storageName --auth-mode login
if (-not $containerExists) {
    Invoke-Az storage container create --name $containerName --account-name $storageName --auth-mode login --public-access off | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}

# ---------- Application Insights ----------
Write-Step "Application Insights: $appInsightsName"
if (-not (Test-AzExists monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup)) {
    # Ensure the provider is registered (silent if already registered)
    Invoke-Az provider register --namespace Microsoft.Insights --consent-to-permissions | Out-Null
    Invoke-Az monitor app-insights component create `
        --app $appInsightsName `
        --location $Location `
        --resource-group $ResourceGroup `
        --kind web `
        --application-type web | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}
$appInsightsConn = (Get-AzJson monitor app-insights component show --app $appInsightsName --resource-group $ResourceGroup).connectionString

# ---------- Key Vault ----------
Write-Step "Key Vault: $keyVaultName"
if (-not (Test-AzExists keyvault show --name $keyVaultName --resource-group $ResourceGroup)) {
    Invoke-Az keyvault create `
        --name $keyVaultName `
        --resource-group $ResourceGroup `
        --location $Location `
        --enable-rbac-authorization true | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}
$kvUri = (Get-AzJson keyvault show --name $keyVaultName --resource-group $ResourceGroup).properties.vaultUri

# Grant the signed-in user Key Vault Secrets Officer (needed to write secrets below)
Write-Step "Granting current user 'Key Vault Secrets Officer'"
$currentUserId = (Get-AzJson ad signed-in-user show).id
$kvScope = (Get-AzJson keyvault show --name $keyVaultName --resource-group $ResourceGroup).id
Invoke-Az role assignment create --role "Key Vault Secrets Officer" --assignee-object-id $currentUserId --assignee-principal-type User --scope $kvScope 2>$null | Out-Null
Write-Host "  done (may take ~30s to propagate)"
Start-Sleep -Seconds 30

# ---------- App Service Plan + Web App ----------
Write-Step "App Service Plan: $planName"
if (-not (Test-AzExists appservice plan show --name $planName --resource-group $ResourceGroup)) {
    Invoke-Az appservice plan create `
        --name $planName `
        --resource-group $ResourceGroup `
        --location $Location `
        --is-linux `
        --sku B1 | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}

Write-Step "Web App: $appName"
if (-not (Test-AzExists webapp show --name $appName --resource-group $ResourceGroup)) {
    Invoke-Az webapp create `
        --name $appName `
        --resource-group $ResourceGroup `
        --plan $planName `
        --runtime "DOTNETCORE:10.0" | Out-Null
    Write-Host "  created"
} else {
    Write-Host "  exists"
}

# Enable system-assigned managed identity
Write-Step "Enabling system-assigned Managed Identity on Web App"
$mi = Get-AzJson webapp identity assign --name $appName --resource-group $ResourceGroup
$miPrincipalId = $mi.principalId
Write-Host "  MI principalId: $miPrincipalId"

# ---------- Graph permissions for the Managed Identity ----------
Write-Step "Graph permissions for Managed Identity"
$graphTokenJson = & az account get-access-token --resource "https://graph.microsoft.com" --output json 2>&1
if ($LASTEXITCODE -eq 0) {
    $graphToken = ($graphTokenJson | ConvertFrom-Json).accessToken
    $graphHeaders = @{ "Authorization" = "Bearer $graphToken"; "Content-Type" = "application/json" }

    $graphSpResp = Invoke-RestMethod -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/servicePrincipals?`$filter=appId eq '00000003-0000-0000-c000-000000000000'&`$select=id,appRoles" `
        -Headers $graphHeaders
    $graphSp   = $graphSpResp.value[0]
    $graphSpId = $graphSp.id

    $existingAssignments = (Invoke-RestMethod -Method GET `
        -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$miPrincipalId/appRoleAssignments" `
        -Headers $graphHeaders).value

    foreach ($roleName in @("User.Invite.All", "User.Read.All", "AppRoleAssignment.ReadWrite.All", "Directory.Read.All")) {
        $appRole = $graphSp.appRoles | Where-Object { $_.value -eq $roleName }
        if (-not $appRole) { Write-Warning "  Graph role '$roleName' not found — skipping"; continue }
        $already = $existingAssignments | Where-Object { $_.appRoleId -eq $appRole.id -and $_.resourceId -eq $graphSpId }
        if ($already) { Write-Host "  $roleName : already granted" -ForegroundColor Yellow; continue }
        $body = @{ principalId = $miPrincipalId; resourceId = $graphSpId; appRoleId = $appRole.id } | ConvertTo-Json -Compress
        Invoke-RestMethod -Method POST `
            -Uri "https://graph.microsoft.com/v1.0/servicePrincipals/$miPrincipalId/appRoleAssignments" `
            -Headers $graphHeaders -Body $body | Out-Null
        Write-Host "  $roleName : granted" -ForegroundColor Green
    }
} else {
    Write-Warning "  Could not get Graph token — skipping Graph permissions. Run Grant-GraphPermissions-AzCli.ps1 -MIObjectId $miPrincipalId manually."
}

# ---------- Role assignments for the Managed Identity ----------
Write-Step "RBAC: MI → Storage Blob Data Contributor"
$storageScope = (Get-AzJson storage account show --name $storageName --resource-group $ResourceGroup).id
Invoke-Az role assignment create --role "Storage Blob Data Contributor" --assignee-object-id $miPrincipalId --assignee-principal-type ServicePrincipal --scope $storageScope 2>$null | Out-Null
Write-Host "  done"

Write-Step "RBAC: MI → Key Vault Secrets User"
Invoke-Az role assignment create --role "Key Vault Secrets User" --assignee-object-id $miPrincipalId --assignee-principal-type ServicePrincipal --scope $kvScope 2>$null | Out-Null
Write-Host "  done"

# ---------- Azure OpenAI ----------
$openAiEndpoint = ""
if (-not $SkipOpenAI) {
    Write-Step "Azure OpenAI: $openAiName"
    if (-not (Test-AzExists cognitiveservices account show --name $openAiName --resource-group $ResourceGroup)) {
        try {
            Invoke-Az cognitiveservices account create `
                --name $openAiName `
                --resource-group $ResourceGroup `
                --location $Location `
                --kind OpenAI `
                --sku S0 `
                --yes | Out-Null
            Write-Host "  created"
        } catch {
            Write-Warning "  Azure OpenAI creation failed (probably no subscription access). Skipping. Pass -SkipOpenAI next time to suppress this."
            $SkipOpenAI = $true
        }
    } else {
        Write-Host "  exists"
    }

    if (-not $SkipOpenAI) {
        $openAiEndpoint = (Get-AzJson cognitiveservices account show --name $openAiName --resource-group $ResourceGroup).properties.endpoint
        $openAiScope = (Get-AzJson cognitiveservices account show --name $openAiName --resource-group $ResourceGroup).id

        Write-Step "Azure OpenAI deployment: $openAiDeployment"
        if (-not (Test-AzExists cognitiveservices account deployment show --name $openAiName --resource-group $ResourceGroup --deployment-name $openAiDeployment)) {
            Invoke-Az cognitiveservices account deployment create `
                --name $openAiName `
                --resource-group $ResourceGroup `
                --deployment-name $openAiDeployment `
                --model-name $openAiDeployment `
                --model-version "2024-07-18" `
                --model-format OpenAI `
                --sku-name "GlobalStandard" `
                --sku-capacity 10 | Out-Null
            Write-Host "  created"
        } else {
            Write-Host "  exists"
        }

        Write-Step "RBAC: MI → Cognitive Services OpenAI User"
        Invoke-Az role assignment create --role "Cognitive Services OpenAI User" --assignee-object-id $miPrincipalId --assignee-principal-type ServicePrincipal --scope $openAiScope 2>$null | Out-Null
        Write-Host "  done"
    }
}

# ---------- Key Vault secrets ----------
Write-Step "Writing secrets to Key Vault"
function Set-KvSecret($name, $value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return $null }
    Invoke-Az keyvault secret set --vault-name $keyVaultName --name $name --value $value --output none | Out-Null
    return (Get-AzJson keyvault secret show --vault-name $keyVaultName --name $name).id
}

if ($ClientSecret) {
    $clientSecretUri = Set-KvSecret "AzureAd--ClientSecret" $ClientSecret
    Write-Host "  AzureAd--ClientSecret set"
} else {
    Write-Warning "  -ClientSecret not provided. Set it manually: az keyvault secret set --vault-name $keyVaultName --name AzureAd--ClientSecret --value <secret>"
}
$spIdSecretUri = Set-KvSecret "AzureAd--ServicePrincipalId" $ServicePrincipalId
Write-Host "  AzureAd--ServicePrincipalId set"

# ---------- App Service application settings ----------
Write-Step "App Service application settings"
$appHostname = (Get-AzJson webapp show --name $appName --resource-group $ResourceGroup).defaultHostName
$appBaseUrl = "https://$appHostname"

$kvRef = { param($uri) "@Microsoft.KeyVault(SecretUri=$uri)" }

$settings = @(
    "AzureAd__Instance=https://login.microsoftonline.com/",
    "AzureAd__TenantId=$TenantId",
    "AzureAd__ClientId=$ClientId",
    "AzureAd__CallbackPath=/signin-oidc",
    "AzureAd__ServicePrincipalId=$(& $kvRef $spIdSecretUri)",
    "Storage__BlobEndpoint=$storageEndpoint",
    "Storage__Container=$containerName",
    "Storage__BlobName=pocs.json",
    "App__BaseUrl=$appBaseUrl",
    "ApplicationInsights__ConnectionString=$appInsightsConn",
    "ASPNETCORE_ENVIRONMENT=Production"
)
if ($clientSecretUri) {
    $settings += "AzureAd__ClientSecret=$(& $kvRef $clientSecretUri)"
}
if (-not $SkipOpenAI -and $openAiEndpoint) {
    $settings += "AzureOpenAI__Endpoint=$openAiEndpoint"
    $settings += "AzureOpenAI__Deployment=$openAiDeployment"
}

Invoke-Az webapp config appsettings set --name $appName --resource-group $ResourceGroup --settings @settings --output none | Out-Null
Write-Host "  done"

Write-Step "Remember to add the production redirect URI to the App Registration"
Write-Host "  $appBaseUrl/signin-oidc" -ForegroundColor Yellow
Write-Host "  Front-channel logout: $appBaseUrl/signout-oidc" -ForegroundColor Yellow
Write-Host "  Run: az ad app update --id $ClientId --web-redirect-uris '$appBaseUrl/signin-oidc'"

} # end if Mode -ne dev

# ---------- Local dev: user-secrets ----------
if ($Mode -ne "azure") {
    Write-Step "Writing dotnet user-secrets for local dev"
    Push-Location (Split-Path $WebProjectPath -Parent)
    try {
        if (-not (Test-Path "Properties")) { New-Item -ItemType Directory -Path "Properties" | Out-Null }
        & dotnet user-secrets init --project $WebProjectPath | Out-Null
        & dotnet user-secrets set "AzureAd:TenantId" $TenantId --project $WebProjectPath | Out-Null
        & dotnet user-secrets set "AzureAd:ClientId" $ClientId --project $WebProjectPath | Out-Null
        & dotnet user-secrets set "AzureAd:ServicePrincipalId" $ServicePrincipalId --project $WebProjectPath | Out-Null
        if ($ClientSecret) {
            & dotnet user-secrets set "AzureAd:ClientSecret" $ClientSecret --project $WebProjectPath | Out-Null
        }
        if ($Mode -eq "both" -and -not [string]::IsNullOrWhiteSpace($storageEndpoint)) {
            & dotnet user-secrets set "Storage:BlobEndpoint" $storageEndpoint --project $WebProjectPath | Out-Null
        }
        if ($Mode -eq "both" -and -not $SkipOpenAI -and $openAiEndpoint) {
            & dotnet user-secrets set "AzureOpenAI:Endpoint" $openAiEndpoint --project $WebProjectPath | Out-Null
            & dotnet user-secrets set "AzureOpenAI:Deployment" $openAiDeployment --project $WebProjectPath | Out-Null
        }
        Write-Host "  done"
    } finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "✓ All done." -ForegroundColor Green
Write-Host ""
Write-Host "Manual follow-up still required:" -ForegroundColor Yellow
Write-Host "  1. Add prod redirect URI to App Registration (see message above)."
Write-Host "  2. Assign initial POC.Admin role to sirilak.pompan@inholland.nl in"
Write-Host "     Enterprise Applications → Users and groups."
Write-Host "  (Graph permissions are granted automatically by this script.)"
