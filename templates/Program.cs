using ApiTemplate;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddRequiredServices();

var app = builder.Build();

app.UseRequiredServices();

try
{
    Log.Information("Starting application");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}