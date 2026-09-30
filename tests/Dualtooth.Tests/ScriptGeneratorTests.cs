using Dualtooth.Core;

namespace Dualtooth.Tests;

public class ScriptGeneratorTests
{
    static readonly BtAddress Adapter = new(0x50EE329CD51A);

    static PairedDevice Device(string name) => new(new BtAddress(0xC0E15A8F83AC))
    {
        Name = name,
        Le = new LeKeys(new byte[16], 0, 0, 16, 1),
    };

    [Fact]
    public void Script_targets_the_adapter_and_writes_each_device()
    {
        var script = ScriptGenerator.Generate(Adapter, [Device("Logi K250")], new DateTime(2026, 9, 29));

        Assert.StartsWith("#!/bin/bash\n", script);
        Assert.Contains("ADAPTER=\"50:EE:32:9C:D5:1A\"", script);
        Assert.Contains("write_device C0:E1:5A:8F:83:AC 'Logi K250' <<'DUALTOOTH_INFO_EOF'\n[General]\n", script);
        Assert.Contains("--dry-run", script);
    }

    [Fact]
    public void Script_uses_linux_line_endings() =>
        Assert.DoesNotContain("\r", ScriptGenerator.Generate(Adapter, [Device("K")], DateTime.Now));

    [Fact]
    public void Quotes_in_names_are_escaped_for_bash()
    {
        var script = ScriptGenerator.Generate(Adapter, [Device("Phil's Mouse")], DateTime.Now);

        Assert.Contains("write_device C0:E1:5A:8F:83:AC 'Phil'\\''s Mouse'", script);
    }

    [Fact]
    public void Empty_selection_is_rejected() =>
        Assert.Throws<ArgumentException>(() => ScriptGenerator.Generate(Adapter, [], DateTime.Now));
}
