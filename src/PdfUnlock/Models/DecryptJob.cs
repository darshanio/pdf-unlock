using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PdfUnlock.Models;

/// <summary>How the user chose to resolve a collision on this job's output path.</summary>
public enum CollisionChoice
{
    Undecided,
    Overwrite,
    Skip,
}

/// <summary>
/// One input PDF, the password to use for it, and its outcome. See CONTEXT.md, "Job".
/// </summary>
public sealed partial class DecryptJob : ObservableObject
{
    public DecryptJob(string inputPath)
    {
        InputPath = inputPath;
        FileName = Path.GetFileName(inputPath);
        var directory = Path.GetDirectoryName(inputPath) ?? string.Empty;
        DirectoryPath = directory;
        // The *name* of the containing folder, not its path: folder rules key on the name so
        // they survive the enclosing year folder changing. See CONTEXT.md.
        FolderName = new DirectoryInfo(directory).Name;
        OutputPath = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(inputPath) + "_decrypted" + Path.GetExtension(inputPath));
    }

    public string InputPath { get; }
    public string FileName { get; }
    public string DirectoryPath { get; }
    public string FolderName { get; }
    public string OutputPath { get; }
    public string OutputFileName => Path.GetFileName(OutputPath);

    [ObservableProperty] private JobState _state = JobState.Pending;
    [ObservableProperty] private FailureReason _reason = FailureReason.None;
    [ObservableProperty] private string _message = string.Empty;

    /// <summary>Set only when this file's password differs from the batch default.</summary>
    [ObservableProperty] private string? _passwordOverride;

    /// <summary>Password supplied by a matching folder rule, if any. Beaten by an
    /// override, beats the batch default.</summary>
    [ObservableProperty] private string? _rulePassword;

    /// <summary>The folder name of the rule that supplied <see cref="RulePassword"/>,
    /// shown so the user can see *why* a password appeared.</summary>
    [ObservableProperty] private string? _ruleName;

    /// <summary>True when more than one rule matched this folder name. Surfaced rather
    /// than resolved silently.</summary>
    [ObservableProperty] private bool _ruleIsAmbiguous;

    /// <summary>Set when this job's success actually demonstrated the password was
    /// correct. False for a permissions-only PDF, which opens regardless.</summary>
    [ObservableProperty] private bool _passwordProven;

    [ObservableProperty] private bool _outputExists;
    [ObservableProperty] private CollisionChoice _collisionChoice = CollisionChoice.Undecided;

    /// <summary>
    /// A collision the user has not yet ruled on. Such a job does not run. A job that has
    /// already succeeded is excluded: the output it collides with is the one it just wrote,
    /// which is not a decision the user owes anybody.
    /// </summary>
    public bool NeedsCollisionDecision =>
        OutputExists && CollisionChoice == CollisionChoice.Undecided && State != JobState.Decrypted;

    public bool IsRemaining => State != JobState.Decrypted;

    // Precedence: an override is the user speaking about this exact file, so it wins; a
    // rule is knowledge about this file's location, so it beats the batch-wide default.
    public string EffectivePassword(string batchDefault) =>
        !string.IsNullOrEmpty(PasswordOverride) ? PasswordOverride
        : !string.IsNullOrEmpty(RulePassword) ? RulePassword
        : batchDefault;

    public PasswordSource SourceOf(string batchDefault) =>
        !string.IsNullOrEmpty(PasswordOverride) ? PasswordSource.Override
        : !string.IsNullOrEmpty(RulePassword) ? PasswordSource.FolderRule
        : !string.IsNullOrEmpty(batchDefault) ? PasswordSource.BatchDefault
        : PasswordSource.None;

    public void CheckForCollision() => OutputExists = File.Exists(OutputPath);

    public void Reset()
    {
        State = JobState.Pending;
        Reason = FailureReason.None;
        Message = string.Empty;
        PasswordProven = false;
    }

    public void Apply(DecryptOutcomeSnapshot outcome)
    {
        State = outcome.State;
        Reason = outcome.Reason;
        Message = outcome.Message;
        PasswordProven = outcome.PasswordProven;
    }

    // Status glyph and colour are derived here rather than in XAML so that the meaning of a
    // state lives with the state. NotEncrypted reads as informational, not as an error.
    public string StatusGlyph => State switch
    {
        JobState.Decrypted => "✓",
        JobState.Running => "…",
        JobState.Failed when Reason == FailureReason.NotEncrypted => "–",
        JobState.Failed => "✗",
        _ when NeedsCollisionDecision => "!",
        _ => "•",
    };

    // Mid-tone hues, chosen to stay legible against both a light and a dark background:
    // the status colour cannot depend on a theme the user picks.
    public string StatusBrush => State switch
    {
        JobState.Decrypted => "#3FA45B",
        JobState.Running => "#4A9EDA",
        JobState.Failed when Reason == FailureReason.NotEncrypted => "#9A9A9A",
        JobState.Failed => "#E5534B",
        _ when NeedsCollisionDecision => "#E08C2C",
        _ => "#9A9A9A",
    };

    public string StateText => State switch
    {
        JobState.Pending when NeedsCollisionDecision => "Needs a decision",
        JobState.Pending => "Pending",
        JobState.Running => "Working…",
        JobState.Decrypted => "Decrypted",
        JobState.Failed => Reason switch
        {
            FailureReason.WrongPassword => "Wrong password",
            FailureReason.NotEncrypted => "Not encrypted",
            FailureReason.Collision => "Output exists",
            FailureReason.QpdfMissing => "qpdf unavailable",
            _ => "Failed",
        },
        _ => string.Empty,
    };

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(State) or nameof(Reason) or nameof(OutputExists) or nameof(CollisionChoice))
        {
            OnPropertyChanged(nameof(StatusGlyph));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(NeedsCollisionDecision));
            OnPropertyChanged(nameof(IsRemaining));
        }
    }
}

/// <summary>Decoupling record so the model does not depend on the service layer.</summary>
public sealed record DecryptOutcomeSnapshot(
    JobState State, FailureReason Reason, string Message, bool PasswordProven = false);
