using Dualtooth.Core;
using Windows.Devices.Bluetooth;

namespace Dualtooth;

/// <summary>Fills in friendly names and device types using Windows' Bluetooth APIs.</summary>
static class DeviceLookup
{
    public static async Task<BtAddress?> GetCurrentAdapterAsync()
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync();
            return adapter is null ? null : new BtAddress(adapter.BluetoothAddress);
        }
        catch { return null; }
    }

    public static async Task DescribeAsync(PairedDevice device)
    {
        if (device.Le is { } le)
        {
            try
            {
                var addressType = le.AddressType == 1 ? BluetoothAddressType.Random : BluetoothAddressType.Public;
                using var leDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(device.Address.Value, addressType);
                if (leDevice is not null)
                {
                    if (!string.IsNullOrWhiteSpace(leDevice.Name)) device.Name ??= leDevice.Name;
                    device.Kind = KindFromAppearance(leDevice.Appearance);
                }
            }
            catch { /* Name lookup is cosmetic; keys are still valid. */ }
        }

        if (device.LinkKey is not null || device.Name is null)
        {
            try
            {
                using var classic = await BluetoothDevice.FromBluetoothAddressAsync(device.Address.Value);
                if (classic is not null)
                {
                    if (!string.IsNullOrWhiteSpace(classic.Name)) device.Name ??= classic.Name;
                    if (device.Kind == "other") device.Kind = KindFromClass(classic.ClassOfDevice);
                }
            }
            catch { /* Same as above. */ }
        }
    }

    static string KindFromAppearance(BluetoothLEAppearance appearance)
    {
        if (appearance.Category == BluetoothLEAppearanceCategories.HumanInterfaceDevice)
        {
            var sub = appearance.SubCategory;
            if (sub == BluetoothLEAppearanceSubcategories.Keyboard) return "keyboard";
            if (sub == BluetoothLEAppearanceSubcategories.Mouse) return "mouse";
            if (sub == BluetoothLEAppearanceSubcategories.Gamepad || sub == BluetoothLEAppearanceSubcategories.Joystick) return "gamepad";
            return "input";
        }
        if (appearance.Category == BluetoothLEAppearanceCategories.Phone) return "phone";
        if (appearance.Category == BluetoothLEAppearanceCategories.Computer) return "computer";
        if (appearance.Category == BluetoothLEAppearanceCategories.Watch) return "watch";
        return "other";
    }

    static string KindFromClass(BluetoothClassOfDevice cod)
    {
        switch (cod.MajorClass)
        {
            case BluetoothMajorClass.AudioVideo: return "audio";
            case BluetoothMajorClass.Phone: return "phone";
            case BluetoothMajorClass.Computer: return "computer";
            case BluetoothMajorClass.Peripheral:
                var minor = (cod.RawValue >> 2) & 0x3F;
                return ((minor >> 4) & 0x3) switch
                {
                    1 => "keyboard",
                    2 => "mouse",
                    3 => "keyboard",
                    _ => (minor & 0xF) is 1 or 2 ? "gamepad" : "input",
                };
            default: return "other";
        }
    }
}
