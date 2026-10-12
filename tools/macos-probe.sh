#!/bin/bash
# Dualtooth macOS probe (read-only, experimental, not yet tested on a real Mac)
#
# Reports WHERE and HOW this Mac stores Bluetooth pairings, so Dualtooth can add
# macOS support. It never prints keys: every key value and most of each address
# is blanked out. It changes nothing.
#
# Run in Terminal:   bash macos-probe.sh
# Then paste the output into: https://github.com/edotwedo/Dualtooth/issues

PLIST="/private/var/root/Library/Preferences/com.apple.bluetoothd.plist"

redact() {
  sed -E \
    -e 's/bytes = 0x[0-9a-fA-F]+( \.\.\. [0-9a-fA-F]+)?/bytes = <hidden>/g' \
    -e 's/<[0-9a-fA-F ]{8,}>/<hidden>/g' \
    -e 's/([0-9a-fA-F]{2})[-:]([0-9a-fA-F]{2})[-:]([0-9a-fA-F]{2})[-:]([0-9a-fA-F]{2})[-:]([0-9a-fA-F]{2})[-:]([0-9a-fA-F]{2})/xx:xx:xx:xx:\5:\6/g' \
    -e 's/"[0-9a-fA-F]{32,}"/"<hidden>"/g'
}

echo "=== Dualtooth macOS probe ==="
echo "macOS: $(sw_vers -productVersion) ($(sw_vers -buildVersion))"
echo "Hardware: $(sysctl -n hw.model 2>/dev/null) / $(uname -m)"
echo
echo "This needs your Mac password to read system Bluetooth settings (read-only)."
sudo -v || { echo "Couldn't get admin access. Nothing was read."; exit 1; }

echo
echo "=== 1. Bluetooth settings file ==="
if sudo test -f "$PLIST"; then
  echo "Found: $PLIST"
  echo "Top-level sections:"
  sudo plutil -p "$PLIST" 2>/dev/null | grep -E '^  "[A-Za-z]+" =>' | sed -E 's/ =>.*//' | redact
  for section in LinkKeys SMPDistributionKeys; do
    echo
    echo "--- $section (structure only) ---"
    sudo plutil -extract "$section" xml1 -o - "$PLIST" >/dev/null 2>&1 \
      && sudo plutil -p "$PLIST" 2>/dev/null | awk -v s="\"$section\"" '
           index($0, s) == 3 { on = 1 } on { print } on && /^  }/ { exit }' | redact \
      || echo "(not present)"
  done
else
  echo "Not found (expected on newer macOS)."
fi

echo
echo "=== 2. System Keychain: Bluetooth entries ==="
sudo security dump-keychain /Library/Keychains/System.keychain 2>/dev/null \
  | grep -iE '"(svce|labl|desc)"<blob>=.*(bluetooth|bt)' \
  | sort | uniq -c | redact
echo "(Counts of Bluetooth-related entries by name. Contents are not read.)"

echo
echo "=== Done ==="
echo "Please paste everything above into a GitHub issue. Thank you!"
