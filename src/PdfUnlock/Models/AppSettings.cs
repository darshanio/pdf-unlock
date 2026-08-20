namespace PdfUnlock.Models;

/// <summary>
/// What happens when the operating system's context menu hands us files.
/// See CONTEXT.md, "Shell Invocation".
/// </summary>
public enum ContextMenuBehaviour
{
    /// <summary>Open the window with the files loaded and let the user drive. The default:
    /// a tool that writes files the instant you right-click surprises people once and
    /// annoys them forever.</summary>
    PreloadAndWait,

    /// <summary>Start decrypting immediately, surfacing the window only when something
    /// needs a decision. Opt-in.</summary>
    RunImmediately,
}

/// <summary>
/// Everything the app remembers between launches. Secrets are deliberately absent:
/// passwords belong in the operating system's secret store, never in this file.
/// </summary>
public sealed class AppSettings
{
    public ContextMenuBehaviour ContextMenuBehaviour { get; set; } = ContextMenuBehaviour.PreloadAndWait;

    /// <summary>An explicit qpdf location, which outranks every other candidate.
    /// Null means "let resolution decide".</summary>
    public string? QpdfPath { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>Off by default: the app accumulates nothing about the user's folders
    /// until they ask it to.</summary>
    public bool PasswordStoreEnabled { get; set; }

    public double WindowWidth { get; set; } = 900;
    public double WindowHeight { get; set; } = 600;
}
