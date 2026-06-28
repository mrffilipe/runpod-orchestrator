using System.Net;
using System.Text.Json;
using RunpodOrchestrator.API.Common;
using RunpodOrchestrator.Application.Exceptions;
using RunpodOrchestrator.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace RunpodOrchestrator.API.Middlewares;

public sealed class ApplicationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApplicationExceptionMiddleware> _logger;

    public ApplicationExceptionMiddleware(RequestDelegate next, ILogger<ApplicationExceptionMiddleware> logger)
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
        catch (DomainValidationException ex)
        {
            _logger.LogInformation(ex, "Validation failed for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadRequest,
                ApiErrorMessages.DOMAIN_VALIDATION_TITLE,
                ex.Message);
        }
        catch (PodStartupTimeoutException ex)
        {
            _logger.LogWarning(ex, "Pod startup timed out for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.Conflict,
                ApiErrorMessages.DOMAIN_BUSINESS_RULE_TITLE,
                ex.Message);
        }
        catch (PodFaultedException ex)
        {
            _logger.LogError(ex, "Pod is faulted for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.ServiceUnavailable,
                ApiErrorMessages.POD_FAULTED_TITLE,
                ex.Message);
        }
        catch (DomainBusinessRuleException ex)
        {
            _logger.LogInformation(ex, "Business rule conflict for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.Conflict,
                ApiErrorMessages.DOMAIN_BUSINESS_RULE_TITLE,
                ex.Message);
        }
        catch (DomainNotFoundException ex)
        {
            _logger.LogInformation(ex, "Resource not found for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.NotFound,
                ApiErrorMessages.NOT_FOUND_TITLE,
                ex.Message);
        }
        catch (RunPodApiException ex)
        {
            _logger.LogError(ex, "RunPod API error for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadGateway,
                ApiErrorMessages.RUNPOD_API_TITLE,
                ex.Message);
        }
        catch (ManagedPodResolutionException ex)
        {
            _logger.LogError(ex, "Managed pod resolution failed for {Path}", context.Request.Path);
            var detail = ex.InnerException?.Message ?? ex.Message;
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadGateway,
                ApiErrorMessages.MANAGED_POD_TITLE,
                detail);
        }
        catch (VllmProxyException ex)
        {
            _logger.LogError(ex, "vLLM proxy error for {Path}", context.Request.Path);
            await WriteProblemAsync(
                context,
                HttpStatusCode.BadGateway,
                ApiErrorMessages.VLLM_PROXY_TITLE,
                ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            var isDevelopment = context.RequestServices.GetService<IHostEnvironment>()?.IsDevelopment() == true;
            var detail = isDevelopment
                ? FormatDevelopmentExceptionDetail(ex)
                : ApiErrorMessages.UNEXPECTED_ERROR_DETAIL;

            await WriteProblemAsync(
                context,
                HttpStatusCode.InternalServerError,
                ApiErrorMessages.UNHANDLED_SERVER_ERROR_TITLE,
                detail);
        }
    }

    private static string FormatDevelopmentExceptionDetail(Exception ex)
    {
        var parts = new List<string>();
        for (var current = ex; current is not null; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        return string.Join(" -> ", parts);
    }

    private static async Task WriteProblemAsync(
        HttpContext context,
        HttpStatusCode code,
        string title,
        string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)code,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = (int)code;
        context.Response.ContentType = ApiErrorMessages.PROBLEM_JSON_CONTENT_TYPE;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem), context.RequestAborted);
    }
}
