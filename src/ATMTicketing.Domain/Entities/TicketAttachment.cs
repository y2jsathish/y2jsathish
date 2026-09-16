namespace ATMTicketing.Domain.Entities;

public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }

    public string UploadedById { get; set; } = string.Empty;
    public ApplicationUser UploadedBy { get; set; } = null!;
    public DateTime UploadedDate { get; set; } = DateTime.UtcNow;
}
