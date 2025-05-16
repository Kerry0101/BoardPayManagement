-- MeterReadings table creation script
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MeterReadings')
BEGIN
    CREATE TABLE [dbo].[MeterReadings] (
        [ReadingId] INT IDENTITY(1,1) NOT NULL,
        [TenantId] NVARCHAR(450) NOT NULL,
        [RoomId] INT NOT NULL,
        [ReadingDate] DATETIME2 NOT NULL,
        [CurrentReading] DECIMAL(18,2) NOT NULL,
        [PreviousReading] DECIMAL(18,2) NULL,
        [RatePerKwh] DECIMAL(18,2) NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [BillId] INT NULL,
        CONSTRAINT [PK_MeterReadings] PRIMARY KEY ([ReadingId]),
        CONSTRAINT [FK_MeterReadings_AspNetUsers_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MeterReadings_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([RoomId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MeterReadings_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [dbo].[Bills] ([BillId]) ON DELETE NO ACTION
    );

    -- Create indexes for better performance
    CREATE INDEX [IX_MeterReadings_TenantId] ON [dbo].[MeterReadings] ([TenantId]);
    CREATE INDEX [IX_MeterReadings_RoomId] ON [dbo].[MeterReadings] ([RoomId]);
    CREATE INDEX [IX_MeterReadings_BillId] ON [dbo].[MeterReadings] ([BillId]);
    
    PRINT 'MeterReadings table created successfully.';
END
ELSE
BEGIN
    PRINT 'MeterReadings table already exists.';
END
