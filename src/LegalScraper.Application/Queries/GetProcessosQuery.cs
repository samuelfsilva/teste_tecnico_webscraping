using System.Collections.Generic;
using LegalScraper.Application.DTOs;
using MediatR;

namespace LegalScraper.Application.Queries;

public class GetProcessosQuery : IRequest<IEnumerable<ProcessoDto>>
{
}
