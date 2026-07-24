$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- Check table schema
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Reservations' ORDER BY ORDINAL_POSITION

-- Check recent records
SELECT TOP 3 * FROM Reservations ORDER BY ReservationDate DESC
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    
    $connection.Open()
    $adapter.Fill($dataset) > $null
    
    Write-Host "Reservations Table Schema:" -ForegroundColor Green
    $dataset.Tables[0] | Format-Table -AutoSize
    
    Write-Host "`nRecent Reservations:" -ForegroundColor Yellow
    $dataset.Tables[1] | Format-Table -AutoSize
    
    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}