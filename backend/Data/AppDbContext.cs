using Microsoft.EntityFrameworkCore;
using Backend.Entities;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // E-posta adreslerinin veritabanında unique (benzersiz) olmasını sağlıyoruz
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Refresh Token tablosunda TokenHash alanı üzerinden hızlı arama yapmak (login/refresh işlemlerinde) için Index ekliyoruz
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.TokenHash);
    }
}
