using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Repositories;
using MediatR;

namespace LegalScraper.Application.Queries;

public class GetProcessoPdfQueryHandler : IRequestHandler<GetProcessoPdfQuery, (byte[]? Conteudo, string? Nome)>
{
    private readonly IProcessoRepository _repository;

    public GetProcessoPdfQueryHandler(IProcessoRepository repository)
    {
        _repository = repository;
    }

    public async Task<(byte[]? Conteudo, string? Nome)> Handle(GetProcessoPdfQuery request, CancellationToken cancellationToken)
    {
        var processo = await _repository.GetByNumeroAsync(request.Numero, cancellationToken);
        if (processo == null)
            return (null, null);

        return (processo.PdfConteudo, processo.PdfNome);
    }
}
