namespace Dualtooth.Core;

/// <summary>Bluetooth Low Energy bonding keys as Windows stores them.</summary>
/// <param name="Ltk">Long-term key, 16 bytes.</param>
/// <param name="ERand">Encrypted diversifier random value. Zero for LE Secure Connections.</param>
/// <param name="Ediv">Encrypted diversifier. Zero for LE Secure Connections.</param>
/// <param name="KeyLength">Encryption key size in bytes.</param>
/// <param name="AddressType">0 = public, 1 = random static.</param>
/// <param name="Irk">Identity resolving key, 16 bytes as Windows stores them, if the device shared one.</param>
public sealed record LeKeys(byte[] Ltk, ulong ERand, uint Ediv, int KeyLength, int AddressType, byte[]? Irk = null)
{
    /// <summary>LE Secure Connections keys have no diversifier; legacy pairing keys do.</summary>
    public bool IsSecureConnections => ERand == 0 && Ediv == 0;
}

/// <summary>One device bonded to a Windows Bluetooth adapter.</summary>
public sealed class PairedDevice(BtAddress address)
{
    public BtAddress Address { get; } = address;

    /// <summary>Classic (BR/EDR) link key, 16 bytes, or null if the device isn't bonded over classic.</summary>
    public byte[]? LinkKey { get; set; }

    /// <summary>Low Energy keys, or null if the device isn't bonded over LE.</summary>
    public LeKeys? Le { get; set; }

    public string? Name { get; set; }

    /// <summary>Short human label such as "keyboard", "mouse" or "audio".</summary>
    public string Kind { get; set; } = "other";

    public string TransportLabel => (LinkKey, Le) switch
    {
        (not null, not null) => "Classic + LE",
        (not null, null) => "Classic",
        _ => "LE",
    };
}

/// <summary>A Windows Bluetooth adapter and the devices bonded to it.</summary>
public sealed record AdapterKeys(BtAddress Address, IReadOnlyList<PairedDevice> Devices);
