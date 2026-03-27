using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Application.Interfaces;
using LegalScraper.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LegalScraper.Application.Commands;

public class ScrapeProcessosCommandHandler : IRequestHandler<ScrapeProcessosCommand, bool>
{
    private readonly IScraperService _scraperService;
    private readonly IProcessoRepository _processoRepository;
    private readonly ILogger<ScrapeProcessosCommandHandler> _logger;

    public ScrapeProcessosCommandHandler(IScraperService scraperService, IProcessoRepository processoRepository, ILogger<ScrapeProcessosCommandHandler> logger)
    {
        _scraperService = scraperService;
        _processoRepository = processoRepository;
        _logger = logger;
    }

    public async Task<bool> Handle(ScrapeProcessosCommand request, CancellationToken cancellationToken)
    {
        bool allSuccess = true;

        foreach (var numero in request.NumerosProcesso)
        {
            try
            {
                _logger.LogInformation("Iniciando scraping do processo {Numero}", numero);
                var processo = await _scraperService.ScrapeProcessoAsync(numero, cancellationToken);
                
                if (processo != null)
                {
                    await _processoRepository.AddOrUpdateAsync(processo, cancellationToken);
                    _logger.LogInformation("Processo {Numero} salvo com sucesso", numero);
                }
                else
                {
                    _logger.LogWarning("Não foi possível encontrar ou debugar o processo {Numero}", numero);
                    allSuccess = false;
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair dados do processo {Numero}", numero);
                allSuccess = false;
            }
        }
        
        await _processoRepository.SaveChangesAsync(cancellationToken);

        return allSuccess;
    }
}
