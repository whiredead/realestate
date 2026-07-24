$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

SELECT TOP 3 Id, BuyerId, Name, Email, CreatedAt FROM Reservations ORDER BY CreatedAt DESC

SELECT TOP 1 Id, BuyerFirstName, BuyerEmail, CreatedAt FROM Sales ORDER BY Id DESC

SELECT TOP 1 Id, Name, Email, CreatedAt FROM Appointments ORDER BY Id DESC
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    
    $connection.Open()
    $adapter.Fill($dataset) > $null
    
    Write-Host "Database Verification Results:" -ForegroundColor Green
    
    Write-Host "`n--- Recent Reservations ---" -ForegroundColor Yellow
    $dataset.Tables[0] | Format-Table Id, BuyerId, Name, Email, CreatedAt
    
    Write-Host "`n--- Recent Sales ---" -ForegroundColor Yellow
    $dataset.Tables[1] | Format-Table Id, BuyerFirstName, BuyerEmail, CreatedAt
    
    Write-Host "`n--- Recent Appointments ---" -ForegroundColor Yellow
    $dataset.Tables[2] | Format-Table Id, Name, Email, CreatedAt
    
    $connection.Close()
    Write-Host "`nDatabase verification completed!" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}