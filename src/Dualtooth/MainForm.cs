using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Text;
using Dualtooth.Core;

namespace Dualtooth;

/// <summary>
/// The one and only Dualtooth window. Laid out as four numbered steps, each saying what it's for,
/// with a "What's next" panel that always tells you what to do.
/// </summary>
sealed class MainForm : Form
{
    const string Tagline = "DUALTOOTH v0.1  //  ONE KEYBOARD, TWO OPERATING SYSTEMS  //  NOW WITH 100% FEWER BORROWED KEYBOARDS  //  PAIR ONCE, BOOT ANYWHERE  //  ";
    const int TitleBarHeight = 24;

    const string AboutText =
        "Dualtooth makes your Bluetooth devices work in both Windows and Linux on a dual-boot PC.\n\n" +
        "Why it's needed: each device remembers only one pairing per computer, but Windows and Linux each " +
        "make their own. Whichever system paired last wins, and the other one stops working.\n\n" +
        "What it does: Dualtooth reads the pairings Windows already has (it only reads; nothing on Windows " +
        "changes) and writes a small script. Run that script once in Linux, and both systems share the same " +
        "pairing, so your devices just work in either one.";

    readonly LcdDisplay lcd = new();
    readonly DevicePlaylist playlist = new() { EmptyText = "Reading your Bluetooth pairings..." };
    readonly SkinButton allButton = new() { Text = "ALL" };
    readonly SkinButton noneButton = new() { Text = "NONE" };
    readonly SkinButton radioButton = new() { Text = "OTHER RADIO ▸", Visible = false };
    readonly LcdReadout pathReadout = new() { PathMode = true, Cursor = Cursors.Hand };
    readonly SkinButton browseButton = new() { Text = "CHANGE" };
    readonly SkinButton createButton = new() { Text = "⇄   CREATE LINUX SCRIPT", Accent = true, Font = Skin.ButtonLarge, Enabled = false };
    readonly LcdReadout nextSteps = new() { Font = Skin.Guide };
    readonly SkinButton showFileButton = new() { Text = "SHOW FILE", Visible = false };
    readonly SkinButton helpButton = new() { Text = "?" };
    readonly SkinButton minimizeButton = new() { Text = "–" };
    readonly SkinButton closeButton = new() { Text = "×" };
    readonly ToolTip tips = new() { InitialDelay = 300 };

    readonly float scale;
    readonly bool demo;
    List<AdapterKeys> adapters = [];
    int adapterIndex;
    BtAddress? currentAdapter;
    bool scriptCreated;
    string savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dualtooth-apply.sh");

    // Step headings: number, title, one-line "what this is for", and their y position.
    static readonly (int Number, string Title, string Hint, int Y)[] Steps =
    [
        (1, "CHOOSE YOUR DEVICES", "Lit up = will work in Linux too. Click a device to switch it on or off.", 126),
        (2, "WHERE TO SAVE THE LINUX SCRIPT", "Anywhere Linux can open. Your Desktop is fine.", 374),
        (3, "CREATE THE SCRIPT", "Writes one small file. Nothing on this PC changes.", 446),
        (4, "WHAT'S NEXT", "", 536),
    ];

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
        ClientSize = new Size(S(470), S(684));

        Place(helpButton, 396, 5, 20, 15);
        Place(minimizeButton, 420, 5, 20, 15);
        Place(closeButton, 444, 5, 20, 15);
        Place(lcd, 14, 34, 442, 80);
        Place(radioButton, 238, 128, 100, 18);
        Place(allButton, 344, 128, 54, 18);
        Place(noneButton, 402, 128, 54, 18);
        Place(playlist, 14, 166, 442, 196);
        Place(pathReadout, 14, 412, 372, 24);
        Place(browseButton, 394, 412, 62, 24);
        Place(createButton, 14, 486, 442, 40);
        Place(showFileButton, 376, 538, 80, 18);
        Place(nextSteps, 14, 562, 442, 96);

        tips.SetToolTip(helpButton, "What is Dualtooth?");
        tips.SetToolTip(allButton, "Switch on every device");
        tips.SetToolTip(noneButton, "Switch off every device");
        tips.SetToolTip(radioButton, "Your PC has pairings saved for more than one Bluetooth radio (for example an old USB dongle). Click to switch.");
        tips.SetToolTip(browseButton, "Choose a different place to save the script");
        tips.SetToolTip(pathReadout, "Click to choose a different place to save the script");
        tips.SetToolTip(createButton, "Write the Linux script for the devices that are lit up");
        tips.SetToolTip(showFileButton, "Open the folder with the script in File Explorer");

        helpButton.Click += (_, _) => MessageBox.Show(this, AboutText, "What is Dualtooth?", MessageBoxButtons.OK, MessageBoxIcon.Information);
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();
        allButton.Click += (_, _) => playlist.SetAll(true);
        noneButton.Click += (_, _) => playlist.SetAll(false);
        radioButton.Click += (_, _) => { adapterIndex = (adapterIndex + 1) % adapters.Count; ShowAdapter(); };
        playlist.SelectionChanged += (_, _) => { scriptCreated = false; UpdateStatus(); };
        browseButton.Click += (_, _) => BrowseForPath();
        pathReadout.Click += (_, _) => BrowseForPath();
        createButton.Click += (_, _) => CreateScript();
        showFileButton.Click += (_, _) => Process.Start("explorer.exe", $"/select,\"{savePath}\"");
        Load += async (_, _) => await LoadPairingsAsync();

        lcd.Marquee = Tagline;
        lcd.Line1 = "LOOKING FOR YOUR BLUETOOTH...";
        pathReadout.Text = savePath;
        nextSteps.Text = "Reading the Bluetooth pairings Windows has saved...";
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
            ShowError("COULDN'T READ YOUR PAIRINGS", $"Dualtooth couldn't read the Bluetooth pairings Windows has saved.\n\nDetails: {ex.Message}");
            playlist.EmptyText = "No devices to show.";
            playlist.Invalidate();
            return;
        }

        if (adapters.Count == 0)
        {
            lcd.Line1 = "NO PAIRED DEVICES FOUND";
            playlist.EmptyText = "Nothing is paired with this PC yet.";
            playlist.Invalidate();
            nextSteps.Text = "Pair your Bluetooth devices in Windows first (Settings › Bluetooth & devices), then open Dualtooth again.";
            return;
        }

        // Start on the radio this PC is actually using.
        adapters = adapters.OrderByDescending(a => a.Address == currentAdapter).ToList();
        adapterIndex = 0;
        radioButton.Visible = adapters.Count > 1;
        ShowAdapter();
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
        var inUse = adapter.Address == currentAdapter;
        lcd.Line1 = (adapters.Count, inUse) switch
        {
            (1, _) => "BLUETOOTH RADIO FOUND",
            (_, true) => $"RADIO {adapterIndex + 1} OF {adapters.Count}: THE ONE IN USE",
            _ => $"RADIO {adapterIndex + 1} OF {adapters.Count}: NOT CONNECTED NOW",
        };
        tips.SetToolTip(lcd, $"Bluetooth radio address: {adapter.Address}");
        playlist.EmptyText = "No devices are paired with this radio.";
        playlist.SetDevices(adapter.Devices);
        playlist.Focus();
    }

    void UpdateStatus()
    {
        var on = playlist.OnCount;
        createButton.Enabled = on > 0;
        showFileButton.Visible = scriptCreated;
        lcd.Line2Color = on > 0 ? Skin.Glow : Skin.GlowDim;
        lcd.Line2 = demo
            ? "DEMO MODE: THESE DEVICES ARE MADE UP"
            : $"{on} OF {playlist.Count} DEVICE{(playlist.Count == 1 ? "" : "S")} WILL WORK IN LINUX";

        if (scriptCreated) return;
        nextSteps.TextColor = Skin.Glow;
        nextSteps.Text = on > 0
            ? "Pick the devices you want in step 1, then press CREATE LINUX SCRIPT.\n\n" +
              "Devices that already work in Windows are the ones Dualtooth can copy."
            : "Switch on at least one device in step 1 to continue.";
    }

    void BrowseForPath()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Where should Dualtooth save the Linux script?",
            FileName = Path.GetFileName(savePath),
            InitialDirectory = Path.GetDirectoryName(savePath),
            Filter = "Shell script (*.sh)|*.sh|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        savePath = dialog.FileName;
        pathReadout.Text = savePath;
    }

    void CreateScript()
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
            ShowError("COULDN'T SAVE THE SCRIPT", $"The script couldn't be saved there. Try a different place in step 2.\n\nDetails: {ex.Message}");
            return;
        }

        var fileName = Path.GetFileName(savePath);
        scriptCreated = true;
        UpdateStatus();
        lcd.Party();
        lcd.Line2Color = Skin.Accent;
        lcd.Line2 = $"SCRIPT CREATED FOR {selected.Count} DEVICE{(selected.Count == 1 ? "" : "S")}!";
        nextSteps.TextColor = Skin.Glow;
        nextSteps.Text =
            "1. Restart your PC into Linux.\n" +
            $"2. Find {fileName} (it's on your Windows drive), right-click its folder › Open in Terminal.\n" +
            $"3. Type  sudo bash {fileName}  and press Enter.\n" +
            "4. Turn your devices off and on. Done!\n" +
            "Afterwards, delete the script. It contains your pairing keys.";
    }

    void ShowError(string headline, string detail)
    {
        lcd.Line2Color = Skin.Red;
        lcd.Line2 = headline;
        nextSteps.TextColor = Skin.Red;
        nextSteps.Text = detail;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        var outer = ClientRectangle;

        // Body: a soft warm glow from the top
        using (var body = new LinearGradientBrush(outer, ControlPaint.Light(Skin.Body, 0.18f), Skin.Body, LinearGradientMode.Vertical))
            g.FillRectangle(body, outer);
        using (var edge = new Pen(Skin.BodyLight))
            g.DrawRectangle(edge, 0, 0, outer.Width - 1, outer.Height - 1);

        DrawTitleBar(g);

        foreach (var screen in new Control[] { lcd, playlist, pathReadout, nextSteps })
            Skin.Well(g, screen.Bounds, scale);

        foreach (var step in Steps)
            DrawStep(g, step.Number, step.Title, step.Hint, step.Y);

        TextRenderer.DrawText(g, "v0.1  ·  MIT  ·  github.com/edotwedo/Dualtooth", Skin.Label, new Point(S(14), S(666)), Skin.GlowDim);
    }

    void DrawStep(Graphics g, int number, string title, string hint, int y)
    {
        // Numbered bulb
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var circle = new RectangleF(S(14), S(y), S(17), S(17));
        using (var halo = new SolidBrush(Color.FromArgb(40, Skin.Glow)))
            g.FillEllipse(halo, RectangleF.Inflate(circle, S(2), S(2)));
        using (var ring = new Pen(Skin.Glow, Math.Max(1, 1.5f * scale)))
            g.DrawEllipse(ring, circle);
        g.SmoothingMode = SmoothingMode.None;
        TextRenderer.DrawText(g, number.ToString(), Skin.Label, Rectangle.Round(circle), Skin.Glow,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        TextRenderer.DrawText(g, title, Skin.Label, new Point(S(38), S(y) + S(1)), Skin.BrightText);
        if (hint.Length > 0)
            TextRenderer.DrawText(g, hint, Skin.Hint, new Point(S(38), S(y + 18)), Skin.LabelText);
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
        TextRenderer.DrawText(g, "dualtooth", Skin.Title, new Point(titleX, titleY), Skin.BrightText);
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing) tips.Dispose();
        base.Dispose(disposing);
    }

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}
