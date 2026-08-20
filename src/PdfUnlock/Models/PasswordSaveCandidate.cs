namespace PdfUnlock.Models;

/// <summary>
/// One row of the after-a-run prompt: a folder, the password proven to work for it, and
/// whether it can be saved. Failures appear too — the user asked to see which passwords
/// worked and which did not — but cannot be selected.
/// </summary>
public sealed class PasswordSaveCandidate(
    string folderName, string path, string password, bool canSave, string note, string outcome)
{
    public string FolderName { get; } = folderName;
    public string Path { get; } = path;
    public string Password { get; } = password;
    public bool CanSave { get; } = canSave;

    /// <summary>Why this row is what it is: "new", "replaces the saved password",
    /// "nothing worked".</summary>
    public string Note { get; } = note;

    public bool IsSelected { get; set; } = canSave;

    /// <summary>What actually happened for this folder, which is not the same question as
    /// whether there is anything to save: a folder can be fully working and still have
    /// nothing new to store.</summary>
    public string Outcome { get; } = outcome;
    public string Masked => CanSave ? new string('•', System.Math.Min(Password.Length, 12)) : "—";
}
