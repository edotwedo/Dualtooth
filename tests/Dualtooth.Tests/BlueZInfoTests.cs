using Dualtooth.Core;

namespace Dualtooth.Tests;

public class BlueZInfoTests
{
    static readonly byte[] Key = Convert.FromHexString("00112233445566778899AABBCCDDEEFF");

    [Fact]
    public void Address_round_trips_from_windows_to_bluez_form()
    {
        Assert.True(BtAddress.TryParseCompact("c0e15a8f83ac", out var address));
        Assert.Equal("C0:E1:5A:8F:83:AC", address.ToString());
    }

    [Theory]
    [InlineData("CentralIRK")]
    [InlineData("c0e15a8f83")]
    [InlineData("zze15a8f83ac")]
    public void Non_address_names_are_rejected(string text) =>
        Assert.False(BtAddress.TryParseCompact(text, out _));

    [Fact]
    public void Secure_connections_device_matches_the_format_verified_on_hardware()
    {
        // Same shape as the Logi K250 file that worked, with a dummy key.
        var device = new PairedDevice(new BtAddress(0xC0E15A8F83AC))
        {
            Name = "Logi K250",
            Le = new LeKeys(Key, ERand: 0, Ediv: 0, KeyLength: 16, AddressType: 1),
        };

        Assert.Equal("""
            [General]
            Name=Logi K250
            AddressType=static
            SupportedTechnologies=LE;
            Trusted=true
            Blocked=false

            [LongTermKey]
            Key=00112233445566778899AABBCCDDEEFF
            Authenticated=2
            EncSize=16
            EDiv=0
            Rand=0

            """.Replace("\r\n", "\n"), BlueZInfo.Build(device));
    }

    [Fact]
    public void Legacy_device_converts_erand_and_ediv_to_decimal()
    {
        // ERand/EDIV from the Logi M196 that worked: registry bytes 75,eb,61,2a,c2,a5,5f,25 and 0x95c2.
        var device = new PairedDevice(new BtAddress(0xD104D8810274))
        {
            Name = "Logi M196",
            Le = new LeKeys(Key, ERand: 0x255FA5C22A61EB75, Ediv: 0x95C2, KeyLength: 16, AddressType: 1),
        };

        var info = BlueZInfo.Build(device);

        Assert.Contains("Authenticated=0\n", info);
        Assert.Contains("EDiv=38338\n", info);
        Assert.Contains("Rand=2693053355544144757\n", info);
    }

    [Fact]
    public void Classic_device_gets_a_link_key_and_no_address_type()
    {
        var device = new PairedDevice(new BtAddress(0x880894EC83D5)) { Name = "Headphones", LinkKey = Key };

        var info = BlueZInfo.Build(device);

        Assert.Contains("SupportedTechnologies=BR/EDR;\n", info);
        Assert.Contains("[LinkKey]\nKey=00112233445566778899AABBCCDDEEFF\nType=4\nPINLength=0\n", info);
        Assert.DoesNotContain("AddressType", info);
        Assert.DoesNotContain("[LongTermKey]", info);
    }

    [Fact]
    public void Dual_mode_device_gets_both_keys()
    {
        var device = new PairedDevice(new BtAddress(0xFCDE90D6A428))
        {
            Name = "Phone",
            LinkKey = Key,
            Le = new LeKeys(Key, 0, 0, 16, AddressType: 0),
        };

        var info = BlueZInfo.Build(device);

        Assert.Contains("AddressType=public\n", info);
        Assert.Contains("SupportedTechnologies=BR/EDR;LE;\n", info);
        Assert.Contains("[LinkKey]", info);
        Assert.Contains("[LongTermKey]", info);
        Assert.Equal("Classic + LE", device.TransportLabel);
    }

    [Theory]
    [InlineData("Evil\nName=x", "EvilName=x")]
    [InlineData("  ", "Unknown device")]
    [InlineData(null, "Unknown device")]
    public void Names_are_kept_on_one_line(string? name, string expected) =>
        Assert.Equal(expected, BlueZInfo.SanitizeName(name));
}
