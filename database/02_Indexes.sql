/* =====================================================================
   02_Indexes.sql — Additional non-clustered indexes for the query
   patterns the app relies on most (ticket lists, SLA scans, reporting).
   Unique/PK indexes are already created in 01_Schema.sql.
   ===================================================================== */

CREATE INDEX IX_AtmMaster_RegionId_Status ON dbo.AtmMaster (RegionId, Status);
CREATE INDEX IX_AtmMaster_VendorId ON dbo.AtmMaster (VendorId);
CREATE INDEX IX_AtmMaster_City ON dbo.AtmMaster (City);
GO

CREATE INDEX IX_Tickets_StatusId_Priority ON dbo.Tickets (StatusId, Priority) INCLUDE (TicketNumber, AtmId, ResolutionDueAt);
CREATE INDEX IX_Tickets_RegionId ON dbo.Tickets (RegionId);
CREATE INDEX IX_Tickets_CreatedDate ON dbo.Tickets (CreatedDate DESC);
CREATE INDEX IX_Tickets_AssignedToId ON dbo.Tickets (AssignedToId) WHERE AssignedToId IS NOT NULL;
CREATE INDEX IX_Tickets_ResolutionDueAt_Open ON dbo.Tickets (ResolutionDueAt) WHERE ResolvedAt IS NULL AND ClosedAt IS NULL;
CREATE INDEX IX_Tickets_IsResolutionBreached ON dbo.Tickets (IsResolutionBreached) WHERE IsResolutionBreached = 1;
GO

CREATE INDEX IX_TicketHistory_TicketId_ActionDate ON dbo.TicketHistory (TicketId, ActionDate DESC);
GO

CREATE INDEX IX_TicketAssignment_TicketId_IsCurrent ON dbo.TicketAssignment (TicketId, IsCurrent);
CREATE INDEX IX_TicketAssignment_AssignedToId ON dbo.TicketAssignment (AssignedToId) WHERE IsCurrent = 1;
GO

CREATE INDEX IX_Notification_UserId_IsRead ON dbo.Notification (UserId, IsRead);
GO

CREATE INDEX IX_AuditLog_Timestamp ON dbo.AuditLog (Timestamp DESC);
CREATE INDEX IX_AuditLog_EntityName_EntityId ON dbo.AuditLog (EntityName, EntityId);
GO
