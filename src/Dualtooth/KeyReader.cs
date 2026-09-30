using System.ComponentModel;
using System.Runtime.InteropServices;
using Dualtooth.Core;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Dualtooth;

/// <summary>
/// Reads Bluetooth pairing keys from the registry. The keys are readable only by SYSTEM, so this
/// enables the backup privilege (which every administrator has) and opens the keys in backup mode.
/// </summary>
static class KeyReader
{
    const string KeysPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Keys";
    const string DevicesPath = @"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices";
    const uint RegOptionBackupRestore = 0x4;
    const int KeyRead = 0x20019;
    const int ErrorFileNotFound = 2;

    static readonly SafeRegistryHandle HklmHandle = new(new IntPtr(unchecked((int)0x80000002)), ownsHandle: false);

    public static List<AdapterKeys> ReadAll()
    {
        EnableBackupPrivilege();
        var adapters = new List<AdapterKeys>();

        using var root = OpenForBackup(HklmHandle, KeysPath);
        if (root is null) return adapters;

        foreach (var adapterName in root.GetSubKeyNames())
        {
            if (!BtAddress.TryParseCompact(adapterName, out var adapterAddress)) continue;
            using var adapterKey = OpenForBackup(root.Handle, adapterName);
            if (adapterKey is null) continue;

            var devices = new Dictionary<BtAddress, PairedDevice>();
            PairedDevice DeviceFor(BtAddress a) => devices.TryGetValue(a, out var d) ? d : devices[a] = new PairedDevice(a);

            // Classic devices: one 16-byte value per device, named by its address.
            foreach (var valueName in adapterKey.GetValueNames())
                if (BtAddress.TryParseCompact(valueName, out var address) && adapterKey.GetValue(valueName) is byte[] { Length: 16 } linkKey)
                    DeviceFor(address).LinkKey = linkKey;

            // Low Energy devices: one subkey per device.
            foreach (var deviceName in adapterKey.GetSubKeyNames())
            {
                if (!BtAddress.TryParseCompact(deviceName, out var address)) continue;
                using var deviceKey = OpenForBackup(adapterKey.Handle, deviceName);
                if (deviceKey?.GetValue("LTK") is not byte[] { Length: 16 } ltk) continue;

                DeviceFor(address).Le = new LeKeys(
                    ltk,
                    ERand: deviceKey.GetValue("ERand") is long erand ? unchecked((ulong)erand) : 0,
                    Ediv: deviceKey.GetValue("EDIV") is int ediv ? unchecked((uint)ediv) : 0,
                    KeyLength: deviceKey.GetValue("KeyLength") as int? ?? 16,
                    AddressType: deviceKey.GetValue("AddressType") as int? ?? 0);
            }

            foreach (var device in devices.Values)
                device.Name = ReadClassicName(device.Address);

            adapters.Add(new AdapterKeys(adapterAddress, devices.Values.OrderBy(d => d.Address.Value).ToList()));
        }

        return adapters;
    }

    /// <summary>Windows keeps the names of classic devices it has seen; this key is readable by administrators.</summary>
    static string? ReadClassicName(BtAddress address)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey($@"{DevicesPath}\{address.Value:x12}");
            if (key?.GetValue("Name") is not byte[] raw) return null;
            var name = System.Text.Encoding.UTF8.GetString(raw).TrimEnd('\0');
            return name.Length > 0 ? name : null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    static RegistryKey? OpenForBackup(SafeRegistryHandle parent, string subKey)
    {
        var result = RegOpenKeyEx(parent, subKey, RegOptionBackupRestore, KeyRead, out var handle);
        if (result == ErrorFileNotFound) return null;
        if (result != 0) throw new Win32Exception(result, $"Couldn't open registry key {subKey}.");
        return RegistryKey.FromHandle(handle);
    }

    static void EnableBackupPrivilege()
    {
        const uint TokenAdjustPrivileges = 0x20, TokenQuery = 0x8;
        const int SePrivilegeEnabled = 0x2, ErrorNotAllAssigned = 1300;

        if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out var token))
            throw new Win32Exception();
        using (token)
        {
            if (!LookupPrivilegeValue(null, "SeBackupPrivilege", out var luid))
                throw new Win32Exception();
            var privileges = new TokenPrivileges { Count = 1, Luid = luid, Attributes = SePrivilegeEnabled };
            if (!AdjustTokenPrivileges(token, false, ref privileges, 0, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception();
            if (Marshal.GetLastWin32Error() == ErrorNotAllAssigned)
                throw new UnauthorizedAccessException("Dualtooth needs to run as administrator to read Bluetooth pairings.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Luid { public uint Low; public int High; }

    [StructLayout(LayoutKind.Sequential)]
    struct TokenPrivileges { public int Count; public Luid Luid; public int Attributes; }

    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern bool LookupPrivilegeValue(string? systemName, string name, out Luid luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool AdjustTokenPrivileges(SafeAccessTokenHandle token, bool disableAll, ref TokenPrivileges newState,
        int bufferLength, IntPtr previousState, IntPtr returnLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
    static extern int RegOpenKeyEx(SafeRegistryHandle key, string subKey, uint options, int access, out SafeRegistryHandle result);
}
