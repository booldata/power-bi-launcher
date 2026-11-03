namespace PowerBILauncher
{
    public sealed class PbiVersion
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DisplayName { get; set; } = "";
        public string Path { get; set; } = "";
        public PbiVersionKind Kind { get; set; }
    }
}
