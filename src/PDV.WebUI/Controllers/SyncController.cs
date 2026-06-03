using MediatR;
using Microsoft.AspNetCore.Mvc;
using PDV.Application.Features.Sync.Commands;
using PDV.Application.Features.Sync.Dtos;
using PDV.Application.Features.Clients.Queries.GetClientsDelta;

namespace PDV.WebUI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SyncController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly ILogger<SyncController> _logger;

    public SyncController(ISender mediator, ILogger<SyncController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok("pong");
    }

    [HttpPost("receive")]
    public async Task<IActionResult> Receive([FromBody] OutboxSyncDto dto)
    {
        try
        {
            var result = await _mediator.Send(new ProcessSyncEventCommand(dto));
            if (result.Success)
            {
                return Ok(result);
            }
            return BadRequest(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing sync message {Id}", dto.MessageId);
            return Problem(ex.Message);
        }
    }

    [HttpPost("receive-batch")]
    public async Task<IActionResult> ReceiveBatch([FromBody] IEnumerable<OutboxSyncDto> dtos)
    {
        var results = new List<MessageSyncResult>();

        foreach (var dto in dtos)
        {
            try
            {
                var result = await _mediator.Send(new ProcessSyncEventCommand(dto));
                results.Add(new MessageSyncResult(dto.MessageId, result.Success, result.ErrorMessage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing sync message {Id} in batch", dto.MessageId);
                results.Add(new MessageSyncResult(dto.MessageId, false, ex.Message));
            }
        }

        return Ok(new BatchSyncResult(results));
    }

    [HttpGet("clients-delta")]
    public async Task<IActionResult> GetClientsDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new GetClientsDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting clients delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("products-delta")]
    public async Task<IActionResult> GetProductsDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Products.Queries.GetProductsDelta.GetProductsDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("branches-delta")]
    public async Task<IActionResult> GetBranchesDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Branches.Queries.GetBranchesDelta.GetBranchesDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting branches delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("unidades-medida")]
    public async Task<IActionResult> GetUnidadesMedida([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime();
            var result = await _mediator.Send(new PDV.Application.Features.UnidadesMedida.GetUnidadesMedidaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting units of measure since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("cash-registers-delta")]
    public async Task<IActionResult> GetCashRegistersDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.CashRegisters.Queries.GetCashRegistersDelta.GetCashRegistersDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cash registers delta since {Since}", since);
            return Problem(ex.Message);
        }
    }

    [HttpGet("users-delta")]
    public async Task<IActionResult> GetUsersDelta([FromQuery] DateTime? since)
    {
        try
        {
            var sinceUtc = since?.ToUniversalTime() ?? DateTime.MinValue;
            var result = await _mediator.Send(new PDV.Application.Features.Sync.Queries.GetUsersDelta.GetUsersDeltaQuery(sinceUtc));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users delta since {Since}", since);
            return Problem(ex.Message);
        }
    }
}
