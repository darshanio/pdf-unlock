using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PdfUnlock.Models;

namespace PdfUnlock.Services;

public sealed record DecryptOutcome(JobState State, FailureReason Reason, string Message);

/// <summary>
/// Drives qpdf for a single job. Exit codes below were confirmed against qpdf 12:
/// --requires-password returns 0 when a password is needed, 3 when the file is encrypted
/// but opens freely (permissions-only), and 2 when it is not encrypted at all.
/// </summary>
public sealed class QpdfDecryptor(QpdfInstallation installation)
{
    public async Task<DecryptOutcome> DecryptAsync(
        string inputPath,
        string outputPath,
        string? password,
        bool overwriteExisting,
        CancellationToken cancellationToken)
    {
        if (File.Exists(outputPath) && !overwriteExisting)
            return new DecryptOutcome(JobState.Failed, FailureReason.Collision,
                $"{Path.GetFileName(outputPath)} already exists.");

        try
        {
            var probe = await ProcessRunner.RunAsync(
                installation.ExecutablePath, ["--requires-password", inputPath], null, cancellationToken);

            switch (probe.ExitCode)
            {
                case 2:
                    return new DecryptOutcome(JobState.Failed, FailureReason.NotEncrypted,
                        "This PDF is not encrypted, so there is nothing to remove.");
                case 3:
                    // Permissions-only: the empty user password opens it, so whatever the user
                    // typed is irrelevant and must not be sent — a wrong one would be rejected.
                    password = null;
                    break;
                case 0:
                    break;
                default:
                    return new DecryptOutcome(JobState.Failed, FailureReason.IoError, Describe(probe));
            }

            string[] arguments = string.IsNullOrEmpty(password)
                ? ["--decrypt", inputPath, outputPath]
                : ["--decrypt", "--password-file=-", inputPath, outputPath];

            var result = await ProcessRunner.RunAsync(
                installation.ExecutablePath, arguments, password, cancellationToken);

            // qpdf uses 3 for "succeeded with warnings", and still writes valid output.
            if (result.ExitCode is 0 or 3)
                return new DecryptOutcome(JobState.Decrypted, FailureReason.None,
                    result.ExitCode == 3 ? "Decrypted, with warnings from qpdf." : "Decrypted.");

            if (result.AllOutput.Contains("invalid password", StringComparison.OrdinalIgnoreCase))
                return new DecryptOutcome(JobState.Failed, FailureReason.WrongPassword,
                    "That password was rejected.");

            DeletePartialOutput(outputPath);
            return new DecryptOutcome(JobState.Failed, FailureReason.IoError, Describe(result));
        }
        catch (OperationCanceledException)
        {
            // qpdf writes its output incrementally, so a killed run leaves a half-written PDF
            // that would otherwise poison the next run's collision check.
            DeletePartialOutput(outputPath);
            throw;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // The binary resolved at launch has since moved or become unrunnable. This
            // abandons the rest of the batch rather than repeating itself per job.
            return new DecryptOutcome(JobState.Failed, FailureReason.QpdfMissing,
                $"qpdf could not be started from {installation.ExecutablePath}.");
        }
        catch (IOException exception)
        {
            return new DecryptOutcome(JobState.Failed, FailureReason.IoError, exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return new DecryptOutcome(JobState.Failed, FailureReason.IoError, exception.Message);
        }
    }

    private static string Describe(ProcessResult result)
    {
        var text = result.AllOutput;
        return string.IsNullOrWhiteSpace(text) ? $"qpdf exited with code {result.ExitCode}." : text;
    }

    private static void DeletePartialOutput(string outputPath)
    {
        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch
        {
            // Nothing useful to do; the collision check will surface it next run.
        }
    }
}
