namespace ShopAssistant.Api.Middleware;

using Contracts.Enums;
using Contracts.Models.User;

/// <summary>
/// Middleware that extracts user claims from the JWT token and builds a request-scoped User context.
/// </summary>
public class UserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Collect claims even if not authenticated (frontend always sends a JWT per your design)
        var principal = context.User;
        var claims = principal.Claims.ToList();

        // Role (case-insensitive). Try standard role, then raw "role".
        var roleValue = claims.FirstOrDefault(c => c.Type == "userRole")?.Value;

        var userRole = Enum.TryParse<UserRole>(roleValue, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : UserRole.Anonymous;

        int userId = int.TryParse(claims.FirstOrDefault(c => c.Type == "userId")?.Value, out var id) ? id : 0;
        var userName = claims.FirstOrDefault(c => c.Type == "userName")?.Value ?? string.Empty;
        var userLanguage = claims.FirstOrDefault(c => c.Type == "userLanguage")?.Value;
        var externalSystem = claims.FirstOrDefault(c => c.Type == "systemName")?.Value ?? "UnknownSystem";
        var sessionId = claims.FirstOrDefault(c => c.Type == "session_id")?.Value ?? claims.FirstOrDefault(c => c.Type == "sid")?.Value;

        sessionId ??= Guid.NewGuid().ToString("N");

        // Build the request-scoped user context
        var user = new User
        {
            Id = (userRole != UserRole.Anonymous) ? userId : null,
            Name = userName,
            Role = userRole,
            Language = userLanguage,
            ExternalSystem = externalSystem,
            UniqueKey = sessionId
        };

        context.Items["User"] = user;

        await next(context);
    }
}
