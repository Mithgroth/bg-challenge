using Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Common;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.JobId);
            
            entity.Property(j => j.JobId)
                .IsRequired();
            
            entity.Property(j => j.Type)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(j => j.ImgUrl)
                .IsRequired()
                .HasMaxLength(2048);
            
            entity.Property(j => j.ResultFile)
                .IsRequired()
                .HasMaxLength(512);
            
            entity.Property(j => j.Status)
                .IsRequired()
                .HasConversion<string>();
            
            entity.Property(j => j.CreatedAt)
                .IsRequired();
            
            entity.Property(j => j.UpdatedAt)
                .IsRequired();

            entity.HasIndex(j => new { j.JobId, j.ResultFile })
                .IsUnique()
                .HasDatabaseName("IX_Jobs_JobId_ResultFile");
        });
    }
}