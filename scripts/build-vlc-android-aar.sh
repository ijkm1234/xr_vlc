#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workspace_root="$(cd "$project_root/.." && pwd)"
vlc_root="${VLC_ANDROID_ROOT:-$workspace_root/vlc-android}"
vlc_build_script="$vlc_root/buildsystem/build-xr-aar.sh"

if [[ ! -x "$vlc_build_script" ]]; then
    echo "Missing executable VLC AAR build script at $vlc_build_script" >&2
    exit 1
fi

"$vlc_build_script"

aar_source="$vlc_root/application/vlc-android/build/outputs/aar/vlc-android-debug.aar"
aar_target="$project_root/Assets/Plugins/Android/vlc-android-debug.aar"

cp "$aar_source" "$aar_target"
cmp -s "$aar_source" "$aar_target"
echo "Built and installed $aar_target"
