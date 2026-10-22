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

    readonly LcdDisplay lcd = new();
    readonly DevicePlaylist playlist = new() { EmptyText = "Reading your Bluetooth pairings..." };
    readonly SkinButton xrayButton = new() { Text = "🔍 X-RAY", Accent = true };
    readonly SkinButton allButton = new() { Text = "ALL" };
    readonly SkinButton noneButton = new() { Text = "NONE" };
    readonly SkinButton radioButton = new() { Text = "OTHER RADIO ▸", Visible = false };
    readonly LcdReadout pathReadout = new() { PathMode = true, Cursor = Cursors.Hand };
    readonly SkinButton usbButton = new() { Text = "🔌 USB", Accent = true };
    readonly SkinButton browseButton = new() { Text = "CHANGE" };
    readonly SkinButton createButton = new() { Text = "⇄   CREATE LINUX SCRIPT", Accent = true, Font = Skin.ButtonLarge, Enabled = false };
    readonly LcdReadout nextSteps = new() { Font = Skin.Guide };
    readonly SkinButton copyCmdButton = new() { Text = "📋 COPY 1-LINER", Accent = true };
    readonly SkinButton showFileButton = new() { Text = "SHOW FILE", Visible = false };
    readonly SkinButton langButton = new() { Text = "EN" };
    readonly SkinButton skinButton = new() { Text = "SKIN" };
    readonly SkinButton supportButton = new() { Text = "♥ SUPPORT", Accent = true };
    readonly SkinButton helpButton = new() { Text = "?" };
    readonly SkinButton guideButton = new()
    {
        Text = "📖   NEW HERE?  WHAT DUALTOOTH DOES + STEP-BY-STEP GUIDE",
        Accent = true,
    };
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
    static readonly (string Code, string Subtitle, string GuideBtn, string CreateBtn, (string T1, string H1, string T2, string H2, string T3, string H3, string T4) Steps)[] Locales =
    [
        ("EN", "Lets your Bluetooth devices work in both Windows and Linux.", "📖   NEW HERE?  WHAT DUALTOOTH DOES + STEP-BY-STEP GUIDE", "⇄   CREATE LINUX SCRIPT",
            ("CHOOSE YOUR DEVICES", "Lit up = will work in Linux too. Click a device to switch it on or off.", "WHERE TO SAVE THE LINUX SCRIPT", "Anywhere Linux can open. Your Desktop is fine.", "CREATE THE SCRIPT", "Writes one small file. Nothing on this PC changes.", "WHAT'S NEXT")),
        ("ES", "Haz que tus dispositivos Bluetooth funcionen en Windows y Linux.", "📖   ¿NUEVO AQUÍ?  QUÉ HACE DUALTOOTH + GUÍA PASO A PASO", "⇄   CREAR SCRIPT DE LINUX",
            ("ELIGE TUS DISPOSITIVOS", "Encendido = funcionará en Linux. Haz clic para activar o desactivar.", "DÓNDE GUARDAR EL SCRIPT", "Cualquier carpeta accesible desde Linux (tu Escritorio sirve).", "CREAR EL SCRIPT", "Genera un archivo pequeño. No modifica nada en Windows.", "QUÉ SIGUE")),
        ("DE", "Nutze deine Bluetooth-Geräte unter Windows und Linux ohne Neu-Koppeln.", "📖   NEU HIER?  WAS DUALTOOTH MACHT + SCHRITT-FÜR-SCHRITT-ANLEITUNG", "⇄   LINUX-SKRIPT ERSTELLEN",
            ("GERÄTE AUSWÄHLEN", "Leuchtet = funktioniert auch in Linux. Anklicken zum Umschalten.", "SPEICHERORT FÜR DAS SKRIPT", "Überall, worauf Linux zugreifen kann (z. B. Desktop).", "SKRIPT ERSTELLEN", "Schreibt eine kleine Datei. An Windows wird nichts geändert.", "NÄCHSTE SCHRITTE")),
        ("PT", "Use seus dispositivos Bluetooth no Windows e no Linux sem precisar parear de novo.", "📖   NOVO AQUI?  O QUE O DUALTOOTH FAZ + GUIA PASSO A PASSO", "⇄   CRIAR SCRIPT LINUX",
            ("ESCOLHA SEUS DISPOSITIVOS", "Aceso = funcionará no Linux também. Clique para alternar.", "ONDE SALVAR O SCRIPT", "Qualquer pasta que o Linux consiga abrir (Área de Trabalho serve).", "CRIAR O SCRIPT", "Gera um pequeno arquivo. Nada muda no seu Windows.", "PRÓXIMOS PASSOS")),
        ("FR", "Utilisez vos appareils Bluetooth sous Windows et Linux sans ré-appairage.", "📖   NOUVEAU ?  CE QUE FAIT DUALTOOTH + GUIDE ÉTAPE PAR ÉTAPE", "⇄   CRÉER LE SCRIPT LINUX",
            ("CHOISISSEZ VOS APPAREILS", "Allumé = fonctionnera aussi sous Linux. Cliquez pour basculer.", "OÙ ENREGISTRER LE SCRIPT", "N'importe quel dossier accessible depuis Linux (ex: Bureau).", "CRÉER LE SCRIPT", "Écrit un petit fichier. Rien n'est modifié sous Windows.", "ET ENSUITE")),
        ("JA", "WindowsとLinuxのデュアルブートでBluetooth機器を再ペアリングなしで共有します。", "📖   初めての方へ：DUALTOOTHの仕組みと使い方ガイド", "⇄   LINUXスクリプトを作成",
            ("デバイスを選択", "点灯＝Linuxでも使用可能。クリックでON/OFFを切り替えます。", "スクリプトの保存先", "Linuxから開ける場所ならどこでもOK（デスクトップ推奨）。", "スクリプトを作成", "小さなファイルを1つ作成します。Windows側は一切変更されません。", "次のステップ")),
        ("ZH", "让您的蓝牙设备在 Windows 和 Linux 双系统间免重新配对无缝切换。", "📖   新手指南：DUALTOOTH 工作原理与分步教程", "⇄   生成 LINUX 同步脚本",
            ("选择蓝牙设备", "亮起 = 同步到 Linux。点击设备可切换开关。", "脚本保存位置", "Linux 可访问的任意位置（例如桌面）。", "创建同步脚本", "仅生成一个小脚本文件，不修改 Windows 系统。", "下一步操作")),
    ];
    int localeIndex;

    static readonly (int Number, string Title, string Hint, int Y)[] Steps =
    [
        (1, "CHOOSE YOUR DEVICES", "Lit up = will work in Linux too. Click a device to switch it on or off.", 186),
        (2, "WHERE TO SAVE THE LINUX SCRIPT", "Anywhere Linux can open. Your Desktop is fine.", 386),
        (3, "CREATE THE SCRIPT", "Writes one small file. Nothing on this PC changes.", 458),
        (4, "WHAT'S NEXT", "", 548),
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
        ClientSize = new Size(S(470), S(680));

        Place(langButton, 310, 5, 30, 15);
        Place(skinButton, 344, 5, 48, 15);
        Place(helpButton, 396, 5, 20, 15);
        Place(minimizeButton, 420, 5, 20, 15);
        Place(closeButton, 444, 5, 20, 15);
        Place(lcd, 14, 34, 442, 80);
        Place(guideButton, 14, 142, 442, 34);
        Place(radioButton, 180, 188, 92, 18);
        Place(xrayButton, 276, 188, 64, 18);
        Place(allButton, 344, 188, 54, 18);
        Place(noneButton, 402, 188, 54, 18);
        Place(playlist, 14, 226, 442, 150);
        Place(pathReadout, 14, 424, 318, 24);
        Place(usbButton, 336, 424, 54, 24);
        Place(browseButton, 394, 424, 62, 24);
        Place(createButton, 14, 498, 442, 40);
        Place(copyCmdButton, 260, 550, 110, 18);
        Place(showFileButton, 376, 550, 80, 18);
        Place(nextSteps, 14, 574, 442, 80);
        Place(supportButton, 370, 658, 86, 18);

        tips.SetToolTip(langButton, "Switch language: EN / ES / DE / PT / FR / JA / ZH");
        tips.SetToolTip(skinButton, "Cycle hardware skin: Nixie Filament / Tektronix CRT / Spanda Cyber / Bazzite Vapor");
        tips.SetToolTip(supportButton, "Support Dualtooth development (Pay-what-you-want via Stripe)");
        tips.SetToolTip(helpButton, "Open the how-to guide");
        tips.SetToolTip(guideButton, "Step-by-step instructions and help if something goes wrong");
        tips.SetToolTip(xrayButton, "Inspect exact Bluetooth keys (Classic LinkKey + BLE LTK + IRK) & see where Windows stores them.");
        tips.SetToolTip(usbButton, "Save directly to a USB thumb drive (bypasses Windows BitLocker when booting into Linux!).");
        tips.SetToolTip(allButton, "Switch on every device");
        tips.SetToolTip(noneButton, "Switch off every device");
        tips.SetToolTip(radioButton, "Your PC has pairings saved for more than one Bluetooth radio (for example an old USB dongle). Click to switch.");
        tips.SetToolTip(browseButton, "Choose a different place to save the script");
        tips.SetToolTip(pathReadout, "Click to choose a different place to save the script");
        tips.SetToolTip(createButton, "Write the Linux script for the devices that are lit up");
        tips.SetToolTip(showFileButton, "Open the folder with the script in File Explorer");

        langButton.Click += (_, _) => CycleLanguage();
        skinButton.Click += (_, _) => CycleSkin();
        supportButton.Click += (_, _) => Process.Start(new ProcessStartInfo("https://buy.stripe.com/9B63cx3OWb9p6iPgFP9AA01?__prefilled_amount=900") { UseShellExecute = true });
        helpButton.Click += (_, _) => OpenGuide();
        guideButton.Click += (_, _) => OpenGuide();
        minimizeButton.Click += (_, _) => WindowState = FormWindowState.Minimized;
        closeButton.Click += (_, _) => Close();
        allButton.Click += (_, _) => playlist.SetAll(true);
        noneButton.Click += (_, _) => playlist.SetAll(false);
        xrayButton.Click += (_, _) => ShowKeyXray();
        usbButton.Click += (_, _) => SelectUsbDrive();
        radioButton.Click += (_, _) => { adapterIndex = (adapterIndex + 1) % adapters.Count; ShowAdapter(); };
        playlist.SelectionChanged += (_, _) => { scriptCreated = false; UpdateStatus(); };
        browseButton.Click += (_, _) => BrowseForPath();
        pathReadout.Click += (_, _) => BrowseForPath();
        createButton.Click += (_, _) => CreateScript();
        copyCmdButton.Click += (_, _) =>
        {
            var fileName = Path.GetFileName(savePath);
            Clipboard.SetText($"sudo bash \"./{fileName}\"");
            lcd.Party();
            lcd.Line2Color = Skin.Accent;
            lcd.Line2 = $"COPIED -> sudo bash \"./{fileName}\"";
        };
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
            ? "Pick the devices you want in step 1, then press CREATE LINUX SCRIPT.\n" +
              "Not sure what to do? Click the big button at the top for the full guide."
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

    void SelectUsbDrive()
    {
        var usb = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Removable);
        if (usb is not null)
        {
            savePath = Path.Combine(usb.RootDirectory.FullName, "dualtooth-apply.sh");
            pathReadout.Text = savePath;
            lcd.Party();
            lcd.Line2Color = Skin.Accent;
            lcd.Line2 = $"USB STICK LOCKED -> {savePath}";
            nextSteps.TextColor = Skin.Glow;
            nextSteps.Text = $"USB Drive ({usb.Name}) selected! Saving to a USB stick bypasses Windows 11 BitLocker so Linux can open the script immediately.";
        }
        else
        {
            lcd.Party();
            lcd.Line2Color = Skin.Accent;
            lcd.Line2 = "NO USB STICK DETECTED -> PICK ANY SHARED DRIVE";
            BrowseForPath();
        }
    }

    void ShowKeyXray()
    {
        if (adapters.Count == 0) return;
        var adapter = adapters[adapterIndex];
        var bleCount = adapter.Devices.Count(d => d.Le is not null);
        var irkCount = adapter.Devices.Count(d => d.Le?.Irk is not null);
        var classicCount = adapter.Devices.Count(d => d.LinkKey is not null);

        lcd.Party();
        lcd.Line1 = $"X-RAY // ADAPTER {adapter.Address}";
        lcd.Line2Color = Skin.Accent;
        lcd.Line2 = $"CLASSIC:{classicCount}  ·  BLE LTK:{bleCount}  ·  IRK PRIVACY:{irkCount}";

        nextSteps.TextColor = Skin.Glow;
        nextSteps.Text =
            $"📥 READS FROM: HKLM\\SYSTEM\\CurrentControlSet\\Services\\BTHPORT\\Parameters\\Keys\\{adapter.Address.Value:x12}\n" +
            $"📤 WRITES TO:  Linux /var/lib/bluetooth/{adapter.Address}/<device>/info (+ .bak rollback)\n" +
            $"⚡ VS bt-dualboot: Exports BLE 5.0 LTK ({bleCount}) + IRK ({irkCount}) + bypasses BitLocker!";
    }

    void CreateScript()
    {
        var adapter = adapters[adapterIndex];
        var selected = playlist.SelectedDevices.ToList();

        try
        {
            var script = ScriptGenerator.Generate(adapter.Address, selected, DateTime.Now);
            File.WriteAllText(savePath, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            // Also drop a double-clickable Linux .desktop launcher right next to the script!
            var desktopLauncherPath = Path.Combine(Path.GetDirectoryName(savePath) ?? "", "Dualtooth-Linux-Install.desktop");
            var scriptName = Path.GetFileName(savePath);
            var desktopFile =
                "[Desktop Entry]\n" +
                "Type=Application\n" +
                "Terminal=true\n" +
                "Name=Install Dualtooth Bluetooth Keys\n" +
                "Comment=Syncs Windows Bluetooth Classic + BLE + IRK keys into Linux BlueZ\n" +
                $"Exec=sh -c 'sudo bash \"$(dirname \"%k\")/{scriptName}\"; echo; read -p \"Press Enter to close...\"'\n" +
                "Icon=preferences-system-bluetooth\n";
            File.WriteAllText(desktopLauncherPath, desktopFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
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

    /// <summary>Unpacks the guide built into the exe and opens it in the default browser, so it works offline.</summary>
    void OpenGuide()
    {
        try
        {
            using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("Dualtooth.guide.html")!;
            var path = Path.Combine(Path.GetTempPath(), "Dualtooth", "guide.html");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var file = File.Create(path)) stream.CopyTo(file);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError("COULDN'T OPEN THE GUIDE", $"The guide is also online at github.com/edotwedo/Dualtooth.\n\nDetails: {ex.Message}");
        }
    }

    void CycleLanguage()
    {
        localeIndex = (localeIndex + 1) % Locales.Length;
        var loc = Locales[localeIndex];
        langButton.Text = loc.Code;
        guideButton.Text = loc.GuideBtn;
        createButton.Text = loc.CreateBtn;
        lcd.Party();
        lcd.Line2Color = Skin.Accent;
        lcd.Line2 = $"LANGUAGE: {loc.Code}";
        Invalidate(true);
    }

    void CycleSkin()
    {
        var paletteName = Skin.CyclePalette();
        BackColor = Skin.Body;
        lcd.BackColor = Skin.Lcd;
        playlist.BackColor = Skin.Lcd;
        pathReadout.BackColor = Skin.Lcd;
        nextSteps.BackColor = Skin.Lcd;
        lcd.Party();
        UpdateStatus();
        lcd.Line2Color = Skin.Accent;
        lcd.Line2 = $"HARDWARE SKIN: {paletteName}";
        Invalidate(true);
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

        var loc = Locales[localeIndex];
        // What this app is, in one sentence, right under the screen.
        TextRenderer.DrawText(g, loc.Subtitle,
            Skin.Hint, new Rectangle(S(14), S(120), S(442), S(18)), Skin.BrightText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);

        DrawStep(g, 1, loc.Steps.T1, loc.Steps.H1, 186);
        DrawStep(g, 2, loc.Steps.T2, loc.Steps.H2, 386);
        DrawStep(g, 3, loc.Steps.T3, loc.Steps.H3, 458);
        DrawStep(g, 4, loc.Steps.T4, "", 548);

        TextRenderer.DrawText(g, "v0.1  ·  MIT  ·  github.com/edotwedo/Dualtooth", Skin.Label, new Point(S(14), S(661)), Skin.GlowDim);
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
