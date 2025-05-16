CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);
GO


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
GO


CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Floors] (
    [FloorId] int NOT NULL IDENTITY,
    [FloorName] nvarchar(100) NOT NULL,
    [FloorNumber] int NOT NULL,
    [BuildingId] int NOT NULL,
    CONSTRAINT [PK_Floors] PRIMARY KEY ([FloorId]),
    CONSTRAINT [FK_Floors_Buildings_BuildingId] FOREIGN KEY ([BuildingId]) REFERENCES [Buildings] ([BuildingId]) ON DELETE CASCADE
);
GO


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
GO


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
GO


CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);
GO


CREATE TABLE [Bills] (
    [BillId] int NOT NULL IDENTITY,
    [TenantId] nvarchar(450) NOT NULL,
    [RoomId] int NOT NULL,
    [BillingDate] datetime2 NOT NULL,
    [BillingMonth] int NOT NULL,
    [BillingYear] int NOT NULL,
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
GO


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
GO


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
GO


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
GO


CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);
GO


CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
GO


CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);
GO


CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);
GO


CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);
GO


CREATE INDEX [IX_AspNetUsers_BuildingId] ON [AspNetUsers] ([BuildingId]);
GO


CREATE UNIQUE INDEX [IX_AspNetUsers_RoomId] ON [AspNetUsers] ([RoomId]) WHERE [RoomId] IS NOT NULL;
GO


CREATE UNIQUE INDEX [IX_AspNetUsers_UserName] ON [AspNetUsers] ([UserName]) WHERE [UserName] IS NOT NULL;
GO


CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
GO


CREATE INDEX [IX_Bills_RoomId] ON [Bills] ([RoomId]);
GO


CREATE INDEX [IX_Bills_TenantId] ON [Bills] ([TenantId]);
GO


CREATE INDEX [IX_Contracts_RoomId] ON [Contracts] ([RoomId]);
GO


CREATE INDEX [IX_Contracts_TenantId] ON [Contracts] ([TenantId]);
GO


CREATE INDEX [IX_Floors_BuildingId] ON [Floors] ([BuildingId]);
GO


CREATE INDEX [IX_MeterReadings_BillId] ON [MeterReadings] ([BillId]);
GO


CREATE INDEX [IX_MeterReadings_RoomId] ON [MeterReadings] ([RoomId]);
GO


CREATE INDEX [IX_MeterReadings_TenantId] ON [MeterReadings] ([TenantId]);
GO


CREATE INDEX [IX_Payments_BillId] ON [Payments] ([BillId]);
GO


CREATE INDEX [IX_Payments_TenantId] ON [Payments] ([TenantId]);
GO


CREATE INDEX [IX_Rooms_FloorId] ON [Rooms] ([FloorId]);
GO


