using DZI_shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DZI_shared;

public class DziDbContext : DbContext
{
    public DziDbContext(DbContextOptions<DziDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ProcessedImage> Images => Set<ProcessedImage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // AppUserId 必须唯一，以便快速查询内部 UserId
        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.AppUserId)
            .IsUnique();

        modelBuilder.Entity<AppUser>()
            .HasMany(u => u.Images)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId);

        modelBuilder.Entity<ProcessedImage>()
            .Property(i => i.Status)
            .HasConversion<string>();
    }
}
