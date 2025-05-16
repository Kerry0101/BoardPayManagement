using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Models
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Building> Buildings { get; set; }
        public DbSet<Floor> Floors { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Bill> Bills { get; set; }
        public DbSet<Contract> Contracts { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MeterReading> MeterReadings { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure relationships
            builder.Entity<Floor>()
                .HasOne(f => f.Building)
                .WithMany(b => b.Floors)
                .HasForeignKey(f => f.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Room>()
                .HasOne(r => r.Floor)
                .WithMany(f => f.Rooms)
                .HasForeignKey(r => r.FloorId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ApplicationUser>()
                .HasOne(u => u.Building)
                .WithMany(b => b.Tenants)
                .HasForeignKey(u => u.BuildingId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Room>()
                .HasOne(r => r.CurrentTenant)
                .WithOne(u => u.CurrentRoom)
                .HasForeignKey<ApplicationUser>(u => u.RoomId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Bills relationships
            builder.Entity<Bill>()
                .HasOne(b => b.Tenant)
                .WithMany()
                .HasForeignKey(b => b.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Bill>()
                .HasOne(b => b.Room)
                .WithMany()
                .HasForeignKey(b => b.RoomId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Contracts relationships
            builder.Entity<Contract>()
                .HasOne(c => c.Tenant)
                .WithMany()
                .HasForeignKey(c => c.TenantId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Contract>()
                .HasOne(c => c.Room)
                .WithMany()
                .HasForeignKey(c => c.RoomId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure MeterReadings relationships
            builder.Entity<MeterReading>()
                .HasOne(m => m.Tenant)
                .WithMany()
                .HasForeignKey(m => m.TenantId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<MeterReading>()
                .HasOne(m => m.Room)
                .WithMany()
                .HasForeignKey(m => m.RoomId)
                .OnDelete(DeleteBehavior.NoAction);
            builder.Entity<MeterReading>()
                .HasOne(m => m.Bill)
                .WithMany()
                .HasForeignKey(m => m.BillId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure decimal precision for Room custom fee properties
            builder.Entity<Room>()
                .Property(r => r.CustomMonthlyRent)
                .HasColumnType("decimal(18,2)");

            builder.Entity<Room>()
                .Property(r => r.CustomWaterFee)
                .HasColumnType("decimal(18,2)");

            builder.Entity<Room>()
                .Property(r => r.CustomElectricityFee)
                .HasColumnType("decimal(18,2)");

            builder.Entity<Room>()
                .Property(r => r.CustomWifiFee)
                .HasColumnType("decimal(18,2)");

            // Configure unique username constraint
            builder.Entity<ApplicationUser>()
                .HasIndex(u => u.UserName)
                .IsUnique();

            // Configure Notifications relationships
            builder.Entity<Notification>()
                .HasOne(n => n.User)
                .WithMany()
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Notification>()
                .HasOne(n => n.Bill)
                .WithMany()
                .HasForeignKey(n => n.BillId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}