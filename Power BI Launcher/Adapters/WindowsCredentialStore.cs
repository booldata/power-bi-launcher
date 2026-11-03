using System.Runtime.InteropServices;
using System.Text;

namespace PowerBILauncher;

/// <summary>
/// Windows Credential Manager adapter. Target is owned by Profile ID.
/// </summary>
public sealed class WindowsCredentialStore : IProfileCredentialStore
{
    public const string TargetPrefix = "booldata.PowerBILauncher/run-as/";

    public static string TargetName(Guid profileId) =>
        TargetPrefix + profileId.ToString("D");

    public ProfileCredentialRead TryRead(Guid profileId)
    {
        if (!CredRead(TargetName(profileId), CredType.Generic, 0, out nint credentialPtr))
        {
            return Marshal.GetLastWin32Error() is 1168 or 1312
                ? ProfileCredentialRead.Missing()
                : ProfileCredentialRead.Unreadable();
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
            if (credential.CredentialBlob == nint.Zero || credential.CredentialBlobSize == 0)
            {
                return ProfileCredentialRead.Found("", credential.UserName);
            }

            string password = Marshal.PtrToStringUni(
                credential.CredentialBlob,
                (int)(credential.CredentialBlobSize / 2)) ?? "";
            return ProfileCredentialRead.Found(password, credential.UserName);
        }
        catch
        {
            return ProfileCredentialRead.Unreadable();
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public bool TryWrite(Guid profileId, string userName, string password, out CredentialStoreErrorKind? errorKind, out string? osDetail)
    {
        byte[] blob = Encoding.Unicode.GetBytes(password ?? "");
        nint blobPtr = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobPtr, blob.Length);
            var credential = new NativeCredential
            {
                Type = CredType.Generic,
                TargetName = TargetName(profileId),
                UserName = userName,
                CredentialBlob = blobPtr,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CredPersist.LocalMachine,
                Comment = "Power BI Launcher Windows network identity"
            };

            if (CredWrite(ref credential, 0))
            {
                errorKind = null;
                osDetail = null;
                return true;
            }

            errorKind = CredentialStoreErrorKind.SaveFailed;
            osDetail = FormatWin32Error();
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public bool TryDelete(Guid profileId, out CredentialStoreErrorKind? errorKind, out string? osDetail)
    {
        if (CredDelete(TargetName(profileId), CredType.Generic, 0))
        {
            errorKind = null;
            osDetail = null;
            return true;
        }

        int code = Marshal.GetLastWin32Error();
        if (code is 1168 or 1312)
        {
            errorKind = null;
            osDetail = null;
            return true;
        }

        errorKind = CredentialStoreErrorKind.RemoveFailed;
        osDetail = FormatWin32Error(code);
        return false;
    }

    private static string FormatWin32Error(int? code = null) =>
        new System.ComponentModel.Win32Exception(code ?? Marshal.GetLastWin32Error()).Message;

    private const int CredTypeGeneric = 1;

    private enum CredType : uint
    {
        Generic = CredTypeGeneric
    }

    private enum CredPersist : uint
    {
        LocalMachine = 2
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public CredType Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public CredPersist Persist;
        public uint AttributeCount;
        public nint Attributes;
        public string TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, CredType type, int reservedFlag, out nint credentialPtr);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, CredType type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(nint buffer);
}
