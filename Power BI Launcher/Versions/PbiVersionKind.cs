namespace PowerBILauncher
{
    public enum PbiVersionKind
    {
        Desktop,
        ReportServer
    }

    public static class PbiVersionKindPolicy
    {
        public static PbiVersionKind Infer(
            string path,
            string? productName,
            string? fileDescription,
            string? displayHint = null)
        {
            if (LooksLikeReportServer(path) ||
                LooksLikeReportServer(productName) ||
                LooksLikeReportServer(fileDescription) ||
                LooksLikeReportServer(displayHint))
            {
                return PbiVersionKind.ReportServer;
            }

            return PbiVersionKind.Desktop;
        }

        public static string ScanNameHead(PbiVersionKind kind) =>
            kind switch
            {
                PbiVersionKind.Desktop => "Desktop",
                PbiVersionKind.ReportServer => "Report Server",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };

        public static bool IsMicrosoftStore(string? path, bool isStorePackage = false)
        {
            if (isStorePackage)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            foreach (string segment in path.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (segment.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeReportServer(string? value) =>
            !string.IsNullOrEmpty(value) &&
            (value.Contains("Report Server", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("Desktop RS", StringComparison.OrdinalIgnoreCase));
    }
}
