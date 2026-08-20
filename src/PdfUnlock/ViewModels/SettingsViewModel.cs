using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfUnlock.Models;
using PdfUnlock.Services;

namespace PdfUnlock.ViewModels;

public enum SettingsSection
{
    General,
    Passwords,
    Qpdf,
    Licences,
}

/// <summary>
/// The settings frame: a rail of sections, one of which may push a detail page over
/// itself. Section contents beyond General arrive with features 0002, 0003 and 0005.
/// </summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly QpdfResolver _resolver = new();

    public SettingsViewModel(SettingsStore store, AppSettings settings, bool isBatchRunning,
                             PasswordStore? passwords = null)
    {
        _store = store;
        Passwords = passwords ?? new PasswordStore(store.DataDirectory, SecretStoreFactory.ForThisPlatform());
        Passwords.Enabled = settings.PasswordStoreEnabled;
        Settings = settings;
        IsBatchRunning = isBatchRunning;
        _contextMenuRunsImmediately = settings.ContextMenuBehaviour == ContextMenuBehaviour.RunImmediately;
        _checkForUpdates = settings.CheckForUpdates;
        _passwordStoreEnabled = settings.PasswordStoreEnabled;
        RefreshRules();
        _ = RefreshQpdfAsync();
    }

    public AppSettings Settings { get; }

    public PasswordStore Passwords { get; }

    public ObservableCollection<DirectoryPasswordRule> Rules { get; } = [];
    public ObservableCollection<FolderCandidate> Candidates { get; } = [];

    public bool HasSecureStore => Passwords.HasSecureStore;

    public string SecureStoreNotice => Passwords.HasSecureStore
        ? $"Passwords are kept in {Passwords.SecureStoreName}."
        : $"Saving is unavailable: {Passwords.SecureStoreName}. Passwords will not be written anywhere.";

    public bool HasRules => Rules.Count > 0;
    public bool HasCandidates => Candidates.Count > 0;

    /// <summary>Shown under the folder field so the matching rule is demonstrated rather
    /// than described.</summary>
    public string MatchingExample =>
        Rules.Count > 0
            ? $"A rule for “{Rules[0].FolderName}” matches /Documents/2025/{Rules[0].FolderName}/ and " +
              $"/Documents/2026/{Rules[0].FolderName}/ alike — the year changes, the folder name does not."
            : "A rule for “A Bank” matches /Documents/2025/A Bank/ and /Documents/2026/A Bank/ alike — " +
              "the year changes, the folder name does not.";

    [ObservableProperty] private string _ruleNotice = string.Empty;

    /// <summary>A qpdf path changed mid-run applies to the next run, not the current one.
    /// The UI says so rather than silently doing the surprising thing.</summary>
    public bool IsBatchRunning { get; }

    public IReadOnlyList<SettingsSection> Sections { get; } =
        [SettingsSection.General, SettingsSection.Passwords, SettingsSection.Qpdf, SettingsSection.Licences];

    [ObservableProperty] private SettingsSection _selectedSection = SettingsSection.General;

    /// <summary>Non-null while a detail page is pushed over the current section. The rail
    /// selection deliberately does not change while one is open.</summary>
    [ObservableProperty] private string? _detailPageTitle;

    [ObservableProperty] private bool _contextMenuRunsImmediately;
    [ObservableProperty] private bool _checkForUpdates;
    [ObservableProperty] private bool _passwordStoreEnabled;

    [ObservableProperty] private string _qpdfStatus = "Looking for qpdf…";
    [ObservableProperty] private string _qpdfPathText = string.Empty;
    [ObservableProperty] private string _qpdfOrigin = string.Empty;
    [ObservableProperty] private bool _isQpdfUsable;

    public bool IsGeneral => SelectedSection == SettingsSection.General && DetailPageTitle is null;
    public bool IsPasswords => SelectedSection == SettingsSection.Passwords && DetailPageTitle is null;
    public bool IsQpdf => SelectedSection == SettingsSection.Qpdf && DetailPageTitle is null;
    public bool IsLicences => SelectedSection == SettingsSection.Licences && DetailPageTitle is null;
    public bool IsDetailOpen => DetailPageTitle is not null;

    public string SettingsFilePath => _store.Path_;

    [RelayCommand]
    private void Select(string section) => SelectedSection = System.Enum.Parse<SettingsSection>(section);

    /// <summary>Pushes a detail page. Feature 0002's rule list is the first real user.</summary>
    [RelayCommand]
    private void OpenDetail(string title) => DetailPageTitle = title;

    [RelayCommand]
    private void CloseDetail() => DetailPageTitle = null;

    [RelayCommand]
    private void DeleteRule(DirectoryPasswordRule rule)
    {
        Passwords.Delete(rule);
        RuleNotice = $"Forgot the password for “{rule.FolderName}”.";
        RefreshRules();
    }

    [RelayCommand]
    private void ForgetAll()
    {
        Passwords.DeleteAll();
        RuleNotice = "All saved passwords forgotten.";
        RefreshRules();
    }

    [RelayCommand]
    private void DismissCandidate(FolderCandidate candidate)
    {
        Passwords.ForgetCandidate(candidate);
        RefreshRules();
    }

    public void RefreshRules()
    {
        Rules.Clear();
        foreach (var rule in Passwords.Rules)
            Rules.Add(rule);
        Candidates.Clear();
        foreach (var candidate in Passwords.Candidates)
            Candidates.Add(candidate);
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(HasCandidates));
        OnPropertyChanged(nameof(MatchingExample));
    }

    /// <summary>
    /// Adds a rule for a folder. A second rule for a folder name that already exists
    /// elsewhere is questioned here, at creation time, naming where the existing one came
    /// from — that is the only moment the user can answer whether it is the same source.
    /// </summary>
    public DirectoryPasswordRule? ConflictFor(string folderName, string path) =>
        Passwords.ConflictingRule(folderName, path);

    public SaveRuleResult AddRule(string folderName, string path, string password)
    {
        var result = Passwords.Save(folderName, path, password);
        RuleNotice = result switch
        {
            SaveRuleResult.Saved => $"Saved the password for “{folderName}”.",
            SaveRuleResult.Updated => $"Updated the password for “{folderName}”.",
            SaveRuleResult.SecretRejected => $"{Passwords.SecureStoreName} refused to store it.",
            SaveRuleResult.NoSecureStore => "No secure store is available on this system.",
            SaveRuleResult.StoreDisabled => "Switch on remembering passwords first.",
            SaveRuleResult.InvalidFolderName => "That folder is too broad to use as a rule.",
            _ => string.Empty,
        };
        RefreshRules();
        return result;
    }

    [RelayCommand]
    private Task RedetectQpdf() => RefreshQpdfAsync();

    private async Task RefreshQpdfAsync()
    {
        var (resolved, tooOld) = await _resolver.ResolveAsync(Settings.QpdfPath);
        IsQpdfUsable = resolved is not null;

        if (resolved is not null)
        {
            QpdfStatus = $"qpdf {resolved.VersionText}";
            QpdfPathText = resolved.ExecutablePath;
            QpdfOrigin = resolved.OriginDescription;
            return;
        }

        QpdfPathText = string.Empty;
        QpdfStatus = tooOld.Count > 0
            ? $"Found qpdf {tooOld[0].VersionText}, which is too old"
            : "No usable qpdf found";
        QpdfOrigin = tooOld.Count > 0
            ? $"Version {QpdfInstallation.MinimumVersion} or newer is required."
            : "Install qpdf, or choose its location below.";
    }

    partial void OnContextMenuRunsImmediatelyChanged(bool value)
    {
        Settings.ContextMenuBehaviour = value
            ? ContextMenuBehaviour.RunImmediately
            : ContextMenuBehaviour.PreloadAndWait;
        _store.Save(Settings);
    }

    partial void OnCheckForUpdatesChanged(bool value)
    {
        Settings.CheckForUpdates = value;
        _store.Save(Settings);
    }

    partial void OnPasswordStoreEnabledChanged(bool value)
    {
        Settings.PasswordStoreEnabled = value;
        Passwords.Enabled = value;
        _store.Save(Settings);
        // Disabling is not deletion: existing rules stay, unused, until the user says
        // otherwise.
        RuleNotice = value ? string.Empty : "Saved passwords are kept but will not be used.";
    }

    partial void OnSelectedSectionChanged(SettingsSection value) => RaiseSectionFlags();

    partial void OnDetailPageTitleChanged(string? value) => RaiseSectionFlags();

    private void RaiseSectionFlags()
    {
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(IsPasswords));
        OnPropertyChanged(nameof(IsQpdf));
        OnPropertyChanged(nameof(IsLicences));
        OnPropertyChanged(nameof(IsDetailOpen));
    }
}
