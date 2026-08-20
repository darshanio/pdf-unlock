using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfUnlock.Models;
using PdfUnlock.Services;

namespace PdfUnlock.ViewModels;

/// <summary>
/// The one open batch: a list of jobs plus the default password shared across them.
/// See CONTEXT.md, "Batch".
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly QpdfResolver _resolver = new();
    private CancellationTokenSource? _runCancellation;

    public SettingsStore SettingsStore { get; }
    public AppSettings Settings { get; }
    public PasswordStore Passwords { get; }

    /// <summary>Passwords proven correct during the last run that are worth offering to
    /// save. Consumed by the view, which shows the prompt.</summary>
    public List<PasswordSaveCandidate> PendingSaves { get; } = [];

    public ObservableCollection<DecryptJob> Jobs { get; } = [];

    [ObservableProperty] private DecryptJob? _selectedJob;

    /// <summary>Applies to every job without an override. Editing it retroactively
    /// changes them all, which is what the user wants after a typo.</summary>
    [ObservableProperty] private string _defaultPassword = string.Empty;

    [ObservableProperty] private QpdfInstallation? _qpdf;
    [ObservableProperty] private string _qpdfStatus = "Looking for qpdf…";
    [ObservableProperty] private bool _isQpdfUsable;

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private double _progressValue;

    /// <summary>Raised when a run ends, so the view can offer to remember passwords.</summary>
    public event System.Action? RunFinished;

    public MainViewModel() : this(new SettingsStore()) { }

    public MainViewModel(SettingsStore store) : this(store, store.Load()) { }

    public MainViewModel(SettingsStore store, AppSettings settings)
        : this(store, settings, new PasswordStore(store.DataDirectory, SecretStoreFactory.ForThisPlatform())) { }

    public MainViewModel(SettingsStore store, AppSettings settings, PasswordStore passwords)
    {
        SettingsStore = store;
        Settings = settings;
        Passwords = passwords;
        Passwords.Enabled = settings.PasswordStoreEnabled;
        Jobs.CollectionChanged += OnJobsChanged;
        _ = ResolveQpdfAsync();
    }

    // Design-time constructor path: the previewer instantiates this type directly.
    public string Title => "PDF Unlock";

    public int RemainingCount => Jobs.Count(job => job.IsRemaining);
    public int CollisionCount => Jobs.Count(job => job.NeedsCollisionDecision);
    public bool HasJobs => Jobs.Count > 0;

    public string DecryptAllLabel => $"Decrypt all ({Jobs.Count})";
    public string DecryptRemainingLabel => $"Decrypt {RemainingCount} remaining";

    public string CollisionWarning => CollisionCount == 0
        ? string.Empty
        : $"{CollisionCount} file{(CollisionCount == 1 ? "" : "s")} already ha{(CollisionCount == 1 ? "s" : "ve")} a decrypted copy — choose what to do with each before running.";

    public bool CanRun => IsQpdfUsable && !IsRunning && HasJobs && CollisionCount == 0;

    public string SelectedPasswordSourceText
    {
        get
        {
            if (SelectedJob is null)
                return string.Empty;
            return SelectedJob.SourceOf(DefaultPassword) switch
            {
                PasswordSource.Override => "Using this file's own password.",
                PasswordSource.BatchDefault => "Using the password for all files.",
                PasswordSource.FolderRule => SelectedJob.RuleIsAmbiguous
                    ? $"Using a saved password for “{SelectedJob.RuleName}” — more than one rule matches that folder name."
                    : $"Using the saved password for folder “{SelectedJob.RuleName}”.",
                _ => "No password — fine for a PDF that opens without one.",
            };
        }
    }

    private async Task ResolveQpdfAsync()
    {
        var (resolved, tooOld) = await _resolver.ResolveAsync(Settings.QpdfPath);
        Qpdf = resolved;
        IsQpdfUsable = resolved is not null;

        QpdfStatus = resolved is not null
            ? $"qpdf {resolved.VersionText} — {resolved.OriginDescription}"
            : tooOld.Count > 0
                ? $"Found qpdf {tooOld[0].VersionText}, but version {QpdfInstallation.MinimumVersion} or newer is required."
                : "qpdf was not found on this machine.";

        OnPropertyChanged(nameof(CanRun));
    }

    [RelayCommand]
    private void AddFiles(IEnumerable<string>? paths)
    {
        if (paths is null)
            return;

        // Files added later append to the existing batch rather than starting a new one.
        var existing = Jobs.Select(job => job.InputPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(path => !existing.Contains(path)))
        {
            var job = new DecryptJob(path);
            job.CheckForCollision();
            ApplyRule(job);
            Passwords.NoteFolder(job.FolderName, job.DirectoryPath);
            job.PropertyChanged += OnJobPropertyChanged;
            Jobs.Add(job);
        }

        SelectedJob ??= Jobs.FirstOrDefault();
    }

    /// <summary>Attaches the password a folder rule supplies, along with the rule's name
    /// so the user can see where it came from.</summary>
    private void ApplyRule(DecryptJob job)
    {
        var rule = Passwords.Match(job.FolderName);
        if (rule is null || !Passwords.TryGetPassword(rule, out var password))
        {
            job.RulePassword = null;
            job.RuleName = null;
            job.RuleIsAmbiguous = false;
            return;
        }

        job.RulePassword = password;
        job.RuleName = rule.FolderName;
        job.RuleIsAmbiguous = Passwords.IsAmbiguous(job.FolderName);
    }

    /// <summary>Re-reads rules for every job. Called after the settings window closes,
    /// where rules may have been added, changed or deleted.</summary>
    public void ReapplyRules()
    {
        Passwords.Enabled = Settings.PasswordStoreEnabled;
        foreach (var job in Jobs)
            ApplyRule(job);
        OnPropertyChanged(nameof(SelectedPasswordSourceText));
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedJob is null)
            return;
        SelectedJob.PropertyChanged -= OnJobPropertyChanged;
        Jobs.Remove(SelectedJob);
        SelectedJob = Jobs.FirstOrDefault();
    }

    [RelayCommand]
    private void Clear()
    {
        foreach (var job in Jobs)
            job.PropertyChanged -= OnJobPropertyChanged;
        Jobs.Clear();
        SelectedJob = null;
    }

    [RelayCommand]
    private void ResolveCollision(string choice)
    {
        if (SelectedJob is null)
            return;
        SelectedJob.CollisionChoice = Enum.Parse<CollisionChoice>(choice);
    }

    [RelayCommand]
    private Task DecryptAll() => RunAsync(Jobs.ToList());

    [RelayCommand]
    private Task DecryptRemaining() => RunAsync(Jobs.Where(job => job.IsRemaining).ToList());

    [RelayCommand]
    private void Cancel() => _runCancellation?.Cancel();

    /// <summary>Called when the settings window closes: the chosen qpdf path may have
    /// changed, and resolution happens once per launch otherwise.</summary>
    [RelayCommand]
    private Task RedetectQpdf() => ResolveQpdfAsync();

    private async Task RunAsync(List<DecryptJob> jobs)
    {
        if (Qpdf is null || IsRunning)
            return;

        jobs = jobs.Where(job => job.CollisionChoice != CollisionChoice.Skip).ToList();
        if (jobs.Count == 0)
            return;

        var decryptor = new QpdfDecryptor(Qpdf);
        _runCancellation = new CancellationTokenSource();
        IsRunning = true;
        ProgressValue = 0;

        try
        {
            for (var index = 0; index < jobs.Count; index++)
            {
                var job = jobs[index];
                ProgressText = $"{index + 1} of {jobs.Count} — {job.FileName}";
                job.Reset();
                job.State = JobState.Running;

                var outcome = await decryptor.DecryptAsync(
                    job.InputPath,
                    job.OutputPath,
                    job.EffectivePassword(DefaultPassword),
                    overwriteExisting: job.CollisionChoice == CollisionChoice.Overwrite,
                    _runCancellation.Token);

                job.Apply(new DecryptOutcomeSnapshot(outcome.State, outcome.Reason, outcome.Message, outcome.PasswordProven));
                job.CheckForCollision();
                ProgressValue = (index + 1) * 100.0 / jobs.Count;

                if (outcome.Reason == FailureReason.QpdfMissing)
                {
                    // Abandonment: the remaining jobs would fail identically and bury the cause.
                    ProgressText = "qpdf is no longer available — stopped.";
                    IsQpdfUsable = false;
                    QpdfStatus = outcome.Message;
                    foreach (var abandoned in jobs.Skip(index + 1))
                        abandoned.Reset();
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            ProgressText = "Cancelled.";
            foreach (var job in jobs.Where(job => job.State == JobState.Running))
                job.Reset();
        }
        finally
        {
            CollectPendingSaves(jobs);
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
            RefreshCounts();
            // The view owns windows; the view model only says that a run finished.
            RunFinished?.Invoke();
        }
    }

    /// <summary>
    /// Gathers the passwords worth offering to save: only those that actually worked, and
    /// only where they differ from what is already stored. A password that just failed is
    /// never offered — storing a known-bad secret is worse than storing nothing.
    /// </summary>
    private void CollectPendingSaves(List<DecryptJob> jobs)
    {
        PendingSaves.Clear();
        if (!Passwords.Enabled || !Passwords.HasSecureStore)
            return;

        foreach (var group in jobs.GroupBy(job => job.FolderName, StringComparer.OrdinalIgnoreCase))
        {
            var path = group.First().DirectoryPath;
            var failed = group.Count(job =>
                job.State == JobState.Failed && job.Reason == FailureReason.WrongPassword);

            // Only a job that actually needed a password proves one. A permissions-only
            // PDF decrypts whatever it is given, so its success is not evidence.
            var proven = group
                .Where(job => job.State == JobState.Decrypted && job.PasswordProven)
                .Select(job => job.EffectivePassword(DefaultPassword))
                .FirstOrDefault(password => !string.IsNullOrEmpty(password));

            if (string.IsNullOrEmpty(proven))
            {
                // The user asked to see which passwords worked and which did not, so a
                // folder where nothing worked still gets a row — just not a savable one.
                if (failed > 0)
                    PendingSaves.Add(new PasswordSaveCandidate(
                        group.Key, path, string.Empty, false,
                        failed == 1 ? "1 file rejected the password" : $"{failed} files rejected the password",
                        "failed"));
                continue;
            }

            var existing = Passwords.Match(group.Key);
            var alreadyStored = existing is not null
                                && Passwords.TryGetPassword(existing, out var stored)
                                && stored == proven;

            if (alreadyStored)
            {
                // Nothing to save, but a mixed result is still worth showing.
                if (failed > 0)
                    PendingSaves.Add(new PasswordSaveCandidate(
                        group.Key, path, string.Empty, false,
                        $"saved password already correct, but {failed} file(s) rejected it",
                        "mixed"));
                continue;
            }

            var note = existing is null ? "new" : "replaces the saved password";
            if (failed > 0)
                note += $" — {failed} file(s) still rejected it";
            PendingSaves.Add(new PasswordSaveCandidate(
                group.Key, path, proven, true, note, failed > 0 ? "mixed" : "worked"));
        }
    }

    private void OnJobsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshCounts();

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DecryptJob.State) or nameof(DecryptJob.OutputExists) or nameof(DecryptJob.CollisionChoice))
            RefreshCounts();
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(CollisionCount));
        OnPropertyChanged(nameof(HasJobs));
        OnPropertyChanged(nameof(DecryptAllLabel));
        OnPropertyChanged(nameof(DecryptRemainingLabel));
        OnPropertyChanged(nameof(CollisionWarning));
        OnPropertyChanged(nameof(CanRun));
    }

    partial void OnDefaultPasswordChanged(string value)
    {
        OnPropertyChanged(nameof(SelectedPasswordSourceText));
    }

    partial void OnSelectedJobChanged(DecryptJob? value)
    {
        OnPropertyChanged(nameof(SelectedPasswordSourceText));
    }

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanRun));

    partial void OnIsQpdfUsableChanged(bool value) => OnPropertyChanged(nameof(CanRun));
}
