using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PdfUnlock.Models;

namespace PdfUnlock.Services;

/// <summary>Why an attempt to save a rule did not happen. The caller shows these to the
/// user, so "it failed" is never the whole message.</summary>
public enum SaveRuleResult
{
    Saved,
    Updated,
    StoreDisabled,
    NoSecureStore,
    /// <summary>The keychain refused or the user dismissed its prompt.</summary>
    SecretRejected,
    InvalidFolderName,
}

/// <summary>
/// Folder rules and their secrets. Metadata is plain JSON beside the settings file;
/// passwords go to the platform secret store keyed by rule id.
/// </summary>
public sealed class PasswordStore
{
    private static readonly JsonSerializerOptions Format = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ISecretStore _secrets;
    private RuleFile _file = new();

    public PasswordStore(string directory, ISecretStore secrets)
    {
        _secrets = secrets;
        _path = Path.Combine(directory, "folder-rules.json");
        Load();
    }

    /// <summary>Mirrors the user's setting. While false, nothing is stored and no folder
    /// is even remembered as a candidate.</summary>
    public bool Enabled { get; set; }

    public bool HasSecureStore => _secrets.IsAvailable;

    public string SecureStoreName => _secrets.DisplayName;

    public IReadOnlyList<DirectoryPasswordRule> Rules => _file.Rules;

    public IReadOnlyList<FolderCandidate> Candidates => _file.Candidates;

    /// <summary>
    /// The rule governing a folder, or null. Where two rules share a name — possible
    /// after a rename — the most recently created wins, and <see cref="IsAmbiguous"/>
    /// reports that the choice was not clear-cut.
    /// </summary>
    public DirectoryPasswordRule? Match(string folderName)
    {
        if (!Enabled)
            return null;
        return _file.Rules
            .Where(rule => rule.Matches(folderName))
            .OrderByDescending(rule => rule.CreatedUtc)
            .FirstOrDefault();
    }

    public bool IsAmbiguous(string folderName) =>
        Enabled && _file.Rules.Count(rule => rule.Matches(folderName)) > 1;

    public bool TryGetPassword(DirectoryPasswordRule rule, out string password) =>
        _secrets.TryGet(rule.Id, out password);

    /// <summary>An existing rule for the same folder name, created from a different path.
    /// Surfaced at creation time so the user can say whether it is the same source.</summary>
    public DirectoryPasswordRule? ConflictingRule(string folderName, string originPath) =>
        _file.Rules.FirstOrDefault(rule =>
            rule.Matches(folderName)
            && !string.Equals(rule.OriginPath, originPath, StringComparison.OrdinalIgnoreCase));

    public SaveRuleResult Save(string folderName, string originPath, string password)
    {
        if (!Enabled)
            return SaveRuleResult.StoreDisabled;
        if (!_secrets.IsAvailable)
            return SaveRuleResult.NoSecureStore;
        if (!IsUsableFolderName(folderName, originPath))
            return SaveRuleResult.InvalidFolderName;

        var existing = _file.Rules.FirstOrDefault(rule => rule.Matches(folderName));
        var rule = existing ?? new DirectoryPasswordRule
        {
            FolderName = folderName,
            OriginPath = originPath,
        };

        if (!_secrets.TrySet(rule.Id, password))
            return SaveRuleResult.SecretRejected;

        if (existing is null)
            _file.Rules.Add(rule);
        rule.LastUsedUtc = DateTimeOffset.UtcNow;

        // A folder with a rule is no longer a suggestion.
        _file.Candidates.RemoveAll(candidate => candidate.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase));
        Persist();
        return existing is null ? SaveRuleResult.Saved : SaveRuleResult.Updated;
    }

    public void Delete(DirectoryPasswordRule rule)
    {
        // Secret first: a metadata entry without a secret is recoverable, an orphaned
        // secret is invisible.
        _secrets.TryDelete(rule.Id);
        _file.Rules.RemoveAll(existing => existing.Id == rule.Id);
        Persist();
    }

    public void DeleteAll()
    {
        foreach (var rule in _file.Rules.ToList())
            _secrets.TryDelete(rule.Id);
        _file.Rules.Clear();
        Persist();
    }

    /// <summary>Records a folder as a candidate, but only while the store is enabled:
    /// with it off, the app accumulates nothing about the user's filesystem.</summary>
    public void NoteFolder(string folderName, string path)
    {
        if (!Enabled || !IsUsableFolderName(folderName, path))
            return;
        if (_file.Rules.Any(rule => rule.Matches(folderName)))
            return;

        var existing = _file.Candidates.FirstOrDefault(candidate =>
            candidate.FolderName.Equals(folderName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.LastSeenUtc = DateTimeOffset.UtcNow;
            existing.Path = path;
        }
        else
        {
            _file.Candidates.Add(new FolderCandidate { FolderName = folderName, Path = path });
        }
        Persist();
    }

    public void ForgetCandidate(FolderCandidate candidate)
    {
        _file.Candidates.RemoveAll(existing =>
            existing.FolderName.Equals(candidate.FolderName, StringComparison.OrdinalIgnoreCase));
        Persist();
    }

    /// <summary>A volume root or an empty name would match far too broadly to be safe.</summary>
    private static bool IsUsableFolderName(string folderName, string path)
    {
        if (string.IsNullOrWhiteSpace(folderName) || folderName is "/" or "\\")
            return false;
        var root = Path.GetPathRoot(path);
        return !string.Equals(Path.GetDirectoryName(path) ?? path, root, StringComparison.OrdinalIgnoreCase)
               || !string.Equals(folderName, root?.Trim('/', '\\'), StringComparison.OrdinalIgnoreCase);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path))
                return;
            var text = File.ReadAllText(_path);
            if (!string.IsNullOrWhiteSpace(text))
                _file = JsonSerializer.Deserialize<RuleFile>(text) ?? new RuleFile();
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            _file = new RuleFile();
        }
    }

    private void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_file, Format));
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed class RuleFile
    {
        public List<DirectoryPasswordRule> Rules { get; set; } = [];
        public List<FolderCandidate> Candidates { get; set; } = [];
    }
}
