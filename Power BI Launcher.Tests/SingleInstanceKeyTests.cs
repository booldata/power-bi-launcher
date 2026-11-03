namespace PowerBILauncher.Tests;

public class SingleInstanceKeyTests
{
    [Fact]
    public void Same_folder_with_different_case_or_trailing_separator_shares_one_key()
    {
        string key = SingleInstanceKey.ForBaseDirectory(@"C:\Tools\Power BI Launcher\");

        Assert.Equal(key, SingleInstanceKey.ForBaseDirectory(@"c:\tools\power bi launcher"));
        Assert.Equal(key, SingleInstanceKey.ForBaseDirectory(@"C:\Tools\Power BI Launcher"));
    }

    [Fact]
    public void Separate_copies_get_separate_keys()
    {
        Assert.NotEqual(
            SingleInstanceKey.ForBaseDirectory(@"C:\Tools\Power BI Launcher"),
            SingleInstanceKey.ForBaseDirectory(@"D:\Portable\Power BI Launcher"));
    }

    [Fact]
    public void Key_is_short_and_namespaced()
    {
        string key = SingleInstanceKey.ForBaseDirectory(@"C:\Tools\Power BI Launcher");

        Assert.StartsWith("booldata.PowerBILauncher.", key, StringComparison.Ordinal);
        Assert.Equal("booldata.PowerBILauncher.".Length + 16, key.Length);
    }
}
