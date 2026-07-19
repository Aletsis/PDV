using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FluentValidation;
using PDV.Domain.Exceptions;

namespace PDV.WebUI.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is not Microsoft.AspNetCore.Components.NavigationException)
        {
            _logger.LogError(ex, "Excepción no controlada capturada en el middleware.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        var statusCode = HttpStatusCode.InternalServerError;
        var errorResponse = new ErrorResponse("Error.Internal", "Ocurrió un error inesperado en el servidor.");

        switch (exception)
        {
            case ValidationException valEx:
                statusCode = HttpStatusCode.BadRequest;
                var errors = valEx.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage)).ToList();
                errorResponse = new ErrorResponse("Error.Validation", "Se presentaron errores de validación.", errors);
                break;

            case DomainException domEx:
                statusCode = HttpStatusCode.BadRequest;
                errorResponse = new ErrorResponse("Error.Domain", domEx.Message);
                break;

            case KeyNotFoundException knfEx:
                statusCode = HttpStatusCode.NotFound;
                errorResponse = new ErrorResponse("Error.NotFound", knfEx.Message);
                break;
        }

        context.Response.StatusCode = (int)statusCode;
        var result = JsonSerializer.Serialize(errorResponse);
        return context.Response.WriteAsync(result);
    }
}

public record ValidationError(string PropertyName, string ErrorMessage);
public record ErrorResponse(string Code, string Message, List<ValidationError>? Errors = null);
