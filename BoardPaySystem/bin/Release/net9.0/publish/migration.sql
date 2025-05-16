IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [Buildings] (
    [BuildingId] int NOT NULL IDENTITY,
    [BuildingName] nvarchar(50) NOT NULL,
    [Address] nvarchar(100) NOT NULL,
    [DefaultMonthlyRent] decimal(10,2) NOT NULL,
    [DefaultWaterFee] decimal(10,2) NOT NULL,
    [DefaultElectricityFee] decimal(10,2) NOT NULL,
    [DefaultWifiFee] decimal(10,2) NOT NULL,
    [LateFee] decimal(10,2) NOT NULL,
    CONSTRAINT [PK_Buildings] PRIMARY KEY ([BuildingId])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Floors] (
    [FloorId] int NOT NULL IDENTITY,
    [FloorName] nvarchar(100) NOT NULL,
    [FloorNumber] int NOT NULL,
    [BuildingId] int NOT NULL,
    CONSTRAINT [PK_Floors] PRIMARY KEY ([FloorId]),
    CONSTRAINT [FK_Floors_Buildings_BuildingId] FOREIGN KEY ([BuildingId]) REFERENCES [Buildings] ([BuildingId]) ON DELETE CASCADE
);

CREATE TABLE [Rooms] (
    [RoomId] int NOT NULL IDENTITY,
    [RoomNumber] nvarchar(20) NOT NULL,
    [Description] nvarchar(100) NULL,
    [IsOccupied] bit NOT NULL,
    [FloorId] int NOT NULL,
    [CustomMonthlyRent] decimal(18,2) NULL,
    [CustomWaterFee] decimal(18,2) NULL,
    [CustomElectricityFee] decimal(18,2) NULL,
    [CustomWifiFee] decimal(18,2) NULL,
    [TenantId] nvarchar(max) NULL,
    CONSTRAINT [PK_Rooms] PRIMARY KEY ([RoomId]),
    CONSTRAINT [FK_Rooms_Floors_FloorId] FOREIGN KEY ([FloorId]) REFERENCES [Floors] ([FloorId]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(50) NOT NULL,
    [LastName] nvarchar(50) NOT NULL,
    [BuildingId] int NULL,
    [RoomId] int NULL,
    [StartDate] datetime2 NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUsers_Buildings_BuildingId] FOREIGN KEY ([BuildingId]) REFERENCES [Buildings] ([BuildingId]),
    CONSTRAINT [FK_AspNetUsers_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([RoomId])
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Bills] (
    [BillId] int NOT NULL IDENTITY,
    [TenantId] nvarchar(450) NOT NULL,
    [RoomId] int NOT NULL,
    [BillingDate] datetime2 NOT NULL,
    [DueDate] datetime2 NOT NULL,
    [MonthlyRent] decimal(18,2) NOT NULL,
    [WaterFee] decimal(18,2) NOT NULL,
    [ElectricityFee] decimal(18,2) NOT NULL,
    [WifiFee] decimal(18,2) NOT NULL,
    [LateFee] decimal(18,2) NULL,
    [OtherFees] decimal(18,2) NULL,
    [OtherFeesDescription] nvarchar(200) NULL,
    [Status] int NOT NULL,
    [PaymentDate] datetime2 NULL,
    [AmountPaid] decimal(18,2) NULL,
    [PaymentReference] nvarchar(200) NULL,
    [Notes] nvarchar(500) NULL,
    [PaymentMethod] nvarchar(50) NULL,
    CONSTRAINT [PK_Bills] PRIMARY KEY ([BillId]),
    CONSTRAINT [FK_Bills_AspNetUsers_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Bills_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([RoomId])
);

CREATE TABLE [Contracts] (
    [ContractId] int NOT NULL IDENTITY,
    [TenantId] nvarchar(450) NOT NULL,
    [RoomId] int NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [MonthlyRent] decimal(18,2) NOT NULL,
    [SecurityDeposit] decimal(18,2) NOT NULL,
    [AdvanceRent] decimal(18,2) NOT NULL,
    [TerminationDate] datetime2 NULL,
    [TerminationReason] nvarchar(500) NULL,
    [Status] int NOT NULL,
    [Notes] nvarchar(500) NULL,
    CONSTRAINT [PK_Contracts] PRIMARY KEY ([ContractId]),
    CONSTRAINT [FK_Contracts_AspNetUsers_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Contracts_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([RoomId])
);

CREATE TABLE [MeterReadings] (
    [ReadingId] int NOT NULL IDENTITY,
    [TenantId] nvarchar(450) NOT NULL,
    [RoomId] int NOT NULL,
    [ReadingDate] datetime2 NOT NULL,
    [CurrentReading] decimal(18,2) NOT NULL,
    [PreviousReading] decimal(18,2) NULL,
    [RatePerKwh] decimal(18,2) NOT NULL,
    [Notes] nvarchar(500) NULL,
    [BillId] int NULL,
    CONSTRAINT [PK_MeterReadings] PRIMARY KEY ([ReadingId]),
    CONSTRAINT [FK_MeterReadings_AspNetUsers_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_MeterReadings_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([BillId]),
    CONSTRAINT [FK_MeterReadings_Rooms_RoomId] FOREIGN KEY ([RoomId]) REFERENCES [Rooms] ([RoomId])
);

CREATE TABLE [Payments] (
    [PaymentId] int NOT NULL IDENTITY,
    [BillId] int NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentDate] datetime2 NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    [ReferenceNumber] nvarchar(100) NULL,
    [ProofUrl] nvarchar(255) NULL,
    [Notes] nvarchar(255) NULL,
    [TenantId] nvarchar(450) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentId]),
    CONSTRAINT [FK_Payments_AspNetUsers_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [AspNetUsers] ([Id]),
    CONSTRAINT [FK_Payments_Bills_BillId] FOREIGN KEY ([BillId]) REFERENCES [Bills] ([BillId]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE INDEX [IX_AspNetUsers_BuildingId] ON [AspNetUsers] ([BuildingId]);

CREATE UNIQUE INDEX [IX_AspNetUsers_RoomId] ON [AspNetUsers] ([RoomId]) WHERE [RoomId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_AspNetUsers_UserName] ON [AspNetUsers] ([UserName]) WHERE [UserName] IS NOT NULL;

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Bills_RoomId] ON [Bills] ([RoomId]);

CREATE INDEX [IX_Bills_TenantId] ON [Bills] ([TenantId]);

CREATE INDEX [IX_Contracts_RoomId] ON [Contracts] ([RoomId]);

CREATE INDEX [IX_Contracts_TenantId] ON [Contracts] ([TenantId]);

CREATE INDEX [IX_Floors_BuildingId] ON [Floors] ([BuildingId]);

CREATE INDEX [IX_MeterReadings_BillId] ON [MeterReadings] ([BillId]);

CREATE INDEX [IX_MeterReadings_RoomId] ON [MeterReadings] ([RoomId]);

CREATE INDEX [IX_MeterReadings_TenantId] ON [MeterReadings] ([TenantId]);

CREATE INDEX [IX_Payments_BillId] ON [Payments] ([BillId]);

CREATE INDEX [IX_Payments_TenantId] ON [Payments] ([TenantId]);

CREATE INDEX [IX_Rooms_FloorId] ON [Rooms] ([FloorId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250507062717_InitialCreate', N'9.0.4');

COMMIT;
GO

