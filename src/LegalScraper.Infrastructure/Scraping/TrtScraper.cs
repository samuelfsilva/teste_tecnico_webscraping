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
        // Stealth mode: remove navigator.webdriver and randomize browser fingerprints
        await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });
        var context = await browser.CreateStealthContext();
        var page = await context.NewPageAsync();

        try
        {
            await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

            // PJe initial search is often inside an input with id #nrProcessoInput
            await page.FillAsync("#nrProcessoInput", numeroProcesso);
            await page.ClickAsync("#btnPesquisar");

            // TRT5 specific: After searching, it shows a list of results (1st Degree, 2nd Degree, etc)
            // We need to click on the first relevant result if we aren't already on the details page.
            try 
            {
                // Wait for the results table or the details panel
                await page.WaitForSelectorAsync(".painel-conteudo, .tabela-processos, .nome-parte", new PageWaitForSelectorOptions { Timeout = 10000 });
                
                // If we see a list of processes, click the first one (usually 1st Degree)
                var firstProcessLink = page.Locator("a.link-processo").First;
                if (await firstProcessLink.IsVisibleAsync())
                {
                    _logger.LogInformation("Clicando no primeiro resultado da lista para o processo {Numero}", numeroProcesso);
                    await firstProcessLink.ClickAsync();
                }
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Não foi possível detectar a lista de resultados ou os detalhes do processo {Numero} após a busca inicial.", numeroProcesso);
            }

            // Wait for results
            try
            {
                // The main detail card
                var contentSelector = ".painel-conteudo";
                await page.WaitForSelectorAsync(contentSelector, new PageWaitForSelectorOptions { Timeout = 5000 });
            }
            catch (TimeoutException)
            {
                // Often PJe has a modal reCaptcha or a custom alphanumeric captcha
                var captchaVisible = await page.Locator("app-hcaptcha").IsVisibleAsync() || 
                                     await page.Locator(".g-recaptcha").IsVisibleAsync() ||
                                     await page.Locator("#imagemCaptcha").IsVisibleAsync() ||
                                     await page.Locator("#captchaInput").IsVisibleAsync();
                
                if (captchaVisible)
                {
                    _logger.LogWarning("CAPTCHA detectado no TRT{TrtNum} para processo {Numero}. Por favor, resolva-o no navegador aberto (você tem 3 minutos).", trtNum, numeroProcesso);
                    try
                    {
                        // Increased timeout to 180 seconds as requested by the user
                        // We wait for either the content panel or the button to generate PDF
                        await page.WaitForSelectorAsync(".painel-conteudo, button[title='Gerar PDF']", new PageWaitForSelectorOptions { Timeout = 180000 });
                    }
                    catch
                    {
                        _logger.LogError("Timeout aguardando resolução manual do CAPTCHA.");
                        return null;
                    }
                }
                else
                {
                    // Check if we need to click on a result link (1st Degree / 2nd Degree)
                    var resultLink = page.Locator("a.link-processo, .tabela-processos a").First;
                    if (await resultLink.IsVisibleAsync())
                    {
                        _logger.LogInformation("Clicando no resultado da busca para o processo {Numero}", numeroProcesso);
                        await resultLink.ClickAsync();
                        
                        // Check for captcha again after clicking
                        await Task.Delay(2000); 
                        if (await page.Locator("#captchaInput").IsVisibleAsync())
                        {
                            _logger.LogWarning("CAPTCHA detectado após selecionar o grau. Por favor, resolva-o (3 minutos).");
                            await page.WaitForSelectorAsync(".painel-conteudo, button[title='Gerar PDF']", new PageWaitForSelectorOptions { Timeout = 180000 });
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Não foi possível carregar os detalhes do processo TRT {Numero}", numeroProcesso);
                        return null;
                    }
                }
            }

            var processo = new Processo { Numero = numeroProcesso };

            // Wait specifically for process details to load
            try
            {
                // Increased timeout to 60 seconds (1 minute) as requested for the second stage
                await page.WaitForSelectorAsync("mat-card-title", new PageWaitForSelectorOptions { Timeout = 60000 });
            }
            catch (TimeoutException)
            {
                // Check if a second captcha appeared after the initial load
                var secondCaptchaVisible = await page.Locator("app-hcaptcha").IsVisibleAsync() || 
                                          await page.Locator(".g-recaptcha").IsVisibleAsync() ||
                                          await page.Locator("#imagemCaptcha").IsVisibleAsync();

                if (secondCaptchaVisible)
                {
                    _logger.LogWarning("Segundo CAPTCHA detectado no TRT{TrtNum} para processo {Numero}. Por favor, resolva-o (você tem 1 minuto).", trtNum, numeroProcesso);
                    try
                    {
                        await page.WaitForSelectorAsync("mat-card-title", new PageWaitForSelectorOptions { Timeout = 60000 });
                    }
                    catch
                    {
                        _logger.LogError("Timeout aguardando resolução do segundo CAPTCHA.");
                        return null;
                    }
                }
                else
                {
                    _logger.LogWarning("Timeout aguardando carregamento dos detalhes do processo {Numero}.", numeroProcesso);
                    return null;
                }
            }


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
            
            // PDF Download logic
            try
            {
                _logger.LogInformation("Tentando baixar o PDF do processo {Numero}", numeroProcesso);
                
                // TRT2 uses: div[id^='botoes-documento'] > a > i.fa-download
                // We click the anchor containing the download icon in the botoes-documento div
                var pdfLink = page.Locator("div[id^='botoes-documento'] a:has(i.fa-download)").First;
                
                if (await pdfLink.IsVisibleAsync())
                {
                    _logger.LogInformation("Botão de download do PDF encontrado. Aguardando download...");
                    
                    // Start waiting for the download BEFORE clicking
                    var downloadTask = page.WaitForDownloadAsync();
                    await pdfLink.ClickAsync();
                    var download = await downloadTask;
                    
                    // Wait for download to finish and read bytes
                    var path = await download.PathAsync();
                    if (!string.IsNullOrEmpty(path))
                    {
                        processo.PdfConteudo = await System.IO.File.ReadAllBytesAsync(path);
                        processo.PdfNome = download.SuggestedFilename;
                        _logger.LogInformation("PDF baixado com sucesso: {Filename} ({Size} bytes)", processo.PdfNome, processo.PdfConteudo.Length);
                    }
                }
                else
                {
                    _logger.LogWarning("Botão de download do PDF não encontrado para o processo {Numero}", numeroProcesso);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Erro ao tentar baixar o PDF: {Message}", ex.Message);
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
