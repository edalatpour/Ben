namespace Ben.Models;

/// <summary>
/// Normalized identity object returned after sign-in with any provider
/// (Microsoft via MSAL, Apple via External ID, Google via External ID).
/// </summary>
public class UnifiedIdentity
{
    /// <summary>The identity provider: "Microsoft", "Apple", or "Google".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>The subject claim (unique user identifier) from the id_token.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>The user's email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>The user's display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The raw id_token JWT returned by the identity provider.</summary>
    public string IdToken { get; set; } = string.Empty;

    /// <summary>The access_token returned by the identity provider.</summary>
    public string AccessToken { get; set; } = string.Empty;
}
