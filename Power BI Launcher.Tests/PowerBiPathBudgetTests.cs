using System;
using System.IO;

namespace PowerBILauncher.Tests;

public class PowerBiPathBudgetTests
{
    [Theory]
    [InlineData(PbiVersionKind.Desktop, 113)]
    [InlineData(PbiVersionKind.ReportServer, 108)]
    public void MaxLocalAppDataLength_matches_measured_power_bi_limit(PbiVersionKind kind, int expected)
    {
        Assert.Equal(expected, PowerBiPathBudget.MaxLocalAppDataLength(kind));
    }

    [Fact]
    public void CheckLocalAppData_accepts_path_at_limit_and_rejects_one_over()
    {
        int max = PowerBiPathBudget.MaxLocalAppDataLength(PbiVersionKind.ReportServer);
        string atLimit = @"C:\" + new string('a', max - 3);
        string over = atLimit + "b";

        Assert.Null(PowerBiPathBudget.CheckLocalAppData(atLimit, PbiVersionKind.ReportServer));
        PathBudgetMiss? miss = PowerBiPathBudget.CheckLocalAppData(over, PbiVersionKind.ReportServer);
        Assert.NotNull(miss);
        Assert.Equal(PathBudgetScope.LocalAppData, miss.Scope);
        Assert.Equal(PbiVersionKind.ReportServer, miss.Kind);
        Assert.Equal(over.Length, miss.ActualLength);
        Assert.Equal(max, miss.MaxLength);
    }

    [Fact]
    public void CheckProfilesRoot_agrees_with_CheckLocalAppData_for_the_fixed_layout()
    {
        int maxRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string rootAtLimit = @"C:\" + new string('r', maxRoot - 3);
        string rootOver = rootAtLimit + "x";

        Assert.Null(PowerBiPathBudget.CheckProfilesRoot(rootAtLimit, PbiVersionKind.Desktop));
        Assert.Null(PowerBiPathBudget.CheckLocalAppData(
            ProfilePaths.ForProfile(rootAtLimit, Guid.NewGuid()).LocalAppData, PbiVersionKind.Desktop));

        Assert.NotNull(PowerBiPathBudget.CheckProfilesRoot(rootOver, PbiVersionKind.Desktop));
        Assert.NotNull(PowerBiPathBudget.CheckLocalAppData(
            ProfilePaths.ForProfile(rootOver, Guid.NewGuid()).LocalAppData, PbiVersionKind.Desktop));
    }

    [Fact]
    public void CheckProfilesRoot_ignores_trailing_separator()
    {
        int maxRoot = PowerBiPathBudget.MaxProfilesRootLength(PbiVersionKind.Desktop);
        string rootAtLimit = @"C:\" + new string('r', maxRoot - 3) + @"\";

        Assert.Null(PowerBiPathBudget.CheckProfilesRoot(rootAtLimit, PbiVersionKind.Desktop));
    }
}
