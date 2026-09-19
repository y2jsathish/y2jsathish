using System.ComponentModel.DataAnnotations;

namespace ATMTicketing.Application.DTOs;

public class RegionListItemDto
{
    public int Id { get; set; }
    public string RegionName { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int AtmCount { get; set; }
    public int VendorCount { get; set; }
    public int UserCount { get; set; }
}

public class RegionEditDto
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string RegionName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Zone { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
