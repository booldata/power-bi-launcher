using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace PowerBILauncher
{
    public class Setting
    {
        public string UserProfilePath { get; set; } = GetDefaultUserProfilePath();
        public List<PbiVersion> Versions { get; set; } = new();
        public Guid? DefaultVersionId { get; set; }

        /// <summary>
        /// Legacy single Power BI Path. Read only so existing settings.json can migrate.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? PowerBIPath { get; set; }

        public MinimizeButtonBehavior MinimizeBehavior { get; set; } = MinimizeButtonBehavior.Minimize;
        public CloseButtonBehavior CloseBehavior { get; set; } = CloseButtonBehavior.Close;
        public bool HasCompletedSetup { get; set; }

        public static string GetDefaultUserProfilePath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Power BI Launcher",
                "Profiles");
    }
}
