<#
.SYNOPSIS
  Builds, packages, and deploys the POC Landing Page application code to its
  Azure App Service.

.DESCRIPTION
  Companion to provision-azure-infra.ps1. That script creates the Azure
  resources (App Service, Storage, Key Vault, …); this script publishes the
  .NET app and pushes the compiled output to the Web App via zip deploy
  (`az webapp deploy`, the Kudu/Oryx-aware successor to `config-zip`).

  Steps:
    1. Preflight  — verify az/dotnet, az login, and that the Web App exists.
    2. Publish    — `dotnet publish -c Release` into a clean output folder.
    3. Package    — zip the publish output with forward-slash entry paths
                    (required by the Linux-based Kudu deployment engine).
    4. Deploy     — `az webapp deploy --type zip` to the target Web App.
    5. Verify     — optional smoke check of the app's hostname.

  Uses the currently-logged-in `az` context. Run `az login` and
  `az account set --subscription <id>` first if you have more than one
  subscription. The Web App must already exist (run provision-azure-infra.ps1
  first).

.PARAMETER ResourceGroup
  Resource Group that contains the Web App. Required.

.PARAMETER NamePrefix
  Same prefix passed to provision-azure-infra.ps1; the Web App name is
  derived as "<NamePrefix>-app". Mutually exclusive with -AppName.

.PARAMETER AppName
  Explicit Web App name. Use instead of -NamePrefix if you renamed the app.

.PARAMETER WebProjectPath
  Path to the Web .csproj. Default: ../PocLandingPage.Web/PocLandingPage.Web.csproj

.PARAMETER Configuration
  Build configuration. Default: Release.

.PARAMETER OutputPath
  Folder for the publish output. Default: a temp folder under the repo
  (publish-out). Cleaned before each run.

.PARAMETER SkipBuild
  Skip dotnet publish and reuse the existing -OutputPath contents. Useful
  for re-deploying the same build.

.PARAMETER NoVerify
  Skip the post-deploy smoke check.

.EXAMPLE
  ./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -NamePrefix pocland

.EXAMPLE
  ./deploy-app-code.ps1 -ResourceGroup rg-poc-landing-page -AppName pocland-app -SkipBuild
#>
[CmdletBinding(DefaultParameterSetName = "ByPrefix")]
param(
    [Parameter(Mandatory = $true)][string]$ResourceGroup,
    [Parameter(Mandatory = $true, ParameterSetName = "ByPrefix")]
    [ValidatePattern('^[a-z][a-z0-9]{2,11}$')][string]$NamePrefix,
    [Parameter(Mandatory = $true, ParameterSetName = "ByName")][string]$AppName,
    [string]$WebProjectPath = (Join-Path $PSScriptRoot "../PocLandingPage.Web/PocLandingPage.Web.csproj"),
    [string]$Configuration = "Release",
    [string]$OutputPath = (Join-Path $PSScriptRoot "../../publish-out"),
    [switch]$SkipBuild,
    [switch]$NoVerify
)

$ErrorActionPreference = "Stop"

# ---------- helpers ----------
function Write-Step($message) {
    Write-Host ""
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Invoke-Az {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & az @Args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "az $($Args -join ' ') failed with exit $LASTEXITCODE"
    }
}

function Get-AzJson {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    $output = & az @Args --output json 2>&1
    if ($LASTEXITCODE -ne 0) { throw "az $($Args -join ' ') failed: $output" }
    if (-not $output) { return $null }
    return $output | ConvertFrom-Json
}

function Test-AzExists {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
    & az @Args --output none 2>$null
    return ($LASTEXITCODE -eq 0)
}

# ---------- preflight ----------
Write-Step "Preflight"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    throw "Azure CLI (az) is not installed. Install from https://aka.ms/installazurecli"
}
if (-not $SkipBuild -and -not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI is not installed (required unless -SkipBuild)."
}

$account = Get-AzJson account show
if (-not $account) {
    throw "Not logged in. Run 'az login' first."
}
Write-Host "  Subscription : $($account.name) ($($account.id))"
Write-Host "  Signed in as : $($account.user.name)"

# Resolve the target Web App name
if ($PSCmdlet.ParameterSetName -eq "ByPrefix") {
    $AppName = "$NamePrefix-app"
}
Write-Host "  Web App      : $AppName (resource group: $ResourceGroup)"

if (-not (Test-Path $WebProjectPath)) {
    throw "Web project not found at $WebProjectPath — pass -WebProjectPath."
}
if (-not (Test-AzExists webapp show --name $AppName --resource-group $ResourceGroup)) {
    throw "Web App '$AppName' not found in '$ResourceGroup'. Run provision-azure-infra.ps1 first."
}

# ---------- Publish ----------
if ($SkipBuild) {
    Write-Step "Skipping build (-SkipBuild); reusing $OutputPath"
    if (-not (Test-Path $OutputPath) -or -not (Get-ChildItem -Path $OutputPath -File -Recurse -ErrorAction SilentlyContinue)) {
        throw "-SkipBuild was set but '$OutputPath' is empty. Run once without -SkipBuild first."
    }
} else {
    Write-Step "dotnet publish ($Configuration)"
    if (Test-Path $OutputPath) {
        Remove-Item $OutputPath -Recurse -Force
    }
    & dotnet publish $WebProjectPath -c $Configuration -o $OutputPath
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit $LASTEXITCODE"
    }
    Write-Host "  published to $OutputPath"
}

# ---------- Package ----------
Write-Step "Packaging zip"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$src = (Resolve-Path $OutputPath).Path
$zipPath = Join-Path (Split-Path $OutputPath -Parent) "publish.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

$zip = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    $files = Get-ChildItem -Path $src -Recurse -File
    foreach ($f in $files) {
        # Relative path with forward slashes (Kudu/Linux requires this)
        $rel = $f.FullName.Substring($src.Length + 1) -replace '\\', '/'
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip, $f.FullName, $rel, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    Write-Host "  zipped $($files.Count) files -> $zipPath"
} finally {
    $zip.Dispose()
}

# ---------- Deploy ----------
Write-Step "Deploying to Web App: $AppName"
Invoke-Az webapp deploy `
    --name $AppName `
    --resource-group $ResourceGroup `
    --src-path $zipPath `
    --type zip `
    --async false | Out-Null
Write-Host "  deployed"

# ---------- Verify ----------
$appHostname = (Get-AzJson webapp show --name $AppName --resource-group $ResourceGroup).defaultHostName
$appUrl = "https://$appHostname"

if (-not $NoVerify) {
    Write-Step "Smoke check: $appUrl"
    Start-Sleep -Seconds 10  # give the site a moment to warm up after deploy
    try {
        $resp = Invoke-WebRequest -Uri $appUrl -Method Head -TimeoutSec 60 -UseBasicParsing
        Write-Host "  HTTP $([int]$resp.StatusCode) $($resp.StatusDescription)" -ForegroundColor Green
    } catch {
        # A 302 to the Entra ID sign-in page is expected and healthy.
        $status = $_.Exception.Response.StatusCode.value__
        if ($status) {
            Write-Host "  HTTP $status (a redirect to sign-in is expected for a protected app)" -ForegroundColor Green
        } else {
            Write-Warning "  Smoke check could not reach the app yet: $($_.Exception.Message)"
            Write-Warning "  It may still be starting. Browse to $appUrl manually in a minute."
        }
    }
}

Write-Host ""
Write-Host "✓ Deployment complete." -ForegroundColor Green
Write-Host "  $appUrl" -ForegroundColor Yellow
