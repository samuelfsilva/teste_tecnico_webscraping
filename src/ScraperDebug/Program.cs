using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using LegalScraper.Infrastructure.Scraping;

class Program
{
    static async Task Main(string[] args)
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });
        var logger = loggerFactory.CreateLogger<TrtScraper>();
        var scraper = new TrtScraper(logger);

        // Process number for TRT5 (provided by browser exploration)
        string processNumber = "0000516-48.2023.5.05.0002";
        
        Console.WriteLine($"Iniciando scraping do processo {processNumber}...");
        
        try
        {
            var processo = await scraper.ScrapeAsync(processNumber, CancellationToken.None);
            
            if (processo != null)
            {
                Console.WriteLine("Scraping concluído com sucesso!");
                Console.WriteLine($"Número: {processo.Numero}");
                Console.WriteLine($"Classe: {processo.Classe}");
                Console.WriteLine($"Foro: {processo.Foro}");
                Console.WriteLine($"Data Distribuição: {processo.DataDistribuicao}");
                Console.WriteLine($"Partes: {processo.Partes.Count}");
                Console.WriteLine($"Andamentos: {processo.Andamentos.Count}");
            }
            else
            {
                Console.WriteLine("Falha no scraping ou timeout no CAPTCHA.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERRO: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}

