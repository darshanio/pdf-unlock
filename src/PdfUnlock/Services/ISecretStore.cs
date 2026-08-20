namespace PdfUnlock.Services;

/// <summary>
/// Where rule passwords actually live. Deliberately narrow: the platform
/// implementations differ wildly, and everything above this interface should be able to
/// ignore that.
/// </summary>
public interface ISecretStore
{
    /// <summary>False when no secret store is available on this platform, in which case
    /// the password store feature must refuse to store anything rather than falling back
    /// to something less safe.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable name of the store, for telling the user where their
    /// passwords went.</summary>
    string DisplayName { get; }

    /// <returns>True on success. A user who declines a keychain prompt is a failure, not
    /// an exception: it is an ordinary outcome that the caller must handle.</returns>
    bool TrySet(string key, string secret);

    bool TryGet(string key, out string secret);

    bool TryDelete(string key);
}

public static class SecretStoreFactory
{
    public static ISecretStore ForThisPlatform()
    {
        if (System.OperatingSystem.IsMacOS())
            return new MacKeychainSecretStore();
        if (System.OperatingSystem.IsWindows())
            return new WindowsCredentialSecretStore();
        return new UnavailableSecretStore();
    }
}
