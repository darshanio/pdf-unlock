using System;
using System.Diagnostics;

namespace PdfUnlock.Services;

/// <summary>
/// The macOS login keychain, driven through the `security` tool.
///
/// The password is written over standard input rather than as an argument, for the same
/// reason qpdf is fed that way: argv is readable by any other process. `security -w`
/// with no value prompts twice, so the secret is written twice — verified against the
/// real tool, not assumed.
/// </summary>
public sealed class MacKeychainSecretStore : ISecretStore
{
    private const string Service = "PDF Unlock";

    public bool IsAvailable => OperatingSystem.IsMacOS();

    public string DisplayName => "your macOS keychain";

    public bool TrySet(string key, string secret)
    {
        // -U updates in place when the item already exists, rather than failing.
        var result = Run(["add-generic-password", "-a", key, "-s", Service, "-U", "-w"],
                         stdin: secret + "\n" + secret + "\n");
        return result.ExitCode == 0 && TryGet(key, out var stored) && stored == secret;
    }

    public bool TryGet(string key, out string secret)
    {
        var result = Run(["find-generic-password", "-a", key, "-s", Service, "-w"]);
        secret = result.ExitCode == 0 ? result.Output.TrimEnd('\n', '\r') : string.Empty;
        return result.ExitCode == 0 && secret.Length > 0;
    }

    public bool TryDelete(string key) =>
        Run(["delete-generic-password", "-a", key, "-s", Service]).ExitCode == 0;

    private static (int ExitCode, string Output) Run(string[] arguments, string? stdin = null)
    {
        try
        {
            var info = new ProcessStartInfo("/usr/bin/security")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument);

            using var process = Process.Start(info);
            if (process is null)
                return (-1, string.Empty);

            if (stdin is not null)
            {
                process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            }

            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (Exception)
        {
            return (-1, string.Empty);
        }
    }
}
