using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Entities;

namespace LegalScraper.Application.Interfaces;

public interface IScraperService
{
    Task<Processo?> ScrapeProcessoAsync(string numeroProcesso, CancellationToken cancellationToken);
}
