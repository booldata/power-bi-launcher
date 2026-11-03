namespace PowerBILauncher
{
    public enum MinimizeButtonBehavior
    {
        Minimize,
        SendToTray
    }

    public enum CloseButtonBehavior
    {
        Close = 0,
        /// <summary>Obsolete: migrated to <see cref="Close"/> on load. Kept for old settings.json values.</summary>
        Minimize = 1,
        SendToTray = 2
    }
}
