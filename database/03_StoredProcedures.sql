/* =====================================================================
   03_StoredProcedures.sql — Representative stored procedures.

   The ASP.NET Core application talks to the database through EF Core
   (see ATMTicketing.Infrastructure.Services), not through these
   procedures. They are provided so the database layer is independently
   usable/auditable by DBAs, reporting tools, or a SQL Agent job (see
   usp_Sla_EvaluateBreaches), and so bulk/aggregate operations have a
   documented, tested SQL-native equivalent to the C# service logic.
   ===================================================================== */

CREATE OR ALTER PROCEDURE dbo.usp_Ticket_Create
    @AtmId              INT,
    @IncidentType        NVARCHAR(100),
    @CategoryId          INT,
    @SubCategory         NVARCHAR(100) = NULL,
    @Priority            TINYINT,
    @Description         NVARCHAR(2000),
    @ContactPerson       NVARCHAR(100),
    @ContactNumber       NVARCHAR(20),
    @CreatedById         NVARCHAR(450),
    @NewTicketId         INT OUTPUT,
    @NewTicketNumber     NVARCHAR(30) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RegionId INT, @VendorId INT, @Now DATETIME2 = SYSUTCDATETIME();
    DECLARE @ResponseMinutes INT, @ResolutionMinutes INT;
    DECLARE @Seq BIGINT;

    SELECT @RegionId = RegionId, @VendorId = VendorId FROM dbo.AtmMaster WHERE Id = @AtmId;
    IF @RegionId IS NULL
    BEGIN
        RAISERROR('ATM %d was not found.', 16, 1, @AtmId);
        RETURN;
    END

    SELECT @ResponseMinutes = ResponseMinutes, @ResolutionMinutes = ResolutionMinutes
    FROM dbo.SlaConfiguration WHERE Priority = @Priority;
    IF @ResponseMinutes IS NULL
    BEGIN
        RAISERROR('No SLA configuration found for priority %d.', 16, 1, @Priority);
        RETURN;
    END

    SET @Seq = NEXT VALUE FOR dbo.TicketNumberSequence;
    SET @NewTicketNumber = 'TCK-' + FORMAT(@Now, 'yyyyMM') + '-' + RIGHT('000000' + CAST(@Seq AS VARCHAR(10)), 6);

    BEGIN TRANSACTION;

    INSERT INTO dbo.Tickets
        (TicketNumber, AtmId, IncidentType, CategoryId, SubCategory, Priority, StatusId,
         Description, ContactPerson, ContactNumber, CreatedById, RegionId, AssignedVendorId,
         ResponseDueAt, ResolutionDueAt, CreatedDate)
    VALUES
        (@NewTicketNumber, @AtmId, @IncidentType, @CategoryId, @SubCategory, @Priority, 1 /* New */,
         @Description, @ContactPerson, @ContactNumber, @CreatedById, @RegionId, @VendorId,
         DATEADD(MINUTE, @ResponseMinutes, @Now), DATEADD(MINUTE, @ResolutionMinutes, @Now), @Now);

    SET @NewTicketId = SCOPE_IDENTITY();

    INSERT INTO dbo.TicketHistory (TicketId, ActionType, NewValue, Notes, ActionById, ActionDate)
    VALUES (@NewTicketId, 1 /* Created */, @NewTicketNumber, 'Ticket created.', @CreatedById, @Now);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Ticket_UpdateStatus
    @TicketId       INT,
    @NewStatusId    INT,
    @Notes          NVARCHAR(2000) = NULL,
    @ActionById     NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OldStatusName NVARCHAR(50), @NewStatusName NVARCHAR(50), @Now DATETIME2 = SYSUTCDATETIME();

    SELECT @OldStatusName = sm.DisplayName
    FROM dbo.Tickets t JOIN dbo.StatusMaster sm ON sm.Id = t.StatusId
    WHERE t.Id = @TicketId;

    SELECT @NewStatusName = DisplayName FROM dbo.StatusMaster WHERE Id = @NewStatusId;
    IF @NewStatusName IS NULL
    BEGIN
        RAISERROR('Status %d does not exist.', 16, 1, @NewStatusId);
        RETURN;
    END

    BEGIN TRANSACTION;

    UPDATE dbo.Tickets
    SET StatusId = @NewStatusId,
        RespondedAt = CASE WHEN RespondedAt IS NULL AND @NewStatusId <> 1 THEN @Now ELSE RespondedAt END,
        ResolvedAt = CASE WHEN @NewStatusId = 7 /* Resolved */ THEN @Now ELSE ResolvedAt END,
        UpdatedDate = @Now
    WHERE Id = @TicketId;

    INSERT INTO dbo.TicketHistory (TicketId, ActionType, OldValue, NewValue, Notes, ActionById, ActionDate)
    VALUES (@TicketId, 2 /* StatusChanged */, @OldStatusName, @NewStatusName, @Notes, @ActionById, @Now);

    COMMIT TRANSACTION;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Ticket_Assign
    @TicketId       INT,
    @EngineerId     NVARCHAR(450),
    @AssignedById   NVARCHAR(450),
    @AssignmentType TINYINT = 2 /* Manual */
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2 = SYSUTCDATETIME();

    BEGIN TRANSACTION;

    UPDATE dbo.TicketAssignment
    SET IsCurrent = 0, UnassignedDate = @Now
    WHERE TicketId = @TicketId AND IsCurrent = 1;

    INSERT INTO dbo.TicketAssignment (TicketId, AssignedToId, AssignedById, AssignedDate, AssignmentType, IsCurrent)
    VALUES (@TicketId, @EngineerId, @AssignedById, @Now, @AssignmentType, 1);

    UPDATE dbo.Tickets
    SET AssignedToId = @EngineerId,
        StatusId = CASE WHEN StatusId = 1 /* New */ THEN 2 /* Assigned */ ELSE StatusId END,
        UpdatedDate = @Now
    WHERE Id = @TicketId;

    INSERT INTO dbo.TicketHistory (TicketId, ActionType, NewValue, ActionById, ActionDate)
    VALUES (@TicketId, 3 /* Assigned */, @EngineerId, @AssignedById, @Now);

    COMMIT TRANSACTION;
END
GO

/* Intended to run on a schedule (e.g. SQL Agent job every 1-5 minutes) as a DB-native
   backstop to the application's SlaMonitorService hosted service. Idempotent. */
CREATE OR ALTER PROCEDURE dbo.usp_Sla_EvaluateBreaches
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Now DATETIME2 = SYSUTCDATETIME();

    UPDATE dbo.Tickets
    SET IsResolutionBreached = 1, UpdatedDate = @Now
    WHERE ResolvedAt IS NULL
      AND ClosedAt IS NULL
      AND IsResolutionBreached = 0
      AND ResolutionDueAt IS NOT NULL
      AND ResolutionDueAt <= @Now;

    UPDATE dbo.Tickets
    SET IsResponseBreached = 1, UpdatedDate = @Now
    WHERE RespondedAt IS NULL
      AND IsResponseBreached = 0
      AND ResponseDueAt IS NOT NULL
      AND ResponseDueAt <= @Now;

    SELECT @@ROWCOUNT AS TicketsFlagged;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_GetSummary
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @OpenStatusIds TABLE (StatusId INT);
    INSERT INTO @OpenStatusIds VALUES (1), (2), (3), (4), (5), (6); -- New..Escalated

    SELECT
        (SELECT COUNT(*) FROM dbo.Tickets) AS TotalTickets,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE StatusId IN (SELECT StatusId FROM @OpenStatusIds)) AS OpenTickets,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE StatusId = 8 /* Closed */) AS ClosedTickets,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE IsResolutionBreached = 1) AS SlaBreaches,
        (SELECT COUNT(*) FROM dbo.AtmMaster WHERE Status IN (2, 3) /* Inactive, UnderMaintenance */) AS AtmDownCount,
        (SELECT COUNT(*) FROM dbo.Tickets WHERE StatusId IN (SELECT StatusId FROM @OpenStatusIds) AND Priority = 1) AS CriticalOpenTickets;
END
GO
