using Dsw2026Tpi.Api.Configurations;
using Dsw2026Tpi.Api.Middlewares;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Dsw2026Tpi.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        try
        {
            Log.Information("Iniciando aplicación Dsw2026Tpi.Api");

            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

            builder.AddSerilogConfiguration();
            builder.Services.AddAppIdentity();
            builder.Services.AddAppAuthentication(builder.Configuration);
            builder.Services.AddSwaggerConfiguration();
            builder.Services.AddApplicationPersistence(builder.Configuration);
            builder.Services.AddAppCors(builder.Configuration);
            builder.Services.AddAppRateLimiting(builder.Configuration);
            builder.Services.AddAppDependencies();
            builder.Services.AddControllers()
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.InvalidModelStateResponseFactory = context =>
                        new BadRequestObjectResult(ModelStateErrors.From(context.ModelState));
                });
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            await app.SeedIdentityAsync();

            app.UseAppForwardedHeaders(builder.Configuration);
            app.UseMiddleware<SecurityHeadersMiddleware>();
            app.UseSerilogRequestLogging();
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            if (app.Environment.IsProduction())
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHealthChecks("/health-check")
                .RequireAuthorization()
                .RequireRateLimiting(RateLimitPolicies.General);

            Log.Information("Aplicación iniciada correctamente");

            await app.RunAsync();
        }
        catch (HostAbortedException)
        {
            Log.Information("El host fue abortado (normal durante migraciones de EF Core)");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "La aplicación falló al iniciar");
            throw;
        }
        finally
        {
            Log.Information("Cerrando aplicación");
            await Log.CloseAndFlushAsync();
        }
    }
}