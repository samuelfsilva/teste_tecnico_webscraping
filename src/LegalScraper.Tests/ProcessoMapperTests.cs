using System;
using Xunit;
using LegalScraper.Domain.Entities;
using LegalScraper.Application.Mappers;

namespace LegalScraper.Tests;

public class ProcessoMapperTests
{
    [Fact]
    public void ToDto_MapsAllFieldsAndOrdersAndamentosDescending()
    {
        var processo = new Processo
        {
            Numero = "123",
            Classe = "Classe A",
            Assunto = "Assunto",
            Foro = "Foro",
            DataDistribuicao = new DateTime(2023, 1, 2),
            PdfConteudo = new byte[] { 1, 2, 3 },
            PdfNome = "doc.pdf"
        };

        processo.Partes.Add(new Parte { Nome = "Parte 1", TipoParte = "Autor" });
        processo.Partes.Add(new Parte { Nome = "Parte 2", TipoParte = "Réu" });

        processo.Andamentos.Add(new Andamento { Data = new DateTime(2023, 1, 1), Descricao = "Primeiro" });
        processo.Andamentos.Add(new Andamento { Data = new DateTime(2023, 2, 1), Descricao = "Segundo" });

        var dto = processo.ToDto();

        Assert.Equal("123", dto.Numero);
        Assert.Equal("Classe A", dto.Classe);
        Assert.Equal("Assunto", dto.Assunto);
        Assert.Equal("Foro", dto.Foro);
        Assert.Equal(new DateTime(2023, 1, 2), dto.DataDistribuicao);

        Assert.True(dto.PdfDisponivel);
        Assert.Equal("doc.pdf", dto.PdfNome);

        Assert.Equal(2, dto.Partes.Count);
        Assert.Contains(dto.Partes, p => p.Nome == "Parte 1");
        Assert.Contains(dto.Partes, p => p.Nome == "Parte 2");

        Assert.Equal(2, dto.Andamentos.Count);
        Assert.Equal("Segundo", dto.Andamentos[0].Descricao);
        Assert.Equal("Primeiro", dto.Andamentos[1].Descricao);
    }
}
