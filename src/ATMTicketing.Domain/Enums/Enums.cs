namespace ATMTicketing.Domain.Enums;

/// <summary>Fixed set of ticket priorities; matches one row each in SlaConfiguration.</summary>
public enum PriorityLevel
{
    Critical = 1,
    High = 2,
    Medium = 3,
    Low = 4
}

/// <summary>
/// Well-known ticket workflow states. Values are seeded 1:1 into StatusMaster.Id so the DB
/// keeps referential integrity on Ticket.StatusId while the app can branch on the enum.
/// </summary>
public enum TicketStatusCode
{
    New = 1,
    Assigned = 2,
    InProgress = 3,
    PendingParts = 4,
    PendingCustomer = 5,
    Escalated = 6,
    Resolved = 7,
    Closed = 8,
    Cancelled = 9
}

public enum AtmOperationalStatus
{
    Active = 1,
    Inactive = 2,
    UnderMaintenance = 3,
    Decommissioned = 4
}

public enum AtmType
{
    Onsite = 1,
    Offsite = 2,
    CashRecycler = 3,
    CashDeposit = 4,
    MiniBranch = 5
}

public enum AssignmentType
{
    Auto = 1,
    Manual = 2,
    Reassigned = 3
}

public enum NotificationChannel
{
    Email = 1,
    Sms = 2,
    InApp = 3
}

public enum NotificationEvent
{
    TicketCreated = 1,
    TicketAssigned = 2,
    TicketEscalated = 3,
    TicketResolved = 4,
    TicketClosed = 5,
    SlaBreachWarning = 6,
    SlaBreached = 7
}

public enum TicketHistoryAction
{
    Created = 1,
    StatusChanged = 2,
    Assigned = 3,
    Reassigned = 4,
    Commented = 5,
    AttachmentAdded = 6,
    Escalated = 7,
    Resolved = 8,
    Closed = 9,
    Cancelled = 10
}
