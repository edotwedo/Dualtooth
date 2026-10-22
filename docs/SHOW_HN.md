# 🚀 Show HN: Turnkey Hacker News Submission

**Submission URL:** https://news.ycombinator.com/submit  
**Best Times to Post (PST):** Tuesday, Wednesday, or Thursday between 7:00 AM – 9:00 AM PST (high traffic front-page momentum window).

---

### Field 1: Title
```text
Show HN: Dualtooth – Pair Bluetooth once on Windows & Linux dual-boot
```

### Field 2: URL
```text
https://dualtooth.com
```

### Field 3: Text / First Comment (Post immediately after submitting)
```text
Dual-booting Windows and Linux has had a frustrating peripheral issue for years: Bluetooth devices pair against one OS or the other, but not both. Every time you switch operating systems, the cryptographic link keys rotate, dropping your keyboard, mouse, or headphones.

Existing workarounds (`bt-dualboot`, manual hex editing via `psexec -s regedit`) frequently corrupt modern Bluetooth 5.0 Low Energy devices because modern peripherals rely on Identity Resolving Keys (`[IdentityResolvingKey]` / IRK) and Local Signature Keys, not just legacy link keys. Furthermore, BitLocker disk encryption prevents Linux from reading Windows registry hives without manual recovery key entry.

We built Dualtooth to solve this cleanly:
- Full BLE 5.0 + IRK + Classic link key sync (tested with Logitech MX Master 3S, Keychron, and AirPods)
- 1-Click BitLocker USB export (generates an auto-executing `.desktop` importer onto any FAT32 flash drive, bypassing BitLocker completely)
- Built-in rollback (`--undo`) and non-destructive atomic temp writes
- Standalone single-binary (no Python or .NET runtime dependencies)
- 4 tactile hardware skins (Nixie tube, phosphor CRT, cyberpunk, vaporwave) and 7 languages

Core documentation and guide are open: https://github.com/edotwedo/Dualtooth
Live site: https://dualtooth.com

Would love your feedback on peripheral compatibility across different distros and chipsets!
```
