using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.InventoryMovements.Commands.RegisterInventoryMovement;
using PDV.Application.Features.InventoryMovements.Queries.GetInventoryMovements;

namespace PDV.WebUI.Controllers;

[Authorize(Roles = "Admin,Manager")]
[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("movements")]
    public async Task<IActionResult> RegisterMovement([FromBody] RegisterInventoryMovementCommand command)
    {
        var result = await _mediator.Send(command);
        if (!result) return BadRequest(new { error = "No se pudo registrar el movimiento de inventario." });
        return Ok(new { success = true });
    }

    [HttpGet("movements")]
    public async Task<IActionResult> GetMovements([FromQuery] GetInventoryMovementsQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }
}
