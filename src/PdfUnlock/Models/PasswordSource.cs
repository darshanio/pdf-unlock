namespace PdfUnlock.Models;

/// <summary>
/// Where a job's effective password came from. Precedence, highest first:
/// Override, then FolderRule, then BatchDefault. See CONTEXT.md.
/// </summary>
public enum PasswordSource
{
    /// <summary>No password at all — correct for a permissions-only PDF.</summary>
    None,
    BatchDefault,
    FolderRule,
    Override,
}
