# Dualtooth

Use the same Bluetooth keyboard, mouse, headphones or controller in Windows **and** Linux on a
dual-boot PC, without pairing them again every time you switch.

## Why pairing breaks

When you pair a device, it and the PC agree on a secret key. Each device only remembers one key per
PC, but Windows and Linux each make their own when you pair. So whichever OS paired last works, and
the other one doesn't. Dualtooth copies Windows' keys into Linux so both use the same one.

## How to use it

1. **Pair each device in Linux first, then in Windows.** (Skip this if they already work in Windows
   and you're fine re-pairing them there once.) Windows must be the *last* place you paired them.
2. In Windows, run **Dualtooth.exe** and click **Yes** on the admin prompt.
3. Tick the devices you want, then click **Generate Linux script**.
4. Boot Linux, open a terminal in the folder with the script (Linux can read your Windows drive), and run:
   ```
   sudo bash dualtooth-apply.sh
   ```
5. Turn each device off and on. Done.

**Afterwards:** delete the script, since it contains pairing keys. Don't pair these devices again in
either OS, or you'll need to repeat this.

## What the Linux script does

- Checks that Linux sees the same Bluetooth adapter as Windows. If not, it stops without changing anything.
- Writes each device's key to `/var/lib/bluetooth/<adapter>/<device>/info`, saving any existing
  file as `info.bak` first.
- Sets `AutoEnable=true` in `/etc/bluetooth/main.conf`, so keyboards work at the login screen.
- Restarts the Bluetooth service.

## How it reads the keys

Windows keeps pairing keys in `HKLM\SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Keys`, which
only the SYSTEM account can read. Dualtooth enables the backup privilege (every administrator has it)
and opens those keys read-only in backup mode. It never changes anything on Windows.

## Status

| | |
|---|---|
| Bluetooth Low Energy, Secure Connections (e.g. Logi K250) | Tested on real hardware |
| Bluetooth Low Energy, legacy pairing (e.g. Logi M196) | Tested on real hardware |
| Classic Bluetooth (most headphones, older devices) | Written to the BlueZ format, **not yet tested** |
| Devices using rotating private addresses | Not supported yet (no IRK export) |

## Building

Requires the .NET 10 SDK.

```
dotnet test
dotnet publish src/Dualtooth -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish-standalone
```

This bundles the .NET runtime into one `Dualtooth.exe` (about 55 MB) that runs on any 64-bit
Windows 10 or 11 PC with nothing else to install.
