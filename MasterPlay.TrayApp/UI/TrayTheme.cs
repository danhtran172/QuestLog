using Microsoft.Win32;

namespace MasterPlay.TrayApp.UI;

/// <summary>
/// Bang mau cua menu tray, lay thang tu token cua web
/// (MasterPlay.Web/src/index.css) de tray va bang Tray trong app cung mot he mau.
/// Gia tri oklch trong CSS da duoc quy doi san ra sRGB o day.
/// </summary>
public sealed class TrayTheme
{
    public required string Name { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceAlt { get; init; }
    public required Color Border { get; init; }
    public required Color Foreground { get; init; }
    public required Color MutedForeground { get; init; }
    public required Color Primary { get; init; }
    public required Color PrimaryForeground { get; init; }
    public required Color Danger { get; init; }

    /// <summary>Nen cua dong dang di chuot qua, bang primary pha loang vao surface.</summary>
    public Color Hover => Blend(Primary, Surface, 0.22);

    // Switch cua web (components/ui.tsx): tat = ray muted-fg/30 vien muted-fg/80,
    // num mau fg; bat = ray va vien primary, num primary-fg.
    public Color SwitchTrackOff => Blend(MutedForeground, Surface, 0.30);
    public Color SwitchBorderOff => Blend(MutedForeground, Surface, 0.80);
    public Color SwitchKnobOff => Foreground;
    public Color SwitchTrackOn => Primary;
    public Color SwitchKnobOn => PrimaryForeground;

    // Card dich vu, theo utility surface-card cua web: mat sang hon nen mot chut,
    // vien pha 9% trang; di chuot qua thi vien pha 45% primary.
    public Color CardFill => Blend(Foreground, Surface, 0.05);
    public Color CardBorder => Blend(Foreground, Border, 0.09);
    public Color CardBorderHover => Blend(Primary, Border, 0.45);

    // --questory-surface / surface-2 / border / fg / muted-fg / primary / primary-fg / danger
    public static TrayTheme Dark { get; } = new()
    {
        Name = "dark",
        Surface = Color.FromArgb(52, 56, 61),
        SurfaceAlt = Color.FromArgb(64, 68, 74),
        Border = Color.FromArgb(75, 79, 85),
        Foreground = Color.FromArgb(195, 198, 201),
        MutedForeground = Color.FromArgb(147, 151, 156),
        Primary = Color.FromArgb(109, 136, 99),
        PrimaryForeground = Color.FromArgb(10, 20, 12),
        Danger = Color.FromArgb(215, 104, 104)
    };

    public static TrayTheme Light { get; } = new()
    {
        Name = "light",
        Surface = Color.FromArgb(253, 254, 251),
        SurfaceAlt = Color.FromArgb(233, 236, 229),
        Border = Color.FromArgb(214, 219, 211),
        Foreground = Color.FromArgb(40, 48, 40),
        MutedForeground = Color.FromArgb(100, 107, 100),
        Primary = Color.FromArgb(84, 107, 80),
        PrimaryForeground = Color.FromArgb(246, 248, 242),
        Danger = Color.FromArgb(171, 65, 62)
    };

    public static TrayTheme Jungle { get; } = new()
    {
        Name = "jungle",
        Surface = Color.FromArgb(39, 51, 56),
        SurfaceAlt = Color.FromArgb(42, 72, 73),
        Border = Color.FromArgb(97, 135, 100),
        Foreground = Color.FromArgb(233, 241, 217),
        MutedForeground = Color.FromArgb(156, 176, 128),
        Primary = Color.FromArgb(156, 176, 128),
        PrimaryForeground = Color.FromArgb(5, 10, 13),
        Danger = Color.FromArgb(214, 112, 100)
    };

    /// <summary>
    /// ThemeMode trong config.json dung chung voi UI (DOMAIN_RULES muc 15).
    /// "system" bam theo cai dat sang/toi cua Windows, giong prefers-color-scheme.
    /// </summary>
    public static TrayTheme Resolve(string? themeMode)
    {
        return (themeMode?.Trim().ToLowerInvariant()) switch
        {
            "light" => Light,
            "dark" => Dark,
            "jungle" => Jungle,
            _ => WindowsPrefersLight() ? Light : Dark
        };
    }

    private static bool WindowsPrefersLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            // Khong doc duoc thi theo mac dinh cua web: :root la theme toi.
            return false;
        }
    }

    public static Color Blend(Color front, Color back, double amount)
    {
        amount = Math.Clamp(amount, 0d, 1d);
        return Color.FromArgb(
            (int)Math.Round(front.R * amount + back.R * (1 - amount)),
            (int)Math.Round(front.G * amount + back.G * (1 - amount)),
            (int)Math.Round(front.B * amount + back.B * (1 - amount)));
    }
}
