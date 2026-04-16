using Microsoft.EntityFrameworkCore;
using Notification.Persistence.Entities;

namespace Notification.Persistence;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : DbContext(options)
{
    public DbSet<TenantEntity> Tenants { get; set; }
    public DbSet<NotificationTemplateEntity> Templates { get; set; }
    public DbSet<AdminUserEntity> AdminUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── Tenants ───────────────────────────────────────────────────────────
        mb.Entity<TenantEntity>(b =>
        {
            b.ToTable("tenants");
            b.HasKey(t => t.Id);
            b.Property(t => t.Id).ValueGeneratedOnAdd();
            b.Property(t => t.ClientId).HasMaxLength(100).IsRequired();
            b.Property(t => t.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(t => t.EmailProvider).HasMaxLength(50).HasDefaultValue("SendGrid");
            b.Property(t => t.PushProvider).HasMaxLength(50).HasDefaultValue("Firebase");

            // JSONB column for Dictionary<string, string>
            b.Property(t => t.ProviderSettings)
             .HasColumnType("jsonb")
             .HasDefaultValueSql("'{}'::jsonb");

            b.Property(t => t.RateLimitPerMinute).HasDefaultValue(100);
            b.Property(t => t.IsActive).HasDefaultValue(true);
            b.Property(t => t.ApiKeyHash).HasMaxLength(64);
            b.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            b.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");

            b.HasIndex(t => t.ClientId)
             .IsUnique()
             .HasDatabaseName("ix_tenants_client_id");

            b.HasIndex(t => t.ApiKeyHash)
             .IsUnique()
             .HasDatabaseName("ix_tenants_api_key_hash")
             .HasFilter("\"ApiKeyHash\" IS NOT NULL");
        });

        // ── Admin Users ───────────────────────────────────────────────────────
        mb.Entity<AdminUserEntity>(b =>
        {
            b.ToTable("admin_users");
            b.HasKey(u => u.Id);
            b.Property(u => u.Username).HasMaxLength(100).IsRequired();
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("admin");
            b.Property(u => u.IsActive).HasDefaultValue(true);
            b.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
            b.HasIndex(u => u.Username).IsUnique().HasDatabaseName("ix_admin_users_username");
        });

        // ── Notification Templates ────────────────────────────────────────────
        mb.Entity<NotificationTemplateEntity>(b =>
        {
            b.ToTable("notification_templates");
            b.HasKey(t => t.Id);
            b.Property(t => t.ClientId).HasMaxLength(100).IsRequired();
            b.Property(t => t.TemplateName).HasMaxLength(200).IsRequired();
            b.Property(t => t.Channel).HasMaxLength(20).IsRequired();
            b.Property(t => t.Language).HasMaxLength(10).HasDefaultValue("en");
            b.Property(t => t.Content).IsRequired();
            b.Property(t => t.IsActive).HasDefaultValue(true);
            b.Property(t => t.Version).HasDefaultValue(1);
            b.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");
            b.Property(t => t.UpdatedAt).HasDefaultValueSql("NOW()");

            // Unique constraint: one template per tenant/name/channel/language combo
            b.HasIndex(t => new { t.ClientId, t.TemplateName, t.Channel, t.Language })
             .IsUnique()
             .HasDatabaseName("ix_templates_lookup");

            // ClientId = "default" does not reference an actual client row, so no strict FK.
        });
    }
}
