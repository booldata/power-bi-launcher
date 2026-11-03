namespace PowerBILauncher.Tests;

internal sealed class StubExecutableInspector : IPbiExecutableInspector
{
    private readonly Dictionary<string, PbiExecutableFacts> _facts = new(StringComparer.OrdinalIgnoreCase);

    public PbiExecutableFacts Default { get; init; } =
        new(Exists: false, FileName: "", ProductName: null, FileDescription: null);

    public void Pass(string path) =>
        _facts[path] = new PbiExecutableFacts(true, "PBIDesktop.exe", "Power BI Desktop", "Power BI Desktop");

    public void PassReportServer(string path) =>
        _facts[path] = new PbiExecutableFacts(true, "PBIDesktop.exe", "Power BI Report Server", "Power BI Report Server");

    public void Missing(string path) =>
        _facts[path] = new PbiExecutableFacts(false, Path.GetFileName(path), null, null);

    public void NotPowerBi(string path, string fileName = "PBIDesktop.exe") =>
        _facts[path] = new PbiExecutableFacts(true, fileName, "Widget", "Widget");

    public PbiExecutableFacts Inspect(string path) =>
        _facts.TryGetValue(path, out PbiExecutableFacts facts) ? facts : Default;
}
