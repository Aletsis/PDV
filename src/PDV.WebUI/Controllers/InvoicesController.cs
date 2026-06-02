using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.Sales.Commands.CreateInvoice;
using PDV.Application.Features.Sales.Queries.GetInvoice;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvoicesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetInvoice), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetInvoice(Guid id)
    {
        var invoice = await _mediator.Send(new GetInvoiceQuery(id));
        if (invoice == null) return NotFound();
        return Ok(invoice);
    }
}
