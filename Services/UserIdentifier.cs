using System.Security.Claims;

namespace Passwords.Services;

public static class UserIdentifier
{
    private static readonly string[] ClaimTypesToCheck =
    {
        "preferred_username",
        "email",
        ClaimTypes.Email,
        ClaimTypes.Upn,
        "upn",
        "unique_name"
    };

    public static string? GetUserIdentifier(ClaimsPrincipal? principal)
    {
        if (principal == null)
        {
            return null;
        }

        foreach (var claimType in ClaimTypesToCheck)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return principal.Identity?.Name;
    }
}
