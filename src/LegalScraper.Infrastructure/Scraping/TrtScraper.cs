using System;
using System.Globalization;
using LegalScraper.Domain.Interfaces;
using LegalScraper.Domain.DTOs;
using LegalScraper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Soenneker.Playwrights.Extensions.Stealth;


namespace LegalScraper.Infrastructure.Scraping;

public class TrtScraper
{
    private readonly ILogger<TrtScraper> _logger;
    private readonly IAiExtractionService _aiService;

    public TrtScraper(ILogger<TrtScraper> logger, IAiExtractionService aiService)
    {
        _logger = logger;
        _aiService = aiService;
    }

    public async Task<Processo?> ScrapeAsync(string numeroProcesso, CancellationToken ct)
    {
        _logger.LogInformation("Iniciando scraper PJe TRT para o processo {Numero}", numeroProcesso);

        string trtNum = "02"; 
        var parts = numeroProcesso.Split('.');
        if (parts.Length > 3) trtNum = parts[3].TrimStart('0');

        var baseUrl = $"https://pje.trt{trtNum}.jus.br/consultaprocessual/detalhe-processo/{numeroProcesso}/1";

        using var playwright = await Playwright.CreateAsync();
        // Headless false para você interagir com o Captcha
        await using var browser = await playwright.LaunchStealthChromium(new BrowserTypeLaunchOptions { Headless = false });
        
        var contextOptions = new BrowserNewContextOptions 
        {
            ViewportSize = new ViewportSize { Width = 1366, Height = 768 },
            // Opcional: Definir a localidade para evitar problemas com datas/moedas
            Locale = "pt-BR" 
        };

        var context = await browser.NewContextAsync(contextOptions);
        var page = await context.NewPageAsync();

        try
        {
            // 1. NAVEGAÇÃO INICIAL
            // Usamos 'Load' em vez de 'DOMContentLoaded' para garantir que os scripts base do Angular baixaram
            await page.GotoAsync(baseUrl, new PageGotoOptions { WaitUntil = WaitUntilState.Load, Timeout = 60000 });

            // --- 2. TRATAMENTO DE BARREIRA (CAPTCHA) ---
            _logger.LogInformation("Aguardando definição da página (Conteúdo ou Captcha)...");
            try 
            {
                // Espera até 20s por QUALQUER sinal de vida (Conteúdo ou Barreira)
                await page.WaitForSelectorAsync(".painel-conteudo, mat-card-title, form[name='captchaForm'], #imagemCaptcha, app-hcaptcha", 
                    new() { Timeout = 20000 });
                
                // Verifica se o que apareceu foi um Captcha (usando .First para evitar Strict Mode Error)
                var captchaLocator = page.Locator("form[name='captchaForm'], #imagemCaptcha, app-hcaptcha, .g-recaptcha");
                if (await captchaLocator.First.IsVisibleAsync())
                {
                    _logger.LogWarning("CAPTCHA detectado. Resolva manualmente (3 min).");
                    
                    // TRAVA: Espera o captcha sumir da tela
                    await page.WaitForSelectorAsync("form[name='captchaForm'], #imagemCaptcha, app-hcaptcha", 
                        new() { State = WaitForSelectorState.Hidden, Timeout = 180000 });

                    // GARANTIA: Espera o conteúdo real carregar e ficar visível após o captcha
                    await page.WaitForSelectorAsync(".painel-conteudo", new() { State = WaitForSelectorState.Visible, Timeout = 30000 });
                }
            }
            catch (TimeoutException) { 
                _logger.LogError("Timeout: A página travou no carregamento inicial.");
                return null; 
            }

            // Aguarda a rede estabilizar para garantir que o Angular terminou de montar a tela
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var processo = new Processo { Id = Guid.NewGuid(), Numero = numeroProcesso };
            var andamentosHtml = new List<AndamentoDto>();

            // --- 3. EXTRAÇÃO DE DADOS BÁSICOS (HTML) ---
            var detailItems = await page.Locator(".item-detalhe").AllAsync();
            foreach (var item in detailItems)
            {
                var label = await item.Locator(".label").First.TextContentAsync();
                var valor = await item.Locator(".valor").First.TextContentAsync();
                if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(valor)) continue;

                if (label.Contains("Classe", StringComparison.OrdinalIgnoreCase)) processo.Classe = valor.Trim();
                else if (label.Contains("Órgão Julgador", StringComparison.OrdinalIgnoreCase)) processo.Foro = valor.Trim();
                else if (label.Contains("Autuação", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParseExact(valor.Trim().Substring(0, 10), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        processo.DataDistribuicao = date;
                }
            }

            // --- 4. COLETA DA TIMELINE (HTML) PARA CONTEXTO DA IA ---
            string dataAtualGrupo = "";
            var itensTimeline = await page.Locator("pje-timeline-item").AllAsync();
            foreach (var item in itensTimeline)
            {
                var elData = item.Locator(".tl-data");
                if (await elData.First.IsVisibleAsync()) dataAtualGrupo = await elData.First.InnerTextAsync();
                
                var desc = await item.Locator(".tl-item-desc").InnerTextAsync();
                var hora = await item.Locator(".tl-item-footer span").InnerTextAsync();
                
                if (!string.IsNullOrEmpty(desc))
                {
                    andamentosHtml.Add(new AndamentoDto(ParseDataPje(dataAtualGrupo, hora), desc.Trim()));
                }
            }

            // --- 5. DOWNLOAD DO PDF E CONSOLIDAÇÃO VIA IA ---
            var pdfLink = page.Locator("div[id^='botoes-documento'] a:has(i.fa-download)").First;
            if (await pdfLink.IsVisibleAsync())
            {
                _logger.LogInformation("Botão de PDF encontrado. Iniciando extração híbrida...");
                var download = await page.RunAndWaitForDownloadAsync(async () => await pdfLink.ClickAsync());
                var path = await download.PathAsync();
                
                if (!string.IsNullOrEmpty(path))
                {
                    processo.PdfConteudo = await File.ReadAllBytesAsync(path);
                    processo.PdfNome = download.SuggestedFilename;

                    // MÉTODO RENOMEADO: Passamos o PDF e a lista do HTML para a IA organizar tudo
                    var extraido = await _aiService.ExtractProcessDataAsync(processo.PdfConteudo, andamentosHtml, ct);

                    if (extraido != null)
                    {
                        // A IA agora consolida os dados finais
                        processo.Classe = extraido.Classe ?? processo.Classe;
                        processo.Assunto = extraido.Assunto;
                        processo.Foro = extraido.Foro ?? processo.Foro;
                        processo.DataDistribuicao = extraido.DataDistribuicao ?? processo.DataDistribuicao;

                        foreach (var p in extraido.Partes ?? new())
                            processo.Partes.Add(new Parte { Id = Guid.NewGuid(), Nome = p.Nome, TipoParte = p.TipoParte, ProcessoId = processo.Id });

                        foreach (var a in extraido.Andamentos ?? new())
                            processo.Andamentos.Add(new Andamento { Id = Guid.NewGuid(), Data = a.Data, Descricao = a.Descricao, ProcessoId = processo.Id });
                    }
                }
            }
            else 
            {
                _logger.LogWarning("PDF indisponível. Usando apenas dados básicos do HTML.");
                // Fallback caso não tenha PDF
                foreach(var a in andamentosHtml)
                    processo.Andamentos.Add(new Andamento { Data = a.Data, Descricao = a.Descricao, ProcessoId = processo.Id });
            }

            return processo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro crítico no Scraper PJe: {Msg}", ex.Message);
            return null;
        }
    }

    // Auxiliar para converter as datas peculiares do PJe (Ex: "19 mar. 2026" + "12:22")
    private DateTime? ParseDataPje(string dataStr, string horaStr)
    {
        try 
        {
            // Lógica simples de parse ou Regex conforme a necessidade
            // Para fins de exemplo, tenta converter o básico:
            if (string.IsNullOrEmpty(dataStr)) return null;
            return DateTime.Now; // Substituir pela lógica de parse real dd/MMM/yyyy
        }
        catch { return null; }
    }
}
