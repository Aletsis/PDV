using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.Cashiers.Commands.CreateCashier;
using PDV.Application.Features.Cashiers.Commands.DeleteCashier;
using PDV.Application.Features.Cashiers.Commands.UpdateCashier;
using PDV.Application.Features.Cashiers.Queries.GetCashier;
using PDV.Application.Features.Cashiers.Queries.ListCashiers;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashiersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashiersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashierCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var dto = await _mediator.Send(new GetCashierQuery(id));
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _mediator.Send(new ListCashiersQuery());
        return Ok(list);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCashierCommand cmd)
    {
        if (id != cmd.Id) return BadRequest();
        var ok = await _mediator.Send(cmd);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _mediator.Send(new DeleteCashierCommand(id));
        if (!ok) return NotFound();
        return NoContent();
    }
}
