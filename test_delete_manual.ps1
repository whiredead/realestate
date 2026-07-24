# Test DELETE with detailed logging
\$firstId = "5a87ada4-0533-4618-beaf-f9164e0974b4"

Write-Host "Testing with Project ID: \$firstId" -ForegroundColor Cyan
Write-Host "Type: [\$firstId.GetType().Name]"

# Test with invalid GUID (should fail validation)
Write-Host "`n=== Test 1: Invalid GUID (0000...) ===" -ForegroundColor Yellow
try {
    \$resp = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects/00000000-0000-0000-0000-000000000000" -Method Delete
    Write-Host "Response: " \$resp.Message
} catch {
    Write-Host "Validation error (expected): " \$_.Exception.Response.StatusCode.value__
}

# Test with valid GUID (non-existent)
Write-Host "`n=== Test 2: Valid GUID (non-existent) ===" -ForegroundColor Yellow
try {
    \$testId = "12345678-1234-1234-1234-123456789abc"
    \$resp = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects/\$testId" -Method Delete
    Write-Host "Response: " \$resp.Message
} catch {
    Write-Host "Error: " \$_.Exception.Message
    \$err = \$_.Exception.Response.GetResponseStream()
    \$rdr = New-Object System.IO.StreamReader(\$err)
    echo \$rdr.ReadToEnd()
}

# Test with real project ID
Write-Host "`n=== Test 3: Real Project ID ===" -ForegroundColor Yellow
Write-Host "Are you sure? This will delete 'Porject Exemple'"
Write-Host "Press ENTER to continue..."
\$null = \$Host.UI.RawUI.ReadKey('NoEcho')

try {
    Write-Host "Calling DELETE..."
    \$resp = Invoke-RestMethod -Uri "https://gpia-projects.azurewebsites.net/api/Projects/\$firstId" -Method Delete
    Write-Host "Success: " \$resp.Success
    Write-Host "Message: " \$resp.Message
} catch {
    Write-Host "Error Status: " \$_.Exception.Response.StatusCode.value__
    \$err = \$_.Exception.Response.GetResponseStream()
    \$rdr = New-Object System.IO.StreamReader(\$err)
    echo \$rdr.ReadToEnd()
}