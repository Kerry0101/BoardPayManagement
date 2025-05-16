-- First, check if the columns already exist, and if not, add them
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'BillingMonth')
BEGIN
    ALTER TABLE Bills ADD BillingMonth INT NOT NULL DEFAULT 0;
    PRINT 'Added BillingMonth column to Bills table';
END
ELSE
BEGIN
    PRINT 'BillingMonth column already exists';
END

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'BillingYear')
BEGIN
    ALTER TABLE Bills ADD BillingYear INT NOT NULL DEFAULT 0;
    PRINT 'Added BillingYear column to Bills table';
END
ELSE
BEGIN
    PRINT 'BillingYear column already exists';
END

-- Update existing records to populate these fields from the BillingDate
UPDATE Bills 
SET 
    BillingMonth = MONTH(BillingDate),
    BillingYear = YEAR(BillingDate)
WHERE BillingMonth = 0 OR BillingYear = 0;
PRINT 'Updated existing bill records with month and year values';

-- Add the migrations to the history table if they don't exist
IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250507062717_InitialCreate')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250507062717_InitialCreate', '9.0.0-preview.3.24154.5');
    PRINT 'Added InitialCreate migration to history';
END
ELSE
BEGIN
    PRINT 'InitialCreate migration already exists in history';
END

IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250509064028_billMonthYear')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250509064028_billMonthYear', '9.0.0-preview.3.24154.5');
    PRINT 'Added billMonthYear migration to history';
END
ELSE
BEGIN
    PRINT 'billMonthYear migration already exists in history';
END