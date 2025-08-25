namespace ShopAssistant.Infrastructure.Identity;

using Contracts.Enums;
using ShopAssistant.Contracts.Interfaces.User;
using ShopAssistant.Contracts.Models.User;

/// <summary>
/// Temporary authentication service used for testing/demo purposes.
/// <para>
/// This implementation does NOT validate passwords, issue JWTs, or consult any user store.
/// It simply maps well-known usernames to canned identities so you can exercise the pipeline
/// (middleware, role mapping, UniqueUserId composition, etc.).
/// </para>
/// <remarks>
/// Production code should:
/// <list type="number">
///   <item>Validate credentials (or federate via OIDC),</item>
///   <item>Issue a JWT with standard claims (sub, sid, role, etc.),</item>
///   <item>Derive <c>UniqueKey</c> from a durable claim (e.g., <c>anon_id</c> or <c>sid</c>),</item>
///   <item>Return minimal PII, and enforce proper error handling/logging.</item>
/// </list>
/// </remarks>
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Creates a canned <see cref="User"/> for known usernames: "admin", "user", "guest".
    /// Throws <see cref="UnauthorizedAccessException"/> for anything else.
    /// </summary>
    /// <param name="model">Simple login request carrying a username (used only for routing to a test identity).</param>
    /// <returns>A <see cref="User"/> instance suitable for wiring into the request context.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="model"/> is null.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when username is empty or unknown.</exception>
    public User Login(LoginModel model)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (string.IsNullOrWhiteSpace(model.Username))
            throw new UnauthorizedAccessException("Username is required");

        // Lowercase the username so test inputs are case-insensitive.
        // Known values: "admin", "user", "guest".
        return model.Username.ToLowerInvariant() switch
        {
            // Admin: authenticated principal with fixed Id and role.
            "admin" => new User
            {
                Id = 1,
                Name = "admin",
                Role = UserRole.Admin,
                Language = "en",
                UniqueKey = "sess-07cf41c1",
                ExternalSystem = "Umsport"
            },

            // Regular user: authenticated principal with fixed Id and role.
            "user" => new User
            {
                Id = 2,
                Name = "user",
                Role = UserRole.User,
                Language = "no",
                UniqueKey = "sess-07cf41c2",
                ExternalSystem = "Umsport"
            },

            // Guest: anonymous principal; no numeric Id on purpose to exercise UniqueKey fallback.
            "guest" => new User
            {
                Id = null,
                Role = UserRole.Anonymous,
                Language = "no",
                UniqueKey = "sess-07cf41c3",
                ExternalSystem = "Umsport"
            },

            // Any other username is rejected in this stub.
            _ => throw new UnauthorizedAccessException("Invalid username or password")
        };
    }
}
