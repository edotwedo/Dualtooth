using Dualtooth.Core;

namespace Dualtooth.Tests;

public class RpaTests
{
    // Sample data from the Bluetooth Core specification, Vol 3, Part H, Appendix D.7.
    static readonly byte[] SpecIrk = Convert.FromHexString("EC0234A357C8AD05341010A60A397D9B");
    const uint SpecPrand = 0x708194;
    const uint SpecHash = 0x0DFBAA;

    [Fact]
    public void Ah_matches_the_bluetooth_spec_sample() =>
        Assert.Equal(SpecHash, Rpa.Ah(SpecIrk, SpecPrand));

    [Fact]
    public void Address_built_from_the_spec_sample_resolves()
    {
        ulong address = (ulong)SpecPrand << 24 | SpecHash;
        Assert.True(Rpa.IsResolvable(address));
        Assert.True(Rpa.Resolves(SpecIrk, address));
    }

    [Fact]
    public void Wrong_byte_order_does_not_resolve()
    {
        ulong address = (ulong)SpecPrand << 24 | SpecHash;
        Assert.False(Rpa.Resolves(SpecIrk.Reverse().ToArray(), address));
    }

    [Theory]
    [InlineData(0xC0E15A8F83ACUL)] // random static (top bits 11)
    [InlineData(0x2BE15A8F83ACUL)] // non-resolvable private (top bits 00)
    public void Other_address_kinds_are_not_resolvable(ulong address) =>
        Assert.False(Rpa.IsResolvable(address));
}
