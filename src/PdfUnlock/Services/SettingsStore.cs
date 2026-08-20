using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using PdfUnlock.Models;

namespace PdfUnlock.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON in the platform's per-user
/// application data directory. A missing, empty or unparseable file yields defaults —
/// settings are a convenience, and losing them must never block the app from starting.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _path;

    public SettingsStore()
    {
        _directory = ResolveDirectory();
        _path = Path.Combine(_directory, "settings.json");
    }

    public string Path_ => _path;

    /// <summary>Where per-user data lives; the folder-rule file shares it.</summary>
    public string DataDirectory => _directory;

    private static string ResolveDirectory()
    {
        // .NET maps ApplicationData to ~/.config on macOS, which is not where a Mac user
        // expects an app's data to live.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "PDF Unlock");

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PDF Unlock");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();

            var text = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(text))
                return new AppSettings();

            return JsonSerializer.Deserialize<AppSettings>(text) ?? new AppSettings();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the bad file rather than deleting it: it is the only evidence of what
            // went wrong, and it may contain settings the user wants back.
            SetAside();
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Format));
            // Move over the top, so a crash mid-write cannot leave a half-written file
            // that fails to parse on next launch.
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Settings are not worth an error dialog. The app works without them.
        }
    }

    private void SetAside()
    {
        try
        {
            if (File.Exists(_path))
                File.Move(_path, _path + ".corrupt", overwrite: true);
        }
        catch
        {
            // Nothing further to do.
        }
    }
}
