#!/usr/bin/env bash
# Copyright (C) 2026 XRVLC contributors
# SPDX-License-Identifier: GPL-2.0-or-later
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
workspace_root="$(cd "$project_root/.." && pwd)"
vlc_root="$workspace_root/vlc-android"
vlc_repository="${VLC_ANDROID_REPOSITORY:-https://github.com/ijkm1234/vlc_android_for_xr_vlc.git}"
vlc_version="${VLC_ANDROID_VERSION:-v0.0.1}"
vlc_tested_hash="${VLC_ANDROID_TESTED_HASH:-d7d8dc62bff041c78bf93dfa601edd3b1f97a83a}"
vlc_use_local="${VLC_ANDROID_USE_LOCAL:-0}"
vlc_build_script="$vlc_root/buildsystem/build-xr-aar.sh"
local_properties="$vlc_root/local.properties"
ndk_version="${ANDROID_NDK_VERSION:-27.3.13750724}"
aar_variant="${VLC_ANDROID_AAR_VARIANT:-debug}"

case "$aar_variant" in
    debug|release) ;;
    *)
        echo "Unsupported VLC_ANDROID_AAR_VARIANT: $aar_variant (expected debug or release)" >&2
        exit 2
        ;;
esac

fail() {
    echo "$1" >&2
    exit 1
}

read_property() {
    [[ -f "$local_properties" ]] || return 0
    sed -n "s/^$1=//p" "$local_properties" | head -n 1
}

if [[ ! -e "$vlc_root/.git" ]]; then
    if [[ -e "$vlc_root" ]]; then
        fail "$vlc_root exists but is not a Git checkout; move it aside before bootstrapping."
    fi
    echo "VLC Android sources not found; cloning release tag $vlc_version from $vlc_repository"
    git clone --filter=blob:none --single-branch --branch "$vlc_version" \
        "$vlc_repository" "$vlc_root"
    git -C "$vlc_root" checkout --detach "${vlc_version}^{commit}"
else
    echo "Using existing VLC Android checkout at $vlc_root (no automatic pull or reset)"
fi

if [[ "$vlc_use_local" != "1" ]]; then
    resolved_vlc_tag_hash="$(git -C "$vlc_root" rev-parse "${vlc_version}^{commit}" 2>/dev/null || true)"
    if [[ "$resolved_vlc_tag_hash" != "$vlc_tested_hash" ]]; then
        git -C "$vlc_root" fetch --force "$vlc_repository" \
            "+refs/tags/${vlc_version}:refs/tags/${vlc_version}"
        resolved_vlc_tag_hash="$(git -C "$vlc_root" rev-parse "${vlc_version}^{commit}")"
    fi
    if [[ "$resolved_vlc_tag_hash" != "$vlc_tested_hash" ]]; then
        fail "VLC Android release tag $vlc_version resolves to $resolved_vlc_tag_hash; expected $vlc_tested_hash."
    fi
    resolved_vlc_revision="$(git -C "$vlc_root" rev-parse HEAD)"
    if [[ "$resolved_vlc_revision" != "$resolved_vlc_tag_hash" ]]; then
        fail "VLC Android checkout is at $resolved_vlc_revision; expected release tag $vlc_version ($resolved_vlc_tag_hash). Update the sibling checkout, or set VLC_ANDROID_USE_LOCAL=1 to build local development sources."
    fi
    if ! git -C "$vlc_root" diff --quiet || ! git -C "$vlc_root" diff --cached --quiet; then
        fail "VLC Android checkout contains tracked local changes. Commit them, or set VLC_ANDROID_USE_LOCAL=1 to build local development sources."
    fi
    echo "Verified VLC Android release tag $vlc_version ($resolved_vlc_tag_hash)"
else
    echo "VLC_ANDROID_USE_LOCAL=1: building the current local VLC Android checkout"
fi

if [[ ! -x "$vlc_build_script" ]]; then
    fail "Missing executable VLC AAR build script at $vlc_build_script"
fi

android_sdk="${ANDROID_SDK:-${ANDROID_SDK_ROOT:-${ANDROID_HOME:-}}}"
android_sdk="${android_sdk:-$(read_property 'sdk\.dir')}"
if [[ -z "$android_sdk" && -d "$HOME/Library/Android/sdk" ]]; then
    android_sdk="$HOME/Library/Android/sdk"
fi
[[ -d "$android_sdk" ]] || fail "Android SDK not found. Set ANDROID_SDK or run ./setup_android_build.sh first."

android_ndk="${ANDROID_NDK:-${ANDROID_NDK_HOME:-}}"
android_ndk="${android_ndk:-$(read_property 'android\.ndkPath')}"
if [[ -z "$android_ndk" && -d "$android_sdk/ndk/$ndk_version" ]]; then
    android_ndk="$android_sdk/ndk/$ndk_version"
fi
[[ -f "$android_ndk/source.properties" ]] || \
    fail "Android NDK $ndk_version not found. Set ANDROID_NDK or run ./setup_android_build.sh first."

resolved_ndk_version="$(sed -n 's/^Pkg.Revision[[:space:]]*=[[:space:]]*//p' "$android_ndk/source.properties" | head -n 1)"
[[ -n "$resolved_ndk_version" ]] || fail "Cannot read the NDK version from $android_ndk/source.properties"

export ANDROID_SDK="$android_sdk"
export ANDROID_NDK="$android_ndk"
export VLC_ANDROID_AAR_VARIANT="$aar_variant"

if [[ ! -f "$local_properties" ]]; then
    printf 'sdk.dir=%s\nandroid.ndkPath=%s\nandroid.ndkFullVersion=%s\n' \
        "$ANDROID_SDK" "$ANDROID_NDK" "$resolved_ndk_version" > "$local_properties"
    echo "Created $local_properties"
fi

"$vlc_build_script"

aar_source="$vlc_root/application/vlc-android/build/outputs/aar/vlc-android-$aar_variant.aar"
aar_target="$project_root/Assets/Plugins/Android/vlc-android-$aar_variant.aar"
if [[ "$aar_variant" == "debug" ]]; then
    other_aar_target="$project_root/Assets/Plugins/Android/vlc-android-release.aar"
else
    other_aar_target="$project_root/Assets/Plugins/Android/vlc-android-debug.aar"
fi

rm -f "$other_aar_target" "$other_aar_target.meta"
cp "$aar_source" "$aar_target"
cmp -s "$aar_source" "$aar_target"
echo "Built and installed $aar_target"
