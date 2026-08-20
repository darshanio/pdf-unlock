using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfUnlock.Services;

/// <summary>
/// Windows Credential Manager, via the credential API. Secrets are passed as a memory
/// blob rather than on a command line, so nothing leaks into the process arguments.
///
/// Not exercised on macOS. Treat as unverified until it has run on Windows.
/// </summary>
public sealed class WindowsCredentialSecretStore : ISecretStore
{
    private const string Prefix = "PDF Unlock:";
    private const int GenericCredential = 1;
    private const int PersistLocalMachine = 2;

    public bool IsAvailable => OperatingSystem.IsWindows();

    public string DisplayName => "Windows Credential Manager";

    public bool TrySet(string key, string secret)
    {
        if (!IsAvailable)
            return false;

        var blob = Encoding.Unicode.GetBytes(secret);
        var handle = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, handle, blob.Length);
            var credential = new Credential
            {
                Type = GenericCredential,
                TargetName = Prefix + key,
                CredentialBlob = handle,
                CredentialBlobSize = blob.Length,
                Persist = PersistLocalMachine,
                UserName = key,
            };
            return CredWrite(ref credential, 0);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            // Zero the copy before releasing it: the secret should not linger in a freed
            // block waiting to be reallocated to something else.
            for (var i = 0; i < blob.Length; i++)
                Marshal.WriteByte(handle, i, 0);
            Marshal.FreeHGlobal(handle);
            Array.Clear(blob);
        }
    }

    public bool TryGet(string key, out string secret)
    {
        secret = string.Empty;
        if (!IsAvailable)
            return false;

        var pointer = IntPtr.Zero;
        try
        {
            if (!CredRead(Prefix + key, GenericCredential, 0, out pointer))
                return false;

            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.CredentialBlobSize == 0)
                return false;

            var blob = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
            secret = Encoding.Unicode.GetString(blob);
            Array.Clear(blob);
            return secret.Length > 0;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (pointer != IntPtr.Zero)
                CredFree(pointer);
        }
    }

    public bool TryDelete(string key) =>
        IsAvailable && CredDelete(Prefix + key, GenericCredential, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);
}
