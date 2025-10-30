using Microsoft.EntityFrameworkCore;
using translator_service.Domain.Entities;

namespace translator_service;

public class TranslatorDbContext : DbContext
{
    public TranslatorDbContext(DbContextOptions<TranslatorDbContext> options)
        : base(options) { }

    public DbSet<RouteResult> Routes { get; set; } = default!;
    public DbSet<RouteCoordinate> RouteCoordinates { get; set; } = default!;
    public DbSet<CreateProcessRequest> CreateProcessRequests { get; set; } = default!;
    public DbSet<TravelTimeRequest> TravelTimeRequests { get; set; } = default!;
    public DbSet<TravelTimeResult> TravelTimeResults { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RouteResult>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.HasMany(r => r.Path)
                .WithOne()
                .HasForeignKey(c => c.RouteResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RouteCoordinate>(entity =>
        {
            entity.HasKey(c => c.Id);
        });

        modelBuilder.Entity<TravelTimeRequest>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.CorrelationId).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
        });

        modelBuilder.Entity<TravelTimeResult>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.CorrelationId).IsRequired();
            entity.Property(t => t.CreatedAt).IsRequired();
        });
    }
}