using ShopAssistant.Contracts.Enums;

namespace ShopAssistant.Contracts.Models.Chat;

/// <summary>
/// Represents the result of a dialog step in a chat interaction, including status, field, prompt, and clarification details.
/// </summary>
public class DialogStepResult(DialogStepStatus status, string? field = null, string? prompt = null, ClarificationType? clarificationType = null, List<ClarificationAlternative>? alternatives = null)
{
    /// <summary>
    /// The status of the dialog step (e.g., InProgress, Completed).
    /// </summary>
    public DialogStepStatus Status { get; } = status;

    /// <summary>
    /// The name of the field associated with this dialog step, if any.
    /// </summary>
    public string? Field { get; } = field;

    /// <summary>
    /// The prompt message to present to the user, if any.
    /// </summary>
    public string? Prompt { get; } = prompt;

    /// <summary>
    /// The type of clarification required, if ambiguity is detected.
    /// </summary>
    public ClarificationType? ClarificationType { get; set; } = clarificationType;

    /// <summary>
    /// The list of alternatives for user clarification, if applicable.
    /// </summary>
    public List<ClarificationAlternative>? Alternatives { get; set; } = alternatives;
}