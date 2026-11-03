namespace PowerBILauncher
{
    public readonly record struct PbiExecutableFacts(
        bool Exists,
        string FileName,
        string? ProductName,
        string? FileDescription);

    public static class PbiExecutablePolicy
    {
        public const string RequiredFileName = "PBIDesktop.exe";
        public const string ProductMarker = "Power BI";

        public static bool IsPowerBiExecutable(PbiExecutableFacts facts)
        {
            if (!facts.Exists)
            {
                return false;
            }

            if (!string.Equals(facts.FileName, RequiredFileName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ContainsMarker(facts.ProductName) || ContainsMarker(facts.FileDescription);
        }

        private static bool ContainsMarker(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.Contains(ProductMarker, StringComparison.OrdinalIgnoreCase);
    }
}
