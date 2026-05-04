using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GoldBank.Core.Modules.Ekub.Domain.Entities;

namespace GoldBank.Core.Modules.Ekub.Infrastructure.Persistence;

public sealed class EkubInvitationEntityConfiguration : IEntityTypeConfiguration<EkubInvitation>
{
    public void Configure(EntityTypeBuilder<EkubInvitation> builder)
    {
        builder.ToTable("ekub_invitations");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.GroupId).IsRequired().HasColumnName("group_id");
        builder.Property(i => i.InviteePhone)
            .IsRequired().HasMaxLength(20).HasColumnName("invitee_phone");
        builder.Property(i => i.InviteeCustomerId).HasColumnName("invitee_customer_id");
        builder.Property(i => i.InviterCustomerId).IsRequired().HasColumnName("inviter_customer_id");
        builder.Property(i => i.Status)
            .IsRequired().HasConversion<string>().HasMaxLength(20).HasColumnName("status");
        builder.Property(i => i.ExpiresAt).HasColumnName("expires_at");
        builder.Property(i => i.RespondedAt).HasColumnName("responded_at");
        builder.Property(i => i.TenantId).IsRequired().HasColumnName("tenant_id");
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        // One outstanding invite per (group, phone)
        builder.HasIndex(i => new { i.GroupId, i.InviteePhone })
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName("ix_ekub_invitations_group_phone_pending_unique");

        builder.HasIndex(i => i.InviteePhone).HasDatabaseName("ix_ekub_invitations_invitee_phone");
        builder.HasIndex(i => i.InviteeCustomerId).HasDatabaseName("ix_ekub_invitations_invitee_customer");

        builder.Ignore(i => i.DomainEvents);
        builder.Ignore(i => i.Version);
    }
}
