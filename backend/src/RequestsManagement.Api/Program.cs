using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Api.ErrorHandling;
using RequestsManagement.Application;
using RequestsManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Enums travel as names ("InProgress"), in normal responses and in Problem Details bodies alike
// (Problem Details are written with the minimal-API JSON options, so both are configured).
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Model-binding errors (malformed JSON, unknown enum name, wrong type) – logged with field names only.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    var defaultFactory = options.InvalidModelStateResponseFactory;
    options.InvalidModelStateResponseFactory = context =>
    {
        var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Validation");
        logger.LogWarning("Invalid input for {Method} {Path}: {Fields}",
            context.HttpContext.Request.Method, context.HttpContext.Request.Path,
            string.Join(", ", context.ModelState.Where(e => e.Value?.Errors.Count > 0).Select(e => e.Key)));
        return defaultFactory(context);
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

/// <summary>Entry point; public so that integration tests can host the API (WebApplicationFactory).</summary>
public partial class Program;
