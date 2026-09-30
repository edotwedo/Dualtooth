using System.Drawing.Drawing2D;
using Dualtooth.Core;

namespace Dualtooth;

/// <summary>Base for the skinned controls: owner-drawn and double-buffered.</summary>
abstract class SkinControl : Control
{
    protected SkinControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected int S(float value) => (int)Math.Round(value * Skin.Scale(this));
}

/// <summary>A rounded, softly lit button.</summary>
sealed class SkinButton : SkinControl
{
    bool hover, pressed;

    public bool Accent { get; set; }

    public SkinButton()
    {
        Font = Skin.Label;
        Cursor = Cursors.Hand;
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var radius = Math.Min(S(5), Height / 2f);

        var lift = !Enabled ? 0.05f : pressed ? 0f : hover ? 0.3f : 0.15f;
        var top = ControlPaint.Light(Skin.Body, lift);
        var bottom = pressed ? Skin.Body : Skin.BodyDark;
        using (var path = Skin.RoundRect(r, radius))
        using (var brush = new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical))
        using (var edge = new Pen(Accent && Enabled ? Color.FromArgb(hover ? 200 : 120, Skin.Accent) : Skin.BodyDark))
        {
            g.FillPath(brush, path);
            g.DrawPath(edge, path);
        }
        if (!pressed)
            using (var shine = new Pen(Color.FromArgb(40, Color.White)))
                g.DrawLine(shine, r.Left + radius, r.Top + 1, r.Right - radius, r.Top + 1);

        var textColor = !Enabled ? Skin.BodyLight : Accent ? Skin.Accent : Skin.BrightText;
        var textRect = Rectangle.Round(r);
        if (pressed) textRect.Offset(0, 1);
        TextRenderer.DrawText(g, Text, Font, textRect, textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

/// <summary>The main screen: a scrolling marquee, two status lines, and a WIN ⇄ LNX signal wave.</summary>
sealed class LcdDisplay : SkinControl
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    readonly List<(float Position, int Direction)> pulses = [];
    readonly Random random = new();
    float scroll, phase;
    int partyFrames;

    public string Marquee { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
    public Color Line2Color { get; set; } = Skin.Glow;

    public LcdDisplay()
    {
        BackColor = Skin.Lcd;
        timer.Tick += (_, _) => Animate();
        timer.Start();
    }

    /// <summary>Sends a burst of pulses back and forth for a couple of seconds.</summary>
    public void Party() => partyFrames = 90;

    void Animate()
    {
        scroll += 1.3f * Skin.Scale(this);
        phase += partyFrames > 0 ? 0.16f : 0.06f;

        if (random.NextDouble() < (partyFrames > 0 ? 0.3 : 0.035))
        {
            var direction = random.Next(2) == 0 ? 1 : -1;
            pulses.Add((direction == 1 ? 0f : 1f, direction));
        }
        var speed = partyFrames > 0 ? 0.028f : 0.013f;
        for (var i = pulses.Count - 1; i >= 0; i--)
        {
            var (position, direction) = pulses[i];
            position += direction * speed;
            if (position is < 0 or > 1) pulses.RemoveAt(i);
            else pulses[i] = (position, direction);
        }

        if (partyFrames > 0) partyFrames--;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Skin.Lcd);
        int pad = S(7), waveWidth = S(150);

        // Marquee
        var marqueeRect = new Rectangle(pad, pad, Width - waveWidth - 3 * pad, Skin.LcdLarge.Height + S(2));
        if (Marquee.Length > 0)
        {
            var textWidth = TextRenderer.MeasureText(g, Marquee, Skin.LcdLarge, Size.Empty, TextFormatFlags.NoPadding).Width;
            var x = marqueeRect.X - (int)(scroll % textWidth);
            g.SetClip(marqueeRect);
            for (var copy = x; copy < marqueeRect.Right; copy += textWidth)
            {
                TextRenderer.DrawText(g, Marquee, Skin.LcdLarge, new Point(copy + 1, marqueeRect.Y + 1), Skin.GlowShadow, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Marquee, Skin.LcdLarge, new Point(copy, marqueeRect.Y), Skin.Glow, TextFormatFlags.NoPadding);
            }
            g.ResetClip();
        }

        // Status lines
        var lineY = marqueeRect.Bottom + S(10);
        const TextFormatFlags lineFlags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, Line1, Skin.LcdSmall, new Rectangle(pad, lineY, marqueeRect.Width, Skin.LcdSmall.Height), Skin.Glow, lineFlags);
        TextRenderer.DrawText(g, Line2, Skin.LcdSmall, new Rectangle(pad, lineY + Skin.LcdSmall.Height + S(4), marqueeRect.Width, Skin.LcdSmall.Height), Line2Color, lineFlags);

        DrawSignalWave(g, new Rectangle(Width - waveWidth - pad, pad, waveWidth, Height - 2 * pad));
        Skin.Scanlines(g, ClientRectangle);
    }

    void DrawSignalWave(Graphics g, Rectangle area)
    {
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        var labelSize = TextRenderer.MeasureText(g, "LNX", Skin.LcdSmall, Size.Empty, flags);
        var labelY = area.Y + (area.Height - labelSize.Height) / 2;
        TextRenderer.DrawText(g, "WIN", Skin.LcdSmall, new Point(area.X, labelY), Skin.Glow, flags);
        TextRenderer.DrawText(g, "LNX", Skin.LcdSmall, new Point(area.Right - labelSize.Width, labelY), Skin.Glow, flags);

        float x0 = area.X + labelSize.Width + S(6), x1 = area.Right - labelSize.Width - S(6);
        float midY = area.Y + area.Height / 2f, amplitude = area.Height * 0.28f;
        PointF At(float t) => new(
            x0 + t * (x1 - x0),
            midY + amplitude * MathF.Sin(t * MathF.PI * 3 + phase) * MathF.Sin(t * MathF.PI));

        // The wave itself, as a dotted line
        using (var dot = new SolidBrush(Skin.GlowDim))
        {
            var step = Math.Max(3, S(4));
            for (var x = x0; x <= x1; x += step)
            {
                var p = At((x - x0) / (x1 - x0));
                g.FillRectangle(dot, p.X, p.Y, Math.Max(1, S(2)), Math.Max(1, S(2)));
            }
        }

        // Pulses travelling between the two systems, with a short trail
        var mode = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var halo = new SolidBrush(Color.FromArgb(60, Skin.Glow));
        using var core = new SolidBrush(Skin.Glow);
        using var trail = new SolidBrush(Color.FromArgb(110, Skin.Glow));
        float big = S(6), small = S(2.5f);
        foreach (var (position, direction) in pulses)
        {
            for (var k = 1; k <= 3; k++)
            {
                var tp = At(Math.Clamp(position - direction * 0.03f * k, 0, 1));
                var size = small * (1 - k * 0.2f);
                g.FillEllipse(trail, tp.X - size / 2, tp.Y - size / 2, size, size);
            }
            var p = At(position);
            g.FillEllipse(halo, p.X - big / 2, p.Y - big / 2, big, big);
            g.FillEllipse(core, p.X - small / 2, p.Y - small / 2, small, small);
        }
        g.SmoothingMode = mode;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A static screen for messages and instructions.</summary>
sealed class LcdReadout : SkinControl
{
    public Color TextColor { get; set; } = Skin.Glow;
    public bool PathMode { get; set; }

    public LcdReadout()
    {
        BackColor = Skin.Lcd;
        Font = Skin.LcdSmall;
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Skin.Lcd);
        var r = Rectangle.Inflate(ClientRectangle, -S(7), -S(5));
        var flags = PathMode
            ? TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            : TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, Text, Font, r, TextColor, flags);
        Skin.Scanlines(g, ClientRectangle);
    }
}

/// <summary>The device list. Each row has an LED that lights up when the device will be synced; click a row to toggle it.</summary>
sealed class DevicePlaylist : SkinControl
{
    sealed class Row(PairedDevice device) { public PairedDevice Device = device; public bool On = device.Name is not null; }

    List<Row> rows = [];
    int current = -1, top;

    public string EmptyText { get; set; } = "";
    public event EventHandler? SelectionChanged;

    public int Count => rows.Count;
    public int OnCount => rows.Count(r => r.On);
    public IEnumerable<PairedDevice> SelectedDevices => rows.Where(r => r.On).Select(r => r.Device);

    public DevicePlaylist()
    {
        BackColor = Skin.Lcd;
        Font = Skin.LcdSmall;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    int RowHeight => Font.Height + S(7);
    int VisibleRows => Math.Max(1, Height / RowHeight);

    public void SetDevices(IEnumerable<PairedDevice> devices)
    {
        rows = devices.Select(d => new Row(d)).ToList();
        current = rows.Count > 0 ? 0 : -1;
        top = 0;
        Changed();
    }

    public void SetAll(bool on)
    {
        foreach (var row in rows) row.On = on;
        Changed();
    }

    void Changed()
    {
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    void Toggle(int index)
    {
        if (index < 0 || index >= rows.Count) return;
        current = index;
        rows[index].On = !rows[index].On;
        Changed();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left) Toggle(top + e.Y / RowHeight);
        base.OnMouseDown(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        top = Math.Clamp(top - Math.Sign(e.Delta) * 3, 0, Math.Max(0, rows.Count - VisibleRows));
        Invalidate();
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Up or Keys.Down or Keys.Space || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (rows.Count == 0) return;
        if (e.KeyCode == Keys.Space) { Toggle(current); return; }
        if (e.KeyCode is Keys.Up or Keys.Down)
        {
            current = Math.Clamp(current + (e.KeyCode == Keys.Up ? -1 : 1), 0, rows.Count - 1);
            if (current < top) top = current;
            if (current >= top + VisibleRows) top = current - VisibleRows + 1;
            Invalidate();
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Skin.Lcd);

        if (rows.Count == 0)
        {
            TextRenderer.DrawText(g, EmptyText, Font, ClientRectangle, Skin.GlowDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        int pad = S(9), led = S(8), rowHeight = RowHeight;
        var scrollbar = rows.Count > VisibleRows ? S(5) : 0;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        for (var i = top; i < Math.Min(rows.Count, top + VisibleRows + 1); i++)
        {
            var row = rows[i];
            var rect = new Rectangle(0, (i - top) * rowHeight, Width - scrollbar, rowHeight);
            var selected = i == current && Focused;
            if (selected)
                using (var sel = new SolidBrush(Skin.Selection)) g.FillRectangle(sel, rect);

            // LED
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var ledRect = new RectangleF(pad, rect.Y + (rowHeight - led) / 2f, led, led);
            if (row.On)
            {
                using var halo = new SolidBrush(Color.FromArgb(55, Skin.Glow));
                using var lit = new SolidBrush(Skin.Glow);
                g.FillEllipse(halo, RectangleF.Inflate(ledRect, S(3), S(3)));
                g.FillEllipse(lit, ledRect);
            }
            else
            {
                using var off = new Pen(Skin.GlowDim, Math.Max(1, S(1)));
                g.DrawEllipse(off, ledRect);
            }
            g.SmoothingMode = SmoothingMode.None;

            var nameColor = selected ? Color.White : row.On ? Skin.Glow : Skin.GlowDim;
            var kind = row.Device.Kind.ToUpperInvariant();
            var transport = row.Device.TransportLabel.ToUpperInvariant();
            var transportWidth = TextRenderer.MeasureText(g, transport, Font, Size.Empty, flags).Width;
            var kindWidth = TextRenderer.MeasureText(g, kind, Font, Size.Empty, flags).Width;
            var transportRect = new Rectangle(rect.Right - transportWidth - pad, rect.Y, transportWidth, rowHeight);
            var kindRect = new Rectangle(transportRect.Left - kindWidth - S(12), rect.Y, kindWidth, rowHeight);
            var nameLeft = (int)ledRect.Right + pad;
            var nameRect = new Rectangle(nameLeft, rect.Y, kindRect.Left - nameLeft - pad, rowHeight);

            TextRenderer.DrawText(g, row.Device.Name ?? "Unknown device", Font, nameRect, nameColor, flags | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, kind, Font, kindRect, row.On ? Skin.Accent : Skin.GlowDim, flags);
            TextRenderer.DrawText(g, transport, Font, transportRect, Skin.GlowDim, flags);
        }

        if (scrollbar > 0)
        {
            var track = new Rectangle(Width - scrollbar, 0, scrollbar, Height);
            using (var trackBrush = new SolidBrush(Skin.GlowShadow)) g.FillRectangle(trackBrush, track);
            var thumbHeight = Math.Max(S(12), Height * VisibleRows / rows.Count);
            var thumbY = (Height - thumbHeight) * top / Math.Max(1, rows.Count - VisibleRows);
            using var thumb = new SolidBrush(Skin.GlowDim);
            g.FillRectangle(thumb, track.X + 1, thumbY, track.Width - 2, thumbHeight);
        }
    }
}
