using System.Text.Json;
using System.Text.Json.Serialization;
using Assimalign.AI.Orchestrator.Application.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Assimalign.AI.Orchestrator.Application.Hosting;

public static class ApiPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddAssimalignAiOrchestratorApiPlatform(
        this IServiceCollection services,
        OrchestratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.EntraTenantId) || string.IsNullOrWhiteSpace(settings.EntraClientId))
        {
            throw new InvalidOperationException(
                "ENTRA_TENANT_ID and ENTRA_CLIENT_ID must be configured for the API.");
        }

        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        services.AddCors(options =>
        {
            options.AddPolicy(
                "frontend",
                policy =>
                {
                    if (settings.CorsOrigin == "*")
                    {
                        policy
                            .SetIsOriginAllowed(_ => true)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                    else
                    {
                        policy
                            .WithOrigins(settings.CorsOrigin)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                });
        });

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"https://login.microsoftonline.com/{settings.EntraTenantId}/v2.0";
                options.IncludeErrorDetails = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudiences =
                    [
                        settings.EntraClientId,
                        $"api://{settings.EntraClientId}",
                    ],
                    ValidateIssuer = true,
                    ValidIssuers =
                    [
                        $"https://login.microsoftonline.com/{settings.EntraTenantId}/v2.0",
                        $"https://login.microsoftonline.com/{settings.EntraTenantId}/",
                        $"https://sts.windows.net/{settings.EntraTenantId}/",
                    ],
                };
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogWarning(
                            context.Exception,
                            "Bearer authentication failed for {Path}.",
                            context.HttpContext.Request.Path);

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");

                        logger.LogInformation(
                            "Bearer challenge for {Path}. Error={Error}; Description={Description}.",
                            context.HttpContext.Request.Path,
                            context.Error,
                            context.ErrorDescription);

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtBearer");
                        var principal = context.Principal;
                        var audience = principal?.FindFirst("aud")?.Value;
                        var issuer = principal?.FindFirst("iss")?.Value;
                        var version = principal?.FindFirst("ver")?.Value;
                        var scope = principal?.FindFirst("scp")?.Value;

                        logger.LogDebug(
                            "Bearer token validated for {Path}. aud={Audience}; iss={Issuer}; ver={Version}; scp={Scope}.",
                            context.HttpContext.Request.Path,
                            audience,
                            issuer,
                            version,
                            scope);

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}
