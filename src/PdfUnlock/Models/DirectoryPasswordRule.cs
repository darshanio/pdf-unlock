using System;

namespace PdfUnlock.Models;

/// <summary>
/// A folder name paired with the password its PDFs use. See CONTEXT.md,
/// "Directory Password Rule". Matching is on <see cref="FolderName"/> alone, so a rule
/// for "a bank" covers that folder wherever it sits and whatever year folder encloses it.
///
/// This type holds no password. The secret lives in the platform secret store under
/// <see cref="Id"/>; that separation is the point — a stolen copy of the metadata file
/// reveals folder names and nothing else.
/// </summary>
public sealed class DirectoryPasswordRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    /// <summary>The name of the containing folder, matched case-insensitively.</summary>
    public string FolderName { get; set; } = string.Empty;

    /// <summary>The full path this rule was created from. Not used for matching: kept so
    /// that a second rule for the same folder name can be questioned at creation time,
    /// naming where the existing one came from.</summary>
    public string OriginPath { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedUtc { get; set; }

    public bool Matches(string folderName) =>
        !string.IsNullOrWhiteSpace(FolderName)
        && string.Equals(FolderName, folderName, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A folder seen in a batch that has no rule yet, offered as a suggestion.</summary>
public sealed class FolderCandidate
{
    public string FolderName { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}
