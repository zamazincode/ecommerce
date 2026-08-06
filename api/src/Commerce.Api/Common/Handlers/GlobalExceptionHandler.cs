using Commerce.Api.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Common.Handlers;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken ct)
    {
        // İstemci bağlantıyı kapattıysa cevap yazacak kimse kalmadı.
        // Log'u da kirletme — bu bir hata değil, normal bir sonlanma.
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
            return true;

        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Bu işlem için yetkiniz yok"),
            BusinessRuleException => (StatusCodes.Status400BadRequest, "İşlem gerçekleştirilemedi"),
            ConflictException => (StatusCodes.Status409Conflict, "Çakışan kayıt"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Kayıt başka biri tarafından değiştirildi"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen bir hata oluştu")
        };

        if (status >= 500)
            logger.LogError(exception, "İşlenmemiş hata. TraceId: {TraceId}", context.TraceIdentifier);
        else
            logger.LogWarning("İş kuralı hatası ({Status}): {Message}", status, exception.Message);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            // 500'de exception mesajını KULLANICIYA GÖSTERME.
            // "connection string invalid" gibi bilgiler sızabilir.
            Detail = status >= 500 && !env.IsDevelopment() ? null : exception.Message,
            Instance = $"{context.Request.Method} {context.Request.Path}"
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}