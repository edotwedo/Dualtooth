using System.Drawing.Drawing2D;
using Dualtooth.Core;

namespace Dualtooth;

/// <summary>Base for the skinned controls: owner-drawn and double-buffered.</summary>
abstract class SkinControl : Control
{
    protected SkinControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected int S(float value) => (int)Math.Round(value * Skin.Scale(this));
}

/// <summary>A chunky beveled button.</summary>
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
        var r = ClientRectangle;
        var top = pressed ? Skin.BodyDark : hover && Enabled ? ControlPaint.Light(Skin.Body, 0.35f) : ControlPaint.Light(Skin.Body, 0.15f);
        var bottom = pressed ? Skin.Body : Skin.BodyDark;
        using (var brush = new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical))
            g.FillRectangle(brush, r);
        Skin.Bevel(g, r, raised: !pressed, width: Math.Max(1, S(1)));

        var textColor = !Enabled ? Skin.BodyLight : Accent ? Skin.Cyan : Color.FromArgb(0xE0, 0xE4, 0xF4);
        var textRect = pressed ? new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height) : r;
        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(textRect.X + 1, textRect.Y + 1, textRect.Width, textRect.Height), Skin.BodyDark, flags);
        TextRenderer.DrawText(g, Text, Font, textRect, textColor, flags);
    }
}

/// <summary>The main LCD: a scrolling marquee, two status lines and a bouncing spectrum analyzer.</summary>
sealed class LcdDisplay : SkinControl
{
    const int BarCount = 18;
    readonly System.Windows.Forms.Timer timer = new() { Interval = 33 };
    readonly float[] bars = new float[BarCount];
    readonly float[] targets = new float[BarCount];
    readonly float[] peaks = new float[BarCount];
    readonly Random random = new();
    float scroll;
    int partyFrames;

    public string Marquee { get; set; } = "";
    public string Line1 { get; set; } = "";
    public string Line2 { get; set; } = "";
    public Color Line2Color { get; set; } = Skin.Green;

    public LcdDisplay()
    {
        timer.Tick += (_, _) => Animate();
        timer.Start();
    }

    /// <summary>Makes the spectrum analyzer go wild for a couple of seconds.</summary>
    public void Party() => partyFrames = 75;

    void Animate()
    {
        scroll += 1.3f * Skin.Scale(this);
        for (var i = 0; i < BarCount; i++)
        {
            if (random.NextDouble() < (partyFrames > 0 ? 0.5 : 0.15))
            {
                var r = random.NextSingle();
                targets[i] = partyFrames > 0 ? 0.55f + 0.45f * r : 0.12f + 0.6f * r * r;
            }
            bars[i] += (targets[i] - bars[i]) * 0.35f;
            peaks[i] = Math.Max(peaks[i] - 0.012f, bars[i]);
        }
        if (partyFrames > 0) partyFrames--;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Skin.Lcd);
        int pad = S(6), spectrumWidth = S(118);

        // Marquee
        var marqueeRect = new Rectangle(pad, pad, Width - spectrumWidth - 3 * pad, Skin.LcdLarge.Height + S(2));
        if (Marquee.Length > 0)
        {
            var textWidth = TextRenderer.MeasureText(g, Marquee, Skin.LcdLarge, Size.Empty, TextFormatFlags.NoPadding).Width;
            var x = marqueeRect.X - (int)(scroll % textWidth);
            g.SetClip(marqueeRect);
            for (var copy = x; copy < marqueeRect.Right; copy += textWidth)
            {
                TextRenderer.DrawText(g, Marquee, Skin.LcdLarge, new Point(copy + 1, marqueeRect.Y + 1), Skin.GreenGlow, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Marquee, Skin.LcdLarge, new Point(copy, marqueeRect.Y), Skin.Green, TextFormatFlags.NoPadding);
            }
            g.ResetClip();
        }

        // Status lines
        var lineY = marqueeRect.Bottom + S(10);
        var lineWidth = marqueeRect.Width;
        const TextFormatFlags lineFlags = TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
        TextRenderer.DrawText(g, Line1, Skin.LcdSmall, new Rectangle(pad, lineY, lineWidth, Skin.LcdSmall.Height), Skin.Green, lineFlags);
        TextRenderer.DrawText(g, Line2, Skin.LcdSmall, new Rectangle(pad, lineY + Skin.LcdSmall.Height + S(4), lineWidth, Skin.LcdSmall.Height), Line2Color, lineFlags);

        // Spectrum analyzer
        int gap = Math.Max(1, S(2)), segment = Math.Max(2, S(2)), segmentGap = Math.Max(1, S(1));
        var area = new Rectangle(Width - spectrumWidth - pad, pad, spectrumWidth, Height - 2 * pad);
        var barWidth = (area.Width - (BarCount - 1) * gap) / BarCount;
        var segments = area.Height / (segment + segmentGap);
        using var green = new SolidBrush(Skin.Green);
        using var yellow = new SolidBrush(Skin.Yellow);
        using var red = new SolidBrush(Skin.Red);
        using var peak = new SolidBrush(Color.FromArgb(0xC8, 0xD0, 0xE8));
        for (var i = 0; i < BarCount; i++)
        {
            var x = area.X + i * (barWidth + gap);
            var lit = (int)(bars[i] * segments);
            for (var k = 0; k < lit; k++)
            {
                var brush = k > segments * 0.8 ? red : k > segments * 0.55 ? yellow : green;
                g.FillRectangle(brush, x, area.Bottom - (k + 1) * (segment + segmentGap), barWidth, segment);
            }
            var peakRow = Math.Min(segments - 1, (int)(peaks[i] * segments));
            g.FillRectangle(peak, x, area.Bottom - (peakRow + 1) * (segment + segmentGap), barWidth, segment);
        }

        Skin.Scanlines(g, ClientRectangle);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>A static LCD panel for messages and instructions.</summary>
sealed class LcdReadout : SkinControl
{
    public Color TextColor { get; set; } = Skin.Green;
    public bool PathMode { get; set; }

    public LcdReadout() => Font = Skin.LcdSmall;

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Skin.Lcd);
        var r = Rectangle.Inflate(ClientRectangle, -S(6), -S(4));
        var flags = PathMode
            ? TextFormatFlags.SingleLine | TextFormatFlags.PathEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
            : TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
        TextRenderer.DrawText(g, Text, Font, r, TextColor, flags);
        Skin.Scanlines(g, ClientRectangle);
    }
}

/// <summary>The device list, drawn like a music player's playlist. Click a row to switch it on or off.</summary>
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
        Font = Skin.LcdSmall;
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    int RowHeight => Font.Height + S(5);
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
        g.Clear(Color.Black);

        if (rows.Count == 0)
        {
            TextRenderer.DrawText(g, EmptyText, Font, ClientRectangle, Skin.GreenDim,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            return;
        }

        int pad = S(6), box = S(8), rowHeight = RowHeight;
        var scrollbar = rows.Count > VisibleRows ? S(5) : 0;
        const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;

        for (var i = top; i < Math.Min(rows.Count, top + VisibleRows + 1); i++)
        {
            var row = rows[i];
            var rect = new Rectangle(0, (i - top) * rowHeight, Width - scrollbar, rowHeight);
            if (i == current && Focused)
                using (var sel = new SolidBrush(Skin.Selection)) g.FillRectangle(sel, rect);

            var color = row.On ? Skin.Green : Skin.GreenDim;
            var textColor = i == current && Focused ? Color.White : color;

            // Checkbox
            var boxRect = new Rectangle(pad, rect.Y + (rowHeight - box) / 2, box, box);
            using (var pen = new Pen(color)) g.DrawRectangle(pen, boxRect);
            if (row.On) using (var fill = new SolidBrush(color)) g.FillRectangle(fill, Rectangle.Inflate(boxRect, -S(2), -S(2)));

            // "3. Logi K250 ........ KEYBOARD  LE", like track name and length
            var right = $"{row.Device.Kind.ToUpperInvariant()}  {row.Device.TransportLabel.ToUpperInvariant()}";
            var rightWidth = TextRenderer.MeasureText(g, right, Font, Size.Empty, flags).Width;
            var rightRect = new Rectangle(rect.Right - rightWidth - pad, rect.Y, rightWidth, rowHeight);
            var nameLeft = boxRect.Right + pad;
            var nameRect = new Rectangle(nameLeft, rect.Y, rightRect.Left - nameLeft - pad, rowHeight);
            TextRenderer.DrawText(g, $"{i + 1}. {row.Device.Name ?? "Unknown device"}", Font, nameRect, textColor, flags | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, right, Font, rightRect, textColor, flags);
        }

        if (scrollbar > 0)
        {
            var track = new Rectangle(Width - scrollbar, 0, scrollbar, Height);
            using (var trackBrush = new SolidBrush(Skin.GreenGlow)) g.FillRectangle(trackBrush, track);
            var thumbHeight = Math.Max(S(12), Height * VisibleRows / rows.Count);
            var thumbY = (Height - thumbHeight) * top / Math.Max(1, rows.Count - VisibleRows);
            using var thumb = new SolidBrush(Skin.GreenDim);
            g.FillRectangle(thumb, track.X + 1, thumbY, track.Width - 2, thumbHeight);
        }
    }
}
