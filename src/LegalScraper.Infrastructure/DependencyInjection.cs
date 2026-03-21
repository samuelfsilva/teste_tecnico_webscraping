using LegalScraper.Application.Interfaces;
using LegalScraper.Infrastructure.Persistence;
using LegalScraper.Infrastructure.Scraping;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LegalScraper.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DefaultConnection") ?? "Data Source=LegalScraper.db"));

        services.AddScoped<IProcessoRepository, ProcessoRepository>();
        
        // Registering Scraper Service
        services.AddScoped<IScraperService, ScraperService>();
        
        // Scraping Strategies
        services.AddTransient<TjspScraper>();
        services.AddTransient<TrtScraper>();

        return services;
    }
}
