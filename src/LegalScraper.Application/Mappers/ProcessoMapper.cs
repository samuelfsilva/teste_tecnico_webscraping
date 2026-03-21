using System.Linq;
using LegalScraper.Application.DTOs;
using LegalScraper.Domain.Entities;

namespace LegalScraper.Application.Mappers;

public static class ProcessoMapper
{
    public static ProcessoDto ToDto(this Processo processo)
    {
        return new ProcessoDto
        {
            Numero = processo.Numero,
            Classe = processo.Classe,
            Assunto = processo.Assunto,
            Foro = processo.Foro,
            DataDistribuicao = processo.DataDistribuicao,
            Partes = processo.Partes.Select(p => new ParteDto
            {
                Nome = p.Nome,
                TipoParte = p.TipoParte
            }).ToList(),
            Andamentos = processo.Andamentos.Select(a => new AndamentoDto
            {
                Data = a.Data,
                Descricao = a.Descricao
            }).OrderByDescending(a => a.Data).ToList()
        };
    }
}
