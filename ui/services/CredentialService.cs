using System.Runtime.InteropServices;
using System.Text;

namespace LimelightX.UI.Services;

/// <summary>
/// Concrete ICredentialService via raw P/Invoke against advapi32.dll - no
/// NuGet credential-manager wrapper is on CLAUDE.md §3.5's approved list, and
/// this needs no new package reference either way. Single shared credential
/// name (not per-EnvironmentProfile, per ui-viewmodels.md §3.3).
/// </summary>
public sealed class CredentialService(string? credentialNameOverride = null) : ICredentialService
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    private readonly string _credentialName = credentialNameOverride ?? "LimelightX/AnthropicApiKey";

    public string? ReadApiKey()
    {
        if (!CredRead(_credentialName, CredTypeGeneric, 0, out var credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public void WriteApiKey(string apiKey)
    {
        var bytes = Encoding.Unicode.GetBytes(apiKey);
        var blobPtr = Marshal.AllocHGlobal(bytes.Length == 0 ? 1 : bytes.Length);
        try
        {
            if (bytes.Length > 0)
            {
                Marshal.Copy(bytes, 0, blobPtr, bytes.Length);
            }

            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = _credentialName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = "LimelightX",
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"Failed to write credential to Windows Credential Manager (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public void DeleteApiKey()
    {
        // Not throwing on failure (e.g. ERROR_NOT_FOUND if it was never set) -
        // deleting an already-absent credential is a no-op from the caller's perspective.
        CredDelete(_credentialName, CredTypeGeneric, 0);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree(IntPtr credentialPtr);
}
