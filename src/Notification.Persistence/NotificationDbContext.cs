using Microsoft.EntityFrameworkCore;
using Notification.Persistence.Entities;

namespace Notification.Persistence;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    public DbSet<TenantEntity> Tenants { get; set; }
    public DbSet<NotificationTemplateEntity> Templates { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Tenants ───────────────────────────────────────────────────────────
        mb.Entity<TenantEntity>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.TenantId);
            b.Property(t => t.TenantId).HasMaxLength(100);
            b.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(t => t.EmailProvider).HasMaxLength(50).HasDefaultValue("SendGrid");
            b.Property(t => t.PushProvider).HasMaxLength(50).HasDefaultValue("Firebase");

            // JSONB column for Dictionary<string, string>
            b.Property(t => t.ProviderSettings)
             .HasColumnType("jsonb")
             .HasDefaultValueSql("'{}'::jsonb");

            b.Property(t => t.RateLimitPerMinute).HasDefaultValue(100);
            b.Property(t => t.IsActive).HasDefaultValue(true);
            b.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            b.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");
        });

        // ── Notification Templates ────────────────────────────────────────────
        mb.Entity<NotificationTemplateEntity>(b =>
        {
            b.ToTable("notification_templates");
            b.HasKey(t => t.Id);
            b.Property(t => t.TenantId).HasMaxLength(100).IsRequired();
            b.Property(t => t.TemplateName).HasMaxLength(200).IsRequired();
            b.Property(t => t.Channel).HasMaxLength(20).IsRequired();
            b.Property(t => t.Language).HasMaxLength(10).HasDefaultValue("en");
            b.Property(t => t.Content).IsRequired();
            b.Property(t => t.IsActive).HasDefaultValue(true);
            b.Property(t => t.Version).HasDefaultValue(1);
            b.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            b.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");

            // Unique constraint: one template per tenant/name/channel/language combo
            b.HasIndex(t => new { t.TenantId, t.TemplateName, t.Channel, t.Language })
             .IsUnique()
             .HasDatabaseName("ix_templates_lookup");

            // FK: templates belong to a tenant
            // TenantId = "default" does NOT reference an actual tenant row → no strict FK
            // so we omit HasForeignKey here to allow "default" as a virtual tenant
        });
    }
}
