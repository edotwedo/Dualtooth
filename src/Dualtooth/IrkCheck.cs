using System.Collections.Concurrent;
using System.Text;
using Dualtooth.Core;
using Windows.Devices.Bluetooth.Advertisement;

namespace Dualtooth;

/// <summary>
/// Works out which byte order Windows stores IRKs in, by listening for nearby devices' rotating
/// addresses and checking which interpretation of each paired device's IRK generates them.
/// Reports only names and match counts, never the keys themselves.
/// </summary>
static class IrkCheck
{
    public static async Task<string> RunAsync(TimeSpan duration)
    {
        var devices = KeyReader.ReadAll().SelectMany(a => a.Devices).Where(d => d.Le?.Irk is not null).ToList();
        foreach (var device in devices) await DeviceLookup.DescribeAsync(device);

        var seen = new ConcurrentDictionary<ulong, int>();
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, e) =>
        {
            if (Rpa.IsResolvable(e.BluetoothAddress)) seen.AddOrUpdate(e.BluetoothAddress, 1, (_, n) => n + 1);
        };
        watcher.Start();
        await Task.Delay(duration);
        watcher.Stop();

        var report = new StringBuilder();
        report.AppendLine($"Dualtooth IRK check, {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine($"Listened for {duration.TotalSeconds:0} seconds. Rotating addresses heard: {seen.Count}. Paired devices with an IRK: {devices.Count}.");
        report.AppendLine();

        int asIs = 0, reversed = 0;
        foreach (var device in devices)
        {
            var stored = device.Le!.Irk!;
            // Windows bytes are least significant first -> the spec's key is the reverse, and BlueZ can take Windows' bytes as-is.
            var matchesAsIs = seen.Keys.Count(a => Rpa.Resolves(stored.Reverse().ToArray(), a));
            // Windows bytes are most significant first -> BlueZ would need them reversed.
            var matchesReversed = seen.Keys.Count(a => Rpa.Resolves(stored, a));
            asIs += matchesAsIs;
            reversed += matchesReversed;

            var verdict = (matchesAsIs, matchesReversed) switch
            {
                ( > 0, 0) => $"MATCH, copy as-is ({matchesAsIs} address(es))",
                (0, > 0) => $"MATCH, needs reversing ({matchesReversed} address(es))",
                ( > 0, > 0) => "MATCHES BOTH WAYS (should be impossible)",
                _ => "not heard",
            };
            report.AppendLine($"  {BlueZInfo.SanitizeName(device.Name),-28} {verdict}");
        }

        report.AppendLine();
        report.AppendLine((asIs, reversed) switch
        {
            ( > 0, 0) => "RESULT: Windows IRKs can be copied to Linux as-is.",
            (0, > 0) => "RESULT: Windows IRKs must be reversed for Linux.",
            ( > 0, > 0) => "RESULT: Conflicting matches. Something is off; don't trust this run.",
            _ => "RESULT: No matches. Wake your phone's screen or open your earbuds' case so they broadcast, then run it again.",
        });
        return report.ToString();
    }
}
