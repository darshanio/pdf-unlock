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

    public MainViewModel() : this(new SettingsStore()) { }

    public MainViewModel(SettingsStore store) : this(store, store.Load()) { }

    public MainViewModel(SettingsStore store, AppSettings settings)
    {
        SettingsStore = store;
        Settings = settings;
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
                PasswordSource.FolderRule => $"Using the saved password for folder “{SelectedJob.FolderName}”.",
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
            job.PropertyChanged += OnJobPropertyChanged;
            Jobs.Add(job);
        }

        SelectedJob ??= Jobs.FirstOrDefault();
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

                job.Apply(new DecryptOutcomeSnapshot(outcome.State, outcome.Reason, outcome.Message));
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
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
            RefreshCounts();
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
