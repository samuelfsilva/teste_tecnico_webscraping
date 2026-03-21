using System;
using System.Collections.Generic;

namespace LegalScraper.Application.DTOs;

public class ParteDto
{
    public string Nome { get; set; } = string.Empty;
    public string? TipoParte { get; set; }
}

public class AndamentoDto
{
    public DateTime? Data { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public class ProcessoDto
{
    public string Numero { get; set; } = string.Empty;
    public string? Classe { get; set; }
    public string? Assunto { get; set; }
    public string? Foro { get; set; }
    public DateTime? DataDistribuicao { get; set; }
    
    public List<ParteDto> Partes { get; set; } = new();
    public List<AndamentoDto> Andamentos { get; set; } = new();
}
