using System.Diagnostics;
using System.Text;
using Dualtooth.Core;

namespace Dualtooth;

sealed class MainForm : Form
{
    readonly ComboBox adapterBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    readonly ListView deviceList = new()
    {
        View = View.Details, CheckBoxes = true, FullRowSelect = true, Dock = DockStyle.Fill,
        HeaderStyle = ColumnHeaderStyle.Nonclickable,
    };
    readonly TextBox pathBox = new() { Dock = DockStyle.Fill };
    readonly Button browseButton = new() { Text = "Browse…", AutoSize = true };
    readonly Button generateButton = new() { Text = "Generate Linux script", AutoSize = true, Padding = new Padding(12, 4, 12, 4), Enabled = false };
    readonly Label statusLabel = new() { AutoSize = true, Dock = DockStyle.Fill, Text = "Reading Bluetooth pairings…" };
    readonly LinkLabel showFileLink = new() { Text = "Show the script in its folder", AutoSize = true, Visible = false };

    List<AdapterKeys> adapters = [];
    BtAddress? currentAdapter;

    public MainForm()
    {
        Text = "Dualtooth";
        Font = new Font("Segoe UI", 10f);
        ClientSize = new Size(700, 560);
        MinimumSize = new Size(560, 460);
        StartPosition = FormStartPosition.CenterScreen;

        deviceList.Columns.Add("Device", 230);
        deviceList.Columns.Add("Type", 90);
        deviceList.Columns.Add("Connection", 110);
        deviceList.Columns.Add("Address", 150);

        pathBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dualtooth-apply.sh");

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label { Text = "Dualtooth", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f) };
        var subtitle = new Label
        {
            AutoSize = true, ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 12),
            Text = "Use the same Bluetooth devices in Windows and Linux without pairing them twice.",
        };
        layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 3);
        layout.Controls.Add(subtitle, 0, 1); layout.SetColumnSpan(subtitle, 3);

        layout.Controls.Add(new Label { Text = "Bluetooth adapter:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        layout.Controls.Add(adapterBox, 1, 2); layout.SetColumnSpan(adapterBox, 2);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(deviceList, 0, 3); layout.SetColumnSpan(deviceList, 3);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Save script to:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 4);
        layout.Controls.Add(pathBox, 1, 4);
        layout.Controls.Add(browseButton, 2, 4);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(generateButton, 0, 5); layout.SetColumnSpan(generateButton, 3);
        generateButton.Anchor = AnchorStyles.None;
        generateButton.Margin = new Padding(3, 12, 3, 12);

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(statusLabel, 0, 6); layout.SetColumnSpan(statusLabel, 3);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(showFileLink, 0, 7); layout.SetColumnSpan(showFileLink, 3);

        Controls.Add(layout);

        adapterBox.SelectedIndexChanged += (_, _) => ShowDevices();
        deviceList.ItemChecked += (_, _) => generateButton.Enabled = deviceList.CheckedItems.Count > 0;
        browseButton.Click += (_, _) => BrowseForPath();
        generateButton.Click += (_, _) => GenerateScript();
        showFileLink.LinkClicked += (_, _) => Process.Start("explorer.exe", $"/select,\"{pathBox.Text}\"");
        Load += async (_, _) => await LoadPairingsAsync();
        Resize += (_, _) => statusLabel.MaximumSize = new Size(ClientSize.Width - 40, 0);
        statusLabel.MaximumSize = new Size(ClientSize.Width - 40, 0);
    }

    async Task LoadPairingsAsync()
    {
        try
        {
            adapters = await Task.Run(KeyReader.ReadAll);
            currentAdapter = await DeviceLookup.GetCurrentAdapterAsync();
            foreach (var device in adapters.SelectMany(a => a.Devices))
                await DeviceLookup.DescribeAsync(device);
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Couldn't read Bluetooth pairings: " + ex.Message;
            return;
        }

        if (adapters.Count == 0)
        {
            statusLabel.Text = "No Bluetooth pairings found on this PC. Pair your devices in Windows first.";
            return;
        }

        // List the adapter this PC is using first.
        adapters = adapters.OrderByDescending(a => a.Address == currentAdapter).ToList();
        foreach (var adapter in adapters)
            adapterBox.Items.Add(adapter.Address == currentAdapter
                ? $"{adapter.Address}  (this PC's Bluetooth)"
                : $"{adapter.Address}  (not currently connected)");
        adapterBox.SelectedIndex = 0;

        statusLabel.Text = "Tick the devices you want to use in Linux, then click Generate.";
    }

    void ShowDevices()
    {
        deviceList.BeginUpdate();
        deviceList.Items.Clear();
        foreach (var device in adapters[adapterBox.SelectedIndex].Devices)
        {
            var item = new ListViewItem(device.Name ?? "Unknown device") { Tag = device, Checked = device.Name is not null };
            item.SubItems.Add(device.Kind);
            item.SubItems.Add(device.TransportLabel);
            item.SubItems.Add(device.Address.ToString());
            deviceList.Items.Add(item);
        }
        deviceList.EndUpdate();
        generateButton.Enabled = deviceList.CheckedItems.Count > 0;
    }

    void BrowseForPath()
    {
        using var dialog = new SaveFileDialog
        {
            FileName = Path.GetFileName(pathBox.Text),
            InitialDirectory = Path.GetDirectoryName(pathBox.Text),
            Filter = "Shell script (*.sh)|*.sh|All files (*.*)|*.*",
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) pathBox.Text = dialog.FileName;
    }

    void GenerateScript()
    {
        var adapter = adapters[adapterBox.SelectedIndex];
        var selected = deviceList.CheckedItems.Cast<ListViewItem>().Select(i => (PairedDevice)i.Tag!).ToList();

        try
        {
            var script = ScriptGenerator.Generate(adapter.Address, selected, DateTime.Now);
            File.WriteAllText(pathBox.Text, script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex)
        {
            statusLabel.Text = "Couldn't save the script: " + ex.Message;
            showFileLink.Visible = false;
            return;
        }

        var fileName = Path.GetFileName(pathBox.Text);
        statusLabel.Text =
            $"Saved {selected.Count} device(s) to {fileName}.\n\n" +
            "Next: boot Linux, open the folder that has the script in a terminal, and run:\n" +
            $"    sudo bash {fileName}\n" +
            $"(Add --dry-run to preview without changing anything.)\n\n" +
            "The script contains pairing keys, so delete it once it has worked. " +
            "Don't pair these devices again in either OS, or they'll need syncing again.";
        showFileLink.Visible = true;
    }
}
