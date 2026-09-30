using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using Dualtooth.Core;

namespace Dualtooth;

/// <summary>The one and only Dualtooth window, with a custom skin and a bit of personality.</summary>
sealed class MainForm : Form
{
    const string Tagline = "DUALTOOTH v0.1  //  ONE KEYBOARD, TWO OPERATING SYSTEMS  //  NOW WITH 100% FEWER BORROWED KEYBOARDS  //  PAIR ONCE, BOOT ANYWHERE  //  ";
    const int TitleBarHeight = 24;

    readonly LcdDisplay lcd = new();
    readonly DevicePlaylist playlist = new() { EmptyText = "READING PAIRINGS..." };
    readonly SkinButton allButton = new() { Text = "ALL" };
    readonly SkinButton noneButton = new() { Text = "NONE" };
    readonly SkinButton adapterButton = new() { Text = "ADAPTER ▸", Visible = false };
    readonly LcdReadout pathReadout = new() { PathMode = true, Cursor = Cursors.Hand };
    readonly SkinButton browseButton = new() { Text = "BROWSE" };
    readonly SkinButton syncButton = new() { Text = "⇄   SYNC TO LINUX", Accent = true, Font = Skin.ButtonLarge, Enabled = false };
    readonly LcdReadout readout = new() { Text = "WAKING UP THE RADIO..." };
    readonly SkinButton showFileButton = new() { Text = "SHOW FILE", Enabled = false };
    readonly SkinButton minimizeButton = new() { Text = "–" };
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

        Place(minimizeButton, 420, 5, 20, 15);
        Place(closeButton, 444, 5, 20, 15);
        Place(lcd, 14, 34, 442, 94);
        Place(adapterButton, 204, 140, 88, 18);
        Place(allButton, 298, 140, 48, 18);
        Place(noneButton, 352, 140, 48, 18);
        Place(playlist, 14, 166, 442, 234);
        Place(pathReadout, 14, 428, 372, 24);
        Place(browseButton, 394, 428, 62, 24);
        Place(syncButton, 14, 464, 442, 42);
        Place(readout, 14, 516, 442, 56);
        Place(showFileButton, 376, 582, 80, 18);

        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();
        allButton.Click += (_, _) => playlist.SetAll(true);
        noneButton.Click += (_, _) => playlist.SetAll(false);
        adapterButton.Click += (_, _) => { adapterIndex = (adapterIndex + 1) % adapters.Count; ShowAdapter(); };
        playlist.SelectionChanged += (_, _) => UpdateCounts();
        browseButton.Click += (_, _) => BrowseForPath();
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
        lcd.Line2Color = Skin.Glow;
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
        lcd.Line2Color = Skin.Accent;
        lcd.Line2 = $"SYNCED {selected.Count} DEVICE{(selected.Count == 1 ? "" : "S")}!";
        readout.TextColor = Skin.Glow;
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

        // Body: a soft indigo glow from the top
        using (var body = new LinearGradientBrush(outer, ControlPaint.Light(Skin.Body, 0.18f), Skin.Body, LinearGradientMode.Vertical))
            g.FillRectangle(body, outer);
        using (var edge = new Pen(Skin.BodyLight))
            g.DrawRectangle(edge, 0, 0, outer.Width - 1, outer.Height - 1);

        DrawTitleBar(g);

        foreach (var screen in new Control[] { lcd, playlist, pathReadout, readout })
            Skin.Well(g, screen.Bounds, scale);

        DrawLabel(g, "DEVICES", 14, 143);
        DrawLabel(g, "SAVE SCRIPT TO", 14, 410);
        DrawLabel(g, "v0.1  ·  MIT  ·  github.com/edotwedo/Dualtooth", 14, 584);
    }

    void DrawTitleBar(Graphics g)
    {
        // Logo: two linked rings, one for each OS
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float ring = S(11), y = (S(TitleBarHeight) - ring) / 2f + S(2);
        using (var left = new Pen(Skin.Glow, Math.Max(1.5f, 1.8f * scale)))
        using (var right = new Pen(Skin.Accent, Math.Max(1.5f, 1.8f * scale)))
        {
            g.DrawEllipse(left, S(14), y, ring, ring);
            g.DrawEllipse(right, S(14) + ring * 0.6f, y, ring, ring);
        }
        g.SmoothingMode = SmoothingMode.None;

        var titleX = S(14) + (int)(ring * 1.6f) + S(8);
        var titleSize = TextRenderer.MeasureText(g, "dualtooth", Skin.Title);
        var titleY = S(2) + (S(TitleBarHeight) - titleSize.Height) / 2;
        TextRenderer.DrawText(g, "dualtooth", Skin.Title, new Point(titleX, titleY), Color.FromArgb(0xE4, 0xE1, 0xF7));
    }

    void DrawLabel(Graphics g, string text, int x, int y) =>
        TextRenderer.DrawText(g, text, Skin.Label, new Point(S(x), S(y)), Skin.LabelText);

    // Rounded window corners on Windows 11 (ignored elsewhere).
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(Handle, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    // Drag the window from anywhere that isn't a control.
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, 2 /* HTCAPTION */, 0);
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
