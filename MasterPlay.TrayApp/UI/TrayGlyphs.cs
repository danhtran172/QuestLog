using System.Drawing.Drawing2D;

namespace MasterPlay.TrayApp.UI;

public enum TrayGlyph
{
    Power,
    AppWindow,
    Activity,
    FolderSearch,
    ScrollText,
    RefreshCw,
    LogOut
}

/// <summary>
/// Bo icon cua menu tray, ve lai theo hinh cua lucide-react - dung bo icon ma
/// web dang dung (TrayPanel.tsx) - tren khung 24x24, net 2px, dau net bo tron.
/// Ve bang GDI+ thay vi nhung file PNG de icon doi mau theo theme va net theo DPI.
/// </summary>
public static class TrayGlyphs
{
    public static Bitmap Render(TrayGlyph glyph, int size, Color color)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.ScaleTransform(size / 24f, size / 24f);

        using var pen = new Pen(color, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        Draw(glyph, graphics, pen);
        return bitmap;
    }

    private static void Draw(TrayGlyph glyph, Graphics g, Pen pen)
    {
        switch (glyph)
        {
            case TrayGlyph.Power:
                g.DrawLine(pen, 12f, 2f, 12f, 12f);
                g.DrawArc(pen, 3f, 3f, 18f, 18f, -40f, 260f);
                break;

            case TrayGlyph.AppWindow:
                using (var body = RoundedRectangle(2f, 4f, 20f, 16f, 2.5f))
                    g.DrawPath(pen, body);
                g.DrawLine(pen, 2f, 8f, 22f, 8f);
                g.DrawLine(pen, 10f, 4f, 10f, 8f);
                break;

            case TrayGlyph.Activity:
                g.DrawLines(pen, new[]
                {
                    new PointF(22f, 12f), new PointF(18f, 12f), new PointF(15f, 21f),
                    new PointF(9f, 3f), new PointF(6f, 12f), new PointF(2f, 12f)
                });
                break;

            case TrayGlyph.FolderSearch:
                g.DrawLines(pen, new[]
                {
                    new PointF(11f, 20f), new PointF(4.5f, 20f), new PointF(2f, 17.5f),
                    new PointF(2f, 5.5f), new PointF(4.5f, 3f), new PointF(8.5f, 3f),
                    new PointF(10.8f, 6f), new PointF(19.5f, 6f), new PointF(22f, 8.5f),
                    new PointF(22f, 10.5f)
                });
                g.DrawEllipse(pen, 14f, 14f, 6f, 6f);
                g.DrawLine(pen, 19.4f, 19.4f, 21.4f, 21.4f);
                break;

            case TrayGlyph.ScrollText:
                using (var page = RoundedRectangle(4f, 2.5f, 15f, 19f, 2.5f))
                    g.DrawPath(pen, page);
                g.DrawLine(pen, 7.5f, 8f, 15.5f, 8f);
                g.DrawLine(pen, 7.5f, 12f, 15.5f, 12f);
                g.DrawLine(pen, 7.5f, 16f, 12.5f, 16f);
                break;

            case TrayGlyph.RefreshCw:
                g.DrawArc(pen, 3f, 3f, 18f, 18f, 180f, 137f);
                g.DrawLines(pen, new[]
                {
                    new PointF(21f, 3f), new PointF(21f, 8f), new PointF(16f, 8f)
                });
                g.DrawArc(pen, 3f, 3f, 18f, 18f, 0f, 137f);
                g.DrawLines(pen, new[]
                {
                    new PointF(3f, 21f), new PointF(3f, 16f), new PointF(8f, 16f)
                });
                break;

            case TrayGlyph.LogOut:
                g.DrawLines(pen, new[]
                {
                    new PointF(9f, 21f), new PointF(5f, 21f), new PointF(3f, 19f),
                    new PointF(3f, 5f), new PointF(5f, 3f), new PointF(9f, 3f)
                });
                g.DrawLines(pen, new[]
                {
                    new PointF(16f, 17f), new PointF(21f, 12f), new PointF(16f, 7f)
                });
                g.DrawLine(pen, 21f, 12f, 9f, 12f);
                break;
        }
    }

    private static GraphicsPath RoundedRectangle(float x, float y, float width, float height, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(x, y, diameter, diameter, 180f, 90f);
        path.AddArc(x + width - diameter, y, diameter, diameter, 270f, 90f);
        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(x, y + height - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
