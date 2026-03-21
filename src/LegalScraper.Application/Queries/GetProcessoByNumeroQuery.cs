using LegalScraper.Application.DTOs;
using MediatR;

namespace LegalScraper.Application.Queries;

public class GetProcessoByNumeroQuery : IRequest<ProcessoDto?>
{
    public string Numero { get; set; }

    public GetProcessoByNumeroQuery(string numero)
    {
        Numero = numero;
    }
}
