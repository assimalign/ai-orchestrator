using Assimalign.AI.Orchestrator.Core.Models;

namespace Assimalign.AI.Orchestrator.Application.Abstractions.Providers;

public interface ISpeechTokenService
{
    Task<SpeechTokenResult?> GetTokenAsync(CancellationToken cancellationToken = default);
}
