using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RequestsManagement.Application.Requests.Entities;

namespace RequestsManagement.Infrastructure.Persistence.Configurations;

internal sealed class RequestStatusHistoryConfiguration : IEntityTypeConfiguration<RequestStatusHistory>
{
    public void Configure(EntityTypeBuilder<RequestStatusHistory> builder)
    {
        builder.ToTable("RequestStatusHistory");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.PreviousStatus).IsRequired();
        builder.Property(h => h.NewStatus).IsRequired();
        builder.Property(h => h.ChangedAt).HasPrecision(3);
        builder.Property(h => h.ChangedBy).HasMaxLength(RequestFieldLimits.PersonNameMaxLength).IsRequired();

        // History of one request, newest first; also used to find "who changed it last" on a 409.
        builder.HasIndex(h => new { h.RequestId, h.ChangedAt }, "IX_RequestStatusHistory_RequestId_ChangedAt")
            .IsDescending(false, true);
    }
}
