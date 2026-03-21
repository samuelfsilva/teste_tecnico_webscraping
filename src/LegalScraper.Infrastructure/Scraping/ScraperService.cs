using System;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Application.Interfaces;
using LegalScraper.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegalScraper.Infrastructure.Scraping;

public class ScraperService : IScraperService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScraperService> _logger;

    public ScraperService(IServiceProvider serviceProvider, ILogger<ScraperService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<Processo?> ScrapeProcessoAsync(string numeroProcesso, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Determinando qual scraper utilizar para o processo {Numero}", numeroProcesso);
        
        // TJSP Format usually has "8.26." (e.g. 1501983-25.2022.8.26.0022)
        if (numeroProcesso.Contains(".8.26."))
        {
            var scraper = _serviceProvider.GetRequiredService<TjspScraper>();
            return await scraper.ScrapeAsync(numeroProcesso, cancellationToken);
        }
        
        // TRT format (e.g. 5.15. for TRT15, 5.02. for TRT2)
        if (numeroProcesso.Contains(".5.02.") || 
            numeroProcesso.Contains(".5.04.") || 
            numeroProcesso.Contains(".5.12.") || 
            numeroProcesso.Contains(".5.15."))
        {
            var scraper = _serviceProvider.GetRequiredService<TrtScraper>();
            return await scraper.ScrapeAsync(numeroProcesso, cancellationToken);
        }

        _logger.LogWarning("Nenhum scraper compatível encontrado para o formato do processo {Numero}", numeroProcesso);
        return null;
    }
}
