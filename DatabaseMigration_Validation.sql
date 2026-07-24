-- =====================================================
-- Database Migration Validation Script
-- Entity Changes for GPIA Real Estate Project
-- =====================================================
-- Validation Date: 2025-10-22
-- Description: 
-- This script validates the current state of the database
-- and helps identify what changes need to be applied
-- =====================================================

PRINT '=== DATABASE MIGRATION VALIDATION REPORT ===';
PRINT 'Generated on: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '';

-- =====================================================
-- 1. CHECK RESERVATIONS TABLE STRUCTURE
-- =====================================================

PRINT '1. RESERVATIONS TABLE ANALYSIS:';
PRINT '===============================';

-- Check if Reservations table exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'Reservations'
)
BEGIN
    PRINT '✓ Reservations table exists';
    
    -- Check for NotaireId column
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'Reservations' 
        AND COLUMN_NAME = 'NotaireId'
    )
    BEGIN
        PRINT '✓ NotaireId column exists in Reservations table';
        
        -- Check NotaireId column details
        SELECT 
            COLUMN_NAME,
            DATA_TYPE,
            CHARACTER_MAXIMUM_LENGTH,
            IS_NULLABLE
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'Reservations' 
        AND COLUMN_NAME = 'NotaireId';
        
        -- Check for data in NotaireId
        DECLARE @NotaireIdCount INT;
        SELECT @NotaireIdCount = COUNT(*) FROM Reservations WHERE NotaireId IS NOT NULL;
        PRINT '  - Records with NotaireId: ' + CAST(@NotaireIdCount AS VARCHAR);
    END
    ELSE
    BEGIN
        PRINT '✗ NotaireId column does NOT exist in Reservations table';
    END
    
    -- Check current ReservationStatus values
    PRINT '';
    PRINT 'Current ReservationStatus distribution:';
    SELECT 
        Status,
        CASE Status
            WHEN 0 THEN 'Pending'
            WHEN 1 THEN 'Approved'
            WHEN 2 THEN 'Rejected'
            WHEN 3 THEN 'Cancelled'
            WHEN 4 THEN 'Sold'
            ELSE 'Unknown'
        END AS StatusName,
        COUNT(*) AS Count
    FROM Reservations 
    GROUP BY Status 
    ORDER BY Status;
END
ELSE
BEGIN
    PRINT '✗ Reservations table does NOT exist';
END

PRINT '';

-- =====================================================
-- 2. CHECK PROJECTTYPEBIEN TABLE STRUCTURE
-- =====================================================

PRINT '2. PROJECTTYPEBIEN TABLE ANALYSIS:';
PRINT '==================================';

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT '✓ ProjectTypeBiens table exists';
    
    -- Check table structure
    SELECT 
        COLUMN_NAME,
        DATA_TYPE,
        CHARACTER_MAXIMUM_LENGTH,
        IS_NULLABLE
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
    ORDER BY ORDINAL_POSITION;
    
    -- Check record count
    DECLARE @ProjectTypeBienCount INT;
    SELECT @ProjectTypeBienCount = COUNT(*) FROM ProjectTypeBiens;
    PRINT '  - Total records: ' + CAST(@ProjectTypeBienCount AS VARCHAR);
    
    -- Check for foreign key constraints
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_Projects_ProjectId'
        AND TABLE_NAME = 'ProjectTypeBiens'
    )
    BEGIN
        PRINT '✓ Foreign key to Projects exists';
    END
    ELSE
    BEGIN
        PRINT '✗ Foreign key to Projects does NOT exist';
    END
    
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_TypeBiens_TypeBienId'
        AND TABLE_NAME = 'ProjectTypeBiens'
    )
    BEGIN
        PRINT '✓ Foreign key to TypeBiens exists';
    END
    ELSE
    BEGIN
        PRINT '✗ Foreign key to TypeBiens does NOT exist';
    END
END
ELSE
BEGIN
    PRINT '✗ ProjectTypeBiens table does NOT exist';
END

PRINT '';

-- =====================================================
-- 3. CHECK IMMEUBLETYPEBIEN TABLE STRUCTURE
-- =====================================================

PRINT '3. IMMEUBLETYPEBIEN TABLE ANALYSIS:';
PRINT '===================================';

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT '✓ ImmeubleTypeBiens table exists';
    
    -- Check record count
    DECLARE @ImmeubleTypeBienCount INT;
    SELECT @ImmeubleTypeBienCount = COUNT(*) FROM ImmeubleTypeBiens;
    PRINT '  - Total records: ' + CAST(@ImmeubleTypeBienCount AS VARCHAR);
    
    -- Check for foreign key constraints
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_Immeubles_ImmeubleId'
        AND TABLE_NAME = 'ImmeubleTypeBiens'
    )
    BEGIN
        PRINT '✓ Foreign key to Immeubles exists';
    END
    ELSE
    BEGIN
        PRINT '✗ Foreign key to Immeubles does NOT exist';
    END
    
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
        WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_TypeBiens_TypeBienId'
        AND TABLE_NAME = 'ImmeubleTypeBiens'
    )
    BEGIN
        PRINT '✓ Foreign key to TypeBiens exists';
    END
    ELSE
    BEGIN
        PRINT '✗ Foreign key to TypeBiens does NOT exist';
    END
END
ELSE
BEGIN
    PRINT '✗ ImmeubleTypeBiens table does NOT exist';
END

PRINT '';

-- =====================================================
-- 4. CHECK INDEXES
-- =====================================================

PRINT '4. INDEX ANALYSIS:';
PRINT '==================';

-- Check Reservations indexes
PRINT 'Reservations table indexes:';
SELECT 
    i.name AS IndexName,
    i.type_desc AS IndexType,
    i.is_unique AS IsUnique,
    STRING_AGG(c.name, ', ') AS Columns
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('Reservations')
AND i.name IS NOT NULL
GROUP BY i.name, i.type_desc, i.is_unique
ORDER BY i.name;

PRINT '';

-- Check ProjectTypeBiens indexes (if table exists)
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'ProjectTypeBiens table indexes:';
    SELECT 
        i.name AS IndexName,
        i.type_desc AS IndexType,
        i.is_unique AS IsUnique,
        STRING_AGG(c.name, ', ') AS Columns
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE i.object_id = OBJECT_ID('ProjectTypeBiens')
    AND i.name IS NOT NULL
    GROUP BY i.name, i.type_desc, i.is_unique
    ORDER BY i.name;
END

PRINT '';

-- =====================================================
-- 5. DATA INTEGRITY CHECKS
-- =====================================================

PRINT '5. DATA INTEGRITY ANALYSIS:';
PRINT '============================';

-- Check for orphaned records in ProjectTypeBiens (if table exists)
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'ProjectTypeBiens orphaned records:';
    
    -- Check for orphaned ProjectId
    DECLARE @OrphanedProjects INT;
    SELECT @OrphanedProjects = COUNT(*) 
    FROM ProjectTypeBiens ptb 
    LEFT JOIN Projects p ON ptb.ProjectId = p.Id 
    WHERE ptb.ProjectId IS NOT NULL AND p.Id IS NULL;
    
    IF @OrphanedProjects > 0
    BEGIN
        PRINT '✗ Found ' + CAST(@OrphanedProjects AS VARCHAR) + ' orphaned ProjectId records';
    END
    ELSE
    BEGIN
        PRINT '✓ No orphaned ProjectId records found';
    END
    
    -- Check for orphaned TypeBienId
    DECLARE @OrphanedTypeBiens INT;
    SELECT @OrphanedTypeBiens = COUNT(*) 
    FROM ProjectTypeBiens ptb 
    LEFT JOIN TypeBiens tb ON ptb.TypeBienId = tb.Id 
    WHERE ptb.TypeBienId IS NOT NULL AND tb.Id IS NULL;
    
    IF @OrphanedTypeBiens > 0
    BEGIN
        PRINT '✗ Found ' + CAST(@OrphanedTypeBiens AS VARCHAR) + ' orphaned TypeBienId records';
    END
    ELSE
    BEGIN
        PRINT '✓ No orphaned TypeBienId records found';
    END
END

-- Check for orphaned records in ImmeubleTypeBiens (if table exists)
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'ImmeubleTypeBiens orphaned records:';
    
    -- Check for orphaned ImmeubleId
    DECLARE @OrphanedImmeubles INT;
    SELECT @OrphanedImmeubles = COUNT(*) 
    FROM ImmeubleTypeBiens itb 
    LEFT JOIN Immeubles i ON itb.ImmeubleId = i.Id 
    WHERE itb.ImmeubleId IS NOT NULL AND i.Id IS NULL;
    
    IF @OrphanedImmeubles > 0
    BEGIN
        PRINT '✗ Found ' + CAST(@OrphanedImmeubles AS VARCHAR) + ' orphaned ImmeubleId records';
    END
    ELSE
    BEGIN
        PRINT '✓ No orphaned ImmeubleId records found';
    END
END

PRINT '';

-- =====================================================
-- 6. MIGRATION STATUS SUMMARY
-- =====================================================

PRINT '6. MIGRATION STATUS SUMMARY:';
PRINT '============================';

DECLARE @MigrationScore INT = 0;
DECLARE @MaxScore INT = 6;

-- Check NotaireId in Reservations
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Reservations' 
    AND COLUMN_NAME = 'NotaireId'
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ NotaireId column exists in Reservations';
END
ELSE
BEGIN
    PRINT '✗ NotaireId column missing from Reservations';
END

-- Check ProjectTypeBiens exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ ProjectTypeBiens table exists';
END
ELSE
BEGIN
    PRINT '✗ ProjectTypeBiens table missing';
END

-- Check ImmeubleTypeBiens removed
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ ImmeubleTypeBiens table removed';
END
ELSE
BEGIN
    PRINT '✗ ImmeubleTypeBiens table still exists';
END

-- Check ProjectTypeBien foreign keys
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_Projects_ProjectId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
AND EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_TypeBiens_TypeBienId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ ProjectTypeBiens foreign keys exist';
END
ELSE
BEGIN
    PRINT '✗ ProjectTypeBiens foreign keys missing';
END

-- Check Reservations foreign key for NotaireId
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_Reservations_AspNetUsers_NotaireId'
    AND TABLE_NAME = 'Reservations'
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ Reservations.NotaireId foreign key exists';
END
ELSE
BEGIN
    PRINT '✗ Reservations.NotaireId foreign key missing';
END

-- Check indexes exist
IF EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Reservations_NotaireId' 
    AND object_id = OBJECT_ID('Reservations')
)
AND EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_ProjectId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    SET @MigrationScore = @MigrationScore + 1;
    PRINT '✓ Performance indexes exist';
END
ELSE
BEGIN
    PRINT '✗ Performance indexes missing';
END

PRINT '';
PRINT 'Migration Completion: ' + CAST(@MigrationScore AS VARCHAR) + '/' + CAST(@MaxScore AS VARCHAR) + ' tasks completed';

IF @MigrationScore = @MaxScore
BEGIN
    PRINT '🎉 MIGRATION IS FULLY COMPLETED!';
END
ELSE
BEGIN
    PRINT '⚠️  MIGRATION IS INCOMPLETE - ' + CAST(@MaxScore - @MigrationScore AS VARCHAR) + ' tasks remaining';
END

PRINT '';
PRINT '=== VALIDATION REPORT COMPLETED ===';