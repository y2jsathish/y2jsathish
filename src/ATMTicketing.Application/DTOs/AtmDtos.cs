using System.ComponentModel.DataAnnotations;
using ATMTicketing.Domain.Enums;

namespace ATMTicketing.Application.DTOs;

public class AtmListItemDto
{
    public int Id { get; set; }
    public string AtmCode { get; set; } = string.Empty;
    public string AtmName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string RegionName { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string AtmType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int OpenTicketCount { get; set; }
}

public class AtmEditDto
{
    public int Id { get; set; }

    [Required, StringLength(30)]
    public string AtmCode { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string AtmName { get; set; } = string.Empty;

    [Required, StringLength(150)]
    public string BankName { get; set; } = string.Empty;

    [Required]
    public int RegionId { get; set; }

    [Required, StringLength(50)]
    public string Zone { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string State { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string City { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required]
    public int VendorId { get; set; }

    [Required]
    public AtmType AtmType { get; set; }

    [Required]
    public AtmOperationalStatus Status { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }
}

public class AtmImportRowResult
{
    public int RowNumber { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string AtmCode { get; set; } = string.Empty;
}
