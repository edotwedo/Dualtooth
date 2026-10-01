# macOS support: help wanted

Dualtooth works for Windows and Linux dual-boot PCs. We'd like to add **macOS**, for
Intel Macs running Windows through Boot Camp, Apple Silicon Macs running Asahi Linux,
and Hackintosh builds. The problem is the same: each OS makes its own pairing, so
devices only work in whichever one paired last.

We don't have a dual-boot Mac to test on, and we won't ship key-writing code we can't
verify. So the first step is gathering facts.

## How you can help (5 minutes, read-only)

1. On your Mac, download [`tools/macos-probe.sh`](../tools/macos-probe.sh).
2. In Terminal, run: `bash macos-probe.sh`
3. Paste the output into a [new issue](https://github.com/edotwedo/Dualtooth/issues/new).

The probe **changes nothing** and **never prints keys**. It reports your macOS
version and *where* and *how* pairings are stored, with key values and most of each
device address blanked out.

## What we know so far

Collected from community guides. None of it has been verified by us yet.

| macOS version | Where pairing keys live |
|---|---|
| High Sierra – Big Sur | `/private/var/root/Library/Preferences/com.apple.bluetoothd.plist`, under `LinkKeys` (classic) and `SMPDistributionKeys` (Low Energy) |
| Monterey | System Keychain, under an entry named `MobileBluetooth` |
| Sonoma and later | System Keychain, one entry per device (read with `sudo security find-generic-password -ga <device address>`) |

Low Energy fields reported in the plist: `LTK`, `LocalLTK`, `EDIV`, `RAND`, `LocalRAND`,
`IRK`, `Address`, `AddressType`, `MITMProtection`.

## Open questions

- **Byte order.** Guides disagree. Several say classic link keys must be byte-reversed
  between Windows and macOS on older versions but not from Big Sur on; one says `EDIV`
  must be byte-reversed (registry `0x80b4` → plist `b480`). Needs testing per version.
- **Direction.** Every existing tool copies keys **Mac → Windows**, because writing to
  the Windows registry is easier than writing to the Mac's Keychain. Dualtooth may need
  to support that direction too.
- **Writing on modern macOS.** Can the Keychain entries be written safely, and does
  `bluetoothd` need restarting or caching to be cleared?
- **Device quirks.** Some keyboards (for example Logitech K380) detect the OS from the
  pairing, so a Mac-made key can make them think Windows is a Mac.

## Sources

- [zhuyie: BLE pairing on macOS/Windows dual boot](https://zhuyie.github.io/posts/ble-paring-on-dualboot-macos-windows/)
- [tonymacx86: Sonoma keys for multiboot with Windows](https://www.tonymacx86.com/threads/mac-sonoma-bluetooth-pairing-secure-keys-for-multiboot-with-windows.327541/)
- [HenrySeed/macosDualBootingBluetoothKeys](https://github.com/HenrySeed/macosDualBootingBluetoothKeys)
- [dortania/bugtracker #125](https://github.com/dortania/bugtracker/issues/125)
- [digitalbirdo/BT-LinkkeySync #12](https://github.com/digitalbirdo/BT-LinkkeySync/issues/12)
- [mmems: macOS dual boot Bluetooth](https://mmems.gitbook.io/calepin/operating-systems/macos/macos-dual-boot-bluetooth)
