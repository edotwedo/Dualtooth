# Dualtooth

<p align="center">
  <img src="docs/banner.jpg" width="720" alt="Dualtooth: One Bluetooth connection. Both operating systems.">
</p>

<p align="center">
  <b>Use the same Bluetooth keyboard, mouse, headphones or controller in Windows and Linux without re-pairing every time you switch.</b>
</p>

<p align="center">
  <a href="https://buy.stripe.com/9B63cx3OWb9p6iPgFP9AA01"><img src="https://img.shields.io/badge/Support_Project-Buy_License_%2F_Tip-635BFF?style=for-the-badge&logo=stripe&logoColor=white" alt="Buy Dualtooth on Stripe"></a>
  <a href="https://github.com/edotwedo/Dualtooth/releases/latest"><img src="https://img.shields.io/badge/Download-Dualtooth.exe-blue?style=for-the-badge&logo=windows&logoColor=white" alt="Download Dualtooth.exe"></a>
</p>

---

## Why pairing breaks

When you pair a device, it and the PC agree on a secret cryptographic key. Each device only remembers one key per PC, but Windows and Linux each generate their own when pairing. Whichever OS paired last works, and the other gets rejected. 

Dualtooth extracts Windows' keys safely and creates an automated script to install them directly into BlueZ on Linux so both systems share the exact same keys.

---

## Quick Start (Takes 60 Seconds)

1. **Make sure your devices work in Windows.** (Pair them in Windows first).
2. Download and run **[Dualtooth.exe](https://github.com/edotwedo/Dualtooth/releases/latest)** (standalone 64-bit executable, nothing to install).
3. Click your devices and press **CREATE LINUX SCRIPT**.
4. Reboot into Linux, open a terminal in that folder, and run:
   ```bash
   sudo bash dualtooth-apply.sh
   ```
5. Turn your Bluetooth mouse/keyboard off and on. Done!

<p align="center">
  <img src="docs/screenshot.png" width="480" alt="Dualtooth Window">
</p>

**Afterwards:** Delete the script, since it contains raw pairing keys. Do not pair these devices again in either OS, or you will need to re-run the sync.

---

## What the Linux script does

- Verifies that Linux detects the same physical Bluetooth adapter as Windows.
- Writes each device's Classic `LinkKey`, BLE `LongTermKey` (`LTK`, `EDiv`, `ERand`), and BLE Privacy `IdentityResolvingKey` (`IRK`) atomically to `/var/lib/bluetooth/<adapter>/<device>/info` (backing up any existing file as `info.bak`).
- Automatically detects and cleans up stale BLE MAC-drift folders (`+1/-1` rotated addresses from mice like Logitech MX Master / Keychron).
- Includes built-in **1-command Rollback** (`sudo bash dualtooth-apply.sh --undo`) and **Live Diagnostics** (`sudo bash dualtooth-apply.sh --status`).
- Drops a double-clickable **`Dualtooth-Linux-Install.desktop`** launcher right next to the script.
- Sets `AutoEnable=true` in `/etc/bluetooth/main.conf` so Bluetooth keyboards work at the Linux login screen.

---

## Dualtooth v0.2 vs `bt-dualboot`

| Feature | **Dualtooth v0.2** | `bt-dualboot` (CLI) |
| :--- | :--- | :--- |
| **Classic Bluetooth (`LinkKey` BR/EDR)** | ✅ Full 128-bit sync | ✅ Supported |
| **Bluetooth Low Energy (`LTK` / `EDiv` / `ERand`)** | ✅ Full BLE 4.0 / 5.0+ sync (Legacy & Secure Connections) | ❌ Fails on BLE mice/keyboards |
| **BLE Privacy / Resolvable MAC (`IRK`)** | ✅ Syncs `[IdentityResolvingKey]` + cleans stale MAC drift | ❌ Unsupported |
| **Windows 11 BitLocker / Fast Startup** | ✅ Runs natively in Windows + `[🔌 USB]` 1-click export | ❌ Fails when Linux cannot mount encrypted/hibernated NTFS |
| **Interface** | ✅ Tactile Hardware GUI (4 Skins, 7 Languages, Live `[🔍 X-RAY]`) | ❌ Terminal CLI (`pip` + `chntpw` + manual `/mnt` flags) |
| **Safety & Rollback** | ✅ Atomic `.tmp` writes + `--undo` rollback + `--status` check | ⚠️ Direct overwrite |

---

## Security & How it reads keys

Windows stores Bluetooth pairing keys in `HKLM\SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Keys`, which only the SYSTEM account can read. 

Dualtooth activates the standard Windows backup privilege (`SeBackupPrivilege`, available to any local administrator) and opens those keys in **read-only** mode. It never writes to or alters your Windows registry.

---

## Support Independent Development

Dualtooth is built independently with no telemetry, no tracking, and no subscriptions. If it saved you an hour of registry hex editing, consider supporting ongoing development:

👉 **[Buy a Supporter License / Tip on Stripe](https://buy.stripe.com/9B63cx3OWb9p6iPgFP9AA01)**

---

## Mac users: help wanted 🍎

Dual-booting a Mac (Boot Camp, Asahi Linux or a Hackintosh)? We'd love to add macOS support, but need real Macs to test on. A **read-only, 5-minute probe** that never touches your keys helps a lot: see [docs/macos.md](docs/macos.md).

## Building from source

Requires .NET 10 SDK:
```bash
dotnet test
dotnet publish src/Dualtooth -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish-standalone
```
