using System.Collections.Generic;
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

    public SettingsViewModel(SettingsStore store, AppSettings settings, bool isBatchRunning)
    {
        _store = store;
        Settings = settings;
        IsBatchRunning = isBatchRunning;
        _contextMenuRunsImmediately = settings.ContextMenuBehaviour == ContextMenuBehaviour.RunImmediately;
        _checkForUpdates = settings.CheckForUpdates;
        _passwordStoreEnabled = settings.PasswordStoreEnabled;
        _ = RefreshQpdfAsync();
    }

    public AppSettings Settings { get; }

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
        _store.Save(Settings);
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
