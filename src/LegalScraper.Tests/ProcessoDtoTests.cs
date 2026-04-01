using System;
using Xunit;
using LegalScraper.Domain.Entities;

namespace LegalScraper.Tests;

public class ProcessoDtoTests
{
    [Fact]
    public void Processo_DefaultsAndCollections()
    {
        var processo = new Processo();

        Assert.NotEqual(Guid.Empty, processo.Id);
        Assert.Equal(string.Empty, processo.Numero);
        Assert.NotNull(processo.Partes);
        Assert.NotNull(processo.Andamentos);
        Assert.Empty(processo.Partes);
        Assert.Empty(processo.Andamentos);
    }
}
