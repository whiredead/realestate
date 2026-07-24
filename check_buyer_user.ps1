$connectionString = "Server=gpiarealestates.database.windows.net;Database=GPIA;User Id=dbadmin;password=123***Sss;"

$query = @"

SELECT Id, Discriminator, FirstName, LastName, Email
FROM AspNetUsers
WHERE Id = '641b0614-3ed6-4ed5-8125-d3b3821f6f45'
"@

try {
    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $command = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $connection.Open()
    $reader = $command.ExecuteReader()
    
    Write-Host "User Info:" -ForegroundColor Green
    while($reader.Read()) {
        Write-Host "Id: $($reader[0])" 
        Write-Host "Discriminator: $($reader[1])"
        Write-Host "FirstName: $($reader[2])"
        Write-Host "LastName: $($reader[3])"
        Write-Host "Email: $($reader[4])"
    }
    
    $reader.Close()
    $connection.Close()
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}