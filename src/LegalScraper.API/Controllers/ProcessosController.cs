using System.Collections.Generic;
using System.Threading.Tasks;
using LegalScraper.Application.Commands;
using LegalScraper.Application.DTOs;
using LegalScraper.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LegalScraper.API.Controllers;

[ApiController]
[Route("[controller]")]
public class ProcessosController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProcessosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessoDto>>> GetAll()
    {
        var result = await _mediator.Send(new GetProcessosQuery());
        return Ok(result);
    }

    [HttpGet("{numeroProcesso}")]
    public async Task<ActionResult<ProcessoDto>> GetByNumero(string numeroProcesso)
    {
        var result = await _mediator.Send(new GetProcessoByNumeroQuery(numeroProcesso));
        if (result == null)
            return NotFound();

        return Ok(result);
    }

    [HttpPost("scrape")]
    public async Task<ActionResult> Scrape([FromBody] List<string> numerosProcesso)
    {
        var success = await _mediator.Send(new ScrapeProcessosCommand(numerosProcesso));
        if (success)
            return Ok(new { Message = "Scraping finalizado com sucesso para todos os processos." });
        
        return StatusCode(207, new { Message = "Scraping finalizado, mas alguns processos falharam ou precisaram de intervenção." });
    }

    [HttpGet("{numeroProcesso}/pdf")]
    public async Task<ActionResult> GetPdf(string numeroProcesso)
    {
        var (conteudo, nome) = await _mediator.Send(new GetProcessoPdfQuery(numeroProcesso));
        
        if (conteudo == null || conteudo.Length == 0)
            return NotFound(new { Message = $"PDF não disponível para o processo {numeroProcesso}." });

        var fileName = string.IsNullOrEmpty(nome) ? $"{numeroProcesso}.pdf" : nome;
        return File(conteudo, "application/pdf", fileName);
    }
}
