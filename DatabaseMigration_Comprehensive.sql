-- =====================================================
-- Comprehensive Database Migration Script
-- Entity Changes for GPIA Real Estate Project
-- =====================================================
-- Migration Date: 2025-10-22
-- Description: 
-- 1. Add NotaireId to Reservation table
-- 2. Update ReservationStatus enum to include Sold status
-- 3. Create ProjectTypeBien junction table (if not exists)
-- 4. Update relationships between Project and TypeBien
-- 5. Remove Immeuble-TypeBien relationship
-- 6. Migrate existing data
-- 7. Add constraints and indexes
-- =====================================================

BEGIN TRANSACTION;
BEGIN TRY

-- =====================================================
-- 1. ADD NOTAIREID TO RESERVATION TABLE
-- =====================================================

-- Check if column already exists before adding
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Reservations' 
    AND COLUMN_NAME = 'NotaireId'
)
BEGIN
    PRINT 'Adding NotaireId column to Reservations table...';
    
    -- Add NotaireId column as nullable initially
    ALTER TABLE Reservations 
    ADD NotaireId NVARCHAR(450) NULL;
    
    PRINT 'NotaireId column added successfully.';
END
ELSE
BEGIN
    PRINT 'NotaireId column already exists in Reservations table.';
END

-- =====================================================
-- 2. UPDATE RESERVATIONSTATUS ENUM (ADD SOLD STATUS)
-- =====================================================

-- Note: Since ReservationStatus is stored as int, we don't need to modify the table structure
-- The enum values are: Pending=0, Approved=1, Rejected=2, Cancelled=3, Sold=4
-- The Sold=4 value is already handled at the application level

PRINT 'ReservationStatus enum already supports Sold status (value 4). No database changes needed.';

-- =====================================================
-- 3. CREATE PROJECTTYPEBIEN JUNCTION TABLE
-- =====================================================

-- Check if ProjectTypeBiens table already exists
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Creating ProjectTypeBiens junction table...';
    
    CREATE TABLE ProjectTypeBiens (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        ProjectId UNIQUEIDENTIFIER NULL,
        TypeBienId INT NULL,
        CONSTRAINT PK_ProjectTypeBiens PRIMARY KEY (Id)
    );
    
    PRINT 'ProjectTypeBiens table created successfully.';
END
ELSE
BEGIN
    PRINT 'ProjectTypeBiens table already exists.';
END

-- =====================================================
-- 4. ADD FOREIGN KEY CONSTRAINTS FOR PROJECTTYPEBIEN
-- =====================================================

-- Add foreign key to Projects table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_Projects_ProjectId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Adding foreign key constraint for ProjectId...';
    
    ALTER TABLE ProjectTypeBiens 
    ADD CONSTRAINT FK_ProjectTypeBiens_Projects_ProjectId 
    FOREIGN KEY (ProjectId) REFERENCES Projects(Id) 
    ON DELETE CASCADE;
    
    PRINT 'Foreign key constraint for ProjectId added successfully.';
END

-- Add foreign key to TypeBiens table
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ProjectTypeBiens_TypeBiens_TypeBienId'
    AND TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT 'Adding foreign key constraint for TypeBienId...';
    
    ALTER TABLE ProjectTypeBiens 
    ADD CONSTRAINT FK_ProjectTypeBiens_TypeBiens_TypeBienId 
    FOREIGN KEY (TypeBienId) REFERENCES TypeBiens(Id) 
    ON DELETE CASCADE;
    
    PRINT 'Foreign key constraint for TypeBienId added successfully.';
END

-- =====================================================
-- 5. ADD FOREIGN KEY CONSTRAINT FOR NOTAIREID IN RESERVATIONS
-- =====================================================

-- Add foreign key constraint for NotaireId if not exists
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_Reservations_AspNetUsers_NotaireId'
    AND TABLE_NAME = 'Reservations'
)
BEGIN
    PRINT 'Adding foreign key constraint for NotaireId...';
    
    ALTER TABLE Reservations 
    ADD CONSTRAINT FK_Reservations_AspNetUsers_NotaireId 
    FOREIGN KEY (NotaireId) REFERENCES AspNetUsers(Id) 
    ON DELETE SET NULL;
    
    PRINT 'Foreign key constraint for NotaireId added successfully.';
END

-- =====================================================
-- 6. DATA MIGRATION: MIGRATE IMMEUBLE-TYPEBIEN TO PROJECT-TYPEBIEN
-- =====================================================

PRINT 'Starting data migration from ImmeubleTypeBiens to ProjectTypeBiens...';

-- Check if there's data to migrate
IF EXISTS (
    SELECT 1 
    FROM ImmeubleTypeBiens 
    WHERE ImmeubleId IS NOT NULL AND TypeBienId IS NOT NULL
)
BEGIN
    PRINT 'Found ImmeubleTypeBien records to migrate.';
    
    -- Migrate data from ImmeubleTypeBiens to ProjectTypeBiens
    -- This creates ProjectTypeBien records for each ImmeubleTypeBien
    INSERT INTO ProjectTypeBiens (ProjectId, TypeBienId)
    SELECT DISTINCT 
        i.ProjectId, 
        itb.TypeBienId
    FROM ImmeubleTypeBiens itb
    INNER JOIN Immeubles i ON itb.ImmeubleId = i.Id
    WHERE i.ProjectId IS NOT NULL 
    AND itb.TypeBienId IS NOT NULL
    AND NOT EXISTS (
        SELECT 1 
        FROM ProjectTypeBiens ptb 
        WHERE ptb.ProjectId = i.ProjectId 
        AND ptb.TypeBienId = itb.TypeBienId
    );
    
    DECLARE @MigratedCount INT = @@ROWCOUNT;
    PRINT CAST(@MigratedCount AS VARCHAR) + ' records migrated from ImmeubleTypeBiens to ProjectTypeBiens.';
END
ELSE
BEGIN
    PRINT 'No ImmeubleTypeBien records found to migrate.';
END

-- =====================================================
-- 7. REMOVE IMMEUBLE-TYPEBIEN RELATIONSHIP
-- =====================================================

-- Drop foreign key constraints first
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_Immeubles_ImmeubleId'
    AND TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Dropping foreign key constraint FK_ImmeubleTypeBiens_Immeubles_ImmeubleId...';
    ALTER TABLE ImmeubleTypeBiens DROP CONSTRAINT FK_ImmeubleTypeBiens_Immeubles_ImmeubleId;
END

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS 
    WHERE CONSTRAINT_NAME = 'FK_ImmeubleTypeBiens_TypeBiens_TypeBienId'
    AND TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Dropping foreign key constraint FK_ImmeubleTypeBiens_TypeBiens_TypeBienId...';
    ALTER TABLE ImmeubleTypeBiens DROP CONSTRAINT FK_ImmeubleTypeBiens_TypeBiens_TypeBienId;
END

-- Drop the table if it exists
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT 'Dropping ImmeubleTypeBiens table...';
    DROP TABLE ImmeubleTypeBiens;
    PRINT 'ImmeubleTypeBiens table dropped successfully.';
END

-- =====================================================
-- 8. ADD INDEXES FOR PERFORMANCE
-- =====================================================

-- Index for Reservations.NotaireId
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_Reservations_NotaireId' 
    AND object_id = OBJECT_ID('Reservations')
)
BEGIN
    PRINT 'Creating index IX_Reservations_NotaireId...';
    CREATE INDEX IX_Reservations_NotaireId ON Reservations(NotaireId);
    PRINT 'Index IX_Reservations_NotaireId created successfully.';
END

-- Index for ProjectTypeBiens.ProjectId
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_ProjectId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Creating index IX_ProjectTypeBiens_ProjectId...';
    CREATE INDEX IX_ProjectTypeBiens_ProjectId ON ProjectTypeBiens(ProjectId);
    PRINT 'Index IX_ProjectTypeBiens_ProjectId created successfully.';
END

-- Index for ProjectTypeBiens.TypeBienId
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_TypeBienId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Creating index IX_ProjectTypeBiens_TypeBienId...';
    CREATE INDEX IX_ProjectTypeBiens_TypeBienId ON ProjectTypeBiens(TypeBienId);
    PRINT 'Index IX_ProjectTypeBiens_TypeBienId created successfully.';
END

-- Composite index for ProjectTypeBiens (ProjectId, TypeBienId) for uniqueness
IF NOT EXISTS (
    SELECT 1 
    FROM sys.indexes 
    WHERE name = 'IX_ProjectTypeBiens_ProjectId_TypeBienId' 
    AND object_id = OBJECT_ID('ProjectTypeBiens')
)
BEGIN
    PRINT 'Creating composite index IX_ProjectTypeBiens_ProjectId_TypeBienId...';
    CREATE UNIQUE INDEX IX_ProjectTypeBiens_ProjectId_TypeBienId ON ProjectTypeBiens(ProjectId, TypeBienId) 
    WHERE ProjectId IS NOT NULL AND TypeBienId IS NOT NULL;
    PRINT 'Composite index IX_ProjectTypeBiens_ProjectId_TypeBienId created successfully.';
END

-- =====================================================
-- 9. UPDATE EXISTING DATA IF NEEDED
-- =====================================================

-- Update any reservations that might need a default NotaireId based on business logic
-- This is optional and should be customized based on business requirements
PRINT 'Checking for reservations that might need NotaireId assignment...';

-- Example: Assign NotaireId from related NotaryAppointment if exists
UPDATE r
SET r.NotaireId = na.NotaireId
FROM Reservations r
INNER JOIN NotaryAppointments na ON r.Id = na.ReservationId
WHERE r.NotaireId IS NULL 
AND na.NotaireId IS NOT NULL;

DECLARE @UpdatedReservations INT = @@ROWCOUNT;
IF @UpdatedReservations > 0
BEGIN
    PRINT CAST(@UpdatedReservations AS VARCHAR) + ' reservations updated with NotaireId from NotaryAppointments.';
END

-- =====================================================
-- 10. VERIFICATION AND SUMMARY
-- =====================================================

PRINT '=== MIGRATION SUMMARY ===';
PRINT '1. NotaireId column added to Reservations table';
PRINT '2. ReservationStatus enum supports Sold status (value 4)';
PRINT '3. ProjectTypeBiens junction table created/configured';
PRINT '4. Foreign key constraints added';
PRINT '5. Data migrated from ImmeubleTypeBiens to ProjectTypeBiens';
PRINT '6. ImmeubleTypeBiens table dropped';
PRINT '7. Performance indexes created';
PRINT '8. Data validation completed';

-- Verify the changes
PRINT '=== VERIFICATION ===';

-- Check Reservations table
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'Reservations' 
    AND COLUMN_NAME = 'NotaireId'
)
BEGIN
    PRINT '✓ NotaireId column exists in Reservations table';
END

-- Check ProjectTypeBiens table
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ProjectTypeBiens'
)
BEGIN
    PRINT '✓ ProjectTypeBiens table exists';
    
    DECLARE @ProjectTypeBienCount INT;
    SELECT @ProjectTypeBienCount = COUNT(*) FROM ProjectTypeBiens;
    PRINT '✓ ProjectTypeBiens contains ' + CAST(@ProjectTypeBienCount AS VARCHAR) + ' records';
END

-- Check ImmeubleTypeBiens table is gone
IF NOT EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.TABLES 
    WHERE TABLE_NAME = 'ImmeubleTypeBiens'
)
BEGIN
    PRINT '✓ ImmeubleTypeBiens table successfully removed';
END

PRINT '=== MIGRATION COMPLETED SUCCESSFULLY ===';

COMMIT TRANSACTION;

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '=== MIGRATION FAILED ===';
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR);
    PRINT 'Error Severity: ' + CAST(ERROR_SEVERITY() AS VARCHAR);
    PRINT 'Error State: ' + CAST(ERROR_STATE() AS VARCHAR);
    PRINT 'Error Procedure: ' + ISNULL(ERROR_PROCEDURE(), 'N/A');
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR);
    PRINT 'Error Message: ' + ERROR_MESSAGE();
    
    -- Re-throw the error to the calling application
    THROW;

END CATCH;