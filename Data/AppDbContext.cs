using FoodDonationPlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace FoodDonationPlatform.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Email).IsUnique();
        b.Entity<User>().Property(u => u.Role).HasConversion<string>();
        b.Entity<Donation>().Property(d => d.Status).HasConversion<string>();
        b.Entity<Donation>().HasOne(d => d.Donor).WithMany().HasForeignKey(d => d.DonorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Donation>().HasOne(d => d.Ngo).WithMany().HasForeignKey(d => d.NgoId).OnDelete(DeleteBehavior.Restrict);
    }
}
