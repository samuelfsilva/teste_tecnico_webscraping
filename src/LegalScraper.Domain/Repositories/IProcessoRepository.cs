using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Entities;

namespace LegalScraper.Domain.Repositories;

public interface IProcessoRepository
{
    Task<Processo?> GetByNumeroAsync(string numero, CancellationToken cancellationToken);
    Task<IEnumerable<Processo>> GetAllAsync(CancellationToken cancellationToken);
    Task AddOrUpdateAsync(Processo processo, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
