#!/usr/bin/env pwsh
<#
.SYNOPSIS
  Completes the App Service application settings that provision-azure-infra.ps1 didn't reach
  because of the gpt-4o-mini deployment failure.
#>
$ErrorActionPreference = "Stop"

$rg              = "rg-poc-landing-page"
$app             = "poclanding-app"
$kv              = "poclanding-kv"
$storageName     = "poclandingst"
$containerName   = "poc-data"
$appInsightsName = "poclanding-ai"
$openAiName      = "poclanding-aoai"
$openAiDeployment = "gpt-4o-mini"
$tenantId        = "5a559fff-9b46-4b3b-bd5f-9d01bab492b0"
$clientId        = "fdfed2e8-a925-4519-b796-acc434c926a3"

Write-Host "==> Collecting resource values..." -ForegroundColor Cyan

$csUri          = (& az keyvault secret show --vault-name $kv --name "AzureAd--ClientSecret" --query "id" -o tsv) -replace '/[^/]+$',''
$spUri          = (& az keyvault secret show --vault-name $kv --name "AzureAd--ServicePrincipalId" --query "id" -o tsv) -replace '/[^/]+$',''
$storageEndpoint = & az storage account show --name $storageName --resource-group $rg --query "primaryEndpoints.blob" -o tsv
$aiConn         = & az monitor app-insights component show --app $appInsightsName --resource-group $rg --query "connectionString" -o tsv
$appHostname    = & az webapp show --name $app --resource-group $rg --query "defaultHostName" -o tsv
$openAiEndpoint = & az cognitiveservices account show --name $openAiName --resource-group $rg --query "properties.endpoint" -o tsv

Write-Host "  Storage endpoint : $storageEndpoint"
Write-Host "  App hostname     : $appHostname"
Write-Host "  OpenAI endpoint  : $openAiEndpoint"
Write-Host "  KV ClientSecret  : $csUri"
Write-Host "  KV SpId          : $spUri"

Write-Host ""
Write-Host "==> Setting App Service application settings..." -ForegroundColor Cyan

# Write settings to a JSON file to avoid CMD/PowerShell quoting issues with
# the @Microsoft.KeyVault(...) syntax (parentheses are special in CMD).
$settingsJson = [ordered]@{
    "AzureAd__Instance"                    = "https://login.microsoftonline.com/"
    "AzureAd__TenantId"                    = $tenantId
    "AzureAd__ClientId"                    = $clientId
    "AzureAd__CallbackPath"                = "/signin-oidc"
    "AzureAd__ClientSecret"                = "@Microsoft.KeyVault(SecretUri=$csUri)"
    "AzureAd__ServicePrincipalId"          = "@Microsoft.KeyVault(SecretUri=$spUri)"
    "Storage__BlobEndpoint"                = $storageEndpoint
    "Storage__Container"                   = $containerName
    "Storage__BlobName"                    = "pocs.json"
    "App__BaseUrl"                         = "https://$appHostname"
    "ApplicationInsights__ConnectionString"= $aiConn
    "AzureOpenAI__Endpoint"                = $openAiEndpoint
    "AzureOpenAI__Deployment"              = $openAiDeployment
    "ASPNETCORE_ENVIRONMENT"               = "Production"
} | ConvertTo-Json

$settingsFile = Join-Path $env:TEMP "poc-appsettings.json"
$settingsJson | Out-File -FilePath $settingsFile -Encoding utf8 -NoNewline

& az webapp config appsettings set --name $app --resource-group $rg --settings "@$settingsFile" --output none
if ($LASTEXITCODE -ne 0) { throw "webapp config appsettings set failed" }
Write-Host "  Done."

Write-Host ""
Write-Host "==> Production redirect URI" -ForegroundColor Cyan
Write-Host "  Add this to the App Registration:" -ForegroundColor Yellow
Write-Host "  https://$appHostname/signin-oidc" -ForegroundColor Yellow
Write-Host ""
Write-Host "  Run:" -ForegroundColor Yellow
Write-Host "  az ad app update --id $clientId --web-redirect-uris 'https://localhost:7000/signin-oidc' 'https://$appHostname/signin-oidc'" -ForegroundColor Yellow
Write-Host ""
Write-Host "==> All done. Remaining manual steps:" -ForegroundColor Green
Write-Host "  1. Run the az ad app update command above (or do it in the Portal)"
Write-Host "  2. Run: pwsh Grant-GraphPermissions.ps1 -MIObjectId 0555224d-508c-43d9-a60a-06b265a0a85a"
Write-Host "  3. Portal -> Enterprise Apps -> poc-landing-page -> Users and groups -> assign POC.Admin to Sirilak.Pompan@INHOLLAND.nl"
