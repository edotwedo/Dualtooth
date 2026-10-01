using Dualtooth.Core;

namespace Dualtooth;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--check-irk"))
        {
            // No window: listen, then write the report next to the other Dualtooth files.
            var report = IrkCheck.RunAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dualtooth-irk-check.txt");
            File.WriteAllText(path, report);
            return;
        }

        if (args.Contains("--irk-probe"))
        {
            // No window: write a fingerprint-only Linux script that settles the IRK byte order.
            var devices = KeyReader.ReadAll().SelectMany(a => a.Devices).ToList();
            foreach (var device in devices) DeviceLookup.DescribeAsync(device).GetAwaiter().GetResult();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "dualtooth-irk-probe.sh");
            File.WriteAllText(path, IrkProbe.Generate(devices, DateTime.Now), new System.Text.UTF8Encoding(false));
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(demo: args.Contains("--demo")));
    }
}