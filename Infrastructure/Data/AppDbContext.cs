using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using platform_ai_backend_netcore.Domain.Entities;

namespace platform_ai_backend_netcore.Infrastructure.Data
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
        public DbSet<Partner> Partners => Set<Partner>();
        public DbSet<PartnerService> PartnerServices => Set<PartnerService>();
        public DbSet<Service> Services => Set<Service>();
        public DbSet<TokenTransaction> TokenTransactions => Set<TokenTransaction>();
        public DbSet<Payment> Payments => Set<Payment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── User ─────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Status).HasDefaultValue("active");
            e.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            e.Property(u => u.UpdatedAt).HasDefaultValueSql("now()");

            e.HasIndex(u => u.FirebaseUid)
             .IsUnique()
             .HasDatabaseName("idx_users_firebase_uid");

            e.HasIndex(u => u.Status)
             .HasDatabaseName("idx_users_status");

            e.HasMany(u => u.Sessions)
             .WithOne(s => s.User)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserSession ───────────────────────────────────────
        modelBuilder.Entity<UserSession>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.IsRevoked).HasDefaultValue(false);
            e.Property(s => s.CreatedAt).HasDefaultValueSql("now()");

            e.HasIndex(s => s.UserId)
             .HasDatabaseName("idx_user_sessions_user_id");

            e.HasIndex(s => new { s.UserId, s.IsRevoked })
             .HasDatabaseName("idx_user_sessions_active");

            e.HasIndex(s => s.ExpiresAt)
             .HasDatabaseName("idx_user_sessions_expiry");
        });

        // ── GuestSession ──────────────────────────────────────
        modelBuilder.Entity<GuestSession>(e =>
        {
            e.HasKey(gs => gs.Id);
            e.Property(gs => gs.Status).HasDefaultValue("active");
            e.Property(gs => gs.CreatedAt).HasDefaultValueSql("now()");

            e.HasIndex(gs => gs.AnonymousId)
             .IsUnique()
             .HasDatabaseName("idx_guest_sessions_anon_id");

            e.HasIndex(gs => gs.PartnerServiceId)
             .HasDatabaseName("idx_guest_sessions_ps_id");

            e.HasIndex(gs => gs.ExpiresAt)
             .HasDatabaseName("idx_guest_sessions_expiry");

            e.HasIndex(gs => gs.Status)
             .HasDatabaseName("idx_guest_sessions_status");

            e.HasOne(gs => gs.PartnerService)
             .WithMany()
             .HasForeignKey(gs => gs.PartnerServiceId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(gs => gs.ConvertedUser)
             .WithMany()
             .HasForeignKey(gs => gs.ConvertedUserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Partner ───────────────────────────────────────────
        modelBuilder.Entity<Partner>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Status).HasDefaultValue("pending");
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

            e.HasIndex(p => p.OwnerUserId)
             .HasDatabaseName("idx_partners_owner");

            e.HasIndex(p => p.Status)
             .HasDatabaseName("idx_partners_status");

            e.HasOne(p => p.OwnerUser)
             .WithMany()
             .HasForeignKey(p => p.OwnerUserId)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasMany(p => p.PartnerServices)
             .WithOne(ps => ps.Partner)
             .HasForeignKey(ps => ps.PartnerId);

            e.HasMany(p => p.TokenTransactions)
             .WithOne()
             .HasForeignKey(t => t.PartnerId);

            e.HasMany(p => p.Payments)
             .WithOne()
             .HasForeignKey(pay => pay.PartnerId);
        });

        // ── ServiceEntity ─────────────────────────────────────
        modelBuilder.Entity<Service>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Status).HasDefaultValue("active");
            e.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
            e.Property(s => s.UpdatedAt).HasDefaultValueSql("now()");

            e.HasIndex(s => s.Type)
             .HasDatabaseName("idx_services_type");

            e.HasIndex(s => s.Status)
             .HasDatabaseName("idx_services_status");
        });

        // ── PartnerServiceEntity ──────────────────────────────
        modelBuilder.Entity<PartnerService>(e =>
        {
            e.HasKey(ps => ps.Id);
            e.Property(ps => ps.TokenUsed).HasDefaultValue(0);
            e.Property(ps => ps.Status).HasDefaultValue("active");
            e.Property(ps => ps.CreatedAt).HasDefaultValueSql("now()");
            e.Property(ps => ps.UpdatedAt).HasDefaultValueSql("now()");

            e.Property(ps => ps.AvailableSchedule)
             .HasColumnType("json");

            e.Property(ps => ps.Config)
             .HasColumnType("jsonb");

            e.HasIndex(ps => new { ps.PartnerId, ps.ServiceId })
             .IsUnique()
             .HasDatabaseName("uq_partner_service");

            e.HasIndex(ps => ps.PartnerId)
             .HasDatabaseName("idx_ps_partner_id");

            e.HasIndex(ps => ps.ServiceId)
             .HasDatabaseName("idx_ps_service_id");

            e.HasIndex(ps => ps.Status)
             .HasDatabaseName("idx_ps_status");

            e.HasOne(ps => ps.Service)
             .WithMany()
             .HasForeignKey(ps => ps.ServiceId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ── TokenTransaction ──────────────────────────────────
        modelBuilder.Entity<TokenTransaction>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            e.Property(t => t.Cost).HasColumnType("numeric(10,4)");

            e.HasIndex(t => t.PartnerId)
             .HasDatabaseName("idx_token_tx_partner");

            e.HasIndex(t => t.PartnerServiceId)
             .HasDatabaseName("idx_token_tx_ps");

            e.HasIndex(t => t.CreatedAt)
             .HasDatabaseName("idx_token_tx_created");
        });

        // ── Payment ───────────────────────────────────────────
        modelBuilder.Entity<Payment>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Status).HasDefaultValue("pending");
            e.Property(p => p.Amount).HasColumnType("numeric(10,2)");
            e.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");

            e.HasIndex(p => p.PartnerId)
             .HasDatabaseName("idx_payments_partner");

            e.HasIndex(p => p.Status)
             .HasDatabaseName("idx_payments_status");
        });
        }
    }
}