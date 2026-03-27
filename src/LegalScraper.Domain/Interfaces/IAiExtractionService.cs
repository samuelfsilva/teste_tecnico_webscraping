using LegalScraper.Domain.DTOs;

namespace LegalScraper.Domain.Interfaces;

public interface IAiExtractionService
{
    // Método renomeado e atualizado para receber o contexto do HTML
    Task<MapDadosPdf> ExtractProcessDataAsync(byte[] pdfBytes, List<AndamentoDto> andamentosHtml, CancellationToken ct = default);
}
