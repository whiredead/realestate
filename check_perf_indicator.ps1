$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- Check PerformanceIndicators table columns
SELECT
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.IS_NULLABLE,
    c.CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS c
WHERE c.TABLE_NAME = 'PerformanceIndicators'
ORDER BY c.ORDINAL_POSITION

-- Check existing PerformanceIndicators records
SELECT * FROM PerformanceIndicators

-- Check Appointment agentId type issue
SELECT TOP 1 * FROM Appointments

-- Check User IDs type
SELECT TOP 5 Id, FirstName, LastName FROM AspNetUsers
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
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}