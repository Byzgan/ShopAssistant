namespace ShopAssistant.Contracts.Models.User;

using Enums;

/// <summary>
/// Represents the identity context of the caller for the current request.
/// </summary>
public class User
{
    /// <summary>
    /// The internal numeric identifier of the authenticated user, if present.
    /// For anonymous/guest callers, this will be null or 0.
    /// </summary>
    public int? Id { get; set; }

    /// <summary>
    /// The display name of the caller, if available in claims.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The role of the caller (Anonymous, User, Admin), mapped from the JWT claim.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Preferred language of the caller, if provided.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// The external system or client application that originated the request
    /// </summary>
    public required string ExternalSystem { get; set; }

    /// <summary>
    /// A durable unique key used to correlate requests across sessions for anonymous callers.
    /// </summary>
    public required string UniqueKey { get; set; }

    /// <summary>
    /// A composite identifier for correlating this caller within the system.
    /// </summary>
    public string UniqueUserId => $"{ExternalSystem}:{Role}:{(Role != UserRole.Anonymous ? Id : UniqueKey)}";
}
