# Publishes ProjectAPI (auth + projects) and deploys it to the Azure web app (Linux).
# Usage (from this folder):  powershell -ExecutionPolicy Bypass -File .\deploy-api.ps1
# Add -Migrate to apply pending EF migrations to the Azure database first.
# Credentials come from the publish profile; the connection string from appsettings.json.
param(
    [string]$PublishProfile = "$env:USERPROFILE\Downloads\app-gpia1539-project.PublishSettings",
    [switch]$Migrate
)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$api = "src\ProjectAPI\src\Api\ProjectAPI.Api.csproj"
$infra = "src\ProjectAPI\src\Infrastructure\ProjectAPI.Infrastructure.csproj"

if ($Migrate) {
    $conn = (Get-Content -Raw "src\ProjectAPI\src\Api\appsettings.json" | ConvertFrom-Json).ConnectionStrings.SqlPrimary
    dotnet ef database update --project $infra --startup-project $api --connection $conn
    if ($LASTEXITCODE -ne 0) { throw "migration failed" }
}

$out = Join-Path $env:TEMP "gpia-api-publish"
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish $api -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# Zip Deploy (Web Deploy does not work on Linux App Services).
[xml]$xml = Get-Content -Raw -Encoding UTF8 $PublishProfile
$p = $xml.publishData.publishProfile | Where-Object { $_.publishMethod -eq "ZipDeploy" } | Select-Object -First 1
$scm = "https://" + ($p.publishUrl -replace ":443$", "")
$auth = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($p.userName):$($p.userPWD)")) }

$zip = Join-Path $env:TEMP "gpia-api.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
tar -a -c -f $zip -C $out .   # forward-slash paths; Compress-Archive breaks on Linux
if ($LASTEXITCODE -ne 0) { throw "zip failed" }
Write-Host ("Uploading {0:N1} MB to {1}" -f ((Get-Item $zip).Length / 1MB), $scm)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
for ($try = 1; ; $try++) {
    try {
        Invoke-RestMethod -Method Post -Uri "$scm/api/zipdeploy?isAsync=true" -Headers $auth `
            -InFile $zip -ContentType "application/zip" -TimeoutSec 900 | Out-Null
        break
    } catch {
        if ($try -ge 3) { throw }
        Write-Host "Upload failed ($($_.Exception.Message)), retrying..."
        Start-Sleep -Seconds 10
    }
}

for ($i = 0; $i -lt 90; $i++) {
    Start-Sleep -Seconds 8
    try { $d = Invoke-RestMethod -Uri "$scm/api/deployments/latest" -Headers $auth -TimeoutSec 60 } catch { continue }
    if ($d.complete) {
        if ($d.status -eq 4) { Write-Host "Deployment succeeded ($($d.end_time))"; exit 0 }
        throw "Deployment failed: $($d.status_text)"
    }
}
throw "Timed out waiting for the deployment to finish"
