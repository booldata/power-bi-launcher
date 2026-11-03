namespace PowerBILauncher.Tests;

internal sealed class MemoryCredentialStore : IProfileCredentialStore
{
    public Dictionary<Guid, string> Passwords { get; } = new();
    public Dictionary<Guid, string> UserNames { get; } = new();
    public HashSet<Guid> Unreadable { get; } = new();
    public CredentialStoreErrorKind? WriteErrorKind { get; set; }
    public string? WriteOsDetail { get; set; }
    public CredentialStoreErrorKind? DeleteErrorKind { get; set; }
    public string? DeleteOsDetail { get; set; }

    /// <summary>Successful writes allowed before <see cref="WriteErrorKind"/> applies (null = fail every write).</summary>
    public int? FailWritesAfter { get; set; }

    /// <summary>Successful deletes allowed before <see cref="DeleteErrorKind"/> applies (null = fail every delete).</summary>
    public int? FailDeletesAfter { get; set; }

    public int WriteCount { get; private set; }
    public int DeleteCount { get; private set; }

    public ProfileCredentialRead TryRead(Guid profileId)
    {
        if (Unreadable.Contains(profileId))
        {
            return ProfileCredentialRead.Unreadable();
        }

        return Passwords.TryGetValue(profileId, out string? password)
            ? ProfileCredentialRead.Found(password, UserNames.GetValueOrDefault(profileId))
            : ProfileCredentialRead.Missing();
    }

    public bool TryWrite(Guid profileId, string userName, string password, out CredentialStoreErrorKind? errorKind, out string? osDetail)
    {
        if (WriteErrorKind is not null && (FailWritesAfter is null || WriteCount >= FailWritesAfter))
        {
            errorKind = WriteErrorKind;
            osDetail = WriteOsDetail;
            return false;
        }

        WriteCount++;
        Passwords[profileId] = password;
        UserNames[profileId] = userName;
        Unreadable.Remove(profileId);
        errorKind = null;
        osDetail = null;
        return true;
    }

    public bool TryDelete(Guid profileId, out CredentialStoreErrorKind? errorKind, out string? osDetail)
    {
        if (DeleteErrorKind is not null && (FailDeletesAfter is null || DeleteCount >= FailDeletesAfter))
        {
            errorKind = DeleteErrorKind;
            osDetail = DeleteOsDetail;
            return false;
        }

        DeleteCount++;
        Passwords.Remove(profileId);
        UserNames.Remove(profileId);
        Unreadable.Remove(profileId);
        errorKind = null;
        osDetail = null;
        return true;
    }
}
