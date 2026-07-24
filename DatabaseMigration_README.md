# Database Migration Scripts for GPIA Real Estate Project

This document provides comprehensive instructions for executing the database migration scripts that implement the entity changes for the GPIA Real Estate project.

## Overview

The migration implements the following changes:

1. **Added NotaireId to Reservation table** - New nullable foreign key column to link reservations with notaries
2. **Added Sold status to ReservationStatus enum** - New enum value (4) for sold reservations
3. **Created ProjectTypeBien junction table** - New many-to-many relationship between Projects and TypeBiens
4. **Updated relationships between Project and TypeBien** - Direct relationship through junction table
5. **Removed Immeuble-TypeBien relationship** - Old junction table removed
6. **Data migration** - Existing relationships migrated from Immeuble-TypeBien to Project-TypeBien
7. **Constraints and indexes** - Foreign keys and performance indexes added

## Files Included

- **`DatabaseMigration_Comprehensive.sql`** - Main migration script
- **`DatabaseMigration_Rollback.sql`** - Rollback script to undo all changes
- **`DatabaseMigration_Validation.sql`** - Validation script to check current state
- **`DatabaseMigration_README.md`** - This documentation file

## Prerequisites

1. **Database Backup**: Always create a full backup of your database before running any migration scripts
2. **Permissions**: Ensure you have sufficient database permissions (ALTER, CREATE, DROP, etc.)
3. **Testing**: Run scripts in a development/staging environment first
4. **Downtime**: Plan for potential downtime during migration execution

## Execution Steps

### Step 1: Pre-Migration Validation

Run the validation script to check the current state of your database:

```sql
-- Execute in SQL Server Management Studio or similar tool
-- File: DatabaseMigration_Validation.sql
```

This will show you:
- Current table structures
- Existing relationships
- Data integrity status
- Migration completion status

### Step 2: Database Backup

Create a full backup of your database:

```sql
BACKUP DATABASE [YourDatabaseName] 
TO DISK = 'C:\Backup\YourDatabaseName_PreMigration.bak'
WITH FORMAT, INIT;
```

### Step 3: Execute Migration Script

Run the main migration script:

```sql
-- Execute in SQL Server Management Studio or similar tool
-- File: DatabaseMigration_Comprehensive.sql
```

The script will:
- Run within a transaction for safety
- Provide detailed progress output
- Roll back automatically if any errors occur
- Show verification results at the end

### Step 4: Post-Migration Validation

Run the validation script again to confirm all changes were applied successfully:

```sql
-- File: DatabaseMigration_Validation.sql
```

### Step 5: Application Testing

- Test the application thoroughly
- Verify all CRUD operations work correctly
- Check that the new relationships function properly
- Validate that existing data is accessible

## Rollback Procedure

If you need to undo the migration for any reason:

1. **Stop the application**
2. **Execute the rollback script**:

```sql
-- File: DatabaseMigration_Rollback.sql
```

3. **Verify rollback** with the validation script
4. **Restart the application**

## Migration Details

### 1. NotaireId Addition

- **Table**: `Reservations`
- **Column**: `NotaireId` (NVARCHAR(450), NULLABLE)
- **Foreign Key**: References `AspNetUsers(Id)` where user is a Notary
- **Index**: `IX_Reservations_NotaireId` for performance

### 2. ReservationStatus Enum

- **New Value**: `Sold = 4`
- **Storage**: Stored as integer in database
- **No structural changes needed** (handled at application level)

### 3. ProjectTypeBien Junction Table

```sql
CREATE TABLE ProjectTypeBiens (
    Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    ProjectId UNIQUEIDENTIFIER NULL,
    TypeBienId INT NULL,
    CONSTRAINT PK_ProjectTypeBiens PRIMARY KEY (Id)
);
```

**Foreign Keys**:
- `FK_ProjectTypeBiens_Projects_ProjectId` → `Projects(Id)` (CASCADE DELETE)
- `FK_ProjectTypeBiens_TypeBiens_TypeBienId` → `TypeBiens(Id)` (CASCADE DELETE)

**Indexes**:
- `IX_ProjectTypeBiens_ProjectId`
- `IX_ProjectTypeBiens_TypeBienId`
- `IX_ProjectTypeBiens_ProjectId_TypeBienId` (UNIQUE filtered)

### 4. ImmeubleTypeBien Removal

- **Table**: `ImmeubleTypeBiens` (completely removed)
- **Data Migration**: Existing relationships migrated to ProjectTypeBien
- **Foreign Keys**: All constraints removed before table drop

### 5. Data Migration Logic

The migration transfers existing Immeuble-TypeBien relationships to Project-TypeBien:

```sql
INSERT INTO ProjectTypeBiens (ProjectId, TypeBienId)
SELECT DISTINCT 
    i.ProjectId, 
    itb.TypeBienId
FROM ImmeubleTypeBiens itb
INNER JOIN Immeubles i ON itb.ImmeubleId = i.Id
WHERE i.ProjectId IS NOT NULL 
AND itb.TypeBienId IS NOT NULL;
```

## Troubleshooting

### Common Issues

1. **Permission Errors**: Ensure your database user has ALTER, CREATE, DROP permissions
2. **Foreign Key Constraints**: Script handles constraint removal before table operations
3. **Data Migration Failures**: Check for orphaned records in source tables
4. **Transaction Timeouts**: For large databases, consider running during off-peak hours

### Error Handling

The migration script includes comprehensive error handling:
- All operations wrapped in a transaction
- Automatic rollback on errors
- Detailed error messages with context
- Progress reporting throughout execution

### Performance Considerations

- **Indexes**: Added for query performance on new relationships
- **Data Migration**: Uses DISTINCT to avoid duplicates
- **Transaction**: Single transaction ensures consistency
- **Batch Processing**: Large datasets processed efficiently

## Validation Checklist

After migration completion, verify:

- [ ] `NotaireId` column exists in `Reservations` table
- [ ] Foreign key constraints are properly created
- [ ] `ProjectTypeBiens` table exists with correct structure
- [ ] `ImmeubleTypeBiens` table is removed
- [ ] Data migration completed successfully
- [ ] Performance indexes are created
- [ ] Application functions correctly
- [ ] No orphaned records exist

## Support

For issues or questions:
1. Check the SQL Server error logs
2. Review the validation script output
3. Verify database permissions
4. Test in development environment first

## Version History

- **v1.0** (2025-10-22): Initial migration implementation
  - Added NotaireId to Reservations
  - Created ProjectTypeBien junction table
  - Removed ImmeubleTypeBien relationship
  - Added comprehensive error handling and rollback support

---

**⚠️ IMPORTANT**: Always test migrations in a non-production environment first and ensure you have recent backups before proceeding with production deployments.