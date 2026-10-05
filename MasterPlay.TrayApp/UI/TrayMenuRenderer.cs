using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MasterPlay.TrayApp.UI;

/// <summary>Vai tro cua mot dong trong menu, quyet dinh cach ve dong do.</summary>
public enum TrayMenuRole
{
    Default,
    Header,
    Subtitle,
    /// <summary>Dich vu nen: ve thanh card co cong tac, giong dong dich vu cua web.</summary>
    Service,
    /// <summary>Cong tac le (khoi dong cung Windows): dong thuong kem cong tac.</summary>
    Toggle,
    Danger
}

/// <summary>
/// Ve menu chuot phai cua tray theo dung ngon ngu thi giac cua bang Tray trong
/// app (MasterPlay.Web/src/components/TrayPanel.tsx): mot mat phang surface, moi
/// dich vu nen la mot card bo tron co cong tac ben phai, dong thuong khi tro toi
/// thi bo tron va pha mau primary.
/// </summary>
public sealed class TrayMenuRenderer : ToolStripProfessionalRenderer
{
    // Le trai/phai chung cua card va nen hover, de mep cac dong thang hang.
    private const int RowInset = 8;

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
    /// Cung KHONG duoc lay Padding.Left cua menu lam mep trai: do la ca cot icon
    /// (do duoc 33px), card va nen hover se bat dau sau icon thay vi bao lay no.
    /// </summary>
    private static Rectangle RowBounds(ToolStripItem item, ToolStrip? owner)
    {
        if (owner == null)
            return new Rectangle(Point.Empty, item.Size);

        var left = -item.Bounds.X;
        var right = owner.ClientSize.Width - item.Bounds.X;
        return Rectangle.FromLTRB(left, 0, Math.Max(left + 1, right), item.Height);
    }

    /// <summary>
    /// Mep trai cua icon (va cua dong trang thai khong co icon): le card cong
    /// padding px-3 cua card web, de icon nam gon trong card nhu ban web.
    /// </summary>
    private static int ContentLeft(ToolStripItem item, ToolStrip? owner) =>
        RowBounds(item, owner).Left + Scale(owner, RowInset + 10);

    /// <summary>
    /// Dai doc ma icon va chu cung can giua vao: ca dong tru padding tren/duoi.
    /// Icon va chu PHAI dung chung dai nay. Truoc day icon can theo dai nay con
    /// chu theo TextRectangle cua WinForms, nen tieu de (padding tren 8, duoi 1)
    /// co chu cao hon icon vai diem anh.
    /// </summary>
    private static Rectangle ContentBand(ToolStripItem item) =>
        new(0, item.Padding.Top, item.Width, Math.Max(1, item.Height - item.Padding.Vertical));

    /// <summary>
    /// Be rong toi thieu de moi dong vua chu cua no. ToolStripDropDownMenu tu tinh
    /// be rong theo cot icon mac dinh va BO QUA Padding.Right cua dong, nen voi
    /// chu da bi day vao trong le card, "Khoi dong cung Windows" de len cong tac.
    /// Hinh hoc o day phai khop OnRenderItemText / DrawSwitch.
    /// </summary>
    public static int MinimumMenuWidth(ToolStrip menu)
    {
        var width = 0;
        foreach (ToolStripItem item in menu.Items)
        {
            if (item is ToolStripSeparator || string.IsNullOrEmpty(item.Text))
                continue;

            var role = RoleOf(item);
            var left = Scale(menu, RowInset + 10);
            if (item.Image != null)
                left += item.Image.Width + Scale(menu, 10);

            var text = TextRenderer.MeasureText(item.Text, item.Font, Size.Empty, TextFormatFlags.NoPrefix).Width;
            var right = role is TrayMenuRole.Service or TrayMenuRole.Toggle
                ? Scale(menu, RowInset + 10 + 34 + 12)
                : Scale(menu, RowInset + 12);

            width = Math.Max(width, left + text + right);
        }

        // Cong vien 1px hai ben cua menu.
        return width + 2;
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        // Cung le voi card va nen hover de mep trai/phai cua moi khoi thang hang.
        var row = RowBounds(e.Item, e.ToolStrip);
        var inset = Scale(e.ToolStrip, RowInset);
        var y = e.Item.Height / 2;
        using var pen = new Pen(_theme.Border);
        e.Graphics.DrawLine(pen, row.Left + inset, y, row.Right - inset, y);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var role = RoleOf(item);
        var bounds = RowBounds(item, e.ToolStrip);

        var owner = e.ToolStrip;
        var inset = Scale(owner, RowInset);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (role == TrayMenuRole.Service)
        {
            var card = Rectangle.Inflate(bounds, -inset, -Scale(owner, 3));
            DrawServiceCard(e.Graphics, owner, item, card);
            return;
        }

        var highlighted = item.Selected && item.Enabled
            && role is not (TrayMenuRole.Header or TrayMenuRole.Subtitle);

        if (highlighted)
        {
            var fill = role == TrayMenuRole.Danger
                ? TrayTheme.Blend(_theme.Danger, _theme.Surface, 0.22)
                : _theme.Hover;

            var padded = Rectangle.Inflate(bounds, -inset, -1);
            using var brush = new SolidBrush(fill);
            using var path = RoundedRectangle(padded, Scale(owner, 8));
            e.Graphics.FillPath(brush, path);
        }

        if (role == TrayMenuRole.Toggle && item is ToolStripMenuItem toggle)
        {
            var row = Rectangle.Inflate(bounds, -inset, 0);
            DrawSwitch(e.Graphics, owner, row, toggle.Checked, item.Enabled);
        }
    }

    // Dong dich vu cua web la mot surface-card: nen sang hon mat menu mot chut,
    // vien mong, bo tron; tro toi thi vien nga sang mau primary.
    private void DrawServiceCard(Graphics graphics, ToolStrip? owner, ToolStripItem item, Rectangle card)
    {
        var radius = Scale(owner, 10);

        // Lui vao nua diem anh de vien 1px khong bi nhoe thanh 2px.
        var edge = new RectangleF(card.X + 0.5f, card.Y + 0.5f, card.Width - 1f, card.Height - 1f);
        using (var path = RoundedRectangle(edge, radius))
        {
            using var fill = new SolidBrush(_theme.CardFill);
            graphics.FillPath(fill, path);

            var hovered = item.Selected && item.Enabled;
            using var pen = new Pen(hovered ? _theme.CardBorderHover : _theme.CardBorder);
            graphics.DrawPath(pen, path);
        }

        if (item is ToolStripMenuItem toggle)
            DrawSwitch(graphics, owner, card, toggle.Checked, item.Enabled);
    }

    // Cong tac gat, thay cho dau tich cua menu Windows. Ti le lay tu Switch cua web
    // (h-6 w-11, num 16px, vien 1px) thu nho lai cho vua dong menu. Canh phai cach
    // mep `container` 10px, nhu padding px-3 cua card web.
    private void DrawSwitch(Graphics graphics, ToolStrip? owner, Rectangle container, bool isOn, bool enabled)
    {
        var width = Scale(owner, 34);
        var height = Scale(owner, 19);
        var margin = Scale(owner, 10);

        var track = new RectangleF(
            container.Right - width - margin + 0.5f,
            container.Top + (container.Height - height) / 2 + 0.5f,
            width - 1f,
            height - 1f);

        // Dang bat/tat do (muc bi vo hieu) thi mo di, nhu opacity-45 cua web.
        Color Faded(Color color) => enabled ? color : TrayTheme.Blend(color, _theme.Surface, 0.45);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var trackPath = RoundedRectangle(track, track.Height / 2))
        {
            using var trackBrush = new SolidBrush(Faded(isOn ? _theme.SwitchTrackOn : _theme.SwitchTrackOff));
            graphics.FillPath(trackBrush, trackPath);

            using var border = new Pen(Faded(isOn ? _theme.SwitchTrackOn : _theme.SwitchBorderOff));
            graphics.DrawPath(border, trackPath);
        }

        var inset = Scale(owner, 3);
        var knobSize = track.Height - inset * 2;
        var knobX = isOn ? track.Right - knobSize - inset : track.Left + inset;

        using var knobBrush = new SolidBrush(Faded(isOn ? _theme.SwitchKnobOn : _theme.SwitchKnobOff));
        graphics.FillEllipse(knobBrush, knobX, track.Top + inset, knobSize, knobSize);
    }

    // Ve chu truc tiep thay vi goi base: base ve chu cua dong bi vo hieu bang
    // kieu "khac chim" cua Windows, tren nen toi cua theme trong ra rat ban.
    // Tieu de va dong trang thai deu la dong bi vo hieu (de khong bam duoc).
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var item = e.Item;
        var role = RoleOf(item);
        var color = role switch
        {
            TrayMenuRole.Header => _theme.Foreground,
            TrayMenuRole.Subtitle => _theme.MutedForeground,
            TrayMenuRole.Danger => item.Selected ? _theme.Danger : _theme.Foreground,
            _ => item.Enabled ? _theme.Foreground : _theme.MutedForeground
        };

        // Chu dat ngay sau icon, cach mot khoang gap-3 nhu web. Dong trang thai
        // khong co icon thi thang mep voi icon cua tieu de (NoPadding ben duoi:
        // TextRenderer mac dinh tu chen le trai, day chu lech khoi mep icon).
        var left = ContentLeft(item, e.ToolStrip);
        if (item.Image != null)
            left += item.Image.Width + Scale(e.ToolStrip, 10);

        // WinForms khong can giua chu theo chieu doc cho dong cao (chu dinh sat
        // mep tren card), nen tu can giua vao cung dai voi icon.
        var band = ContentBand(item);
        var right = RowBounds(item, e.ToolStrip).Right;
        var bounds = Rectangle.FromLTRB(left, band.Top, Math.Max(left + 1, right), band.Bottom);
        var format = (e.TextFormat & ~(TextFormatFlags.Top | TextFormatFlags.Bottom | TextFormatFlags.HorizontalCenter | TextFormatFlags.Right))
            | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding;

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont ?? item.Font,
            bounds,
            color,
            format);
    }

    // Icon cua dong bi vo hieu cung khong duoc lam xam: icon o day la mau cua
    // theme, khong phai anh chup man hinh cua Windows. Icon duoc dat lai vao
    // trong le cua card thay vi giua cot icon mac dinh cua Windows.
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
    {
        if (e.Image == null)
            return;

        var item = e.Item;
        var band = ContentBand(item);
        var target = e.ImageRectangle;
        target.X = ContentLeft(item, e.ToolStrip);
        target.Y = band.Top + (band.Height - target.Height) / 2;

        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(e.Image, target);
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

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
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
