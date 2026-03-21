using LegalScraper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LegalScraper.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public DbSet<Processo> Processos { get; set; } = null!;
    public DbSet<Parte> Partes { get; set; } = null!;
    public DbSet<Andamento> Andamentos { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Processo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Numero).IsUnique(); // Numero must be unique
            
            entity.HasMany(e => e.Partes)
                  .WithOne(p => p.Processo)
                  .HasForeignKey(p => p.ProcessoId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasMany(e => e.Andamentos)
                  .WithOne(a => a.Processo)
                  .HasForeignKey(a => a.ProcessoId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Parte>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Andamento>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
        
        base.OnModelCreating(modelBuilder);
    }
}
