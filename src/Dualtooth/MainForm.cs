using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using Dualtooth.Core;

namespace Dualtooth;

/// <summary>The one and only Dualtooth window, skinned in the spirit of late-90s media players.</summary>
sealed class MainForm : Form
{
    const string Tagline = "DUALTOOTH v0.1  ***  ONE KEYBOARD, TWO OPERATING SYSTEMS  ***  IT REALLY PAIRS THE PENGUIN'S ASS  ***  ";
    const int TitleBarHeight = 20;

    readonly LcdDisplay lcd = new();
    readonly DevicePlaylist playlist = new() { EmptyText = "READING PAIRINGS..." };
    readonly SkinButton allButton = new() { Text = "ALL" };
    readonly SkinButton noneButton = new() { Text = "NONE" };
    readonly SkinButton adapterButton = new() { Text = "ADAPTER ▸", Visible = false };
    readonly LcdReadout pathReadout = new() { PathMode = true, Cursor = Cursors.Hand };
    readonly SkinButton ejectButton = new() { Text = "⏏", Font = new Font("Segoe UI Symbol", 10f, FontStyle.Bold) };
    readonly SkinButton syncButton = new() { Text = "▶   SYNC TO LINUX", Accent = true, Font = Skin.ButtonLarge, Enabled = false };
    readonly LcdReadout readout = new() { Text = "WARMING UP THE TUBES..." };
    readonly SkinButton showFileButton = new() { Text = "SHOW FILE", Enabled = false };
    readonly SkinButton minimizeButton = new() { Text = "_" };
    readonly SkinButton closeButton = new() { Text = "×" };

    readonly float scale;
    readonly bool demo;
    List<AdapterKeys> adapters = [];
    int adapterIndex;
    BtAddress? currentAdapter;
    string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dualtooth-apply.sh");

    int S(float value) => (int)Math.Round(value * scale);

    /// <param name="demo">Show made-up devices instead of reading real pairings (for screenshots).</param>
    public MainForm(bool demo = false)
    {
        this.demo = demo;
        scale = DeviceDpi / 96f;
        Text = "Dualtooth";
        FormBorderStyle = FormBorderStyle.None;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Skin.Body;
        DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(S(470), S(604));

        Place(minimizeButton, 438, 5, 12, 11);
        Place(closeButton, 452, 5, 12, 11);
        Place(lcd, 12, 30, 446, 96);
        Place(allButton, 300, 134, 44, 17);
        Place(noneButton, 348, 134, 44, 17);
        Place(adapterButton, 208, 134, 88, 17);
        Place(playlist, 12, 158, 446, 244);
        Place(pathReadout, 12, 428, 404, 24);
        Place(ejectButton, 422, 428, 36, 24);
        Place(syncButton, 12, 462, 446, 42);
        Place(readout, 12, 514, 446, 58);
        Place(showFileButton, 378, 580, 80, 17);

        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();
        allButton.Click += (_, _) => playlist.SetAll(true);
        noneButton.Click += (_, _) => playlist.SetAll(false);
        adapterButton.Click += (_, _) => { adapterIndex = (adapterIndex + 1) % adapters.Count; ShowAdapter(); };
        playlist.SelectionChanged += (_, _) => UpdateCounts();
        ejectButton.Click += (_, _) => BrowseForPath();
        pathReadout.Click += (_, _) => BrowseForPath();
        syncButton.Click += (_, _) => Sync();
        showFileButton.Click += (_, _) => Process.Start("explorer.exe", $"/select,\"{savePath}\"");
        Load += async (_, _) => await LoadPairingsAsync();

        lcd.Marquee = Tagline;
        lcd.Line1 = "READING BLUETOOTH PAIRINGS...";
        pathReadout.Text = savePath;
    }

    void Place(Control control, int x, int y, int width, int height)
    {
        control.Bounds = new Rectangle(S(x), S(y), S(width), S(height));
        Controls.Add(control);
    }

    // Borderless, but still minimizable from the taskbar.
    protected override CreateParams CreateParams
    {
        get
        {
            const int WsMinimizeBox = 0x20000, WsSysMenu = 0x80000;
            var cp = base.CreateParams;
            cp.Style |= WsMinimizeBox | WsSysMenu;
            return cp;
        }
    }

    async Task LoadPairingsAsync()
    {
        if (demo)
        {
            adapters = [DemoAdapter()];
            currentAdapter = adapters[0].Address;
            ShowAdapter();
            readout.Text = "DEMO MODE: THESE DEVICES ARE MADE UP.";
            return;
        }

        try
        {
            adapters = await Task.Run(KeyReader.ReadAll);
            currentAdapter = await DeviceLookup.GetCurrentAdapterAsync();
            foreach (var device in adapters.SelectMany(a => a.Devices))
                await DeviceLookup.DescribeAsync(device);
        }
        catch (Exception ex)
        {
            ShowError("COULDN'T READ BLUETOOTH PAIRINGS", ex.Message);
            playlist.EmptyText = "NO SIGNAL";
            playlist.Invalidate();
            return;
        }

        if (adapters.Count == 0)
        {
            lcd.Line1 = "NO PAIRINGS FOUND";
            playlist.EmptyText = "NOTHING PAIRED YET.\nPAIR YOUR DEVICES IN WINDOWS, THEN REOPEN DUALTOOTH.";
            playlist.Invalidate();
            readout.Text = "PAIR YOUR DEVICES IN WINDOWS FIRST, THEN COME BACK.";
            return;
        }

        // Start on the adapter this PC is actually using.
        adapters = adapters.OrderByDescending(a => a.Address == currentAdapter).ToList();
        adapterIndex = 0;
        adapterButton.Visible = adapters.Count > 1;
        ShowAdapter();
        readout.Text = "CLICK A DEVICE TO SWITCH IT ON OR OFF, THEN HIT SYNC.";
    }

    static AdapterKeys DemoAdapter()
    {
        var key = new byte[16];
        PairedDevice Le(ulong address, string name, string kind) =>
            new(new BtAddress(address)) { Name = name, Kind = kind, Le = new LeKeys(key, 0, 0, 16, 1) };
        PairedDevice Classic(ulong address, string name, string kind) =>
            new(new BtAddress(address)) { Name = name, Kind = kind, LinkKey = key };

        return new AdapterKeys(new BtAddress(0x001A7DDA7101), [
            Le(0xC00000000001, "Clicky Keyboard 3000", "keyboard"),
            Le(0xC00000000002, "Glide Mouse", "mouse"),
            Classic(0x000000000003, "Bass Cannon Headphones", "audio"),
            Le(0xC00000000004, "Wireless Controller", "gamepad"),
            new PairedDevice(new BtAddress(0x000000000005)) { Name = "Pocket Phone", Kind = "phone", LinkKey = key, Le = new LeKeys(key, 0, 0, 16, 0) },
            Le(0xC00000000006, null!, "other"),
        ]);
    }

    void ShowAdapter()
    {
        var adapter = adapters[adapterIndex];
        lcd.Line1 = adapter.Address == currentAdapter
            ? $"ADAPTER {adapter.Address}  [ACTIVE]"
            : $"ADAPTER {adapter.Address}  [NOT CONNECTED]";
        playlist.EmptyText = "NO DEVICES ON THIS ADAPTER";
        playlist.SetDevices(adapter.Devices);
        playlist.Focus();
    }

    void UpdateCounts()
    {
        lcd.Line2Color = Skin.Green;
        lcd.Line2 = $"{playlist.OnCount} OF {playlist.Count} DEVICES READY TO SYNC";
        syncButton.Enabled = playlist.OnCount > 0;
    }

    void BrowseForPath()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(savePath),
            InitialDirectory = Path.GetDirectoryName(savePath),
            Filter = "Shell script (*.sh)|*.sh|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        savePath = dialog.FileName;
        pathReadout.Text = savePath;
    }

    void Sync()
    {
        var adapter = adapters[adapterIndex];
        var selected = playlist.SelectedDevices.ToList();

        try
        {
            var script = ScriptGenerator.Generate(adapter.Address, selected, DateTime.Now);
            File.WriteAllText(savePath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            ShowError("COULDN'T SAVE THE SCRIPT", ex.Message);
            return;
        }

        var fileName = Path.GetFileName(savePath);
        lcd.Party();
        lcd.Line2Color = Skin.Cyan;
        lcd.Line2 = $"SYNCED {selected.Count} DEVICE{(selected.Count == 1 ? "" : "S")}!";
        readout.TextColor = Skin.Green;
        readout.Text =
            $"NEXT: BOOT LINUX, OPEN A TERMINAL WHERE {fileName} IS SAVED, AND RUN:\n" +
            $"   sudo bash {fileName}\n" +
            "THEN DELETE THE SCRIPT. IT HOLDS YOUR PAIRING KEYS.";
        showFileButton.Enabled = true;
    }

    void ShowError(string headline, string detail)
    {
        lcd.Line2Color = Skin.Red;
        lcd.Line2 = headline;
        readout.TextColor = Skin.Red;
        readout.Text = detail;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var outer = ClientRectangle;

        // Body: subtle vertical sheen
        using (var body = new LinearGradientBrush(outer, ControlPaint.Light(Skin.Body, 0.12f), Skin.Body, LinearGradientMode.Vertical))
            g.FillRectangle(body, outer);
        Skin.Bevel(g, outer, raised: true, width: S(2));

        DrawTitleBar(g);

        // Sunken wells around the "screens"
        foreach (var screen in new Control[] { lcd, playlist, pathReadout, readout })
            Skin.Bevel(g, Rectangle.Inflate(screen.Bounds, S(2), S(2)), raised: false, width: S(2));

        DrawLabel(g, "DEVICES", 14, 136);
        DrawLabel(g, "SAVE SCRIPT TO", 14, 412);
        DrawLabel(g, "v0.1  ·  MIT  ·  github.com/edotwedo/Dualtooth", 14, 582);
    }

    void DrawTitleBar(Graphics g)
    {
        var bar = new Rectangle(S(3), S(3), ClientSize.Width - S(6), S(TitleBarHeight) - S(2));
        using (var brush = new LinearGradientBrush(bar, Skin.BodyLight, Skin.Body, LinearGradientMode.Vertical))
            g.FillRectangle(brush, bar);

        const string title = "D U A L T O O T H";
        var titleSize = TextRenderer.MeasureText(g, title, Skin.Label);
        var titleX = (ClientSize.Width - titleSize.Width) / 2;
        var titleY = bar.Y + (bar.Height - titleSize.Height) / 2;

        // Ribbed grooves either side of the title
        using var light = new Pen(Skin.BodyLight);
        using var dark = new Pen(Skin.BodyDark);
        var grooveEnd = minimizeButton.Left - S(8);
        for (var i = 0; i < 3; i++)
        {
            var y = bar.Y + S(5) + i * S(3);
            g.DrawLine(dark, S(10), y, titleX - S(8), y);
            g.DrawLine(light, S(10), y + 1, titleX - S(8), y + 1);
            g.DrawLine(dark, titleX + titleSize.Width + S(8), y, grooveEnd, y);
            g.DrawLine(light, titleX + titleSize.Width + S(8), y + 1, grooveEnd, y + 1);
        }
        TextRenderer.DrawText(g, title, Skin.Label, new Point(titleX + 1, titleY + 1), Skin.BodyDark);
        TextRenderer.DrawText(g, title, Skin.Label, new Point(titleX, titleY), Skin.Cyan);
    }

    void DrawLabel(Graphics g, string text, int x, int y) =>
        TextRenderer.DrawText(g, text, Skin.Label, new Point(S(x), S(y)), Skin.LabelText);

    // Drag the window from anywhere that isn't a control, like the old players.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, 0);
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
