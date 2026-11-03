namespace PowerBILauncher;

public enum ProfileCredentialReadKind
{
    Found,
    Missing,
    Unreadable
}

public enum CredentialStoreErrorKind
{
    SaveFailed,
    RemoveFailed
}

public sealed class ProfileCredentialRead
{
    public ProfileCredentialReadKind Kind { get; init; }
    public string? Password { get; init; }
    public string? UserName { get; init; }

    public static ProfileCredentialRead Found(string password, string? userName = null) =>
        new() { Kind = ProfileCredentialReadKind.Found, Password = password, UserName = userName };

    public static ProfileCredentialRead Missing() =>
        new() { Kind = ProfileCredentialReadKind.Missing };

    public static ProfileCredentialRead Unreadable() =>
        new() { Kind = ProfileCredentialReadKind.Unreadable };
}

public interface IProfileCredentialStore
{
    ProfileCredentialRead TryRead(Guid profileId);
    bool TryWrite(Guid profileId, string userName, string password, out CredentialStoreErrorKind? errorKind, out string? osDetail);
    bool TryDelete(Guid profileId, out CredentialStoreErrorKind? errorKind, out string? osDetail);
}
