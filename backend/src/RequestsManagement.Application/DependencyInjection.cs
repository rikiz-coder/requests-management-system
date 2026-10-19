using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RequestsManagement.Application.Requests.Contracts;
using RequestsManagement.Application.Requests.Services;
using RequestsManagement.Application.Requests.Validation;

namespace RequestsManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        // Validators are stateless: one instance for the whole application.
        services.AddSingleton<IValidator<RequestFilter>, RequestFilterValidator>();
        services.AddSingleton<IValidator<RequestQuery>, RequestQueryValidator>();
        services.AddSingleton<IValidator<UpdateStatusCommand>, UpdateStatusCommandValidator>();
        services.AddSingleton<IValidator<BulkUpdateStatusCommand>, BulkUpdateStatusCommandValidator>();

        // Services depend on the repository (DbContext), so they live per request.
        services.AddScoped<RequestQueryService>();
        services.AddScoped<RequestCommandService>();

        return services;
    }
}
