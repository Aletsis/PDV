using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.Sales.Commands.CancelSale;
using PDV.Application.Features.Sales.Commands.CreateSale;
using PDV.Application.Features.Sales.Commands.ReturnItem;
using PDV.Application.Features.Sales.Commands.ReturnSale;
using PDV.Application.Features.Sales.Commands.CashCut;
using PDV.Application.Features.Sales.Commands.CashCollection;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleCommand command)
    {
        var id = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetSale), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public IActionResult GetSale(Guid id)
    {
        return Ok(new { id });
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelSale(Guid id, [FromBody] CancelSaleRequest req)
    {
        try
        {
            var ok = await _mediator.Send(new CancelSaleCommand(id, req.Reason ?? string.Empty, req.UserId ?? string.Empty));
            if (!ok) return BadRequest();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("items/{itemId}/return")]
    public async Task<IActionResult> ReturnItem(Guid itemId, [FromBody] ReturnItemRequest req)
    {
        try
        {
            var ok = await _mediator.Send(new ReturnItemCommand(itemId, req.Quantity, req.Reason ?? string.Empty, req.UserId ?? string.Empty));
            if (!ok) return BadRequest();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/return")]
    public async Task<IActionResult> ReturnSale(Guid id, [FromBody] ReturnSaleRequest req)
    {
        try
        {
            var ok = await _mediator.Send(new ReturnSaleCommand(id, req.Reason ?? string.Empty, req.UserId ?? string.Empty));
            if (!ok) return BadRequest();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("/api/cashcuts")]
    public async Task<IActionResult> CashCut([FromBody] CashCutCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(null, new { id }, new { id });
    }

    [HttpPost("/api/cashcollections")]
    public async Task<IActionResult> CashCollection([FromBody] CashCollectionCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(null, new { id }, new { id });
    }

    public class CancelSaleRequest { public string? Reason { get; set; } public string? UserId { get; set; } }
    public class ReturnItemRequest { public int Quantity { get; set; } public string? Reason { get; set; } public string? UserId { get; set; } }
    public class ReturnSaleRequest { public string? Reason { get; set; } public string? UserId { get; set; } }
}
