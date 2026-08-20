namespace PdfUnlock.Services;

/// <summary>
/// Used where no platform secret store exists. It refuses rather than degrading: saving
/// bank passwords to a plain file would be worse than not offering the feature.
/// </summary>
public sealed class UnavailableSecretStore : ISecretStore
{
    public bool IsAvailable => false;

    public string DisplayName => "no secure store available on this system";

    public bool TrySet(string key, string secret) => false;

    public bool TryGet(string key, out string secret)
    {
        secret = string.Empty;
        return false;
    }

    public bool TryDelete(string key) => false;
}
