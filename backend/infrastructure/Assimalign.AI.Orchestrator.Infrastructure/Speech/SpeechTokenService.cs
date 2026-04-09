using Assimalign.AI.Orchestrator.Application.Abstractions.Providers;
using Assimalign.AI.Orchestrator.Application.Configuration;
using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Infrastructure.Speech;

public sealed class SpeechTokenService(
    HttpClient httpClient,
    OrchestratorSettings settings,
    ISecretProvider secrets) : ISpeechTokenService
{
    public async Task<SpeechTokenResult?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.AzureSpeechRegion))
        {
            return null;
        }

        var speechKey = await secrets.GetAsync(
            settings.AzureSpeechKeySecretName,
            settings.AzureSpeechKey,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(speechKey))
        {
            return null;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://{settings.AzureSpeechRegion}.api.cognitive.microsoft.com/sts/v1.0/issueToken");
        request.Headers.Add("Ocp-Apim-Subscription-Key", speechKey);
        request.Content = new StringContent(string.Empty);
        request.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Speech token request failed with {(int)response.StatusCode}: {body}");
        }

        return new SpeechTokenResult
        {
            Token = body,
            Region = settings.AzureSpeechRegion,
            Voice = settings.SpeechVoice,
        };
    }
}
