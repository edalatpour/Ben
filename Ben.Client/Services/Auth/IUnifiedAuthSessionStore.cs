namespace Ben.Services.Auth;

public interface IUnifiedAuthSessionStore
{
    UnifiedAuthSession? Load();

    void Save(UnifiedAuthSession session);

    void Clear();
}
