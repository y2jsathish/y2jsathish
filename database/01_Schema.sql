/* =====================================================================
   ATM Call Log & Ticket Management System
   01_Schema.sql — Core table definitions (SQL Server 2019+)

   Run order: 01_Schema.sql -> 02_Indexes.sql -> 03_StoredProcedures.sql
              -> 04_Views.sql -> 05_SeedData.sql

   This script is the authoritative, hand-written schema for the system.
   It mirrors (but is not code-generated from) the EF Core model in
   src/ATMTicketing.Infrastructure/Persistence — apply EF Core migrations
   in application environments, or run these scripts directly for a
   DBA-managed / non-EF deployment.
   ===================================================================== */

IF DB_ID('AtmTicketingDb') IS NULL
BEGIN
    PRINT 'Run this script against an existing AtmTicketingDb database, or CREATE DATABASE AtmTicketingDb first.';
END
GO

/* ---------------------------------------------------------------------
   Identity & Access Management
   --------------------------------------------------------------------- */

CREATE TABLE dbo.RegionMaster
(
    Id            INT IDENTITY(1,1) NOT NULL,
    RegionName    NVARCHAR(100)     NOT NULL,
    Zone          NVARCHAR(50)      NOT NULL,
    IsActive      BIT               NOT NULL CONSTRAINT DF_RegionMaster_IsActive DEFAULT (1),
    CreatedDate   DATETIME2         NOT NULL CONSTRAINT DF_RegionMaster_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate   DATETIME2         NULL,
    CONSTRAINT PK_RegionMaster PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_RegionMaster_RegionName UNIQUE (RegionName)
);
GO

CREATE TABLE dbo.Roles
(
    Id                  NVARCHAR(450)  NOT NULL,
    Name                NVARCHAR(256)  NULL,
    NormalizedName      NVARCHAR(256)  NULL,
    Description         NVARCHAR(300)  NULL,
    ConcurrencyStamp    NVARCHAR(MAX)  NULL,
    CONSTRAINT PK_Roles PRIMARY KEY CLUSTERED (Id)
);
CREATE UNIQUE INDEX IX_Roles_NormalizedName ON dbo.Roles (NormalizedName) WHERE NormalizedName IS NOT NULL;
GO

CREATE TABLE dbo.Users
(
    Id                      NVARCHAR(450)  NOT NULL,
    FullName                NVARCHAR(150)  NOT NULL,
    EmployeeCode            NVARCHAR(30)   NULL,
    RegionId                INT            NULL,
    IsActive                BIT            NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedDate             DATETIME2      NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME()),
    LastLoginDate           DATETIME2      NULL,
    UserName                NVARCHAR(256)  NULL,
    NormalizedUserName      NVARCHAR(256)  NULL,
    Email                   NVARCHAR(256)  NULL,
    NormalizedEmail         NVARCHAR(256)  NULL,
    EmailConfirmed          BIT            NOT NULL CONSTRAINT DF_Users_EmailConfirmed DEFAULT (0),
    PasswordHash            NVARCHAR(MAX)  NULL,
    SecurityStamp           NVARCHAR(MAX)  NULL,
    ConcurrencyStamp        NVARCHAR(MAX)  NULL,
    PhoneNumber             NVARCHAR(20)   NULL,
    PhoneNumberConfirmed    BIT            NOT NULL CONSTRAINT DF_Users_PhoneConfirmed DEFAULT (0),
    TwoFactorEnabled        BIT            NOT NULL CONSTRAINT DF_Users_TwoFactor DEFAULT (0),
    LockoutEnd              DATETIMEOFFSET NULL,
    LockoutEnabled          BIT            NOT NULL CONSTRAINT DF_Users_LockoutEnabled DEFAULT (1),
    AccessFailedCount       INT            NOT NULL CONSTRAINT DF_Users_AccessFailedCount DEFAULT (0),
    CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Users_RegionMaster FOREIGN KEY (RegionId) REFERENCES dbo.RegionMaster (Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IX_Users_NormalizedUserName ON dbo.Users (NormalizedUserName) WHERE NormalizedUserName IS NOT NULL;
CREATE INDEX IX_Users_NormalizedEmail ON dbo.Users (NormalizedEmail);
CREATE INDEX IX_Users_RegionId ON dbo.Users (RegionId);
GO

CREATE TABLE dbo.UserRoles
(
    UserId  NVARCHAR(450) NOT NULL,
    RoleId  NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_UserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_UserRoles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_UserRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.UserClaims
(
    Id          INT IDENTITY(1,1) NOT NULL,
    UserId      NVARCHAR(450)     NOT NULL,
    ClaimType   NVARCHAR(MAX)     NULL,
    ClaimValue  NVARCHAR(MAX)     NULL,
    CONSTRAINT PK_UserClaims PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_UserClaims_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.UserLogins
(
    LoginProvider        NVARCHAR(450) NOT NULL,
    ProviderKey           NVARCHAR(450) NOT NULL,
    ProviderDisplayName   NVARCHAR(MAX) NULL,
    UserId                NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_UserLogins PRIMARY KEY CLUSTERED (LoginProvider, ProviderKey),
    CONSTRAINT FK_UserLogins_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.UserTokens
(
    UserId          NVARCHAR(450) NOT NULL,
    LoginProvider   NVARCHAR(450) NOT NULL,
    Name            NVARCHAR(450) NOT NULL,
    Value           NVARCHAR(MAX) NULL,
    CONSTRAINT PK_UserTokens PRIMARY KEY CLUSTERED (UserId, LoginProvider, Name),
    CONSTRAINT FK_UserTokens_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.RoleClaims
(
    Id          INT IDENTITY(1,1) NOT NULL,
    RoleId      NVARCHAR(450)     NOT NULL,
    ClaimType   NVARCHAR(MAX)     NULL,
    ClaimValue  NVARCHAR(MAX)     NULL,
    CONSTRAINT PK_RoleClaims PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_RoleClaims_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE CASCADE
);
GO

/* ---------------------------------------------------------------------
   Master Data
   --------------------------------------------------------------------- */

CREATE TABLE dbo.VendorMaster
(
    Id                INT IDENTITY(1,1) NOT NULL,
    VendorCode        NVARCHAR(20)      NOT NULL,
    VendorName        NVARCHAR(150)     NOT NULL,
    ContactPerson     NVARCHAR(100)     NOT NULL,
    ContactNumber     NVARCHAR(20)      NOT NULL,
    Email             NVARCHAR(150)     NOT NULL,
    ServiceRegionId   INT               NOT NULL,
    IsActive          BIT               NOT NULL CONSTRAINT DF_VendorMaster_IsActive DEFAULT (1),
    CreatedDate       DATETIME2         NOT NULL CONSTRAINT DF_VendorMaster_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate       DATETIME2         NULL,
    CONSTRAINT PK_VendorMaster PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_VendorMaster_VendorCode UNIQUE (VendorCode),
    CONSTRAINT FK_VendorMaster_RegionMaster FOREIGN KEY (ServiceRegionId) REFERENCES dbo.RegionMaster (Id)
);
GO

CREATE TABLE dbo.AtmMaster
(
    Id           INT IDENTITY(1,1) NOT NULL,
    AtmCode      NVARCHAR(30)      NOT NULL,
    AtmName      NVARCHAR(150)     NOT NULL,
    BankName     NVARCHAR(150)     NOT NULL,
    RegionId     INT               NOT NULL,
    Zone         NVARCHAR(50)      NOT NULL,
    State        NVARCHAR(50)      NOT NULL,
    City         NVARCHAR(100)     NOT NULL,
    Address      NVARCHAR(300)     NOT NULL,
    VendorId     INT               NOT NULL,
    AtmType      TINYINT           NOT NULL, -- 1=Onsite,2=Offsite,3=CashRecycler,4=CashDeposit,5=MiniBranch
    Status       TINYINT           NOT NULL, -- 1=Active,2=Inactive,3=UnderMaintenance,4=Decommissioned
    Latitude     DECIMAL(9,6)      NULL,
    Longitude    DECIMAL(9,6)      NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_AtmMaster_IsActive DEFAULT (1),
    CreatedDate  DATETIME2         NOT NULL CONSTRAINT DF_AtmMaster_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate  DATETIME2         NULL,
    CONSTRAINT PK_AtmMaster PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_AtmMaster_AtmCode UNIQUE (AtmCode),
    CONSTRAINT FK_AtmMaster_RegionMaster FOREIGN KEY (RegionId) REFERENCES dbo.RegionMaster (Id),
    CONSTRAINT FK_AtmMaster_VendorMaster FOREIGN KEY (VendorId) REFERENCES dbo.VendorMaster (Id),
    CONSTRAINT CK_AtmMaster_AtmType CHECK (AtmType BETWEEN 1 AND 5),
    CONSTRAINT CK_AtmMaster_Status CHECK (Status BETWEEN 1 AND 4)
);
GO

CREATE TABLE dbo.CategoryMaster
(
    Id           INT IDENTITY(1,1) NOT NULL,
    Name         NVARCHAR(100)     NOT NULL,
    Description  NVARCHAR(300)     NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_CategoryMaster_IsActive DEFAULT (1),
    CreatedDate  DATETIME2         NOT NULL CONSTRAINT DF_CategoryMaster_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate  DATETIME2         NULL,
    CONSTRAINT PK_CategoryMaster PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_CategoryMaster_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.StatusMaster
(
    Id           INT IDENTITY(1,1) NOT NULL,
    Code         NVARCHAR(30)      NOT NULL,
    DisplayName  NVARCHAR(50)      NOT NULL,
    SortOrder    INT               NOT NULL,
    ColorHex     NVARCHAR(10)      NOT NULL CONSTRAINT DF_StatusMaster_ColorHex DEFAULT ('#6c757d'),
    IsTerminal   BIT               NOT NULL CONSTRAINT DF_StatusMaster_IsTerminal DEFAULT (0),
    IsActive     BIT               NOT NULL CONSTRAINT DF_StatusMaster_IsActive DEFAULT (1),
    CreatedDate  DATETIME2         NOT NULL CONSTRAINT DF_StatusMaster_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate  DATETIME2         NULL,
    CONSTRAINT PK_StatusMaster PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_StatusMaster_Code UNIQUE (Code)
);
GO

CREATE TABLE dbo.SlaConfiguration
(
    Id                       INT IDENTITY(1,1) NOT NULL,
    Priority                 TINYINT           NOT NULL, -- 1=Critical,2=High,3=Medium,4=Low
    ResponseMinutes          INT               NOT NULL,
    ResolutionMinutes        INT               NOT NULL,
    WarningThresholdPercent  INT               NOT NULL CONSTRAINT DF_SlaConfiguration_Warning DEFAULT (80),
    IsActive                 BIT               NOT NULL CONSTRAINT DF_SlaConfiguration_IsActive DEFAULT (1),
    CreatedDate              DATETIME2         NOT NULL CONSTRAINT DF_SlaConfiguration_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate              DATETIME2         NULL,
    CONSTRAINT PK_SlaConfiguration PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_SlaConfiguration_Priority UNIQUE (Priority),
    CONSTRAINT CK_SlaConfiguration_Priority CHECK (Priority BETWEEN 1 AND 4),
    CONSTRAINT CK_SlaConfiguration_Minutes CHECK (ResponseMinutes > 0 AND ResolutionMinutes > 0)
);
GO

/* Backs gap-tolerant ticket number generation (TCK-yyyyMM-######) under concurrent inserts. */
CREATE SEQUENCE dbo.TicketNumberSequence
    AS BIGINT
    START WITH 1
    INCREMENT BY 1
    NO CYCLE;
GO

/* ---------------------------------------------------------------------
   Ticketing
   --------------------------------------------------------------------- */

CREATE TABLE dbo.Tickets
(
    Id                     INT IDENTITY(1,1) NOT NULL,
    TicketNumber           NVARCHAR(30)      NOT NULL,
    AtmId                  INT               NOT NULL,
    IncidentType           NVARCHAR(100)     NOT NULL,
    CategoryId             INT               NOT NULL,
    SubCategory            NVARCHAR(100)     NULL,
    Priority               TINYINT           NOT NULL, -- 1=Critical,2=High,3=Medium,4=Low
    StatusId               INT               NOT NULL,
    Description            NVARCHAR(2000)    NOT NULL,
    ContactPerson          NVARCHAR(100)     NOT NULL,
    ContactNumber          NVARCHAR(20)      NOT NULL,
    CreatedById            NVARCHAR(450)     NOT NULL,
    AssignedToId           NVARCHAR(450)     NULL,
    AssignedVendorId       INT               NULL,
    RegionId               INT               NOT NULL,
    ResponseDueAt          DATETIME2         NULL,
    ResolutionDueAt        DATETIME2         NULL,
    RespondedAt            DATETIME2         NULL,
    ResolvedAt             DATETIME2         NULL,
    ClosedAt               DATETIME2         NULL,
    IsResponseBreached     BIT               NOT NULL CONSTRAINT DF_Tickets_IsResponseBreached DEFAULT (0),
    IsResolutionBreached   BIT               NOT NULL CONSTRAINT DF_Tickets_IsResolutionBreached DEFAULT (0),
    IsEscalated            BIT               NOT NULL CONSTRAINT DF_Tickets_IsEscalated DEFAULT (0),
    ResolutionNotes        NVARCHAR(2000)    NULL,
    ClosureRemarks         NVARCHAR(1000)    NULL,
    IsActive               BIT               NOT NULL CONSTRAINT DF_Tickets_IsActive DEFAULT (1),
    CreatedDate            DATETIME2         NOT NULL CONSTRAINT DF_Tickets_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate            DATETIME2         NULL,
    CONSTRAINT PK_Tickets PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Tickets_TicketNumber UNIQUE (TicketNumber),
    CONSTRAINT FK_Tickets_AtmMaster FOREIGN KEY (AtmId) REFERENCES dbo.AtmMaster (Id),
    CONSTRAINT FK_Tickets_CategoryMaster FOREIGN KEY (CategoryId) REFERENCES dbo.CategoryMaster (Id),
    CONSTRAINT FK_Tickets_StatusMaster FOREIGN KEY (StatusId) REFERENCES dbo.StatusMaster (Id),
    CONSTRAINT FK_Tickets_CreatedBy FOREIGN KEY (CreatedById) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Tickets_AssignedTo FOREIGN KEY (AssignedToId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_Tickets_VendorMaster FOREIGN KEY (AssignedVendorId) REFERENCES dbo.VendorMaster (Id),
    CONSTRAINT FK_Tickets_RegionMaster FOREIGN KEY (RegionId) REFERENCES dbo.RegionMaster (Id),
    CONSTRAINT CK_Tickets_Priority CHECK (Priority BETWEEN 1 AND 4)
);
GO

CREATE TABLE dbo.TicketHistory
(
    Id            INT IDENTITY(1,1) NOT NULL,
    TicketId      INT               NOT NULL,
    ActionType    TINYINT           NOT NULL, -- see ATMTicketing.Domain.Enums.TicketHistoryAction
    OldValue      NVARCHAR(500)     NULL,
    NewValue      NVARCHAR(500)     NULL,
    Notes         NVARCHAR(2000)    NULL,
    ActionById    NVARCHAR(450)     NOT NULL,
    ActionDate    DATETIME2         NOT NULL CONSTRAINT DF_TicketHistory_ActionDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TicketHistory PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TicketHistory_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets (Id) ON DELETE CASCADE,
    CONSTRAINT FK_TicketHistory_Users FOREIGN KEY (ActionById) REFERENCES dbo.Users (Id)
);
GO

CREATE TABLE dbo.TicketAssignment
(
    Id               INT IDENTITY(1,1) NOT NULL,
    TicketId         INT               NOT NULL,
    AssignedToId     NVARCHAR(450)     NOT NULL,
    AssignedById     NVARCHAR(450)     NOT NULL,
    AssignedDate     DATETIME2         NOT NULL CONSTRAINT DF_TicketAssignment_AssignedDate DEFAULT (SYSUTCDATETIME()),
    UnassignedDate   DATETIME2         NULL,
    AssignmentType   TINYINT           NOT NULL, -- 1=Auto,2=Manual,3=Reassigned
    IsCurrent        BIT               NOT NULL CONSTRAINT DF_TicketAssignment_IsCurrent DEFAULT (1),
    CONSTRAINT PK_TicketAssignment PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TicketAssignment_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets (Id) ON DELETE CASCADE,
    CONSTRAINT FK_TicketAssignment_AssignedTo FOREIGN KEY (AssignedToId) REFERENCES dbo.Users (Id),
    CONSTRAINT FK_TicketAssignment_AssignedBy FOREIGN KEY (AssignedById) REFERENCES dbo.Users (Id)
);
GO

CREATE TABLE dbo.TicketAttachment
(
    Id                INT IDENTITY(1,1) NOT NULL,
    TicketId          INT               NOT NULL,
    FileName          NVARCHAR(260)     NOT NULL,
    StoredFilePath    NVARCHAR(500)     NOT NULL,
    FileType          NVARCHAR(50)      NOT NULL,
    FileSizeBytes     BIGINT            NOT NULL,
    UploadedById      NVARCHAR(450)     NOT NULL,
    UploadedDate      DATETIME2         NOT NULL CONSTRAINT DF_TicketAttachment_UploadedDate DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_TicketAttachment PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_TicketAttachment_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets (Id) ON DELETE CASCADE,
    CONSTRAINT FK_TicketAttachment_Users FOREIGN KEY (UploadedById) REFERENCES dbo.Users (Id)
);
GO

CREATE TABLE dbo.Notification
(
    Id            INT IDENTITY(1,1) NOT NULL,
    UserId        NVARCHAR(450)     NULL,
    TicketId      INT               NULL,
    Channel       TINYINT           NOT NULL, -- 1=Email,2=Sms,3=InApp
    Event         TINYINT           NOT NULL, -- see ATMTicketing.Domain.Enums.NotificationEvent
    Subject       NVARCHAR(200)     NOT NULL,
    Message       NVARCHAR(2000)    NOT NULL,
    IsRead        BIT               NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT (0),
    IsSent        BIT               NOT NULL CONSTRAINT DF_Notification_IsSent DEFAULT (0),
    ErrorMessage  NVARCHAR(1000)    NULL,
    CreatedDate   DATETIME2         NOT NULL CONSTRAINT DF_Notification_CreatedDate DEFAULT (SYSUTCDATETIME()),
    SentDate      DATETIME2         NULL,
    CONSTRAINT PK_Notification PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Notification_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_Notification_Tickets FOREIGN KEY (TicketId) REFERENCES dbo.Tickets (Id) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.AuditLog
(
    Id           BIGINT IDENTITY(1,1) NOT NULL,
    UserId       NVARCHAR(450)        NULL,
    UserName     NVARCHAR(256)        NULL,
    Action       NVARCHAR(100)        NOT NULL,
    EntityName   NVARCHAR(100)        NOT NULL,
    EntityId     NVARCHAR(50)         NULL,
    OldValues    NVARCHAR(MAX)        NULL,
    NewValues    NVARCHAR(MAX)        NULL,
    IpAddress    NVARCHAR(50)         NULL,
    Timestamp    DATETIME2            NOT NULL CONSTRAINT DF_AuditLog_Timestamp DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (Id)
);
GO
