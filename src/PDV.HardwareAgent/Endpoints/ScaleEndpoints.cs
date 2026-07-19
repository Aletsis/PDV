using Microsoft.AspNetCore.Mvc;
using PDV.Application.Common.Interfaces;
using PDV.HardwareAgent.Contracts.Requests;

namespace PDV.HardwareAgent.Endpoints;

public static class ScaleEndpoints
{
    public static IEndpointRouteBuilder MapScaleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/scale");

        group.MapPost("/weight", async (
            [FromBody] ScaleRequest request,
            [FromServices] IScaleService scaleService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Port))
            {
                return Results.BadRequest(new ScaleWeightDto(0, "kg", false, false, "Port name is required."));
            }

            try
            {
                var result = await scaleService.ReadWeightAsync(request.Port, request.BaudRate, request.Protocol, cancellationToken);
                if (!result.Success)
                {
                    return Results.Ok(result); // Return 200 with error details inside DTO
                }
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.Ok(new ScaleWeightDto(0, "kg", false, false, ex.Message));
            }
        });

        return app;
    }
}
