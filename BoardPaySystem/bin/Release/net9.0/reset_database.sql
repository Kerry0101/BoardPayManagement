-- Reset database script
PRINT 'Resetting database...';

-- Disable foreign key constraints
ALTER DATABASE [BoardPaySystem] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- Clear all tables in proper order
DELETE FROM MeterReadings;
DELETE FROM Bills;
DELETE FROM Payments;
DELETE FROM Contracts;
UPDATE Rooms SET CurrentTenantId = NULL;
UPDATE AspNetUsers SET RoomId = NULL, BuildingId = NULL;
DELETE FROM Rooms;
DELETE FROM Floors;
DELETE FROM Buildings;

-- Keep landlord users, delete all other users
DELETE FROM AspNetUsers WHERE Id NOT IN (SELECT UserId FROM AspNetUserRoles WHERE RoleId = (SELECT Id FROM AspNetRoles WHERE Name = 'Landlord'));

-- Re-enable constraints
EXEC sp_MSforeachtable 'ALTER TABLE ? CHECK CONSTRAINT ALL';
ALTER DATABASE [BoardPaySystem] SET MULTI_USER;

PRINT 'Database reset complete';
