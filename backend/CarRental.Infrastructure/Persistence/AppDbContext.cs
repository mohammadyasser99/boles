using CarRental.Domain.Entities;
using CarRental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<Fine> Fines => Set<Fine>();
    public DbSet<EntranceFee> EntranceFees => Set<EntranceFee>();
    public DbSet<UserDocument> UserDocuments => Set<UserDocument>();

 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Admin ───────────────────────────────────────────────────────────
        modelBuilder.Entity<Admin>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Name).IsRequired().HasMaxLength(200);
            e.Property(a => a.Username).IsRequired().HasMaxLength(100);
            e.HasIndex(a => a.Username).IsUnique();
            e.Property(a => a.PasswordHash).IsRequired();
            e.Property(a => a.Role)
             .IsRequired()
             .HasConversion<string>()
             .HasMaxLength(20);
            e.Property(a => a.RefreshToken).HasMaxLength(512);
        });

        // ── User ────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).IsRequired().HasMaxLength(200);
            e.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(30);
            e.Property(u => u.Email).IsRequired().HasMaxLength(200);
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.NationalId).IsUnique();
        });

        // ── Car ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<Car>(e =>
        {
            e.HasKey(c => c.CarPlate);
            e.Property(c => c.CarPlate).HasMaxLength(20);
            e.Property(c => c.RentalPrice).HasColumnType("decimal(18,2)");
            e.Property(c => c.ChassisNumber).HasMaxLength(17);
            e.HasIndex(u => u.ChassisNumber).IsUnique();
            e.HasOne(c => c.User)
             .WithMany(u => u.Cars)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Fine ────────────────────────────────────────────────────────────
        modelBuilder.Entity<Fine>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.ViolationNumber).IsRequired().HasMaxLength(100);
            e.HasIndex(f => f.ViolationNumber).IsUnique();
            e.Property(f => f.CarPlate).IsRequired().HasMaxLength(20);
            e.Property(f => f.Amount).HasColumnType("decimal(18,2)");
            e.Property(f => f.Description).HasMaxLength(500);

            e.HasOne(f => f.Car)
             .WithMany(c => c.Fines)
             .HasForeignKey(f => f.CarPlate)
             .HasPrincipalKey(c => c.CarPlate)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── EntranceFee ─────────────────────────────────────────────────────────
        modelBuilder.Entity<EntranceFee>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.TripNumber).IsRequired().HasMaxLength(100);
            e.HasIndex(f => f.TripNumber).IsUnique();
            e.Property(f => f.CarPlate).IsRequired().HasMaxLength(20);
            e.Property(f => f.Amount).HasColumnType("decimal(18,2)");
            e.Property(f => f.GateName).HasMaxLength(200);
            e.Property(f => f.Direction).HasMaxLength(100);

            e.HasOne(f => f.Car)
             .WithMany(c => c.EntranceFees)
             .HasForeignKey(f => f.CarPlate)
             .HasPrincipalKey(c => c.CarPlate)
             .OnDelete(DeleteBehavior.Cascade);
        });
        // ── UserDocument ─────────────────────────────────────────────────────────
        modelBuilder.Entity<UserDocument>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.FileName).IsRequired().HasMaxLength(300);
            e.Property(d => d.StoredFileName).IsRequired().HasMaxLength(300);
            e.Property(d => d.FilePath).IsRequired().HasMaxLength(500);
            e.Property(d => d.ContentType).IsRequired().HasMaxLength(100);
            e.Property(d => d.DocumentType)
             .IsRequired()
             .HasConversion<string>()
             .HasMaxLength(30);

            e.HasOne(d => d.User)
 .WithMany(u => u.Documents)
 .HasForeignKey(d => d.UserId)
 .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(d => new { d.UserId, d.DocumentType });
        });

    }
}
