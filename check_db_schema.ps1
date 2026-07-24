$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- List all tables
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME

-- Check Reservations table columns
SELECT 
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'Reservations'
ORDER BY c.ORDINAL_POSITION

-- Check Appointments table columns
SELECT 
    c.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'Appointments'
ORDER BY c.ORDINAL_POSITION

-- Check Users table columns
SELECT TOP 10 COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'AspNetUsers' OR TABLE_NAME = 'Users'
ORDER BY COLUMN_NAME
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    
    $connection.Open()
    $adapter.Fill($dataset) > $null
    
    Write-Host "Query Results:" -ForegroundColor Green
    foreach ($table in $dataset.Tables) {
        Write-Host "`n--- $($table.TableName) ---" -ForegroundColor Yellow
        $table | Format-Table -AutoSize
    }
    
    $connection.Close()
    Write-Host "`nDatabase schema check completed successfully!" -ForegroundColor Green
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
}