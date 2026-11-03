using System;
using System.Globalization;
using Windows.UI;

namespace PowerBILauncher
{
    public static class ColorContrast
    {
        public static bool TryParseHex(string? hexColor, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hexColor))
            {
                return false;
            }

            string hex = hexColor.Trim();
            if (hex.StartsWith('#'))
            {
                hex = hex[1..];
            }

            if (hex.Length == 6 &&
                byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                color = Color.FromArgb(255, r, g, b);
                return true;
            }

            if (hex.Length == 8 &&
                byte.TryParse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte a) &&
                byte.TryParse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r) &&
                byte.TryParse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out g) &&
                byte.TryParse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
            {
                color = Color.FromArgb(a, r, g, b);
                return true;
            }

            return false;
        }

        public static Color ResolveCardColor(string? hexColor)
        {
            if (TryParseHex(hexColor, out Color color))
            {
                return color;
            }

            return AppTheme.CardSurfaceColor;
        }

        public static Color ContrastForeground(string? hexColor)
        {
            Color color = ResolveCardColor(hexColor);
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5
                ? Color.FromArgb(255, 100, 100, 100)
                : Color.FromArgb(255, 200, 200, 200);
        }

        /// <summary>
        /// Subtle hover/pressed lift: lighten dark fills, darken light fills.
        /// </summary>
        public static Color ShiftForInteraction(Color color, byte amount)
        {
            double luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255;
            return luminance > 0.5
                ? Color.FromArgb(
                    color.A,
                    (byte)Math.Max(0, color.R - amount),
                    (byte)Math.Max(0, color.G - amount),
                    (byte)Math.Max(0, color.B - amount))
                : Color.FromArgb(
                    color.A,
                    (byte)Math.Min(255, color.R + amount),
                    (byte)Math.Min(255, color.G + amount),
                    (byte)Math.Min(255, color.B + amount));
        }

        public static string ToHex(Color color) =>
            $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
