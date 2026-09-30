using System.Drawing.Drawing2D;

namespace Dualtooth;

/// <summary>Colors, fonts and drawing helpers for the Dualtooth skin.</summary>
static class Skin
{
    public static readonly Color Body = Color.FromArgb(0x1F, 0x1C, 0x36);
    public static readonly Color BodyLight = Color.FromArgb(0x45, 0x3F, 0x70);
    public static readonly Color BodyDark = Color.FromArgb(0x0E, 0x0C, 0x1C);
    public static readonly Color Lcd = Color.FromArgb(0x05, 0x10, 0x16);
    public static readonly Color Glow = Color.FromArgb(0x5C, 0xF2, 0xE6);
    public static readonly Color GlowDim = Color.FromArgb(0x2A, 0x7A, 0x78);
    public static readonly Color GlowShadow = Color.FromArgb(0x0A, 0x2E, 0x33);
    public static readonly Color Accent = Color.FromArgb(0xFF, 0x6F, 0xAE);
    public static readonly Color Red = Color.FromArgb(0xFF, 0x5A, 0x5A);
    public static readonly Color Selection = Color.FromArgb(0x3A, 0x2A, 0x7A);
    public static readonly Color LabelText = Color.FromArgb(0xA9, 0xA3, 0xD6);

    public static readonly Font LcdLarge = new("Consolas", 13f, FontStyle.Bold);
    public static readonly Font LcdSmall = new("Consolas", 9.5f, FontStyle.Bold);
    public static readonly Font Label = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font Title = new("Segoe UI Semibold", 9.5f);
    public static readonly Font ButtonLarge = new("Segoe UI", 11f, FontStyle.Bold);

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>The recessed, rounded frame that sits behind each "screen".</summary>
    public static void Well(Graphics g, Rectangle screen, float scale)
    {
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var outer = RectangleF.Inflate(screen, 3 * scale, 3 * scale);
        using (var path = RoundRect(outer, 5 * scale))
        using (var fill = new SolidBrush(BodyDark))
        using (var edge = new Pen(Color.FromArgb(90, BodyLight), Math.Max(1, scale)))
        {
            g.FillPath(fill, path);
            g.DrawPath(edge, path);
        }
        g.SmoothingMode = mode;
    }

    /// <summary>Faint horizontal lines that make a dark panel read as an old screen.</summary>
    public static void Scanlines(Graphics g, Rectangle r)
    {
        using var pen = new Pen(Color.FromArgb(50, 0, 0, 0));
        for (var y = r.Top; y < r.Bottom; y += 2)
            g.DrawLine(pen, r.Left, y, r.Right, y);
    }

    public static float Scale(Control c) => c.DeviceDpi / 96f;
}
