$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- Get valid user IDs with their roles
SELECT TOP 10 
    u.Id,
    u.FirstName,
    u.LastName,
    u.Email,
    u.PhoneNumber
FROM AspNetUsers u
ORDER BY u.Id

-- Also check which users exist as agents
SELECT 
    a.Id,
    a.FirstName,
    a.LastName,
    a.Email
FROM Agents a
LIMIT 5
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet

    $connection.Open()
    $adapter.Fill($dataset) > $null

    Write-Host "Valid User IDs from database:" -ForegroundColor Green
    $dataset.Tables[0] | Format-Table -AutoSize

    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}