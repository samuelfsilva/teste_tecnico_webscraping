using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Application.DTOs;
using LegalScraper.Application.Interfaces;
using LegalScraper.Application.Mappers;
using MediatR;

namespace LegalScraper.Application.Queries;

public class GetProcessoByNumeroQueryHandler : IRequestHandler<GetProcessoByNumeroQuery, ProcessoDto?>
{
    private readonly IProcessoRepository _repository;

    public GetProcessoByNumeroQueryHandler(IProcessoRepository repository)
    {
        _repository = repository;
    }

    public async Task<ProcessoDto?> Handle(GetProcessoByNumeroQuery request, CancellationToken cancellationToken)
    {
        var processo = await _repository.GetByNumeroAsync(request.Numero, cancellationToken);
        return processo?.ToDto();
    }
}
