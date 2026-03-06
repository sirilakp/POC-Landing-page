# ============================================================
#  Inholland POC Portal — Azure Static Web Apps Deploy Script
#  Run from the folder containing index.html:
#    cd "c:\Inholland\POCs\POC Landing page"
#    .\deploy.ps1
# ============================================================

# ── CONFIG — edit these if you want different names ─────────
$ResourceGroup  = "rg-poc-portal"
$AppName        = "poc-portal-inholland"
$Location       = "westeurope"
$Sku            = "Free"
# ────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"

function Write-Step($msg)  { Write-Host "`n▶ $msg" -ForegroundColor Cyan }
function Write-Ok($msg)    { Write-Host "  ✓ $msg" -ForegroundColor Green }
function Write-Warn($msg)  { Write-Host "  ⚠ $msg" -ForegroundColor Yellow }
function Write-Fail($msg)  { Write-Host "  ✗ $msg" -ForegroundColor Red; exit 1 }

# ── 1. Check Azure CLI ───────────────────────────────────────
Write-Step "Checking prerequisites..."

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Fail "Azure CLI not found. Install from: https://aka.ms/installazurecliwindows"
}
Write-Ok "Azure CLI found: $(az version --query '\"azure-cli\"' -o tsv)"

# ── 2. Check Node.js / SWA CLI ───────────────────────────────
if (-not (Get-Command node -ErrorAction SilentlyContinue)) {
    Write-Fail "Node.js not found. Install from: https://nodejs.org"
}
Write-Ok "Node.js found: $(node --version)"

if (-not (Get-Command swa -ErrorAction SilentlyContinue)) {
    Write-Warn "SWA CLI not found. Installing globally..."
    npm install -g @azure/static-web-apps-cli
    if ($LASTEXITCODE -ne 0) { Write-Fail "Failed to install SWA CLI." }
    Write-Ok "SWA CLI installed."
} else {
    Write-Ok "SWA CLI found: $(swa --version)"
}

# ── 3. Azure Login ───────────────────────────────────────────
Write-Step "Checking Azure login status..."

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Warn "Not logged in. Opening browser for Azure login..."
    az login
    $account = az account show | ConvertFrom-Json
}
Write-Ok "Logged in as: $($account.user.name)"
Write-Ok "Subscription:  $($account.name) ($($account.id))"

# Ask to confirm or switch subscription
Write-Host ""
$confirm = Read-Host "  Use this subscription? [Y/n]"
if ($confirm -match '^[Nn]') {
    Write-Host ""
    az account list --output table
    Write-Host ""
    $subId = Read-Host "  Enter Subscription ID to use"
    az account set --subscription $subId
    $account = az account show | ConvertFrom-Json
    Write-Ok "Switched to: $($account.name)"
}

# ── 4. Create Resource Group ─────────────────────────────────
Write-Step "Ensuring resource group '$ResourceGroup' exists in '$Location'..."

$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Ok "Resource group already exists."
} else {
    az group create --name $ResourceGroup --location $Location --output none
    Write-Ok "Resource group created."
}

# ── 5. Create Static Web App ─────────────────────────────────
Write-Step "Checking if Static Web App '$AppName' exists..."

$existing = az staticwebapp list --resource-group $ResourceGroup `
    --query "[?name=='$AppName']" -o json | ConvertFrom-Json

if ($existing.Count -gt 0) {
    Write-Ok "Static Web App already exists — will redeploy."
} else {
    Write-Step "Creating Static Web App '$AppName'..."
    az staticwebapp create `
        --name $AppName `
        --resource-group $ResourceGroup `
        --location $Location `
        --sku $Sku `
        --output none
    Write-Ok "Static Web App created."
}

# ── 6. Get Deployment Token ──────────────────────────────────
Write-Step "Retrieving deployment token..."

$token = az staticwebapp secrets list `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query "properties.apiKey" -o tsv

if (-not $token) { Write-Fail "Could not retrieve deployment token." }
Write-Ok "Deployment token retrieved."

# ── 7. Deploy using SWA CLI (reliable path) ─────────────────
Write-Step "Packaging and deploying with SWA CLI..."

$deployDir    = Split-Path -Parent $MyInvocation.MyCommand.Path
$stagingRoot  = Join-Path $env:TEMP "swa-poc-portal"
$stagingDir   = Join-Path $stagingRoot "site"

if (Test-Path $stagingRoot) {
    Remove-Item $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -Path $stagingDir -ItemType Directory -Force | Out-Null

# Copy only files we want to publish (skip deploy script and old zip artifact if present)
$filesToDeploy = Get-ChildItem $deployDir -File |
    Where-Object { $_.Name -notin @("deploy.ps1", "poc-portal-deploy.zip") }

if ($filesToDeploy.Count -eq 0) {
    Write-Fail "No files found to deploy in: $deployDir"
}

Write-Host "  Files to deploy:"
$filesToDeploy | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $stagingDir -Force
    Write-Host "    · $($_.Name)" -ForegroundColor Gray
}

$subscriptionId = az account show --query "id" -o tsv
if (-not $subscriptionId) {
    Write-Fail "Could not resolve Azure subscription ID."
}

# Some environments leak DEPLOYMENT_ACTION=close, which causes SWA uploads to fail.
$previousDeploymentAction = $env:DEPLOYMENT_ACTION
$env:DEPLOYMENT_ACTION = "upload"

Push-Location $stagingRoot
try {
    swa deploy "site" `
        --deployment-token $token `
        --env production `
        --app-name $AppName `
        --resource-group $ResourceGroup `
        --subscription-id $subscriptionId `
        --swa-config-location "site" `
        --no-use-keychain

    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Deployment failed. Check the output above."
    }
    Write-Ok "Deployment completed."
}
catch {
    Write-Fail "Deployment failed: $($_.Exception.Message)"
}
finally {
    Pop-Location

    if ($null -eq $previousDeploymentAction) {
        Remove-Item Env:DEPLOYMENT_ACTION -ErrorAction SilentlyContinue
    }
    else {
        $env:DEPLOYMENT_ACTION = $previousDeploymentAction
    }

    Remove-Item $stagingRoot -Recurse -Force -ErrorAction SilentlyContinue
}

# ── 8. Show URL ──────────────────────────────────────────────
Write-Step "Fetching live URL..."

$url = az staticwebapp show `
    --name $AppName `
    --resource-group $ResourceGroup `
    --query "defaultHostname" -o tsv

Write-Host ""
Write-Host "  ============================================" -ForegroundColor Magenta
Write-Host "  ✓ DEPLOYED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "  URL: https://$url" -ForegroundColor Cyan
Write-Host "  ============================================" -ForegroundColor Magenta
Write-Host ""
Write-Host "  To redeploy after changes, just run:" -ForegroundColor Gray
Write-Host "  .\deploy.ps1" -ForegroundColor White
Write-Host ""
