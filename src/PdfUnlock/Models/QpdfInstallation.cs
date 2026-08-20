namespace PdfUnlock.Models;

/// <summary>How resolution found an installation. Displayed to the user, because this
/// is the fact that explains a later breakage. See CONTEXT.md, "Resolution".</summary>
public enum QpdfOrigin
{
    UserChosen,
    SearchPath,
    ConventionalLocation,
    Bundled,
}

/// <param name="Version">The major version. 11 is the minimum usable: it introduced
/// --password-file, which is how passwords stay out of the process argument list.</param>
public sealed record QpdfInstallation(string ExecutablePath, string VersionText, int Version, QpdfOrigin Origin)
{
    public const int MinimumVersion = 11;

    public bool IsUsable => Version >= MinimumVersion;

    public string OriginDescription => Origin switch
    {
        QpdfOrigin.UserChosen => "using the location you chose",
        QpdfOrigin.SearchPath => "found on PATH",
        QpdfOrigin.ConventionalLocation => "found in a standard install location",
        QpdfOrigin.Bundled => "using the copy bundled with PDF Unlock",
        _ => "unknown",
    };
}
