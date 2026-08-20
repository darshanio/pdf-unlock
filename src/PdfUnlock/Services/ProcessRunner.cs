using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PdfUnlock.Services;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public string AllOutput => (StandardOutput + "\n" + StandardError).Trim();
}

public static class ProcessRunner
{
    /// <summary>
    /// Runs a process, optionally writing <paramref name="stdinText"/> to its standard input.
    /// Passwords travel this way rather than as arguments: argv is readable by any other
    /// process on both macOS and Windows.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string executable,
        string[] arguments,
        string? stdinText = null,
        CancellationToken cancellationToken = default)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdinText is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"Could not start {executable}.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        if (stdinText is not null)
        {
            await process.StandardInput.WriteLineAsync(stdinText);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancel kills the in-flight qpdf; its partial output is deleted by the caller,
            // which owns the knowledge of where that output was going.
            TryKill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already gone; nothing to clean up.
        }
    }
}
