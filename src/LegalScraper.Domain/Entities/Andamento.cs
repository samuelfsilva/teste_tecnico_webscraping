using System;

namespace LegalScraper.Domain.Entities;

public class Andamento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProcessoId { get; set; }
    public DateTime? Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
    
    public Processo? Processo { get; set; }
}
