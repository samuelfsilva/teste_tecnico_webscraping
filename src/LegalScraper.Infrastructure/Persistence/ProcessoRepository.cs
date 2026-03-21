using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Application.Interfaces;
using LegalScraper.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LegalScraper.Infrastructure.Persistence;

public class ProcessoRepository : IProcessoRepository
{
    private readonly AppDbContext _context;

    public ProcessoRepository(AppDbContext context)
    {
        _context = context;
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
            // Remove old partes and andamentos, and add new ones
            _context.Partes.RemoveRange(existingProcesso.Partes);
            _context.Andamentos.RemoveRange(existingProcesso.Andamentos);
            
            existingProcesso.Classe = processo.Classe;
            existingProcesso.Assunto = processo.Assunto;
            existingProcesso.Foro = processo.Foro;
            existingProcesso.DataDistribuicao = processo.DataDistribuicao;
            
            foreach (var parte in processo.Partes)
            {
                parte.ProcessoId = existingProcesso.Id;
                existingProcesso.Partes.Add(parte);
            }
            
            foreach (var andamento in processo.Andamentos)
            {
                andamento.ProcessoId = existingProcesso.Id;
                existingProcesso.Andamentos.Add(andamento);
            }
            
            _context.Processos.Update(existingProcesso);
        }
        else
        {
            await _context.Processos.AddAsync(processo, cancellationToken);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
