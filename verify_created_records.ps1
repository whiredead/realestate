$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"
-- Check if Reservation was created
SELECT 'Reservations Count' AS Query, COUNT(*) AS Count FROM Reservations

SELECT TOP 3 * FROM Reservations ORDER BY CreatedAt DESC

SELECT 'Sales Count' AS Query, COUNT(*) AS Count FROM Sales

SELECT TOP 1 * FROM Sales ORDER BY Id DESC

SELECT 'Appointments Count' AS Query, COUNT(*) AS Count FROM Appointments

SELECT TOP 1 * FROM Appointments ORDER BY Id DESC
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $connection.Open()
    $adapter.Fill($dataset) > $null

    Write-Host "SQL Database Verification Results:" -ForegroundColor Green
    Write-Host "`n--- Reservations ---" -ForegroundColor Yellow
    $dataset.Tables[1] | Format-Table -AutoSize
    Write-Host "`n--- Sales ---" -ForegroundColor Yellow
    $dataset.Tables[3] | Format-Table -AutoSize
    Write-Host "`n--- Appointments ---" -ForegroundColor Yellow
    $dataset.Tables[5] | Format-Table -AutoSize

    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}