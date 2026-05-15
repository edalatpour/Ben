using System.Text.Json;

namespace Ben.Services.Auth;

public sealed class PreferencesUnifiedAuthSessionStore : IUnifiedAuthSessionStore
{
    private const string SessionKey = "UnifiedAuth.Session";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public UnifiedAuthSession? Load()
    {
        try
        {
            string? raw = Preferences.Default.Get(SessionKey, (string?)null);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return JsonSerializer.Deserialize<UnifiedAuthSession>(raw, SerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(UnifiedAuthSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        string raw = JsonSerializer.Serialize(session, SerializerOptions);
        Preferences.Default.Set(SessionKey, raw);
    }

    public void Clear()
    {
        Preferences.Default.Remove(SessionKey);
    }
}
