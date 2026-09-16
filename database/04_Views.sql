/* =====================================================================
   04_Views.sql — Reporting/read views used by ad-hoc queries, BI tools,
   and as a documented equivalent to the EF Core projections in
   ATMTicketing.Infrastructure.Services.ReportService / DashboardService.
   ===================================================================== */

CREATE OR ALTER VIEW dbo.vw_TicketFullDetails
AS
SELECT
    t.Id,
    t.TicketNumber,
    a.AtmCode,
    a.AtmName,
    a.BankName,
    r.RegionName,
    a.Zone,
    a.State,
    a.City,
    c.Name              AS CategoryName,
    t.SubCategory,
    t.Priority,
    CASE t.Priority WHEN 1 THEN 'Critical' WHEN 2 THEN 'High' WHEN 3 THEN 'Medium' WHEN 4 THEN 'Low' END AS PriorityName,
    sm.Code             AS StatusCode,
    sm.DisplayName      AS StatusName,
    t.Description,
    t.ContactPerson,
    t.ContactNumber,
    cu.FullName         AS CreatedByName,
    au.FullName         AS AssignedToName,
    v.VendorName,
    t.CreatedDate,
    t.ResponseDueAt,
    t.ResolutionDueAt,
    t.RespondedAt,
    t.ResolvedAt,
    t.ClosedAt,
    t.IsResponseBreached,
    t.IsResolutionBreached,
    t.IsEscalated
FROM dbo.Tickets t
JOIN dbo.AtmMaster a       ON a.Id = t.AtmId
JOIN dbo.RegionMaster r    ON r.Id = t.RegionId
JOIN dbo.CategoryMaster c  ON c.Id = t.CategoryId
JOIN dbo.StatusMaster sm   ON sm.Id = t.StatusId
JOIN dbo.Users cu          ON cu.Id = t.CreatedById
LEFT JOIN dbo.Users au     ON au.Id = t.AssignedToId
LEFT JOIN dbo.VendorMaster v ON v.Id = t.AssignedVendorId;
GO

CREATE OR ALTER VIEW dbo.vw_OpenTickets
AS
SELECT *
FROM dbo.vw_TicketFullDetails
WHERE StatusCode NOT IN ('RESOLVED', 'CLOSED', 'CANCELLED');
GO

CREATE OR ALTER VIEW dbo.vw_SlaCompliance
AS
SELECT
    PriorityName,
    COUNT(*)                                              AS TotalTickets,
    SUM(CASE WHEN IsResolutionBreached = 0 THEN 1 ELSE 0 END) AS MetSla,
    SUM(CASE WHEN IsResolutionBreached = 1 THEN 1 ELSE 0 END) AS Breached,
    CAST(100.0 * SUM(CASE WHEN IsResolutionBreached = 0 THEN 1 ELSE 0 END) / NULLIF(COUNT(*), 0) AS DECIMAL(5,1)) AS CompliancePercent
FROM dbo.vw_TicketFullDetails
WHERE StatusCode IN ('RESOLVED', 'CLOSED')
GROUP BY PriorityName;
GO

CREATE OR ALTER VIEW dbo.vw_EngineerWorkload
AS
SELECT
    u.Id            AS EngineerId,
    u.FullName      AS EngineerName,
    COUNT(*)        AS OpenTickets,
    SUM(CASE WHEN t.StatusId = 3 THEN 1 ELSE 0 END) AS InProgressTickets
FROM dbo.Tickets t
JOIN dbo.Users u ON u.Id = t.AssignedToId
WHERE t.StatusId NOT IN (7, 8, 9) -- exclude Resolved/Closed/Cancelled
GROUP BY u.Id, u.FullName;
GO

CREATE OR ALTER VIEW dbo.vw_AtmDowntime
AS
SELECT
    a.Id            AS AtmId,
    a.AtmCode,
    a.AtmName,
    r.RegionName,
    t.Id            AS TicketId,
    t.CreatedDate    AS DownSince,
    COALESCE(t.ClosedAt, t.ResolvedAt) AS RestoredAt,
    DATEDIFF(MINUTE, t.CreatedDate, COALESCE(t.ClosedAt, t.ResolvedAt, SYSUTCDATETIME())) AS DowntimeMinutes
FROM dbo.Tickets t
JOIN dbo.AtmMaster a ON a.Id = t.AtmId
JOIN dbo.RegionMaster r ON r.Id = a.RegionId
JOIN dbo.CategoryMaster c ON c.Id = t.CategoryId
WHERE c.Name = 'ATM Down';
GO
