-- Add BillingMonth and BillingYear columns to Bills table
ALTER TABLE Bills ADD BillingMonth INT NULL;
ALTER TABLE Bills ADD BillingYear INT NULL;

-- Update existing records to populate these fields from the BillingDate
UPDATE Bills 
SET 
    BillingMonth = MONTH(BillingDate),
    BillingYear = YEAR(BillingDate);

-- Change columns to NOT NULL after populating data
ALTER TABLE Bills ALTER COLUMN BillingMonth INT NOT NULL;
ALTER TABLE Bills ALTER COLUMN BillingYear INT NOT NULL;

-- Create an index for faster querying by month/year
CREATE INDEX IX_Bills_BillingMonthYear ON Bills(BillingYear, BillingMonth);