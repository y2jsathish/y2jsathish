using Microsoft.AspNetCore.Identity;

namespace ATMTicketing.Domain.Entities;

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }

    public ApplicationRole() { }

    public ApplicationRole(string roleName, string? description = null) : base(roleName)
    {
        Description = description;
    }
}

/// <summary>Canonical role names used across authorization policies and seed data.</summary>
public static class Roles
{
    public const string Administrator = "Administrator";
    public const string CallCenterAgent = "CallCenterAgent";
    public const string FieldEngineer = "FieldEngineer";
    public const string TeamLead = "TeamLead";
    public const string OperationsManager = "OperationsManager";

    public static readonly string[] All =
    {
        Administrator, CallCenterAgent, FieldEngineer, TeamLead, OperationsManager
    };
}
