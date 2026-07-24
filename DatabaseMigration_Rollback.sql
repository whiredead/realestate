-- =====================================================
-- Comprehensive Database Migration Rollback Script
-- Entity Changes for GPIA Real Estate Project
-- =====================================================
-- Rollback Date: 2025-10-22
-- Description: 
-- 1. Remove NotaireId from Reservation table
-- 2. Revert ReservationStatus enum changes (if needed)
-- 3. Recreate Immeuble-TypeBien relationship
-- 4. Remove ProjectTypeBien junction table
-- 5. Migrate data back from Project-TypeBien to Immeuble-TypeBien
-- 6. Remove constraints and indexes
-- =====================================================

BEGIN TRANSACTION;
BEGIN TRY

-- =====================================================
-- 1. RECREATE IMMEUBLETYPEBIEN TABLE AND MIGRATE DATA BACK
-- =====================================================

-- Recreate ImmeubleTypeBiens table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Recreating ImmeubleTypeBiens table...';
    
    CREATE TABLE ImmeubleTypeBiens (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        ImmeubleId UNIQUEIDENTIFIER NULL,
        TypeBienId INT NULL,
        CONSTRAINT PK_ImmeubleTypeBiens PRIMARY KEY (Id)
    );
    
    PRINT 'ImmeubleTypeBiens table recreated successfully.';
END

-- Migrate data back from ProjectTypeBiens to ImmeubleTypeBiens
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Migrating data back from ProjectTypeBiens to ImmeubleTypeBiens...';
    
    -- Insert data back to ImmeubleTypeBiens
    -- Note: This will create ImmeubleTypeBien records for each Immeuble in the Project
    INSERT INTO ImmeubleTypeBiens (ImmeubleId, TypeBienId)
    SELECT DISTINCT 
        i.Id AS ImmeubleId,
        ptb.TypeBienId
    FROM ProjectTypeBiens ptb
    INNER JOIN Projects p ON ptb.ProjectId = p.Id
    INNER JOIN Immeubles i ON p.Id = i.ProjectId
    WHERE ptb.TypeBienId IS NOT NULL
    AND i.Id IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 
        FROM ImmeubleTypeBiens itb 
        WHERE itb.ImmeubleId = i.Id 
        AND itb.TypeBienId = ptb.TypeBienId
    );
    
    DECLARE @MigratedBackCount INT = @@ROWCOUNT;
    PRINT CAST(@MigratedBackCount AS VARCHAR) + ' records migrated back to ImmeubleTypeBiens.';
END

-- =====================================================
-- 2. ADD FOREIGN KEY CONSTRAINTS FOR IMMEUBLETYPEBIEN
-- =====================================================

-- Add foreign key to Immeubles table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_Immeubles_ImmeubleId'
    AND TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Adding foreign key constraint for ImmeubleId...';
    
    ALTER TABLE ImmeubleTypeBiens 
    ADD CONSTRAINT FK_ImmeubleTypeBiens_Immeubles_ImmeubleId 
    FOREIGN KEY (ImmeubleId) REFERENCES Immeubles(Id) 
    ON DELETE CASCADE;
    
    PRINT 'Foreign key constraint for ImmeubleId added successfully.';
END

-- Add foreign key to TypeBiens table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_TypeBiens_TypeBienId'
    AND TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Adding foreign key constraint for TypeBienId...';
    
    ALTER TABLE ImmeubleTypeBiens 
    ADD CONSTRAINT FK_ImmeubleTypeBiens_TypeBiens_TypeBienId 
    FOREIGN KEY (TypeBienId) REFERENCES TypeBiens(Id) 
    ON DELETE CASCADE;
    
    PRINT 'Foreign key constraint for TypeBienId added successfully.';
END

-- =====================================================
-- 3. REMOVE PROJECTTYPEBIEN TABLE AND CONSTRAINTS
-- =====================================================

-- Drop foreign key constraints first
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_Projects_ProjectId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Dropping foreign key constraint FK_ProjectTypeBiens_Projects_ProjectId...';
    ALTER TABLE ProjectTypeBiens DROP CONSTRAINT FK_ProjectTypeBiens_Projects_ProjectId;
END

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_TypeBiens_TypeBienId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Dropping foreign key constraint FK_ProjectTypeBiens_TypeBiens_TypeBienId...';
    ALTER TABLE ProjectTypeBiens DROP CONSTRAINT FK_ProjectTypeBiens_TypeBiens_TypeBienId;
END

-- Drop the table if it exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Dropping ProjectTypeBiens table...';
    DROP TABLE ProjectTypeBiens;
    PRINT 'ProjectTypeBiens table dropped successfully.';
END

-- =====================================================
-- 4. REMOVE NOTAIREID FROM RESERVATIONS TABLE
-- =====================================================

-- Drop foreign key constraint first
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_Reservations_AspNetUsers_NotaireId'
    AND TABLE_NAME = 'Reservations'
)
BEGIN
    PRINT 'Dropping foreign key constraint FK_Reservations_AspNetUsers_NotaireId...';
    ALTER TABLE Reservations DROP CONSTRAINT FK_Reservations_AspNetUsers_NotaireId;
END

-- Drop the index if it exists
IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Reservations_NotaireId' 
    AND object_id = OBJECT_ID('Reservations')
)
BEGIN
    PRINT 'Dropping index IX_Reservations_NotaireId...';
    DROP INDEX IX_Reservations_NotaireId ON Reservations;
END

-- Drop the column if it exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Reservations' 
    AND COLUMN_NAME = 'NotaireId'
)
BEGIN
    PRINT 'Dropping NotaireId column from Reservations table...';
    ALTER TABLE Reservations DROP COLUMN NotaireId;
    PRINT 'NotaireId column dropped successfully.';
END

-- =====================================================
-- 5. CLEAN UP REMAINING INDEXES
-- =====================================================

-- Drop any remaining ProjectTypeBien indexes (in case table wasn't dropped)
IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_ProjectId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Dropping index IX_ProjectTypeBiens_ProjectId...';
    DROP INDEX IX_ProjectTypeBiens_ProjectId ON ProjectTypeBiens;
END

IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_TypeBienId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Dropping index IX_ProjectTypeBiens_TypeBienId...';
    DROP INDEX IX_ProjectTypeBiens_TypeBienId ON ProjectTypeBiens;
END

IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_ProjectId_TypeBienId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Dropping index IX_ProjectTypeBiens_ProjectId_TypeBienId...';
    DROP INDEX IX_ProjectTypeBiens_ProjectId_TypeBienId ON ProjectTypeBiens;
END

-- =====================================================
-- 6. VERIFICATION AND SUMMARY
-- =====================================================

PRINT '=== ROLLBACK SUMMARY ===';
PRINT '1. ImmeubleTypeBiens table recreated';
PRINT '2. Data migrated back from ProjectTypeBiens to ImmeubleTypeBiens';
PRINT '3. Foreign key constraints recreated for ImmeubleTypeBiens';
PRINT '4. ProjectTypeBiens table and constraints removed';
PRINT '5. NotaireId column and related constraints removed from Reservations';
PRINT '6. Performance indexes cleaned up';

-- Verify the rollback
PRINT '=== VERIFICATION ===';

-- Check ImmeubleTypeBiens table exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT '✓ ImmeubleTypeBiens table exists';
    
    DECLARE @ImmeubleTypeBienCount INT;
    SELECT @ImmeubleTypeBienCount = COUNT(*) FROM ImmeubleTypeBiens;
    PRINT '✓ ImmeubleTypeBiens contains ' + CAST(@ImmeubleTypeBienCount AS VARCHAR) + ' records';
END

-- Check ProjectTypeBiens table is gone
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT '✓ ProjectTypeBiens table successfully removed';
END

-- Check NotaireId column is gone from Reservations
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Reservations' 
    AND COLUMN_NAME = 'NotaireId'
)
BEGIN
    PRINT '✓ NotaireId column successfully removed from Reservations table';
END

PRINT '=== ROLLBACK COMPLETED SUCCESSFULLY ===';

COMMIT TRANSACTION;

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '=== ROLLBACK FAILED ===';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR);
    PRINT 'Error Severity: ' + CAST(ERROR_SEVERITY() AS VARCHAR);
    PRINT 'Error State: ' + CAST(ERROR_STATE() AS VARCHAR);
    PRINT 'Error Procedure: ' + ISNULL(ERROR_PROCEDURE(), 'N/A');
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR);
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    
    -- Re-throw the error to the calling application
    THROW;

END CATCH;