$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- Check Sales table
SELECT TOP 3 Id, BuyerFirstName, BuyerEmail FROM Sales ORDER BY Id DESC

-- Check Appointments table  
SELECT TOP 3 Id, Name, Email, CreatedAt FROM Appointments ORDER BY Id DESC

-- Check Projects table count
SELECT COUNT(*) as TotalProjects FROM Projects

-- Check one project
SELECT TOP 1 Id, Name FROM Projects
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    
    $connection.Open()
    $adapter.Fill($dataset) > $null
    
    Write-Host "Recent Sales:" -ForegroundColor Green
    $dataset.Tables[0] | Format-Table -AutoSize
    
    Write-Host "`nRecent Appointments:" -ForegroundColor Yellow
    $dataset.Tables[1] | Format-Table -AutoSize
    
    Write-Host "`nProject Stats:" -ForegroundColor Cyan
    $dataset.Tables[2] | Format-Table -AutoSize
    
    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}