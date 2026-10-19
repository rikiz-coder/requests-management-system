using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("Requests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Title).HasMaxLength(RequestFieldLimits.TitleMaxLength).IsRequired();
        builder.Property(r => r.OrganizationName).HasMaxLength(RequestFieldLimits.OrganizationNameMaxLength).IsRequired();
        builder.Property(r => r.AssignedTo).HasMaxLength(RequestFieldLimits.PersonNameMaxLength);

        // Enums with a byte underlying type map to tinyint: narrow columns and narrow indexes.
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.Priority).IsRequired();

        // datetime2(3): millisecond precision is enough, and the value returned to the client
        // is exactly the value that was stored.
        builder.Property(r => r.CreatedAt).HasPrecision(3);
        builder.Property(r => r.UpdatedAt).HasPrecision(3);

        // SQL Server rowversion: updated by the database on every write; EF adds it to the
        // WHERE clause of every UPDATE, which is what enforces optimistic concurrency.
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasMany(r => r.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for the main search, filter and aggregation scenarios.
        // Each one is justified and measured in docs/PERFORMANCE.md.

        // Status filter + default sort (newest first), and the per-status aggregation.
        builder.HasIndex(r => new { r.Status, r.CreatedAt }, "IX_Requests_Status_CreatedAt")
            .IsDescending(false, true)
            .IncludeProperties(r => r.Priority);

        // Default list (no filter) sorted by creation date, and date-range filters.
        builder.HasIndex(r => r.CreatedAt, "IX_Requests_CreatedAt")
            .IsDescending();

        // "Requests of handler X", optionally by status.
        builder.HasIndex(r => new { r.AssignedTo, r.Status }, "IX_Requests_AssignedTo_Status");

        // Organization filter (equality / starts-with) and the organizations lookup.
        builder.HasIndex(r => r.OrganizationName, "IX_Requests_OrganizationName");

        // Priority filter and the per-priority aggregation.
        builder.HasIndex(r => new { r.Priority, r.Status }, "IX_Requests_Priority_Status");
    }
}
