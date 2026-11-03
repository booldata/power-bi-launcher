using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using IOPath = System.IO.Path;

namespace PowerBILauncher;

/// <summary>
/// Shared Avatar presentation for Profile board cards and Edit Profile preview:
/// letter vs custom image, Avatar Color + contrast, async ImageBrush load,
/// stale-path guard, and letter fallback on load failure.
/// </summary>
public static class AvatarPaint
{
    public static void Apply(
        string? iconPath,
        string? name,
        string? avatarColor,
        Ellipse ellipse,
        TextBlock letter)
    {
        ellipse.Tag = null;
        ellipse.Visibility = Visibility.Visible;
        letter.Visibility = Visibility.Visible;
        CenterLetterInCircle(letter);
        letter.Text = LetterAvatar.GetLetter(name).ToString();

        string effectiveAvatarHex = avatarColor ?? "";
        if (!ColorContrast.TryParseHex(effectiveAvatarHex, out Color color))
        {
            effectiveAvatarHex = LetterAvatar.PickRandomColor();
            ColorContrast.TryParseHex(effectiveAvatarHex, out color);
        }

        ellipse.Fill = new SolidColorBrush(color);
        letter.Foreground = new SolidColorBrush(ColorContrast.ContrastForeground(effectiveAvatarHex));

        if (!LetterAvatar.HasCustomIcon(iconPath))
        {
            return;
        }

        string logoPath = IOPath.GetFullPath(iconPath!);
        ellipse.Tag = logoPath;
        _ = ApplyLogoBrushAsync(logoPath, ellipse, letter);
    }

    private static async Task ApplyLogoBrushAsync(
        string logoPath,
        Ellipse ellipse,
        TextBlock letter)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(logoPath);
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);

            if (!string.Equals(ellipse.Tag as string, logoPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ellipse.Fill = new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.UniformToFill
            };
            letter.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // Keep the letter avatar already applied by Apply.
        }
    }

    // Tight = cap height to baseline. TrimToCapHeight still includes descent, so the glyph sits high.
    private static void CenterLetterInCircle(TextBlock letter)
    {
        letter.HorizontalAlignment = HorizontalAlignment.Center;
        letter.VerticalAlignment = VerticalAlignment.Center;
        letter.TextAlignment = TextAlignment.Center;
        letter.TextWrapping = TextWrapping.NoWrap;
        letter.TextTrimming = TextTrimming.None;
        letter.TextLineBounds = TextLineBounds.Tight;
        letter.OpticalMarginAlignment = OpticalMarginAlignment.TrimSideBearings;
        letter.UseLayoutRounding = false;
        letter.Padding = new Thickness(0);
        letter.Margin = new Thickness(0);

        double fontSize = letter.FontSize;
        if (double.IsNaN(fontSize) || fontSize <= 0)
        {
            fontSize = 20;
        }

        letter.RenderTransform = new TranslateTransform
        {
            X = -fontSize * 0.06
        };
    }
}
