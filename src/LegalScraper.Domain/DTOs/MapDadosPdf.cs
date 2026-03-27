namespace LegalScraper.Domain.DTOs;

public record MapDadosPdf(
    string? Classe,
    string? Assunto,
    string? Foro,
    DateTime? DataDistribuicao,
    List<ParteDto> Partes,
    List<AndamentoDto> Andamentos
);

public record ParteDto(string Nome, string? TipoParte);
public record AndamentoDto(DateTime? Data, string Descricao);
