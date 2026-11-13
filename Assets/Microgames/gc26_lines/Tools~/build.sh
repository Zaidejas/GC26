#!/bin/bash
# Builds Lines on its own, as a phone app, from the command line. Unity must be closed.
# Run it from anywhere in the project, e.g. from the project root:
#   Assets/Microgames/gc26_lines/Tools~/build.sh ios-sim      build for the iOS Simulator, install it and start it
#   Assets/Microgames/gc26_lines/Tools~/build.sh ios-device   Xcode project for a real iPhone (Builds/iOS-Device);
#                                 with LINES_APPLE_TEAM set and an iPhone on the cable, also signs, installs and starts it
#   Assets/Microgames/gc26_lines/Tools~/build.sh android      Android APK (Builds/Android/Lines.apk); installs
#                                 and starts it on every running Android emulator or connected phone
# Options (environment variables):
#   LINES_SIM=<name or UDID>  which simulated iPhone to use (default: the running one, else "iPhone 18 Pro")
#   LINES_APPLE_TEAM=<id>     your Apple team ID for signing the iPhone build
#   LINES_IPHONE=<UDID>       which connected iPhone (default: the first one on the cable)
set -euo pipefail
cd "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.5.6f1/Unity.app/Contents/MacOS/Unity}"
LOGS=Builds/logs
mkdir -p "$LOGS"

if [ -f Temp/UnityLockfile ] && pgrep -f "Unity.app/Contents/MacOS/Unity" >/dev/null; then
  echo "Unity is open. Close it first (it locks the project)." >&2; exit 1
fi

# The build sets our phone-app settings (name, app id, icon, splash) and Unity touches other shared
# settings files too. Those belong to the whole team (RULES.md), so they are copied first and put back
# exactly as they were once Unity has finished, whether the build worked or not.
SHARED="ProjectSettings Assets/Settings Packages/packages-lock.json GC26.slnx"
KEEP="$(mktemp -d)"
for f in $SHARED; do mkdir -p "$KEEP/$(dirname "$f")"; cp -Rp "$f" "$KEEP/$f"; done
put_back() {
  [ -d "$KEEP" ] || return 0
  for f in $SHARED; do rm -rf "$f"; cp -Rp "$KEEP/$f" "$f"; done
  rm -rf "$KEEP"
}
trap put_back EXIT   # also when the build is stopped halfway

unity() { # $1 build target, $2 method, $3 log name
  echo "Unity: building ($1)… log: $LOGS/$3.log"
  local ok=1
  "$UNITY" -batchmode -nographics -quit -projectPath . -buildTarget "$1" \
    -executeMethod "GC26.Lines.EditorTools.BuildScripts.$2" -logFile "$LOGS/$3.log" || ok=0
  put_back
  if [ "$ok" = 0 ]; then
    echo "Unity build failed; see $LOGS/$3.log" >&2; grep -m5 "error" "$LOGS/$3.log" >&2 || true; exit 1
  fi
}

case "${1:-}" in
  ios-sim)
    unity iOS IosSimulator unity-ios-sim
    echo "Xcode: compiling for the simulator… log: $LOGS/xcode-ios-sim.log"
    xcodebuild -project Builds/iOS-Simulator/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
      -configuration Debug -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' \
      -derivedDataPath Builds/iOS-Simulator/DerivedData CODE_SIGNING_ALLOWED=NO build \
      > "$LOGS/xcode-ios-sim.log" 2>&1 || { echo "Xcode build failed; see $LOGS/xcode-ios-sim.log" >&2; grep -m5 "error:" "$LOGS/xcode-ios-sim.log" >&2 || true; exit 1; }
    APP=$(find Builds/iOS-Simulator/DerivedData/Build/Products/Debug-iphonesimulator -maxdepth 1 -name "*.app" | head -1)
    BUNDLE=$(/usr/libexec/PlistBuddy -c "Print CFBundleIdentifier" "$APP/Info.plist")
    SIM="${LINES_SIM:-$(xcrun simctl list devices booted | grep -m1 -o '[0-9A-F-]\{36\}' || true)}"
    SIM="${SIM:-iPhone 18 Pro}"
    xcrun simctl boot "$SIM" 2>/dev/null || true
    open -b com.apple.iphonesimulator 2>/dev/null || true   # show the Simulator app if this Xcode has one
    xcrun simctl install "$SIM" "$APP"
    xcrun simctl launch "$SIM" "$BUNDLE"
    echo "Running $BUNDLE on the simulator ($SIM)."
    ;;
  ios-device)
    unity iOS IosDevice unity-ios-device
    echo "Xcode project ready: Builds/iOS-Device/Unity-iPhone.xcodeproj"
    # With a team ID and an iPhone on the cable: sign, install and start the game on it.
    PHONE="${LINES_IPHONE:-$(xcrun devicectl list devices 2>/dev/null | awk '(/connected/ || /available/) && /physical/ && /iPhone/ { for (i = 1; i <= NF; i++) if ($i ~ /^[0-9A-F]{8}-[0-9A-F]{16}$/) print $i }' | head -1)}"
    if [ -n "${LINES_APPLE_TEAM:-}" ] && [ -n "$PHONE" ]; then
      echo "Xcode: signing and compiling for the iPhone ($PHONE)… log: $LOGS/xcode-ios-device.log"
      xcodebuild -project Builds/iOS-Device/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
        -destination "id=$PHONE" -allowProvisioningUpdates -derivedDataPath Builds/iOS-Device/DerivedData build \
        > "$LOGS/xcode-ios-device.log" 2>&1 || { echo "Xcode build failed; see $LOGS/xcode-ios-device.log" >&2; grep -m5 "error:" "$LOGS/xcode-ios-device.log" >&2 || true; exit 1; }
      APP=$(find Builds/iOS-Device/DerivedData/Build/Products/Release-iphoneos -maxdepth 1 -name "*.app" | head -1)
      BUNDLE=$(/usr/libexec/PlistBuddy -c "Print CFBundleIdentifier" "$APP/Info.plist")
      xcrun devicectl device install app --device "$PHONE" "$APP" >/dev/null
      xcrun devicectl device process launch --device "$PHONE" "$BUNDLE" >/dev/null \
        && echo "Running $BUNDLE on the iPhone." \
        || echo "Installed. If it doesn't open: Settings → General → VPN & Device Management → trust your Apple ID."
    else
      echo "To install on an iPhone: connect it and set LINES_APPLE_TEAM=<your team ID> (see CLAUDE.md)."
    fi
    ;;
  android)
    unity Android Android unity-android
    echo "APK ready: Builds/Android/Lines.apk"
    ADB="$(dirname "$UNITY")/../../../PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb"
    devices() { "$ADB" devices 2>/dev/null | awk 'NR > 1 && $2 == "device" { print $1 }'; }
    # The build restarts adb; running emulators take a few seconds to reconnect.
    for _ in 1 2 3 4 5 6 7 8 9 10; do [ -n "$(devices)" ] && break; sleep 1; done
    for DEVICE in $(devices); do
      "$ADB" -s "$DEVICE" install -r Builds/Android/Lines.apk >/dev/null
      "$ADB" -s "$DEVICE" shell monkey -p com.gc26.lines 1 >/dev/null 2>&1
      echo "Running com.gc26.lines on $DEVICE."
    done
    ;;
  *)
    sed -n '2,11p' "$0"; exit 1 ;;
esac
