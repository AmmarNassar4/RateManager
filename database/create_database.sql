CREATE DATABASE RateManagerDb;
GO
USE RateManagerDb;
GO

CREATE TABLE RoomTypes (
    RoomTypeId INT IDENTITY PRIMARY KEY,
    RoomCode NVARCHAR(50) NOT NULL UNIQUE,
    RoomName NVARCHAR(150) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE RatePlans (
    RatePlanId INT IDENTITY PRIMARY KEY,
    RatePlanCode NVARCHAR(50) NOT NULL UNIQUE,
    RatePlanName NVARCHAR(150) NOT NULL,
    MealPlanCode NVARCHAR(20) NULL,
    CurrencyCode NVARCHAR(10) NOT NULL DEFAULT 'SAR',
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE RateCalculationRules (
    RateCalculationRuleId INT IDENTITY PRIMARY KEY,
    RatePlanId INT NOT NULL,
    RuleName NVARCHAR(100) NOT NULL,
    RuleScope INT NOT NULL,
    PercentageValue DECIMAL(9,4) NOT NULL,
    RoomTypeId INT NULL,
    Weekday INT NULL,
    StartDate DATE NULL,
    EndDate DATE NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_RateCalculationRules_RatePlans FOREIGN KEY (RatePlanId) REFERENCES RatePlans(RatePlanId),
    CONSTRAINT FK_RateCalculationRules_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(RoomTypeId)
);

CREATE TABLE RateGenerationBatches (
    RateGenerationBatchId INT IDENTITY PRIMARY KEY,
    RatePlanId INT NOT NULL,
    SourceType INT NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    NumberOfDays INT NOT NULL,
    GlobalAdjustmentPercent DECIMAL(9,4) NOT NULL DEFAULT 0,
    SourceFilePath NVARCHAR(500) NULL,
    Notes NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_RateGenerationBatches_RatePlans FOREIGN KEY (RatePlanId) REFERENCES RatePlans(RatePlanId)
);

CREATE TABLE DailyRoomRates (
    DailyRoomRateId BIGINT IDENTITY PRIMARY KEY,
    RateGenerationBatchId INT NOT NULL,
    RatePlanId INT NOT NULL,
    RoomTypeId INT NOT NULL,
    RateDate DATE NOT NULL,
    DayName NVARCHAR(20) NOT NULL,
    GuestCount INT NOT NULL,
    RoomCount INT NOT NULL,
    BaseRate DECIMAL(18,3) NOT NULL,
    TotalAdjustmentPercent DECIMAL(9,4) NOT NULL,
    CalculatedRate DECIMAL(18,3) NOT NULL,
    ManualRate DECIMAL(18,3) NULL,
    FinalRate DECIMAL(18,3) NOT NULL,
    IsManualOverride BIT NOT NULL DEFAULT 0,
    CalculationNote NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_DailyRoomRates_Batches FOREIGN KEY (RateGenerationBatchId) REFERENCES RateGenerationBatches(RateGenerationBatchId),
    CONSTRAINT FK_DailyRoomRates_RatePlans FOREIGN KEY (RatePlanId) REFERENCES RatePlans(RatePlanId),
    CONSTRAINT FK_DailyRoomRates_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(RoomTypeId)
);

CREATE UNIQUE INDEX UX_DailyRoomRates_BatchRoomDateGuestRooms
ON DailyRoomRates (RateGenerationBatchId, RatePlanId, RoomTypeId, RateDate, GuestCount, RoomCount);

CREATE TABLE RateOverrides (
    RateOverrideId BIGINT IDENTITY PRIMARY KEY,
    DailyRoomRateId BIGINT NOT NULL,
    OldRate DECIMAL(18,3) NOT NULL,
    NewRate DECIMAL(18,3) NOT NULL,
    Reason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL,
    CONSTRAINT FK_RateOverrides_DailyRoomRates FOREIGN KEY (DailyRoomRateId) REFERENCES DailyRoomRates(DailyRoomRateId)
);

CREATE TABLE RateAuditLogs (
    RateAuditLogId BIGINT IDENTITY PRIMARY KEY,
    EntityName NVARCHAR(100) NOT NULL,
    EntityId BIGINT NOT NULL,
    ActionType NVARCHAR(50) NOT NULL,
    FieldName NVARCHAR(100) NULL,
    OldValue NVARCHAR(500) NULL,
    NewValue NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL
);

INSERT INTO RatePlans (RatePlanCode, RatePlanName, MealPlanCode, CurrencyCode) VALUES
('WALK-IN', 'Walk-In Rates', 'RO', 'SAR'),
('FAMILY-25', 'Family and Friends 25%', 'RO', 'SAR');

INSERT INTO RoomTypes (RoomCode, RoomName) VALUES
('STD-KING', 'Standard King Room'),
('STD-TWIN', 'Standard Twin Room'),
('JUNIOR-SUITE', 'Junior Suite'),
('EXEC-SUITE', 'Executive Suite'),
('ELITE-SUITE', 'Elite Suite');
GO
