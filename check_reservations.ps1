$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;Encrypt=True;TrustServerCertificate=False;"

$query = @"
SELECT TOP 5 r.Id, r.BuyerId, r.Name, r.LastName, r.Email, r.CreatedAt FROM Reservations r ORDER BY r.CreatedAt DESC
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($command)
    $dataset = New-Object System.Data.DataSet
    $connection.Open()
    $adapter.Fill($dataset) > $null

    Write-Host "Recent Reservations:" -ForegroundColor Green
    $dataset.Tables[0] | Format-Table -AutoSize

    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}