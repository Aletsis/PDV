using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.CashRegisters.Commands.CreateCashRegister;
using PDV.Application.Features.CashRegisters.Commands.DeleteCashRegister;
using PDV.Application.Features.CashRegisters.Commands.UpdateCashRegister;
using PDV.Application.Features.CashRegisters.Queries.GetCashRegister;
using PDV.Application.Features.CashRegisters.Queries.ListCashRegisters;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashRegistersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CashRegistersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashRegisterCommand cmd)
    {
        var id = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var dto = await _mediator.Send(new GetCashRegisterQuery(id));
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var list = await _mediator.Send(new ListCashRegistersQuery());
        return Ok(list);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCashRegisterCommand cmd)
    {
        if (id != cmd.Id) return BadRequest();
        var ok = await _mediator.Send(cmd);
        if (!ok) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var ok = await _mediator.Send(new DeleteCashRegisterCommand(id));
        if (!ok) return NotFound();
        return NoContent();
    }
}
