using FluentValidation;

namespace Commerce.Api.Common.Filters;

public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
    where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return TypedResults.Problem(
                title: "İstek gövdesi okunamadı.",
                statusCode: StatusCodes.Status400BadRequest);

        var result = await validator.ValidateAsync(
            argument, context.HttpContext.RequestAborted);

        if (!result.IsValid)
            return TypedResults.ValidationProblem(result.ToDictionary());

        return await next(context);
    }
}

public static class ValidationFilterExtensions
{
    /// Kullanımı: .WithValidation<CreateProductRequest>()
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder)
        where T : class
        => builder
            .AddEndpointFilter<ValidationFilter<T>>()
            .ProducesValidationProblem();
}