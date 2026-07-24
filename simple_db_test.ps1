$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"
-- Test direct SQL connection with simpler query
SELECT COUNT(*) AS ReservationCount FROM Reservations
SELECT 'Success' AS Test
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection("Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;Connect Timeout=5;Encrypt=True;")
    $connection.Open()
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT TOP 2 Id, BuyerId, Name, Email, CreatedAt FROM Reservations ORDER BY CreatedAt DESC"
    $reader = $cmd.ExecuteReader()
    
    Write-Host "Recent Reservations:" -ForegroundColor Green
    while($reader.Read()) {
        Write-Host "  ID: $($reader[0])"
        Write-Host "  BuyerId: $($reader[1])"
        Write-Host "  Name: $($reader[2])"
        Write-Host "  CreatedAt: $($reader[4])"
        Write-Host "  ---"
    }
    
    $reader.Close()
    $connection.Close()
} catch {
    Write-Host "SQL Connection Error: $($_.Exception.Message)" -ForegroundColor Red
}