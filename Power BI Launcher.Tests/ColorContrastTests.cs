using Windows.UI;

namespace PowerBILauncher.Tests;

public class ColorContrastTests
{
    [Theory]
    [InlineData("#FFFFFF", 255, 255, 255)]
    [InlineData("#000000", 0, 0, 0)]
    [InlineData("4F6BED", 79, 107, 237)]
    public void TryParseHex_parses_six_digit_hex(string hex, byte r, byte g, byte b)
    {
        bool ok = ColorContrast.TryParseHex(hex, out Color color);

        Assert.True(ok);
        Assert.Equal(255, color.A);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void TryParseHex_rejects_blank_or_invalid()
    {
        Assert.False(ColorContrast.TryParseHex(null, out _));
        Assert.False(ColorContrast.TryParseHex("   ", out _));
        Assert.False(ColorContrast.TryParseHex("#GGGGGG", out _));
    }

    [Fact]
    public void ResolveCardColor_uses_theme_surface_when_missing()
    {
        Color resolved = ColorContrast.ResolveCardColor(null);

        Assert.Equal(AppTheme.CardSurfaceColor, resolved);
    }

    [Fact]
    public void ContrastForeground_picks_readable_text_on_light_and_dark_cards()
    {
        Color onLight = ColorContrast.ContrastForeground("#FFFFFF");
        Color onDark = ColorContrast.ContrastForeground("#000000");

        Assert.True(onLight.R + onLight.G + onLight.B < onDark.R + onDark.G + onDark.B);
    }

    [Fact]
    public void ContrastForeground_picks_readable_text_on_avatar_palette_colors()
    {
        foreach (string hex in new[]
                 {
                     "#4F6BED",
                     "#D83B01",
                     "#107C10",
                     "#8764B8",
                     "#038387"
                 })
        {
            Color background = ColorContrast.ResolveCardColor(hex);
            Color foreground = ColorContrast.ContrastForeground(hex);
            double bgLuminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
            double fgLuminance = (0.299 * foreground.R + 0.587 * foreground.G + 0.114 * foreground.B) / 255;

            Assert.True(Math.Abs(bgLuminance - fgLuminance) > 0.2);
        }
    }

    [Fact]
    public void ToHex_round_trips_rgb()
    {
        var color = Color.FromArgb(255, 79, 107, 237);

        string hex = ColorContrast.ToHex(color);

        Assert.True(ColorContrast.TryParseHex(hex, out Color parsed));
        Assert.Equal(color.R, parsed.R);
        Assert.Equal(color.G, parsed.G);
        Assert.Equal(color.B, parsed.B);
    }
}
