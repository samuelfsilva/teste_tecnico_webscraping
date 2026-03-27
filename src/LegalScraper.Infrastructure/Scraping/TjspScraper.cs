using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth;

namespace LegalScraper.Infrastructure.Scraping;

public class TjspScraper
{
    private readonly ILogger<TjspScraper> _logger;

    public TjspScraper(ILogger<TjspScraper> logger)
    {
        _logger = logger;
    }

    public async Task<Processo?> ScrapeAsync(string numeroProcesso, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando scraper TJSP para o processo {Numero}", numeroProcesso);
        
        using var playwright = await Playwright.CreateAsync();
        
        // Stealth mode: remove navigator.webdriver and randomize browser fingerprints
        await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.CreateStealthContext();
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync("https://esaj.tjsp.jus.br/cpopg/open.do", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            
            // Fill the search form
            // e-SAJ often has two fields for the process number, or one unified field depending on the radio button
            await page.Locator("#numeroDigitoAnoUnificado").FillAsync(numeroProcesso.Substring(0, 15));
            await page.Locator("#foroNumeroUnificado").FillAsync(numeroProcesso.Substring(21, 4));
            
            await page.Locator("#botaoConsultarProcessos").ClickAsync();
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Check for multiselect table (when the search returns multiple unities)
            var modalList = await page.Locator("#modalDecisao").IsVisibleAsync();
            if (modalList)
            {
                // Click the first one
                await page.Locator("#processoSelecionado").First.ClickAsync();
                await page.Locator("#botaoEnviarIncidente").ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
            
            // Wait for results
            var processInfoVisible = await page.Locator("#numeroProcesso").IsVisibleAsync();
            if (!processInfoVisible)
            {
                var msgErro = await page.Locator("#mensagemRetorno").IsVisibleAsync();
                if (msgErro)
                {
                    _logger.LogWarning("Processo não encontrado ou erro do sistema do TJ");
                    return null;
                }

                _logger.LogError("Informações do processo não encontradas no TJSP (possível bloqueio anti-bot).");
                return null;
            }

            var processo = new Processo { Numero = numeroProcesso };
            
            // Extract details
            processo.Classe = await GetTextContentAsync(page, "#classeProcesso");
            processo.Assunto = await GetTextContentAsync(page, "#assuntoProcesso");
            processo.Foro = await GetTextContentAsync(page, "#foroProcesso");
            
            var dataDiv = await GetTextContentAsync(page, "#dataHoraDistribuicaoProcesso");
            if (!string.IsNullOrEmpty(dataDiv) && dataDiv.Length >= 10)
            {
                if (DateTime.TryParseExact(dataDiv.Substring(0, 10), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    processo.DataDistribuicao = date;
            }

            // Extract Partes
            var rowsPartes = await page.Locator("#tableTodasPartes .fundoClaro").AllAsync();
            if(!rowsPartes.Any())
                rowsPartes = await page.Locator("#tablePartesPrincipais .fundoClaro").AllAsync(); // Fallback to main partes

            foreach (var row in rowsPartes)
            {
                var tipo = await row.Locator("td.label").InnerTextAsync();
                var nomeFull = await row.Locator("td.nomeParteEAdvogado").InnerTextAsync();

                if (string.IsNullOrWhiteSpace(tipo) && string.IsNullOrWhiteSpace(nomeFull))
                {
                    _logger.LogDebug("Linha de parte sem tipo e nome encontrado; pulando.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(nomeFull))
                {
                    _logger.LogDebug("nomeParteEAdvogado vazio para processo {Numero}", numeroProcesso);
                    continue;
                }

                var nome = NormalizeParteName(nomeFull);
                var tipoClean = tipo?.Trim().TrimEnd(':') ?? string.Empty;

                processo.Partes.Add(new Parte { TipoParte = tipoClean, Nome = nome, ProcessoId = processo.Id });
            }
            
            // Extract Andamentos
            var andamentosRows = await page.Locator("#tabelaTodasMovimentacoes tr").AllAsync();
            foreach (var row in andamentosRows)
            {
                var dataStr = await row.Locator("td.dataMovimentacao").TextContentAsync();
                var desc = await row.Locator("td.descricaoMovimentacao").TextContentAsync();
                
                if (!string.IsNullOrEmpty(dataStr) && !string.IsNullOrEmpty(desc))
                {
                    var dataStrClean = dataStr.Trim();
                    DateTime? dataMov = null;
                    if (DateTime.TryParseExact(dataStrClean, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        dataMov = parsed;

                    processo.Andamentos.Add(new Andamento
                    {
                        Data = dataMov,
                        Descricao = string.Join(" ", desc.Split(new[] { '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())).Trim(),
                        ProcessoId = processo.Id
                    });
                }
            }

            return processo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro TJSP: {Message}", ex.Message);
            return null;
        }
    }

    private async Task<string?> GetTextContentAsync(IPage page, string selector)
    {
        try
        {
            var element = await page.Locator(selector).First.TextContentAsync();
            return element?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeParteName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Replace non-breaking spaces with normal spaces
        var t = input.Replace('\u00A0', ' ');

        // Normalize line endings and split into meaningful lines
        t = Regex.Replace(t, "\r\n|\r|\n", "\n");
        var parts = t.Split('\n').Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)).ToArray();

        var first = parts.Length > 0 ? parts[0] : t.Trim();

        // Collapse multiple whitespace into a single space
        first = Regex.Replace(first, "\\s{2,}", " ");

        return first.Trim();
    }
}
