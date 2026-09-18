using ATMTicketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ATMTicketing.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();
        builder.Property(u => u.EmployeeCode).HasMaxLength(30);

        builder.HasOne(u => u.Region)
            .WithMany(r => r.Users)
            .HasForeignKey(u => u.RegionId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class RegionConfiguration : IEntityTypeConfiguration<Region>
{
    public void Configure(EntityTypeBuilder<Region> builder)
    {
        builder.ToTable("RegionMaster");
        builder.Property(r => r.RegionName).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Zone).HasMaxLength(50).IsRequired();
        builder.HasIndex(r => r.RegionName).IsUnique();
    }
}

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("VendorMaster");
        builder.Property(v => v.VendorCode).HasMaxLength(20).IsRequired();
        builder.Property(v => v.VendorName).HasMaxLength(150).IsRequired();
        builder.Property(v => v.ContactPerson).HasMaxLength(100).IsRequired();
        builder.Property(v => v.ContactNumber).HasMaxLength(20).IsRequired();
        builder.Property(v => v.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(v => v.VendorCode).IsUnique();

        builder.HasOne(v => v.ServiceRegion)
            .WithMany(r => r.Vendors)
            .HasForeignKey(v => v.ServiceRegionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AtmConfiguration : IEntityTypeConfiguration<Atm>
{
    public void Configure(EntityTypeBuilder<Atm> builder)
    {
        builder.ToTable("AtmMaster");
        builder.Property(a => a.AtmCode).HasMaxLength(30).IsRequired();
        builder.Property(a => a.AtmName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.BankName).HasMaxLength(150).IsRequired();
        builder.Property(a => a.Zone).HasMaxLength(50).IsRequired();
        builder.Property(a => a.State).HasMaxLength(50).IsRequired();
        builder.Property(a => a.City).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Address).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Latitude).HasColumnType("decimal(9,6)");
        builder.Property(a => a.Longitude).HasColumnType("decimal(9,6)");
        builder.Property(a => a.AtmType).HasConversion<byte>();
        builder.Property(a => a.Status).HasConversion<byte>();
        builder.HasIndex(a => a.AtmCode).IsUnique();
        builder.HasIndex(a => new { a.RegionId, a.Status });

        builder.HasOne(a => a.Region)
            .WithMany(r => r.Atms)
            .HasForeignKey(a => a.RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Vendor)
            .WithMany(v => v.Atms)
            .HasForeignKey(a => a.VendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CategoryMasterConfiguration : IEntityTypeConfiguration<CategoryMaster>
{
    public void Configure(EntityTypeBuilder<CategoryMaster> builder)
    {
        builder.ToTable("CategoryMaster");
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(c => c.Name).IsUnique();
    }
}

public class StatusMasterConfiguration : IEntityTypeConfiguration<StatusMaster>
{
    public void Configure(EntityTypeBuilder<StatusMaster> builder)
    {
        builder.ToTable("StatusMaster");
        builder.Property(s => s.Code).HasMaxLength(30).IsRequired();
        builder.Property(s => s.DisplayName).HasMaxLength(50).IsRequired();
        builder.Property(s => s.ColorHex).HasMaxLength(10).IsRequired();
        builder.HasIndex(s => s.Code).IsUnique();
    }
}

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.Property(t => t.Priority).HasConversion<byte>();
        builder.Property(t => t.TicketNumber).HasMaxLength(30).IsRequired();
        builder.Property(t => t.IncidentType).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(2000).IsRequired();
        builder.Property(t => t.ContactPerson).HasMaxLength(100).IsRequired();
        builder.Property(t => t.ContactNumber).HasMaxLength(20).IsRequired();
        builder.Property(t => t.ResolutionNotes).HasMaxLength(2000);
        builder.Property(t => t.ClosureRemarks).HasMaxLength(1000);

        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.HasIndex(t => new { t.StatusId, t.Priority });
        builder.HasIndex(t => t.RegionId);
        builder.HasIndex(t => t.CreatedDate);
        builder.HasIndex(t => t.AssignedToId);

        builder.HasOne(t => t.Atm)
            .WithMany(a => a.Tickets)
            .HasForeignKey(t => t.AtmId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Status)
            .WithMany(s => s.Tickets)
            .HasForeignKey(t => t.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTickets)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedTo)
            .WithMany(u => u.AssignedTickets)
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedVendor)
            .WithMany(v => v.Tickets)
            .HasForeignKey(t => t.AssignedVendorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("TicketHistory");
        builder.Property(h => h.ActionType).HasConversion<byte>();
        builder.Property(h => h.OldValue).HasMaxLength(500);
        builder.Property(h => h.NewValue).HasMaxLength(500);
        builder.Property(h => h.Notes).HasMaxLength(2000);
        builder.HasIndex(h => h.TicketId);

        builder.HasOne(h => h.Ticket)
            .WithMany(t => t.History)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ActionBy)
            .WithMany()
            .HasForeignKey(h => h.ActionById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketAssignmentConfiguration : IEntityTypeConfiguration<TicketAssignment>
{
    public void Configure(EntityTypeBuilder<TicketAssignment> builder)
    {
        builder.ToTable("TicketAssignment");
        builder.Property(a => a.AssignmentType).HasConversion<byte>();
        builder.HasIndex(a => new { a.TicketId, a.IsCurrent });

        builder.HasOne(a => a.Ticket)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.AssignedTo)
            .WithMany()
            .HasForeignKey(a => a.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssignedBy)
            .WithMany()
            .HasForeignKey(a => a.AssignedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachment");
        builder.Property(a => a.FileName).HasMaxLength(260).IsRequired();
        builder.Property(a => a.StoredFilePath).HasMaxLength(500).IsRequired();
        builder.Property(a => a.FileType).HasMaxLength(50).IsRequired();

        builder.HasOne(a => a.Ticket)
            .WithMany(t => t.Attachments)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.UploadedBy)
            .WithMany()
            .HasForeignKey(a => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SlaConfigurationEntityConfiguration : IEntityTypeConfiguration<SlaConfiguration>
{
    public void Configure(EntityTypeBuilder<SlaConfiguration> builder)
    {
        builder.ToTable("SlaConfiguration");
        builder.Property(s => s.Priority).HasConversion<byte>();
        builder.HasIndex(s => s.Priority).IsUnique();
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.Property(n => n.Channel).HasConversion<byte>();
        builder.Property(n => n.Event).HasConversion<byte>();
        builder.Property(n => n.Subject).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(n => new { n.UserId, n.IsRead });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Ticket)
            .WithMany(t => t.Notifications)
            .HasForeignKey(n => n.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityId).HasMaxLength(50);
        builder.Property(a => a.IpAddress).HasMaxLength(50);
        builder.HasIndex(a => a.Timestamp);
    }
}
