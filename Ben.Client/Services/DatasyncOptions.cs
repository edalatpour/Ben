namespace Ben.Services;

public sealed class DatasyncOptions
{
    public Uri? Endpoint { get; init; }
    public AuthTokenHandler? AuthHandler { get; init; }
}
