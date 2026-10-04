using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp.UI;

/// <summary>Vai tro cua mot dong trong menu, quyet dinh cach ve dong do.</summary>
public enum TrayMenuRole
{
    Default,
    Header,
    Subtitle,
    Toggle,
    Danger
}

/// <summary>
/// Ve menu chuot phai cua tray theo dung ngon ngu thi giac cua bang Tray trong
/// app (MasterPlay.Web/src/components/TrayPanel.tsx): mot mat phang surface, dong
/// dang tro toi bo tron va pha mau primary, dich vu co cong tac bat/tat ben phai.
/// </summary>
public sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly TrayTheme _theme;

    public TrayMenuRenderer(TrayTheme theme) : base(new TrayColorTable(theme))
    {
        _theme = theme;
        RoundedEdges = false;
    }

    private static TrayMenuRole RoleOf(ToolStripItem item) =>
        item.Tag is TrayMenuRole role ? role : TrayMenuRole.Default;

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.Clear(_theme.Surface);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(_theme.Border);
        var bounds = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // Khong ve cot anh mau khac nhu menu Windows mac dinh: bang Tray trong app
        // la mot mat phang lien, khong co dai xam ben trai.
        using var brush = new SolidBrush(_theme.Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    /// <summary>
    /// Khung that cua mot dong, tinh trong he toa do cua chinh dong do.
    /// KHONG duoc dung Item.Width: do la be rong dong "muon co", co the rong hon
    /// khung menu (do thay 328 trong khi menu chi 269), nen moi thu neo vao canh
    /// phai cua no deu bi ve ra ngoai va mat hut.
    /// </summary>
    private static Rectangle RowBounds(ToolStripItem item, ToolStrip? owner)
    {
        if (owner == null)
            return new Rectangle(Point.Empty, item.Size);

        var left = owner.Padding.Left - item.Bounds.X;
        var right = owner.ClientSize.Width - owner.Padding.Right - item.Bounds.X;
        return Rectangle.FromLTRB(left, 0, Math.Max(left + 1, right), item.Height);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var row = RowBounds(e.Item, e.ToolStrip);
        var inset = Scale(e.ToolStrip, 10);
        var y = e.Item.Height / 2;
        using var pen = new Pen(_theme.Border);
        e.Graphics.DrawLine(pen, row.Left + inset, y, row.Right - inset, y);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var role = RoleOf(item);
        var bounds = RowBounds(item, e.ToolStrip);

        var highlighted = item.Selected && item.Enabled
            && role is not (TrayMenuRole.Header or TrayMenuRole.Subtitle);

        if (highlighted)
        {
            var fill = role == TrayMenuRole.Danger
                ? TrayTheme.Blend(_theme.Danger, _theme.Surface, 0.22)
                : _theme.Hover;

            var padded = Rectangle.Inflate(bounds, -Scale(e.ToolStrip, 4), -1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(fill);
            using var path = RoundedRectangle(padded, Scale(e.ToolStrip, 6));
            e.Graphics.FillPath(brush, path);
        }

        if (role == TrayMenuRole.Toggle && item is ToolStripMenuItem toggle)
            DrawSwitch(e.Graphics, e.ToolStrip, bounds, toggle.Checked);
    }

    // Cong tac gat, thay cho dau tich cua menu Windows: bang Tray trong app dung
    // Switch cho ba dich vu nen o day cung phai la cong tac.
    private void DrawSwitch(Graphics graphics, ToolStrip? owner, Rectangle bounds, bool isOn)
    {
        var width = Scale(owner, 26);
        var height = Scale(owner, 14);
        var margin = Scale(owner, 10);

        var track = new Rectangle(
            bounds.Right - width - margin,
            bounds.Top + (bounds.Height - height) / 2,
            width,
            height);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var trackBrush = new SolidBrush(isOn ? _theme.SwitchTrackOn : _theme.SwitchTrackOff))
        using (var trackPath = RoundedRectangle(track, height / 2))
            graphics.FillPath(trackBrush, trackPath);

        var inset = Scale(owner, 2);
        var knobSize = height - inset * 2;
        var knobX = isOn ? track.Right - knobSize - inset : track.Left + inset;

        using var knobBrush = new SolidBrush(isOn ? _theme.SwitchKnobOn : _theme.SwitchKnobOff);
        graphics.FillEllipse(knobBrush, knobX, track.Top + inset, knobSize, knobSize);
    }

    // Ve chu truc tiep thay vi goi base: base ve chu cua dong bi vo hieu bang
    // kieu "khac chim" cua Windows, tren nen toi cua theme trong ra rat ban.
    // Tieu de va dong trang thai deu la dong bi vo hieu (de khong bam duoc).
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var color = RoleOf(e.Item) switch
        {
            TrayMenuRole.Header => _theme.Foreground,
            TrayMenuRole.Subtitle => _theme.MutedForeground,
            TrayMenuRole.Danger => e.Item.Selected ? _theme.Danger : _theme.Foreground,
            _ => e.Item.Enabled ? _theme.Foreground : _theme.MutedForeground
        };

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont ?? e.Item.Font,
            e.TextRectangle,
            color,
            e.TextFormat);
    }

    // Icon cua dong bi vo hieu cung khong duoc lam xam: icon o day la mau cua
    // theme, khong phai anh chup man hinh cua Windows.
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image == null)
            return;

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(e.Image, e.ImageRectangle);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        // Trang thai bat/tat da nam o cong tac ben phai, khong ve them dau tich
        // de len icon ben trai.
    }

    private static int Scale(ToolStrip? owner, int value)
    {
        var dpi = owner?.DeviceDpi ?? 96;
        return (int)Math.Round(value * dpi / 96d);
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = radius * 2;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270f, 90f);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }

    private sealed class TrayColorTable : ProfessionalColorTable
    {
        private readonly TrayTheme _theme;

        public TrayColorTable(TrayTheme theme)
        {
            _theme = theme;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => _theme.Surface;
        public override Color ImageMarginGradientBegin => _theme.Surface;
        public override Color ImageMarginGradientMiddle => _theme.Surface;
        public override Color ImageMarginGradientEnd => _theme.Surface;
        public override Color MenuBorder => _theme.Border;
        public override Color MenuItemBorder => _theme.Hover;
        public override Color MenuItemSelected => _theme.Hover;
        public override Color MenuItemSelectedGradientBegin => _theme.Hover;
        public override Color MenuItemSelectedGradientEnd => _theme.Hover;
        public override Color MenuItemPressedGradientBegin => _theme.Hover;
        public override Color MenuItemPressedGradientMiddle => _theme.Hover;
        public override Color MenuItemPressedGradientEnd => _theme.Hover;
        public override Color SeparatorDark => _theme.Border;
        public override Color SeparatorLight => _theme.Border;
        public override Color CheckBackground => _theme.Hover;
        public override Color CheckSelectedBackground => _theme.Hover;
        public override Color CheckPressedBackground => _theme.Hover;
    }
}

/// <summary>
/// Bo goc cua cua so menu theo kieu Windows 11. Khong lam duoc thi thoi, menu
/// van hien binh thuong voi goc vuong.
/// </summary>
public static class TrayMenuWindow
{
    private const int DwmWindowCornerPreference = 33;
    private const int DwmCornerRound = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void UseRoundedCorners(Control control)
    {
        try
        {
            if (!control.IsHandleCreated)
                return;

            var preference = DwmCornerRound;
            DwmSetWindowAttribute(control.Handle, DwmWindowCornerPreference, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 tro xuong khong co thuoc tinh nay.
        }
    }
}
