using MediatR;

namespace LegalScraper.Application.Queries;

public record GetProcessoPdfQuery(string Numero) : IRequest<(byte[]? Conteudo, string? Nome)>;
