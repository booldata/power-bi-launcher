namespace PowerBILauncher.Tests;

public class WindowsCredentialStoreTests
{
    /// <summary>Reserved GUID used only by this adapter test. Never a real Profile id.</summary>
    private static readonly Guid TestProfileId = Guid.Parse("a11ce000-0000-4000-8000-00000000c0de");

    [Fact]
    [Trait("Category", "Integration")]
    public void Writes_reads_and_deletes_a_reserved_test_entry()
    {
        var store = new WindowsCredentialStore();
        Guid id = TestProfileId;
        try
        {
            Assert.True(store.TryWrite(id, @"TEST\adapter", "adapter-secret", out _, out string? writeError), writeError);
            ProfileCredentialRead read = store.TryRead(id);
            Assert.Equal(ProfileCredentialReadKind.Found, read.Kind);
            Assert.Equal("adapter-secret", read.Password);
            Assert.Equal(@"TEST\adapter", read.UserName);
            Assert.True(store.TryDelete(id, out _, out string? deleteError), deleteError);
            Assert.Equal(ProfileCredentialReadKind.Missing, store.TryRead(id).Kind);
        }
        finally
        {
            store.TryDelete(id, out _, out _);
        }
    }
}
