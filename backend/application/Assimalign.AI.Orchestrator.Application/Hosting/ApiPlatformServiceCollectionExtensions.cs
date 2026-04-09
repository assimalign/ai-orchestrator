using System.Text.Json;
using System.Text.Json.Serialization;
using Assimalign.AI.Orchestrator.Application.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
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
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudiences =
                    [
                        settings.EntraClientId,
                        $"api://{settings.EntraClientId}",
                    ],
                    ValidateIssuer = true,
                };
            });

        services.AddAuthorization();
        return services;
    }
}
