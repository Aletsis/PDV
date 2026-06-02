using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PDV.Application.Features.Printers.Commands.CreatePrinter;
using PDV.Application.Features.Printers.Commands.DeletePrinter;
using PDV.Application.Features.Printers.Commands.UpdatePrinter;
using PDV.Application.Features.Printers.Queries.GetPrinter;
using PDV.Application.Features.Printers.Queries.ListPrinters;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrintersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PrintersController> _logger;

    public PrintersController(IMediator mediator, ILogger<PrintersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePrinterCommand cmd)
    {
        _logger.LogInformation("Creating printer {Name} at {Ip}:{Port}", cmd.Name, cmd.IpAddress, cmd.Port);
        var id = await _mediator.Send(cmd);
        _logger.LogInformation("Created printer {Id}", id);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        _logger.LogInformation("Get printer {Id}", id);
        var dto = await _mediator.Send(new GetPrinterQuery(id));
        if (dto == null)
        {
            _logger.LogWarning("Printer {Id} not found", id);
            return NotFound();
        }
        return Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        _logger.LogInformation("Listing printers");
        var list = await _mediator.Send(new ListPrintersQuery());
        return Ok(list);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePrinterCommand cmd)
    {
        if (id != cmd.Id) return BadRequest();
        _logger.LogInformation("Updating printer {Id}", id);
        var ok = await _mediator.Send(cmd);
        if (!ok)
        {
            _logger.LogWarning("Printer {Id} not found for update", id);
            return NotFound();
        }
        _logger.LogInformation("Printer {Id} updated", id);
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        _logger.LogInformation("Deleting printer {Id}", id);
        var ok = await _mediator.Send(new DeletePrinterCommand(id));
        if (!ok)
        {
            _logger.LogWarning("Printer {Id} not found for delete", id);
            return NotFound();
        }
        _logger.LogInformation("Printer {Id} deleted", id);
        return NoContent();
    }
}
