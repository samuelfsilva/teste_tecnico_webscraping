using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Iniciando teste de Playwright...");
        try
        {
            using var playwright = await Playwright.CreateAsync();
            Console.WriteLine("Playwright criado. Tentando lançar o Chromium...");
            
            var options = new BrowserTypeLaunchOptions 
            { 
                Headless = true,
                ExecutablePath = "/home/samuel/.cache/ms-playwright/chromium-1155/chrome-linux/chrome"
            };
            
            await using var browser = await playwright.Chromium.LaunchAsync(options);
            Console.WriteLine("Browser lançado com sucesso!");
            
            var page = await browser.NewPageAsync();
            await page.GotoAsync("https://www.google.com");
            Console.WriteLine("Navegou para o Google!");
            
            Console.WriteLine("Título: " + await page.TitleAsync());
            await browser.CloseAsync();
            Console.WriteLine("Teste concluído com sucesso!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERRO FATAL: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
