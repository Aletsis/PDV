using Microsoft.AspNetCore.Mvc;
using PDV.Application.Common.Interfaces;
using PDV.HardwareAgent.Contracts.Requests;

namespace PDV.HardwareAgent.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payment");

        group.MapPost("/process", async (
            [FromBody] PaymentRequest request,
            [FromServices] IPaymentTerminalService paymentService,
            CancellationToken cancellationToken) =>
        {
            if (request.Amount <= 0)
            {
                return Results.BadRequest(new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "Monto inválido", "INVALID_AMOUNT"));
            }

            try
            {
                var result = await paymentService.ProcessPaymentAsync(
                    request.Amount, 
                    request.Reference, 
                    request.TransactionType, 
                    request.Protocol, 
                    request.Port, 
                    cancellationToken);
                
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Ok(new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, ex.Message, "EXCEPTION"));
            }
        });

        group.MapPost("/cancel", async (
            [FromBody] CancelPaymentRequest request,
            [FromServices] IPaymentTerminalService paymentService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.TransactionId))
            {
                return Results.BadRequest(new PaymentResultDto(false, string.Empty, string.Empty, string.Empty, string.Empty, "Transaction ID is required.", "MISSING_TX_ID"));
            }

            try
            {
                var result = await paymentService.CancelPaymentAsync(
                    request.TransactionId, 
                    request.Protocol, 
                    request.Port, 
                    cancellationToken);
                
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Ok(new PaymentResultDto(false, request.TransactionId, string.Empty, string.Empty, string.Empty, ex.Message, "EXCEPTION"));
            }
        });

        return app;
    }
}
