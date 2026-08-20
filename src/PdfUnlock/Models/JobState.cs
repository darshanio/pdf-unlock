namespace PdfUnlock.Models;

public enum JobState
{
    Pending,
    Running,
    Decrypted,
    Failed,
}

/// <summary>Why a <see cref="JobState.Failed"/> job failed. See CONTEXT.md, "Job".</summary>
public enum FailureReason
{
    None,
    WrongPassword,
    NotEncrypted,
    Collision,
    QpdfMissing,
    IoError,
}
