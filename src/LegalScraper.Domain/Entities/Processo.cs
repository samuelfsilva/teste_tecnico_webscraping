using System;
using System.Collections.Generic;

namespace LegalScraper.Domain.Entities;

public class Processo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Numero { get; set; } = string.Empty;
    public string? Classe { get; set; }
    public string? Assunto { get; set; }
    public string? Foro { get; set; }
    public DateTime? DataDistribuicao { get; set; }
    public byte[]? PdfConteudo { get; set; }
    public string? PdfNome { get; set; }
    
    // Navigation properties
    public ICollection<Parte> Partes { get; set; } = new List<Parte>();
    public ICollection<Andamento> Andamentos { get; set; } = new List<Andamento>();
}
