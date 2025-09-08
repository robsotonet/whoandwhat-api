using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using WhoAndWhat.Application.Common;

namespace WhoAndWhat.Application.Behaviors;

/// <summary>
/// Pipeline behavior that validates commands and queries using FluentValidation
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly ILogger<ValidationBehavior<TRequest, TResponse>> _logger;

    public ValidationBehavior(
        IEnumerable<IValidator<TRequest>> validators,
        ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    {
        _validators = validators;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await System.Threading.Tasks.Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Any())
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogWarning("Validation failed for {RequestName}. Errors: {Errors}",
                requestName, string.Join("; ", failures.Select(f => f.ErrorMessage)));

            var errorMessages = failures.Select(f => f.ErrorMessage).ToList();
            var errorMessage = string.Join("; ", errorMessages);

            // If TResponse is Result<T>, return a failure result
            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var resultType = typeof(TResponse).GetGenericArguments()[0];
                var failureMethod = typeof(Result<>)
                    .MakeGenericType(resultType)
                    .GetMethod(nameof(Result<object>.Failure), new[] { typeof(string) });

                if (failureMethod != null)
                {
                    var result = failureMethod.Invoke(null, new[] { errorMessage });
                    return (TResponse)result!;
                }
            }

            throw new ValidationException(failures);
        }

        return await next();
    }
}
