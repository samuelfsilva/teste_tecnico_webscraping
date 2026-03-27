using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using GenerativeAI;
using GenerativeAI.Types;
using LegalScraper.Domain.DTOs;
using LegalScraper.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Linq;

namespace LegalScraper.Infrastructure.Services;

public class GeminiExtractionService : IAiExtractionService
{
    private readonly string _apiKey;
    private readonly ILogger<GeminiExtractionService> _logger;

    public GeminiExtractionService(IConfiguration config, ILogger<GeminiExtractionService> logger)
    {
        // Garante que a chave existe ou lança erro claro
        _apiKey = config["Gemini:ApiKey"] ?? throw new ArgumentNullException("Gemini:ApiKey não encontrada no appsettings.json");
        _logger = logger;
    }

    public async Task<MapDadosPdf> ExtractProcessDataAsync(byte[] pdfBytes, List<AndamentoDto> andamentosHtml, CancellationToken ct = default)
    {
        _logger.LogInformation("Iniciando extração de dados do PDF usando Gemini 2.5 Flash");

        // 1. Usamos a string direta do modelo para evitar erro de 'ModelNames'
        var model = new GenerativeModel(_apiKey, "gemini-2.5-flash");

        // 2. Prompt ajustado para bater com as propriedades do seu DTO MapDadosPdf
        var andamentosContexto = JsonSerializer.Serialize(andamentosHtml);

        string prompt = $@"Você é um assistente jurídico especializado. 
        Recebi os seguintes andamentos extraídos do site do tribunal: {andamentosContexto}
        
        Analise também o PDF anexo e retorne um JSON consolidado.
        REGRAS:
        1. Combine os andamentos do site com os do PDF.
        2. Remova duplicatas (mesmo evento com descrições levemente diferentes).
        3. Priorize a data mais precisa.
        4. Extraia também: Classe, Assunto, Foro, Data de Distribuição e as Partes.

        Retorne APENAS o JSON:
        {{
        ""classe"": ""string"",
        ""assunto"": ""string"",
        ""foro"": ""string"",
        ""dataDistribuicao"": ""ISO8601"",
        ""partes"": [{{ ""nome"": ""string"", ""tipoParte"": ""string"" }}],
        ""andamentos"": [{{ ""data"": ""ISO8601"", ""descricao"": ""string"" }}]
        }}";

        // 3. Montagem do conteúdo no formato aceito pela lib (Parts com InlineData)
        var content = new Content
        {
            Parts = new List<Part>
            {
                new Part { Text = prompt },
                new Part 
                { 
                    InlineData = new Blob 
                    { 
                        MimeType = "application/pdf", 
                        Data = Convert.ToBase64String(pdfBytes) 
                    } 
                }
            }
        };

        var request = new GenerateContentRequest
        {
            Contents = new List<Content> { content }
        };

        try
        {
            _logger.LogInformation("Enviando requisição para Gemini com o PDF anexado. Tamanho do PDF: {Size} bytes", pdfBytes.Length);
            // 4. Chamada da API
            var response = await model.GenerateContentAsync(request);
            var jsonResponse = response.Text(); 

            if (string.IsNullOrEmpty(jsonResponse))
            {
                _logger.LogWarning("A IA retornou uma resposta vazia.");
                return new MapDadosPdf(null, null, null, null, new List<ParteDto>(), new List<AndamentoDto>());
            }

            return ParseJson(jsonResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao chamar a API do Gemini para extração de PDF.");
            return new MapDadosPdf(null, null, null, null, new List<ParteDto>(), new List<AndamentoDto>());
        }
    }

    private MapDadosPdf ParseJson(string json) 
    {
        try 
        {
            // Limpa marcações de markdown se a IA enviar
            var cleanJson = json.Replace("```json", "").Replace("```", "").Trim();
            
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };

            return JsonSerializer.Deserialize<MapDadosPdf>(cleanJson, options) 
                   ?? new MapDadosPdf(null, null, null, null, new List<ParteDto>(), new List<AndamentoDto>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao fazer o parse do JSON da IA: {Json}", json);
            return new MapDadosPdf(null, null, null, null, new List<ParteDto>(), new List<AndamentoDto>());
        }
    }
}
