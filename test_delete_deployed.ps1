# Get real project IDs from database
$Query = "SELECT TOP 3 CAST(id AS NVARCHAR(50)) as Id, name_ar as [Name] FROM Projects WHERE deleted_at IS NULL ORDER BY created_at DESC"
$Results = sqlcmd -S localhost -d GPIA -E -Q $Query -W -h -1 | Select-Object -Skip 2

Write-Host "Real Project IDs from Database:" -ForegroundColor Cyan
$Results

# List all projects to test GET endpoint
Write-Host "`n=== Testing GET /api/Projects ===" -ForegroundColor Cyan
$projectsResponse = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects" -Method Get
Write-Host "Projects count: $($projectsResponse.Count)"
if ($projectsResponse.Count -gt 0) {
    Write-Host "First project ID: $($projectsResponse[0].Id)"
}

# Test DELETE endpoint with a known invalid ID first (should fail gracefully)
Write-Host "`n=== Testing DELETE /api/Projects/{id} (non-existent ID) ===" -ForegroundColor Cyan
try {
    $deleteResponse = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects/00000000-0000-0000-0000-000000000000" -Method Delete
    Write-Host "Response Success: $($deleteResponse.Success)"
    Write-Host "Response Message: $($deleteResponse.Message)"
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    Write-Host "Status: $($_.Exception.Response.StatusCode)"
}

# Test DELETE with valid project ID if we found one
if ($Results) {
    $firstId = ($Results -split '\s+')[0]
    Write-Host "`n=== Testing DELETE /api/Projects/$firstId (valid ID) ===" -ForegroundColor Yellow
    Write-Host "WARNING: This will PERMANENTLY delete the project!" -ForegroundColor Red
    Write-Host "Press 'Y' to proceed, any other key to skip..."
    $key = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    if ($key.KeyChar -eq 'Y' -or $key.KeyChar -eq 'y') {
        try {
            $deleteResponse = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects/$firstId" -Method Delete
            Write-Host "Response Success: $($deleteResponse.Success)" -ForegroundColor Green
            Write-Host "Response Message: $($deleteResponse.Message)"
            if ($deleteResponse.DeletedEntities) {
                Write-Host "Deleted Entities:"
                $deleteResponse.DeletedEntities | ForEach-Object { Write-Host "  - $_" }
            }
        } catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    } else {
        Write-Host "Skipped DELETE test" -ForegroundColor Yellow
    }
}