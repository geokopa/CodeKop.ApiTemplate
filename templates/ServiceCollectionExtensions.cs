using ApiTemplate.Customization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Enrichers.Span;

namespace ApiTemplate;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddRequiredServices(this WebApplicationBuilder builder)
    {
        Log.Logger = ConfigureLogging(builder.Configuration);

        builder.Host.UseSerilog();

        var configuration = builder.Configuration;

        builder.Services.AddResponseCompression(cfg =>
        {
            cfg.EnableForHttps = true;
            cfg.MimeTypes = ["application/json", "text/plain", "text/css", "application/javascript"];
            cfg.Providers.Add<GzipCompressionProvider>();
        });

        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        
        builder.Services.AddHealthChecks();
        
        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ctx =>
            {
                if (ctx.ProblemDetails.Extensions.TryGetValue("errors", out var errors))
                {
                    ctx.ProblemDetails.Extensions["validationErrors"] = errors;
                }
            };
        });
        
        builder.Services.AddControllers();
        builder.Services.AddExtendedOpenApi(configuration);

        return builder;
    }

    public static WebApplication UseRequiredServices(this WebApplication app)
    {
        var appName = app.Configuration.GetValue<string>("ApplicationName") ?? "Api.Template";

        app.UseSerilogRequestLogging(options =>
            {
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestPath", httpContext.Request.Path);
                };
            });

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwagger();
            app.UseSwaggerUI(opt =>
            {
                opt.SwaggerEndpoint("/swagger/v1/swagger.json", appName);
                opt.DefaultModelsExpandDepth(-1);
            });
            app.MapScalarApiReference();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.UseResponseCompression();

        app.MapHealthChecks("/health");

        app.MapControllers();
        
        return app;
    }

    private static Serilog.ILogger ConfigureLogging(IConfiguration configuration)
    {
        return new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProcessId()
            .Enrich.WithProcessName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .Enrich.WithThreadName()
            .Enrich.WithClientIp()
            .Enrich.WithCorrelationId()
            .Enrich.WithSpan()
            .CreateLogger();
    }
    
    private static void AddExtendedOpenApi(this IServiceCollection services, IConfiguration configuration)
    {
        var appName = configuration.GetValue<string>("ApplicationName") ?? "Api.Template";
        services.AddOpenApi();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = appName, Version = "v1" });
            c.UseInlineDefinitionsForEnums();
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer"
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            c.DocInclusionPredicate((_, _) => true);
        });
    }
}