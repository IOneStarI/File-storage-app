using FileStorage.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
namespace FileStorage.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options) {
    public DbSet<StoredFile> Files => Set<StoredFile>();
    /// <summary>
    /// Configures Identity tables, the file-owner relationship, and indexes for owner queries and unique stored names.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder builder) {
        base.OnModelCreating(builder);
        builder.Entity<StoredFile>().HasOne(f => f.Owner).WithMany().HasForeignKey(f => f.OwnerId).OnDelete(DeleteBehavior.Cascade);
        builder.Entity<StoredFile>().HasIndex(f => new { f.OwnerId, f.UploadedAt });
        builder.Entity<StoredFile>().HasIndex(f => f.StoredName).IsUnique();
    }
}
