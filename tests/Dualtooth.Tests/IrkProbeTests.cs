using System.Security.Cryptography;
using System.Text;
using Dualtooth.Core;

namespace Dualtooth.Tests;

public class IrkProbeTests
{
    static readonly byte[] Irk = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    static PairedDevice Phone(byte[]? irk) => new(new BtAddress(0xFCDE90D6A428))
    {
        Name = "Phil's S10e",
        Le = new LeKeys(new byte[16], 0, 0, 16, 0, irk),
    };

    [Fact]
    public void Fingerprint_is_sha256_of_upper_case_hex_like_sha256sum()
    {
        var expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes("00112233445566778899AABBCCDDEEFF")));
        Assert.Equal(expected, IrkProbe.Fingerprint(Irk));
    }

    [Fact]
    public void Script_checks_both_orders_and_never_contains_the_key()
    {
        var script = IrkProbe.Generate([Phone(Irk)], DateTime.Now);

        Assert.Contains($"check FC:DE:90:D6:A4:28 'Phil'\\''s S10e' {IrkProbe.Fingerprint(Irk)} " +
                        IrkProbe.Fingerprint(Irk.Reverse().ToArray()), script);
        Assert.DoesNotContain("00112233445566778899AABBCCDDEEFF", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FFEEDDCCBBAA99887766554433221100", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\r", script);
    }

    [Fact]
    public void Devices_without_an_irk_are_left_out() =>
        Assert.Throws<ArgumentException>(() => IrkProbe.Generate([Phone(null)], DateTime.Now));
}
