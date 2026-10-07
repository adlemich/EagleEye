namespace EagleEye.Service.Communication;

/// <summary>Extracts the token from an <c>Authorization: Bearer &lt;token&gt;</c> header (ADR-008 §5).</summary>
internal static class BearerToken
{
    private const string Scheme = "Bearer ";

    /// <summary>Returns the token, or <c>null</c> if the header is missing, not Bearer or empty.</summary>
    public static string? Parse(string? authorizationHeader)
    {
        if (authorizationHeader is null || !authorizationHeader.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader[Scheme.Length..].Trim();
        return token.Length == 0 ? null : token;
    }
}
