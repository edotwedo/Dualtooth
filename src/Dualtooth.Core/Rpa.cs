using System.Security.Cryptography;

namespace Dualtooth.Core;

/// <summary>
/// Resolvable private addresses: the rotating addresses privacy-minded BLE devices use.
/// A device's address can be recognized by anyone holding its Identity Resolving Key (IRK).
/// </summary>
public static class Rpa
{
    /// <summary>Resolvable private addresses have 01 as their top two bits.</summary>
    public static bool IsResolvable(ulong address) => (address >> 46) == 0b01;

    /// <summary>
    /// The Bluetooth random address hash function ah (Core spec, Vol 3, Part H, 2.2.2).
    /// </summary>
    /// <param name="irk">The IRK, most significant byte first.</param>
    /// <param name="prand">The top 24 bits of the address.</param>
    public static uint Ah(byte[] irk, uint prand)
    {
        using var aes = Aes.Create();
        aes.Key = irk;
        var block = new byte[16];
        block[13] = (byte)(prand >> 16);
        block[14] = (byte)(prand >> 8);
        block[15] = (byte)prand;
        var result = aes.EncryptEcb(block, PaddingMode.None);
        return (uint)(result[13] << 16 | result[14] << 8 | result[15]);
    }

    /// <summary>True if <paramref name="address"/> was generated from <paramref name="irk"/> (most significant byte first).</summary>
    public static bool Resolves(byte[] irk, ulong address) =>
        IsResolvable(address) && Ah(irk, (uint)(address >> 24)) == (uint)(address & 0xFFFFFF);
}
