using System.Drawing.Drawing2D;

namespace Dualtooth;

/// <summary>Colors, fonts and drawing helpers for the Dualtooth skin.</summary>
static class Skin
{
    // Warm incandescent palette: filament-orange glow on smoky dark brown.
    public static readonly Color Body = Color.FromArgb(0x22, 0x18, 0x12);
    public static readonly Color BodyLight = Color.FromArgb(0x4E, 0x36, 0x27);
    public static readonly Color BodyDark = Color.FromArgb(0x0F, 0x09, 0x06);
    public static readonly Color Lcd = Color.FromArgb(0x12, 0x08, 0x04);
    public static readonly Color Glow = Color.FromArgb(0xFF, 0x9F, 0x3A);
    public static readonly Color GlowDim = Color.FromArgb(0x8A, 0x4B, 0x18);
    public static readonly Color GlowShadow = Color.FromArgb(0x3D, 0x1A, 0x06);
    public static readonly Color Accent = Color.FromArgb(0xFF, 0xD9, 0x8A);
    public static readonly Color Red = Color.FromArgb(0xFF, 0x55, 0x3D);
    public static readonly Color Selection = Color.FromArgb(0x5C, 0x2C, 0x10);
    public static readonly Color LabelText = Color.FromArgb(0xD6, 0xB4, 0x96);
    public static readonly Color BrightText = Color.FromArgb(0xF6, 0xE6, 0xD6);

    public static readonly Font LcdLarge = new("Consolas", 13f, FontStyle.Bold);
    public static readonly Font LcdSmall = new("Consolas", 9.5f, FontStyle.Bold);
    public static readonly Font Label = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font Hint = new("Segoe UI", 8f);
    public static readonly Font Guide = new("Consolas", 9f, FontStyle.Bold);
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
