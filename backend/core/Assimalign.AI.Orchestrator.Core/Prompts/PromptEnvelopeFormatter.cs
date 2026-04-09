using Assimalign.AI.Orchestrator.Core.Models;
using Assimalign.AI.Orchestrator.Core.Utilities;

namespace Assimalign.AI.Orchestrator.Core.Prompts;

public static class PromptEnvelopeFormatter
{
    public static string BuildPromptText(ProviderPromptRequest request)
    {
        var lines = new List<string>
        {
            "Latest user request:",
            request.Requirement,
            string.Empty,
            "GitHub context:",
            JsonExtraction.Serialize((object?)request.Context ?? new Dictionary<string, string>()),
        };

        if (request.ThreadHistory is { Count: > 0 })
        {
            lines.Add(string.Empty);
            lines.Add("Thread history:");
            lines.Add(FormatThreadHistory(request.ThreadHistory));
        }

        if (request.Plan is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Codex draft:");
            lines.Add(request.Plan.Message);
            lines.Add(string.Empty);
            lines.Add($"Requires repository access: {(request.Plan.RequiresRepositoryAccess ? "yes" : "no")}");
            lines.Add(string.Empty);
            lines.Add($"Requires implementation: {(request.Plan.RequiresImplementation ? "yes" : "no")}");

            if (!string.IsNullOrWhiteSpace(request.Plan.SuggestedBranchName))
            {
                lines.Add(string.Empty);
                lines.Add($"Suggested branch name: {request.Plan.SuggestedBranchName}");
            }
        }

        if (request.Review is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Claude feedback:");
            lines.Add(request.Review.Message);
            lines.Add(string.Empty);
            lines.Add($"Claude aligned: {(request.Review.IsAligned ? "yes" : "no")}");

            if (request.Review.NeedsUserDecision)
            {
                lines.Add(string.Empty);
                lines.Add("Claude recommends user decision.");

                if (!string.IsNullOrWhiteSpace(request.Review.UserDecisionPrompt))
                {
                    lines.Add(request.Review.UserDecisionPrompt);
                }
            }
        }

        if (request.Debate is not null)
        {
            lines.Add(string.Empty);
            lines.Add("Latest Codex position:");
            lines.Add(request.Debate.Message);
            lines.Add(string.Empty);
            lines.Add($"Codex aligned: {(request.Debate.IsAligned ? "yes" : "no")}");

            if (request.Debate.NeedsUserDecision)
            {
                lines.Add(string.Empty);
                lines.Add("Codex recommends user decision.");

                if (!string.IsNullOrWhiteSpace(request.Debate.UserDecisionPrompt))
                {
                    lines.Add(request.Debate.UserDecisionPrompt);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.AdditionalContext))
        {
            lines.Add(string.Empty);
            lines.Add(request.AdditionalContext);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatThreadHistory(IReadOnlyList<ThreadMessage> threadHistory)
    {
        var relevantMessages = threadHistory
            .Where(message => message.Role is not ThreadMessageRole.System)
            .TakeLast(8)
            .ToArray();

        if (relevantMessages.Length == 0)
        {
            return "No prior thread history.";
        }

        return string.Join(
            Environment.NewLine,
            relevantMessages.Select(
                message =>
                    $"[{message.CreatedAt:HH:mm}] {BuildMessageLabel(message)}: {message.Content.Trim()}"));
    }

    private static string BuildMessageLabel(ThreadMessage message)
    {
        return message.Role switch
        {
            ThreadMessageRole.User => "User",
            ThreadMessageRole.Assistant => "Codex",
            ThreadMessageRole.Stage => $"{BuildProviderLabel(message.Provider)} {message.Stage?.ToString() ?? "update"}",
            _ => "System",
        };
    }

    private static string BuildProviderLabel(string? provider) =>
        provider?.ToLowerInvariant() switch
        {
            "claude" => "Claude",
            "codex" => "Codex",
            _ => "Agent",
        };
}
