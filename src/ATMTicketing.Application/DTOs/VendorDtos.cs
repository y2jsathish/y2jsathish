using System.ComponentModel.DataAnnotations;

namespace ATMTicketing.Application.DTOs;

public class VendorListItemDto
{
    public int Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ServiceRegionName { get; set; } = string.Empty;
    public int AtmCount { get; set; }
    public bool IsActive { get; set; }
}

public class VendorEditDto
{
    public int Id { get; set; }

    [Required, StringLength(20)]
    public string VendorCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string VendorName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, Phone, StringLength(20)]
    public string ContactNumber { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public int ServiceRegionId { get; set; }

    public bool IsActive { get; set; } = true;
}
