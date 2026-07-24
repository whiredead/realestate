$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

-- Check if Assignments table exists or if it's named differently
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Assignment%' OR TABLE_NAME LIKE '%assign%'

-- Also check the Foreign Keys on Reservations to see what it references
SELECT 
    f.name AS ForeignKey,
    OBJECT_NAME(f.parent_object_id) AS TableName,
    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS ColumnName,
    OBJECT_NAME(f.referenced_object_id) AS ReferencedTable,
    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS ReferencedColumn
FROM sys.foreign_keys AS f
INNER JOIN sys.foreign_key_columns AS fc ON f.object_id = fc.constraint_object_id
INNER JOIN sys.tables t ON f.referenced_object_id = t.object_id
WHERE OBJECT_NAME(f.parent_object_id) IN ('Reservations', 'Sales', 'Appointments')
ORDER BY TableName, ForeignKey

-- Check Immeubes navigation properties
SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Immeubles'
ORDER BY ORDINAL_POSITION
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet

    $connection.Open()
    $adapter.Fill($dataset) > $null

    for ($i = 0; $i -lt $dataset.Tables.Count; $i++) {
        Write-Host "`n--- Result Set $($i + 1) ---" -ForegroundColor Yellow
        $dataset.Tables[$i] | Format-Table -AutoSize
    }

    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}