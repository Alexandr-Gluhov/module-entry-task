using Microsoft.EntityFrameworkCore;
using ModuleEntryTask.Models;

namespace ModuleEntryTask.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Operation> Operations { get; set; } = null!;
    public DbSet<OperationEvent> OperationEvents { get; set; } = null!;
    public DbSet<SubmitIntent> SubmitIntents { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Operation>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Id)
                .ValueGeneratedNever();

            entity.Property(o => o.Amount)
                .HasPrecision(18, 2);

            entity.Property(o => o.Status)
                .HasConversion<string>();

            entity.Property(o => o.Currency)
                .HasConversion<string>();
        });

        modelBuilder.Entity<OperationEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type)
                .HasConversion<string>();

            entity.Property(e => e.FromStatus)
                .HasConversion<string>();

            entity.Property(e => e.ToStatus)
                .HasConversion<string>();

            entity.HasOne(e => e.Operation)
                .WithMany()
                .HasForeignKey(e => e.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubmitIntent>(entity =>
        {
            entity.HasKey(s => s.Id);

            // один активный intent на операцию
            entity.HasIndex(s => s.OperationId)
                .IsUnique();

            entity.HasOne(s => s.Operation)
                .WithOne()
                .HasForeignKey<SubmitIntent>(s => s.OperationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}