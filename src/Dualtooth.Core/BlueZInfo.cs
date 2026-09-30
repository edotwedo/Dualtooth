using System.Text;

namespace Dualtooth.Core;

/// <summary>Builds the BlueZ /var/lib/bluetooth/&lt;adapter&gt;/&lt;device&gt;/info file for a device.</summary>
public static class BlueZInfo
{
    public static string Build(PairedDevice device)
    {
        var technologies = (device.LinkKey is not null ? "BR/EDR;" : "") + (device.Le is not null ? "LE;" : "");
        var sb = new StringBuilder();

        sb.Append("[General]\n");
        sb.Append($"Name={SanitizeName(device.Name)}\n");
        if (device.Le is { } le)
            sb.Append($"AddressType={(le.AddressType == 1 ? "static" : "public")}\n");
        sb.Append($"SupportedTechnologies={technologies}\n");
        sb.Append("Trusted=true\n");
        sb.Append("Blocked=false\n");

        if (device.LinkKey is { } linkKey)
        {
            // Windows and BlueZ store classic link keys in the same byte order.
            sb.Append("\n[LinkKey]\n");
            sb.Append($"Key={Convert.ToHexString(linkKey)}\n");
            sb.Append("Type=4\n");
            sb.Append("PINLength=0\n");
        }

        if (device.Le is { } keys)
        {
            // The LTK is copied as-is. Windows stores ERand as a little-endian QWORD, which the
            // registry already hands back as the number BlueZ wants in decimal.
            // Authenticated: 2 = unauthenticated LE Secure Connections key, 0 = unauthenticated legacy key.
            // Both values were verified on real hardware (Logi K250 = SC, Logi M196 = legacy).
            sb.Append("\n[LongTermKey]\n");
            sb.Append($"Key={Convert.ToHexString(keys.Ltk)}\n");
            sb.Append($"Authenticated={(keys.IsSecureConnections ? 2 : 0)}\n");
            sb.Append($"EncSize={keys.KeyLength}\n");
            sb.Append($"EDiv={keys.Ediv}\n");
            sb.Append($"Rand={keys.ERand}\n");
        }

        return sb.ToString();
    }

    /// <summary>Keeps a device name on one line so it can't break the info file or the generated script.</summary>
    public static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unknown device";
        var clean = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? "Unknown device" : clean;
    }
}
