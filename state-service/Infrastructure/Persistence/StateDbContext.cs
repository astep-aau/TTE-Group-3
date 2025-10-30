using Microsoft.EntityFrameworkCore;
using StateService.Domain.Entities;
using StateService.Domain.Value;
using LiveTaskEntity = StateService.Domain.Entities.Task;

namespace StateService.Infrastructure.Persistence
{
    public class StateDbContext : DbContext
    {
        public StateDbContext(DbContextOptions<StateDbContext> options) : base(options) { }

        public DbSet<LiveTaskEntity> Tasks => Set<LiveTaskEntity>();
        public DbSet<ProcessLog> ProcessLogs => Set<ProcessLog>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LiveTaskEntity>(e =>
            {
                e.HasKey(t => t.Pid);
                e.Property(t => t.CurrentState)
                    .HasConversion<string>()
                    .IsRequired();
                e.Property(t => t.CreatedAt).IsRequired();
                e.Property(t => t.UpdatedAt).IsRequired();
                e.Property(t => t.RowVersion).IsRowVersion();
            });

            modelBuilder.Entity<ProcessLog>(e =>
            {
                e.HasKey(l => l.LogId);
                e.Property(l => l.Pid).IsRequired();
                e.Property(l => l.FinishedAt).IsRequired();
                e.Property(l => l.Status).HasMaxLength(32).IsRequired();
                e.Property(l => l.CorrelationId).HasMaxLength(64);
                e.HasIndex(l => l.Pid);
            });

            modelBuilder.Entity<OutboxMessage>(e =>
            {
                e.HasKey(o => o.Id);
                e.Property(o => o.Type).HasMaxLength(64).IsRequired();
                e.Property(o => o.Payload).IsRequired();
                e.Property(o => o.CorrelationId).HasMaxLength(64);
                e.HasIndex(o => new { o.ProcessedAt });
            });
        }
    }
}
