$Query = "SELECT TOP 5 CAST(id AS NVARCHAR(50)) as Id, name_ar as [Name] FROM [GPIA].[dbo].[Projects] WHERE deleted_at IS NULL"
sqlcmd -S localhost -d GPIA -E -Q $Query -W