namespace PowerBILauncher.Tests;

internal sealed class StubPbiDiscovery : IPbiDiscovery
{
    public List<PbiDiscoveryCandidate> Candidates { get; } = new();

    public IReadOnlyList<PbiDiscoveryCandidate> FindCandidates() => Candidates;
}
