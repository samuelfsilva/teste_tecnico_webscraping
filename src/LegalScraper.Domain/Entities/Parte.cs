using System;

namespace LegalScraper.Domain.Entities;

public class Parte
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcessoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? TipoParte { get; set; }
    
    public Processo? Processo { get; set; }
}
