using System.Security.Cryptography;
using System.Text;

namespace Dualtooth.Core;

/// <summary>
/// Settles which byte order BlueZ uses for Windows IRKs, without moving any keys.
/// Windows' IRK is fingerprinted both ways; after the device is paired in Linux, the generated
/// script fingerprints Linux's copy and reports which one matches. A device's IRK is its own
/// identity key, so it is the same no matter which OS paired it.
/// </summary>
public static class IrkProbe
{
    /// <summary>SHA-256 of the key written as BlueZ writes it: upper-case hex, no separators.</summary>
    public static string Fingerprint(byte[] key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Convert.ToHexString(key))));

    public static string Generate(IEnumerable<PairedDevice> devices, DateTime generatedAt)
    {
        var withIrk = devices.Where(d => d.Le?.Irk is not null).ToList();
        if (withIrk.Count == 0) throw new ArgumentException("No paired devices have an IRK.", nameof(devices));

        var sb = new StringBuilder();
        sb.Append($$"""
            #!/bin/bash
            # Dualtooth IRK probe, generated {{generatedAt:yyyy-MM-dd HH:mm}}.
            # Works out whether Windows IRKs must be byte-reversed for Linux. Contains only
            # fingerprints (one-way hashes), never keys, and changes nothing.
            #
            # 1. Pair one of the devices below in Linux (Settings > Bluetooth).
            # 2. Run:  sudo bash dualtooth-irk-probe.sh
            if [ "$(id -u)" -ne 0 ]; then echo "Please run with sudo:  sudo bash $0"; exit 1; fi

            asis=0; reversed=0
            check() {  # $1 = address, $2 = name, $3 = as-is fingerprint, $4 = reversed fingerprint
              local info key fp
              info=$(ls /var/lib/bluetooth/*/"$1"/info 2>/dev/null | head -n 1)
              if [ -z "$info" ]; then printf '  %-28s not paired in Linux yet\n' "$2"; return; fi
              key=$(awk -F= '/^\[IdentityResolvingKey\]/{s=1;next} /^\[/{s=0} s&&$1=="Key"{print $2}' "$info")
              if [ -z "$key" ]; then printf '  %-28s paired, but Linux has no IRK for it\n' "$2"; return; fi
              fp=$(printf '%s' "$key" | tr 'a-f' 'A-F' | sha256sum | cut -d' ' -f1)
              if [ "$fp" = "$3" ]; then printf '  %-28s MATCH: copy as-is\n' "$2"; asis=$((asis+1))
              elif [ "$fp" = "$4" ]; then printf '  %-28s MATCH: needs reversing\n' "$2"; reversed=$((reversed+1))
              else printf '  %-28s IRK differs both ways (unexpected)\n' "$2"; fi
            }

            echo "Dualtooth IRK probe"

            """.Replace("\r\n", "\n"));

        foreach (var device in withIrk)
        {
            var name = BlueZInfo.SanitizeName(device.Name).Replace("'", "'\\''");
            var irk = device.Le!.Irk!;
            sb.Append($"check {device.Address} '{name}' {Fingerprint(irk)} {Fingerprint(irk.Reverse().ToArray())}\n");
        }

        sb.Append("""

            echo
            if [ "$asis" -gt 0 ] && [ "$reversed" -eq 0 ]; then echo "RESULT: copy as-is"
            elif [ "$reversed" -gt 0 ] && [ "$asis" -eq 0 ]; then echo "RESULT: needs reversing"
            elif [ "$asis" -gt 0 ]; then echo "RESULT: conflicting, please report this"
            else echo "RESULT: no answer yet. Pair one of the devices above in Linux, then run this again."; fi

            """.Replace("\r\n", "\n"));
        return sb.ToString();
    }
}
