using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Repositories;
using LegalScraper.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.Extensions.Logging;

namespace LegalScraper.Infrastructure.Persistence;

public class ProcessoRepository : IProcessoRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<ProcessoRepository> _logger;

    public ProcessoRepository(AppDbContext context, ILogger<ProcessoRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Processo>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Processos
            .Include(p => p.Partes)
            .Include(p => p.Andamentos)
            .ToListAsync(cancellationToken);
    }

    public async Task<Processo?> GetByNumeroAsync(string numero, CancellationToken cancellationToken)
    {
        return await _context.Processos
            .Include(p => p.Partes)
            .Include(p => p.Andamentos)
            .FirstOrDefaultAsync(p => p.Numero == numero, cancellationToken);
    }

    public async Task AddOrUpdateAsync(Processo processo, CancellationToken cancellationToken)
    {
        var existingProcesso = await GetByNumeroAsync(processo.Numero, cancellationToken);

        if (existingProcesso != null)
        {
            // Update scalar properties only when provided (avoid overwriting with nulls/defaults)
            existingProcesso.Classe = processo.Classe ?? existingProcesso.Classe;
            existingProcesso.Assunto = processo.Assunto ?? existingProcesso.Assunto;
            existingProcesso.Foro = processo.Foro ?? existingProcesso.Foro;
            existingProcesso.DataDistribuicao = processo.DataDistribuicao != default ? processo.DataDistribuicao : existingProcesso.DataDistribuicao;
            existingProcesso.PdfConteudo = processo.PdfConteudo ?? existingProcesso.PdfConteudo;
            existingProcesso.PdfNome = processo.PdfNome ?? existingProcesso.PdfNome;

            // Synchronize Partes
            var existingPartes = existingProcesso.Partes?.ToList() ?? new List<Parte>();
            var incomingPartes = processo.Partes?.ToList() ?? new List<Parte>();

            var existingPartesById = existingPartes.ToDictionary(p => p.Id);
            var incomingIds = incomingPartes.Where(p => p.Id != Guid.Empty).Select(p => p.Id).ToHashSet();

            // Remove partes that are not in incoming list
            var toRemovePartes = existingPartes.Where(p => !incomingIds.Contains(p.Id)).ToList();
            if (toRemovePartes.Count > 0) _context.Partes.RemoveRange(toRemovePartes);

            // Add or update incoming partes
            foreach (var parte in incomingPartes)
            {
                if (parte.Id == Guid.Empty || !existingPartesById.ContainsKey(parte.Id))
                {
                    if (parte.Id == Guid.Empty) parte.Id = Guid.NewGuid();
                    parte.ProcessoId = existingProcesso.Id;
                    _context.Partes.Add(parte);
                }
                else
                {
                    var existing = existingPartesById[parte.Id];
                    existing.Nome = parte.Nome ?? existing.Nome;
                    existing.TipoParte = parte.TipoParte ?? existing.TipoParte;
                    _context.Entry(existing).State = EntityState.Modified;
                }
            }

            // Synchronize Andamentos
            var existingAndamentos = existingProcesso.Andamentos?.ToList() ?? new List<Andamento>();
            var incomingAndamentos = processo.Andamentos?.ToList() ?? new List<Andamento>();

            var existingAndById = existingAndamentos.ToDictionary(a => a.Id);
            var incomingAndIds = incomingAndamentos.Where(a => a.Id != Guid.Empty).Select(a => a.Id).ToHashSet();

            var toRemoveAnd = existingAndamentos.Where(a => !incomingAndIds.Contains(a.Id)).ToList();
            if (toRemoveAnd.Count > 0) _context.Andamentos.RemoveRange(toRemoveAnd);

            foreach (var andamento in incomingAndamentos)
            {
                if (andamento.Id == Guid.Empty || !existingAndById.ContainsKey(andamento.Id))
                {
                    if (andamento.Id == Guid.Empty) andamento.Id = Guid.NewGuid();
                    andamento.ProcessoId = existingProcesso.Id;
                    _context.Andamentos.Add(andamento);
                }
                else
                {
                    var existing = existingAndById[andamento.Id];
                    existing.Data = andamento.Data ?? existing.Data;
                    existing.Descricao = andamento.Descricao ?? existing.Descricao;
                    _context.Entry(existing).State = EntityState.Modified;
                }
            }

            _context.Entry(existingProcesso).State = EntityState.Modified;
        }
        else
        {
            // New processo: ensure child FK and Ids
            var partes = processo.Partes?.ToList() ?? new List<Parte>();
            var andamentos = processo.Andamentos?.ToList() ?? new List<Andamento>();

            processo.Partes = new List<Parte>();
            processo.Andamentos = new List<Andamento>();

            foreach (var parte in partes)
            {
                if (parte.Id == Guid.Empty) parte.Id = Guid.NewGuid();
                parte.ProcessoId = processo.Id;
                processo.Partes.Add(parte);
            }

            foreach (var andamento in andamentos)
            {
                if (andamento.Id == Guid.Empty) andamento.Id = Guid.NewGuid();
                andamento.ProcessoId = processo.Id;
                processo.Andamentos.Add(andamento);
            }

            await _context.Processos.AddAsync(processo, cancellationToken);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // Persist changes using EF Core transaction
        var processoEntries = _context.ChangeTracker
            .Entries<Processo>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .ToList();

        if (!processoEntries.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency exception during SaveChangesAsync. Entries: {Count}", ex.Entries?.Count ?? 0);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during SaveChangesAsync, rolling back transaction.");
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
