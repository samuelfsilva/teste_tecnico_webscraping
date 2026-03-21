using System.Collections.Generic;
using MediatR;

namespace LegalScraper.Application.Commands;

public class ScrapeProcessosCommand : IRequest<bool>
{
    public List<string> NumerosProcesso { get; set; } = new();

    public ScrapeProcessosCommand(List<string> numerosProcesso)
    {
        NumerosProcesso = numerosProcesso ?? new List<string>();
    }
}
