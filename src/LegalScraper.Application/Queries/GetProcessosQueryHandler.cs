using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Application.DTOs;
using LegalScraper.Application.Interfaces;
using LegalScraper.Application.Mappers;
using MediatR;

namespace LegalScraper.Application.Queries;

public class GetProcessosQueryHandler : IRequestHandler<GetProcessosQuery, IEnumerable<ProcessoDto>>
{
    private readonly IProcessoRepository _repository;

    public GetProcessosQueryHandler(IProcessoRepository repository)
    {
        _repository = repository;
    }

    public async Task<IEnumerable<ProcessoDto>> Handle(GetProcessosQuery request, CancellationToken cancellationToken)
    {
        var processos = await _repository.GetAllAsync(cancellationToken);
        return processos.Select(p => p.ToDto());
    }
}
