using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Infrastructure.Persistence.Configurations;

internal sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    /// <summary>
    /// Shadow property (in the database only, not in the entity): the text that free-text search scans.
    /// </summary>
    public const string SearchText = nameof(SearchText);

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

        // Indexes for the main search, filter and aggregation scenarios – each one measured on 100,000
        // requests (docs/PERFORMANCE.md). The list is always ordered by "CreatedAt DESC, Id DESC", so the
        // indexes end with exactly that order: SQL Server then reads the first page in index order and stops
        // after 20 rows, instead of sorting every matching row. (Without the explicit Id DESC the index would
        // carry Id ascending, which does not match the ORDER BY, and the optimizer fell back to a full scan.)

        // Free-text search ("contains" in the title or the organization name). LIKE '%term%' cannot seek, so it
        // scans; this makes the scan cheap (measured in docs/PERFORMANCE.md):
        // * one persisted computed column, so SQL Server keeps it in sync by itself (it only changes if the
        //   title or the organization changes, which never happens here);
        // * a binary collation: comparing bytes instead of applying linguistic rules is most of the gain;
        // * UPPER on both sides keeps the search case-insensitive for Latin letters (Hebrew has no case);
        // * a separator that cannot be typed, so a term never matches across the title/organization boundary.
        builder.Property<string>(SearchText)
            .HasComputedColumnSql(
                "CAST(UPPER([Title] + NCHAR(31) + [OrganizationName]) AS nvarchar(401)) COLLATE Latin1_General_100_BIN2",
                stored: true);

        // Default list (no filter), newest first; date-range filters. SearchText is included, so a search reads
        // this index in list order and checks the text in the index itself: the first page stops after 20
        // matches, a rare term scans only this index (no row lookups), and the COUNT scans it too.
        builder.HasIndex(r => new { r.CreatedAt, r.Id }, "IX_Requests_CreatedAt_Id")
            .IsDescending(true, true)
            .IncludeProperties(SearchText);

        // Status filter in list order; covers the per-status count and the summary (Priority included).
        builder.HasIndex(r => new { r.Status, r.CreatedAt, r.Id }, "IX_Requests_Status_CreatedAt_Id")
            .IsDescending(false, true, true)
            .IncludeProperties(r => r.Priority);

        // "Requests of handler X" in list order; Status included for the handler + status filter.
        // Also the distinct handlers lookup.
        builder.HasIndex(r => new { r.AssignedTo, r.CreatedAt, r.Id }, "IX_Requests_AssignedTo_CreatedAt_Id")
            .IsDescending(false, true, true)
            .IncludeProperties(r => r.Status);

        // Organization filter (exact name or prefix) in list order; also the organizations lookup.
        builder.HasIndex(r => new { r.OrganizationName, r.CreatedAt, r.Id }, "IX_Requests_OrganizationName_CreatedAt_Id")
            .IsDescending(false, true, true);

        // Priority filter and the per-priority aggregation.
        builder.HasIndex(r => new { r.Priority, r.Status }, "IX_Requests_Priority_Status");
    }
}
