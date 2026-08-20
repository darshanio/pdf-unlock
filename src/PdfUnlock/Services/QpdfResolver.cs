using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PdfUnlock.Models;

namespace PdfUnlock.Services;

/// <summary>
/// Finds the qpdf installation to drive. Candidates are tried in precedence order —
/// user-chosen, PATH, conventional locations, bundled — and the first one new enough
/// wins. See docs/adr/0001-qpdf-resolution-strategy.md.
/// </summary>
public sealed class QpdfResolver
{
    private static readonly Regex VersionPattern = new(@"qpdf version (\d+)\.(\S+)", RegexOptions.IgnoreCase);

    private readonly IReadOnlyList<string>? _conventionalOverride;

    public QpdfResolver() { }

    /// <summary>
    /// Overrides the conventional install locations. Exists so that the bundled-copy path
    /// can be exercised without a machine that happens to have no qpdf installed.
    /// </summary>
    public QpdfResolver(IReadOnlyList<string> conventionalLocations) =>
        _conventionalOverride = conventionalLocations;

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private static string ExecutableName => IsWindows ? "qpdf.exe" : "qpdf";

    /// <summary>
    /// Resolves once, returning the winning installation, or null when nothing usable
    /// exists. Rejected candidates are reported so the UI can say *why* — "your qpdf is
    /// version 9" is a different problem from "you have no qpdf".
    /// </summary>
    public async Task<(QpdfInstallation? Resolved, List<QpdfInstallation> TooOld)> ResolveAsync(string? userChosenPath)
    {
        var tooOld = new List<QpdfInstallation>();

        foreach (var (path, origin) in EnumerateCandidates(userChosenPath))
        {
            var installation = await ProbeAsync(path, origin);
            if (installation is null)
                continue;
            if (installation.IsUsable)
                return (installation, tooOld);
            tooOld.Add(installation);
        }

        return (null, tooOld);
    }

    /// <summary>
    /// Examines one specific path the user chose, reporting *why* it is unusable rather
    /// than quietly falling through to another candidate. A rejected choice must leave the
    /// previous resolution standing.
    /// </summary>
    public async Task<(QpdfInstallation? Found, string? Problem)> InspectAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (null, "No location given.");
        if (!File.Exists(path))
            return (null, "There is nothing at that location.");

        var installation = await ProbeAsync(path, QpdfOrigin.UserChosen);
        if (installation is null)
            return (null, "That program does not report itself as qpdf.");
        if (!installation.IsUsable)
            return (null, $"That is qpdf {installation.VersionText}. " +
                          $"Version {QpdfInstallation.MinimumVersion} or newer is required, because " +
                          "older versions cannot take a password without exposing it to other programs.");

        return (installation, null);
    }

    private IEnumerable<(string Path, QpdfOrigin Origin)> EnumerateCandidates(string? userChosenPath)
    {
        if (!string.IsNullOrWhiteSpace(userChosenPath))
            yield return (userChosenPath, QpdfOrigin.UserChosen);

        foreach (var path in FromSearchPath())
            yield return (path, QpdfOrigin.SearchPath);

        var conventional = _conventionalOverride is not null
            ? _conventionalOverride.Where(File.Exists)
            : FromConventionalLocations();
        foreach (var path in conventional)
            yield return (path, QpdfOrigin.ConventionalLocation);

        var bundled = BundledPath();
        if (bundled is not null)
            yield return (bundled, QpdfOrigin.Bundled);
    }

    private static IEnumerable<string> FromSearchPath()
    {
        var searchPath = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(searchPath))
            yield break;

        foreach (var directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), ExecutableName);
            if (File.Exists(candidate))
                yield return candidate;
        }
    }

    private static IEnumerable<string> FromConventionalLocations()
    {
        if (IsWindows)
        {
            // winget and the official installer both land under Program Files, in a
            // version-stamped directory, so the exact path is not knowable in advance.
            foreach (var root in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     })
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                IEnumerable<string> versionDirectories;
                try
                {
                    versionDirectories = Directory.EnumerateDirectories(root, "qpdf*");
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var directory in versionDirectories)
                {
                    var candidate = Path.Combine(directory, "bin", ExecutableName);
                    if (File.Exists(candidate))
                        yield return candidate;
                }
            }
            yield break;
        }

        // Homebrew on Apple Silicon, Homebrew on Intel, and a from-source install.
        foreach (var candidate in new[] { "/opt/homebrew/bin/qpdf", "/usr/local/bin/qpdf", "/usr/bin/qpdf" })
            if (File.Exists(candidate))
                yield return candidate;
    }

    /// <summary>
    /// The last-resort copy shipped inside the app. Absent in a plain `dotnet run`, which
    /// is why the setup banner still has a job to do during development.
    /// </summary>
    private static string? BundledPath()
    {
        var directory = AppContext.BaseDirectory;
        var candidate = Path.Combine(directory, "qpdf", ExecutableName);
        return File.Exists(candidate) ? candidate : null;
    }

    private static async Task<QpdfInstallation?> ProbeAsync(string path, QpdfOrigin origin)
    {
        try
        {
            var result = await ProcessRunner.RunAsync(path, ["--version"]);
            var match = VersionPattern.Match(result.AllOutput);
            if (!match.Success)
                return null;

            var versionText = match.Value.Replace("qpdf version ", string.Empty, StringComparison.OrdinalIgnoreCase);
            return new QpdfInstallation(path, versionText, int.Parse(match.Groups[1].Value), origin);
        }
        catch (Exception)
        {
            // Not executable, wrong architecture, or not qpdf at all. Try the next candidate.
            return null;
        }
    }
}
