namespace Dualtooth;

/// <summary>Colors, fonts and bevel drawing for the Dualtooth skin.</summary>
static class Skin
{
    public static readonly Color Body = Color.FromArgb(0x2A, 0x2D, 0x3E);
    public static readonly Color BodyLight = Color.FromArgb(0x5A, 0x60, 0x7E);
    public static readonly Color BodyDark = Color.FromArgb(0x10, 0x11, 0x18);
    public static readonly Color Lcd = Color.FromArgb(0x03, 0x08, 0x06);
    public static readonly Color Green = Color.FromArgb(0x39, 0xFF, 0x7A);
    public static readonly Color GreenDim = Color.FromArgb(0x1E, 0x7A, 0x45);
    public static readonly Color GreenGlow = Color.FromArgb(0x0B, 0x33, 0x1D);
    public static readonly Color Cyan = Color.FromArgb(0x3E, 0xE8, 0xFF);
    public static readonly Color Yellow = Color.FromArgb(0xFF, 0xE0, 0x3A);
    public static readonly Color Red = Color.FromArgb(0xFF, 0x4D, 0x4D);
    public static readonly Color Selection = Color.FromArgb(0x1A, 0x2A, 0xC8);
    public static readonly Color LabelText = Color.FromArgb(0xA8, 0xB0, 0xD0);

    public static readonly Font LcdLarge = new("Consolas", 13f, FontStyle.Bold);
    public static readonly Font LcdSmall = new("Consolas", 9.5f, FontStyle.Bold);
    public static readonly Font Label = new("Segoe UI", 7.5f, FontStyle.Bold);
    public static readonly Font ButtonLarge = new("Segoe UI", 11f, FontStyle.Bold);

    /// <summary>Draws a classic 3D bevel: light on the top and left, dark on the bottom and right (or reversed when sunken).</summary>
    public static void Bevel(Graphics g, Rectangle r, bool raised, int width = 1)
    {
        using var light = new Pen(raised ? BodyLight : BodyDark);
        using var dark = new Pen(raised ? BodyDark : BodyLight);
        for (var i = 0; i < width; i++)
        {
            g.DrawLine(light, r.Left + i, r.Top + i, r.Right - 1 - i, r.Top + i);
            g.DrawLine(light, r.Left + i, r.Top + i, r.Left + i, r.Bottom - 1 - i);
            g.DrawLine(dark, r.Left + i, r.Bottom - 1 - i, r.Right - 1 - i, r.Bottom - 1 - i);
            g.DrawLine(dark, r.Right - 1 - i, r.Top + i, r.Right - 1 - i, r.Bottom - 1 - i);
        }
    }

    /// <summary>Faint horizontal lines that make a black panel read as an old LCD.</summary>
    public static void Scanlines(Graphics g, Rectangle r)
    {
        using var pen = new Pen(Color.FromArgb(60, 0, 0, 0));
        for (var y = r.Top; y < r.Bottom; y += 2)
            g.DrawLine(pen, r.Left, y, r.Right, y);
    }

    public static float Scale(Control c) => c.DeviceDpi / 96f;
}
