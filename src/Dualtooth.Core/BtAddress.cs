using System.Globalization;

namespace Dualtooth.Core;

/// <summary>A 48-bit Bluetooth device address.</summary>
public readonly record struct BtAddress(ulong Value)
{
    /// <summary>Parses the 12-hex-digit form Windows uses for registry key names, e.g. "c0e15a8f83ac".</summary>
    public static bool TryParseCompact(string text, out BtAddress address)
    {
        address = default;
        if (text.Length != 12 || !ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            return false;
        address = new BtAddress(value);
        return true;
    }

    /// <summary>The colon-separated upper-case form BlueZ uses for directory names, e.g. "C0:E1:5A:8F:83:AC".</summary>
    public override string ToString()
    {
        var value = Value;
        return string.Join(':', Enumerable.Range(0, 6).Select(i => ((value >> (8 * (5 - i))) & 0xFF).ToString("X2")));
    }
}
