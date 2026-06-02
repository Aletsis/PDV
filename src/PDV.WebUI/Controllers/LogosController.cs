using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.Logos.Commands.CreateLogo;
using PDV.Application.Features.Logos.Queries.GetLogo;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LogosController : ControllerBase
{
    private readonly IMediator _mediator;

    public LogosController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file == null) return BadRequest("file required");
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var id = await _mediator.Send(new CreateLogoCommand(bytes, file.FileName, file.ContentType));
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var res = await _mediator.Send(new GetLogoQuery(id));
        if (res == null) return NotFound();
        return Ok(res);
    }
}
