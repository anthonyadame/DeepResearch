using DeepResearchAgent.Models;

namespace DeepResearchAgent.Services;

/// <summary>
/// Helper service for formatting and displaying StreamState objects.
/// Useful for UI components and CLI applications.
/// </summary>
public static class StreamStateFormatter
{
    /// <summary>
    /// Formats a single StreamState field for console output.
    /// </summary>
    public static void WriteStreamStateField(string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"🗨️  StreamState {label}: {value}");
        }
    }

    /// <summary>
    /// Formats all StreamState fields for console output.
    /// Displays relevant fields that contain non-empty values.
    /// </summary>
    public static void WriteStreamStateFields(StreamState response)
    {
        var streamFields = new (string Label, string? Value)[]
        {
            ("Status", response.Status),
            ("ResearchId", response.ResearchId),
            ("UserQuery", response.UserQuery),
            ("BriefPreview", response.BriefPreview),
            ("ResearchBrief", response.ResearchBrief),
            ("DraftReport", response.DraftReport),
            ("RefinedSummary", response.RefinedSummary),
            ("FinalReport", response.FinalReport),
            ("SupervisorUpdate", response.SupervisorUpdate),
            ("SupervisorUpdateCount", response.SupervisorUpdateCount > 0 ? response.SupervisorUpdateCount.ToString() : null)
        };

        foreach (var (label, value) in streamFields)
        {
            WriteStreamStateField(label, value);
        }
    }

    /// <summary>
    /// Gets a human-readable summary of StreamState progress.
    /// Useful for status bars and progress indicators.
    /// </summary>
    public static string GetProgressSummary(StreamState state)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(state.Status))
            parts.Add($"Status: {state.Status}");

        if (!string.IsNullOrWhiteSpace(state.ResearchBrief))
            parts.Add("✓ Research Brief");

        if (!string.IsNullOrWhiteSpace(state.DraftReport))
            parts.Add("✓ Draft Report");

        if (state.SupervisorUpdateCount > 0)
            parts.Add($"✓ Supervisor Updates ({state.SupervisorUpdateCount})");

        if (!string.IsNullOrWhiteSpace(state.RefinedSummary))
            parts.Add("✓ Refined Summary");

        if (!string.IsNullOrWhiteSpace(state.FinalReport))
            parts.Add("✓ Final Report");

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Extracts the primary content based on current pipeline phase.
    /// Useful for displaying the most relevant information to users.
    /// </summary>
    public static string GetPhaseContent(StreamState state)
    {
        if (!string.IsNullOrWhiteSpace(state.FinalReport))
            return state.FinalReport;

        if (!string.IsNullOrWhiteSpace(state.RefinedSummary))
            return state.RefinedSummary;

        if (!string.IsNullOrWhiteSpace(state.DraftReport))
            return state.DraftReport;

        if (!string.IsNullOrWhiteSpace(state.ResearchBrief))
            return state.ResearchBrief;

        if (!string.IsNullOrWhiteSpace(state.BriefPreview))
            return state.BriefPreview;

        return state.SupervisorUpdate ?? state.Status ?? string.Empty;
    }
}
