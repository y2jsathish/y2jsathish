using System.ComponentModel.DataAnnotations;

namespace ATMTicketing.Application.DTOs;

public class UserListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? RegionName { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public DateTime? LastLoginDate { get; set; }
}

public class UserCreateDto
{
    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string? EmployeeCode { get; set; }

    public int? RegionId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;
}

public class UserEditDto
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? EmployeeCode { get; set; }

    public int? RegionId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class ChangePasswordDto
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;
}
