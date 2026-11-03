using System.Reflection;
using System.Reflection.Emit;

namespace PowerBILauncher.Tests;

public class LauncherBuildInfoTests
{
    [Fact]
    public void TryGetBuildDate_parses_yyyy_MM_dd_metadata()
    {
        Assembly assembly = CreateAssemblyWithMetadata("BuildDate", "2026-03-14");

        DateTimeOffset? date = LauncherBuildInfo.TryGetBuildDate(assembly);

        Assert.Equal(new DateTimeOffset(2026, 3, 14, 0, 0, 0, TimeSpan.Zero), date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("2026/03/14")]
    [InlineData("14-03-2026")]
    [InlineData("2026-3-14")]
    [InlineData("20260314")]
    public void TryGetBuildDate_returns_null_when_metadata_is_missing_or_invalid(string? value)
    {
        Assembly assembly = value is null
            ? CreateAssembly()
            : CreateAssemblyWithMetadata("BuildDate", value);

        Assert.Null(LauncherBuildInfo.TryGetBuildDate(assembly));
    }

    [Fact]
    public void TryGetBuildDate_reads_compile_day_from_the_launcher_assembly()
    {
        DateTimeOffset? date = LauncherBuildInfo.TryGetBuildDate(typeof(LauncherBuildInfo).Assembly);

        Assert.NotNull(date);
        Assert.Equal(TimeSpan.Zero, date.Value.Offset);
        Assert.Equal(TimeSpan.Zero, date.Value.TimeOfDay);
    }

    [Fact]
    public void TryGetBuildDate_does_not_read_informational_version()
    {
        Assembly assembly = CreateAssemblyWithInformationalVersion("1.2.3+2026-03-14");

        Assert.Null(LauncherBuildInfo.TryGetBuildDate(assembly));
    }

    private static Assembly CreateAssemblyWithMetadata(string key, string value)
    {
        AssemblyBuilder builder = CreateAssembly();
        ConstructorInfo ctor = typeof(AssemblyMetadataAttribute).GetConstructor(
            [typeof(string), typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [key, value]));
        return builder;
    }

    private static Assembly CreateAssemblyWithInformationalVersion(string informationalVersion)
    {
        AssemblyBuilder builder = CreateAssembly();
        ConstructorInfo ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor(
            [typeof(string)])!;
        builder.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informationalVersion]));
        return builder;
    }

    private static AssemblyBuilder CreateAssembly()
    {
        var name = new AssemblyName("BuildDateFixture_" + Guid.NewGuid().ToString("N"));
        AssemblyBuilder builder = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
        builder.DefineDynamicModule("Main");
        return builder;
    }
}
