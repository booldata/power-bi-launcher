using System;
using System.Collections.Generic;
using System.IO;

namespace PowerBILauncher
{
    public static class LetterAvatar
    {
        private static readonly Random Random = new();

        private static readonly string[] Palette =
        {
            "#4F6BED",
            "#D83B01",
            "#107C10",
            "#8764B8",
            "#038387",
            "#C239B3",
            "#004E8C",
            "#CA5010",
            "#69797E",
            "#8E562E"
        };

        /// <summary>Preset Avatar colors shown as Edit Profile swatches.</summary>
        public static IReadOnlyList<string> PaletteColors => Palette;

        public static string PickRandomColor() => Palette[Random.Next(Palette.Length)];

        public static char GetLetter(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return '?';
            }

            char first = name.Trim()[0];
            return char.IsLetterOrDigit(first) ? char.ToUpperInvariant(first) : '?';
        }

        public static bool HasCustomIcon(string? iconPath) =>
            !string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath);
    }
}
