/* =====================================================================
   05_SeedData.sql — Master data required for the application to function
   (roles, statuses, categories, SLA policy, sample regions).

   Note: seeding Users requires ASP.NET Identity password hashing, which
   this script cannot perform — the application's DbInitializer
   (src/ATMTicketing.Infrastructure/Persistence/Seed/DbInitializer.cs)
   creates the default admin/engineer accounts on first run instead.
   ===================================================================== */

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM dbo.Roles)
BEGIN
    INSERT INTO dbo.Roles (Id, Name, NormalizedName, Description, ConcurrencyStamp) VALUES
        (NEWID(), 'Administrator',    'ADMINISTRATOR',    'Full system access: users, roles, SLA, masters, settings', NEWID()),
        (NEWID(), 'CallCenterAgent',  'CALLCENTERAGENT',  'Creates and tracks ATM incident tickets', NEWID()),
        (NEWID(), 'FieldEngineer',    'FIELDENGINEER',    'Works assigned tickets in the field', NEWID()),
        (NEWID(), 'TeamLead',         'TEAMLEAD',         'Assigns/reassigns tickets, monitors SLA and escalations', NEWID()),
        (NEWID(), 'OperationsManager','OPERATIONSMANAGER','Dashboards, reports, SLA compliance oversight', NEWID());
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.StatusMaster)
BEGIN
    SET IDENTITY_INSERT dbo.StatusMaster ON;
    INSERT INTO dbo.StatusMaster (Id, Code, DisplayName, SortOrder, ColorHex, IsTerminal) VALUES
        (1, 'NEW',               'New',               1, '#0d6efd', 0),
        (2, 'ASSIGNED',          'Assigned',          2, '#6610f2', 0),
        (3, 'IN_PROGRESS',       'In Progress',       3, '#0dcaf0', 0),
        (4, 'PENDING_PARTS',     'Pending Parts',     4, '#fd7e14', 0),
        (5, 'PENDING_CUSTOMER',  'Pending Customer',  5, '#ffc107', 0),
        (6, 'ESCALATED',         'Escalated',         6, '#dc3545', 0),
        (7, 'RESOLVED',          'Resolved',          7, '#20c997', 0),
        (8, 'CLOSED',            'Closed',            8, '#198754', 1),
        (9, 'CANCELLED',         'Cancelled',         9, '#6c757d', 1);
    SET IDENTITY_INSERT dbo.StatusMaster OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CategoryMaster)
BEGIN
    INSERT INTO dbo.CategoryMaster (Name) VALUES
        ('ATM Down'), ('Cash Jam'), ('Cash Out'), ('Printer Failure'), ('Receipt Issue'),
        ('Network Failure'), ('Card Reader Issue'), ('Power Failure'), ('CCTV Failure'),
        ('Security Incident'), ('Preventive Maintenance');
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SlaConfiguration)
BEGIN
    INSERT INTO dbo.SlaConfiguration (Priority, ResponseMinutes, ResolutionMinutes, WarningThresholdPercent) VALUES
        (1, 15, 120,  80), -- Critical: 15 min response / 2 hr resolution
        (2, 30, 240,  80), -- High: 30 min response / 4 hr resolution
        (3, 60, 480,  80), -- Medium: 1 hr response / 8 hr resolution
        (4, 240, 1440, 80); -- Low: 4 hr response / 24 hr resolution
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.RegionMaster)
BEGIN
    INSERT INTO dbo.RegionMaster (RegionName, Zone) VALUES
        ('North', 'Zone A'), ('South', 'Zone B'), ('East', 'Zone C'), ('West', 'Zone D');
END
GO
