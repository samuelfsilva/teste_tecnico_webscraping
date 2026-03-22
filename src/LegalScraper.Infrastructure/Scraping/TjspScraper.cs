using System;
using System.Globalization;
using System.Linq;
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
        await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });
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
            
            // Wait for Captcha or Results
            var processInfoVisible = await page.Locator("#numeroProcesso").IsVisibleAsync();
            if (!processInfoVisible)
            {
                // Probably a Captcha block or "Processo não encontrado"
                var captchaVisible = await page.Locator("#captcha_image").IsVisibleAsync() || await page.Locator(".g-recaptcha").IsVisibleAsync();
                if (captchaVisible)
                {
                    _logger.LogWarning("CAPTCHA detectado no TJSP para processo {Numero}. Por favor, resolva-o no navegador de Chromium aberto (você tem 30 segundos).", numeroProcesso);
                    
                    // Estratégia de contorno: Esperar intervenção manual (comum em robôs assistidos)
                    // Num cenário 100% headless usaríamos serviços como 2Captcha ou AntiCaptcha.
                    try 
                    {
                        await page.WaitForSelectorAsync("#numeroProcesso", new PageWaitForSelectorOptions { Timeout = 30000 });
                    }
                    catch (TimeoutException)
                    {
                        _logger.LogError("Timeout aguardando a resolução do CAPTCHA.");
                        return null;
                    }
                }
                else 
                {
                    var msgErro = await page.Locator("#mensagemRetorno").IsVisibleAsync();
                    if (msgErro) 
                    {
                        _logger.LogWarning("Processo não encontrado ou erro do sistema do TJ");
                        return null;
                    }
                }
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
                var tipo = await row.Locator("td.label").TextContentAsync();
                var nomeFull = await row.Locator("td.nomeParteEAdvogado").TextContentAsync();
                if (tipo != null && nomeFull != null)
                {
                    var nome = nomeFull.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    processo.Partes.Add(new Parte { TipoParte = tipo.Trim().TrimEnd(':'), Nome = nome, ProcessoId = processo.Id });
                }
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
}
