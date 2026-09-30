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

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(demo: args.Contains("--demo")));
    }
}