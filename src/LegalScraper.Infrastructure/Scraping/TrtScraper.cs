using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegalScraper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace LegalScraper.Infrastructure.Scraping;

public class TrtScraper
{
    private readonly ILogger<TrtScraper> _logger;

    public TrtScraper(ILogger<TrtScraper> logger)
    {
        _logger = logger;
    }

    public async Task<Processo?> ScrapeAsync(string numeroProcesso, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando scraper PJe TRT para o processo {Numero}", numeroProcesso);

        // Determine the TRT base url based on number (e.g., 5.15 -> trt15)
        string trtNum = "15"; // Default
        var parts = numeroProcesso.Split('.');
        if (parts.Length > 2)
        {
            var trtSegment = parts[2];
            if (trtSegment.StartsWith("0"))
                trtNum = trtSegment.TrimStart('0');
            else
                trtNum = trtSegment;
        }

        var baseUrl = $"https://pje.trt{trtNum}.jus.br/consultaprocessual/";

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false }); 
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // PJe initial search is often inside an input with id #nrProcessoInput
            await page.FillAsync("#nrProcessoInput", numeroProcesso);
            await page.ClickAsync("#btnPesquisar");

            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Wait for results
            try
            {
                // The main detail card
                var contentSelector = ".painel-conteudo";
                await page.WaitForSelectorAsync(contentSelector, new PageWaitForSelectorOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                // Often PJe has a modal reCaptcha if detecting scraping
                var captchaVisible = await page.Locator("app-hcaptcha").IsVisibleAsync() || await page.Locator(".g-recaptcha").IsVisibleAsync();
                
                if (captchaVisible)
                {
                    _logger.LogWarning("CAPTCHA detectado no TRT{TrtNum} para processo {Numero}. Por favor, resolva-o no navegador aberto.", trtNum, numeroProcesso);
                    try
                    {
                        await page.WaitForSelectorAsync(".painel-conteudo", new PageWaitForSelectorOptions { Timeout = 45000 });
                    }
                    catch
                    {
                        _logger.LogError("Timeout aguardando resolução manual do CAPTCHA.");
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("Não foi possível carregar os detalhes do processo TRT {Numero}", numeroProcesso);
                    return null;
                }
            }

            var processo = new Processo { Numero = numeroProcesso };

            // Wait specifically for process details to load
            await page.WaitForSelectorAsync("mat-card-title", new PageWaitForSelectorOptions { Timeout = 10000 });

            // Extract Class and Assunto
            // Elements are often wrapped in <div class="valor"> inside generic rows
            // e-Saj is simpler, PJe is an Angular SPA.
            var headerText = await GetTextContentAsync(page, "mat-card-title");
            if (headerText != null)
            {
                // Usually "CLASSE DO PROCESSO (Número)"
            }
            
            // Actually, best PJe parsing: 
            // the tags on top often contain "Classe", "Autuação", "Órgão Julgador"
            var detailItems = await page.Locator(".item-detalhe").AllAsync();
            foreach (var item in detailItems)
            {
                var label = await item.Locator(".label").First.TextContentAsync();
                var valor = await item.Locator(".valor").First.TextContentAsync();

                if (label == null || valor == null) continue;
                
                if (label.Contains("Classe", StringComparison.OrdinalIgnoreCase))
                    processo.Classe = valor.Trim();
                else if (label.Contains("Órgão Julgador", StringComparison.OrdinalIgnoreCase))
                    processo.Foro = valor.Trim();
                else if (label.Contains("Autuação", StringComparison.OrdinalIgnoreCase))
                {
                    var dataStr = valor.Trim();
                    if (dataStr.Length >= 10 && DateTime.TryParseExact(dataStr.Substring(0, 10), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        processo.DataDistribuicao = date;
                }
            }

            // Extract Partes (Inside <mat-tab-group> often)
            // It might require clicking "Partes" tab
            var partesTab = page.Locator("div[role='tab'] .mdc-tab__text-label", new PageLocatorOptions { HasTextString = "Partes" });
            if (await partesTab.IsVisibleAsync())
            {
                await partesTab.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                var partesElements = await page.Locator("app-polo-parte tr").AllAsync(); 
                // Actual PJe architecture can vary, commonly it's a table with Polos
                foreach (var element in partesElements)
                {
                    var nomeTexto = await element.Locator(".nome-parte").TextContentAsync();
                    var tipoPolo = await element.Locator(".tipo-polo").TextContentAsync(); // or similar structure
                    
                    if (!string.IsNullOrEmpty(nomeTexto))
                    {
                        processo.Partes.Add(new Parte { 
                            Nome = nomeTexto.Trim(),
                            TipoParte = tipoPolo?.Trim() ?? "Parte",
                            ProcessoId = processo.Id
                        });
                    }
                }
            }
            
            // Extract Andamentos (Movimentações)
            var andamentosTab = page.Locator("div[role='tab'] .mdc-tab__text-label", new PageLocatorOptions { HasTextString = "Movimentações" });
            if (await andamentosTab.IsVisibleAsync())
            {
                await andamentosTab.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                
                var historyItems = await page.Locator("app-historico-movimentacao .movimentacao-item").AllAsync(); // Fallback selector, PJE frontend changes
                foreach (var item in historyItems)
                {
                     var dataStr = await item.Locator(".data").TextContentAsync();
                     var desc = await item.Locator(".descricao").TextContentAsync();
                     
                     if (!string.IsNullOrEmpty(dataStr) && !string.IsNullOrEmpty(desc))
                     {
                         DateTime? dataMov = null;
                         if (DateTime.TryParseExact(dataStr.Trim().Substring(0, 10), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                            dataMov = parsed;
                            
                         processo.Andamentos.Add(new Andamento {
                             Data = dataMov,
                             Descricao = desc.Trim(),
                             ProcessoId = processo.Id
                         });
                     }
                }
            }

            return processo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no PJe TRT{TrtNum}: {Message}", trtNum, ex.Message);
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
